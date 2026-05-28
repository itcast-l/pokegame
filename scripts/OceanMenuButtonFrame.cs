using Godot;

public partial class OceanMenuButtonFrame : Control
{
	[Export]
	public Color AccentColor { get; set; } = new(0.55f, 1.0f, 0.92f, 1.0f);

	[Export]
	public bool FlipOrnament { get; set; }

	[Export]
	public bool Selected { get; set; }

	private Button? _button;
	private float _glow;
	private readonly Vector2[] _plaquePoints = new Vector2[6];
	private readonly Vector2[] _insetPoints = new Vector2[6];
	private readonly Vector2[] _shadowPoints = new Vector2[6];
	private readonly Vector2[] _closedPlaquePoints = new Vector2[7];
	private readonly Vector2[] _closedInsetPoints = new Vector2[7];
	private readonly Vector2[] _causticPoints = new Vector2[3];
	private readonly StyleBoxFlat _selectionGlowStyle = new();

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_button = GetNodeOrNull<Button>("Button");
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		float target = Selected ? 0.56f : 0.0f;
		if (_button != null)
		{
			Rect2 buttonRect = _button.GetGlobalRect();
			bool hovered = buttonRect.HasPoint(GetGlobalMousePosition());
			target = Mathf.Max(target, hovered ? 1.0f : 0.0f);
			if (_button.ButtonPressed)
			{
				target = 0.62f;
			}
		}

