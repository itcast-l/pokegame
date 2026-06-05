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
	public delegate void RoomListUpdatedEventHandler(Godot.Collections.Array rooms);

	[Signal]
	public delegate void PlayerListUpdatedEventHandler(Godot.Collections.Array players);

	[Signal]
	public delegate void ReadyChangedEventHandler(int seatIndex, bool isReady);

	[Signal]
	public delegate void BeginSceneLoadEventHandler(Godot.Collections.Dictionary bootstrap);

	[Signal]
	public delegate void BoardInitializeEventHandler(Godot.Collections.Dictionary payload);

	[Signal]
	public delegate void InGameDisconnectRetentionStartedEventHandler(int seatIndex, double seconds);

	[Signal]
	public delegate void InGameDisconnectRetentionFailedEventHandler(int seatIndex);

	private const string GameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const double DiscoveryBroadcastSeconds = 1.0;
	private const double RoomStaleSeconds = 5.0;

	private readonly List<NetworkPlayerPublicState> _players = new();
	private readonly List<NetworkRoomInfo> _rooms = new();
	private readonly Dictionary<string, double> _roomLastSeenAt = new();
	private readonly Dictionary<int, double> _retainedDisconnectedSeats = new();
	private PacketPeerUdp? _lanDiscoveryResponder;
	private PacketPeerUdp? _lanDiscoveryBrowser;
	private ENetMultiplayerPeer? _enetPeer;
	private MultiplayerPeer? _steamPeer;
	private NetworkSessionConfig _config = new(
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
	private ulong _steamLobbyId;
	private bool _signalsConnected;
	private bool _steamSignalsConnected;
	private bool _pendingSteamHostLobby;

	public NetworkSessionState State => _state;
	public bool SteamAvailable => GetSteamSingleton() != null && IsSteamInitialized();
	public bool IsInLobby => _state is NetworkSessionState.Lobby or NetworkSessionState.Starting or NetworkSessionState.LoadingScene;
	public bool IsHost => Multiplayer.MultiplayerPeer != null && Multiplayer.IsServer();
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
		PollSeatRetention(now);
	}

	public void HostLan(int playerCount, bool itemMode, int winningScore)
	{
		RefreshLocalIdentity();
		LeaveSession();
		SetState(NetworkSessionState.Hosting);
		_config = CreateConfig(NetworkTransport.Lan, playerCount, itemMode, winningScore);

		_enetPeer = new ENetMultiplayerPeer();
		Error error = _enetPeer.CreateServer(NetworkSessionDefaults.LanGamePort, NetworkSessionDefaults.MaxPlayers - 1);
		if (error != Error.Ok)
		{
			Fail($"LAN host failed: {error}");
			return;
		}

		Multiplayer.MultiplayerPeer = _enetPeer;
		_players.Clear();
		_players.Add(CreateLocalPlayer(peerId: 1, seatIndex: 0, isHost: true));
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
		Error bindError = _lanDiscoveryBrowser.Bind(0);
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

	public void JoinLan(string address)
	{
		if (string.IsNullOrWhiteSpace(address))
		{
			Fail("LAN address is empty.");
			return;
		}

		RefreshLocalIdentity();
		LeaveSession();
		SetState(NetworkSessionState.Joining);
		_config = CreateConfig(NetworkTransport.Lan, NetworkSessionDefaults.DefaultPlayerCount, NetworkSessionDefaults.DefaultItemMode, NetworkSessionDefaults.DefaultWinningScore);
		_enetPeer = new ENetMultiplayerPeer();
		Error error = _enetPeer.CreateClient(address.Trim(), NetworkSessionDefaults.LanGamePort);
		if (error != Error.Ok)
		{
			Fail($"LAN join failed: {error}");
			return;
		}

		Multiplayer.MultiplayerPeer = _enetPeer;
	}

	public void HostSteam(int playerCount, bool itemMode, int winningScore)
	{
		RefreshLocalIdentity();
		LeaveSession();
		if (!SteamAvailable)
		{
			Fail("Steam is not available.");
			return;
		}

		SetState(NetworkSessionState.Hosting);
		_config = CreateConfig(NetworkTransport.Steam, playerCount, itemMode, winningScore);
		_players.Clear();
		_players.Add(CreateLocalPlayer(peerId: 1, seatIndex: 0, isHost: true));
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

	public void JoinSteamLobby(ulong lobbyId)
	{
		RefreshLocalIdentity();
		LeaveSession();
		if (!SteamAvailable || lobbyId == 0)
		{
			Fail("Steam lobby is not available.");
			return;
		}

		SetState(NetworkSessionState.Joining);
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

		int localPeerId = Multiplayer.GetUniqueId();
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
		SetState(NetworkSessionState.Starting);
		Godot.Collections.Dictionary bootstrap = BuildMatchBootstrap();
		Rpc(MethodName.RpcBeginSceneLoad, bootstrap);
		RpcBeginSceneLoad(bootstrap);
	}

	public void LeaveSession()
	{
		StopLanDiscoveryResponder();
		StopLanDiscoveryBrowser();
		_loadHandshake = null;
		_retainedDisconnectedSeats.Clear();
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		_enetPeer = null;
		_steamPeer = null;
		_steamLobbyId = 0;
		_pendingSteamHostLobby = false;
		_players.Clear();
		_rooms.Clear();
		_roomLastSeenAt.Clear();
		_config = CreateConfig(NetworkTransport.Lan, NetworkSessionDefaults.DefaultPlayerCount, NetworkSessionDefaults.DefaultItemMode, NetworkSessionDefaults.DefaultWinningScore);
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
		foreach (NetworkPlayerPublicState player in _players.OrderBy(player => player.SeatIndex))
		{
			result.Add(ToDictionary(player));
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

	private NetworkSessionConfig CreateConfig(NetworkTransport transport, int playerCount, bool itemMode, int winningScore)
	{
		return new NetworkSessionConfig(
			Guid.NewGuid().ToString("N"),
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
		int peerId = Multiplayer.GetUniqueId();
		NetworkPlayerPublicState localPlayer = _players.FirstOrDefault(player => player.IsLocal);
		if (localPlayer.PeerId != 0)
		{
			return localPlayer;
		}

		localPlayer = _players.FirstOrDefault(player => player.PeerId == peerId);
		return localPlayer.PeerId != 0
			? localPlayer
			: new NetworkPlayerPublicState(peerId == 0 ? 1 : peerId, GetStableUserId(), _localDisplayName, _localAvatarId, 0, IsHost, true, false, true);
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
		}
	}

	private void SendLanDiscoveryPing()
	{
		if (_lanDiscoveryBrowser == null)
		{
			return;
		}

		_lanDiscoveryBrowser.SetBroadcastEnabled(true);
		_lanDiscoveryBrowser.SetDestAddress("255.255.255.255", NetworkSessionDefaults.LanDiscoveryPort);
		_lanDiscoveryBrowser.PutPacket(Encoding.UTF8.GetBytes("deckland_discover"));
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
			_retainedDisconnectedSeats[seatIndex] = Time.GetTicksMsec() / 1000.0;
			EmitSignal(SignalName.InGameDisconnectRetentionStarted, seatIndex, NetworkSessionDefaults.InGameSeatRetentionSeconds);
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

		if (IsHost)
		{
			BroadcastPlayerList();
		}
	}

	private void OnConnectedToServer()
	{
		RefreshLocalIdentity();
		RpcId(1, MethodName.RpcRegisterPlayer, _localDisplayName, _localAvatarId, GetStableUserId());
	}

	private void OnConnectionFailed()
	{
		Fail("Connection failed.");
	}

	private void OnServerDisconnected()
	{
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
		RpcId(1, MethodName.RpcRegisterPlayer, _localDisplayName, _localAvatarId, GetStableUserId());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRegisterPlayer(string displayName, string avatarId, string stableUserId)
	{
		if (!IsHost || _state != NetworkSessionState.Lobby)
		{
			return;
		}

		int senderId = Multiplayer.GetRemoteSenderId();
		if (_players.Any(player => player.PeerId == senderId))
		{
			return;
		}

		int seatIndex = MultiplayerLobbyRules.GetNextSeatIndex(_players, _config.PlayerCount);
		if (seatIndex < 0)
		{
			RpcId(senderId, MethodName.RpcLobbyRejected, "Lobby is full.");
			return;
		}

		_players.Add(new NetworkPlayerPublicState(
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
		Fail(message);
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
		_config = ParseConfig(config);
		_players.Clear();
		int localPeerId = Multiplayer.GetUniqueId();
		foreach (Variant variant in players)
		{
			if (variant.AsGodotDictionary() is not { } playerDictionary)
			{
				continue;
			}

			NetworkPlayerPublicState player = ParsePlayer(playerDictionary);
			_players.Add(player with { IsLocal = player.PeerId == localPeerId || (localPeerId == 0 && player.IsHost) });
		}

		SetState(NetworkSessionState.Lobby);
		EmitPlayerListUpdated();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBeginSceneLoad(Godot.Collections.Dictionary bootstrap)
	{
		_config = ParseConfig(bootstrap);
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
		return string.Join(",", _players
			.OrderBy(player => player.SeatIndex)
			.Select(player => EscapeCsvValue(player.DisplayName)));
	}

	private string BuildPlayerAvatarIdsCsv()
	{
		return string.Join(",", _players
			.OrderBy(player => player.SeatIndex)
			.Select(player => EscapeCsvValue(player.AvatarId)));
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

	private void PollSeatRetention(double now)
	{
		if (_state != NetworkSessionState.InGame)
		{
			return;
		}

		foreach (KeyValuePair<int, double> retainedSeat in _retainedDisconnectedSeats.ToArray())
		{
			if (now - retainedSeat.Value < NetworkSessionDefaults.InGameSeatRetentionSeconds)
			{
				continue;
			}

			_retainedDisconnectedSeats.Remove(retainedSeat.Key);
			EmitSignal(SignalName.InGameDisconnectRetentionFailed, retainedSeat.Key);
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
		GodotObject? steam = GetSteamSingleton();
		steam?.Call("setLobbyJoinable", _steamLobbyId, true);
		Dictionary<string, string> metadata = SteamLobbyMetadata.Build(_config, _localDisplayName, NetworkSessionState.Lobby.ToString());
		foreach (KeyValuePair<string, string> entry in metadata)
		{
			steam?.Call("setLobbyData", _steamLobbyId, entry.Key, entry.Value);
		}

		if (!TryCreateSteamPeer(server: true, lobbyId, out MultiplayerPeer? steamPeer))
		{
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
		if (_pendingSteamHostLobby || _state != NetworkSessionState.Joining)
		{
			return;
		}

		if (response != 1 || locked)
		{
			Fail("Steam lobby join failed.");
			return;
		}

		_steamLobbyId = lobbyId;
		TryReadSteamLobbyConfig(lobbyId);
		if (!TryCreateSteamPeer(server: false, lobbyId, out MultiplayerPeer? steamPeer))
		{
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
		JoinSteamLobby(lobbyId);
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
			SteamLobbyMetadata.HostNameKey,
			SteamLobbyMetadata.PlayerCountKey,
			SteamLobbyMetadata.ItemModeKey,
			SteamLobbyMetadata.WinningScoreKey,
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

	private void SetState(NetworkSessionState state)
	{
		if (_state == state)
		{
			return;
		}

		_state = state;
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

	private static Godot.Collections.Dictionary ToDictionary(NetworkSessionConfig config)
	{
		return new Godot.Collections.Dictionary
		{
			["session_id"] = config.SessionId,
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
			["host_name"] = room.HostName,
			["address"] = room.Address,
			["transport"] = room.Transport.ToString(),
			["player_count"] = room.PlayerCount,
			["item_mode"] = room.ItemMode,
			["winning_score"] = room.WinningScore,
			["status"] = room.Status,
			["steam_lobby_id"] = room.SteamLobbyId,
		};
	}

	private static NetworkSessionConfig ParseConfig(Godot.Collections.Dictionary dictionary)
	{
		return new NetworkSessionConfig(
			ReadString(dictionary, "session_id"),
			ReadString(dictionary, "match_id"),
			ReadLong(dictionary, "match_seed"),
			ReadEnum(dictionary, "transport", NetworkTransport.Lan),
			NetworkSessionDefaults.ClampPlayerCount(ReadInt(dictionary, "player_count", NetworkSessionDefaults.DefaultPlayerCount)),
			ReadBool(dictionary, "item_mode", NetworkSessionDefaults.DefaultItemMode),
			NetworkSessionDefaults.NormalizeWinningScore(ReadInt(dictionary, "winning_score", NetworkSessionDefaults.DefaultWinningScore)),
			ReadEnum(dictionary, "lobby_visibility", LobbyVisibility.FriendsOnly));
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
}
