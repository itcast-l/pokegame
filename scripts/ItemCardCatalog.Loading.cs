using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
{
	private static List<ItemCardDefinition> LoadDefinitions(Node sceneNode, out bool loadedFromPandora)
	{
		bool pandoraIsLoaded = CanReadPandora(sceneNode);
		if (pandoraIsLoaded)
		{
			loadedFromPandora = true;
			if (!TryLoadPandoraDefinitions(sceneNode, out List<ItemCardDefinition> pandoraDefinitions, out string errorMessage))
			{
				GD.PushWarning($"Item card Pandora manifest is invalid and will not use .tres gameplay fallback: {errorMessage}");
				return new List<ItemCardDefinition>();
			}

			return SortDefinitions(pandoraDefinitions);
		}

		loadedFromPandora = false;
		if (!ItemCardManifestValidationRules.ShouldUseFallback(pandoraIsLoaded))
		{
			return new List<ItemCardDefinition>();
		}
		var fallbackDefinitions = new List<ItemCardDefinition>();
		var fallbackIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (ItemCardDefinition definition in GetResourceDefinitions().Values)
		{
			if (!fallbackIds.Add(definition.Id))
			{
				GD.PushWarning($"Duplicate item card fallback definition '{definition.Id}'.");
				return new List<ItemCardDefinition>();
			}

			ItemCardContractValidationResult validation = ItemCardEffectContractRegistry.Validate(definition);
			if (!validation.IsValid)
			{
				GD.PushWarning($"Invalid item card fallback definition: {validation.ErrorMessage}");
				return new List<ItemCardDefinition>();
			}

			fallbackDefinitions.Add(definition);
		}

		return SortDefinitions(fallbackDefinitions);
	}

	private static List<ItemCardDefinition> SortDefinitions(List<ItemCardDefinition> definitions)
	{
		definitions.Sort((a, b) =>
		{
			int orderComparison = a.SortOrder.CompareTo(b.SortOrder);
			return orderComparison != 0 ? orderComparison : string.CompareOrdinal(a.Id, b.Id);
		});
		return definitions;
	}

	private static bool IsRuntimeRuleDefinition(ItemCardDefinition definition)
	{
		return !definition.EnabledInGame && !definition.PlayableFromHand && !definition.CanBeQuestionReward;
	}
}
