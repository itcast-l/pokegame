using Godot;

public partial class AdventureUpgradeChoiceArt : Control
{
	[Export] public FontFile? HeadingFont { get; set; }
	[Export] public FontFile? BodyFont { get; set; }
	[Export] public Texture2D? PreviewCardTexture { get; set; }
	[Export] public string PreviewSuit { get; set; } = string.Empty;
	[Export] public string PreviewTier { get; set; } = string.Empty;
	[Export] public string PreviewTitle { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string PreviewDescription { get; set; } = string.Empty;
	[Export] public Color PreviewTitleColor { get; set; } = new(0.93f, 0.96f, 1.0f, 0.99f);
	[Export] public Color PreviewGlowColor { get; set; } = new(0.48f, 0.92f, 1.0f, 0.06f);
	[Export] public Color PreviewRuleColor { get; set; } = new(0.98f, 0.9f, 0.7f, 0.08f);

	private Texture2D? _cardTexture;
	private string _title = string.Empty;
	private string _description = string.Empty;
	private bool _highlighted;
	private Color _titleColor = new(0.93f, 0.96f, 1.0f, 0.99f);
	private Color _glowColor = new(0.48f, 0.92f, 1.0f, 0.06f);
	private Color _ruleColor = new(0.98f, 0.9f, 0.7f, 0.08f);
	private readonly StyleBoxFlat _highlightStyle = new();
	private readonly StyleBoxFlat _descriptionWellStyle = new();
	private static readonly Rect2 DescriptionBandNormalizedRect = new(0.115f, 0.805f, 0.77f, 0.105f);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		if (_cardTexture == null)
		{
			_cardTexture = ResolvePreviewCardTexture();
			_title = PreviewTitle.Trim();
			_description = PreviewDescription.Trim();
		}
		_titleColor = PreviewTitleColor;
		_glowColor = PreviewGlowColor;
		_ruleColor = PreviewRuleColor;
		ConfigureHighlightStyle();
		ConfigureDescriptionWellStyle();
	}

	public void ConfigureChoice(Texture2D? cardTexture, string title, string description, bool highlighted)
	{
		_cardTexture = cardTexture;
		_title = string.Empty;
		_description = description?.Trim() ?? string.Empty;
		_highlighted = highlighted;
		QueueRedraw();
	}

	public void SetHighlighted(bool highlighted)
	{
		if (_highlighted == highlighted)
		{
			return;
		}

		_highlighted = highlighted;
		QueueRedraw();
	}

	public void SetPalette(Color titleColor, Color glowColor, Color ruleColor)
	{
		_titleColor = titleColor;
		_glowColor = glowColor;
		_ruleColor = ruleColor;
		ConfigureHighlightStyle();
		ConfigureDescriptionWellStyle();
		QueueRedraw();
	}

	public Godot.Collections.Dictionary DebugGetDescriptionBand()
	{
		Rect2 rect = GetDescriptionBandRect();
		return new Godot.Collections.Dictionary
		{
			["x"] = rect.Position.X,
			["y"] = rect.Position.Y,
			["width"] = rect.Size.X,
			["height"] = rect.Size.Y,
		};
	}

	public string DebugGetTitleText()
	{
		return _title;
	}

	public string DebugGetDescriptionText()
	{
		return _description;
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		Rect2 rect = new(Vector2.Zero, Size);
		if (_highlighted)
		{
			DrawStyleBox(_highlightStyle, rect.Grow(2.0f));
		}

		if (_cardTexture != null)
		{
			Color cardTint = _highlighted
				? new Color(0.98f, 0.99f, 1.0f, 1.0f)
				: new Color(0.88f, 0.91f, 0.96f, 0.93f);
			DrawTextureRect(_cardTexture, rect, false, cardTint);
		}

		if (_highlighted)
		{
			DrawRect(
				new Rect2(Size.X * 0.06f, Size.Y * 0.05f, Size.X * 0.88f, Size.Y * 0.9f),
				_glowColor,
				false);
			DrawCircle(
				new Vector2(Size.X * 0.5f, Size.Y * 0.42f),
				Size.X * 0.19f,
				new Color(_glowColor.R, _glowColor.G, _glowColor.B, _glowColor.A * 0.58f));
		}

		Font bodyFont = BodyFont ?? ThemeDB.FallbackFont;
		int bodyFontSize = GetDescriptionFontSize(_description);

		Rect2 descriptionRect = GetDescriptionBandRect();
		DrawDescriptionWell(descriptionRect);
		DrawEmbossedWrappedCenteredText(
			bodyFont,
			bodyFontSize,
			_description,
			descriptionRect,
			new Color(0.97f, 0.99f, 1.0f, 0.96f),
			new Color(0, 0, 0, 0.76f),
			new Color(_ruleColor.R, _ruleColor.G, _ruleColor.B, _ruleColor.A),
			1.0f);
	}

