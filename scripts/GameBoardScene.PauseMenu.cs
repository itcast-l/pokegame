using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";
	private const string AdventureAvatarMenuScenePath = "res://scenes/adventure_avatar_menu_scene.tscn";

	private GameBoardPauseMenuController? _pauseMenuController;

	private GameBoardPauseMenuController PauseMenu
		=> _pauseMenuController ??= new GameBoardPauseMenuController(
			this,
			Localize,
			GetPauseReturnButtonKey,
			GetPauseRestartButtonKey,
			CanRestartCurrentMatchFromPauseMenu,
			ReturnToPauseMenuDestination,
			RestartCurrentMatchFromPauseMenu,
			QuitGameFromPauseMenu,
			ConfirmQuitGameFromPauseMenu,
			() => PlayGameSfx(GameSfx.UiPauseOpen, -2.0f, 0.98f, 1.02f, 0.08),
			() => PlayGameSfx(GameSfx.UiPauseClose, -2.0f, 0.98f, 1.02f, 0.08),
			() => PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08));

	private bool IsPauseMenuOpen => _pauseMenuController?.IsOpen == true;

	public override void _Input(InputEvent inputEvent)
	{
		if (!TryOpenPauseMenuFromInput(inputEvent))
		{
			return;
		}

		GetViewport().SetInputAsHandled();
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (TryOpenPauseMenuFromInput(inputEvent))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (TryHandleGameBoardControlInput(inputEvent))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (IsPauseMenuOpen)
		{
			return;
		}

		if (!IsPauseMenuRequested(inputEvent))
		{
			return;
		}

		ShowPauseMenu();
		GetViewport().SetInputAsHandled();
	}

	private bool TryOpenPauseMenuFromInput(InputEvent inputEvent)
	{
		if (IsPauseMenuOpen || !IsPauseMenuRequested(inputEvent))
		{
			return false;
		}

		if (HasVisibleSelectionOverlay())
		{
			return false;
		}

		ShowPauseMenu();
		return true;
	}

	private static bool IsPauseMenuRequested(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed(GameInputActions.GamePause))
		{
			return true;
		}

		return inputEvent is InputEventKey
		{
			Pressed: true,
			Echo: false,
			Keycode: Key.Escape,
		};
	}

	private void ShowPauseMenu()
	{
		PauseMenu.Show();
	}

	private void HidePauseMenu()
	{
		PauseMenu.Hide();
	}

	private async void ReturnToMainMenu()
	{
		await ReturnToMenuSceneAsync(MainMenuScenePath);
	}

	private async void ReturnToAdventureMenu()
	{
		await ReturnToMenuSceneAsync(AdventureAvatarMenuScenePath);
	}

	private void ReturnToPauseMenuDestination()
	{
		if (adventure_mode)
		{
			ReturnToAdventureMenu();
			return;
		}

		ReturnToMainMenu();
	}

	private string GetPauseReturnButtonKey()
	{
		return adventure_mode ? "GAME_RETURN_ADVENTURE" : "GAME_RETURN_MAIN_MENU";
	}

	private string GetPauseRestartButtonKey()
	{
		return ShouldUseAdventureUpgradeSystem()
			? "GAME_RESTART_ADVENTURE_JOURNEY"
			: "GAME_RESTART_MATCH";
	}

	private async Task ReturnToMenuSceneAsync(string scenePath)
	{
		if (_isLeavingGame)
		{
			return;
		}

		PackedScene? menuScene = GD.Load<PackedScene>(scenePath);
		if (menuScene == null)
		{
			GD.PushError($"Missing menu scene: {scenePath}");
			return;
		}

		_isLeavingGame = true;
		_pauseMenuController?.CloseForSceneTransition();
		GetTree().Paused = false;
			if (IsNetworkMultiplayerMode())
			{
				NetworkManager? networkManager = GetNetworkManager();
				if (networkManager != null)
				{
					await networkManager.LeaveSessionAfterHostEndedMatchAsync();
				}
			}

		await CircularSceneTransition.PlaySceneSwapAsync(this, () =>
		{
			Node menu = menuScene.Instantiate();
			GetTree().Root.AddChild(menu);
			GetTree().CurrentScene = menu;
			QueueFree();
		});
	}

	private void QuitGameFromPauseMenu()
	{
		ShowPauseQuitConfirm();
	}

	private void ShowPauseQuitConfirm()
	{
		if (_isLeavingGame)
		{
			return;
		}

		PauseMenu.ShowQuitConfirmation();
	}

	private void HidePauseQuitConfirm()
	{
		_pauseMenuController?.HideQuitConfirmation();
	}

	private bool CanRestartCurrentMatchFromPauseMenu()
	{
		return !IsNetworkMultiplayerMode();
	}

	private void RestartCurrentMatchFromPauseMenu()
	{
		if (!CanRestartCurrentMatchFromPauseMenu() || _isLeavingGame)
		{
			return;
		}

		HidePauseMenu();
		if (ShouldUseAdventureUpgradeSystem())
		{
			RestartAdventureJourneyFromPauseMenu();
			return;
		}
		DealCardsToPlayers(playOpeningDealAnimation: true);
	}

	private void RestartAdventureJourneyFromPauseMenu()
	{
		string firstAvatarId = GetFirstMainAdventureAvatarId();
		string scenePath = AdventureAvatarMenuScene.GetGameBoardScenePathForAdventureAvatar(firstAvatarId);
		PackedScene? gameBoardScene = GD.Load<PackedScene>(scenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {scenePath}");
			return;
		}

		AdventureRunWriteResult writeResult = GetAdventureRunSessionStore()?.BeginNewMainRun(
			1,
			firstAvatarId) ?? AdventureRunWriteResult.SaveFailed;
		if (writeResult != AdventureRunWriteResult.Succeeded)
		{
			GD.PushWarning($"Failed to restart adventure journey: {writeResult}");
			ShowPauseMenu();
			return;
		}
		_ = StartNextAdventureStageWithTransitionAsync(gameBoardScene, firstAvatarId);
	}

	private async void ConfirmQuitGameFromPauseMenu()
	{
		if (_isLeavingGame)
		{
			return;
		}

		_isLeavingGame = true;
		GetTree().Paused = false;
			if (IsNetworkMultiplayerMode())
			{
				NetworkManager? networkManager = GetNetworkManager();
				if (networkManager != null)
				{
					await networkManager.LeaveSessionAfterHostEndedMatchAsync();
				}
			}

		GetTree().Quit();
	}
}
