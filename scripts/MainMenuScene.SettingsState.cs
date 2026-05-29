using Godot;

public partial class MainMenuScene
{
	private void LoadSettings()
	{
		GameSaveStore.EnsureInitialized();
		_masterVolume = Mathf.Clamp(GameSaveStore.GetFloat(GameSaveStore.MasterVolumeKey, DefaultMasterVolume), 0.0f, 1.0f);
		_musicVolume = Mathf.Clamp(GameSaveStore.GetFloat(GameSaveStore.MusicVolumeKey, DefaultMusicVolume), 0.0f, 1.0f);
		_muteAudio = GameSaveStore.GetBool(GameSaveStore.MuteAudioKey, DefaultMuteAudio);
		_fullscreen = GameSaveStore.GetBool(GameSaveStore.FullscreenKey, DefaultFullscreen);
		_selectedResolution = ParseResolutionSetting(GameSaveStore.GetString(GameSaveStore.ResolutionKey, ResolutionToString(DefaultResolution)));
		_oceanMotion = GameSaveStore.GetBool(GameSaveStore.OceanMotionKey, DefaultOceanMotion);
		_motionScale = Mathf.Clamp(GameSaveStore.GetFloat(GameSaveStore.MotionScaleKey, DefaultMotionScale), 0.5f, 1.5f);
		_languagePreference = Localization.NormalizePreference(GameSaveStore.GetString(GameSaveStore.LanguageKey, Localization.DefaultLanguagePreference));
		_selectedAvatarId = CharacterAvatarCatalog.NormalizeUnlockedAvatarId(GameSaveStore.GetString(GameSaveStore.SelectedAvatarKey, CharacterAvatarCatalog.DefaultAvatarId));
	}

