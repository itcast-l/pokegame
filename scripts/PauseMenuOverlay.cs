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

	private Label _confirmTitle = default!;
	private Label _confirmMessage = default!;
	private Label _confirmHint = default!;
	private PlayerCountDialogFrameArt _quitCancelFrame = default!;
	private PlayerCountDialogFrameArt _quitConfirmFrame = default!;
	private Control _restartActionRoot = default!;

	public Control PauseMenuPanel { get; private set; } = default!;
	public Control QuitConfirmRoot { get; private set; } = default!;
	public Button ContinueButton { get; private set; } = default!;
	public Button ReturnToMainMenuButton { get; private set; } = default!;
	public Button RestartButton { get; private set; } = default!;
	public Button QuitButton { get; private set; } = default!;
	public Button QuitCancelButton { get; private set; } = default!;
	public Button QuitConfirmButton { get; private set; } = default!;

	public override void _Ready()
	{
		PauseMenuPanel = GetNode<Control>("%PauseMenuPanel");
		QuitConfirmRoot = GetNode<Control>("%PauseQuitConfirmRoot");
		_restartActionRoot = GetNode<Control>("%RestartActionRoot");
		ContinueButton = GetNode<Button>("%ContinueButton");
		ReturnToMainMenuButton = GetNode<Button>("%ReturnToMainMenuButton");
		RestartButton = GetNode<Button>("%RestartButton");
		QuitButton = GetNode<Button>("%QuitButton");
		QuitCancelButton = GetNode<Button>("%CancelQuitButton");
		QuitConfirmButton = GetNode<Button>("%ConfirmQuitButton");
		_quitCancelFrame = QuitCancelButton.GetParent() as PlayerCountDialogFrameArt
			?? throw new InvalidOperationException("Quit cancel button must be wrapped by PlayerCountDialogFrameArt.");
		_quitConfirmFrame = QuitConfirmButton.GetParent() as PlayerCountDialogFrameArt
			?? throw new InvalidOperationException("Quit confirm button must be wrapped by PlayerCountDialogFrameArt.");
		_confirmTitle = GetNode<Label>("%QuitConfirmTitle");
		_confirmMessage = GetNode<Label>("%QuitConfirmMessage");
		_confirmHint = GetNode<Label>("%QuitConfirmHint");

		MainMenuInteractiveStyle.ApplyActionButton(ContinueButton, 20);
		MainMenuInteractiveStyle.ApplyActionButton(ReturnToMainMenuButton, 20);
		MainMenuInteractiveStyle.ApplyActionButton(RestartButton, 18);
		MainMenuInteractiveStyle.ApplyActionButton(QuitButton, 20);
		MainMenuInteractiveStyle.ApplyActionButton(QuitCancelButton, 20);
		MainMenuInteractiveStyle.ApplyActionButton(QuitConfirmButton, 20);
		ContinueButton.Pressed += () => EmitSignal(SignalName.ContinueRequested);
		ReturnToMainMenuButton.Pressed += () => EmitSignal(SignalName.ReturnToMainMenuRequested);
		RestartButton.Pressed += () => EmitSignal(SignalName.RestartRequested);
		QuitButton.Pressed += () => EmitSignal(SignalName.QuitRequested);
		QuitCancelButton.Pressed += HideQuitConfirm;
		QuitConfirmButton.Pressed += () => EmitSignal(SignalName.ConfirmQuitRequested);
		GetNode<ColorRect>("%ConfirmScrim").GuiInput += OnConfirmScrimGuiInput;
	}

	public override void _Input(InputEvent inputEvent)
	{
		base._Input(inputEvent);
		if (!QuitConfirmRoot.Visible)
		{
			return;
		}

		if (IsPointerInput(inputEvent))
		{
			SetQuitConfirmFocusHighlightEnabled(false);
			return;
		}

		if (IsNavigationInput(inputEvent))
		{
			SetQuitConfirmFocusHighlightEnabled(true);
		}
	}

	public void RefreshLocalizedText(Func<string, string> localize, string returnButtonKey = "GAME_RETURN_MAIN_MENU")
	{
		ContinueButton.Text = localize("GAME_CONTINUE");
		ReturnToMainMenuButton.Text = localize(returnButtonKey);
		RestartButton.Text = localize("GAME_RESTART_MATCH");
		QuitButton.Text = localize("GAME_QUIT");
		QuitCancelButton.Text = localize("GAME_QUIT_CONFIRM_CANCEL");
		QuitConfirmButton.Text = localize("GAME_QUIT_CONFIRM_ACCEPT");
		_confirmTitle.Text = localize("GAME_QUIT_CONFIRM_TITLE");
		_confirmMessage.Text = localize("GAME_QUIT_CONFIRM_MESSAGE");
		_confirmHint.Text = localize("GAME_QUIT_CONFIRM_HINT");
	}

	public void SetRestartAvailable(bool available)
	{
		_restartActionRoot.Visible = available;
		RestartButton.Disabled = !available;
	}

	public void ShowQuitConfirm()
	{
		QuitConfirmRoot.Visible = true;
		SetQuitConfirmFocusHighlightEnabled(false);
		QuitCancelButton.GrabFocus();
	}

	public void HideQuitConfirm()
	{
		QuitConfirmRoot.Visible = false;
		QuitButton.GrabFocus();
	}

	private void OnConfirmScrimGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		HideQuitConfirm();
		GetViewport().SetInputAsHandled();
	}

	private void SetQuitConfirmFocusHighlightEnabled(bool enabled)
	{
		_quitCancelFrame.HighlightFocus = enabled;
		_quitConfirmFrame.HighlightFocus = enabled;
		MainMenuInteractiveStyle.SetActionButtonFocusHighlight(QuitCancelButton, enabled);
		MainMenuInteractiveStyle.SetActionButtonFocusHighlight(QuitConfirmButton, enabled);
	}

	private static bool IsPointerInput(InputEvent inputEvent)
	{
		return inputEvent is InputEventMouseButton or InputEventMouseMotion;
	}

	private static bool IsNavigationInput(InputEvent inputEvent)
	{
		return inputEvent is InputEventKey { Pressed: true, Echo: false }
			|| inputEvent is InputEventJoypadButton { Pressed: true }
			|| inputEvent is InputEventJoypadMotion joypadMotion && Mathf.Abs(joypadMotion.AxisValue) > 0.45f;
	}
}
