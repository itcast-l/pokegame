using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";
	private const string AdventureAvatarMenuScenePath = "res://scenes/adventure_avatar_menu_scene.tscn";

	private CanvasLayer? _pauseMenuLayer;
	private PauseMenuOverlay? _pauseMenuRoot;
	private Button? _pauseContinueButton;
	private Button? _pauseQuitButton;
	private Control? _pauseQuitConfirmRoot;
	private bool _isPauseMenuOpen;
	private bool _isLeavingGame;

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!inputEvent.IsActionPressed("ui_cancel"))
		{
			return;
		}

		ShowPauseMenu();
		GetViewport().SetInputAsHandled();
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
		_pauseQuitButton = pauseMenu.QuitButton;
		_pauseQuitConfirmRoot = pauseMenu.QuitConfirmRoot;
		pauseMenu.ContinueRequested += HidePauseMenu;
		pauseMenu.ReturnToMainMenuRequested += ReturnToMainMenu;
		pauseMenu.QuitRequested += QuitGameFromPauseMenu;
		pauseMenu.ConfirmQuitRequested += ConfirmQuitGameFromPauseMenu;
		pauseMenu.RefreshLocalizedText(Localize);
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
		_pauseMenuRoot.Visible = true;
		_pauseMenuRoot.Modulate = Colors.White;
		if (_pauseQuitConfirmRoot != null)
		{
			_pauseQuitConfirmRoot.Visible = false;
		}

		GetTree().Paused = true;
		_pauseContinueButton?.GrabFocus();
	}

	private void HandlePauseMenuCancelPressed()
	{
		if (_pauseQuitConfirmRoot != null && _pauseQuitConfirmRoot.Visible)
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
		GetTree().Paused = false;
		if (_pauseMenuRoot != null)
		{
			_pauseMenuRoot.Visible = false;
		}

		if (_pauseQuitConfirmRoot != null)
		{
			_pauseQuitConfirmRoot.Visible = false;
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
		_pauseMenuRoot?.ShowQuitConfirm();
	}

	private void HidePauseQuitConfirm()
	{
		if (_pauseQuitConfirmRoot == null || !_pauseQuitConfirmRoot.Visible)
		{
			return;
		}

		_pauseMenuRoot?.HideQuitConfirm();
		_pauseQuitButton?.GrabFocus();
	}

	private void ConfirmQuitGameFromPauseMenu()
	{
		if (_isLeavingGame)
		{
			return;
		}

		_isLeavingGame = true;
		GetTree().Paused = false;
		GetTree().Quit();
	}
}
