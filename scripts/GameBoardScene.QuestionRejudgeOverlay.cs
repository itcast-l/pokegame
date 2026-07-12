using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private readonly record struct AdventureQuestionRejudgePromptResult(
		AdventureQuestionRejudgeChoice Choice,
		Rect2 PreviewGlobalRect,
		AdventureQuestionRejudgeOverlay? Overlay);

	private const string RejudgeKeepFallbackText = "Keep";
	private const string RejudgeRerollFallbackText = "Reroll Once";
	private const string RejudgePromptFallbackText = "Reset this reward?";

	private bool ShowAdventureQuestionRejudgeOverlay(
		ItemCard rewardItem,
		Action<AdventureQuestionRejudgeChoice, Rect2> resolveChoice,
		Action? onDisposed = null)
	{
		PackedScene? overlayScene = GD.Load<PackedScene>(AdventureQuestionRejudgeOverlayScenePath);
		if (overlayScene?.Instantiate() is not AdventureQuestionRejudgeOverlay overlay)
		{
			GD.PushError($"Missing adventure question rejudge overlay scene: {AdventureQuestionRejudgeOverlayScenePath}");
			return false;
		}

		void Resolve(AdventureQuestionRejudgeChoice choice)
		{
			overlay.HideDecisionPanelForResolution();
			resolveChoice(choice, GetAdventureQuestionRejudgePreviewGlobalRect(overlay));
		}

		AddChild(overlay);
		PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
		overlay.Configure(
			rewardItem.Texture,
			GetAdventureQuestionRewardEffectText(rewardItem),
			GetAdventureQuestionRejudgePromptText(),
			GetAdventureQuestionRejudgeKeepText(),
			GetAdventureQuestionRejudgeRerollText());
		overlay.OptionSelected += optionIndex =>
		{
			Resolve(optionIndex == 1
				? AdventureQuestionRejudgeChoice.Reroll
				: AdventureQuestionRejudgeChoice.Keep);
		};
		overlay.CancelRequested += () => Resolve(AdventureQuestionRejudgeChoice.Keep);

		RegisterSelectionOverlaySession(new SelectionOverlaySession(
			SelectionOverlayKind.QuestionRejudge,
			overlay,
			2,
			optionIndex =>
			{
				Resolve(optionIndex == 1
					? AdventureQuestionRejudgeChoice.Reroll
					: AdventureQuestionRejudgeChoice.Keep);
				return true;
			},
			cancel: () => Resolve(AdventureQuestionRejudgeChoice.Keep),
			dispose: () =>
			{
				onDisposed?.Invoke();
				if (IsInstanceValid(overlay))
				{
					RemoveChild(overlay);
					overlay.CallDeferred(Node.MethodName.QueueFree);
				}
			}));
		return true;
	}

	private async Task<AdventureQuestionRejudgePromptResult> PromptAdventureQuestionRejudgeOverlayAsync(ItemCard rewardItem)
	{
		var completionSource = new TaskCompletionSource<AdventureQuestionRejudgePromptResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		var fallbackResult = new AdventureQuestionRejudgePromptResult(
			AdventureQuestionRejudgeChoice.Keep,
			new Rect2(),
			null);
		if (!ShowAdventureQuestionRejudgeOverlay(rewardItem, (choice, previewGlobalRect) =>
		{
			AdventureQuestionRejudgeOverlay? overlay = GetActiveSelectionOverlaySession(SelectionOverlayKind.QuestionRejudge)?.Root as AdventureQuestionRejudgeOverlay;
			if (overlay != null)
			{
				completionSource.TrySetResult(new AdventureQuestionRejudgePromptResult(choice, previewGlobalRect, overlay));
			}
		}, onDisposed: () => completionSource.TrySetResult(fallbackResult)))
		{
			return fallbackResult;
		}

		return await completionSource.Task;
	}

	private static Rect2 GetAdventureQuestionRejudgePreviewGlobalRect(AdventureQuestionRejudgeOverlay overlay)
	{
		TextureRect? rewardCard = overlay.GetNodeOrNull<TextureRect>("RewardPreview/RewardMargin/RewardCard");
		return rewardCard != null && GodotObject.IsInstanceValid(rewardCard)
			? new Rect2(rewardCard.GlobalPosition, rewardCard.Size)
			: new Rect2();
	}

	private async Task MaybeResolveAppliedAdventureQuestionRewardRejudgeAsync(
		int playerIndex,
		QuestionCardRewardAnimation? rewardAnimation,
		int turnSequenceGeneration)
	{
		if (!rewardAnimation.HasValue
			|| playerIndex != MainPlayerIndex
			|| !ShouldUseAdventureUpgradeSystem()
			|| _adventureQuestionRejudgeUsedThisBattle)
		{
			return;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true
			|| !AdventureUpgradeEffectService.ShouldOfferQuestionRewardRejudge(
				runState.GetAcquiredUpgradeIds(),
				_adventureQuestionRejudgeUsedThisBattle)
			|| ShouldAutoResolveAdventureQuestionRejudge())
		{
			return;
		}

		AdventureQuestionRejudgePromptResult promptResult = await PromptAdventureQuestionRejudgeOverlayAsync(rewardAnimation.Value.ItemCard);
		AdventureQuestionRejudgeOverlay? sourceOverlay = promptResult.Overlay;
		if (turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver()
			|| promptResult.Choice != AdventureQuestionRejudgeChoice.Reroll
			|| sourceOverlay == null)
		{
			DisposeActiveSelectionOverlay();
			return;
		}

		string rerolledItemCardId = PickQuestionRewardItemCardId();
		if (string.IsNullOrEmpty(rerolledItemCardId))
		{
			DisposeActiveSelectionOverlay();
			return;
		}

		ItemCard? rerolledItemCard = CreateItemCard(rerolledItemCardId);
		if (!rerolledItemCard.HasValue)
		{
			DisposeActiveSelectionOverlay();
			return;
		}

		RerollReplacementPlan? replacementPlan = await PrepareQuestionRewardReplacementPlanAsync(
			playerIndex,
			rewardAnimation.Value,
			rerolledItemCardId,
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver()
			|| !replacementPlan.HasValue)
		{
			DisposeActiveSelectionOverlay();
			return;
		}

		await PlayQuestionCardRewardRejudgeAnimationAsync(
			playerIndex,
			promptResult.PreviewGlobalRect,
			sourceOverlay,
			rewardAnimation.Value,
			rerolledItemCard.Value,
			replacementPlan.Value.TargetItemHandIndex,
			replacementPlan.Value.RerolledRequiresAttachment,
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			DisposeActiveSelectionOverlay();
			return;
		}

		if (!ApplyPreparedQuestionRewardReplacement(
				playerIndex,
				rewardAnimation.Value,
				replacementPlan.Value))
		{
			return;
		}

		_adventureQuestionRejudgeUsedThisBattle = true;
		_debugLastQuestionRewardItemCardId = rerolledItemCardId;
		await RefillAdventureClubFlourishAsync(turnSequenceGeneration);
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
	}

	private readonly record struct RerollReplacementPlan(
		string RerolledItemCardId,
		bool RerolledRequiresAttachment,
		int TargetItemHandIndex);

	private async Task<RerollReplacementPlan?> PrepareQuestionRewardReplacementPlanAsync(
		int playerIndex,
		QuestionCardRewardAnimation grantedReward,
		string rerolledItemCardId,
		int turnSequenceGeneration)
	{
		bool rerolledRequiresAttachment = IsAttachmentItemCard(rerolledItemCardId);
		if (grantedReward.RequiresMagnetAttachment)
		{
			if (rerolledRequiresAttachment)
			{
				return new RerollReplacementPlan(rerolledItemCardId, true, -1);
			}

			if (!HasPendingAttachmentReward(playerIndex, grantedReward.ItemCard.Id))
			{
				return null;
			}

			if (!HasRoomForNewHandCard(playerIndex, 0)
				&& !await DiscardHandCardForNewItemCardAsync(playerIndex, turnSequenceGeneration))
			{
				return null;
			}

			return new RerollReplacementPlan(
				rerolledItemCardId,
				false,
				_playerItemHands[playerIndex].Count);
		}

		if (grantedReward.ItemHandIndex < 0
			|| grantedReward.ItemHandIndex >= _playerItemHands[playerIndex].Count)
		{
			return null;
		}

		if (!rerolledRequiresAttachment)
		{
			return new RerollReplacementPlan(rerolledItemCardId, false, grantedReward.ItemHandIndex);
		}

		return new RerollReplacementPlan(rerolledItemCardId, true, -1);
	}

	private bool ApplyPreparedQuestionRewardReplacement(
		int playerIndex,
		QuestionCardRewardAnimation grantedReward,
		RerollReplacementPlan replacementPlan)
	{
		ItemCard? rerolledItemCard = CreateItemCard(replacementPlan.RerolledItemCardId);
		if (!rerolledItemCard.HasValue)
		{
			return false;
		}

		if (grantedReward.RequiresMagnetAttachment)
		{
			if (replacementPlan.RerolledRequiresAttachment)
			{
				return TryReplacePendingAttachmentReward(playerIndex, grantedReward.ItemCard.Id, replacementPlan.RerolledItemCardId);
			}

			if (!TryRemovePendingAttachmentReward(playerIndex, grantedReward.ItemCard.Id))
			{
				return false;
			}

			_playerItemHands[playerIndex].Add(rerolledItemCard.Value);
			return true;
		}

		if (grantedReward.ItemHandIndex < 0
			|| grantedReward.ItemHandIndex >= _playerItemHands[playerIndex].Count)
		{
			return false;
		}

		if (!replacementPlan.RerolledRequiresAttachment)
		{
			_playerItemHands[playerIndex][grantedReward.ItemHandIndex] = rerolledItemCard.Value;
			return true;
		}

		_playerItemHands[playerIndex].RemoveAt(grantedReward.ItemHandIndex);
		_pendingItemAttachmentRewards.Enqueue(new PendingItemAttachmentReward(playerIndex, replacementPlan.RerolledItemCardId));
		return true;
	}

	private bool HasPendingAttachmentReward(int playerIndex, string itemCardId)
	{
		foreach (PendingItemAttachmentReward reward in _pendingItemAttachmentRewards)
		{
			if (reward.PlayerIndex == playerIndex
				&& string.Equals(reward.ItemCardId, itemCardId, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryRemovePendingAttachmentReward(int playerIndex, string itemCardId)
	{
		PendingItemAttachmentReward[] queuedRewards = _pendingItemAttachmentRewards.ToArray();
		int removeIndex = -1;
		for (int index = queuedRewards.Length - 1; index >= 0; index--)
		{
			if (queuedRewards[index].PlayerIndex == playerIndex
				&& string.Equals(queuedRewards[index].ItemCardId, itemCardId, StringComparison.Ordinal))
			{
				removeIndex = index;
				break;
			}
		}

		if (removeIndex < 0)
		{
			return false;
		}

		_pendingItemAttachmentRewards.Clear();
		for (int index = 0; index < queuedRewards.Length; index++)
		{
			if (index != removeIndex)
			{
				_pendingItemAttachmentRewards.Enqueue(queuedRewards[index]);
			}
		}

		return true;
	}

	private bool TryReplacePendingAttachmentReward(int playerIndex, string originalItemCardId, string rerolledItemCardId)
	{
		PendingItemAttachmentReward[] queuedRewards = _pendingItemAttachmentRewards.ToArray();
		int replaceIndex = -1;
		for (int index = queuedRewards.Length - 1; index >= 0; index--)
		{
			if (queuedRewards[index].PlayerIndex == playerIndex
				&& string.Equals(queuedRewards[index].ItemCardId, originalItemCardId, StringComparison.Ordinal))
			{
				replaceIndex = index;
				break;
			}
		}

		if (replaceIndex < 0)
		{
			return false;
		}

		queuedRewards[replaceIndex] = new PendingItemAttachmentReward(playerIndex, rerolledItemCardId);
		_pendingItemAttachmentRewards.Clear();
		foreach (PendingItemAttachmentReward reward in queuedRewards)
		{
			_pendingItemAttachmentRewards.Enqueue(reward);
		}

		return true;
	}

	private string GetAdventureQuestionRejudgePromptText()
	{
		string localized = Localize("GAME_REJUDGE_PROMPT");
		return localized == "GAME_REJUDGE_PROMPT" ? RejudgePromptFallbackText : localized;
	}

	private string GetAdventureQuestionRejudgeKeepText()
	{
		string localized = Localize("GAME_REJUDGE_KEEP");
		return localized == "GAME_REJUDGE_KEEP" ? RejudgeKeepFallbackText : localized;
	}

	private string GetAdventureQuestionRejudgeRerollText()
	{
		string localized = Localize("GAME_REJUDGE_REROLL_ONCE");
		return localized == "GAME_REJUDGE_REROLL_ONCE" ? RejudgeRerollFallbackText : localized;
	}

	private string GetAdventureQuestionRewardEffectText(ItemCard rewardItem)
	{
		if (_itemCardDescriptions.TryGetValue(rewardItem.Id, out string? description)
			&& !string.IsNullOrWhiteSpace(description))
		{
			return description;
		}

		if (!_itemCardCatalog.TryGetDefinition(rewardItem.Id, out ItemCardDefinition definition))
		{
			return string.Empty;
		}

		string localizedDescription = GetItemDescription(definition.DescriptionKey);
		if (!string.IsNullOrWhiteSpace(localizedDescription)
			&& !string.Equals(localizedDescription, definition.DescriptionKey, StringComparison.Ordinal))
		{
			return localizedDescription;
		}

		return definition.Description;
	}
}
