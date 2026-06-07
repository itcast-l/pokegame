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
		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);
		_avatarEntry.Visible = false;
		_settingsDialogRoot.Visible = true;
		_settingsDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_settingsDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		UpdateSettingsDialogPosition();
		_settingsDialogPanel.Scale = Vector2.One;
		_settingsDialogPanel.PivotOffset = Vector2.Zero;
		SyncSettingsControls();

		_settingsDialogTween = CreateTween();
		_settingsDialogTween.TweenProperty(_settingsDialogRoot, "modulate:a", 1.0f, DialogFadeSeconds);
		CallDeferred(nameof(FocusSettingsDialogDefault));
		await CompleteMenuCameraPushIn();
	}

	private void HideSettingsDialog()
	{
		HideControlsDialog();
		_settingsDialogTween?.Kill();
		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_avatarEntry.Visible = true;
		_settingsDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
		ResetMenuCamera();

		_settingsDialogTween = CreateTween();
		_settingsDialogTween.TweenProperty(_settingsDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_settingsDialogTween.Finished += () =>
		{
			_settingsDialogRoot.Visible = false;
			FocusSettingsButton();
		};
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
