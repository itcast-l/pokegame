using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private enum AdventureQuestionRejudgeChoice
	{
		Keep = 0,
		Reroll = 1,
	}

	private readonly record struct QuestionCardRewardPlan(
		ItemCard ItemCard,
		bool RequiresMagnetAttachment,
		int DiscardDisplayIndex,
		bool ReserveReplacementCard,
		bool ConsumeRejudgeOpportunity,
		bool RejudgeResolvedForReward);

	private readonly record struct QuestionRewardItemPlan(
		string ItemCardId,
		bool ConsumeRejudgeOpportunity,
		bool RejudgeResolvedForReward);
	private int PlaceQuestionCardOnNextTableSlot()
	{
		if (!item_mode)
		{
			return -1;
		}

		if (HasTableSpecialCard())
		{
			return -1;
		}

		ItemCard? itemOverlay = CreateItemCard(QuestionMarkItemCardId);
		if (!itemOverlay.HasValue)
		{
			return -1;
		}

		return TrySubmitPlaceTableItemRuleCommand(
			_currentTurnPlayerIndex,
			itemOverlay.Value.Id,
			_tableCards.Count,
			isFaceUp: true,
			itemOwnerPlayerIndex: -1,
			_turnSequenceGeneration,
			out RuleCommandResult result)
			? result.Mutation.TargetIndex
			: -1;
	}

	private bool HasTableSpecialCard()
	{
		foreach (TableCard tableCard in _tableCards)
		{
			if (tableCard.ItemOverlay.HasValue)
			{
				return true;
			}
		}

		return false;
	}

	private bool HasTableQuestionCard()
	{
		foreach (TableCard tableCard in _tableCards)
		{
			if (IsQuestionTableCard(tableCard))
			{
				return true;
			}
		}

		return false;
	}

	private bool RemoveExpiredQuestionCardFromTable(int actingPlayerIndex)
	{
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!IsQuestionTableCard(tableCard))
			{
				continue;
			}

			ClearAdventureTraceHuntMarkerIfTableCardRemoved(tableCard);
			if (!TrySubmitRemoveTableCardRangeRuleCommand(
					actingPlayerIndex,
					tableCardIndex,
					ImmutableArray.Create(tableCard.InstanceId),
					_turnSequenceGeneration,
					out _))
			{
				return false;
			}

			RenderTableCards();
			return true;
		}

		return false;
	}

	private async Task<QuestionCardRewardPlan?> PlanQuestionCardRewardAsync(
		int playerIndex,
		bool reserveReplacementCard,
		int turnSequenceGeneration,
		Func<bool>? canPlan = null,
		int previouslyPlannedHandCardDelta = 0,
		IReadOnlySet<int>? excludedDiscardDisplayIndexes = null,
		bool rejudgeOpportunityAlreadyPlanned = false)
	{
		bool CanPlanReward()
		{
			return turnSequenceGeneration == _turnSequenceGeneration
				&& !_isLeavingGame
				&& !IsGameOver()
				&& (canPlan?.Invoke() ?? true);
		}

		if (!CanPlanReward()
			|| !item_mode
			|| playerIndex < 0
			|| playerIndex >= _playerItemHands.Count)
		{
			return null;
		}

		string originalItemCardId = PickQuestionRewardItemCardId();
		if (string.IsNullOrEmpty(originalItemCardId))
		{
			return null;
		}

		QuestionRewardItemPlan rewardItemPlan =
			await PlanQuestionRewardItemCardIdAsync(
				originalItemCardId,
				playerIndex,
				rejudgeOpportunityAlreadyPlanned: rejudgeOpportunityAlreadyPlanned);
		if (!CanPlanReward())
		{
			return null;
		}
		ItemCard? itemCard = CreateItemCard(rewardItemPlan.ItemCardId);
		if (!itemCard.HasValue)
		{
			return null;
		}

		bool requiresAttachment = IsAttachmentItemCard(rewardItemPlan.ItemCardId);
		int discardDisplayIndex = -1;
		if (!requiresAttachment
			&& !HasRoomForNewHandCard(
				playerIndex,
				(reserveReplacementCard ? 1 : 0) + Math.Max(0, previouslyPlannedHandCardDelta)))
		{
			if (_networkAuthorityRuleTransactionActive
				&& ShouldUseInteractiveHumanItemFollowup(playerIndex))
			{
				BeginNetworkAuthorityQuestionRewardTransaction(
					playerIndex,
					rewardItemPlan.ItemCardId,
					requiresAttachment: false,
					reserveReplacement: reserveReplacementCard,
					turnSequenceGeneration);
			}
			int? plannedDiscard = await PlanHandLimitDiscardForQuestionRewardAsync(
				playerIndex,
				turnSequenceGeneration,
				excludedDiscardDisplayIndexes);
			if (!plannedDiscard.HasValue || !CanPlanReward())
			{
				return null;
			}
			discardDisplayIndex = plannedDiscard.Value;
		}

		return new QuestionCardRewardPlan(
			itemCard.Value,
			requiresAttachment,
			discardDisplayIndex,
			reserveReplacementCard,
			rewardItemPlan.ConsumeRejudgeOpportunity,
			rewardItemPlan.RejudgeResolvedForReward);
	}

	private QuestionCardRewardGrant? ApplyQuestionCardRewardPlan(
		int playerIndex,
		QuestionCardRewardPlan plan)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return null;
		}

		BeginNetworkAuthorityQuestionRewardTransaction(
			playerIndex,
			plan.ItemCard.Id,
			plan.RequiresMagnetAttachment,
			reserveReplacement: plan.ReserveReplacementCard,
			_turnSequenceGeneration);
		if (plan.ConsumeRejudgeOpportunity)
		{
			MarkAdventureQuestionRejudgeUsed();
		}

		if (plan.RequiresMagnetAttachment)
		{
			if (!TrySubmitEnqueuePendingAttachmentRewardRuleCommand(
					playerIndex,
					plan.ItemCard.Id,
					_turnSequenceGeneration))
			{
				return null;
			}
			CommitNetworkAuthorityQuestionRewardQueued();
			_debugQuestionRewardGrantCount++;
			_debugLastQuestionRewardItemCardId = plan.ItemCard.Id;
			return new QuestionCardRewardGrant(
				plan.ItemCard,
				-1,
				true,
				plan.RejudgeResolvedForReward);
		}

		long expectedDiscardCardRuntimeInstanceId = 0;
		string expectedDiscardItemId = string.Empty;
		if (plan.DiscardDisplayIndex >= 0
			&& !TryGetHandDisplayIdentity(
				playerIndex,
				plan.DiscardDisplayIndex,
				out expectedDiscardCardRuntimeInstanceId,
				out expectedDiscardItemId))
		{
			return null;
		}

		if (!TrySubmitGrantItemToHandRuleCommand(
			playerIndex,
			plan.ItemCard.Id,
			MaxTotalHandCards,
			plan.DiscardDisplayIndex,
			expectedDiscardCardRuntimeInstanceId,
			expectedDiscardItemId,
			_turnSequenceGeneration,
			out RuleCommandResult grantResult))
		{
			return null;
		}

		if (plan.DiscardDisplayIndex >= 0)
		{
			ShiftHandDisplayStateAfterRemovedCard(playerIndex, plan.DiscardDisplayIndex);
			CommitNetworkAuthorityDiscardMutation();
		}
		int itemHandIndex = grantResult.Mutation.TargetIndex;
		CommitNetworkAuthorityQuestionRewardHandMutation(playerIndex, itemHandIndex);
		_debugQuestionRewardGrantCount++;
		_debugLastQuestionRewardItemCardId = plan.ItemCard.Id;
		return new QuestionCardRewardGrant(
			plan.ItemCard,
			itemHandIndex,
			false,
			plan.RejudgeResolvedForReward);
	}

	private string PickQuestionRewardItemCardId()
	{
		if (ShouldForceBombQuestionReward())
		{
			return BombItemCardId;
		}

		string forcedItemCardId =
			ConsumeDebugForcedNextQuestionRewardItemCardId();
		if (!string.IsNullOrEmpty(forcedItemCardId))
		{
			return forcedItemCardId;
		}

		var options = ImmutableArray.CreateBuilder<RuleWeightedOption>();
		foreach (string itemCardId in _itemCardCatalog.RewardIds)
		{
			if (!_itemCardCatalog.TryGetDefinition(
				itemCardId,
				out ItemCardDefinition definition))
			{
				continue;
			}

			options.Add(new RuleWeightedOption(
				itemCardId,
				definition.Weight));
		}

		return options.Count > 0
			&& TrySubmitChooseWeightedOptionRuleCommand(
				_currentTurnPlayerIndex,
				options.ToImmutable(),
				_turnSequenceGeneration,
				out string selectedItemCardId)
				? selectedItemCardId
				: string.Empty;
	}

	private async Task<string> MaybeRejudgeQuestionRewardItemCardIdAsync(
		string originalItemCardId,
		int playerIndex,
		AdventureQuestionRejudgeChoice? forcedChoice = null)
	{
		QuestionRewardItemPlan plan = await PlanQuestionRewardItemCardIdAsync(
			originalItemCardId,
			playerIndex,
			forcedChoice);
		if (plan.ConsumeRejudgeOpportunity)
		{
			MarkAdventureQuestionRejudgeUsed();
		}
		return plan.ItemCardId;
	}

	private async Task<QuestionRewardItemPlan> PlanQuestionRewardItemCardIdAsync(
		string originalItemCardId,
		int playerIndex,
		AdventureQuestionRejudgeChoice? forcedChoice = null,
		bool rejudgeOpportunityAlreadyPlanned = false)
	{
		if (string.IsNullOrEmpty(originalItemCardId)
			|| !ShouldUseAdventureUpgradeSystem()
			|| playerIndex != MainPlayerIndex
			|| CurrentMatchFlowState.QuestionRejudgeUsedThisBattle
			|| rejudgeOpportunityAlreadyPlanned)
		{
			return new QuestionRewardItemPlan(originalItemCardId, false, false);
		}

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true
			|| !runState.ShouldOfferQuestionRewardRejudge(
				CurrentMatchFlowState.QuestionRejudgeUsedThisBattle))
		{
			return new QuestionRewardItemPlan(originalItemCardId, false, false);
		}

		AdventureQuestionRejudgeChoice choice;
		if (forcedChoice.HasValue)
		{
			choice = forcedChoice.Value;
		}
		else if (ShouldAutoResolveAdventureQuestionRejudge())
		{
			choice = AdventureQuestionRejudgeChoice.Keep;
		}
		else
		{
			choice = await PromptAdventureQuestionRejudgeAsync(originalItemCardId);
		}

		AdventureQuestionRejudgeRuleResult rejudgeResult =
			runState.ResolveQuestionRewardRejudgeChoice(
				CurrentMatchFlowState.QuestionRejudgeUsedThisBattle,
				choice == AdventureQuestionRejudgeChoice.Reroll
					? AdventureQuestionRejudgeRuleChoice.Reroll
					: AdventureQuestionRejudgeRuleChoice.Keep);
		if (!rejudgeResult.ShouldReroll)
		{
			return new QuestionRewardItemPlan(
				originalItemCardId,
				false,
				RejudgeResolvedForReward: rejudgeResult.Offered);
		}

		return new QuestionRewardItemPlan(
			PickQuestionRewardItemCardId(),
			rejudgeResult.ConsumesOpportunity,
			RejudgeResolvedForReward: true);
	}

	private bool ShouldAutoResolveAdventureQuestionRejudge()
	{
		string displayName = DisplayServer.GetName();
		return string.Equals(displayName, "headless", StringComparison.OrdinalIgnoreCase)
			|| !IsLocalSeatHumanConnected();
	}

	private async Task<AdventureQuestionRejudgeChoice> PromptAdventureQuestionRejudgeAsync(string originalItemCardId)
	{
		ItemCard? rewardItem = CreateItemCard(originalItemCardId);
		if (!rewardItem.HasValue)
		{
			return AdventureQuestionRejudgeChoice.Keep;
		}

		return (await PromptAdventureQuestionRejudgeOverlayAsync(rewardItem.Value)).Choice;
	}

}
