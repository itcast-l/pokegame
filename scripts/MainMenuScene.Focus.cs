using Godot;

public partial class MainMenuScene : Control
{
	private void ConfigureMainMenuFocus()
	{
		ConfigureAvatarEntryFocus();
		SetPrimaryMenuFocusEnabled(true);
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
			|| _skillCodexOverlay?.IsOpen == true
			|| IsVisible(_settingsDialogRoot)
			|| IsVisible(_controlsDialogRoot)
			|| IsVisible(_exitDialogRoot)
			|| IsVisible(_avatarEditDialogRoot)
			|| IsVisible(_avatarUnlockPromptRoot)
			|| IsVisible(_adventureTransitionRoot);
	}

	private void SyncPrimaryMenuFocusState()
	{
		SetPrimaryMenuFocusEnabled(!IsMenuOverlayVisible());
	}

	private void SetPrimaryMenuFocusEnabled(bool enabled)
	{
		Control.FocusModeEnum focusMode = enabled ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
		SetFocusModeIfValid(_avatarEntry?.Button, focusMode);
		SetFocusModeIfValid(_singlePlayerButton, focusMode);
		SetFocusModeIfValid(_adventureButton, focusMode);
		SetFocusModeIfValid(_skillCodexButton, focusMode);
		SetFocusModeIfValid(_settingsButton, focusMode);
		SetFocusModeIfValid(_exitButton, focusMode);
		SetFocusModeIfValid(_multiplayerButton, Control.FocusModeEnum.None);

		if (enabled)
		{
			return;
		}

		Control? focusOwner = GetViewport().GuiGetFocusOwner();
		if (IsPrimaryMenuControl(focusOwner))
		{
			GetViewport().GuiReleaseFocus();
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

	private bool IsPrimaryMenuControl(Control? control)
	{
		return control == _avatarEntry?.Button
			|| control == _singlePlayerButton
			|| control == _adventureButton
			|| control == _multiplayerButton
			|| control == _skillCodexButton
			|| control == _settingsButton
			|| control == _exitButton;
	}

	private static void SetFocusModeIfValid(Control? control, Control.FocusModeEnum focusMode)
	{
		if (control != null && GodotObject.IsInstanceValid(control))
		{
			control.FocusMode = focusMode;
		}
	}
}
