using System;
using Godot;

public partial class MainMenuScene : Control
{
	private Label CreateDialogLabel(string text, int fontSize, Color color, HorizontalAlignment alignment)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		return label;
	}

	private Control CreateDialogButton(string text, Color accentColor, Action pressedAction, bool flipOrnament, float width = 190.0f)
	{
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{text}Frame",
			CustomMinimumSize = new Vector2(width, 64.0f),
			AccentColor = accentColor,
			FlipOrnament = flipOrnament,
		};

		var button = new Button
		{
			Name = "Button",
			Text = text,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		button.SetAnchorsPreset(LayoutPreset.FullRect);
		button.OffsetLeft = flipOrnament ? 26.0f : 4.0f;
		button.OffsetTop = 5.0f;
		button.OffsetRight = flipOrnament ? -4.0f : -26.0f;
		button.OffsetBottom = -5.0f;
		button.AddThemeFontSizeOverride("font_size", 20);
		button.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.78f));
		button.AddThemeColorOverride("font_pressed_color", new Color(0.78f, 1.0f, 0.96f));
		button.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		button.AddThemeConstantOverride("shadow_offset_x", 1);
		button.AddThemeConstantOverride("shadow_offset_y", 2);
		StyleBoxEmpty emptyStyle = new();
		button.AddThemeStyleboxOverride("normal", emptyStyle);
		button.AddThemeStyleboxOverride("hover", emptyStyle);
		button.AddThemeStyleboxOverride("pressed", emptyStyle);
		button.AddThemeStyleboxOverride("focus", emptyStyle);
		button.Pressed += pressedAction;

		frame.AddChild(button);
		return frame;
	}

	private static Texture2D? LoadTextureWithImageFallback(string path)
	{
		Texture2D? texture = GD.Load<Texture2D>(path);
		if (texture != null)
		{
			return texture;
		}

		Image image = Image.LoadFromFile(path);
		return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
	}

	private static StyleBoxFlat CreateDialogPanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.012f, 0.14f, 0.18f, 0.9f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.62f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.68f),
			ShadowSize = 26,
			ShadowOffset = new Vector2(0.0f, 10.0f),
		};
	}

	private static StyleBoxFlat CreateSettingsRowStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.01f, 0.18f, 0.22f, 0.48f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.16f),
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

	private static StyleBoxFlat CreateSliderTrackStyle(bool filled)
	{
		return new StyleBoxFlat
		{
			BgColor = filled
				? new Color(0.56f, 1.0f, 0.92f, 0.52f)
				: new Color(0.0f, 0.08f, 0.12f, 0.72f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, filled ? 0.28f : 0.18f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginTop = 5,
			ContentMarginBottom = 5,
		};
	}

	private static StyleBoxFlat CreateSliderGrabberStyle(bool hovered)
	{
		return new StyleBoxFlat
		{
			BgColor = hovered
				? new Color(1.0f, 1.0f, 0.78f, 0.95f)
				: new Color(0.84f, 1.0f, 0.96f, 0.92f),
			BorderColor = new Color(0.08f, 0.34f, 0.38f, 0.9f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 8,
			ContentMarginBottom = 8,
		};
	}

	private static StyleBoxFlat CreateOptionButtonStyle(bool hovered)
	{
		return new StyleBoxFlat
		{
			BgColor = hovered
				? new Color(0.03f, 0.28f, 0.33f, 0.86f)
				: new Color(0.015f, 0.18f, 0.23f, 0.78f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, hovered ? 0.62f : 0.36f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 12,
			ContentMarginRight = 12,
			ContentMarginTop = 7,
			ContentMarginBottom = 7,
		};
	}

	private static StyleBoxFlat CreatePlayerCountOptionStyle(Color accentColor, bool hovered)
	{
		return new StyleBoxFlat
		{
			BgColor = hovered
				? new Color(0.035f, 0.24f, 0.29f, 0.88f)
				: new Color(0.015f, 0.17f, 0.21f, 0.78f),
			BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, hovered ? 0.86f : 0.54f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(accentColor.R, accentColor.G, accentColor.B, hovered ? 0.22f : 0.12f),
			ShadowSize = hovered ? 16 : 10,
			ShadowOffset = new Vector2(0.0f, hovered ? 5.0f : 4.0f),
		};
	}

	private static StyleBoxFlat CreatePlayerCountBadgeStyle(Color accentColor)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.13f),
			BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.36f),
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

	private static void DrawPearlDivider(Control target)
	{
		Vector2 center = target.Size * 0.5f;
		float lineWidth = Mathf.Max(target.Size.X * 0.34f, 80.0f);
		Color lineColor = new(0.58f, 1.0f, 0.92f, 0.22f);
		target.DrawLine(center - new Vector2(lineWidth, 0.0f), center - new Vector2(14.0f, 0.0f), lineColor, 1.4f, true);
		target.DrawLine(center + new Vector2(14.0f, 0.0f), center + new Vector2(lineWidth, 0.0f), lineColor, 1.4f, true);
		target.DrawCircle(center, 7.0f, new Color(0.88f, 1.0f, 0.94f, 0.82f));
		target.DrawCircle(center - new Vector2(2.5f, 2.5f), 2.3f, new Color(1.0f, 1.0f, 0.86f, 0.88f));
	}
}
