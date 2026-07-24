using Godot;

public partial class AdventureUpgradeChoiceArt : Control
{
	private const float DescriptionLineSpacing = 1.0f;
	private const int MaxDescriptionFontSize = 17;
	private const int MinDescriptionFontSize = 11;
	private static readonly Rect2 DescriptionPanelNormalizedRect = new(0.11f, 0.515f, 0.78f, 0.25f);
	private static readonly Rect2 TitlePanelNormalizedRect = new(0.12f, 0.43f, 0.76f, 0.075f);
	private static readonly Rect2 TitleBandNormalizedRect = new(0.15f, 0.435f, 0.70f, 0.057f);
	private static readonly Rect2 DescriptionBandNormalizedRect = new(0.14f, 0.545f, 0.72f, 0.18f);

	[Export] public FontFile? HeadingFont { get; set; }
	[Export] public FontFile? BodyFont { get; set; }
	[Export] public Texture2D? PreviewCardTexture { get; set; }
	[Export] public string PreviewSuit { get; set; } = string.Empty;
	[Export] public string PreviewTier { get; set; } = string.Empty;
	[Export] public string PreviewTitle { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string PreviewDescription { get; set; } = string.Empty;
	[Export] public Color PreviewTitleColor { get; set; } = new(0.93f, 0.96f, 1.0f, 0.99f);
	[Export] public Color PreviewGlowColor { get; set; } = new(0.34f, 0.92f, 1.0f, 1.0f);
	[Export] public Color PreviewRuleColor { get; set; } = new(0.98f, 0.9f, 0.7f, 0.08f);

	private Panel _groundShadowLayer = default!;
	private Panel _selectionGlowLayer = default!;
	private TextureRect _cardSurfaceLayer = default!;
	private Panel _descriptionPanel = default!;
	private Panel _titlePanel = default!;
	private Label _titleLayer = default!;
	private Label _descriptionLayer = default!;
	private StyleBoxFlat _groundShadowStyle = default!;
	private StyleBoxFlat _selectionGlowStyle = default!;
	private StyleBoxFlat _descriptionPanelStyle = default!;
	private StyleBoxFlat _titlePanelStyle = default!;
	private Texture2D? _cardTexture;
	private string _title = string.Empty;
	private string _description = string.Empty;
	private float _highlightAmount;
	private Color _accentColor = new(0.52f, 0.93f, 1.0f, 1.0f);
	private Color _glowColor = new(0.34f, 0.92f, 1.0f, 1.0f);

	public float HighlightAmount
	{
		get => _highlightAmount;
		set
		{
			float clamped = Mathf.Clamp(value, 0.0f, 1.0f);
			if (Mathf.IsEqualApprox(_highlightAmount, clamped))
			{
				return;
			}
			_highlightAmount = clamped;
			UpdateVisualState();
		}
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		BuildLayers();
		if (_cardTexture == null)
		{
			_cardTexture = ResolvePreviewCardTexture();
			_title = PreviewTitle.Trim();
			_description = PreviewDescription.Trim();
		}
		_accentColor = PreviewTitleColor;
		_glowColor = PreviewGlowColor;
		Resized += LayoutLayers;
		ApplyContent();
		LayoutLayers();
		UpdateVisualState();
	}

	public void ConfigureChoice(
		Texture2D? cardTexture,
		string title,
		string description,
		bool highlighted)
	{
		_cardTexture = cardTexture;
		_title = title?.Trim() ?? string.Empty;
		_description = description?.Trim() ?? string.Empty;
		ApplyContent();
		LayoutLayers();
		HighlightAmount = highlighted ? 1.0f : 0.0f;
	}

	public void SetHighlighted(bool highlighted) => HighlightAmount = highlighted ? 1.0f : 0.0f;
	public void SetHighlightAmount(float amount) => HighlightAmount = amount;

	public void SetPalette(Color titleColor, Color glowColor, Color ruleColor)
	{
		_accentColor = titleColor;
		_glowColor = glowColor;
		UpdateVisualState();
	}

	public Godot.Collections.Dictionary DebugGetDescriptionBand() => RectToDictionary(GetDescriptionBandRect());
	public Godot.Collections.Dictionary DebugGetCardDrawRect() => RectToDictionary(GetCardDrawRect());
	public string DebugGetTitleText() => _title;
	public string DebugGetDescriptionText() => _description;
	public int DebugGetResolvedDescriptionFontSize() => ResolveDescriptionFontSize();

	public Godot.Collections.Dictionary DebugGetCardVisualState()
	{
		return new Godot.Collections.Dictionary
		{
			["surface_shader_path"] = string.Empty,
			["surface_is_flat"] = true,
			["thickness_layer_count"] = 0,
			["highlight"] = _highlightAmount,
			["shadow_offset_y"] = _groundShadowLayer.Position.Y - GetCardDrawRect().Position.Y,
			["shadow_alpha"] = _groundShadowStyle.ShadowColor.A,
			["glow_alpha"] = _selectionGlowStyle.BorderColor.A,
			["surface_has_texture"] = _cardSurfaceLayer.Texture != null,
			["uses_complete_card_texture"] = _cardSurfaceLayer.Texture != null,
			["suit_is_separate_layer"] = false,
			["card_texture_path"] = GetTextureSourcePath(_cardSurfaceLayer.Texture),
			["surface_material_id"] = _cardSurfaceLayer.Material?.GetInstanceId() ?? 0UL,
			["glow_material_id"] = 0UL,
			["glow_uses_outline_panel"] = true,
			["description_panel_alpha"] = _descriptionPanelStyle.BgColor.A,
		};
	}

	private void BuildLayers()
	{
		_groundShadowStyle = CreateRoundedStyle(new Color(0.01f, 0.025f, 0.035f, 0.02f), 9);
		_groundShadowStyle.ShadowColor = new Color(0.0f, 0.01f, 0.02f, 0.28f);
		_groundShadowStyle.ShadowSize = 10;
		_groundShadowStyle.ShadowOffset = new Vector2(0.0f, 6.0f);
		_groundShadowLayer = CreatePanelLayer("GroundShadowLayer", -6, _groundShadowStyle);

		_selectionGlowStyle = CreateRoundedStyle(Colors.Transparent, 9);
		_selectionGlowStyle.SetBorderWidthAll(2);
		_selectionGlowStyle.ExpandMarginLeft = 1.0f;
		_selectionGlowStyle.ExpandMarginTop = 1.0f;
		_selectionGlowStyle.ExpandMarginRight = 1.0f;
		_selectionGlowStyle.ExpandMarginBottom = 1.0f;
		_selectionGlowStyle.ShadowSize = 6;
		_selectionGlowLayer = CreatePanelLayer("SelectionGlowLayer", -5, _selectionGlowStyle);

		_cardSurfaceLayer = CreateTextureLayer("CardSurfaceLayer", 0, null);

		_descriptionPanelStyle = CreateRoundedStyle(new Color(0.025f, 0.055f, 0.06f, 0.86f), 4);
		_descriptionPanelStyle.BorderColor = new Color(0.38f, 0.72f, 0.73f, 0.24f);
		_descriptionPanelStyle.BorderWidthTop = 1;
		_descriptionPanelStyle.BorderWidthBottom = 1;
		_descriptionPanel = CreatePanelLayer("DescriptionPanel", 2, _descriptionPanelStyle);

		_titlePanelStyle = CreateRoundedStyle(new Color(0.025f, 0.055f, 0.06f, 0.94f), 3);
		_titlePanelStyle.BorderColor = new Color(0.78f, 0.69f, 0.46f, 0.46f);
		_titlePanelStyle.SetBorderWidthAll(1);
		_titlePanel = CreatePanelLayer("TitlePanel", 3, _titlePanelStyle);
		_titleLayer = CreateTextLayer("TitleLayer", 4, HeadingFont ?? BodyFont, HorizontalAlignment.Center);
		_titleLayer.VerticalAlignment = VerticalAlignment.Center;
		_titleLayer.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		_titleLayer.AddThemeFontSizeOverride("font_size", 17);
		_titleLayer.AddThemeColorOverride("font_color", new Color(0.94f, 0.88f, 0.72f, 1.0f));
		_titleLayer.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.01f, 0.015f, 0.9f));
		_titleLayer.AddThemeConstantOverride("shadow_offset_y", 1);

