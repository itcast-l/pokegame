using Godot;

public partial class PearlDivider : Control
{
	[Export]
	public Color LineColor { get; set; } = new(0.62f, 1.0f, 0.94f, 0.54f);

	[Export]
	public Color PearlColor { get; set; } = new(0.96f, 1.0f, 0.92f, 0.78f);

	public override void _Draw()
	{
		Vector2 center = Size * 0.5f;
		float lineWidth = Mathf.Max(Size.X * 0.34f, 80.0f);
		float left = center.X - lineWidth;
		float right = center.X + lineWidth;
		DrawLine(new Vector2(left, center.Y), new Vector2(center.X - 14.0f, center.Y), LineColor, 1.4f);
		DrawLine(new Vector2(center.X + 14.0f, center.Y), new Vector2(right, center.Y), LineColor, 1.4f);
		DrawCircle(center, 5.0f, PearlColor);
		DrawCircle(center + new Vector2(-18.0f, 0.0f), 2.6f, new Color(PearlColor.R, PearlColor.G, PearlColor.B, 0.46f));
		DrawCircle(center + new Vector2(18.0f, 0.0f), 2.6f, new Color(PearlColor.R, PearlColor.G, PearlColor.B, 0.46f));
	}
}
