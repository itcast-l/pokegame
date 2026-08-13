using Godot;

public partial class OceanMenuButtonFrame : Control
{
	private readonly Vector2[] _plaquePoints = new Vector2[6];
	private readonly Vector2[] _insetPoints = new Vector2[6];
	private readonly Vector2[] _shadowPoints = new Vector2[6];
	private readonly Vector2[] _closedPlaquePoints = new Vector2[7];
	private readonly Vector2[] _closedInsetPoints = new Vector2[7];
	private readonly Vector2[] _causticPoints = new Vector2[3];
	private readonly Vector2[] _sideAccentPoints = new Vector2[3];
	private readonly Vector2[] _cardHighlightPoints = new Vector2[4];
	private readonly StyleBoxFlat _selectionGlowStyle = new();
	private readonly StyleBoxFlat _glassGlowStyle = new();
	private readonly StyleBoxFlat _cardBackStyle = new();
	private readonly StyleBoxFlat _cardFrameStyle = new();
	private readonly StyleBoxFlat _cardGlowStyle = new();

	private void DrawAdventureGlassPlaqueFrame(Rect2 rect)
	{
		float w = rect.Size.X;
		float h = rect.Size.Y;
		float bevel = Mathf.Min(h * 0.25f, w * 0.12f);
		Vector2 origin = rect.Position;
		float interaction = Mathf.Max(_glow, Selected ? 0.78f : 0.0f);

		_plaquePoints[0] = origin + new Vector2(bevel, 2.0f);
		_plaquePoints[1] = origin + new Vector2(w - bevel * 0.48f, 2.0f);
		_plaquePoints[2] = origin + new Vector2(w - 2.0f, h * 0.5f);
		_plaquePoints[3] = origin + new Vector2(w - bevel * 0.48f, h - 2.0f);
		_plaquePoints[4] = origin + new Vector2(bevel, h - 2.0f);
		_plaquePoints[5] = origin + new Vector2(2.0f, h * 0.5f);

		float inset = Mathf.Clamp(h * 0.11f, 4.0f, 7.0f);
		_insetPoints[0] = origin + new Vector2(bevel + inset, inset + 1.0f);
		_insetPoints[1] = origin + new Vector2(w - bevel * 0.48f - inset, inset + 1.0f);
		_insetPoints[2] = origin + new Vector2(w - inset - 2.0f, h * 0.5f);
		_insetPoints[3] = origin + new Vector2(w - bevel * 0.48f - inset, h - inset - 1.0f);
		_insetPoints[4] = origin + new Vector2(bevel + inset, h - inset - 1.0f);
		_insetPoints[5] = origin + new Vector2(inset + 2.0f, h * 0.5f);

		for (int index = 0; index < _plaquePoints.Length; index++)
		{
			_shadowPoints[index] = _plaquePoints[index] + new Vector2(0.0f, 4.0f);
		}

		DrawColoredPolygon(
			_shadowPoints,
			new Color(0.0f, 0.02f, 0.035f, 0.24f + interaction * 0.08f));

		if (UsePlaqueGlowBackdrop && (Selected || _glow > 0.04f))
		{
			ConfigureAdventureGlassGlowStyle(_glassGlowStyle, interaction);
			DrawStyleBox(_glassGlowStyle, rect.Grow(Selected ? 4.0f : 2.0f));
		}

		Color outer = new(
			0.015f,
			0.20f + interaction * 0.05f,
			0.24f + interaction * 0.06f,
			0.42f + interaction * 0.10f);
		Color inner = new(
			0.03f,
			0.31f + interaction * 0.08f,
			0.34f + interaction * 0.09f,
			0.18f + interaction * 0.10f);
		Color rim = AccentColor.Lerp(Colors.White, 0.12f + interaction * 0.24f);

		CloseInto(_plaquePoints, _closedPlaquePoints);
		CloseInto(_insetPoints, _closedInsetPoints);
		DrawColoredPolygon(_plaquePoints, outer);
		DrawColoredPolygon(_insetPoints, inner);
		DrawPolyline(
			_closedPlaquePoints,
			new Color(rim.R, rim.G, rim.B, 0.46f + interaction * 0.38f),
			1.15f + interaction * 0.55f,
			true);
		DrawPolyline(
			_closedInsetPoints,
			new Color(0.82f, 1.0f, 0.96f, 0.12f + interaction * 0.16f),
			1.0f,
			true);

		DrawSideAccent(origin, w, h, rim);
		DrawCausticStreaks(origin, w, h);
	}

