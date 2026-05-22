using Godot;

public partial class MainMenuScene
{
	private async void ShowSettingsDialog()
	{
		if (!BeginMenuCameraPushIn(true))
		{
			return;
		}

		_settingsDialogTween?.Kill();
		_avatarEntry.Visible = false;
		_settingsDialogRoot.Visible = true;
		_settingsDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_settingsDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		UpdateSettingsDialogPosition();
		_settingsDialogPanel.Scale = new Vector2(0.94f, 0.94f);
		_settingsDialogPanel.PivotOffset = _settingsDialogPanel.Size * 0.5f;
		SyncSettingsControls();

		_settingsDialogTween = CreateTween();
		_settingsDialogTween.SetParallel(true);
		_settingsDialogTween.TweenProperty(_settingsDialogRoot, "modulate:a", 1.0f, DialogFadeSeconds);
		_settingsDialogTween.TweenProperty(_settingsDialogPanel, "scale", Vector2.One, DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		await CompleteMenuCameraPushIn();
	}

	private void HideSettingsDialog()
	{
		_settingsDialogTween?.Kill();
		_avatarEntry.Visible = true;
		_settingsDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
		ResetMenuCamera();

		_settingsDialogTween = CreateTween();
		_settingsDialogTween.SetParallel(true);
		_settingsDialogTween.TweenProperty(_settingsDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_settingsDialogTween.TweenProperty(_settingsDialogPanel, "scale", new Vector2(0.96f, 0.96f), DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_settingsDialogTween.Finished += () => _settingsDialogRoot.Visible = false;
	}

	private void UpdateSettingsDialogPosition()
	{
		if (_settingsDialogRoot != null && _settingsDialogRoot.Visible)
		{
			_settingsDialogRoot.Position = GetSecondaryMenuDepthPosition();
			_settingsDialogRoot.Scale = GetSecondaryMenuWorldScale();
		}
	}
}
