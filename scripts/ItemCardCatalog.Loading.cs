using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
{
	private static List<ItemCardDefinition> LoadDefinitions(Node sceneNode, out bool loadedFromPandora)
	{
		var definitionsById = new Dictionary<string, ItemCardDefinition>(StringComparer.Ordinal);
		foreach (ItemCardDefinition definition in GetResourceDefinitions().Values)
		{
			if (IsRuntimeRuleDefinition(definition))
			{
				continue;
			}

			definitionsById[definition.Id] = definition;
		}

		List<ItemCardDefinition> pandoraDefinitions = LoadPandoraDefinitions(sceneNode);
		loadedFromPandora = pandoraDefinitions.Count > 0;
		foreach (ItemCardDefinition definition in pandoraDefinitions)
		{
			definitionsById[definition.Id] = definition;
		}

		var definitions = new List<ItemCardDefinition>(definitionsById.Values);
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
