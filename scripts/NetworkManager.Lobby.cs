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
			RpcId(1, MethodName.RpcSetReady, isReady);
			return;
		}

		int localPeerId = GetLocalPeerId();
		SetPlayerReady(localPeerId == 0 ? 1 : localPeerId, isReady);
		BroadcastPlayerList();
	}

	public void StartMatch()
	{
		if (!IsHost || _state != NetworkSessionState.Lobby)
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
		InitializeMatchSeatControlStates();
		SetState(NetworkSessionState.Starting);
		SendReconnectCredentialsToConnectedPeers();
		Godot.Collections.Dictionary bootstrap = BuildMatchBootstrap();
		Rpc(MethodName.RpcBeginSceneLoad, bootstrap);
		RpcBeginSceneLoad(bootstrap);
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
			Rpc(MethodName.RpcHostEndedMatch, "Host disconnected.");
			_hostEndedMatchBroadcasted = true;
		}

		StopLanDiscoveryResponder();
		StopLanDiscoveryBrowser();
		_loadHandshake = null;
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		_localReconnectToken = string.Empty;
		_lastLanJoinAddress = string.Empty;
		_nextReconnectAttemptAt = 0.0;
		_reconnectAttemptInFlight = false;
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

		RpcId(1, MethodName.RpcClientSceneLoaded, _config.MatchId, LocalSeatIndex);
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
		Rpc(MethodName.RpcHostEndedMatch, "Host disconnected.");
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
		BroadcastPlayerList();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcLobbyRejected(string message)
	{
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

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSetReady(bool isReady)
	{
		if (!IsHost || _state != NetworkSessionState.Lobby)
		{
			return;
		}

		SetPlayerReady(Multiplayer.GetRemoteSenderId(), isReady);
		BroadcastPlayerList();
	}

	private void SetPlayerReady(int peerId, bool isReady)
	{
		int playerIndex = _players.FindIndex(player => player.PeerId == peerId);
		if (playerIndex < 0)
		{
			return;
		}

		_players[playerIndex] = _players[playerIndex] with { IsReady = isReady };
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
		Rpc(MethodName.RpcSyncLobby, config, players);
		RpcSyncLobby(config, players);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSyncLobby(Godot.Collections.Dictionary config, Godot.Collections.Array players)
	{
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

		SetState(NetworkSessionState.Lobby);
		EmitPlayerListUpdated();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBeginSceneLoad(Godot.Collections.Dictionary bootstrap)
	{
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
	private void RpcClientSceneLoaded(string matchId, int seatIndex)
	{
		if (!IsHost)
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
			Rpc(MethodName.RpcInitializeBoard, boardInit);
			RpcInitializeBoard(boardInit);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcInitializeBoard(Godot.Collections.Dictionary payload)
	{
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
		return ToDictionary(_config);
	}

	private Godot.Collections.Dictionary BuildBoardInitializePayload()
	{
		Godot.Collections.Dictionary payload = ToDictionary(_config);
		payload["player_names_csv"] = BuildPlayerNamesCsv();
		payload["player_avatar_ids_csv"] = BuildPlayerAvatarIdsCsv();
		payload["network_wait_for_initialize"] = false;
		return payload;
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
		Rpc(MethodName.RpcCancelMatchStart, message);
		ApplyMatchStartCancellation(message, returnGeneration);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcCancelMatchStart(string message)
	{
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
			return;
		}

		const string lobbyScenePath =
			"res://scenes/multiplayer_lobby_scene.tscn";
		Error changeSceneError = tree.ChangeSceneToFile(lobbyScenePath);
		if (changeSceneError != Error.Ok)
		{
			Fail($"Failed to return to multiplayer lobby: {changeSceneError}");
		}
	}
}
