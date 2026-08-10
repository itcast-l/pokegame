using Godot;

public static class MainMenuInteractiveStyle
{
	public static readonly Color PauseDerivedBodyText = new(243.0f / 255.0f, 247.0f / 255.0f, 244.0f / 255.0f, 1.0f);
	public static readonly Color PauseDerivedSecondaryText = new(158.0f / 255.0f, 219.0f / 255.0f, 213.0f / 255.0f, 1.0f);
	public static readonly Color PauseDerivedCyan = new(34.0f / 255.0f, 185.0f / 255.0f, 192.0f / 255.0f, 1.0f);
	public static readonly Color PauseDerivedGold = new(212.0f / 255.0f, 179.0f / 255.0f, 79.0f / 255.0f, 1.0f);
	public static readonly Color PauseDerivedControlSurface = new(7.0f / 255.0f, 31.0f / 255.0f, 45.0f / 255.0f, 1.0f);

	public sealed class Palette
	{
		public Palette(
			Color surface,
			Color activeSurface,
			Color border,
			Color activeBorder,
			Color text,
			Color focusText,
			Color disabledText)
		{
			Surface = surface;
			ActiveSurface = activeSurface;
			Border = border;
			ActiveBorder = activeBorder;
			Text = text;
			FocusText = focusText;
			DisabledText = disabledText;
		}

		public Color Surface { get; }
		public Color ActiveSurface { get; }
		public Color Border { get; }
		public Color ActiveBorder { get; }
		public Color Text { get; }
		public Color FocusText { get; }
		public Color DisabledText { get; }
	}

	public static readonly Palette SecondaryMenuPalette = new(
		new Color(0.025f, 0.15f, 0.18f, 0.72f),
		new Color(0.035f, 0.25f, 0.28f, 0.88f),
		new Color(0.35f, 0.72f, 0.73f, 0.48f),
		new Color(0.62f, 1.0f, 0.94f, 0.84f),
		new Color(0.92f, 0.98f, 0.97f, 1.0f),
		new Color(0.98f, 1.0f, 0.98f, 1.0f),
		new Color(0.50f, 0.62f, 0.62f, 0.68f));
	public static readonly Palette SecondaryMenuPrimaryPalette = new(
		new Color(0.03f, 0.25f, 0.28f, 0.88f),
		new Color(0.04f, 0.36f, 0.38f, 0.95f),
		new Color(0.28f, 0.78f, 0.82f, 0.68f),
		new Color(0.42f, 0.92f, 0.94f, 0.96f),
		new Color(0.95f, 1.0f, 0.98f, 1.0f),
		new Color(1.0f, 1.0f, 0.94f, 1.0f),
		new Color(0.48f, 0.66f, 0.62f, 0.68f));
	public static readonly Palette PauseDerivedPalette = new(
		PauseDerivedControlSurface,
		new Color(0.027f, 0.220f, 0.255f, 0.98f),
		new Color(PauseDerivedCyan.R, PauseDerivedCyan.G, PauseDerivedCyan.B, 0.56f),
		PauseDerivedGold,
		PauseDerivedBodyText,
		PauseDerivedBodyText,
		new Color(PauseDerivedSecondaryText.R, PauseDerivedSecondaryText.G, PauseDerivedSecondaryText.B, 0.62f));

