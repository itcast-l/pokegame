using Godot;
using System;

public partial class AdventureUpgradeCatalogCard : Button
{
	[Export] public string PreviewSuit { get; set; } = string.Empty;
	[Export] public string PreviewRarity { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string PreviewDescription { get; set; } = string.Empty;
	[Export] public string PreviewRarityLabel { get; set; } = string.Empty;
	[Export] public bool PreviewInteractive { get; set; }

	private TextureRect _cardTexture = default!;
	private Control _placeholder = default!;
	private Label _descriptionLabel = default!;
	private Label _rarityLabel = default!;
	private Panel _feedbackBorder = default!;
	private bool _interactive;
	private bool _hovered;
	private string _suit = string.Empty;
	private string _rarity = string.Empty;
	private string _visualTier = string.Empty;

	public string Suit => _suit;
	public string Rarity => _rarity;
	public string Description => _descriptionLabel?.Text ?? string.Empty;
	public string RarityLabel => _rarityLabel?.Text ?? string.Empty;
	public string VisualTier => _visualTier;
	public bool IsInteractive => _interactive;
	public bool HasCardTexture => _cardTexture?.Texture != null;

	public override void _Ready()
	{
		_cardTexture = GetNode<TextureRect>("CardTexture");
		_placeholder = GetNode<Control>("MissingTexturePlaceholder");
		_descriptionLabel = GetNode<Label>("DescriptionLayer/DescriptionMargin/DescriptionLabel");
		_rarityLabel = GetNode<Label>("RarityLabel");
		_feedbackBorder = GetNode<Panel>("FeedbackBorder");

		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;
		FocusEntered += RefreshFeedback;
		FocusExited += RefreshFeedback;
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
		string rarityLabel,
		bool interactive)
	{
		_suit = Normalize(suit);
		_rarity = Normalize(rarity);
		_interactive = interactive;
		_hovered = false;

		AdventureUpgradeCardVisualEntryResource? entry =
			AdventureUpgradeCardVisualResolver.ResolveEntry(_suit, _rarity);
		_visualTier = entry?.Tier?.Trim().ToLowerInvariant() ?? string.Empty;
		Texture2D? texture = AdventureUpgradeCardVisualResolver.LoadAtlasTextureForRarity(
			_suit,
			_rarity);
		_cardTexture.Texture = texture;
		_placeholder.Visible = texture == null;

		_descriptionLabel.Text = description?.Trim() ?? string.Empty;
		_descriptionLabel.GetParent().GetParent<Control>().Visible = !string.IsNullOrWhiteSpace(_descriptionLabel.Text);
		_rarityLabel.Text = rarityLabel?.Trim() ?? string.Empty;
		_rarityLabel.Visible = !string.IsNullOrWhiteSpace(_rarityLabel.Text);

		FocusMode = interactive ? FocusModeEnum.All : FocusModeEnum.None;
		MouseFilter = interactive ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		ButtonMask = interactive ? MouseButtonMask.Left : (MouseButtonMask)0;
		ActionMode = ActionModeEnum.Release;
		Disabled = false;
		RefreshFeedback();
	}

	public Godot.Collections.Dictionary DebugGetCardState()
	{
		Rect2 region = _cardTexture.Texture is AtlasTexture atlasTexture
			? atlasTexture.Region
			: default;
		return new Godot.Collections.Dictionary
		{
			["suit"] = _suit,
			["rarity"] = _rarity,
			["visual_tier"] = _visualTier,
			["description"] = Description,
			["rarity_label"] = RarityLabel,
			["interactive"] = _interactive,
			["has_texture"] = HasCardTexture,
			["atlas_region"] = region,
			["feedback_visible"] = _feedbackBorder.Visible,
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
			_feedbackBorder.Visible = _interactive && (_hovered || HasFocus());
		}
	}

	private static string Normalize(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? string.Empty
			: value.Trim().ToLowerInvariant();
	}
}
