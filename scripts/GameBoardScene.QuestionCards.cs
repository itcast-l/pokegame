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

	private async Task<QuestionCardRewardGrant?> GrantQuestionCardRewardAsync(
	int playerIndex,
	bool reserveReplacementCard,
	int turnSequenceGeneration,
	Func<bool>? canCommit = null)
{
	bool CanCommitReward()
	{
		return turnSequenceGeneration == _turnSequenceGeneration
			&& !_isLeavingGame
			&& !IsGameOver()
			&& (canCommit?.Invoke() ?? true);
	}

	if (!CanCommitReward() || !item_mode)
	{
		return null;
	}

	if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
	{
		return null;
	}

	string itemCardId = PickQuestionRewardItemCardId();
	if (string.IsNullOrEmpty(itemCardId))
	{
		return null;
	}

	BeginNetworkAuthorityQuestionRewardTransaction(
		playerIndex,
		itemCardId,
		IsAttachmentItemCard(itemCardId),
		reserveReplacementCard,
		turnSequenceGeneration);

	ItemCard? itemCard = CreateItemCard(itemCardId);
	if (!itemCard.HasValue)
	{
		return null;
	}

	if (IsAttachmentItemCard(itemCardId))
	{
		if (!CanCommitReward())
		{
			return null;
		}

		_pendingItemAttachmentRewards.Enqueue(
			new PendingItemAttachmentReward(playerIndex, itemCardId));
		CommitNetworkAuthorityQuestionRewardQueued();
		_debugQuestionRewardGrantCount++;
		_debugLastQuestionRewardItemCardId = itemCardId;
		return new QuestionCardRewardGrant(itemCard.Value, -1, true);
	}

	if (!HasRoomForNewHandCard(
			playerIndex,
			reserveReplacementCard ? 1 : 0)
		&& !await DiscardHandCardForNewItemCardAsync(
			playerIndex,
			turnSequenceGeneration,
			CanCommitReward))
	{
		return null;
	}

	if (!CanCommitReward())
	{
		return null;
	}

	int itemHandIndex = _playerItemHands[playerIndex].Count;
	_playerItemHands[playerIndex].Add(itemCard.Value);
	CommitNetworkAuthorityQuestionRewardHandMutation(playerIndex, itemHandIndex);
	_debugQuestionRewardGrantCount++;
	_debugLastQuestionRewardItemCardId = itemCardId;
	return new QuestionCardRewardGrant(itemCard.Value, itemHandIndex);
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
		if (string.IsNullOrEmpty(originalItemCardId)
			|| !ShouldUseAdventureUpgradeSystem()
			|| playerIndex != MainPlayerIndex
			|| _adventureQuestionRejudgeUsedThisBattle)
		{
			return originalItemCardId;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true
			|| !AdventureUpgradeEffectService.ShouldOfferQuestionRewardRejudge(
				runState.GetAcquiredUpgradeIds(),
				_adventureQuestionRejudgeUsedThisBattle))
		{
			return originalItemCardId;
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
		if (rejudgeResult.ConsumesOpportunity)
		{
			_adventureQuestionRejudgeUsedThisBattle = true;
		}
		if (!rejudgeResult.ShouldReroll)
		{
			return originalItemCardId;
		}

		return PickQuestionRewardItemCardId();
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

	private AdventureQuestionRewardEffectResult? ResolveAdventureQuestionRewardEffects(int playerIndex)
	{
		if (!ShouldUseAdventureUpgradeSystem() || playerIndex != MainPlayerIndex)
		{
			return null;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? runState = store?.GetRunState();
		if (runState?.IsActive != true)
		{
			return null;
		}

		AdventureQuestionRewardEffectResult effectResult = AdventureUpgradeEffectService.OnQuestionRewardGranted(
			runState.EffectState,
			runState.GetAcquiredUpgradeIds());
		store!.ReplaceEffectState(runState.EffectState.Clone());
		return effectResult;
	}
}
