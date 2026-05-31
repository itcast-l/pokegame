using System;
using Godot;

public partial class PauseMenuOverlay : PauseMenuInputCatcher
{
	[Signal]
	public delegate void ContinueRequestedEventHandler();

	[Signal]
	public delegate void ReturnToMainMenuRequestedEventHandler();

	[Signal]
	public delegate void QuitRequestedEventHandler();

	[Signal]
	public delegate void ConfirmQuitRequestedEventHandler();

	private Label _confirmTitle = default!;
	private Label _confirmMessage = default!;
	private Label _confirmHint = default!;

	public Control PauseMenuPanel { get; private set; } = default!;
	public Control QuitConfirmRoot { get; private set; } = default!;
	public Button ContinueButton { get; private set; } = default!;
	public Button ReturnToMainMenuButton { get; private set; } = default!;
	public Button QuitButton { get; private set; } = default!;
	public Button QuitCancelButton { get; private set; } = default!;
	public Button QuitConfirmButton { get; private set; } = default!;

	public override void _Ready()
	{
		PauseMenuPanel = GetNode<Control>("%PauseMenuPanel");
		QuitConfirmRoot = GetNode<Control>("%PauseQuitConfirmRoot");
		ContinueButton = GetNode<Button>("%ContinueButton");
		ReturnToMainMenuButton = GetNode<Button>("%ReturnToMainMenuButton");
		QuitButton = GetNode<Button>("%QuitButton");
		QuitCancelButton = GetNode<Button>("%CancelQuitButton");
		QuitConfirmButton = GetNode<Button>("%ConfirmQuitButton");
		_confirmTitle = GetNode<Label>("%QuitConfirmTitle");
		_confirmMessage = GetNode<Label>("%QuitConfirmMessage");
		_confirmHint = GetNode<Label>("%QuitConfirmHint");

		ContinueButton.Pressed += () => EmitSignal(SignalName.ContinueRequested);
		ReturnToMainMenuButton.Pressed += () => EmitSignal(SignalName.ReturnToMainMenuRequested);
		QuitButton.Pressed += () => EmitSignal(SignalName.QuitRequested);
		QuitCancelButton.Pressed += HideQuitConfirm;
		QuitConfirmButton.Pressed += () => EmitSignal(SignalName.ConfirmQuitRequested);
		GetNode<ColorRect>("%ConfirmScrim").GuiInput += OnConfirmScrimGuiInput;
	}

	public void RefreshLocalizedText(Func<string, string> localize, string returnButtonKey = "GAME_RETURN_MAIN_MENU")
	{
		ContinueButton.Text = localize("GAME_CONTINUE");
		ReturnToMainMenuButton.Text = localize(returnButtonKey);
		QuitButton.Text = localize("GAME_QUIT");
		QuitCancelButton.Text = localize("GAME_QUIT_CONFIRM_CANCEL");
		QuitConfirmButton.Text = localize("GAME_QUIT_CONFIRM_ACCEPT");
		_confirmTitle.Text = localize("GAME_QUIT_CONFIRM_TITLE");
		_confirmMessage.Text = localize("GAME_QUIT_CONFIRM_MESSAGE");
		_confirmHint.Text = localize("GAME_QUIT_CONFIRM_HINT");
	}

	public void ShowQuitConfirm()
	{
		QuitConfirmRoot.Visible = true;
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
}
