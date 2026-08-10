using System;
using Godot;

public partial class MainMenuSettingsDialog : Control
{
	private enum SettingsCategory
	{
		Audio,
		Display,
		Language,
	}

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
	private Label _audioVolumeSectionLabel = default!;
	private Label _audioMuteSectionLabel = default!;
	private Button _audioCategoryButton = default!;
	private Button _displayCategoryButton = default!;
	private Button _languageCategoryButton = default!;
	private Button _controlsCategoryButton = default!;
	private Button _resetButton = default!;
	private Button _doneButton = default!;
	private ColorRect _scrim = default!;
	private ScrollContainer _settingsScroll = default!;
	private PanelContainer _categoryPanel = default!;
	private PanelContainer _settingsContentPanel = default!;
	private Control _masterVolumeRow = default!;
	private Control _musicVolumeRow = default!;
	private Control _muteBgmRow = default!;
	private Control _muteSfxRow = default!;
	private Control _windowModeRow = default!;
	private Control _resolutionRow = default!;
	private Control _languageRow = default!;
	private Func<string, string>? _localize;
	private SettingsCategory _activeCategory = SettingsCategory.Audio;

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
	public Button ControlsButton => _controlsCategoryButton;
	public Button ResetDefaultsButton => _resetButton;
	public Button DoneButton => _doneButton;

