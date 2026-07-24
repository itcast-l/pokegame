using Godot;
using System;

public partial class AdventureUpgradeCatalogCard : Button
{
	private const float BaseCardWidth = 196.0f;
	private const int MaxDescriptionFontSize = 13;
	private const int MinDescriptionFontSize = 9;

	[Export] public string PreviewSuit { get; set; } = string.Empty;
	[Export] public string PreviewRarity { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string PreviewDescription { get; set; } = string.Empty;
	[Export] public string PreviewRarityLabel { get; set; } = string.Empty;
	[Export] public bool PreviewInteractive { get; set; }

	private TextureRect _cardTexture = default!;
	private Control _placeholder = default!;
	private Control _lockedPlaceholder = default!;
	private Label _lockedLabel = default!;
	private Label _descriptionLabel = default!;
	private Label _rarityLabel = default!;
	private Panel _feedbackBorder = default!;
	private bool _interactive;
	private bool _hovered;
	private bool _isLocked;
	private bool _focusHighlightEnabled = true;
	private string _suit = string.Empty;
	private string _rarity = string.Empty;
	private string _visualTier = string.Empty;

	public string Suit => _suit;
	public string Rarity => _rarity;
	public string Description => _descriptionLabel?.Text ?? string.Empty;
	public string RarityLabel => _rarityLabel?.Text ?? string.Empty;
	public string VisualTier => _visualTier;
	public bool IsInteractive => _interactive;
	public bool IsLocked => _isLocked;
	public bool HasCardTexture => _cardTexture?.Texture != null;
	public bool FocusHighlightEnabled => _focusHighlightEnabled;

	public override void _Ready()
	{
		_cardTexture = GetNode<TextureRect>("CardTexture");
		_placeholder = GetNode<Control>("MissingTexturePlaceholder");
		_lockedPlaceholder = GetNode<Control>("LockedPlaceholder");
		_lockedLabel = GetNode<Label>("LockedPlaceholder/LockedLabel");
		_descriptionLabel = GetNode<Label>("DescriptionLayer/DescriptionMargin/DescriptionLabel");
		_rarityLabel = GetNode<Label>("RarityLabel");
		_feedbackBorder = GetNode<Panel>("FeedbackBorder");

		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;
		FocusEntered += RefreshFeedback;
		FocusExited += RefreshFeedback;
		Resized += RefreshDescriptionFontSize;
		ConfigureCard(
			PreviewSuit,
			PreviewRarity,
			PreviewDescription,
			PreviewRarityLabel,
			PreviewInteractive);
	}

	public void ConfigureCard(
		string suit,
		string rarity,
		string description,
		bool interactive)
	{
		ConfigureCard(suit, rarity, description, string.Empty, interactive);
	}

	public void ConfigureCard(
		string suit,
		string rarity,
		string description,
		string rarityLabel,
		bool interactive)
	{
		_isLocked = false;
		_suit = Normalize(suit);
		_rarity = Normalize(rarity);
		_interactive = interactive;
		_hovered = false;

		AdventureUpgradeCardVisualEntryResource? entry =
			AdventureUpgradeCardVisualResolver.ResolveEntry(_suit, _rarity);
		_visualTier = entry?.Tier?.Trim().ToLowerInvariant() ?? string.Empty;
		_cardTexture.Texture = AdventureUpgradeCardVisualResolver.LoadAtlasTextureForRarity(_suit, _rarity);
		_placeholder.Visible = !HasCardTexture;
		_lockedPlaceholder.Visible = false;

		_descriptionLabel.Text = description?.Trim() ?? string.Empty;
		_descriptionLabel.GetParent().GetParent<Control>().Visible = !string.IsNullOrWhiteSpace(_descriptionLabel.Text);
		_rarityLabel.Text = string.IsNullOrWhiteSpace(rarityLabel)
			? GetDefaultRarityLabel(_rarity)
			: rarityLabel.Trim();
		_rarityLabel.Visible = !string.IsNullOrWhiteSpace(_rarityLabel.Text);
		RefreshDescriptionFontSize();
		FocusMode = interactive ? FocusModeEnum.All : FocusModeEnum.None;
		MouseFilter = interactive ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		ButtonMask = interactive ? MouseButtonMask.Left : (MouseButtonMask)0;
		MouseDefaultCursorShape = interactive ? CursorShape.PointingHand : CursorShape.Arrow;
		ActionMode = ActionModeEnum.Release;
		Disabled = false;
		RefreshFeedback();
	}

	public void ConfigureLockedCard(string lockedLabel)
	{
		_isLocked = true;
		_interactive = false;
		_hovered = false;
		_suit = string.Empty;
		_rarity = string.Empty;
		_visualTier = string.Empty;
		_cardTexture.Texture = null;
		_placeholder.Visible = false;
		_lockedPlaceholder.Visible = true;
		_lockedLabel.Text = lockedLabel?.Trim() ?? string.Empty;
		_descriptionLabel.Text = string.Empty;
		_descriptionLabel.GetParent().GetParent<Control>().Visible = false;
		_rarityLabel.Text = string.Empty;
		_rarityLabel.Visible = false;
		FocusMode = FocusModeEnum.None;
		MouseFilter = MouseFilterEnum.Ignore;
		ButtonMask = (MouseButtonMask)0;
		MouseDefaultCursorShape = CursorShape.Arrow;
		Disabled = true;
		RefreshFeedback();
	}

	public void SetFocusHighlightEnabled(bool enabled)
	{
		_focusHighlightEnabled = enabled;
		RefreshFeedback();
	}

	public Godot.Collections.Dictionary DebugGetCardState()
	{
		Rect2 region = _cardTexture.Texture is AtlasTexture atlasTexture
			? atlasTexture.Region
			: default;
		string texturePath = _cardTexture.Texture is AtlasTexture atlas
			? atlas.Atlas?.ResourcePath ?? string.Empty
			: _cardTexture.Texture?.ResourcePath ?? string.Empty;
		return new Godot.Collections.Dictionary
		{
			["suit"] = _suit,
			["rarity"] = _rarity,
			["visual_tier"] = _visualTier,
			["description"] = Description,
			["description_font_size"] = _descriptionLabel.GetThemeFontSize("font_size"),
			["rarity_label"] = RarityLabel,
			["interactive"] = _interactive,
			["locked"] = _isLocked,
			["locked_label"] = _lockedLabel.Text,
			["has_texture"] = HasCardTexture,
			["missing_texture_placeholder_visible"] = _placeholder.Visible,
			["atlas_region"] = region,
			["card_texture_path"] = texturePath,
			["uses_complete_card_texture"] = HasCardTexture,
			["feedback_visible"] = _feedbackBorder.Visible,
			["focus_highlight_enabled"] = _focusHighlightEnabled,
		};
	}

	private void OnMouseEntered()
	{
		_hovered = true;
		RefreshFeedback();
	}

	private void OnMouseExited()
	{
		_hovered = false;
		RefreshFeedback();
	}

	private void RefreshFeedback()
	{
		if (_feedbackBorder != null)
		{
			_feedbackBorder.Visible = !_isLocked
				&& _interactive
				&& (_hovered || (_focusHighlightEnabled && HasFocus()));
		}
	}

	private void RefreshDescriptionFontSize()
	{
		if (_descriptionLabel == null)
		{
			return;
		}

		float widthScale = Mathf.Max(0.75f, Size.X / BaseCardWidth);
		int effectiveLength = Mathf.CeilToInt(Description.Length / widthScale);
		int baseFontSize = effectiveLength switch
		{
			> 120 => MinDescriptionFontSize,
			> 96 => 10,
			> 78 => 11,
			> 60 => 12,
			_ => MaxDescriptionFontSize,
		};
		_descriptionLabel.AddThemeFontSizeOverride(
			"font_size",
			Mathf.Max(MinDescriptionFontSize, Mathf.RoundToInt(baseFontSize * widthScale)));
	}

	private static string GetDefaultRarityLabel(string rarity)
	{
		return rarity switch
		{
			"common" => "Common",
			"rare" => "Rare",
			"legendary" => "Legendary",
			_ => string.Empty,
		};
	}

	private static string Normalize(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? string.Empty
			: value.Trim().ToLowerInvariant();
	}
}
