using Godot;

public partial class MainMenuScene : Control
{
	private void BuildExitDialog()
	{
		PackedScene? dialogScene = GD.Load<PackedScene>(ExitDialogScenePath);
		if (dialogScene?.Instantiate() is not MainMenuExitDialog dialog)
		{
			GD.PushError($"Missing exit dialog scene: {ExitDialogScenePath}");
			return;
		}

		_exitDialog = dialog;
		_exitDialogRoot = dialog;
		_exitDialogLayer = new CanvasLayer
		{
			Name = "ExitDialogLayer",
			Layer = 10,
		};
		AddChild(_exitDialogLayer);
		_exitDialogLayer.AddChild(dialog);
		_exitDialogPanel = dialog.DialogPanel;
		SuppressDefaultPressedSfx(dialog.CancelButton);
		dialog.CloseRequested += HideExitDialog;
		dialog.ConfirmRequested += QuitGame;
	}

	private async void ShowExitDialog()
	{
		if (!BeginMenuCameraPushIn(true))
		{
			return;
		}

		_exitDialogTween?.Kill();
		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);
		_avatarEntry.Visible = false;
		_exitDialogRoot.Visible = true;
		SyncPrimaryMenuFocusState();
		_exitDialog.ResetActionFocusHighlight();
		_exitDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_exitDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		UpdateExitDialogPosition();
		_exitDialogPanel.Scale = new Vector2(0.94f, 0.94f);
		_exitDialogPanel.PivotOffset = _exitDialogPanel.Size * 0.5f;

		_exitDialogTween = CreateTween();
		_exitDialogTween.SetParallel(true);
		_exitDialogTween.TweenProperty(_exitDialogRoot, "modulate:a", 1.0f, DialogFadeSeconds);
		_exitDialogTween.TweenProperty(_exitDialogPanel, "scale", Vector2.One, DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		CallDeferred(nameof(FocusExitDialogDefault));
		await CompleteMenuCameraPushIn();
	}

	private void HideExitDialog()
	{
		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_exitDialogTween?.Kill();
		_avatarEntry.Visible = true;
		_exitDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
		ResetMenuCamera();

		_exitDialogTween = CreateTween();
		_exitDialogTween.SetParallel(true);
		_exitDialogTween.TweenProperty(_exitDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_exitDialogTween.TweenProperty(_exitDialogPanel, "scale", new Vector2(0.96f, 0.96f), DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_exitDialogTween.Finished += () =>
		{
			_exitDialogRoot.Visible = false;
			SyncPrimaryMenuFocusState();
			FocusExitButton();
		};
	}

	private void UpdateExitDialogPosition()
	{
		if (_exitDialogRoot != null && _exitDialogRoot.Visible)
		{
			_exitDialogRoot.Position = GetSecondaryMenuDepthPosition();
			_exitDialogRoot.Scale = GetSecondaryMenuWorldScale();
		}
	}

	private void QuitGame()
	{
		GetTree().Quit();
	}
}
