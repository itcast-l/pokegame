using Godot;

public partial class AdventureAvatarMenuScene : Control
{
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
}
