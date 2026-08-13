using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
	private RuleCommandResult ApplyInitialize(
		InitializeMatchCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateInitialize(command);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		return ApplyMatchInitialization(
			command.PlayerCount,
			command.WinningScore,
			command.StartingHandSize,
			command.DealEqualRanks,
			command.Deck,
			command.LifecycleGeneration,
			turnGeneration: 1,
			command.CreateOpeningPresentationBarrier,
			command.StartingScores,
			command.MaximumHandSize,
			command.AdditionalStartingDraws,
			command.SeatCardSupplyLimits,
			intents);
	}

	private RuleCommandResult ApplyRestart(
		RestartMatchCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateInitializeConfiguration(
			command.PlayerCount,
			command.WinningScore,
			command.StartingHandSize,
			command.Deck,
			command.LifecycleGeneration,
			command.StartingScores,
			command.MaximumHandSize,
			command.AdditionalStartingDraws,
			command.SeatCardSupplyLimits);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		return ApplyMatchInitialization(
			command.PlayerCount,
			command.WinningScore,
			command.StartingHandSize,
			command.DealEqualRanks,
			command.Deck,
			command.LifecycleGeneration,
			command.TurnGeneration + 1,
			command.CreateOpeningPresentationBarrier,
			command.StartingScores,
			command.MaximumHandSize,
			command.AdditionalStartingDraws,
			command.SeatCardSupplyLimits,
			intents);
	}

	private RuleCommandResult ApplyScriptedInitialize(
		InitializeScriptedMatchCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateScriptedInitialize(command);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ResetForInitialization(
			_state,
			command.PlayerCount,
			command.WinningScore,
			command.LifecycleGeneration,
			command.TurnGeneration + 1);
		_state.CurrentTurnPlayerIndex = command.CurrentTurnSeat;
		for (int seat = 0; seat < command.PlayerCount; seat++)
		{
			PlayerStateSnapshot player = command.Players[seat];
			_state.PlayerDecks[seat].AddRange(player.Deck.Select(FromLogicalCard));
			_state.PlayerHands[seat].AddRange(player.Hand.Select(FromLogicalCard));
			_state.PlayerItemHands[seat].AddRange(
				player.ItemHand.Select(item => new ItemCard(item.Id)));
			_state.PlayerScores[seat] = player.Score;
		}

		foreach (TableCardState tableCard in command.TableCards)
		{
			TableCard restored = TableCard.Restore(
				tableCard.RuntimeInstanceId,
				tableCard.Card.HasValue ? FromLogicalCard(tableCard.Card.Value) : null,
				tableCard.IsFaceUp,
				tableCard.ItemOverlay.HasValue ? new ItemCard(tableCard.ItemOverlay.Value.Id) : null,
				tableCard.ItemOwnerSeat,
				tableCard.CardOwnerSeat,
				tableCard.RemoveOnCapture);
			restored.IsHiddenDuringCapture = tableCard.IsHiddenDuringCapture;
			restored.IsBuriedInSand = tableCard.IsBuried;
			restored.IsSlimeCoated = tableCard.IsScoreSuppressed;
			_state.TableCards.Add(restored);
		}

		_state.RuleRevision = 1;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.MatchInitialized,
			"match.scripted_initialized"));
		return Accepted(intents);
	}

	private static RuleRejection? ValidateScriptedInitialize(InitializeScriptedMatchCommand command)
	{
		if (command.LifecycleGeneration <= 0
			|| command.PlayerCount is < 2 or > 4
			|| command.CurrentTurnSeat < 0
			|| command.CurrentTurnSeat >= command.PlayerCount
			|| command.WinningScore <= 0
			|| command.Players.IsDefault
			|| command.Players.Length != command.PlayerCount
			|| command.TableCards.IsDefault)
		{
			return new RuleRejection(RuleRejectionCode.InvalidConfiguration, string.Empty);
		}

		var runtimeIds = new HashSet<long>();
		for (int seat = 0; seat < command.Players.Length; seat++)
		{
			PlayerStateSnapshot player = command.Players[seat];
			if (player.Seat != seat
				|| player.Score < 0
				|| player.Deck.IsDefault
				|| player.Hand.IsDefault
				|| player.ItemHand.IsDefault
				|| player.ItemHand.Any(item => string.IsNullOrWhiteSpace(item.Id)))
			{
				return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Invalid scripted player state.");
			}

			foreach (LogicalCardState card in player.Deck.Concat(player.Hand))
			{
				if (!IsValidLogicalCard(card) || !runtimeIds.Add(card.RuntimeInstanceId))
				{
					return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Invalid scripted card state.");
				}
			}
		}

		var tableRuntimeIds = new HashSet<long>();
		foreach (TableCardState tableCard in command.TableCards)
		{
			if (tableCard.RuntimeInstanceId <= 0
				|| !tableRuntimeIds.Add(tableCard.RuntimeInstanceId)
				|| tableCard.Card.HasValue
					&& (!IsValidLogicalCard(tableCard.Card.Value)
						|| !runtimeIds.Add(tableCard.Card.Value.RuntimeInstanceId))
				|| tableCard.ItemOverlay.HasValue
					&& string.IsNullOrWhiteSpace(tableCard.ItemOverlay.Value.Id)
				|| !tableCard.Card.HasValue && !tableCard.ItemOverlay.HasValue)
			{
				return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Invalid scripted table state.");
			}
		}

		return null;
	}

	private static bool IsValidLogicalCard(LogicalCardState card)
	{
		return card.RuntimeInstanceId > 0
			&& !string.IsNullOrWhiteSpace(card.Code)
			&& card.Suit != null
			&& (card.Suit.Length == 0 || !string.IsNullOrWhiteSpace(card.Suit))
			&& !string.IsNullOrWhiteSpace(card.Rank)
			&& !card.AttachedEffectIds.IsDefault;
	}

	private RuleCommandResult ApplyMatchInitialization(
		int playerCount,
		int winningScore,
		int startingHandSize,
		bool dealEqualRanks,
		ImmutableArray<LogicalCardState> logicalDeck,
		int lifecycleGeneration,
		int turnGeneration,
		bool createOpeningPresentationBarrier,
		ImmutableArray<int> startingScores,
		int maximumHandSize,
		ImmutableArray<int> additionalStartingDraws,
		ImmutableArray<int> seatCardSupplyLimits,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		ResetForInitialization(
			_state,
			playerCount,
			winningScore,
			lifecycleGeneration,
			turnGeneration);
		List<PlayingCard> deck = logicalDeck.Select(FromLogicalCard).ToList();
		if (dealEqualRanks)
		{
			DealEqualRanks(_state, deck, _randomSource);
		}
		else
		{
			Shuffle(deck, _randomSource);
			Cut(deck, _randomSource);
			for (int cardIndex = 0; cardIndex < deck.Count; cardIndex++)
			{
				_state.PlayerDecks[cardIndex % playerCount].Add(deck[cardIndex]);
			}
		}

		for (int seat = 0; seat < playerCount; seat++)
		{
			List<PlayingCard> playerDeck = _state.PlayerDecks[seat];
			List<PlayingCard> playerHand = _state.PlayerHands[seat];
			int drawCount = Math.Min(startingHandSize, playerDeck.Count);
			for (int index = 0; index < drawCount; index++)
			{
				playerHand.Add(playerDeck[0]);
				playerDeck.RemoveAt(0);
			}

			int additionalDrawCount = additionalStartingDraws.IsDefault
				? 0
				: additionalStartingDraws[seat];
			for (int index = 0;
				index < additionalDrawCount
					&& playerDeck.Count > 0
					&& playerHand.Count < maximumHandSize;
				index++)
			{
				playerHand.Add(playerDeck[0]);
				playerDeck.RemoveAt(0);
			}

			int cardSupplyLimit = seatCardSupplyLimits.IsDefault
				? -1
				: seatCardSupplyLimits[seat];
			if (cardSupplyLimit >= 0)
			{
				int retainedHandCount = Math.Min(cardSupplyLimit, playerHand.Count);
				if (playerHand.Count > retainedHandCount)
				{
					playerHand.RemoveRange(retainedHandCount, playerHand.Count - retainedHandCount);
				}

				int retainedDeckCount = Math.Min(
					Math.Max(0, cardSupplyLimit - retainedHandCount),
					playerDeck.Count);
				if (playerDeck.Count > retainedDeckCount)
				{
					playerDeck.RemoveRange(retainedDeckCount, playerDeck.Count - retainedDeckCount);
				}
			}

			_state.PlayerScores[seat] = startingScores.IsDefault ? 0 : startingScores[seat];
		}

		_state.RuleRevision = 1;
		if (createOpeningPresentationBarrier)
		{
			_state.RulePendingPresentationBarrierId = _nextPresentationBarrierId++;
		}

		intents.Add(new PresentationIntent(
			PresentationIntentKind.MatchInitialized,
			"match.initialized",
			BarrierId: _state.RulePendingPresentationBarrierId));
		return Accepted(intents);
	}

	private static RuleRejection? ValidateInitialize(InitializeMatchCommand command)
	{
		return ValidateInitializeConfiguration(
			command.PlayerCount,
			command.WinningScore,
			command.StartingHandSize,
			command.Deck,
			command.LifecycleGeneration,
			command.StartingScores,
			command.MaximumHandSize,
			command.AdditionalStartingDraws,
			command.SeatCardSupplyLimits);
	}

	private static RuleRejection? ValidateInitializeConfiguration(
		int playerCount,
		int winningScore,
		int startingHandSize,
		ImmutableArray<LogicalCardState> deck,
		int lifecycleGeneration,
		ImmutableArray<int> startingScores,
		int maximumHandSize,
		ImmutableArray<int> additionalStartingDraws,
		ImmutableArray<int> seatCardSupplyLimits)
	{
		if (lifecycleGeneration <= 0
			|| playerCount is < 2 or > 4
			|| winningScore <= 0
			|| startingHandSize < 0
			|| maximumHandSize < startingHandSize
			|| deck.IsDefault
			|| deck.Length < playerCount * startingHandSize
			|| !startingScores.IsDefault && startingScores.Length != playerCount
			|| !startingScores.IsDefault && startingScores.Any(score => score < 0)
			|| !additionalStartingDraws.IsDefault && additionalStartingDraws.Length != playerCount
			|| !additionalStartingDraws.IsDefault && additionalStartingDraws.Any(drawCount => drawCount < 0)
			|| !additionalStartingDraws.IsDefault
				&& additionalStartingDraws.Any(drawCount => drawCount > maximumHandSize - startingHandSize)
			|| !seatCardSupplyLimits.IsDefault && seatCardSupplyLimits.Length != playerCount
			|| !seatCardSupplyLimits.IsDefault && seatCardSupplyLimits.Any(limit => limit < -1))
		{
			return new RuleRejection(RuleRejectionCode.InvalidConfiguration, string.Empty);
		}

		var runtimeIds = new HashSet<long>();
		foreach (LogicalCardState card in deck)
		{
			if (card.RuntimeInstanceId <= 0
				|| string.IsNullOrWhiteSpace(card.Code)
				|| string.IsNullOrWhiteSpace(card.Suit)
				|| string.IsNullOrWhiteSpace(card.Rank)
				|| card.AttachedEffectIds.IsDefault
				|| !runtimeIds.Add(card.RuntimeInstanceId))
			{
				return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Deck contains an invalid card.");
			}
		}

		return null;
	}


	private static void ResetForInitialization(
		GameBoardState state,
		int playerCount,
		int winningScore,
		int lifecycleGeneration,
		int turnGeneration)
	{
		state.PlayerCount = playerCount;
		state.WinningScore = winningScore;
		state.CurrentTurnPlayerIndex = 0;
		state.TurnSequenceGeneration = turnGeneration;
		state.CompletedTurnsSinceQuestionCardRefresh = 0;
		state.QuestionCardRefreshAnchorPlayerIndex = -1;
		state.MatchOutcomeKind = global::MatchOutcomeKind.InProgress;
		state.WinnerPlayerIndex = -1;
		state.MandatorySelection = MandatorySelectionState.None;
		state.PendingAttachmentRewards.Clear();
		state.ExtraActions.Reset();
		state.MatchFlow = MatchFlowState.NewMatch;
		state.RuleSessionInitialized = true;
		state.RuleSessionEnded = false;
		state.RuleRevision = 0;
		state.RuleLifecycleGeneration = lifecycleGeneration;
		state.RulePendingPresentationBarrierId = 0;
		state.RulePendingTableOrderRuntimeInstanceIds.Clear();

		state.PlayerDecks.Clear();
		state.PlayerHands.Clear();
		state.PlayerItemHands.Clear();
		state.TableCards.Clear();
		Array.Clear(state.PlayerScores);
		for (int seat = 0; seat < CompatibilityPlayerCapacity; seat++)
		{
			state.PlayerDecks.Add(new List<PlayingCard>());
			state.PlayerHands.Add(new List<PlayingCard>());
			state.PlayerItemHands.Add(new List<ItemCard>());
		}
	}


	private static void DealEqualRanks(
		GameBoardState state,
		List<PlayingCard> deck,
		IRuleRandomSource randomSource)
	{
		var cardsByRank = new Dictionary<string, List<PlayingCard>>(StringComparer.Ordinal);
		foreach (PlayingCard card in deck)
		{
			if (!cardsByRank.TryGetValue(card.Rank, out List<PlayingCard>? rankCards))
			{
				rankCards = new List<PlayingCard>();
				cardsByRank.Add(card.Rank, rankCards);
			}

			rankCards.Add(card);
		}

		var remainingCards = new List<PlayingCard>();
		foreach (List<PlayingCard> rankCards in cardsByRank.Values)
		{
			Shuffle(rankCards, randomSource);
			int cardsPerPlayer = rankCards.Count / state.PlayerCount;
			for (int seat = 0; seat < state.PlayerCount; seat++)
			{
				for (int cardOffset = 0; cardOffset < cardsPerPlayer; cardOffset++)
				{
					state.PlayerDecks[seat].Add(rankCards[seat * cardsPerPlayer + cardOffset]);
				}
			}

			for (int index = cardsPerPlayer * state.PlayerCount; index < rankCards.Count; index++)
			{
				remainingCards.Add(rankCards[index]);
			}
		}

		Shuffle(remainingCards, randomSource);
		foreach (PlayingCard card in remainingCards)
		{
			int seat = randomSource.NextIntInclusive(0, state.PlayerCount - 1);
			state.PlayerDecks[seat].Add(card);
		}

		for (int seat = 0; seat < state.PlayerCount; seat++)
		{
			Shuffle(state.PlayerDecks[seat], randomSource);
			Cut(state.PlayerDecks[seat], randomSource);
		}
	}

	private static void Shuffle<T>(List<T> values, IRuleRandomSource randomSource)
	{
		for (int index = values.Count - 1; index > 0; index--)
		{
			int swapIndex = randomSource.NextIntInclusive(0, index);
			(values[index], values[swapIndex]) = (values[swapIndex], values[index]);
		}
	}

	private static void Cut<T>(List<T> values, IRuleRandomSource randomSource)
	{
		if (values.Count < 2)
		{
			return;
		}

		int cutIndex = randomSource.NextIntInclusive(1, values.Count - 1);
		values.Reverse(0, cutIndex);
		values.Reverse(cutIndex, values.Count - cutIndex);
		values.Reverse();
	}

}
