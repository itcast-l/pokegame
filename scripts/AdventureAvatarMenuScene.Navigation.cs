using System.Threading.Tasks;
using Godot;

public partial class AdventureAvatarMenuScene
{
	public void SetEmbeddedVisualWarmup(bool enabled)
	{
		_isEmbeddedVisualWarmup = enabled;
	}

	public void PrepareForEmbeddedDisplay()
	{
		PrepareForEmbeddedDisplayForInputMode(keyboardOrJoypad: true);
	}

	public void PrepareForEmbeddedDisplayForInputMode(bool keyboardOrJoypad)
	{
		SetListSelectionHighlightVisible(keyboardOrJoypad);
		_isReturningToMainMenu = false;
		_backButton.Disabled = false;
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 0;
		Modulate = Colors.White;
		Scale = Vector2.One;
		_startButton.Disabled = false;
		ShowPendingAdventureAvatarUnlockPrompt();
	}

	private void StartAdventure()
	{
		if (_isStartingAdventure || _isReturningToMainMenu || _isShowingRunResumeDialog)
		{
			return;
		}
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		AdventureRunSessionStore? runSessionStore = GetAdventureRunSessionStore();
		GameModePolicy selectedPolicy = _selectedAdventureId == AdventureCampaignCatalog.TutorialCampaignId
			? GameModePolicy.AdventureTutorial
			: GameModePolicy.AdventureMain;
		runSessionStore?.ConfigureForPolicy(selectedPolicy);
		runSessionStore?.ReloadFromActiveProfile();
		if (_selectedAdventureId == AdventureCampaignCatalog.MainCampaignId
			&& (runSessionStore?.HasActiveRun == true
				|| runSessionStore?.RequiresRunRecovery == true))
		{
			ShowRunResumeDialog();
			return;
		}

		if (!IsStageUnlocked(_selectedAvatarId))
		{
			RefreshSelection();
			return;
		}

		string selectedAvatarId = _selectedAvatarId;
		string gameBoardScenePath = GetGameBoardScenePathForAvatar(selectedAvatarId);
		PackedScene? gameBoardScene = GD.Load<PackedScene>(gameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {gameBoardScenePath}");
			return;
		}

		_isStartingAdventure = true;
		_backButton.Disabled = true;
		_startButton.Disabled = true;
		MouseFilter = MouseFilterEnum.Ignore;

		_ = StartAdventureWithTransitionAsync(gameBoardScene, selectedAvatarId);
	}

	private async Task StartAdventureWithTransitionAsync(
		PackedScene gameBoardScene,
		string selectedAvatarId,
		bool beginNewRun = true)
	{
		if (!EnsureItemCardCatalogReady())
		{
			_isStartingAdventure = false;
			_backButton.Disabled = false;
			_startButton.Disabled = false;
			MouseFilter = MouseFilterEnum.Stop;
			return;
		}

		if (AdventureCampaignCatalog.TryGetMainStageIndex(selectedAvatarId, out int mainStageIndex))
		{
			GetAdventureRunSessionStore()?.ConfigureForPolicy(GameModePolicy.AdventureMain);
			if (beginNewRun)
			{
				AdventureRunWriteResult writeResult = GetAdventureRunSessionStore()?.BeginNewMainRun(
					mainStageIndex,
					selectedAvatarId) ?? AdventureRunWriteResult.SaveFailed;
				if (writeResult != AdventureRunWriteResult.Succeeded)
				{
					GD.PushWarning($"Failed to start adventure run: {writeResult}");
					_isStartingAdventure = false;
					_backButton.Disabled = false;
					_startButton.Disabled = false;
					MouseFilter = MouseFilterEnum.Stop;
					RefreshSelection();
					return;
				}
			}
		}
		else
		{
			GetAdventureRunSessionStore()?.ConfigureForPolicy(GameModePolicy.AdventureTutorial);
		}

		await CircularSceneTransition.PlaySceneSwapAsync(this, () =>
		{
			Node gameBoard = gameBoardScene.Instantiate();
			gameBoard.Set("player_count", 2);
			gameBoard.Set("item_mode", true);
			gameBoard.Set("solo_mode", true);
			gameBoard.Set("adventure_mode", true);
			gameBoard.Set("adventure_enemy_avatar_id", selectedAvatarId);
			Node? currentScene = GetTree().CurrentScene;
			GetTree().Root.AddChild(gameBoard);
			GetTree().CurrentScene = gameBoard;
			if (currentScene != null && GodotObject.IsInstanceValid(currentScene) && currentScene != gameBoard)
			{
				currentScene.QueueFree();
			}

			QueueFree();
		});
	}

	private bool EnsureItemCardCatalogReady()
	{
		if (ItemCardCatalogPreflight.TryValidate(this, out _))
		{
			return true;
		}

		ItemCardCatalogPreflight.ShowErrorDialog(this);
		return false;
	}

	private void ReturnToMainMenu()
	{
		if (_isReturningToMainMenu)
		{
			return;
		}

		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_isReturningToMainMenu = true;
		_backButton.Disabled = true;
		_startButton.Disabled = true;
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 1000;

		MainMenuScene? embeddedMainMenu = GetEmbeddedMainMenuParent();
		if (embeddedMainMenu != null)
		{
			if (_isShowingAvatarUnlockPrompt)
			{
				HideAvatarUnlockPrompt(showNext: false);
			}

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
