using System;
using Godot;

public partial class PauseMenuOverlay : PauseMenuInputCatcher
{
	private static readonly Color TitleTextColor = new(1.0f, 0.96f, 0.86f, 1.0f);
	private static readonly Vector2 PauseMenuDesignSize = new(620.0f, 470.0f);
	private const float TargetViewportWidthRatio = 0.52f;
	private const float MinimumPanelWidth = 496.0f;
	private const float MaximumPanelWidth = 860.0f;

	[Signal]
	public delegate void ContinueRequestedEventHandler();

	[Signal]
	public delegate void ReturnToMainMenuRequestedEventHandler();

	[Signal]
	public delegate void RestartRequestedEventHandler();

	private Label _continueLabel = default!;
	private Label _returnToMainMenuLabel = default!;
	private Label _restartLabel = default!;
	private Label _pauseTitleLabel = default!;
	private OceanMenuButtonFrame _continueFrame = default!;
	private OceanMenuButtonFrame _returnToMainMenuFrame = default!;
	private OceanMenuButtonFrame _restartFrame = default!;
	private Control _pauseMenuCenter = default!;
	private ColorRect _scrim = default!;
	private bool _inputFocusVisible;

	public Control PauseMenuPanel { get; private set; } = default!;
	public Button ContinueButton { get; private set; } = default!;
	public Button ReturnToMainMenuButton { get; private set; } = default!;
	public Button RestartButton { get; private set; } = default!;

	public override void _Ready()
	{
		PauseMenuPanel = GetNode<Control>("%PauseMenuPanel");
		ContinueButton = GetNode<Button>("%ContinueButton");
		ReturnToMainMenuButton = GetNode<Button>("%ReturnToMainMenuButton");
		RestartButton = GetNode<Button>("%RestartButton");
		_pauseTitleLabel = GetNode<Label>("%PauseMenuTitle");
		_continueLabel = GetNode<Label>("%ContinueLabel");
		_returnToMainMenuLabel = GetNode<Label>("%ReturnToMainMenuLabel");
		_restartLabel = GetNode<Label>("%RestartLabel");
		_continueFrame = GetNode<OceanMenuButtonFrame>("%ContinueFrame");
		_returnToMainMenuFrame = GetNode<OceanMenuButtonFrame>("%ReturnToMainMenuFrame");
		_restartFrame = GetNode<OceanMenuButtonFrame>("%RestartFrame");
		_pauseMenuCenter = GetNode<Control>("PauseMenuCenter");
		_scrim = GetNode<ColorRect>("%Scrim");

		PrepareRowButton(ContinueButton);
		PrepareRowButton(ReturnToMainMenuButton);
		PrepareRowButton(RestartButton);
		ConfigureFocusNavigation(restartAvailable: true);
		ConfigureGeneratedVisualStyle();
		SetInputFocusVisible(true);
		VisibilityChanged += OnVisibilityChanged;
		ContinueButton.Pressed += () => EmitSignal(SignalName.ContinueRequested);
		ReturnToMainMenuButton.Pressed += () => EmitSignal(SignalName.ReturnToMainMenuRequested);
		RestartButton.Pressed += () => EmitSignal(SignalName.RestartRequested);
		ConfigureResponsiveLayout();
	}

	private void ConfigureGeneratedVisualStyle()
	{
		ConfigureFrame(_continueFrame);
		ConfigureFrame(_returnToMainMenuFrame);
		ConfigureFrame(_restartFrame);
		ConfigureAction(ContinueButton, _continueLabel);
		ConfigureAction(ReturnToMainMenuButton, _returnToMainMenuLabel);
		ConfigureAction(RestartButton, _restartLabel);
		_pauseTitleLabel.AddThemeColorOverride("font_color", TitleTextColor);
	}

	private static void ConfigureFrame(OceanMenuButtonFrame frame)
	{
		SecondaryMenuGlassStyle.ConfigurePauseDerivedFrame(
			frame,
			selected: false,
			showIdleTexture: true,
			installDivider: false);
	}

	private void ConfigureResponsiveLayout()
	{
		SetMeta(
			SecondaryMenuGlassStyle.VisualProfileMetadata,
			SecondaryMenuGlassStyle.PauseDerivedVisual.Id);
		PauseMenuPanel.SetMeta(
			SecondaryMenuGlassStyle.PauseDerivedCenterOrnamentsRemovedMetadata,
			true);
		SecondaryMenuGlassStyle.ApplyReadabilityVeil(
			_scrim,
			SecondaryMenuGlassStyle.PauseDerivedVisual);

		Viewport viewport = GetViewport();
		void ApplyLayout()
		{
			if (!GodotObject.IsInstanceValid(PauseMenuPanel)
				|| !GodotObject.IsInstanceValid(_pauseMenuCenter)
				|| !GodotObject.IsInstanceValid(viewport))
			{
				return;
			}

			Vector2 viewportSize = viewport.GetVisibleRect().Size;
			if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
			{
				viewportSize = Size;
			}

			Vector2 available = new(
				Mathf.Max(1.0f, viewportSize.X - SecondaryMenuGlassStyle.SafePadding * 2.0f),
				Mathf.Max(1.0f, viewportSize.Y - SecondaryMenuGlassStyle.SafePadding * 2.0f));
			float desiredWidth = Mathf.Clamp(
				viewportSize.X * TargetViewportWidthRatio,
				MinimumPanelWidth,
				MaximumPanelWidth);
			float desiredScale = desiredWidth / PauseMenuDesignSize.X;
			float fitScale = Mathf.Min(
				available.X / PauseMenuDesignSize.X,
				available.Y / PauseMenuDesignSize.Y);
			float panelScale = Mathf.Min(desiredScale, fitScale);

			PauseMenuPanel.CustomMinimumSize = PauseMenuDesignSize;
			PauseMenuPanel.SetMeta("pause_menu_scale", panelScale);
			_pauseMenuCenter.PivotOffset = _pauseMenuCenter.Size * 0.5f;
			_pauseMenuCenter.Scale = Vector2.One * panelScale;
		}

		viewport.SizeChanged += ApplyLayout;
		_pauseMenuCenter.Resized += () => Callable.From(ApplyLayout).CallDeferred();
		TreeExiting += () =>
		{
			Callable applyLayout = Callable.From(ApplyLayout);
			if (GodotObject.IsInstanceValid(viewport)
				&& viewport.IsConnected(Viewport.SignalName.SizeChanged, applyLayout))
			{
				viewport.Disconnect(Viewport.SignalName.SizeChanged, applyLayout);
			}
		};
		ApplyLayout();
		Callable.From(ApplyLayout).CallDeferred();
	}

