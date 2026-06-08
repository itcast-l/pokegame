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
		_mainMenuMusicTrackOption = dialog.MainMenuMusicTrackOption;
		_controlsButton = dialog.ControlsButton;
		SuppressDefaultPressedSfx(dialog.DoneButton);
		SuppressDefaultPressedSfx(_controlsButton);

		dialog.CloseRequested += HideSettingsDialog;
		dialog.DoneButton.Pressed += HideSettingsDialog;
		dialog.ResetDefaultsButton.Pressed += ResetSettingsToDefaults;
		_controlsButton.Pressed += ShowControlsDialog;
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
		_mainMenuMusicTrackOption.ItemSelected += index =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			if (index < 0 || index >= _mainMenuMusicOptions.Count)
			{
				return;
			}

			_selectedMainMenuMusicTrack = _mainMenuMusicOptions[(int)index].Id;
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

	private void BuildControlsDialog()
	{
		PackedScene? dialogScene = GD.Load<PackedScene>(ControlsDialogScenePath);
		if (dialogScene?.Instantiate() is not MainMenuControlsDialog dialog)
		{
			GD.PushError($"Missing controls dialog scene: {ControlsDialogScenePath}");
			return;
		}

		_controlsDialog = dialog;
		_controlsDialogRoot = dialog;
		AddChild(dialog);
		_controlsDialogPanel = dialog.DialogPanel;
		SuppressDefaultPressedSfx(dialog.DoneButton);
		dialog.CloseRequested += HideControlsDialog;
		dialog.ResetDefaultsRequested += ResetControlsToDefaults;
		dialog.BindingEditRequested += OnBindingEditRequested;
	}
}
