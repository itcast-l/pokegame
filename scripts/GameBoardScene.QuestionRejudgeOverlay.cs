using System;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private readonly record struct AdventureQuestionRejudgePromptResult(
		AdventureQuestionRejudgeChoice Choice,
		Rect2 PreviewGlobalRect,
		AdventureQuestionRejudgeOverlay? Overlay);

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
			GetItemCardTexture(rewardItem),
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
			},
			abnormalRelease: () => Resolve(AdventureQuestionRejudgeChoice.Keep)));
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
			|| rewardAnimation.Value.RejudgeResolvedForReward
			|| playerIndex != MainPlayerIndex
			|| !ShouldUseAdventureUpgradeSystem()
			|| CurrentMatchFlowState.QuestionRejudgeUsedThisBattle)
		{
			return;
		}

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true
			|| !runState.ShouldOfferQuestionRewardRejudge(
				CurrentMatchFlowState.QuestionRejudgeUsedThisBattle)
			|| ShouldAutoResolveAdventureQuestionRejudge())
		{
			return;
		}

		AdventureQuestionRejudgePromptResult promptResult = await PromptAdventureQuestionRejudgeOverlayAsync(rewardAnimation.Value.ItemCard);
		AdventureQuestionRejudgeOverlay? sourceOverlay = promptResult.Overlay;
		if (turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver())
		{
			DisposeActiveSelectionOverlay();
			return;
		}

		AdventureQuestionRejudgeRuleResult rejudgeResult =
			runState.ResolveQuestionRewardRejudgeChoice(
				CurrentMatchFlowState.QuestionRejudgeUsedThisBattle,
				promptResult.Choice == AdventureQuestionRejudgeChoice.Reroll
					? AdventureQuestionRejudgeRuleChoice.Reroll
					: AdventureQuestionRejudgeRuleChoice.Keep);
		if (rejudgeResult.ConsumesOpportunity)
		{
			MarkAdventureQuestionRejudgeUsed();
		}
		if (!rejudgeResult.ShouldReroll || sourceOverlay == null)
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

		_debugLastQuestionRewardItemCardId = rerolledItemCardId;
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

			if (!HasPendingAttachmentReward(playerIndex, grantedReward.ItemCard.Id)
				|| !TrySubmitGrantItemToHandRuleCommand(
					playerIndex,
					replacementPlan.RerolledItemCardId,
					MaxTotalHandCards,
					-1,
					0,
					string.Empty,
					_turnSequenceGeneration,
					out _)
				|| !TryRemovePendingAttachmentReward(playerIndex, grantedReward.ItemCard.Id))
			{
				return false;
			}

			return true;
		}

		if (grantedReward.ItemHandIndex < 0
			|| grantedReward.ItemHandIndex >= _playerItemHands[playerIndex].Count)
		{
			return false;
		}

		if (!replacementPlan.RerolledRequiresAttachment)
		{
			return TrySubmitReplaceItemInHandRuleCommand(
				playerIndex,
				grantedReward.ItemHandIndex,
				grantedReward.ItemCard.Id,
				replacementPlan.RerolledItemCardId,
				_turnSequenceGeneration,
				out _);
		}

		int displayIndex = _playerHands[playerIndex].Count + grantedReward.ItemHandIndex;
		if (!TrySubmitDiscardHandCardRuleCommand(
			playerIndex,
			displayIndex,
			0,
			grantedReward.ItemCard.Id,
			_turnSequenceGeneration,
			out _))
		{
			return false;
		}

		ShiftHandDisplayStateAfterRemovedCard(playerIndex, displayIndex);
		return TrySubmitEnqueuePendingAttachmentRewardRuleCommand(
			playerIndex,
			replacementPlan.RerolledItemCardId,
			_turnSequenceGeneration);
	}

	private bool HasPendingAttachmentReward(int playerIndex, string itemCardId)
	{
		foreach (PendingAttachmentRewardState reward in GetPendingAttachmentRewards())
		{
			if (reward.Seat == playerIndex
				&& string.Equals(reward.ItemId, itemCardId, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryRemovePendingAttachmentReward(int playerIndex, string itemCardId)
	{
		return TrySubmitRemovePendingAttachmentRewardRuleCommand(
			playerIndex,
			itemCardId,
			_turnSequenceGeneration);
	}

	private bool TryReplacePendingAttachmentReward(int playerIndex, string originalItemCardId, string rerolledItemCardId)
	{
		return TrySubmitReplacePendingAttachmentRewardRuleCommand(
			playerIndex,
			originalItemCardId,
			rerolledItemCardId,
			_turnSequenceGeneration);
	}

	private string GetAdventureQuestionRejudgePromptText()
	{
		return GetRequiredLocalizedText("GAME_REJUDGE_PROMPT", "question rejudge prompt");
	}

	private string GetAdventureQuestionRejudgeKeepText()
	{
		return GetRequiredLocalizedText("GAME_REJUDGE_KEEP", "question rejudge keep action");
	}

	private string GetAdventureQuestionRejudgeRerollText()
	{
		return GetRequiredLocalizedText("GAME_REJUDGE_REROLL_ONCE", "question rejudge reroll action");
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

		return GetItemDescription(definition.DescriptionKey);
	}
}