	private void OnVisibilityChanged()
	{
		if (Visible)
		{
			SetInputFocusVisible(true);
		}
	}

	private void ConfigureAction(Button button, Label label)
	{
		void RefreshLabelColor()
		{
			label.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		}

		button.FocusEntered += RefreshLabelColor;
		button.FocusExited += RefreshLabelColor;
		button.MouseEntered += () =>
		{
			if (!button.Disabled && button.FocusMode != Control.FocusModeEnum.None)
			{
				button.GrabFocus();
			}
			RefreshLabelColor();
		};
		button.MouseExited += RefreshLabelColor;
		button.ButtonDown += RefreshLabelColor;
		button.ButtonUp += RefreshLabelColor;
		RefreshLabelColor();
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (!Visible)
		{
			return;
		}

		if (inputEvent is InputEventMouseButton or InputEventMouseMotion)
		{
			SetInputFocusVisible(false);
			return;
		}

		if (IsKeyboardOrJoypadNavigationInput(inputEvent))
		{
			SetInputFocusVisible(true);
		}
	}

	private void SetInputFocusVisible(bool visible)
	{
		if (_inputFocusVisible == visible)
		{
			return;
		}

		_inputFocusVisible = visible;
		foreach (OceanMenuButtonFrame frame in new[] { _continueFrame, _returnToMainMenuFrame, _restartFrame })
		{
			frame.HighlightFocus = visible;
			frame.QueueRedraw();
		}

		RefreshActionLabelColor(_continueLabel);
		RefreshActionLabelColor(_returnToMainMenuLabel);
		RefreshActionLabelColor(_restartLabel);
	}

	private static void RefreshActionLabelColor(Label label)
	{
		label.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
	}

	private static bool IsKeyboardOrJoypadNavigationInput(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed(GameInputActions.GameConfirm)
			|| inputEvent.IsActionPressed(GameInputActions.HandPrevious)
			|| inputEvent.IsActionPressed(GameInputActions.HandNext)
			|| inputEvent.IsActionPressed("ui_accept")
			|| inputEvent.IsActionPressed("ui_up")
			|| inputEvent.IsActionPressed("ui_down")
			|| inputEvent.IsActionPressed("ui_left")
			|| inputEvent.IsActionPressed("ui_right"))
		{
			return true;
		}

		return inputEvent is InputEventJoypadButton { Pressed: true }
			|| inputEvent is InputEventJoypadMotion joypadMotion
				&& Mathf.Abs(joypadMotion.AxisValue) > 0.45f;
	}

	public void RefreshLocalizedText(
		Func<string, string> localize,
		string returnButtonKey = "GAME_RETURN_MAIN_MENU",
		string restartButtonKey = "GAME_RESTART_MATCH")
	{
		_pauseTitleLabel.Text = localize("GAME_PAUSE_TITLE");
		_continueLabel.Text = localize("GAME_CONTINUE");
		_returnToMainMenuLabel.Text = localize(returnButtonKey);
		_restartLabel.Text = localize(restartButtonKey);
	}

	public void SetRestartAvailable(bool available)
	{
		_restartFrame.Visible = available;
		RestartButton.Disabled = !available;
		ConfigureFocusNavigation(available);
	}

	private void ConfigureFocusNavigation(bool restartAvailable)
	{
		NodePath continuePath = ContinueButton.GetPath();
		NodePath returnPath = ReturnToMainMenuButton.GetPath();
		NodePath restartPath = RestartButton.GetPath();

		ContinueButton.FocusNeighborTop = restartAvailable ? restartPath : returnPath;
		ContinueButton.FocusNeighborBottom = returnPath;
		ContinueButton.FocusNeighborLeft = restartAvailable ? restartPath : returnPath;
		ContinueButton.FocusNeighborRight = returnPath;

		ReturnToMainMenuButton.FocusNeighborTop = continuePath;
		ReturnToMainMenuButton.FocusNeighborBottom = restartAvailable ? restartPath : continuePath;
		ReturnToMainMenuButton.FocusNeighborLeft = continuePath;
		ReturnToMainMenuButton.FocusNeighborRight = restartAvailable ? restartPath : continuePath;

		RestartButton.FocusNeighborTop = returnPath;
		RestartButton.FocusNeighborBottom = continuePath;
		RestartButton.FocusNeighborLeft = returnPath;
		RestartButton.FocusNeighborRight = continuePath;
	}

	private static void PrepareRowButton(Button button)
	{
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.Flat = true;
	}
}
