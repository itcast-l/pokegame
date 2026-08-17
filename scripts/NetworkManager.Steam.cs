using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class NetworkManager
{
	private ulong _pendingSteamLobbyJoinId;
	private ulong _acceptedSteamLobbyJoinId;
	private ulong _originalSteamLobbyOwnerId;
	private ulong _lastObservedSteamLobbyOwnerId;
	private Callable _steamReadyCallable;
	private Callable _steamLobbyCreatedCallable;
	private Callable _steamLobbyJoinedCallable;
	private Callable _steamLobbyMatchListCallable;
	private Callable _steamJoinRequestedCallable;
	private Callable _steamLobbyDataUpdateCallable;
	private static readonly string[] SteamLobbyMetadataKeys =
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
	};

	private void InitializeSteamPlatformAdapter()
	{
		_steamPlatform = new SteamPlatformAdapter(GetNodeOrNull<Node>("/root/SteamManager"));
	}

	public void HostSteam(int playerCount, bool itemMode, int winningScore, string roomName, string roomPassword)
	{
		if (!PrepareForNewSessionStart())
		{
			return;
		}
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		RefreshLocalIdentity();
		if (!SteamAvailable)
		{
			CloseSessionAndFail(NetworkSessionCloseReason.ConnectionCreationFailed, NetworkUserErrorCategory.Steam, "Steam is not available.");
			return;
		}
		InitializeLocalHostSessionIdentity();

		SetState(NetworkSessionState.Hosting);
		_config = CreateConfig(NetworkTransport.Steam, playerCount, itemMode, winningScore, roomName, roomPassword);
		_lobbyRoster.ClearPlayers();
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

		_steamPlatform?.RequestLobbyList();
		EmitRoomListUpdated();
	}

	public void JoinSteamLobby(ulong lobbyId, string roomPassword)
	{
		if (!PrepareForNewSessionStart())
		{
			return;
		}
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		RefreshLocalIdentity();
		if (!SteamAvailable || lobbyId == 0)
		{
			CloseSessionAndFail(NetworkSessionCloseReason.ConnectionCreationFailed, NetworkUserErrorCategory.Steam, "Steam lobby is not available.");
			return;
		}

		if (TryReadSteamRoom(lobbyId, out NetworkRoomInfo room)
			&& !MultiplayerLobbyRules.CanJoinListedRoom(room.Status))
		{
			EmitSignal(SignalName.ErrorRaised, "MULTIPLAYER_LOBBY_ROOM_IN_PROGRESS");
			return;
		}

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
		_originalSteamLobbyOwnerId = GetSteamLobbyOwner(lobbyId);
		StartSteamLobbyJoin(lobbyId);
	}

	public void InviteSteamFriend(ulong steamId)
	{
		if (!SteamAvailable || _steamLobbyId == 0)
		{
			return;
		}

		if (_steamPlatform?.InviteUserToLobby(_steamLobbyId, steamId) == true)
		{
			return;
		}

		_steamPlatform?.ActivateLobbyInviteDialog(_steamLobbyId);
	}

	private void TryConnectSteamSignals()
	{
		if (_steamSignalsConnected)
		{
			return;
		}

		if (_steamPlatform == null)
		{
			return;
		}

		_steamReadyCallable = Callable.From(OnSteamReady);
		_steamLobbyCreatedCallable = Callable.From<int, ulong>(OnSteamLobbyCreated);
		_steamLobbyJoinedCallable = Callable.From<ulong, int, bool, int>(OnSteamLobbyJoined);
		_steamLobbyMatchListCallable = Callable.From<Godot.Collections.Array>(OnSteamLobbyMatchList);
		_steamJoinRequestedCallable = Callable.From<ulong, ulong>(OnSteamJoinRequested);
		_steamLobbyDataUpdateCallable = Callable.From<int, ulong, ulong>(OnSteamLobbyDataUpdate);
		bool allSignalsConnected = _steamPlatform.ConnectSignal("steam_ready", _steamReadyCallable);
		allSignalsConnected &= _steamPlatform.ConnectSignal("lobby_created", _steamLobbyCreatedCallable);
		allSignalsConnected &= _steamPlatform.ConnectSignal("lobby_joined", _steamLobbyJoinedCallable);
		allSignalsConnected &= _steamPlatform.ConnectSignal("lobby_match_list", _steamLobbyMatchListCallable);
		allSignalsConnected &= _steamPlatform.ConnectSignal("join_requested", _steamJoinRequestedCallable);
		allSignalsConnected &= _steamPlatform.ConnectSignal("lobby_data_update", _steamLobbyDataUpdateCallable);
		_steamSignalsConnected = true;
		if (!allSignalsConnected)
		{
			DisconnectSteamSignals();
		}
	}

	private void OnSteamReady()
	{
		_steamPlatform?.RefreshState();
		TryConnectSteamSignals();
	}

	private void DisconnectSteamSignals()
	{
		if (!_steamSignalsConnected)
		{
			return;
		}

		_steamPlatform?.DisconnectSignal("steam_ready", _steamReadyCallable);
		_steamPlatform?.DisconnectSignal("lobby_created", _steamLobbyCreatedCallable);
		_steamPlatform?.DisconnectSignal("lobby_joined", _steamLobbyJoinedCallable);
		_steamPlatform?.DisconnectSignal("lobby_match_list", _steamLobbyMatchListCallable);
		_steamPlatform?.DisconnectSignal("join_requested", _steamJoinRequestedCallable);
		_steamPlatform?.DisconnectSignal("lobby_data_update", _steamLobbyDataUpdateCallable);
		_steamSignalsConnected = false;
		_steamReadyCallable = default;
		_steamLobbyCreatedCallable = default;
		_steamLobbyJoinedCallable = default;
		_steamLobbyMatchListCallable = default;
		_steamJoinRequestedCallable = default;
		_steamLobbyDataUpdateCallable = default;
	}

	private bool TryCreateSteamPeer(bool server, ulong lobbyId, out MultiplayerPeer? peer)
	{
		peer = null;
		return lobbyId <= (ulong)long.MaxValue
			&& SteamHostMigrationTransport.TryCreatePeer(
				server,
				(long)lobbyId,
				out peer,
				out _);
	}

	private void TryCreateSteamLobby()
	{
		if (_steamPlatform?.CreateLobby(1, _config.PlayerCount) != true)
		{
			CloseSessionAndFail(NetworkSessionCloseReason.ConnectionCreationFailed, NetworkUserErrorCategory.Steam, "Steam lobby creation is unavailable.");
		}
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
			CloseSessionAndFail(NetworkSessionCloseReason.ConnectionCreationFailed, NetworkUserErrorCategory.Steam, "Steam lobby creation failed.");
			return;
		}

		_steamLobbyId = lobbyId;
		_originalSteamLobbyOwnerId = GetSteamLobbyOwner(lobbyId);
		_lastObservedSteamLobbyOwnerId = _originalSteamLobbyOwnerId;
		if (!TryCreateSteamPeer(server: true, lobbyId, out MultiplayerPeer? steamPeer))
		{
			LeaveSteamLobbyIfNeeded();
			CloseSessionAndFail(NetworkSessionCloseReason.ConnectionCreationFailed, NetworkUserErrorCategory.Steam, "Steam multiplayer peer is not available.");
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
				if (callbackState == NetworkSessionState.Reconnecting)
				{
					HandleHostDisconnected();
					return;
				}
				CloseSessionAndFail(
					NetworkSessionCloseReason.ConnectionCreationFailed,
					NetworkUserErrorCategory.Steam,
					"Steam lobby join failed.");
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
		if (_steamPlatform?.JoinLobby(lobbyId) != true)
		{
			_pendingSteamLobbyJoinId = 0;
			if (_state == NetworkSessionState.Reconnecting)
			{
				ScheduleReconnectRetry();
				return;
			}
			CloseSessionAndFail(
				NetworkSessionCloseReason.ConnectionCreationFailed,
				NetworkUserErrorCategory.Steam,
				"Steam lobby join is unavailable.");
		}
	}

	private void CompleteExpectedSteamLobbyJoin(ulong lobbyId)
	{
		ulong currentLobbyOwnerId = GetSteamLobbyOwner(lobbyId);
		if (NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
				_state,
				_originalSteamLobbyOwnerId,
				currentLobbyOwnerId))
		{
			ClearAcceptedSteamLobbyJoinIfNeeded(lobbyId);
			LeaveSteamLobbyIfNeeded();
			HandleHostDisconnected();
			return;
		}
		if (_state == NetworkSessionState.Joining && currentLobbyOwnerId != 0)
		{
			_originalSteamLobbyOwnerId = currentLobbyOwnerId;
		}
		_lastObservedSteamLobbyOwnerId = currentLobbyOwnerId;

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

			CloseSessionAndFail(NetworkSessionCloseReason.ConnectionCreationFailed, NetworkUserErrorCategory.Steam, "Steam multiplayer peer is not available.");
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
			if (lobbyVariant.VariantType != Variant.Type.Int || lobbyVariant.AsInt64() <= 0)
			{
				continue;
			}
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

	private void OnSteamLobbyDataUpdate(int success, ulong lobbyId, ulong memberId)
	{
		if (success == 0 || lobbyId == 0 || lobbyId != _steamLobbyId)
		{
			return;
		}

		_lastObservedSteamLobbyOwnerId = GetSteamLobbyOwner(lobbyId);
		if (!IsHost
			&& _originalSteamLobbyOwnerId != 0
			&& _lastObservedSteamLobbyOwnerId != 0
			&& _lastObservedSteamLobbyOwnerId != _originalSteamLobbyOwnerId
			&& _state is NetworkSessionState.Lobby
				or NetworkSessionState.Starting
				or NetworkSessionState.LoadingScene
				or NetworkSessionState.InGame
				or NetworkSessionState.Reconnecting)
		{
			HandleHostDisconnected();
			return;
		}
		if (HostMigrationCapabilityAvailable
			&& (_activeMigrationPending || _state == NetworkSessionState.MigratingHost))
		{
			GD.Print($"MIGRATION_STEAM_OWNER_UPDATE lobby={lobbyId} member={memberId} owner={_lastObservedSteamLobbyOwnerId}");
		}
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
		if (_steamPlatform == null || lobbyId == 0)
		{
			return false;
		}

		var metadata = new Dictionary<string, string>();
		foreach (string key in SteamLobbyMetadataKeys)
		{
			if (!_steamPlatform.TryGetLobbyData(lobbyId, key, out string value))
			{
				return false;
			}
			metadata[key] = value;
		}

		return SteamLobbyMetadata.TryReadRoom(metadata, lobbyId, out room);
	}

	private ulong GetSteamLobbyOwner(ulong lobbyId)
	{
		if (lobbyId == 0 || lobbyId > (ulong)long.MaxValue
			|| _steamPlatform?.TryGetLobbyOwner((long)lobbyId, out long owner) != true)
		{
			return 0;
		}

		return (ulong)owner;
	}

	private long GetLastObservedSteamLobbyOwnerId()
	{
		return _lastObservedSteamLobbyOwnerId <= (ulong)long.MaxValue
			? (long)_lastObservedSteamLobbyOwnerId
			: 0;
	}

	private bool TryGetLocalSteamId(out long steamId)
	{
		steamId = 0;
		return _steamPlatform?.TryGetLocalSteamId(out steamId) == true;
	}

	private bool TryGetSteamIdForPeer(int peerId, out long steamId)
	{
		steamId = 0;
		MultiplayerPeer? peer = _steamPeer ?? Multiplayer.MultiplayerPeer;
		return _steamPlatform?.TryGetSteamIdForPeer(peer, peerId, out steamId) == true;
	}

	private void RefreshSteamLobbyAdvertisedState()
	{
		TryRefreshSteamLobbyAdvertisedState();
	}

	private bool TryRefreshSteamLobbyAdvertisedState()
	{
		if (_config.Transport != NetworkTransport.Steam)
		{
			return true;
		}
		if (!IsHost || _steamLobbyId == 0 || _steamLobbyId > (ulong)long.MaxValue)
		{
			return false;
		}

		if (_steamPlatform == null)
		{
			return false;
		}

		bool updated = _steamPlatform.SetLobbyJoinable(
			_steamLobbyId,
			SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(_state));
		TryGetLocalSteamId(out long localSteamId);
		Dictionary<string, string> metadata = SteamLobbyMetadata.Build(
			_config,
			_localDisplayName,
			_state.ToString(),
			GetCurrentAuthorityEpoch(),
			localSteamId);
		foreach (KeyValuePair<string, string> entry in metadata)
		{
			updated &= _steamPlatform.SetLobbyData(_steamLobbyId, entry.Key, entry.Value);
		}

		return updated;
	}

	private void LeaveSteamLobby(ulong lobbyId)
	{
		if (lobbyId == 0)
		{
			return;
		}

		_steamPlatform?.LeaveLobby(lobbyId);
	}

	private void LeaveSteamLobbyIfNeeded()
	{
		LeaveSteamLobby(_steamLobbyId);
	}
}