	private void SaveSettings()
	{
		Error saveResult = GameSaveStore.SetValues(
			new GameSaveStore.SaveValue(GameSaveStore.MasterVolumeKey, _masterVolume),
			new GameSaveStore.SaveValue(GameSaveStore.MusicVolumeKey, _musicVolume),
			new GameSaveStore.SaveValue(GameSaveStore.MuteAudioKey, _muteAudio),
			new GameSaveStore.SaveValue(GameSaveStore.FullscreenKey, _fullscreen),
			new GameSaveStore.SaveValue(GameSaveStore.ResolutionKey, ResolutionToString(_selectedResolution)),
			new GameSaveStore.SaveValue(GameSaveStore.OceanMotionKey, _oceanMotion),
			new GameSaveStore.SaveValue(GameSaveStore.MotionScaleKey, _motionScale),
			new GameSaveStore.SaveValue(GameSaveStore.LanguageKey, _languagePreference),
			new GameSaveStore.SaveValue(GameSaveStore.SelectedAvatarKey, _selectedAvatarId));
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
		_languagePreference = Localization.NormalizePreference(_languagePreference);
		TranslationServer.SetLocale(Localization.ResolveLocale(this, _languagePreference));

		AudioServer.SetBusVolumeDb(0, LinearVolumeToDb(_masterVolume));
		AudioServer.SetBusMute(0, _muteAudio);
		ApplyMusicVolume();

		ApplyWindowSettings();

		if (_backgroundAnimator != null)
		{
			_backgroundAnimator.motion_scale = _motionScale;
			_backgroundAnimator.SetMotionEnabled(_oceanMotion);
		}

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

	private void ApplyMusicVolume()
	{
		Node? musicManager = GetMusicManager();
		if (musicManager == null)
		{
			return;
		}

		float volumeDb = LinearVolumeToDb(_musicVolume);
		if (!float.IsNaN(_appliedMusicVolumeDb) && Mathf.IsEqualApprox(_appliedMusicVolumeDb, volumeDb))
		{
			return;
		}

		musicManager.Call("set_volume", volumeDb);
		_appliedMusicVolumeDb = volumeDb;
	}

	private void SyncSettingsControls()
	{
		_isSyncingSettingsControls = true;
		_masterVolumeSlider.Value = Mathf.RoundToInt(_masterVolume * 100.0f);
		_musicVolumeSlider.Value = Mathf.RoundToInt(_musicVolume * 100.0f);
		_muteAudioToggle.ButtonPressed = _muteAudio;
		_windowModeOption.Select(_fullscreen ? 1 : 0);
		RefreshResolutionOptions();
		_resolutionOption.Select(GetResolutionOptionIndex(_selectedResolution));
		_languageOption.Select(Localization.GetPreferenceIndex(_languagePreference));
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

	private void RefreshWindowModeOptions()
	{
		if (_windowModeOption == null)
		{
			return;
		}

		_windowModeOption.SetItemText(0, Localize("SETTINGS_WINDOWED"));
		_windowModeOption.SetItemText(1, Localize("SETTINGS_FULLSCREEN"));
	}

	private void RefreshResolutionOptions()
	{
		RefreshResolutionOptions(_resolutionOption);
	}

	private void RefreshResolutionOptions(OptionButton? option)
	{
		if (option == null)
		{
			return;
		}

		option.Clear();
		for (int presetIndex = 0; presetIndex < ResolutionPresets.Length; presetIndex++)
		{
			ResolutionPreset preset = ResolutionPresets[presetIndex];
			option.AddItem(preset.Label, presetIndex);
		}

		option.Select(GetResolutionOptionIndex(option, _selectedResolution));
	}

	private void RefreshLanguageOptions()
	{
		if (_languageOption == null)
		{
			return;
		}

		for (int index = 0; index < Localization.SupportedLanguages.Length; index++)
		{
			_languageOption.SetItemText(index, Localize(Localization.SupportedLanguages[index].LabelKey));
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
		_selectedResolution = DefaultResolution;
		_oceanMotion = DefaultOceanMotion;
		_languagePreference = Localization.DefaultLanguagePreference;
		_selectedAvatarId = CharacterAvatarCatalog.DefaultAvatarId;
		ApplySelectedAvatar();
		SyncSettingsControls();
		ApplySettings();
		SaveSettings();
	}

	private void ApplyWindowSettings()
	{
		_selectedResolution = NormalizeResolution(_selectedResolution);
		Window window = GetWindow();
		ApplyContentResolution(window);

		bool windowModeChanged = !_hasAppliedWindowSettings || _lastAppliedFullscreen != _fullscreen;
		bool resolutionChanged = !_hasAppliedWindowSettings || !AreSameResolution(_lastAppliedResolution, _selectedResolution);
		if (!windowModeChanged && !resolutionChanged)
		{
			return;
		}

		bool nativeWindowResizeAllowed = IsNativeWindowResizeAllowed(window);
		if (nativeWindowResizeAllowed)
		{
			DisplayServer.WindowSetMode(_fullscreen
				? DisplayServer.WindowMode.Fullscreen
				: DisplayServer.WindowMode.Windowed);
		}

		if (!_fullscreen)
		{
			_windowResolutionApplyToken++;
			ApplyWindowedResolutionDeferred(_selectedResolution, _windowResolutionApplyToken);
		}

		_hasAppliedWindowSettings = true;
		_lastAppliedFullscreen = _fullscreen;
		_lastAppliedResolution = _selectedResolution;
	}

	private async void ApplyWindowedResolutionDeferred(Vector2I resolution, uint applyToken)
	{
		ApplyWindowedResolution(resolution, false);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!GodotObject.IsInstanceValid(this) || applyToken != _windowResolutionApplyToken)
		{
			return;
		}

		ApplyWindowedResolution(resolution, false);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!GodotObject.IsInstanceValid(this) || applyToken != _windowResolutionApplyToken)
		{
			return;
		}

		ApplyWindowedResolution(resolution, true);
	}

	private void ApplyWindowedResolution(Vector2I resolution, bool reportResult)
	{
		Window window = GetWindow();
		ApplyContentResolution(window);
		bool nativeWindowResizeAllowed = IsNativeWindowResizeAllowed(window);
		if (nativeWindowResizeAllowed)
		{
			window.Size = resolution;
			DisplayServer.WindowSetSize(resolution);
			CenterWindow(resolution);
		}

		if (reportResult)
		{
			Vector2I windowSize = window.Size;
			Vector2I contentSize = window.ContentScaleSize;
			string nativeResizeStatus = nativeWindowResizeAllowed ? "native resize attempted" : "native resize skipped for embedded editor window";
			GD.Print($"Resolution applied: requested {resolution.X}x{resolution.Y}, window {windowSize.X}x{windowSize.Y}, content {contentSize.X}x{contentSize.Y}, {nativeResizeStatus}");
		}
	}

	private static void ApplyContentResolution(Window window)
	{
		if (window.ContentScaleMode == Window.ContentScaleModeEnum.Disabled)
		{
			window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
		}

		// Resolution presets resize the output window only; keep the UI design viewport fixed.
		if (window.ContentScaleSize != DesignResolution)
		{
			window.ContentScaleSize = DesignResolution;
		}
	}

	private static bool IsNativeWindowResizeAllowed(Window window)
	{
		return !Engine.IsEmbeddedInEditor() && !window.IsEmbedded();
	}

	private static void CenterWindow(Vector2I resolution)
	{
		Rect2I usableRect = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
		if (usableRect.Size.X <= 0 || usableRect.Size.Y <= 0)
		{
			return;
		}

		Vector2I position = new(
			usableRect.Position.X + Mathf.Max((usableRect.Size.X - resolution.X) / 2, 0),
			usableRect.Position.Y + Mathf.Max((usableRect.Size.Y - resolution.Y) / 2, 0));
		DisplayServer.WindowSetPosition(position);
	}

	private static Vector2I ParseResolutionSetting(string rawValue)
	{
		if (TryParseResolution(rawValue, out Vector2I resolution))
		{
			return NormalizeResolution(resolution);
		}

		return DefaultResolution;
	}

	private static bool TryParseResolution(string rawValue, out Vector2I resolution)
	{
		resolution = default;
		if (string.IsNullOrWhiteSpace(rawValue))
		{
			return false;
		}

		string normalized = rawValue.Trim().ToLowerInvariant().Replace("*", "x");
		string[] parts = normalized.Split('x');
		if (parts.Length != 2)
		{
			return false;
		}

		if (!int.TryParse(parts[0].Trim(), out int width) || !int.TryParse(parts[1].Trim(), out int height))
		{
			return false;
		}

		resolution = new Vector2I(width, height);
		return true;
	}

	private static Vector2I NormalizeResolution(Vector2I resolution)
	{
		for (int index = 0; index < ResolutionPresets.Length; index++)
		{
			if (AreSameResolution(ResolutionPresets[index].Size, resolution))
			{
				return ResolutionPresets[index].Size;
			}
		}

		return DefaultResolution;
	}

	private int GetResolutionOptionIndex(Vector2I resolution)
	{
		return GetResolutionOptionIndex(_resolutionOption, resolution);
	}

	private static int GetResolutionOptionIndex(OptionButton option, Vector2I resolution)
	{
		Vector2I normalizedResolution = NormalizeResolution(resolution);
		for (int itemIndex = 0; itemIndex < option.ItemCount; itemIndex++)
		{
			int presetIndex = option.GetItemId(itemIndex);
			if (presetIndex >= 0
				&& presetIndex < ResolutionPresets.Length
				&& AreSameResolution(ResolutionPresets[presetIndex].Size, normalizedResolution))
			{
				return itemIndex;
			}
		}

		return 0;
	}

	private static string ResolutionToString(Vector2I resolution)
	{
		return $"{resolution.X}x{resolution.Y}";
	}

	private static bool AreSameResolution(Vector2I first, Vector2I second)
	{
		return first.X == second.X && first.Y == second.Y;
	}
}
