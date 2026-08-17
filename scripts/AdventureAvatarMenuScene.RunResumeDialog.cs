using Godot;
using System.Threading.Tasks;

public partial class AdventureAvatarMenuScene
{
	private const string AdventureRunResumeDialogScenePath = "res://scenes/adventure_run_resume_dialog.tscn";

	private AdventureRunSessionStore? GetAdventureRunSessionStore()
	{
		if (_adventureRunSessionStore != null && GodotObject.IsInstanceValid(_adventureRunSessionStore))
		{
			return _adventureRunSessionStore;
		}

		_adventureRunSessionStore = GetNodeOrNull<AdventureRunSessionStore>("/root/AdventureRunSessionStore");
		return _adventureRunSessionStore;
	}

	private void EnsureRunResumeDialog()
	{
		if (_runResumeDialog != null && GodotObject.IsInstanceValid(_runResumeDialog))
		{
			return;
		}

		PackedScene? dialogScene = GD.Load<PackedScene>(AdventureRunResumeDialogScenePath);
		if (dialogScene?.Instantiate() is not AdventureRunResumeDialog dialog)
		{
			GD.PushError($"Missing adventure run resume dialog scene: {AdventureRunResumeDialogScenePath}");
			return;
		}

		AddChild(dialog);
		dialog.ContinueRequested += ContinueSavedAdventureRun;
		dialog.RestartRequested += RestartAdventureFromFirstStage;
		dialog.CancelRequested += HideRunResumeDialog;
		_runResumeDialog = dialog;
		RefreshRunResumeDialogText();
	}

	private void RefreshRunResumeDialogText()
	{
		if (_runResumeDialog == null || !GodotObject.IsInstanceValid(_runResumeDialog))
		{
			return;
		}

		bool recoveryMode = _runResumeDialogMode == RunResumeDialogMode.Recovery;
		_runResumeDialog.Configure(
			recoveryMode,
			Localize(recoveryMode
				? "ADVENTURE_RECOVERY_TITLE"
				: "ADVENTURE_RESUME_RUN_TITLE"),
			Localize("ADVENTURE_CONTINUE_RUN"),
			Localize(recoveryMode
				? "ADVENTURE_RECOVERY_RESTART"
				: "ADVENTURE_RESTART_RUN"),
			Localize("ADVENTURE_BACK"));
	}

	private void ShowRunResumeDialog()
	{
		if (_isShowingRunResumeDialog)
		{
			return;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		_runResumeDialogMode = store?.RequiresRunRecovery == true
			? RunResumeDialogMode.Recovery
			: RunResumeDialogMode.Resume;
		_runRecoverySaveFailed = false;
		EnsureRunResumeDialog();
		if (_runResumeDialog == null || !GodotObject.IsInstanceValid(_runResumeDialog))
		{
			return;
		}

		RefreshRunResumeDialogText();
		_isShowingRunResumeDialog = true;
		SetAdventureMenuButtonsDisabled(true);
		_runResumeDialog.ShowDialog(focusDefault: !_isShowingAvatarUnlockPrompt);
		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);
		if (_isShowingAvatarUnlockPrompt)
		{
			GrabFocusIfAvailable(_avatarUnlockPromptButton);
		}
	}

	private void HideRunResumeDialog()
	{
		if (_isAdventureRunPersistenceInProgress
			|| _runResumeDialog == null
			|| !GodotObject.IsInstanceValid(_runResumeDialog))
		{
			return;
		}

		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_runResumeDialog.HideDialog();
		_isShowingRunResumeDialog = false;
		RefreshSelection();
		bool disableBackground =
			_isShowingAvatarUnlockPrompt
			|| _isStartingAdventure
			|| _isReturningToMainMenu;
		SetAdventureMenuButtonsDisabled(disableBackground);
		if (_isShowingAvatarUnlockPrompt)
		{
			GrabFocusIfAvailable(_avatarUnlockPromptButton);
			return;
		}

		if (!_isStartingAdventure && !_isReturningToMainMenu)
		{
			FocusDefaultControlDeferred();
		}
	}