	private void DrawAdventureGlassSelectorFrame(Rect2 rect)
	{
		float w = rect.Size.X;
		float h = rect.Size.Y;
		float bevel = Mathf.Min(h * 0.36f, w * 0.10f);
		Vector2 origin = rect.Position;
		float interaction = _glow;

		_plaquePoints[0] = origin + new Vector2(bevel, 1.0f);
		_plaquePoints[1] = origin + new Vector2(w - bevel * 0.62f, 1.0f);
		_plaquePoints[2] = origin + new Vector2(w - 1.0f, h * 0.5f);
		_plaquePoints[3] = origin + new Vector2(w - bevel * 0.62f, h - 1.0f);
		_plaquePoints[4] = origin + new Vector2(bevel, h - 1.0f);
		_plaquePoints[5] = origin + new Vector2(1.0f, h * 0.5f);

		float inset = Mathf.Clamp(h * 0.14f, 4.0f, 6.0f);
		_insetPoints[0] = origin + new Vector2(bevel + inset, inset);
		_insetPoints[1] = origin + new Vector2(w - bevel * 0.62f - inset, inset);
		_insetPoints[2] = origin + new Vector2(w - inset, h * 0.5f);
		_insetPoints[3] = origin + new Vector2(w - bevel * 0.62f - inset, h - inset);
		_insetPoints[4] = origin + new Vector2(bevel + inset, h - inset);
		_insetPoints[5] = origin + new Vector2(inset, h * 0.5f);

		for (int index = 0; index < _plaquePoints.Length; index++)
		{
			_shadowPoints[index] = _plaquePoints[index] + new Vector2(0.0f, 3.0f);
		}

		if (interaction > 0.04f)
		{
			ConfigureAdventureGlassGlowStyle(_glassGlowStyle, interaction);
			DrawStyleBox(_glassGlowStyle, rect.Grow(2.0f));
		}

		Color rim = AccentColor.Lerp(Colors.White, 0.12f + interaction * 0.30f);
		DrawColoredPolygon(_shadowPoints, new Color(0.0f, 0.015f, 0.03f, 0.26f));
		DrawColoredPolygon(_plaquePoints, new Color(0.01f, 0.17f + interaction * 0.06f, 0.21f + interaction * 0.07f, 0.76f));
		DrawColoredPolygon(_insetPoints, new Color(0.035f, 0.29f + interaction * 0.08f, 0.32f + interaction * 0.08f, 0.36f));
		CloseInto(_plaquePoints, _closedPlaquePoints);
		CloseInto(_insetPoints, _closedInsetPoints);
		DrawPolyline(_closedPlaquePoints, new Color(rim.R, rim.G, rim.B, 0.50f + interaction * 0.36f), 1.2f + interaction * 0.5f, true);
		DrawPolyline(_closedInsetPoints, new Color(0.86f, 1.0f, 0.97f, 0.14f + interaction * 0.20f), 1.0f, true);

		float indicatorX = origin.X + w - Mathf.Max(27.0f, h * 0.68f);
		DrawLine(
			new Vector2(indicatorX - 12.0f, origin.Y + inset + 1.0f),
			new Vector2(indicatorX - 12.0f, origin.Y + h - inset - 1.0f),
			new Color(rim.R, rim.G, rim.B, 0.24f + interaction * 0.22f),
			1.0f);
		Vector2 chevronCenter = new(indicatorX + 3.0f, origin.Y + h * 0.5f);
		Color chevron = new(0.82f, 1.0f, 0.95f, 0.70f + interaction * 0.30f);
		DrawLine(chevronCenter + new Vector2(-5.0f, -2.5f), chevronCenter + new Vector2(0.0f, 3.0f), chevron, 2.0f, true);
		DrawLine(chevronCenter + new Vector2(0.0f, 3.0f), chevronCenter + new Vector2(5.0f, -2.5f), chevron, 2.0f, true);
		DrawLine(
			origin + new Vector2(bevel + 9.0f, inset + 1.0f),
			new Vector2(indicatorX - 21.0f, origin.Y + inset + 1.0f),
			new Color(0.78f, 1.0f, 0.95f, 0.12f + interaction * 0.12f),
			1.0f);
	}

