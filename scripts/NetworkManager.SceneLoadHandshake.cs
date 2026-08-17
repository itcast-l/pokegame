using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
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

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBeginSceneLoad(
		string sessionId,
		string matchId,
		long authorityEpoch,
		Godot.Collections.Dictionary bootstrap)
	{
		if (sessionId is null || matchId is null)
		{
			return;
		}
		bool isInitialSceneTransition = _state is NetworkSessionState.Lobby or NetworkSessionState.Starting
			&& string.Equals(_config.SessionId, sessionId, StringComparison.Ordinal)
			&& string.IsNullOrEmpty(_config.MatchId)
			&& !string.IsNullOrWhiteSpace(matchId)
			&& authorityEpoch == GetCurrentAuthorityEpoch();
		if (!isInitialSceneTransition
			&& !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: true))
		{
			return;
		}
		if (!TryReadRpcContext(bootstrap, out NetworkRpcContext payloadContext)
			|| payloadContext != new NetworkRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}
		if (!TryParseSharedConfig(bootstrap, out NetworkSessionConfig decodedConfig)
			|| !string.Equals(decodedConfig.SessionId, sessionId, StringComparison.Ordinal)
			|| !string.Equals(decodedConfig.MatchId, matchId, StringComparison.Ordinal))
		{
			return;
		}

		if (isInitialSceneTransition)
		{
			ResetMigrationArtifactsForNewMatch();
		}
		_config = decodedConfig;
		SetState(NetworkSessionState.LoadingScene);
		EmitSignal(SignalName.BeginSceneLoad, bootstrap);
		if (TryGetActiveEndpointRegistration(out _))
		{
			return;
		}

		LoadGameBoardScene();
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
		if (!AllowPeerRequest(NetworkRequestCategory.ProtocolAck))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.CanAcceptSceneLoadAck(_lobbyRoster.Players, senderPeerId, seatIndex, out int senderSeatIndex))
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
		NetworkSceneLoadSeatAvailability seatAvailability = NetworkSceneLoadSeatAvailability.Unknown;
		if (TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
		{
			seatAvailability = _lobbyRoster.Players[playerIndex].IsConnected
				? NetworkSceneLoadSeatAvailability.Connected
				: NetworkSceneLoadSeatAvailability.Offline;
		}

		NetworkSceneLoadAckResult result = _sceneLoadCoordinator.Acknowledge(
			matchId,
			seatIndex,
			seatAvailability);
		switch (result)
		{
			case NetworkSceneLoadAckResult.UnknownSeat:
				GD.PushWarning($"Ignoring scene-load ACK for unknown seat {seatIndex}.");
				return;
			case NetworkSceneLoadAckResult.OfflineSeat:
				GD.PushWarning($"Ignoring scene-load ACK for offline seat {seatIndex}.");
				return;
			case NetworkSceneLoadAckResult.StaleMatch:
				GD.PushWarning($"Ignoring stale scene-load ACK for seat {seatIndex}: expected match {_sceneLoadCoordinator.MatchId}, got {matchId}.");
				return;
			case NetworkSceneLoadAckResult.UnexpectedSeat:
				GD.PushWarning($"Ignoring expired scene-load ACK for seat {seatIndex}.");
				return;
			case NetworkSceneLoadAckResult.Completed:
				BroadcastBoardInitialization();
				return;
			case NetworkSceneLoadAckResult.Inactive:
			case NetworkSceneLoadAckResult.Duplicate:
			case NetworkSceneLoadAckResult.Accepted:
			default:
				return;
		}
	}

	private void BroadcastBoardInitialization()
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

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcInitializeBoard(
		string sessionId,
		string matchId,
		long authorityEpoch,
		Godot.Collections.Dictionary payload)
	{
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: true)
			|| !TryReadRpcContext(payload, out NetworkRpcContext payloadContext)
			|| payloadContext != new NetworkRpcContext(sessionId, matchId, authorityEpoch)
			|| !TryParseSharedConfig(payload, out NetworkSessionConfig decodedConfig)
			|| decodedConfig != _config
			|| !TryDecodeBoardInitialization(payload, decodedConfig, authorityEpoch, out NetworkBoardInitialization initialization))
		{
			return;
		}

		if (!TryGetReplicaEndpoint(out INetworkReplicaEndpoint replica))
		{
			RetainPendingBoardInitialization(initialization);
			return;
		}
		if (!replica.TryInitialize(initialization))
		{
			return;
		}

		CompleteBoardInitialization(initialization);
	}

	private bool TryDecodeBoardInitialization(
		Godot.Collections.Dictionary payload,
		NetworkSessionConfig config,
		long authorityEpoch,
		out NetworkBoardInitialization initialization)
	{
		initialization = default!;
		if (!NetworkVariantReader.TryReadString(payload, "player_names_csv", 1024, out string playerNames)
			|| !NetworkVariantReader.TryReadString(payload, "player_avatar_ids_csv", 1024, out string avatarIds)
			|| !NetworkVariantReader.TryReadBool(payload, "network_wait_for_initialize", out bool waitForInitialize)
			|| waitForInitialize)
		{
			return false;
		}

		string[] decodedPlayerNames = playerNames.Split(',');
		string[] decodedAvatarIds = avatarIds.Split(',');
		if (decodedPlayerNames.Length != config.PlayerCount
			|| decodedAvatarIds.Length != config.PlayerCount
			|| LocalSeatIndex < 0
			|| LocalSeatIndex >= config.PlayerCount)
		{
			return false;
		}

		initialization = new NetworkBoardInitialization(
			config,
			authorityEpoch,
			LocalSeatIndex,
			decodedPlayerNames,
			decodedAvatarIds);
		return true;
	}

	private void CompleteBoardInitialization(NetworkBoardInitialization initialization)
	{
		SetState(NetworkSessionState.InGame);
		EmitSignal(SignalName.BoardInitialize, initialization.ToVariant());
		if (IsHost)
		{
			SendInitialBoardSnapshotsFromHost();
		}
	}

	private void LoadGameBoardScene()
	{
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		PackedScene? gameBoardScene = GD.Load<PackedScene>(GameBoardScenePath);
		if (gameBoardScene == null)
		{
			Fail(NetworkUserErrorCategory.SceneLoad, $"Missing game board scene: {GameBoardScenePath}");
			return;
		}

		GameBoardLaunchOptions options;
		try
		{
			options = GameBoardLaunchOptions.Network(
				_config,
				LocalSeatIndex,
				BuildPlayerNamesCsv().Split(',', StringSplitOptions.TrimEntries),
				BuildPlayerAvatarIdsCsv().Split(',', StringSplitOptions.TrimEntries),
				waitForInitialize: true);
		}
		catch (Exception error)
		{
			Fail(NetworkUserErrorCategory.SceneLoad, $"Invalid game board launch configuration: {error.Message}");
			return;
		}

		if (!GameBoardSceneFactory.TryCreate(gameBoardScene, options, out GameBoardScene gameBoard, out string createError))
		{
			Fail(NetworkUserErrorCategory.SceneLoad, $"Unable to create game board scene: {createError}");
			return;
		}
		Node? oldScene = GetTree().CurrentScene;
		GetTree().Root.AddChild(gameBoard);
		GetTree().CurrentScene = gameBoard;
		oldScene?.QueueFree();
	}

	private Godot.Collections.Dictionary BuildMatchBootstrap()
	{
		_sceneLoadCoordinator.Start(
			_config.MatchId,
			_lobbyRoster.Players.Select(player => player.SeatIndex),
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
		return string.Join(",", _lobbyRoster.Players.Select(player => EscapeCsvValue(player.DisplayName)));
	}

	private string BuildPlayerAvatarIdsCsv()
	{
		return string.Join(",", _lobbyRoster.Players.Select(player => EscapeCsvValue(player.AvatarId)));
	}

	private static string EscapeCsvValue(string value)
	{
		return value.Replace(",", " ");
	}

	private void PollLoadHandshake(double now)
	{
		if (!IsHost || _state != NetworkSessionState.LoadingScene)
		{
			return;
		}

		if (_sceneLoadCoordinator.Poll(now) == NetworkSceneLoadPollResult.TimedOut)
		{
			CancelMatchStart(NetworkUserErrorCategory.SceneLoad, "Scene load timed out.");
		}
	}

	private void CancelMatchStart(NetworkUserErrorCategory category, string technicalDetails)
	{
		GD.PushWarning($"Match start cancelled category={category} detail={technicalDetails}");
		string localizationKey = NetworkUserErrorRules.GetLocalizationKey(category);
		long returnGeneration = GetGenerationAfterStateTransition(NetworkSessionState.Lobby);
		_sceneLoadCoordinator.Reset();
		SetState(NetworkSessionState.Lobby);
		NetworkRpcContext context = GetCurrentRpcContext();
		Rpc(
			MethodName.RpcCancelMatchStart,
			context.SessionId,
			context.MatchId,
			context.AuthorityEpoch,
			localizationKey);
		ApplyMatchStartCancellation(
			NetworkUserErrorRules.NormalizeLocalizationKey(localizationKey, NetworkUserErrorCategory.SceneLoad),
			returnGeneration);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcCancelMatchStart(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string localizationKey)
	{
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: true))
		{
			return;
		}

		long returnGeneration = GetGenerationAfterStateTransition(NetworkSessionState.Lobby);
		ApplyMatchStartCancellation(localizationKey, returnGeneration);
	}

	private void ApplyMatchStartCancellation(string localizationKey, long returnGeneration)
	{
		_sceneLoadCoordinator.Reset();
		SetState(NetworkSessionState.Lobby);
		EmitSignal(SignalName.ErrorRaised, localizationKey);
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
			_pendingLobbyErrorLocalizationKey = string.Empty;
			_migrationFailureReturnPending = false;
			_hostDisconnectReturnPending = false;
			_hostDisconnectErrorPublished = false;
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
		_hostDisconnectReturnPending = false;
		_hostDisconnectErrorPublished = false;
	}

}
