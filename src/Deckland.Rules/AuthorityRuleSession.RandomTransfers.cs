using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
	private RuleCommandResult ApplyChooseRandomHandDisplayIndex(
		ChooseRandomHandDisplayIndexCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.HandSeat < 0 || command.HandSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidSeat, command.HandSeat.ToString()),
				ReadModel);
		}

		int totalHandCount = _state.PlayerHands[command.HandSeat].Count
			+ _state.PlayerItemHands[command.HandSeat].Count;
		if (totalHandCount <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "The hand is empty."),
				ReadModel);
		}

		var excludedIndexes = new HashSet<int>();
		if (!command.ExcludedDisplayIndexes.IsDefaultOrEmpty)
		{
			foreach (int excludedIndex in command.ExcludedDisplayIndexes)
			{
				if (excludedIndex < 0
					|| excludedIndex >= totalHandCount
					|| !excludedIndexes.Add(excludedIndex))
				{
					return RuleCommandResult.Rejected(
						new RuleRejection(RuleRejectionCode.InvalidIndex, excludedIndex.ToString()),
						ReadModel);
				}
			}
		}

		if (excludedIndexes.Count == totalHandCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Every hand card is excluded."),
				ReadModel);
		}

		int selectedIndex = _randomSource.NextIntInclusive(0, totalHandCount - 1);
		if (excludedIndexes.Contains(selectedIndex))
		{
			for (int displayIndex = 0; displayIndex < totalHandCount; displayIndex++)
			{
				if (!excludedIndexes.Contains(displayIndex))
				{
					selectedIndex = displayIndex;
					break;
				}
			}
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.HandSeat,
				SourceIndex = selectedIndex,
			});
	}

	private RuleCommandResult ApplyChooseRandomHandCardIndex(
		ChooseRandomHandCardIndexCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (command.CandidateHandIndexes.IsDefaultOrEmpty)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No hand-card candidate was supplied."),
				ReadModel);
		}

		var seenIndexes = new HashSet<int>();
		int handCount = _state.PlayerHands[command.Seat].Count;
		foreach (int candidateIndex in command.CandidateHandIndexes)
		{
			if (candidateIndex < 0
				|| candidateIndex >= handCount
				|| !seenIndexes.Add(candidateIndex))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidIndex, candidateIndex.ToString()),
					ReadModel);
			}
		}

		int randomOrdinal = _randomSource.NextIntInclusive(0, int.MaxValue);
		int selectedIndex = command.CandidateHandIndexes[
			(randomOrdinal & int.MaxValue) % command.CandidateHandIndexes.Length];
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = selectedIndex,
			});
	}

	private RuleCommandResult ApplyChooseRandomTargetSeat(
		ChooseRandomTargetSeatCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.CandidateSeats.IsDefaultOrEmpty)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No target seat was supplied."),
				ReadModel);
		}

		var seenSeats = new HashSet<int>();
		foreach (int candidateSeat in command.CandidateSeats)
		{
			if (candidateSeat < 0
				|| candidateSeat >= _state.PlayerCount
				|| candidateSeat == command.ActingSeat
				|| !seenSeats.Add(candidateSeat))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidSeat, candidateSeat.ToString()),
					ReadModel);
			}
		}

		int selectedSeat = command.CandidateSeats[
			_randomSource.NextIntInclusive(0, command.CandidateSeats.Length - 1)];
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				SourceIndex = selectedSeat,
			});
	}

	private RuleCommandResult ApplyChooseWeightedOption(
		ChooseWeightedOptionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.Options.IsDefaultOrEmpty)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No weighted option was supplied."),
				ReadModel);
		}

		var seenIds = new HashSet<string>(StringComparer.Ordinal);
		int maximumTotalWeight = 0;
		foreach (RuleWeightedOption option in command.Options)
		{
			int weight = Math.Max(0, option.Weight);
			if (string.IsNullOrWhiteSpace(option.Id) || !seenIds.Add(option.Id))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, option.Id ?? string.Empty),
					ReadModel);
			}
			if (weight > int.MaxValue - maximumTotalWeight)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Weighted option total exceeds Int32."),
					ReadModel);
			}
			maximumTotalWeight += weight;
		}
		if (maximumTotalWeight == 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Every weighted option has zero weight."),
				ReadModel);
		}

		string selectedId = string.Empty;
		int totalWeight = 0;
		foreach (RuleWeightedOption option in command.Options)
		{
			int weight = Math.Max(0, option.Weight);
			if (weight == 0)
			{
				continue;
			}
			totalWeight += weight;
			if (_randomSource.NextIntInclusive(1, totalWeight) <= weight)
			{
				selectedId = option.Id;
			}
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				LogicalItemId = selectedId,
			});
	}

	private RuleCommandResult ApplyChooseRandomTableCardIndex(
		ChooseRandomTableCardIndexCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.Candidates.IsDefaultOrEmpty)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No table-card candidate was supplied."),
				ReadModel);
		}

		var seenIndexes = new HashSet<int>();
		foreach (RuleTableCardReference candidate in command.Candidates)
		{
			if (candidate.Index < 0
				|| candidate.Index >= _state.TableCards.Count
				|| !seenIndexes.Add(candidate.Index)
				|| _state.TableCards[candidate.Index].InstanceId != candidate.RuntimeInstanceId)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidIndex, candidate.Index.ToString()),
					ReadModel);
			}
		}

		int selectedIndex = -1;
		int candidateCount = 0;
		foreach (RuleTableCardReference candidate in command.Candidates)
		{
			candidateCount++;
			if (_randomSource.NextIntInclusive(1, candidateCount) == 1)
			{
				selectedIndex = candidate.Index;
			}
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				SourceIndex = selectedIndex,
			});
	}

	private RuleCommandResult ApplyChooseRandomIndex(
		ChooseRandomIndexCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.MinimumInclusive < 0
			|| command.MaximumInclusive < command.MinimumInclusive)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, "Invalid random range."),
				ReadModel);
		}

		int selectedIndex = _randomSource.NextIntInclusive(
			command.MinimumInclusive,
			command.MaximumInclusive);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				SourceIndex = selectedIndex,
			});
	}

	private RuleCommandResult ApplyChooseReservoirHandDisplayIndex(
		ChooseReservoirHandDisplayIndexCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.HandSeat < 0 || command.HandSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidSeat, command.HandSeat.ToString()),
				ReadModel);
		}
		if (command.CandidateDisplayIndexes.IsDefaultOrEmpty)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No hand-display candidate was supplied."),
				ReadModel);
		}

		int totalHandCount = _state.PlayerHands[command.HandSeat].Count
			+ _state.PlayerItemHands[command.HandSeat].Count;
		var seenIndexes = new HashSet<int>();
		foreach (int candidateIndex in command.CandidateDisplayIndexes)
		{
			if (candidateIndex < 0
				|| candidateIndex >= totalHandCount
				|| !seenIndexes.Add(candidateIndex))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidIndex, candidateIndex.ToString()),
					ReadModel);
			}
		}

		int selectedIndex = -1;
		int candidateCount = 0;
		foreach (int candidateIndex in command.CandidateDisplayIndexes)
		{
			candidateCount++;
			if (_randomSource.NextIntInclusive(1, candidateCount) == 1)
			{
				selectedIndex = candidateIndex;
			}
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.HandSeat,
				SourceIndex = selectedIndex,
				CollectedCardCount = candidateCount,
			});
	}

	private RuleCommandResult ApplyTransferHandCard(
		TransferHandCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		bool expectsBaseCard = command.ExpectedCardRuntimeInstanceId > 0;
		bool expectsItemCard = !string.IsNullOrEmpty(command.ExpectedItemId);
		if (command.SourceSeat < 0
			|| command.SourceSeat >= _state.PlayerCount
			|| command.SourceSeat == command.ActingSeat
			|| command.MaximumHandSize <= 0
			|| expectsBaseCard == expectsItemCard)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.SourceSeat.ToString()),
				ReadModel);
		}

		RuleRejection? sourceRejection = ValidateHandDisplayCard(
			command.SourceSeat,
			command.SourceDisplayIndex,
			command.ExpectedCardRuntimeInstanceId,
			command.ExpectedItemId,
			out bool isBaseCard,
			out int sourceZoneIndex);
		if (sourceRejection.HasValue)
		{
			return RuleCommandResult.Rejected(sourceRejection.Value, ReadModel);
		}

		int destinationHandCount = _state.PlayerHands[command.ActingSeat].Count
			+ _state.PlayerItemHands[command.ActingSeat].Count;
		if (destinationHandCount >= command.MaximumHandSize)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Destination hand is full."),
				ReadModel);
		}

		long cardId = 0;
		string itemId = string.Empty;
		int targetDisplayIndex;
		if (isBaseCard)
		{
			PlayingCard card = _state.PlayerHands[command.SourceSeat][sourceZoneIndex];
			cardId = card.InstanceId;
			_state.PlayerHands[command.SourceSeat].RemoveAt(sourceZoneIndex);
			targetDisplayIndex = _state.PlayerHands[command.ActingSeat].Count;
			_state.PlayerHands[command.ActingSeat].Add(card);
		}
		else
		{
			ItemCard item = _state.PlayerItemHands[command.SourceSeat][sourceZoneIndex];
			itemId = item.Id;
			_state.PlayerItemHands[command.SourceSeat].RemoveAt(sourceZoneIndex);
			targetDisplayIndex = _state.PlayerHands[command.ActingSeat].Count
				+ _state.PlayerItemHands[command.ActingSeat].Count;
			_state.PlayerItemHands[command.ActingSeat].Add(item);
		}

		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.HandCardTransferred,
			"hand.card_transferred",
			cardId,
			command.SourceSeat,
			command.SourceDisplayIndex,
			targetDisplayIndex,
			command.ActingSeat,
			LogicalItemId: itemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = cardId,
				SourceSeat = command.SourceSeat,
				SourceIndex = command.SourceDisplayIndex,
				TargetIndex = targetDisplayIndex,
				LogicalItemId = itemId,
			});
	}

	private RuleCommandResult ApplyTransferDeckCards(
		TransferDeckCardsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (command.SourceSeat < 0
			|| command.SourceSeat >= _state.PlayerCount
			|| command.SourceSeat == command.ActingSeat
			|| command.MaximumCount < 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.SourceSeat.ToString()),
				ReadModel);
		}

		List<PlayingCard> sourceDeck = _state.PlayerDecks[command.SourceSeat];
		List<PlayingCard> destinationDeck = _state.PlayerDecks[command.ActingSeat];
		int cardsToMove = Math.Min(sourceDeck.Count, command.MaximumCount);
		ImmutableArray<long> expectedCardIds = command.ExpectedSourceTopCardRuntimeInstanceIds.IsDefault
			? ImmutableArray<long>.Empty
			: command.ExpectedSourceTopCardRuntimeInstanceIds;
		if (expectedCardIds.Length != cardsToMove)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, expectedCardIds.Length.ToString()),
				ReadModel);
		}

		for (int index = 0; index < cardsToMove; index++)
		{
			if (expectedCardIds[index] <= 0
				|| sourceDeck[index].InstanceId != expectedCardIds[index])
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, index.ToString()),
					ReadModel);
			}
		}

		for (int index = 0; index < cardsToMove; index++)
		{
			PlayingCard card = sourceDeck[0];
			sourceDeck.RemoveAt(0);
			destinationDeck.Add(
				command.PreserveCardEffectsWhenEnteringDeck
					? card
					: card.WithoutItemEffects());
		}

		Shuffle(destinationDeck, _randomSource);
		Shuffle(sourceDeck, _randomSource);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.DeckCardsTransferred,
			"deck.cards_transferred",
			Seat: command.SourceSeat,
			TargetIndex: command.ActingSeat,
			Value: cardsToMove));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.SourceSeat,
				TargetIndex = command.ActingSeat,
				CollectedCardCount = cardsToMove,
			});
	}

	private RuleCommandResult ApplyTransferTopDeckCard(
		TransferTopDeckCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.SourceSeat < 0
			|| command.SourceSeat >= _state.PlayerCount
			|| command.SourceSeat == command.ActingSeat
			|| command.ExpectedRuntimeInstanceId <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.SourceSeat.ToString()),
				ReadModel);
		}

		List<PlayingCard> sourceDeck = _state.PlayerDecks[command.SourceSeat];
		if (sourceDeck.Count == 0
			|| sourceDeck[0].InstanceId != command.ExpectedRuntimeInstanceId)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.ExpectedRuntimeInstanceId.ToString()),
				ReadModel);
		}

		PlayingCard card = sourceDeck[0];
		sourceDeck.RemoveAt(0);
		_state.PlayerDecks[command.ActingSeat].Add(
			command.PreserveCardEffectsWhenEnteringDeck
				? card
				: card.WithoutItemEffects());
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.DeckCardsTransferred,
			"deck.top_card_transferred",
			card.InstanceId,
			command.SourceSeat,
			TargetIndex: command.ActingSeat,
			Value: 1));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = card.InstanceId,
				SourceSeat = command.SourceSeat,
				TargetIndex = command.ActingSeat,
				CollectedCardCount = 1,
			});
	}

	private RuleCommandResult ApplyShuffleDeck(
		ShuffleDeckCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.DeckSeat < 0 || command.DeckSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidSeat, command.DeckSeat.ToString()),
				ReadModel);
		}

		List<PlayingCard> deck = _state.PlayerDecks[command.DeckSeat];
		Shuffle(deck, _randomSource);
		if (command.CutAfterShuffle)
		{
			Cut(deck, _randomSource);
		}
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.SemanticEffect,
			command.CutAfterShuffle ? "deck.shuffled_and_cut" : "deck.shuffled",
			Seat: command.DeckSeat));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.DeckSeat,
			});
	}

}
