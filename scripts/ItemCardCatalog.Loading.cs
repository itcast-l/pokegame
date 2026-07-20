using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
{
	private static bool TryLoadDefinitions(
		Node sceneNode,
		IReadOnlyDictionary<string, ItemCardVisualDefinition> visualsById,
		out List<ItemCardDefinition> definitions,
		out string errorMessage)
	{
		if (!TryLoadPandoraDefinitions(sceneNode, visualsById, out definitions, out errorMessage))
		{
			return false;
		}

		definitions = SortDefinitions(definitions);
		return true;
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

}
