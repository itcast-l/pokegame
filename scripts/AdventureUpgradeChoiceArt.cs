using Godot;

public partial class AdventureUpgradeChoiceArt : Control
{
	private const string PerspectiveShaderPath = "res://shaders/adventure_upgrade_card_perspective.gdshader";
	private const float PerspectiveVerticalCompression = 0.96f;
	private const float PerspectiveTopWidthRatio = 0.97f;
	private const float PerspectiveBottomWidthRatio = 1.0f;
	private const float PerspectivePlaneRotationDegrees = 0.0f;
	private const float DescriptionLineSpacing = 1.0f;
	private const int MaxDescriptionFontSize = 17;
	private const int MinDescriptionFontSize = 11;
	private static readonly Rect2 SuitBadgeNormalizedRect = new(0.18f, 0.035f, 0.64f, 0.42f);
	private static readonly Rect2 DescriptionPanelNormalizedRect = new(0.105f, 0.505f, 0.79f, 0.285f);
	private static readonly Rect2 TitlePanelNormalizedRect = new(0.115f, 0.425f, 0.77f, 0.085f);
	private static readonly Rect2 TitleBandNormalizedRect = new(0.14f, 0.432f, 0.72f, 0.065f);
	private static readonly Rect2 DescriptionBandNormalizedRect = new(0.135f, 0.545f, 0.73f, 0.205f);
	private static readonly Rect2 RarityPanelNormalizedRect = new(0.235f, 0.79f, 0.53f, 0.068f);
	private static readonly Rect2 RarityBandNormalizedRect = new(0.265f, 0.798f, 0.47f, 0.05f);

