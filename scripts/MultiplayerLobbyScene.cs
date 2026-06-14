using System.Collections.Generic;
using Godot;

public partial class MultiplayerLobbyScene : Control
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";

	[Export]
	public PackedScene RoomRowScene { get; set; } = default!;

	[Export]
	public PackedScene PlayerRowScene { get; set; } = default!;

	[Export]
	public Label StatusLabel { get; set; } = default!;

	[Export]
	public Label ToastLabel { get; set; } = default!;

	[Export]
	public LineEdit RoomNameInput { get; set; } = default!;

	[Export]
	public LineEdit RoomPasswordInput { get; set; } = default!;

	[Export]
	public Label LanHostInfoLabel { get; set; } = default!;

	[Export]
	public Label SteamStatusLabel { get; set; } = default!;

	[Export]
	public OptionButton PlayerCountOption { get; set; } = default!;

	[Export]
	public OptionButton ItemModeOption { get; set; } = default!;

	[Export]
	public OptionButton WinningScoreOption { get; set; } = default!;

	[Export]
	public VBoxContainer RoomListContainer { get; set; } = default!;

	[Export]
	public VBoxContainer PlayerListContainer { get; set; } = default!;

	[Export]
	public Label EmptyRoomsLabel { get; set; } = default!;

	[Export]
	public Label EmptyPlayersLabel { get; set; } = default!;

	[Export]
	public Button LanHostButton { get; set; } = default!;

	[Export]
	public Button LanSearchButton { get; set; } = default!;

	[Export]
	public Button SteamHostButton { get; set; } = default!;

	[Export]
	public Button SteamRefreshButton { get; set; } = default!;

	[Export]
	public Button ReadyButton { get; set; } = default!;

	[Export]
	public Button StartButton { get; set; } = default!;

	[Export]
	public Button LeaveSessionButton { get; set; } = default!;

	[Export]
	public Button BackButton { get; set; } = default!;

	private readonly List<MultiplayerLobbyRoomRow> _roomRows = new();
	private readonly List<MultiplayerLobbyPlayerRow> _playerRows = new();
	private NetworkManager _network = default!;
	private Godot.Collections.Array _latestPlayers = new();
	private bool _localReady;
	private bool _buttonSignalsConnected;
	private Tween? _toastTween;

	public override void _Ready()
	{
		GameInputActions.EnsureRegistered();
		Localization.EnsureLoaded();
		if (!ValidateExportBindings())
		{
			return;
		}

		_network = EnsureNetworkManager();
		InitializeSceneDefaults();
		MainMenuInteractiveStyle.ApplyOptionLikeButton(PlayerCountOption);
		MainMenuInteractiveStyle.ApplyOptionLikeButton(ItemModeOption);
		MainMenuInteractiveStyle.ApplyOptionLikeButton(WinningScoreOption);
		ConnectButtonSignals();
		ConnectNetworkSignals();
		RefreshState((int)_network.State);
		RefreshRooms(_network.GetRoomsVariant());
		RefreshPlayers(_network.GetPlayersVariant());
	}

	public override void _ExitTree()
	{
		if (_network != null && GodotObject.IsInstanceValid(_network))
		{
			_network.StateChanged -= RefreshState;
			_network.RoomListUpdated -= RefreshRooms;
			_network.PlayerListUpdated -= RefreshPlayers;
			_network.ReadyChanged -= OnReadyChanged;
			_network.ErrorRaised -= ShowToast;
		}

		DisconnectButtonSignals();
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (GameInputActions.IsCancelPressed(inputEvent))
		{
			ReturnToMainMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	private bool ValidateExportBindings()
	{
		if (RoomRowScene == null
			|| PlayerRowScene == null
			|| StatusLabel == null
			|| ToastLabel == null
			|| RoomNameInput == null
			|| RoomPasswordInput == null
			|| LanHostInfoLabel == null
			|| SteamStatusLabel == null
			|| PlayerCountOption == null
			|| ItemModeOption == null
			|| WinningScoreOption == null
			|| RoomListContainer == null
			|| PlayerListContainer == null
			|| EmptyRoomsLabel == null
			|| EmptyPlayersLabel == null
			|| LanHostButton == null
			|| LanSearchButton == null
			|| SteamHostButton == null
			|| SteamRefreshButton == null
			|| ReadyButton == null
			|| StartButton == null
			|| LeaveSessionButton == null
			|| BackButton == null)
		{
			GD.PushError("MultiplayerLobbyScene has missing exported UI bindings. Open the scene and assign every exported field.");
			return false;
		}

		return true;
	}

	private NetworkManager EnsureNetworkManager()
	{
		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (networkManager != null)
		{
			return networkManager;
		}

		GD.PushWarning("NetworkManager autoload is missing; creating a temporary runtime instance for the lobby.");
		networkManager = new NetworkManager { Name = "NetworkManager" };
		GetTree().Root.AddChild(networkManager);
		return networkManager;
	}

	private void InitializeSceneDefaults()
	{
		RoomNameInput.Text = RoomNameRules.Normalize(RoomNameInput.Text);
		SteamStatusLabel.Text = _network.SteamAvailable ? "Steam 可用" : "Steam 不可用";
	}

	private void ConnectButtonSignals()
	{
		BackButton.Pressed += ReturnToMainMenu;
		LanHostButton.Pressed += HostLan;
		LanSearchButton.Pressed += SearchLan;
		SteamHostButton.Pressed += HostSteam;
		SteamRefreshButton.Pressed += RefreshSteamLobbies;
		ReadyButton.Pressed += ToggleReady;
		StartButton.Pressed += StartMatch;
		LeaveSessionButton.Pressed += LeaveSession;
		_buttonSignalsConnected = true;
	}

	private void DisconnectButtonSignals()
	{
		if (!_buttonSignalsConnected)
		{
			return;
		}

		BackButton.Pressed -= ReturnToMainMenu;
		LanHostButton.Pressed -= HostLan;
		LanSearchButton.Pressed -= SearchLan;
		SteamHostButton.Pressed -= HostSteam;
		SteamRefreshButton.Pressed -= RefreshSteamLobbies;
		ReadyButton.Pressed -= ToggleReady;
		StartButton.Pressed -= StartMatch;
		LeaveSessionButton.Pressed -= LeaveSession;
		_buttonSignalsConnected = false;
	}

	private void ConnectNetworkSignals()
	{
		_network.StateChanged += RefreshState;
		_network.RoomListUpdated += RefreshRooms;
		_network.PlayerListUpdated += RefreshPlayers;
		_network.ReadyChanged += OnReadyChanged;
		_network.ErrorRaised += ShowToast;
	}

	private int SelectedPlayerCount()
	{
		return PlayerCountOption.GetSelectedId();
	}

	private bool SelectedItemMode()
	{
		return ItemModeOption.GetSelectedId() == 1;
	}

	private int SelectedWinningScore()
	{
		return WinningScoreOption.GetSelectedId();
	}

	private string SelectedRoomPassword()
	{
		return RoomPasswordRules.Normalize(RoomPasswordInput.Text);
	}

	private bool TryGetSelectedRoomName(out string roomName)
	{
		roomName = RoomNameInput.Text.Trim();
		if (RoomNameRules.IsValid(roomName))
		{
			return true;
		}

		ShowToast($"房间名仅支持中英文、数字、空格、-、_，最多 {RoomNameRules.MaxLength} 个字");
		RoomNameInput.GrabFocus();
		return false;
	}

	private void HostLan()
	{
		if (!TryGetSelectedRoomName(out string roomName))
		{
			return;
		}

		_localReady = true;
		_network.HostLan(SelectedPlayerCount(), SelectedItemMode(), SelectedWinningScore(), roomName, SelectedRoomPassword());
	}

	private void HostSteam()
	{
		if (!TryGetSelectedRoomName(out string roomName))
		{
			return;
		}

		_localReady = true;
		_network.HostSteam(SelectedPlayerCount(), SelectedItemMode(), SelectedWinningScore(), roomName, SelectedRoomPassword());
	}

	private void SearchLan()
	{
		_network.SearchLan();
	}

	private void RefreshSteamLobbies()
	{
		_network.RefreshSteamLobbies();
	}

	private void ToggleReady()
	{
		_localReady = !_localReady;
		_network.SetLocalReady(_localReady);
		ReadyButton.Text = _localReady ? "取消准备" : "准备";
	}

	private void StartMatch()
	{
		_network.StartMatch();
	}

	private void LeaveSession()
	{
		_network.LeaveSession();
	}

	private void RefreshState(int stateValue)
	{
		var state = (NetworkSessionState)stateValue;
		StatusLabel.Text = state.ToString();
		SteamStatusLabel.Text = _network.SteamAvailable ? "Steam 可用" : "Steam 不可用";

		bool steamEnabled = _network.SteamAvailable;
		bool canConfigureSession = state is NetworkSessionState.Idle or NetworkSessionState.Searching or NetworkSessionState.Failed;
		bool inBusyOrJoinedState = state is NetworkSessionState.Hosting
			or NetworkSessionState.Joining
			or NetworkSessionState.Lobby
			or NetworkSessionState.Starting
			or NetworkSessionState.LoadingScene
			or NetworkSessionState.InGame;

		LanHostButton.Disabled = !canConfigureSession;
		LanSearchButton.Disabled = inBusyOrJoinedState;
		SteamHostButton.Disabled = !steamEnabled || !canConfigureSession;
		SteamRefreshButton.Disabled = !steamEnabled || inBusyOrJoinedState;
		RoomNameInput.Editable = canConfigureSession;
		RoomPasswordInput.Editable = canConfigureSession;
		PlayerCountOption.Disabled = !canConfigureSession;
		ItemModeOption.Disabled = !canConfigureSession;
		WinningScoreOption.Disabled = !canConfigureSession;
		ReadyButton.Disabled = state != NetworkSessionState.Lobby;
		LeaveSessionButton.Disabled = state is NetworkSessionState.Idle or NetworkSessionState.Searching or NetworkSessionState.Failed;
		LanHostInfoLabel.Text = state == NetworkSessionState.Lobby && _network.CurrentConfig.Transport == NetworkTransport.Lan
			? $"本机局域网地址: {_network.GetLocalLanAddressSummary()}"
			: "本机局域网地址将在建房后显示";
		UpdateStartButtonState();
	}

	private void RefreshRooms(Godot.Collections.Array rooms)
	{
		ClearRoomRows();
		EmptyRoomsLabel.Visible = rooms.Count == 0;
		if (rooms.Count == 0)
		{
			return;
		}

		foreach (Variant variant in rooms)
		{
			Godot.Collections.Dictionary room = variant.AsGodotDictionary();
			MultiplayerLobbyRoomRow row = InstantiateRoomRow();
			row.Configure(room);
			row.JoinRequested += OnRoomJoinRequested;
			RoomListContainer.AddChild(row);
			_roomRows.Add(row);
		}
	}

	private void RefreshPlayers(Godot.Collections.Array players)
	{
		_latestPlayers = players.Duplicate();
		ClearPlayerRows();
		EmptyPlayersLabel.Visible = players.Count == 0;
		if (players.Count == 0)
		{
			_localReady = false;
			ReadyButton.Text = "准备";
			UpdateStartButtonState();
			return;
		}

		foreach (Variant variant in players)
		{
			Godot.Collections.Dictionary player = variant.AsGodotDictionary();
			MultiplayerLobbyPlayerRow row = InstantiatePlayerRow();
			row.Configure(player);
			PlayerListContainer.AddChild(row);
			_playerRows.Add(row);
			if (ReadDictionaryBool(player, "is_local", false))
			{
				_localReady = ReadDictionaryBool(player, "is_ready", false);
				ReadyButton.Text = _localReady ? "取消准备" : "准备";
			}
		}

		UpdateStartButtonState();
	}

	private MultiplayerLobbyRoomRow InstantiateRoomRow()
	{
		Node instance = RoomRowScene.Instantiate();
		if (instance is not MultiplayerLobbyRoomRow row)
		{
			instance.QueueFree();
			throw new System.InvalidOperationException("RoomRowScene root must be MultiplayerLobbyRoomRow.");
		}

		return row;
	}

	private MultiplayerLobbyPlayerRow InstantiatePlayerRow()
	{
		Node instance = PlayerRowScene.Instantiate();
		if (instance is not MultiplayerLobbyPlayerRow row)
		{
			instance.QueueFree();
			throw new System.InvalidOperationException("PlayerRowScene root must be MultiplayerLobbyPlayerRow.");
		}

		return row;
	}

	private void OnRoomJoinRequested(Godot.Collections.Dictionary room)
	{
		string status = ReadDictionaryString(room, "status", NetworkSessionState.Idle.ToString());
		if (!MultiplayerLobbyRules.CanJoinListedRoom(status))
		{
			ShowToast("该房间对局进行中，不可加入");
			return;
		}

		bool hasPassword = ReadDictionaryBool(room, "has_password", false);
		if (hasPassword && string.IsNullOrEmpty(SelectedRoomPassword()))
		{
			ShowToast("该房间已加锁，请先输入房间密码");
			RoomPasswordInput.GrabFocus();
			return;
		}

		string transport = ReadDictionaryString(room, "transport", NetworkTransport.Lan.ToString());
		if (transport == NetworkTransport.Steam.ToString())
		{
			_network.JoinSteamLobby(unchecked((ulong)ReadDictionaryLong(room, "steam_lobby_id", 0)), SelectedRoomPassword());
			return;
		}

		_network.JoinLan(ReadDictionaryString(room, "address", string.Empty), SelectedRoomPassword());
	}

	private void OnReadyChanged(int seatIndex, bool isReady)
	{
		RefreshPlayers(_network.GetPlayersVariant());
	}

	private void UpdateStartButtonState()
	{
		bool canStart = _network.IsHost
			&& _network.State == NetworkSessionState.Lobby
			&& MultiplayerLobbyRules.CanStart(ParsePlayers(_latestPlayers), _network.CurrentConfig.PlayerCount);
		StartButton.Disabled = !canStart;
	}

	private static IReadOnlyList<NetworkPlayerPublicState> ParsePlayers(Godot.Collections.Array players)
	{
		var result = new List<NetworkPlayerPublicState>();
		foreach (Variant variant in players)
		{
			Godot.Collections.Dictionary player = variant.AsGodotDictionary();
			result.Add(new NetworkPlayerPublicState(
				ReadDictionaryInt(player, "peer_id", 0),
				ReadDictionaryString(player, "stable_user_id", string.Empty),
				ReadDictionaryString(player, "display_name", "Player"),
				ReadDictionaryString(player, "avatar_id", string.Empty),
				ReadDictionaryInt(player, "seat_index", 0),
				ReadDictionaryBool(player, "is_host", false),
				ReadDictionaryBool(player, "is_local", false),
				ReadDictionaryBool(player, "is_ready", false),
				ReadDictionaryBool(player, "is_connected", true)));
		}

		return result;
	}

	private void ClearRoomRows()
	{
		foreach (MultiplayerLobbyRoomRow row in _roomRows)
		{
			if (GodotObject.IsInstanceValid(row))
			{
				row.JoinRequested -= OnRoomJoinRequested;
				row.QueueFree();
			}
		}

		_roomRows.Clear();
	}

	private void ClearPlayerRows()
	{
		foreach (MultiplayerLobbyPlayerRow row in _playerRows)
		{
			if (GodotObject.IsInstanceValid(row))
			{
				row.QueueFree();
			}
		}

		_playerRows.Clear();
	}

	private void ShowToast(string message)
	{
		_toastTween?.Kill();
		ToastLabel.Text = message;
		ToastLabel.Visible = true;
		_toastTween = CreateTween();
		_toastTween.TweenInterval(3.0);
		_toastTween.TweenCallback(Callable.From(() => ToastLabel.Visible = false));
	}

	private void ReturnToMainMenu()
	{
		_network.LeaveSession();
		GetTree().ChangeSceneToFile(MainMenuScenePath);
	}

	private static string ReadDictionaryString(Godot.Collections.Dictionary dictionary, string key, string fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) && !string.IsNullOrWhiteSpace(value.AsString())
			? value.AsString()
			: fallback;
	}

	private static int ReadDictionaryInt(Godot.Collections.Dictionary dictionary, string key, int fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsInt32() : fallback;
	}

	private static long ReadDictionaryLong(Godot.Collections.Dictionary dictionary, string key, long fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsInt64() : fallback;
	}

	private static bool ReadDictionaryBool(Godot.Collections.Dictionary dictionary, string key, bool fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsBool() : fallback;
	}
}
