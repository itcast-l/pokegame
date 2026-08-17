using System.Collections.Generic;
using Godot;

public partial class MainMenuScene : Control
{
	private readonly List<OceanMenuButtonFrame> _primaryMenuFrames = new();
	private bool _primaryMenuFocusHighlightVisible = true;

	private void ConfigureMainMenuFocus()
	{
		ConfigureAvatarEntryFocus();
		ConfigurePrimaryMenuFocusHighlights();
		SetPrimaryMenuFocusEnabled(true);
	}

	private void ConfigurePrimaryMenuFocusHighlights()
	{
		_primaryMenuFrames.Clear();
		AddPrimaryMenuFrame(_singlePlayerButton);
		AddPrimaryMenuFrame(_adventureButton);
		AddPrimaryMenuFrame(_multiplayerButton);
		AddPrimaryMenuFrame(_skillCodexButton);
		AddPrimaryMenuFrame(_settingsButton);
		AddPrimaryMenuFrame(_exitButton);
		SetPrimaryMenuFocusHighlightVisible(false);
	}

	private void TrackPrimaryMenuInputMode(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseMotion)
		{
			EnterPrimaryMenuPointerMode();
			return;
		}
		if (inputEvent is InputEventMouseButton)
		{
			SetPrimaryMenuFocusHighlightVisible(false);
			return;
		}

		if (IsPrimaryMenuNavigationInput(inputEvent))
		{
			SetPrimaryMenuFocusHighlightVisible(true);
			if (GetViewport().GuiGetFocusOwner() == null)
			{
				FocusInitialMainMenuButton();
			}
		}
	}

	private void EnterPrimaryMenuPointerMode()
	{
		SetPrimaryMenuFocusHighlightVisible(false);
		if (IsPrimaryMenuControl(GetViewport().GuiGetFocusOwner()))
		{
			GetViewport().GuiReleaseFocus();
		}
	}

	private void SetPrimaryMenuFocusHighlightVisible(bool visible)
	{
		if (_primaryMenuFocusHighlightVisible == visible)
		{
			return;
		}

		_primaryMenuFocusHighlightVisible = visible;
		foreach (OceanMenuButtonFrame frame in _primaryMenuFrames)
		{
			if (!GodotObject.IsInstanceValid(frame))
			{
				continue;
			}

			frame.HighlightFocus = visible;
			frame.QueueRedraw();
		}
	}

	private void AddPrimaryMenuFrame(Button button)
	{
		if (button.GetParent() is OceanMenuButtonFrame frame)
		{
			_primaryMenuFrames.Add(frame);
			button.MouseEntered += EnterPrimaryMenuPointerMode;
		}
	}

	private static bool IsPrimaryMenuNavigationInput(InputEvent inputEvent)
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

#if DECKLAND_TEST_HOOKS
	public bool DebugIsPrimaryMenuFocusHighlightVisible()
	{
		return _primaryMenuFocusHighlightVisible;
	}
#endif

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
		_playerCountDialog?.FocusDefaultControl();
	}

	private void FocusSettingsDialogDefault()
	{
		_settingsDialog?.FocusDefaultControl();
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
			|| (_adventureTransitionRoot?.ProcessMode != ProcessModeEnum.Disabled
				&& IsVisible(_adventureTransitionRoot));
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
		SetFocusModeIfValid(_multiplayerButton, focusMode);
		SetFocusModeIfValid(_skillCodexButton, focusMode);
		SetFocusModeIfValid(_settingsButton, focusMode);
		SetFocusModeIfValid(_exitButton, focusMode);

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
