using System;
using System.Collections.Generic;

internal readonly record struct ItemCardPandoraManifestEntry(
	string Id,
	IReadOnlySet<string> PresentGameplayProperties,
	ItemCardDefinition Definition);

internal readonly record struct ItemCardManifestValidationResult(
	bool IsValid,
	string ErrorMessage)
{
	internal static ItemCardManifestValidationResult Valid => new(true, string.Empty);

	internal static ItemCardManifestValidationResult Invalid(string message)
	{
		return new ItemCardManifestValidationResult(false, message);
	}
}

internal static class ItemCardManifestValidationRules
{
	internal static readonly IReadOnlySet<string> RequiredGameplayProperties =
		new HashSet<string>(StringComparer.Ordinal)
		{
			"id",
			"enabled_in_game",
			"playable_from_hand",
			"can_be_question_reward",
			"weight",
			"rarity",
			"tags",
			"reward_delivery",
			"play_presentation",
			"trigger",
			"effect_type",
			"effect_values",
			"priority",
			"attached_trigger",
			"attached_effect_type",
			"attached_effect_values",
			"attached_priority",
			"description_key",
		};

	internal static ItemCardManifestValidationResult ValidatePandoraEntries(
		IEnumerable<ItemCardPandoraManifestEntry>? entries,
		IEnumerable<string>? fallbackIds)
	{
		if (entries == null)
		{
			return ItemCardManifestValidationResult.Invalid("Pandora entries are missing.");
		}

		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (ItemCardPandoraManifestEntry entry in entries)
		{
			string id = entry.Id?.Trim() ?? string.Empty;
			if (id.Length == 0)
			{
				return ItemCardManifestValidationResult.Invalid("Pandora item card ID is empty.");
			}

			if (!ids.Add(id))
			{
				return ItemCardManifestValidationResult.Invalid($"Duplicate item card ID '{id}'.");
			}

			foreach (string requiredProperty in RequiredGameplayProperties)
			{
				if (!entry.PresentGameplayProperties.Contains(requiredProperty))
				{
					return ItemCardManifestValidationResult.Invalid(
						$"Item card '{id}' is missing gameplay property '{requiredProperty}'.");
				}
			}

			ItemCardContractValidationResult contract = ItemCardEffectContractRegistry.Validate(entry.Definition);
			if (!contract.IsValid)
			{
				return ItemCardManifestValidationResult.Invalid(contract.ErrorMessage);
			}
		}

		var expectedIds = new HashSet<string>(fallbackIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		if (!ids.SetEquals(expectedIds))
		{
			return ItemCardManifestValidationResult.Invalid(
				$"Pandora/fallback item ID set differs: Pandora={ids.Count}, fallback={expectedIds.Count}.");
		}

		return ItemCardManifestValidationResult.Valid;
	}

	internal static bool ShouldUseFallback(bool pandoraIsLoaded)
	{
		return !pandoraIsLoaded;
	}
}