	private void DrawEmbossedCenteredText(Font font, int fontSize, string text, Rect2 rect, Color color, Color shadowColor, Color highlightColor)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		Vector2 size = font.GetStringSize(text, HorizontalAlignment.Center, -1, fontSize);
		Vector2 position = new(rect.Position.X + rect.Size.X * 0.5f - size.X * 0.5f, rect.Position.Y + rect.Size.Y * 0.5f + size.Y * 0.36f);
		DrawString(font, position + new Vector2(0, 1), text, HorizontalAlignment.Left, -1, fontSize, shadowColor);
		DrawString(font, position + new Vector2(1, 0), text, HorizontalAlignment.Left, -1, fontSize, new Color(shadowColor, shadowColor.A * 0.34f));
		DrawString(font, position, text, HorizontalAlignment.Left, -1, fontSize, color);
		DrawString(font, position + new Vector2(0, -1), text, HorizontalAlignment.Left, -1, fontSize, highlightColor);
	}

	private void DrawEmbossedWrappedCenteredText(Font font, int fontSize, string text, Rect2 rect, Color color, Color shadowColor, Color highlightColor, float lineSpacing)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		string[] lines = WrapLines(font, fontSize, text, rect.Size.X);
		float lineHeight = font.GetHeight(fontSize) + lineSpacing;
		float totalHeight = lines.Length * lineHeight;
		float startY = rect.Position.Y + (rect.Size.Y - totalHeight) * 0.5f + font.GetAscent(fontSize);

		for (int i = 0; i < lines.Length; i++)
		{
			string line = lines[i];
			Vector2 size = font.GetStringSize(line, HorizontalAlignment.Center, -1, fontSize);
			Vector2 position = new(rect.Position.X + rect.Size.X * 0.5f - size.X * 0.5f, startY + i * lineHeight);
			DrawString(font, position + new Vector2(0, 1), line, HorizontalAlignment.Left, -1, fontSize, shadowColor);
			DrawString(font, position + new Vector2(1, 0), line, HorizontalAlignment.Left, -1, fontSize, new Color(shadowColor, shadowColor.A * 0.28f));
			DrawString(font, position, line, HorizontalAlignment.Left, -1, fontSize, color);
			DrawString(font, position + new Vector2(0, -1), line, HorizontalAlignment.Left, -1, fontSize, highlightColor);
		}
	}

	private static string[] WrapLines(Font font, int fontSize, string text, float maxWidth)
	{
		string[] words = text.Split(' ');
		if (words.Length <= 1)
		{
			return BreakLongString(font, fontSize, text, maxWidth);
		}

		var lines = new System.Collections.Generic.List<string>();
		string current = string.Empty;
		foreach (string word in words)
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

		if (lines.Count == 0)
		{
			return BreakLongString(font, fontSize, text, maxWidth);
		}

		for (int i = 0; i < lines.Count; i++)
		{
			if (font.GetStringSize(lines[i], HorizontalAlignment.Left, -1, fontSize).X > maxWidth)
			{
				return BreakLongString(font, fontSize, text, maxWidth);
			}
		}

		return lines.ToArray();
	}

	private static string[] BreakLongString(Font font, int fontSize, string text, float maxWidth)
	{
		var lines = new System.Collections.Generic.List<string>();
		var current = new System.Text.StringBuilder();
		foreach (char character in text)
		{
			string candidate = current.ToString() + character;
			if (font.GetStringSize(candidate, HorizontalAlignment.Left, -1, fontSize).X <= maxWidth || current.Length == 0)
			{
				current.Append(character);
				continue;
			}

			lines.Add(current.ToString());
			current.Clear();
			current.Append(character);
		}

		if (current.Length > 0)
		{
			lines.Add(current.ToString());
		}

		return lines.ToArray();
	}

	private static int GetDescriptionFontSize(string description)
	{
		int length = string.IsNullOrWhiteSpace(description) ? 0 : description.Length;
		if (length >= 34)
		{
			return 8;
		}

		if (length >= 24)
		{
			return 10;
		}

		return 11;
	}

	private Texture2D? ResolvePreviewCardTexture()
	{
		if (!string.IsNullOrWhiteSpace(PreviewSuit) && !string.IsNullOrWhiteSpace(PreviewTier))
		{
			Texture2D? resolvedTexture = AdventureUpgradeCardVisualResolver.LoadAtlasTexture(PreviewSuit, PreviewTier);
			if (resolvedTexture != null)
			{
				return resolvedTexture;
			}
		}

		return PreviewCardTexture;
	}

	private void DrawDescriptionWell(Rect2 rect)
	{
		Rect2 wellRect = new(rect.Position.X - 10.0f, rect.Position.Y - 8.0f, rect.Size.X + 20.0f, rect.Size.Y + 14.0f);
		DrawStyleBox(_descriptionWellStyle, wellRect);
		DrawLine(
			new Vector2(wellRect.Position.X + 18.0f, wellRect.Position.Y + 2.0f),
			new Vector2(wellRect.End.X - 18.0f, wellRect.Position.Y + 2.0f),
			_ruleColor,
			1.0f);
	}

	private Rect2 GetDescriptionBandRect()
	{
		return new Rect2(
			Size.X * DescriptionBandNormalizedRect.Position.X,
			Size.Y * DescriptionBandNormalizedRect.Position.Y,
			Size.X * DescriptionBandNormalizedRect.Size.X,
			Size.Y * DescriptionBandNormalizedRect.Size.Y);
	}

	private void ConfigureHighlightStyle()
	{
		_highlightStyle.DrawCenter = false;
		_highlightStyle.BgColor = new Color(1, 1, 1, 0);
		_highlightStyle.BorderColor = new Color(_glowColor.R, _glowColor.G, _glowColor.B, 0.22f);
		_highlightStyle.BorderWidthLeft = 1;
		_highlightStyle.BorderWidthTop = 1;
		_highlightStyle.BorderWidthRight = 1;
		_highlightStyle.BorderWidthBottom = 1;
		_highlightStyle.CornerRadiusTopLeft = 12;
		_highlightStyle.CornerRadiusTopRight = 12;
		_highlightStyle.CornerRadiusBottomRight = 12;
		_highlightStyle.CornerRadiusBottomLeft = 12;
		_highlightStyle.ShadowColor = new Color(_glowColor.R, _glowColor.G, _glowColor.B, 0.04f);
		_highlightStyle.ShadowSize = 3;
	}

	private void ConfigureDescriptionWellStyle()
	{
		_descriptionWellStyle.BgColor = new Color(0.01f, 0.03f, 0.05f, 0.14f);
		_descriptionWellStyle.DrawCenter = true;
		_descriptionWellStyle.CornerRadiusTopLeft = 10;
		_descriptionWellStyle.CornerRadiusTopRight = 10;
		_descriptionWellStyle.CornerRadiusBottomRight = 10;
		_descriptionWellStyle.CornerRadiusBottomLeft = 10;
		_descriptionWellStyle.BorderWidthLeft = 0;
		_descriptionWellStyle.BorderWidthTop = 0;
		_descriptionWellStyle.BorderWidthRight = 0;
		_descriptionWellStyle.BorderWidthBottom = 0;
		_descriptionWellStyle.ShadowSize = 0;
	}
}
