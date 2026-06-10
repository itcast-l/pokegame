using System;
using Godot;

public partial class MainMenuSettingsDialog : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	private Label _titleLabel = default!;
	private Label _messageLabel = default!;
	private Label _masterVolumeLabel = default!;
	private Label _musicVolumeLabel = default!;
	private Label _muteBgmLabel = default!;
	private Label _muteSfxLabel = default!;
	private Label _windowModeLabel = default!;
	private Label _resolutionLabel = default!;
	private Label _languageLabel = default!;
	private Label _mainMenuMusicTrackLabel = default!;
	private Label _controlsLabel = default!;
	private Label _oceanMotionLabel = default!;
	private Button _controlsButton = default!;
	private Button _resetButton = default!;
	private Button _doneButton = default!;
	private ColorRect _scrim = default!;
	private ScrollContainer _settingsScroll = default!;
	private Func<string, string>? _localize;

	public Control DialogPanel { get; private set; } = default!;
	public HSlider MasterVolumeSlider { get; private set; } = default!;
	public HSlider MusicVolumeSlider { get; private set; } = default!;
	public Label MasterVolumeValue { get; private set; } = default!;
	public Label MusicVolumeValue { get; private set; } = default!;
	public CheckButton MuteBgmToggle { get; private set; } = default!;
	public CheckButton MuteSfxToggle { get; private set; } = default!;
	public CheckButton OceanMotionToggle { get; private set; } = default!;
	public OptionButton WindowModeOption { get; private set; } = default!;
	public OptionButton ResolutionOption { get; private set; } = default!;
	public OptionButton LanguageOption { get; private set; } = default!;
	public OptionButton MainMenuMusicTrackOption { get; private set; } = default!;
	public Button ControlsButton => _controlsButton;
	public Button ResetDefaultsButton => _resetButton;
	public Button DoneButton => _doneButton;

	public override void _Ready()
	{
		DialogPanel = GetNode<Control>("%SettingsDialogPanel");
		MasterVolumeSlider = GetNode<HSlider>("%MasterVolumeSlider");
		MusicVolumeSlider = GetNode<HSlider>("%MusicVolumeSlider");
		MasterVolumeValue = GetNode<Label>("%MasterVolumeValue");
		MusicVolumeValue = GetNode<Label>("%MusicVolumeValue");
		MuteBgmToggle = GetNode<CheckButton>("%MuteBgmToggle");
		MuteSfxToggle = GetNode<CheckButton>("%MuteSfxToggle");
		OceanMotionToggle = GetNode<CheckButton>("%OceanMotionToggle");
		WindowModeOption = GetNode<OptionButton>("%WindowModeOption");
		ResolutionOption = GetNode<OptionButton>("%ResolutionOption");
		LanguageOption = GetNode<OptionButton>("%LanguageOption");
		MainMenuMusicTrackOption = GetNode<OptionButton>("%MainMenuMusicTrackOption");
		_titleLabel = GetNode<Label>("%SettingsTitle");
		_messageLabel = GetNode<Label>("%SettingsMessage");
		_masterVolumeLabel = GetNode<Label>("%MasterVolumeLabel");
		_musicVolumeLabel = GetNode<Label>("%MusicVolumeLabel");
		_muteBgmLabel = GetNode<Label>("%MuteBgmLabel");
		_muteSfxLabel = GetNode<Label>("%MuteSfxLabel");
		_windowModeLabel = GetNode<Label>("%WindowModeLabel");
		_resolutionLabel = GetNode<Label>("%ResolutionLabel");
		_languageLabel = GetNode<Label>("%LanguageLabel");
		_mainMenuMusicTrackLabel = GetNode<Label>("%MainMenuMusicTrackLabel");
		_controlsLabel = GetNode<Label>("%ControlsLabel");
		_oceanMotionLabel = GetNode<Label>("%OceanMotionLabel");
		_settingsScroll = GetNode<ScrollContainer>("%SettingsScroll");
		_controlsButton = GetNode<Button>("%ControlsButton");
		_resetButton = GetNode<Button>("%ResetDefaultsButton");
		_doneButton = GetNode<Button>("%DoneButton");
		_scrim = GetNode<ColorRect>("%Scrim");

		_scrim.GuiInput += OnScrimGuiInput;
		MainMenuInteractiveStyle.ApplyToggleButton(MuteBgmToggle);
		MainMenuInteractiveStyle.ApplyToggleButton(MuteSfxToggle);
		MainMenuInteractiveStyle.ApplyToggleButton(OceanMotionToggle);
		MainMenuInteractiveStyle.ApplyOptionLikeButton(WindowModeOption);
		MainMenuInteractiveStyle.ApplyOptionLikeButton(ResolutionOption);
		MainMenuInteractiveStyle.ApplyOptionLikeButton(LanguageOption);
		MainMenuInteractiveStyle.ApplyOptionLikeButton(MainMenuMusicTrackOption);
		MainMenuInteractiveStyle.ApplyOptionLikeButton(_controlsButton);
		MainMenuInteractiveStyle.ApplyActionButton(_resetButton, 20);
		MainMenuInteractiveStyle.ApplyActionButton(_doneButton, 20);
		MuteBgmToggle.Toggled += _ => UpdateToggleText(MuteBgmToggle);
		MuteSfxToggle.Toggled += _ => UpdateToggleText(MuteSfxToggle);
		OceanMotionToggle.Toggled += _ => UpdateToggleText(OceanMotionToggle);
		EnableKeyboardFocus(
			MasterVolumeSlider,
			MusicVolumeSlider,
			MuteBgmToggle,
			MuteSfxToggle,
			WindowModeOption,
			ResolutionOption,
			LanguageOption,
			MainMenuMusicTrackOption,
			_controlsButton,
			OceanMotionToggle,
			_resetButton,
			_doneButton);
		StyleScrollBar(_settingsScroll.GetVScrollBar());
	}

	public void RefreshLocalizedText(Func<string, string> localize)
	{
		_localize = localize;
		_titleLabel.Text = localize("SETTINGS_TITLE");
		_messageLabel.Text = localize("SETTINGS_MESSAGE");
		_masterVolumeLabel.Text = localize("SETTINGS_MASTER_VOLUME");
		_musicVolumeLabel.Text = localize("SETTINGS_MUSIC_VOLUME");
		_muteBgmLabel.Text = localize("SETTINGS_MUTE_BGM");
		_muteSfxLabel.Text = localize("SETTINGS_MUTE_SFX");
		_windowModeLabel.Text = localize("SETTINGS_WINDOW_MODE");
		_resolutionLabel.Text = localize("SETTINGS_RESOLUTION");
		_languageLabel.Text = localize("SETTINGS_LANGUAGE");
		_mainMenuMusicTrackLabel.Text = localize("SETTINGS_BACKGROUND_MUSIC");
		_controlsLabel.Text = localize("SETTINGS_CONTROLS");
		_controlsButton.Text = localize("SETTINGS_CONTROLS_EDIT");
		_oceanMotionLabel.Text = localize("SETTINGS_OCEAN_MOTION");
		_resetButton.Text = localize("SETTINGS_RESET_DEFAULTS");
		_doneButton.Text = localize("SETTINGS_DONE");
		UpdateToggleText(MuteBgmToggle);
		UpdateToggleText(MuteSfxToggle);
		UpdateToggleText(OceanMotionToggle);
	}

	private void OnScrimGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		EmitSignal(SignalName.CloseRequested);
		GetViewport().SetInputAsHandled();
	}

	private static void EnableKeyboardFocus(params Control[] controls)
	{
		foreach (Control control in controls)
		{
			control.FocusMode = Control.FocusModeEnum.All;
		}
	}

	private void UpdateToggleText(CheckButton toggle)
	{
		if (_localize == null)
		{
			return;
		}

		toggle.Text = _localize(toggle.ButtonPressed ? "PLAYER_COUNT_ITEM_MODE_ON" : "PLAYER_COUNT_ITEM_MODE_OFF");
	}

	private static void StyleScrollBar(ScrollBar scrollBar)
	{
		scrollBar.CustomMinimumSize = new Vector2(10.0f, 0.0f);
		scrollBar.AddThemeConstantOverride("padding_left", 2);
		scrollBar.AddThemeConstantOverride("padding_right", 2);

		var track = new StyleBoxFlat
		{
			BgColor = new Color(0.03f, 0.14f, 0.18f, 0.82f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginTop = 4.0f,
			ContentMarginBottom = 4.0f,
		};
		var grabber = new StyleBoxFlat
		{
			BgColor = new Color(0.78f, 1.0f, 0.96f, 0.68f),
			BorderColor = new Color(0.18f, 0.64f, 0.68f, 0.92f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};
		var grabberHighlight = new StyleBoxFlat
		{
			BgColor = new Color(0.94f, 1.0f, 0.98f, 0.86f),
			BorderColor = new Color(0.30f, 0.88f, 0.86f, 1.0f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};

		scrollBar.AddThemeStyleboxOverride("scroll", track);
		scrollBar.AddThemeStyleboxOverride("scroll_focus", track);
		scrollBar.AddThemeStyleboxOverride("grabber", grabber);
		scrollBar.AddThemeStyleboxOverride("grabber_highlight", grabberHighlight);
		scrollBar.AddThemeStyleboxOverride("grabber_pressed", grabberHighlight);
	}
}
