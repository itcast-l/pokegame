using System;
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
		_controlsDialogRoot.MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Inherited;
		_controlsDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_controlsDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_controlsDialogPanel.Scale = new Vector2(0.98f, 0.98f);
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

		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_ = FlushDeferredSettingsAsync("controls close");
		ClearPendingBindingCapture();
		_controlsDialogTween?.Kill();
		_controlsDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
		_controlsDialogRoot.MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Disabled;

		_controlsDialogTween = CreateTween();
		_controlsDialogTween.TweenProperty(_controlsDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_controlsDialogTween.TweenProperty(_controlsDialogPanel, "scale", new Vector2(0.98f, 0.98f), DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		_controlsDialogTween.Finished += () =>
		{
			_controlsDialogRoot.Visible = false;
			SyncPrimaryMenuFocusState();
			_controlsButton?.GrabFocus();
		};
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

		if (GameInputActions.IsCancelPressed(inputEvent))
		{
			ClearPendingBindingCapture();
			RefreshControlsDialogText();
			GetViewport().SetInputAsHandled();
			return true;
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

		_ = RebindAndPersistInputBinding(
			_pendingBindingActionId,
			_pendingBindingSlot.Value,
			inputEvent);
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
		if (!GameInputActions.AreBindingsAtDefaults())
		{
			GameInputActions.ResetAllToDefaults();
			_inputBindingsDirty = true;
			SaveInputBindings();
			_ = FlushDeferredSettingsAsync("controls defaults");
		}
		ClearPendingBindingCapture();
		RefreshControlsDialogText();
	}

	private bool RebindAndPersistInputBinding(
		string actionId,
		GameInputActions.BindingSlot slot,
		InputEvent inputEvent)
	{
		if (GameInputActions.IsCurrentBinding(actionId, slot, inputEvent))
		{
			return false;
		}

		GameInputActions.Rebind(actionId, slot, inputEvent);
		_inputBindingsDirty = true;
		SaveInputBindings();
		return true;
	}

#if DECKLAND_TEST_HOOKS
	public int DebugGetInputBindingsSerializationCount()
	{
		return GameInputActions.DebugGetSerializeBindingsCallCount();
	}

	public bool DebugResetControlsToDefaultsForPersistenceProbe()
	{
		int previousCount = GameInputActions.DebugGetSerializeBindingsCallCount();
		ResetControlsToDefaults();
		return GameInputActions.DebugGetSerializeBindingsCallCount() != previousCount;
	}

	public bool DebugRebindKeyboardForPersistenceProbe(string actionId, long keycode)
	{
		Key key = (Key)keycode;
		return RebindAndPersistInputBinding(
			actionId,
			GameInputActions.BindingSlot.Keyboard,
			new InputEventKey { Keycode = key, PhysicalKeycode = key });
	}
#endif
}
