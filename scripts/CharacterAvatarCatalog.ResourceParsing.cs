using System;
using System.Collections.Generic;
using Godot;

public static partial class CharacterAvatarCatalog
{
	private static CharacterAvatarDefinition? LoadAvatarDefinition(string path)
	{
		Resource? resource = GD.Load(path);
		if (resource is CharacterAvatarDefinitionResource avatarResource)
		{
			return avatarResource.ToDefinition();
		}

		if (resource == null)
		{
			return null;
		}

		return new CharacterAvatarDefinition(
			GetResourceString(resource, "Id"),
			GetResourceString(resource, "DisplayName"),
			GetResourceString(resource, "RootPath"),
			GetResourceInt(resource, "IdleFrameCount", 1),
			GetResourceInt(resource, "ActionFrameCount", 4),
			GetResourceInt(resource, "AdventureStageNumber"),
			GetResourceString(resource, "AdventureAbilityNameKey", "ADVENTURE_ABILITY_NONE_NAME"),
			GetResourceString(resource, "AdventureAbilityDescriptionKey", "ADVENTURE_ABILITY_NONE_DESC"),
			GetResourceString(resource, "AbilityIdsCsv"),
			GetResourceString(resource, "AbilityValuesRaw"),
			GetResourceString(resource, "DamageEffectFramePattern"),
			GetResourceInt(resource, "DamageEffectFrameCount"),
			GetResourceBool(resource, "DamageEffectPreferImageFile"),
			GetResourceString(resource, "AttackFxRoot"),
			GetResourceFloat(resource, "AttackEffectFlightSeconds"),
			GetResourceString(resource, "AttackEffectStyle"),
			GetResourceString(resource, "AccentColorHtml"),
			GetResourceBool(resource, "BossBorrowedAbilityFxEligible", true));
	}

	private static string GetResourceString(Resource resource, string propertyName, string fallback = "")
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsString();
	}

	private static int GetResourceInt(Resource resource, string propertyName, int fallback = 0)
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
	}

	private static float GetResourceFloat(Resource resource, string propertyName, float fallback = 0.0f)
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsSingle();
	}

	private static bool GetResourceBool(Resource resource, string propertyName, bool fallback = false)
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
	}
}
