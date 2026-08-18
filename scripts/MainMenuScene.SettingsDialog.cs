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
		_settingsDialogRoot.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Inherited;
		SyncPrimaryMenuFocusState();
		_settingsDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_settingsDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
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
		_ = FlushDeferredSettingsAsync("settings close");
		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		HideControlsDialog();
		_settingsDialogTween?.Kill();
		_avatarEntry.Visible = true;
		_settingsDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
		_settingsDialogRoot.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
		ResetMenuCamera();

		_settingsDialogTween = CreateTween();
		_settingsDialogTween.TweenProperty(_settingsDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		Tween closingTween = _settingsDialogTween;
		uint returnToken = BeginPrimaryMenuReturn(
			() =>
			{
				closingTween.Kill();
				_controlsDialogTween?.Kill();
				_controlsDialogTween = null;
				_controlsDialogRoot.Visible = false;
				_controlsDialogRoot.Modulate = Colors.White;
				_controlsDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
				_controlsDialogRoot.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
				_controlsDialogPanel.Scale = Vector2.One;
				_settingsDialogRoot.Visible = false;
				_settingsDialogRoot.Modulate = Colors.White;
				_settingsDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
				if (_settingsDialogTween == closingTween)
				{
					_settingsDialogTween = null;
				}
			},
			FocusSettingsButton);
		closingTween.Finished += () =>
		{
			CompletePrimaryMenuReturn(returnToken);
		};
	}

}
