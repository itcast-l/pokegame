using System;
using System.Threading.Tasks;
using Godot;

public partial class AdventureAvatarMenuScene
{
	private static readonly int[] AdventurePersistenceRetryDelayMilliseconds =
	{
		500,
		1000,
		2000,
	};

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
		RefreshAdventureProfileSnapshot();
		_ = keyboardOrJoypad;
		_isReturningToMainMenu = false;
		_backButton.Disabled = false;
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 0;
		Modulate = Colors.White;
		Scale = Vector2.One;
		_startButton.Disabled = false;
		RefreshSelection();
		ShowPendingAdventureAvatarUnlockPrompt();
	}

	public void RequestApplicationQuit()
	{
		ApplicationShutdownService.Request(this, () =>
		{
			_isReturningToMainMenu = true;
			_isStartingAdventure = true;
			_backButton.Disabled = true;
			_startButton.Disabled = true;
			MouseFilter = MouseFilterEnum.Ignore;
			SetProcessInput(false);
			SetProcessUnhandledInput(false);
		});
	}

	private void RefreshAdventureProfileSnapshot()
	{
		LoadAdventureProfileSnapshot();
		ConfigureAdventureRunStoreFromProfileSnapshot();
	}

	private void LoadAdventureProfileSnapshot()
	{
		_profileSnapshot = GameSaveStore.LoadActiveProfile();
	}

#if DECKLAND_TEST_HOOKS
	public string DebugGetProfilePlayerName()
	{
		return PlayerNameRules.Normalize(_profileSnapshot.PlayerName);
	}
#endif

	private void ConfigureAdventureRunStoreFromProfileSnapshot()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			return;
		}

		store.ConfigureForPolicy(
			_selectedAdventureId == AdventureCampaignCatalog.MainCampaignId
				? GameModePolicy.AdventureMain
				: GameModePolicy.AdventureTutorial);
		store.ReloadFromProfileSnapshot(_profileSnapshot);
	}

	private void SyncAdventureProfileSnapshot(AdventureRunSessionStore store)
	{
		if (store.TryGetActiveProfileSnapshot(out GameSaveStore.GameProgressData profile))
		{
			_profileSnapshot = profile;
		}
	}

	private async Task<AdventureRunWriteResult> PersistPreparedAdventureOperationWithRetryAsync(
		AdventureRunSessionStore store,
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> operation)
	{
		if (!operation.IsPrepared)
		{
			return operation.InitialResult;
		}

		AdventureRunWriteResult result = AdventureRunWriteResult.SaveFailed;
		for (int attemptIndex = 0;
			attemptIndex <= AdventurePersistenceRetryDelayMilliseconds.Length;
			attemptIndex++)
		{
			try
			{
				result = await operation.PersistAttemptAsync();
			}
			catch (System.Exception error)
			{
				GD.PushWarning($"Adventure menu persistence attempt failed: {error.Message}");
				operation.Abort();
				return AdventureRunWriteResult.SaveFailed;
			}

			if (result == AdventureRunWriteResult.Succeeded)
			{
				SyncAdventureProfileSnapshot(store);
				return result;
			}
			if (result != AdventureRunWriteResult.SaveFailed)
			{
				operation.Abort();
				return result;
			}
			if (attemptIndex < AdventurePersistenceRetryDelayMilliseconds.Length)
			{
				await Task.Delay(AdventurePersistenceRetryDelayMilliseconds[attemptIndex]);
			}
		}

		operation.Abort();
		return result;
	}

	private void StartAdventure()
	{
		if (_isConsumingPendingAvatarUnlock
			|| _isStartingAdventure
			|| _isReturningToMainMenu
			|| _isShowingRunResumeDialog)
		{
			return;
		}
		if (!EnsureItemCardCatalogReady())
		{
			return;
		}

		GameModePolicy selectedPolicy = _selectedAdventureId == AdventureCampaignCatalog.TutorialCampaignId
			? GameModePolicy.AdventureTutorial
			: GameModePolicy.AdventureMain;
		AdventureRunSessionStore? runSessionStore = GetAdventureRunSessionStore();
		runSessionStore?.ConfigureForPolicy(selectedPolicy);
		runSessionStore?.ReloadFromProfileSnapshot(_profileSnapshot);
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
			AdventureRunSessionStore? store = GetAdventureRunSessionStore();
			store?.ConfigureForPolicy(GameModePolicy.AdventureMain);
			if (beginNewRun)
			{
				AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> operation =
					store?.PrepareBeginNewMainRun(mainStageIndex, selectedAvatarId)
					?? AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult>.Rejected(
						AdventureRunWriteResult.SaveFailed,
						static result => result);
				AdventureRunWriteResult writeResult = store == null
					? AdventureRunWriteResult.SaveFailed
					: await PersistPreparedAdventureOperationWithRetryAsync(store, operation);
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
			GameBoardLaunchOptions options;
			try
			{
				if (AdventureCampaignCatalog.IsMainAvatar(selectedAvatarId))
				{
					options = GameBoardLaunchOptions.AdventureMain(
						selectedAvatarId,
						PlayerNameRules.Normalize(_profileSnapshot.PlayerName));
				}
				else if (AdventureTutorialFlowRules.TryResolveForAvatar(
					selectedAvatarId,
					out AdventureTutorialFlowKind tutorialFlow,
					out string flowError))
				{
					options = GameBoardLaunchOptions.AdventureTutorial(
						selectedAvatarId,
						tutorialFlow,
						PlayerNameRules.Normalize(_profileSnapshot.PlayerName));
				}
				else
				{
					GD.PushError(flowError);
					return;
				}
			}
			catch (Exception error)
			{
				GD.PushError($"Unable to configure adventure game board: {error.Message}");
				return;
			}

			if (!GameBoardSceneFactory.TryCreate(gameBoardScene, options, out GameBoardScene gameBoard, out string createError))
			{
				GD.PushError($"Unable to create adventure game board: {createError}");
				return;
			}
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
		MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Disabled;

		PackedScene? mainMenuScene = GD.Load<PackedScene>(MainMenuScenePath);
		if (mainMenuScene == null)
		{
			GD.PushError($"Missing main menu scene: {MainMenuScenePath}");
			_isReturningToMainMenu = false;
			_backButton.Disabled = false;
			_startButton.Disabled = false;
			MouseFilter = MouseFilterEnum.Stop;
			MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Inherited;
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
			MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Inherited;
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
