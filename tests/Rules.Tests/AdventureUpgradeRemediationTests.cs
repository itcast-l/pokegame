using System;
using System.Collections.Generic;
using System.IO;

internal static partial class Program
{
	private static void RunAdventureUpgradeRemediationTests()
	{
		Run("extra action grant is consumed only when the action is accepted", ExtraActionGrantIsConsumedOnlyWhenActionIsAccepted);
		Run("extra action queue enforces eight actions per real turn", ExtraActionQueueEnforcesEightActionsPerRealTurn);
		Run("extra action root chain enforces thirty two events", ExtraActionRootChainEnforcesThirtyTwoEvents);
		Run("extra action root chain blocks source reentry and inherits resume", ExtraActionRootChainBlocksSourceReentryAndInheritsResume);
		Run("extra action abort clears the chain and preserves normal turn promise", ExtraActionAbortClearsChainAndPreservesNormalTurnPromise);
		Run("continue branch is limited once per real turn", ContinueBranchIsLimitedOncePerRealTurn);
		Run("miss bandage commits only at a real turn end", MissBandageCommitsOnlyAtRealTurnEnd);
		Run("capture resolution context deduplicates extensions by stable identity", CaptureResolutionContextDeduplicatesExtensionsByStableIdentity);
		Run("capture resolution context separates explicit duplicate score items", CaptureResolutionContextSeparatesExplicitDuplicateScoreItems);
		Run("capture resolution context keeps rule fallback separate from attachments", CaptureResolutionContextKeepsRuleFallbackSeparateFromAttachments);
		Run("capture resolution context counts stable base card identities once", CaptureResolutionContextCountsStableBaseCardIdentitiesOnce);
		Run("capture resolution context merges actual attachment ids", CaptureResolutionContextMergesActualAttachmentIds);
		Run("hollow burst evaluates the complete capture set", HollowBurstEvaluatesCompleteCaptureSet);
		Run("enchanted recovery caps once per action and only draws on capture", EnchantedRecoveryCapsOncePerActionAndOnlyDrawsOnCapture);
		Run("draw requests aggregate then clamp to capacity", DrawRequestsAggregateThenClampToCapacity);
		Run("shuffle then hang left preserves both effects", ShuffleThenHangLeftPreservesBothEffects);
		Run("real turn end clears turn scope before intercept", RealTurnEndClearsTurnScopeBeforeIntercept);
	}

	private static void ExtraActionGrantIsConsumedOnlyWhenActionIsAccepted()
	{
		var queue = new AdventureExtraActionQueueState();
		AdventureExtraActionGrantResult granted = queue.TryGrant(
			new AdventureExtraActionGrantRequest("source_a", ResumeNormalTurnAfterChain: false));
		True(granted.Granted);
		Equal(0, queue.ExtraActionsExecutedThisRealTurn);
		Equal(1, queue.QueuedGrantCount);

		AdventureExtraActionAcceptResult noGrant = queue.TryAccept(null);
		Equal(AdventureExtraActionAcceptStatus.NoGrant, noGrant.Status);
		Equal(0, queue.ExtraActionsExecutedThisRealTurn);
		Equal(1, queue.QueuedGrantCount);

		AdventureExtraActionGrant expected = granted.Grant!.Value;
		var mismatched = expected with { SourceUpgradeId = "other_source" };
		AdventureExtraActionAcceptResult mismatch = queue.TryAccept(mismatched);
		Equal(AdventureExtraActionAcceptStatus.QueueMismatch, mismatch.Status);
		Equal(0, queue.ExtraActionsExecutedThisRealTurn);
		Equal(1, queue.QueuedGrantCount);

		AdventureExtraActionAcceptResult accepted = queue.TryAccept(expected);
		True(accepted.Accepted);
		Equal(1, queue.ExtraActionsExecutedThisRealTurn);
		Equal(0, queue.QueuedGrantCount);
		queue.FinishActionExecution(accepted.Grant);
		AdventureExtraActionCompletionResult completion = queue.Complete(expected);
		Equal(AdventureExtraActionCompletionKind.FollowActionResult, completion.Kind);
		Equal(0L, queue.ActiveRootChainId);
	}