	[Export] public FontFile? HeadingFont { get; set; }
	[Export] public FontFile? BodyFont { get; set; }
	[Export] public Texture2D? PreviewCardTexture { get; set; }
	[Export] public Texture2D? PreviewSuitTexture { get; set; }
	[Export] public string PreviewSuit { get; set; } = string.Empty;
	[Export] public string PreviewTier { get; set; } = string.Empty;
	[Export] public string PreviewTitle { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string PreviewDescription { get; set; } = string.Empty;
	[Export] public string PreviewRarityLabel { get; set; } = string.Empty;
	[Export] public Color PreviewTitleColor { get; set; } = new(0.93f, 0.96f, 1.0f, 0.99f);
	[Export] public Color PreviewGlowColor { get; set; } = new(0.34f, 0.92f, 1.0f, 1.0f);
	[Export] public Color PreviewRuleColor { get; set; } = new(0.98f, 0.9f, 0.7f, 0.08f);

	private Panel _groundShadowLayer = default!;
	private Panel _selectionGlowLayer = default!;
	private readonly TextureRect[] _thicknessLayers = new TextureRect[3];
	private TextureRect _cardSurfaceLayer = default!;
	private TextureRect _suitShadowLayer = default!;
	private TextureRect _suitBadgeLayer = default!;
	private Panel _descriptionPanel = default!;
	private Panel _titlePanel = default!;
	private Label _titleLayer = default!;
	private Label _descriptionLayer = default!;
	private Panel _rarityPanel = default!;
	private Label _rarityLayer = default!;
	private ShaderMaterial? _perspectiveMaterial;
	private ShaderMaterial? _suitCleanupMaterial;
	private StyleBoxFlat _groundShadowStyle = default!;
	private StyleBoxFlat _selectionGlowStyle = default!;
	private StyleBoxFlat _descriptionPanelStyle = default!;
	private StyleBoxFlat _titlePanelStyle = default!;
	private StyleBoxFlat _rarityPanelStyle = default!;
	private Texture2D? _cardTexture;
	private Texture2D? _suitTexture;
	private string _title = string.Empty;
	private string _description = string.Empty;
	private string _rarityLabel = string.Empty;
	private float _highlightAmount;
	private float _elevationAmount;
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

	public float ElevationAmount => _elevationAmount;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		BuildLayers();
		if (_cardTexture == null)
		{
			_cardTexture = ResolvePreviewCardTexture();
			_suitTexture = ResolvePreviewSuitTexture();
			_title = PreviewTitle.Trim();
			_description = PreviewDescription.Trim();
			_rarityLabel = PreviewRarityLabel.Trim();
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
		Texture2D? suitTexture,
		string title,
		string description,
		bool highlighted)
	{
		_cardTexture = cardTexture;
		_suitTexture = suitTexture;
		_title = title?.Trim() ?? string.Empty;
		_description = description?.Trim() ?? string.Empty;
		ApplyContent();
		LayoutLayers();
		HighlightAmount = highlighted ? 1.0f : 0.0f;
	}

	public void SetRarityLabel(string rarityLabel)
	{
		_rarityLabel = rarityLabel?.Trim() ?? string.Empty;
		if (_rarityLayer != null)
		{
			_rarityLayer.Text = _rarityLabel;
		}
	}

	public void SetHighlighted(bool highlighted) => HighlightAmount = highlighted ? 1.0f : 0.0f;
	public void SetHighlightAmount(float amount) => HighlightAmount = amount;

	public void SetElevationAmount(float amount)
	{
		float clamped = Mathf.Clamp(amount, 0.0f, 1.0f);
		if (Mathf.IsEqualApprox(_elevationAmount, clamped))
		{
			return;
		}
		_elevationAmount = clamped;
		LayoutLayers();
		UpdateVisualState();
	}

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
	public string DebugGetRarityText() => _rarityLabel;
	public int DebugGetResolvedDescriptionFontSize() => ResolveDescriptionFontSize();

	public Godot.Collections.Dictionary DebugGetPseudo3DState()
	{
		return new Godot.Collections.Dictionary
		{
			["shader_path"] = _perspectiveMaterial?.Shader?.ResourcePath ?? string.Empty,
			["vertical_compression"] = ReadShaderFloat("vertical_compression"),
			["top_width_ratio"] = ReadShaderFloat("top_width_ratio"),
			["bottom_width_ratio"] = ReadShaderFloat("bottom_width_ratio"),
			["plane_rotation_degrees"] = ReadShaderFloat("plane_rotation_degrees"),
			["description_has_material"] = _descriptionLayer.Material != null,
			["thickness_layer_count"] = _thicknessLayers.Length,
			["elevation"] = _elevationAmount,
			["highlight"] = _highlightAmount,
			["shadow_offset_y"] = _groundShadowLayer.Position.Y - GetCardDrawRect().Position.Y,
			["shadow_alpha"] = _groundShadowStyle.ShadowColor.A,
			["glow_alpha"] = _selectionGlowStyle.BorderColor.A,
			["surface_has_texture"] = _cardSurfaceLayer.Texture != null,
			["suit_has_texture"] = _suitBadgeLayer.Texture != null,
			["suit_is_separate_layer"] = _suitBadgeLayer.Texture != null && !ReferenceEquals(_suitBadgeLayer.Texture, _cardSurfaceLayer.Texture),
			["card_texture_path"] = _cardSurfaceLayer.Texture?.ResourcePath ?? string.Empty,
			["suit_texture_path"] = _suitBadgeLayer.Texture?.ResourcePath ?? string.Empty,
			["suit_shadow_alpha"] = _suitShadowLayer.Modulate.A,
			["suit_badge_rect"] = RectToDictionary(new Rect2(_suitBadgeLayer.Position, _suitBadgeLayer.Size)),
			["thickness_alpha_1"] = _thicknessLayers[0].Modulate.A,
			["thickness_alpha_2"] = _thicknessLayers[1].Modulate.A,
			["thickness_alpha_3"] = _thicknessLayers[2].Modulate.A,
			["surface_material_id"] = _cardSurfaceLayer.Material?.GetInstanceId() ?? 0UL,
			["glow_material_id"] = 0UL,
			["glow_uses_outline_panel"] = true,
			["description_panel_alpha"] = _descriptionPanelStyle.BgColor.A,
		};
	}

	private void BuildLayers()
	{
		_perspectiveMaterial = CreatePerspectiveMaterial();
		_suitCleanupMaterial = CreateSuitCleanupMaterial();

		_groundShadowStyle = CreateRoundedStyle(new Color(0.01f, 0.025f, 0.035f, 0.02f), 9);
		_groundShadowStyle.ShadowColor = new Color(0.0f, 0.01f, 0.02f, 0.30f);
		_groundShadowStyle.ShadowSize = 14;
		_groundShadowStyle.ShadowOffset = new Vector2(0.0f, 8.0f);
		_groundShadowLayer = CreatePanelLayer("GroundShadowLayer", -6, _groundShadowStyle);

		_selectionGlowStyle = CreateRoundedStyle(Colors.Transparent, 9);
		_selectionGlowStyle.SetBorderWidthAll(2);
		_selectionGlowStyle.ExpandMarginLeft = 1.0f;
		_selectionGlowStyle.ExpandMarginTop = 1.0f;
		_selectionGlowStyle.ExpandMarginRight = 1.0f;
		_selectionGlowStyle.ExpandMarginBottom = 1.0f;
		_selectionGlowStyle.ShadowSize = 7;
		_selectionGlowLayer = CreatePanelLayer("SelectionGlowLayer", -5, _selectionGlowStyle);

		for (int index = 0; index < _thicknessLayers.Length; index++)
		{
			_thicknessLayers[index] = CreateTextureLayer($"ThicknessLayer{index + 1}", -4 + index, _perspectiveMaterial);
		}
		_cardSurfaceLayer = CreateTextureLayer("CardSurfaceLayer", 0, _perspectiveMaterial);
		_suitShadowLayer = CreateTextureLayer("SuitShadowLayer", 1, null);
		_suitBadgeLayer = CreateTextureLayer("SuitBadgeLayer", 2, _suitCleanupMaterial);

		_descriptionPanelStyle = CreateRoundedStyle(new Color(0.56f, 0.58f, 0.55f, 0.88f), 4);
		_descriptionPanelStyle.BorderColor = new Color(0.09f, 0.13f, 0.14f, 0.82f);
		_descriptionPanelStyle.BorderWidthTop = 1;
		_descriptionPanelStyle.BorderWidthBottom = 1;
		_descriptionPanel = CreatePanelLayer("DescriptionPanel", 2, _descriptionPanelStyle);

		_titlePanelStyle = CreateRoundedStyle(new Color(0.055f, 0.085f, 0.09f, 0.94f), 3);
		_titlePanelStyle.BorderColor = new Color(0.72f, 0.66f, 0.48f, 0.52f);
		_titlePanelStyle.SetBorderWidthAll(1);
		_titlePanel = CreatePanelLayer("TitlePanel", 3, _titlePanelStyle);
		_titleLayer = CreateTextLayer("TitleLayer", 4, HeadingFont ?? BodyFont, HorizontalAlignment.Center);
		_titleLayer.VerticalAlignment = VerticalAlignment.Center;
		_titleLayer.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		_titleLayer.AddThemeFontSizeOverride("font_size", 18);
		_titleLayer.AddThemeColorOverride("font_color", new Color(0.94f, 0.88f, 0.72f, 1.0f));
		_titleLayer.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.01f, 0.015f, 0.9f));
		_titleLayer.AddThemeConstantOverride("shadow_offset_y", 1);

