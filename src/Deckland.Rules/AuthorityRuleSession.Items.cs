using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
	private RuleCommandResult ApplyPlayItemFromHand(
		PlayItemFromHandCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		List<ItemCard> itemHand = _state.PlayerItemHands[command.Seat];
		if (command.ItemHandIndex < 0
			|| command.ItemHandIndex >= itemHand.Count
			|| string.IsNullOrWhiteSpace(command.ExpectedItemId)
			|| !string.Equals(
				itemHand[command.ItemHandIndex].Id,
				command.ExpectedItemId,
				StringComparison.Ordinal)
			|| !Enum.IsDefined(command.Destination))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.ItemHandIndex.ToString()),
				ReadModel);
		}

		if (command.Destination == RuleItemPlayDestination.Consume
			&& command.TableIndex != -1
			|| command.Destination == RuleItemPlayDestination.Table
				&& (command.TableIndex < 0 || command.TableIndex > _state.TableCards.Count))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.TableIndex.ToString()),
				ReadModel);
		}

		ItemCard item = itemHand[command.ItemHandIndex];
		itemHand.RemoveAt(command.ItemHandIndex);
		int targetIndex = -1;
		if (command.Destination == RuleItemPlayDestination.Table)
		{
			var tableItem = new TableCard(
				null,
				command.TableItemFacesUp,
				item,
				command.Seat);
			_state.TableCards.Insert(command.TableIndex, tableItem);
			targetIndex = command.TableIndex;
		}

		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.ItemPlayed,
			command.Destination == RuleItemPlayDestination.Table
				? "item.played_to_table"
				: "item.played",
			Seat: command.Seat,
			SourceIndex: command.ItemHandIndex,
			TargetIndex: targetIndex,
			LogicalItemId: item.Id));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = command.ItemHandIndex,
				TargetIndex = targetIndex,
				LogicalItemId = item.Id,
			});
	}

	private RuleCommandResult ApplyDiscardHandCard(
		DiscardHandCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		RuleRejection? displayRejection = ValidateHandDisplayCard(
			command.Seat,
			command.DisplayIndex,
			command.ExpectedCardRuntimeInstanceId,
			command.ExpectedItemId,
			out bool isBaseCard,
			out int zoneIndex);
		if (displayRejection.HasValue)
		{
			return RuleCommandResult.Rejected(displayRejection.Value, ReadModel);
		}

		long cardId = 0;
		string itemId = string.Empty;
		if (isBaseCard)
		{
			cardId = _state.PlayerHands[command.Seat][zoneIndex].InstanceId;
			_state.PlayerHands[command.Seat].RemoveAt(zoneIndex);
		}
		else
		{
			itemId = _state.PlayerItemHands[command.Seat][zoneIndex].Id;
			_state.PlayerItemHands[command.Seat].RemoveAt(zoneIndex);
		}

		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.HandCardDiscarded,
			"hand.card_discarded",
			cardId,
			command.Seat,
			command.DisplayIndex,
			LogicalItemId: itemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = cardId,
				SourceSeat = command.Seat,
				SourceIndex = command.DisplayIndex,
				LogicalItemId = itemId,
			});
	}

	private RuleCommandResult ApplyGrantItemToHand(
		GrantItemToHandCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (string.IsNullOrWhiteSpace(command.ItemId) || command.MaximumHandSize <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.ItemId),
				ReadModel);
		}

		bool hasDiscard = command.DiscardDisplayIndex >= 0;
		bool discardIsBaseCard = false;
		int discardZoneIndex = -1;
		if (hasDiscard)
		{
			RuleRejection? discardRejection = ValidateHandDisplayCard(
				command.Seat,
				command.DiscardDisplayIndex,
				command.ExpectedDiscardCardRuntimeInstanceId,
				command.ExpectedDiscardItemId,
				out discardIsBaseCard,
				out discardZoneIndex);
			if (discardRejection.HasValue)
			{
				return RuleCommandResult.Rejected(discardRejection.Value, ReadModel);
			}
		}
		else if (command.ExpectedDiscardCardRuntimeInstanceId != 0
			|| !string.IsNullOrEmpty(command.ExpectedDiscardItemId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, string.Empty),
				ReadModel);
		}

		int totalHandCount = _state.PlayerHands[command.Seat].Count
			+ _state.PlayerItemHands[command.Seat].Count;
		if (totalHandCount - (hasDiscard ? 1 : 0) >= command.MaximumHandSize)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Item reward exceeds hand capacity."),
				ReadModel);
		}

		if (hasDiscard)
		{
			long discardedCardId = 0;
			string discardedItemId = string.Empty;
			if (discardIsBaseCard)
			{
				discardedCardId = _state.PlayerHands[command.Seat][discardZoneIndex].InstanceId;
				_state.PlayerHands[command.Seat].RemoveAt(discardZoneIndex);
			}
			else
			{
				discardedItemId = _state.PlayerItemHands[command.Seat][discardZoneIndex].Id;
				_state.PlayerItemHands[command.Seat].RemoveAt(discardZoneIndex);
			}

			intents.Add(new PresentationIntent(
				PresentationIntentKind.HandCardDiscarded,
				"hand.card_discarded",
				discardedCardId,
				command.Seat,
				command.DiscardDisplayIndex,
				LogicalItemId: discardedItemId));
		}

		int itemHandIndex = _state.PlayerItemHands[command.Seat].Count;
		_state.PlayerItemHands[command.Seat].Add(new ItemCard(command.ItemId));
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.ItemGranted,
			"item.granted",
			Seat: command.Seat,
			SourceIndex: command.DiscardDisplayIndex,
			TargetIndex: itemHandIndex,
			LogicalItemId: command.ItemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = command.DiscardDisplayIndex,
				TargetIndex = itemHandIndex,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyReplaceItemInHand(
		ReplaceItemInHandCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		List<ItemCard> itemHand = _state.PlayerItemHands[command.Seat];
		if (command.ItemHandIndex < 0
			|| command.ItemHandIndex >= itemHand.Count
			|| string.IsNullOrWhiteSpace(command.ExpectedItemId)
			|| string.IsNullOrWhiteSpace(command.ReplacementItemId)
			|| !string.Equals(
				itemHand[command.ItemHandIndex].Id,
				command.ExpectedItemId,
				StringComparison.Ordinal))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.ItemHandIndex.ToString()),
				ReadModel);
		}

		itemHand[command.ItemHandIndex] = new ItemCard(command.ReplacementItemId);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.ItemGranted,
			"item.replaced",
			Seat: command.Seat,
			SourceIndex: command.ItemHandIndex,
			TargetIndex: command.ItemHandIndex,
			LogicalItemId: command.ReplacementItemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = command.ItemHandIndex,
				TargetIndex = command.ItemHandIndex,
				LogicalItemId = command.ReplacementItemId,
			});
	}

	private RuleCommandResult ApplyAttachItemToHandCard(
		AttachItemToHandCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (string.IsNullOrWhiteSpace(command.ItemId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, string.Empty),
				ReadModel);
		}

		List<PlayingCard> hand = _state.PlayerHands[command.Seat];
		int handIndex = command.HandIndex;
		if (handIndex < 0)
		{
			if (command.RandomCandidateHandIndexes.IsDefaultOrEmpty)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.NoLegalAction, "No attachment target was supplied."),
					ReadModel);
			}

			var seenIndexes = new HashSet<int>();
			foreach (int candidateIndex in command.RandomCandidateHandIndexes)
			{
				if (candidateIndex < 0
					|| candidateIndex >= hand.Count
					|| !seenIndexes.Add(candidateIndex)
					|| hand[candidateIndex].HasAttachedItemEffect(command.ItemId))
				{
					return RuleCommandResult.Rejected(
						new RuleRejection(RuleRejectionCode.InvalidIndex, candidateIndex.ToString()),
						ReadModel);
				}
			}

			int randomOrdinal = _randomSource.NextIntInclusive(0, int.MaxValue);
			handIndex = command.RandomCandidateHandIndexes[
				(randomOrdinal & int.MaxValue) % command.RandomCandidateHandIndexes.Length];
		}
		else if (!command.RandomCandidateHandIndexes.IsDefaultOrEmpty
			|| handIndex >= hand.Count
			|| hand[handIndex].HasAttachedItemEffect(command.ItemId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, handIndex.ToString()),
				ReadModel);
		}

		PlayingCard card = hand[handIndex];
		hand[handIndex] = card.WithAttachedItemEffect(command.ItemId);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.ItemAttached,
			"item.attached",
			card.InstanceId,
			command.Seat,
			TargetIndex: handIndex,
			LogicalItemId: command.ItemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = card.InstanceId,
				SourceSeat = command.Seat,
				TargetIndex = handIndex,
				LogicalItemId = command.ItemId,
			});
	}

}
