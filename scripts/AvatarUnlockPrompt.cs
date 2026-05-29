using System;
using Godot;

public partial class AvatarUnlockPrompt : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	private Label _titleLabel = default!;
	private Button _confirmButton = default!;
	private ColorRect _scrim = default!;

	public Control PromptPanel { get; private set; } = default!;
	public CharacterAvatar UnlockedAvatar { get; private set; } = default!;
	public Button ConfirmButton => _confirmButton;

	public override void _Ready()
	{
		PromptPanel = GetNode<Control>("%AvatarUnlockPromptPanel");
		UnlockedAvatar = GetNode<CharacterAvatar>("%UnlockedAvatar");
		_titleLabel = GetNode<Label>("%UnlockTitle");
		_confirmButton = GetNode<Button>("%ConfirmButton");
		_scrim = GetNode<ColorRect>("%Scrim");

		_scrim.GuiInput += OnScrimGuiInput;
		_confirmButton.Pressed += () => EmitSignal(SignalName.CloseRequested);
	}

	public void RefreshLocalizedText(Func<string, string> localize)
	{
		_titleLabel.Text = localize("AVATAR_UNLOCK_TITLE");
		_confirmButton.Text = localize("AVATAR_UNLOCK_CONFIRM");
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
