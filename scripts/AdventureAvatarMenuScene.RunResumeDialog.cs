using Godot;

public partial class AdventureAvatarMenuScene
{
	private const string MenuBlurScrimShaderPath = "res://shaders/menu_blur_scrim.gdshader";
	private const float RunResumeDialogPanelWidth = 744.0f;
	private const float RunResumeDialogPanelHeight = 480.0f;
	private const float RunResumeDialogContentMarginLeft = 84.0f;
	private const float RunResumeDialogContentMarginTop = 74.0f;
	private const float RunResumeDialogContentMarginRight = 84.0f;
	private const float RunResumeDialogContentMarginBottom = 62.0f;
	private const float RunResumeDialogButtonHeight = 48.0f;
	private const float RunResumeDialogButtonMinWidth = 208.0f;
	private const float RunResumeDialogButtonHorizontalPadding = 76.0f;
	private const float RunResumeDialogButtonRowWidth = RunResumeDialogPanelWidth - RunResumeDialogContentMarginLeft - RunResumeDialogContentMarginRight;
	private const int RunResumeDialogTitleFontSize = 34;
	private const int RunResumeDialogTitleMinFontSize = 26;
	private const int RunResumeDialogBodyFontSize = 18;
	private const int RunResumeDialogBodyMinFontSize = 16;
	private const int RunResumeDialogButtonFontSize = 20;
	private const int RunResumeDialogButtonMinFontSize = 16;

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

		Control root = new()
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

		ColorRect scrim = new()
		{
			Name = "Scrim",
			Color = new Color(0.0f, 0.035f, 0.07f, 0.62f),
			MouseFilter = MouseFilterEnum.Stop,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
		};
		scrim.Material = BuildRunResumeDialogScrimMaterial();
		root.AddChild(scrim);

