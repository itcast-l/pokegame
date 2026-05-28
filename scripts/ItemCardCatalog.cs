using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed class ItemCardCatalog
{
	public const string QuestionMarkItemCardId = "question_mark";
	public const string BombItemCardId = "bomb";
	public const string MagnetItemCardId = "magnet";
	public const string GreedItemCardId = "greed";
	public const string LensItemCardId = "lens";
	public const string DoubleItemCardId = "double";
	public const string OvertimeItemCardId = "overtime";
	public const string DrawCardItemCardId = "draw_card";
	public const string ShuffleItemCardId = "shuffle";

	private const string ItemCardCategoryId = "item_cards";
	private const string ItemCardResourceDirectory = "res://resources/item_cards";
	private const string ItemCardRootPath = "res://art/cards/item_cards";
	private const string DefaultArtRunName = "run-004";
	private const int DefaultFrameCount = 4;
	private const int DefaultSortOrder = 1000;
	private const string DefaultRarity = "common";
	private const int DefaultWeight = 100;

	private static ItemCardCatalog? _cached;
	private static IReadOnlyDictionary<string, ItemCardDefinition>? _resourceDefinitionsById;

	private readonly Dictionary<string, ItemCardDefinition> _definitionsById = new(StringComparer.Ordinal);
	private readonly Dictionary<string, ItemCardDefinition> _runtimeRulesById = new(StringComparer.Ordinal);
	private readonly Dictionary<string, string> _idsBySpriteName = new(StringComparer.Ordinal);
	private readonly List<string> _allIds = new();
	private readonly List<string> _rewardIds = new();
	private readonly HashSet<string> _playableIds = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Texture2D> _firstFrameTextures = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Texture2D[]> _frameTextures = new(StringComparer.Ordinal);

	private ItemCardCatalog()
	{
	}

	public static ItemCardCatalog Load(Node sceneNode)
	{
		if (_cached == null || !_cached.LoadedFromPandora && CanReadPandora(sceneNode))
		{
			_cached = Build(sceneNode);
		}

		return _cached;
	}

	public IReadOnlyList<string> AllIds => _allIds;

	public IReadOnlyList<string> RewardIds => _rewardIds;

	private bool LoadedFromPandora { get; set; }

	public bool TryGetIdForSpriteName(string spriteName, out string itemCardId)
	{
		if (_idsBySpriteName.TryGetValue(spriteName, out string? value))
		{
			itemCardId = value;
			return true;
		}

		itemCardId = string.Empty;
		return false;
	}

	public bool IsEnabled(string itemCardId)
	{
		return _definitionsById.TryGetValue(itemCardId, out ItemCardDefinition definition) && definition.EnabledInGame;
	}

	public bool IsPlayable(string itemCardId)
	{
		return _playableIds.Contains(itemCardId);
	}

	public bool TryGetDefinition(string itemCardId, out ItemCardDefinition definition)
	{
		if (_definitionsById.TryGetValue(itemCardId, out definition))
		{
			return true;
		}

		return _runtimeRulesById.TryGetValue(itemCardId, out definition);
	}

	public bool HasEffectType(string itemCardId, string effectType)
	{
		return TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			&& string.Equals(definition.EffectType, effectType, StringComparison.Ordinal);
	}

	public bool HasTag(string itemCardId, string tag)
	{
		return TryGetDefinition(itemCardId, out ItemCardDefinition definition) && definition.HasTag(tag);
	}

	public string GetDescriptionKey(string itemCardId)
	{
		return _definitionsById.TryGetValue(itemCardId, out ItemCardDefinition definition)
			? definition.DescriptionKey
			: itemCardId;
	}

	public bool TryGetFirstFrameTexture(string itemCardId, out Texture2D texture)
	{
		if (_firstFrameTextures.TryGetValue(itemCardId, out Texture2D? value))
		{
			texture = value;
			return true;
		}

		texture = default!;
		return false;
	}

	public bool TryGetFrameTextures(string itemCardId, out Texture2D[] textures)
	{
		if (_frameTextures.TryGetValue(itemCardId, out Texture2D[]? value))
		{
			textures = value;
			return true;
		}

		textures = Array.Empty<Texture2D>();
		return false;
	}

	private static ItemCardCatalog Build(Node sceneNode)
	{
		var catalog = new ItemCardCatalog();
		List<ItemCardDefinition> definitions = LoadDefinitions(sceneNode, out bool loadedFromPandora);
		catalog.LoadedFromPandora = loadedFromPandora;
		foreach (ItemCardDefinition runtimeRule in GetResourceDefinitions().Values)
		{
			if (IsRuntimeRuleDefinition(runtimeRule))
			{
				catalog._runtimeRulesById[runtimeRule.Id] = runtimeRule;
			}
		}

		foreach (ItemCardDefinition definition in definitions)
		{
			if (string.IsNullOrWhiteSpace(definition.Id) || catalog._definitionsById.ContainsKey(definition.Id))
			{
				continue;
			}

			catalog._definitionsById[definition.Id] = definition;
			catalog._idsBySpriteName[definition.SpriteName] = definition.Id;
			catalog._allIds.Add(definition.Id);
			if (definition.EnabledInGame && definition.CanBeQuestionReward)
			{
				catalog._rewardIds.Add(definition.Id);
			}
			if (definition.EnabledInGame && definition.PlayableFromHand)
			{
				catalog._playableIds.Add(definition.Id);
			}

			Texture2D[] frames = LoadFrames(definition);
			if (frames.Length == 0)
			{
				continue;
			}

			catalog._frameTextures[definition.Id] = frames;
			catalog._firstFrameTextures[definition.Id] = frames[0];
		}

		return catalog;
	}

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

	private static Texture2D[] LoadFrames(ItemCardDefinition definition)
	{
		var frames = new List<Texture2D>();
		for (int frameIndex = 1; frameIndex <= definition.FrameCount; frameIndex++)
		{
			string path = GetFramePath(definition, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing item card frame: {path}");
				continue;
			}

			frames.Add(texture);
		}

		return frames.ToArray();
	}

	private static string GetFramePath(ItemCardDefinition definition, int frameIndex)
	{
		return string.IsNullOrWhiteSpace(definition.FramePathPattern)
			? $"{ItemCardRootPath}/{definition.Id}/{definition.ArtRunName}/processed/frames/frame-{frameIndex:000}.png"
			: string.Format(CultureInfo.InvariantCulture, definition.FramePathPattern, frameIndex);
	}
}

