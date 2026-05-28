using System;
using System.Threading.Tasks;
using Godot;

public partial class MainMenuScene : Control
{
	private const string BackgroundMusicBankLabel = "main_menu";
	private const string BackgroundMusicTrackName = "background";
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
	private const string AvatarEntryPath = "AvatarEntryLayer/MainMenuAvatarEntry";
	private const string GameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const string AdventureAvatarMenuScenePath = "res://scenes/adventure_avatar_menu_scene.tscn";
	private const string AvatarEditScenePath = "res://scenes/character_avatar_edit_scene.tscn";
	private const string SettingsPanelTexturePath = "res://art/ui/settings_menu/run-002/processed/clean.png";
	private const string SettingsConfigPath = "user://settings.cfg";
	private const float DialogFadeSeconds = 0.14f;
	private const int AvatarEditDialogLayer = 42;
	private const int MinMenuPlayers = 2;
	private const int MaxMenuPlayers = 4;
	private const float DefaultMasterVolume = 0.82f;
	private const float DefaultMusicVolume = 0.76f;
	private const float DefaultMotionScale = 1.0f;
	private const bool DefaultMuteAudio = false;
	private const bool DefaultFullscreen = false;
	private const bool DefaultOceanMotion = true;
	private const bool DefaultItemMode = true;
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
	private MainMenuBackgroundAnimator? _backgroundAnimator;
	private MainMenuOceanDynamics? _oceanDynamicsLayer;
	private UnderwaterAmbientLayer? _underwaterAmbientLayer;
	private CanvasItem? _waterCausticOverlay;
	private Control _playerCountDialogRoot = default!;
	private Control _playerCountDialogMotion = default!;
	private PanelContainer _playerCountDialogPanel = default!;
	private Tween? _playerCountDialogTween;
	private OptionButton _playerCountOption = default!;
	private OptionButton _itemModeOption = default!;
	private OptionButton _winningScoreOption = default!;
	private Control _settingsDialogRoot = default!;
	private Control _settingsDialogPanel = default!;
	private Tween? _settingsDialogTween;
	private HSlider _masterVolumeSlider = default!;
	private HSlider _musicVolumeSlider = default!;
	private HSlider _motionScaleSlider = default!;
	private Label _masterVolumeValue = default!;
	private Label _musicVolumeValue = default!;
	private Label _motionScaleValue = default!;
	private CheckButton _muteAudioToggle = default!;
	private CheckButton _oceanMotionToggle = default!;
	private OptionButton _windowModeOption = default!;
	private OptionButton _languageOption = default!;
	private MainMenuAvatarEntry _avatarEntry = default!;
	private Control _exitDialogRoot = default!;
	private PanelContainer _exitDialogPanel = default!;
	private Tween? _exitDialogTween;
	private CanvasLayer? _avatarEditDialogLayer;
	private Control? _adventureTransitionRoot;
	private Control? _adventureTransitionMotion;
	private Tween? _adventureTransitionTween;
	private Control? _preparedAdventureAvatarMenu;
	private bool _playAdventureReturnTransition;
	private float _masterVolume = DefaultMasterVolume;
	private float _musicVolume = DefaultMusicVolume;
	private float _motionScale = DefaultMotionScale;
	private bool _muteAudio = DefaultMuteAudio;
	private bool _fullscreen = DefaultFullscreen;
	private bool _oceanMotion = DefaultOceanMotion;
	private string _languagePreference = Localization.DefaultLanguagePreference;
	private string _selectedAvatarId = CharacterAvatarCatalog.DefaultAvatarId;
	private bool _isSyncingSettingsControls;
	private bool _isStartingGameTransition;
	private bool _isMenuCameraTransitioning;
	private ulong _menuCameraTransitionStartMsec;
	private float _menuCameraTransitionTotalSeconds;
	private float _appliedMusicVolumeDb = float.NaN;
	private readonly System.Collections.Generic.List<(Label Label, string Key)> _localizedLabels = new();
	private readonly System.Collections.Generic.List<(Button Button, string Key)> _localizedButtons = new();

	public void PrepareAdventureReturnTransition()
	{
		_playAdventureReturnTransition = true;
	}

