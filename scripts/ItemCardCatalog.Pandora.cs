using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
{
	private static readonly IReadOnlySet<string> RequiredPandoraGameplayProperties =
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

	private static bool CanReadPandora(Node sceneNode)
	{
		Node? pandora = sceneNode.GetNodeOrNull<Node>("/root/Pandora");
		return pandora != null && (bool)pandora.Call("is_loaded");
	}

	private static bool TryLoadPandoraDefinitions(
		Node sceneNode,
		out List<ItemCardDefinition> definitions,
		out string errorMessage)
	{
		definitions = new List<ItemCardDefinition>();
		errorMessage = string.Empty;
		Node? pandora = sceneNode.GetNodeOrNull<Node>("/root/Pandora");
		if (pandora == null || !(bool)pandora.Call("is_loaded"))
		{
			errorMessage = "Pandora is not loaded.";
			return false;
		}

		GodotObject? category = pandora.Call("get_category", ItemCardCategoryId).AsGodotObject();
		if (category == null)
		{
			errorMessage = $"Missing Pandora category '{ItemCardCategoryId}'.";
			return false;
		}

		Godot.Collections.Array entities = pandora.Call("get_all_entities", category).AsGodotArray();
		if (entities.Count == 0)
		{
			errorMessage = "The item card category is empty.";
			return false;
		}

		var ids = new HashSet<string>(StringComparer.Ordinal);
		var manifestEntries = new List<ItemCardPandoraManifestEntry>();
		foreach (Variant entityVariant in entities)
		{
			GodotObject? entity = entityVariant.AsGodotObject();
			if (entity == null)
			{
				errorMessage = "The item card category contains a non-object entity.";
				return false;
			}

			if (!TryCreateDefinitionFromPandoraEntity(entity, out ItemCardDefinition definition, out errorMessage))
			{
				return false;
			}

			if (!ids.Add(definition.Id))
			{
				errorMessage = $"Duplicate item card ID '{definition.Id}'.";
				return false;
			}

			manifestEntries.Add(new ItemCardPandoraManifestEntry(
				definition.Id,
				ItemCardManifestValidationRules.RequiredGameplayProperties,
				definition));
			definitions.Add(definition);
		}

		IReadOnlyDictionary<string, ItemCardDefinition> resourceDefinitions = GetResourceDefinitions();
		ItemCardManifestValidationResult manifestValidation =
			ItemCardManifestValidationRules.ValidatePandoraEntries(manifestEntries, resourceDefinitions.Keys);
		if (!manifestValidation.IsValid)
		{
			errorMessage = manifestValidation.ErrorMessage;
			return false;
		}

		return true;
	}
}
