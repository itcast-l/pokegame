using Godot;

[GlobalClass]
public partial class AdventureUpgradeCardVisualEntryResource : Resource
{
	[Export] public string Suit { get; set; } = string.Empty;
	[Export] public string Tier { get; set; } = string.Empty;
	[Export] public string TexturePath { get; set; } = string.Empty;
	[Export] public Rect2 Region { get; set; } = new Rect2();
	[Export] public int SortOrder { get; set; }
	[Export] public string PreviewLabel { get; set; } = string.Empty;

	public string Key => $"{Suit.Trim().ToLowerInvariant()}::{Tier.Trim().ToLowerInvariant()}";
}
