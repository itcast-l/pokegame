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
		if (ShouldUseAdventureUpgradeSystem())
		{
			ClearAdventureUpgradeRun();
		}
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
			int avatarStageNumber = CharacterAvatarCatalog.GetAdventureStageNumber(adventure_enemy_avatar_id);
			ClearAdventureUpgradeRun();
			BeginAdventureUpgradeRun(avatarStageNumber);
		}
		DealCardsToPlayers(playOpeningDealAnimation: true);
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
