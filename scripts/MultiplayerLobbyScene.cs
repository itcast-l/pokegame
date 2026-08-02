using System.Collections.Generic;
using Godot;

public partial class MultiplayerLobbyScene : Control
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";
	private const string TitleLabelPath = "SafeArea/MainLayout/Header/Title";
	private const string ModeTitleLabelPath = "SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/ModeTitle";
	private const string RoomTitleLabelPath = "SafeArea/MainLayout/BodyScroll/Body/RoomBrowser/PanelMargin/RoomBrowserContent/RoomTitle";
	private const string PlayerTitleLabelPath = "SafeArea/MainLayout/BodyScroll/Body/LobbyPanel/PanelMargin/LobbyContent/PlayerTitle";
	private const string SteamNoteLabelPath = "SafeArea/MainLayout/BodyScroll/Body/FriendsPanel/PanelMargin/FriendsContent/SteamNoteLabel";
	private const string MigrationTrustNoticeLabelPath = "SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/MigrationTrustNoticeLabel";

	private Label _titleLabel = default!;
	private Label _modeTitleLabel = default!;
	private Label _roomTitleLabel = default!;
	private Label _playerTitleLabel = default!;
	private Label _steamNoteLabel = default!;
	private Label _migrationTrustNoticeLabel = default!;

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
	public CheckButton HostMigrationToggle { get; set; } = default!;

	[Export]
	public CheckButton TrustedHotStandbyToggle { get; set; } = default!;

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
	private bool _isReturningToMainMenu;
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
		BindStaticLabels();
		InitializeSceneDefaults();
		ConfigureUnifiedVisualStyle();
		SuppressDefaultPressedSfx(BackButton);
		ConnectButtonSignals();
		ConnectNetworkSignals();
		RefreshLocalizedText();
		RefreshState((int)_network.State);
		RefreshRooms(_network.GetRoomsVariant());
		RefreshPlayers(_network.GetPlayersVariant());
		string pendingErrorMessage = _network.ConsumePendingLobbyErrorMessage();
		if (!string.IsNullOrWhiteSpace(pendingErrorMessage))
		{
			ShowToast(pendingErrorMessage);
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && IsNodeReady())
		{
			RefreshLocalizedText();
			RefreshState((int)_network.State);
			RefreshRooms(_network.GetRoomsVariant());
			RefreshPlayers(_network.GetPlayersVariant());
		}
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
			|| HostMigrationToggle == null
			|| TrustedHotStandbyToggle == null
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

	private void BindStaticLabels()
	{
		_titleLabel = GetNode<Label>(TitleLabelPath);
		_modeTitleLabel = GetNode<Label>(ModeTitleLabelPath);
		_roomTitleLabel = GetNode<Label>(RoomTitleLabelPath);
		_playerTitleLabel = GetNode<Label>(PlayerTitleLabelPath);
		_steamNoteLabel = GetNode<Label>(SteamNoteLabelPath);
		_migrationTrustNoticeLabel = GetNode<Label>(MigrationTrustNoticeLabelPath);
	}

	private void InitializeSceneDefaults()
	{
		string roomName = RoomNameInput.Text.Trim();
		RoomNameInput.Text = RoomNameRules.IsValid(roomName) ? roomName : string.Empty;
		RoomPasswordInput.Text = RoomPasswordRules.Normalize(RoomPasswordInput.Text);
		ApplyHostMigrationPreferences();
	}

	private void ConfigureUnifiedVisualStyle()
	{
		SetMeta(SecondaryMenuGlassStyle.VisualProfileMetadata, SecondaryMenuGlassStyle.UnifiedVisual.Id);
		SecondaryMenuGlassStyle.ApplyReadabilityVeil(
			GetNode<ColorRect>("ReadabilityVeil"),
			SecondaryMenuGlassStyle.UnifiedVisual);

		foreach (string panelName in new[] { "ModeRail", "RoomBrowser", "LobbyPanel", "FriendsPanel" })
		{
			SecondaryMenuGlassStyle.StyleGlassPanel(
				GetNode<PanelContainer>($"SafeArea/MainLayout/BodyScroll/Body/{panelName}"),
				SecondaryMenuGlassStyle.UnifiedVisual);
		}

		MainMenuInteractiveStyle.ApplyThemedLineEdit(RoomNameInput, MainMenuInteractiveStyle.SecondaryMenuPalette, 16);
		MainMenuInteractiveStyle.ApplyThemedLineEdit(RoomPasswordInput, MainMenuInteractiveStyle.SecondaryMenuPalette, 16);
		MainMenuInteractiveStyle.ApplyThemedControl(HostMigrationToggle, MainMenuInteractiveStyle.SecondaryMenuPalette, 15);
		MainMenuInteractiveStyle.ApplyThemedControl(TrustedHotStandbyToggle, MainMenuInteractiveStyle.SecondaryMenuPalette, 15);
		foreach (OptionButton option in new[] { PlayerCountOption, ItemModeOption, WinningScoreOption })
		{
			MainMenuInteractiveStyle.ApplyThemedControl(option, MainMenuInteractiveStyle.SecondaryMenuPalette, 16);
			option.ClipText = true;
		}

		StyleButton(BackButton, MainMenuInteractiveStyle.SecondaryMenuPalette);
		StyleButton(LanHostButton, MainMenuInteractiveStyle.SecondaryMenuPalette);
		StyleButton(LanSearchButton, MainMenuInteractiveStyle.SecondaryMenuPalette);
		StyleButton(SteamHostButton, MainMenuInteractiveStyle.SecondaryMenuPalette);
		StyleButton(SteamRefreshButton, MainMenuInteractiveStyle.SecondaryMenuPalette);
		StyleButton(ReadyButton, MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
		StyleButton(StartButton, MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
		StyleButton(LeaveSessionButton, MainMenuInteractiveStyle.DangerPalette);

		_titleLabel.AddThemeColorOverride("font_color", new Color(0.91f, 0.95f, 0.97f, 1.0f));
		StatusLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.SecondaryMenuPalette.ActiveBorder);
		LanHostInfoLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.82f, 0.86f, 0.94f));
		SteamStatusLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.SecondaryMenuPalette.Text);
		_steamNoteLabel.AddThemeColorOverride("font_color", new Color(0.66f, 0.72f, 0.75f, 0.94f));
		_migrationTrustNoticeLabel.AddThemeColorOverride("font_color", new Color(0.66f, 0.78f, 0.80f, 0.96f));
		foreach (Label sectionTitle in new[]
		{
			_modeTitleLabel,
			_roomTitleLabel,
			_playerTitleLabel,
			GetNode<Label>("SafeArea/MainLayout/BodyScroll/Body/FriendsPanel/PanelMargin/FriendsContent/SteamTitle"),
		})
		{
			sectionTitle.AddThemeColorOverride("font_color", new Color(0.89f, 0.94f, 0.96f, 1.0f));
		}

		foreach (string scrollPath in new[]
		{
			"SafeArea/MainLayout/BodyScroll",
			"SafeArea/MainLayout/BodyScroll/Body/RoomBrowser/PanelMargin/RoomBrowserContent/RoomScroll",
			"SafeArea/MainLayout/BodyScroll/Body/LobbyPanel/PanelMargin/LobbyContent/PlayerScroll",
		})
		{
			ScrollContainer scroll = GetNode<ScrollContainer>(scrollPath);
			scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			SecondaryMenuGlassStyle.StyleScrollBar(scroll.GetVScrollBar(), SecondaryMenuGlassStyle.UnifiedVisual);
		}

		Resized += () => CallDeferred(nameof(ApplyResponsiveLayout));
		CallDeferred(nameof(ApplyResponsiveLayout));
	}

	private static void StyleButton(Button button, MainMenuInteractiveStyle.Palette palette)
	{
		MainMenuInteractiveStyle.ApplyThemedControl(button, palette, 16);
		button.ClipText = true;
	}

	private void ApplyResponsiveLayout()
	{
		if (!IsNodeReady())
		{
			return;
		}

		Vector2 viewportSize = Size;
		if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
		{
			viewportSize = GetViewportRect().Size;
		}
		bool compact = viewportSize.X < 1180.0f || viewportSize.Y < 640.0f;
		GridContainer body = GetNode<GridContainer>("SafeArea/MainLayout/BodyScroll/Body");
		body.Columns = compact ? 2 : 4;
		body.AddThemeConstantOverride("h_separation", compact ? 10 : 16);
		body.AddThemeConstantOverride("v_separation", compact ? 10 : 16);

		MarginContainer safeArea = GetNode<MarginContainer>("SafeArea");
		SetMargins(safeArea, compact ? 18 : 38, compact ? 14 : 30, compact ? 18 : 38, compact ? 14 : 30);
		GetNode<VBoxContainer>("SafeArea/MainLayout").AddThemeConstantOverride("separation", compact ? 10 : 16);
		GetNode<HBoxContainer>("SafeArea/MainLayout/Header").AddThemeConstantOverride("separation", compact ? 10 : 14);
		_titleLabel.AddThemeFontSizeOverride("font_size", compact ? 28 : 34);
		StatusLabel.CustomMinimumSize = new Vector2(compact ? 104.0f : 130.0f, 0.0f);

		var panelWidths = new Dictionary<string, float>
		{
			["ModeRail"] = 220.0f,
			["RoomBrowser"] = 280.0f,
			["LobbyPanel"] = 300.0f,
			["FriendsPanel"] = 190.0f,
		};
		foreach (KeyValuePair<string, float> pair in panelWidths)
		{
			PanelContainer panel = GetNode<PanelContainer>($"SafeArea/MainLayout/BodyScroll/Body/{pair.Key}");
			panel.CustomMinimumSize = new Vector2(compact ? 0.0f : pair.Value, 0.0f);
			SetMargins(
				panel.GetNode<MarginContainer>("PanelMargin"),
				compact ? 12 : 16,
				compact ? 10 : 14,
				compact ? 12 : 16,
				compact ? 10 : 14);
		}

		ToastLabel.OffsetLeft = compact ? 72.0f : 300.0f;
		ToastLabel.OffsetRight = compact ? -72.0f : -300.0f;
		GetNode<ScrollContainer>("SafeArea/MainLayout/BodyScroll").ScrollHorizontal = 0;
	}

	private static void SetMargins(MarginContainer margin, int left, int top, int right, int bottom)
	{
		margin.AddThemeConstantOverride("margin_left", left);
		margin.AddThemeConstantOverride("margin_top", top);
		margin.AddThemeConstantOverride("margin_right", right);
		margin.AddThemeConstantOverride("margin_bottom", bottom);
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
		HostMigrationToggle.Toggled += OnHostMigrationPreferenceToggled;
		TrustedHotStandbyToggle.Toggled += OnTrustedHotStandbyPreferenceToggled;
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
		HostMigrationToggle.Toggled -= OnHostMigrationPreferenceToggled;
		TrustedHotStandbyToggle.Toggled -= OnTrustedHotStandbyPreferenceToggled;
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

	private void ApplyHostMigrationPreferences()
	{
		_network.ConfigureHostMigration(
			HostMigrationToggle.ButtonPressed,
			TrustedHotStandbyToggle.ButtonPressed);
	}

	private void OnHostMigrationPreferenceToggled(bool enabled)
	{
		ApplyHostMigrationPreferences();
	}

	private void OnTrustedHotStandbyPreferenceToggled(bool accepted)
	{
		ApplyHostMigrationPreferences();
	}

	private bool TryGetSelectedRoomName(out string roomName)
	{
		roomName = RoomNameInput.Text.Trim();
		if (RoomNameRules.IsValid(roomName))
		{
			return true;
		}

		ShowToast(LocalizeFormat("MULTIPLAYER_LOBBY_INVALID_ROOM_NAME", RoomNameRules.MaxLength));
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
		ApplyHostMigrationPreferences();
		_network.HostLan(SelectedPlayerCount(), SelectedItemMode(), SelectedWinningScore(), roomName, SelectedRoomPassword());
	}

	private void HostSteam()
	{
		if (!TryGetSelectedRoomName(out string roomName))
		{
			return;
		}

		_localReady = true;
		ApplyHostMigrationPreferences();
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
		UpdateReadyButtonText();
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
		StatusLabel.Text = Localize(GetNetworkStateLocalizationKey(state));
		SteamStatusLabel.Text = _network.SteamAvailable
			? Localize("MULTIPLAYER_LOBBY_STEAM_AVAILABLE")
			: Localize("MULTIPLAYER_LOBBY_STEAM_UNAVAILABLE");

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
		HostMigrationToggle.Disabled = !canConfigureSession;
		TrustedHotStandbyToggle.Disabled = !canConfigureSession;
		if (!canConfigureSession)
		{
			HostMigrationToggle.SetPressedNoSignal(_network.HostMigrationEnabled);
			TrustedHotStandbyToggle.SetPressedNoSignal(_network.TrustedHotStandbyAccepted);
		}
		ReadyButton.Disabled = state != NetworkSessionState.Lobby;
		LeaveSessionButton.Disabled = state is NetworkSessionState.Idle or NetworkSessionState.Searching or NetworkSessionState.Failed;
		LanHostInfoLabel.Text = state == NetworkSessionState.Lobby && _network.CurrentConfig.Transport == NetworkTransport.Lan
			? LocalizeFormat("MULTIPLAYER_LOBBY_LAN_ADDRESS_FORMAT", _network.GetLocalLanAddressSummary())
			: Localize("MULTIPLAYER_LOBBY_LAN_ADDRESS_PENDING");
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
			UpdateReadyButtonText();
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
				UpdateReadyButtonText();
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
			ShowToast("MULTIPLAYER_LOBBY_ROOM_IN_PROGRESS");
			return;
		}

		bool hasPassword = ReadDictionaryBool(room, "has_password", false);
		if (hasPassword && string.IsNullOrEmpty(SelectedRoomPassword()))
		{
			ShowToast("MULTIPLAYER_LOBBY_ROOM_PASSWORD_REQUIRED");
			RoomPasswordInput.GrabFocus();
			return;
		}

		string transport = ReadDictionaryString(room, "transport", NetworkTransport.Lan.ToString());
		ApplyHostMigrationPreferences();
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
				ReadDictionaryString(player, "player_session_id", string.Empty),
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
		ToastLabel.Text = LooksLikeLocalizationKey(message) ? Localize(message) : message;
		ToastLabel.Visible = true;
		_toastTween = CreateTween();
		_toastTween.TweenInterval(3.0);
		_toastTween.TweenCallback(Callable.From(() => ToastLabel.Visible = false));
	}

	public bool DebugIsMigrationFailureToastVisible()
	{
		return ToastLabel.Visible
			&& ToastLabel.Text.Contains("Host migration failed", System.StringComparison.Ordinal);
	}

	public string DebugGetMigrationFailureToastText()
	{
		return ToastLabel.Visible ? ToastLabel.Text : string.Empty;
	}

	private async void ReturnToMainMenu()
	{
		if (_isReturningToMainMenu)
		{
			return;
		}

		PackedScene? mainMenuScene = GD.Load<PackedScene>(MainMenuScenePath);
		if (mainMenuScene == null)
		{
			GD.PushError($"Missing main menu scene: {MainMenuScenePath}");
			return;
		}

		_isReturningToMainMenu = true;
		LockLobbyInteractionForSceneTransition();
		await CircularSceneTransition.PlaySceneSwapAsync(this, () =>
		{
			_network.LeaveSession();
			Node mainMenu = mainMenuScene.Instantiate();
			GetTree().Root.AddChild(mainMenu);
			GetTree().CurrentScene = mainMenu;
			QueueFree();
		});

		if (GodotObject.IsInstanceValid(this) && IsInsideTree())
		{
			_isReturningToMainMenu = false;
			BackButton.Disabled = false;
			RefreshState((int)_network.State);
		}
	}

	public bool DebugIsReturningToMainMenu()
	{
		return _isReturningToMainMenu;
	}

	private static void SuppressDefaultPressedSfx(BaseButton? button)
	{
		if (button != null && GodotObject.IsInstanceValid(button))
		{
			button.SetMeta(GameAudioManager.UiSuppressPressedSfxMeta, true);
		}
	}

	private void LockLobbyInteractionForSceneTransition()
	{
		GetViewport().GuiReleaseFocus();
		BackButton.Disabled = true;
		LanHostButton.Disabled = true;
		LanSearchButton.Disabled = true;
		SteamHostButton.Disabled = true;
		SteamRefreshButton.Disabled = true;
		ReadyButton.Disabled = true;
		StartButton.Disabled = true;
		LeaveSessionButton.Disabled = true;
		PlayerCountOption.Disabled = true;
		ItemModeOption.Disabled = true;
		WinningScoreOption.Disabled = true;
		RoomNameInput.Editable = false;
		RoomPasswordInput.Editable = false;
		HostMigrationToggle.Disabled = true;
		TrustedHotStandbyToggle.Disabled = true;
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

	private void RefreshLocalizedText()
	{
		_titleLabel.Text = Localize("MENU_MULTIPLAYER");
		_modeTitleLabel.Text = Localize("MULTIPLAYER_LOBBY_ROOM_SETTINGS");
		_roomTitleLabel.Text = Localize("MULTIPLAYER_LOBBY_ROOM_LIST_TITLE");
		_playerTitleLabel.Text = Localize("MULTIPLAYER_LOBBY_PLAYER_LIST_TITLE");
		_steamNoteLabel.Text = Localize("MULTIPLAYER_LOBBY_STEAM_NOTE");
		RoomNameInput.PlaceholderText = Localize("MULTIPLAYER_LOBBY_ROOM_NAME_PLACEHOLDER");
		RoomPasswordInput.PlaceholderText = Localize("MULTIPLAYER_LOBBY_ROOM_PASSWORD_PLACEHOLDER");
		HostMigrationToggle.Text = Localize("MULTIPLAYER_LOBBY_HOST_MIGRATION_TOGGLE");
		TrustedHotStandbyToggle.Text = Localize("MULTIPLAYER_LOBBY_TRUSTED_STANDBY_TOGGLE");
		_migrationTrustNoticeLabel.Text = Localize("MULTIPLAYER_LOBBY_MIGRATION_TRUST_NOTICE");
		BackButton.Text = Localize("ADVENTURE_BACK");
		LanHostButton.Text = Localize("MULTIPLAYER_LOBBY_HOST_LAN");
		LanSearchButton.Text = Localize("MULTIPLAYER_LOBBY_SEARCH_LAN");
		SteamHostButton.Text = Localize("MULTIPLAYER_LOBBY_HOST_STEAM");
		SteamRefreshButton.Text = Localize("MULTIPLAYER_LOBBY_REFRESH_STEAM");
		EmptyRoomsLabel.Text = Localize("MULTIPLAYER_LOBBY_EMPTY_ROOMS");
		EmptyPlayersLabel.Text = Localize("MULTIPLAYER_LOBBY_EMPTY_PLAYERS");
		StartButton.Text = Localize("PLAYER_COUNT_START_GAME");
		LeaveSessionButton.Text = Localize("MULTIPLAYER_LOBBY_LEAVE");
		UpdateReadyButtonText();
		RefreshOptionTexts();
	}

	private void RefreshOptionTexts()
	{
		for (int index = 0; index < PlayerCountOption.ItemCount; index++)
		{
			PlayerCountOption.SetItemText(index, GetPlayerCountOptionText(PlayerCountOption.GetItemId(index)));
		}

		for (int index = 0; index < ItemModeOption.ItemCount; index++)
		{
			int itemId = ItemModeOption.GetItemId(index);
			ItemModeOption.SetItemText(index, Localize(itemId == 1 ? "PLAYER_COUNT_ITEM_MODE_ON" : "PLAYER_COUNT_ITEM_MODE_OFF"));
		}

		for (int index = 0; index < WinningScoreOption.ItemCount; index++)
		{
			WinningScoreOption.SetItemText(index, LocalizeFormat("PLAYER_COUNT_WINNING_SCORE_OPTION", WinningScoreOption.GetItemId(index)));
		}
	}

	private void UpdateReadyButtonText()
	{
		ReadyButton.Text = Localize(_localReady ? "MULTIPLAYER_LOBBY_CANCEL_READY" : "MULTIPLAYER_LOBBY_READY");
	}

	private string GetPlayerCountOptionText(int playerCount)
	{
		return playerCount switch
		{
			2 => Localize("PLAYER_COUNT_2_TITLE"),
			3 => Localize("PLAYER_COUNT_3_TITLE"),
			4 => Localize("PLAYER_COUNT_4_TITLE"),
			_ => playerCount.ToString(),
		};
	}

	private string Localize(string key)
	{
		string text = Tr(key).ToString();
		return text == key ? key : text;
	}

	private string LocalizeFormat(string key, params object[] args)
	{
		return string.Format(Localize(key), args);
	}

	private static bool LooksLikeLocalizationKey(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || !value.Contains('_', System.StringComparison.Ordinal))
		{
			return false;
		}

		for (int index = 0; index < value.Length; index++)
		{
			char character = value[index];
			if ((character < 'A' || character > 'Z') && (character < '0' || character > '9') && character != '_')
			{
				return false;
			}
		}

		return true;
	}

	private static string GetNetworkStateLocalizationKey(NetworkSessionState state)
	{
		return state switch
		{
			NetworkSessionState.Idle => "NETWORK_STATE_IDLE",
			NetworkSessionState.Searching => "NETWORK_STATE_SEARCHING",
			NetworkSessionState.Hosting => "NETWORK_STATE_HOSTING",
			NetworkSessionState.Joining => "NETWORK_STATE_JOINING",
			NetworkSessionState.Lobby => "NETWORK_STATE_LOBBY",
			NetworkSessionState.Starting => "NETWORK_STATE_STARTING",
			NetworkSessionState.LoadingScene => "NETWORK_STATE_LOADING_SCENE",
			NetworkSessionState.InGame => "NETWORK_STATE_IN_GAME",
			NetworkSessionState.Reconnecting => "NETWORK_STATE_RECONNECTING",
			NetworkSessionState.Failed => "NETWORK_STATE_FAILED",
			_ => "NETWORK_STATE_IDLE",
		};
	}
}
