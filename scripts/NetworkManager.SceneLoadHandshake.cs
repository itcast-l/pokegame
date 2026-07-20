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
