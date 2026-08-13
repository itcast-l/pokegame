using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcClaimMigratedLobbySeat(
		string sessionId,
		long authorityEpoch,
		string playerSessionId,
		long credentialGeneration,
		string credential)
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| _state != NetworkSessionState.Lobby
			|| _networkMigrationPlan is not { Phase: NetworkAuthorityPhase.Lobby } migrationPlan
			|| !MatchesCurrentRpcContext(sessionId, string.Empty, authorityEpoch)
			|| authorityEpoch != migrationPlan.TargetEpoch)
		{
			return;
		}
		if (!AllowPeerRequest(NetworkRequestCategory.Recovery, requireRegisteredPeer: false))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (string.Equals(playerSessionId, migrationPlan.SourcePlayerSessionId, StringComparison.Ordinal))
		{
			NetworkRpcContext context = GetCurrentRpcContext();
			RpcId(
				senderPeerId,
				MethodName.RpcLobbyRejected,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				NetworkMigrationFailureCode.ExpiredCredential.ToString());
			return;
		}

		int playerIndex = _players.FindIndex(player =>
			string.Equals(player.PlayerSessionId, playerSessionId, StringComparison.Ordinal));
		if (playerIndex < 0
			|| !TryGetSeatControlState(_players[playerIndex].SeatIndex, out SeatControlState control, out int controlIndex)
			|| credentialGeneration != control.CredentialGeneration
			|| !SeatCredentialRules.Verify(credential, control.CredentialVerifier)
			|| (control.PendingCredentialGeneration > 0 && control.PendingCredentialPeerId != senderPeerId))
		{
			NetworkRpcContext context = GetCurrentRpcContext();
			RpcId(
				senderPeerId,
				MethodName.RpcLobbyRejected,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				NetworkMigrationFailureCode.ExpiredCredential.ToString());
			return;
		}

		NetworkPlayerPublicState player = _players[playerIndex];
		_players[playerIndex] = player with
		{
			PeerId = senderPeerId,
			IsHost = false,
			IsLocal = false,
			IsConnected = true,
		};
		SeatControlState boundControl = control with
		{
			Mode = SeatControlMode.HumanConnected,
			PeerId = senderPeerId,
		};
		string credentialToSend = string.Empty;
		if (control.PendingCredentialGeneration == 0)
		{
			if (!SeatCredentialRules.TryBeginRotation(
				boundControl,
				senderPeerId,
				out boundControl,
				out string nextCredential))
			{
				return;
			}
			_issuedSeatCredentialsBySeat[player.SeatIndex] = nextCredential;
			credentialToSend = nextCredential;
		}
		else if (_issuedSeatCredentialsBySeat.TryGetValue(player.SeatIndex, out string? pendingCredential))
		{
			credentialToSend = pendingCredential;
		}
		_seatControlStates[controlIndex] = boundControl;
		if (!string.IsNullOrEmpty(credentialToSend))
		{
			SendSeatCredentialToPeer(senderPeerId, boundControl, credentialToSend);
		}

		BroadcastPlayerList();
		CommitAuthorityCheckpointFromLobby();
	}


	private void MarkCandidateAsNewLobbyHost(int candidateSeat, string sourcePlayerSessionId)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		var oldControlsByPlayerSessionId = _seatControlStates
			.ToDictionary(state => state.PlayerSessionId, StringComparer.Ordinal);
		List<NetworkPlayerPublicState> remainingPlayers = _players
			.Where(player => !string.Equals(player.PlayerSessionId, sourcePlayerSessionId, StringComparison.Ordinal))
			.OrderBy(player => player.SeatIndex)
			.ToList();
		if (!remainingPlayers.Any(player => player.SeatIndex == candidateSeat))
		{
			FailMigration(NetworkMigrationFailureCode.NotCandidate);
			return;
		}

		_players.Clear();
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		_issuedSeatCredentialsBySeat.Clear();

		for (int newSeat = 0; newSeat < remainingPlayers.Count; newSeat++)
		{
			NetworkPlayerPublicState oldPlayer = remainingPlayers[newSeat];
			bool isLocalCandidate = string.Equals(oldPlayer.PlayerSessionId, GetPlayerSessionId(), StringComparison.Ordinal);
			NetworkPlayerPublicState player = oldPlayer with
			{
				PeerId = isLocalCandidate ? 1 : 0,
				SeatIndex = newSeat,
				IsHost = isLocalCandidate,
				IsLocal = isLocalCandidate,
				IsConnected = isLocalCandidate,
				CredentialsAcknowledged = isLocalCandidate || oldPlayer.CredentialsAcknowledged,
			};
			_players.Add(player);

			if (!oldControlsByPlayerSessionId.TryGetValue(oldPlayer.PlayerSessionId, out SeatControlState oldControl))
			{
				string replacementCredential = SeatCredentialRules.IssueCredential();
				oldControl = SeatControlRules.CreateInitialState(
					oldPlayer,
					SeatCredentialRules.ComputeVerifier(replacementCredential));
				_issuedSeatCredentialsBySeat[newSeat] = replacementCredential;
			}

			SeatControlState control = oldControl with
			{
				SeatIndex = newSeat,
				Mode = isLocalCandidate ? SeatControlMode.HumanConnected : SeatControlMode.HumanReclaimPending,
				PeerId = isLocalCandidate ? 1 : 0,
			};
			_seatControlStates.Add(control);
			_reconnectClaimsBySeat[newSeat] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, control);
		}

		EmitPlayerListUpdated();
		EmitSeatControlStatesUpdated();
	}

	private void BeginLobbyMigrationRecovery(long targetEpoch, double now)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		_lobbyMigrationRecoveryState = new LobbyMigrationRecoveryState(
			targetEpoch,
			now,
			NetworkMigrationRules.LobbyRecoveryTimeoutSeconds,
			_players
				.Where(player => !player.IsLocal)
				.Select(player => player.PlayerSessionId));
	}

	private void PollLobbyMigrationRecovery(double now)
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| _state != NetworkSessionState.Lobby
			|| _lobbyMigrationRecoveryState is not { } recovery)
		{
			return;
		}

		if (recovery.IsComplete)
		{
			TryFinalizeLobbyMigrationRecovery(now);
			return;
		}
		if (!recovery.HasExpired(now))
		{
			return;
		}

		CompactLobbyAfterMigrationTimeout(recovery);
		BroadcastPlayerList();
		EmitSeatControlStatesUpdated();
		CommitAuthorityCheckpointFromLobby();
		TryFinalizeLobbyMigrationRecovery(now, force: true);
	}

	private void TryFinalizeLobbyMigrationRecovery(double now, bool force = false)
	{
		if (!HostMigrationCapabilityAvailable
			|| _lobbyMigrationRecoveryState is not { } recovery
			|| (!force && !recovery.IsComplete))
		{
			return;
		}

		_lobbyMigrationRecoveryState = null;
		ClearLocalStandbyCheckpointCache();
		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Completed);
		ReleaseHotStandbyCandidate(rekeyFormerCandidate: false, invalidateAvailability: false);
		_failedHotStandbySeats.Clear();
		CommitAuthorityCheckpointFromLobby();
		SendCheckpointToHotStandbyIfNeeded(now);
	}


	private void CompactLobbyAfterMigrationTimeout(LobbyMigrationRecoveryState recovery)
	{
		List<NetworkPlayerPublicState> keptPlayers = _players
			.Where(player => player.IsLocal
				|| !recovery.ExpectedPlayerSessionIds.Contains(player.PlayerSessionId)
				|| recovery.IsClaimed(player.PlayerSessionId))
			.OrderBy(player => player.SeatIndex)
			.ToList();
		CompactLobbySeatMappings(keptPlayers);
	}

	private void CompactLobbySeatMappings(IReadOnlyList<NetworkPlayerPublicState> keptPlayers)
	{
		var controlsByPlayerSessionId = _seatControlStates.ToDictionary(
			state => state.PlayerSessionId,
			StringComparer.Ordinal);
		var claimsByPlayerSessionId = _reconnectClaimsBySeat.Values.ToDictionary(
			claim => claim.PlayerSessionId,
			StringComparer.Ordinal);
		var oldSeatByPlayerSessionId = _players.ToDictionary(
			player => player.PlayerSessionId,
			player => player.SeatIndex,
			StringComparer.Ordinal);
		var compactPlayers = new List<NetworkPlayerPublicState>(keptPlayers.Count);
		var compactControls = new List<SeatControlState>(keptPlayers.Count);
		var compactClaims = new Dictionary<int, ReconnectClaimRecord>();
		var compactIssuedCredentials = new Dictionary<int, string>();
		for (int newSeat = 0; newSeat < keptPlayers.Count; newSeat++)
		{
			NetworkPlayerPublicState player = keptPlayers[newSeat] with { SeatIndex = newSeat };
			SeatControlState control = controlsByPlayerSessionId[player.PlayerSessionId] with { SeatIndex = newSeat };
			ReconnectClaimRecord claim = claimsByPlayerSessionId.TryGetValue(player.PlayerSessionId, out ReconnectClaimRecord existingClaim)
				? existingClaim with { SeatIndex = newSeat }
				: SeatControlRules.CreateInitialClaimRecord(string.Empty, control);
			compactPlayers.Add(player);
			compactControls.Add(control);
			compactClaims[newSeat] = claim;
			if (oldSeatByPlayerSessionId.TryGetValue(player.PlayerSessionId, out int oldSeat)
				&& _issuedSeatCredentialsBySeat.TryGetValue(oldSeat, out string? credential))
			{
				compactIssuedCredentials[newSeat] = credential;
			}
		}

		_players.Clear();
		_players.AddRange(compactPlayers);
		_seatControlStates.Clear();
		_seatControlStates.AddRange(compactControls);
		_reconnectClaimsBySeat.Clear();
		foreach (KeyValuePair<int, ReconnectClaimRecord> pair in compactClaims)
		{
			_reconnectClaimsBySeat[pair.Key] = pair.Value;
		}
		_issuedSeatCredentialsBySeat.Clear();
		foreach (KeyValuePair<int, string> pair in compactIssuedCredentials)
		{
			_issuedSeatCredentialsBySeat[pair.Key] = pair.Value;
		}
	}

}