	private static void ExtraActionQueueEnforcesEightActionsPerRealTurn()
	{
		var queue = new AdventureExtraActionQueueState();
		for (int actionIndex = 0;
			actionIndex < AdventureExtraActionQueueState.DefaultMaxExtraActionsPerRealTurn;
			actionIndex++)
		{
			AdventureExtraActionGrantResult granted = queue.TryGrant(
				new AdventureExtraActionGrantRequest(
					$"budget_source_{actionIndex}",
					ResumeNormalTurnAfterChain: false));
			True(granted.Granted);
			AdventureExtraActionAcceptResult accepted = queue.TryAccept(granted.Grant);
			True(accepted.Accepted);
			queue.FinishActionExecution(accepted.Grant);
			Equal(
				AdventureExtraActionCompletionKind.FollowActionResult,
				queue.Complete(accepted.Grant!.Value).Kind);
		}

		Equal(8, queue.ExtraActionsExecutedThisRealTurn);
		AdventureExtraActionGrantResult exhausted = queue.TryGrant(
			new AdventureExtraActionGrantRequest("ninth_source", ResumeNormalTurnAfterChain: false));
		Equal(AdventureExtraActionGrantStatus.ExtraActionBudgetExhausted, exhausted.Status);
		False(queue.HasPendingGrant);

		queue.BeginRealTurn();
		AdventureExtraActionGrantResult nextTurn = queue.TryGrant(
			new AdventureExtraActionGrantRequest("next_turn_source", ResumeNormalTurnAfterChain: false));
		True(nextTurn.Granted);
	}

	private static void ExtraActionRootChainEnforcesThirtyTwoEvents()
	{
		var queue = new AdventureExtraActionQueueState(
			maxExtraActionsPerRealTurn: 64,
			maxEventsPerRootChain: 32);
		AdventureExtraActionGrantResult root = queue.TryGrant(
			new AdventureExtraActionGrantRequest("root_source", ResumeNormalTurnAfterChain: true));
		AdventureExtraActionAcceptResult acceptedRoot = queue.TryAccept(root.Grant);
		True(acceptedRoot.Accepted);

		for (int eventIndex = 1; eventIndex < 32; eventIndex++)
		{
			True(queue.TryGrant(
				new AdventureExtraActionGrantRequest(
					$"root_event_{eventIndex}",
					ResumeNormalTurnAfterChain: false)).Granted);
		}

		AdventureExtraActionGrantResult exhausted = queue.TryGrant(
			new AdventureExtraActionGrantRequest("root_event_32", ResumeNormalTurnAfterChain: false));
		Equal(AdventureExtraActionGrantStatus.RootEventBudgetExhausted, exhausted.Status);
		Equal(32, exhausted.RootEventCount);
		False(queue.HasPendingGrant);
		queue.FinishActionExecution(acceptedRoot.Grant);
		AdventureExtraActionCompletionResult completion = queue.Complete(acceptedRoot.Grant!.Value);
		Equal(AdventureExtraActionCompletionKind.ResumeNormalTurn, completion.Kind);
		Equal(0L, queue.ActiveRootChainId);
	}

	private static void ExtraActionRootChainBlocksSourceReentryAndInheritsResume()
	{
		var queue = new AdventureExtraActionQueueState();
		AdventureExtraActionGrantResult root = queue.TryGrant(
			new AdventureExtraActionGrantRequest("intercept", ResumeNormalTurnAfterChain: true));
		AdventureExtraActionAcceptResult acceptedRoot = queue.TryAccept(root.Grant);
		True(acceptedRoot.Accepted);

		AdventureExtraActionGrantResult reentry = queue.TryGrant(
			new AdventureExtraActionGrantRequest("intercept", ResumeNormalTurnAfterChain: false));
		Equal(AdventureExtraActionGrantStatus.ReentryBlocked, reentry.Status);

		AdventureExtraActionGrantResult derived = queue.TryGrant(
			new AdventureExtraActionGrantRequest("continue", ResumeNormalTurnAfterChain: false));
		True(derived.Granted);
		True(derived.Grant!.Value.ResumeNormalTurnAfterChain);
		Equal(root.RootChainId, derived.RootChainId);
		AdventureExtraActionAcceptResult duplicateCallback = queue.TryAccept(derived.Grant);
		Equal(AdventureExtraActionAcceptStatus.ActionInProgress, duplicateCallback.Status);
		Equal(1, queue.QueuedGrantCount);

		queue.FinishActionExecution(acceptedRoot.Grant);
		Equal(
			AdventureExtraActionCompletionKind.ContinueChain,
			queue.Complete(acceptedRoot.Grant!.Value).Kind);
		AdventureExtraActionAcceptResult acceptedDerived = queue.TryAccept(derived.Grant);
		True(acceptedDerived.Accepted);
		queue.FinishActionExecution(acceptedDerived.Grant);
		Equal(
			AdventureExtraActionCompletionKind.ResumeNormalTurn,
			queue.Complete(acceptedDerived.Grant!.Value).Kind);
	}