		_descriptionLayer = CreateTextLayer("DescriptionLayer", 4, BodyFont, HorizontalAlignment.Center);
		_descriptionLayer.VerticalAlignment = VerticalAlignment.Center;
		_descriptionLayer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_descriptionLayer.AddThemeColorOverride("font_color", new Color(0.035f, 0.055f, 0.06f, 1.0f));
		_descriptionLayer.AddThemeColorOverride("font_shadow_color", new Color(1.0f, 1.0f, 0.96f, 0.38f));
		_descriptionLayer.AddThemeConstantOverride("shadow_offset_y", 1);

		_rarityPanelStyle = CreateRoundedStyle(new Color(0.08f, 0.105f, 0.105f, 0.96f), 3);
		_rarityPanelStyle.BorderColor = new Color(0.65f, 0.62f, 0.48f, 0.72f);
		_rarityPanelStyle.SetBorderWidthAll(1);
		_rarityPanel = CreatePanelLayer("RarityPanel", 3, _rarityPanelStyle);
		_rarityLayer = CreateTextLayer("RarityLayer", 4, HeadingFont ?? BodyFont, HorizontalAlignment.Center);
		_rarityLayer.VerticalAlignment = VerticalAlignment.Center;
		_rarityLayer.AddThemeFontSizeOverride("font_size", 13);
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

