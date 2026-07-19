using System;
using Godot;

internal sealed class GameBoardPauseMenuController
{
	private const string PauseMenuScenePath = "res://scenes/pause_menu.tscn";

	private readonly Node _owner;
	private readonly Func<string, string> _localize;
	private readonly Func<string> _getReturnButtonKey;
	private readonly Func<string> _getRestartButtonKey;
	private readonly Func<bool> _canRestart;
	private readonly Func<bool> _shouldPauseSceneTree;
	private readonly Action _returnToDestination;
	private readonly Action _restart;
	private readonly Action _quit;
	private readonly Action _confirmQuit;
	private readonly Action _playOpenSound;
	private readonly Action _playCloseSound;
	private readonly Action _playPanelOpenSound;
	private CanvasLayer? _pauseMenuLayer;
	private PauseMenuOverlay? _pauseMenuRoot;
	private Button? _pauseContinueButton;
	private bool _ownsSceneTreePause;

	internal GameBoardPauseMenuController(
		Node owner,
		Func<string, string> localize,
		Func<string> getReturnButtonKey,
		Func<string> getRestartButtonKey,
		Func<bool> canRestart,
		Func<bool> shouldPauseSceneTree,
		Action returnToDestination,
		Action restart,
		Action quit,
		Action confirmQuit,
		Action playOpenSound,
		Action playCloseSound,
		Action playPanelOpenSound)
	{
		_owner = owner;
		_localize = localize;
		_getReturnButtonKey = getReturnButtonKey;
		_getRestartButtonKey = getRestartButtonKey;
		_canRestart = canRestart;
		_shouldPauseSceneTree = shouldPauseSceneTree;
		_returnToDestination = returnToDestination;
		_restart = restart;
		_quit = quit;
		_confirmQuit = confirmQuit;
		_playOpenSound = playOpenSound;
		_playCloseSound = playCloseSound;
		_playPanelOpenSound = playPanelOpenSound;
	}

	internal bool IsOpen { get; private set; }

	internal bool HasVisibleQuitConfirmation
		=> _pauseMenuRoot?.QuitConfirmRoot.Visible == true;

	internal void Show()
	{
		if (IsOpen)
		{
			return;
		}

		EnsurePauseMenu();
		if (_pauseMenuRoot == null)
		{
			return;
		}

		IsOpen = true;
		_playOpenSound();
		_pauseMenuRoot.RefreshLocalizedText(_localize, _getReturnButtonKey(), _getRestartButtonKey());
		_pauseMenuRoot.SetRestartAvailable(_canRestart());
		_pauseMenuRoot.Visible = true;
		_pauseMenuRoot.Modulate = Colors.White;
		_pauseMenuRoot.HideQuitConfirm();

		SceneTree sceneTree = _owner.GetTree();
		if (_shouldPauseSceneTree() && !sceneTree.Paused)
		{
			sceneTree.Paused = true;
			_ownsSceneTreePause = true;
		}
		_pauseContinueButton?.GrabFocus();
	}

	internal void Hide()
	{
		if (!IsOpen)
		{
			return;
		}

		IsOpen = false;
		_playCloseSound();
		RestoreSceneTreePauseIfOwned();
		if (_pauseMenuRoot != null)
		{
			_pauseMenuRoot.HideQuitConfirm();
			_pauseMenuRoot.Visible = false;
		}
	}

	internal void HandleCancel()
	{
		if (HasVisibleQuitConfirmation)
		{
			HideQuitConfirmation();
			return;
		}

		Hide();
	}

	internal void ShowQuitConfirmation()
	{
		EnsurePauseMenu();
		_playPanelOpenSound();
		_pauseMenuRoot?.ShowQuitConfirm();
	}

	internal void HideQuitConfirmation()
	{
		_pauseMenuRoot?.HideQuitConfirm();
	}

	internal void RefreshLocalizedText()
	{
		_pauseMenuRoot?.RefreshLocalizedText(_localize, _getReturnButtonKey(), _getRestartButtonKey());
	}

	internal void CloseForSceneTransition()
	{
		IsOpen = false;
		RestoreSceneTreePauseIfOwned();
		if (_pauseMenuRoot != null)
		{
			_pauseMenuRoot.MouseFilter = Control.MouseFilterEnum.Ignore;
		}
	}

	private void RestoreSceneTreePauseIfOwned()
	{
		if (!_ownsSceneTreePause)
		{
			return;
		}

		_ownsSceneTreePause = false;
		_owner.GetTree().Paused = false;
	}

	private void EnsurePauseMenu()
	{
		if (_pauseMenuRoot != null && GodotObject.IsInstanceValid(_pauseMenuRoot))
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
			ProcessMode = Node.ProcessModeEnum.Always,
		};
		_owner.AddChild(_pauseMenuLayer);
		_pauseMenuLayer.AddChild(pauseMenu);

		_pauseMenuRoot = pauseMenu;
		_pauseMenuRoot.CancelPressed = HandleCancel;
		_pauseContinueButton = pauseMenu.ContinueButton;
		pauseMenu.ContinueRequested += Hide;
		pauseMenu.ReturnToMainMenuRequested += () => _returnToDestination();
		pauseMenu.RestartRequested += () => _restart();
		pauseMenu.QuitRequested += () => _quit();
		pauseMenu.ConfirmQuitRequested += () => _confirmQuit();
		_pauseMenuRoot.RefreshLocalizedText(_localize, _getReturnButtonKey(), _getRestartButtonKey());
		_pauseMenuRoot.SetRestartAvailable(_canRestart());
	}
}
