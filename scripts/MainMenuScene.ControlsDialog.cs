using Godot;

public partial class MainMenuScene : Control
{
	private void ShowControlsDialog()
	{
		if (_controlsDialogRoot == null || !_settingsDialogRoot.Visible)
		{
			return;
		}

		_controlsDialogTween?.Kill();
		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);
		_controlsDialogRoot.Visible = true;
		_controlsDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_controlsDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_controlsDialogPanel.Scale = new Vector2(0.98f, 0.98f);
		UpdateControlsDialogPosition();
		RefreshControlsDialogText();

		_controlsDialogTween = CreateTween();
		_controlsDialogTween.TweenProperty(_controlsDialogRoot, "modulate:a", 1.0f, DialogFadeSeconds);
		_controlsDialogTween.TweenProperty(_controlsDialogPanel, "scale", Vector2.One, DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		CallDeferred(nameof(FocusControlsDialogDefault));
	}

	private void HideControlsDialog()
	{
		if (_controlsDialogRoot == null || !_controlsDialogRoot.Visible)
		{
			return;
		}

		ClearPendingBindingCapture();
		_controlsDialogTween?.Kill();
		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_controlsDialogRoot.MouseFilter = MouseFilterEnum.Ignore;

		_controlsDialogTween = CreateTween();
		_controlsDialogTween.TweenProperty(_controlsDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_controlsDialogTween.TweenProperty(_controlsDialogPanel, "scale", new Vector2(0.98f, 0.98f), DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		_controlsDialogTween.Finished += () =>
		{
			_controlsDialogRoot.Visible = false;
			_controlsButton?.GrabFocus();
		};
	}

	private void UpdateControlsDialogPosition()
	{
		if (_controlsDialogRoot != null && _controlsDialogRoot.Visible)
		{
			_controlsDialogRoot.Position = GetSecondaryMenuDepthPosition();
			_controlsDialogRoot.Scale = GetSecondaryMenuWorldScale();
		}
	}

	private void RefreshControlsDialogText()
	{
		_controlsDialog?.RefreshLocalizedText(Localize);
	}

	private void OnBindingEditRequested(string actionId, long slot)
	{
		_pendingBindingActionId = actionId;
		_pendingBindingSlot = (GameInputActions.BindingSlot)(int)slot;
		_bindingCaptureEarliestFrame = Engine.GetFramesDrawn() + 1;
		RefreshControlsDialogText();
	}

	private bool TryHandleControlsBindingCapture(InputEvent inputEvent)
	{
		if (_controlsDialog == null
			|| _controlsDialog.CaptureActionId == null
			|| _controlsDialog.CaptureSlot == null)
		{
			return false;
		}

		_pendingBindingActionId = _controlsDialog.CaptureActionId;
		_pendingBindingSlot = _controlsDialog.CaptureSlot;
		_bindingCaptureEarliestFrame = _controlsDialog.CaptureEarliestFrame;

		if (Engine.GetFramesDrawn() <= _bindingCaptureEarliestFrame)
		{
			return false;
		}

		if (!GameInputActions.IsBindingCaptureEvent(inputEvent, _pendingBindingSlot.Value))
		{
			return false;
		}

		GameInputActions.Rebind(_pendingBindingActionId, _pendingBindingSlot.Value, inputEvent);
		SaveInputBindings();
		PlayMenuSfx(GameSfx.UiButtonClick, -2.0f, 0.98f, 1.02f, 0.05);
		ClearPendingBindingCapture();
		RefreshControlsDialogText();
		GetViewport().SetInputAsHandled();
		return true;
	}

	private void ClearPendingBindingCapture()
	{
		_pendingBindingActionId = null;
		_pendingBindingSlot = null;
		_bindingCaptureEarliestFrame = 0;
		_controlsDialog?.ClearBindingCapture();
	}

	private void ResetControlsToDefaults()
	{
		GameInputActions.ResetAllToDefaults();
		SaveInputBindings();
		ClearPendingBindingCapture();
		RefreshControlsDialogText();
	}
}
