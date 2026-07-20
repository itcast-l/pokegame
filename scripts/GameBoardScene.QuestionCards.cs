using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
	private readonly HashSet<int> _plannedQuestionRewardDiscardExclusions = new();

	private int AdvanceQuestionCardRefreshTurnCounter(int nextTurnPlayerIndex)
	{
		QuestionCardRefreshAdvanceResult refreshResult = _turnAdvancementService.AdvanceQuestionCardRefresh(
			_state,
			item_mode,
			HasTableSpecialCard(),
			nextTurnPlayerIndex,
			QuestionCardRefreshCyclesAfterCapture);

		return refreshResult.ShouldRefreshQuestionCard ? PlaceQuestionCardOnNextTableSlot() : -1;
	}

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

		_tableCards.Add(new TableCard(null, true, itemOverlay));
		return _tableCards.Count - 1;
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
		if (plan.DiscardDisplayIndex >= 0
			&& !RemoveHandCardFromGame(playerIndex, plan.DiscardDisplayIndex))
		{
			return null;
		}
		if (plan.DiscardDisplayIndex >= 0)
		{
			CommitNetworkAuthorityDiscardMutation();
		}
		if (plan.ConsumeRejudgeOpportunity)
		{
			_adventureQuestionRejudgeUsedThisBattle = true;
		}

		if (plan.RequiresMagnetAttachment)
		{
			_pendingItemAttachmentRewards.Enqueue(
				new PendingItemAttachmentReward(playerIndex, plan.ItemCard.Id));
			CommitNetworkAuthorityQuestionRewardQueued();
			_debugQuestionRewardGrantCount++;
			_debugLastQuestionRewardItemCardId = plan.ItemCard.Id;
			return new QuestionCardRewardGrant(
				plan.ItemCard,
				-1,
				true,
				plan.RejudgeResolvedForReward);
		}

		int itemHandIndex = _playerItemHands[playerIndex].Count;
		_playerItemHands[playerIndex].Add(plan.ItemCard);
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
	string forcedItemCardId =
		ConsumeDebugForcedNextQuestionRewardItemCardId();
	if (!string.IsNullOrEmpty(forcedItemCardId))
	{
		return forcedItemCardId;
	}

	var candidates = new List<RewardCandidate>();
	foreach (string itemCardId in _itemCardCatalog.RewardIds)
	{
		if (!_itemCardCatalog.TryGetDefinition(
			itemCardId,
			out ItemCardDefinition definition))
		{
			continue;
		}

		candidates.Add(new RewardCandidate(
			itemCardId,
			definition.Weight));
	}

	if (candidates.Count == 0)
	{
		return string.Empty;
	}

	RewardPlan reward = RewardResolver.PickWeightedReward(
		candidates,
		maxInclusive => _rng.RandiRange(1, maxInclusive));
	return reward.ItemCardId;
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
			_adventureQuestionRejudgeUsedThisBattle = true;
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
			|| _adventureQuestionRejudgeUsedThisBattle
			|| rejudgeOpportunityAlreadyPlanned)
		{
			return new QuestionRewardItemPlan(originalItemCardId, false, false);
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true
			|| !AdventureUpgradeEffectService.ShouldOfferQuestionRewardRejudge(
				runState.GetAcquiredUpgradeIds(),
				_adventureQuestionRejudgeUsedThisBattle))
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
			AdventureUpgradeEffectService.ResolveQuestionRewardRejudgeChoice(
				runState.GetAcquiredUpgradeIds(),
				_adventureQuestionRejudgeUsedThisBattle,
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