public readonly struct ItemCardDefinition
{
	public ItemCardDefinition(
		string id,
		string displayName,
		string spriteName,
		string descriptionKey,
		bool enabledInGame,
		bool playableFromHand,
		bool canBeQuestionReward,
		string artRunName,
		string framePathPattern,
		int frameCount,
		int sortOrder,
		string rarity,
		string tagsCsv,
		string attachmentOverlayTexturePath,
		int attachmentOverlayPriority,
		string playPresentation,
		string trigger,
		string effectType,
		string effectValuesRaw,
		int priority,
		string attachedTrigger,
		string attachedEffectType,
		string attachedEffectValuesRaw,
		int attachedPriority,
		string icon,
		int weight,
		string description = "")
	{
		Id = id;
		DisplayName = displayName;
		SpriteName = spriteName;
		DescriptionKey = descriptionKey;
		EnabledInGame = enabledInGame;
		PlayableFromHand = playableFromHand;
		CanBeQuestionReward = canBeQuestionReward;
		ArtRunName = artRunName;
		FramePathPattern = string.IsNullOrWhiteSpace(framePathPattern) ? string.Empty : framePathPattern;
		FrameCount = frameCount;
		SortOrder = sortOrder;
		Rarity = rarity;
		TagsCsv = tagsCsv;
		AttachmentOverlayTexturePath = attachmentOverlayTexturePath;
		AttachmentOverlayPriority = attachmentOverlayPriority;
		PlayPresentation = string.IsNullOrWhiteSpace(playPresentation)
			? CardPlayPresentationTypes.DirectTrigger
			: playPresentation;
		Trigger = trigger;
		EffectType = effectType;
		EffectValuesRaw = effectValuesRaw;
		EffectValues = CardEffectValues.Parse(effectValuesRaw);
		Priority = priority;
		AttachedTrigger = attachedTrigger;
		AttachedEffectType = attachedEffectType;
		AttachedEffectValuesRaw = attachedEffectValuesRaw;
		AttachedEffectValues = CardEffectValues.Parse(attachedEffectValuesRaw);
		AttachedPriority = attachedPriority;
		Icon = icon;
		Weight = weight;
		Description = description;
		Tags = ParseTags(tagsCsv);
	}

	public string Id { get; }
	public string DisplayName { get; }
	public string SpriteName { get; }
	public string DescriptionKey { get; }
	public bool EnabledInGame { get; }
	public bool PlayableFromHand { get; }
	public bool CanBeQuestionReward { get; }
	public string ArtRunName { get; }
	public string FramePathPattern { get; }
	public int FrameCount { get; }
	public int SortOrder { get; }
	public string Rarity { get; }
	public IReadOnlyList<string> Tags { get; }
	public string TagsCsv { get; }
	public string AttachmentOverlayTexturePath { get; }
	public int AttachmentOverlayPriority { get; }
	public string PlayPresentation { get; }
	public string Trigger { get; }
	public string EffectType { get; }
	public string EffectValuesRaw { get; }
	public CardEffectValues EffectValues { get; }
	public int Priority { get; }
	public string AttachedTrigger { get; }
	public string AttachedEffectType { get; }
	public string AttachedEffectValuesRaw { get; }
	public CardEffectValues AttachedEffectValues { get; }
	public int AttachedPriority { get; }
	public string Icon { get; }
	public int Weight { get; }
	public string Description { get; }

	public ItemCardEffectRule EffectRule => new(Trigger, EffectType, EffectValues, Priority);
	public ItemCardEffectRule AttachedEffectRule => new(AttachedTrigger, AttachedEffectType, AttachedEffectValues, AttachedPriority);

	public bool HasTag(string tag)
	{
		foreach (string existingTag in Tags)
		{
			if (string.Equals(existingTag, tag, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static IReadOnlyList<string> ParseTags(string tagsCsv)
	{
		if (string.IsNullOrWhiteSpace(tagsCsv))
		{
			return Array.Empty<string>();
		}

		string[] tags = tagsCsv.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return tags.Length == 0 ? Array.Empty<string>() : tags;
	}
}
