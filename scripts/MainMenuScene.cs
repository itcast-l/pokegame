using System;
using Godot;

public partial class MainMenuScene : Control
{
	private const string BackgroundMusicPath = "BackgroundMusic";
	private const string OceanDynamicsLayerPath = "OceanDynamicsLayer";
	private const string UnderwaterAmbientLayerPath = "UnderwaterAmbientLayer";
	private const string WaterCausticOverlayPath = "WaterCausticOverlay";
	private const string SinglePlayerButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SinglePlayerFrame/Button";
	private const string SettingsButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SettingsFrame/Button";
	private const string ExitButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/ExitFrame/Button";
	private const string AvatarEntryPath = "AvatarEntryLayer/MainMenuAvatarEntry";
	private const string GameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const string AvatarEditScenePath = "res://scenes/character_avatar_edit_scene.tscn";
	private const string SettingsPanelTexturePath = "res://art/ui/settings_menu/run-002/processed/clean.png";
	private const string SettingsConfigPath = "user://settings.cfg";
	private const float DialogFadeSeconds = 0.14f;
	private const int AvatarEditDialogLayer = 42;
	private const float DefaultMasterVolume = 0.82f;
	private const float DefaultMusicVolume = 0.76f;
	private const float DefaultMotionScale = 1.0f;
	private const bool DefaultMuteAudio = false;
	private const bool DefaultFullscreen = false;
	private const bool DefaultOceanMotion = true;

	private AudioStreamPlayer _backgroundMusic = default!;
	private MainMenuOceanDynamics? _oceanDynamicsLayer;
	private UnderwaterAmbientLayer? _underwaterAmbientLayer;
	private CanvasItem? _waterCausticOverlay;
	private CanvasLayer _playerCountDialogLayer = default!;
	private Control _playerCountDialogRoot = default!;
	private PanelContainer _playerCountDialogPanel = default!;
	private Tween? _playerCountDialogTween;
	private CanvasLayer _settingsDialogLayer = default!;
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
	private CanvasLayer _exitDialogLayer = default!;
	private Control _exitDialogRoot = default!;
	private PanelContainer _exitDialogPanel = default!;
	private Tween? _exitDialogTween;
	private CanvasLayer? _avatarEditDialogLayer;
	private float _masterVolume = DefaultMasterVolume;
	private float _musicVolume = DefaultMusicVolume;
	private float _motionScale = DefaultMotionScale;
	private bool _muteAudio = DefaultMuteAudio;
	private bool _fullscreen = DefaultFullscreen;
	private bool _oceanMotion = DefaultOceanMotion;
	private bool _isSyncingSettingsControls;

	public override void _Ready()
	{
		PrepareBackgroundMusic();
		_oceanDynamicsLayer = GetNodeOrNull<MainMenuOceanDynamics>(OceanDynamicsLayerPath);
		_underwaterAmbientLayer = GetNodeOrNull<UnderwaterAmbientLayer>(UnderwaterAmbientLayerPath);
		_waterCausticOverlay = GetNodeOrNull<CanvasItem>(WaterCausticOverlayPath);

		Button singlePlayerButton = GetNode<Button>(SinglePlayerButtonPath);
		singlePlayerButton.Pressed += ShowPlayerCountDialog;

		Button settingsButton = GetNode<Button>(SettingsButtonPath);
		settingsButton.Pressed += ShowSettingsDialog;

		Button exitButton = GetNode<Button>(ExitButtonPath);
		exitButton.Pressed += ShowExitDialog;

		MainMenuAvatarEntry avatarEntry = GetNode<MainMenuAvatarEntry>(AvatarEntryPath);
		avatarEntry.AvatarPressed += OpenAvatarEditor;

		BuildPlayerCountDialog();
		BuildSettingsDialog();
		BuildExitDialog();
		LoadSettings();
		SyncSettingsControls();
		ApplySettings();
	}

	private void PrepareBackgroundMusic()
	{
		_backgroundMusic = GetNode<AudioStreamPlayer>(BackgroundMusicPath);
		if (_backgroundMusic.Stream is AudioStreamMP3 backgroundMp3)
		{
			backgroundMp3.Loop = true;
		}

		if (!_backgroundMusic.Playing)
		{
			_backgroundMusic.Play();
		}
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
