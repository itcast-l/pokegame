using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class MainMenuScene : Control
{
	private const float BackgroundMusicFadeSeconds = 2.0f;
	private const string BackgroundArtPath = "BackgroundArt";
	private const string MenuCameraPath = "MenuCamera2D";
	private const string MenuCameraHomePath = "MenuCameraHome";
	private const string MenuCameraPushInPath = "MenuCameraPushIn";
	private const string MenuCameraAdventureDivePath = "MenuCameraAdventureDive";
	private const string OceanDynamicsLayerPath = "OceanDynamicsLayer";
	private const string UnderwaterAmbientLayerPath = "UnderwaterAmbientLayer";
	private const string WaterCausticOverlayPath = "WaterCausticOverlay";
	private const string SinglePlayerButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SinglePlayerFrame/Button";
	private const string AdventureButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/AdventureFrame/Button";
	private const string MultiplayerButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/MultiplayerFrame/Button";
	private const string SettingsButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SettingsFrame/Button";
	private const string ExitButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/ExitFrame/Button";
	private const string TitleLabelPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/Title";
	private const string SubtitleLabelPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/Subtitle";
	private const string AvatarEntryPath = "AvatarEntryLayer/MainMenuAvatarEntry";
	private const string GameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const string MultiplayerLobbyScenePath = "res://scenes/multiplayer_lobby_scene.tscn";
	private const string AdventureAvatarMenuScenePath = "res://scenes/adventure_avatar_menu_scene.tscn";
	private const string AvatarEditScenePath = "res://scenes/character_avatar_edit_scene.tscn";
	private const string PlayerCountDialogScenePath = "res://scenes/main_menu_player_count_dialog.tscn";
	private const string SettingsDialogScenePath = "res://scenes/main_menu_settings_dialog.tscn";
	private const string ControlsDialogScenePath = "res://scenes/main_menu_controls_dialog.tscn";
	private const string ExitDialogScenePath = "res://scenes/main_menu_exit_dialog.tscn";
	private const string AvatarUnlockPromptScenePath = "res://scenes/avatar_unlock_prompt.tscn";
	private const string AdventureTransitionScenePath = "res://scenes/main_menu_adventure_transition.tscn";
	private const string SoundBusName = "Sound";
	private const string MusicBusName = "Music";
	private const string MainMenuCustomMusicDirectory = "res://asset/audio/bgm/main_menu_custom";
	private const float DialogFadeSeconds = 0.14f;
	private const int AvatarEditDialogLayer = 42;
	private const int MinMenuPlayers = 2;
	private const int MaxMenuPlayers = 4;
	private const float DefaultMasterVolume = 0.82f;
	private const float DefaultMusicVolume = 0.76f;
	private const bool DefaultMuteBgm = false;
	private const bool DefaultMuteSfx = false;
	private const bool DefaultFullscreen = false;
	private const bool DefaultItemMode = true;
	private static readonly Vector2I DesignResolution = new(1280, 720);
	private static readonly Vector2I DefaultResolution = DesignResolution;
	private static readonly ResolutionPreset[] ResolutionPresets =
	{
		new(960, 540),
		new(1024, 576),
		new(1280, 720),
		new(1366, 768),
		new(1600, 900),
		new(1920, 1080),
		new(2560, 1440),
	};
	private const int MenuCameraHomePriority = 10;
	private const int MenuCameraPushInPriority = 20;
	private const int MenuCameraAdventureDivePriority = 30;
	private const int MenuCameraInactivePriority = 0;
	private const float MenuCameraTransitionPaddingSeconds = 0.08f;
	private const float AdventureMenuFadeSeconds = 0.32f;

	private Camera2D? _menuCamera;
	private Node2D? _menuCameraHome;
	private Node2D? _menuCameraPushIn;
	private Node2D? _menuCameraAdventureDive;
	private Button _singlePlayerButton = default!;
	private Button _adventureButton = default!;
	private Button _multiplayerButton = default!;
	private Button _settingsButton = default!;
	private Button _exitButton = default!;
	private Label _titleLabel = default!;
	private Label _subtitleLabel = default!;
	private MainMenuBackgroundAnimator? _backgroundAnimator;
	private MainMenuOceanDynamics? _oceanDynamicsLayer;
	private UnderwaterAmbientLayer? _underwaterAmbientLayer;
	private CanvasItem? _waterCausticOverlay;
	private MainMenuPlayerCountDialog _playerCountDialog = default!;
	private CanvasLayer _playerCountDialogLayer = default!;
	private Control _playerCountDialogRoot = default!;
	private Control _playerCountDialogMotion = default!;
	private Control _playerCountDialogPanel = default!;
	private Tween? _playerCountDialogTween;
	private OptionButton _playerCountOption = default!;
	private OptionButton _difficultyOption = default!;
	private OptionButton _itemModeOption = default!;
	private OptionButton _winningScoreOption = default!;
	private MainMenuSettingsDialog _settingsDialog = default!;
	private Control _settingsDialogRoot = default!;
	private Control _settingsDialogPanel = default!;
	private Tween? _settingsDialogTween;
	private MainMenuControlsDialog _controlsDialog = default!;
	private Control _controlsDialogRoot = default!;
	private Control _controlsDialogPanel = default!;
	private Tween? _controlsDialogTween;
	private HSlider _masterVolumeSlider = default!;
	private HSlider _musicVolumeSlider = default!;
	private Label _masterVolumeValue = default!;
	private Label _musicVolumeValue = default!;
	private CheckButton _muteBgmToggle = default!;
	private CheckButton _muteSfxToggle = default!;
	private OptionButton _windowModeOption = default!;
	private OptionButton _resolutionOption = default!;
	private OptionButton _languageOption = default!;
	private OptionButton _mainMenuMusicTrackOption = default!;
	private Button _controlsButton = default!;
	private MainMenuAvatarEntry _avatarEntry = default!;
	private MainMenuExitDialog _exitDialog = default!;
	private Control _exitDialogRoot = default!;
	private PanelContainer _exitDialogPanel = default!;
	private Tween? _exitDialogTween;
	private CharacterAvatarEditScene? _avatarEditDialogRoot;
	private PanelContainer? _avatarEditDialogPanel;
	private Tween? _avatarEditDialogTween;
	private MainMenuAdventureTransition? _adventureTransitionRoot;
	private Control? _adventureTransitionMotion;
	private Tween? _adventureTransitionTween;
	private Control? _preparedAdventureAvatarMenu;
	private GameAudioManager? _gameAudio;
	private bool _playAdventureReturnTransition;
	private float _masterVolume = DefaultMasterVolume;
	private float _musicVolume = DefaultMusicVolume;
	private bool _muteBgm = DefaultMuteBgm;
	private bool _muteSfx = DefaultMuteSfx;
	private bool _fullscreen = DefaultFullscreen;
	private Vector2I _selectedResolution = DefaultResolution;
	private string _languagePreference = Localization.DefaultLanguagePreference;
	private string _selectedAvatarId = CharacterAvatarCatalog.DefaultAvatarId;
	private string _playerName = PlayerNameRules.DefaultPlayerName;
	private string _selectedMainMenuMusicTrack = string.Empty;
	private bool _isSyncingSettingsControls;
	private string? _pendingBindingActionId;
	private GameInputActions.BindingSlot? _pendingBindingSlot;
	private int _bindingCaptureEarliestFrame;
	private bool _isStartingGameTransition;
	private bool _isMenuCameraTransitioning;
	private bool _hasAppliedWindowSettings;
	private bool _lastAppliedFullscreen;
	private Vector2I _lastAppliedResolution = DefaultResolution;
	private uint _windowResolutionApplyToken;
	private ulong _menuCameraTransitionStartMsec;
	private float _menuCameraTransitionTotalSeconds;
	private readonly List<MainMenuMusicOption> _mainMenuMusicOptions = new();
	private readonly System.Collections.Generic.List<(Label Label, string Key)> _localizedLabels = new();
	private readonly System.Collections.Generic.List<(Button Button, string Key)> _localizedButtons = new();

	private readonly struct MainMenuMusicOption
	{
		public MainMenuMusicOption(string id, string displayName, string absolutePath)
		{
			Id = id;
			DisplayName = displayName;
			AbsolutePath = absolutePath;
		}

		public string Id { get; }
		public string DisplayName { get; }
		public string AbsolutePath { get; }
	}

	private readonly struct ResolutionPreset
	{
		public ResolutionPreset(int width, int height)
		{
			Size = new Vector2I(width, height);
			Label = $"{width} x {height}";
		}

		public Vector2I Size { get; }
		public string Label { get; }
	}

	public override void _Ready()
	{
		GameInputActions.EnsureRegistered();
		Localization.EnsureLoaded();
		_menuCamera = GetNodeOrNull<Camera2D>(MenuCameraPath);
		_menuCameraHome = GetNodeOrNull<Node2D>(MenuCameraHomePath);
		_menuCameraPushIn = GetNodeOrNull<Node2D>(MenuCameraPushInPath);
		_menuCameraAdventureDive = GetNodeOrNull<Node2D>(MenuCameraAdventureDivePath);
		SyncMenuCameraToViewport();
		_backgroundAnimator = GetNodeOrNull<MainMenuBackgroundAnimator>(BackgroundArtPath);
		_oceanDynamicsLayer = GetNodeOrNull<MainMenuOceanDynamics>(OceanDynamicsLayerPath);
		_underwaterAmbientLayer = GetNodeOrNull<UnderwaterAmbientLayer>(UnderwaterAmbientLayerPath);
		_waterCausticOverlay = GetNodeOrNull<CanvasItem>(WaterCausticOverlayPath);

		_singlePlayerButton = GetNode<Button>(SinglePlayerButtonPath);
		_singlePlayerButton.Pressed += StartSinglePlayerMenu;

		_adventureButton = GetNode<Button>(AdventureButtonPath);
		_adventureButton.Pressed += StartAdventureMode;

		_multiplayerButton = GetNode<Button>(MultiplayerButtonPath);
		_multiplayerButton.Pressed += StartMultiplayerLobby;

		_settingsButton = GetNode<Button>(SettingsButtonPath);
		_settingsButton.Pressed += ShowSettingsDialog;

		_exitButton = GetNode<Button>(ExitButtonPath);
		_exitButton.Pressed += ShowExitDialog;
		SuppressDefaultPressedSfx(_singlePlayerButton);
		SuppressDefaultPressedSfx(_adventureButton);
		SuppressDefaultPressedSfx(_settingsButton);
		SuppressDefaultPressedSfx(_exitButton);

		_titleLabel = GetNode<Label>(TitleLabelPath);
		_subtitleLabel = GetNode<Label>(SubtitleLabelPath);

		_avatarEntry = GetNode<MainMenuAvatarEntry>(AvatarEntryPath);
		SuppressDefaultPressedSfx(_avatarEntry.Button);
		_avatarEntry.AvatarPressed += OpenAvatarEditor;

		BuildPlayerCountDialog();
		BuildSettingsDialog();
		BuildControlsDialog();
		BuildExitDialog();
		ConfigureMainMenuFocus();
		LoadSettings();
		ApplySelectedAvatar();
		ApplyPlayerName();
		SyncSettingsControls();
		ApplySettings();
		RefreshLocalizedText();
		if (_playAdventureReturnTransition)
		{
			SetMenuCameraPushInInitial();
			_avatarEntry.Visible = false;
			SetMainMenuButtonsDisabled(true);
			_playAdventureReturnTransition = false;
			PlayAdventureReturnTransition();
		}
		else
		{
			PrepareAdventureAvatarMenuCache();
			ShowPendingAdventureAvatarUnlockPrompt();
			CallDeferred(nameof(FocusInitialMainMenuButton));
		}
		PrepareBackgroundMusic();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && IsNodeReady())
		{
			RefreshLocalizedText();
		}

		if (what == NotificationResized && IsNodeReady())
		{
			SyncMenuCameraToViewport();
			UpdatePlayerCountDialogPanelPosition();
			UpdateSettingsDialogPosition();
			UpdateControlsDialogPosition();
			UpdateExitDialogPosition();
			UpdateAvatarEditorPosition();
			UpdateAdventureTransitionPosition();
		}
	}

	private async void StartSinglePlayerMenu()
	{
		if (!BeginMenuCameraPushIn(true))
		{
			return;
		}

		_avatarEntry.Visible = false;
		ShowPlayerCountDialog();
		await CompleteMenuCameraPushIn();
	}

	public override void _Input(InputEvent inputEvent)
	{
		TryHandleControlsBindingCapture(inputEvent);
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (TryHandleControlsBindingCapture(inputEvent))
		{
			return;
		}

		if (_controlsDialogRoot.Visible && GameInputActions.IsCancelPressed(inputEvent))
		{
			if (_pendingBindingActionId != null)
			{
				ClearPendingBindingCapture();
				RefreshControlsDialogText();
			}
			else
			{
				HideControlsDialog();
			}

			GetViewport().SetInputAsHandled();
			return;
		}

		if (_settingsDialogRoot.Visible && GameInputActions.IsCancelPressed(inputEvent))
		{
			HideSettingsDialog();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_playerCountDialogRoot.Visible && GameInputActions.IsCancelPressed(inputEvent))
		{
			HidePlayerCountDialog();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_exitDialogRoot.Visible && GameInputActions.IsCancelPressed(inputEvent))
		{
			HideExitDialog();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!IsMenuOverlayVisible() && GameInputActions.IsCancelPressed(inputEvent))
		{
			ShowExitDialog();
			GetViewport().SetInputAsHandled();
		}
	}

	private GameAudioManager? GameAudio => _gameAudio ??= GetNodeOrNull<GameAudioManager>("/root/GameAudio");

	private void PlayMenuSfx(
		string sfxId,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		GameAudio?.PlaySfx(sfxId, volumeDb, pitchMin, pitchMax, cooldownSeconds);
	}

	private static void SuppressDefaultPressedSfx(BaseButton? button)
	{
		if (button == null || !GodotObject.IsInstanceValid(button))
		{
			return;
		}

		button.SetMeta(GameAudioManager.UiSuppressPressedSfxMeta, true);
	}

}