	private static void ExtraActionAbortClearsChainAndPreservesNormalTurnPromise()
	{
		var queue = new AdventureExtraActionQueueState();
		AdventureExtraActionGrantResult first = queue.TryGrant(
			new AdventureExtraActionGrantRequest("intercept", ResumeNormalTurnAfterChain: true));
		True(first.Granted);
		True(queue.TryGrant(
			new AdventureExtraActionGrantRequest("derived", ResumeNormalTurnAfterChain: false)).Granted);

		AdventureExtraActionAbortResult aborted = queue.Abort();
		True(aborted.HadActiveRootChain);
		True(aborted.ResumeNormalTurn);
		Equal(2, aborted.ClearedGrantCount);
		False(queue.HasPendingGrant);
		Equal(0L, queue.ActiveRootChainId);

		AdventureExtraActionGrantResult nextRoot = queue.TryGrant(
			new AdventureExtraActionGrantRequest("later", ResumeNormalTurnAfterChain: false));
		True(nextRoot.Granted);
		False(first.RootChainId == nextRoot.RootChainId);
	}

	private static void ContinueBranchIsLimitedOncePerRealTurn()
	{
		var queue = new AdventureExtraActionQueueState();
		AdventureExtraActionGrantRequest request = new(
			"clubs_continue_branch",
			ResumeNormalTurnAfterChain: false,
			OncePerRealTurn: true);
		AdventureExtraActionGrantResult first = queue.TryGrant(request);
		AdventureExtraActionAcceptResult accepted = queue.TryAccept(first.Grant);
		queue.FinishActionExecution(accepted.Grant);
		queue.Complete(accepted.Grant!.Value);

		Equal(
			AdventureExtraActionGrantStatus.OncePerRealTurnBlocked,
			queue.TryGrant(request).Status);
		queue.BeginRealTurn();
		True(queue.TryGrant(request).Granted);
	}

	private static void MissBandageCommitsOnlyAtRealTurnEnd()
	{
		var queue = new AdventureExtraActionQueueState();
		queue.RegisterMissBandageForRealTurnEnd();
		True(queue.PendingMissBandageRegistration);

		queue.BeginRealTurn();
		True(queue.PendingMissBandageRegistration);
		AdventureExtraActionGrantResult intercept = queue.TryGrant(
			new AdventureExtraActionGrantRequest("intercept", ResumeNormalTurnAfterChain: true));
		True(intercept.Granted);
		True(queue.Abort().ResumeNormalTurn);
		True(queue.PendingMissBandageRegistration);

		True(queue.EndRealTurn());
		False(queue.PendingMissBandageRegistration);
		False(queue.EndRealTurn());

		queue.RegisterMissBandageForRealTurnEnd();
		queue.Reset();
		False(queue.EndRealTurn());
	}

	private static void CaptureResolutionContextDeduplicatesExtensionsByStableIdentity()
	{
		PlayingCard playedCard = new("clubs", "4", null!);
		PlayingCard otherCard = new("hearts", "4", null!);
		var first = new TableCard(playedCard, isFaceUp: true);
		var second = new TableCard(otherCard, isFaceUp: true);
		BaseCardActionContext action = CreateActionContext(playedCard);
		var context = new CaptureResolutionContext(
			action,
			new[]
			{
				CaptureEntry(first, 2, AdventureCaptureEntrySource.PrimaryRange, isPlayedCard: true),
				CaptureEntry(first, 2, AdventureCaptureEntrySource.MagnetExtension, isPlayedCard: true),
				CaptureEntry(second, 1, AdventureCaptureEntrySource.GapRootingExtension),
			},
			new[]
			{
				ScoreItem(first, 2, 2),
				ScoreItem(first, 2, 4),
				ScoreItem(second, 3, 3),
			},
			AdventureCaptureMatchSource.Rank);

		Equal(2, context.CapturedCards.Count);
		Equal(2, context.CompleteBaseCardCount);
		Equal(2, context.NormalScoreItems.Count);
		Equal(4, context.NormalScoreItems[0].ResolvedPoints);
		True((context.CapturedCards[1].Source & AdventureCaptureEntrySource.PrimaryRange) != 0);
		True((context.CapturedCards[1].Source & AdventureCaptureEntrySource.MagnetExtension) != 0);
	}

