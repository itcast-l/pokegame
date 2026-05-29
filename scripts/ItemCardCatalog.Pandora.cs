using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
{
	private static bool CanReadPandora(Node sceneNode)
	{
		Node? pandora = sceneNode.GetNodeOrNull<Node>("/root/Pandora");
		return pandora != null && (bool)pandora.Call("is_loaded");
	}

	private static List<ItemCardDefinition> LoadPandoraDefinitions(Node sceneNode)
	{
		var definitions = new List<ItemCardDefinition>();
		Node? pandora = sceneNode.GetNodeOrNull<Node>("/root/Pandora");
		if (pandora == null || !(bool)pandora.Call("is_loaded"))
		{
			return definitions;
		}

		GodotObject? category = pandora.Call("get_category", ItemCardCategoryId).AsGodotObject();
		if (category == null)
		{
			return definitions;
		}

		Godot.Collections.Array entities = pandora.Call("get_all_entities", category).AsGodotArray();
		foreach (Variant entityVariant in entities)
		{
			GodotObject? entity = entityVariant.AsGodotObject();
			if (entity == null || !TryCreateDefinitionFromPandoraEntity(entity, out ItemCardDefinition definition))
			{
				continue;
			}

			definitions.Add(definition);
		}

		return definitions;
	}
}
