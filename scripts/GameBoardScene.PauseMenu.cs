using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";
	private const string AdventureAvatarMenuScenePath = "res://scenes/adventure_avatar_menu_scene.tscn";
	private const string PauseMenuFrameTexturePath = "res://art/ui/pause_menu/run-001/processed/clean.png";
	private const float PauseMenuWidth = 560.0f;
	private const float PauseMenuHeight = 430.0f;

	private CanvasLayer? _pauseMenuLayer;
	private PauseMenuInputCatcher? _pauseMenuRoot;
	private Control? _pauseMenuPanel;
	private Button? _pauseContinueButton;
	private Button? _pauseQuitButton;
	private Control? _pauseQuitConfirmRoot;
	private Button? _pauseQuitCancelButton;
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

		_pauseMenuLayer = new CanvasLayer
		{
			Name = "PauseMenuLayer",
			Layer = 60,
			ProcessMode = ProcessModeEnum.Always,
		};
		AddChild(_pauseMenuLayer);

		_pauseMenuRoot = new PauseMenuInputCatcher
		{
			Name = "PauseMenuRoot",
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always,
			CancelPressed = HandlePauseMenuCancelPressed,
		};
		_pauseMenuRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		_pauseMenuLayer.AddChild(_pauseMenuRoot);

		var scrim = new ColorRect
		{
			Name = "Scrim",
			Color = new Color(0.0f, 0.025f, 0.05f, 0.58f),
			MouseFilter = MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always,
		};
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		_pauseMenuRoot.AddChild(scrim);

		var center = new CenterContainer
		{
			Name = "PauseMenuCenter",
			MouseFilter = MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Always,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		_pauseMenuRoot.AddChild(center);

		_pauseMenuPanel = BuildPauseMenuPanel();
		center.AddChild(_pauseMenuPanel);

		_pauseQuitConfirmRoot = BuildPauseQuitConfirmDialog();
		_pauseMenuRoot.AddChild(_pauseQuitConfirmRoot);
	}

	private Control BuildPauseMenuPanel()
	{
		var panel = new Control
		{
			Name = "PauseMenuPanel",
			CustomMinimumSize = new Vector2(PauseMenuWidth, PauseMenuHeight),
			MouseFilter = MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always,
		};

		Texture2D? frameTexture = GD.Load<Texture2D>(PauseMenuFrameTexturePath);
		if (frameTexture != null)
		{
			var frameArt = new TextureRect
			{
				Name = "GeneratedFrameArt",
				Texture = frameTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				MouseFilter = MouseFilterEnum.Ignore,
				ProcessMode = ProcessModeEnum.Always,
			};
			frameArt.SetAnchorsPreset(LayoutPreset.FullRect);
			frameArt.OffsetLeft = -18.0f;
			frameArt.OffsetTop = -36.0f;
			frameArt.OffsetRight = 18.0f;
			frameArt.OffsetBottom = 36.0f;
			panel.AddChild(frameArt);
		}
		else
		{
			var fallbackPanel = new Panel
			{
				Name = "FallbackPanel",
				MouseFilter = MouseFilterEnum.Ignore,
				ProcessMode = ProcessModeEnum.Always,
			};
			fallbackPanel.SetAnchorsPreset(LayoutPreset.FullRect);
			fallbackPanel.AddThemeStyleboxOverride("panel", CreatePausePanelStyle());
			panel.AddChild(fallbackPanel);
			GD.PushWarning($"Missing pause menu frame texture: {PauseMenuFrameTexturePath}");
		}

		var margin = new MarginContainer
		{
			Name = "PauseMenuMargin",
			MouseFilter = MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Always,
		};
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 82);
		margin.AddThemeConstantOverride("margin_top", 66);
		margin.AddThemeConstantOverride("margin_right", 82);
		margin.AddThemeConstantOverride("margin_bottom", 58);
		panel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "PauseMenuLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Always,
		};
		layout.AddThemeConstantOverride("separation", 14);
		margin.AddChild(layout);

		Control continueFrame = CreatePauseButton("ContinueGame", "GAME_CONTINUE", new Color(0.58f, 1.0f, 0.92f), HidePauseMenu, false);
		Control mainMenuFrame = CreatePauseButton("ReturnToMainMenu", "GAME_RETURN_MAIN_MENU", new Color(0.78f, 0.9f, 1.0f), ReturnToMainMenu, false);
		Control quitFrame = CreatePauseButton("QuitGame", "GAME_QUIT", new Color(0.95f, 0.68f, 0.56f), QuitGameFromPauseMenu, false);
		layout.AddChild(continueFrame);
		layout.AddChild(mainMenuFrame);
		layout.AddChild(quitFrame);

		return panel;
	}

	private Label CreatePauseLabel(string text, int fontSize, Color color, HorizontalAlignment alignment)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = alignment,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Always,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		return label;
	}

	private Control BuildPauseQuitConfirmDialog()
	{
		var root = new Control
		{
			Name = "PauseQuitConfirmRoot",
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always,
		};
		root.SetAnchorsPreset(LayoutPreset.FullRect);

		var scrim = new ColorRect
		{
			Name = "ConfirmScrim",
			Color = new Color(0.0f, 0.01f, 0.02f, 0.48f),
			MouseFilter = MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always,
		};
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		scrim.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				HidePauseQuitConfirm();
				GetViewport().SetInputAsHandled();
			}
		};
		root.AddChild(scrim);

		var center = new CenterContainer
		{
			Name = "ConfirmCenter",
			MouseFilter = MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Always,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		root.AddChild(center);

		var panel = new PanelContainer
		{
			Name = "PauseQuitConfirmPanel",
			CustomMinimumSize = new Vector2(520.0f, 286.0f),
			MouseFilter = MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always,
		};
		panel.AddThemeStyleboxOverride("panel", CreatePausePanelStyle());
		center.AddChild(panel);

		var margin = new MarginContainer
		{
			Name = "ConfirmMargin",
			ProcessMode = ProcessModeEnum.Always,
		};
		margin.AddThemeConstantOverride("margin_left", 34);
		margin.AddThemeConstantOverride("margin_top", 28);
		margin.AddThemeConstantOverride("margin_right", 34);
		margin.AddThemeConstantOverride("margin_bottom", 28);
		panel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "ConfirmLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Always,
		};
		layout.AddThemeConstantOverride("separation", 12);
		margin.AddChild(layout);

		Label title = CreatePauseLabel(Localize("GAME_QUIT_CONFIRM_TITLE"), 29, new Color(0.9f, 1.0f, 0.96f), HorizontalAlignment.Center);
		layout.AddChild(title);

		Label message = CreatePauseLabel(Localize("GAME_QUIT_CONFIRM_MESSAGE"), 20, new Color(0.66f, 0.96f, 0.92f, 0.92f), HorizontalAlignment.Center);
		message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		message.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		layout.AddChild(message);

		Label hint = CreatePauseLabel(Localize("GAME_QUIT_CONFIRM_HINT"), 16, new Color(0.95f, 0.84f, 0.58f, 0.82f), HorizontalAlignment.Center);
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		layout.AddChild(hint);

		var spacer = new Control
		{
			CustomMinimumSize = new Vector2(0.0f, 8.0f),
			ProcessMode = ProcessModeEnum.Always,
		};
		layout.AddChild(spacer);

		var actions = new HBoxContainer
		{
			Name = "ConfirmActions",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Always,
		};
		actions.AddThemeConstantOverride("separation", 16);
		layout.AddChild(actions);

		Control cancelFrame = CreatePauseButton("CancelQuitGame", "GAME_QUIT_CONFIRM_CANCEL", new Color(0.58f, 1.0f, 0.92f), HidePauseQuitConfirm, false, 206.0f);
		Control confirmFrame = CreatePauseButton("ConfirmQuitGame", "GAME_QUIT_CONFIRM_ACCEPT", new Color(0.95f, 0.68f, 0.56f), ConfirmQuitGameFromPauseMenu, true, 206.0f);
		actions.AddChild(cancelFrame);
		actions.AddChild(confirmFrame);

		_pauseQuitCancelButton = cancelFrame.GetNodeOrNull<Button>("Button");
		return root;
	}

	private Control CreatePauseButton(string nodeName, string textKey, Color accentColor, Action pressedAction, bool flipOrnament, float width = 288.0f)
	{
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{nodeName}Frame",
			CustomMinimumSize = new Vector2(width, 62.0f),
			AccentColor = accentColor,
			FlipOrnament = flipOrnament,
			ProcessMode = ProcessModeEnum.Always,
		};

		var button = new Button
		{
			Name = "Button",
			Text = Localize(textKey),
			Flat = true,
			FocusMode = FocusModeEnum.All,
			MouseFilter = MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always,
		};
		button.SetAnchorsPreset(LayoutPreset.FullRect);
		button.OffsetLeft = flipOrnament ? 26.0f : 4.0f;
		button.OffsetTop = 5.0f;
		button.OffsetRight = flipOrnament ? -4.0f : -26.0f;
		button.OffsetBottom = -5.0f;
		button.AddThemeFontSizeOverride("font_size", 20);
		button.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.78f));
		button.AddThemeColorOverride("font_pressed_color", new Color(0.78f, 1.0f, 0.96f));
		button.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		button.AddThemeConstantOverride("shadow_offset_x", 1);
		button.AddThemeConstantOverride("shadow_offset_y", 2);
		StyleBoxEmpty emptyStyle = new();
		button.AddThemeStyleboxOverride("normal", emptyStyle);
		button.AddThemeStyleboxOverride("hover", emptyStyle);
		button.AddThemeStyleboxOverride("pressed", emptyStyle);
		button.AddThemeStyleboxOverride("focus", emptyStyle);
		button.Pressed += pressedAction;

		if (_pauseContinueButton == null)
		{
			_pauseContinueButton = button;
		}

		if (nodeName == "QuitGame")
		{
			_pauseQuitButton = button;
		}

		TrackLocalizedGameButton(button, textKey);
		frame.AddChild(button);
		return frame;
	}

	private static StyleBoxFlat CreatePausePanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.012f, 0.14f, 0.18f, 0.82f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.58f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.66f),
			ShadowSize = 24,
			ShadowOffset = new Vector2(0.0f, 10.0f),
		};
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
		if (_pauseQuitConfirmRoot == null)
		{
			return;
		}

		_pauseQuitConfirmRoot.Visible = true;
		_pauseQuitCancelButton?.GrabFocus();
	}

	private void HidePauseQuitConfirm()
	{
		if (_pauseQuitConfirmRoot == null || !_pauseQuitConfirmRoot.Visible)
		{
			return;
		}

		_pauseQuitConfirmRoot.Visible = false;
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
