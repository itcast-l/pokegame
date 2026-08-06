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
	public delegate void ErrorRaisedEventHandler(string localizationKey);

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

	[Signal]
	public delegate void SessionLeaveCompletedEventHandler(int reason);

	private const string GameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const double DiscoveryBroadcastSeconds = 1.0;
	private const double RoomStaleSeconds = 5.0;
	private const double ReconnectRetrySeconds = 2.0;
	private const double ClientReconnectWindowSeconds = 10.0;
	private const double SeatCredentialRetrySeconds = 1.0;
	private const double LocalReadyIntentRetrySeconds = 1.0;
	private const double HostEndedMatchBroadcastGraceSeconds = 0.15;
	private static readonly byte[] LanDiscoveryPingPayload = Encoding.UTF8.GetBytes("deckland_discover");

	private readonly List<NetworkPlayerPublicState> _players = new();
	private readonly List<NetworkRoomInfo> _rooms = new();
	private readonly Dictionary<string, double> _roomLastSeenAt = new();
	private readonly List<SeatControlState> _seatControlStates = new();
	private readonly Dictionary<int, ReconnectClaimRecord> _reconnectClaimsBySeat = new();
	private readonly Dictionary<int, string> _issuedSeatCredentialsBySeat = new();
	private readonly Dictionary<int, bool> _pendingLobbyReadyByPeerId = new();
	private readonly NetworkPeerRateLimiter _peerRateLimiter = new();
	private readonly Dictionary<int, double> _lastCorrectiveSnapshotAtByPeer = new();
	private readonly List<string> _lanDiscoveryBroadcastAddresses = new();
	private byte[] _lanRoomAdvertisementPayload = Array.Empty<byte>();
	private LanDiscoverySocket? _lanDiscoveryResponder;
	private LanDiscoverySocket? _lanDiscoveryBrowser;
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
	private string _localIdentityHint = string.Empty;
	private string _localPlayerSessionId = string.Empty;
	private string _localSeatCredential = string.Empty;
	private long _localSeatCredentialGeneration;
	private string _pendingLocalSeatCredential = string.Empty;
	private long _pendingLocalSeatCredentialGeneration;
	private string _lastLanJoinAddress = string.Empty;
	private string _pendingJoinRoomPassword = string.Empty;
	private ulong _steamLobbyId;
	private bool _signalsConnected;
	private bool _steamSignalsConnected;
	private ISteamPlatformAdapter? _steamPlatform;
	private bool _pendingSteamHostLobby;
	private double _nextReconnectAttemptAt;
	private double _lastSeatCredentialRetryAt = -1.0;
	private double _lastLocalReadyIntentSentAt = -1.0;
	private bool? _pendingLocalReadyIntent;
	private bool _reconnectAttemptInFlight;
	private double _reconnectAttemptStartedAt = -1.0;
	private double _reconnectWindowStartedAt = -1.0;
	private bool _lobbyRejectPending;
	private string _lobbyRejectLocalizationKey = string.Empty;
	private bool _hostEndedMatchPending;
	private string _hostEndedMatchLocalizationKey = string.Empty;
	private bool _hostEndedMatchBroadcasted;
	private bool _migrationFailureReturnPending;
	private bool _hostDisconnectReturnPending;
	private bool _hostDisconnectErrorPublished;
	private string _pendingLobbyErrorLocalizationKey = string.Empty;
	private long _stateGeneration;
	private long _authorityEpoch;

	public NetworkSessionState State => _state;
	public bool SteamAvailable => _steamPlatform is { IsAvailable: true, IsInitialized: true };
	public bool IsInLobby => _state is NetworkSessionState.Lobby or NetworkSessionState.Starting or NetworkSessionState.LoadingScene;
	public bool IsHost => TryGetCachedLocalIsHost(out bool isHost) ? isHost : TryIsServer();
	public int LocalSeatIndex => GetLocalPlayer().SeatIndex;
	public NetworkSessionConfig CurrentConfig => _config;
	public bool IsMigrationFailureReturnPending => _migrationFailureReturnPending;
	public bool IsHostDisconnectReturnPending => _hostDisconnectReturnPending;
	internal long AuthorityEpoch => _authorityEpoch;

	public string ConsumePendingLobbyErrorLocalizationKey()
	{
		string localizationKey = _pendingLobbyErrorLocalizationKey;
		_pendingLobbyErrorLocalizationKey = string.Empty;
		return localizationKey;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		InitializeNetworkMaintenanceScheduler();
		InitializeSteamPlatformAdapter();
		ConnectMultiplayerSignals();
		TryConnectSteamSignals();
		RefreshLocalIdentity();
		SetState(NetworkSessionState.Idle);
	}

	public override void _ExitTree()
	{
		StopNetworkMaintenanceScheduler();
		AbortSessionCloseAndCleanup(NetworkSessionCloseReason.ApplicationExit);
	}

	public void HostLan(int playerCount, bool itemMode, int winningScore, string roomName, string roomPassword)
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
		InitializeLocalHostSessionIdentity();
		SetState(NetworkSessionState.Hosting);
		_config = CreateConfig(NetworkTransport.Lan, playerCount, itemMode, winningScore, roomName, roomPassword);

		_enetPeer = new ENetMultiplayerPeer();
		Error error = _enetPeer.CreateServer(NetworkSessionDefaults.LanGamePort, NetworkSessionDefaults.MaxPlayers - 1);
		if (error != Error.Ok)
		{
			CloseSessionAndFail(NetworkSessionCloseReason.ConnectionCreationFailed, NetworkUserErrorCategory.Lan, $"LAN host failed: {error}");
			return;
		}

		Multiplayer.MultiplayerPeer = _enetPeer;
		_players.Clear();
		InsertPlayerInSeatOrder(CreateLocalPlayer(peerId: 1, seatIndex: 0, isHost: true));
		EnsureLobbySeatControlStates();
		StartLanDiscoveryResponder();
		SetState(NetworkSessionState.Lobby);
		CommitAuthorityCheckpointFromLobby();
		EmitPlayerListUpdated();
	}

	public void SearchLan()
	{
		if (_state == NetworkSessionState.Leaving)
		{
			return;
		}
		StopLanDiscoveryBrowser();
		_rooms.Clear();
		_roomLastSeenAt.Clear();
		bool bound = LanDiscoverySocket.TryBind(
			NetworkSessionDefaults.LanDiscoveryPort,
			MaxLanDiscoveryPacketBytes,
			out _lanDiscoveryBrowser,
			out string bindError);
		if (!bound)
		{
			bound = LanDiscoverySocket.TryBind(
				0,
				MaxLanDiscoveryPacketBytes,
				out _lanDiscoveryBrowser,
				out bindError);
		}
		if (!bound || _lanDiscoveryBrowser == null)
		{
			_lanDiscoveryBrowser?.Dispose();
			_lanDiscoveryBrowser = null;
			CloseSessionAndFail(NetworkSessionCloseReason.ConnectionCreationFailed, NetworkUserErrorCategory.Lan, $"LAN search failed: {bindError}");
			return;
		}

		RefreshLanDiscoveryBroadcastAddresses();
		SetState(NetworkSessionState.Searching);
		SendLanDiscoveryPing();
		EmitRoomListUpdated();
	}

	public string GetLocalLanAddressSummary()
	{
		IReadOnlyList<string> addresses = LanAddressHelper.GetLocalIpv4Addresses();
		return addresses.Count == 0 ? string.Empty : string.Join(" / ", addresses);
	}

	public void JoinLan(string address, string roomPassword)
	{
		if (!PrepareForNewSessionStart())
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(address))
		{
			Fail(NetworkUserErrorCategory.Lan, "LAN address is empty.");
			return;
		}
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		RefreshLocalIdentity();
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
			CloseSessionAndFail(NetworkSessionCloseReason.ConnectionCreationFailed, NetworkUserErrorCategory.Lan, $"LAN join failed: {error}");
			return;
		}

		Multiplayer.MultiplayerPeer = _enetPeer;
	}

	public void RequestPlayHandCard(int handIndex, int turnSequenceGeneration)
	{
		RequestPlayHandCard(handIndex, turnSequenceGeneration, NextClientActionId());
	}

	public void RequestPlayHandCard(int handIndex, int turnSequenceGeneration, long clientActionId)
	{
		if (_state != NetworkSessionState.InGame || IsHost)
		{
			return;
		}

		NetworkRpcContext context = GetCurrentRpcContext();
		RpcId(1, MethodName.RpcRequestPlayHandCard, context.SessionId, context.MatchId, context.AuthorityEpoch, handIndex, turnSequenceGeneration, clientActionId);
	}

	public void RequestPlayItemCard(int itemHandIndex, int turnSequenceGeneration)
	{
		RequestPlayItemCard(itemHandIndex, turnSequenceGeneration, NextClientActionId());
	}

	public void RequestPlayItemCard(int itemHandIndex, int turnSequenceGeneration, long clientActionId)
	{
		if (_state != NetworkSessionState.InGame || IsHost)
		{
			return;
		}

		NetworkRpcContext context = GetCurrentRpcContext();
		RpcId(1, MethodName.RpcRequestPlayItemCard, context.SessionId, context.MatchId, context.AuthorityEpoch, itemHandIndex, turnSequenceGeneration, clientActionId);
	}

	public void RequestPendingItemSelection(int selectionIndex, int turnSequenceGeneration)
	{
		RequestPendingItemSelection(selectionIndex, turnSequenceGeneration, NextClientActionId());
	}

	public void RequestPendingItemSelection(int selectionIndex, int turnSequenceGeneration, long clientActionId)
	{
		if (_state != NetworkSessionState.InGame || IsHost)
		{
			return;
		}

		NetworkRpcContext context = GetCurrentRpcContext();
		RpcId(1, MethodName.RpcRequestPendingItemSelection, context.SessionId, context.MatchId, context.AuthorityEpoch, selectionIndex, turnSequenceGeneration, clientActionId);
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
		string normalizedPassword = RoomPasswordRules.Normalize(roomPassword);
		return new NetworkSessionConfig(
			Guid.NewGuid().ToString("N"),
			RoomNameRules.Normalize(roomName),
			string.Empty,
			string.Empty,
			0,
			transport,
			NetworkSessionDefaults.ClampPlayerCount(playerCount),
			itemMode,
			NetworkSessionDefaults.NormalizeWinningScore(winningScore),
			LobbyVisibility.FriendsOnly)
		{
			RoomPasswordVerifier = RoomPasswordRules.CreateVerifier(normalizedPassword),
			RoomPasswordRequired = !string.IsNullOrEmpty(normalizedPassword),
		};
	}

	private void RefreshLocalIdentity()
	{
		GameSaveStore.EnsureInitialized();
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		_localDisplayName = PlayerNameRules.Normalize(profile.PlayerName);
		_localAvatarId = CharacterAvatarCatalog.NormalizeUnlockedAvatarId(profile.SelectedAvatarId);
		_localIdentityHint = BuildIdentityHint();
	}

	private void InitializeLocalHostSessionIdentity()
	{
		InitializeHostMigrationSecurity();
		string testSessionId = OS.GetEnvironment("CODEX_TEST_PLAYER_SESSION_ID").Trim();
		_localPlayerSessionId = !string.IsNullOrWhiteSpace(testSessionId)
			&& testSessionId.Length <= NetworkProtocol.MaxPlayerIdLength
			? testSessionId
			: SeatCredentialRules.IssuePlayerSessionId();
		_localSeatCredential = SeatCredentialRules.IssueCredential();
		_localSeatCredentialGeneration = SeatCredentialRules.InitialGeneration;
		_pendingLocalSeatCredential = string.Empty;
		_pendingLocalSeatCredentialGeneration = 0;
		_issuedSeatCredentialsBySeat[0] = _localSeatCredential;
	}

	private NetworkPlayerPublicState CreateLocalPlayer(int peerId, int seatIndex, bool isHost)
	{
		return new NetworkPlayerPublicState(
			peerId,
			GetPlayerSessionId(),
			_localDisplayName,
			_localAvatarId,
			seatIndex,
			isHost,
			true,
			isHost,
			true)
		{
			CredentialsAcknowledged = isHost,
		};
	}

	private string GetPlayerSessionId()
	{
		return _localPlayerSessionId;
	}

	private string BuildIdentityHint()
	{
		string testIdentityHint = OS.GetEnvironment("CODEX_TEST_STABLE_USER_ID").Trim();
		if (!string.IsNullOrWhiteSpace(testIdentityHint))
		{
			return testIdentityHint[..Math.Min(testIdentityHint.Length, NetworkProtocol.MaxPlayerIdLength)];
		}

		if (SteamAvailable)
		{
			if (_steamPlatform?.TryGetLocalSteamId(out long steamId) == true)
			{
				return $"steam:{steamId}";
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
			: new NetworkPlayerPublicState(peerId == 0 ? 1 : peerId, GetPlayerSessionId(), _localDisplayName, _localAvatarId, 0, TryGetCachedLocalIsHost(out bool isHost) && isHost, true, false, true)
			{
				CredentialsAcknowledged = isHost,
			};
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
		if (IsHost)
		{
			ConfigureLanPeerTimeout((int)peerId);
		}
		if ((HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost)
			|| (IsHost && _state == NetworkSessionState.InGame))
		{
			GD.Print($"NETWORK_PEER_CONNECTED peer={peerId} state={_state} local_peer={GetLocalPeerId()}");
		}

		if (!IsHost)
		{
			return;
		}

		// In-game arrivals must prove their existing seat credential; the room
		// password is intentionally discarded after the initial join handshake.
		bool resumesAuthenticatedSeat = _state == NetworkSessionState.InGame
			|| (HostMigrationCapabilityAvailable && _lobbyMigrationRecoveryState is not null);
		BeginPeerSecurityHandshake((int)peerId, resumesAuthenticatedSeat);

		if (_state != NetworkSessionState.Lobby)
		{
			return;
		}
		if (HostMigrationCapabilityAvailable && _lobbyMigrationRecoveryState is not null)
		{
			// Migrated lobby peers authenticate through the recovery claim path;
			// do not race that claim with a fresh join request.
			return;
		}

	}

	private void OnPeerDisconnected(long peerId)
	{
		_pendingLobbyReadyByPeerId.Remove((int)peerId);
		_peerRateLimiter.ClearPeer((int)peerId);
		_lastCorrectiveSnapshotAtByPeer.Remove((int)peerId);
		ClearPeerMigrationSecurity((int)peerId);
		ClearMigrationCandidateLatency((int)peerId);
		if ((HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost)
			|| (IsHost && _state == NetworkSessionState.InGame))
		{
			GD.Print($"NETWORK_PEER_DISCONNECTED peer={peerId} state={_state} local_peer={GetLocalPeerId()}");
		}

		int playerIndex = _players.FindIndex(
			player => player.PeerId == (int)peerId);
		if (playerIndex < 0)
		{
			return;
		}

		if (_state == NetworkSessionState.InGame)
		{
			HandleHotStandbyDisconnected(_players[playerIndex].SeatIndex);
			_players[playerIndex] = _players[playerIndex] with { IsConnected = false };
			if (NetworkReconnectRules.ShouldHandOffDisconnectedSeatToAi(_state, _players[playerIndex].IsHost))
			{
				TryMarkSeatDisconnected((int)peerId);
			}
			RemoveMigrationRecoverySeatAfterDisconnect(_players[playerIndex].SeatIndex);

			if (TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoard))
			{
				SendBoardSnapshotsFromHost(gameBoard.CreateNextSnapshotId());
				gameBoard.BeginAiProxyTurnIfNeeded();
			}
		}
		else if (_state is NetworkSessionState.Starting or NetworkSessionState.LoadingScene)
		{
			if (!IsHost)
			{
				return;
			}

			_players.RemoveAt(playerIndex);
			CancelMatchStart(NetworkUserErrorCategory.SceneLoad, "A player disconnected while loading.");
		}
		else if (HostMigrationCapabilityAvailable
			&& _state == NetworkSessionState.Lobby
			&& _lobbyMigrationRecoveryState is not null)
		{
			int seatIndex = _players[playerIndex].SeatIndex;
			_players[playerIndex] = _players[playerIndex] with
			{
				PeerId = 0,
				IsConnected = false,
			};
			if (TryGetSeatControlState(seatIndex, out SeatControlState control, out int controlIndex))
			{
				_seatControlStates[controlIndex] = SeatCredentialRules.CancelPendingRotation(control) with
				{
					Mode = SeatControlMode.HumanReclaimPending,
					PeerId = 0,
				};
			}
			_issuedSeatCredentialsBySeat.Remove(seatIndex);
			CommitAuthorityCheckpointFromLobby();
		}
		else if (_state == NetworkSessionState.Lobby && IsHost)
		{
			HandleHotStandbyDisconnected(_players[playerIndex].SeatIndex);
			_players.RemoveAt(playerIndex);
			CompactLobbySeatMappings(_players);
			EmitPlayerListUpdated();
			EmitSeatControlStatesUpdated();
			CommitAuthorityCheckpointFromLobby();
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
		ConfigureLanPeerTimeout(1);
		PrepareLocalMigrationSecurity();
		if (HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost)
		{
			GD.Print($"MIGRATION_CONNECTED_TO_SERVER peer={GetLocalPeerId()} player_session={GetPlayerSessionId().Length} credential_generation={_localSeatCredentialGeneration} target_epoch={_networkMigrationPlan?.TargetEpoch}");
			_migrationPeerAttemptInFlight = false;
			_migrationPeerAttemptStartedAt = -1.0;
		}

	}

	private void ContinueClientAuthenticationAfterSecurityHandshake()
	{
		if (!_localSecurityHandshakeAccepted)
		{
			return;
		}

		if (HostMigrationCapabilityAvailable
			&& _state == NetworkSessionState.MigratingHost
			&& _networkMigrationPlan is { Phase: NetworkAuthorityPhase.Lobby } lobbyPlan)
		{
			if (OS.GetEnvironment("CODEX_LAN_INJECT_EXPIRED_HOST_CREDENTIAL") == "1")
			{
				RpcId(
					1,
					MethodName.RpcClaimMigratedLobbySeat,
					lobbyPlan.SessionId,
					lobbyPlan.TargetEpoch,
					lobbyPlan.SourcePlayerSessionId,
					_localSeatCredentialGeneration,
					string.Empty);
				GD.Print("MIGRATION_EXPIRED_HOST_CREDENTIAL_INJECTED");
				return;
			}

			RpcId(
				1,
				MethodName.RpcClaimMigratedLobbySeat,
				lobbyPlan.SessionId,
				lobbyPlan.TargetEpoch,
				GetPlayerSessionId(),
				_localSeatCredentialGeneration,
				_localSeatCredential);
			return;
		}

		if ((_state == NetworkSessionState.Reconnecting
				|| (HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost))
			&& CanAttemptReconnectClaim())
		{
			_reconnectAttemptInFlight = false;
			_reconnectAttemptStartedAt = -1.0;
			long reconnectEpoch = GetCurrentAuthorityEpoch();
			if (HostMigrationCapabilityAvailable
				&& _state == NetworkSessionState.MigratingHost
				&& _networkMigrationPlan is { } migrationPlan)
			{
				reconnectEpoch = migrationPlan.TargetEpoch;
				if (OS.GetEnvironment("CODEX_LAN_INJECT_OLD_EPOCH") == "1")
				{
					DebugSendStaleMigrationPlan();
				}
			}

			RpcId(
				1,
				MethodName.RpcReconnectPlayer,
				_config.SessionId,
				_config.MatchId,
				reconnectEpoch,
				GetPlayerSessionId(),
				_localSeatCredentialGeneration,
				_localSeatCredential);
			return;
		}

		RpcId(1, MethodName.RpcRegisterPlayer, _localDisplayName, _localAvatarId, _localIdentityHint);
	}

	private void OnConnectionFailed()
	{
		if (HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost)
		{
			GD.Print($"MIGRATION_CONNECTION_FAILED endpoint={GetMigrationEndpointLog(_networkMigrationPlan)}");
			HandleMigrationConnectionFailure();
			return;
		}

		if (_state == NetworkSessionState.Reconnecting)
		{
			if (HostMigrationCapabilityAvailable
				&& _config.Transport == NetworkTransport.Lan
				&& _networkMigrationPlan is { } migrationPlan
				&& NetworkMigrationRules.IsReadyForMigration(_migrationCoordinatorState)
				&& NetworkMigrationRules.IsPlanVisibleToClients(migrationPlan))
			{
				StartMigrationAttemptsAfterHostDisconnect(migrationPlan);
				return;
			}
			ScheduleReconnectRetry();
			return;
		}

		CloseSessionAndFail(NetworkSessionCloseReason.ConnectionCreationFailed, NetworkUserErrorCategory.Connection, "Connection failed.");
	}

	private void OnServerDisconnected()
	{
		if (_lobbyRejectPending)
		{
			FinalizeLobbyRejectedJoinIfNeeded();
			return;
		}

		if (IsHost
			|| _state is NetworkSessionState.Idle
				or NetworkSessionState.Leaving
				or NetworkSessionState.Failed)
		{
			return;
		}

		if (_hostEndedMatchPending)
		{
			HandleHostDisconnected();
			return;
		}
		if (TryAdvancePlannedMigrationAfterSourceDisconnect())
		{
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

		HandleHostDisconnected();
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

	private void Fail(NetworkUserErrorCategory category, string technicalDetails = "")
	{
		if (!string.IsNullOrWhiteSpace(technicalDetails))
		{
			GD.PushWarning($"Network user error category={category} detail={technicalDetails}");
		}
		SetState(NetworkSessionState.Failed);
		EmitSignal(SignalName.ErrorRaised, NetworkUserErrorRules.GetLocalizationKey(category));
	}

	private void RejectLobbyJoin(int senderId, NetworkUserErrorCategory category, string technicalDetails = "")
	{
		if (!string.IsNullOrWhiteSpace(technicalDetails))
		{
			GD.PushWarning($"Lobby join rejected category={category} detail={technicalDetails}");
		}
		NetworkRpcContext context = GetCurrentRpcContext();
		RpcId(
			senderId,
			MethodName.RpcLobbyRejected,
			context.SessionId,
			context.MatchId,
			context.AuthorityEpoch,
			NetworkUserErrorRules.GetLocalizationKey(category));
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

		string localizationKey = _lobbyRejectLocalizationKey;
		_lobbyRejectPending = false;
		_lobbyRejectLocalizationKey = string.Empty;
		_ = RequestSessionCloseAsync(NetworkSessionCloseReason.ProtocolRejected);
		SetState(NetworkSessionState.Failed);
		EmitSignal(SignalName.ErrorRaised, localizationKey);
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
		RescheduleNetworkMaintenance(immediate: true);
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

	private bool AllowPeerRequest(NetworkRequestCategory category, bool requireRegisteredPeer = true)
	{
		int peerId = Multiplayer.GetRemoteSenderId();
		double now = Time.GetTicksMsec() / 1000.0;
		if (!_peerRateLimiter.TryConsume(peerId, category, now, out bool shouldDisconnect))
		{
			if (shouldDisconnect)
			{
				Multiplayer.MultiplayerPeer?.DisconnectPeer(peerId);
			}
			else if (_peerRateLimiter.ShouldLogWarning(peerId, category, now))
			{
				GD.PushWarning($"Network request rate limit exceeded for peer {peerId}, category {category}.");
			}
			return false;
		}

		return !requireRegisteredPeer || TryGetPlayerIndexByPeerId(peerId, out _);
	}

}
