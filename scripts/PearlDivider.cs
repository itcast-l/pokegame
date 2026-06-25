using Godot;

public partial class PearlDivider : Control
{
	[Export]
	public Color LineColor { get; set; } = new(0.62f, 1.0f, 0.94f, 0.54f);

	[Export]
	public Color PearlColor { get; set; } = new(0.96f, 1.0f, 0.92f, 0.78f);

	[Export]
	public Texture2D? DividerLineTexture { get; set; }

	[Export]
	public Texture2D? CenterPearlTexture { get; set; }

	[Export]
	public Texture2D? SidePearlTexture { get; set; }

	public override void _Draw()
	{
		Vector2 center = Size * 0.5f;
		float lineWidth = Mathf.Max(Size.X * 0.34f, 80.0f);
		float left = center.X - lineWidth;
		float right = center.X + lineWidth;
		if (DividerLineTexture != null)
		{
			DrawTextureRect(DividerLineTexture, new Rect2(new Vector2(left, center.Y - 1.0f), new Vector2(center.X - 14.0f - left, 2.0f)), false, LineColor);
			DrawTextureRect(DividerLineTexture, new Rect2(new Vector2(center.X + 14.0f, center.Y - 1.0f), new Vector2(right - (center.X + 14.0f), 2.0f)), false, LineColor);
		}
		else
		{
			DrawLine(new Vector2(left, center.Y), new Vector2(center.X - 14.0f, center.Y), LineColor, 1.4f);
			DrawLine(new Vector2(center.X + 14.0f, center.Y), new Vector2(right, center.Y), LineColor, 1.4f);
		}

		if (CenterPearlTexture != null)
		{
			Vector2 size = new(10.0f, 10.0f);
			DrawTextureRect(CenterPearlTexture, new Rect2(center - size / 2.0f, size), false, PearlColor);
		}
		else
		{
			DrawCircle(center, 5.0f, PearlColor);
		}

		Vector2 sidePearlSize = new(5.2f, 5.2f);
		Color sideColor = new(PearlColor.R, PearlColor.G, PearlColor.B, 0.46f);
		Vector2 leftPearlCenter = center + new Vector2(-18.0f, 0.0f);
		Vector2 rightPearlCenter = center + new Vector2(18.0f, 0.0f);
		if (SidePearlTexture != null)
		{
			DrawTextureRect(SidePearlTexture, new Rect2(leftPearlCenter - sidePearlSize / 2.0f, sidePearlSize), false, sideColor);
			DrawTextureRect(SidePearlTexture, new Rect2(rightPearlCenter - sidePearlSize / 2.0f, sidePearlSize), false, sideColor);
			return;
		}

		DrawCircle(leftPearlCenter, 2.6f, sideColor);
		DrawCircle(rightPearlCenter, 2.6f, sideColor);
	}
}