	private static void CaptureResolutionContextSeparatesExplicitDuplicateScoreItems()
	{
		PlayingCard playedCard = new("diamonds", "10", null!);
		var tableCard = new TableCard(playedCard, isFaceUp: true);
		var context = new CaptureResolutionContext(
			CreateActionContext(playedCard),
			new[] { CaptureEntry(tableCard, 0, AdventureCaptureEntrySource.PrimaryRange, isPlayedCard: true) },
			new[]
			{
				ScoreItem(tableCard, 10, 20),
				new AdventureCaptureScoreItem(
					tableCard.InstanceId,
					playedCard.InstanceId,
					10,
					20,
					IsExplicitDuplicate: true,
					SourceUpgradeId: "diamonds_top_prize_repeat"),
			},
			AdventureCaptureMatchSource.Rank);

		Equal(1, context.NormalScoreItems.Count);
		Equal(1, context.ExplicitDuplicateScoreItems.Count);
		Equal("diamonds_top_prize_repeat", context.ExplicitDuplicateScoreItems[0].SourceUpgradeId);
	}

	private static void CaptureResolutionContextKeepsRuleFallbackSeparateFromAttachments()
	{
		PlayingCard playedCard = new("clubs", "7", null!);
		PlayingCard endpointCard = new("clubs", "2", null!);
		var first = new TableCard(playedCard, isFaceUp: true);
		var second = new TableCard(endpointCard, isFaceUp: true);
		var context = new CaptureResolutionContext(
			CreateActionContext(playedCard),
			new[]
			{
				CaptureEntry(first, 0, AdventureCaptureEntrySource.PrimaryRange, isPlayedCard: true),
				CaptureEntry(second, 1, AdventureCaptureEntrySource.PrimaryRange),
			},
			Array.Empty<AdventureCaptureScoreItem>(),
			AdventureCaptureMatchSource.SuitFallback,
			Array.Empty<string>());

		Equal(AdventureCaptureMatchSource.SuitFallback, context.MatchSource);
		Equal(0, context.TriggeredAttachedItemCardIds.Count);
		True(context.CapturedOnlyTwoVisibleEndpoints());
		False(context.HasTriggeredAttachedItem("suit_collect"));
	}

	private static void CaptureResolutionContextCountsStableBaseCardIdentitiesOnce()
	{
		PlayingCard playedCard = new("clubs", "5", null!);
		var firstTableCard = new TableCard(playedCard, isFaceUp: true);
		var mirroredTableCard = new TableCard(playedCard, isFaceUp: true);
		var context = new CaptureResolutionContext(
			CreateActionContext(playedCard),
			new[]
			{
				CaptureEntry(firstTableCard, 0, AdventureCaptureEntrySource.PrimaryRange, isPlayedCard: true),
				CaptureEntry(mirroredTableCard, 1, AdventureCaptureEntrySource.OtherExtension, isPlayedCard: true),
			},
			Array.Empty<AdventureCaptureScoreItem>(),
			AdventureCaptureMatchSource.Rank);

		Equal(2, context.CapturedCards.Count);
		Equal(1, context.CompleteBaseCardCount);
	}

	private static void CaptureResolutionContextMergesActualAttachmentIds()
	{
		PlayingCard playedCard = new("hearts", "6", null!);
		BaseCardActionContext action = CreateActionContext(playedCard) with
		{
			TriggeredAttachedItemCardIds = new[] { "borrow_tide", "borrow_tide" },
		};
		var context = new CaptureResolutionContext(
			action,
			Array.Empty<AdventureCapturedCardEntry>(),
			Array.Empty<AdventureCaptureScoreItem>(),
			AdventureCaptureMatchSource.AlternateRank,
			new[] { "overtime", "borrow_tide" });

		Equal(2, context.TriggeredAttachedItemCardIds.Count);
		Equal("borrow_tide", context.TriggeredAttachedItemCardIds[0]);
		Equal("overtime", context.TriggeredAttachedItemCardIds[1]);
		Equal(2, context.Action.TriggeredAttachedItemCardIds.Count);
	}