	public static readonly Palette UtilityPalette = new(
		new Color(0.20f, 0.24f, 0.24f, 0.58f),
		new Color(0.24f, 0.34f, 0.30f, 0.78f),
		new Color(0.58f, 0.69f, 0.65f, 0.38f),
		new Color(0.63f, 0.88f, 0.75f, 0.76f),
		new Color(0.94f, 0.97f, 0.96f, 1.0f),
		new Color(0.90f, 1.0f, 0.94f, 1.0f),
		new Color(0.62f, 0.68f, 0.66f, 0.68f));
	public static readonly Palette NeutralPalette = new(
		new Color(0.16f, 0.18f, 0.19f, 0.64f),
		new Color(0.24f, 0.27f, 0.28f, 0.82f),
		new Color(0.55f, 0.60f, 0.60f, 0.40f),
		new Color(0.76f, 0.82f, 0.81f, 0.72f),
		new Color(0.94f, 0.96f, 0.96f, 1.0f),
		new Color(1.0f, 1.0f, 1.0f, 1.0f),
		new Color(0.58f, 0.62f, 0.62f, 0.66f));
	public static readonly Palette EmeraldPalette = new(
		new Color(0.08f, 0.30f, 0.22f, 0.78f),
		new Color(0.10f, 0.42f, 0.29f, 0.90f),
		new Color(0.34f, 0.76f, 0.57f, 0.54f),
		new Color(0.62f, 0.96f, 0.76f, 0.88f),
		new Color(0.92f, 1.0f, 0.96f, 1.0f),
		new Color(0.97f, 1.0f, 0.98f, 1.0f),
		new Color(0.47f, 0.64f, 0.56f, 0.64f));
	public static readonly Palette DangerPalette = new(
		new Color(0.30f, 0.10f, 0.12f, 0.76f),
		new Color(0.42f, 0.13f, 0.15f, 0.88f),
		new Color(0.70f, 0.31f, 0.34f, 0.56f),
		new Color(0.90f, 0.52f, 0.54f, 0.84f),
		new Color(1.0f, 0.93f, 0.93f, 1.0f),
		new Color(1.0f, 0.98f, 0.98f, 1.0f),
		new Color(0.62f, 0.48f, 0.49f, 0.66f));
	public static readonly Palette IcePalette = new(
		new Color(0.10f, 0.23f, 0.27f, 0.72f),
		new Color(0.12f, 0.34f, 0.40f, 0.86f),
		new Color(0.37f, 0.67f, 0.74f, 0.50f),
		new Color(0.64f, 0.90f, 0.96f, 0.84f),
		new Color(0.91f, 0.98f, 1.0f, 1.0f),
		new Color(0.96f, 1.0f, 1.0f, 1.0f),
		new Color(0.48f, 0.60f, 0.63f, 0.66f));
	public static readonly Palette HeartPalette = CreateAccentPalette(new Color(0.76f, 0.27f, 0.43f));
	public static readonly Palette DiamondPalette = CreateAccentPalette(new Color(0.35f, 0.76f, 0.84f));
	public static readonly Palette SpadePalette = CreateAccentPalette(new Color(0.57f, 0.68f, 0.82f));
	public static readonly Palette ClubPalette = CreateAccentPalette(new Color(0.30f, 0.72f, 0.49f));

	private static readonly Color UnifiedDisabledButtonText = new(
		PauseDerivedSecondaryText.R,
		PauseDerivedSecondaryText.G,
		PauseDerivedSecondaryText.B,
		0.62f);
	private const int ThemedPopupShadowSize = 12;
	private static Texture2D? _popupDiamondSelectedIcon;
	private static Texture2D? _popupDiamondIdleIcon;
	private static Texture2D? _transparentOptionArrow;

	public static void ApplyUnifiedButtonText(BaseButton button)
	{
		button.AddThemeColorOverride("font_color", PauseDerivedBodyText);
		button.AddThemeColorOverride("font_pressed_color", PauseDerivedBodyText);
		button.AddThemeColorOverride("font_hover_color", PauseDerivedBodyText);
		button.AddThemeColorOverride("font_hover_pressed_color", PauseDerivedBodyText);
		button.AddThemeColorOverride("font_focus_color", PauseDerivedBodyText);
		button.AddThemeColorOverride("font_disabled_color", UnifiedDisabledButtonText);
	}

