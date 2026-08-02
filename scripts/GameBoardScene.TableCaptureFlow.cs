using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<CaptureTableCardsResult> CaptureTableCardsAsync(
		int playerIndex,
		int captureStartIndex,
		bool reserveReplacementCard = false,
		int turnSequenceGeneration = 0,
		HashSet<int>? extraTableCardIndexes = null,
		int adventureFlatDamageDelta = 0,
		IReadOnlyCollection<TableCard>? prePlayTableCards = null,
		int preserveTailTableCardIndex = -1,
		bool suppressGardenEelCaptureRecord = false,
		IReadOnlyList<TableCard>? expectedTableCardReferences = null,
		Action? commitImmediatelyBeforeApply = null,
		int blackLedgerRedirectTableCardIndex = -2,
		BaseCardActionContext? baseCardActionContext = null,
		string actionSource = "capture",
		AdventureCaptureMatchSource matchSource = AdventureCaptureMatchSource.CollectAll,
		IReadOnlyCollection<string>? triggeredAttachedItemCardIds = null)
{
	if (expectedTableCardReferences == null
		|| _isLeavingGame
		|| IsGameOver()
		|| !IsCaptureTableReferenceSnapshotCurrent(
			turnSequenceGeneration,
			expectedTableCardReferences))
	{
		return CaptureTableCardsResult.AbortedBeforeApply;
	}

	if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
	{
		return CaptureTableCardsResult.NotApplied;
	}

	if (captureStartIndex < 0
		|| captureStartIndex >= _tableCards.Count)
	{
		return CaptureTableCardsResult.NotApplied;
	}

	CaptureStatePlan capturePlan = _captureStateService.CreatePlan(
		_state,
		captureStartIndex,
		extraTableCardIndexes);
	if (!capturePlan.IsValid)
	{
		return CaptureTableCardsResult.NotApplied;
	}
	if (blackLedgerRedirectTableCardIndex == -2)
	{
		blackLedgerRedirectTableCardIndex =
			ResolveBlackLedgerRedirectTableCardIndex(
				playerIndex,
				captureStartIndex);
	}

	CaptureScoringRules.TableCard[] captureScoringTableCards =
		ToCaptureScoringTableCards();
			int scorePlayerIndex = ScoreService.GetCapturedScorePlayerIndex(
					captureScoringTableCards,
					captureStartIndex,
					playerIndex,
					player_count);
		CaptureFlowPlan captureFlow = CaptureFlowPlanner.Build(
			captureScoringTableCards,
			captureStartIndex,
			scorePlayerIndex,
			BuildCaptureScoringContext(scorePlayerIndex),
			extraTableCardIndexes);
	int collectedCardCount = 0;
	int removedTableCardCount = 0;
	QuestionCardRewardAnimation? questionRewardAnimation = null;
		AdventureQuestionRewardEffectResult? adventureQuestionRewardEffects = null;
		AdventurePostCaptureEffectResult? adventurePostCaptureEffects = null;
		CaptureResolutionContext? adventureCaptureContext = null;
		AdventureCaptureDamageResult resolvedCaptureDamage = default;
		AdventureClubCaptureEffectResult committedClubCaptureEffects = default;
		bool capturedTraceMark = false;
		bool capturedHuntMark = false;
		TableCard? clubGapRootingTargetTableCard = null;
		Vector2? questionRewardStartGlobalPosition = null;
		QuestionCardRewardPlan? capturedQuestionRewardPlan = null;
		QuestionCardRewardPlan? linkedStemsRewardPlan = null;

	CaptureTableCardsResult BuildAppliedResult(bool aborted)
	{
		var result = new CaptureTableCardsResult(
			Applied: true,
			Aborted: aborted,
			CollectedCardCount: collectedCardCount,
			RemovedTableCardCount: removedTableCardCount,
			QuestionRewardAnimation: questionRewardAnimation,
			AdventureQuestionRewardEffects:
				adventureQuestionRewardEffects,
				AdventurePostCaptureEffects:
					adventurePostCaptureEffects,
				AdventureCaptureContext:
					adventureCaptureContext);
		RecordDebugCaptureOwnershipResult(result);
		return result;
	}

		if (captureFlow.Capture.CapturedMiddleCards
		&& ShouldPlayCrabScoreReductionAbility(
			scorePlayerIndex,
			captureStartIndex,
			extraTableCardIndexes))
	{
		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(
			1,
			turnSequenceGeneration,
			AdventureEnemyAbilities.TideGuard);
		if (_isLeavingGame
			|| IsGameOver()
			|| !IsCaptureTableReferenceSnapshotCurrent(
				turnSequenceGeneration,
				expectedTableCardReferences))
		{
			return CaptureTableCardsResult.AbortedBeforeApply;
		}
	}

	Rect2 tableLayoutRect = GetAuthorityRuleSafeTableLayoutRect();
	Vector2 centerArenaGlobalPosition = GetAuthorityRuleSafeCenterArenaGlobalPosition();
	foreach (int tableCardIndex in capturePlan.BaseTableCardIndexes)
	{
		if (tableCardIndex < 0
			|| tableCardIndex >= _tableCards.Count)
			{
				return CaptureTableCardsResult.NotApplied;
			}
		}

		AdventureClubCaptureExtensionPlan clubCaptureExtensionPlan =
			BuildAdventureClubCaptureExtensionPlan(
				playerIndex,
				captureStartIndex,
				extraTableCardIndexes);
		clubGapRootingTargetTableCard =
			clubCaptureExtensionPlan.GapRootingTarget;

	HashSet<int>? preservedTableCardIndexes =
		preserveTailTableCardIndex >= 0
			? new HashSet<int>
			{
				preserveTailTableCardIndex,
			}
			: null;
	bool preserveCardEffectsWhenEnteringDeck =
		ShouldPreserveCardEffectsWhenEnteringDeck();
	await WaitForDebugCaptureBeforeApplyBarrierAsync();
	if (_isLeavingGame
		|| IsGameOver()
		|| !IsCaptureTableReferenceSnapshotCurrent(
			turnSequenceGeneration,
			expectedTableCardReferences))
	{
		return CaptureTableCardsResult.AbortedBeforeApply;
	}

		AdventureCapturePreparation capturePreparation =
			BuildAdventureCapturePreparation(
				playerIndex,
				captureStartIndex,
				scorePlayerIndex,
				capturePlan,
				prePlayTableCards,
				adventureFlatDamageDelta,
				baseCardActionContext,
				actionSource,
				matchSource,
				triggeredAttachedItemCardIds,
				clubGapRootingTargetTableCard,
				preserveTailTableCardIndex);
		adventureCaptureContext = capturePreparation.Context;
		foreach (AdventureCapturedCardEntry capturedEntry in
			adventureCaptureContext.CapturedCards)
		{
			capturedTraceMark |= ReferenceEquals(
				capturedEntry.TableCard,
				_adventureTraceHuntMarkedTableCard);
			capturedHuntMark |= ReferenceEquals(
				capturedEntry.TableCard,
				_adventureHuntOrderMarkedTableCard);
			if (!questionRewardStartGlobalPosition.HasValue
				&& IsQuestionTableCard(capturedEntry.TableCard))
			{
				questionRewardStartGlobalPosition =
					centerArenaGlobalPosition
					+ GetTableCardPosition(
						capturedEntry.PreCaptureTableIndex,
						tableLayoutRect);
			}
		}

			if (questionRewardStartGlobalPosition.HasValue)
			{
				capturedQuestionRewardPlan = await PlanQuestionCardRewardAsync(
				playerIndex,
				reserveReplacementCard,
					turnSequenceGeneration);
			}
			int previouslyPlannedRewardHandCardDelta =
				capturedQuestionRewardPlan is QuestionCardRewardPlan capturedPlan
					&& !capturedPlan.RequiresMagnetAttachment
					&& capturedPlan.DiscardDisplayIndex < 0
						? 1
						: 0;
			HashSet<int>? excludedRewardDiscardIndexes =
				capturedQuestionRewardPlan is QuestionCardRewardPlan capturedDiscardPlan
					&& capturedDiscardPlan.DiscardDisplayIndex >= 0
						? new HashSet<int> { capturedDiscardPlan.DiscardDisplayIndex }
						: null;
			AdventureUpgradeRunState? rewardRunState = GetAdventureUpgradeRunState();
		if (playerIndex == MainPlayerIndex
			&& rewardRunState?.IsActive == true
			&& AdventureUpgradeEffectService.ShouldGrantLinkedStemsQuestionReward(
				rewardRunState.GetAcquiredUpgradeIds(),
				_adventureMainPlayerSuccessfulCapturesThisTurn + 1))
		{
				linkedStemsRewardPlan = await PlanQuestionCardRewardAsync(
					playerIndex,
					reserveReplacementCard: false,
					turnSequenceGeneration,
					previouslyPlannedHandCardDelta: previouslyPlannedRewardHandCardDelta,
					excludedDiscardDisplayIndexes: excludedRewardDiscardIndexes,
					rejudgeOpportunityAlreadyPlanned:
						capturedQuestionRewardPlan?.ConsumeRejudgeOpportunity == true);
		}
		if (_isLeavingGame
			|| IsGameOver()
			|| !IsCaptureTableReferenceSnapshotCurrent(
				turnSequenceGeneration,
				expectedTableCardReferences))
		{
			return CaptureTableCardsResult.AbortedBeforeApply;
		}

			if (ShouldUseAdventureUpgradeSystem())
			{
				AdventureRunSessionStore? store = GetAdventureRunSessionStore();
				var candidateTableCards = new List<TableCard>(_tableCards);
				if (prePlayTableCards != null)
				{
					_tableCards.Clear();
					_tableCards.AddRange(prePlayTableCards);
					RenderTableCards();
				}
				AdventureClubCaptureCommitInput clubCaptureInput =
				BuildAdventureClubCaptureCommitInput(playerIndex, adventureCaptureContext);
			int plannedQuestionRewardCount = playerIndex == MainPlayerIndex
				? (capturedQuestionRewardPlan.HasValue ? 1 : 0)
					+ (linkedStemsRewardPlan.HasValue ? 1 : 0)
				: 0;
			AdventureSaveGateResult<AdventureCaptureCommitResult> gate =
				await ExecuteAdventureSaveGateAsync(
					() => store?.ResolveCaptureAction(new AdventureCaptureCommitInput(
						CapturerIsMainPlayer: playerIndex == MainPlayerIndex,
						PlayedCardRank: adventureCaptureContext.Action.PlayedCard.Rank,
						ConsumeBorrowSuitSet: adventureCaptureContext.Action.PlayedCardInstanceId > 0
							&& ShouldAdventureBorrowSuitSetAffectPlayedCard(
								adventureCaptureContext.Action.PlayedCard),
						ConsumeGapRooting: clubGapRootingTargetTableCard != null,
						capturePreparation.DamageInput,
						SuccessfulCapturesThisTurn: _adventureMainPlayerSuccessfulCapturesThisTurn + 1,
						adventureCaptureContext,
						clubCaptureInput,
						plannedQuestionRewardCount,
						_adventureMainPlayerHalfHealthRescueUsed,
						_adventureMainPlayerQuarterHealthRescueUsed))
						?? new AdventureCaptureCommitResult(
							AdventureRunWriteResult.SaveFailed,
							default,
							default,
							default,
							default,
							null,
							default,
							0,
							false,
							false),
					result => result.WriteResult == AdventureRunWriteResult.Succeeded,
					result => result.WriteResult == AdventureRunWriteResult.SaveFailed);
				if (!gate.Succeeded)
				{
					GD.PushWarning($"Failed to commit adventure capture action: {gate.Result.WriteResult}");
					return CaptureTableCardsResult.AbortedBeforeApply;
				}
				_tableCards.Clear();
				_tableCards.AddRange(candidateTableCards);

				AdventureCaptureCommitResult commitResult = gate.Result;
			resolvedCaptureDamage = commitResult.DamageResult;
			committedClubCaptureEffects = commitResult.ClubCaptureEffectResult;
			adventurePostCaptureEffects = commitResult.PostCaptureEffectResult with { HealAmount = 0 };
			if (plannedQuestionRewardCount > 0)
			{
				adventureQuestionRewardEffects = commitResult.QuestionRewardEffectResult with { HealAmount = 0 };
			}

			int damageTargetPlayerIndex = GetAdventureDamageTargetPlayerIndex(scorePlayerIndex);
			if (damageTargetPlayerIndex == MainPlayerIndex)
			{
				ApplyCommittedAdventurePlayerVitals(
					scorePlayerIndex,
					resolvedCaptureDamage.FinalDamage,
					commitResult.CurrentHealth,
					commitResult.RescueResult,
					commitResult.ConsumedHalfHealthRescue,
					commitResult.ConsumedQuarterHealthRescue);
			}
			else
			{
				ApplyAdventureDamageInMemory(
					scorePlayerIndex,
					damageTargetPlayerIndex,
					resolvedCaptureDamage.FinalDamage);
				if (commitResult.HealingResult.HasValue)
				{
					ApplyCommittedAdventurePlayerVitals(
						MainPlayerIndex,
						rawDamage: 0,
						commitResult.CurrentHealth,
						default,
						false,
						false);
				}
			}
		}
		else
		{
			resolvedCaptureDamage = PreviewAdventureCaptureDamage(
				playerIndex,
				capturePreparation.DamageInput);
			if (resolvedCaptureDamage.FinalDamage > 0)
			{
				AddPlayerScore(scorePlayerIndex, resolvedCaptureDamage.FinalDamage);
			}
		}
		await WaitForAuthorityTransactionFaultAsync("after_score");
		commitImmediatelyBeforeApply?.Invoke();
	CaptureStateResult captureStateResult =
		_captureStateService.ApplyCapture(
			_state,
			capturePlan,
			playerIndex,
			preserveCardEffectsWhenEnteringDeck,
			preservedTableCardIndexes,
			blackLedgerRedirectTableCardIndex,
			MainPlayerIndex);
		await WaitForAuthorityTransactionFaultAsync("after_capture");
		collectedCardCount =
			captureStateResult.CollectedCardCount;
		removedTableCardCount =
			captureStateResult.RemovedTableCardCount;
		if (clubGapRootingTargetTableCard != null
			&& _tableCards.Contains(clubGapRootingTargetTableCard)
			&& clubGapRootingTargetTableCard.Card.HasValue)
		{
			_tableCards.Remove(clubGapRootingTargetTableCard);
			AddCardToPlayerDeck(
				playerIndex,
				clubGapRootingTargetTableCard.Card.Value);
			collectedCardCount++;
			removedTableCardCount++;
		}
		await WaitForDebugCaptureAfterApplyBarrierAsync();
	if (turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame)
	{
		return BuildAppliedResult(aborted: true);
	}

	if (IsGameOver())
	{
		return BuildAppliedResult(aborted: false);
	}

	var postApplyTableCardReferences =
		new List<TableCard>(_tableCards);
		if (capturedQuestionRewardPlan.HasValue
			&& questionRewardStartGlobalPosition.HasValue)
		{
			QuestionCardRewardGrant? rewardGrant = ApplyQuestionCardRewardPlan(
				playerIndex,
				capturedQuestionRewardPlan.Value);
			if (rewardGrant.HasValue)
			{
				questionRewardAnimation =
				new QuestionCardRewardAnimation(
					rewardGrant.Value.ItemCard,
					rewardGrant.Value.ItemHandIndex,
					questionRewardStartGlobalPosition.Value,
						rewardGrant.Value
							.RequiresMagnetAttachment,
						rewardGrant.Value.RejudgeResolvedForReward);
			}
		}
			if (linkedStemsRewardPlan.HasValue)
			{
				QuestionCardRewardPlan linkedPlan = linkedStemsRewardPlan.Value;
				if (capturedQuestionRewardPlan?.DiscardDisplayIndex >= 0
					&& linkedPlan.DiscardDisplayIndex
						> capturedQuestionRewardPlan.Value.DiscardDisplayIndex)
				{
					linkedPlan = linkedPlan with
					{
						DiscardDisplayIndex = linkedPlan.DiscardDisplayIndex - 1,
					};
				}
				_ = ApplyQuestionCardRewardPlan(playerIndex, linkedPlan);
			}

		await ApplyAdventureClubCaptureReturnsAsync(
				playerIndex,
				adventureCaptureContext,
				committedClubCaptureEffects,
				turnSequenceGeneration);
	if (turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame)
	{
		return BuildAppliedResult(aborted: true);
	}

	if (IsGameOver())
	{
		return BuildAppliedResult(aborted: false);
	}

	if (!IsCaptureTableReferenceSnapshotCurrent(
		turnSequenceGeneration,
		postApplyTableCardReferences))
	{
		return BuildAppliedResult(aborted: true);
	}

		RefreshGardenEelBuriedCardIndex();
	if (collectedCardCount > 0)
	{
		ApplyAdventureEnemyCaptureEffects(playerIndex);
	}

	if (captureFlow.ShouldTrackQuestionCardRefresh)
	{
		QuestionCardRefreshState questionCardRefreshState =
			_turnAdvancementService
				.StartQuestionCardRefreshTracking(
					playerIndex);
		_completedTurnsSinceQuestionCardRefresh =
			questionCardRefreshState
				.CompletedTurnsSinceQuestionCardRefresh;
		_questionCardRefreshAnchorPlayerIndex =
			questionCardRefreshState
				.QuestionCardRefreshAnchorPlayerIndex;
	}

	if (capturedTraceMark)
	{
		SetAdventureTraceHuntMarkedTableCard(
			null,
			playIntro: false);
	}

	if (playerIndex == MainPlayerIndex
			&& collectedCardCount > 0)
	{
		if (capturedHuntMark)
		{
			_adventureHuntOrderMarkedTableCard = null;
		}

		if (prePlayTableCards != null)
		{
			RefreshAdventureTraceHuntMarkAfterMainPlayerCapture(
				prePlayTableCards);
		}

		RefreshAdventureCanopyAnchorAfterTableMutation();
			_adventureMainPlayerSuccessfulCapturesThisTurn++;
			_adventureMainPlayerCaptureCountThisBattle++;
			RenderPlayerHands();
	}

	bool shouldQueueBigCaptureUpgrade =
			captureFlow.CapturedScoringCards
			&& ShouldQueueAdventureBigCaptureUpgrade(
				playerIndex,
				adventureCaptureContext.CapturedCards.Count);
	await CheckForWinnerAsync(
		turnSequenceGeneration,
		shouldQueueBigCaptureUpgrade);
	if (IsGameOver())
	{
		return BuildAppliedResult(aborted: false);
	}
	if (turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame)
	{
		return BuildAppliedResult(aborted: true);
	}

	if (removedTableCardCount > 0)
	{
		if (captureFlow.CapturedScoringCards
			&& !suppressGardenEelCaptureRecord)
		{
			RecordGardenEelCaptureForSandBite(
				playerIndex);
		}

		if (captureFlow.CapturedScoringCards)
		{
			await ApplyJellyfishBubbleAfterCaptureAsync(
				playerIndex,
				turnSequenceGeneration);
			if (turnSequenceGeneration
					!= _turnSequenceGeneration
				|| _isLeavingGame)
			{
				return BuildAppliedResult(aborted: true);
			}

			if (IsGameOver())
			{
				return BuildAppliedResult(aborted: false);
			}
		}

		await ApplyNautilusBombAfterMainPlayerCaptureAsync(
			playerIndex,
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration
			|| _isLeavingGame)
		{
			return BuildAppliedResult(aborted: true);
		}

		if (IsGameOver())
		{
			return BuildAppliedResult(aborted: false);
		}

		if (captureFlow.CapturedScoringCards)
		{
			RecordNarwhalCaptureForDouble(playerIndex);
		}

		await ApplyDragonQueenAndKingAfterEnemyCaptureAsync(
			playerIndex,
			removedTableCardCount,
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration
			|| _isLeavingGame)
		{
			return BuildAppliedResult(aborted: true);
		}

		if (IsGameOver())
		{
			return BuildAppliedResult(aborted: false);
		}
	}

	return BuildAppliedResult(aborted: false);
}
	private bool IsCaptureTableReferenceSnapshotCurrent(
	int turnSequenceGeneration,
	IReadOnlyList<TableCard> tableCardReferenceSnapshot)
{
	if (turnSequenceGeneration != _turnSequenceGeneration
		|| tableCardReferenceSnapshot.Count != _tableCards.Count)
	{
		return false;
	}

	for (int tableCardIndex = 0;
		tableCardIndex < tableCardReferenceSnapshot.Count;
		tableCardIndex++)
	{
		if (!ReferenceEquals(
			tableCardReferenceSnapshot[tableCardIndex],
			_tableCards[tableCardIndex]))
		{
			return false;
		}
	}

	return true;
}

}
