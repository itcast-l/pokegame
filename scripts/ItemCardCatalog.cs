using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
{
	public const string QuestionMarkItemCardId = "question_mark";
	public const string BombItemCardId = "bomb";
	public const string MagnetItemCardId = "magnet";
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

	public static bool TryLoad(Node sceneNode, out ItemCardCatalog catalog, out string errorMessage)
	{
		if (_cached != null)
		{
			catalog = _cached;
			errorMessage = string.Empty;
			return true;
		}

		if (!TryBuild(sceneNode, out catalog, out errorMessage))
		{
			return false;
		}

		_cached = catalog;
		return true;
	}

	public static ItemCardCatalog LoadRequired(Node sceneNode)
	{
		if (TryLoad(sceneNode, out ItemCardCatalog catalog, out string errorMessage))
		{
			return catalog;
		}

		throw new InvalidOperationException($"Item card catalog preflight failed: {errorMessage}");
	}

	public IReadOnlyList<string> AllIds => _allIds;

	public IReadOnlyList<string> RewardIds => _rewardIds;

	public string GameplayManifestHash =>
		GameplayManifestHashRules.ComputeItemCardManifestHash(_definitionsById.Values);

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

		definition = default;
		return false;
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

	private static bool TryBuild(Node sceneNode, out ItemCardCatalog catalog, out string errorMessage)
	{
		catalog = new ItemCardCatalog();
		if (!TryLoadVisualDefinitions(out IReadOnlyDictionary<string, ItemCardVisualDefinition> visualsById, out errorMessage)
			|| !TryLoadDefinitions(sceneNode, visualsById, out List<ItemCardDefinition> definitions, out errorMessage))
		{
			catalog = default!;
			return false;
		}

		foreach (ItemCardDefinition definition in definitions)
		{
			if (!catalog._definitionsById.TryAdd(definition.Id, definition))
			{
				errorMessage = $"Duplicate item card definition '{definition.Id}'.";
				catalog = default!;
				return false;
			}

			if (!catalog._idsBySpriteName.TryAdd(definition.SpriteName, definition.Id))
			{
				errorMessage = $"Duplicate item card sprite name '{definition.SpriteName}'.";
				catalog = default!;
				return false;
			}
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

		errorMessage = string.Empty;
		return true;
	}

}