	private void DrawAdventureGlassMenuRowFrame(Rect2 rect)
	{
		DrawAdventureGlassPlaqueFrame(rect);

		float interaction = Mathf.Max(_glow, Selected ? 0.78f : 0.0f);
		float dividerX = rect.End.X - Mathf.Max(44.0f, rect.Size.Y * 0.72f);
		Color divider = new(0.72f, 1.0f, 0.94f, 0.16f + interaction * 0.18f);
		Color chevron = new(0.82f, 1.0f, 0.95f, 0.68f + interaction * 0.30f);
		float centerY = rect.GetCenter().Y;

		DrawLine(
			new Vector2(dividerX, rect.Position.Y + 10.0f),
			new Vector2(dividerX, rect.End.Y - 10.0f),
			divider,
			1.0f);
		DrawLine(
			new Vector2(dividerX + 13.0f, centerY - 3.0f),
			new Vector2(dividerX + 19.0f, centerY + 3.0f),
			chevron,
			2.0f,
			true);
		DrawLine(
			new Vector2(dividerX + 19.0f, centerY + 3.0f),
			new Vector2(dividerX + 25.0f, centerY - 3.0f),
			chevron,
			2.0f,
			true);

		if (interaction <= 0.04f)
		{
			return;
		}

		Vector2 center = new(rect.GetCenter().X, rect.Position.Y + 4.0f);
		Vector2[] diamond =
		{
			center + new Vector2(0.0f, -4.0f),
			center + new Vector2(4.0f, 0.0f),
			center + new Vector2(0.0f, 4.0f),
			center + new Vector2(-4.0f, 0.0f),
		};
		DrawColoredPolygon(diamond, new Color(1.0f, 0.90f, 0.58f, 0.48f + interaction * 0.42f));
	}

