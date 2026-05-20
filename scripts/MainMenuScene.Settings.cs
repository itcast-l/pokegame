using System;
using Godot;

public partial class MainMenuScene : Control
{
	private void BuildSettingsDialog()
	{
		_settingsDialogLayer = new CanvasLayer
		{
			Name = "SettingsDialogLayer",
			Layer = 38,
		};
		AddChild(_settingsDialogLayer);

		_settingsDialogRoot = new Control
		{
			Name = "SettingsDialogRoot",
			Visible = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_settingsDialogRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		_settingsDialogLayer.AddChild(_settingsDialogRoot);

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
		center.OffsetTop = -28.0f;
		center.OffsetBottom = -28.0f;
		_settingsDialogRoot.AddChild(center);

		_settingsDialogPanel = new Control
		{
			Name = "SettingsDialogPanel",
			CustomMinimumSize = new Vector2(560.0f, 500.0f),
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
		margin.AddThemeConstantOverride("margin_left", 74);
		margin.AddThemeConstantOverride("margin_top", 64);
		margin.AddThemeConstantOverride("margin_right", 74);
		margin.AddThemeConstantOverride("margin_bottom", 56);
		_settingsDialogPanel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "SettingsDialogLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 12);
		margin.AddChild(layout);

		var title = CreateDialogLabel("设置", 34, new Color(0.9f, 1.0f, 0.96f), HorizontalAlignment.Center);
		layout.AddChild(title);

		var message = CreateDialogLabel("基础游戏选项", 17, new Color(0.66f, 0.96f, 0.92f, 0.86f), HorizontalAlignment.Center);
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
			"主音量",
			value =>
			{
				_masterVolume = (float)value / 100.0f;
				OnSettingsChanged();
			},
			hidden: true);
		(_musicVolumeSlider, _musicVolumeValue) = CreateSettingsSlider(
			settingsList,
			"音乐音量",
			value =>
			{
				_musicVolume = (float)value / 100.0f;
				OnSettingsChanged();
			});

		_muteAudioToggle = CreateSettingsToggle(settingsList, "静音", pressed =>
		{
			_muteAudio = pressed;
			OnSettingsChanged();
		});

		_windowModeOption = CreateWindowModeOption(settingsList);

		_oceanMotionToggle = CreateSettingsToggle(settingsList, "海洋动态", pressed =>
		{
			_oceanMotion = pressed;
			_motionScaleSlider.Editable = pressed;
			OnSettingsChanged();
		}, hidden: true);

		(_motionScaleSlider, _motionScaleValue) = CreateSettingsSlider(
			settingsList,
			"动态强度",
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

		actions.AddChild(CreateDialogButton("恢复默认", new Color(0.78f, 0.9f, 1.0f), ResetSettingsToDefaults, false, 180.0f));
		actions.AddChild(CreateDialogButton("完成", new Color(0.58f, 1.0f, 0.92f), HideSettingsDialog, true, 160.0f));
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
		var row = CreateSettingsRow(parent, "窗口模式");
		var spacer = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(spacer);

		var option = new OptionButton
		{
			Name = "WindowModeOption",
			CustomMinimumSize = new Vector2(174.0f, 40.0f),
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		option.AddItem("窗口", 0);
		option.AddItem("全屏", 1);
		option.AddThemeFontSizeOverride("font_size", 17);
		option.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		option.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.78f));
		option.AddThemeColorOverride("font_pressed_color", new Color(0.78f, 1.0f, 0.96f));
		option.AddThemeStyleboxOverride("normal", CreateOptionButtonStyle(false));
		option.AddThemeStyleboxOverride("hover", CreateOptionButtonStyle(true));
		option.AddThemeStyleboxOverride("pressed", CreateOptionButtonStyle(true));
		option.AddThemeStyleboxOverride("focus", CreateOptionButtonStyle(true));
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

		var label = CreateDialogLabel(labelText, 18, new Color(0.88f, 1.0f, 0.96f, 0.96f), HorizontalAlignment.Left);
		label.CustomMinimumSize = new Vector2(104.0f, 38.0f);
		label.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(label);

		return row;
	}

	private void LoadSettings()
	{
		var config = new ConfigFile();
		Error loadResult = config.Load(SettingsConfigPath);
		if (loadResult != Error.Ok)
		{
			return;
		}

		_masterVolume = Mathf.Clamp(config.GetValue("audio", "master_volume", DefaultMasterVolume).AsSingle(), 0.0f, 1.0f);
		_musicVolume = Mathf.Clamp(config.GetValue("audio", "music_volume", DefaultMusicVolume).AsSingle(), 0.0f, 1.0f);
		_muteAudio = config.GetValue("audio", "mute", DefaultMuteAudio).AsBool();
		_fullscreen = config.GetValue("video", "fullscreen", DefaultFullscreen).AsBool();
		_oceanMotion = config.GetValue("video", "ocean_motion", DefaultOceanMotion).AsBool();
		_motionScale = Mathf.Clamp(config.GetValue("video", "motion_scale", DefaultMotionScale).AsSingle(), 0.5f, 1.5f);
	}

	private void SaveSettings()
	{
		var config = new ConfigFile();
		config.SetValue("audio", "master_volume", _masterVolume);
		config.SetValue("audio", "music_volume", _musicVolume);
		config.SetValue("audio", "mute", _muteAudio);
		config.SetValue("video", "fullscreen", _fullscreen);
		config.SetValue("video", "ocean_motion", _oceanMotion);
		config.SetValue("video", "motion_scale", _motionScale);

		Error saveResult = config.Save(SettingsConfigPath);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to save settings: {saveResult}");
		}
	}

