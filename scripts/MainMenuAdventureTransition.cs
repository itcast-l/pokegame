using Godot;

public partial class MainMenuAdventureTransition : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	public Control MenuHost { get; private set; } = default!;

	public override void _Ready()
	{
		MenuHost = GetNode<Control>("%MenuHost");
		GetNode<ColorRect>("%Scrim").GuiInput += OnScrimGuiInput;
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
