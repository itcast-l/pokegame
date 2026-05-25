using Godot;

public partial class AdventureAvatarMenuScene
{
	private void StartAdventure()
	{
		if (!IsStageUnlocked(_selectedAvatarId))
		{
			RefreshSelection();
			return;
		}

		PackedScene? gameBoardScene = GD.Load<PackedScene>(GameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {GameBoardScenePath}");
			return;
		}

		Node gameBoard = gameBoardScene.Instantiate();
		gameBoard.Set("player_count", 2);
		gameBoard.Set("item_mode", true);
		gameBoard.Set("solo_mode", true);
		gameBoard.Set("adventure_mode", true);
		gameBoard.Set("adventure_enemy_avatar_id", _selectedAvatarId);
		Node? currentScene = GetTree().CurrentScene;
		GetTree().Root.AddChild(gameBoard);
		GetTree().CurrentScene = gameBoard;
		if (currentScene != null && GodotObject.IsInstanceValid(currentScene) && currentScene != gameBoard)
		{
			currentScene.QueueFree();
		}

		QueueFree();
	}

	private void ReturnToMainMenu()
	{
		if (_isReturningToMainMenu)
		{
			return;
		}

		_isReturningToMainMenu = true;
		_backButton.Disabled = true;
		_startButton.Disabled = true;
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 1000;

		MainMenuScene? embeddedMainMenu = GetEmbeddedMainMenuParent();
		if (embeddedMainMenu != null)
		{
			embeddedMainMenu.HideAdventureAvatarMenu(this);
			return;
		}

		PackedScene? mainMenuScene = GD.Load<PackedScene>(MainMenuScenePath);
		if (mainMenuScene == null)
		{
			GD.PushError($"Missing main menu scene: {MainMenuScenePath}");
			_isReturningToMainMenu = false;
			_backButton.Disabled = false;
			_startButton.Disabled = false;
			MouseFilter = MouseFilterEnum.Stop;
			ZIndex = 0;
			return;
		}

		Node mainMenuNode = mainMenuScene.Instantiate();
		if (mainMenuNode is not MainMenuScene mainMenu)
		{
			GD.PushError($"{MainMenuScenePath} root must use MainMenuScene.");
			mainMenuNode.QueueFree();
			_isReturningToMainMenu = false;
			_backButton.Disabled = false;
			_startButton.Disabled = false;
			MouseFilter = MouseFilterEnum.Stop;
			ZIndex = 0;
			return;
		}

		SceneTree tree = GetTree();
		mainMenu.PrepareAdventureReturnTransition();
		tree.Root.AddChild(mainMenu);
		tree.CurrentScene = mainMenu;

		PivotOffset = Size * 0.5f;
		Tween returnTween = CreateTween();
		returnTween.SetParallel(true);
		returnTween.TweenProperty(this, "modulate:a", 0.0f, ReturnFadeSeconds)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
		returnTween.TweenProperty(this, "scale", new Vector2(ReturnScale, ReturnScale), ReturnFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		returnTween.Finished += () => QueueFree();
	}

	private MainMenuScene? GetEmbeddedMainMenuParent()
	{
		Node? parent = GetParent();
		while (parent != null)
		{
			if (parent is MainMenuScene mainMenu)
			{
				return mainMenu;
			}

			parent = parent.GetParent();
		}

		return null;
	}
}
