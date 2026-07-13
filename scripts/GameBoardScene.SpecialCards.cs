using System;
using System.Collections.Generic;
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
		if (gameEvent.Trigger == CardGameEvents.OnCollect
			&& (gameEvent.CaptureStartIndex < 0 || !HasCapturedMiddleTableCards(gameEvent.CaptureStartIndex)))
		{
			yield break;
		}

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

			if (gameEvent.ExtraTableCardIndexes.Count == 0)
			{
				yield break;
			}

			for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
			{
				if (!ContainsTableCardIndex(gameEvent.ExtraTableCardIndexes, tableCardIndex))
				{
					continue;
				}

				if (TryGetTableCardTriggerDefinition(tableCardIndex, out ItemCardDefinition extraDefinition))
				{
					yield return extraDefinition;
				}
			}
		}
	}

	private static bool ContainsTableCardIndex(IReadOnlyCollection<int> tableCardIndexes, int targetTableCardIndex)
	{
		foreach (int tableCardIndex in tableCardIndexes)
		{
			if (tableCardIndex == targetTableCardIndex)
			{
				return true;
			}
		}

		return false;
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
		long dispatchId = _cardEventBus.BeginDispatch();
		try
		{
			if (turnSequenceGeneration != _turnSequenceGeneration
				|| IsGameOver())
			{
				return new PlayCardResult(false, false);
			}

			_cardEventBus.Publish(dispatchId, gameEvent, this);
			PlayCardResult finalResult = new(false, false);
			var resolved = new HashSet<CardTriggerResolutionKey>();
			while (turnSequenceGeneration == _turnSequenceGeneration
				&& !IsGameOver()
				&& _cardEventBus.TryDequeue(
					dispatchId,
					out CardTriggerRequest request))
			{
				CardTriggerResolutionKey resolutionKey = BuildRuntimeResolutionKey(request);
				if (!resolved.Add(resolutionKey))
				{
					continue;
				}

				PlayCardResult result = await _effectResolver.ResolveAsync(request, turnSequenceGeneration);
				finalResult = MergePlayCardResults(finalResult, result);
			}

			return turnSequenceGeneration == _turnSequenceGeneration
				&& !IsGameOver()
				? finalResult
				: new PlayCardResult(false, false);
		}
		finally
		{
			_cardEventBus.EndDispatch(dispatchId);
		}
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
		PlayCardResult result = await PublishAndResolveCardEventAsync(
			new CardGameEvent(
				CardGameEvents.OnPlay,
				ownerPlayerIndex,
				itemCard.Id,
				sourceHandIndex,
				targetPlayerIndex: targetPlayerIndex,
				targetHandIndex: targetHandIndex,
				originGlobalCenter: originGlobalCenter),
			turnSequenceGeneration);
			return result;
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

	private static CardTriggerResolutionKey BuildRuntimeResolutionKey(CardTriggerRequest request)
	{
		return new CardTriggerResolutionKey(
			request.Definition.Id,
			request.Rule.Trigger,
			request.Rule.EffectType,
			request.Event.SourceTableCardIndex,
			request.Event.SourceHandIndex);
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
			if (_itemCardCatalog.TryGetDefinition(attachedItemCardId, out ItemCardDefinition definition)
				&& string.Equals(definition.AttachedEffectType, effectType, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryGetAttachedItemIdByEffectType(PlayingCard card, string effectType, out string itemCardId)
	{
		foreach (string attachedItemCardId in card.GetAttachedItemCardIds())
		{
			if (_itemCardCatalog.TryGetDefinition(attachedItemCardId, out ItemCardDefinition definition)
				&& string.Equals(definition.AttachedEffectType, effectType, StringComparison.Ordinal))
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
		private delegate Task<PlayCardResult> ResolveEffectAsync(CardTriggerRequest request, int turnSequenceGeneration);

		private readonly GameBoardScene _runtime;
		private readonly Dictionary<string, ResolveEffectAsync> _resolvers;

		public SpecialCardEffectResolver(GameBoardScene runtime)
		{
			_runtime = runtime;
			_resolvers = new Dictionary<string, ResolveEffectAsync>(StringComparer.Ordinal)
			{
				[CardEffectTypes.PlaceTablePairDestroy] = _runtime.ResolvePlaceTablePairDestroyAsync,
				[CardEffectTypes.DrawCard] = _runtime.ResolveDrawCardEffectAsync,
				[CardEffectTypes.AttachModifier] = _runtime.ResolveAttachModifierEffectAsync,
				[CardEffectTypes.RevealCards] = _runtime.ResolveRevealCardsEffectAsync,
				[CardEffectTypes.ShuffleTable] = _runtime.ResolveShuffleTableEffectAsync,
				[CardEffectTypes.CollectTable] = _runtime.ResolveCollectTableEffectAsync,
				[CardEffectTypes.StealCard] = _runtime.ResolveStealCardEffectAsync,
					[CardEffectTypes.StealDeckCards] = _runtime.ResolveStealDeckCardsEffectAsync,
				[CardEffectTypes.DoubleCollect] = _runtime.ResolveDoubleCollectEffectAsync,
				[CardEffectTypes.ExtraTurn] = _runtime.ResolveExtraTurnEffectAsync,
				[CardEffectTypes.GrantReward] = (request, _) => Task.FromResult(_runtime.ResolveGrantRewardEffect(request)),
				[CardEffectTypes.DestroyCards] = _runtime.ResolveDestroyCardsEffectAsync,
			};
			if (!ItemCardEffectContractRegistry.HasExactResolverCoverage(
				_resolvers.Keys,
				out string coverageError))
			{
				throw new InvalidOperationException(coverageError);
			}
		}

		public Task<PlayCardResult> ResolveAsync(CardTriggerRequest request, int turnSequenceGeneration)
		{
			return _resolvers.TryGetValue(request.Rule.EffectType, out ResolveEffectAsync? resolver)
				? resolver(request, turnSequenceGeneration)
				: ResolveUnsupportedEffectAsync(request);
		}

		private static Task<PlayCardResult> ResolveUnsupportedEffectAsync(CardTriggerRequest request)
		{
			GD.PushWarning($"Unsupported item card effect type '{request.Rule.EffectType}' for card '{request.Definition.Id}'.");
			return Task.FromResult(new PlayCardResult(false, false));
		}
	}
}
