using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class NetworkManager
{
	private void PollReconnect(double now)
	{
		if (_state != NetworkSessionState.Reconnecting)
		{
			return;
		}

		if (_reconnectAttemptInFlight)
		{
			if (_reconnectAttemptStartedAt < 0.0)
			{
				_reconnectAttemptStartedAt = now;
			}
				if (_config.Transport == NetworkTransport.Lan
					&& _networkMigrationPlan is { } plan
					&& NetworkMigrationRules.IsReadyForMigration(_migrationCoordinatorState)
					&& NetworkMigrationRules.IsPlanVisibleToClients(plan)
				&& now - _reconnectAttemptStartedAt >= NetworkMigrationRules.FollowerConnectionAttemptTimeoutSeconds)
			{
				GD.Print($"MIGRATION_ORIGINAL_HOST_RECONNECT_TIMEOUT elapsed={now - _reconnectAttemptStartedAt:0.00}");
				Multiplayer.MultiplayerPeer?.Close();
				Multiplayer.MultiplayerPeer = null;
				_enetPeer = null;
				_reconnectAttemptInFlight = false;
				_reconnectAttemptStartedAt = -1.0;
				StartMigrationAttemptsAfterHostDisconnect(plan);
			}
			return;
		}

		if (Multiplayer.MultiplayerPeer is { } currentPeer)
		{
			if (currentPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
			{
				return;
			}

			Multiplayer.MultiplayerPeer = null;
			_enetPeer = null;
			_steamPeer = null;
		}

		if (now < _nextReconnectAttemptAt)
		{
			return;
		}

		TryStartReconnectAttempt(now);
	}

	private void InitializeMatchSeatControlStates()
	{
		var previousByPlayerSessionId = _seatControlStates
			.ToDictionary(state => state.PlayerSessionId, StringComparer.Ordinal);
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		ResetClientActionIds();
		foreach (NetworkPlayerPublicState player in _players)
		{
			SeatControlState state;
			if (previousByPlayerSessionId.TryGetValue(player.PlayerSessionId, out SeatControlState previous))
			{
				state = SeatCredentialRules.CancelPendingRotation(previous) with
				{
					SeatIndex = player.SeatIndex,
					Mode = SeatControlMode.HumanConnected,
					PeerId = player.PeerId,
				};
			}
			else
			{
				state = IssueInitialSeatCredential(player);
			}
			InsertSeatControlStateInSeatOrder(state);
			_reconnectClaimsBySeat[player.SeatIndex] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, state);
		}

		EmitSeatControlStatesUpdated();
	}

	private void EnsureLobbySeatControlStates()
	{
		if (_state is not (NetworkSessionState.Hosting or NetworkSessionState.Lobby)
			&& !IsHost)
		{
			return;
		}

		for (int index = _seatControlStates.Count - 1; index >= 0; index--)
		{
			SeatControlState state = _seatControlStates[index];
			bool matchesRoster = _players.Any(player =>
				player.SeatIndex == state.SeatIndex
				&& string.Equals(player.PlayerSessionId, state.PlayerSessionId, StringComparison.Ordinal));
			if (matchesRoster)
			{
				continue;
			}

			_seatControlStates.RemoveAt(index);
			_reconnectClaimsBySeat.Remove(state.SeatIndex);
			_issuedSeatCredentialsBySeat.Remove(state.SeatIndex);
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (TryGetSeatControlState(player.SeatIndex, out _, out _))
			{
				continue;
			}

			SeatControlState state = IssueInitialSeatCredential(player);
			InsertSeatControlStateInSeatOrder(state);
			_reconnectClaimsBySeat[player.SeatIndex] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, state);
		}
	}

	private SeatControlState IssueInitialSeatCredential(NetworkPlayerPublicState player)
	{
		string credential;
		if (player.IsLocal && !string.IsNullOrEmpty(_localSeatCredential))
		{
			credential = _localSeatCredential;
		}
		else
		{
			credential = SeatCredentialRules.IssueCredential();
		}

		_issuedSeatCredentialsBySeat[player.SeatIndex] = credential;
		return SeatControlRules.CreateInitialState(
			player,
			SeatCredentialRules.ComputeVerifier(credential));
	}

	private void InsertSeatControlStateInSeatOrder(SeatControlState state)
	{
		int insertIndex = _seatControlStates.FindIndex(candidate => candidate.SeatIndex > state.SeatIndex);
		if (insertIndex < 0)
		{
			_seatControlStates.Add(state);
			return;
		}

		_seatControlStates.Insert(insertIndex, state);
	}

	private void EnsureSeatCredentialsForRoster()
	{
		if (!IsHost)
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			EnsureSeatCredential(player.SeatIndex);
		}

		SendSeatCredentialsToUnacknowledgedPeers();
	}

	private void EnsureSeatCredential(int seatIndex)
	{
		if (seatIndex < 0 || TryGetSeatControlState(seatIndex, out _, out _))
		{
			return;
		}
		if (TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
		{
			InsertSeatControlStateInSeatOrder(IssueInitialSeatCredential(_players[playerIndex]));
		}
	}

	private void SendSeatCredentialsToUnacknowledgedPeers()
	{
		if (!IsHost || _state is not (NetworkSessionState.Lobby or NetworkSessionState.Starting or NetworkSessionState.LoadingScene or NetworkSessionState.InGame))
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (player.IsHost
				|| !player.IsConnected
				|| player.CredentialsAcknowledged
				|| !_issuedSeatCredentialsBySeat.TryGetValue(player.SeatIndex, out string? credential)
				|| !TryGetSeatControlState(player.SeatIndex, out SeatControlState state, out _))
			{
				continue;
			}

			RpcId(
				player.PeerId,
				MethodName.RpcReceiveSeatCredential,
				_config.SessionId,
				_config.MatchId,
				GetCurrentAuthorityEpoch(),
				player.PlayerSessionId,
				state.CredentialGeneration,
				credential);
		}
	}

	private void PollSeatCredentialDelivery(double now)
	{
		if (!IsHost || _state != NetworkSessionState.Lobby)
		{
			_lastSeatCredentialRetryAt = -1.0;
			return;
		}

		bool hasUnacknowledgedPeer = _players.Any(player =>
			!player.IsHost
			&& player.IsConnected
			&& !player.CredentialsAcknowledged);
		if (!hasUnacknowledgedPeer)
		{
			_lastSeatCredentialRetryAt = -1.0;
			return;
		}
		if (_lastSeatCredentialRetryAt >= 0.0
			&& now - _lastSeatCredentialRetryAt < SeatCredentialRetrySeconds)
		{
			return;
		}

		_lastSeatCredentialRetryAt = now;
		SendSeatCredentialsToUnacknowledgedPeers();
	}

	internal void ApplySeatControlStatesFromAuthoritySnapshot(IReadOnlyList<SeatControlState> states)
	{
		if (IsHost || states.Count == 0)
		{
			return;
		}

		var previousStatesBySeat = _seatControlStates
			.ToDictionary(state => state.SeatIndex);
		_seatControlStates.Clear();
		// The network snapshot decoder writes states into a seat-indexed array and
		// rejects duplicate or out-of-range seat indexes before this method runs.
		foreach (SeatControlState state in states)
		{
			string verifier = previousStatesBySeat.TryGetValue(state.SeatIndex, out SeatControlState previous)
				? previous.CredentialVerifier
				: string.Empty;
			_seatControlStates.Add(state with { CredentialVerifier = verifier });
		}
		EmitSeatControlStatesUpdated();
	}

	private bool TryGetSeatControlState(int seatIndex, out SeatControlState state, out int stateIndex)
	{
		if (seatIndex >= 0
			&& seatIndex < _seatControlStates.Count
			&& _seatControlStates[seatIndex].SeatIndex == seatIndex)
		{
			stateIndex = seatIndex;
			state = _seatControlStates[stateIndex];
			return true;
		}

		stateIndex = _seatControlStates.FindIndex(candidate => candidate.SeatIndex == seatIndex);
		if (stateIndex < 0)
		{
			state = default;
			return false;
		}

		state = _seatControlStates[stateIndex];
		return true;
	}

	private bool TryFindSeatControlStateByPlayerSessionId(string playerSessionId, out SeatControlState state, out int stateIndex)
	{
		stateIndex = _seatControlStates.FindIndex(candidate => string.Equals(candidate.PlayerSessionId, playerSessionId, StringComparison.Ordinal));
		if (stateIndex < 0)
		{
			state = default;
			return false;
		}

		state = _seatControlStates[stateIndex];
		return true;
	}

	private void TryMarkSeatDisconnected(int disconnectedPeerId)
	{
		int playerIndex = _players.FindIndex(player => player.PeerId == disconnectedPeerId);
		if (playerIndex < 0)
		{
			return;
		}

		int seatIndex = _players[playerIndex].SeatIndex;
		if (!TryGetSeatControlState(seatIndex, out SeatControlState current, out int stateIndex))
		{
			return;
		}

		_reconnectClaimsBySeat.TryGetValue(seatIndex, out ReconnectClaimRecord claim);
		if (!SeatControlRules.TryMarkDisconnected(current, disconnectedPeerId, out SeatControlState next, out ReconnectClaimRecord? updatedClaim, claim))
		{
			return;
		}

		_seatControlStates[stateIndex] = next;
		if (next.PendingCredentialGeneration > 0)
		{
			_seatControlStates[stateIndex] = SeatCredentialRules.CancelPendingRotation(next);
			_issuedSeatCredentialsBySeat.Remove(seatIndex);
		}
		if (updatedClaim.HasValue)
		{
			_reconnectClaimsBySeat[seatIndex] = updatedClaim.Value;
		}

		EmitSeatControlStatesUpdated();
	}

	private bool CanAcceptRemoteBoardActionFromSeat(int seatIndex)
	{
		return TryGetSeatControlState(seatIndex, out SeatControlState state, out _)
			&& NetworkSeatControlRules.CanAcceptRemotePlayerAction(state.Mode);
	}

	private void SendReconnectCredentialsToConnectedPeers()
	{
		if (!NetworkReconnectRules.ShouldSendReconnectCredentials(IsHost, _state))
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (player.IsHost || !player.IsConnected)
			{
				continue;
			}

			if (!player.CredentialsAcknowledged)
			{
				SendSeatCredentialsToUnacknowledgedPeers();
				break;
			}
		}
	}

	private bool CanAttemptReconnectClaim()
	{
		return !string.IsNullOrWhiteSpace(_config.MatchId)
			&& !string.IsNullOrWhiteSpace(_localPlayerSessionId)
			&& SeatCredentialRules.IsCredentialEncodingValid(_localSeatCredential)
			&& _localSeatCredentialGeneration >= SeatCredentialRules.InitialGeneration
			&& (!string.IsNullOrWhiteSpace(_lastLanJoinAddress) || _steamLobbyId != 0);
	}

	private void EnterReconnectState()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		_enetPeer = null;
		_steamPeer = null;
		_reconnectAttemptInFlight = false;
		_reconnectAttemptStartedAt = -1.0;
		_nextReconnectAttemptAt = 0.0;
		MarkLocalPlayerConnection(isConnected: false);
		SetState(NetworkSessionState.Reconnecting);
	}

	private void ScheduleReconnectRetry()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		_enetPeer = null;
		_steamPeer = null;
		_reconnectAttemptInFlight = false;
		_reconnectAttemptStartedAt = -1.0;
		_nextReconnectAttemptAt = Time.GetTicksMsec() / 1000.0 + ReconnectRetrySeconds;
	}

	private void TryStartReconnectAttempt(double now)
	{
		if (_config.Transport == NetworkTransport.Lan)
		{
			if (string.IsNullOrWhiteSpace(_lastLanJoinAddress))
			{
				Fail("Reconnect failed: LAN host address is missing.");
				return;
			}

			_enetPeer = new ENetMultiplayerPeer();
			Error error = _enetPeer.CreateClient(_lastLanJoinAddress, NetworkSessionDefaults.LanGamePort);
			if (error != Error.Ok)
			{
				_enetPeer = null;
				_nextReconnectAttemptAt = now + ReconnectRetrySeconds;
				return;
			}

			_reconnectAttemptInFlight = true;
			_reconnectAttemptStartedAt = now;
			Multiplayer.MultiplayerPeer = _enetPeer;
			return;
		}

		if (_config.Transport == NetworkTransport.Steam)
		{
			if (!SteamAvailable || _steamLobbyId == 0)
			{
				Fail("Reconnect failed: Steam lobby is not available.");
				return;
			}

			_reconnectAttemptInFlight = true;
			_reconnectAttemptStartedAt = now;
			StartSteamLobbyJoin(_steamLobbyId);
			return;
		}

		Fail("Reconnect failed: unsupported transport.");
	}

	private void BindReconnectedPeerToSeat(int seatIndex, int senderPeerId)
	{
		if (!TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
		{
			return;
		}

		_players[playerIndex] = _players[playerIndex] with
		{
			PeerId = senderPeerId,
			IsConnected = true,
		};
	}

	private void MarkLocalPlayerConnection(bool isConnected)
	{
		int localSeatIndex = LocalSeatIndex;
		if (!TryGetPlayerIndexBySeatIndex(localSeatIndex, out int playerIndex))
		{
			return;
		}

		_players[playerIndex] = _players[playerIndex] with
		{
			PeerId = isConnected ? GetLocalPeerId() : _players[playerIndex].PeerId,
			IsConnected = isConnected,
			IsLocal = true,
		};
	}

	private void UpdateLocalPlayerAfterReconnect(int seatIndex)
	{
		if (!TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
		{
			return;
		}

		int peerId = GetLocalPeerId();
		_players[playerIndex] = _players[playerIndex] with
		{
			PeerId = peerId == 0 ? _players[playerIndex].PeerId : peerId,
			IsConnected = true,
			IsLocal = true,
		};
	}

	internal bool TryGetSeatControlStateForUi(int seatIndex, out SeatControlState state)
	{
		return TryGetSeatControlState(seatIndex, out state, out _);
	}

	public bool TryPromoteReclaimedSeatAtTurnStart(int seatIndex)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return false;
		}

		if (!TryGetSeatControlState(seatIndex, out SeatControlState current, out int stateIndex)
			|| !SeatControlRules.TryPromoteAtTurnStart(current, seatIndex, out SeatControlState next))
		{
			return false;
		}

		_seatControlStates[stateIndex] = next;
		EmitSeatControlStatesUpdated();
		return true;
	}

	public bool IsSeatAiControlled(int seatIndex)
	{
		if (!TryGetSeatControlState(seatIndex, out SeatControlState state, out _))
		{
			return false;
		}

		return state.Mode != SeatControlMode.HumanConnected;
	}

	public bool DebugSetSeatAiProxyForCoreTurnProbe(int seatIndex)
	{
		if (!IsHost
			|| _state != NetworkSessionState.InGame
			|| !TryGetSeatControlState(seatIndex, out SeatControlState state, out int stateIndex))
		{
			return false;
		}

		_seatControlStates[stateIndex] = state with
		{
			Mode = SeatControlMode.AiProxyReclaimable,
			PeerId = 0,
		};
		EmitSeatControlStatesUpdated();
		return true;
	}

	public void NotifyReconnectSnapshotApplied(string matchId)
	{
		if (_state is not (NetworkSessionState.Reconnecting or NetworkSessionState.MigratingHost)
			|| !string.Equals(_config.MatchId, matchId, StringComparison.Ordinal))
		{
			return;
		}

		_reconnectAttemptInFlight = false;
		_reconnectAttemptStartedAt = -1.0;
		_nextReconnectAttemptAt = 0.0;
		if (_state == NetworkSessionState.MigratingHost && IsMigrationReadinessFenceActive())
		{
			NetworkMigrationPlan migrationPlan = _networkMigrationPlan!;
			_migrationPeerAttemptInFlight = false;
			_migrationPeerAttemptStartedAt = -1.0;
			if (OS.GetEnvironment("CODEX_LAN_SUPPRESS_MIGRATION_SNAPSHOT_ACK") == "1")
			{
				GD.Print($"MIGRATION_SNAPSHOT_ACK_SUPPRESSED epoch={migrationPlan.TargetEpoch} seat={LocalSeatIndex}");
				return;
			}
			RpcId(
				1,
				MethodName.RpcMigrationFollowerSnapshotApplied,
				migrationPlan.SessionId,
				migrationPlan.MatchId,
				migrationPlan.TargetEpoch,
				LocalSeatIndex,
				GetPlayerSessionId());
			GD.Print($"MIGRATION_SNAPSHOT_WAITING_READINESS epoch={_migrationReadyEpoch}");
			return;
		}
		if (_networkMigrationPlan != null)
		{
			_networkMigrationPlan = null;
			_migrationPeerAttemptInFlight = false;
		}
		if (_state == NetworkSessionState.MigratingHost)
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Completed);
		}
		SetState(NetworkSessionState.InGame);
	}
}
