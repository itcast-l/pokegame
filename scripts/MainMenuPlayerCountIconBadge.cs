using Godot;

public partial class MainMenuPlayerCountIconBadge : Control
{
	[Export(PropertyHint.Enum, "Players,Items,Score")]
	public int IconType { get; set; }

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Draw()
	{
		float radius = Mathf.Min(Size.X, Size.Y) * 0.5f - 2.0f;
		Vector2 center = Size * 0.5f;
		Color glow = new(0.34f, 0.97f, 0.95f, 0.10f);
		Color fill = new(0.04f, 0.20f, 0.25f, 0.96f);
		Color rim = new(0.67f, 0.98f, 0.95f, 0.55f);
		Color icon = new(0.92f, 1.0f, 0.97f, 0.96f);

		DrawCircle(center, radius + 3.0f, glow);
		DrawCircle(center, radius, fill);
		DrawArc(center, radius, 0.0f, Mathf.Tau, 40, rim, 1.4f, true);

		switch (IconType)
		{
			case 1:
				DrawItemsIcon(center, icon);
				break;
			case 2:
				DrawScoreIcon(center, icon);
				break;
			default:
				DrawPlayersIcon(center, icon);
				break;
		}
	}

	private void DrawPlayersIcon(Vector2 center, Color color)
	{
		DrawCircle(center + new Vector2(0.0f, -5.5f), 4.5f, color);
		Vector2[] shoulders =
		{
			center + new Vector2(-7.0f, 8.0f),
			center + new Vector2(-4.0f, 2.0f),
			center + new Vector2(4.0f, 2.0f),
			center + new Vector2(7.0f, 8.0f),
		};
		DrawColoredPolygon(shoulders, color);
	}

	private void DrawItemsIcon(Vector2 center, Color color)
	{
		DrawLine(center + new Vector2(-6.0f, 7.0f), center + new Vector2(6.0f, -7.0f), color, 2.2f);
		DrawLine(center + new Vector2(-6.0f, -7.0f), center + new Vector2(6.0f, 7.0f), color, 2.2f);
		DrawLine(center + new Vector2(-8.0f, 9.0f), center + new Vector2(-4.0f, 5.0f), color, 1.4f);
		DrawLine(center + new Vector2(4.0f, -5.0f), center + new Vector2(8.0f, -9.0f), color, 1.4f);
		DrawLine(center + new Vector2(-8.0f, -9.0f), center + new Vector2(-4.0f, -5.0f), color, 1.4f);
		DrawLine(center + new Vector2(4.0f, 5.0f), center + new Vector2(8.0f, 9.0f), color, 1.4f);
	}

	private void DrawScoreIcon(Vector2 center, Color color)
	{
		Vector2[] cup =
		{
			center + new Vector2(-6.5f, -7.0f),
			center + new Vector2(6.5f, -7.0f),
			center + new Vector2(4.0f, 0.5f),
			center + new Vector2(-4.0f, 0.5f),
		};
		DrawColoredPolygon(cup, color);
		DrawLine(center + new Vector2(-7.5f, -5.0f), center + new Vector2(-10.5f, -2.0f), color, 1.6f);
		DrawLine(center + new Vector2(-10.5f, -2.0f), center + new Vector2(-7.0f, 1.0f), color, 1.6f);
		DrawLine(center + new Vector2(7.5f, -5.0f), center + new Vector2(10.5f, -2.0f), color, 1.6f);
		DrawLine(center + new Vector2(10.5f, -2.0f), center + new Vector2(7.0f, 1.0f), color, 1.6f);
		DrawRect(new Rect2(center + new Vector2(-1.4f, 1.0f), new Vector2(2.8f, 5.5f)), color);
		DrawRect(new Rect2(center + new Vector2(-5.0f, 6.0f), new Vector2(10.0f, 2.4f)), color);
	}
}
