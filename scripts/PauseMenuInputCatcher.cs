using System;
using Godot;

public partial class PauseMenuInputCatcher : Control
{
	public Action? CancelPressed { get; set; }

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!Visible || !inputEvent.IsActionPressed("ui_cancel"))
		{
			return;
		}

		CancelPressed?.Invoke();
		GetViewport().SetInputAsHandled();
	}
}
