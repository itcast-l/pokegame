using System;
using Godot;

public partial class MainMenuExitDialog : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	[Signal]
	public delegate void ConfirmRequestedEventHandler();

	private Label _titleLabel = default!;
	private Label _messageLabel = default!;
	private Label _hintLabel = default!;
	private Button _cancelButton = default!;
	private Button _confirmButton = default!;
	private ColorRect _scrim = default!;

	public PanelContainer DialogPanel { get; private set; } = default!;

	public override void _Ready()
	{
		DialogPanel = GetNode<PanelContainer>("%ExitDialogPanel");
		_titleLabel = GetNode<Label>("%ExitTitle");
		_messageLabel = GetNode<Label>("%ExitMessage");
		_hintLabel = GetNode<Label>("%ExitHint");
		_cancelButton = GetNode<Button>("%CancelButton");
		_confirmButton = GetNode<Button>("%ConfirmButton");
		_scrim = GetNode<ColorRect>("%Scrim");

		_scrim.GuiInput += OnScrimGuiInput;
		_cancelButton.Pressed += () => EmitSignal(SignalName.CloseRequested);
		_confirmButton.Pressed += () => EmitSignal(SignalName.ConfirmRequested);
	}

	public void RefreshLocalizedText(Func<string, string> localize)
	{
		_titleLabel.Text = localize("EXIT_TITLE");
		_messageLabel.Text = localize("EXIT_MESSAGE");
		_hintLabel.Text = localize("EXIT_HINT");
		_cancelButton.Text = localize("EXIT_CANCEL");
		_confirmButton.Text = localize("EXIT_CONFIRM");
	}

	private void OnScrimGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		EmitSignal(SignalName.CloseRequested);
		GetViewport().SetInputAsHandled();
	}
}