	private void ApplySettings()
	{
		_masterVolume = Mathf.Clamp(_masterVolume, 0.0f, 1.0f);
		_musicVolume = Mathf.Clamp(_musicVolume, 0.0f, 1.0f);
		_motionScale = Mathf.Clamp(_motionScale, 0.5f, 1.5f);

		AudioServer.SetBusVolumeDb(0, LinearVolumeToDb(_masterVolume));
		AudioServer.SetBusMute(0, _muteAudio);
		_backgroundMusic.VolumeDb = LinearVolumeToDb(_musicVolume);

		DisplayServer.WindowSetMode(_fullscreen
			? DisplayServer.WindowMode.Fullscreen
			: DisplayServer.WindowMode.Windowed);

		if (_oceanDynamicsLayer != null)
		{
			_oceanDynamicsLayer.motion_scale = _motionScale;
			_oceanDynamicsLayer.Visible = _oceanMotion;
			_oceanDynamicsLayer.SetProcess(_oceanMotion);
		}

		if (_underwaterAmbientLayer != null)
		{
			_underwaterAmbientLayer.motion_scale = _motionScale;
			_underwaterAmbientLayer.Visible = _oceanMotion;
			_underwaterAmbientLayer.SetProcess(_oceanMotion);
		}

		if (_waterCausticOverlay != null)
		{
			_waterCausticOverlay.Visible = _oceanMotion;
		}

		if (_motionScaleSlider != null)
		{
			_motionScaleSlider.Editable = _oceanMotion;
		}

		UpdateSettingsLabels();
	}

	private static float LinearVolumeToDb(float value)
	{
		return value <= 0.001f ? -80.0f : Mathf.LinearToDb(value);
	}

	private void SyncSettingsControls()
	{
		_isSyncingSettingsControls = true;
		_masterVolumeSlider.Value = Mathf.RoundToInt(_masterVolume * 100.0f);
		_musicVolumeSlider.Value = Mathf.RoundToInt(_musicVolume * 100.0f);
		_muteAudioToggle.ButtonPressed = _muteAudio;
		_windowModeOption.Select(_fullscreen ? 1 : 0);
		_oceanMotionToggle.ButtonPressed = _oceanMotion;
		_motionScaleSlider.Value = Mathf.RoundToInt(_motionScale * 100.0f);
		_motionScaleSlider.Editable = _oceanMotion;
		_isSyncingSettingsControls = false;
		UpdateSettingsLabels();
	}

	private void UpdateSettingsLabels()
	{
		if (_masterVolumeValue != null)
		{
			_masterVolumeValue.Text = $"{Mathf.RoundToInt(_masterVolume * 100.0f)}%";
		}

		if (_musicVolumeValue != null)
		{
			_musicVolumeValue.Text = $"{Mathf.RoundToInt(_musicVolume * 100.0f)}%";
		}

		if (_motionScaleValue != null)
		{
			_motionScaleValue.Text = $"{Mathf.RoundToInt(_motionScale * 100.0f)}%";
		}
	}

	private void OnSettingsChanged()
	{
		ApplySettings();
		SaveSettings();
	}

	private void ResetSettingsToDefaults()
	{
		_masterVolume = DefaultMasterVolume;
		_musicVolume = DefaultMusicVolume;
		_motionScale = DefaultMotionScale;
		_muteAudio = DefaultMuteAudio;
		_fullscreen = DefaultFullscreen;
		_oceanMotion = DefaultOceanMotion;
		SyncSettingsControls();
		ApplySettings();
		SaveSettings();
	}

	private void ShowSettingsDialog()
	{
		_settingsDialogTween?.Kill();
		_settingsDialogRoot.Visible = true;
		_settingsDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_settingsDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_settingsDialogPanel.Scale = new Vector2(0.94f, 0.94f);
		_settingsDialogPanel.PivotOffset = _settingsDialogPanel.Size * 0.5f;
		SyncSettingsControls();

		_settingsDialogTween = CreateTween();
		_settingsDialogTween.SetParallel(true);
		_settingsDialogTween.TweenProperty(_settingsDialogRoot, "modulate:a", 1.0f, DialogFadeSeconds);
		_settingsDialogTween.TweenProperty(_settingsDialogPanel, "scale", Vector2.One, DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}

	private void HideSettingsDialog()
	{
		_settingsDialogTween?.Kill();
		_settingsDialogRoot.MouseFilter = MouseFilterEnum.Ignore;

		_settingsDialogTween = CreateTween();
		_settingsDialogTween.SetParallel(true);
		_settingsDialogTween.TweenProperty(_settingsDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_settingsDialogTween.TweenProperty(_settingsDialogPanel, "scale", new Vector2(0.96f, 0.96f), DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_settingsDialogTween.Finished += () => _settingsDialogRoot.Visible = false;
	}
}
