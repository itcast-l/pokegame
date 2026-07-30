using System;
using Godot;

public partial class PauseMenuOverlay : PauseMenuInputCatcher
{
	private static readonly Color IdleTextColor = new(0.95f, 0.95f, 0.90f, 1.0f);
	private static readonly Color ActiveTextColor = new(1.0f, 0.96f, 0.84f, 1.0f);
	private static readonly Color RestartIdleTextColor = new(1.0f, 0.82f, 0.58f, 1.0f);
	private static readonly Color TitleTextColor = new(1.0f, 0.96f, 0.86f, 1.0f);

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
		_scrim = GetNode<ColorRect>("%Scrim");

		PrepareRowButton(ContinueButton);
		PrepareRowButton(ReturnToMainMenuButton);
		PrepareRowButton(RestartButton);
		ConfigureFocusNavigation(restartAvailable: true);
		ConfigureGeneratedVisualStyle();
		SetInputFocusVisible(false);
		ContinueButton.Pressed += () => EmitSignal(SignalName.ContinueRequested);
		ReturnToMainMenuButton.Pressed += () => EmitSignal(SignalName.ReturnToMainMenuRequested);
		RestartButton.Pressed += () => EmitSignal(SignalName.RestartRequested);
		SecondaryMenuGlassStyle.AttachImagePanel(
			this,
			PauseMenuPanel,
			_scrim,
			new Vector2(620.0f, 436.0f),
			SecondaryMenuGlassStyle.UnifiedVisual);
	}

	private void ConfigureGeneratedVisualStyle()
	{
		ConfigureAction(ContinueButton, _continueLabel, IdleTextColor);
		ConfigureAction(ReturnToMainMenuButton, _returnToMainMenuLabel, IdleTextColor);
		ConfigureAction(RestartButton, _restartLabel, RestartIdleTextColor);
		_pauseTitleLabel.AddThemeColorOverride("font_color", TitleTextColor);
	}

	private void ConfigureAction(Button button, Label label, Color idleTextColor)
	{
		void RefreshLabelColor()
		{
			bool active = _inputFocusVisible && button.HasFocus()
				|| button.ButtonPressed
				|| button.GetGlobalRect().HasPoint(button.GetGlobalMousePosition());
			label.AddThemeColorOverride("font_color", active ? ActiveTextColor : idleTextColor);
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
		_inputFocusVisible = visible;
		foreach (OceanMenuButtonFrame frame in new[] { _continueFrame, _returnToMainMenuFrame, _restartFrame })
		{
			frame.HighlightFocus = visible;
			frame.QueueRedraw();
		}

		RefreshActionLabelColor(ContinueButton, _continueLabel, IdleTextColor);
		RefreshActionLabelColor(ReturnToMainMenuButton, _returnToMainMenuLabel, IdleTextColor);
		RefreshActionLabelColor(RestartButton, _restartLabel, RestartIdleTextColor);
	}

	private void RefreshActionLabelColor(Button button, Label label, Color idleTextColor)
	{
		bool active = _inputFocusVisible && button.HasFocus()
			|| button.ButtonPressed
			|| button.GetGlobalRect().HasPoint(button.GetGlobalMousePosition());
		label.AddThemeColorOverride("font_color", active ? ActiveTextColor : idleTextColor);
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
