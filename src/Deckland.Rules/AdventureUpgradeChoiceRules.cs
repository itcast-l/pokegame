using System;
using System.Collections.Generic;

public readonly record struct AdventureUpgradeDefinitionModel(
	string Id,
	string DisplayName,
	string Suit,
	string Rarity,
	string TriggerText,
	string EffectText,
	bool Enabled);

public readonly record struct AdventureUpgradeRarityWeights(
	int CommonWeight,
	int RareWeight,
	int LegendaryWeight);

internal static class AdventureUpgradeChoiceRules
{
	private const int MainAdventureFirstAvatarStage = 3;
	private const int MainAdventureLastAvatarStage = 14;
	private const int OfferSize = 3;
	private const int BigCaptureThreshold = 6;

	internal static bool IsMainAdventureAvatarStageNumber(int avatarStageNumber)
	{
		return AdventureCampaignCatalog.TryMapLegacyRunStage(avatarStageNumber, out _);
	}

	internal static bool IsMainAdventureStageIndex(int mainStageIndex)
	{
		return mainStageIndex >= 1 && mainStageIndex <= AdventureCampaignCatalog.MainStageCount;
	}

	internal static int ToRunStageIndex(int avatarStageNumber)
	{
		if (!IsMainAdventureAvatarStageNumber(avatarStageNumber))
		{
			return 0;
		}

		return avatarStageNumber - MainAdventureFirstAvatarStage + 1;
	}

	internal static int ToRunStageIndexFromMainStageIndex(int mainStageIndex)
	{
		return IsMainAdventureStageIndex(mainStageIndex) ? mainStageIndex : 0;
	}

	internal static AdventureUpgradeRarityWeights GetRarityWeights(int runStageIndex)
	{
		if (runStageIndex <= 4)
		{
			return new AdventureUpgradeRarityWeights(70, 25, 5);
		}

		if (runStageIndex <= 8)
		{
			return new AdventureUpgradeRarityWeights(50, 35, 15);
		}

		return new AdventureUpgradeRarityWeights(30, 45, 25);
	}

	internal static bool ShouldGrantBigCapture(int totalCapturedCardCount)
	{
		return totalCapturedCardCount >= BigCaptureThreshold;
	}