	private static ShaderMaterial? CreatePerspectiveMaterial()
	{
		return CreateTextureMaterial(
			PerspectiveVerticalCompression,
			PerspectiveTopWidthRatio,
			PerspectiveBottomWidthRatio,
			PerspectivePlaneRotationDegrees);
	}

	private static ShaderMaterial? CreateSuitCleanupMaterial()
	{
		return CreateTextureMaterial(1.0f, 1.0f, 1.0f, 0.0f);
	}

	private static ShaderMaterial? CreateTextureMaterial(
		float verticalCompression,
		float topWidthRatio,
		float bottomWidthRatio,
		float planeRotationDegrees)
	{
		Shader? shader = GD.Load<Shader>(PerspectiveShaderPath);
		if (shader == null)
		{
			GD.PushWarning($"Missing adventure upgrade perspective shader: {PerspectiveShaderPath}");
			return null;
		}
		var material = new ShaderMaterial { Shader = shader };
		material.SetShaderParameter("vertical_compression", verticalCompression);
		material.SetShaderParameter("top_width_ratio", topWidthRatio);
		material.SetShaderParameter("bottom_width_ratio", bottomWidthRatio);
		material.SetShaderParameter("plane_rotation_degrees", planeRotationDegrees);
		return material;
	}

	private void ApplyContent()
	{
		if (_groundShadowLayer == null)
		{
			return;
		}
		foreach (TextureRect layer in _thicknessLayers)
		{
			layer.Texture = _cardTexture;
		}
		_cardSurfaceLayer.Texture = _cardTexture;
		_suitShadowLayer.Texture = _suitTexture;
		_suitBadgeLayer.Texture = _suitTexture;
		_titleLayer.Text = _title;
		_descriptionLayer.Text = _description;
		_rarityLayer.Text = _rarityLabel;
	}

