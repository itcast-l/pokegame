using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
{
	private static bool TryCreateDefinitionFromPandoraEntity(
		GodotObject entity,
		out ItemCardDefinition definition,
		out string errorMessage)
	{
		definition = default;
		errorMessage = string.Empty;
		foreach (string propertyName in RequiredPandoraGameplayProperties)
		{
			if (!entity.Call("has_entity_property", propertyName).AsBool())
			{
				errorMessage = $"Entity '{entity.Call("get_entity_id").AsString()}' is missing gameplay property '{propertyName}'.";
				return false;
			}
		}

		string id = GetRequiredEntityString(entity, "id");
		if (string.IsNullOrWhiteSpace(id))
		{
			errorMessage = "Item card entity has an empty id.";
			return false;
		}

		GetResourceDefinitions().TryGetValue(id, out ItemCardDefinition defaults);
		string displayName = GetEntityName(entity, string.IsNullOrWhiteSpace(defaults.DisplayName) ? ToPascalCase(id) : defaults.DisplayName);
		string spriteName = string.IsNullOrWhiteSpace(defaults.SpriteName) ? ToPascalCase(id) : defaults.SpriteName;
		string descriptionKey = GetRequiredEntityString(entity, "description_key");
		string artRunName = string.IsNullOrWhiteSpace(defaults.ArtRunName) ? DefaultArtRunName : defaults.ArtRunName;
		string framePathPattern = defaults.FramePathPattern;
		int frameCount = defaults.FrameCount > 0 ? defaults.FrameCount : DefaultFrameCount;
		int sortOrder = defaults.SortOrder != 0 ? defaults.SortOrder : DefaultSortOrder;
		string attachmentOverlayTexturePath = defaults.AttachmentOverlayTexturePath;
		int attachmentOverlayPriority = defaults.AttachmentOverlayPriority;
		string icon = defaults.Icon;
		string description = defaults.Description;
		bool enabledInGame = GetRequiredEntityBool(entity, "enabled_in_game");
		bool playableFromHand = GetRequiredEntityBool(entity, "playable_from_hand");
		bool canBeQuestionReward = GetRequiredEntityBool(entity, "can_be_question_reward");
		string rarity = GetRequiredEntityString(entity, "rarity");
		string tags = GetRequiredEntityString(entity, "tags");
		string rewardDeliveryRaw = GetRequiredEntityString(entity, "reward_delivery");
		if (!RewardDeliveryModeRules.TryParse(rewardDeliveryRaw, out RewardDeliveryMode rewardDelivery)
			|| string.IsNullOrWhiteSpace(rewardDeliveryRaw))
		{
			errorMessage = $"Item card '{id}' has invalid reward delivery '{rewardDeliveryRaw}'.";
			return false;
		}

		string playPresentation = GetRequiredEntityString(entity, "play_presentation");
		string trigger = GetRequiredEntityString(entity, "trigger");
		string effectType = GetRequiredEntityString(entity, "effect_type");
		string effectValues = GetRequiredEntityString(entity, "effect_values");
		int priority = GetRequiredEntityInt(entity, "priority");
		string attachedTrigger = GetRequiredEntityString(entity, "attached_trigger");
		string attachedEffectType = GetRequiredEntityString(entity, "attached_effect_type");
		string attachedEffectValues = GetRequiredEntityString(entity, "attached_effect_values");
		int attachedPriority = GetRequiredEntityInt(entity, "attached_priority");
		int weight = GetRequiredEntityInt(entity, "weight");
		if (!CardEffectValues.TryParse(effectValues, out _)
			|| !CardEffectValues.TryParse(attachedEffectValues, out _))
		{
			errorMessage = $"Item card '{id}' has malformed effect values.";
			return false;
		}

		definition = new ItemCardDefinition(
			id,
			displayName,
			spriteName,
			descriptionKey,
			enabledInGame,
			playableFromHand,
			canBeQuestionReward,
			artRunName,
			framePathPattern,
			Math.Max(1, frameCount),
			sortOrder,
			rarity,
			tags,
			attachmentOverlayTexturePath,
			attachmentOverlayPriority,
			playPresentation,
			trigger,
			effectType,
			effectValues,
			priority,
			attachedTrigger,
			attachedEffectType,
			attachedEffectValues,
			attachedPriority,
			icon,
			weight,
			description,
			rewardDelivery);
		ItemCardContractValidationResult validation = ItemCardEffectContractRegistry.Validate(definition);
		if (!validation.IsValid)
		{
			errorMessage = validation.ErrorMessage;
			definition = default;
			return false;
		}

		return true;
	}

	private static string GetRequiredEntityString(GodotObject entity, string propertyName)
	{
		return entity.Call("get_string", propertyName).AsString();
	}

	private static bool GetRequiredEntityBool(GodotObject entity, string propertyName)
	{
		return entity.Call("get_bool", propertyName).AsBool();
	}

	private static int GetRequiredEntityInt(GodotObject entity, string propertyName)
	{
		return entity.Call("get_integer", propertyName).AsInt32();
	}

	private static string GetEntityString(GodotObject entity, string propertyName, string fallback)
	{
		if (!(bool)entity.Call("has_entity_property", propertyName))
		{
			return fallback;
		}

		string value = entity.Call("get_string", propertyName).AsString();
		return string.IsNullOrWhiteSpace(value) ? fallback : value;
	}

	private static bool GetEntityBool(GodotObject entity, string propertyName, bool fallback)
	{
		return (bool)entity.Call("has_entity_property", propertyName)
			? entity.Call("get_bool", propertyName).AsBool()
			: fallback;
	}

	private static int GetEntityInt(GodotObject entity, string propertyName, int fallback)
	{
		return (bool)entity.Call("has_entity_property", propertyName)
			? entity.Call("get_integer", propertyName).AsInt32()
			: fallback;
	}

	private static string GetEntityName(GodotObject entity, string fallback)
	{
		string entityName = entity.Call("get_entity_name").AsString();
		return string.IsNullOrWhiteSpace(entityName) ? fallback : entityName;
	}

	private static string ToPascalCase(string value)
	{
		var words = value.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (words.Length == 0)
		{
			return value;
		}

		var output = string.Empty;
		foreach (string word in words)
		{
			output += char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word[1..] : string.Empty);
		}

		return output;
	}
}
