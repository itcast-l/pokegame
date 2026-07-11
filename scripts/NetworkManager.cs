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
	private long _stateGeneration;

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
		int playerIndex = _players.FindIndex(
			player => player.PeerId == (int)peerId);
		if (playerIndex < 0)
		{
			return;
		}

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
			if (!IsHost)
			{
				return;
			}

			_players.RemoveAt(playerIndex);
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

		if (_state is NetworkSessionState.Starting or NetworkSessionState.LoadingScene)
		{
			const string message = "Host disconnected.";
			long returnGeneration = GetGenerationAfterStateTransition(NetworkSessionState.Idle);
			LeaveSession();
			EmitSignal(SignalName.ErrorRaised, message);
			CallDeferred(
				nameof(ReturnToMultiplayerLobbyScene),
				(int)NetworkSessionState.Idle,
				returnGeneration);
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

	private void SetState(NetworkSessionState state)
	{
		if (_state == state)
		{
			return;
		}

		_state = state;
		_stateGeneration++;
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

}
