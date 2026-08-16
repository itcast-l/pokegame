using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
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
		IReadOnlyCollection<string>? triggeredAttachedItemCardIds = null,
		MatchStateSnapshot? adventureSaveRollbackSnapshot = null,
		long basePlayCardRuntimeInstanceId = 0)
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

	CaptureStatePlan capturePlan = CreateAuthorityCapturePlan(
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

	CaptureScoringTableCard[] captureScoringTableCards =
		ToCaptureScoringTableCards();
			int scorePlayerIndex = MatchReadModel.GetCapturedScorePlayerIndex(
					captureScoringTableCards,
					captureStartIndex,
					playerIndex,
					player_count);
		CaptureFlowPlan captureFlow = MatchReadModel.BuildCaptureFlow(
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
		if (questionRewardAnimation.HasValue
			&& !questionRewardAnimation.Value.RequiresMagnetAttachment
			&& (aborted
				|| _isLeavingGame
				|| IsGameOver()
				|| turnSequenceGeneration != _turnSequenceGeneration))
		{
			EndQuestionRewardHandDelivery(
				playerIndex,
				questionRewardAnimation.Value.ItemHandIndex);
			RenderPlayerHands();
		}

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
				GetAdventureTraceHuntMarkedTableCard());
			capturedHuntMark |= ReferenceEquals(
				capturedEntry.TableCard,
				GetAdventureHuntOrderMarkedTableCard());
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
			AdventureRunReadModel? rewardRunState = GetAdventureUpgradeRunState();
		if (playerIndex == MainPlayerIndex
			&& rewardRunState?.IsActive == true
			&& rewardRunState.ShouldGrantLinkedStemsQuestionReward(
				CurrentMatchFlowState.SuccessfulCapturesThisTurn + 1))
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

		var scoreMutations = ImmutableArray.CreateBuilder<RuleScoreMutation>(2);
		var scorePresentations = new List<CaptureScorePresentation>(2);
		if (ShouldUseAdventureUpgradeSystem())
		{
			MatchStateSnapshot? adventureSaveCommitSnapshot = null;
			bool adventureSaveSnapshotRolledBack = false;
			if (adventureSaveRollbackSnapshot != null)
			{
				if (!TryCaptureAuthorityRuleSnapshot(
					out MatchStateSnapshot commitSnapshot,
					out string commitSnapshotError))
				{
					GD.PushError(
						"Failed to capture the prepared adventure save commit snapshot: "
						+ commitSnapshotError);
					return CaptureTableCardsResult.AbortedBeforeApply;
				}

				adventureSaveCommitSnapshot = commitSnapshot;
			}
			AdventureRunSessionStore? store = GetAdventureRunSessionStore();
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
						SuccessfulCapturesThisTurn: CurrentMatchFlowState.SuccessfulCapturesThisTurn + 1,
						adventureCaptureContext.ToRuleFacts(),
						clubCaptureInput,
						plannedQuestionRewardCount,
						CurrentMatchFlowState.HalfHealthRescueUsed,
						CurrentMatchFlowState.QuarterHealthRescueUsed))
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
					result => result.WriteResult == AdventureRunWriteResult.SaveFailed,
					onRetryableFailure: adventureSaveRollbackSnapshot == null
						? null
						: () =>
						{
							adventureSaveSnapshotRolledBack =
								RestoreAdventureSaveGateRuleSnapshot(
									adventureSaveRollbackSnapshot,
									"pre-play rollback");
							return adventureSaveSnapshotRolledBack;
						},
					onSucceeded: adventureSaveCommitSnapshot == null
						? null
						: () => !adventureSaveSnapshotRolledBack
							|| RestoreAdventureSaveGateRuleSnapshot(
								adventureSaveCommitSnapshot,
								"prepared capture commit"));
				if (!gate.Succeeded)
				{
					GD.PushWarning($"Failed to commit adventure capture action: {gate.Result.WriteResult}");
					return CaptureTableCardsResult.AbortedBeforeApply;
				}
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
					int previousHealth = _playerScores[MainPlayerIndex];
					scoreMutations.Add(new RuleScoreMutation(
						MainPlayerIndex,
						RuleScoreMutationKind.Set,
						commitResult.CurrentHealth));
					scorePresentations.Add(CaptureScorePresentation.CommittedVitals(
						scorePlayerIndex,
						previousHealth,
						resolvedCaptureDamage.FinalDamage,
						commitResult.RescueResult,
						commitResult.ConsumedHalfHealthRescue,
						commitResult.ConsumedQuarterHealthRescue));
				}
				else
				{
					if (resolvedCaptureDamage.FinalDamage > 0)
					{
						int previousHealth = _playerScores[damageTargetPlayerIndex];
						scoreMutations.Add(new RuleScoreMutation(
							damageTargetPlayerIndex,
							RuleScoreMutationKind.Subtract,
							resolvedCaptureDamage.FinalDamage));
						scorePresentations.Add(CaptureScorePresentation.AdventureDamage(
							scorePlayerIndex,
							damageTargetPlayerIndex,
							previousHealth));
					}
					if (commitResult.HealingResult.HasValue)
					{
						int previousHealth = _playerScores[MainPlayerIndex];
						scoreMutations.Add(new RuleScoreMutation(
							MainPlayerIndex,
							RuleScoreMutationKind.Set,
							commitResult.CurrentHealth));
						scorePresentations.Add(CaptureScorePresentation.CommittedVitals(
							MainPlayerIndex,
							previousHealth,
							rawDamage: 0,
							default,
							false,
							false));
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
					if (adventure_mode)
					{
						int damageTargetPlayerIndex = GetAdventureDamageTargetPlayerIndex(scorePlayerIndex);
						int previousHealth = _playerScores[damageTargetPlayerIndex];
						scoreMutations.Add(new RuleScoreMutation(
							damageTargetPlayerIndex,
							RuleScoreMutationKind.Subtract,
							resolvedCaptureDamage.FinalDamage));
						scorePresentations.Add(CaptureScorePresentation.AdventureDamage(
							scorePlayerIndex,
							damageTargetPlayerIndex,
							previousHealth));
					}
					else
					{
						scoreMutations.Add(new RuleScoreMutation(
							scorePlayerIndex,
							RuleScoreMutationKind.Add,
							resolvedCaptureDamage.FinalDamage));
						scorePresentations.Add(CaptureScorePresentation.NormalScore(scorePlayerIndex));
					}
				}
			}
			IReadOnlyCollection<int>? captureMutationExtraIndexes = extraTableCardIndexes;
			if (clubGapRootingTargetTableCard != null)
			{
				int gapRootingTargetIndex = _tableCards.IndexOf(clubGapRootingTargetTableCard);
				if (gapRootingTargetIndex >= 0)
				{
					var combinedIndexes = extraTableCardIndexes == null
						? new HashSet<int>()
						: new HashSet<int>(extraTableCardIndexes);
					combinedIndexes.Add(gapRootingTargetIndex);
					captureMutationExtraIndexes = combinedIndexes;
				}
			}
			commitImmediatelyBeforeApply?.Invoke();
			// Capturing other cards must not reset the lifetime of a question card left behind.
			bool shouldRestartQuestionRefreshTracking =
				captureFlow.ShouldTrackQuestionCardRefresh
				&& (!HasTableQuestionCard() || questionRewardStartGlobalPosition.HasValue);
		if (!TrySubmitCaptureCardsRuleCommand(
			playerIndex,
			captureStartIndex,
			turnSequenceGeneration,
			captureMutationExtraIndexes,
			preserveCardEffectsWhenEnteringDeck,
			preservedTableCardIndexes,
			blackLedgerRedirectTableCardIndex,
			MainPlayerIndex,
				scoreMutations.ToImmutable(),
				shouldRestartQuestionRefreshTracking,
				basePlayCardRuntimeInstanceId,
				out RuleCommandResult captureCommandResult))
		{
			return CaptureTableCardsResult.AbortedBeforeApply;
		}
			foreach (CaptureScorePresentation presentation in scorePresentations)
			{
				PresentCaptureScoreChange(presentation);
			}
			await WaitForAuthorityTransactionFaultAsync("after_score");
			await WaitForAuthorityTransactionFaultAsync("after_capture");
			collectedCardCount =
				captureCommandResult.Mutation.CollectedCardCount;
			removedTableCardCount =
				captureCommandResult.Mutation.RemovedTableCardCount;
		if (captureCommandResult.Mutation.BigTideTriggered)
		{
			PublishCaptureThresholdFeedback(
				playerIndex,
				bigTide: true,
				captureCommandResult.Mutation.FinalTableCaptureCount,
				turnSequenceGeneration);
		}
		else
		{
			MarkActivePlayerStatusTooltipForRefresh();
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
						rewardGrant.Value.RequiresMagnetAttachment,
						rewardGrant.Value.RejudgeResolvedForReward);
				if (!rewardGrant.Value.RequiresMagnetAttachment)
				{
					BeginQuestionRewardHandDelivery(
						playerIndex,
						rewardGrant.Value.ItemHandIndex);
				}
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

	if (collectedCardCount > 0)
	{
		ApplyAdventureEnemyCaptureEffects(playerIndex);
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
			TryUpdateMatchFlowState(state => state with
			{
				TurnCaptureMarkRuntimeId = 0,
			});
		}

		if (prePlayTableCards != null)
		{
			RefreshAdventureTraceHuntMarkAfterMainPlayerCapture(
				prePlayTableCards);
		}

		RefreshAdventureCanopyAnchorAfterTableMutation();
			TryUpdateMatchFlowState(state => state with
			{
				SuccessfulCapturesThisTurn = state.SuccessfulCapturesThisTurn == int.MaxValue
					? int.MaxValue
					: state.SuccessfulCapturesThisTurn + 1,
				MainPlayerCapturesThisBattle = state.MainPlayerCapturesThisBattle == int.MaxValue
					? int.MaxValue
					: state.MainPlayerCapturesThisBattle + 1,
			});
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

	private void PresentCaptureScoreChange(CaptureScorePresentation presentation)
	{
		switch (presentation.Kind)
		{
			case CaptureScorePresentationKind.NormalScore:
				UpdatePlayerScoreMeter(presentation.SourcePlayerIndex, true);
				PlayPlayerAvatarAction(
					presentation.SourcePlayerIndex,
					CharacterAvatar.AvatarAction.Score);
				RefreshGameplayTensionMusic();
				break;
			case CaptureScorePresentationKind.AdventureDamage:
				PresentAdventureDamageInMemory(
					presentation.SourcePlayerIndex,
					presentation.TargetPlayerIndex,
					presentation.PreviousValue);
				break;
			case CaptureScorePresentationKind.CommittedVitals:
				PresentCommittedAdventurePlayerVitals(
					presentation.SourcePlayerIndex,
					presentation.RawDamage,
					presentation.PreviousValue,
					presentation.RescueResult,
					presentation.ConsumedHalfHealthRescue,
					presentation.ConsumedQuarterHealthRescue);
				break;
		}
	}

	private enum CaptureScorePresentationKind
	{
		NormalScore,
		AdventureDamage,
		CommittedVitals,
	}

	private readonly record struct CaptureScorePresentation(
		CaptureScorePresentationKind Kind,
		int SourcePlayerIndex,
		int TargetPlayerIndex,
		int PreviousValue,
		int RawDamage,
		AdventureLowHealthRescueResult RescueResult,
		bool ConsumedHalfHealthRescue,
		bool ConsumedQuarterHealthRescue)
	{
		internal static CaptureScorePresentation NormalScore(int sourcePlayerIndex)
		{
			return new CaptureScorePresentation(
				CaptureScorePresentationKind.NormalScore,
				sourcePlayerIndex,
				-1,
				0,
				0,
				default,
				false,
				false);
		}

		internal static CaptureScorePresentation AdventureDamage(
			int sourcePlayerIndex,
			int targetPlayerIndex,
			int previousValue)
		{
			return new CaptureScorePresentation(
				CaptureScorePresentationKind.AdventureDamage,
				sourcePlayerIndex,
				targetPlayerIndex,
				previousValue,
				0,
				default,
				false,
				false);
		}

		internal static CaptureScorePresentation CommittedVitals(
			int sourcePlayerIndex,
			int previousValue,
			int rawDamage,
			AdventureLowHealthRescueResult rescueResult,
			bool consumedHalfHealthRescue,
			bool consumedQuarterHealthRescue)
		{
			return new CaptureScorePresentation(
				CaptureScorePresentationKind.CommittedVitals,
				sourcePlayerIndex,
				MainPlayerIndex,
				previousValue,
				rawDamage,
				rescueResult,
				consumedHalfHealthRescue,
				consumedQuarterHealthRescue);
		}
	}

	}
