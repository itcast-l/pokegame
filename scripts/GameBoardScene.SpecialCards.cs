using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene : ICardTriggerProvider
{
	IEnumerable<CardTriggerRequest> ICardTriggerProvider.CollectCardTriggers(CardGameEvent gameEvent, int sequenceStart)
	{
		int sequence = sequenceStart;
		foreach (ItemCardDefinition definition in CollectTriggerDefinitions(gameEvent))
		{
			ItemCardEffectRule rule = definition.EffectRule;
			if (!string.Equals(rule.Trigger, gameEvent.Trigger, StringComparison.Ordinal))
			{
				continue;
			}

			yield return new CardTriggerRequest(definition, rule, gameEvent, sequence++);
		}
	}

	private IEnumerable<ItemCardDefinition> CollectTriggerDefinitions(CardGameEvent gameEvent)
	{
		if (!string.IsNullOrEmpty(gameEvent.SourceCardId)
			&& _itemCardCatalog.TryGetDefinition(gameEvent.SourceCardId, out ItemCardDefinition sourceDefinition))
		{
			yield return sourceDefinition;
			yield break;
		}

		if (gameEvent.SourceTableCardIndex >= 0
			&& gameEvent.SourceTableCardIndex < _tableCards.Count
			&& TryGetTableCardTriggerDefinition(gameEvent.SourceTableCardIndex, out ItemCardDefinition tableDefinition))
		{
			yield return tableDefinition;
			yield break;
		}

		if (gameEvent.Trigger == CardGameEvents.OnCollect && gameEvent.CaptureStartIndex >= 0)
		{
			for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= gameEvent.CaptureStartIndex; tableCardIndex--)
			{
				if (TryGetTableCardTriggerDefinition(tableCardIndex, out ItemCardDefinition capturedDefinition))
				{
					yield return capturedDefinition;
				}
			}

			foreach (int tableCardIndex in gameEvent.ExtraTableCardIndexes.OrderByDescending(index => index))
			{
				if (TryGetTableCardTriggerDefinition(tableCardIndex, out ItemCardDefinition extraDefinition))
				{
					yield return extraDefinition;
				}
			}
		}
	}

	private bool TryGetTableCardTriggerDefinition(int tableCardIndex, out ItemCardDefinition definition)
	{
		definition = default;
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return false;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (tableCard.ItemOverlay.HasValue
			&& _itemCardCatalog.TryGetDefinition(tableCard.ItemOverlay.Value.Id, out definition))
		{
			return true;
		}

		if (tableCard.Card.HasValue
			&& TryGetFirstAttachedItemDefinition(tableCard.Card.Value, out definition))
		{
			return true;
		}

		return false;
	}

	private async Task<PlayCardResult> PublishAndResolveCardEventAsync(CardGameEvent gameEvent, int turnSequenceGeneration)
	{
		_cardEventBus.Publish(gameEvent, this);
		PlayCardResult finalResult = new(false, false);
		var resolved = new HashSet<string>(StringComparer.Ordinal);
		while (_cardEventBus.TryDequeue(out CardTriggerRequest request))
		{
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return new PlayCardResult(false, false);
			}

			string resolutionKey = BuildRuntimeResolutionKey(request);
			if (!resolved.Add(resolutionKey))
			{
				continue;
			}

			PlayCardResult result = await _effectResolver.ResolveAsync(request, turnSequenceGeneration);
			finalResult = MergePlayCardResults(finalResult, result);
		}

		return finalResult;
	}

	private async Task<PlayCardResult> ResolveItemCardOnPlayAsync(
		ItemCard itemCard,
		int ownerPlayerIndex,
		int sourceHandIndex,
		int targetPlayerIndex,
		int targetHandIndex,
		Vector2 originGlobalCenter,
		int turnSequenceGeneration)
	{
		return await PublishAndResolveCardEventAsync(
			new CardGameEvent(
				CardGameEvents.OnPlay,
				ownerPlayerIndex,
				itemCard.Id,
				sourceHandIndex,
				targetPlayerIndex: targetPlayerIndex,
				targetHandIndex: targetHandIndex,
				originGlobalCenter: originGlobalCenter),
			turnSequenceGeneration);
	}

	private static PlayCardResult MergePlayCardResults(PlayCardResult current, PlayCardResult next)
	{
		if (!current.Played)
		{
			return next;
		}

		return current with
		{
			Played = current.Played || next.Played,
			Captured = current.Captured || next.Captured,
			DeckCollected = current.DeckCollected || next.DeckCollected,
			QuestionRewardAnimation = current.QuestionRewardAnimation ?? next.QuestionRewardAnimation,
			CompletesTurn = current.CompletesTurn && next.CompletesTurn,
		};
	}

	private static string BuildRuntimeResolutionKey(CardTriggerRequest request)
	{
		return $"{request.Definition.Id}:{request.Rule.Trigger}:{request.Rule.EffectType}:{request.Event.SourceTableCardIndex}:{request.Event.SourceHandIndex}";
	}

	private bool IsItemEffectType(string itemCardId, string effectType)
	{
		return _itemCardCatalog.HasEffectType(itemCardId, effectType);
	}

	private bool IsItemEffectType(TableCard tableCard, string effectType)
	{
		return tableCard.ItemOverlay.HasValue && IsItemEffectType(tableCard.ItemOverlay.Value.Id, effectType);
	}

	private bool IsItemTag(string itemCardId, string tag)
	{
		return _itemCardCatalog.HasTag(itemCardId, tag);
	}

	private bool IsItemTag(TableCard tableCard, string tag)
	{
		return tableCard.ItemOverlay.HasValue && IsItemTag(tableCard.ItemOverlay.Value.Id, tag);
	}

	private bool IsAttachedEffectType(PlayingCard card, string effectType)
	{
		foreach (string attachedItemCardId in card.GetAttachedItemCardIds())
		{
			if (IsItemEffectType(attachedItemCardId, effectType))
			{
				return true;
			}
		}

		return false;
	}

	private bool IsAttachedModifier(PlayingCard card, string modifier)
	{
		foreach (string attachedItemCardId in card.GetAttachedItemCardIds())
		{
			if (_itemCardCatalog.TryGetDefinition(attachedItemCardId, out ItemCardDefinition definition)
				&& string.Equals(definition.EffectValues.GetString(CardEffectValueKeys.Modifier), modifier, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryGetAttachedItemIdByModifier(PlayingCard card, string modifier, out string itemCardId)
	{
		foreach (string attachedItemCardId in card.GetAttachedItemCardIds())
		{
			if (_itemCardCatalog.TryGetDefinition(attachedItemCardId, out ItemCardDefinition definition)
				&& string.Equals(definition.EffectValues.GetString(CardEffectValueKeys.Modifier), modifier, StringComparison.Ordinal))
			{
				itemCardId = attachedItemCardId;
				return true;
			}
		}

		itemCardId = string.Empty;
		return false;
	}

	private bool TryGetFirstAttachedItemDefinition(PlayingCard card, out ItemCardDefinition definition)
	{
		foreach (string attachedItemCardId in card.GetAttachedItemCardIds())
		{
			if (_itemCardCatalog.TryGetDefinition(attachedItemCardId, out definition))
			{
				return true;
			}
		}

		definition = default;
		return false;
	}

	private string GetAttachmentModifier(string itemCardId)
	{
		return _itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			? definition.EffectValues.GetString(CardEffectValueKeys.Modifier)
			: string.Empty;
	}

	private bool IsQuestionItemCard(string itemCardId)
	{
		return IsItemEffectType(itemCardId, CardEffectTypes.GrantReward);
	}

	private bool IsQuestionTableCard(TableCard tableCard)
	{
		return tableCard.ItemOverlay.HasValue && IsQuestionItemCard(tableCard.ItemOverlay.Value.Id);
	}

	private bool IsBombTableEffect(TableCard tableCard)
	{
		return IsItemEffectType(tableCard, CardEffectTypes.PlaceTablePairDestroy);
	}

	private bool IsFixedDuringTableShuffleByData(TableCard tableCard)
	{
		return tableCard.IsBuriedInSand || IsItemTag(tableCard, CardTags.Fixed);
	}

	private sealed class SpecialCardEffectResolver
	{
		private readonly GameBoardScene _runtime;

		public SpecialCardEffectResolver(GameBoardScene runtime)
		{
			_runtime = runtime;
		}

		public async Task<PlayCardResult> ResolveAsync(CardTriggerRequest request, int turnSequenceGeneration)
		{
			return request.Rule.EffectType switch
			{
				CardEffectTypes.PlaceTablePairDestroy => await _runtime.ResolvePlaceTablePairDestroyAsync(request, turnSequenceGeneration),
				CardEffectTypes.DrawCard => await _runtime.ResolveDrawCardEffectAsync(request, turnSequenceGeneration),
				CardEffectTypes.AttachModifier => await _runtime.ResolveAttachModifierEffectAsync(request, turnSequenceGeneration),
				CardEffectTypes.RevealCards => await _runtime.ResolveRevealCardsEffectAsync(request, turnSequenceGeneration),
				CardEffectTypes.ShuffleTable => await _runtime.ResolveShuffleTableEffectAsync(request, turnSequenceGeneration),
				CardEffectTypes.CollectTable => await _runtime.ResolveCollectTableEffectAsync(request, turnSequenceGeneration),
				CardEffectTypes.StealCard => _runtime.ResolveStealCardEffect(request),
				CardEffectTypes.StealDeckCards => _runtime.ResolveStealDeckCardsEffect(request),
				CardEffectTypes.DoubleCollect => await _runtime.ResolveDoubleCollectEffectAsync(request, turnSequenceGeneration),
				CardEffectTypes.ExtraTurn => await _runtime.ResolveExtraTurnEffectAsync(request, turnSequenceGeneration),
				CardEffectTypes.GrantReward => _runtime.ResolveGrantRewardEffect(request),
				CardEffectTypes.DestroyCards => await _runtime.ResolveDestroyCardsEffectAsync(request, turnSequenceGeneration),
				CardEffectTypes.ReverseCollect => _runtime.ResolveReverseCollectEffect(request),
				CardEffectTypes.BlockMatch => _runtime.ResolveBlockMatchEffect(request),
				_ => new PlayCardResult(false, false),
			};
		}
	}
}
