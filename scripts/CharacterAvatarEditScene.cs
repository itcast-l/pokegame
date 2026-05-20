using Godot;

public partial class CharacterAvatarEditScene : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	private Button _backButton = default!;
	private Button _defaultAvatarButton = default!;

	public override void _Ready()
	{
		_backButton = GetNode<Button>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/Header/HeaderLayout/BackButton");
		_backButton.Pressed += RequestClose;

		_defaultAvatarButton = GetNode<Button>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/GridScroll/AvatarGrid/DefaultAvatarOption/SelectButton");
		_defaultAvatarButton.Pressed += RequestClose;
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed("ui_cancel"))
		{
			RequestClose();
			GetViewport().SetInputAsHandled();
		}
	}

	private void RequestClose()
	{
		EmitSignal(SignalName.CloseRequested);
	}
}