	private void LayoutLayers()
	{
		if (_groundShadowLayer == null || Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}
		Rect2 cardRect = GetCardDrawRect();
		_groundShadowLayer.Position = cardRect.Position + new Vector2(0.0f, 8.0f + 8.0f * _elevationAmount);
		_groundShadowLayer.Size = cardRect.Size;
		float glowInsetX = cardRect.Size.X * 0.075f;
		float glowInsetTop = cardRect.Size.Y * 0.105f;
		float glowInsetBottom = cardRect.Size.Y * 0.075f;
		_selectionGlowLayer.Position = cardRect.Position + new Vector2(glowInsetX, glowInsetTop);
		_selectionGlowLayer.Size = new Vector2(
			cardRect.Size.X - glowInsetX * 2.0f,
			cardRect.Size.Y - glowInsetTop - glowInsetBottom);

		for (int index = 0; index < _thicknessLayers.Length; index++)
		{
			TextureRect layer = _thicknessLayers[index];
			layer.Position = cardRect.Position + new Vector2(0.0f, 4.0f + index * 3.0f + 2.0f * _elevationAmount);
			layer.Size = cardRect.Size;
		}
		_cardSurfaceLayer.Position = cardRect.Position;
		_cardSurfaceLayer.Size = cardRect.Size;

		Rect2 suitRect = GetNormalizedRect(cardRect, SuitBadgeNormalizedRect);
		float badgeLift = 5.0f * _elevationAmount;
		_suitShadowLayer.Position = suitRect.Position + new Vector2(5.0f, 8.0f + 7.0f * _elevationAmount);
		_suitShadowLayer.Size = suitRect.Size;
		_suitBadgeLayer.Position = suitRect.Position + new Vector2(0.0f, -badgeLift);
		_suitBadgeLayer.Size = suitRect.Size;
		_suitCleanupMaterial?.SetShaderParameter("rect_size", suitRect.Size);

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
		Rect2 rarityPanelRect = GetNormalizedRect(cardRect, RarityPanelNormalizedRect);
		_rarityPanel.Position = rarityPanelRect.Position;
		_rarityPanel.Size = rarityPanelRect.Size;
		Rect2 rarityRect = GetNormalizedRect(cardRect, RarityBandNormalizedRect);
		_rarityLayer.Position = rarityRect.Position;
		_rarityLayer.Size = rarityRect.Size;
		_perspectiveMaterial?.SetShaderParameter("rect_size", cardRect.Size);
	}

	private void UpdateVisualState()
	{
		if (_groundShadowLayer == null)
		{
			return;
		}
		_groundShadowStyle.ShadowColor = new Color(0.0f, 0.01f, 0.02f, 0.30f + 0.18f * _elevationAmount);
		_groundShadowStyle.ShadowSize = Mathf.RoundToInt(14.0f + 5.0f * _elevationAmount);
		for (int index = 0; index < _thicknessLayers.Length; index++)
		{
			float alpha = (0.40f - index * 0.08f) + 0.08f * _elevationAmount;
			_thicknessLayers[index].Modulate = new Color(0.025f, 0.045f, 0.06f, alpha);
		}
		_cardSurfaceLayer.Modulate = new Color(0.98f, 0.99f, 1.0f, 1.0f).Lerp(Colors.White, _highlightAmount);
		_suitShadowLayer.Modulate = new Color(0.0f, 0.015f, 0.025f, 0.28f + 0.18f * _elevationAmount);
		_suitBadgeLayer.Modulate = new Color(0.96f, 0.98f, 1.0f, 1.0f).Lerp(Colors.White, _highlightAmount);

		float glowAlpha = _highlightAmount * (0.64f + 0.30f * _elevationAmount);
		_selectionGlowStyle.BorderColor = new Color(_glowColor.R, _glowColor.G, _glowColor.B, glowAlpha);
		_selectionGlowStyle.ShadowColor = new Color(_glowColor.R, _glowColor.G, _glowColor.B, glowAlpha * 0.24f);
		_selectionGlowLayer.Visible = glowAlpha > 0.001f;
		_rarityLayer.AddThemeColorOverride("font_color", new Color(_accentColor.R, _accentColor.G, _accentColor.B, 0.96f));
		_rarityPanelStyle.BorderColor = new Color(_accentColor.R, _accentColor.G, _accentColor.B, 0.72f);
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
		return PreviewCardTexture ?? AdventureUpgradeChoiceVisualResolver.LoadCardBaseTexture();
	}

	private Texture2D? ResolvePreviewSuitTexture()
	{
		return PreviewSuitTexture ?? AdventureUpgradeChoiceVisualResolver.LoadSuitTexture(PreviewSuit);
	}

	private float ReadShaderFloat(string parameter) => _perspectiveMaterial?.GetShaderParameter(parameter).AsSingle() ?? 0.0f;

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
