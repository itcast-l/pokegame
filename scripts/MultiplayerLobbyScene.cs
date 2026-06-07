using Godot;

public partial class MultiplayerLobbyScene : Control
{
	private const string BackgroundTexturePath = "res://art/menu/main_menu_concept_10.png";
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";

	private NetworkManager _network = default!;
	private VBoxContainer _roomList = default!;
	private VBoxContainer _playerList = default!;
	private Label _statusLabel = default!;
	private Label _toastLabel = default!;
	private LineEdit _roomNameInput = default!;
	private OptionButton _playerCountOption = default!;
	private OptionButton _itemModeOption = default!;
	private OptionButton _winningScoreOption = default!;
	private Button _lanHostButton = default!;
	private Button _lanSearchButton = default!;
	private Button _readyButton = default!;
	private Button _startButton = default!;
	private Button _steamHostButton = default!;
	private Button _steamRefreshButton = default!;
	private bool _localReady;

	public override void _Ready()
	{
		GameInputActions.EnsureRegistered();
		Localization.EnsureLoaded();
		_network = EnsureNetworkManager();
		BuildLayout();
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
			_network.ErrorRaised -= ShowToast;
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (GameInputActions.IsCancelPressed(inputEvent))
		{
			ReturnToMainMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildLayout()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);

		TextureRect backgroundArt = new()
		{
			Name = "BackgroundArt",
			Texture = GD.Load<Texture2D>(BackgroundTexturePath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		backgroundArt.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(backgroundArt);

		var oceanDynamicsLayer = new MainMenuOceanDynamics
		{
			Name = "OceanDynamicsLayer",
			MouseFilter = MouseFilterEnum.Ignore,
			bubble_count = 56,
			glint_count = 34,
			motion_scale = 1.0f,
		};
		oceanDynamicsLayer.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(oceanDynamicsLayer);

		ColorRect waterCausticOverlay = new()
		{
			Name = "WaterCausticOverlay",
			Color = new Color(0.16f, 0.72f, 0.82f, 0.08f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		waterCausticOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(waterCausticOverlay);

		CanvasLayer toastLayer = new() { Name = "ToastLayer", Layer = 20 };
		AddChild(toastLayer);
		_toastLabel = new Label
		{
			Name = "Toast",
			Visible = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_toastLabel.AddThemeColorOverride("font_color", new Color(0.96f, 1.0f, 0.94f));
		_toastLabel.AddThemeFontSizeOverride("font_size", 18);
		_toastLabel.SetAnchorsPreset(LayoutPreset.TopWide);
		_toastLabel.OffsetLeft = 300;
		_toastLabel.OffsetTop = 18;
		_toastLabel.OffsetRight = -300;
		_toastLabel.OffsetBottom = 62;
		toastLayer.AddChild(_toastLabel);

		MarginContainer safeArea = new() { Name = "SafeArea" };
		safeArea.SetAnchorsPreset(LayoutPreset.FullRect);
		safeArea.AddThemeConstantOverride("margin_left", 38);
		safeArea.AddThemeConstantOverride("margin_top", 30);
		safeArea.AddThemeConstantOverride("margin_right", 38);
		safeArea.AddThemeConstantOverride("margin_bottom", 30);
		AddChild(safeArea);

		VBoxContainer multiRoot = new() { Name = "MultiRoot" };
		multiRoot.AddThemeConstantOverride("separation", 16);
		safeArea.AddChild(multiRoot);

		HBoxContainer header = new() { Name = "Header" };
		header.AddThemeConstantOverride("separation", 14);
		multiRoot.AddChild(header);

		Label title = new()
		{
			Text = "多人大厅",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		title.AddThemeColorOverride("font_color", new Color(0.9f, 1.0f, 0.96f));
		title.AddThemeFontSizeOverride("font_size", 34);
		header.AddChild(title);

		_statusLabel = new Label
		{
			Text = "Idle",
			VerticalAlignment = VerticalAlignment.Center,
		};
		_statusLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.96f, 0.94f));
		_statusLabel.AddThemeFontSizeOverride("font_size", 16);
		header.AddChild(_statusLabel);

		Button leaveButton = CreateButton("返回");
		leaveButton.Pressed += ReturnToMainMenu;
		header.AddChild(leaveButton);

		HBoxContainer body = new() { Name = "Body", SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 16);
		multiRoot.AddChild(body);

		VBoxContainer modeRail = AddPanelColumn(body, "ModeRail", 240);
		modeRail.AddChild(CreateSectionTitle("房间设置"));
		_roomNameInput = CreateRoomNameInput();
		modeRail.AddChild(_roomNameInput);
		_playerCountOption = CreatePlayerCountOption();
		_itemModeOption = CreateItemModeOption();
		_winningScoreOption = CreateWinningScoreOption();
		modeRail.AddChild(_playerCountOption);
		modeRail.AddChild(_itemModeOption);
		modeRail.AddChild(_winningScoreOption);
		_lanHostButton = CreateButton("LAN 建房");
		_lanHostButton.Pressed += HostLan;
		modeRail.AddChild(_lanHostButton);
		_lanSearchButton = CreateButton("搜索 LAN");
		_lanSearchButton.Pressed += () => _network.SearchLan();
		modeRail.AddChild(_lanSearchButton);
		_steamHostButton = CreateButton("Steam 建房");
		_steamHostButton.Pressed += HostSteam;
		modeRail.AddChild(_steamHostButton);
		_steamRefreshButton = CreateButton("刷新 Steam");
		_steamRefreshButton.Pressed += () => _network.RefreshSteamLobbies();
		modeRail.AddChild(_steamRefreshButton);

		VBoxContainer roomBrowser = AddPanelColumn(body, "RoomBrowser", 320);
		roomBrowser.AddChild(CreateSectionTitle("房间列表"));
		ScrollContainer roomScroll = CreateListScroll();
		_roomList = CreateListContainer();
		_roomList.AddThemeConstantOverride("separation", 8);
		roomScroll.AddChild(_roomList);
		roomBrowser.AddChild(roomScroll);

		VBoxContainer lobbyPanel = AddPanelColumn(body, "LobbyPanel", 360);
		lobbyPanel.AddChild(CreateSectionTitle("玩家"));
		ScrollContainer playerScroll = CreateListScroll();
		_playerList = CreateListContainer();
		_playerList.AddThemeConstantOverride("separation", 8);
		playerScroll.AddChild(_playerList);
		lobbyPanel.AddChild(playerScroll);
		_readyButton = CreateButton("准备");
		_readyButton.Pressed += ToggleReady;
		lobbyPanel.AddChild(_readyButton);
		_startButton = CreateButton("开始");
		_startButton.Pressed += () => _network.StartMatch();
		lobbyPanel.AddChild(_startButton);
		Button leaveSessionButton = CreateButton("离开房间");
		leaveSessionButton.Pressed += () => _network.LeaveSession();
		lobbyPanel.AddChild(leaveSessionButton);

		VBoxContainer friendsPanel = AddPanelColumn(body, "FriendsPanel", 230);
		friendsPanel.AddChild(CreateSectionTitle("Steam"));
		Label steamStatus = new()
		{
			Text = _network.SteamAvailable ? "Steam 可用" : "Steam 不可用",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		steamStatus.AddThemeColorOverride("font_color", new Color(0.76f, 0.95f, 0.95f));
		steamStatus.AddThemeFontSizeOverride("font_size", 16);
		friendsPanel.AddChild(steamStatus);
		Label note = new()
		{
			Text = "Steam App ID 为 0 或初始化失败时，这里会禁用；LAN 不受影响。",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		note.AddThemeColorOverride("font_color", new Color(0.62f, 0.86f, 0.86f));
		note.AddThemeFontSizeOverride("font_size", 14);
		friendsPanel.AddChild(note);
	}

	private NetworkManager EnsureNetworkManager()
	{
		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (networkManager != null)
		{
			return networkManager;
		}

		networkManager = new NetworkManager { Name = "NetworkManager" };
		GetTree().Root.AddChild(networkManager);
		return networkManager;
	}

	private void ConnectNetworkSignals()
	{
		_network.StateChanged += RefreshState;
		_network.RoomListUpdated += RefreshRooms;
		_network.PlayerListUpdated += RefreshPlayers;
		_network.ErrorRaised += ShowToast;
	}

	private VBoxContainer AddPanelColumn(Container parent, string nodeName, float minWidth)
	{
		PanelContainer panel = new()
		{
			Name = nodeName,
			CustomMinimumSize = new Vector2(minWidth, 0),
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
		parent.AddChild(panel);

		MarginContainer margin = new();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		VBoxContainer column = new()
		{
			Name = $"{nodeName}Content",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		column.AddThemeConstantOverride("separation", 10);
		margin.AddChild(column);
		return column;
	}

	private static StyleBoxFlat CreatePanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.008f, 0.10f, 0.13f, 0.76f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.28f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.5f),
			ShadowSize = 18,
			ShadowOffset = new Vector2(0, 8),
		};
	}

	private static Label CreateSectionTitle(string text)
	{
		Label label = new() { Text = text };
		label.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.96f));
		label.AddThemeFontSizeOverride("font_size", 20);
		return label;
	}

	private static Button CreateButton(string text)
	{
		Button button = new()
		{
			Text = text,
			CustomMinimumSize = new Vector2(0, 42),
			FocusMode = FocusModeEnum.All,
		};
		button.AddThemeFontSizeOverride("font_size", 16);
		return button;
	}

	private static OptionButton CreatePlayerCountOption()
	{
		OptionButton option = CreateOption();
		option.AddItem("2 人", 2);
		option.AddItem("3 人", 3);
		option.AddItem("4 人", 4);
		option.Select(0);
		return option;
	}

	private static OptionButton CreateItemModeOption()
	{
		OptionButton option = CreateOption();
		option.AddItem("道具开启", 1);
		option.AddItem("道具关闭", 0);
		option.Select(0);
		return option;
	}

	private static OptionButton CreateWinningScoreOption()
	{
		OptionButton option = CreateOption();
		option.AddItem("50 分", 50);
		option.AddItem("100 分", 100);
		option.AddItem("200 分", 200);
		option.Select(1);
		return option;
	}

	private static OptionButton CreateOption()
	{
		return new OptionButton
		{
			CustomMinimumSize = new Vector2(0, 42),
			FocusMode = FocusModeEnum.All,
		};
	}

	private static LineEdit CreateRoomNameInput()
	{
		LineEdit input = new()
		{
			Text = RoomNameRules.DefaultRoomName,
			PlaceholderText = "房间名称",
			MaxLength = RoomNameRules.MaxLength,
			CustomMinimumSize = new Vector2(0, 42),
			FocusMode = FocusModeEnum.All,
		};
		input.AddThemeFontSizeOverride("font_size", 16);
		return input;
	}

	private static ScrollContainer CreateListScroll()
	{
		return new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
	}

	private static VBoxContainer CreateListContainer()
	{
		return new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
	}

	private int SelectedPlayerCount()
	{
		return _playerCountOption.GetSelectedId();
	}

	private bool SelectedItemMode()
	{
		return _itemModeOption.GetSelectedId() == 1;
	}

	private int SelectedWinningScore()
	{
		return _winningScoreOption.GetSelectedId();
	}

	private bool TryGetSelectedRoomName(out string roomName)
	{
		roomName = _roomNameInput.Text.Trim();
		if (RoomNameRules.IsValid(roomName))
		{
			return true;
		}

		ShowToast($"房间名仅支持中英文、数字、空格、-、_，最多 {RoomNameRules.MaxLength} 个字");
		_roomNameInput.GrabFocus();
		return false;
	}

	private void HostLan()
	{
		if (!TryGetSelectedRoomName(out string roomName))
		{
			return;
		}

		_localReady = true;
		_network.HostLan(SelectedPlayerCount(), SelectedItemMode(), SelectedWinningScore(), roomName);
	}

	private void HostSteam()
	{
		if (!TryGetSelectedRoomName(out string roomName))
		{
			return;
		}

		_localReady = true;
		_network.HostSteam(SelectedPlayerCount(), SelectedItemMode(), SelectedWinningScore(), roomName);
	}

	private void ToggleReady()
	{
		_localReady = !_localReady;
		_network.SetLocalReady(_localReady);
		_readyButton.Text = _localReady ? "取消准备" : "准备";
	}

	private void RefreshState(int stateValue)
	{
		var state = (NetworkSessionState)stateValue;
		_statusLabel.Text = state.ToString();
		bool steamEnabled = _network.SteamAvailable;
		bool canConfigureSession = state is NetworkSessionState.Idle or NetworkSessionState.Searching or NetworkSessionState.Failed;
		_lanHostButton.Disabled = !canConfigureSession;
		_lanSearchButton.Disabled = state is NetworkSessionState.Hosting or NetworkSessionState.Joining or NetworkSessionState.Lobby or NetworkSessionState.Starting or NetworkSessionState.LoadingScene or NetworkSessionState.InGame;
		_steamHostButton.Disabled = !steamEnabled || !canConfigureSession;
		_steamRefreshButton.Disabled = !steamEnabled || state is NetworkSessionState.Hosting or NetworkSessionState.Joining or NetworkSessionState.Lobby or NetworkSessionState.Starting or NetworkSessionState.LoadingScene or NetworkSessionState.InGame;
		_roomNameInput.Editable = canConfigureSession;
		_playerCountOption.Disabled = !canConfigureSession;
		_itemModeOption.Disabled = !canConfigureSession;
		_winningScoreOption.Disabled = !canConfigureSession;
		_startButton.Disabled = !_network.IsHost || state != NetworkSessionState.Lobby;
		_readyButton.Disabled = state != NetworkSessionState.Lobby;
	}

	private void RefreshRooms(Godot.Collections.Array rooms)
	{
		ClearChildren(_roomList);
		if (rooms.Count == 0)
		{
			_roomList.AddChild(CreateMutedLabel("没有发现房间"));
			return;
		}

		foreach (Variant variant in rooms)
		{
			Godot.Collections.Dictionary room = variant.AsGodotDictionary();
			string roomName = ReadDictionaryString(room, "room_name", ReadDictionaryString(room, "host_name", "房间"));
			Button row = CreateButton(
				$"{roomName}  {room["transport"].AsString()}  {room["player_count"].AsInt32()}人");
			row.TooltipText = $"{ReadDictionaryString(room, "host_name", "Host")} / {room["address"].AsString()} / {room["status"].AsString()}";
			row.Pressed += () =>
			{
				string transport = room["transport"].AsString();
				if (transport == NetworkTransport.Steam.ToString())
				{
					_network.JoinSteamLobby(unchecked((ulong)room["steam_lobby_id"].AsInt64()));
				}
				else
				{
					_network.JoinLan(room["address"].AsString());
				}
			};
			_roomList.AddChild(row);
		}
	}

	private void RefreshPlayers(Godot.Collections.Array players)
	{
		ClearChildren(_playerList);
		if (players.Count == 0)
		{
			_playerList.AddChild(CreateMutedLabel("尚未加入房间"));
			return;
		}

		foreach (Variant variant in players)
		{
			Godot.Collections.Dictionary player = variant.AsGodotDictionary();
			string ready = player["is_ready"].AsBool() ? "Ready" : "Waiting";
			string host = player["is_host"].AsBool() ? "Host" : "Peer";
			Label row = new()
			{
				Text = $"{player["seat_index"].AsInt32() + 1}. {player["display_name"].AsString()}  {host}  {ready}",
				CustomMinimumSize = new Vector2(0, 32),
				VerticalAlignment = VerticalAlignment.Center,
			};
			row.AddThemeColorOverride("font_color", new Color(0.86f, 1.0f, 0.96f));
			row.AddThemeFontSizeOverride("font_size", 16);
			_playerList.AddChild(row);
			if (player["is_local"].AsBool())
			{
				_localReady = player["is_ready"].AsBool();
				_readyButton.Text = _localReady ? "取消准备" : "准备";
			}
		}
	}

	private static Label CreateMutedLabel(string text)
	{
		Label label = new()
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.Off,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		label.AddThemeColorOverride("font_color", new Color(0.62f, 0.86f, 0.86f));
		label.AddThemeFontSizeOverride("font_size", 15);
		return label;
	}

	private static string ReadDictionaryString(Godot.Collections.Dictionary dictionary, string key, string fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) && !string.IsNullOrWhiteSpace(value.AsString())
			? value.AsString()
			: fallback;
	}

	private static void ClearChildren(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void ShowToast(string message)
	{
		_toastLabel.Text = message;
		_toastLabel.Visible = true;
		Tween tween = CreateTween();
		tween.TweenInterval(3.0);
		tween.TweenCallback(Callable.From(() => _toastLabel.Visible = false));
	}

	private void ReturnToMainMenu()
	{
		_network.LeaveSession();
		GetTree().ChangeSceneToFile(MainMenuScenePath);
	}
}
