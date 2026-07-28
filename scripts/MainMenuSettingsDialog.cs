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
	private Button _controlsButton = default!;
	private Button _backButton = default!;
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
	public OptionButton WindowModeOption { get; private set; } = default!;
	public OptionButton ResolutionOption { get; private set; } = default!;
	public OptionButton LanguageOption { get; private set; } = default!;
	public OptionButton MainMenuMusicTrackOption { get; private set; } = default!;
	public Button ControlsButton => _controlsButton;
	public Button ResetDefaultsButton => _resetButton;
	public Button DoneButton => _doneButton;

	public void FocusDefaultControl()
	{
		_settingsScroll.ScrollVertical = 0;
		MasterVolumeSlider.GrabFocus();
		CallDeferred(nameof(ResetSettingsScrollToTop));
	}

	public override void _Ready()
	{
		DialogPanel = GetNode<Control>("%SettingsDialogPanel");
		MasterVolumeSlider = GetNode<HSlider>("%MasterVolumeSlider");
		MusicVolumeSlider = GetNode<HSlider>("%MusicVolumeSlider");
		MasterVolumeValue = GetNode<Label>("%MasterVolumeValue");
		MusicVolumeValue = GetNode<Label>("%MusicVolumeValue");
		MuteBgmToggle = GetNode<CheckButton>("%MuteBgmToggle");
		MuteSfxToggle = GetNode<CheckButton>("%MuteSfxToggle");
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
		_settingsScroll = GetNode<ScrollContainer>("%SettingsScroll");
		_controlsButton = GetNode<Button>("%ControlsButton");
		_backButton = GetNode<Button>("%BackButton");
		_resetButton = GetNode<Button>("%ResetDefaultsButton");
		_doneButton = GetNode<Button>("%DoneButton");
		_scrim = GetNode<ColorRect>("%Scrim");

		_scrim.GuiInput += OnScrimGuiInput;
		_backButton.Pressed += () => EmitSignal(SignalName.CloseRequested);
		MainMenuInteractiveStyle.ApplyGlassToggleButton(MuteBgmToggle);
		MainMenuInteractiveStyle.ApplyGlassToggleButton(MuteSfxToggle);
		MainMenuInteractiveStyle.ApplyGlassOptionLikeButton(WindowModeOption);
		MainMenuInteractiveStyle.ApplyGlassOptionLikeButton(ResolutionOption);
		MainMenuInteractiveStyle.ApplyGlassOptionLikeButton(LanguageOption);
		MainMenuInteractiveStyle.ApplyGlassOptionLikeButton(MainMenuMusicTrackOption);
		MainMenuInteractiveStyle.ApplyGlassOptionLikeButton(_controlsButton);
		MainMenuInteractiveStyle.ApplyActionButton(_backButton, 18);
		MainMenuInteractiveStyle.ApplyActionButton(_resetButton, 20);
		MainMenuInteractiveStyle.ApplyActionButton(_doneButton, 20);
		MuteBgmToggle.Toggled += _ => UpdateToggleText(MuteBgmToggle);
		MuteSfxToggle.Toggled += _ => UpdateToggleText(MuteSfxToggle);
		EnableKeyboardFocus(
			_backButton,
			MasterVolumeSlider,
			MusicVolumeSlider,
			MuteBgmToggle,
			MuteSfxToggle,
			WindowModeOption,
			ResolutionOption,
			LanguageOption,
			MainMenuMusicTrackOption,
			_controlsButton,
			_resetButton,
			_doneButton);
		ConfigureSettingsFocusScrolling(
			MasterVolumeSlider,
			MusicVolumeSlider,
			MuteBgmToggle,
			MuteSfxToggle,
			WindowModeOption,
			ResolutionOption,
			LanguageOption,
			MainMenuMusicTrackOption,
			_controlsButton);
		SecondaryMenuGlassStyle.StyleScrollBar(_settingsScroll.GetVScrollBar());
		SecondaryMenuGlassStyle.Attach(
			this,
			DialogPanel,
			_scrim,
			new Vector2(620.0f, 580.0f),
			GetNode<PanelContainer>("%SettingsContentPanel"));
	}

	public void RefreshLocalizedText(Func<string, string> localize)
	{
		_localize = localize;
		_titleLabel.Text = localize("SETTINGS_TITLE");
		_backButton.Text = localize("ADVENTURE_BACK");
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
		_resetButton.Text = localize("SETTINGS_RESET_DEFAULTS");
		_doneButton.Text = localize("SETTINGS_DONE");
		UpdateToggleText(MuteBgmToggle);
		UpdateToggleText(MuteSfxToggle);
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

	private void ResetSettingsScrollToTop()
	{
		_settingsScroll.ScrollVertical = 0;
	}

	private void ConfigureSettingsFocusScrolling(params Control[] controls)
	{
		_settingsScroll.FollowFocus = false;
		foreach (Control control in controls)
		{
			control.FocusEntered += () => CallDeferred(nameof(EnsureSettingControlVisible), control);
		}
	}

	private void EnsureSettingControlVisible(Control control)
	{
		if (!GodotObject.IsInstanceValid(control) || !control.IsVisibleInTree())
		{
			return;
		}

		const float padding = 8.0f;
		Rect2 viewportRect = _settingsScroll.GetGlobalRect();
		Rect2 controlRect = control.GetGlobalRect();
		float delta = 0.0f;
		if (controlRect.Position.Y < viewportRect.Position.Y + padding)
		{
			delta = controlRect.Position.Y - viewportRect.Position.Y - padding;
		}
		else if (controlRect.End.Y > viewportRect.End.Y - padding)
		{
			delta = controlRect.End.Y - viewportRect.End.Y + padding;
		}

		if (!Mathf.IsZeroApprox(delta))
		{
			_settingsScroll.ScrollVertical = Mathf.Max(
				0,
				_settingsScroll.ScrollVertical + Mathf.RoundToInt(delta));
		}
	}

}
