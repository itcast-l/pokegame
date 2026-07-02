using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class AdventureUpgradeCardVisualCatalogResource : Resource
{
	[Export] public Godot.Collections.Array<AdventureUpgradeCardVisualEntryResource> Entries { get; set; } = new();
	[Export] public Godot.Collections.Array<AdventureUpgradeRarityTierMappingResource> RarityTierMappings { get; set; } = new();

	public IReadOnlyList<AdventureUpgradeCardVisualEntryResource> GetSortedEntries()
	{
		var results = new List<AdventureUpgradeCardVisualEntryResource>();
		foreach (AdventureUpgradeCardVisualEntryResource entry in Entries)
		{
			if (entry != null)
			{
				results.Add(entry);
			}
		}

		results.Sort((a, b) =>
		{
			int sortComparison = a.SortOrder.CompareTo(b.SortOrder);
			return sortComparison != 0 ? sortComparison : string.CompareOrdinal(a.Key, b.Key);
		});
		return results;
	}

	public AdventureUpgradeCardVisualEntryResource? Find(string suit, string tier)
	{
		string key = $"{Normalize(suit)}::{Normalize(tier)}";
		foreach (AdventureUpgradeCardVisualEntryResource entry in Entries)
		{
			if (entry != null && entry.Key == key)
			{
				return entry;
			}
		}

		return null;
	}

	public string ResolveTier(string rarity)
	{
		string normalizedRarity = Normalize(rarity);
		foreach (AdventureUpgradeRarityTierMappingResource mapping in RarityTierMappings)
		{
			if (mapping != null && Normalize(mapping.Rarity) == normalizedRarity)
			{
				string mappedTier = Normalize(mapping.Tier);
				if (!string.IsNullOrWhiteSpace(mappedTier))
				{
					return mappedTier;
				}
			}
		}

		return normalizedRarity;
	}

	private static string Normalize(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
	}
}
