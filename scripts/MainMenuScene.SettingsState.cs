using Godot;

public partial class MainMenuScene
{
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
		_languagePreference = Localization.NormalizePreference(config.GetValue("general", "language", Localization.DefaultLanguagePreference).AsString());
		_selectedAvatarId = CharacterAvatarCatalog.NormalizeUnlockedAvatarId(config.GetValue("general", "avatar", CharacterAvatarCatalog.DefaultAvatarId).AsString());
	}

	private void SaveSettings()
	{
		var config = new ConfigFile();
		config.Load(SettingsConfigPath);
		config.SetValue("audio", "master_volume", _masterVolume);
		config.SetValue("audio", "music_volume", _musicVolume);
		config.SetValue("audio", "mute", _muteAudio);
		config.SetValue("video", "fullscreen", _fullscreen);
		config.SetValue("video", "ocean_motion", _oceanMotion);
		config.SetValue("video", "motion_scale", _motionScale);
		config.SetValue("general", "language", _languagePreference);
		config.SetValue("general", "avatar", _selectedAvatarId);

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
		_languagePreference = Localization.NormalizePreference(_languagePreference);
		TranslationServer.SetLocale(Localization.ResolveLocale(this, _languagePreference));

		AudioServer.SetBusVolumeDb(0, LinearVolumeToDb(_masterVolume));
		AudioServer.SetBusMute(0, _muteAudio);
		ApplyMusicVolume();

		DisplayServer.WindowSetMode(_fullscreen
			? DisplayServer.WindowMode.Fullscreen
			: DisplayServer.WindowMode.Windowed);

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
		_oceanMotion = DefaultOceanMotion;
		_languagePreference = Localization.DefaultLanguagePreference;
		_selectedAvatarId = CharacterAvatarCatalog.DefaultAvatarId;
		ApplySelectedAvatar();
		SyncSettingsControls();
		ApplySettings();
		SaveSettings();
	}
}
