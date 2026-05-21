using Godot;
using System;
using System.Collections.Generic;

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
	public const string JackItemCardId = "J";
	public const string QueenItemCardId = "Q";
	public const string KingItemCardId = "K";

	private const string ItemCardCategoryId = "item_cards";
	private const string ItemCardRootPath = "res://art/cards/item_cards";
	private const string DefaultArtRunName = "run-004";
	private const int DefaultFrameCount = 4;
	private const int DefaultSortOrder = 1000;
	private const string DefaultRarity = "common";
	private const int DefaultWeight = 100;

	private static readonly Dictionary<string, ItemCardDefinition> DefaultsById = new()
	{
		[QuestionMarkItemCardId] = new ItemCardDefinition(QuestionMarkItemCardId, "Question Mark", "QuestionMark", "ITEM_DESC_QUESTION_MARK", true, false, false, DefaultArtRunName, DefaultFrameCount, 0, DefaultRarity, "fixed,reward", CardGameEvents.OnCollect, CardEffectTypes.GrantReward, "", 100, "", DefaultWeight),
		[BombItemCardId] = new ItemCardDefinition(BombItemCardId, "Bomb", "Bomb", "ITEM_DESC_BOMB", true, true, true, DefaultArtRunName, DefaultFrameCount, 10, "rare", "bomb", CardGameEvents.OnPlay, CardEffectTypes.PlaceTablePairDestroy, "pair_tag=bomb", 80, "", 35),
		[MagnetItemCardId] = new ItemCardDefinition(MagnetItemCardId, "Magnet", "Magnet", "ITEM_DESC_MAGNET", true, false, true, DefaultArtRunName, DefaultFrameCount, 20, "uncommon", "attachment", CardGameEvents.OnPlay, CardEffectTypes.AttachModifier, "modifier=magnet", 50, "", 70),
		[GreedItemCardId] = new ItemCardDefinition(GreedItemCardId, "Greed", "Greed", "ITEM_DESC_GREED", false, false, true, DefaultArtRunName, DefaultFrameCount, 30, "rare", "", CardGameEvents.OnPlay, CardEffectTypes.None, "", 10, "", 25),
		[LensItemCardId] = new ItemCardDefinition(LensItemCardId, "Lens", "Lens", "ITEM_DESC_LENS", true, true, true, DefaultArtRunName, DefaultFrameCount, 40, "uncommon", "", CardGameEvents.OnPlay, CardEffectTypes.RevealCards, "target=opponent_table_face_down;count=2", 40, "", 70),
		[DoubleItemCardId] = new ItemCardDefinition(DoubleItemCardId, "Double", "Double", "ITEM_DESC_DOUBLE", true, true, true, "run-005", DefaultFrameCount, 50, "rare", "attachment", CardGameEvents.OnPlay, CardEffectTypes.AttachModifier, "modifier=double", 60, "", 45),
		[OvertimeItemCardId] = new ItemCardDefinition(OvertimeItemCardId, "Overtime", "Overtime", "ITEM_DESC_OVERTIME", true, true, true, DefaultArtRunName, DefaultFrameCount, 60, "rare", "attachment", CardGameEvents.OnPlay, CardEffectTypes.AttachModifier, "modifier=overtime", 55, "", 45),
		[DrawCardItemCardId] = new ItemCardDefinition(DrawCardItemCardId, "Draw Card", "DrawCard", "ITEM_DESC_DRAW_CARD", true, true, true, DefaultArtRunName, DefaultFrameCount, 70, DefaultRarity, "", CardGameEvents.OnPlay, CardEffectTypes.DrawCard, "count=1", 30, "", 100),
		[ShuffleItemCardId] = new ItemCardDefinition(ShuffleItemCardId, "Shuffle", "Shuffle", "ITEM_DESC_SHUFFLE", true, true, true, DefaultArtRunName, DefaultFrameCount, 80, "uncommon", "", CardGameEvents.OnPlay, CardEffectTypes.ShuffleTable, "target=table", 20, "", 60),
	};

	private static readonly Dictionary<string, ItemCardDefinition> LegacyRuntimeRulesById = new()
	{
		[JackItemCardId] = new ItemCardDefinition(JackItemCardId, "Jack", "Jack", "ITEM_DESC_JACK", false, false, false, DefaultArtRunName, DefaultFrameCount, 900, "legacy", "", CardGameEvents.OnPlay, CardEffectTypes.CollectTable, "target=table", 30, "", 0),
		[QueenItemCardId] = new ItemCardDefinition(QueenItemCardId, "Queen", "Queen", "ITEM_DESC_QUEEN", false, false, false, DefaultArtRunName, DefaultFrameCount, 901, "legacy", "", CardGameEvents.OnPlay, CardEffectTypes.StealCard, "target=player_hand;count=1", 30, "", 0),
		[KingItemCardId] = new ItemCardDefinition(KingItemCardId, "King", "King", "ITEM_DESC_KING", false, false, false, DefaultArtRunName, DefaultFrameCount, 902, "legacy", "", CardGameEvents.OnPlay, CardEffectTypes.StealDeckCards, "target=player_deck;count=3", 30, "", 0),
	};

	private static ItemCardCatalog? _cached;

	private readonly Dictionary<string, ItemCardDefinition> _definitionsById = new(StringComparer.Ordinal);
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

		return LegacyRuntimeRulesById.TryGetValue(itemCardId, out definition);
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

	public static string GetDefaultDescriptionKey(string itemCardId)
	{
		return DefaultsById.TryGetValue(itemCardId, out ItemCardDefinition definition)
			? definition.DescriptionKey
			: itemCardId;
	}

	private static ItemCardCatalog Build(Node sceneNode)
	{
		var catalog = new ItemCardCatalog();
		List<ItemCardDefinition> definitions = LoadDefinitions(sceneNode, out bool loadedFromPandora);
		catalog.LoadedFromPandora = loadedFromPandora;
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
		var definitions = LoadPandoraDefinitions(sceneNode);
		loadedFromPandora = definitions.Count > 0;
		if (definitions.Count == 0)
		{
			definitions.AddRange(DefaultsById.Values);
		}

		definitions.Sort((a, b) =>
		{
			int orderComparison = a.SortOrder.CompareTo(b.SortOrder);
			return orderComparison != 0 ? orderComparison : string.CompareOrdinal(a.Id, b.Id);
		});
		return definitions;
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

		DefaultsById.TryGetValue(id, out ItemCardDefinition defaults);
		string displayName = GetEntityString(entity, "name", GetEntityName(entity, string.IsNullOrWhiteSpace(defaults.DisplayName) ? ToPascalCase(id) : defaults.DisplayName));
		string spriteName = GetEntityString(entity, "sprite_name", string.IsNullOrWhiteSpace(defaults.SpriteName) ? ToPascalCase(id) : defaults.SpriteName);
		string descriptionKey = GetEntityString(entity, "description_key", string.IsNullOrWhiteSpace(defaults.DescriptionKey) ? id : defaults.DescriptionKey);
		string description = GetEntityString(entity, "description", descriptionKey);
		string artRunName = GetEntityString(entity, "art_run", string.IsNullOrWhiteSpace(defaults.ArtRunName) ? DefaultArtRunName : defaults.ArtRunName);
		int frameCount = GetEntityInt(entity, "frame_count", defaults.FrameCount > 0 ? defaults.FrameCount : DefaultFrameCount);
		int sortOrder = GetEntityInt(entity, "sort_order", defaults.SortOrder != 0 ? defaults.SortOrder : DefaultSortOrder);
		bool enabledInGame = GetEntityBool(entity, "enabled_in_game", defaults.EnabledInGame);
		bool playableFromHand = GetEntityBool(entity, "playable_from_hand", defaults.PlayableFromHand);
		bool canBeQuestionReward = GetEntityBool(entity, "can_be_question_reward", defaults.CanBeQuestionReward);
		string rarity = GetEntityString(entity, "rarity", string.IsNullOrWhiteSpace(defaults.Rarity) ? DefaultRarity : defaults.Rarity);
		string tags = GetEntityString(entity, "tags", defaults.TagsCsv);
		string trigger = GetEntityString(entity, "trigger", defaults.Trigger);
		string effectType = GetEntityString(entity, "effect_type", defaults.EffectType);
		string effectValues = GetEntityString(entity, "effect_values", defaults.EffectValuesRaw);
		int priority = GetEntityInt(entity, "priority", defaults.Priority);
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
			Math.Max(1, frameCount),
			sortOrder,
			rarity,
			tags,
			trigger,
			effectType,
			effectValues,
			priority,
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
			string path = $"{ItemCardRootPath}/{definition.Id}/{definition.ArtRunName}/processed/frames/frame-{frameIndex:000}.png";
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
		int frameCount,
		int sortOrder,
		string rarity,
		string tagsCsv,
		string trigger,
		string effectType,
		string effectValuesRaw,
		int priority,
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
		FrameCount = frameCount;
		SortOrder = sortOrder;
		Rarity = rarity;
		TagsCsv = tagsCsv;
		Trigger = trigger;
		EffectType = effectType;
		EffectValuesRaw = effectValuesRaw;
		EffectValues = CardEffectValues.Parse(effectValuesRaw);
		Priority = priority;
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
	public int FrameCount { get; }
	public int SortOrder { get; }
	public string Rarity { get; }
	public IReadOnlyList<string> Tags { get; }
	public string TagsCsv { get; }
	public string Trigger { get; }
	public string EffectType { get; }
	public string EffectValuesRaw { get; }
	public CardEffectValues EffectValues { get; }
	public int Priority { get; }
	public string Icon { get; }
	public int Weight { get; }
	public string Description { get; }

	public ItemCardEffectRule EffectRule => new(Trigger, EffectType, EffectValues, Priority);

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
