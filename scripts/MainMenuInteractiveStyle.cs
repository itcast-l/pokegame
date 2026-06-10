using Godot;

public static class MainMenuInteractiveStyle
{
	private static readonly Color ActionButtonFontColor = new(0.95f, 1.0f, 0.97f, 1.0f);
	private static readonly Color ActionButtonPressedFontColor = new(0.92f, 1.0f, 0.98f, 1.0f);
	private static readonly Color ActionButtonHighlightFontColor = new(1.0f, 1.0f, 0.84f, 1.0f);
	private static Texture2D? _popupDiamondSelectedIcon;
	private static Texture2D? _popupDiamondIdleIcon;

	public static void ApplyOptionLikeButton(BaseButton button)
	{
		Vector2 minimumSize = button.CustomMinimumSize;
		button.CustomMinimumSize = new Vector2(minimumSize.X, Mathf.Max(minimumSize.Y, 42.0f));
		button.AddThemeColorOverride("font_color", new Color(0.94f, 1.0f, 0.97f, 1.0f));
		button.AddThemeColorOverride("font_pressed_color", new Color(0.94f, 1.0f, 0.97f, 1.0f));
		button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.84f, 1.0f));
		button.AddThemeColorOverride("font_focus_color", new Color(1.0f, 1.0f, 0.84f, 1.0f));
		button.AddThemeFontSizeOverride("font_size", 18);
		button.AddThemeStyleboxOverride("normal", CreateOptionStyle(false));
		button.AddThemeStyleboxOverride("pressed", CreateOptionStyle(false));
		button.AddThemeStyleboxOverride("hover", CreateOptionStyle(true));
		button.AddThemeStyleboxOverride("focus", CreateOptionStyle(true));

		if (button is OptionButton option)
		{
			option.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			ApplyOptionPopup(option);
		}
	}

	public static void ApplyToggleButton(CheckButton toggle)
	{
		ApplyOptionLikeButton(toggle);
		EnsurePopupIcons();
		toggle.Set("theme_override_icons/checked", _popupDiamondSelectedIcon!);
		toggle.Set("theme_override_icons/unchecked", _popupDiamondIdleIcon!);
	}

	public static void ApplyActionButton(Button button, int fontSize)
	{
		button.AddThemeColorOverride("font_color", ActionButtonFontColor);
		button.AddThemeColorOverride("font_pressed_color", ActionButtonPressedFontColor);
		button.AddThemeColorOverride("font_hover_color", ActionButtonHighlightFontColor);
		SetActionButtonFocusHighlight(button, true);
		button.AddThemeFontSizeOverride("font_size", fontSize);
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.Flat = true;
	}

	public static void SetActionButtonFocusHighlight(Button button, bool enabled)
	{
		button.AddThemeColorOverride("font_focus_color", enabled ? ActionButtonHighlightFontColor : ActionButtonFontColor);
	}

	public static void ApplyOptionPopup(OptionButton option)
	{
		PopupMenu popup = option.GetPopup();
		popup.PreferNativeMenu = false;
		popup.ShrinkWidth = false;
		popup.ShrinkHeight = true;
		popup.MinSize = new Vector2I(Mathf.RoundToInt(option.CustomMinimumSize.X), 0);
		popup.Theme = BuildPopupTheme();
	}

	private static StyleBoxFlat CreateOptionStyle(bool hovered)
	{
		return new StyleBoxFlat
		{
			BgColor = hovered
				? new Color(0.07f, 0.24f, 0.29f, 1.0f)
				: new Color(0.05f, 0.17f, 0.22f, 1.0f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = hovered
				? new Color(0.88f, 1.0f, 0.94f, 0.54f)
				: new Color(0.71f, 0.91f, 0.84f, 0.34f),
			CornerRadiusTopLeft = 18,
			CornerRadiusTopRight = 18,
			CornerRadiusBottomLeft = 18,
			CornerRadiusBottomRight = 18,
			ContentMarginLeft = 18.0f,
			ContentMarginTop = 6.0f,
			ContentMarginRight = 18.0f,
			ContentMarginBottom = 6.0f,
		};
	}

	private static Theme BuildPopupTheme()
	{
		EnsurePopupIcons();

		Theme theme = new();
		var popupStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.12f, 0.17f, 0.97f),
			BorderColor = new Color(0.50f, 0.98f, 0.94f, 0.36f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 14,
			CornerRadiusTopRight = 14,
			CornerRadiusBottomLeft = 14,
			CornerRadiusBottomRight = 14,
			ContentMarginLeft = 12.0f,
			ContentMarginTop = 12.0f,
			ContentMarginRight = 12.0f,
			ContentMarginBottom = 12.0f,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.42f),
			ShadowSize = 14,
			ShadowOffset = new Vector2(0.0f, 5.0f),
		};
		var hoverStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.28f, 0.33f, 0.92f),
			BorderColor = new Color(0.86f, 1.0f, 0.96f, 0.24f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10,
			ContentMarginLeft = 10.0f,
			ContentMarginTop = 7.0f,
			ContentMarginRight = 10.0f,
			ContentMarginBottom = 7.0f,
		};
		var separatorStyle = new StyleBoxLine
		{
			Color = new Color(0.55f, 0.98f, 0.94f, 0.16f),
			Thickness = 1,
			GrowBegin = 10.0f,
			GrowEnd = 10.0f,
		};

		theme.SetStylebox("panel", "PopupMenu", popupStyle);
		theme.SetStylebox("hover", "PopupMenu", hoverStyle);
		theme.SetStylebox("focus", "PopupMenu", hoverStyle);
		theme.SetStylebox("separator", "PopupMenu", separatorStyle);
		theme.SetColor("font_color", "PopupMenu", new Color(0.90f, 1.0f, 0.97f, 1.0f));
		theme.SetColor("font_hover_color", "PopupMenu", new Color(1.0f, 0.98f, 0.84f, 1.0f));
		theme.SetColor("font_focus_color", "PopupMenu", new Color(1.0f, 0.98f, 0.84f, 1.0f));
		theme.SetColor("font_disabled_color", "PopupMenu", new Color(0.56f, 0.78f, 0.77f, 0.64f));
		theme.SetColor("font_outline_color", "PopupMenu", new Color(0.0f, 0.04f, 0.07f, 0.92f));
		theme.SetColor("font_separator_color", "PopupMenu", new Color(0.55f, 0.98f, 0.94f, 0.18f));
		theme.SetFontSize("font_size", "PopupMenu", 19);
		theme.SetConstant("h_separation", "PopupMenu", 10);
		theme.SetConstant("item_start_padding", "PopupMenu", 12);
		theme.SetConstant("item_end_padding", "PopupMenu", 12);
		theme.SetConstant("icon_max_width", "PopupMenu", 16);
		theme.SetConstant("outline_size", "PopupMenu", 1);
		theme.SetConstant("v_separation", "PopupMenu", 5);
		theme.SetIcon("checked", "PopupMenu", _popupDiamondSelectedIcon);
		theme.SetIcon("radio_checked", "PopupMenu", _popupDiamondSelectedIcon);
		theme.SetIcon("unchecked", "PopupMenu", _popupDiamondIdleIcon);
		theme.SetIcon("radio_unchecked", "PopupMenu", _popupDiamondIdleIcon);
		return theme;
	}

	private static void EnsurePopupIcons()
	{
		_popupDiamondSelectedIcon ??= CreateDiamondIndicatorTexture(
			new Color(0.24f, 0.98f, 0.94f, 0.42f),
			new Color(0.92f, 1.0f, 0.98f, 0.92f),
			new Color(1.0f, 0.93f, 0.68f, 0.96f),
			false);
		_popupDiamondIdleIcon ??= CreateDiamondIndicatorTexture(
			new Color(0.24f, 0.98f, 0.94f, 0.12f),
			new Color(0.68f, 0.98f, 0.94f, 0.58f),
			Colors.Transparent,
			true);
	}

	private static Texture2D CreateDiamondIndicatorTexture(Color glowColor, Color rimColor, Color fillColor, bool hollow)
	{
		const int size = 18;
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		Vector2 center = new(size * 0.5f - 0.5f, size * 0.5f - 0.5f);

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = Mathf.Abs(x - center.X);
				float dy = Mathf.Abs(y - center.Y);
				float diamondDistance = dx + dy;
				Color pixel = Colors.Transparent;

				if (diamondDistance <= 8.0f)
				{
					float glowFactor = 1.0f - diamondDistance / 8.0f;
					pixel = pixel.Blend(new Color(glowColor.R, glowColor.G, glowColor.B, glowColor.A * glowFactor * glowFactor));
				}

				float rimDistance = Mathf.Abs(diamondDistance - 4.3f);
				if (rimDistance <= 1.15f)
				{
					float rimFactor = 1.0f - rimDistance / 1.15f;
					pixel = pixel.Blend(new Color(rimColor.R, rimColor.G, rimColor.B, rimColor.A * rimFactor));
				}

				if (!hollow && diamondDistance <= 3.0f)
				{
					float fillFactor = 1.0f - diamondDistance / 3.0f;
					pixel = pixel.Blend(new Color(fillColor.R, fillColor.G, fillColor.B, fillColor.A * (0.70f + fillFactor * 0.30f)));
				}

				if (pixel.A > 0.0f)
				{
					image.SetPixel(x, y, pixel);
				}
			}
		}

		return ImageTexture.CreateFromImage(image);
	}
}
