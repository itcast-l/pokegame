using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
{
	private static IReadOnlyDictionary<string, ItemCardDefinition> GetResourceDefinitions()
	{
		if (_resourceDefinitionsById != null)
		{
			return _resourceDefinitionsById;
		}

		var definitionsById = new Dictionary<string, ItemCardDefinition>(StringComparer.Ordinal);
		using DirAccess? directory = DirAccess.Open(ItemCardResourceDirectory);
		if (directory == null)
		{
			GD.PushWarning($"Missing item card resource directory: {ItemCardResourceDirectory}");
			_resourceDefinitionsById = definitionsById;
			return _resourceDefinitionsById;
		}

		directory.ListDirBegin();
		while (true)
		{
			string fileName = directory.GetNext();
			if (string.IsNullOrEmpty(fileName))
			{
				break;
			}

			if (directory.CurrentIsDir() || !TryGetResourceFileName(fileName, out string resourceFileName))
			{
				continue;
			}

			string path = $"{ItemCardResourceDirectory}/{resourceFileName}";
			ItemCardDefinition? definition = LoadItemCardDefinition(path);
			if (definition == null)
			{
				GD.PushWarning($"Invalid item card definition resource: {path}");
				continue;
			}

			ItemCardDefinition value = definition.Value;
			if (string.IsNullOrWhiteSpace(value.Id))
			{
				GD.PushWarning($"Item card definition resource is missing an id: {path}");
				continue;
			}

			definitionsById[value.Id] = value;
		}

		_resourceDefinitionsById = definitionsById;
		return _resourceDefinitionsById;
	}

	private static bool TryGetResourceFileName(string fileName, out string resourceFileName)
	{
		const string ResourceExtension = ".tres";
		const string RemapExtension = ".remap";

		if (fileName.EndsWith(ResourceExtension, StringComparison.OrdinalIgnoreCase))
		{
			resourceFileName = fileName;
			return true;
		}

		if (fileName.EndsWith(ResourceExtension + RemapExtension, StringComparison.OrdinalIgnoreCase))
		{
			resourceFileName = fileName[..^RemapExtension.Length];
			return true;
		}

		resourceFileName = string.Empty;
		return false;
	}

	private static ItemCardDefinition? LoadItemCardDefinition(string path)
	{
		Resource? resource = GD.Load(path);
		if (resource is ItemCardDefinitionResource itemCardResource)
		{
			return itemCardResource.ToDefinition();
		}

		if (resource == null)
		{
			return null;
		}

		return new ItemCardDefinition(
			GetResourceString(resource, "Id"),
			GetResourceString(resource, "DisplayName"),
			GetResourceString(resource, "SpriteName"),
			GetResourceString(resource, "DescriptionKey"),
			GetResourceBool(resource, "EnabledInGame", true),
			GetResourceBool(resource, "PlayableFromHand", true),
			GetResourceBool(resource, "CanBeQuestionReward", true),
			GetResourceString(resource, "ArtRunName", DefaultArtRunName),
			GetResourceString(resource, "FramePathPattern"),
			GetResourceInt(resource, "FrameCount", DefaultFrameCount),
			GetResourceInt(resource, "SortOrder", DefaultSortOrder),
			GetResourceString(resource, "Rarity", DefaultRarity),
			GetResourceString(resource, "TagsCsv"),
			GetResourceString(resource, "AttachmentOverlayTexturePath"),
			GetResourceInt(resource, "AttachmentOverlayPriority"),
			GetResourceString(resource, "PlayPresentation", CardPlayPresentationTypes.DirectTrigger),
			GetResourceString(resource, "Trigger", CardGameEvents.OnPlay),
			GetResourceString(resource, "EffectType", CardEffectTypes.None),
			GetResourceString(resource, "EffectValuesRaw"),
			GetResourceInt(resource, "Priority"),
			GetResourceString(resource, "AttachedTrigger"),
			GetResourceString(resource, "AttachedEffectType", CardEffectTypes.None),
			GetResourceString(resource, "AttachedEffectValuesRaw"),
			GetResourceInt(resource, "AttachedPriority"),
			GetResourceString(resource, "Icon"),
			GetResourceInt(resource, "Weight", DefaultWeight),
			GetResourceString(resource, "Description"));
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

	private static bool GetResourceBool(Resource resource, string propertyName, bool fallback = false)
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
	}
}
