using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
	private RuleCommandResult ApplyBeginMandatorySelection(
		BeginMandatorySelectionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (_state.MandatorySelection.Kind != MandatorySelectionKind.None)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(
					RuleRejectionCode.MandatorySelectionPending,
					_state.MandatorySelection.Kind.ToString()),
				ReadModel);
		}

		ImmutableArray<int> legalIndexes = command.LegalSelectionIndexes.IsDefault
			? ImmutableArray<int>.Empty
			: command.LegalSelectionIndexes;
		var seenIndexes = new HashSet<int>();
		int maximumIndex = command.Kind switch
		{
			MandatorySelectionKind.HandLimitDiscard =>
				_state.PlayerHands[command.ActingSeat].Count
				+ _state.PlayerItemHands[command.ActingSeat].Count,
			MandatorySelectionKind.AttachmentHandCard =>
				_state.PlayerHands[command.ActingSeat].Count,
			_ => -1,
		};
		if (maximumIndex < 0
			|| legalIndexes.IsEmpty
			|| command.Kind == MandatorySelectionKind.HandLimitDiscard
				&& (!string.IsNullOrEmpty(command.ItemId) || command.SourceHandIndex != -1)
			|| command.Kind == MandatorySelectionKind.AttachmentHandCard
				&& (string.IsNullOrWhiteSpace(command.ItemId)
					|| command.SourceHandIndex < -1
					|| command.SourceHandIndex >= _state.PlayerItemHands[command.ActingSeat].Count)
			|| legalIndexes.Any(index => index < 0 || index >= maximumIndex || !seenIndexes.Add(index)))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.Kind.ToString()),
				ReadModel);
		}

		if (command.Kind == MandatorySelectionKind.AttachmentHandCard)
		{
			if (command.SourceHandIndex >= 0
				&& !string.Equals(
					_state.PlayerItemHands[command.ActingSeat][command.SourceHandIndex].Id,
					command.ItemId,
					StringComparison.Ordinal))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, command.SourceHandIndex.ToString()),
					ReadModel);
			}

			foreach (int handIndex in legalIndexes)
			{
				if (_state.PlayerHands[command.ActingSeat][handIndex]
					.HasAttachedItemEffect(command.ItemId))
				{
					return RuleCommandResult.Rejected(
						new RuleRejection(RuleRejectionCode.InvalidCommand, handIndex.ToString()),
						ReadModel);
				}
			}
		}

		_state.MandatorySelection = new MandatorySelectionState(
			command.Kind,
			command.ActingSeat,
			command.SourceHandIndex,
			command.TurnGeneration,
			command.ItemId,
			legalIndexes);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.MandatorySelectionBegan,
			"mandatory_selection.began",
			Seat: command.ActingSeat,
			SourceIndex: command.SourceHandIndex,
			Value: (int)command.Kind,
			LogicalItemId: command.ItemId));
		return Accepted(intents);
	}

	private RuleCommandResult ApplyResolveMandatorySelection(
		ResolveMandatorySelectionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		MandatorySelectionState pending = _state.MandatorySelection;
		RuleRejection? rejection = ValidatePendingMandatorySelection(
			command.ActingSeat,
			command.ExpectedKind,
			pending);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ImmutableArray<int> legalIndexes = pending.NormalizedLegalSelectionIndexes;
		if (!legalIndexes.Contains(command.SelectionIndex))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.SelectionIndex.ToString()),
				ReadModel);
		}

		long cardId = 0;
		string itemId = string.Empty;
		if (pending.Kind == MandatorySelectionKind.HandLimitDiscard)
		{
			RuleRejection? displayRejection = ValidateHandDisplayCard(
				pending.Seat,
				command.SelectionIndex,
				command.ExpectedCardRuntimeInstanceId,
				command.ExpectedItemId,
				out bool isBaseCard,
				out int zoneIndex);
			if (displayRejection.HasValue)
			{
				return RuleCommandResult.Rejected(displayRejection.Value, ReadModel);
			}
			if (isBaseCard
					? command.ExpectedCardRuntimeInstanceId <= 0
					: string.IsNullOrEmpty(command.ExpectedItemId))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, command.SelectionIndex.ToString()),
					ReadModel);
			}

			if (isBaseCard)
			{
				cardId = _state.PlayerHands[pending.Seat][zoneIndex].InstanceId;
				_state.PlayerHands[pending.Seat].RemoveAt(zoneIndex);
			}
			else
			{
				itemId = _state.PlayerItemHands[pending.Seat][zoneIndex].Id;
				_state.PlayerItemHands[pending.Seat].RemoveAt(zoneIndex);
			}
			intents.Add(new PresentationIntent(
				PresentationIntentKind.HandCardDiscarded,
				"hand.card_discarded",
				cardId,
				pending.Seat,
				command.SelectionIndex,
				LogicalItemId: itemId));
		}
		else
		{
			if (command.ExpectedCardRuntimeInstanceId <= 0
				|| !string.IsNullOrEmpty(command.ExpectedItemId))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, command.SelectionIndex.ToString()),
					ReadModel);
			}

			PlayingCard card = _state.PlayerHands[pending.Seat][command.SelectionIndex];
			if (card.InstanceId != command.ExpectedCardRuntimeInstanceId
				|| card.HasAttachedItemEffect(pending.ItemId))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, command.SelectionIndex.ToString()),
					ReadModel);
			}
			cardId = card.InstanceId;
			itemId = pending.ItemId;
			_state.PlayerHands[pending.Seat][command.SelectionIndex] =
				card.WithAttachedItemEffect(pending.ItemId);
			intents.Add(new PresentationIntent(
				PresentationIntentKind.ItemAttached,
				"item.attached",
				cardId,
				pending.Seat,
				TargetIndex: command.SelectionIndex,
				LogicalItemId: pending.ItemId));
		}

		_state.MandatorySelection = MandatorySelectionState.None;
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.MandatorySelectionResolved,
			"mandatory_selection.resolved",
			cardId,
			pending.Seat,
			TargetIndex: command.SelectionIndex,
			Value: (int)pending.Kind,
			LogicalItemId: itemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = cardId,
				SourceSeat = pending.Seat,
				SourceIndex = command.SelectionIndex,
				TargetIndex = command.SelectionIndex,
				LogicalItemId = itemId,
			});
	}

	private RuleCommandResult ApplyClearMandatorySelection(
		ClearMandatorySelectionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		MandatorySelectionState pending = _state.MandatorySelection;
		RuleRejection? rejection = ValidatePendingMandatorySelection(
			command.ActingSeat,
			command.ExpectedKind,
			pending);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		_state.MandatorySelection = MandatorySelectionState.None;
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.MandatorySelectionCleared,
			"mandatory_selection.cleared",
			Seat: pending.Seat,
			Value: (int)pending.Kind,
			LogicalItemId: pending.ItemId));
		return Accepted(intents);
	}

	private RuleCommandResult ApplyEnqueuePendingAttachmentReward(
		EnqueuePendingAttachmentRewardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidatePendingAttachmentReward(
			command.Seat,
			command.ItemId);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		_state.PendingAttachmentRewards.Add(
			new PendingAttachmentRewardState(command.Seat, command.ItemId));
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				TargetIndex = _state.PendingAttachmentRewards.Count - 1,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyTakePendingAttachmentReward(
		TakePendingAttachmentRewardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidatePendingAttachmentReward(
			command.ExpectedSeat,
			command.ExpectedItemId);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (_state.PendingAttachmentRewards.Count == 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, command.ExpectedItemId),
				ReadModel);
		}

		PendingAttachmentRewardState first = _state.PendingAttachmentRewards[0];
		if (first.Seat != command.ExpectedSeat
			|| !string.Equals(first.ItemId, command.ExpectedItemId, StringComparison.Ordinal))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, command.ExpectedItemId),
				ReadModel);
		}

		_state.PendingAttachmentRewards.RemoveAt(0);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = first.Seat,
				SourceIndex = 0,
				LogicalItemId = first.ItemId,
			});
	}

	private RuleCommandResult ApplyRemovePendingAttachmentReward(
		RemovePendingAttachmentRewardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidatePendingAttachmentReward(
			command.Seat,
			command.ItemId);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		int rewardIndex = FindLastPendingAttachmentReward(command.Seat, command.ItemId);
		if (rewardIndex < 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, command.ItemId),
				ReadModel);
		}

		_state.PendingAttachmentRewards.RemoveAt(rewardIndex);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = rewardIndex,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyReplacePendingAttachmentReward(
		ReplacePendingAttachmentRewardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidatePendingAttachmentReward(
			command.Seat,
			command.ExpectedItemId);
		rejection ??= ValidatePendingAttachmentReward(
			command.Seat,
			command.ReplacementItemId);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		int rewardIndex = FindLastPendingAttachmentReward(
			command.Seat,
			command.ExpectedItemId);
		if (rewardIndex < 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, command.ExpectedItemId),
				ReadModel);
		}

		_state.PendingAttachmentRewards[rewardIndex] =
			new PendingAttachmentRewardState(command.Seat, command.ReplacementItemId);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = rewardIndex,
				TargetIndex = rewardIndex,
				LogicalItemId = command.ReplacementItemId,
			});
	}

	private RuleCommandResult ApplySetPendingAttachmentRewards(
		SetPendingAttachmentRewardsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		if (!_profile.Capabilities.Contains(MatchCapability.ItemRules))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.ModuleRejected, "Item rules are disabled."),
				ReadModel);
		}

		ImmutableArray<PendingAttachmentRewardState> rewards = command.Rewards.IsDefault
			? ImmutableArray<PendingAttachmentRewardState>.Empty
			: command.Rewards;
		foreach (PendingAttachmentRewardState reward in rewards)
		{
			RuleRejection? rejection = ValidatePendingAttachmentReward(
				reward.Seat,
				reward.ItemId);
			if (rejection.HasValue)
			{
				return RuleCommandResult.Rejected(rejection.Value, ReadModel);
			}
		}

		_state.PendingAttachmentRewards.Clear();
		_state.PendingAttachmentRewards.AddRange(rewards);
		_state.RuleRevision++;
		return Accepted(intents);
	}

}
