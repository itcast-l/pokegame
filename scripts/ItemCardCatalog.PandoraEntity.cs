using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
{
	private static bool TryCreateDefinitionFromPandoraEntity(GodotObject entity, out ItemCardDefinition definition)
	{
		string id = GetEntityString(entity, "id", "");
		if (string.IsNullOrWhiteSpace(id))
		{
			id = entity.Call("get_entity_id").AsString();
		}

		if (string.IsNullOrWhiteSpace(id))
		{
			definition = default;
			return false;
		}

		GetResourceDefinitions().TryGetValue(id, out ItemCardDefinition defaults);
		string displayName = GetEntityString(entity, "name", GetEntityName(entity, string.IsNullOrWhiteSpace(defaults.DisplayName) ? ToPascalCase(id) : defaults.DisplayName));
		string spriteName = GetEntityString(entity, "sprite_name", string.IsNullOrWhiteSpace(defaults.SpriteName) ? ToPascalCase(id) : defaults.SpriteName);
		string descriptionKey = GetEntityString(entity, "description_key", string.IsNullOrWhiteSpace(defaults.DescriptionKey) ? id : defaults.DescriptionKey);
		string description = GetEntityString(entity, "description", descriptionKey);
		string artRunName = GetEntityString(entity, "art_run", string.IsNullOrWhiteSpace(defaults.ArtRunName) ? DefaultArtRunName : defaults.ArtRunName);
		string framePathPattern = GetEntityString(entity, "frame_path_pattern", defaults.FramePathPattern);
		int frameCount = GetEntityInt(entity, "frame_count", defaults.FrameCount > 0 ? defaults.FrameCount : DefaultFrameCount);
		int sortOrder = GetEntityInt(entity, "sort_order", defaults.SortOrder != 0 ? defaults.SortOrder : DefaultSortOrder);
		bool enabledInGame = GetEntityBool(entity, "enabled_in_game", defaults.EnabledInGame);
		bool playableFromHand = GetEntityBool(entity, "playable_from_hand", defaults.PlayableFromHand);
		bool canBeQuestionReward = GetEntityBool(entity, "can_be_question_reward", defaults.CanBeQuestionReward);
		string rarity = GetEntityString(entity, "rarity", string.IsNullOrWhiteSpace(defaults.Rarity) ? DefaultRarity : defaults.Rarity);
		string tags = GetEntityString(entity, "tags", defaults.TagsCsv);
		string attachmentOverlayTexturePath = GetEntityString(entity, "attachment_overlay_texture_path", defaults.AttachmentOverlayTexturePath);
		int attachmentOverlayPriority = GetEntityInt(entity, "attachment_overlay_priority", defaults.AttachmentOverlayPriority);
		string playPresentation = GetEntityString(entity, "play_presentation", defaults.PlayPresentation);
		string trigger = GetEntityString(entity, "trigger", defaults.Trigger);
		string effectType = GetEntityString(entity, "effect_type", defaults.EffectType);
		string effectValues = GetEntityString(entity, "effect_values", defaults.EffectValuesRaw);
		int priority = GetEntityInt(entity, "priority", defaults.Priority);
		string attachedTrigger = GetEntityString(entity, "attached_trigger", defaults.AttachedTrigger);
		string attachedEffectType = GetEntityString(entity, "attached_effect_type", defaults.AttachedEffectType);
		string attachedEffectValues = GetEntityString(entity, "attached_effect_values", defaults.AttachedEffectValuesRaw);
		int attachedPriority = GetEntityInt(entity, "attached_priority", defaults.AttachedPriority);
		string icon = GetEntityString(entity, "icon", defaults.Icon);
		int weight = GetEntityInt(entity, "weight", defaults.Weight != 0 ? defaults.Weight : DefaultWeight);

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
			description);
		return true;
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
