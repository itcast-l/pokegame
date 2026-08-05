using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	public void StartMatch()
	{
		if (!IsHost || _state != NetworkSessionState.Lobby)
		{
			return;
		}
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		if (!MultiplayerLobbyRules.CanStart(_players, _config.PlayerCount))
		{
			EmitSignal(SignalName.ErrorRaised, "MULTIPLAYER_LOBBY_START_REQUIRE_READY");
			return;
		}

		_pendingLobbyReadyByPeerId.Clear();
		ClearPendingLocalReadyIntent();
		ResetMigrationArtifactsForNewMatch();
		_config = _config with
		{
			MatchId = Guid.NewGuid().ToString("N"),
			MatchSeed = ResolveMatchSeedForStart(),
		};
		SetAuthorityEpoch(_authorityEpoch);
		EnsureSeatCredentialsForRoster();
		InitializeMatchSeatControlStates();
		SetState(NetworkSessionState.Starting);
		SendReconnectCredentialsToConnectedPeers();
		SendSeatCredentialsToUnacknowledgedPeers();
		Godot.Collections.Dictionary bootstrap = BuildMatchBootstrap();
		NetworkRpcContext context = GetCurrentRpcContext();
		Rpc(
			MethodName.RpcBeginSceneLoad,
			context.SessionId,
			context.MatchId,
			context.AuthorityEpoch,
			bootstrap);
		RpcBeginSceneLoad(
			context.SessionId,
			context.MatchId,
			context.AuthorityEpoch,
			bootstrap);
	}

	public bool RequestReturnToLobbyAfterMatch()
	{
		NetworkRpcContext context = GetCurrentRpcContext();
		bool hasWinner = TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoard)
			&& gameBoard.IsGameOver;
		if (!MultiplayerRematchRules.CanIssueReturnRequest(_state, context, hasWinner))
		{
			return false;
		}

		int requesterPeerId = GetLocalPeerId();
		if (IsHost)
		{
			return TryReturnToLobbyAfterMatch(context, requesterPeerId);
		}

		MultiplayerPeer? peer = Multiplayer.MultiplayerPeer;
		if (peer == null || peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
		{
			return false;
		}

		RpcId(
			1,
			MethodName.RpcRequestReturnToLobbyAfterMatch,
			context.SessionId,
			context.MatchId,
			context.AuthorityEpoch);
		return true;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestReturnToLobbyAfterMatch(
		string sessionId,
		string matchId,
		long authorityEpoch)
	{
		if (!IsHost)
		{
			return;
		}
		if (!AllowPeerRequest(NetworkRequestCategory.LobbyMutation))
		{
			return;
		}

		TryReturnToLobbyAfterMatch(
			new NetworkRpcContext(sessionId ?? string.Empty, matchId ?? string.Empty, authorityEpoch),
			Multiplayer.GetRemoteSenderId());
	}

	private bool TryReturnToLobbyAfterMatch(NetworkRpcContext completedMatchContext, int requesterPeerId)
	{
		bool hasWinner = TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoard)
			&& gameBoard.IsGameOver;
		bool canAccept = MultiplayerRematchRules.CanHostAcceptReturnRequest(
			_state,
			completedMatchContext,
			_config,
			GetCurrentAuthorityEpoch(),
			_players,
			requesterPeerId,
			hasWinner);
		if (!IsHost
			|| !canAccept)
		{
			return false;
		}

		NetworkSessionConfig lobbyConfig = MultiplayerRematchRules.CreateReturnedLobbyConfig(_config);
		List<NetworkPlayerPublicState> connectedPlayers = MultiplayerRematchRules.RetainConnectedPlayers(_players);
		var players = new Godot.Collections.Array();
		foreach (NetworkPlayerPublicState player in connectedPlayers)
		{
			players.Add(ToDictionary(player));
		}
		Godot.Collections.Dictionary config = ToDictionary(lobbyConfig);

		Rpc(
			MethodName.RpcReturnToLobbyAfterMatch,
			completedMatchContext.SessionId,
			completedMatchContext.MatchId,
			completedMatchContext.AuthorityEpoch,
			config,
			players);
		ApplyReturnToLobbyAfterMatch(completedMatchContext, config, players);
		return true;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReturnToLobbyAfterMatch(
		string completedSessionId,
		string completedMatchId,
		long completedAuthorityEpoch,
		Godot.Collections.Dictionary config,
		Godot.Collections.Array players)
	{
		ApplyReturnToLobbyAfterMatch(
			new NetworkRpcContext(
				completedSessionId ?? string.Empty,
				completedMatchId ?? string.Empty,
				completedAuthorityEpoch),
			config,
			players);
	}

	private void ApplyReturnToLobbyAfterMatch(
		NetworkRpcContext completedMatchContext,
		Godot.Collections.Dictionary config,
		Godot.Collections.Array players)
	{
		if (!MatchesCurrentRpcContext(
			completedMatchContext.SessionId,
			completedMatchContext.MatchId,
			completedMatchContext.AuthorityEpoch))
		{
			return;
		}

		int localPeerId = GetLocalPeerId();
		if (!TryDecodeLobbyState(config, players, localPeerId, out NetworkSessionConfig lobbyConfig, out List<NetworkPlayerPublicState> connectedPlayers))
		{
			return;
		}
		bool validReturn = MultiplayerRematchRules.IsValidAuthorityReturn(_config, lobbyConfig, connectedPlayers);
		if (!validReturn)
		{
			return;
		}

		long returnGeneration = GetGenerationAfterStateTransition(NetworkSessionState.Lobby);
		_config = lobbyConfig;
		_players.Clear();
		_players.AddRange(connectedPlayers);

		ResetCompletedMatchState();
		if (IsHost)
		{
			foreach (NetworkPlayerPublicState player in _players)
			{
				EnsureSeatCredential(player.SeatIndex);
			}
			EnsureLobbySeatControlStates();
		}
		SetState(NetworkSessionState.Lobby);
		if (IsHost)
		{
			SendSeatCredentialsToUnacknowledgedPeers();
			CommitAuthorityCheckpointFromLobby();
		}
		EmitPlayerListUpdated();
		EmitSeatControlStatesUpdated();
		CallDeferred(
			nameof(ReturnToMultiplayerLobbyScene),
			(int)NetworkSessionState.Lobby,
			returnGeneration);
	}

	private void ResetCompletedMatchState()
	{
		ClearMatchEndpointRegistration();
		_loadHandshake = null;
		_pendingLobbyReadyByPeerId.Clear();
		ClearPendingLocalReadyIntent();
		var previousSeatControlsByPlayerSessionId = _seatControlStates
			.GroupBy(state => state.PlayerSessionId, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		foreach (NetworkPlayerPublicState player in _players.OrderBy(player => player.SeatIndex))
		{
			SeatControlState state;
			if (previousSeatControlsByPlayerSessionId.TryGetValue(player.PlayerSessionId, out SeatControlState previous))
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
				state = IsHost
					? IssueInitialSeatCredential(player)
					: SeatControlRules.CreateInitialState(player, string.Empty);
			}

			_seatControlStates.Add(state);
			_reconnectClaimsBySeat[state.SeatIndex] = SeatControlRules.CreateInitialClaimRecord(string.Empty, state);
		}
		_nextReconnectAttemptAt = 0.0;
		_reconnectAttemptInFlight = false;
		_reconnectAttemptStartedAt = -1.0;
		_reconnectWindowStartedAt = -1.0;
		ResetClientActionIds();

		ReplaceLatestAuthorityCheckpointEnvelope(AuthorityCheckpointEnvelope.Empty);
		_authorityCommitSequence = 0;
		_authorityCheckpointCommitFailureCount = 0;
		_authorityCheckpointCommitFailureReported = false;
		_networkMigrationPlan = null;
		ClearPendingMigrationPlan();
		ClearLocalStandbyCheckpointCache();
		_issuedStandbyCheckpointNonces.Reset();
		_hotStandbySeat = -1;
		_standbyAckedCheckpointId = -1;
		_migrationEligibleCheckpointId = -1;
		_standbyPendingCheckpointId = -1;
		_standbyPendingCheckpointHash = string.Empty;
		_standbyPendingEncryptedCheckpoint = null;
		_standbyCheckpointSentAt = -1.0;
		_lastStandbyCheckpointSentAt = -1.0;
		_lastStandbyCheckpointAckedAt = -1.0;
		_consecutiveUnackedCheckpoints = 0;
		_failedHotStandbySeats.Clear();
		_migrationStartedAt = -1.0;
		_nextMigrationAttemptAt = 0.0;
		_migrationServerStarted = false;
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
		_standbyCandidateHostAddress = string.Empty;
		_activeMigrationPending = false;
		_activeMigrationAwaitingSourceClose = false;
		_activeMigrationDeadline = -1.0;
		_activeMigrationConfirmedSeats.Clear();
		_migrationReadyEpoch = -1;
		_lastCompletedMigrationReadyEpoch = -1;
		_migrationRecoveryReadyForPeers = false;
		_gameMigrationRecoveryState = null;
		_lobbyMigrationRecoveryState = null;
		_migrationCoordinatorState = NetworkMigrationCoordinatorState.Disabled;
		_lastObservedMigrationPlanRevision = 0;
		_pendingMigrationPlanRevocationRevision = -1;
		ResetMigrationTransportState();
		_migrationLatencyMillisecondsByPeerId.Clear();
		_pendingMigrationLatencyProbesByPeerId.Clear();
		_lastMigrationLatencyProbeAt = -1.0;
		_nextMigrationLatencyProbeSequence = 0;
		_lastObservedSteamLobbyOwnerId = 0;

		_issuedSeatCredentialsBySeat.Clear();
		_peerRateLimiter.Clear();
		_lastCorrectiveSnapshotAtByPeer.Clear();
		_migrationFailureReturnPending = false;
		_pendingLobbyErrorMessage = string.Empty;
		_hostEndedMatchPending = false;
		_hostEndedMatchMessage = string.Empty;
		_hostEndedMatchBroadcasted = false;
		_hostDisconnectReturnPending = false;
		_hostDisconnectErrorPublished = false;
		ResetMigrationSecurityAfterCompletedMatch();
	}

	private static long ResolveMatchSeedForStart()
	{
		string configuredSeed = OS.GetEnvironment("CODEX_LAN_MATCH_SEED").Trim();
		return long.TryParse(configuredSeed, out long seed) && seed != 0
			? seed
			: (long)GD.Randi();
	}

	private void PerformImmediateSessionCleanup(NetworkSessionCloseReason reason)
	{
		_ = reason;
		ClearMatchEndpointRegistration();
		StopLanDiscoveryResponder();
		StopLanDiscoveryBrowser();
		_loadHandshake = null;
		_pendingLobbyReadyByPeerId.Clear();
		ClearPendingLocalReadyIntent();
		_lastSeatCredentialRetryAt = -1.0;
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		_issuedSeatCredentialsBySeat.Clear();
		_localPlayerSessionId = string.Empty;
		_peerRateLimiter.Clear();
		_lastCorrectiveSnapshotAtByPeer.Clear();
		_localSeatCredential = string.Empty;
		_localSeatCredentialGeneration = 0;
		_pendingLocalSeatCredential = string.Empty;
		_pendingLocalSeatCredentialGeneration = 0;
		DisposeMigrationSecurity();
		_lastLanJoinAddress = string.Empty;
		_nextReconnectAttemptAt = 0.0;
		_reconnectAttemptInFlight = false;
		_reconnectAttemptStartedAt = -1.0;
		_reconnectWindowStartedAt = -1.0;
		ResetClientActionIds();
		ReplaceLatestAuthorityCheckpointEnvelope(AuthorityCheckpointEnvelope.Empty);
		_authorityCommitSequence = 0;
		_authorityCheckpointCommitFailureCount = 0;
		_authorityCheckpointCommitFailureReported = false;
		_networkMigrationPlan = null;
		ClearPendingMigrationPlan();
		ClearLocalStandbyCheckpointCache();
		_issuedStandbyCheckpointNonces.Reset();
		_hotStandbySeat = -1;
		_standbyAckedCheckpointId = -1;
		_migrationEligibleCheckpointId = -1;
		_standbyPendingCheckpointId = -1;
		_standbyPendingCheckpointHash = string.Empty;
		_standbyPendingEncryptedCheckpoint = null;
		_standbyCheckpointSentAt = -1.0;
		_lastStandbyCheckpointSentAt = -1.0;
		_lastStandbyCheckpointAckedAt = -1.0;
		_consecutiveUnackedCheckpoints = 0;
		_failedHotStandbySeats.Clear();
		_migrationReadyEpoch = -1;
		_lastCompletedMigrationReadyEpoch = -1;
		_migrationRecoveryReadyForPeers = false;
		_gameMigrationRecoveryState = null;
		_lobbyMigrationRecoveryState = null;
		_migrationCoordinatorState = NetworkMigrationCoordinatorState.Disabled;
		_lastObservedMigrationPlanRevision = 0;
		_pendingMigrationPlanRevocationRevision = -1;
		ResetMigrationTransportState();
		_migrationLatencyMillisecondsByPeerId.Clear();
		_pendingMigrationLatencyProbesByPeerId.Clear();
		_lastMigrationLatencyProbeAt = -1.0;
		_nextMigrationLatencyProbeSequence = 0;
		_lastObservedSteamLobbyOwnerId = 0;
		_authorityEpoch = 0;
		_migrationStartedAt = -1.0;
		_migrationServerStarted = false;
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
		_activeMigrationPending = false;
		_activeMigrationAwaitingSourceClose = false;
		_activeMigrationDeadline = -1.0;
		_activeMigrationConfirmedSeats.Clear();
		_migrationFailureReturnPending = false;
		_pendingLobbyErrorMessage = string.Empty;
		_lobbyRejectPending = false;
		_lobbyRejectMessage = string.Empty;
		_hostEndedMatchPending = false;
		_hostEndedMatchMessage = string.Empty;
		_hostEndedMatchBroadcasted = false;
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		LeaveSteamLobbyIfNeeded();
		_enetPeer = null;
		_steamPeer = null;
		_pendingJoinRoomPassword = string.Empty;
		_steamLobbyId = 0;
		_pendingSteamLobbyJoinId = 0;
		_acceptedSteamLobbyJoinId = 0;
		_originalSteamLobbyOwnerId = 0;
		_pendingSteamHostLobby = false;
		_players.Clear();
		_rooms.Clear();
		_roomLastSeenAt.Clear();
		_config = CreateConfig(
			NetworkTransport.Lan,
			NetworkSessionDefaults.DefaultPlayerCount,
			NetworkSessionDefaults.DefaultItemMode,
			NetworkSessionDefaults.DefaultWinningScore,
			RoomNameRules.DefaultRoomName,
			string.Empty);
		SetState(NetworkSessionState.Idle);
		EmitPlayerListUpdated();
		EmitRoomListUpdated();
	}

	private bool _isSessionCloseInProgress;
	private long _sessionCloseGeneration;
	private Task? _sessionCloseTask;
	private NetworkSessionCloseReason _activeSessionCloseReason;
	private long _sessionCloseExecutionCount;
	private long _sessionLeaveCompletionCount;
	private long _hostEndedMatchCloseBroadcastCount;

	public Task RequestSessionCloseAsync(NetworkSessionCloseReason reason)
	{
		if (_isSessionCloseInProgress && _sessionCloseTask != null)
		{
			return _sessionCloseTask;
		}

		bool shouldBroadcast = NetworkReconnectRules.ShouldBroadcastHostEndedMatch(IsHost, _state)
			&& !_hostEndedMatchBroadcasted;
		NetworkSessionClosePolicy policy = NetworkSessionClosePolicy.Resolve(reason, shouldBroadcast);
		long generation = ++_sessionCloseGeneration;
		var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		_sessionCloseTask = completion.Task;
		_isSessionCloseInProgress = true;
		_activeSessionCloseReason = reason;
		_sessionCloseExecutionCount++;
		SetState(NetworkSessionState.Leaving);
		_ = ExecuteSessionCloseAsync(reason, policy, generation, completion);
		return _sessionCloseTask;
	}

	public void LeaveSession()
	{
		_ = RequestSessionCloseAsync(IsHost
			? NetworkSessionCloseReason.UserRequested
			: NetworkSessionCloseReason.ClientRequested);
	}

	private void CloseSessionAndFail(NetworkSessionCloseReason reason, string message)
	{
		_ = RequestSessionCloseAsync(reason);
		Fail(message);
	}

	private async Task ExecuteSessionCloseAsync(
		NetworkSessionCloseReason reason,
		NetworkSessionClosePolicy policy,
		long generation,
		TaskCompletionSource<bool> completion)
	{
		try
		{
			if (policy.BroadcastHostEndedMatch && !_hostEndedMatchBroadcasted && Multiplayer.MultiplayerPeer != null)
			{
				NetworkRpcContext context = GetCurrentRpcContext();
				Rpc(
					MethodName.RpcHostEndedMatch,
					context.SessionId,
					context.MatchId,
					context.AuthorityEpoch,
					"Host disconnected.");
				_hostEndedMatchBroadcasted = true;
				_hostEndedMatchCloseBroadcastCount++;
			}

			if (policy.WaitOneProcessFrame && IsInsideTree())
			{
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			if (policy.ReliablePacketGraceSeconds > 0.0 && IsInsideTree())
			{
				await ToSignal(
					GetTree().CreateTimer(policy.ReliablePacketGraceSeconds, processAlways: true),
					SceneTreeTimer.SignalName.Timeout);
			}

			if (generation == _sessionCloseGeneration)
			{
				PerformImmediateSessionCleanup(reason);
				_sessionLeaveCompletionCount++;
				EmitSignal(SignalName.SessionLeaveCompleted, (int)reason);
			}
		}
		catch (Exception error)
		{
			GD.PushWarning($"Session close failed over to immediate cleanup: {error.GetType().Name}");
			if (generation == _sessionCloseGeneration)
			{
				PerformImmediateSessionCleanup(NetworkSessionCloseReason.InternalFailure);
				_sessionLeaveCompletionCount++;
				EmitSignal(SignalName.SessionLeaveCompleted, (int)NetworkSessionCloseReason.InternalFailure);
			}
		}
		finally
		{
			if (generation == _sessionCloseGeneration)
			{
				_isSessionCloseInProgress = false;
			}
			completion.TrySetResult(true);
		}
	}

	private void AbortSessionCloseAndCleanup(NetworkSessionCloseReason reason)
	{
		_sessionCloseGeneration++;
		_isSessionCloseInProgress = false;
		PerformImmediateSessionCleanup(reason);
	}

	private bool PrepareForNewSessionStart()
	{
		if (_state == NetworkSessionState.Leaving || _isSessionCloseInProgress)
		{
			return false;
		}
		if (_state is NetworkSessionState.Idle)
		{
			return true;
		}
		if (_state is NetworkSessionState.Searching or NetworkSessionState.Failed)
		{
			AbortSessionCloseAndCleanup(NetworkSessionCloseReason.SessionReplacement);
			return true;
		}

		_ = RequestSessionCloseAsync(NetworkSessionCloseReason.SessionReplacement);
		return false;
	}

	public NetworkSessionCloseReason DebugGetActiveSessionCloseReason() => _activeSessionCloseReason;
	public bool DebugIsSessionCloseInProgress() => _isSessionCloseInProgress;
	public long DebugGetSessionCloseExecutionCount() => _sessionCloseExecutionCount;
	public long DebugGetSessionLeaveCompletionCount() => _sessionLeaveCompletionCount;
	public long DebugGetHostEndedMatchCloseBroadcastCount() => _hostEndedMatchCloseBroadcastCount;

	public async Task LeaveSessionAfterHostEndedMatchAsync()
	{
		await RequestSessionCloseAsync(NetworkSessionCloseReason.UserRequested);
	}

	public void LeaveSessionAfterHostEndedMatch()
	{
		_ = LeaveSessionAfterHostEndedMatchAsync();
	}


}
