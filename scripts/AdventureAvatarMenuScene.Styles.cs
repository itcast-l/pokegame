using Godot;

public partial class AdventureAvatarMenuScene
{
	private static StyleBoxFlat CreateMenuPanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.008f, 0.13f, 0.16f, 0.92f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.58f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.72f),
			ShadowSize = 22,
			ShadowOffset = new Vector2(0.0f, 8.0f),
		};
	}

	private static StyleBoxFlat CreatePanelStyle(float alpha)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.005f, 0.105f, 0.13f, alpha),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.30f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.28f),
			ShadowSize = 6,
		};
	}

	private static StyleBoxFlat CreateBadgeStyle(Color accentColor)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(accentColor.R * 0.12f, accentColor.G * 0.15f, accentColor.B * 0.16f, 0.88f),
			BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.58f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
		};
	}

	private static StyleBoxFlat CreatePreviewFrameStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.0f, 0.06f, 0.075f, 0.32f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.24f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
		};
	}

	private static StyleBoxFlat CreateAbilityCardStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.16f, 0.19f, 0.20f, 0.42f),
			BorderColor = new Color(0.72f, 0.80f, 0.82f, 0.42f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.22f),
			ShadowSize = 4,
		};
	}
}
