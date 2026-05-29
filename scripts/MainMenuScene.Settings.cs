using System;
using Godot;

public partial class MainMenuScene : Control
{
	private void BuildSettingsDialog()
	{
		_settingsDialogRoot = new Control
		{
			Name = "SettingsDialogRoot",
			Visible = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_settingsDialogRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_settingsDialogRoot);

		var scrim = new ColorRect
		{
			Name = "Scrim",
			Color = new Color(0.0f, 0.035f, 0.07f, 0.58f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		scrim.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				HideSettingsDialog();
				GetViewport().SetInputAsHandled();
			}
		};
		_settingsDialogRoot.AddChild(scrim);

		var center = new CenterContainer
		{
			Name = "DialogCenter",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		_settingsDialogRoot.AddChild(center);

		_settingsDialogPanel = new Control
		{
			Name = "SettingsDialogPanel",
			CustomMinimumSize = new Vector2(680.0f, 640.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		center.AddChild(_settingsDialogPanel);

		Texture2D? frameTexture = LoadTextureWithImageFallback(SettingsPanelTexturePath);
		if (frameTexture != null)
		{
			var frameArt = new TextureRect
			{
				Name = "GeneratedSettingsFrame",
				Texture = frameTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			frameArt.SetAnchorsPreset(LayoutPreset.FullRect);
			frameArt.OffsetLeft = -8.0f;
			frameArt.OffsetTop = -14.0f;
			frameArt.OffsetRight = 8.0f;
			frameArt.OffsetBottom = 14.0f;
			_settingsDialogPanel.AddChild(frameArt);
		}
		else
		{
			var fallbackPanel = new Panel
			{
				Name = "FallbackPanel",
				MouseFilter = MouseFilterEnum.Ignore,
			};
			fallbackPanel.SetAnchorsPreset(LayoutPreset.FullRect);
			fallbackPanel.AddThemeStyleboxOverride("panel", CreateDialogPanelStyle());
			_settingsDialogPanel.AddChild(fallbackPanel);
			GD.PushWarning($"Missing settings menu frame texture: {SettingsPanelTexturePath}");
		}

		var margin = new MarginContainer
		{
			Name = "SettingsDialogMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 84);
		margin.AddThemeConstantOverride("margin_top", 72);
		margin.AddThemeConstantOverride("margin_right", 84);
		margin.AddThemeConstantOverride("margin_bottom", 64);
		_settingsDialogPanel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "SettingsDialogLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 12);
		margin.AddChild(layout);

		var title = CreateLocalizedDialogLabel("SETTINGS_TITLE", 34, new Color(0.9f, 1.0f, 0.96f), HorizontalAlignment.Center);
		layout.AddChild(title);

		var message = CreateLocalizedDialogLabel("SETTINGS_MESSAGE", 17, new Color(0.66f, 0.96f, 0.92f, 0.86f), HorizontalAlignment.Center);
		layout.AddChild(message);

		var settingsList = new VBoxContainer
		{
			Name = "SettingsList",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		settingsList.AddThemeConstantOverride("separation", 10);
		layout.AddChild(settingsList);

		(_masterVolumeSlider, _masterVolumeValue) = CreateSettingsSlider(
			settingsList,
			"SETTINGS_MASTER_VOLUME",
			value =>
			{
				_masterVolume = (float)value / 100.0f;
				OnSettingsChanged();
			},
			hidden: true);
		(_musicVolumeSlider, _musicVolumeValue) = CreateSettingsSlider(
			settingsList,
			"SETTINGS_MUSIC_VOLUME",
			value =>
			{
				_musicVolume = (float)value / 100.0f;
				OnSettingsChanged();
			});

		_muteAudioToggle = CreateSettingsToggle(settingsList, "SETTINGS_MUTE", pressed =>
		{
			_muteAudio = pressed;
			OnSettingsChanged();
		});

		_windowModeOption = CreateWindowModeOption(settingsList);
		_resolutionOption = CreateResolutionOption(settingsList);
		_languageOption = CreateLanguageOption(settingsList);

		_oceanMotionToggle = CreateSettingsToggle(settingsList, "SETTINGS_OCEAN_MOTION", pressed =>
		{
			_oceanMotion = pressed;
			_motionScaleSlider.Editable = pressed;
			OnSettingsChanged();
		}, hidden: true);

		(_motionScaleSlider, _motionScaleValue) = CreateSettingsSlider(
			settingsList,
			"SETTINGS_MOTION_SCALE",
			value =>
			{
				_motionScale = (float)value / 100.0f;
				OnSettingsChanged();
			},
			50.0,
			150.0,
			hidden: true);

		var actions = new HBoxContainer
		{
			Name = "SettingsActions",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		actions.AddThemeConstantOverride("separation", 18);
		layout.AddChild(actions);

		actions.AddChild(CreateLocalizedDialogButton("SETTINGS_RESET_DEFAULTS", new Color(0.78f, 0.9f, 1.0f), ResetSettingsToDefaults, false, 180.0f));
		actions.AddChild(CreateLocalizedDialogButton("SETTINGS_DONE", new Color(0.58f, 1.0f, 0.92f), HideSettingsDialog, true, 160.0f));
	}

	private (HSlider Slider, Label ValueLabel) CreateSettingsSlider(
		Container parent,
		string labelText,
		Action<double> valueChanged,
		double minValue = 0.0,
		double maxValue = 100.0,
		bool hidden = false)
	{
		var row = CreateSettingsRow(parent, labelText, hidden);

		var slider = new HSlider
		{
			Name = $"{labelText}Slider",
			MinValue = minValue,
			MaxValue = maxValue,
			Step = 1.0,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
		};
		slider.AddThemeStyleboxOverride("slider", CreateSliderTrackStyle(false));
		slider.AddThemeStyleboxOverride("grabber_area", CreateSliderTrackStyle(true));
		slider.AddThemeStyleboxOverride("grabber_area_highlight", CreateSliderTrackStyle(true));
		slider.AddThemeStyleboxOverride("grabber", CreateSliderGrabberStyle(false));
		slider.AddThemeStyleboxOverride("grabber_highlight", CreateSliderGrabberStyle(true));
		slider.ValueChanged += value =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			valueChanged(value);
		};
		row.AddChild(slider);

		var valueLabel = CreateDialogLabel("100%", 16, new Color(0.88f, 1.0f, 0.96f, 0.92f), HorizontalAlignment.Right);
		valueLabel.CustomMinimumSize = new Vector2(58.0f, 0.0f);
		row.AddChild(valueLabel);

		return (slider, valueLabel);
	}

	private CheckButton CreateSettingsToggle(Container parent, string labelText, Action<bool> toggled, bool hidden = false)
	{
		var row = CreateSettingsRow(parent, labelText, hidden);

		var spacer = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(spacer);

		var toggle = new CheckButton
		{
			Name = $"{labelText}Toggle",
			Text = "",
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		toggle.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		toggle.Toggled += pressed =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			toggled(pressed);
		};
		row.AddChild(toggle);
		return toggle;
	}

	private OptionButton CreateWindowModeOption(Container parent)
	{
		var row = CreateSettingsRow(parent, "SETTINGS_WINDOW_MODE");
		var spacer = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(spacer);

		var option = new OptionButton
		{
			Name = "WindowModeOption",
			CustomMinimumSize = new Vector2(220.0f, 40.0f),
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		option.AddItem(Localize("SETTINGS_WINDOWED"), 0);
		option.AddItem(Localize("SETTINGS_FULLSCREEN"), 1);
		StyleDropdownOptionButton(option);
		option.ItemSelected += index =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_fullscreen = index == 1;
			OnSettingsChanged();
		};
		row.AddChild(option);
		return option;
	}

	private OptionButton CreateResolutionOption(Container parent)
	{
		var row = CreateSettingsRow(parent, "SETTINGS_RESOLUTION");
		var spacer = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(spacer);

		var option = new OptionButton
		{
			Name = "ResolutionOption",
			CustomMinimumSize = new Vector2(220.0f, 40.0f),
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		RefreshResolutionOptions(option);
		StyleDropdownOptionButton(option);
		option.ItemSelected += index =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			int presetIndex = option.GetItemId((int)index);
			if (presetIndex < 0 || presetIndex >= ResolutionPresets.Length)
			{
				return;
			}

			_selectedResolution = ResolutionPresets[presetIndex].Size;
			if (_fullscreen)
			{
				_fullscreen = false;
				_windowModeOption.Select(0);
			}
			OnSettingsChanged();
		};
		row.AddChild(option);
		return option;
	}

	private OptionButton CreateLanguageOption(Container parent)
	{
		var row = CreateSettingsRow(parent, "SETTINGS_LANGUAGE");
		var spacer = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(spacer);

		var option = new OptionButton
		{
			Name = "LanguageOption",
			CustomMinimumSize = new Vector2(220.0f, 40.0f),
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		for (int index = 0; index < Localization.SupportedLanguages.Length; index++)
		{
			option.AddItem(Localize(Localization.SupportedLanguages[index].LabelKey), index);
		}

		StyleDropdownOptionButton(option);
		option.ItemSelected += index =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_languagePreference = Localization.SupportedLanguages[(int)index].Preference;
			OnSettingsChanged();
		};
		row.AddChild(option);
		return option;
	}

	private HBoxContainer CreateSettingsRow(Container parent, string labelText, bool hidden = false)
	{
		var panel = new PanelContainer
		{
			Name = $"{labelText}RowPanel",
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = !hidden,
		};
		panel.AddThemeStyleboxOverride("panel", CreateSettingsRowStyle());
		parent.AddChild(panel);

		var margin = new MarginContainer
		{
			Name = "RowMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 7);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 7);
		panel.AddChild(margin);

		var row = new HBoxContainer
		{
			Name = "RowLayout",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 12);
		margin.AddChild(row);

		var label = CreateLocalizedDialogLabel(labelText, 18, new Color(0.88f, 1.0f, 0.96f, 0.96f), HorizontalAlignment.Left);
		label.CustomMinimumSize = new Vector2(132.0f, 38.0f);
		label.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(label);

		return row;
	}

}
