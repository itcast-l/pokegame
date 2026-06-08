using Godot;

public partial class MainMenuAdventureTransition : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	public Control MenuHost { get; private set; } = default!;
	private ColorRect _scrim = default!;

	public override void _Ready()
	{
		MenuHost = GetNode<Control>("%MenuHost");
		_scrim = GetNode<ColorRect>("%Scrim");
		_scrim.GuiInput += OnScrimGuiInput;
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (TryRequestCloseFromPointer(inputEvent))
		{
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnScrimGuiInput(InputEvent inputEvent)
	{
		if (TryRequestCloseFromPointer(inputEvent))
		{
			GetViewport().SetInputAsHandled();
		}
	}

	private bool TryRequestCloseFromPointer(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return false;
		}

		if (IsPointerInsideMenuPanel())
		{
			return false;
		}

		EmitSignal(SignalName.CloseRequested);
		return true;
	}

	private bool IsPointerInsideMenuPanel()
	{
		Control? menuPanel = MenuHost.GetNodeOrNull<Control>("%AdventureMenuPanel");
		if (menuPanel == null || !GodotObject.IsInstanceValid(menuPanel))
		{
			return false;
		}

		Vector2 pointerPosition = GetGlobalMousePosition();
		Rect2 panelRect = menuPanel.GetGlobalRect();
		return panelRect.HasPoint(pointerPosition);
	}
}
