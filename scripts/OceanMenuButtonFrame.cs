using Godot;

public partial class OceanMenuButtonFrame : Control
{
	public enum ButtonFrameStyle
	{
		OceanPlaque,
		CardSlabs
	}

	[Export]
	public ButtonFrameStyle FrameStyle { get; set; } = ButtonFrameStyle.OceanPlaque;

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
	private readonly Vector2[] _sideAccentPoints = new Vector2[3];
	private readonly Vector2[] _cardHighlightPoints = new Vector2[4];
	private readonly StyleBoxFlat _selectionGlowStyle = new();
	private readonly StyleBoxFlat _cardBackStyle = new();
	private readonly StyleBoxFlat _cardFrameStyle = new();
	private readonly StyleBoxFlat _cardGlowStyle = new();

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_button = GetNodeOrNull<Button>("Button") ?? FindDirectButtonChild();
		SetProcess(true);
	}

	private Button? FindDirectButtonChild()
	{
		for (int index = 0; index < GetChildCount(); index++)
		{
			if (GetChild(index) is Button button)
			{
				return button;
			}
		}

		return null;
	}

	public override void _Process(double delta)
	{
		float target = Selected ? 0.56f : 0.0f;
		if (_button != null)
		{
			Rect2 buttonRect = _button.GetGlobalRect();
			bool hovered = buttonRect.HasPoint(GetGlobalMousePosition());
			target = Mathf.Max(target, hovered ? 1.0f : 0.0f);
			target = Mathf.Max(target, _button.HasFocus() ? 1.0f : 0.0f);
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
		if (FrameStyle == ButtonFrameStyle.CardSlabs)
		{
			DrawCardSlabFrame(rect);
			return;
		}

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

		DrawSideAccent(origin, w, h, highlight);
		DrawCausticStreaks(origin, w, h);
	}

	private void DrawCardSlabFrame(Rect2 rect)
	{
		float selectedBoost = Selected ? 0.12f : 0.0f;
		float pressedInset = _button?.ButtonPressed == true ? 1.0f : 0.0f;
		Rect2 frameRect = new(rect.Position + new Vector2(0.0f, pressedInset), rect.Size);
		Rect2 backRect = new(frameRect.Position + new Vector2(4.0f, 5.0f), frameRect.Size - new Vector2(2.0f, 0.0f));
		Color cardAccent = AccentColor.Lerp(new Color(0.72f, 0.95f, 0.94f, 1.0f), 0.62f);

		ConfigureCardGlowStyle(_cardGlowStyle, cardAccent);
		DrawStyleBox(
			_cardGlowStyle,
			new Rect2(frameRect.Position - new Vector2(2.0f, 2.0f), frameRect.Size + new Vector2(4.0f, 4.0f)));

		ConfigureCardBackStyle(_cardBackStyle);
		DrawStyleBox(_cardBackStyle, backRect);

		ConfigureCardFrameStyle(_cardFrameStyle, cardAccent, selectedBoost);
		DrawStyleBox(_cardFrameStyle, frameRect);

		if (_glow > 0.05f || Selected)
		{
			_selectionGlowStyle.BgColor = new Color(cardAccent.R, cardAccent.G, cardAccent.B, 0.34f + _glow * 0.18f);
			_selectionGlowStyle.BorderColor = new Color(cardAccent.R, cardAccent.G, cardAccent.B, 0.18f + _glow * 0.28f);
			_selectionGlowStyle.BorderWidthLeft = 1;
			_selectionGlowStyle.BorderWidthTop = 0;
			_selectionGlowStyle.BorderWidthRight = 1;
			_selectionGlowStyle.BorderWidthBottom = 0;
			_selectionGlowStyle.CornerRadiusTopLeft = 3;
			_selectionGlowStyle.CornerRadiusTopRight = 3;
			_selectionGlowStyle.CornerRadiusBottomLeft = 3;
			_selectionGlowStyle.CornerRadiusBottomRight = 3;
			DrawStyleBox(
				_selectionGlowStyle,
				new Rect2(frameRect.Position + new Vector2(7.0f, 11.0f), new Vector2(6.0f, frameRect.Size.Y - 22.0f)));
		}

		float x0 = frameRect.Position.X + 14.0f;
		float x1 = frameRect.End.X - 14.0f;
		float y = frameRect.Position.Y + 8.0f;
		_cardHighlightPoints[0] = new Vector2(x0 + 8.0f, y);
		_cardHighlightPoints[1] = new Vector2(x1 - 8.0f, y);
		_cardHighlightPoints[2] = new Vector2(x1 - 20.0f, y + 1.0f);
		_cardHighlightPoints[3] = new Vector2(x0 + 20.0f, y + 1.0f);
		DrawColoredPolygon(
			_cardHighlightPoints,
			new Color(0.88f, 1.0f, 0.96f, 0.08f + _glow * 0.07f + selectedBoost * 0.25f));
	}

	private void DrawSideAccent(Vector2 origin, float w, float h, Color highlight)
	{
		float direction = FlipOrnament ? -1.0f : 1.0f;
		float edgeX = FlipOrnament ? w - h * 0.2f : h * 0.2f;
		float innerX = edgeX + direction * h * 0.18f;

		_sideAccentPoints[0] = origin + new Vector2(innerX, h * 0.24f);
		_sideAccentPoints[1] = origin + new Vector2(edgeX, h * 0.5f);
		_sideAccentPoints[2] = origin + new Vector2(innerX, h * 0.76f);

		DrawPolyline(_sideAccentPoints, new Color(0.0f, 0.03f, 0.04f, 0.38f), 4.0f, true);
		DrawPolyline(_sideAccentPoints, new Color(highlight.R, highlight.G, highlight.B, 0.54f + _glow * 0.18f), 2.0f, true);
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

	private static void ConfigureCardBackStyle(StyleBoxFlat style)
	{
		style.BgColor = new Color(0.004f, 0.036f, 0.052f, 0.74f);
		style.BorderColor = new Color(0.12f, 0.32f, 0.36f, 0.46f);
		style.BorderWidthLeft = 1;
		style.BorderWidthTop = 1;
		style.BorderWidthRight = 1;
		style.BorderWidthBottom = 1;
		style.CornerRadiusTopLeft = 10;
		style.CornerRadiusTopRight = 10;
		style.CornerRadiusBottomLeft = 10;
		style.CornerRadiusBottomRight = 10;
		style.ShadowColor = new Color(0.0f, 0.014f, 0.022f, 0.32f);
		style.ShadowSize = 10;
		style.ShadowOffset = new Vector2(0.0f, 3.0f);
	}

	private void ConfigureCardFrameStyle(StyleBoxFlat style, Color cardAccent, float selectedBoost)
	{
		style.BgColor = new Color(
			0.012f,
			0.094f + _glow * 0.036f + selectedBoost * 0.08f,
			0.118f + _glow * 0.034f + selectedBoost * 0.08f,
			0.83f + _glow * 0.06f);
		style.BorderColor = new Color(
			cardAccent.R,
			cardAccent.G,
			cardAccent.B,
			0.46f + _glow * 0.30f + selectedBoost * 0.18f);
		style.BorderWidthLeft = 1;
		style.BorderWidthTop = 1;
		style.BorderWidthRight = 1;
		style.BorderWidthBottom = 1;
		style.CornerRadiusTopLeft = 10;
		style.CornerRadiusTopRight = 10;
		style.CornerRadiusBottomLeft = 10;
		style.CornerRadiusBottomRight = 10;
		style.ShadowColor = new Color(0.0f, 0.014f, 0.022f, 0.18f + _glow * 0.08f);
		style.ShadowSize = 6;
		style.ShadowOffset = new Vector2(0.0f, 2.0f);
	}

	private void ConfigureCardGlowStyle(StyleBoxFlat style, Color cardAccent)
	{
		style.BgColor = new Color(cardAccent.R, cardAccent.G, cardAccent.B, _glow * 0.035f);
		style.BorderColor = new Color(cardAccent.R, cardAccent.G, cardAccent.B, 0.03f + _glow * 0.14f);
		style.BorderWidthLeft = 1;
		style.BorderWidthTop = 1;
		style.BorderWidthRight = 1;
		style.BorderWidthBottom = 1;
		style.CornerRadiusTopLeft = 12;
		style.CornerRadiusTopRight = 12;
		style.CornerRadiusBottomLeft = 12;
		style.CornerRadiusBottomRight = 12;
		style.ShadowColor = new Color(cardAccent.R, cardAccent.G, cardAccent.B, _glow * 0.09f);
		style.ShadowSize = 12;
		style.ShadowOffset = Vector2.Zero;
	}
}