	public static void ApplyOptionLikeButton(BaseButton button)
	{
		Vector2 minimumSize = button.CustomMinimumSize;
		button.CustomMinimumSize = new Vector2(minimumSize.X, Mathf.Max(minimumSize.Y, 42.0f));
		ApplyUnifiedButtonText(button);
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

	public static void ApplyGlassOptionLikeButton(BaseButton button, int fontSize = 18)
	{
		Vector2 minimumSize = button.CustomMinimumSize;
		button.CustomMinimumSize = new Vector2(minimumSize.X, Mathf.Max(minimumSize.Y, 44.0f));
		ApplyUnifiedButtonText(button);
		button.AddThemeFontSizeOverride("font_size", fontSize);
		button.AddThemeStyleboxOverride("normal", CreateGlassOptionStyle(false));
		button.AddThemeStyleboxOverride("pressed", CreateGlassOptionStyle(true));
		button.AddThemeStyleboxOverride("hover", CreateGlassOptionStyle(true));
		button.AddThemeStyleboxOverride("focus", CreateGlassOptionStyle(true));

		if (button is OptionButton option)
		{
			option.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			ApplyOptionPopup(option);
		}
	}

	public static void ApplyThemedControl(BaseButton button, Palette palette, int fontSize = 18)
	{
		Vector2 minimumSize = button.CustomMinimumSize;
		float targetHeight = ReferenceEquals(palette, PauseDerivedPalette) ? 54.0f : 44.0f;
		button.CustomMinimumSize = new Vector2(minimumSize.X, Mathf.Max(minimumSize.Y, targetHeight));
		ApplyUnifiedButtonText(button);
		button.AddThemeFontSizeOverride("font_size", fontSize);
		button.AddThemeStyleboxOverride("normal", CreateThemedControlStyle(palette, active: false));
		button.AddThemeStyleboxOverride("pressed", CreateThemedControlStyle(palette, active: true));
		button.AddThemeStyleboxOverride("hover", CreateThemedControlStyle(palette, active: true));
		button.AddThemeStyleboxOverride("focus", CreateThemedControlStyle(palette, active: true));

		if (button is OptionButton option)
		{
			option.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			ApplyThemedOptionPopup(option, palette);
		}
	}

	public static void ApplySecondaryMenuGameConfigurationOption(OptionButton option)
	{
		ApplyThemedControl(option, SecondaryMenuPalette, 20);
		option.AddThemeStyleboxOverride("normal", CreateSecondaryMenuGameConfigurationOptionStyle(active: false));
		option.AddThemeStyleboxOverride("pressed", CreateSecondaryMenuGameConfigurationOptionStyle(active: true));
		option.AddThemeStyleboxOverride("hover", CreateSecondaryMenuGameConfigurationOptionStyle(active: true));
		option.AddThemeStyleboxOverride("focus", CreateSecondaryMenuGameConfigurationOptionStyle(active: true));
		option.AddThemeIconOverride("arrow", GetTransparentOptionArrow());
		option.AddThemeConstantOverride("arrow_margin", 0);
		option.Alignment = HorizontalAlignment.Right;
		option.ClipText = true;
		option.GetPopup().Theme = BuildSecondaryMenuGameConfigurationPopupTheme();
	}

	public static void ApplyPauseDerivedGameConfigurationOption(OptionButton option)
	{
		ApplyThemedControl(option, PauseDerivedPalette, 21);
		option.AddThemeStyleboxOverride("normal", CreatePauseDerivedGameConfigurationOptionStyle(active: false));
		option.AddThemeStyleboxOverride("pressed", CreatePauseDerivedGameConfigurationOptionStyle(active: true));
		option.AddThemeStyleboxOverride("hover", CreatePauseDerivedGameConfigurationOptionStyle(active: true));
		option.AddThemeStyleboxOverride("focus", CreatePauseDerivedGameConfigurationOptionStyle(active: true));
		option.AddThemeIconOverride("arrow", GetTransparentOptionArrow());
		option.AddThemeConstantOverride("arrow_margin", 0);
		option.Alignment = HorizontalAlignment.Left;
		option.ClipText = true;
		option.Flat = false;
		PopupMenu popup = option.GetPopup();
		popup.Theme = BuildThemedPopupTheme(
			PauseDerivedPalette,
			showSelectionIcons: false,
			popupShadowSize: 0);
		popup.AboutToPopup += () => AlignThemedPopup(option, popup);
	}

	public static void ApplyThemedToggle(CheckButton toggle, Palette palette, int fontSize = 18)
	{
		ApplyThemedControl(toggle, palette, fontSize);
		EnsurePopupIcons();
		toggle.Set("theme_override_icons/checked", _popupDiamondSelectedIcon!);
		toggle.Set("theme_override_icons/unchecked", _popupDiamondIdleIcon!);
	}

	public static void ApplyThemedSlider(HSlider slider, Palette palette)
	{
		float targetHeight = ReferenceEquals(palette, PauseDerivedPalette) ? 54.0f : 20.0f;
		slider.CustomMinimumSize = new Vector2(slider.CustomMinimumSize.X, Mathf.Max(targetHeight, slider.CustomMinimumSize.Y));
		slider.AddThemeStyleboxOverride("slider", CreateSliderTrackStyle(palette));
		slider.AddThemeStyleboxOverride("grabber_area", CreateSliderFillStyle(palette, active: false));
		slider.AddThemeStyleboxOverride("grabber_area_highlight", CreateSliderFillStyle(palette, active: true));
		slider.AddThemeStyleboxOverride("grabber", CreateSliderGrabberStyle(palette, active: false));
		slider.AddThemeStyleboxOverride("grabber_highlight", CreateSliderGrabberStyle(palette, active: true));
	}

	public static void ApplyThemedLineEdit(LineEdit input, Palette palette, int fontSize = 18)
	{
		Vector2 minimumSize = input.CustomMinimumSize;
		float targetHeight = ReferenceEquals(palette, PauseDerivedPalette) ? 54.0f : 44.0f;
		input.CustomMinimumSize = new Vector2(minimumSize.X, Mathf.Max(minimumSize.Y, targetHeight));
		input.AddThemeColorOverride("font_color", palette.Text);
		input.AddThemeColorOverride("font_placeholder_color", new Color(
			palette.DisabledText.R,
			palette.DisabledText.G,
			palette.DisabledText.B,
			Mathf.Max(0.72f, palette.DisabledText.A)));
		input.AddThemeColorOverride("font_uneditable_color", palette.DisabledText);
		input.AddThemeColorOverride("caret_color", palette.ActiveBorder);
		input.AddThemeColorOverride("selection_color", new Color(
			palette.ActiveBorder.R,
			palette.ActiveBorder.G,
			palette.ActiveBorder.B,
			0.38f));
		input.AddThemeFontSizeOverride("font_size", fontSize);
		input.AddThemeStyleboxOverride("normal", CreateThemedControlStyle(palette, active: false));
		input.AddThemeStyleboxOverride("focus", CreateThemedControlStyle(palette, active: true));
		input.AddThemeStyleboxOverride("read_only", CreateThemedControlStyle(NeutralPalette, active: false));
	}

	public static void ApplyThemedTab(Button button, Palette palette, bool selected, int fontSize = 18)
	{
		ApplyThemedControl(button, selected ? palette : NeutralPalette, fontSize);
		button.ButtonPressed = selected;
		button.AddThemeStyleboxOverride("pressed", CreateThemedControlStyle(palette, active: true));
	}

	public static void ApplyGameConfigurationOption(OptionButton option)
	{
		Vector2 minimumSize = option.CustomMinimumSize;
		option.CustomMinimumSize = new Vector2(minimumSize.X, Mathf.Max(minimumSize.Y, 44.0f));
		ApplyUnifiedButtonText(option);
		option.AddThemeFontSizeOverride("font_size", 19);
		option.AddThemeStyleboxOverride("normal", CreateGameConfigurationOptionContentStyle());
		option.AddThemeStyleboxOverride("pressed", CreateGameConfigurationOptionContentStyle());
		option.AddThemeStyleboxOverride("hover", CreateGameConfigurationOptionContentStyle());
		option.AddThemeStyleboxOverride("focus", CreateGameConfigurationOptionContentStyle());
		option.AddThemeIconOverride("arrow", GetTransparentOptionArrow());
		option.AddThemeConstantOverride("arrow_margin", 0);
		option.Alignment = HorizontalAlignment.Right;
		option.Flat = true;
		option.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		ApplyOptionPopup(option);
	}

	public static void ApplyNeutralSmokeGameConfigurationOption(OptionButton option)
	{
		Vector2 minimumSize = option.CustomMinimumSize;
		option.CustomMinimumSize = new Vector2(minimumSize.X, Mathf.Max(minimumSize.Y, 44.0f));
		ApplyUnifiedButtonText(option);
		option.AddThemeFontSizeOverride("font_size", 20);
		option.AddThemeStyleboxOverride("normal", CreateNeutralSmokeConfigurationOptionContentStyle());
		option.AddThemeStyleboxOverride("pressed", CreateNeutralSmokeConfigurationOptionContentStyle());
		option.AddThemeStyleboxOverride("hover", CreateNeutralSmokeConfigurationOptionContentStyle());
		option.AddThemeStyleboxOverride("focus", CreateNeutralSmokeConfigurationOptionContentStyle());
		option.AddThemeIconOverride("arrow", GetTransparentOptionArrow());
		option.AddThemeConstantOverride("arrow_margin", 0);
		option.Alignment = HorizontalAlignment.Right;
		option.ClipText = true;
		option.Flat = true;
		option.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		ApplyNeutralSmokeOptionPopup(option);
	}

	public static void ApplyGlassToggleButton(CheckButton toggle)
	{
		ApplyGlassOptionLikeButton(toggle);
		EnsurePopupIcons();
		toggle.Set("theme_override_icons/checked", _popupDiamondSelectedIcon!);
		toggle.Set("theme_override_icons/unchecked", _popupDiamondIdleIcon!);
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
		ApplyActionButton(button, fontSize, null);
	}

	public static void ApplyActionButton(Button button, int fontSize, Palette? palette)
	{
		ApplyUnifiedButtonText(button);
		SetActionButtonFocusHighlight(button, true, palette);
		button.AddThemeFontSizeOverride("font_size", fontSize);
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.Flat = true;
	}

	public static void SetActionButtonFocusHighlight(Button button, bool enabled)
	{
		SetActionButtonFocusHighlight(button, enabled, null);
	}

	public static void SetActionButtonFocusHighlight(Button button, bool enabled, Palette? palette)
	{
		ApplyUnifiedButtonText(button);
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

	private static void ApplyThemedOptionPopup(OptionButton option, Palette palette)
	{
		PopupMenu popup = option.GetPopup();
		popup.PreferNativeMenu = false;
		popup.ShrinkWidth = false;
		popup.ShrinkHeight = true;
		popup.MinSize = new Vector2I(Mathf.RoundToInt(option.CustomMinimumSize.X), 0);
		popup.Theme = BuildThemedPopupTheme(palette);
		popup.AboutToPopup += () => AlignThemedPopup(option, popup);
	}

	private static void AlignThemedPopup(OptionButton option, PopupMenu popup)
	{
		if (!GodotObject.IsInstanceValid(option) || !GodotObject.IsInstanceValid(popup))
		{
			return;
		}

		Rect2 optionRect = option.GetGlobalRect();
		int width = Mathf.RoundToInt(optionRect.Size.X);
		int shadowReserve = GetPopupHorizontalShadowReserve(popup);
		popup.MinSize = new Vector2I(Mathf.Max(1, width - shadowReserve), 0);
		popup.Size = new Vector2I(width, popup.Size.Y);
		popup.Position = new Vector2I(Mathf.RoundToInt(optionRect.Position.X), popup.Position.Y);
		Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(option) && GodotObject.IsInstanceValid(popup))
			{
				Rect2 deferredRect = option.GetGlobalRect();
				int deferredWidth = Mathf.RoundToInt(deferredRect.Size.X);
				int deferredShadowReserve = GetPopupHorizontalShadowReserve(popup);
				popup.MinSize = new Vector2I(Mathf.Max(1, deferredWidth - deferredShadowReserve), 0);
				popup.Size = new Vector2I(deferredWidth, popup.Size.Y);
				popup.Position = new Vector2I(Mathf.RoundToInt(deferredRect.Position.X), popup.Position.Y);
			}
		}).CallDeferred();
	}

	private static int GetPopupHorizontalShadowReserve(PopupMenu popup)
	{
		return popup.GetThemeStylebox("panel", "PopupMenu") is StyleBoxFlat panel
			? panel.ShadowSize * 2
			: 0;
	}

	private static StyleBoxFlat CreateThemedControlStyle(Palette palette, bool active)
	{
		return new StyleBoxFlat
		{
			BgColor = active ? palette.ActiveSurface : palette.Surface,
			BorderColor = active ? palette.ActiveBorder : palette.Border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 12.0f,
			ContentMarginTop = 6.0f,
			ContentMarginRight = 12.0f,
			ContentMarginBottom = 6.0f,
		};
	}

	private static StyleBoxFlat CreateSecondaryMenuGameConfigurationOptionStyle(bool active)
	{
		StyleBoxFlat style = CreateThemedControlStyle(SecondaryMenuPalette, active);
		style.ContentMarginRight = 48.0f;
		return style;
	}

	private static StyleBoxFlat CreatePauseDerivedGameConfigurationOptionStyle(bool active)
	{
		StyleBoxFlat style = CreateThemedControlStyle(PauseDerivedPalette, active);
		style.ContentMarginLeft = 14.0f;
		style.ContentMarginRight = 48.0f;
		return style;
	}

	private static StyleBoxFlat CreateSliderTrackStyle(Palette palette)
	{
		return new StyleBoxFlat
		{
			BgColor = palette.Surface,
			BorderColor = palette.Border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginTop = 4.0f,
			ContentMarginBottom = 4.0f,
		};
	}

	private static StyleBoxFlat CreateSliderFillStyle(Palette palette, bool active)
	{
		return new StyleBoxFlat
		{
			BgColor = active ? palette.ActiveBorder : new Color(palette.ActiveBorder.R, palette.ActiveBorder.G, palette.ActiveBorder.B, 0.54f),
			BorderColor = active ? palette.FocusText : palette.ActiveBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginTop = 4.0f,
			ContentMarginBottom = 4.0f,
		};
	}

	private static StyleBoxFlat CreateSliderGrabberStyle(Palette palette, bool active)
	{
		return new StyleBoxFlat
		{
			BgColor = active ? palette.FocusText : palette.Text,
			BorderColor = active ? palette.ActiveBorder : palette.Border,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 7,
			CornerRadiusTopRight = 7,
			CornerRadiusBottomLeft = 7,
			CornerRadiusBottomRight = 7,
			ContentMarginLeft = 7.0f,
			ContentMarginTop = 7.0f,
			ContentMarginRight = 7.0f,
			ContentMarginBottom = 7.0f,
		};
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

	private static StyleBoxFlat CreateGlassOptionStyle(bool active)
	{
		return new StyleBoxFlat
		{
			BgColor = active
				? new Color(0.05f, 0.25f, 0.29f, 0.66f)
				: new Color(0.018f, 0.14f, 0.17f, 0.46f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = active
				? new Color(0.84f, 1.0f, 0.95f, 0.72f)
				: new Color(0.62f, 1.0f, 0.94f, 0.34f),
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 14.0f,
			ContentMarginTop = 6.0f,
			ContentMarginRight = 14.0f,
			ContentMarginBottom = 6.0f,
		};
	}

	private static StyleBoxEmpty CreateGameConfigurationOptionContentStyle()
	{
		return new StyleBoxEmpty
		{
			ContentMarginLeft = 10.0f,
			ContentMarginTop = 6.0f,
			ContentMarginRight = 62.0f,
			ContentMarginBottom = 6.0f,
		};
	}

	private static StyleBoxEmpty CreateNeutralSmokeConfigurationOptionContentStyle()
	{
		return new StyleBoxEmpty
		{
			ContentMarginLeft = 18.0f,
			ContentMarginTop = 8.0f,
			ContentMarginRight = 64.0f,
			ContentMarginBottom = 8.0f,
		};
	}

	private static void ApplyNeutralSmokeOptionPopup(OptionButton option)
	{
		PopupMenu popup = option.GetPopup();
		popup.PreferNativeMenu = false;
		popup.ShrinkWidth = false;
		popup.ShrinkHeight = true;
		popup.MinSize = new Vector2I(320, 0);
		popup.Theme = BuildNeutralSmokePopupTheme();
		popup.AboutToPopup += () =>
		{
			AlignNeutralSmokePopup(option, popup);
			Callable.From(() => AlignNeutralSmokePopup(option, popup)).CallDeferred();
		};
	}

	private static void AlignNeutralSmokePopup(OptionButton option, PopupMenu popup)
	{
		if (!GodotObject.IsInstanceValid(option) || !GodotObject.IsInstanceValid(popup))
		{
			return;
		}

		Rect2 optionRect = option.GetGlobalRect();
		int width = Mathf.RoundToInt(optionRect.Size.X);
		popup.MinSize = new Vector2I(width, 0);
		popup.Size = new Vector2I(width, popup.Size.Y);
		popup.Position = new Vector2I(
			Mathf.RoundToInt(optionRect.Position.X),
			popup.Position.Y);
	}

	private static Theme BuildThemedPopupTheme(
		Palette palette,
		bool showSelectionIcons = true,
		int popupShadowSize = ThemedPopupShadowSize)
	{
		EnsurePopupIcons();
		Theme theme = new();
		LocalizationTypography.ApplyDefaultFont(theme);
		StyleBoxFlat popupStyle = CreateThemedControlStyle(palette, active: false);
		popupStyle.BgColor = new Color(palette.Surface.R, palette.Surface.G, palette.Surface.B, 0.98f);
		popupStyle.ContentMarginLeft = 10.0f;
		popupStyle.ContentMarginTop = 10.0f;
		popupStyle.ContentMarginRight = 10.0f;
		popupStyle.ContentMarginBottom = 10.0f;
		popupStyle.ShadowColor = new Color(0.01f, 0.02f, 0.025f, 0.42f);
		popupStyle.ShadowSize = popupShadowSize;
		popupStyle.ShadowOffset = new Vector2(0.0f, 5.0f);
		StyleBoxFlat hoverStyle = CreateThemedControlStyle(palette, active: true);
		StyleBoxLine separatorStyle = new()
		{
			Color = new Color(palette.Border.R, palette.Border.G, palette.Border.B, 0.34f),
			Thickness = 1,
			GrowBegin = 8.0f,
			GrowEnd = 8.0f,
		};

		theme.SetStylebox("panel", "PopupMenu", popupStyle);
		theme.SetStylebox("hover", "PopupMenu", hoverStyle);
		theme.SetStylebox("focus", "PopupMenu", hoverStyle);
		theme.SetStylebox("separator", "PopupMenu", separatorStyle);
		theme.SetColor("font_color", "PopupMenu", palette.Text);
		theme.SetColor("font_hover_color", "PopupMenu", palette.FocusText);
		theme.SetColor("font_focus_color", "PopupMenu", palette.FocusText);
		theme.SetColor("font_disabled_color", "PopupMenu", palette.DisabledText);
		theme.SetColor("font_outline_color", "PopupMenu", new Color(0.01f, 0.02f, 0.025f, 0.80f));
		theme.SetFontSize("font_size", "PopupMenu", 18);
		theme.SetConstant("h_separation", "PopupMenu", 8);
		theme.SetConstant("item_start_padding", "PopupMenu", 10);
		theme.SetConstant("item_end_padding", "PopupMenu", 10);
		theme.SetConstant("icon_max_width", "PopupMenu", showSelectionIcons ? 16 : 1);
		theme.SetConstant("outline_size", "PopupMenu", 1);
		theme.SetConstant("v_separation", "PopupMenu", 4);
		Texture2D selectedIcon = showSelectionIcons ? _popupDiamondSelectedIcon! : GetTransparentOptionArrow();
		Texture2D idleIcon = showSelectionIcons ? _popupDiamondIdleIcon! : GetTransparentOptionArrow();
		theme.SetIcon("checked", "PopupMenu", selectedIcon);
		theme.SetIcon("radio_checked", "PopupMenu", selectedIcon);
		theme.SetIcon("unchecked", "PopupMenu", idleIcon);
		theme.SetIcon("radio_unchecked", "PopupMenu", idleIcon);
		return theme;
	}

	private static Theme BuildSecondaryMenuGameConfigurationPopupTheme()
	{
		Theme theme = BuildThemedPopupTheme(
			SecondaryMenuPalette,
			showSelectionIcons: false,
			popupShadowSize: 0);
		StyleBoxFlat hoverStyle = CreateThemedControlStyle(SecondaryMenuPalette, active: true);
		hoverStyle.BgColor = new Color(0.055f, 0.34f, 0.36f, 0.96f);
		hoverStyle.BorderColor = new Color(0.68f, 1.0f, 0.94f, 0.92f);
		hoverStyle.BorderWidthLeft = 3;
		hoverStyle.CornerRadiusTopLeft = 5;
		hoverStyle.CornerRadiusTopRight = 5;
		hoverStyle.CornerRadiusBottomLeft = 5;
		hoverStyle.CornerRadiusBottomRight = 5;
		theme.SetStylebox("hover", "PopupMenu", hoverStyle);
		theme.SetStylebox("focus", "PopupMenu", hoverStyle);
		theme.SetColor("font_hover_color", "PopupMenu", SecondaryMenuPalette.FocusText);
		theme.SetColor("font_focus_color", "PopupMenu", SecondaryMenuPalette.FocusText);
		return theme;
	}

	private static Theme BuildPopupTheme()
	{
		EnsurePopupIcons();

		Theme theme = new();
		LocalizationTypography.ApplyDefaultFont(theme);
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

	private static Theme BuildNeutralSmokePopupTheme()
	{
		Theme theme = new();
		LocalizationTypography.ApplyDefaultFont(theme);
		StyleBoxFlat popupStyle = new()
		{
			BgColor = new Color(0.26f, 0.31f, 0.32f, 0.98f),
			BorderColor = new Color(0.68f, 0.77f, 0.76f, 0.64f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5,
			ContentMarginLeft = 10.0f,
			ContentMarginTop = 10.0f,
			ContentMarginRight = 10.0f,
			ContentMarginBottom = 10.0f,
			ShadowColor = new Color(0.01f, 0.02f, 0.025f, 0.44f),
			ShadowSize = 12,
			ShadowOffset = new Vector2(0.0f, 5.0f),
		};
		StyleBoxFlat hoverStyle = new()
		{
			BgColor = new Color(0.19f, 0.55f, 0.44f, 0.88f),
			BorderColor = new Color(0.66f, 0.96f, 0.84f, 0.54f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginLeft = 10.0f,
			ContentMarginTop = 8.0f,
			ContentMarginRight = 10.0f,
			ContentMarginBottom = 8.0f,
		};
		StyleBoxLine separatorStyle = new()
		{
			Color = new Color(0.68f, 0.77f, 0.76f, 0.22f),
			Thickness = 1,
			GrowBegin = 8.0f,
			GrowEnd = 8.0f,
		};

		theme.SetStylebox("panel", "PopupMenu", popupStyle);
		theme.SetStylebox("hover", "PopupMenu", hoverStyle);
		theme.SetStylebox("focus", "PopupMenu", hoverStyle);
		theme.SetStylebox("separator", "PopupMenu", separatorStyle);
		theme.SetColor("font_color", "PopupMenu", new Color(0.94f, 0.96f, 0.96f, 1.0f));
		theme.SetColor("font_hover_color", "PopupMenu", new Color(0.96f, 1.0f, 0.98f, 1.0f));
		theme.SetColor("font_focus_color", "PopupMenu", new Color(0.96f, 1.0f, 0.98f, 1.0f));
		theme.SetColor("font_disabled_color", "PopupMenu", new Color(0.64f, 0.70f, 0.70f, 0.66f));
		theme.SetColor("font_outline_color", "PopupMenu", new Color(0.02f, 0.03f, 0.035f, 0.86f));
		theme.SetFontSize("font_size", "PopupMenu", 20);
		theme.SetConstant("h_separation", "PopupMenu", 0);
		theme.SetConstant("item_start_padding", "PopupMenu", 16);
		theme.SetConstant("item_end_padding", "PopupMenu", 12);
		theme.SetConstant("icon_max_width", "PopupMenu", 1);
		theme.SetConstant("outline_size", "PopupMenu", 1);
		theme.SetConstant("v_separation", "PopupMenu", 6);
		Texture2D transparentIcon = GetTransparentOptionArrow();
		theme.SetIcon("checked", "PopupMenu", transparentIcon);
		theme.SetIcon("radio_checked", "PopupMenu", transparentIcon);
		theme.SetIcon("unchecked", "PopupMenu", transparentIcon);
		theme.SetIcon("radio_unchecked", "PopupMenu", transparentIcon);
		return theme;
	}

	private static Palette CreateAccentPalette(Color accent)
	{
		return new Palette(
			new Color(accent.R * 0.16f, accent.G * 0.16f, accent.B * 0.16f, 0.62f),
			new Color(accent.R * 0.34f, accent.G * 0.34f, accent.B * 0.34f, 0.84f),
			new Color(accent.R, accent.G, accent.B, 0.42f),
			new Color(accent.R, accent.G, accent.B, 0.86f),
			new Color(0.88f, 0.91f, 0.92f, 0.96f),
			new Color(0.97f, 1.0f, 1.0f, 1.0f),
			new Color(0.52f, 0.56f, 0.58f, 0.66f));
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

	private static Texture2D GetTransparentOptionArrow()
	{
		if (_transparentOptionArrow != null)
		{
			return _transparentOptionArrow;
		}

		Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
		image.SetPixel(0, 0, Colors.Transparent);
		_transparentOptionArrow = ImageTexture.CreateFromImage(image);
		return _transparentOptionArrow;
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
