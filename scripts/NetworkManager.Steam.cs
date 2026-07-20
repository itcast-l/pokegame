using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class NetworkManager
{
	private ulong _pendingSteamLobbyJoinId;
	private ulong _acceptedSteamLobbyJoinId;

	public void HostSteam(int playerCount, bool itemMode, int winningScore, string roomName, string roomPassword)
	{
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		RefreshLocalIdentity();
		LeaveSession();
		if (!SteamAvailable)
		{
			Fail("Steam is not available.");
			return;
		}

		SetState(NetworkSessionState.Hosting);
		_config = CreateConfig(NetworkTransport.Steam, playerCount, itemMode, winningScore, roomName, roomPassword);
		_players.Clear();
		InsertPlayerInSeatOrder(CreateLocalPlayer(peerId: 1, seatIndex: 0, isHost: true));
		_pendingSteamLobbyJoinId = 0;
		_pendingSteamHostLobby = true;
		TryCreateSteamLobby();
		EmitPlayerListUpdated();
	}

	public void RefreshSteamLobbies()
	{
		_rooms.RemoveAll(room => room.Transport == NetworkTransport.Steam);
		if (!SteamAvailable)
		{
			EmitRoomListUpdated();
			return;
		}

		GodotObject? steam = GetSteamSingleton();
		steam?.Call("requestLobbyList");
		EmitRoomListUpdated();
	}

	public void JoinSteamLobby(ulong lobbyId, string roomPassword)
	{
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		RefreshLocalIdentity();
		if (!SteamAvailable || lobbyId == 0)
		{
			Fail("Steam lobby is not available.");
			return;
		}

		if (TryReadSteamRoom(lobbyId, out NetworkRoomInfo room)
			&& !MultiplayerLobbyRules.CanJoinListedRoom(room.Status))
		{
			EmitSignal(SignalName.ErrorRaised, "MULTIPLAYER_LOBBY_ROOM_IN_PROGRESS");
			return;
		}

		LeaveSession();
		SetState(NetworkSessionState.Joining);
		_pendingJoinRoomPassword = RoomPasswordRules.Normalize(roomPassword);
		_config = CreateConfig(
			NetworkTransport.Steam,
			NetworkSessionDefaults.DefaultPlayerCount,
			NetworkSessionDefaults.DefaultItemMode,
			NetworkSessionDefaults.DefaultWinningScore,
			RoomNameRules.DefaultRoomName,
			string.Empty);
		_steamLobbyId = lobbyId;
		StartSteamLobbyJoin(lobbyId);
	}

	public void InviteSteamFriend(ulong steamId)
	{
		if (!SteamAvailable || _steamLobbyId == 0)
		{
			return;
		}

		GodotObject? steam = GetSteamSingleton();
		Variant result = steam?.Call("inviteUserToLobby", _steamLobbyId, steamId) ?? default;
		if (result.VariantType == Variant.Type.Bool && result.AsBool())
		{
			return;
		}

		steam?.Call("activateGameOverlayInviteDialog", _steamLobbyId);
	}

	private void TryConnectSteamSignals()
	{
		if (_steamSignalsConnected)
		{
			return;
		}

		GodotObject? steam = GetSteamSingleton();
		if (steam == null)
		{
			Node? steamManager = GetNodeOrNull<Node>("/root/SteamManager");
			steamManager?.Connect("steam_ready", Callable.From(TryConnectSteamSignals));
			return;
		}

		ConnectSignalIfAvailable(steam, "lobby_created", Callable.From<int, ulong>(OnSteamLobbyCreated));
		ConnectSignalIfAvailable(steam, "lobby_joined", Callable.From<ulong, int, bool, int>(OnSteamLobbyJoined));
		ConnectSignalIfAvailable(steam, "lobby_match_list", Callable.From<Godot.Collections.Array>(OnSteamLobbyMatchList));
		ConnectSignalIfAvailable(steam, "join_requested", Callable.From<ulong, ulong>(OnSteamJoinRequested));
		_steamSignalsConnected = true;
	}

	private static void ConnectSignalIfAvailable(GodotObject target, string signalName, Callable callable)
	{
		if (target.HasSignal(signalName))
		{
			target.Connect(signalName, callable);
		}
	}

	private bool TryCreateSteamPeer(bool server, ulong lobbyId, out MultiplayerPeer? peer)
	{
		peer = null;
		Variant created = ClassDB.Instantiate("SteamMultiplayerPeer");
		if (created.VariantType != Variant.Type.Object || created.AsGodotObject() is not MultiplayerPeer multiplayerPeer)
		{
			return false;
		}

		string method = server && lobbyId != 0 && multiplayerPeer.HasMethod("host_with_lobby")
			? "host_with_lobby"
			: !server && lobbyId != 0 && multiplayerPeer.HasMethod("connect_to_lobby")
				? "connect_to_lobby"
				: server
					? "create_host"
					: "create_client";
		if (!multiplayerPeer.HasMethod(method))
		{
			multiplayerPeer.Free();
			return false;
		}

		Variant result = method is "host_with_lobby" or "connect_to_lobby"
			? multiplayerPeer.Call(method, lobbyId)
			: server
				? multiplayerPeer.Call(method, 0)
				: multiplayerPeer.Call(method, GetSteamLobbyOwner(lobbyId));
		if (result.VariantType == Variant.Type.Int && (Error)result.AsInt32() != Error.Ok)
		{
			multiplayerPeer.Free();
			return false;
		}

		peer = multiplayerPeer;
		return true;
	}

	private void TryCreateSteamLobby()
	{
		GodotObject? steam = GetSteamSingleton();
		if (steam == null)
		{
			return;
		}

		steam.Call("createLobby", 1, _config.PlayerCount);
	}

	private void OnSteamLobbyCreated(int connect, ulong lobbyId)
	{
		if (!_pendingSteamHostLobby)
		{
			return;
		}

		_pendingSteamHostLobby = false;
		if (connect != 1 || lobbyId == 0)
		{
			Fail("Steam lobby creation failed.");
			return;
		}

		_steamLobbyId = lobbyId;
		if (!TryCreateSteamPeer(server: true, lobbyId, out MultiplayerPeer? steamPeer))
		{
			LeaveSteamLobbyIfNeeded();
			Fail("Steam multiplayer peer is not available.");
			return;
		}

		_steamPeer = steamPeer;
		Multiplayer.MultiplayerPeer = _steamPeer;
		_acceptedSteamLobbyJoinId = lobbyId;
		SetState(NetworkSessionState.Lobby);
		EmitPlayerListUpdated();
	}

	private void OnSteamLobbyJoined(ulong lobbyId, int permissions, bool locked, int response)
	{
		NetworkSessionState callbackState = _state;
		NetworkReconnectRules.DispatchSteamLobbyJoinCallback(
			callbackState,
			_pendingSteamHostLobby,
			_pendingSteamLobbyJoinId,
			lobbyId,
			_acceptedSteamLobbyJoinId,
			response,
			LeaveSteamLobby,
			() =>
			{
				_pendingSteamLobbyJoinId = 0;
				ScheduleReconnectRetry();
			},
			() =>
			{
				_pendingSteamLobbyJoinId = 0;
				Fail(callbackState == NetworkSessionState.Reconnecting
					? "Reconnect failed: Steam lobby join was denied."
					: "Steam lobby join failed.");
			},
			() =>
			{
				_pendingSteamLobbyJoinId = 0;
				_acceptedSteamLobbyJoinId = lobbyId;
				CompleteExpectedSteamLobbyJoin(lobbyId);
			});
	}

	private void StartSteamLobbyJoin(ulong lobbyId)
	{
		_pendingSteamLobbyJoinId = lobbyId;
		_acceptedSteamLobbyJoinId = 0;
		GodotObject? steam = GetSteamSingleton();
		steam?.Call("joinLobby", lobbyId);
	}

	private void CompleteExpectedSteamLobbyJoin(ulong lobbyId)
	{
		if (NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			_state,
			GetOriginalHostStableUserId(),
			GetSteamLobbyOwner(lobbyId)))
		{
			ClearAcceptedSteamLobbyJoinIfNeeded(lobbyId);
			LeaveSteamLobbyIfNeeded();
			Fail("Reconnect failed: original host is no longer available.");
			return;
		}

		TryReadSteamLobbyConfig(lobbyId);
		if (!TryCreateSteamPeer(server: false, lobbyId, out MultiplayerPeer? steamPeer))
		{
			ClearAcceptedSteamLobbyJoinIfNeeded(lobbyId);
			LeaveSteamLobbyIfNeeded();
			if (_state == NetworkSessionState.Reconnecting)
			{
				ScheduleReconnectRetry();
				return;
			}

			Fail("Steam multiplayer peer is not available.");
			return;
		}

		_steamPeer = steamPeer;
		Multiplayer.MultiplayerPeer = _steamPeer;
	}

	private void ClearAcceptedSteamLobbyJoinIfNeeded(ulong lobbyId)
	{
		if (_acceptedSteamLobbyJoinId == lobbyId)
		{
			_acceptedSteamLobbyJoinId = 0;
		}
	}

	private void OnSteamLobbyMatchList(Godot.Collections.Array lobbies)
	{
		_rooms.RemoveAll(room => room.Transport == NetworkTransport.Steam);
		foreach (Variant lobbyVariant in lobbies)
		{
			ulong lobbyId = unchecked((ulong)lobbyVariant.AsInt64());
			if (TryReadSteamRoom(lobbyId, out NetworkRoomInfo room))
			{
				UpsertRoom(room);
			}
		}

		EmitRoomListUpdated();
	}

	private void OnSteamJoinRequested(ulong lobbyId, ulong friendId)
	{
		if (!MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(_state))
		{
			EmitSignal(SignalName.ErrorRaised, "MULTIPLAYER_LOBBY_STEAM_INVITE_BUSY");
			return;
		}

		JoinSteamLobby(lobbyId, string.Empty);
	}

	private void TryReadSteamLobbyConfig(ulong lobbyId)
	{
		if (!TryReadSteamRoom(lobbyId, out NetworkRoomInfo room))
		{
			return;
		}

		_config = _config with
		{
			SessionId = room.SessionId,
			RoomName = room.RoomName,
			Transport = NetworkTransport.Steam,
			PlayerCount = room.PlayerCount,
			ItemMode = room.ItemMode,
			WinningScore = room.WinningScore,
		};
	}

	private bool TryReadSteamRoom(ulong lobbyId, out NetworkRoomInfo room)
	{
		room = default;
		GodotObject? steam = GetSteamSingleton();
		if (steam == null || lobbyId == 0)
		{
			return false;
		}

		var metadata = new Dictionary<string, string>();
		foreach (string key in new[]
		{
			SteamLobbyMetadata.ProtocolKey,
			SteamLobbyMetadata.SessionIdKey,
			SteamLobbyMetadata.RoomNameKey,
			SteamLobbyMetadata.HostNameKey,
			SteamLobbyMetadata.PlayerCountKey,
			SteamLobbyMetadata.ItemModeKey,
			SteamLobbyMetadata.WinningScoreKey,
			SteamLobbyMetadata.HasPasswordKey,
			SteamLobbyMetadata.StatusKey,
		})
		{
			metadata[key] = steam.Call("getLobbyData", lobbyId, key).AsString();
		}

		return SteamLobbyMetadata.TryReadRoom(metadata, lobbyId, out room);
	}

	private ulong GetSteamLobbyOwner(ulong lobbyId)
	{
		GodotObject? steam = GetSteamSingleton();
		if (steam == null || lobbyId == 0 || !steam.HasMethod("getLobbyOwner"))
		{
			return 0;
		}

		return unchecked((ulong)steam.Call("getLobbyOwner", lobbyId).AsInt64());
	}

	private string GetOriginalHostStableUserId()
	{
		return _players.FirstOrDefault(player => player.IsHost).StableUserId;
	}

	private GodotObject? GetSteamSingleton()
	{
		return Engine.HasSingleton("Steam") ? Engine.GetSingleton("Steam") : null;
	}

	private bool IsSteamInitialized()
	{
		Node? steamManager = GetNodeOrNull<Node>("/root/SteamManager");
		if (steamManager != null)
		{
			Variant initialized = steamManager.Get("is_initialized");
			if (initialized.VariantType == Variant.Type.Bool)
			{
				return initialized.AsBool();
			}
		}

		GodotObject? steam = GetSteamSingleton();
		return steam != null && steam.HasMethod("isSteamRunning") && steam.Call("isSteamRunning").AsBool();
	}

	private void RefreshSteamLobbyAdvertisedState()
	{
		if (!IsHost || _config.Transport != NetworkTransport.Steam || _steamLobbyId == 0)
		{
			return;
		}

		GodotObject? steam = GetSteamSingleton();
		if (steam == null)
		{
			return;
		}

		steam.Call("setLobbyJoinable", _steamLobbyId, SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(_state));
		Dictionary<string, string> metadata = SteamLobbyMetadata.Build(_config, _localDisplayName, _state.ToString());
		foreach (KeyValuePair<string, string> entry in metadata)
		{
			steam.Call("setLobbyData", _steamLobbyId, entry.Key, entry.Value);
		}
	}

	private void LeaveSteamLobby(ulong lobbyId)
	{
		if (lobbyId == 0)
		{
			return;
		}

		GodotObject? steam = GetSteamSingleton();
		if (steam == null || !steam.HasMethod("leaveLobby"))
		{
			return;
		}

		steam.Call("leaveLobby", lobbyId);
	}

	private void LeaveSteamLobbyIfNeeded()
	{
		LeaveSteamLobby(_steamLobbyId);
	}
}
