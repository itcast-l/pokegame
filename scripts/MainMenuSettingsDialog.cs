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
	private Label _controlsLabel = default!;
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
	public OptionButton WindowModeOption { get; private set; } = default!;
	public OptionButton ResolutionOption { get; private set; } = default!;
	public OptionButton LanguageOption { get; private set; } = default!;
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
		_titleLabel = GetNode<Label>("%SettingsTitle");
		_messageLabel = GetNode<Label>("%SettingsMessage");
		_masterVolumeLabel = GetNode<Label>("%MasterVolumeLabel");
		_musicVolumeLabel = GetNode<Label>("%MusicVolumeLabel");
		_muteBgmLabel = GetNode<Label>("%MuteBgmLabel");
		_muteSfxLabel = GetNode<Label>("%MuteSfxLabel");
		_windowModeLabel = GetNode<Label>("%WindowModeLabel");
		_resolutionLabel = GetNode<Label>("%ResolutionLabel");
		_languageLabel = GetNode<Label>("%LanguageLabel");
		_controlsLabel = GetNode<Label>("%ControlsLabel");
		_settingsScroll = GetNode<ScrollContainer>("%SettingsScroll");
		_controlsButton = GetNode<Button>("%ControlsButton");
		_resetButton = GetNode<Button>("%ResetDefaultsButton");
		_doneButton = GetNode<Button>("%DoneButton");
		_scrim = GetNode<ColorRect>("%Scrim");

		_scrim.GuiInput += OnScrimGuiInput;
		MainMenuInteractiveStyle.ApplyThemedSlider(MasterVolumeSlider, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedSlider(MusicVolumeSlider, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedToggle(MuteBgmToggle, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedToggle(MuteSfxToggle, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedControl(WindowModeOption, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedControl(ResolutionOption, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedControl(LanguageOption, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedControl(_controlsButton, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyActionButton(_resetButton, 20, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyActionButton(_doneButton, 20, MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
		ConfigureUnifiedVisualStyle();
		MuteBgmToggle.Toggled += _ => UpdateToggleText(MuteBgmToggle);
		MuteSfxToggle.Toggled += _ => UpdateToggleText(MuteSfxToggle);
		EnableKeyboardFocus(
			MasterVolumeSlider,
			MusicVolumeSlider,
			MuteBgmToggle,
			MuteSfxToggle,
			WindowModeOption,
			ResolutionOption,
			LanguageOption,
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
			_controlsButton);
		SecondaryMenuGlassStyle.StyleScrollBar(_settingsScroll.GetVScrollBar(), SecondaryMenuGlassStyle.UnifiedVisual);
		SecondaryMenuGlassStyle.Attach(
			this,
			DialogPanel,
			_scrim,
			new Vector2(620.0f, 580.0f),
			SecondaryMenuGlassStyle.UnifiedVisual,
			GetNode<PanelContainer>("%SettingsContentPanel"));
	}

	private void ConfigureUnifiedVisualStyle()
	{
		Color utilityAccent = MainMenuInteractiveStyle.SecondaryMenuPalette.ActiveBorder;
		foreach (string frameName in new[]
		{
			"MasterVolumeRowPanel",
			"MusicVolumeRowPanel",
			"MuteBgmRowPanel",
			"MuteSfxRowPanel",
			"WindowModeRowPanel",
			"ResolutionRowPanel",
			"LanguageRowPanel",
			"ControlsRowPanel",
		})
		{
			ConfigureFrame(frameName, OceanMenuButtonFrame.ButtonFrameStyle.IndividualToolRow, utilityAccent);
		}

		ConfigureFrame("ResetDefaultsFrame", OceanMenuButtonFrame.ButtonFrameStyle.IndividualAction, MainMenuInteractiveStyle.SecondaryMenuPalette.ActiveBorder);
		ConfigureFrame("DoneFrame", OceanMenuButtonFrame.ButtonFrameStyle.IndividualAction, MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette.ActiveBorder);

		_titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.98f, 0.97f));
		_messageLabel.AddThemeColorOverride("font_color", new Color(0.66f, 0.96f, 0.92f));
		foreach (Label label in new[]
		{
			_masterVolumeLabel,
			_musicVolumeLabel,
			_muteBgmLabel,
			_muteSfxLabel,
			_windowModeLabel,
			_resolutionLabel,
			_languageLabel,
			_controlsLabel,
		})
		{
			label.AddThemeColorOverride("font_color", new Color(0.84f, 0.96f, 0.94f));
		}
		MasterVolumeValue.AddThemeColorOverride("font_color", new Color(0.72f, 0.98f, 0.93f));
		MusicVolumeValue.AddThemeColorOverride("font_color", new Color(0.72f, 0.98f, 0.93f));
		if (FindChild("TitleUnderline", true, false) is ColorRect underline)
		{
			underline.Color = new Color(0.62f, 1.0f, 0.94f, 0.58f);
		}
	}

	private void ConfigureFrame(
		string frameName,
		OceanMenuButtonFrame.ButtonFrameStyle style,
		Color accent)
	{
		if (FindChild(frameName, true, false) is not OceanMenuButtonFrame frame)
		{
			return;
		}

		frame.FrameStyle = style;
		frame.AccentColor = accent;
		frame.QueueRedraw();
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
