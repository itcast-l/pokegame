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

		MultiplayerPeer? currentPeer = Multiplayer.MultiplayerPeer;
		bool peerBlocksAttempt = currentPeer != null
			&& currentPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected;
		NetworkMigrationPlan? reconnectMigrationPlan = _networkMigrationPlan;
		bool enableAttemptTimeout = HostMigrationCapabilityAvailable
			&& _config.Transport == NetworkTransport.Lan
			&& reconnectMigrationPlan is not null
			&& NetworkMigrationRules.IsReadyForMigration(_migrationCoordinatorState)
			&& NetworkMigrationRules.IsPlanVisibleToClients(reconnectMigrationPlan);
		NetworkReconnectPollAction action = _reconnectCoordinator.Poll(
			now,
			peerBlocksAttempt,
			enableAttemptTimeout,
			NetworkMigrationRules.FollowerConnectionAttemptTimeoutSeconds);
		switch (action)
		{
			case NetworkReconnectPollAction.WindowExpired:
				HandleHostDisconnected();
				return;
			case NetworkReconnectPollAction.AttemptTimedOut:
				double elapsed = now - _reconnectCoordinator.AttemptStartedAtSeconds;
				GD.Print($"MIGRATION_ORIGINAL_HOST_RECONNECT_TIMEOUT elapsed={elapsed:0.00}");
				Multiplayer.MultiplayerPeer?.Close();
				Multiplayer.MultiplayerPeer = null;
				_enetPeer = null;
				_reconnectCoordinator.Reset();
				StartMigrationAttemptsAfterHostDisconnect(reconnectMigrationPlan!);
				return;
			case NetworkReconnectPollAction.StartAttempt:
				if (currentPeer != null)
				{
					Multiplayer.MultiplayerPeer = null;
					_enetPeer = null;
					_steamPeer = null;
				}
				TryStartReconnectAttempt(now);
				return;
			case NetworkReconnectPollAction.Inactive:
			case NetworkReconnectPollAction.Waiting:
			default:
				return;
		}
	}

	private void InitializeMatchSeatControlStates()
	{
		var previousByPlayerSessionId = _lobbyRoster.SeatControls
			.ToDictionary(state => state.PlayerSessionId, StringComparer.Ordinal);
		_lobbyRoster.ClearSeatControls();
		_lobbyRoster.ClearReconnectClaims();
		ResetClientActionIds();
		foreach (NetworkPlayerPublicState player in _lobbyRoster.Players)
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
			_lobbyRoster.SetReconnectClaim(
				player.SeatIndex,
				SeatControlRules.CreateInitialClaimRecord(_config.MatchId, state));
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

		for (int index = _lobbyRoster.SeatControls.Count - 1; index >= 0; index--)
		{
			SeatControlState state = _lobbyRoster.SeatControls[index];
			bool matchesRoster = _lobbyRoster.Players.Any(player =>
				player.SeatIndex == state.SeatIndex
				&& string.Equals(player.PlayerSessionId, state.PlayerSessionId, StringComparison.Ordinal));
			if (matchesRoster)
			{
				continue;
			}

			_lobbyRoster.RemoveSeatControlAt(index);
			_lobbyRoster.RemoveReconnectClaim(state.SeatIndex);
			_lobbyRoster.RemoveIssuedSeatCredential(state.SeatIndex);
		}

		foreach (NetworkPlayerPublicState player in _lobbyRoster.Players)
		{
			if (TryGetSeatControlState(player.SeatIndex, out _, out _))
			{
				continue;
			}

			SeatControlState state = IssueInitialSeatCredential(player);
			InsertSeatControlStateInSeatOrder(state);
			_lobbyRoster.SetReconnectClaim(
				player.SeatIndex,
				SeatControlRules.CreateInitialClaimRecord(_config.MatchId, state));
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

		_lobbyRoster.SetIssuedSeatCredential(player.SeatIndex, credential);
		return SeatControlRules.CreateInitialState(
			player,
			SeatCredentialRules.ComputeVerifier(credential));
	}

	private void InsertSeatControlStateInSeatOrder(SeatControlState state)
	{
		_lobbyRoster.InsertSeatControlInSeatOrder(state);
	}

	private void EnsureSeatCredentialsForRoster()
	{
		if (!IsHost)
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _lobbyRoster.Players)
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
			InsertSeatControlStateInSeatOrder(IssueInitialSeatCredential(_lobbyRoster.Players[playerIndex]));
		}
	}

	private void SendSeatCredentialsToUnacknowledgedPeers()
	{
		if (!IsHost || _state is not (NetworkSessionState.Lobby or NetworkSessionState.Starting or NetworkSessionState.LoadingScene or NetworkSessionState.InGame))
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _lobbyRoster.Players)
		{
			if (player.IsHost
				|| !player.IsConnected
				|| player.CredentialsAcknowledged
				|| !_lobbyRoster.IssuedSeatCredentialsBySeat.TryGetValue(player.SeatIndex, out string? credential)
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

		bool hasUnacknowledgedPeer = _lobbyRoster.Players.Any(player =>
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

		var previousStatesBySeat = _lobbyRoster.SeatControls
			.ToDictionary(state => state.SeatIndex);
		var mergedStates = new List<SeatControlState>(states.Count);
		// The network snapshot decoder writes states into a seat-indexed array and
		// rejects duplicate or out-of-range seat indexes before this method runs.
		foreach (SeatControlState state in states)
		{
			string verifier = previousStatesBySeat.TryGetValue(state.SeatIndex, out SeatControlState previous)
				? previous.CredentialVerifier
				: string.Empty;
			mergedStates.Add(state with { CredentialVerifier = verifier });
		}
		_lobbyRoster.ReplaceSeatControls(mergedStates);
		EmitSeatControlStatesUpdated();
	}

	private bool TryGetSeatControlState(int seatIndex, out SeatControlState state, out int stateIndex)
	{
		if (seatIndex >= 0
			&& seatIndex < _lobbyRoster.SeatControls.Count
			&& _lobbyRoster.SeatControls[seatIndex].SeatIndex == seatIndex)
		{
			stateIndex = seatIndex;
			state = _lobbyRoster.SeatControls[stateIndex];
			return true;
		}

		stateIndex = _lobbyRoster.FindSeatControlIndex(candidate => candidate.SeatIndex == seatIndex);
		if (stateIndex < 0)
		{
			state = default;
			return false;
		}

		state = _lobbyRoster.SeatControls[stateIndex];
		return true;
	}

	private bool TryFindSeatControlStateByPlayerSessionId(string playerSessionId, out SeatControlState state, out int stateIndex)
	{
		stateIndex = _lobbyRoster.FindSeatControlIndex(candidate => string.Equals(candidate.PlayerSessionId, playerSessionId, StringComparison.Ordinal));
		if (stateIndex < 0)
		{
			state = default;
			return false;
		}

		state = _lobbyRoster.SeatControls[stateIndex];
		return true;
	}

	private void TryMarkSeatDisconnected(int disconnectedPeerId)
	{
		int playerIndex = _lobbyRoster.FindPlayerIndex(player => player.PeerId == disconnectedPeerId);
		if (playerIndex < 0)
		{
			return;
		}

		int seatIndex = _lobbyRoster.Players[playerIndex].SeatIndex;
		if (!TryGetSeatControlState(seatIndex, out SeatControlState current, out int stateIndex))
		{
			return;
		}

		_lobbyRoster.ReconnectClaimsBySeat.TryGetValue(seatIndex, out ReconnectClaimRecord claim);
		if (!SeatControlRules.TryMarkDisconnected(current, disconnectedPeerId, out SeatControlState next, out ReconnectClaimRecord? updatedClaim, claim))
		{
			return;
		}

		_lobbyRoster.SetSeatControlAt(stateIndex, next);
		if (next.PendingCredentialGeneration > 0)
		{
			_lobbyRoster.SetSeatControlAt(stateIndex, SeatCredentialRules.CancelPendingRotation(next));
			_lobbyRoster.RemoveIssuedSeatCredential(seatIndex);
		}
		if (updatedClaim.HasValue)
		{
			_lobbyRoster.SetReconnectClaim(seatIndex, updatedClaim.Value);
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

		foreach (NetworkPlayerPublicState player in _lobbyRoster.Players)
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
		double now = Time.GetTicksMsec() / 1000.0;
		if (!_reconnectCoordinator.Begin(GetReconnectContext(), now))
		{
			return;
		}
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		_enetPeer = null;
		_steamPeer = null;
		MarkLocalPlayerConnection(isConnected: false);
		SetState(NetworkSessionState.Reconnecting);
	}

	private void ScheduleReconnectRetry()
	{
		ScheduleReconnectRetry(Time.GetTicksMsec() / 1000.0);
	}

	private void ScheduleReconnectRetry(double now)
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		_enetPeer = null;
		_steamPeer = null;
		NetworkReconnectTransitionResult result = _reconnectCoordinator.ScheduleRetry(
			GetReconnectContext(),
			now);
		if (result == NetworkReconnectTransitionResult.Expired)
		{
			HandleHostDisconnected();
		}
	}

	private void HandleHostDisconnected()
	{
		if (IsHost
			|| _hostDisconnectReturnPending
			|| _state is NetworkSessionState.Idle
				or NetworkSessionState.Leaving
				or NetworkSessionState.Failed)
		{
			return;
		}

		string localizationKey = NetworkUserErrorRules.GetLocalizationKey(NetworkUserErrorCategory.HostDisconnected);
		_hostDisconnectReturnPending = true;
		bool publishError = !_hostDisconnectErrorPublished;
		_hostDisconnectErrorPublished = true;
		_ = RequestSessionCloseAsync(NetworkSessionCloseReason.InternalFailure);
		bool needsLobbySceneChange = IsInsideTree()
			&& GetTree().CurrentScene is not MultiplayerLobbyScene;
		_pendingLobbyErrorLocalizationKey = needsLobbySceneChange ? localizationKey : string.Empty;
		SetState(NetworkSessionState.Failed);
		if (publishError)
		{
			EmitSignal(SignalName.ErrorRaised, localizationKey);
		}
		CallDeferred(
			nameof(ReturnToMultiplayerLobbyScene),
			(int)NetworkSessionState.Failed,
			_stateGeneration);
	}

	private void TryStartReconnectAttempt(double now)
	{
		if (_config.Transport == NetworkTransport.Lan)
		{
			if (string.IsNullOrWhiteSpace(_lastLanJoinAddress))
			{
				_reconnectCoordinator.Reset();
				HandleHostDisconnected();
				return;
			}

			_enetPeer = new ENetMultiplayerPeer();
			Error error = _enetPeer.CreateClient(_lastLanJoinAddress, NetworkSessionDefaults.LanGamePort);
			if (error != Error.Ok)
			{
				_enetPeer = null;
				ScheduleReconnectRetry(now);
				return;
			}

			Multiplayer.MultiplayerPeer = _enetPeer;
			return;
		}

		if (_config.Transport == NetworkTransport.Steam)
		{
			if (!SteamAvailable || _steamLobbyId == 0)
			{
				_reconnectCoordinator.Reset();
				HandleHostDisconnected();
				return;
			}

			StartSteamLobbyJoin(_steamLobbyId);
			return;
		}

		_reconnectCoordinator.Reset();
		HandleHostDisconnected();
	}

	private NetworkReconnectContext GetReconnectContext()
	{
		return new NetworkReconnectContext(
			_config.SessionId,
			_config.MatchId,
			GetCurrentAuthorityEpoch());
	}

	private void BindReconnectedPeerToSeat(int seatIndex, int senderPeerId)
	{
		if (!TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
		{
			return;
		}

		_lobbyRoster.SetPlayerAt(
			playerIndex,
			_lobbyRoster.Players[playerIndex] with
			{
				PeerId = senderPeerId,
				IsConnected = true,
			});
	}

	private void MarkLocalPlayerConnection(bool isConnected)
	{
		int localSeatIndex = LocalSeatIndex;
		if (!TryGetPlayerIndexBySeatIndex(localSeatIndex, out int playerIndex))
		{
			return;
		}

		_lobbyRoster.SetPlayerAt(
			playerIndex,
			_lobbyRoster.Players[playerIndex] with
			{
				PeerId = isConnected ? GetLocalPeerId() : _lobbyRoster.Players[playerIndex].PeerId,
				IsConnected = isConnected,
				IsLocal = true,
			});
	}

	private void UpdateLocalPlayerAfterReconnect(int seatIndex)
	{
		if (!TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
		{
			return;
		}

		int peerId = GetLocalPeerId();
		_lobbyRoster.SetPlayerAt(
			playerIndex,
			_lobbyRoster.Players[playerIndex] with
			{
				PeerId = peerId == 0 ? _lobbyRoster.Players[playerIndex].PeerId : peerId,
				IsConnected = true,
				IsLocal = true,
			});
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

		_lobbyRoster.SetSeatControlAt(stateIndex, next);
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

#if DECKLAND_TEST_HOOKS
	public bool DebugSetSeatAiProxyForCoreTurnProbe(int seatIndex)
	{
		if (!IsHost
			|| _state != NetworkSessionState.InGame
			|| !TryGetSeatControlState(seatIndex, out SeatControlState state, out int stateIndex))
		{
			return false;
		}

		_lobbyRoster.SetSeatControlAt(
			stateIndex,
			state with
			{
				Mode = SeatControlMode.AiProxyReclaimable,
				PeerId = 0,
			});
		EmitSeatControlStatesUpdated();
		return true;
	}
#endif

	public void NotifyReconnectSnapshotApplied(string matchId)
	{
		if ((_state != NetworkSessionState.Reconnecting
				&& (!HostMigrationCapabilityAvailable || _state != NetworkSessionState.MigratingHost))
			|| !string.Equals(_config.MatchId, matchId, StringComparison.Ordinal))
		{
			return;
		}

		if (_state == NetworkSessionState.Reconnecting)
		{
			NetworkReconnectTransitionResult completion = _reconnectCoordinator.CompleteSnapshot(
				GetReconnectContext(),
				Time.GetTicksMsec() / 1000.0);
			if (completion != NetworkReconnectTransitionResult.Accepted)
			{
				return;
			}
		}
		else
		{
			_reconnectCoordinator.Reset();
		}
		if (HostMigrationCapabilityAvailable
			&& _state == NetworkSessionState.MigratingHost
			&& IsMigrationReadinessFenceActive())
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
		if (HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost)
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Completed);
		}
		SetState(NetworkSessionState.InGame);
	}
}