	private async void PlayAdventureReturnTransition()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		ResetMenuCamera();
		await ToSignal(
			GetTree().CreateTimer(GetMenuCameraHomeTotalSeconds()),
			SceneTreeTimer.SignalName.Timeout);

		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		_adventureTransitionRoot?.QueueFree();
		_adventureTransitionRoot = null;
		_adventureTransitionMotion = null;
		_avatarEntry.Visible = true;
		SetMainMenuButtonsDisabled(false);
		PrepareAdventureAvatarMenuCache();
		ShowPendingAdventureAvatarUnlockPrompt();
	}

	public override void _Ready()
	{
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

		_settingsButton = GetNode<Button>(SettingsButtonPath);
		_settingsButton.Pressed += ShowSettingsDialog;

		_exitButton = GetNode<Button>(ExitButtonPath);
		_exitButton.Pressed += ShowExitDialog;

		_avatarEntry = GetNode<MainMenuAvatarEntry>(AvatarEntryPath);
		_avatarEntry.AvatarPressed += OpenAvatarEditor;

		BuildPlayerCountDialog();
		BuildSettingsDialog();
		BuildExitDialog();
		LoadSettings();
		ApplySelectedAvatar();
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
			UpdateExitDialogPosition();
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

	private bool BeginMenuCameraPushIn(bool startCameraImmediately)
	{
		if (_isMenuCameraTransitioning)
		{
			return false;
		}

		_isMenuCameraTransitioning = true;
		SetMainMenuButtonsDisabled(true);
		if (startCameraImmediately)
		{
			SetMenuCameraPushInActive();
		}

		return true;
	}

	private bool BeginMenuCameraAdventureDive()
	{
		if (_isMenuCameraTransitioning)
		{
			return false;
		}

		_isMenuCameraTransitioning = true;
		SetMainMenuButtonsDisabled(true);
		if (!SetMenuCameraAdventureDiveActive())
		{
			SetMenuCameraPushInActive();
		}

		return true;
	}

	private void CancelMenuCameraPushIn()
	{
		ResetMenuCamera();
		SetMainMenuButtonsDisabled(false);
		_isMenuCameraTransitioning = false;
	}

	private async Task<bool> CompleteMenuCameraPushIn()
	{
		return await CompleteMenuCameraTransition();
	}

	private async Task<bool> CompleteMenuCameraAdventureDive()
	{
		return await CompleteMenuCameraTransition();
	}

	private async Task<bool> CompleteMenuCameraTransition()
	{
		try
		{
			await WaitForMenuCameraTransition();
			return GodotObject.IsInstanceValid(this);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(this))
			{
				SetMainMenuButtonsDisabled(false);
				_isMenuCameraTransitioning = false;
			}
		}
	}

	private async Task WaitForMenuCameraTransition()
	{
		SceneTree tree = GetTree();
		await ToSignal(
			tree.CreateTimer(GetMenuCameraTransitionRemainingSeconds()),
			SceneTreeTimer.SignalName.Timeout);
	}

	private float GetMenuCameraPushInTotalSeconds()
	{
		return GetMenuCameraPushInDuration() + MenuCameraTransitionPaddingSeconds;
	}

	private float GetMenuCameraAdventureDiveTotalSeconds()
	{
		return GetMenuCameraAdventureDiveDuration() + MenuCameraTransitionPaddingSeconds;
	}

	private float GetMenuCameraHomeTotalSeconds()
	{
		return GetMenuCameraHomeDuration() + MenuCameraTransitionPaddingSeconds;
	}

	private float GetMenuCameraHomeDuration()
	{
		if (_menuCameraHome == null)
		{
			return 0.0f;
		}

		return Mathf.Max(0.0f, _menuCameraHome.Call("get_tween_duration").AsSingle());
	}

	private float GetMenuCameraPushInDuration()
	{
		if (_menuCameraPushIn == null)
		{
			return 0.0f;
		}

		return Mathf.Max(0.0f, _menuCameraPushIn.Call("get_tween_duration").AsSingle());
	}

	private float GetMenuCameraAdventureDiveDuration()
	{
		if (_menuCameraAdventureDive == null)
		{
			return GetMenuCameraPushInDuration();
		}

		return Mathf.Max(0.0f, _menuCameraAdventureDive.Call("get_tween_duration").AsSingle());
	}

	private float GetMenuCameraTransitionRemainingSeconds()
	{
		float elapsedSeconds = 0.0f;
		if (_menuCameraTransitionStartMsec > 0)
		{
			elapsedSeconds = (Time.GetTicksMsec() - _menuCameraTransitionStartMsec) / 1000.0f;
		}

		return Mathf.Max(0.0f, _menuCameraTransitionTotalSeconds - elapsedSeconds);
	}

	private void StartMenuCameraTransition(float totalSeconds)
	{
		_menuCameraTransitionStartMsec = Time.GetTicksMsec();
		_menuCameraTransitionTotalSeconds = Mathf.Max(0.0f, totalSeconds);
	}

	private void ClearMenuCameraTransition()
	{
		_menuCameraTransitionStartMsec = 0;
		_menuCameraTransitionTotalSeconds = 0.0f;
	}

	private void SetMenuCameraPushInActive()
	{
		if (_menuCameraHome == null || _menuCameraPushIn == null)
		{
			return;
		}

		_menuCameraPushIn.Position = GetViewportRect().Size * 0.5f;
		StartMenuCameraTransition(GetMenuCameraPushInTotalSeconds());
		_menuCameraAdventureDive?.Call("set_priority", MenuCameraInactivePriority);
		_menuCameraHome.Call("set_priority", MenuCameraHomePriority);
		_menuCameraPushIn.Call("set_priority", MenuCameraPushInPriority);
	}

	private bool SetMenuCameraAdventureDiveActive()
	{
		if (_menuCameraHome == null || _menuCameraAdventureDive == null)
		{
			return false;
		}

		_menuCameraAdventureDive.Position = GetMenuCameraAdventureDivePosition();
		StartMenuCameraTransition(GetMenuCameraAdventureDiveTotalSeconds());
		_menuCameraPushIn?.Call("set_priority", MenuCameraInactivePriority);
		_menuCameraHome.Call("set_priority", MenuCameraHomePriority);
		_menuCameraAdventureDive.Call("set_priority", MenuCameraAdventureDivePriority);
		return true;
	}

	private void SetMenuCameraPushInInitial()
	{
		if (_menuCameraPushIn == null)
		{
			return;
		}

		Vector2 center = GetViewportRect().Size * 0.5f;
		_menuCameraPushIn.Position = center;
		if (_menuCamera != null)
		{
			_menuCamera.Position = center;
			_menuCamera.Zoom = GetMenuCameraPushInZoom();
		}

		if (_menuCameraHome != null)
		{
			_menuCameraHome.Call("set_priority", MenuCameraHomePriority);
		}

		_menuCameraAdventureDive?.Call("set_priority", MenuCameraInactivePriority);
		_menuCameraPushIn.Call("set_priority", MenuCameraPushInPriority);
		ClearMenuCameraTransition();
	}

	private void ResetMenuCamera()
	{
		if (_menuCameraHome == null || _menuCameraPushIn == null)
		{
			return;
		}

		ClearMenuCameraTransition();
		_menuCameraPushIn.Call("set_priority", MenuCameraInactivePriority);
		_menuCameraAdventureDive?.Call("set_priority", MenuCameraInactivePriority);
		_menuCameraHome.Call("set_priority", MenuCameraHomePriority);
	}

	private void SyncMenuCameraToViewport()
	{
		Vector2 center = GetViewportRect().Size * 0.5f;

		if (_menuCamera != null)
		{
			_menuCamera.Position = center;
		}

		if (_menuCameraHome != null)
		{
			_menuCameraHome.Position = center;
		}

		if (_menuCameraPushIn != null)
		{
			_menuCameraPushIn.Position = center;
		}

		if (_menuCameraAdventureDive != null)
		{
			_menuCameraAdventureDive.Position = GetMenuCameraAdventureDivePosition();
		}
	}

	private Vector2 GetSecondaryMenuDepthPosition()
	{
		return GetSecondaryMenuDepthPosition(GetViewportRect().Size);
	}

	private Vector2 GetSecondaryMenuDepthPosition(Vector2 contentSize)
	{
		return GetSecondaryMenuDepthPosition(
			contentSize,
			GetViewportRect().Size * 0.5f,
			GetMenuCameraPushInZoom());
	}

	private Vector2 GetSecondaryMenuWorldScale()
	{
		return GetSecondaryMenuWorldScale(GetViewportRect().Size);
	}

	private Vector2 GetSecondaryMenuWorldScale(Vector2 contentSize)
	{
		return GetSecondaryMenuWorldScale(contentSize, GetMenuCameraPushInZoom());
	}

	private Vector2 GetAdventureSecondaryMenuDepthPosition()
	{
		return GetSecondaryMenuDepthPosition(
			GetViewportRect().Size,
			GetMenuCameraAdventureDivePosition(),
			GetMenuCameraAdventureDiveZoom());
	}

	private Vector2 GetAdventureSecondaryMenuWorldScale()
	{
		return GetSecondaryMenuWorldScale(GetViewportRect().Size, GetMenuCameraAdventureDiveZoom());
	}

	private Vector2 GetSecondaryMenuDepthPosition(Vector2 contentSize, Vector2 cameraPosition, Vector2 zoom)
	{
		Vector2 scale = GetSecondaryMenuWorldScale(contentSize, zoom);
		return cameraPosition - contentSize * scale * 0.5f;
	}

	private Vector2 GetSecondaryMenuWorldScale(Vector2 contentSize, Vector2 zoom)
	{
		Vector2 viewportSize = GetViewportRect().Size;
		return new Vector2(
			viewportSize.X / Mathf.Max(contentSize.X, 1.0f) / zoom.X,
			viewportSize.Y / Mathf.Max(contentSize.Y, 1.0f) / zoom.Y);
	}

	private Vector2 GetMenuCameraPushInZoom()
	{
		if (_menuCameraPushIn == null)
		{
			return Vector2.One;
		}

		Vector2 zoom = _menuCameraPushIn.Call("get_zoom").AsVector2();
		return new Vector2(
			Mathf.Max(zoom.X, 0.001f),
			Mathf.Max(zoom.Y, 0.001f));
	}

	private Vector2 GetMenuCameraAdventureDiveZoom()
	{
		if (_menuCameraAdventureDive == null)
		{
			return GetMenuCameraPushInZoom();
		}

		Vector2 zoom = _menuCameraAdventureDive.Call("get_zoom").AsVector2();
		return new Vector2(
			Mathf.Max(zoom.X, 0.001f),
			Mathf.Max(zoom.Y, 0.001f));
	}

	private Vector2 GetMenuCameraAdventureDivePosition()
	{
		return GetViewportRect().Size * 0.5f;
	}

	private void SetMainMenuButtonsDisabled(bool disabled)
	{
		_singlePlayerButton.Disabled = disabled;
		_adventureButton.Disabled = disabled;
		_multiplayerButton.Disabled = disabled;
		_settingsButton.Disabled = disabled;
		_exitButton.Disabled = disabled;
	}

	private async void PrepareBackgroundMusic()
	{
		Node? musicManager = GetMusicManager();
		if (musicManager == null)
		{
			GD.PushWarning("Missing Resonate MusicManager autoload; background music will not play.");
			return;
		}

		if (!musicManager.Get("has_loaded").AsBool())
		{
			await ToSignal(musicManager, "loaded");
			if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(musicManager))
			{
				return;
			}
		}

		if (!musicManager.Call("is_playing", BackgroundMusicBankLabel, BackgroundMusicTrackName).AsBool())
		{
			bool started = musicManager.Call(
				"play",
				BackgroundMusicBankLabel,
				BackgroundMusicTrackName,
				BackgroundMusicFadeSeconds,
				true).AsBool();
			if (!started)
			{
				return;
			}
		}

		musicManager.Call(
			"stop_on_exit",
			this,
			BackgroundMusicBankLabel,
			BackgroundMusicTrackName,
			BackgroundMusicFadeSeconds);
	}

	private Node? GetMusicManager()
	{
		return GetNodeOrNull<Node>("/root/MusicManager");
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (_settingsDialogRoot.Visible && inputEvent.IsActionPressed("ui_cancel"))
		{
			HideSettingsDialog();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_playerCountDialogRoot.Visible && inputEvent.IsActionPressed("ui_cancel"))
		{
			HidePlayerCountDialog();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_exitDialogRoot.Visible && inputEvent.IsActionPressed("ui_cancel"))
		{
			HideExitDialog();
			GetViewport().SetInputAsHandled();
		}
	}

}