	private void ContinueSavedAdventureRun()
	{
		if (_isAdventureRunPersistenceInProgress || !EnsureItemCardCatalogReady())
		{
			return;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		store?.ConfigureForPolicy(GameModePolicy.AdventureMain);
		if (store == null || !store.TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot))
		{
			HideRunResumeDialog();
			return;
		}

		string savedAvatarId = snapshot.EnemyAvatarId;
		if (!AdventureCampaignCatalog.TryGetMainStageIndex(savedAvatarId, out int mainStageIndex)
			|| mainStageIndex != snapshot.MainStageIndex)
		{
			_ = ClearInvalidSavedAdventureRunAsync(store);
			return;
		}

		string gameBoardScenePath = GetGameBoardScenePathForAvatar(savedAvatarId);
		PackedScene? gameBoardScene = GD.Load<PackedScene>(gameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {gameBoardScenePath}");
			HideRunResumeDialog();
			return;
		}

		HideRunResumeDialog();
		_isStartingAdventure = true;
		_backButton.Disabled = true;
		_startButton.Disabled = true;
		MouseFilter = MouseFilterEnum.Ignore;
		_ = CircularSceneTransition.PlaySceneSwapAsync(this, () =>
		{
			GameBoardLaunchOptions options = GameBoardLaunchOptions.AdventureMain(
				savedAvatarId,
				PlayerNameRules.Normalize(_profileSnapshot.PlayerName));
			if (!GameBoardSceneFactory.TryCreate(gameBoardScene, options, out GameBoardScene gameBoard, out string error))
			{
				GD.PushError($"Unable to create resumed adventure game board: {error}");
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

	private async Task ClearInvalidSavedAdventureRunAsync(AdventureRunSessionStore store)
	{
		if (!TryBeginAdventureRunPersistence())
		{
			return;
		}

		AdventureRunWriteResult result = await PersistPreparedAdventureOperationWithRetryAsync(
			store,
			store.PrepareClearRun());
		EndAdventureRunPersistence();
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}
		if (result == AdventureRunWriteResult.Succeeded)
		{
			HideRunResumeDialog();
			return;
		}

		_runRecoverySaveFailed = true;
		RefreshRunResumeDialogText();
	}

	private void RestartAdventureFromFirstStage()
	{
		if (_isAdventureRunPersistenceInProgress || !EnsureItemCardCatalogReady())
		{
			return;
		}
		_ = RestartAdventureFromFirstStageAsync();
	}

	private async Task RestartAdventureFromFirstStageAsync()
	{
		string selectedAvatarId = AdventureCampaignCatalog.GetMainAvatarId(1);
		string gameBoardScenePath = GetGameBoardScenePathForAvatar(selectedAvatarId);
		PackedScene? gameBoardScene = GD.Load<PackedScene>(gameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {gameBoardScenePath}");
			return;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		store?.ConfigureForPolicy(GameModePolicy.AdventureMain);
		if (store == null || !TryBeginAdventureRunPersistence())
		{
			_runRecoverySaveFailed = true;
			RefreshRunResumeDialogText();
			return;
		}

		AdventureRunWriteResult result = await PersistPreparedAdventureOperationWithRetryAsync(
			store,
			store.PrepareRestartMainRun(1, selectedAvatarId));
		EndAdventureRunPersistence();
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}
		if (result != AdventureRunWriteResult.Succeeded)
		{
			_runRecoverySaveFailed = true;
			RefreshRunResumeDialogText();
			return;
		}

		HideRunResumeDialog();
		_isStartingAdventure = true;
		_backButton.Disabled = true;
		_startButton.Disabled = true;
		MouseFilter = MouseFilterEnum.Ignore;
		_ = StartAdventureWithTransitionAsync(gameBoardScene, selectedAvatarId, beginNewRun: false);
	}

	private bool TryBeginAdventureRunPersistence()
	{
		if (_isAdventureRunPersistenceInProgress)
		{
			return false;
		}

		_isAdventureRunPersistenceInProgress = true;
		_runResumeDialog?.SetActionsDisabled(true);
		return true;
	}

	private void EndAdventureRunPersistence()
	{
		_isAdventureRunPersistenceInProgress = false;
		if (_runResumeDialog != null && GodotObject.IsInstanceValid(_runResumeDialog))
		{
			_runResumeDialog.SetActionsDisabled(false);
		}
	}

#if DECKLAND_TEST_HOOKS
	public bool DebugIsAdventureRunResumeDialogVisible()
	{
		return _isShowingRunResumeDialog
			&& _runResumeDialog != null
			&& GodotObject.IsInstanceValid(_runResumeDialog)
			&& _runResumeDialog.IsDialogVisible;
	}

	public bool DebugShowAdventureRunResumeDialog()
	{
		ShowRunResumeDialog();
		return DebugIsAdventureRunResumeDialogVisible();
	}

	public bool DebugAreAdventureMenuBackgroundButtonsDisabled()
	{
		if (!_backButton.Disabled || !_startButton.Disabled)
		{
			return false;
		}

		foreach (Button adventureButton in _adventureButtons.Values)
		{
			if (GodotObject.IsInstanceValid(adventureButton)
				&& !adventureButton.Disabled)
			{
				return false;
			}
		}

		foreach (Button stageButton in _stageButtons.Values)
		{
			if (GodotObject.IsInstanceValid(stageButton)
				&& !stageButton.Disabled)
			{
				return false;
			}
		}

		return true;
	}

	public bool DebugSelectAdventureRunResumeContinue()
	{
		if (!DebugIsAdventureRunResumeDialogVisible())
		{
			return false;
		}

		ContinueSavedAdventureRun();
		return true;
	}

	public bool DebugSelectAdventureRunResumeRestart()
	{
		if (!DebugIsAdventureRunResumeDialogVisible())
		{
			return false;
		}

		RestartAdventureFromFirstStage();
		return true;
	}
#endif
}