		_glow = Mathf.Lerp(_glow, target, Mathf.Clamp((float)delta * 10.0f, 0.0f, 1.0f));
		QueueRedraw();
	}

	public override void _Draw()
	{
		Rect2 rect = _button != null
			? new Rect2(_button.Position, _button.Size)
			: new Rect2(Vector2.Zero, Size);

		if (rect.Size.X <= 0.0f || rect.Size.Y <= 0.0f)
		{
			return;
		}

		float w = rect.Size.X;
		float h = rect.Size.Y;
		if (w < h * 2.05f || h < 28.0f)
		{
			DrawCompactFrame(rect);
			return;
		}

		float bevel = h * 0.28f;
		Vector2 origin = rect.Position;

		_plaquePoints[0] = origin + new Vector2(bevel, 2.0f);
		_plaquePoints[1] = origin + new Vector2(w - bevel * 0.48f, 2.0f);
		_plaquePoints[2] = origin + new Vector2(w - 2.0f, h * 0.5f);
		_plaquePoints[3] = origin + new Vector2(w - bevel * 0.48f, h - 2.0f);
		_plaquePoints[4] = origin + new Vector2(bevel, h - 2.0f);
		_plaquePoints[5] = origin + new Vector2(2.0f, h * 0.5f);

		_insetPoints[0] = origin + new Vector2(bevel + 12.0f, 12.0f);
		_insetPoints[1] = origin + new Vector2(w - bevel * 0.58f - 12.0f, 10.0f);
		_insetPoints[2] = origin + new Vector2(w - 18.0f, h * 0.5f);
		_insetPoints[3] = origin + new Vector2(w - bevel * 0.58f - 12.0f, h - 10.0f);
		_insetPoints[4] = origin + new Vector2(bevel + 12.0f, h - 12.0f);
		_insetPoints[5] = origin + new Vector2(18.0f, h * 0.5f);

		Color shadow = new(0.0f, 0.02f, 0.04f, 0.32f + _glow * 0.08f);
		for (int i = 0; i < _plaquePoints.Length; i++)
		{
			_shadowPoints[i] = _plaquePoints[i] + new Vector2(0.0f, 5.0f);
		}

		float selectedBoost = Selected ? 0.08f : 0.0f;
		Color outer = new(0.02f, 0.22f + _glow * 0.08f + selectedBoost, 0.28f + _glow * 0.08f + selectedBoost, 0.8f);
		Color inner = new(0.04f, 0.42f + _glow * 0.15f + selectedBoost, 0.46f + _glow * 0.13f + selectedBoost, 0.66f);
		Color rim = AccentColor.Lerp(Colors.White, 0.18f + _glow * 0.22f);
		Color highlight = AccentColor.Lerp(new Color(1.0f, 0.94f, 0.66f), _glow * 0.45f);

		DrawColoredPolygon(_shadowPoints, shadow);
		if (Selected)
		{
			ConfigureSelectionGlowStyle(_selectionGlowStyle);
			DrawStyleBox(
				_selectionGlowStyle,
				new Rect2(rect.Position - new Vector2(3.0f, 3.0f), rect.Size + new Vector2(6.0f, 6.0f)));
		}

		CloseInto(_plaquePoints, _closedPlaquePoints);
		CloseInto(_insetPoints, _closedInsetPoints);
		DrawColoredPolygon(_plaquePoints, outer);
		DrawColoredPolygon(_insetPoints, inner);
		DrawPolyline(_closedPlaquePoints, new Color(rim.R, rim.G, rim.B, 0.58f + _glow * 0.28f), 2.0f + _glow, true);
		DrawPolyline(_closedInsetPoints, new Color(0.82f, 1.0f, 0.95f, 0.22f + _glow * 0.18f), 1.2f, true);

		DrawOrnament(origin, w, h, highlight);
		DrawCausticStreaks(origin, w, h);
	}

	private void DrawOrnament(Vector2 origin, float w, float h, Color highlight)
	{
		float side = FlipOrnament ? w - h * 0.5f : h * 0.5f;
		Vector2 center = origin + new Vector2(side, h * 0.5f);
		float direction = FlipOrnament ? -1.0f : 1.0f;

		DrawCircle(center, h * 0.24f, new Color(0.02f, 0.22f, 0.27f, 0.78f));
		DrawCircle(center, h * 0.17f, new Color(highlight.R, highlight.G, highlight.B, 0.62f));
		DrawCircle(center + new Vector2(-direction * h * 0.04f, -h * 0.05f), h * 0.07f, new Color(1.0f, 1.0f, 0.9f, 0.72f));

		for (int i = 0; i < 3; i++)
		{
			float radius = h * (0.34f + i * 0.075f);
			DrawArc(center, radius, -0.58f * direction, 0.58f * direction, 18, new Color(highlight.R, highlight.G, highlight.B, 0.22f), 1.1f, true);
		}
	}

	private void DrawCausticStreaks(Vector2 origin, float w, float h)
	{
		var lineColor = new Color(0.72f, 1.0f, 0.95f, 0.10f + _glow * 0.09f);
		for (int i = 0; i < 3; i++)
		{
			float y = origin.Y + h * (0.3f + i * 0.18f);
			float x0 = origin.X + h * (0.84f + i * 0.15f);
			float x1 = origin.X + w - h * (0.78f + i * 0.1f);
			_causticPoints[0] = new Vector2(x0, y);
			_causticPoints[1] = new Vector2((x0 + x1) * 0.5f, y - 4.0f);
			_causticPoints[2] = new Vector2(x1, y + 2.0f);
			DrawPolyline(_causticPoints, lineColor, 1.0f, true);
		}
	}

	private void DrawCompactFrame(Rect2 rect)
	{
		ConfigureSelectionGlowStyle(_selectionGlowStyle);
		_selectionGlowStyle.BgColor = new Color(0.02f, 0.30f + _glow * 0.1f, 0.34f + _glow * 0.1f, 0.72f);
		_selectionGlowStyle.BorderColor = AccentColor.Lerp(Colors.White, 0.14f + _glow * 0.22f);
		DrawStyleBox(_selectionGlowStyle, rect);
	}

	private static void CloseInto(Vector2[] points, Vector2[] closed)
	{
		for (int index = 0; index < points.Length; index++)
		{
			closed[index] = points[index];
		}

		closed[points.Length] = points[0];
	}

	private void ConfigureSelectionGlowStyle(StyleBoxFlat style)
	{
		style.BgColor = new Color(AccentColor.R, AccentColor.G, AccentColor.B, 0.09f);
		style.BorderColor = new Color(AccentColor.R, AccentColor.G, AccentColor.B, 0.24f);
		style.BorderWidthLeft = 1;
		style.BorderWidthTop = 1;
		style.BorderWidthRight = 1;
		style.BorderWidthBottom = 1;
		style.CornerRadiusTopLeft = 8;
		style.CornerRadiusTopRight = 8;
		style.CornerRadiusBottomLeft = 8;
		style.CornerRadiusBottomRight = 8;
	}
}
