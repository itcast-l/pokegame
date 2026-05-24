using Godot;

[Tool]
public partial class DraggableAvatarArea : Control
{
	[Export]
	public bool drag_enabled { get; set; } = true;

	[Export]
	public Color fill_color { get; set; } = new(0.04f, 0.30f, 0.34f, 0.18f);

	[Export]
	public Color rim_color { get; set; } = new(0.58f, 1.0f, 0.95f, 0.56f);

	[Export]
	public Color dragging_rim_color { get; set; } = new(1.0f, 0.92f, 0.48f, 0.82f);

	private bool _dragging;
	private Vector2 _dragOffset;

	public override void _Ready()
	{
		drag_enabled = false;
		MouseFilter = MouseFilterEnum.Ignore;
		MouseDefaultCursorShape = CursorShape.Arrow;
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (!drag_enabled)
		{
			return;
		}

		if (inputEvent is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
		{
			_dragging = mouseButton.Pressed;
			_dragOffset = GetGlobalMousePosition() - GlobalPosition;
			QueueRedraw();
			AcceptEvent();
			return;
		}

		if (_dragging && inputEvent is InputEventMouseMotion)
		{
			GlobalPosition = GetGlobalMousePosition() - _dragOffset;
			AcceptEvent();
		}
	}

	public override bool _HasPoint(Vector2 point)
	{
		float radius = Mathf.Min(Size.X, Size.Y) / 2.0f;
		if (radius <= 0.0f)
		{
			return false;
		}

		return point.DistanceSquaredTo(Size / 2.0f) <= radius * radius;
	}

	public override void _Draw()
	{
		Vector2 center = Size / 2.0f;
		float radius = Mathf.Min(Size.X, Size.Y) / 2.0f;
		if (radius <= 0.0f)
		{
			return;
		}

		Color ringColor = _dragging ? dragging_rim_color : rim_color;
		DrawCircle(center, radius * 0.94f, fill_color);
		DrawArc(center, radius * 0.94f, 0.0f, Mathf.Tau, 96, ringColor, 3.0f, true);
		DrawArc(center, radius * 0.76f, 0.0f, Mathf.Tau, 96, new Color(ringColor.R, ringColor.G, ringColor.B, ringColor.A * 0.32f), 1.5f, true);
	}
}
