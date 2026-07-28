using System;
using Godot;

public partial class PauseMenuOverlay : PauseMenuInputCatcher
{
	[Signal]
	public delegate void ContinueRequestedEventHandler();

	[Signal]
	public delegate void ReturnToMainMenuRequestedEventHandler();

	[Signal]
	public delegate void RestartRequestedEventHandler();

	[Signal]
	public delegate void QuitRequestedEventHandler();

	[Signal]
	public delegate void ConfirmQuitRequestedEventHandler();

	private Label _continueLabel = default!;
	private Label _returnToMainMenuLabel = default!;
	private Label _restartLabel = default!;
	private Label? _quitLabel;
	private Label _pauseTitleLabel = default!;
	private Label _pauseMessageLabel = default!;
	private Control _restartFrame = default!;
	private Button _backButton = default!;
	private ColorRect _scrim = default!;

	public Control PauseMenuPanel { get; private set; } = default!;
	public MainMenuExitDialog QuitConfirmRoot { get; private set; } = default!;
	public Button ContinueButton { get; private set; } = default!;
	public Button ReturnToMainMenuButton { get; private set; } = default!;
	public Button RestartButton { get; private set; } = default!;
	public Button QuitButton { get; private set; } = default!;

	public override void _Ready()
	{
		PauseMenuPanel = GetNode<Control>("%PauseMenuPanel");
		QuitConfirmRoot = GetNode<MainMenuExitDialog>("PauseQuitConfirmRoot");
		ContinueButton = GetNode<Button>("%ContinueButton");
		ReturnToMainMenuButton = GetNode<Button>("%ReturnToMainMenuButton");
		RestartButton = GetNode<Button>("%RestartButton");
		QuitButton = GetNode<Button>("%QuitButton");
		_pauseTitleLabel = GetNode<Label>("%PauseMenuTitle");
		_pauseMessageLabel = GetNode<Label>("%PauseMenuMessage");
		_continueLabel = GetNode<Label>("%ContinueLabel");
		_returnToMainMenuLabel = GetNode<Label>("%ReturnToMainMenuLabel");
		_restartLabel = GetNode<Label>("%RestartLabel");
		_quitLabel = GetNodeOrNull<Label>("%QuitLabel");
		_restartFrame = GetNode<Control>("%RestartFrame");
		_backButton = GetNode<Button>("%BackButton");
		_scrim = GetNode<ColorRect>("%Scrim");

		PrepareRowButton(ContinueButton);
		PrepareRowButton(ReturnToMainMenuButton);
		PrepareRowButton(RestartButton);
		PrepareRowButton(QuitButton);
		MainMenuInteractiveStyle.ApplyActionButton(_backButton, 18);
		_backButton.Pressed += () => EmitSignal(SignalName.ContinueRequested);
		ContinueButton.Pressed += () => EmitSignal(SignalName.ContinueRequested);
		ReturnToMainMenuButton.Pressed += () => EmitSignal(SignalName.ReturnToMainMenuRequested);
		RestartButton.Pressed += () => EmitSignal(SignalName.RestartRequested);
		QuitButton.Pressed += () => EmitSignal(SignalName.QuitRequested);
		QuitConfirmRoot.CloseRequested += HideQuitConfirm;
		QuitConfirmRoot.ConfirmRequested += () => EmitSignal(SignalName.ConfirmQuitRequested);
		SecondaryMenuGlassStyle.StyleScrollBar(GetNode<ScrollContainer>("%ActionsScroll").GetVScrollBar());
		SecondaryMenuGlassStyle.Attach(
			this,
			PauseMenuPanel,
			_scrim,
			new Vector2(600.0f, 500.0f),
			GetNode<PanelContainer>("%PauseActionsPanel"));
	}

	public void RefreshLocalizedText(
		Func<string, string> localize,
		string returnButtonKey = "GAME_RETURN_MAIN_MENU",
		string restartButtonKey = "GAME_RESTART_MATCH")
	{
		_pauseTitleLabel.Text = localize("GAME_PAUSE_TITLE");
		_backButton.Text = localize("ADVENTURE_BACK");
		_pauseMessageLabel.Text = localize("GAME_PAUSE_MESSAGE");
		_continueLabel.Text = localize("GAME_CONTINUE");
		_returnToMainMenuLabel.Text = localize(returnButtonKey);
		_restartLabel.Text = localize(restartButtonKey);
		string quitText = localize("GAME_QUIT");
		if (_quitLabel != null && GodotObject.IsInstanceValid(_quitLabel))
		{
			_quitLabel.Text = quitText;
			QuitButton.Text = string.Empty;
		}
		else
		{
			QuitButton.Text = quitText;
		}
		QuitConfirmRoot.RefreshLocalizedText(localize);
	}

	public void SetRestartAvailable(bool available)
	{
		_restartFrame.Visible = available;
		RestartButton.Disabled = !available;
	}

	public void ShowQuitConfirm()
	{
		QuitConfirmRoot.Visible = true;
		QuitConfirmRoot.Modulate = Colors.White;
		QuitConfirmRoot.DialogPanel.Scale = Vector2.One;
		QuitConfirmRoot.ResetActionFocusHighlight();
		QuitConfirmRoot.CancelButton.GrabFocus();
	}

	public void HideQuitConfirm()
	{
		if (!QuitConfirmRoot.Visible)
		{
			return;
		}

		QuitConfirmRoot.Visible = false;
		QuitConfirmRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		QuitButton.GrabFocus();
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
