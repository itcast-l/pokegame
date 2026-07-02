using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";
	private const string AdventureAvatarMenuScenePath = "res://scenes/adventure_avatar_menu_scene.tscn";

	private CanvasLayer? _pauseMenuLayer;
	private PauseMenuOverlay? _pauseMenuRoot;
	private Button? _pauseContinueButton;
	private bool _isPauseMenuOpen;

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

		if (_isPauseMenuOpen)
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
		if (_isPauseMenuOpen || !IsPauseMenuRequested(inputEvent))
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

	private void EnsurePauseMenu()
	{
		if (_pauseMenuRoot != null)
		{
			return;
		}

		PackedScene? pauseMenuScene = GD.Load<PackedScene>(PauseMenuScenePath);
		if (pauseMenuScene?.Instantiate() is not PauseMenuOverlay pauseMenu)
		{
			GD.PushError($"Missing pause menu scene: {PauseMenuScenePath}");
			return;
		}

		_pauseMenuLayer = new CanvasLayer
		{
			Name = "PauseMenuLayer",
			Layer = 60,
			ProcessMode = ProcessModeEnum.Always,
		};
		AddChild(_pauseMenuLayer);
		_pauseMenuLayer.AddChild(pauseMenu);

		_pauseMenuRoot = pauseMenu;
		_pauseMenuRoot.CancelPressed = HandlePauseMenuCancelPressed;
		_pauseContinueButton = pauseMenu.ContinueButton;
		pauseMenu.ContinueRequested += HidePauseMenu;
		pauseMenu.ReturnToMainMenuRequested += ReturnToPauseMenuDestination;
		pauseMenu.RestartRequested += RestartCurrentMatchFromPauseMenu;
		pauseMenu.QuitRequested += QuitGameFromPauseMenu;
		pauseMenu.ConfirmQuitRequested += ConfirmQuitGameFromPauseMenu;
		pauseMenu.RefreshLocalizedText(Localize, GetPauseReturnButtonKey());
		pauseMenu.SetRestartAvailable(CanRestartCurrentMatchFromPauseMenu());
	}

	private void ShowPauseMenu()
	{
		if (_isPauseMenuOpen)
		{
			return;
		}

		EnsurePauseMenu();
		if (_pauseMenuRoot == null)
		{
			return;
		}

		_isPauseMenuOpen = true;
		PlayGameSfx(GameSfx.UiPauseOpen, -2.0f, 0.98f, 1.02f, 0.08);
		_pauseMenuRoot.RefreshLocalizedText(Localize, GetPauseReturnButtonKey());
		_pauseMenuRoot.SetRestartAvailable(CanRestartCurrentMatchFromPauseMenu());
		_pauseMenuRoot.Visible = true;
		_pauseMenuRoot.Modulate = Colors.White;
		_pauseMenuRoot.HideQuitConfirm();

		GetTree().Paused = true;
		_pauseContinueButton?.GrabFocus();
	}

	private void HandlePauseMenuCancelPressed()
	{
		if (_pauseMenuRoot?.QuitConfirmRoot.Visible == true)
		{
			HidePauseQuitConfirm();
			return;
		}

		HidePauseMenu();
	}

	private void HidePauseMenu()
	{
		if (!_isPauseMenuOpen)
		{
			return;
		}

		_isPauseMenuOpen = false;
		PlayGameSfx(GameSfx.UiPauseClose, -2.0f, 0.98f, 1.02f, 0.08);
		GetTree().Paused = false;
		if (_pauseMenuRoot != null)
		{
			_pauseMenuRoot.HideQuitConfirm();
			_pauseMenuRoot.Visible = false;
		}
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
		_isPauseMenuOpen = false;
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

		if (_pauseMenuRoot != null)
		{
			_pauseMenuRoot.MouseFilter = MouseFilterEnum.Ignore;
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

		EnsurePauseMenu();
		PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
		_pauseMenuRoot?.ShowQuitConfirm();
	}

	private void HidePauseQuitConfirm()
	{
		_pauseMenuRoot?.HideQuitConfirm();
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
