using Godot;

public partial class MainMenuScene : Control
{
	private void ConfigureMainMenuFocus()
	{
		ConfigureAvatarEntryFocus();
		EnableKeyboardFocus(_singlePlayerButton, _adventureButton, _settingsButton, _exitButton);
		_multiplayerButton.FocusMode = Control.FocusModeEnum.None;
	}

	private void FocusInitialMainMenuButton()
	{
		if (IsMenuOverlayVisible())
		{
			return;
		}

		GrabFocusIfAvailable(_singlePlayerButton);
	}

	private void FocusSettingsButton()
	{
		GrabFocusIfAvailable(_settingsButton);
	}

	private void FocusExitButton()
	{
		GrabFocusIfAvailable(_exitButton);
	}

	private void FocusPlayerCountDialogDefault()
	{
		GrabFocusIfAvailable(_playerCountOption);
	}

	private void FocusSettingsDialogDefault()
	{
		GrabFocusIfAvailable(_masterVolumeSlider);
	}

	private void FocusControlsDialogDefault()
	{
		_controlsDialog?.FocusDefaultButton();
	}

	private void FocusExitDialogDefault()
	{
		GrabFocusIfAvailable(_exitDialog.CancelButton);
	}

	private bool IsMenuOverlayVisible()
	{
		return IsVisible(_playerCountDialogRoot)
			|| IsVisible(_settingsDialogRoot)
			|| IsVisible(_controlsDialogRoot)
			|| IsVisible(_exitDialogRoot)
			|| IsVisible(_avatarEditDialogRoot)
			|| IsVisible(_avatarUnlockPromptRoot)
			|| IsVisible(_adventureTransitionRoot);
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

	private void ConfigureAvatarEntryFocus()
	{
		if (!GodotObject.IsInstanceValid(_avatarEntry.Button))
		{
			return;
		}

		NodePath avatarEntryPath = GetPathTo(_avatarEntry.Button);
		NodePath singlePlayerPath = GetPathTo(_singlePlayerButton);
		_avatarEntry.Button.FocusNeighborTop = singlePlayerPath;
		_avatarEntry.Button.FocusNeighborLeft = singlePlayerPath;
		_avatarEntry.Button.FocusNeighborBottom = singlePlayerPath;
		_avatarEntry.Button.FocusNeighborRight = singlePlayerPath;

		_singlePlayerButton.FocusNeighborTop = avatarEntryPath;
		_singlePlayerButton.FocusNeighborLeft = avatarEntryPath;
	}

	private static void GrabFocusIfAvailable(Control? control)
	{
		if (control == null || !IsVisible(control) || control.FocusMode == Control.FocusModeEnum.None)
		{
			return;
		}

		if (control is BaseButton { Disabled: true })
		{
			return;
		}

		control.GrabFocus();
	}

	private static bool IsVisible(CanvasItem? item)
	{
		return item != null && GodotObject.IsInstanceValid(item) && item.IsVisibleInTree();
	}
}
