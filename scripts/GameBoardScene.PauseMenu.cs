using System;
using Godot;

public partial class GameBoardScene
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";
	private const string PauseMenuFrameTexturePath = "res://art/ui/pause_menu/run-001/processed/clean.png";
	private const float PauseMenuWidth = 560.0f;
	private const float PauseMenuHeight = 430.0f;

	private CanvasLayer? _pauseMenuLayer;
	private PauseMenuInputCatcher? _pauseMenuRoot;
	private Control? _pauseMenuPanel;
	private Button? _pauseContinueButton;
	private bool _isPauseMenuOpen;

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
			CancelPressed = HidePauseMenu,
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

		Control continueFrame = CreatePauseButton("ContinueGame", "继续游戏", new Color(0.58f, 1.0f, 0.92f), HidePauseMenu, false);
		Control mainMenuFrame = CreatePauseButton("ReturnToMainMenu", "回到主菜单", new Color(0.78f, 0.9f, 1.0f), ReturnToMainMenu, false);
		Control quitFrame = CreatePauseButton("QuitGame", "退出游戏", new Color(0.95f, 0.68f, 0.56f), QuitGameFromPauseMenu, false);
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

	private Control CreatePauseButton(string nodeName, string text, Color accentColor, Action pressedAction, bool flipOrnament)
	{
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{nodeName}Frame",
			CustomMinimumSize = new Vector2(288.0f, 62.0f),
			AccentColor = accentColor,
			FlipOrnament = flipOrnament,
			ProcessMode = ProcessModeEnum.Always,
		};

		var button = new Button
		{
			Name = "Button",
			Text = text,
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
		GetTree().Paused = true;
		_pauseContinueButton?.GrabFocus();
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
	}

	private void ReturnToMainMenu()
	{
		GetTree().Paused = false;
		PackedScene? mainMenuScene = GD.Load<PackedScene>(MainMenuScenePath);
		if (mainMenuScene == null)
		{
			GD.PushError($"Missing main menu scene: {MainMenuScenePath}");
			return;
		}

		Node mainMenu = mainMenuScene.Instantiate();
		GetTree().Root.AddChild(mainMenu);
		GetTree().CurrentScene = mainMenu;
		QueueFree();
	}

	private void QuitGameFromPauseMenu()
	{
		GetTree().Paused = false;
		GetTree().Quit();
	}
}
