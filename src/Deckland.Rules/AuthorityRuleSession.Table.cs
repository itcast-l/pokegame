using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
	private RuleCommandResult ApplyPlaceTableItem(
		PlaceTableItemCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (string.IsNullOrWhiteSpace(command.ItemId)
			|| command.TableIndex < 0
			|| command.TableIndex > _state.TableCards.Count
			|| command.ItemOwnerSeat < -1
			|| command.ItemOwnerSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableIndex.ToString()),
				ReadModel);
		}

		var tableCard = new TableCard(
			null,
			command.IsFaceUp,
			new ItemCard(command.ItemId),
			command.ItemOwnerSeat);
		_state.TableCards.Insert(command.TableIndex, tableCard);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TableItemPlaced,
			"table.item_placed",
			tableCard.InstanceId,
			command.ActingSeat,
			TargetIndex: command.TableIndex,
			LogicalItemId: command.ItemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = tableCard.InstanceId,
				SourceSeat = command.ActingSeat,
				TargetIndex = command.TableIndex,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyPlaceTableCard(
		PlaceTableCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (!IsValidLogicalCard(command.Card)
			|| ContainsCardRuntimeInstanceId(command.Card.RuntimeInstanceId)
			|| command.TableIndex < 0
			|| command.TableIndex > _state.TableCards.Count
			|| command.CardOwnerSeat < -1
			|| command.CardOwnerSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableIndex.ToString()),
				ReadModel);
		}

		var tableCard = new TableCard(
			FromLogicalCard(command.Card),
			command.IsFaceUp,
			cardOwnerPlayerIndex: command.CardOwnerSeat,
			removeCardOnCapture: command.RemoveOnCapture);
		_state.TableCards.Insert(command.TableIndex, tableCard);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.CardPlayed,
			"table.card_placed",
			tableCard.InstanceId,
			command.ActingSeat,
			TargetIndex: command.TableIndex));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = tableCard.InstanceId,
				SourceSeat = command.ActingSeat,
				TargetIndex = command.TableIndex,
			});
	}

	private RuleCommandResult ApplyChooseTableCards(
		ChooseTableCardsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		RuleRejection? candidateRejection = ValidateTableCardReferences(command.Candidates);
		if (candidateRejection.HasValue)
		{
			return RuleCommandResult.Rejected(candidateRejection.Value, ReadModel);
		}

		if (command.MaximumCount <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.MaximumCount.ToString()),
				ReadModel);
		}

		int targetLimit = Math.Min(command.MaximumCount, command.Candidates.Length);
		var selected = new List<RuleTableCardReference>(targetLimit);
		int candidateCount = 0;
		foreach (RuleTableCardReference candidate in command.Candidates)
		{
			candidateCount++;
			if (selected.Count < targetLimit)
			{
				selected.Add(candidate);
				continue;
			}

			int replacementIndex = _randomSource.NextIntInclusive(0, candidateCount - 1);
			if (replacementIndex < targetLimit)
			{
				selected[replacementIndex] = candidate;
			}
		}

		Shuffle(selected, _randomSource);
		_state.RuleRevision++;
		foreach (RuleTableCardReference target in selected)
		{
			intents.Add(new PresentationIntent(
				PresentationIntentKind.TableCardsSelected,
				"table.card_selected",
				target.RuntimeInstanceId,
				command.ActingSeat,
				TargetIndex: target.Index));
		}

		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				CollectedCardCount = selected.Count,
			});
	}

	private RuleCommandResult ApplySetTableCardsFaceUp(
		SetTableCardsFaceUpCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		RuleRejection? tableRejection = ValidateTableCardReferences(command.TableCards);
		if (tableRejection.HasValue)
		{
			return RuleCommandResult.Rejected(tableRejection.Value, ReadModel);
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			if (_state.TableCards[reference.Index].IsFaceUp != command.ExpectedFaceUp)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, reference.Index.ToString()),
					ReadModel);
			}
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			_state.TableCards[reference.Index].IsFaceUp = command.FaceUp;
			intents.Add(new PresentationIntent(
				PresentationIntentKind.TableCardStateChanged,
				command.FaceUp ? "table.card_revealed" : "table.card_concealed",
				reference.RuntimeInstanceId,
				command.ActingSeat,
				TargetIndex: reference.Index));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				CollectedCardCount = command.TableCards.Length,
			});
	}

	private RuleCommandResult ApplySetTableCardsHidden(
		SetTableCardsHiddenCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		RuleRejection? tableRejection = ValidateTableCardReferences(command.TableCards);
		if (tableRejection.HasValue)
		{
			return RuleCommandResult.Rejected(tableRejection.Value, ReadModel);
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			if (_state.TableCards[reference.Index].IsHiddenDuringCapture != command.ExpectedHidden)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, reference.Index.ToString()),
					ReadModel);
			}
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			_state.TableCards[reference.Index].IsHiddenDuringCapture = command.Hidden;
			intents.Add(new PresentationIntent(
				PresentationIntentKind.TableCardStateChanged,
				command.Hidden ? "table.card_hidden" : "table.card_shown",
				reference.RuntimeInstanceId,
				command.ActingSeat,
				TargetIndex: reference.Index));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				CollectedCardCount = command.TableCards.Length,
			});
	}

	private RuleCommandResult ApplySetTableCardsScoreSuppressed(
		SetTableCardsScoreSuppressedCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		RuleRejection? tableRejection = ValidateTableCardReferences(command.TableCards);
		if (tableRejection.HasValue)
		{
			return RuleCommandResult.Rejected(tableRejection.Value, ReadModel);
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			if (_state.TableCards[reference.Index].IsSlimeCoated
				!= command.ExpectedScoreSuppressed)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, reference.Index.ToString()),
					ReadModel);
			}
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			_state.TableCards[reference.Index].IsSlimeCoated = command.ScoreSuppressed;
			intents.Add(new PresentationIntent(
				PresentationIntentKind.TableCardStateChanged,
				command.ScoreSuppressed
					? "table.score_suppressed"
					: "table.score_suppression_cleared",
				_state.TableCards[reference.Index].InstanceId,
				command.ActingSeat,
				TargetIndex: reference.Index,
				Value: command.ScoreSuppressed ? 1 : 0));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				CollectedCardCount = command.TableCards.Length,
			});
	}

	private RuleCommandResult ApplyRemoveTableCardAttachment(
		RemoveTableCardAttachmentCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (command.TableIndex < 0
			|| command.TableIndex >= _state.TableCards.Count
			|| command.ExpectedTableCardRuntimeInstanceId <= 0
			|| string.IsNullOrWhiteSpace(command.ItemId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableIndex.ToString()),
				ReadModel);
		}

		TableCard tableCard = _state.TableCards[command.TableIndex];
		if (tableCard.InstanceId != command.ExpectedTableCardRuntimeInstanceId
			|| !tableCard.Card.HasValue
			|| !tableCard.Card.Value.HasAttachedItemEffect(command.ItemId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableIndex.ToString()),
				ReadModel);
		}

		tableCard.ClearAttachedItemEffect(command.ItemId);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TableCardStateChanged,
			"table.card_attachment_removed",
			tableCard.InstanceId,
			command.ActingSeat,
			TargetIndex: command.TableIndex,
			LogicalItemId: command.ItemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = tableCard.InstanceId,
				SourceSeat = command.ActingSeat,
				TargetIndex = command.TableIndex,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyRemoveTableCardRange(
		RemoveTableCardRangeCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ImmutableArray<long> expectedIds = command.ExpectedRuntimeInstanceIds.IsDefault
			? ImmutableArray<long>.Empty
			: command.ExpectedRuntimeInstanceIds;
		if (command.StartIndex < 0
			|| expectedIds.IsEmpty
			|| command.StartIndex > _state.TableCards.Count - expectedIds.Length)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.StartIndex.ToString()),
				ReadModel);
		}

		for (int offset = 0; offset < expectedIds.Length; offset++)
		{
			if (expectedIds[offset] <= 0
				|| _state.TableCards[command.StartIndex + offset].InstanceId != expectedIds[offset])
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, offset.ToString()),
					ReadModel);
			}
		}

		_state.TableCards.RemoveRange(command.StartIndex, expectedIds.Length);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TableCardsRemoved,
			"table.cards_removed",
			Seat: command.ActingSeat,
			SourceIndex: command.StartIndex,
			Value: expectedIds.Length));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				SourceIndex = command.StartIndex,
				RemovedTableCardCount = expectedIds.Length,
			});
	}

	private RuleCommandResult ApplyMoveTableCard(
		MoveTableCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (command.SourceIndex < 0
			|| command.SourceIndex >= _state.TableCards.Count
			|| command.TargetIndex < 0
			|| command.TargetIndex >= _state.TableCards.Count
			|| command.ExpectedRuntimeInstanceId <= 0
			|| _state.TableCards[command.SourceIndex].InstanceId != command.ExpectedRuntimeInstanceId)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.SourceIndex.ToString()),
				ReadModel);
		}

		TableCard tableCard = _state.TableCards[command.SourceIndex];
		_state.TableCards.RemoveAt(command.SourceIndex);
		_state.TableCards.Insert(command.TargetIndex, tableCard);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TableCardMoved,
			"table.card_moved",
			tableCard.InstanceId,
			command.ActingSeat,
			command.SourceIndex,
			command.TargetIndex));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = tableCard.InstanceId,
				SourceSeat = command.ActingSeat,
				SourceIndex = command.SourceIndex,
				TargetIndex = command.TargetIndex,
			});
	}

	private RuleCommandResult ApplyMoveTableCardToDeck(
		MoveTableCardToDeckCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.TableIndex < 0
			|| command.TableIndex >= _state.TableCards.Count
			|| command.ExpectedTableCardRuntimeInstanceId <= 0
			|| command.DeckSeat < 0
			|| command.DeckSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableIndex.ToString()),
				ReadModel);
		}

		TableCard tableCard = _state.TableCards[command.TableIndex];
		if (tableCard.InstanceId != command.ExpectedTableCardRuntimeInstanceId
			|| !tableCard.Card.HasValue)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.ExpectedTableCardRuntimeInstanceId.ToString()),
				ReadModel);
		}

		PlayingCard card = tableCard.Card.Value;
		_state.TableCards.RemoveAt(command.TableIndex);
		_state.PlayerDecks[command.DeckSeat].Add(
			command.PreserveCardEffectsWhenEnteringDeck
				? card
				: card.WithoutItemEffects());
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TableCardsRemoved,
			"table.card_moved_to_deck",
			tableCard.InstanceId,
			command.ActingSeat,
			SourceIndex: command.TableIndex,
			TargetIndex: command.DeckSeat,
			Value: 1));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = tableCard.InstanceId,
				SourceSeat = command.ActingSeat,
				SourceIndex = command.TableIndex,
				TargetIndex = command.DeckSeat,
				RemovedTableCardCount = 1,
			});
	}

	private RuleCommandResult ApplyPlanTableShuffle(
		PlanTableShuffleCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		RuleRejection? tableRejection = ValidateTableCardReferences(command.TableCards);
		if (tableRejection.HasValue)
		{
			return RuleCommandResult.Rejected(tableRejection.Value, ReadModel);
		}

		if (command.TableCards.Length != _state.TableCards.Count)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableCards.Length.ToString()),
				ReadModel);
		}
		for (int index = 0; index < command.TableCards.Length; index++)
		{
			if (command.TableCards[index].Index != index)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidIndex, index.ToString()),
					ReadModel);
			}
		}

		var fixedIndexes = new HashSet<int>();
		if (!command.FixedTableIndexes.IsDefaultOrEmpty)
		{
			foreach (int fixedIndex in command.FixedTableIndexes)
			{
				if (fixedIndex < 0
					|| fixedIndex >= command.TableCards.Length
					|| !fixedIndexes.Add(fixedIndex))
				{
					return RuleCommandResult.Rejected(
						new RuleRejection(RuleRejectionCode.InvalidIndex, fixedIndex.ToString()),
						ReadModel);
				}
			}
		}

		var movableIndexes = new List<int>();
		var movableReferences = new List<RuleTableCardReference>();
		for (int index = 0; index < command.TableCards.Length; index++)
		{
			if (!fixedIndexes.Contains(index))
			{
				movableIndexes.Add(index);
				movableReferences.Add(command.TableCards[index]);
			}
		}
		if (movableReferences.Count < 2)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Fewer than two table cards can move."),
				ReadModel);
		}

		Shuffle(movableReferences, _randomSource);
		bool unchanged = true;
		for (int index = 0; index < movableReferences.Count; index++)
		{
			if (movableReferences[index].RuntimeInstanceId
				!= command.TableCards[movableIndexes[index]].RuntimeInstanceId)
			{
				unchanged = false;
				break;
			}
		}
		if (unchanged)
		{
			(movableReferences[^1], movableReferences[^2]) =
				(movableReferences[^2], movableReferences[^1]);
		}

		var plannedOrder = command.TableCards
			.Select(reference => reference.RuntimeInstanceId)
			.ToArray();
		int movableOrdinal = 0;
		for (int index = 0; index < plannedOrder.Length; index++)
		{
			if (!fixedIndexes.Contains(index))
			{
				plannedOrder[index] = movableReferences[movableOrdinal++].RuntimeInstanceId;
			}
		}

		_state.RulePendingTableOrderRuntimeInstanceIds.Clear();
		_state.RulePendingTableOrderRuntimeInstanceIds.AddRange(plannedOrder);
		_state.RulePendingPresentationBarrierId = _nextPresentationBarrierId++;
		_state.RuleRevision++;
		for (int sourceIndex = 0; sourceIndex < command.TableCards.Length; sourceIndex++)
		{
			RuleTableCardReference source = command.TableCards[sourceIndex];
			int targetIndex = Array.IndexOf(plannedOrder, source.RuntimeInstanceId);
			intents.Add(new PresentationIntent(
				PresentationIntentKind.TableShufflePlanned,
				"table.shuffle_planned",
				source.RuntimeInstanceId,
				command.ActingSeat,
				sourceIndex,
				targetIndex,
				BarrierId: _state.RulePendingPresentationBarrierId));
		}

		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				CollectedCardCount = movableReferences.Count,
			});
	}

}
