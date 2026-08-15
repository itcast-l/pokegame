using Godot;

public partial class AdventureAvatarMenuScene : Control
{
	public override void _Input(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton or InputEventMouseMotion)
		{
			SetListSelectionHighlightVisible(false);
			return;
		}

		if (IsKeyboardOrJoypadNavigationInput(inputEvent))
		{
			SetListSelectionHighlightVisible(true);
		}
	}

	public void FocusDefaultControlDeferred()
	{
		CallDeferred(nameof(FocusSelectedStageButton));
	}

	private void ConfigureAdventureMenuFocus()
	{
		EnableKeyboardFocus(_backButton, _startButton);
	}

	private void FocusSelectedStageButton()
	{
		if (_isShowingAvatarUnlockPrompt
			|| _isShowingRunResumeDialog
			|| _isStartingAdventure
			|| _isReturningToMainMenu)
		{
			return;
		}

		if (_stageButtons.TryGetValue(_selectedAvatarId, out Button? button))
		{
			GrabFocusIfAvailable(button);
			if (button.HasFocus())
			{
				return;
			}
		}

		GrabFocusIfAvailable(_startButton);
	}

	private static void EnableKeyboardFocus(params Control[] controls)
	{
		foreach (Control control in controls)
		{
			if (GodotObject.IsInstanceValid(control))
			{
				control.FocusMode = Control.FocusModeEnum.All;
			}
		}
	}

	private static void GrabFocusIfAvailable(Control? control)
	{
		if (control == null
			|| !GodotObject.IsInstanceValid(control)
			|| !control.IsVisibleInTree()
			|| control.FocusMode == Control.FocusModeEnum.None)
		{
			return;
		}

		if (control is BaseButton { Disabled: true })
		{
			return;
		}

		control.GrabFocus();
	}

	private void SetListSelectionHighlightVisible(bool visible)
	{
		if (_listSelectionHighlightVisible == visible)
		{
			return;
		}

		_listSelectionHighlightVisible = visible;
		RefreshSelection();
	}

	private static bool IsKeyboardOrJoypadNavigationInput(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed(GameInputActions.GameConfirm)
			|| inputEvent.IsActionPressed(GameInputActions.GameCancel)
			|| inputEvent.IsActionPressed(GameInputActions.HandPrevious)
			|| inputEvent.IsActionPressed(GameInputActions.HandNext)
			|| inputEvent.IsActionPressed("ui_accept")
			|| inputEvent.IsActionPressed("ui_cancel")
			|| inputEvent.IsActionPressed("ui_up")
			|| inputEvent.IsActionPressed("ui_down")
			|| inputEvent.IsActionPressed("ui_left")
			|| inputEvent.IsActionPressed("ui_right"))
		{
			return true;
		}

		return inputEvent is InputEventJoypadButton { Pressed: true }
			|| inputEvent is InputEventJoypadMotion joypadMotion
				&& Mathf.Abs(joypadMotion.AxisValue) > 0.45f;
	}
}
