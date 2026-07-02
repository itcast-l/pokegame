using Godot;

[GlobalClass]
public partial class AdventureUpgradeCardVisualEntryResource : Resource
{
	[Export] public string Suit { get; set; } = string.Empty;
	[Export] public string Tier { get; set; } = string.Empty;
	[Export] public string TexturePath { get; set; } = string.Empty;
	[Export] public Rect2 Region { get; set; } = new Rect2();
	[Export] public Color TitleColor { get; set; } = new(0.93f, 0.96f, 1.0f, 0.99f);
	[Export] public Color GlowColor { get; set; } = new(0.48f, 0.92f, 1.0f, 0.06f);
	[Export] public Color RuleColor { get; set; } = new(0.98f, 0.9f, 0.7f, 0.08f);
	[Export] public int SortOrder { get; set; }
	[Export] public string PreviewLabel { get; set; } = string.Empty;

	public string Key => $"{Suit.Trim().ToLowerInvariant()}::{Tier.Trim().ToLowerInvariant()}";
}
