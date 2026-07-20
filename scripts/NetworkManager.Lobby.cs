using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	public void SetLocalReady(bool isReady)
	{
		if (_state != NetworkSessionState.Lobby)
		{
			return;
		}

		if (!IsHost)
		{
			NetworkRpcContext context = GetCurrentRpcContext();
			RpcId(
				1,
				MethodName.RpcSetReady,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				isReady);
			return;
		}

		int localPeerId = GetLocalPeerId();
		SetPlayerReady(localPeerId == 0 ? 1 : localPeerId, isReady);
		BroadcastPlayerList();
		CommitAuthorityCheckpointFromLobby();
	}

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

	public Godot.Collections.Array GetRoomsVariant()
	{
		var result = new Godot.Collections.Array();
		foreach (NetworkRoomInfo room in _rooms)
		{
			result.Add(ToDictionary(room));
		}

		return result;
	}

	public Godot.Collections.Array GetPlayersVariant()
	{
		var result = new Godot.Collections.Array();
		foreach (NetworkPlayerPublicState player in _players)
		{
			result.Add(ToDictionary(player));
		}

		return result;
	}

	public Godot.Collections.Array GetSeatControlStatesVariant(bool includeReconnectTokens = true)
	{
		var result = new Godot.Collections.Array();
		foreach (SeatControlState state in _seatControlStates)
		{
			result.Add(ToDictionary(includeReconnectTokens ? state : SeatControlRules.RedactReconnectTokenForSharedSnapshot(state)));
		}

		return result;
	}

	public Godot.Collections.Dictionary GetCurrentConfigVariant()
	{
		return ToDictionary(_config);
	}

	public void NotifyLocalBoardReady()
	{
		if (_state != NetworkSessionState.LoadingScene && _state != NetworkSessionState.Starting)
		{
			return;
		}

		if (IsHost)
		{
			MarkClientSceneLoaded(_config.MatchId, LocalSeatIndex);
			return;
		}

		NetworkRpcContext context = GetCurrentRpcContext();
		RpcId(
			1,
			MethodName.RpcClientSceneLoaded,
			context.SessionId,
			context.MatchId,
			context.AuthorityEpoch,
			LocalSeatIndex);
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

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestClientInfo()
	{
		RefreshLocalIdentity();
		RpcId(1, MethodName.RpcRegisterPlayer, _localDisplayName, _localAvatarId, GetStableUserId(), _pendingJoinRoomPassword);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRegisterPlayer(string displayName, string avatarId, string stableUserId, string roomPassword)
	{
		if (!IsHost)
		{
			return;
		}

		int senderId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerLobbyRules.CanAcceptJoinRequest(_state))
		{
			RejectLobbyJoin(senderId, "当前房间不可加入");
			return;
		}

		if (RoomPasswordRules.HasPassword(_config.RoomPassword)
			&& !string.Equals(_config.RoomPassword, RoomPasswordRules.Normalize(roomPassword), StringComparison.Ordinal))
		{
			RejectLobbyJoin(senderId, "房间密码错误");
			return;
		}

		if (TryGetPlayerIndexByPeerId(senderId, out _))
		{
			return;
		}

		int seatIndex = MultiplayerLobbyRules.GetNextSeatIndex(_players, _config.PlayerCount);
		if (seatIndex < 0)
		{
			RejectLobbyJoin(senderId, "Lobby is full.");
			return;
		}

		InsertPlayerInSeatOrder(new NetworkPlayerPublicState(
			senderId,
			string.IsNullOrWhiteSpace(stableUserId) ? $"peer:{senderId}" : stableUserId,
			PlayerNameRules.Normalize(displayName),
			CharacterAvatarCatalog.NormalizeAvatarId(avatarId),
			seatIndex,
			false,
			false,
			false,
			true));
		EnsureSessionMigrationToken(seatIndex);
		EnsureLobbySeatControlStates();
		if (_sessionMigrationTokensBySeat.TryGetValue(seatIndex, out string? migrationToken))
		{
			RpcId(
				senderId,
				MethodName.RpcReceiveSessionMigrationToken,
				_config.SessionId,
				_config.MatchId,
				GetCurrentAuthorityEpoch(),
				seatIndex,
				migrationToken,
				string.IsNullOrWhiteSpace(stableUserId) ? $"peer:{senderId}" : stableUserId);
		}
		BroadcastPlayerList();
		CommitAuthorityCheckpointFromLobby();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcLobbyRejected(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string message)
	{
		NetworkRpcContext context = new(sessionId ?? string.Empty, matchId ?? string.Empty, authorityEpoch);
		if (!context.IsValid
			|| _state is not (NetworkSessionState.Joining or NetworkSessionState.MigratingHost)
			|| (_state == NetworkSessionState.MigratingHost
				&& !MatchesCurrentRpcContext(context.SessionId, context.MatchId, context.AuthorityEpoch, allowHigherEpoch: true)))
		{
			return;
		}

		if (_state == NetworkSessionState.MigratingHost)
		{
			SetAuthorityEpoch(context.AuthorityEpoch);
			FailMigration(ParseMigrationFailureCode(message));
			return;
		}

		_lobbyRejectPending = true;
		_lobbyRejectMessage = string.IsNullOrWhiteSpace(message) ? "Join rejected." : message;
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		_enetPeer = null;
		_steamPeer = null;
		CallDeferred(nameof(FinalizeLobbyRejectedJoinIfNeeded));
	}

	private static NetworkMigrationFailureCode ParseMigrationFailureCode(string message)
	{
		return Enum.TryParse(message, ignoreCase: true, out NetworkMigrationFailureCode code)
			? code
			: NetworkMigrationFailureCode.OldHostTokenRejected;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSetReady(
		string sessionId,
		string matchId,
		long authorityEpoch,
		bool isReady)
	{
		if (!IsHost
			|| _state != NetworkSessionState.Lobby
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		SetPlayerReady(Multiplayer.GetRemoteSenderId(), isReady);
		BroadcastPlayerList();
		CommitAuthorityCheckpointFromLobby();
	}

	private void SetPlayerReady(int peerId, bool isReady)
	{
		int playerIndex = _players.FindIndex(player => player.PeerId == peerId);
		if (playerIndex < 0)
		{
			return;
		}

		_players[playerIndex] = _players[playerIndex] with { IsReady = isReady };
		EnsureLobbySeatControlStates();
		EmitSignal(SignalName.ReadyChanged, _players[playerIndex].SeatIndex, isReady);
		EmitPlayerListUpdated();
	}

	private void BroadcastPlayerList()
	{
		if (!IsHost || _state != NetworkSessionState.Lobby)
		{
			return;
		}

		Godot.Collections.Dictionary config = ToDictionary(_config);
		Godot.Collections.Array players = GetPlayersVariant();
		NetworkRpcContext context = GetCurrentRpcContext();
		Rpc(MethodName.RpcSyncLobby, context.SessionId, context.MatchId, context.AuthorityEpoch, config, players);
		RpcSyncLobby(context.SessionId, context.MatchId, context.AuthorityEpoch, config, players);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSyncLobby(
		string sessionId,
		string matchId,
		long authorityEpoch,
		Godot.Collections.Dictionary config,
		Godot.Collections.Array players)
	{
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: true))
		{
			return;
		}

		SetAuthorityEpoch(authorityEpoch);
		_config = ParseSharedConfig(config);
		_players.Clear();
		int localPeerId = GetLocalPeerId();
		foreach (Variant variant in players)
		{
			if (variant.AsGodotDictionary() is not { } playerDictionary)
			{
				continue;
			}

			NetworkPlayerPublicState player = ParsePlayer(playerDictionary);
			InsertPlayerInSeatOrder(player with { IsLocal = player.PeerId == localPeerId || (localPeerId == 0 && player.IsHost) });
		}

		if (_state == NetworkSessionState.MigratingHost)
		{
			bool isLobbyMigration = _networkMigrationPlan?.Phase == NetworkAuthorityPhase.Lobby;
			bool localSeatClaimed = _players.Any(player =>
				player.IsLocal
				&& player.IsConnected
				&& string.Equals(player.StableUserId, GetStableUserId(), StringComparison.Ordinal));
			if (isLobbyMigration && !localSeatClaimed)
			{
				// The new host broadcasts its roster before every follower has
				// claimed a seat. Keep the follower in migration state so a
				// rejection, such as an old-host token, is still actionable.
				EmitPlayerListUpdated();
				return;
			}

			_networkMigrationPlan = null;
			_migrationStartedAt = -1.0;
			_migrationPeerAttemptInFlight = false;
			_migrationPeerAttemptStartedAt = -1.0;
		}
		SetState(NetworkSessionState.Lobby);
		EmitPlayerListUpdated();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBeginSceneLoad(
		string sessionId,
		string matchId,
		long authorityEpoch,
		Godot.Collections.Dictionary bootstrap)
	{
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: true)
			|| !TryReadRpcContext(bootstrap, out NetworkRpcContext payloadContext)
			|| payloadContext != new NetworkRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		_config = ParseSharedConfig(bootstrap);
		SetState(NetworkSessionState.LoadingScene);
		EmitSignal(SignalName.BeginSceneLoad, bootstrap);
		if (GetTree().CurrentScene is GameBoardScene)
		{
			return;
		}

		LoadGameBoardScene(bootstrap);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcClientSceneLoaded(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int seatIndex)
	{
		if (!IsHost
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.CanAcceptSceneLoadAck(_players, senderPeerId, seatIndex, out int senderSeatIndex))
		{
			if (senderSeatIndex < 0)
			{
				GD.PushWarning($"Ignoring scene-load ACK from unknown peer {senderPeerId} for reported seat {seatIndex}.");
				return;
			}

			GD.PushWarning($"Ignoring scene-load ACK from peer {senderPeerId}: reported seat {seatIndex} does not match sender seat {senderSeatIndex}.");
			return;
		}

		MarkClientSceneLoaded(matchId, seatIndex);
	}

	private void MarkClientSceneLoaded(string matchId, int seatIndex)
	{
		if (_loadHandshake == null)
		{
			return;
		}

		if (!TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
		{
			GD.PushWarning($"Ignoring scene-load ACK for unknown seat {seatIndex}.");
			return;
		}

		NetworkPlayerPublicState player = _players[playerIndex];
		if (!player.IsConnected)
		{
			GD.PushWarning($"Ignoring scene-load ACK for offline seat {seatIndex}.");
			return;
		}

		if (!string.Equals(_loadHandshake.MatchId, matchId, StringComparison.Ordinal))
		{
			GD.PushWarning($"Ignoring stale scene-load ACK for seat {seatIndex}: expected match {_loadHandshake.MatchId}, got {matchId}.");
			return;
		}

		if (!_loadHandshake.ExpectedSeats.Contains(seatIndex))
		{
			GD.PushWarning($"Ignoring expired scene-load ACK for seat {seatIndex}.");
			return;
		}

		if (!_loadHandshake.MarkLoaded(matchId, seatIndex))
		{
			return;
		}

		if (_loadHandshake.IsComplete)
		{
			Godot.Collections.Dictionary boardInit = BuildBoardInitializePayload();
			NetworkRpcContext context = GetCurrentRpcContext();
			Rpc(
				MethodName.RpcInitializeBoard,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				boardInit);
			RpcInitializeBoard(
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				boardInit);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcInitializeBoard(
		string sessionId,
		string matchId,
		long authorityEpoch,
		Godot.Collections.Dictionary payload)
	{
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: true)
			|| !TryReadRpcContext(payload, out NetworkRpcContext payloadContext)
			|| payloadContext != new NetworkRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		SetState(NetworkSessionState.InGame);
		EmitSignal(SignalName.BoardInitialize, payload);
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.Set("local_player_index", LocalSeatIndex);
			gameBoard.ApplyNetworkBoardInitialize(payload);
		}
	}

	private void LoadGameBoardScene(Godot.Collections.Dictionary bootstrap)
	{
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		PackedScene? gameBoardScene = GD.Load<PackedScene>(GameBoardScenePath);
		if (gameBoardScene == null)
		{
			Fail($"Missing game board scene: {GameBoardScenePath}");
			return;
		}

		Node gameBoard = gameBoardScene.Instantiate();
		gameBoard.Set("player_count", _config.PlayerCount);
		gameBoard.Set("item_mode", _config.ItemMode);
		gameBoard.Set("winning_score", _config.WinningScore);
		gameBoard.Set("solo_mode", false);
		gameBoard.Set("local_player_index", LocalSeatIndex);
		gameBoard.Set("network_session_id", _config.SessionId);
		gameBoard.Set("network_match_id", _config.MatchId);
		gameBoard.Set("match_seed", _config.MatchSeed);
		gameBoard.Set("player_names_csv", BuildPlayerNamesCsv());
		gameBoard.Set("player_avatar_ids_csv", BuildPlayerAvatarIdsCsv());
		gameBoard.Set("network_wait_for_initialize", true);
		Node? oldScene = GetTree().CurrentScene;
		GetTree().Root.AddChild(gameBoard);
		GetTree().CurrentScene = gameBoard;
		oldScene?.QueueFree();
	}

	private Godot.Collections.Dictionary BuildMatchBootstrap()
	{
		_loadHandshake = new SceneLoadHandshakeTracker(
			_config.MatchId,
			_players.Select(player => player.SeatIndex),
			NetworkSessionDefaults.SceneLoadTimeoutSeconds,
			Time.GetTicksMsec() / 1000.0);
		Godot.Collections.Dictionary payload = ToDictionary(_config);
		StampRpcContext(payload, GetCurrentRpcContext());
		return payload;
	}

	private Godot.Collections.Dictionary BuildBoardInitializePayload()
	{
		Godot.Collections.Dictionary payload = ToDictionary(_config);
		StampRpcContext(payload, GetCurrentRpcContext());
		payload["player_names_csv"] = BuildPlayerNamesCsv();
		payload["player_avatar_ids_csv"] = BuildPlayerAvatarIdsCsv();
		payload["network_wait_for_initialize"] = false;
		return payload;
	}

	private static void StampRpcContext(
		Godot.Collections.Dictionary payload,
		NetworkRpcContext context)
	{
		payload["protocol_version"] = NetworkProtocol.CurrentVersion;
		payload["session_id"] = context.SessionId;
		payload["match_id"] = context.MatchId;
		payload["authority_epoch"] = context.AuthorityEpoch;
	}

	private string BuildPlayerNamesCsv()
	{
		return string.Join(",", _players.Select(player => EscapeCsvValue(player.DisplayName)));
	}

	private string BuildPlayerAvatarIdsCsv()
	{
		return string.Join(",", _players.Select(player => EscapeCsvValue(player.AvatarId)));
	}

	private static string EscapeCsvValue(string value)
	{
		return value.Replace(",", " ");
	}

	private void PollLoadHandshake(double now)
	{
		if (!IsHost || _state != NetworkSessionState.LoadingScene || _loadHandshake == null)
		{
			return;
		}

		if (_loadHandshake.HasTimedOut(now))
		{
			CancelMatchStart("Scene load timed out.");
		}
	}

	private void CancelMatchStart(string message)
	{
		long returnGeneration = GetGenerationAfterStateTransition(NetworkSessionState.Lobby);
		_loadHandshake = null;
		SetState(NetworkSessionState.Lobby);
		NetworkRpcContext context = GetCurrentRpcContext();
		Rpc(
			MethodName.RpcCancelMatchStart,
			context.SessionId,
			context.MatchId,
			context.AuthorityEpoch,
			message);
		ApplyMatchStartCancellation(message, returnGeneration);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcCancelMatchStart(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string message)
	{
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: true))
		{
			return;
		}

		long returnGeneration = GetGenerationAfterStateTransition(NetworkSessionState.Lobby);
		ApplyMatchStartCancellation(message, returnGeneration);
	}

	private void ApplyMatchStartCancellation(string message, long returnGeneration)
	{
		_loadHandshake = null;
		SetState(NetworkSessionState.Lobby);
		EmitSignal(SignalName.ErrorRaised, message);
		CallDeferred(
			nameof(ReturnToMultiplayerLobbyScene),
			(int)NetworkSessionState.Lobby,
			returnGeneration);
	}

	private long GetGenerationAfterStateTransition(NetworkSessionState state)
	{
		return _state == state ? _stateGeneration : _stateGeneration + 1;
	}

	private void ReturnToMultiplayerLobbyScene(int expectedState, long expectedGeneration)
	{
		if (!GodotObject.IsInstanceValid(this)
			|| !IsInsideTree()
			|| _state != (NetworkSessionState)expectedState
			|| _stateGeneration != expectedGeneration)
		{
			return;
		}

		SceneTree tree = GetTree();
		if (tree.CurrentScene is MultiplayerLobbyScene)
		{
			_migrationFailureReturnPending = false;
			return;
		}

		const string lobbyScenePath =
			"res://scenes/multiplayer_lobby_scene.tscn";
		Error changeSceneError = tree.ChangeSceneToFile(lobbyScenePath);
		if (changeSceneError != Error.Ok)
		{
			GD.PushError($"Failed to return to multiplayer lobby: {changeSceneError}");
			return;
		}

		_migrationFailureReturnPending = false;
	}
}
