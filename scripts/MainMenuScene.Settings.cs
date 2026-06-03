using Godot;

public partial class MainMenuScene : Control
{
	private void BuildSettingsDialog()
	{
		PackedScene? dialogScene = GD.Load<PackedScene>(SettingsDialogScenePath);
		if (dialogScene?.Instantiate() is not MainMenuSettingsDialog dialog)
		{
			GD.PushError($"Missing settings dialog scene: {SettingsDialogScenePath}");
			return;
		}

		_settingsDialog = dialog;
		_settingsDialogRoot = dialog;
		AddChild(dialog);
		_settingsDialogPanel = dialog.DialogPanel;
		_masterVolumeSlider = dialog.MasterVolumeSlider;
		_musicVolumeSlider = dialog.MusicVolumeSlider;
		_motionScaleSlider = dialog.MotionScaleSlider;
		_masterVolumeValue = dialog.MasterVolumeValue;
		_musicVolumeValue = dialog.MusicVolumeValue;
		_motionScaleValue = dialog.MotionScaleValue;
		_muteBgmToggle = dialog.MuteBgmToggle;
		_muteSfxToggle = dialog.MuteSfxToggle;
		_oceanMotionToggle = dialog.OceanMotionToggle;
		_windowModeOption = dialog.WindowModeOption;
		_resolutionOption = dialog.ResolutionOption;
		_languageOption = dialog.LanguageOption;

		dialog.CloseRequested += HideSettingsDialog;
		dialog.DoneButton.Pressed += HideSettingsDialog;
		dialog.ResetDefaultsButton.Pressed += ResetSettingsToDefaults;
		_masterVolumeSlider.ValueChanged += value =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_masterVolume = (float)value / 100.0f;
			OnSettingsChanged();
		};
		_musicVolumeSlider.ValueChanged += value =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_musicVolume = (float)value / 100.0f;
			OnSettingsChanged();
		};
		_muteBgmToggle.Toggled += pressed =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_muteBgm = pressed;
			OnSettingsChanged();
		};
		_muteSfxToggle.Toggled += pressed =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_muteSfx = pressed;
			OnSettingsChanged();
		};
		_windowModeOption.ItemSelected += index =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_fullscreen = index == 1;
			OnSettingsChanged();
		};
		_resolutionOption.ItemSelected += index =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			int presetIndex = _resolutionOption.GetItemId((int)index);
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
		_languageOption.ItemSelected += index =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_languagePreference = Localization.SupportedLanguages[(int)index].Preference;
			OnSettingsChanged();
		};
		_oceanMotionToggle.Toggled += pressed =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_oceanMotion = pressed;
			SyncMotionScaleControlState();
			OnSettingsChanged();
		};
		_motionScaleSlider.ValueChanged += value =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_motionScale = (float)value / 100.0f;
			OnSettingsChanged();
		};
	}
}
