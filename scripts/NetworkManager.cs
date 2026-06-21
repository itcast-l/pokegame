using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public partial class NetworkManager : Node
{
	[Signal]
	public delegate void StateChangedEventHandler(int state);

	[Signal]
	public delegate void ErrorRaisedEventHandler(string message);

	[Signal]
	public delegate void SeatControlStatesUpdatedEventHandler();

	[Signal]
	public delegate void RoomListUpdatedEventHandler(Godot.Collections.Array rooms);

	[Signal]
	public delegate void PlayerListUpdatedEventHandler(Godot.Collections.Array players);

	[Signal]
	public delegate void ReadyChangedEventHandler(int seatIndex, bool isReady);

	[Signal]
	public delegate void BeginSceneLoadEventHandler(Godot.Collections.Dictionary bootstrap);

	[Signal]
	public delegate void BoardInitializeEventHandler(Godot.Collections.Dictionary payload);

	private const string GameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const double DiscoveryBroadcastSeconds = 1.0;
	private const double RoomStaleSeconds = 5.0;
	private const double ReconnectRetrySeconds = 2.0;
	private const double HostEndedMatchBroadcastGraceSeconds = 0.15;

	private readonly List<NetworkPlayerPublicState> _players = new();
	private readonly List<NetworkRoomInfo> _rooms = new();
	private readonly Dictionary<string, double> _roomLastSeenAt = new();
	private readonly List<SeatControlState> _seatControlStates = new();
	private readonly Dictionary<int, ReconnectClaimRecord> _reconnectClaimsBySeat = new();
	private PacketPeerUdp? _lanDiscoveryResponder;
	private PacketPeerUdp? _lanDiscoveryBrowser;
	private ENetMultiplayerPeer? _enetPeer;
	private MultiplayerPeer? _steamPeer;
	private NetworkSessionConfig _config = new(
		string.Empty,
		RoomNameRules.DefaultRoomName,
		string.Empty,
		string.Empty,
		0,
		NetworkTransport.Lan,
		NetworkSessionDefaults.DefaultPlayerCount,
		NetworkSessionDefaults.DefaultItemMode,
		NetworkSessionDefaults.DefaultWinningScore,
		LobbyVisibility.FriendsOnly);
	private SceneLoadHandshakeTracker? _loadHandshake;
	private NetworkSessionState _state = NetworkSessionState.Idle;
	private double _lastDiscoveryBroadcastAt;
	private string _localDisplayName = PlayerNameRules.DefaultPlayerName;
	private string _localAvatarId = CharacterAvatarCatalog.DefaultAvatarId;
	private string _localReconnectToken = string.Empty;
	private string _lastLanJoinAddress = string.Empty;
	private string _pendingJoinRoomPassword = string.Empty;
	private ulong _steamLobbyId;
	private bool _signalsConnected;
	private bool _steamSignalsConnected;
	private bool _pendingSteamHostLobby;
	private double _nextReconnectAttemptAt;
	private bool _reconnectAttemptInFlight;
	private bool _lobbyRejectPending;
	private string _lobbyRejectMessage = string.Empty;
	private bool _hostEndedMatchPending;
	private string _hostEndedMatchMessage = string.Empty;
	private bool _hostEndedMatchBroadcasted;

	public NetworkSessionState State => _state;
	public bool SteamAvailable => GetSteamSingleton() != null && IsSteamInitialized();
	public bool IsInLobby => _state is NetworkSessionState.Lobby or NetworkSessionState.Starting or NetworkSessionState.LoadingScene;
	public bool IsHost => TryGetCachedLocalIsHost(out bool isHost) ? isHost : TryIsServer();
	public int LocalSeatIndex => GetLocalPlayer().SeatIndex;
	public NetworkSessionConfig CurrentConfig => _config;

	public override void _Ready()
	{
		ConnectMultiplayerSignals();
		TryConnectSteamSignals();
		RefreshLocalIdentity();
		SetState(NetworkSessionState.Idle);
	}

	public override void _ExitTree()
	{
		LeaveSession();
	}

	public override void _Process(double delta)
	{
		double now = Time.GetTicksMsec() / 1000.0;
		PollLanDiscoveryResponder(now);
		PollLanDiscoveryBrowser(now);
		PollLoadHandshake(now);
		PollReconnect(now);
	}

	public void HostLan(int playerCount, bool itemMode, int winningScore, string roomName, string roomPassword)
	{
		RefreshLocalIdentity();
		LeaveSession();
		SetState(NetworkSessionState.Hosting);
		_config = CreateConfig(NetworkTransport.Lan, playerCount, itemMode, winningScore, roomName, roomPassword);

		_enetPeer = new ENetMultiplayerPeer();
		Error error = _enetPeer.CreateServer(NetworkSessionDefaults.LanGamePort, NetworkSessionDefaults.MaxPlayers - 1);
		if (error != Error.Ok)
		{
			Fail($"LAN host failed: {error}");
			return;
		}

		Multiplayer.MultiplayerPeer = _enetPeer;
		_players.Clear();
		InsertPlayerInSeatOrder(CreateLocalPlayer(peerId: 1, seatIndex: 0, isHost: true));
		StartLanDiscoveryResponder();
		SetState(NetworkSessionState.Lobby);
		EmitPlayerListUpdated();
	}

	public void SearchLan()
	{
		StopLanDiscoveryBrowser();
		_rooms.Clear();
		_roomLastSeenAt.Clear();
		_lanDiscoveryBrowser = new PacketPeerUdp();
		Error bindError = _lanDiscoveryBrowser.Bind(NetworkSessionDefaults.LanDiscoveryPort);
		if (bindError != Error.Ok)
		{
			bindError = _lanDiscoveryBrowser.Bind(0);
		}
		if (bindError != Error.Ok)
		{
			_lanDiscoveryBrowser.Close();
			_lanDiscoveryBrowser = null;
			Fail($"LAN search failed: {bindError}");
			return;
		}

		SetState(NetworkSessionState.Searching);
		SendLanDiscoveryPing();
		EmitRoomListUpdated();
	}

	public string GetLocalLanAddressSummary()
	{
		IReadOnlyList<string> addresses = LanAddressHelper.GetLocalIpv4Addresses();
		return addresses.Count == 0 ? "未检测到局域网 IPv4 地址" : string.Join(" / ", addresses);
	}

	public void JoinLan(string address, string roomPassword)
	{
		if (string.IsNullOrWhiteSpace(address))
		{
			Fail("LAN address is empty.");
			return;
		}

		RefreshLocalIdentity();
		LeaveSession();
		_lastLanJoinAddress = address.Trim();
		SetState(NetworkSessionState.Joining);
		_pendingJoinRoomPassword = RoomPasswordRules.Normalize(roomPassword);
		_config = CreateConfig(
			NetworkTransport.Lan,
			NetworkSessionDefaults.DefaultPlayerCount,
			NetworkSessionDefaults.DefaultItemMode,
			NetworkSessionDefaults.DefaultWinningScore,
			RoomNameRules.DefaultRoomName,
			string.Empty);
		_enetPeer = new ENetMultiplayerPeer();
		Error error = _enetPeer.CreateClient(address.Trim(), NetworkSessionDefaults.LanGamePort);
		if (error != Error.Ok)
		{
			Fail($"LAN join failed: {error}");
			return;
		}

		Multiplayer.MultiplayerPeer = _enetPeer;
	}

	public void HostSteam(int playerCount, bool itemMode, int winningScore, string roomName, string roomPassword)
	{
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
		RefreshLocalIdentity();
		if (!SteamAvailable || lobbyId == 0)
		{
			Fail("Steam lobby is not available.");
			return;
		}

		if (TryReadSteamRoom(lobbyId, out NetworkRoomInfo room)
			&& !MultiplayerLobbyRules.CanJoinListedRoom(room.Status))
		{
			EmitSignal(SignalName.ErrorRaised, "该房间对局进行中，不可加入");
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
		GodotObject? steam = GetSteamSingleton();
		steam?.Call("joinLobby", lobbyId);
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
			EmitSignal(SignalName.ErrorRaised, "All seats must be filled and ready.");
			return;
		}

		_config = _config with
		{
			MatchId = Guid.NewGuid().ToString("N"),
			MatchSeed = (long)GD.Randi(),
		};
		InitializeMatchSeatControlStates();
		SetState(NetworkSessionState.Starting);
		SendReconnectCredentialsToConnectedPeers();
		Godot.Collections.Dictionary bootstrap = BuildMatchBootstrap();
		Rpc(MethodName.RpcBeginSceneLoad, bootstrap);
		RpcBeginSceneLoad(bootstrap);
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

	public void RequestPlayHandCard(int handIndex, int turnSequenceGeneration)
	{
		if (_state != NetworkSessionState.InGame || IsHost)
		{
			return;
		}

		RpcId(1, MethodName.RpcRequestPlayHandCard, handIndex, turnSequenceGeneration);
	}

	public void RequestPlayItemCard(int itemHandIndex, int turnSequenceGeneration)
	{
		if (_state != NetworkSessionState.InGame || IsHost)
		{
			return;
		}

		RpcId(1, MethodName.RpcRequestPlayItemCard, itemHandIndex, turnSequenceGeneration);
	}

	public void RequestPendingItemSelection(int targetPlayerIndex, int targetHandIndex, int turnSequenceGeneration)
	{
		if (_state != NetworkSessionState.InGame || IsHost)
		{
			return;
		}

		RpcId(1, MethodName.RpcRequestPendingItemSelection, targetPlayerIndex, targetHandIndex, turnSequenceGeneration);
	}

	internal void BroadcastCardPlayedEventFromHost(
		int actorSeatIndex,
		int handIndex,
		string playedCardCode,
		bool playedCardIsFaceUp,
		NetworkHandPlayVisualSync visualSync,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.PlayHandCard,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = actorSeatIndex,
			[NetworkBoardVisualEventTypes.HandIndexKey] = handIndex,
			[NetworkBoardVisualEventTypes.PlayedCardCodeKey] = playedCardCode,
			[NetworkBoardVisualEventTypes.PlayedCardIsFaceUpKey] = playedCardIsFaceUp,
			[NetworkBoardVisualEventTypes.TableCardIndexKey] = visualSync.TableCardIndex,
			[NetworkBoardVisualEventTypes.MatchingTableCardIndexKey] = visualSync.MatchingTableCardIndex,
			[NetworkBoardVisualEventTypes.MatchedTableCardFlippedKey] = visualSync.MatchedTableCardFlipped,
			[NetworkBoardVisualEventTypes.BorrowTideAdjustedRankKey] = visualSync.BorrowTideAdjustedRank,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		BroadcastBoardVisualEventFromHost(payload);
	}

	internal void BroadcastItemCardPlayedVisualEventFromHost(
		int actorSeatIndex,
		NetworkItemPlayVisualSync visualSync,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.PlayItemCard,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = actorSeatIndex,
			[NetworkBoardVisualEventTypes.HandIndexKey] = visualSync.ItemHandIndex,
			[NetworkBoardVisualEventTypes.ItemCardIdKey] = visualSync.ItemCardId,
			[NetworkBoardVisualEventTypes.PlayPresentationKey] = visualSync.PlayPresentation,
			[NetworkBoardVisualEventTypes.TargetTableCardIndexKey] = visualSync.TargetTableCardIndex,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		if (visualSync.PlayPresentation == CardPlayPresentationTypes.TableOverlay)
		{
			BroadcastBoardVisualEventFromHost(payload);
			return;
		}

		SendBoardVisualEventToSeatFromHost(actorSeatIndex, payload);
	}

	internal void BroadcastCaptureVisualEventFromHost(
		NetworkCaptureVisualSync visualSync,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.CaptureToDeck,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = visualSync.PlayerIndex,
			[NetworkBoardVisualEventTypes.CaptureStartIndexKey] = visualSync.CaptureStartIndex,
			[NetworkBoardVisualEventTypes.ExtraTableCardIndexesKey] = new Godot.Collections.Array<int>(visualSync.ExtraTableCardIndexes),
			[NetworkBoardVisualEventTypes.FlightStaggerSecondsKey] = visualSync.FlightStaggerSeconds,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		BroadcastBoardVisualEventFromHost(payload);
	}

	internal void BroadcastCaptureRevealVisualEventFromHost(
		NetworkCaptureRevealVisualSync visualSync,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.CaptureReveal,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = visualSync.PlayerIndex,
			[NetworkBoardVisualEventTypes.RevealTableCardIndexesKey] = new Godot.Collections.Array<int>(visualSync.RevealTableCardIndexes),
			[NetworkBoardVisualEventTypes.RevealCardCodesKey] = new Godot.Collections.Array<string>(visualSync.RevealCardCodes),
			[NetworkBoardVisualEventTypes.RevealAttachedItemCardIdsCsvKey] = new Godot.Collections.Array<string>(visualSync.RevealAttachedItemCardIdsCsv),
			[NetworkBoardVisualEventTypes.RevealPointValuesKey] = new Godot.Collections.Array<int>(visualSync.RevealPointValues),
			[NetworkBoardVisualEventTypes.RevealPointDoubleFlagsKey] = new Godot.Collections.Array<bool>(visualSync.RevealPointDoubleFlags),
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(visualSync.PlayerIndex, payload);
	}

	internal void BroadcastDrawCardVisualEventFromHost(
		int playerIndex,
		int displayIndex,
		string drawnCardCode,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (!NetworkReconnectRules.ShouldSendRealtimeBoardEventToPeer(
				IsHost,
				_state,
				player.IsConnected,
				player.IsHost))
			{
				continue;
			}

			var payload = new Godot.Collections.Dictionary
			{
				[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.DrawCard,
				[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = playerIndex,
				[NetworkBoardVisualEventTypes.DisplayIndexKey] = displayIndex,
				[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
				[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
			};
			if (player.SeatIndex == playerIndex && !string.IsNullOrWhiteSpace(drawnCardCode))
			{
				payload[NetworkBoardVisualEventTypes.DrawnCardCodeKey] = drawnCardCode;
			}

			RpcId(player.PeerId, MethodName.RpcCardPlayedEvent, payload);
		}
	}

	internal void BroadcastDeckShuffleVisualEventFromHost(int playerIndex, int turnSequenceGeneration, int snapshotId)
	{
		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.DeckShuffle,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = playerIndex,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(playerIndex, payload);
	}

	internal void BroadcastStealHandCardVisualEventFromHost(
		int ownerSeatIndex,
		int targetSeatIndex,
		int targetHandIndex,
		string stolenCardCode,
		IReadOnlyList<string> attachedItemCardIds,
		string stolenItemCardId,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.StealHandCard,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = ownerSeatIndex,
			[NetworkBoardVisualEventTypes.TargetSeatIndexKey] = targetSeatIndex,
			[NetworkBoardVisualEventTypes.TargetHandIndexKey] = targetHandIndex,
			[NetworkBoardVisualEventTypes.PlayedCardCodeKey] = stolenCardCode,
			[NetworkBoardVisualEventTypes.ItemCardIdKey] = stolenItemCardId,
			[NetworkBoardVisualEventTypes.AttachedItemCardIdsKey] = new Godot.Collections.Array<string>(attachedItemCardIds ?? Array.Empty<string>()),
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerSeatIndex, payload);
	}

	internal void BroadcastStealDeckCardsVisualEventFromHost(
		int ownerSeatIndex,
		int targetSeatIndex,
		int movedCardCount,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame || movedCardCount <= 0)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.StealDeckCards,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = ownerSeatIndex,
			[NetworkBoardVisualEventTypes.TargetSeatIndexKey] = targetSeatIndex,
			[NetworkBoardVisualEventTypes.MovedCardCountKey] = movedCardCount,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerSeatIndex, payload);
	}

	internal void BroadcastLensRevealVisualEventFromHost(
		int ownerPlayerIndex,
		Vector2 splitGlobalCenter,
		IReadOnlyList<int> targetIndexes,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (!TryGetConnectedRemotePlayerBySeatIndex(ownerPlayerIndex, out NetworkPlayerPublicState recipient))
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.LensReveal,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = ownerPlayerIndex,
			[NetworkBoardVisualEventTypes.SplitCenterXKey] = splitGlobalCenter.X,
			[NetworkBoardVisualEventTypes.SplitCenterYKey] = splitGlobalCenter.Y,
			[NetworkBoardVisualEventTypes.TargetTableCardIndexesKey] = new Godot.Collections.Array<int>(targetIndexes),
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		RpcId(recipient.PeerId, MethodName.RpcCardPlayedEvent, payload);
	}

	internal void BroadcastTableShuffleVisualEventFromHost(
		int ownerPlayerIndex,
		IReadOnlyList<int> targetIndexes,
		int turnSequenceGeneration,
		int snapshotId)
	{
		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.TableShuffle,
			[NetworkBoardVisualEventTypes.ShuffleTargetIndexesKey] = new Godot.Collections.Array<int>(targetIndexes),
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerPlayerIndex, payload);
	}

	internal void BroadcastQuestionRefreshVisualEventFromHost(int ownerPlayerIndex, int tableCardIndex, int turnSequenceGeneration, int snapshotId)
	{
		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.QuestionRefresh,
			[NetworkBoardVisualEventTypes.TableCardIndexKey] = tableCardIndex,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerPlayerIndex, payload);
	}

	internal void BroadcastBombPairVisualEventFromHost(int ownerPlayerIndex, int turnSequenceGeneration, int snapshotId)
	{
		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.BombPair,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerPlayerIndex, payload);
	}

	internal void BroadcastMantaTideShiftVisualEventFromHost(int ownerPlayerIndex, int turnSequenceGeneration, int snapshotId)
	{
		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.MantaTideShift,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerPlayerIndex, payload);
	}

	internal void SendQuestionRewardVisualEventToSeatFromHost(
		int ownerSeatIndex,
		QuestionCardRewardAnimation rewardAnimation,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (!TryGetConnectedRemotePlayerBySeatIndex(ownerSeatIndex, out NetworkPlayerPublicState recipient))
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.QuestionReward,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = ownerSeatIndex,
			[NetworkBoardVisualEventTypes.ItemCardIdKey] = rewardAnimation.ItemCard.Id,
			[NetworkBoardVisualEventTypes.HandIndexKey] = rewardAnimation.ItemHandIndex,
			[NetworkBoardVisualEventTypes.StartGlobalXKey] = rewardAnimation.StartGlobalPosition.X,
			[NetworkBoardVisualEventTypes.StartGlobalYKey] = rewardAnimation.StartGlobalPosition.Y,
			[NetworkBoardVisualEventTypes.RequiresMagnetAttachmentKey] = rewardAnimation.RequiresMagnetAttachment,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		RpcId(recipient.PeerId, MethodName.RpcCardPlayedEvent, payload);
	}

	private void BroadcastBoardVisualEventFromHost(Godot.Collections.Dictionary payload)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (!NetworkReconnectRules.ShouldSendRealtimeBoardEventToPeer(
				IsHost,
				_state,
				player.IsConnected,
				player.IsHost))
			{
				continue;
			}

			RpcId(player.PeerId, MethodName.RpcCardPlayedEvent, payload);
		}
	}

	private void SendBoardVisualEventToSeatFromHost(int seatIndex, Godot.Collections.Dictionary payload)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (!TryGetConnectedRemotePlayerBySeatIndex(seatIndex, out NetworkPlayerPublicState recipient))
		{
			return;
		}

		RpcId(recipient.PeerId, MethodName.RpcCardPlayedEvent, payload);
	}

	public void SendInitialBoardSnapshotsFromHost(GameBoardScene gameBoard)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		SendReconnectCredentialsToConnectedPeers();
		SendBoardSnapshotsFromHost(gameBoard, gameBoard.CreateNextNetworkSnapshotId());
	}

	public void SendBoardSnapshotsFromHost(GameBoardScene gameBoard, int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (!player.IsConnected)
			{
				continue;
			}

			if (player.IsHost)
			{
				continue;
			}

			SendBoardSnapshotToPeerFromHost(gameBoard, player.PeerId, player.SeatIndex, snapshotId);
		}
	}

	private void SendBoardSnapshotToPeerFromHost(GameBoardScene gameBoard, int peerId, int seatIndex, int snapshotId)
	{
		if (!IsHost
			|| _state != NetworkSessionState.InGame
			|| peerId <= 0
			|| seatIndex < 0)
		{
			return;
		}

		Godot.Collections.Dictionary snapshot = gameBoard.BuildNetworkBoardSnapshotForSeat(seatIndex, snapshotId);
		RpcId(peerId, MethodName.RpcBoardSnapshot, snapshot);
	}

	private void ConnectMultiplayerSignals()
	{
		if (_signalsConnected)
		{
			return;
		}

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
		_signalsConnected = true;
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

	private NetworkSessionConfig CreateConfig(NetworkTransport transport, int playerCount, bool itemMode, int winningScore, string roomName, string roomPassword)
	{
		return new NetworkSessionConfig(
			Guid.NewGuid().ToString("N"),
			RoomNameRules.Normalize(roomName),
			RoomPasswordRules.Normalize(roomPassword),
			string.Empty,
			0,
			transport,
			NetworkSessionDefaults.ClampPlayerCount(playerCount),
			itemMode,
			NetworkSessionDefaults.NormalizeWinningScore(winningScore),
			LobbyVisibility.FriendsOnly);
	}

	private void RefreshLocalIdentity()
	{
		GameSaveStore.EnsureInitialized();
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		_localDisplayName = PlayerNameRules.Normalize(profile.PlayerName);
		_localAvatarId = CharacterAvatarCatalog.NormalizeUnlockedAvatarId(profile.SelectedAvatarId);
	}

	private NetworkPlayerPublicState CreateLocalPlayer(int peerId, int seatIndex, bool isHost)
	{
		return new NetworkPlayerPublicState(
			peerId,
			GetStableUserId(),
			_localDisplayName,
			_localAvatarId,
			seatIndex,
			isHost,
			true,
			isHost,
			true);
	}

	private string GetStableUserId()
	{
		if (SteamAvailable)
		{
			GodotObject? steam = GetSteamSingleton();
			Variant steamId = steam?.Call("getSteamID") ?? default;
			if (steamId.VariantType is Variant.Type.Int or Variant.Type.Float)
			{
				return $"steam:{steamId.AsUInt64()}";
			}
		}

		return $"peer:{OS.GetUniqueId()}";
	}

	private NetworkPlayerPublicState GetLocalPlayer()
	{
		if (TryGetCachedLocalPlayer(out NetworkPlayerPublicState localPlayer))
		{
			return localPlayer;
		}

		int peerId = GetLocalPeerId();
		localPlayer = _players.FirstOrDefault(player => player.PeerId == peerId);
		return localPlayer.PeerId != 0
			? localPlayer
			: new NetworkPlayerPublicState(peerId == 0 ? 1 : peerId, GetStableUserId(), _localDisplayName, _localAvatarId, 0, TryGetCachedLocalIsHost(out bool isHost) && isHost, true, false, true);
	}

	private bool TryGetCachedLocalPlayer(out NetworkPlayerPublicState localPlayer)
	{
		int localPlayerIndex = _players.FindIndex(player => player.IsLocal);
		if (localPlayerIndex < 0)
		{
			localPlayer = default;
			return false;
		}

		localPlayer = _players[localPlayerIndex];
		return true;
	}

	private bool TryGetCachedLocalIsHost(out bool isHost)
	{
		if (!TryGetCachedLocalPlayer(out NetworkPlayerPublicState localPlayer))
		{
			isHost = false;
			return false;
		}

		isHost = localPlayer.IsHost;
		return true;
	}

	private int GetLocalPeerId()
	{
		if (Multiplayer.MultiplayerPeer == null)
		{
			return 0;
		}

		try
		{
			return Multiplayer.GetUniqueId();
		}
		catch (InvalidOperationException)
		{
			return 0;
		}
	}

	private bool TryIsServer()
	{
		if (Multiplayer.MultiplayerPeer == null)
		{
			return false;
		}

		try
		{
			return Multiplayer.IsServer();
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private void StartLanDiscoveryResponder()
	{
		StopLanDiscoveryResponder();
		_lanDiscoveryResponder = new PacketPeerUdp();
		Error bindError = _lanDiscoveryResponder.Bind(NetworkSessionDefaults.LanDiscoveryPort);
		if (bindError != Error.Ok)
		{
			GD.PushWarning($"LAN discovery responder failed: {bindError}");
			_lanDiscoveryResponder.Close();
			_lanDiscoveryResponder = null;
		}
	}

	private void StopLanDiscoveryResponder()
	{
		if (_lanDiscoveryResponder == null)
		{
			return;
		}

		_lanDiscoveryResponder.Close();
		_lanDiscoveryResponder = null;
	}

	private void StopLanDiscoveryBrowser()
	{
		if (_lanDiscoveryBrowser == null)
		{
			return;
		}

		_lanDiscoveryBrowser.Close();
		_lanDiscoveryBrowser = null;
	}

	private void PollLanDiscoveryResponder(double now)
	{
		if (_lanDiscoveryResponder == null || _state != NetworkSessionState.Lobby || _config.Transport != NetworkTransport.Lan)
		{
			return;
		}

		while (_lanDiscoveryResponder.GetAvailablePacketCount() > 0)
		{
			byte[] packet = _lanDiscoveryResponder.GetPacket();
			string message = Encoding.UTF8.GetString(packet);
			if (!string.Equals(message, "deckland_discover", StringComparison.Ordinal))
			{
				continue;
			}

			string senderIp = _lanDiscoveryResponder.GetPacketIP();
			int senderPort = _lanDiscoveryResponder.GetPacketPort();
			string payload = LanDiscoveryMessage.Build(_config, _localDisplayName, _state.ToString());
			_lanDiscoveryResponder.SetDestAddress(senderIp, senderPort);
			_lanDiscoveryResponder.PutPacket(Encoding.UTF8.GetBytes(payload));
		}

		if (now - _lastDiscoveryBroadcastAt >= DiscoveryBroadcastSeconds)
		{
			_lastDiscoveryBroadcastAt = now;
			BroadcastLanRoomAdvertisement();
		}
	}

	private void SendLanDiscoveryPing()
	{
		if (_lanDiscoveryBrowser == null)
		{
			return;
		}

		_lanDiscoveryBrowser.SetBroadcastEnabled(true);
		byte[] payload = Encoding.UTF8.GetBytes("deckland_discover");
		foreach (string broadcastAddress in LanAddressHelper.GetBroadcastAddresses())
		{
			_lanDiscoveryBrowser.SetDestAddress(broadcastAddress, NetworkSessionDefaults.LanDiscoveryPort);
			_lanDiscoveryBrowser.PutPacket(payload);
		}
	}

	private void BroadcastLanRoomAdvertisement()
	{
		if (_lanDiscoveryResponder == null || _state != NetworkSessionState.Lobby || _config.Transport != NetworkTransport.Lan)
		{
			return;
		}

		_lanDiscoveryResponder.SetBroadcastEnabled(true);
		byte[] payload = Encoding.UTF8.GetBytes(LanDiscoveryMessage.Build(_config, _localDisplayName, _state.ToString()));
		foreach (string broadcastAddress in LanAddressHelper.GetBroadcastAddresses())
		{
			_lanDiscoveryResponder.SetDestAddress(broadcastAddress, NetworkSessionDefaults.LanDiscoveryPort);
			_lanDiscoveryResponder.PutPacket(payload);
		}
	}

	private void PollLanDiscoveryBrowser(double now)
	{
		if (_lanDiscoveryBrowser == null || _state != NetworkSessionState.Searching)
		{
			return;
		}

		if (now - _lastDiscoveryBroadcastAt >= DiscoveryBroadcastSeconds)
		{
			_lastDiscoveryBroadcastAt = now;
			SendLanDiscoveryPing();
		}

		bool changed = false;
		while (_lanDiscoveryBrowser.GetAvailablePacketCount() > 0)
		{
			byte[] packet = _lanDiscoveryBrowser.GetPacket();
			string senderIp = _lanDiscoveryBrowser.GetPacketIP();
			string payload = Encoding.UTF8.GetString(packet);
			if (!LanDiscoveryMessage.TryParse(payload, senderIp, out NetworkRoomInfo room))
			{
				continue;
			}

			UpsertRoom(room);
			_roomLastSeenAt[room.SessionId] = now;
			changed = true;
		}

		for (int index = _rooms.Count - 1; index >= 0; index--)
		{
			NetworkRoomInfo room = _rooms[index];
			if (room.Transport == NetworkTransport.Lan
				&& _roomLastSeenAt.TryGetValue(room.SessionId, out double lastSeenAt)
				&& now - lastSeenAt > RoomStaleSeconds)
			{
				_rooms.RemoveAt(index);
				_roomLastSeenAt.Remove(room.SessionId);
				changed = true;
			}
		}

		if (changed)
		{
			EmitRoomListUpdated();
		}
	}

	private void UpsertRoom(NetworkRoomInfo room)
	{
		int existingIndex = _rooms.FindIndex(candidate =>
			candidate.Transport == room.Transport
			&& (candidate.Transport == NetworkTransport.Steam
				? candidate.SteamLobbyId == room.SteamLobbyId
				: candidate.SessionId == room.SessionId));
		if (existingIndex >= 0)
		{
			_rooms[existingIndex] = room;
			return;
		}

		_rooms.Add(room);
	}

	private void OnPeerConnected(long peerId)
	{
		if (!IsHost)
		{
			return;
		}

		if (_state != NetworkSessionState.Lobby)
		{
			return;
		}

		RpcId((int)peerId, MethodName.RpcRequestClientInfo);
	}

	private void OnPeerDisconnected(long peerId)
	{
		int playerIndex = _players.FindIndex(player => player.PeerId == (int)peerId);
		if (playerIndex < 0)
		{
			return;
		}

		int seatIndex = _players[playerIndex].SeatIndex;
		if (_state == NetworkSessionState.InGame)
		{
			_players[playerIndex] = _players[playerIndex] with { IsConnected = false };
			if (NetworkReconnectRules.ShouldHandOffDisconnectedSeatToAi(_state, _players[playerIndex].IsHost))
			{
				TryMarkSeatDisconnected((int)peerId);
			}

			if (GetTree().CurrentScene is GameBoardScene gameBoard)
			{
				SendBoardSnapshotsFromHost(gameBoard, gameBoard.CreateNextNetworkSnapshotId());
				gameBoard.BeginNetworkAiProxyTurnIfNeeded();
			}
		}
		else if (_state is NetworkSessionState.Starting or NetworkSessionState.LoadingScene)
		{
			CancelMatchStart("A player disconnected while loading.");
		}
		else
		{
			_players.RemoveAt(playerIndex);
			EmitPlayerListUpdated();
		}

		if (IsHost && _state == NetworkSessionState.Lobby)
		{
			BroadcastPlayerList();
		}
	}

	private void OnConnectedToServer()
	{
		RefreshLocalIdentity();
		if (_state == NetworkSessionState.Reconnecting && CanAttemptReconnectClaim())
		{
			_reconnectAttemptInFlight = false;
			RpcId(1, MethodName.RpcReconnectPlayer, _config.MatchId, _localReconnectToken, GetStableUserId());
			return;
		}

		RpcId(1, MethodName.RpcRegisterPlayer, _localDisplayName, _localAvatarId, GetStableUserId(), _pendingJoinRoomPassword);
	}

	private void OnConnectionFailed()
	{
		if (_state == NetworkSessionState.Reconnecting)
		{
			ScheduleReconnectRetry();
			return;
		}

		Fail("Connection failed.");
	}

	private void OnServerDisconnected()
	{
		if (_lobbyRejectPending)
		{
			FinalizeLobbyRejectedJoinIfNeeded();
			return;
		}

		if (_state == NetworkSessionState.Failed)
		{
			return;
		}

		if (_hostEndedMatchPending)
		{
			_reconnectAttemptInFlight = false;
			_nextReconnectAttemptAt = 0.0;
			return;
		}

		MultiplayerPeer? activePeer = Multiplayer.MultiplayerPeer;
		if (NetworkReconnectRules.ShouldIgnoreLateServerDisconnectAfterReconnect(
			_state,
			IsHost,
			activePeer != null,
			activePeer?.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected))
		{
			return;
		}

		if (NetworkReconnectRules.ShouldAttemptReconnectAfterServerDisconnect(
			_state,
			IsHost,
			CanAttemptReconnectClaim(),
			hostEndedMatch: false))
		{
			EnterReconnectState();
			return;
		}

		if (_state == NetworkSessionState.Reconnecting)
		{
			ScheduleReconnectRetry();
			return;
		}

		if (_state == NetworkSessionState.InGame)
		{
			Fail("Host disconnected.");
			return;
		}

		LeaveSession();
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
		Godot.Collections.Array players = GetPlayersVariant();
		Rpc(MethodName.RpcSyncLobby, ToDictionary(_config), players);
		RpcSyncLobby(ToDictionary(_config), players);
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

		MarkClientSceneLoaded(matchId, seatIndex);
	}

	private void MarkClientSceneLoaded(string matchId, int seatIndex)
	{
		if (_loadHandshake == null || !_loadHandshake.MarkLoaded(matchId, seatIndex))
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
			gameBoard.ApplyNetworkBoardInitialize(payload);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPlayHandCard(int handIndex, int turnSequenceGeneration)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, handIndex);
			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, handIndex);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat)
			{
				SendBoardSnapshotToPeerFromHost(
					gameBoardForRejectedSeat,
					senderPeerId,
					actorSeatIndex,
					gameBoardForRejectedSeat.CreateNextNetworkSnapshotId());
			}
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, handIndex);
			return;
		}

		if (!gameBoard.TryStartNetworkHandCardPlayFromHost(
			actorSeatIndex,
			handIndex,
			turnSequenceGeneration,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, handIndex);
			SendBoardSnapshotToPeerFromHost(gameBoard, senderPeerId, actorSeatIndex, gameBoard.CreateNextNetworkSnapshotId());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPlayItemCard(int itemHandIndex, int turnSequenceGeneration)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, itemHandIndex);
			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, itemHandIndex);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat)
			{
				SendBoardSnapshotToPeerFromHost(
					gameBoardForRejectedSeat,
					senderPeerId,
					actorSeatIndex,
					gameBoardForRejectedSeat.CreateNextNetworkSnapshotId());
			}
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, itemHandIndex);
			return;
		}

		if (!gameBoard.TryStartNetworkItemCardPlayFromHost(
			actorSeatIndex,
			itemHandIndex,
			turnSequenceGeneration,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, itemHandIndex);
			SendBoardSnapshotToPeerFromHost(gameBoard, senderPeerId, actorSeatIndex, gameBoard.CreateNextNetworkSnapshotId());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPendingItemSelection(int targetPlayerIndex, int targetHandIndex, int turnSequenceGeneration)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, -1);
			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, -1);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat)
			{
				SendBoardSnapshotToPeerFromHost(
					gameBoardForRejectedSeat,
					senderPeerId,
					actorSeatIndex,
					gameBoardForRejectedSeat.CreateNextNetworkSnapshotId());
			}
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, -1);
			return;
		}

		if (!gameBoard.TryStartNetworkPendingItemSelectionFromHost(
			actorSeatIndex,
			targetPlayerIndex,
			targetHandIndex,
			turnSequenceGeneration,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, -1);
			SendBoardSnapshotToPeerFromHost(gameBoard, senderPeerId, actorSeatIndex, gameBoard.CreateNextNetworkSnapshotId());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReconnectPlayer(string matchId, string reconnectToken, string stableUserId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (string.IsNullOrWhiteSpace(reconnectToken))
		{
			RpcId(senderPeerId, MethodName.RpcReconnectResult, false, "TokenMismatch", -1);
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			RpcId(senderPeerId, MethodName.RpcReconnectResult, false, "ActionInProgress", -1);
			return;
		}

		if (!TryFindSeatControlStateByToken(reconnectToken, out SeatControlState currentSeatState, out int stateIndex))
		{
			RpcId(senderPeerId, MethodName.RpcReconnectResult, false, "TokenMismatch", -1);
			return;
		}

		if (NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			currentSeatState.Mode,
			currentSeatState.PeerId,
			senderPeerId,
			currentSeatState.ReconnectToken,
			reconnectToken,
			currentSeatState.StableUserId,
			stableUserId))
		{
			RpcId(senderPeerId, MethodName.RpcReconnectResult, false, "ActionInProgress", currentSeatState.SeatIndex);
			return;
		}

		_reconnectClaimsBySeat.TryGetValue(currentSeatState.SeatIndex, out ReconnectClaimRecord existingClaim);
		ReconnectClaimResult result = SeatControlRules.ApplyReconnectClaim(
			_config.MatchId,
			gameBoard.IsNetworkMatchClaimable(),
			currentSeatState,
			existingClaim,
			matchId,
			reconnectToken,
			stableUserId,
			senderPeerId);
		if (!result.Accepted)
		{
			RpcId(senderPeerId, MethodName.RpcReconnectResult, false, result.RejectReason.ToString(), currentSeatState.SeatIndex);
			return;
		}

		_seatControlStates[stateIndex] = result.SeatControlState;
		_reconnectClaimsBySeat[currentSeatState.SeatIndex] = result.ClaimRecord;
		if (currentSeatState.PeerId != senderPeerId)
		{
			BindReconnectedPeerToSeat(currentSeatState.SeatIndex, senderPeerId);
		}

		RpcId(senderPeerId, MethodName.RpcReconnectResult, true, string.Empty, currentSeatState.SeatIndex);
		if (result.StateChanged)
		{
			SendReconnectCredentialsToConnectedPeers();
		}

		if (result.ShouldSendSnapshot)
		{
			SendBoardSnapshotsFromHost(gameBoard, gameBoard.CreateNextNetworkSnapshotId());
		}
	}

	private void SendRejectBoardAction(
		int peerId,
		MultiplayerBoardActionRejectReason reason,
		int turnSequenceGeneration,
		int rejectedHandIndex)
	{
		if (peerId <= 0)
		{
			return;
		}

		RpcId(peerId, MethodName.RpcRejectBoardAction, reason.ToString(), turnSequenceGeneration, rejectedHandIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRejectBoardAction(string reason, int turnSequenceGeneration, int rejectedHandIndex)
	{
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.RejectNetworkBoardAction(reason, turnSequenceGeneration, rejectedHandIndex);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcCardPlayedEvent(Godot.Collections.Dictionary payload)
	{
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.QueueNetworkCardPlayedEvent(payload);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBoardSnapshot(Godot.Collections.Dictionary snapshot)
	{
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.ApplyNetworkBoardSnapshot(snapshot);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReceiveReconnectCredentials(string matchId, int seatIndex, string reconnectToken, string stableUserId)
	{
		if (!string.Equals(GetStableUserId(), stableUserId, StringComparison.Ordinal))
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(matchId))
		{
			_config = _config with { MatchId = matchId };
		}

		_localReconnectToken = reconnectToken?.Trim() ?? string.Empty;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReconnectResult(bool accepted, string reason, int seatIndex)
	{
		_reconnectAttemptInFlight = false;
		if (!accepted)
		{
			if (_state == NetworkSessionState.Reconnecting
				&& string.Equals(reason, "ActionInProgress", StringComparison.Ordinal))
			{
				ScheduleReconnectRetry();
				return;
			}

			Fail(string.IsNullOrWhiteSpace(reason) ? "Reconnect rejected." : $"Reconnect rejected: {reason}");
			return;
		}

		UpdateLocalPlayerAfterReconnect(seatIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcHostEndedMatch(string message)
	{
		if (IsHost)
		{
			return;
		}

		_hostEndedMatchPending = true;
		_hostEndedMatchMessage = string.IsNullOrWhiteSpace(message) ? "Host disconnected." : message;
		_reconnectAttemptInFlight = false;
		_nextReconnectAttemptAt = 0.0;
		Fail(_hostEndedMatchMessage);
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
		_loadHandshake = null;
		SetState(NetworkSessionState.Lobby);
		Rpc(MethodName.RpcCancelMatchStart, message);
		RpcCancelMatchStart(message);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcCancelMatchStart(string message)
	{
		SetState(NetworkSessionState.Lobby);
		EmitSignal(SignalName.ErrorRaised, message);
	}

	private void PollReconnect(double now)
	{
		if (_state != NetworkSessionState.Reconnecting)
		{
			return;
		}

		if (Multiplayer.MultiplayerPeer != null)
		{
			return;
		}

		if (_reconnectAttemptInFlight || now < _nextReconnectAttemptAt)
		{
			return;
		}

		TryStartReconnectAttempt(now);
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
		SetState(NetworkSessionState.Lobby);
		EmitPlayerListUpdated();
	}

	private void OnSteamLobbyJoined(ulong lobbyId, int permissions, bool locked, int response)
	{
		if (_pendingSteamHostLobby || (_state != NetworkSessionState.Joining && _state != NetworkSessionState.Reconnecting))
		{
			return;
		}

		if (!NetworkReconnectRules.IsSteamLobbyJoinSuccessful(response))
		{
			if (NetworkReconnectRules.ShouldRetrySteamLobbyJoinAfterFailure(_state, response))
			{
				ScheduleReconnectRetry();
				return;
			}

			Fail(_state == NetworkSessionState.Reconnecting
				? "Reconnect failed: Steam lobby join was denied."
				: "Steam lobby join failed.");
			return;
		}

		_steamLobbyId = lobbyId;
		if (NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			_state,
			GetOriginalHostStableUserId(),
			GetSteamLobbyOwner(lobbyId)))
		{
			LeaveSteamLobbyIfNeeded();
			Fail("Reconnect failed: original host is no longer available.");
			return;
		}

		TryReadSteamLobbyConfig(lobbyId);
		if (!TryCreateSteamPeer(server: false, lobbyId, out MultiplayerPeer? steamPeer))
		{
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
			EmitSignal(SignalName.ErrorRaised, "请先离开当前房间或对局，再接受 Steam 邀请");
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

	private void InsertPlayerInSeatOrder(NetworkPlayerPublicState player)
	{
		int insertIndex = _players.FindIndex(candidate => candidate.SeatIndex > player.SeatIndex);
		if (insertIndex < 0)
		{
			_players.Add(player);
			return;
		}

		_players.Insert(insertIndex, player);
	}

	private bool TryGetPlayerIndexByPeerId(int peerId, out int playerIndex)
	{
		playerIndex = _players.FindIndex(player => player.PeerId == peerId);
		return playerIndex >= 0;
	}

	private bool TryGetPlayerIndexBySeatIndex(int seatIndex, out int playerIndex)
	{
		if (seatIndex >= 0
			&& seatIndex < _players.Count
			&& _players[seatIndex].SeatIndex == seatIndex)
		{
			playerIndex = seatIndex;
			return true;
		}

		playerIndex = _players.FindIndex(player => player.SeatIndex == seatIndex);
		return playerIndex >= 0;
	}

	private bool TryGetConnectedRemotePlayerBySeatIndex(int seatIndex, out NetworkPlayerPublicState recipient)
	{
		if (TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
		{
			NetworkPlayerPublicState candidate = _players[playerIndex];
			if (candidate.IsConnected && !candidate.IsHost)
			{
				recipient = candidate;
				return true;
			}
		}

		int fallbackIndex = _players.FindIndex(player => player.SeatIndex == seatIndex && player.IsConnected && !player.IsHost);
		if (fallbackIndex < 0)
		{
			recipient = default;
			return false;
		}

		recipient = _players[fallbackIndex];
		return true;
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

	private void Fail(string message)
	{
		SetState(NetworkSessionState.Failed);
		EmitSignal(SignalName.ErrorRaised, message);
	}

	private void RejectLobbyJoin(int senderId, string message)
	{
		RpcId(senderId, MethodName.RpcLobbyRejected, message);
		CallDeferred(nameof(DisconnectRejectedLobbyPeer), senderId);
	}

	private void DisconnectRejectedLobbyPeer(int senderId)
	{
		if (!IsHost || senderId <= 0 || Multiplayer.MultiplayerPeer == null)
		{
			return;
		}

		Multiplayer.MultiplayerPeer.DisconnectPeer(senderId);
	}

	private void FinalizeLobbyRejectedJoinIfNeeded()
	{
		if (!_lobbyRejectPending)
		{
			return;
		}

		string message = _lobbyRejectMessage;
		_lobbyRejectPending = false;
		_lobbyRejectMessage = string.Empty;
		LeaveSession();
		Fail(message);
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

	private void LeaveSteamLobbyIfNeeded()
	{
		if (_steamLobbyId == 0)
		{
			return;
		}

		GodotObject? steam = GetSteamSingleton();
		if (steam == null || !steam.HasMethod("leaveLobby"))
		{
			return;
		}

		steam.Call("leaveLobby", _steamLobbyId);
	}

	private void SetState(NetworkSessionState state)
	{
		if (_state == state)
		{
			return;
		}

		_state = state;
		RefreshSteamLobbyAdvertisedState();
		EmitSignal(SignalName.StateChanged, (int)_state);
	}

	private void EmitRoomListUpdated()
	{
		EmitSignal(SignalName.RoomListUpdated, GetRoomsVariant());
	}

	private void EmitPlayerListUpdated()
	{
		EmitSignal(SignalName.PlayerListUpdated, GetPlayersVariant());
	}

	private void EmitSeatControlStatesUpdated()
	{
		EmitSignal(SignalName.SeatControlStatesUpdated);
	}

	private static Godot.Collections.Dictionary ToDictionary(SeatControlState state)
	{
		return new Godot.Collections.Dictionary
		{
			["seat_index"] = state.SeatIndex,
			["mode"] = state.Mode.ToString(),
			["peer_id"] = state.PeerId,
			["stable_user_id"] = state.StableUserId,
			["reconnect_token"] = state.ReconnectToken,
		};
	}

	private static Godot.Collections.Dictionary ToDictionary(NetworkSessionConfig config)
	{
		return new Godot.Collections.Dictionary
		{
			["session_id"] = config.SessionId,
			["room_name"] = config.RoomName,
			["room_password"] = string.Empty,
			["match_id"] = config.MatchId,
			["match_seed"] = config.MatchSeed,
			["transport"] = config.Transport.ToString(),
			["player_count"] = config.PlayerCount,
			["item_mode"] = config.ItemMode,
			["winning_score"] = config.WinningScore,
			["lobby_visibility"] = config.LobbyVisibility.ToString(),
		};
	}

	private static Godot.Collections.Dictionary ToDictionary(NetworkPlayerPublicState player)
	{
		return new Godot.Collections.Dictionary
		{
			["peer_id"] = player.PeerId,
			["stable_user_id"] = player.StableUserId,
			["display_name"] = player.DisplayName,
			["avatar_id"] = player.AvatarId,
			["seat_index"] = player.SeatIndex,
			["is_host"] = player.IsHost,
			["is_local"] = player.IsLocal,
			["is_ready"] = player.IsReady,
			["is_connected"] = player.IsConnected,
		};
	}

	private static Godot.Collections.Dictionary ToDictionary(NetworkRoomInfo room)
	{
		return new Godot.Collections.Dictionary
		{
			["session_id"] = room.SessionId,
			["room_name"] = room.RoomName,
			["host_name"] = room.HostName,
			["address"] = room.Address,
			["transport"] = room.Transport.ToString(),
			["player_count"] = room.PlayerCount,
			["item_mode"] = room.ItemMode,
			["winning_score"] = room.WinningScore,
			["has_password"] = room.HasPassword,
			["status"] = room.Status,
			["steam_lobby_id"] = room.SteamLobbyId,
		};
	}

	private static NetworkSessionConfig ParseConfig(Godot.Collections.Dictionary dictionary)
	{
		return new NetworkSessionConfig(
			ReadString(dictionary, "session_id"),
			RoomNameRules.Normalize(ReadString(dictionary, "room_name")),
			RoomPasswordRules.Normalize(ReadString(dictionary, "room_password")),
			ReadString(dictionary, "match_id"),
			ReadLong(dictionary, "match_seed"),
			ReadEnum(dictionary, "transport", NetworkTransport.Lan),
			NetworkSessionDefaults.ClampPlayerCount(ReadInt(dictionary, "player_count", NetworkSessionDefaults.DefaultPlayerCount)),
			ReadBool(dictionary, "item_mode", NetworkSessionDefaults.DefaultItemMode),
			NetworkSessionDefaults.NormalizeWinningScore(ReadInt(dictionary, "winning_score", NetworkSessionDefaults.DefaultWinningScore)),
			ReadEnum(dictionary, "lobby_visibility", LobbyVisibility.FriendsOnly));
	}

	private NetworkSessionConfig ParseSharedConfig(Godot.Collections.Dictionary dictionary)
	{
		NetworkSessionConfig parsed = ParseConfig(dictionary);
		if (IsHost
			&& RoomPasswordRules.HasPassword(_config.RoomPassword)
			&& !RoomPasswordRules.HasPassword(parsed.RoomPassword))
		{
			return parsed with { RoomPassword = _config.RoomPassword };
		}

		return parsed;
	}

	private static NetworkPlayerPublicState ParsePlayer(Godot.Collections.Dictionary dictionary)
	{
		return new NetworkPlayerPublicState(
			ReadInt(dictionary, "peer_id", 0),
			ReadString(dictionary, "stable_user_id"),
			PlayerNameRules.Normalize(ReadString(dictionary, "display_name")),
			CharacterAvatarCatalog.NormalizeAvatarId(ReadString(dictionary, "avatar_id")),
			ReadInt(dictionary, "seat_index", 0),
			ReadBool(dictionary, "is_host", false),
			ReadBool(dictionary, "is_local", false),
			ReadBool(dictionary, "is_ready", false),
			ReadBool(dictionary, "is_connected", true));
	}

	private static string ReadString(Godot.Collections.Dictionary dictionary, string key)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsString() : string.Empty;
	}

	private static int ReadInt(Godot.Collections.Dictionary dictionary, string key, int fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) ? (int)value.AsInt64() : fallback;
	}

	private static long ReadLong(Godot.Collections.Dictionary dictionary, string key)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsInt64() : 0;
	}

	private static bool ReadBool(Godot.Collections.Dictionary dictionary, string key, bool fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsBool() : fallback;
	}

	private static TEnum ReadEnum<TEnum>(Godot.Collections.Dictionary dictionary, string key, TEnum fallback)
		where TEnum : struct, Enum
	{
		if (!dictionary.TryGetValue(key, out Variant value))
		{
			return fallback;
		}

		return Enum.TryParse(value.AsString(), ignoreCase: true, out TEnum parsed) ? parsed : fallback;
	}

	private void InitializeMatchSeatControlStates()
	{
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		foreach (NetworkPlayerPublicState player in _players)
		{
			string reconnectToken = Guid.NewGuid().ToString("N");
			SeatControlState state = SeatControlRules.CreateInitialState(player, reconnectToken);
			_seatControlStates.Add(state);
			_reconnectClaimsBySeat[player.SeatIndex] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, state);
			if (player.IsLocal)
			{
				_localReconnectToken = reconnectToken;
			}
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

	private bool TryFindSeatControlStateByToken(string reconnectToken, out SeatControlState state, out int stateIndex)
	{
		stateIndex = _seatControlStates.FindIndex(candidate => string.Equals(candidate.ReconnectToken, reconnectToken, StringComparison.Ordinal));
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

			if (!TryGetSeatControlState(player.SeatIndex, out SeatControlState seatControlState, out _))
			{
				continue;
			}

			RpcId(
				player.PeerId,
				MethodName.RpcReceiveReconnectCredentials,
				_config.MatchId,
				seatControlState.SeatIndex,
				seatControlState.ReconnectToken,
				seatControlState.StableUserId);
		}
	}

	private bool CanAttemptReconnectClaim()
	{
		return !string.IsNullOrWhiteSpace(_config.MatchId)
			&& !string.IsNullOrWhiteSpace(_localReconnectToken)
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
			GodotObject? steam = GetSteamSingleton();
			steam?.Call("joinLobby", _steamLobbyId);
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

	public void NotifyReconnectSnapshotApplied(string matchId)
	{
		if (_state != NetworkSessionState.Reconnecting
			|| !string.Equals(_config.MatchId, matchId, StringComparison.Ordinal))
		{
			return;
		}

		_reconnectAttemptInFlight = false;
		_nextReconnectAttemptAt = 0.0;
		SetState(NetworkSessionState.InGame);
	}
}
