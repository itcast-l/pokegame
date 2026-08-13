using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private bool TrySubmitBeginMandatorySelectionRuleCommand(
		int actingPlayerIndex,
		MandatorySelectionKind kind,
		int sourceHandIndex,
		string itemId,
		ImmutableArray<int> legalSelectionIndexes,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new BeginMandatorySelectionCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			kind,
			sourceHandIndex,
			itemId,
			legalSelectionIndexes));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Mandatory-selection begin rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitResolveMandatorySelectionRuleCommand(
		int actingPlayerIndex,
		MandatorySelectionKind expectedKind,
		int selectionIndex,
		long expectedCardRuntimeInstanceId,
		string expectedItemId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new ResolveMandatorySelectionCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			expectedKind,
			selectionIndex,
			expectedCardRuntimeInstanceId,
			expectedItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Mandatory-selection resolve rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitClearMandatorySelectionRuleCommand(
		int actingPlayerIndex,
		MandatorySelectionKind expectedKind,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new ClearMandatorySelectionCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			expectedKind));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Mandatory-selection clear rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private ImmutableArray<PendingAttachmentRewardState> GetPendingAttachmentRewards()
	{
		if (_authorityRuleSession != null)
		{
			return _authorityRuleSession.ReadModel.NormalizedPendingAttachmentRewards;
		}

		return _replicaRuleSession?.ReadModel.NormalizedPendingAttachmentRewards
			?? ImmutableArray<PendingAttachmentRewardState>.Empty;
	}

	private bool TrySubmitEnqueuePendingAttachmentRewardRuleCommand(
		int seat,
		string itemId,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new EnqueuePendingAttachmentRewardCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				seat,
				itemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Pending attachment enqueue was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TryTakePendingAttachmentReward(
		int turnSequenceGeneration,
		out PendingAttachmentRewardState reward)
	{
		reward = default;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		ImmutableArray<PendingAttachmentRewardState> rewards =
			current.NormalizedPendingAttachmentRewards;
		if (rewards.IsEmpty)
		{
			return false;
		}

		reward = rewards[0];
		RuleCommandResult result = _authorityRuleSession.Submit(
			new TakePendingAttachmentRewardCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				reward.Seat,
				reward.ItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Pending attachment take was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		reward = default;
		return false;
	}

	private bool TrySubmitRemovePendingAttachmentRewardRuleCommand(
		int seat,
		string itemId,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new RemovePendingAttachmentRewardCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				seat,
				itemId));
		if (result.Accepted)
		{
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Pending attachment removal was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitReplacePendingAttachmentRewardRuleCommand(
		int seat,
		string expectedItemId,
		string replacementItemId,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ReplacePendingAttachmentRewardCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				seat,
				expectedItemId,
				replacementItemId));
		if (result.Accepted)
		{
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Pending attachment replacement was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitSetPendingAttachmentRewardsRuleCommand(
		ImmutableArray<PendingAttachmentRewardState> rewards,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new SetPendingAttachmentRewardsCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				rewards));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Pending attachment queue restore was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

}