	private void DrawFirstVersionRow(Rect2 rect)
	{
		float interaction = Mathf.Max(_glow, Selected ? 0.78f : 0.0f);
		_glassGlowStyle.BgColor = new Color(
			0.005f,
			0.105f + interaction * 0.14f,
			0.125f + interaction * 0.16f,
			0.34f + interaction * 0.36f);
		_glassGlowStyle.BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.24f + interaction * 0.48f);
		_glassGlowStyle.BorderWidthLeft = 1;
		_glassGlowStyle.BorderWidthTop = 1;
		_glassGlowStyle.BorderWidthRight = 1;
		_glassGlowStyle.BorderWidthBottom = 1;
		_glassGlowStyle.CornerRadiusTopLeft = 8;
		_glassGlowStyle.CornerRadiusTopRight = 8;
		_glassGlowStyle.CornerRadiusBottomLeft = 8;
		_glassGlowStyle.CornerRadiusBottomRight = 8;
		_glassGlowStyle.ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.18f + interaction * 0.06f);
		_glassGlowStyle.ShadowSize = 4;
		_glassGlowStyle.ShadowOffset = new Vector2(0.0f, 2.0f);
		DrawStyleBox(_glassGlowStyle, rect);

		DrawLine(
			new Vector2(rect.Position.X + 12.0f, rect.Position.Y + 2.0f),
			new Vector2(rect.End.X - 12.0f, rect.Position.Y + 2.0f),
			new Color(0.82f, 1.0f, 0.96f, 0.08f + interaction * 0.14f),
			1.0f,
			true);
	}

	private void DrawFirstVersionBack(Rect2 rect)
	{
		float interaction = Mathf.Max(_glow, Selected ? 0.78f : 0.0f);
		_selectionGlowStyle.BgColor = new Color(0.004f, 0.12f + interaction * 0.06f, 0.15f + interaction * 0.06f, 0.80f);
		_selectionGlowStyle.BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.18f + interaction * 0.28f);
		_selectionGlowStyle.BorderWidthLeft = 1;
		_selectionGlowStyle.BorderWidthTop = 1;
		_selectionGlowStyle.BorderWidthRight = 1;
		_selectionGlowStyle.BorderWidthBottom = 1;
		_selectionGlowStyle.CornerRadiusTopLeft = 3;
		_selectionGlowStyle.CornerRadiusTopRight = 3;
		_selectionGlowStyle.CornerRadiusBottomLeft = 3;
		_selectionGlowStyle.CornerRadiusBottomRight = 3;
		_selectionGlowStyle.ShadowColor = new Color(0.0f, 0.02f, 0.03f, 0.34f);
		_selectionGlowStyle.ShadowSize = 6;
		_selectionGlowStyle.ShadowOffset = new Vector2(0.0f, 2.0f);
		DrawStyleBox(_selectionGlowStyle, rect);
	}

	private void DrawFirstVersionPrimary(Rect2 rect)
	{
		float interaction = Mathf.Max(_glow, Selected ? 0.78f : 0.0f);
		_selectionGlowStyle.BgColor = new Color(
			0.18f + interaction * 0.04f,
			0.64f + interaction * 0.10f,
			0.62f + interaction * 0.10f,
			0.94f);
		_selectionGlowStyle.BorderColor = new Color(0.78f, 1.0f, 0.95f, 0.50f + interaction * 0.28f);
		_selectionGlowStyle.BorderWidthLeft = 1;
		_selectionGlowStyle.BorderWidthTop = 1;
		_selectionGlowStyle.BorderWidthRight = 1;
		_selectionGlowStyle.BorderWidthBottom = 1;
		_selectionGlowStyle.CornerRadiusTopLeft = 8;
		_selectionGlowStyle.CornerRadiusTopRight = 8;
		_selectionGlowStyle.CornerRadiusBottomLeft = 8;
		_selectionGlowStyle.CornerRadiusBottomRight = 8;
		_selectionGlowStyle.ShadowColor = new Color(0.18f, 0.88f, 0.82f, 0.16f + interaction * 0.12f);
		_selectionGlowStyle.ShadowSize = 8;
		_selectionGlowStyle.ShadowOffset = new Vector2(0.0f, 2.0f);
		DrawStyleBox(_selectionGlowStyle, rect);

		DrawLine(
			new Vector2(rect.Position.X + 14.0f, rect.Position.Y + 3.0f),
			new Vector2(rect.End.X - 14.0f, rect.Position.Y + 3.0f),
			new Color(0.92f, 1.0f, 0.98f, 0.34f + interaction * 0.20f),
			1.0f,
			true);
	}

	private void DrawNeutralSmokeRow(Rect2 rect)
	{
		float interaction = Mathf.Max(_glow, Selected ? 0.76f : 0.0f);
		Color rim = new Color(0.66f, 0.75f, 0.75f, 1.0f).Lerp(
			new Color(0.48f, 0.90f, 0.76f, 1.0f),
			interaction * 0.72f);

		_glassGlowStyle.BgColor = new Color(
			0.34f + interaction * 0.02f,
			0.38f + interaction * 0.03f,
			0.39f + interaction * 0.03f,
			0.54f + interaction * 0.06f);
		_glassGlowStyle.BorderColor = new Color(rim.R, rim.G, rim.B, 0.34f + interaction * 0.38f);
		_glassGlowStyle.BorderWidthLeft = 1;
		_glassGlowStyle.BorderWidthTop = 1;
		_glassGlowStyle.BorderWidthRight = 1;
		_glassGlowStyle.BorderWidthBottom = 1;
		_glassGlowStyle.CornerRadiusTopLeft = 5;
		_glassGlowStyle.CornerRadiusTopRight = 5;
		_glassGlowStyle.CornerRadiusBottomLeft = 5;
		_glassGlowStyle.CornerRadiusBottomRight = 5;
		_glassGlowStyle.ShadowColor = new Color(0.01f, 0.02f, 0.025f, 0.22f + interaction * 0.06f);
		_glassGlowStyle.ShadowSize = 5;
		_glassGlowStyle.ShadowOffset = new Vector2(0.0f, 3.0f);
		DrawStyleBox(_glassGlowStyle, rect);

		float valueLeft = rect.Position.X + rect.Size.X * 0.54f;
		Rect2 valueRect = new(
			new Vector2(valueLeft, rect.Position.Y + 8.0f),
			new Vector2(rect.End.X - valueLeft - 12.0f, rect.Size.Y - 16.0f));
		_selectionGlowStyle.BgColor = new Color(
			0.43f + interaction * 0.02f,
			0.47f + interaction * 0.03f,
			0.48f + interaction * 0.03f,
			0.58f + interaction * 0.08f);
		_selectionGlowStyle.BorderColor = new Color(rim.R, rim.G, rim.B, 0.44f + interaction * 0.34f);
		_selectionGlowStyle.BorderWidthLeft = 1;
		_selectionGlowStyle.BorderWidthTop = 1;
		_selectionGlowStyle.BorderWidthRight = 1;
		_selectionGlowStyle.BorderWidthBottom = 1;
		_selectionGlowStyle.CornerRadiusTopLeft = 4;
		_selectionGlowStyle.CornerRadiusTopRight = 4;
		_selectionGlowStyle.CornerRadiusBottomLeft = 4;
		_selectionGlowStyle.CornerRadiusBottomRight = 4;
		_selectionGlowStyle.ShadowColor = new Color(0.12f, 0.36f, 0.32f, 0.03f + interaction * 0.07f);
		_selectionGlowStyle.ShadowSize = 4;
		_selectionGlowStyle.ShadowOffset = Vector2.Zero;
		DrawStyleBox(_selectionGlowStyle, valueRect);

		DrawLine(
			new Vector2(rect.Position.X + 18.0f, rect.Position.Y + 3.0f),
			new Vector2(rect.End.X - 18.0f, rect.Position.Y + 3.0f),
			new Color(0.82f, 0.87f, 0.86f, 0.12f + interaction * 0.10f),
			1.0f,
			true);
		DrawLine(
			new Vector2(valueRect.Position.X + 12.0f, valueRect.End.Y - 2.0f),
			new Vector2(valueRect.End.X - 12.0f, valueRect.End.Y - 2.0f),
			new Color(0.42f, 0.84f, 0.70f, 0.08f + interaction * 0.18f),
			1.0f,
			true);
	}

	private void DrawNeutralSmokeBack(Rect2 rect)
	{
		float interaction = Mathf.Max(_glow, Selected ? 0.76f : 0.0f);
		Color rim = new Color(0.68f, 0.76f, 0.76f, 1.0f).Lerp(
			new Color(0.52f, 0.91f, 0.78f, 1.0f),
			interaction * 0.70f);
		_selectionGlowStyle.BgColor = new Color(
			0.39f + interaction * 0.03f,
			0.43f + interaction * 0.03f,
			0.44f + interaction * 0.03f,
			0.60f);
		_selectionGlowStyle.BorderColor = new Color(rim.R, rim.G, rim.B, 0.40f + interaction * 0.34f);
		_selectionGlowStyle.BorderWidthLeft = 1;
		_selectionGlowStyle.BorderWidthTop = 1;
		_selectionGlowStyle.BorderWidthRight = 1;
		_selectionGlowStyle.BorderWidthBottom = 1;
		_selectionGlowStyle.CornerRadiusTopLeft = 4;
		_selectionGlowStyle.CornerRadiusTopRight = 4;
		_selectionGlowStyle.CornerRadiusBottomLeft = 4;
		_selectionGlowStyle.CornerRadiusBottomRight = 4;
		_selectionGlowStyle.ShadowColor = new Color(0.01f, 0.02f, 0.025f, 0.28f);
		_selectionGlowStyle.ShadowSize = 5;
		_selectionGlowStyle.ShadowOffset = new Vector2(0.0f, 3.0f);
		DrawStyleBox(_selectionGlowStyle, rect);
		DrawLine(
			new Vector2(rect.Position.X + 12.0f, rect.Position.Y + 3.0f),
			new Vector2(rect.End.X - 12.0f, rect.Position.Y + 3.0f),
			new Color(0.84f, 0.88f, 0.87f, 0.16f + interaction * 0.14f),
			1.0f,
			true);
	}

	private void DrawNeutralSmokePrimary(Rect2 rect)
	{
		float interaction = Mathf.Max(_glow, Selected ? 0.76f : 0.0f);
		float bevel = Mathf.Clamp(rect.Size.Y * 0.30f, 14.0f, 21.0f);
		Vector2 origin = rect.Position;
		float w = rect.Size.X;
		float h = rect.Size.Y;

		_plaquePoints[0] = origin + new Vector2(bevel, 1.0f);
		_plaquePoints[1] = origin + new Vector2(w - bevel, 1.0f);
		_plaquePoints[2] = origin + new Vector2(w - 1.0f, h * 0.5f);
		_plaquePoints[3] = origin + new Vector2(w - bevel, h - 1.0f);
		_plaquePoints[4] = origin + new Vector2(bevel, h - 1.0f);
		_plaquePoints[5] = origin + new Vector2(1.0f, h * 0.5f);

		float inset = 7.0f;
		_insetPoints[0] = origin + new Vector2(bevel + inset, inset);
		_insetPoints[1] = origin + new Vector2(w - bevel - inset, inset);
		_insetPoints[2] = origin + new Vector2(w - inset, h * 0.5f);
		_insetPoints[3] = origin + new Vector2(w - bevel - inset, h - inset);
		_insetPoints[4] = origin + new Vector2(bevel + inset, h - inset);
		_insetPoints[5] = origin + new Vector2(inset, h * 0.5f);
		for (int index = 0; index < _plaquePoints.Length; index++)
		{
			_shadowPoints[index] = _plaquePoints[index] + new Vector2(0.0f, 5.0f);
		}

		Color rim = AccentColor.Lerp(new Color(0.86f, 0.91f, 0.89f, 1.0f), 0.20f + interaction * 0.20f);
		DrawColoredPolygon(_shadowPoints, new Color(0.01f, 0.03f, 0.03f, 0.36f));
		DrawColoredPolygon(
			_plaquePoints,
			new Color(0.10f, 0.34f + interaction * 0.06f, 0.27f + interaction * 0.06f, 0.94f));
		DrawColoredPolygon(
			_insetPoints,
			new Color(0.20f, 0.55f + interaction * 0.06f, 0.43f + interaction * 0.06f, 0.34f));
		CloseInto(_plaquePoints, _closedPlaquePoints);
		CloseInto(_insetPoints, _closedInsetPoints);
		DrawPolyline(_closedPlaquePoints, new Color(rim.R, rim.G, rim.B, 0.68f + interaction * 0.24f), 1.5f, true);
		DrawPolyline(_closedInsetPoints, new Color(0.82f, 0.95f, 0.90f, 0.20f + interaction * 0.20f), 1.0f, true);
		DrawLine(
			new Vector2(origin.X + bevel + 10.0f, origin.Y + 4.0f),
			new Vector2(origin.X + w - bevel - 10.0f, origin.Y + 4.0f),
			new Color(0.90f, 0.96f, 0.93f, 0.34f + interaction * 0.18f),
			1.0f,
			true);
	}

	private void DrawIndividualToolRow(Rect2 rect)
	{
		float interaction = Mathf.Clamp(_glow, 0.0f, 1.0f);
		float selectedStrength = Selected ? 1.0f : 0.0f;
		float transientStrength = Selected
			? Mathf.Clamp((interaction - 0.56f) / 0.44f, 0.0f, 1.0f)
			: interaction;
		float emphasis = Mathf.Max(selectedStrength, transientStrength);
		Color accent = AccentColor.Lerp(Colors.White, 0.06f + emphasis * 0.14f);
		float idleBackdrop = UseToolRowIdleBackdrop ? 1.0f : 0.0f;
		_glassGlowStyle.BgColor = new Color(
			0.025f + accent.R * 0.15f,
			0.045f + accent.G * 0.17f,
			0.05f + accent.B * 0.17f,
			0.16f * idleBackdrop + selectedStrength * 0.26f + transientStrength * 0.16f);
		_glassGlowStyle.BorderColor = new Color(
			accent.R,
			accent.G,
			accent.B,
			Mathf.Min(0.96f, 0.14f * idleBackdrop + selectedStrength * 0.62f + transientStrength * 0.70f));
		int outlineWidth = emphasis > 0.01f ? (Selected ? 2 : 1) : 0;
		_glassGlowStyle.BorderWidthLeft = outlineWidth;
		_glassGlowStyle.BorderWidthTop = outlineWidth;
		_glassGlowStyle.BorderWidthRight = outlineWidth;
		_glassGlowStyle.BorderWidthBottom = Mathf.Max(1, outlineWidth);
		_glassGlowStyle.CornerRadiusTopLeft = 6;
		_glassGlowStyle.CornerRadiusTopRight = 6;
		_glassGlowStyle.CornerRadiusBottomLeft = 6;
		_glassGlowStyle.CornerRadiusBottomRight = 6;
		_glassGlowStyle.ShadowColor = new Color(
			accent.R,
			accent.G,
			accent.B,
			selectedStrength * 0.13f + transientStrength * 0.11f);
		_glassGlowStyle.ShadowSize = emphasis > 0.01f ? (Selected ? 7 : 5) : 0;
		_glassGlowStyle.ShadowOffset = Vector2.Zero;
		DrawStyleBox(_glassGlowStyle, rect);

		if (emphasis > 0.01f)
		{
			float markerWidth = Selected ? 4.0f : 2.0f;
			DrawLine(
				new Vector2(rect.Position.X + markerWidth * 0.5f, rect.Position.Y + 9.0f),
				new Vector2(rect.Position.X + markerWidth * 0.5f, rect.End.Y - 9.0f),
				new Color(accent.R, accent.G, accent.B, 0.60f + emphasis * 0.34f),
				markerWidth,
				true);
		}

		DrawLine(
			new Vector2(rect.Position.X + 12.0f, rect.Position.Y + 1.0f),
			new Vector2(rect.End.X - 12.0f, rect.Position.Y + 1.0f),
			new Color(
				accent.R,
				accent.G,
				accent.B,
				0.05f * idleBackdrop + selectedStrength * 0.22f + transientStrength * 0.32f),
			1.0f,
			true);
	}

	private void DrawIndividualAction(Rect2 rect)
	{
		float interaction = Mathf.Max(_glow, Selected ? 0.76f : 0.0f);
		Color accent = AccentColor.Lerp(Colors.White, 0.05f + interaction * 0.12f);
		_selectionGlowStyle.BgColor = new Color(
			accent.R * (0.25f + interaction * 0.06f),
			accent.G * (0.25f + interaction * 0.06f),
			accent.B * (0.25f + interaction * 0.06f),
			0.78f + interaction * 0.12f);
		_selectionGlowStyle.BorderColor = new Color(accent.R, accent.G, accent.B, 0.46f + interaction * 0.38f);
		_selectionGlowStyle.BorderWidthLeft = 1;
		_selectionGlowStyle.BorderWidthTop = 1;
		_selectionGlowStyle.BorderWidthRight = 1;
		_selectionGlowStyle.BorderWidthBottom = 1;
		_selectionGlowStyle.CornerRadiusTopLeft = 6;
		_selectionGlowStyle.CornerRadiusTopRight = 6;
		_selectionGlowStyle.CornerRadiusBottomLeft = 6;
		_selectionGlowStyle.CornerRadiusBottomRight = 6;
		_selectionGlowStyle.ShadowColor = new Color(accent.R, accent.G, accent.B, 0.05f + interaction * 0.08f);
		_selectionGlowStyle.ShadowSize = 5;
		_selectionGlowStyle.ShadowOffset = new Vector2(0.0f, 2.0f);
		DrawStyleBox(_selectionGlowStyle, rect);
		DrawLine(
			new Vector2(rect.Position.X + 12.0f, rect.Position.Y + 2.0f),
			new Vector2(rect.End.X - 12.0f, rect.Position.Y + 2.0f),
			new Color(accent.R, accent.G, accent.B, 0.18f + interaction * 0.16f),
			1.0f,
			true);
	}

	private void ConfigureAdventureGlassGlowStyle(StyleBoxFlat style, float interaction)
	{
		Color glowColor = Selected ? AdventureSelectedGlowColor : AccentColor;
		style.BgColor = new Color(glowColor.R, glowColor.G, glowColor.B, Selected ? 0.08f : 0.025f);
		style.BorderColor = new Color(
			glowColor.R,
			glowColor.G,
			glowColor.B,
			(Selected ? 0.34f : 0.12f) + interaction * 0.16f);
		style.BorderWidthLeft = 1;
		style.BorderWidthTop = 1;
		style.BorderWidthRight = 1;
		style.BorderWidthBottom = 1;
		style.CornerRadiusTopLeft = 8;
		style.CornerRadiusTopRight = 8;
		style.CornerRadiusBottomLeft = 8;
		style.CornerRadiusBottomRight = 8;
		style.ShadowColor = new Color(
			glowColor.R,
			glowColor.G,
			glowColor.B,
			(Selected ? 0.20f : 0.04f) + interaction * 0.08f);
		style.ShadowSize = Selected ? 12 : 7;
		style.ShadowOffset = Vector2.Zero;
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

	private void ConfigureCardBackStyle(StyleBoxFlat style)
	{
		style.BgColor = new Color(
			0.004f,
			0.036f,
			0.052f,
			0.74f * CardSlabSurfaceOpacityScale);
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
			(0.83f + _glow * 0.06f) * CardSlabSurfaceOpacityScale);
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
