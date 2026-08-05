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
		AdventureUpgradeManifest? manifest = null)
	{
		var available = BuildAvailablePool(allDefinitions, acquiredIds, manifest);
		if (available.Count <= OfferSize)
		{
			return available;
		}

		AdventureUpgradeRarityWeights weights = GetRarityWeights(runStageIndex);
		var offer = new List<AdventureUpgradeDefinitionModel>(OfferSize);
		for (int slotIndex = 0; slotIndex < OfferSize; slotIndex++)
		{
			AdventureUpgradeDefinitionModel? pickedDefinition = TryPickOfferDefinition(available, offer, weights, nextIntExclusive);
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
		AdventureUpgradeManifest? manifest)
	{
		var acquired = new HashSet<string>(acquiredIds, StringComparer.Ordinal);
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
			if (!definition.Enabled || string.IsNullOrWhiteSpace(definition.Id) || acquired.Contains(definition.Id))
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

			int pickedIndex = nextIntExclusive(rarityPool.Count);
			return rarityPool[pickedIndex];
		}

		return null;
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