	private static void DrawRequestsAggregateThenClampToCapacity()
	{
		AdventureCaptureRewardApplicationPlan plan =
			AdventureUpgradeEffectService.BuildCaptureRewardApplicationPlan(
				new AdventureQuestionRewardEffectResult(
					BonusDirectDamage: 4,
					HealAmount: int.MaxValue,
					DrawCount: 2),
				new AdventurePostCaptureEffectResult(
					HealAmount: 5,
					DrawCount: 3,
					RevealRemainingHiddenCards: true),
				currentHandCount: 4,
				maximumHandCount: 6,
				availableDeckCardCount: 1);

		Equal(int.MaxValue, plan.HealAmount);
		Equal(1, plan.DrawCount);
		Equal(4, plan.BonusDirectDamage);
		True(plan.RevealRemainingHiddenCards);
	}

	private static void ShuffleThenHangLeftPreservesBothEffects()
	{
		string source = File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"scripts",
			"GameBoardScene.AdventureUpgrades.cs"));
		int methodStart = source.IndexOf(
			"private async Task<bool> ApplyAdventureMainPlayerMissTableEffectsAsync(",
			StringComparison.Ordinal);
		int methodEnd = source.IndexOf(
			"private void ApplyAdventureCanopyRevealToNewTableCard(",
			methodStart,
			StringComparison.Ordinal);
		True(methodStart >= 0);
		True(methodEnd > methodStart);

		string method = source[methodStart..methodEnd];
		int stableReferenceIndex = method.IndexOf(
			"TableCard playedTableCard = _tableCards[playedTableCardIndex]",
			StringComparison.Ordinal);
		int shuffledApplyIndex = method.IndexOf(
			"ApplyShuffledTableCards(shuffledTableCards)",
			StringComparison.Ordinal);
		int hangLeftIndex = method.IndexOf(
			"_tableCards.Insert(0, playedTableCard)",
			StringComparison.Ordinal);
		True(stableReferenceIndex >= 0);
		True(shuffledApplyIndex > stableReferenceIndex);
		True(hangLeftIndex > shuffledApplyIndex);
	}

	private static void RealTurnEndClearsTurnScopeBeforeIntercept()
	{
		string source = File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"scripts",
			"GameBoardScene.TurnAdvancement.cs"));
		int methodStart = source.IndexOf(
			"private async Task<bool> EndRealMainPlayerTurnAsync()",
			StringComparison.Ordinal);
		int methodEnd = source.IndexOf(
			"private async Task ResolveActionCompletionAsync(",
			methodStart,
			StringComparison.Ordinal);
		True(methodStart >= 0);
		True(methodEnd > methodStart);

		string method = source[methodStart..methodEnd];
		int queueEndIndex = method.IndexOf(
			"AdventureExtraActionQueue.EndRealTurn()",
			StringComparison.Ordinal);
		int transactionIndex = method.IndexOf(
			"store.ResolveRealTurnEnd(commitMissBandage)",
			StringComparison.Ordinal);
		int captureOrdinalResetIndex = method.IndexOf(
			"_adventureMainPlayerSuccessfulCapturesThisTurn = 0",
			StringComparison.Ordinal);
		True(transactionIndex >= 0);
		True(queueEndIndex >= 0);
		True(queueEndIndex > transactionIndex);
		True(captureOrdinalResetIndex > queueEndIndex);
	}

	private static void HollowBurstEvaluatesCompleteCaptureSet()
	{
		PlayingCard playedCard = new("clubs", "2", null!);
		PlayingCard endpointCard = new("hearts", "2", null!);
		PlayingCard extensionCard = new("spades", "2", null!);
		var first = new TableCard(playedCard, isFaceUp: true);
		var second = new TableCard(endpointCard, isFaceUp: true);
		var extension = new TableCard(extensionCard, isFaceUp: true);
		BaseCardActionContext action = CreateActionContext(playedCard);
		AdventureCapturedCardEntry[] endpoints =
		{
			CaptureEntry(first, 0, AdventureCaptureEntrySource.PrimaryRange, isPlayedCard: true),
			CaptureEntry(second, 2, AdventureCaptureEntrySource.PrimaryRange),
		};
		var endpointOnly = new CaptureResolutionContext(
			action,
			endpoints,
			Array.Empty<AdventureCaptureScoreItem>(),
			AdventureCaptureMatchSource.Rank);
		True(endpointOnly.CapturedOnlyTwoVisibleEndpoints());

		var withMagnet = new CaptureResolutionContext(
			action,
			new[]
			{
				endpoints[0],
				CaptureEntry(extension, 1, AdventureCaptureEntrySource.MagnetExtension),
				endpoints[1],
			},
			Array.Empty<AdventureCaptureScoreItem>(),
			AdventureCaptureMatchSource.Rank);
		False(withMagnet.CapturedOnlyTwoVisibleEndpoints());

		var withGapRooting = new CaptureResolutionContext(
			action,
			new[]
			{
				endpoints[0],
				endpoints[1],
				CaptureEntry(extension, -1, AdventureCaptureEntrySource.GapRootingExtension),
			},
			Array.Empty<AdventureCaptureScoreItem>(),
			AdventureCaptureMatchSource.Rank);
		False(withGapRooting.CapturedOnlyTwoVisibleEndpoints());
	}

	private static void EnchantedRecoveryCapsOncePerActionAndOnlyDrawsOnCapture()
	{
		string[] acquired = { "hearts_enchanted_recovery" };
		string[] triggered = { "borrow_tide", "double", "overtime" };
		AdventurePostCaptureEffectResult captured =
			AdventureUpgradeEffectService.ResolveEnchantedRecoveryActionEffects(
				acquired,
				triggered,
				successfulCapture: true);
		Equal(3, captured.HealAmount);
		Equal(1, captured.DrawCount);

		AdventurePostCaptureEffectResult missed =
			AdventureUpgradeEffectService.ResolveEnchantedRecoveryActionEffects(
				acquired,
				triggered,
				successfulCapture: false);
		Equal(3, missed.HealAmount);
		Equal(0, missed.DrawCount);

		AdventurePostCaptureEffectResult noAttachment =
			AdventureUpgradeEffectService.ResolveEnchantedRecoveryActionEffects(
				acquired,
				Array.Empty<string>(),
				successfulCapture: true);
		Equal(0, noAttachment.HealAmount);
		Equal(0, noAttachment.DrawCount);
	}

	private static BaseCardActionContext CreateActionContext(PlayingCard playedCard)
	{
		return new BaseCardActionContext(
			ActionId: 1,
			RootChainId: 1,
			ActionSource: "test",
			IsExtraAction: false,
			playedCard.InstanceId,
			playedCard,
			Array.Empty<string>());
	}

	private static AdventureCapturedCardEntry CaptureEntry(
		TableCard tableCard,
		int tableIndex,
		AdventureCaptureEntrySource source,
		bool isPlayedCard = false)
	{
		return new AdventureCapturedCardEntry(
			tableCard.InstanceId,
			tableCard.Card?.InstanceId ?? 0,
			tableCard,
			tableIndex,
			tableCard.IsFaceUp,
			tableCard.IsBuriedInSand,
			isPlayedCard,
			source);
	}

	private static AdventureCaptureScoreItem ScoreItem(
		TableCard tableCard,
		int rawPoints,
		int resolvedPoints)
	{
		return new AdventureCaptureScoreItem(
			tableCard.InstanceId,
			tableCard.Card?.InstanceId ?? 0,
			rawPoints,
			resolvedPoints,
			IsExplicitDuplicate: false,
			SourceUpgradeId: string.Empty);
	}

	private static CaptureResolutionContext CreatePostCaptureTestContext(
		bool capturedOnlyVisibleEndpoints = false,
		bool triggeredAttachment = false)
	{
		PlayingCard firstCard = new("clubs", "4", null!);
		PlayingCard secondCard = new("hearts", "4", null!);
		var firstTableCard = new TableCard(firstCard, isFaceUp: true);
		var capturedEntries = new List<AdventureCapturedCardEntry>();
		if (capturedOnlyVisibleEndpoints)
		{
			var secondTableCard = new TableCard(secondCard, isFaceUp: true);
			capturedEntries.Add(CaptureEntry(
				firstTableCard,
				0,
				AdventureCaptureEntrySource.PrimaryRange,
				isPlayedCard: true));
			capturedEntries.Add(CaptureEntry(
				secondTableCard,
				1,
				AdventureCaptureEntrySource.PrimaryRange));
		}

		return new CaptureResolutionContext(
			CreateActionContext(firstCard),
			capturedEntries,
			Array.Empty<AdventureCaptureScoreItem>(),
			AdventureCaptureMatchSource.Rank,
			triggeredAttachment
				? new[] { "double" }
				: Array.Empty<string>());
	}
}
