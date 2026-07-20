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

		_config = _config with
		{
			MatchId = Guid.NewGuid().ToString("N"),
			MatchSeed = ResolveMatchSeedForStart(),
		};
		SetAuthorityEpoch(_authorityEpoch);
		EnsureSessionMigrationTokensForRoster();
		InitializeMatchSeatControlStates();
		SetState(NetworkSessionState.Starting);
		SendReconnectCredentialsToConnectedPeers();
		SendSessionMigrationTokensToConnectedPeers();
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
		bool hasWinner = GetTree().CurrentScene is GameBoardScene gameBoard
			&& gameBoard.IsNetworkGameOver();
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

		TryReturnToLobbyAfterMatch(
			new NetworkRpcContext(sessionId ?? string.Empty, matchId ?? string.Empty, authorityEpoch),
			Multiplayer.GetRemoteSenderId());
	}

	private bool TryReturnToLobbyAfterMatch(NetworkRpcContext completedMatchContext, int requesterPeerId)
	{
		bool hasWinner = GetTree().CurrentScene is GameBoardScene gameBoard
			&& gameBoard.IsNetworkGameOver();
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

		NetworkSessionConfig lobbyConfig = ParseSharedConfig(config);
		var connectedPlayers = new List<NetworkPlayerPublicState>();
		int localPeerId = GetLocalPeerId();
		foreach (Variant variant in players)
		{
			if (variant.AsGodotDictionary() is not { } playerDictionary)
			{
				return;
			}

			NetworkPlayerPublicState player = ParsePlayer(playerDictionary);
			connectedPlayers.Add(player with
			{
				IsLocal = player.PeerId == localPeerId || (localPeerId == 0 && player.IsHost),
			});
		}
		bool validReturn = MultiplayerRematchRules.IsValidAuthorityReturn(_config, lobbyConfig, connectedPlayers);
		if (!validReturn)
		{
			return;
		}

		long returnGeneration = GetGenerationAfterStateTransition(NetworkSessionState.Lobby);
		_config = lobbyConfig;
		_players.Clear();
		foreach (NetworkPlayerPublicState player in connectedPlayers)
		{
			InsertPlayerInSeatOrder(player);
		}

		ResetCompletedMatchState();
		if (IsHost)
		{
			foreach (NetworkPlayerPublicState player in _players)
			{
				EnsureSessionMigrationToken(player.SeatIndex);
			}
			EnsureLobbySeatControlStates();
		}
		SetState(NetworkSessionState.Lobby);
		if (IsHost)
		{
			SendSessionMigrationTokensToConnectedPeers();
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
		_loadHandshake = null;
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		_localReconnectToken = string.Empty;
		_nextReconnectAttemptAt = 0.0;
		_reconnectAttemptInFlight = false;
		ResetClientActionIds();

		_latestAuthorityCheckpoint = Array.Empty<byte>();
		_latestAuthorityCheckpointHash = string.Empty;
		_latestAuthorityCheckpointId = 0;
		_networkMigrationPlan = null;
		_standbyCheckpoint = null;
		_standbyCheckpointPayload = Array.Empty<byte>();
		_standbyCheckpointHash = string.Empty;
		_standbyCheckpointsById.Clear();
		_hotStandbySeat = -1;
		_standbyCheckpointAcked = false;
		_standbyAckedCheckpointId = -1;
		_standbyPendingCheckpointId = -1;
		_standbyCheckpointSentAt = -1.0;
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
		_migrationRecoveryReadyForPeers = false;
		_migrationReadinessSeatIndices.Clear();

		_sessionMigrationTokensBySeat.Clear();
		_localSessionMigrationToken = string.Empty;
		_migrationFailureReturnPending = false;
		_pendingLobbyErrorMessage = string.Empty;
		_hostEndedMatchPending = false;
		_hostEndedMatchMessage = string.Empty;
		_hostEndedMatchBroadcasted = false;
	}

	private static long ResolveMatchSeedForStart()
	{
		string configuredSeed = OS.GetEnvironment("CODEX_LAN_MATCH_SEED").Trim();
		return long.TryParse(configuredSeed, out long seed) && seed != 0
			? seed
			: (long)GD.Randi();
	}

	public void LeaveSession()
	{
		if (NetworkReconnectRules.ShouldBroadcastHostEndedMatch(IsHost, _state) && !_hostEndedMatchBroadcasted)
		{
			NetworkRpcContext context = GetCurrentRpcContext();
			Rpc(
				MethodName.RpcHostEndedMatch,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				"Host disconnected.");
			_hostEndedMatchBroadcasted = true;
		}

		StopLanDiscoveryResponder();
		StopLanDiscoveryBrowser();
		_loadHandshake = null;
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		_sessionMigrationTokensBySeat.Clear();
		_localReconnectToken = string.Empty;
		_localSessionMigrationToken = string.Empty;
		_lastLanJoinAddress = string.Empty;
		_nextReconnectAttemptAt = 0.0;
		_reconnectAttemptInFlight = false;
		ResetClientActionIds();
		_latestAuthorityCheckpoint = Array.Empty<byte>();
		_latestAuthorityCheckpointHash = string.Empty;
		_latestAuthorityCheckpointId = 0;
		_networkMigrationPlan = null;
		_standbyCheckpoint = null;
		_standbyCheckpointPayload = Array.Empty<byte>();
		_standbyCheckpointHash = string.Empty;
		_standbyCheckpointsById.Clear();
		_hotStandbySeat = -1;
		_standbyCheckpointAcked = false;
		_standbyAckedCheckpointId = -1;
		_standbyPendingCheckpointId = -1;
		_standbyCheckpointSentAt = -1.0;
		_consecutiveUnackedCheckpoints = 0;
		_failedHotStandbySeats.Clear();
		_migrationReadyEpoch = -1;
		_migrationRecoveryReadyForPeers = false;
		_migrationReadinessSeatIndices.Clear();
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

	public async Task NotifyHostEndedMatchIfNeededAsync()
	{
		if (!NetworkReconnectRules.ShouldBroadcastHostEndedMatch(IsHost, _state)
			|| _hostEndedMatchBroadcasted
			|| !IsInsideTree())
		{
			return;
		}

		_hostEndedMatchBroadcasted = true;
		NetworkRpcContext context = GetCurrentRpcContext();
		Rpc(
			MethodName.RpcHostEndedMatch,
			context.SessionId,
			context.MatchId,
			context.AuthorityEpoch,
			"Host disconnected.");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!IsInsideTree())
		{
			return;
		}

		await ToSignal(GetTree().CreateTimer(HostEndedMatchBroadcastGraceSeconds), SceneTreeTimer.SignalName.Timeout);
	}

	public async Task LeaveSessionAfterHostEndedMatchAsync()
	{
		await NotifyHostEndedMatchIfNeededAsync();
		LeaveSession();
	}

	public void LeaveSessionAfterHostEndedMatch()
	{
		_ = LeaveSessionAfterHostEndedMatchAsync();
	}


}
