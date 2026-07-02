using Godot;
using System;
using System.Collections.Generic;

public static class AdventureUpgradeCardVisualResolver
{
	private const string CatalogPath = "res://resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres";

	private static AdventureUpgradeCardVisualCatalogResource? _cachedCatalog;
	private static readonly Dictionary<string, AdventureUpgradeCardVisualEntryResource> EntryCache = new(StringComparer.Ordinal);
	private static readonly Dictionary<string, Texture2D> AtlasTextureCache = new(StringComparer.Ordinal);

	public static AdventureUpgradeCardVisualEntryResource? ResolveEntry(string suit, string rarity)
	{
		AdventureUpgradeCardVisualCatalogResource? catalog = LoadCatalog();
		if (catalog == null)
		{
			return null;
		}

		string tier = catalog.ResolveTier(rarity);
		return TryGetEntry(suit, tier, out AdventureUpgradeCardVisualEntryResource entry)
			? entry
			: null;
	}

	public static bool TryGetEntry(string suit, string tier, out AdventureUpgradeCardVisualEntryResource entry)
	{
		string key = BuildKey(suit, tier);
		if (EntryCache.TryGetValue(key, out AdventureUpgradeCardVisualEntryResource? cachedEntry))
		{
			entry = cachedEntry;
			return true;
		}

		AdventureUpgradeCardVisualCatalogResource? catalog = LoadCatalog();
		AdventureUpgradeCardVisualEntryResource? foundEntry = catalog?.Find(suit, tier);
		if (foundEntry == null)
		{
			entry = default!;
			return false;
		}

		EntryCache[key] = foundEntry;
		entry = foundEntry;
		return true;
	}

	public static Texture2D? LoadAtlasTexture(string suit, string tier)
	{
		string key = BuildKey(suit, tier);
		if (AtlasTextureCache.TryGetValue(key, out Texture2D? cachedTexture))
		{
			return cachedTexture;
		}

		if (!TryGetEntry(suit, tier, out AdventureUpgradeCardVisualEntryResource entry))
		{
			return null;
		}

		Texture2D? sourceTexture = GD.Load<Texture2D>(entry.TexturePath);
		if (sourceTexture == null)
		{
			GD.PushWarning($"Missing adventure upgrade card visual texture: {entry.TexturePath}");
			return null;
		}

		Texture2D atlasTexture = new AtlasTexture
		{
			Atlas = sourceTexture,
			Region = entry.Region,
		};
		AtlasTextureCache[key] = atlasTexture;
		return atlasTexture;
	}

	public static Texture2D? LoadAtlasTextureForRarity(string suit, string rarity)
	{
		AdventureUpgradeCardVisualCatalogResource? catalog = LoadCatalog();
		if (catalog == null)
		{
			return null;
		}

		string tier = catalog.ResolveTier(rarity);
		return LoadAtlasTexture(suit, tier);
	}

	private static AdventureUpgradeCardVisualCatalogResource? LoadCatalog()
	{
		if (_cachedCatalog != null)
		{
			return _cachedCatalog;
		}

		_cachedCatalog = GD.Load<AdventureUpgradeCardVisualCatalogResource>(CatalogPath);
		if (_cachedCatalog == null)
		{
			GD.PushWarning($"Missing adventure upgrade card visual catalog: {CatalogPath}");
		}

		return _cachedCatalog;
	}

	private static string BuildKey(string suit, string tier)
	{
		return $"{Normalize(suit)}::{Normalize(tier)}";
	}

	private static string Normalize(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? string.Empty
			: value.Trim().ToLowerInvariant();
	}
}
