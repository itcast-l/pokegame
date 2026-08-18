using System;
using Godot;

public partial class MainMenuScene : Control
{
	private enum PrimaryMenuAction
	{
		SinglePlayer,
		Adventure,
		Multiplayer,
		SkillCodex,
		Settings,
		Exit,
		AvatarEditor,
	}

	private Action? _primaryMenuReturnFinalizer;
	private Action? _primaryMenuReturnFocus;
	private uint _primaryMenuReturnToken;

	private void DispatchPrimaryMenuAction(PrimaryMenuAction action)
	{
		if (_quitInProgress || _isStartingGameTransition || _isShowingAvatarUnlockPrompt)
		{
			return;
		}

		FinalizePrimaryMenuReturn(forNewAction: true);
		if (_isMenuCameraTransitioning || IsMenuOverlayVisible())
		{
			return;
		}

		InvalidatePendingAvatarUnlockProbe();
		switch (action)
		{
			case PrimaryMenuAction.SinglePlayer:
				StartSinglePlayerMenu();
				break;
			case PrimaryMenuAction.Adventure:
				StartAdventureMode();
				break;
			case PrimaryMenuAction.Multiplayer:
				StartMultiplayerLobby();
				break;
			case PrimaryMenuAction.SkillCodex:
				ShowSkillCodex();
				break;
			case PrimaryMenuAction.Settings:
				ShowSettingsDialog();
				break;
			case PrimaryMenuAction.Exit:
				ShowExitDialog();
				break;
			case PrimaryMenuAction.AvatarEditor:
				OpenAvatarEditor();
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(action), action, null);
		}
	}

	private uint BeginPrimaryMenuReturn(Action finalizer, Action? returnFocus = null)
	{
		FinalizePrimaryMenuReturn(forNewAction: true);
		_primaryMenuReturnToken++;
		_primaryMenuReturnFinalizer = finalizer;
		_primaryMenuReturnFocus = returnFocus;
		SetMainMenuButtonsDisabled(false);
		return _primaryMenuReturnToken;
	}

	private void CompletePrimaryMenuReturn(uint returnToken)
	{
		if (_primaryMenuReturnFinalizer == null || returnToken != _primaryMenuReturnToken)
		{
			return;
		}

		FinalizePrimaryMenuReturn(forNewAction: false);
	}

	private void FinalizePrimaryMenuReturn(bool forNewAction)
	{
		Action? finalizer = _primaryMenuReturnFinalizer;
		if (finalizer == null)
		{
			return;
		}

		Action? returnFocus = _primaryMenuReturnFocus;
		_primaryMenuReturnFinalizer = null;
		_primaryMenuReturnFocus = null;
		_primaryMenuReturnToken++;
		finalizer();
		SetMainMenuButtonsDisabled(false);
		SyncPrimaryMenuFocusState();

		if (forNewAction)
		{
			return;
		}

		returnFocus?.Invoke();
		ShowPendingAdventureAvatarUnlockPrompt();
	}

	private bool IsPrimaryMenuReturning()
	{
		return _primaryMenuReturnFinalizer != null;
	}

#if DECKLAND_TEST_HOOKS
	public string DebugGetPrimaryMenuInteractionState()
	{
		return $"quit={_quitInProgress} starting={_isStartingGameTransition} "
			+ $"unlock={_isShowingAvatarUnlockPrompt} camera={_isMenuCameraTransitioning} "
			+ $"returning={IsPrimaryMenuReturning()} overlay={IsMenuOverlayVisible()} "
			+ $"player={IsVisible(_playerCountDialogRoot)} settings={IsVisible(_settingsDialogRoot)} "
			+ $"controls={IsVisible(_controlsDialogRoot)} exit={IsVisible(_exitDialogRoot)} "
			+ $"avatar={IsVisible(_avatarEditDialogRoot)} skill={_skillCodexOverlay?.IsOpen == true} "
			+ $"adventure={IsVisible(_adventureTransitionRoot)}";
	}
#endif
}