		_descriptionLayer = CreateTextLayer("DescriptionLayer", 4, BodyFont, HorizontalAlignment.Center);
		_descriptionLayer.VerticalAlignment = VerticalAlignment.Center;
		_descriptionLayer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_descriptionLayer.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 0.90f, 1.0f));
		_descriptionLayer.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.015f, 0.02f, 0.9f));
		_descriptionLayer.AddThemeConstantOverride("shadow_offset_y", 1);

	}

	private Panel CreatePanelLayer(string name, int zIndex, StyleBoxFlat style)
	{
		var panel = new Panel { Name = name, MouseFilter = MouseFilterEnum.Ignore, ZIndex = zIndex };
		panel.AddThemeStyleboxOverride("panel", style);
		AddChild(panel);
		return panel;
	}

	private TextureRect CreateTextureLayer(string name, int zIndex, Material? material)
	{
		var layer = new TextureRect
		{
			Name = name,
			MouseFilter = MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ZIndex = zIndex,
			Material = material,
		};
		AddChild(layer);
		return layer;
	}

	private Label CreateTextLayer(string name, int zIndex, FontFile? font, HorizontalAlignment alignment)
	{
		var label = new Label
		{
			Name = name,
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = alignment,
			ZIndex = zIndex,
		};
		if (font != null)
		{
			label.AddThemeFontOverride("font", font);
		}
		AddChild(label);
		return label;
	}

	private static StyleBoxFlat CreateRoundedStyle(Color background, int radius)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
		};
	}

	private void ApplyContent()
	{
		if (_groundShadowLayer == null)
		{
			return;
		}
		_cardSurfaceLayer.Texture = _cardTexture;
		_titleLayer.Text = _title;
		_descriptionLayer.Text = _description;
	}

	private void LayoutLayers()
	{
		if (_groundShadowLayer == null || Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}
		Rect2 cardRect = GetCardDrawRect();
		_groundShadowLayer.Position = cardRect.Position + new Vector2(0.0f, 6.0f);
		_groundShadowLayer.Size = cardRect.Size;
		float glowInsetX = cardRect.Size.X * 0.075f;
		float glowInsetTop = cardRect.Size.Y * 0.105f;
		float glowInsetBottom = cardRect.Size.Y * 0.075f;
		_selectionGlowLayer.Position = cardRect.Position + new Vector2(glowInsetX, glowInsetTop);
		_selectionGlowLayer.Size = new Vector2(
			cardRect.Size.X - glowInsetX * 2.0f,
			cardRect.Size.Y - glowInsetTop - glowInsetBottom);

		_cardSurfaceLayer.Position = cardRect.Position;
		_cardSurfaceLayer.Size = cardRect.Size;

		Rect2 panelRect = GetNormalizedRect(cardRect, DescriptionPanelNormalizedRect);
		_descriptionPanel.Position = panelRect.Position;
		_descriptionPanel.Size = panelRect.Size;
		Rect2 titlePanelRect = GetNormalizedRect(cardRect, TitlePanelNormalizedRect);
		_titlePanel.Position = titlePanelRect.Position;
		_titlePanel.Size = titlePanelRect.Size;
		Rect2 titleRect = GetNormalizedRect(cardRect, TitleBandNormalizedRect);
		_titleLayer.Position = titleRect.Position;
		_titleLayer.Size = titleRect.Size;
		Rect2 descriptionRect = GetDescriptionBandRect();
		_descriptionLayer.Position = descriptionRect.Position;
		_descriptionLayer.Size = descriptionRect.Size;
		_descriptionLayer.AddThemeFontSizeOverride("font_size", ResolveDescriptionFontSize());
	}

	private void UpdateVisualState()
	{
		if (_groundShadowLayer == null)
		{
			return;
		}
		_groundShadowStyle.ShadowColor = new Color(0.0f, 0.01f, 0.02f, 0.28f);
		_groundShadowStyle.ShadowSize = 10;
		_cardSurfaceLayer.Modulate = new Color(0.98f, 0.99f, 1.0f, 1.0f).Lerp(Colors.White, _highlightAmount);

		float glowAlpha = _highlightAmount * 0.64f;
		_selectionGlowStyle.BorderColor = new Color(_glowColor.R, _glowColor.G, _glowColor.B, glowAlpha);
		_selectionGlowStyle.ShadowColor = new Color(_glowColor.R, _glowColor.G, _glowColor.B, glowAlpha * 0.24f);
		_selectionGlowLayer.Visible = glowAlpha > 0.001f;
	}

	private Rect2 GetCardDrawRect()
	{
		return new Rect2(Vector2.Zero, Size);
	}

	private Rect2 GetDescriptionBandRect() => GetNormalizedRect(GetCardDrawRect(), DescriptionBandNormalizedRect);

	private static Rect2 GetNormalizedRect(Rect2 cardRect, Rect2 normalized)
	{
		return new Rect2(
			cardRect.Position + cardRect.Size * normalized.Position,
			cardRect.Size * normalized.Size);
	}

	private int ResolveDescriptionFontSize()
	{
		Font font = BodyFont ?? ThemeDB.FallbackFont;
		Vector2 size = GetDescriptionBandRect().Size;
		if (string.IsNullOrWhiteSpace(_description))
		{
			return MinDescriptionFontSize;
		}
		for (int fontSize = MaxDescriptionFontSize; fontSize >= MinDescriptionFontSize; fontSize--)
		{
			string[] lines = WrapLines(font, fontSize, _description, size.X);
			if (lines.Length * (font.GetHeight(fontSize) + DescriptionLineSpacing) <= size.Y)
			{
				return fontSize;
			}
		}
		return MinDescriptionFontSize;
	}

	private static string[] WrapLines(Font font, int fontSize, string text, float maxWidth)
	{
		var lines = new System.Collections.Generic.List<string>();
		string current = string.Empty;
		foreach (string word in text.Split(' '))
		{
			string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
			if (font.GetStringSize(candidate, HorizontalAlignment.Left, -1, fontSize).X <= maxWidth)
			{
				current = candidate;
				continue;
			}
			if (!string.IsNullOrEmpty(current))
			{
				lines.Add(current);
			}
			current = word;
		}
		if (!string.IsNullOrEmpty(current))
		{
			lines.Add(current);
		}
		return lines.Count == 0 ? new[] { text } : lines.ToArray();
	}

	private Texture2D? ResolvePreviewCardTexture()
	{
		return PreviewCardTexture ?? AdventureUpgradeCardVisualResolver.LoadAtlasTexture(PreviewSuit, PreviewTier);
	}

	private static string GetTextureSourcePath(Texture2D? texture)
	{
		return texture is AtlasTexture atlasTexture
			? atlasTexture.Atlas?.ResourcePath ?? string.Empty
			: texture?.ResourcePath ?? string.Empty;
	}

	private static Godot.Collections.Dictionary RectToDictionary(Rect2 rect)
	{
		return new Godot.Collections.Dictionary
		{
			["x"] = rect.Position.X,
			["y"] = rect.Position.Y,
			["width"] = rect.Size.X,
			["height"] = rect.Size.Y,
		};
	}
}
