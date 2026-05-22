using Godot;

public partial class AdventureAvatarMenuScene
{
	private void StartAdventure()
	{
		PackedScene? gameBoardScene = GD.Load<PackedScene>(GameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {GameBoardScenePath}");
			return;
		}

		Node gameBoard = gameBoardScene.Instantiate();
		gameBoard.Set("player_count", 2);
		gameBoard.Set("item_mode", true);
		gameBoard.Set("adventure_mode", true);
		gameBoard.Set("adventure_enemy_avatar_id", _selectedAvatarId);
		GetTree().Root.AddChild(gameBoard);
		GetTree().CurrentScene = gameBoard;
		QueueFree();
	}

	private void ReturnToMainMenu()
	{
		if (_isReturningToMainMenu)
		{
			return;
		}

		PackedScene? mainMenuScene = GD.Load<PackedScene>(MainMenuScenePath);
		if (mainMenuScene == null)
		{
			GD.PushError($"Missing main menu scene: {MainMenuScenePath}");
			return;
		}

		_isReturningToMainMenu = true;
		_backButton.Disabled = true;
		_startButton.Disabled = true;

		Node mainMenuNode = mainMenuScene.Instantiate();
		if (mainMenuNode is not MainMenuScene mainMenu)
		{
			GD.PushError($"{MainMenuScenePath} root must use MainMenuScene.");
			mainMenuNode.QueueFree();
			_isReturningToMainMenu = false;
			_backButton.Disabled = false;
			_startButton.Disabled = false;
			return;
		}

		SceneTree tree = GetTree();
		mainMenu.PrepareAdventureReturnTransition();
		tree.Root.AddChild(mainMenu);
		tree.CurrentScene = mainMenu;
		QueueFree();
	}
}
