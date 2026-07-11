using Godot;

public partial class MainMenuScene
{
	private void LoadSettings()
	{
		GameSaveStore.EnsureInitialized();
		GameInputActions.EnsureRegistered();
		GameInputActions.LoadBindings(GameSaveStore.GetDictionary(GameSaveStore.InputBindingsKey));
		_masterVolume = Mathf.Clamp(GameSaveStore.GetFloat(GameSaveStore.MasterVolumeKey, DefaultMasterVolume), 0.0f, 1.0f);
		_musicVolume = Mathf.Clamp(GameSaveStore.GetFloat(GameSaveStore.MusicVolumeKey, DefaultMusicVolume), 0.0f, 1.0f);
		bool legacyMuteAudio = GameSaveStore.GetBool(GameSaveStore.MuteAudioKey, false);
		_muteBgm = GameSaveStore.HasValue(GameSaveStore.MuteBgmKey)
			? GameSaveStore.GetBool(GameSaveStore.MuteBgmKey, DefaultMuteBgm)
			: legacyMuteAudio;
		_muteSfx = GameSaveStore.HasValue(GameSaveStore.MuteSfxKey)
			? GameSaveStore.GetBool(GameSaveStore.MuteSfxKey, DefaultMuteSfx)
			: legacyMuteAudio;
		_fullscreen = GameSaveStore.GetBool(GameSaveStore.FullscreenKey, DefaultFullscreen);
		_selectedResolution = ParseResolutionSetting(GameSaveStore.GetString(GameSaveStore.ResolutionKey, ResolutionToString(DefaultResolution)));
		_languagePreference = Localization.NormalizePreference(GameSaveStore.GetString(GameSaveStore.LanguageKey, Localization.DefaultLanguagePreference));
		_selectedMainMenuMusicTrack = GameSaveStore.GetString(GameSaveStore.MainMenuMusicTrackKey, string.Empty).Trim();
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		_selectedAvatarId = CharacterAvatarCatalog.NormalizeUnlockedAvatarId(profile.SelectedAvatarId);
		_playerName = PlayerNameRules.Normalize(profile.PlayerName);
		RefreshMainMenuMusicOptions();
	}