	public void FocusDefaultControl()
	{
		ShowCategory(SettingsCategory.Audio, focusContent: false);
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
		_audioVolumeSectionLabel = GetNode<Label>("%AudioVolumeSectionLabel");
		_audioMuteSectionLabel = GetNode<Label>("%AudioMuteSectionLabel");
		_audioCategoryButton = GetNode<Button>("%AudioCategoryButton");
		_displayCategoryButton = GetNode<Button>("%DisplayCategoryButton");
		_languageCategoryButton = GetNode<Button>("%LanguageCategoryButton");
		_controlsCategoryButton = GetNode<Button>("%ControlsCategoryButton");
		_settingsScroll = GetNode<ScrollContainer>("%SettingsScroll");
		_categoryPanel = GetNode<PanelContainer>("%CategoryPanel");
		_settingsContentPanel = GetNode<PanelContainer>("%SettingsContentPanel");
		_resetButton = GetNode<Button>("%ResetDefaultsButton");
		_doneButton = GetNode<Button>("%DoneButton");
		_scrim = GetNode<ColorRect>("%Scrim");
		_masterVolumeRow = FindRequiredControl("MasterVolumeRowPanel");
		_musicVolumeRow = FindRequiredControl("MusicVolumeRowPanel");
		_muteBgmRow = FindRequiredControl("MuteBgmRowPanel");
		_muteSfxRow = FindRequiredControl("MuteSfxRowPanel");
		_windowModeRow = FindRequiredControl("WindowModeRowPanel");
		_resolutionRow = FindRequiredControl("ResolutionRowPanel");
		_languageRow = FindRequiredControl("LanguageRowPanel");

		_scrim.GuiInput += OnScrimGuiInput;
		MainMenuInteractiveStyle.ApplyThemedSlider(MasterVolumeSlider, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedSlider(MusicVolumeSlider, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MasterVolumeSlider.CustomMinimumSize = new Vector2(MasterVolumeSlider.CustomMinimumSize.X, 18.0f);
		MusicVolumeSlider.CustomMinimumSize = new Vector2(MusicVolumeSlider.CustomMinimumSize.X, 18.0f);
		MainMenuInteractiveStyle.ApplyThemedToggle(MuteBgmToggle, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedToggle(MuteSfxToggle, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedControl(WindowModeOption, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedControl(ResolutionOption, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyThemedControl(LanguageOption, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyActionButton(_resetButton, 20, MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
		MainMenuInteractiveStyle.ApplyActionButton(_doneButton, 20, MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
		ConfigureUnifiedVisualStyle();
		_audioCategoryButton.Pressed += () => ShowCategory(SettingsCategory.Audio);
		_displayCategoryButton.Pressed += () => ShowCategory(SettingsCategory.Display);
		_languageCategoryButton.Pressed += () => ShowCategory(SettingsCategory.Language);
		MuteBgmToggle.Toggled += _ => UpdateToggleText(MuteBgmToggle);
		MuteSfxToggle.Toggled += _ => UpdateToggleText(MuteSfxToggle);
		EnableKeyboardFocus(
			_audioCategoryButton,
			_displayCategoryButton,
			_languageCategoryButton,
			_controlsCategoryButton,
			MasterVolumeSlider,
			MusicVolumeSlider,
			MuteBgmToggle,
			MuteSfxToggle,
			WindowModeOption,
			ResolutionOption,
			LanguageOption,
			_resetButton,
			_doneButton);
		ConfigureSettingsFocusScrolling(
			MasterVolumeSlider,
			MusicVolumeSlider,
			MuteBgmToggle,
			MuteSfxToggle,
			WindowModeOption,
			ResolutionOption,
			LanguageOption);
		ShowCategory(SettingsCategory.Audio, focusContent: false);
		SecondaryMenuGlassStyle.StyleScrollBar(_settingsScroll.GetVScrollBar(), SecondaryMenuGlassStyle.UnifiedVisual);
		SecondaryMenuGlassStyle.AttachUniformlyScaled(
			this,
			DialogPanel,
			_scrim,
			SecondaryMenuGlassStyle.PauseDerivedCoreDesignSize,
			SecondaryMenuGlassStyle.UnifiedVisual,
			_categoryPanel,
			_settingsContentPanel);
	}

	private void ConfigureUnifiedVisualStyle()
	{
		foreach (string frameName in new[]
		{
			"MasterVolumeRowPanel",
			"MusicVolumeRowPanel",
			"MuteBgmRowPanel",
			"MuteSfxRowPanel",
			"WindowModeRowPanel",
			"ResolutionRowPanel",
			"LanguageRowPanel",
		})
		{
			ConfigureUnifiedFrame(
				frameName,
				OceanMenuButtonFrame.ButtonFrameStyle.IndividualToolRow,
				MainMenuInteractiveStyle.SecondaryMenuPalette.ActiveBorder,
				suppressVisuals: true);
		}

		ConfigureUnifiedFrame(
			"ResetDefaultsFrame",
			OceanMenuButtonFrame.ButtonFrameStyle.IndividualAction,
			MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette.ActiveBorder);
		ConfigureUnifiedFrame(
			"DoneFrame",
			OceanMenuButtonFrame.ButtonFrameStyle.IndividualAction,
			MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette.ActiveBorder);

		_titleLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.SecondaryMenuPalette.FocusText);
		_messageLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette.ActiveBorder);
		Color sectionAccent = new(0.72f, 0.98f, 0.93f, 0.92f);
		_audioVolumeSectionLabel.AddThemeColorOverride("font_color", sectionAccent);
		_audioMuteSectionLabel.AddThemeColorOverride("font_color", sectionAccent);
		foreach (Label label in new[]
		{
			_masterVolumeLabel,
			_musicVolumeLabel,
			_muteBgmLabel,
			_muteSfxLabel,
			_windowModeLabel,
			_resolutionLabel,
			_languageLabel,
		})
		{
			label.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		}
		MasterVolumeValue.AddThemeColorOverride("font_color", sectionAccent);
		MusicVolumeValue.AddThemeColorOverride("font_color", sectionAccent);
		if (FindChild("TitleUnderline", true, false) is ColorRect underline)
		{
			underline.Color = new Color(0.62f, 1.0f, 0.94f, 0.58f);
		}
	}

	private void ConfigureUnifiedFrame(
		string frameName,
		OceanMenuButtonFrame.ButtonFrameStyle frameStyle,
		Color accent,
		bool suppressVisuals = false)
	{
		if (FindChild(frameName, true, false) is not OceanMenuButtonFrame frame)
		{
			return;
		}

		frame.FrameStyle = frameStyle;
		frame.AccentColor = accent;
		frame.Selected = false;
		frame.HighlightFocus = true;
		frame.UseToolRowIdleBackdrop = false;
		frame.SuppressVisuals = suppressVisuals;
		frame.CustomMinimumSize = new Vector2(
			frame.CustomMinimumSize.X,
			Mathf.Max(54.0f, frame.CustomMinimumSize.Y));
		frame.QueueRedraw();
	}

	public void RefreshLocalizedText(Func<string, string> localize)
	{
		_localize = localize;
		_titleLabel.Text = localize("SETTINGS_TITLE");
		_audioCategoryButton.Text = localize("SETTINGS_AUDIO");
		_displayCategoryButton.Text = localize("SETTINGS_DISPLAY");
		_languageCategoryButton.Text = localize("SETTINGS_LANGUAGE");
		_controlsCategoryButton.Text = localize("SETTINGS_CONTROLS");
		_audioVolumeSectionLabel.Text = localize("SETTINGS_VOLUME");
		_audioMuteSectionLabel.Text = localize("SETTINGS_MUTE");
		_masterVolumeLabel.Text = localize("SETTINGS_MASTER_VOLUME");
		_musicVolumeLabel.Text = localize("SETTINGS_MUSIC_VOLUME");
		_muteBgmLabel.Text = localize("SETTINGS_MUTE_BGM");
		_muteSfxLabel.Text = localize("SETTINGS_MUTE_SFX");
		_windowModeLabel.Text = localize("SETTINGS_WINDOW_MODE");
		_resolutionLabel.Text = localize("SETTINGS_RESOLUTION");
		_languageLabel.Text = localize("SETTINGS_LANGUAGE");
		_resetButton.Text = localize("SETTINGS_RESET_DEFAULTS");
		_doneButton.Text = localize("SETTINGS_DONE");
		UpdateToggleText(MuteBgmToggle);
		UpdateToggleText(MuteSfxToggle);
		UpdateCategoryVisualState();
	}

	private Control FindRequiredControl(string name)
	{
		return FindChild(name, recursive: true, owned: false) as Control
			?? throw new InvalidOperationException($"Settings dialog is missing {name}.");
	}

	private void ShowCategory(SettingsCategory category, bool focusContent = true)
	{
		_activeCategory = category;
		bool showAudio = category == SettingsCategory.Audio;
		_audioVolumeSectionLabel.Visible = showAudio;
		_audioMuteSectionLabel.Visible = showAudio;
		_masterVolumeRow.Visible = showAudio;
		_musicVolumeRow.Visible = showAudio;
		_muteBgmRow.Visible = showAudio;
		_muteSfxRow.Visible = showAudio;
		_windowModeRow.Visible = category == SettingsCategory.Display;
		_resolutionRow.Visible = category == SettingsCategory.Display;
		_languageRow.Visible = category == SettingsCategory.Language;
		_settingsScroll.ScrollVertical = 0;
		UpdateCategoryVisualState();
		if (focusContent)
		{
			CallDeferred(nameof(FocusActiveCategoryContent));
		}
	}

	private void FocusActiveCategoryContent()
	{
		Control target = _activeCategory switch
		{
			SettingsCategory.Display => WindowModeOption,
			SettingsCategory.Language => LanguageOption,
			_ => MasterVolumeSlider,
		};
		target.GrabFocus();
	}

	private void UpdateCategoryVisualState()
	{
		_messageLabel.Text = _localize?.Invoke(GetCategoryLabelKey(_activeCategory))
			?? GetCategoryLabelKey(_activeCategory);
		StyleCategoryButton(_audioCategoryButton, _activeCategory == SettingsCategory.Audio);
		StyleCategoryButton(_displayCategoryButton, _activeCategory == SettingsCategory.Display);
		StyleCategoryButton(_languageCategoryButton, _activeCategory == SettingsCategory.Language);
		StyleCategoryButton(_controlsCategoryButton, selected: false);
	}

	private static string GetCategoryLabelKey(SettingsCategory category)
	{
		return category switch
		{
			SettingsCategory.Display => "SETTINGS_DISPLAY",
			SettingsCategory.Language => "SETTINGS_LANGUAGE",
			_ => "SETTINGS_AUDIO",
		};
	}

	private static void StyleCategoryButton(Button button, bool selected)
	{
		button.SetPressedNoSignal(selected);
		button.Alignment = HorizontalAlignment.Left;
		button.AddThemeFontSizeOverride("font_size", 19);
		MainMenuInteractiveStyle.ApplyUnifiedButtonText(button);
		button.AddThemeStyleboxOverride("normal", CreateCategoryButtonStyle(selected, highlighted: false));
		button.AddThemeStyleboxOverride("hover", CreateCategoryButtonStyle(selected, highlighted: true));
		button.AddThemeStyleboxOverride("focus", CreateCategoryButtonStyle(selected, highlighted: true));
		button.AddThemeStyleboxOverride("pressed", CreateCategoryButtonStyle(true, highlighted: true));
	}

	private static StyleBoxFlat CreateCategoryButtonStyle(bool selected, bool highlighted)
	{
		Color cyan = MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette.ActiveBorder;
		return new StyleBoxFlat
		{
			BgColor = selected
				? new Color(cyan.R * 0.22f, cyan.G * 0.34f, cyan.B * 0.38f, highlighted ? 0.62f : 0.48f)
				: highlighted
					? new Color(0.03f, 0.22f, 0.27f, 0.42f)
					: Colors.Transparent,
			BorderColor = selected ? cyan : new Color(cyan.R, cyan.G, cyan.B, 0.52f),
			BorderWidthLeft = highlighted && !selected ? 1 : 0,
			BorderWidthTop = highlighted && !selected ? 1 : 0,
			BorderWidthRight = selected ? 3 : highlighted ? 1 : 0,
			BorderWidthBottom = highlighted && !selected ? 1 : 0,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginLeft = 18.0f,
			ContentMarginTop = 8.0f,
			ContentMarginRight = 18.0f,
			ContentMarginBottom = 8.0f,
		};
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
