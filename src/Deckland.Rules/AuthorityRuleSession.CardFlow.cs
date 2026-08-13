using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
	private RuleCommandResult ApplyPlayCardFromHand(
		PlayCardFromHandCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidatePlayCardFromHand(command);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		PlayingCard playedCard = _state.PlayerHands[command.Seat][command.HandIndex];
		var tableCards = new CardPlayTableCard[_state.TableCards.Count];
		for (int index = 0; index < tableCards.Length; index++)
		{
			TableCard tableCard = _state.TableCards[index];
			tableCards[index] = new CardPlayTableCard(
				tableCard.Card?.Rank,
				tableCard.IsFaceUp,
				tableCard.IsBuriedInSand,
				tableCard.Card?.Suit);
		}

		IReadOnlyList<string>? alternateRanks = command.AlternateMatchRanks.IsDefaultOrEmpty
			? null
			: command.AlternateMatchRanks;
		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard(
				command.PrimaryMatchRank,
				command.CollectEntireTable,
				playedCard.Suit,
				command.CollectBySuit,
				command.SuitMatchIsFallbackOnly),
			alternateRanks,
			command.InsertAtTableStart,
			command.TableInsertIndexOverride,
			command.PlayedCardFacesUp);
		CardPlayStartResult start = _cardPlayStateService.TryStartPlayFromHand(
			_state,
			command.Seat,
			command.HandIndex,
			playedCard,
			plan);
		if (!start.Played)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.HandIndex.ToString()),
				ReadModel);
		}

		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.CardPlayed,
			"card.played",
			playedCard.InstanceId,
			command.Seat,
			command.HandIndex,
			start.PlayedTableCardIndex,
			start.CaptureStartIndex));
		if (start.MatchedTableCardFlipped)
		{
			intents.Add(new PresentationIntent(
				PresentationIntentKind.SemanticEffect,
				"card.match_reveal",
				Seat: command.Seat,
				TargetIndex: plan.MatchingTableCardIndex));
		}

		return Accepted(
			intents,
			new RuleMutationSummary(
				playedCard.InstanceId,
				command.Seat,
				command.HandIndex,
				start.PlayedTableCardIndex,
				start.CaptureStartIndex,
				0,
				0,
				ImmutableArray<RuleScoreChange>.Empty,
				start.CaptureStartIndex >= 0,
				false));
	}

	private RuleRejection? ValidatePlayCardFromHand(PlayCardFromHandCommand command)
	{
		RuleRejection? seatRejection = ValidateActingSeat(command.Seat);
		if (seatRejection.HasValue)
		{
			return seatRejection;
		}

		if (command.HandIndex < 0 || command.HandIndex >= _state.PlayerHands[command.Seat].Count)
		{
			return new RuleRejection(RuleRejectionCode.InvalidIndex, command.HandIndex.ToString());
		}

		if (string.IsNullOrWhiteSpace(command.PrimaryMatchRank)
			|| command.TableInsertIndexOverride < -1
			|| command.TableInsertIndexOverride > _state.TableCards.Count)
		{
			return new RuleRejection(RuleRejectionCode.InvalidCommand, string.Empty);
		}

		if (!command.AlternateMatchRanks.IsDefault)
		{
			var seenRanks = new HashSet<string>(StringComparer.Ordinal);
			foreach (string rank in command.AlternateMatchRanks)
			{
				if (string.IsNullOrWhiteSpace(rank) || !seenRanks.Add(rank))
				{
					return new RuleRejection(
						RuleRejectionCode.InvalidCommand,
						"Alternate match ranks must be non-empty and distinct.");
				}
			}
		}

		return null;
	}

	private RuleCommandResult ApplyCaptureCards(
		CaptureCardsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? seatRejection = ValidateActingSeat(command.Seat);
		if (seatRejection.HasValue)
		{
			return RuleCommandResult.Rejected(seatRejection.Value, ReadModel);
		}

		if (command.CaptureStartIndex < 0 || command.CaptureStartIndex >= _state.TableCards.Count)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.CaptureStartIndex.ToString()),
				ReadModel);
		}

		RuleRejection? extraIndexRejection = TryBuildCaptureIndexSet(
			command.ExtraTableCardIndexes,
			command.CaptureStartIndex,
			requireBeforeCaptureStart: true,
			out HashSet<int>? extraIndexes);
		if (extraIndexRejection.HasValue)
		{
			return RuleCommandResult.Rejected(extraIndexRejection.Value, ReadModel);
		}

		CaptureStatePlan plan = _captureStateService.CreatePlan(
			_state,
			command.CaptureStartIndex,
			extraIndexes);
		if (!plan.IsValid)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.CaptureStartIndex.ToString()),
				ReadModel);
		}

		RuleRejection? preservedIndexRejection = TryBuildPreservedCaptureIndexSet(
			command.PreservedTableCardIndexes,
			plan,
			out HashSet<int>? preservedIndexes);
		if (preservedIndexRejection.HasValue)
		{
			return RuleCommandResult.Rejected(preservedIndexRejection.Value, ReadModel);
		}

		if (command.RedirectedTableCardIndex < -1
			|| command.RedirectedTableCardIndex >= 0
				&& (!IsCapturedIndex(plan, command.RedirectedTableCardIndex)
					|| command.RedirectedSeat < 0
					|| command.RedirectedSeat >= _state.PlayerCount)
			|| command.RedirectedTableCardIndex < 0 && command.RedirectedSeat != -1)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.RedirectedTableCardIndex.ToString()),
				ReadModel);
		}

		RuleRejection? scoreRejection = TryBuildScoreChanges(
			command.ScoreMutations,
			allowEmpty: true,
			out ImmutableArray<RuleScoreChange> scoreChanges);
		if (scoreRejection.HasValue)
		{
			return RuleCommandResult.Rejected(scoreRejection.Value, ReadModel);
		}

		ApplyScoreChanges(scoreChanges);
		CaptureStateResult captureResult = _captureStateService.ApplyCapture(
			_state,
			plan,
			command.Seat,
			command.PreserveCardEffectsWhenEnteringDeck,
			preservedIndexes,
			command.RedirectedTableCardIndex,
			command.RedirectedSeat);
		if (command.RestartQuestionRefreshTracking)
		{
			QuestionCardRefreshState refreshState =
				_turnAdvancementService.StartQuestionCardRefreshTracking(command.Seat);
			_state.CompletedTurnsSinceQuestionCardRefresh =
				refreshState.CompletedTurnsSinceQuestionCardRefresh;
			_state.QuestionCardRefreshAnchorPlayerIndex =
				refreshState.QuestionCardRefreshAnchorPlayerIndex;
		}
		_state.RuleRevision++;

		intents.Add(new PresentationIntent(
			PresentationIntentKind.CardsCaptured,
			"cards.captured",
			Seat: command.Seat,
			SourceIndex: command.CaptureStartIndex,
			Value: captureResult.CollectedCardCount));
		AddScoreChangedIntents(intents, scoreChanges);
		return Accepted(
			intents,
			new RuleMutationSummary(
				0,
				command.Seat,
				command.CaptureStartIndex,
				-1,
				command.CaptureStartIndex,
				captureResult.CollectedCardCount,
				captureResult.RemovedTableCardCount,
				scoreChanges,
				true,
				false));
	}

	private RuleCommandResult ApplyScoreMutations(
		ApplyScoreMutationsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = TryBuildScoreChanges(
			command.Mutations,
			allowEmpty: false,
			out ImmutableArray<RuleScoreChange> scoreChanges);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ApplyScoreChanges(scoreChanges);
		_state.RuleRevision++;
		AddScoreChangedIntents(intents, scoreChanges);
		return Accepted(
			intents,
			RuleMutationSummary.Empty with { ScoreChanges = scoreChanges });
	}

	private RuleCommandResult ApplyDrawCards(
		DrawCardsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		if (command.Seat < 0 || command.Seat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidSeat, command.Seat.ToString()),
				ReadModel);
		}

		if (command.Count <= 0 || command.MaximumHandSize < 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.Count.ToString()),
				ReadModel);
		}

		List<PlayingCard> deck = _state.PlayerDecks[command.Seat];
		List<PlayingCard> hand = _state.PlayerHands[command.Seat];
		int totalHandCount = hand.Count + _state.PlayerItemHands[command.Seat].Count;
		int drawCount = Math.Min(
			command.Count,
			Math.Min(deck.Count, Math.Max(0, command.MaximumHandSize - totalHandCount)));
		if (drawCount <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No card can be drawn."),
				ReadModel);
		}

		long firstCardId = 0;
		int firstHandIndex = hand.Count;
		for (int index = 0; index < drawCount; index++)
		{
			PlayingCard card = deck[0];
			deck.RemoveAt(0);
			hand.Add(card);
			firstCardId = firstCardId == 0 ? card.InstanceId : firstCardId;
			intents.Add(new PresentationIntent(
				PresentationIntentKind.CardDrawn,
				"card.drawn",
				card.InstanceId,
				command.Seat,
				SourceIndex: 0,
				TargetIndex: hand.Count - 1));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			new RuleMutationSummary(
				firstCardId,
				command.Seat,
				0,
				firstHandIndex,
				-1,
				drawCount,
				0,
				ImmutableArray<RuleScoreChange>.Empty,
				false,
				false));
	}

	private RuleCommandResult ApplyReturnCardsToHand(
		ReturnCardsToHandCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? seatRejection = ValidateActingSeat(command.Seat);
		if (seatRejection.HasValue)
		{
			return RuleCommandResult.Rejected(seatRejection.Value, ReadModel);
		}

		if (command.CardRuntimeInstanceIds.IsDefaultOrEmpty
			|| command.MaximumHandSize < 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, string.Empty),
				ReadModel);
		}

		List<PlayingCard> deck = _state.PlayerDecks[command.Seat];
		List<PlayingCard> hand = _state.PlayerHands[command.Seat];
		int totalHandCount = hand.Count + _state.PlayerItemHands[command.Seat].Count;
		if (command.CardRuntimeInstanceIds.Length > command.MaximumHandSize - totalHandCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Returned cards exceed hand capacity."),
				ReadModel);
		}

		var seenIds = new HashSet<long>();
		var cards = new PlayingCard[command.CardRuntimeInstanceIds.Length];
		for (int requestedIndex = 0;
			requestedIndex < command.CardRuntimeInstanceIds.Length;
			requestedIndex++)
		{
			long runtimeInstanceId = command.CardRuntimeInstanceIds[requestedIndex];
			if (runtimeInstanceId <= 0 || !seenIds.Add(runtimeInstanceId))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, runtimeInstanceId.ToString()),
					ReadModel);
			}

			int deckIndex = deck.FindIndex(card => card.InstanceId == runtimeInstanceId);
			if (deckIndex < 0)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidIndex, runtimeInstanceId.ToString()),
					ReadModel);
			}

			cards[requestedIndex] = deck[deckIndex];
		}

		int firstHandIndex = hand.Count;
		for (int requestedIndex = 0; requestedIndex < cards.Length; requestedIndex++)
		{
			PlayingCard card = cards[requestedIndex];
			int deckIndex = deck.FindIndex(candidate => candidate.InstanceId == card.InstanceId);
			deck.RemoveAt(deckIndex);
			hand.Add(card);
			intents.Add(new PresentationIntent(
				PresentationIntentKind.CardReturnedToHand,
				"card.returned_to_hand",
				card.InstanceId,
				command.Seat,
				deckIndex,
				hand.Count - 1));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			new RuleMutationSummary(
				cards[0].InstanceId,
				command.Seat,
				-1,
				firstHandIndex,
				-1,
				cards.Length,
				0,
				ImmutableArray<RuleScoreChange>.Empty,
				false,
				false));
	}

}