	internal static IReadOnlyList<AdventureUpgradeDefinitionModel> BuildOffer(
		IReadOnlyList<AdventureUpgradeDefinitionModel> allDefinitions,
		IEnumerable<string> acquiredIds,
		int runStageIndex,
		Func<int, int> nextIntExclusive,
		AdventureUpgradeManifest? manifest = null,
		IEnumerable<string>? retiredIds = null)
	{
		var acquired = new HashSet<string>(acquiredIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		var available = BuildAvailablePool(allDefinitions, acquired, retiredIds, manifest);
		if (available.Count <= OfferSize)
		{
			return available;
		}

		AdventureUpgradeRarityWeights weights = GetRarityWeights(runStageIndex);
		Dictionary<string, int> acquiredSuitCounts = CountAcquiredSuits(allDefinitions, acquired, manifest);
		var offer = new List<AdventureUpgradeDefinitionModel>(OfferSize);
		for (int slotIndex = 0; slotIndex < OfferSize; slotIndex++)
		{
			AdventureUpgradeDefinitionModel? pickedDefinition = TryPickOfferDefinition(
				available,
				offer,
				weights,
				acquiredSuitCounts,
				nextIntExclusive);
			if (!pickedDefinition.HasValue)
			{
				break;
			}

			offer.Add(pickedDefinition.Value);
		}

		return offer;
	}

	private static List<AdventureUpgradeDefinitionModel> BuildAvailablePool(
		IReadOnlyList<AdventureUpgradeDefinitionModel> allDefinitions,
		IEnumerable<string> acquiredIds,
		IEnumerable<string>? retiredIds,
		AdventureUpgradeManifest? manifest)
	{
		var acquired = new HashSet<string>(acquiredIds, StringComparer.Ordinal);
		var retired = new HashSet<string>(retiredIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		var acquiredConflictGroups = new HashSet<string>(StringComparer.Ordinal);
		foreach (string acquiredId in acquired)
		{
			if (TryGetConflictGroup(manifest, acquiredId, out string conflictGroup))
			{
				acquiredConflictGroups.Add(conflictGroup);
			}
		}

		var available = new List<AdventureUpgradeDefinitionModel>();
		foreach (AdventureUpgradeDefinitionModel definition in allDefinitions)
		{
			if (!definition.Enabled
				|| string.IsNullOrWhiteSpace(definition.Id)
				|| acquired.Contains(definition.Id)
				|| retired.Contains(definition.Id))
			{
				continue;
			}

			if (TryGetConflictGroup(manifest, definition.Id, out string conflictGroup)
				&& acquiredConflictGroups.Contains(conflictGroup))
			{
				continue;
			}

			available.Add(definition);
		}

		return available;
	}

	private static bool TryGetConflictGroup(
		AdventureUpgradeManifest? manifest,
		string upgradeId,
		out string conflictGroup)
	{
		if (manifest?.TryGetEnabledEntry(upgradeId, out AdventureUpgradeManifestEntry entry) == true)
		{
			conflictGroup = entry.ConflictGroup;
		}
		else if (AdventureUpgradeRuntimeContract.TryGetMetadata(
			upgradeId,
			out AdventureUpgradeRuntimeMetadata metadata))
		{
			conflictGroup = metadata.ConflictGroup;
		}
		else
		{
			conflictGroup = string.Empty;
		}

		return !string.IsNullOrWhiteSpace(conflictGroup);
	}

	private static AdventureUpgradeDefinitionModel? TryPickOfferDefinition(
		IReadOnlyList<AdventureUpgradeDefinitionModel> available,
		IReadOnlyList<AdventureUpgradeDefinitionModel> currentOffer,
		AdventureUpgradeRarityWeights weights,
		IReadOnlyDictionary<string, int> acquiredSuitCounts,
		Func<int, int> nextIntExclusive)
	{
		string rolledRarity = RollRarity(weights, nextIntExclusive);
		foreach (string candidateRarity in EnumerateFallbackRarities(rolledRarity))
		{
			List<AdventureUpgradeDefinitionModel> rarityPool = FilterPoolByRarity(available, currentOffer, candidateRarity);
			if (rarityPool.Count == 0)
			{
				continue;
			}

			return PickWeightedBySuit(rarityPool, acquiredSuitCounts, nextIntExclusive);
		}

		return null;
	}

	private static AdventureUpgradeDefinitionModel PickWeightedBySuit(
		IReadOnlyList<AdventureUpgradeDefinitionModel> pool,
		IReadOnlyDictionary<string, int> acquiredSuitCounts,
		Func<int, int> nextIntExclusive)
	{
		int totalWeight = 0;
		foreach (AdventureUpgradeDefinitionModel definition in pool)
		{
			totalWeight += 2 + GetSuitCount(acquiredSuitCounts, definition.Suit);
		}

		int roll = nextIntExclusive(totalWeight);
		foreach (AdventureUpgradeDefinitionModel definition in pool)
		{
			roll -= 2 + GetSuitCount(acquiredSuitCounts, definition.Suit);
			if (roll < 0)
			{
				return definition;
			}
		}

		return pool[^1];
	}

	private static Dictionary<string, int> CountAcquiredSuits(
		IReadOnlyList<AdventureUpgradeDefinitionModel> definitions,
		IEnumerable<string> acquiredIds,
		AdventureUpgradeManifest? manifest)
	{
		var definitionsById = new Dictionary<string, AdventureUpgradeDefinitionModel>(StringComparer.Ordinal);
		foreach (AdventureUpgradeDefinitionModel definition in definitions)
		{
			definitionsById[definition.Id] = definition;
		}

		var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (string acquiredId in acquiredIds)
		{
			string suit = manifest?.TryGetEnabledEntry(acquiredId, out AdventureUpgradeManifestEntry entry) == true
				? entry.Suit
				: definitionsById.TryGetValue(acquiredId, out AdventureUpgradeDefinitionModel definition)
					? definition.Suit
					: string.Empty;
			if (string.IsNullOrWhiteSpace(suit))
			{
				continue;
			}
			counts[suit] = GetSuitCount(counts, suit) + 1;
		}

		return counts;
	}

	private static int GetSuitCount(IReadOnlyDictionary<string, int> counts, string suit)
	{
		return !string.IsNullOrWhiteSpace(suit) && counts.TryGetValue(suit, out int count)
			? count
			: 0;
	}

	private static List<AdventureUpgradeDefinitionModel> FilterPoolByRarity(
		IReadOnlyList<AdventureUpgradeDefinitionModel> available,
		IReadOnlyList<AdventureUpgradeDefinitionModel> currentOffer,
		string rarity)
	{
		var pool = new List<AdventureUpgradeDefinitionModel>();
		foreach (AdventureUpgradeDefinitionModel definition in available)
		{
			if (!string.Equals(definition.Rarity, rarity, StringComparison.Ordinal))
			{
				continue;
			}

			if (ContainsId(currentOffer, definition.Id))
			{
				continue;
			}

			pool.Add(definition);
		}

		return pool;
	}

	private static bool ContainsId(IReadOnlyList<AdventureUpgradeDefinitionModel> definitions, string id)
	{
		foreach (AdventureUpgradeDefinitionModel definition in definitions)
		{
			if (definition.Id == id)
			{
				return true;
			}
		}

		return false;
	}

	private static string RollRarity(AdventureUpgradeRarityWeights weights, Func<int, int> nextIntExclusive)
	{
		int totalWeight = weights.CommonWeight + weights.RareWeight + weights.LegendaryWeight;
		if (totalWeight <= 0)
		{
			return "common";
		}

		int roll = nextIntExclusive(totalWeight);
		if (roll < weights.CommonWeight)
		{
			return "common";
		}

		roll -= weights.CommonWeight;
		if (roll < weights.RareWeight)
		{
			return "rare";
		}

		return "legendary";
	}

	private static IEnumerable<string> EnumerateFallbackRarities(string rolledRarity)
	{
		yield return rolledRarity;
		switch (rolledRarity)
		{
			case "legendary":
				yield return "rare";
				yield return "common";
				yield break;
			case "rare":
				yield return "common";
				yield return "legendary";
				yield break;
			default:
				yield return "rare";
				yield return "legendary";
				yield break;
		}
	}
}