		Control dialogMotion = new()
		{
			Name = "DialogMotion",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		root.AddChild(dialogMotion);

		Control panel = new()
		{
			Name = "Panel",
			MouseFilter = MouseFilterEnum.Stop,
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -RunResumeDialogPanelWidth * 0.5f,
			OffsetTop = -RunResumeDialogPanelHeight * 0.5f,
			OffsetRight = RunResumeDialogPanelWidth * 0.5f,
			OffsetBottom = RunResumeDialogPanelHeight * 0.5f,
		};
		dialogMotion.AddChild(panel);

		OceanDialogPanelArt frameArt = new()
		{
			Name = "FrameArt",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			MouseFilter = MouseFilterEnum.Ignore,
			PanelTexturePath = "res://assets/ui/player_count_dialog_blank_panel.png",
			CoverBuiltInActionWell = false,
			CoverBuiltInRowSlots = false,
			InnerTopRatio = 0.24f,
			InnerBottomRatio = 0.13f,
			InnerSideRatio = 0.10f,
		};
		panel.AddChild(frameArt);

		Control contentBounds = new()
		{
			Name = "ContentMargin",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = RunResumeDialogContentMarginLeft,
			OffsetTop = RunResumeDialogContentMarginTop,
			OffsetRight = -RunResumeDialogContentMarginRight,
			OffsetBottom = -RunResumeDialogContentMarginBottom,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panel.AddChild(contentBounds);

		VBoxContainer layout = new()
		{
			Name = "Layout",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		layout.AddThemeConstantOverride("separation", 18);
		contentBounds.AddChild(layout);

		Label title = new()
		{
			Name = "TitleLabel",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		title.AddThemeColorOverride("font_color", new Color(0.92f, 0.96f, 0.90f, 1.0f));
		title.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.92f));
		title.AddThemeConstantOverride("shadow_offset_x", 1);
		title.AddThemeConstantOverride("shadow_offset_y", 2);
		title.AddThemeFontSizeOverride("font_size", RunResumeDialogTitleFontSize);
		layout.AddChild(title);

		ColorRect divider = new()
		{
			Name = "Divider",
			CustomMinimumSize = new Vector2(0.0f, 2.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Color = new Color(0.22f, 0.82f, 0.90f, 0.26f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddChild(divider);

		Label body = new()
		{
			Name = "BodyLabel",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		body.AddThemeColorOverride("font_color", new Color(0.74f, 0.96f, 0.98f, 0.96f));
		body.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.82f));
		body.AddThemeConstantOverride("shadow_offset_x", 1);
		body.AddThemeConstantOverride("shadow_offset_y", 2);
		body.AddThemeFontSizeOverride("font_size", RunResumeDialogBodyFontSize);
		layout.AddChild(body);

		Control spacer = new()
		{
			Name = "ButtonSpacer",
			CustomMinimumSize = new Vector2(0.0f, 8.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddChild(spacer);

		HFlowContainer buttonRow = new()
		{
			Name = "ButtonRow",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Alignment = FlowContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		buttonRow.AddThemeConstantOverride("h_separation", 18);
		buttonRow.AddThemeConstantOverride("v_separation", 12);
		layout.AddChild(buttonRow);

		OceanMenuButtonFrame continueFrame = CreateRunResumeActionFrame(
			"ContinueFrame",
			new Color(0.62f, 1.0f, 0.96f, 1.0f),
			true,
			out Button continueButton);
		OceanMenuButtonFrame restartFrame = CreateRunResumeActionFrame(
			"RestartFrame",
			new Color(0.58f, 0.84f, 0.86f, 0.92f),
			false,
			out Button restartButton);
		OceanMenuButtonFrame cancelFrame = CreateRunResumeActionFrame(
			"CancelFrame",
			new Color(0.34f, 0.54f, 0.58f, 0.70f),
			false,
			out Button cancelButton);
		buttonRow.AddChild(continueFrame);
		buttonRow.AddChild(restartFrame);
		buttonRow.AddChild(cancelFrame);

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

	private static ShaderMaterial? BuildRunResumeDialogScrimMaterial()
	{
		Shader? shader = GD.Load<Shader>(MenuBlurScrimShaderPath);
		if (shader == null)
		{
			return null;
		}

		ShaderMaterial material = new()
		{
			Shader = shader,
		};
		material.SetShaderParameter("blur_lod", 2.8f);
		material.SetShaderParameter("tint_color", new Color(0.0f, 0.035f, 0.07f, 0.48f));
		return material;
	}

	private static OceanMenuButtonFrame CreateRunResumeActionFrame(string frameName, Color accentColor, bool selected, out Button button)
	{
		OceanMenuButtonFrame frame = new()
		{
			Name = frameName,
			AccentColor = accentColor,
			Selected = selected,
			CustomMinimumSize = new Vector2(RunResumeDialogButtonMinWidth, RunResumeDialogButtonHeight),
			MouseFilter = MouseFilterEnum.Ignore,
		};

		button = new Button
		{
			Name = frameName.Replace("Frame", "Button"),
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			FocusMode = Control.FocusModeEnum.All,
			CustomMinimumSize = new Vector2(RunResumeDialogButtonMinWidth, RunResumeDialogButtonHeight),
		};
		MainMenuInteractiveStyle.ApplyActionButton(button, RunResumeDialogButtonFontSize);
		frame.AddChild(button);
		return frame;
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

		RefreshRunResumeDialogLayout();
	}

	private void RefreshRunResumeDialogLayout()
	{
		ApplyRunResumeLabelSizing(_runResumeTitleLabel, RunResumeDialogTitleFontSize, RunResumeDialogTitleMinFontSize, 2.4f);
		ApplyRunResumeLabelSizing(_runResumeBodyLabel, RunResumeDialogBodyFontSize, RunResumeDialogBodyMinFontSize, 3.8f);
		ApplyRunResumeButtonSizing();
	}

	private static void ApplyRunResumeLabelSizing(Label? label, int maxFontSize, int minFontSize, float estimatedLineLimit)
	{
		if (label == null || !GodotObject.IsInstanceValid(label))
		{
			return;
		}

		string text = label.Text?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(text))
		{
			label.AddThemeFontSizeOverride("font_size", maxFontSize);
			return;
		}

		Font font = label.GetThemeFont("font");
		int resolvedSize = minFontSize;
		for (int fontSize = maxFontSize; fontSize >= minFontSize; fontSize--)
		{
			float singleLineWidth = font.GetStringSize(text, label.HorizontalAlignment, -1.0f, fontSize).X;
			if (singleLineWidth <= RunResumeDialogButtonRowWidth * estimatedLineLimit)
			{
				resolvedSize = fontSize;
				break;
			}
		}

		label.AddThemeFontSizeOverride("font_size", resolvedSize);
	}

	private void ApplyRunResumeButtonSizing()
	{
		Button?[] buttons =
		{
			_runResumeContinueButton,
			_runResumeRestartButton,
			_runResumeCancelButton,
		};
		Color[] baseColors =
		{
			new Color(0.96f, 1.0f, 0.98f, 1.0f),
			new Color(0.78f, 0.96f, 0.96f, 0.98f),
			new Color(0.84f, 0.90f, 0.92f, 0.92f),
		};
		Color[] hoverColors =
		{
			new Color(1.0f, 0.98f, 0.84f, 1.0f),
			new Color(0.96f, 1.0f, 0.90f, 1.0f),
			new Color(0.96f, 1.0f, 0.94f, 1.0f),
		};
		bool[] primaryFlags = { true, false, false };

		int widestTextWidth = 0;
		int resolvedFontSize = RunResumeDialogButtonFontSize;
		foreach (Button? candidate in buttons)
		{
			if (candidate == null || !GodotObject.IsInstanceValid(candidate))
			{
				return;
			}

			int candidateFontSize = ResolveRunResumeButtonFontSize(candidate.Text ?? string.Empty, candidate);
			resolvedFontSize = Mathf.Min(resolvedFontSize, candidateFontSize);
		}

		foreach (Button? candidate in buttons)
		{
			if (candidate == null || !GodotObject.IsInstanceValid(candidate))
			{
				return;
			}

			int textWidth = Mathf.CeilToInt(MeasureButtonTextWidth(candidate, candidate.Text ?? string.Empty, resolvedFontSize));
			widestTextWidth = Mathf.Max(widestTextWidth, textWidth);
		}

		float preferredWidth = Mathf.Clamp(
			widestTextWidth + RunResumeDialogButtonHorizontalPadding,
			RunResumeDialogButtonMinWidth,
			RunResumeDialogButtonRowWidth);
		Vector2 minimumSize = new(preferredWidth, RunResumeDialogButtonHeight);

		for (int index = 0; index < buttons.Length; index++)
		{
			Button button = buttons[index]!;
			Color baseFontColor = baseColors[index];
			Color hoverFontColor = hoverColors[index];
			bool primary = primaryFlags[index];

			button.AddThemeFontSizeOverride("font_size", resolvedFontSize);
			button.AddThemeColorOverride("font_color", baseFontColor);
			button.AddThemeColorOverride("font_pressed_color", baseFontColor.Lerp(Colors.White, primary ? 0.10f : 0.05f));
			button.AddThemeColorOverride("font_hover_color", hoverFontColor);
			button.AddThemeColorOverride("font_focus_color", hoverFontColor);
			button.CustomMinimumSize = minimumSize;
			if (button.GetParent() is Control frame)
			{
				frame.CustomMinimumSize = minimumSize;
			}
		}
	}

	private static int ResolveRunResumeButtonFontSize(string text, Button button)
	{
		for (int fontSize = RunResumeDialogButtonFontSize; fontSize >= RunResumeDialogButtonMinFontSize; fontSize--)
		{
			if (MeasureButtonTextWidth(button, text, fontSize) + RunResumeDialogButtonHorizontalPadding <= RunResumeDialogButtonRowWidth)
			{
				return fontSize;
			}
		}

		return RunResumeDialogButtonMinFontSize;
	}

	private static float MeasureButtonTextWidth(Button button, string text, int fontSize)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0.0f;
		}

		Font font = button.GetThemeFont("font");
		return font.GetStringSize(text, HorizontalAlignment.Center, -1.0f, fontSize).X;
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
		CallDeferred(nameof(RefreshRunResumeDialogLayout));
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
