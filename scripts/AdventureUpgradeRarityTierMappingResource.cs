using Godot;

[GlobalClass]
public partial class AdventureUpgradeRarityTierMappingResource : Resource
{
	[Export] public string Rarity { get; set; } = string.Empty;
	[Export] public string Tier { get; set; } = string.Empty;
}