	private void SaveSettings()
	{
		Error saveResult = GameSaveStore.SetValues(
			new GameSaveStore.SaveValue(GameSaveStore.MasterVolumeKey, _masterVolume),
			new GameSaveStore.SaveValue(GameSaveStore.MusicVolumeKey, _musicVolume),
			new GameSaveStore.SaveValue(GameSaveStore.MuteBgmKey, _muteBgm),
			new GameSaveStore.SaveValue(GameSaveStore.MuteSfxKey, _muteSfx),
			new GameSaveStore.SaveValue(GameSaveStore.FullscreenKey, _fullscreen),
			new GameSaveStore.SaveValue(GameSaveStore.ResolutionKey, ResolutionToString(_selectedResolution)),
			new GameSaveStore.SaveValue(GameSaveStore.LanguageKey, _languagePreference),
			new GameSaveStore.SaveValue(GameSaveStore.MainMenuMusicTrackKey, _selectedMainMenuMusicTrack),
			new GameSaveStore.SaveValue(GameSaveStore.InputBindingsKey, GameInputActions.SerializeBindings()));
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to save settings: {saveResult}");
		}
	}

	private void SaveInputBindings()
	{
		Error saveResult = GameSaveStore.SetValue(GameSaveStore.InputBindingsKey, GameInputActions.SerializeBindings());
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to save input bindings: {saveResult}");
		}
	}

	private void SavePlayerProfile()
	{
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		Error saveResult = GameSaveStore.SaveActiveProfile(profile with
		{
			SelectedAvatarId = _selectedAvatarId,
			PlayerName = _playerName,
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to save player profile: {saveResult}");
		}
	}

	private void ApplySettings()
	{
		_masterVolume = Mathf.Clamp(_masterVolume, 0.0f, 1.0f);
		_musicVolume = Mathf.Clamp(_musicVolume, 0.0f, 1.0f);
		_languagePreference = Localization.NormalizePreference(_languagePreference);
		TranslationServer.SetLocale(Localization.ResolveLocale(this, _languagePreference));

		AudioServer.SetBusVolumeDb(0, LinearVolumeToDb(_masterVolume));
		ApplyBusMute(MusicBusName, _muteBgm);
		ApplyBusMute(SoundBusName, _muteSfx);
		ApplyMusicVolume();
		ApplyMainMenuMusicSelection();

		ApplyWindowSettings();

		if (_backgroundAnimator != null)
		{
			_backgroundAnimator.SetMotionEnabled(true);
		}

		if (_oceanDynamicsLayer != null)
		{
			_oceanDynamicsLayer.Visible = true;
			_oceanDynamicsLayer.SetProcess(true);
		}

		if (_underwaterAmbientLayer != null)
		{
			_underwaterAmbientLayer.Visible = true;
			_underwaterAmbientLayer.SetProcess(true);
		}

		if (_waterCausticOverlay != null)
		{
			_waterCausticOverlay.Visible = true;
		}

		UpdateSettingsLabels();
	}

	private static float LinearVolumeToDb(float value)
	{
		return value <= 0.001f ? -80.0f : Mathf.LinearToDb(value);
	}

	private static void ApplyBusMute(string busName, bool muted)
	{
		int busIndex = AudioServer.GetBusIndex(busName);
		if (busIndex >= 0)
		{
			AudioServer.SetBusMute(busIndex, muted);
		}
	}

	private void ApplyMusicVolume()
	{
		float volumeDb = _muteBgm
			? LinearVolumeToDb(0.0f)
			: LinearVolumeToDb(_musicVolume);
		GameAudio?.SetMusicVolumeDb(volumeDb);
	}

	private void SyncSettingsControls()
	{
		_isSyncingSettingsControls = true;
		_masterVolumeSlider.Value = Mathf.RoundToInt(_masterVolume * 100.0f);
		_musicVolumeSlider.Value = Mathf.RoundToInt(_musicVolume * 100.0f);
		_muteBgmToggle.ButtonPressed = _muteBgm;
		_muteSfxToggle.ButtonPressed = _muteSfx;
		_windowModeOption.Select(_fullscreen ? 1 : 0);
		RefreshResolutionOptions();
		_resolutionOption.Select(GetResolutionOptionIndex(_selectedResolution));
		_languageOption.Select(Localization.GetPreferenceIndex(_languagePreference));
		RefreshMainMenuMusicTrackOptions();
		_isSyncingSettingsControls = false;
		UpdateSettingsLabels();
	}

	private void RefreshMainMenuMusicTrackOptions()
	{
		if (_mainMenuMusicTrackOption == null)
		{
			return;
		}

		_mainMenuMusicTrackOption.Clear();
		for (int index = 0; index < _mainMenuMusicOptions.Count; index++)
		{
			MainMenuMusicOption option = _mainMenuMusicOptions[index];
			_mainMenuMusicTrackOption.AddItem(option.DisplayName, index);
		}

		int selectedIndex = GetMainMenuMusicOptionIndex(_selectedMainMenuMusicTrack);
		if (selectedIndex < 0 && _mainMenuMusicOptions.Count > 0)
		{
			selectedIndex = 0;
		}

		if (selectedIndex >= 0 && selectedIndex < _mainMenuMusicTrackOption.ItemCount)
		{
			_mainMenuMusicTrackOption.Select(selectedIndex);
		}
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

		for (int presetIndex = 0; presetIndex < ResolutionPresets.Length; presetIndex++)
		{
			if (presetIndex >= option.ItemCount)
			{
				GD.PushWarning("Settings resolution option scene is missing preset rows.");
				return;
			}

			ResolutionPreset preset = ResolutionPresets[presetIndex];
			option.SetItemText(presetIndex, preset.Label);
			option.SetItemId(presetIndex, presetIndex);
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
			if (index >= _languageOption.ItemCount)
			{
				GD.PushWarning("Settings language option scene is missing language rows.");
				return;
			}

			_languageOption.SetItemText(index, Localize(Localization.SupportedLanguages[index].LabelKey));
			_languageOption.SetItemId(index, index);
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
		_muteBgm = DefaultMuteBgm;
		_muteSfx = DefaultMuteSfx;
		_fullscreen = DefaultFullscreen;
		_selectedResolution = DefaultResolution;
		_languagePreference = Localization.DefaultLanguagePreference;
		_selectedMainMenuMusicTrack = string.Empty;
		RefreshMainMenuMusicOptions();
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

		uint applyToken = ++_windowResolutionApplyToken;

		bool nativeWindowResizeAllowed = IsNativeWindowResizeAllowed(window);
		if (nativeWindowResizeAllowed)
		{
			DisplayServer.WindowSetMode(_fullscreen
				? DisplayServer.WindowMode.Fullscreen
				: DisplayServer.WindowMode.Windowed);
		}

		if (!_fullscreen)
		{
			ApplyWindowedResolutionDeferred(_selectedResolution, applyToken);
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

		if (window.ContentScaleAspect != Window.ContentScaleAspectEnum.Expand)
		{
			window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
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
