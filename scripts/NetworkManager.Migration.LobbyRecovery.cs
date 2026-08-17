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

		int playerIndex = _lobbyRoster.FindPlayerIndex(player =>
			string.Equals(player.PlayerSessionId, playerSessionId, StringComparison.Ordinal));
		if (playerIndex < 0
			|| !TryGetSeatControlState(_lobbyRoster.Players[playerIndex].SeatIndex, out SeatControlState control, out int controlIndex)
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

		NetworkPlayerPublicState player = _lobbyRoster.Players[playerIndex];
		_lobbyRoster.SetPlayerAt(
			playerIndex,
			player with
			{
				PeerId = senderPeerId,
				IsHost = false,
				IsLocal = false,
				IsConnected = true,
			});
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
			_lobbyRoster.SetIssuedSeatCredential(player.SeatIndex, nextCredential);
			credentialToSend = nextCredential;
		}
		else if (_lobbyRoster.IssuedSeatCredentialsBySeat.TryGetValue(player.SeatIndex, out string? pendingCredential))
		{
			credentialToSend = pendingCredential;
		}
		_lobbyRoster.SetSeatControlAt(controlIndex, boundControl);
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

		var oldControlsByPlayerSessionId = _lobbyRoster.SeatControls
			.ToDictionary(state => state.PlayerSessionId, StringComparer.Ordinal);
		List<NetworkPlayerPublicState> remainingPlayers = _lobbyRoster.Players
			.Where(player => !string.Equals(player.PlayerSessionId, sourcePlayerSessionId, StringComparison.Ordinal))
			.OrderBy(player => player.SeatIndex)
			.ToList();
		if (!remainingPlayers.Any(player => player.SeatIndex == candidateSeat))
		{
			FailMigration(NetworkMigrationFailureCode.NotCandidate);
			return;
		}

		var migratedPlayers = new List<NetworkPlayerPublicState>(remainingPlayers.Count);
		var migratedControls = new List<SeatControlState>(remainingPlayers.Count);
		var migratedClaims = new Dictionary<int, ReconnectClaimRecord>();
		var migratedCredentials = new Dictionary<int, string>();

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
			migratedPlayers.Add(player);

			if (!oldControlsByPlayerSessionId.TryGetValue(oldPlayer.PlayerSessionId, out SeatControlState oldControl))
			{
				string replacementCredential = SeatCredentialRules.IssueCredential();
				oldControl = SeatControlRules.CreateInitialState(
					oldPlayer,
					SeatCredentialRules.ComputeVerifier(replacementCredential));
				migratedCredentials[newSeat] = replacementCredential;
			}

			SeatControlState control = oldControl with
			{
				SeatIndex = newSeat,
				Mode = isLocalCandidate ? SeatControlMode.HumanConnected : SeatControlMode.HumanReclaimPending,
				PeerId = isLocalCandidate ? 1 : 0,
			};
			migratedControls.Add(control);
			migratedClaims[newSeat] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, control);
		}
		_lobbyRoster.ReplaceRosterState(
			migratedPlayers,
			migratedControls,
			migratedClaims,
			migratedCredentials);

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
			_lobbyRoster.Players
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
		List<NetworkPlayerPublicState> keptPlayers = _lobbyRoster.Players
			.Where(player => player.IsLocal
				|| !recovery.ExpectedPlayerSessionIds.Contains(player.PlayerSessionId)
				|| recovery.IsClaimed(player.PlayerSessionId))
			.OrderBy(player => player.SeatIndex)
			.ToList();
		_lobbyRoster.CompactSeatMappings(keptPlayers);
	}

}
