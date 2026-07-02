using Godot;

public partial class AdventureAvatarMenuScene
{
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
		if (_runResumeDialogRoot != null && GodotObject.IsInstanceValid(_runResumeDialogRoot))
		{
			return;
		}

		var root = new Control
		{
			Name = "AdventureRunResumeDialog",
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			ZIndex = 100,
		};

		var blocker = new ColorRect
		{
			Color = new Color(0.04f, 0.08f, 0.12f, 0.78f),
			MouseFilter = MouseFilterEnum.Stop,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
		};
		root.AddChild(blocker);

		var panel = new PanelContainer
		{
			Name = "Panel",
			MouseFilter = MouseFilterEnum.Stop,
			CustomMinimumSize = new Vector2(520.0f, 0.0f),
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -260.0f,
			OffsetTop = -120.0f,
			OffsetRight = 260.0f,
			OffsetBottom = 120.0f,
		};
		root.AddChild(panel);

		var layout = new VBoxContainer
		{
			ThemeTypeVariation = "MarginContainer",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		panel.AddChild(layout);

		var title = new Label
		{
			Name = "TitleLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		layout.AddChild(title);

		var body = new Label
		{
			Name = "BodyLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		body.AddThemeFontSizeOverride("font_size", 18);
		layout.AddChild(body);

		var buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
		layout.AddChild(buttonRow);

		var continueButton = new Button
		{
			Name = "ContinueButton",
			CustomMinimumSize = new Vector2(140.0f, 0.0f),
			FocusMode = Control.FocusModeEnum.All,
		};
		buttonRow.AddChild(continueButton);

		var restartButton = new Button
		{
			Name = "RestartButton",
			CustomMinimumSize = new Vector2(140.0f, 0.0f),
			FocusMode = Control.FocusModeEnum.All,
		};
		buttonRow.AddChild(restartButton);

		var cancelButton = new Button
		{
			Name = "CancelButton",
			CustomMinimumSize = new Vector2(140.0f, 0.0f),
			FocusMode = Control.FocusModeEnum.All,
		};
		buttonRow.AddChild(cancelButton);

		AddChild(root);
		_runResumeDialogRoot = root;
		_runResumeTitleLabel = title;
		_runResumeBodyLabel = body;
		_runResumeContinueButton = continueButton;
		_runResumeRestartButton = restartButton;
		_runResumeCancelButton = cancelButton;

		SuppressDefaultPressedSfx(continueButton);
		SuppressDefaultPressedSfx(restartButton);
		SuppressDefaultPressedSfx(cancelButton);

		continueButton.Pressed += ContinueSavedAdventureRun;
		restartButton.Pressed += RestartAdventureFromCurrentSelection;
		cancelButton.Pressed += HideRunResumeDialog;
		RefreshRunResumeDialogText();
	}

	private void RefreshRunResumeDialogText()
	{
		if (_runResumeTitleLabel != null && GodotObject.IsInstanceValid(_runResumeTitleLabel))
		{
			_runResumeTitleLabel.Text = Localize("ADVENTURE_RESUME_RUN_TITLE");
		}

		if (_runResumeBodyLabel != null && GodotObject.IsInstanceValid(_runResumeBodyLabel))
		{
			_runResumeBodyLabel.Text = Localize("ADVENTURE_RESUME_RUN_BODY");
		}

		if (_runResumeContinueButton != null && GodotObject.IsInstanceValid(_runResumeContinueButton))
		{
			_runResumeContinueButton.Text = Localize("ADVENTURE_CONTINUE_RUN");
		}

		if (_runResumeRestartButton != null && GodotObject.IsInstanceValid(_runResumeRestartButton))
		{
			_runResumeRestartButton.Text = Localize("ADVENTURE_RESTART_RUN");
		}

		if (_runResumeCancelButton != null && GodotObject.IsInstanceValid(_runResumeCancelButton))
		{
			_runResumeCancelButton.Text = Localize("ADVENTURE_BACK");
		}
	}

	private void ShowRunResumeDialog()
	{
		if (_isShowingRunResumeDialog)
		{
			return;
		}

		EnsureRunResumeDialog();
		if (_runResumeDialogRoot == null)
		{
			return;
		}

		RefreshRunResumeDialogText();
		_isShowingRunResumeDialog = true;
		SetAdventureMenuButtonsDisabled(true);
		_runResumeDialogRoot.Visible = true;
		_runResumeDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);
		_runResumeContinueButton?.GrabFocus();
	}

	private void HideRunResumeDialog()
	{
		if (_runResumeDialogRoot == null || !GodotObject.IsInstanceValid(_runResumeDialogRoot))
		{
			return;
		}

		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_runResumeDialogRoot.Visible = false;
		_runResumeDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
		_isShowingRunResumeDialog = false;
		SetAdventureMenuButtonsDisabled(_isShowingAvatarUnlockPrompt);
		RefreshSelection();
		FocusDefaultControlDeferred();
	}

	private void ContinueSavedAdventureRun()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null || !store.TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot))
		{
			HideRunResumeDialog();
			return;
		}

		string savedAvatarId = CharacterAvatarCatalog.GetAdventureAvatarIdForStage(snapshot.CurrentAvatarStageNumber);
		if (string.IsNullOrWhiteSpace(savedAvatarId) || savedAvatarId == CharacterAvatarCatalog.DefaultAvatarId)
		{
			store.ClearRun();
			HideRunResumeDialog();
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
			Node gameBoard = gameBoardScene.Instantiate();
			gameBoard.Set("player_count", 2);
			gameBoard.Set("item_mode", true);
			gameBoard.Set("solo_mode", true);
			gameBoard.Set("adventure_mode", true);
			gameBoard.Set("adventure_enemy_avatar_id", savedAvatarId);
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

	private void RestartAdventureFromCurrentSelection()
	{
		GetAdventureRunSessionStore()?.ClearRun();
		HideRunResumeDialog();

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

	public bool DebugIsAdventureRunResumeDialogVisible()
	{
		return _isShowingRunResumeDialog
			&& _runResumeDialogRoot != null
			&& GodotObject.IsInstanceValid(_runResumeDialogRoot)
			&& _runResumeDialogRoot.Visible;
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

		RestartAdventureFromCurrentSelection();
		return true;
	}
}
