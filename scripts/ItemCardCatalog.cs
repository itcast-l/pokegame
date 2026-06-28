using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
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
	public const string EndpointCrownItemCardId = "endpoint_crown";
	public const string BorrowTideItemCardId = "borrow_tide";
	public const string ReverseAnchorItemCardId = "reverse_anchor";
	public const string MidTideUpgradeItemCardId = "mid_tide_upgrade";
	public const string ReverseFlowAnchorItemCardId = "reverse_flow_anchor";
	public const string EmptyTideContinuationItemCardId = "empty_tide_continuation";
	public const string SuitCollectItemCardId = "suit_collect";

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
		return _definitionsById.TryGetValue(itemCardId, out ItemCardDefinition definition)
			&& definition.EnabledInGame
			&& ItemCardEffectSupport.IsSupportedEffectType(definition.EffectType);
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
			if (definition.EnabledInGame && !ItemCardEffectSupport.IsSupportedEffectType(definition.EffectType))
			{
				GD.PushWarning($"Disabled item card '{definition.Id}' because effect type '{definition.EffectType}' is not supported.");
			}

			if (definition.EnabledInGame
				&& ItemCardEffectSupport.IsSupportedEffectType(definition.EffectType)
				&& definition.CanBeQuestionReward)
			{
				catalog._rewardIds.Add(definition.Id);
			}
			if (definition.EnabledInGame
				&& ItemCardEffectSupport.IsSupportedEffectType(definition.EffectType)
				&& definition.PlayableFromHand)
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

}
