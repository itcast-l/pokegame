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
	private Label _muteAudioLabel = default!;
	private Label _windowModeLabel = default!;
	private Label _resolutionLabel = default!;
	private Label _languageLabel = default!;
	private Label _oceanMotionLabel = default!;
	private Label _motionScaleLabel = default!;
	private Button _resetButton = default!;
	private Button _doneButton = default!;
	private ColorRect _scrim = default!;

	public Control DialogPanel { get; private set; } = default!;
	public HSlider MasterVolumeSlider { get; private set; } = default!;
	public HSlider MusicVolumeSlider { get; private set; } = default!;
	public HSlider MotionScaleSlider { get; private set; } = default!;
	public Label MasterVolumeValue { get; private set; } = default!;
	public Label MusicVolumeValue { get; private set; } = default!;
	public Label MotionScaleValue { get; private set; } = default!;
	public CheckButton MuteAudioToggle { get; private set; } = default!;
	public CheckButton OceanMotionToggle { get; private set; } = default!;
	public OptionButton WindowModeOption { get; private set; } = default!;
	public OptionButton ResolutionOption { get; private set; } = default!;
	public OptionButton LanguageOption { get; private set; } = default!;
	public Button ResetDefaultsButton => _resetButton;
	public Button DoneButton => _doneButton;

	public override void _Ready()
	{
		DialogPanel = GetNode<Control>("%SettingsDialogPanel");
		MasterVolumeSlider = GetNode<HSlider>("%MasterVolumeSlider");
		MusicVolumeSlider = GetNode<HSlider>("%MusicVolumeSlider");
		MotionScaleSlider = GetNode<HSlider>("%MotionScaleSlider");
		MasterVolumeValue = GetNode<Label>("%MasterVolumeValue");
		MusicVolumeValue = GetNode<Label>("%MusicVolumeValue");
		MotionScaleValue = GetNode<Label>("%MotionScaleValue");
		MuteAudioToggle = GetNode<CheckButton>("%MuteAudioToggle");
		OceanMotionToggle = GetNode<CheckButton>("%OceanMotionToggle");
		WindowModeOption = GetNode<OptionButton>("%WindowModeOption");
		ResolutionOption = GetNode<OptionButton>("%ResolutionOption");
		LanguageOption = GetNode<OptionButton>("%LanguageOption");
		_titleLabel = GetNode<Label>("%SettingsTitle");
		_messageLabel = GetNode<Label>("%SettingsMessage");
		_masterVolumeLabel = GetNode<Label>("%MasterVolumeLabel");
		_musicVolumeLabel = GetNode<Label>("%MusicVolumeLabel");
		_muteAudioLabel = GetNode<Label>("%MuteAudioLabel");
		_windowModeLabel = GetNode<Label>("%WindowModeLabel");
		_resolutionLabel = GetNode<Label>("%ResolutionLabel");
		_languageLabel = GetNode<Label>("%LanguageLabel");
		_oceanMotionLabel = GetNode<Label>("%OceanMotionLabel");
		_motionScaleLabel = GetNode<Label>("%MotionScaleLabel");
		_resetButton = GetNode<Button>("%ResetDefaultsButton");
		_doneButton = GetNode<Button>("%DoneButton");
		_scrim = GetNode<ColorRect>("%Scrim");

		_scrim.GuiInput += OnScrimGuiInput;
		EnableKeyboardFocus(
			MasterVolumeSlider,
			MusicVolumeSlider,
			MuteAudioToggle,
			WindowModeOption,
			ResolutionOption,
			LanguageOption,
			OceanMotionToggle,
			MotionScaleSlider,
			_resetButton,
			_doneButton);
		StyleOptionPopup(WindowModeOption);
		StyleOptionPopup(ResolutionOption);
		StyleOptionPopup(LanguageOption);
	}

	public void RefreshLocalizedText(Func<string, string> localize)
	{
		_titleLabel.Text = localize("SETTINGS_TITLE");
		_messageLabel.Text = localize("SETTINGS_MESSAGE");
		_masterVolumeLabel.Text = localize("SETTINGS_MASTER_VOLUME");
		_musicVolumeLabel.Text = localize("SETTINGS_MUSIC_VOLUME");
		_muteAudioLabel.Text = localize("SETTINGS_MUTE");
		_windowModeLabel.Text = localize("SETTINGS_WINDOW_MODE");
		_resolutionLabel.Text = localize("SETTINGS_RESOLUTION");
		_languageLabel.Text = localize("SETTINGS_LANGUAGE");
		_oceanMotionLabel.Text = localize("SETTINGS_OCEAN_MOTION");
		_motionScaleLabel.Text = localize("SETTINGS_MOTION_SCALE");
		_resetButton.Text = localize("SETTINGS_RESET_DEFAULTS");
		_doneButton.Text = localize("SETTINGS_DONE");
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

	private static void StyleOptionPopup(OptionButton option)
	{
		PopupMenu popup = option.GetPopup();
		popup.PreferNativeMenu = false;
		popup.ShrinkWidth = false;
		popup.ShrinkHeight = true;
		popup.MinSize = new Vector2I(Mathf.RoundToInt(option.CustomMinimumSize.X), 0);
	}

	private static void EnableKeyboardFocus(params Control[] controls)
	{
		foreach (Control control in controls)
		{
			control.FocusMode = Control.FocusModeEnum.All;
		}
	}
}
