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
		int turnSequenceGeneration)
	{
		if (!item_mode)
		{
			return null;
		}

		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return null;
		}

		string itemCardId = PickQuestionRewardItemCardId();
		itemCardId = await MaybeRejudgeQuestionRewardItemCardIdAsync(itemCardId, playerIndex);
		if (string.IsNullOrEmpty(itemCardId))
		{
			return null;
		}

		ItemCard? itemCard = CreateItemCard(itemCardId);
		if (!itemCard.HasValue)
		{
			return null;
		}

		if (IsAttachmentItemCard(itemCardId))
		{
			_pendingItemAttachmentRewards.Enqueue(new PendingItemAttachmentReward(playerIndex, itemCardId));
			return new QuestionCardRewardGrant(itemCard.Value, -1, true);
		}

		if (!HasRoomForNewHandCard(playerIndex, reserveReplacementCard ? 1 : 0)
			&& !await DiscardHandCardForNewItemCardAsync(playerIndex, turnSequenceGeneration))
		{
			return null;
		}

		int itemHandIndex = _playerItemHands[playerIndex].Count;
		_playerItemHands[playerIndex].Add(itemCard.Value);
		return new QuestionCardRewardGrant(itemCard.Value, itemHandIndex);
	}

	private string PickQuestionRewardItemCardId()
	{
		var candidates = new List<RewardCandidate>();
		foreach (string itemCardId in _itemCardCatalog.RewardIds)
		{
			if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition))
			{
				continue;
			}

			candidates.Add(new RewardCandidate(itemCardId, definition.Weight));
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

	private async Task<string> MaybeRejudgeQuestionRewardItemCardIdAsync(string originalItemCardId, int playerIndex)
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

		_adventureQuestionRejudgeUsedThisBattle = true;
		if (ShouldAutoResolveAdventureQuestionRejudge())
		{
			return originalItemCardId;
		}

		AdventureQuestionRejudgeChoice choice = await PromptAdventureQuestionRejudgeAsync(originalItemCardId);
		return choice == AdventureQuestionRejudgeChoice.Reroll
			? PickQuestionRewardItemCardId()
			: originalItemCardId;
	}

	private bool ShouldAutoResolveAdventureQuestionRejudge()
	{
		string displayName = DisplayServer.GetName();
		return string.Equals(displayName, "headless", StringComparison.OrdinalIgnoreCase)
			|| !IsLocalSeatHumanConnected();
	}

	private async Task<AdventureQuestionRejudgeChoice> PromptAdventureQuestionRejudgeAsync(string originalItemCardId)
	{
		var completionSource = new TaskCompletionSource<AdventureQuestionRejudgeChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
		PackedScene? selectionOverlayScene = GD.Load<PackedScene>(ItemSelectionOverlayScenePath);
		if (selectionOverlayScene?.Instantiate() is not ItemSelectionOverlay selectionOverlay)
		{
			return AdventureQuestionRejudgeChoice.Keep;
		}

		void ResolveChoice(AdventureQuestionRejudgeChoice choice)
		{
			if (completionSource.TrySetResult(choice))
			{
				DisposeActiveSelectionOverlay();
			}
		}

		AddChild(selectionOverlay);
		PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
		selectionOverlay.Configure(
			$"Reward: {originalItemCardId}",
			new List<string> { "Keep", "Reroll Once" },
			Localize("GAME_CANCEL"),
			showCancel: true);
		selectionOverlay.OptionSelected += optionIndex =>
		{
			ResolveChoice(optionIndex == 1
				? AdventureQuestionRejudgeChoice.Reroll
				: AdventureQuestionRejudgeChoice.Keep);
		};
		selectionOverlay.CancelRequested += () => ResolveChoice(AdventureQuestionRejudgeChoice.Keep);

		RegisterSelectionOverlaySession(new SelectionOverlaySession(
			SelectionOverlayKind.ItemSelection,
			selectionOverlay,
			2,
			optionIndex =>
			{
				ResolveChoice(optionIndex == 1
					? AdventureQuestionRejudgeChoice.Reroll
					: AdventureQuestionRejudgeChoice.Keep);
				return true;
			},
			cancel: () => ResolveChoice(AdventureQuestionRejudgeChoice.Keep),
			dispose: () =>
			{
				if (IsInstanceValid(selectionOverlay))
				{
					RemoveChild(selectionOverlay);
					selectionOverlay.CallDeferred(Node.MethodName.QueueFree);
				}
			}));

		return await completionSource.Task;
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
		if (!_adventureMainPlayerQuestionRewardEchoActive)
		{
			foreach (string upgradeId in runState.GetAcquiredUpgradeIds())
			{
				if (upgradeId == "diamonds_question_mark_echo")
				{
					_adventureMainPlayerQuestionRewardEchoActive = true;
					break;
				}
			}
		}
		store!.ReplaceEffectState(runState.EffectState.Clone());
		return effectResult;
	}
}
