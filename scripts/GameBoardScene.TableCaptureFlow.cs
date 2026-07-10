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
		bool awardScore = true,
		int turnSequenceGeneration = 0,
		HashSet<int>? extraTableCardIndexes = null,
		int adventureFlatDamageDelta = 0,
		IReadOnlyCollection<TableCard>? prePlayTableCards = null,
		bool playedCardUsedAttachedEffect = false,
		PlayingCard? playedCardForAdventureReturns = null,
		int preserveTailTableCardIndex = -1,
		bool suppressGardenEelCaptureRecord = false)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return new CaptureTableCardsResult(0, 0, null);
		}

		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return new CaptureTableCardsResult(0, 0, null);
		}

		CaptureStatePlan capturePlan = _captureStateService.CreatePlan(_state, captureStartIndex, extraTableCardIndexes);
		if (!capturePlan.IsValid)
		{
			return new CaptureTableCardsResult(0, 0, null);
		}

		CaptureScoringRules.TableCard[] captureScoringTableCards = ToCaptureScoringTableCards();
		int scorePlayerIndex = awardScore
			? ScoreService.GetCapturedScorePlayerIndex(captureScoringTableCards, captureStartIndex, playerIndex, player_count)
			: playerIndex;
		CaptureFlowPlan captureFlow = CaptureFlowPlanner.Build(
			captureScoringTableCards,
			captureStartIndex,
			scorePlayerIndex,
			BuildCaptureScoringContext(scorePlayerIndex),
			awardScore,
			extraTableCardIndexes);
		int collectedCardCount = 0;
		int removedTableCardCount = 0;
		QuestionCardRewardAnimation? questionRewardAnimation = null;
		AdventureQuestionRewardEffectResult? adventureQuestionRewardEffects = null;
		AdventurePostCaptureEffectResult? adventurePostCaptureEffects = null;
		var adventureCaptureRawPoints = new List<int>();
		var adventureCaptureResolvedPoints = new List<int>();
		var capturedBaseCards = new List<PlayingCard>();
		bool capturedTraceMark = false;
		bool capturedHuntMark = false;
		TableCard? clubGapRootingTargetTableCard = null;
		Rect2 tableLayoutRect = GetTableCardLayoutRect();
		if (awardScore
			&& captureFlow.Capture.CapturedMiddleCards
			&& ShouldPlayCrabScoreReductionAbility(scorePlayerIndex, captureStartIndex, extraTableCardIndexes))
		{
			await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(1, turnSequenceGeneration, AdventureEnemyAbilities.TideGuard);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return new CaptureTableCardsResult(0, 0, null, adventureQuestionRewardEffects, adventurePostCaptureEffects);
			}
		}

		foreach (int tableCardIndex in capturePlan.BaseTableCardIndexes)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.Card.HasValue && !tableCard.RemoveCardOnCapture)
			{
				capturedBaseCards.Add(tableCard.Card.Value);
			}
			if (captureFlow.Capture.CapturedMiddleCards && IsQuestionTableCard(tableCard))
			{
				QuestionCardRewardGrant? rewardGrant = await GrantQuestionCardRewardAsync(playerIndex, reserveReplacementCard, turnSequenceGeneration);
				if (rewardGrant.HasValue)
				{
					adventureQuestionRewardEffects = ResolveAdventureQuestionRewardEffects(playerIndex);
					questionRewardAnimation = new QuestionCardRewardAnimation(
						rewardGrant.Value.ItemCard,
						rewardGrant.Value.ItemHandIndex,
						_centerArena.GlobalPosition + GetTableCardPosition(tableCardIndex, tableLayoutRect),
						rewardGrant.Value.RequiresMagnetAttachment);
				}
			}
		}
		capturedTraceMark = IsCapturedTableCardReference(_adventureTraceHuntMarkedTableCard, captureStartIndex, extraTableCardIndexes);
		capturedHuntMark = IsCapturedTableCardReference(_adventureHuntOrderMarkedTableCard, captureStartIndex, extraTableCardIndexes);
		if (playerIndex == MainPlayerIndex)
		{
			bool hasMiddleScoringCards = false;
			for (int tableCardIndex = captureStartIndex + 1; tableCardIndex < _tableCards.Count - 1; tableCardIndex++)
			{
				TableCard middleTableCard = _tableCards[tableCardIndex];
				if (!middleTableCard.IsBuriedInSand
					&& middleTableCard.Card.HasValue
					&& GetCardScoreValue(middleTableCard.Card.Value) > 0)
				{
					hasMiddleScoringCards = true;
					break;
				}
			}

			AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
			bool leftAdjacentBaseCardAvailable = captureStartIndex > 0
				&& !_tableCards[captureStartIndex - 1].IsBuriedInSand
				&& _tableCards[captureStartIndex - 1].Card.HasValue;
			if (runState?.IsActive == true)
			{
				AdventureClubCaptureExtensionResult clubCaptureExtensionResult = AdventureUpgradeEffectService.ResolveClubCaptureExtensions(
					runState.EffectState,
					runState.GetAcquiredUpgradeIds(),
					hasMiddleScoringCards,
					leftAdjacentBaseCardAvailable);
				if (clubCaptureExtensionResult.CaptureLeftAdjacentBaseCard)
				{
					clubGapRootingTargetTableCard = _tableCards[captureStartIndex - 1];
					GetAdventureRunSessionStore()?.ReplaceEffectState(runState.EffectState.Clone());
				}
			}
		}

		HashSet<int>? preservedTableCardIndexes = preserveTailTableCardIndex >= 0
			? new HashSet<int> { preserveTailTableCardIndex }
			: null;
		CaptureStateResult captureStateResult = _captureStateService.ApplyCapture(
			_state,
			capturePlan,
			playerIndex,
			ShouldPreserveCardEffectsWhenEnteringDeck(),
			preservedTableCardIndexes);
		await ApplyAdventureClubCaptureReturnsAsync(
			playerIndex,
			capturedBaseCards,
			playedCardForAdventureReturns,
			playedCardUsedAttachedEffect,
			turnSequenceGeneration);
		collectedCardCount = captureStateResult.CollectedCardCount;
		removedTableCardCount = captureStateResult.RemovedTableCardCount;
		if (clubGapRootingTargetTableCard != null
			&& _tableCards.Contains(clubGapRootingTargetTableCard)
			&& clubGapRootingTargetTableCard.Card.HasValue)
		{
			_tableCards.Remove(clubGapRootingTargetTableCard);
			AddCardToPlayerDeck(playerIndex, clubGapRootingTargetTableCard.Card.Value);
			collectedCardCount++;
			removedTableCardCount++;
		}
		RefreshGardenEelBuriedCardIndex();
		if (collectedCardCount > 0)
		{
			ApplyAdventureEnemyCaptureEffects(playerIndex);
		}

		if (captureFlow.ShouldTrackQuestionCardRefresh)
		{
			QuestionCardRefreshState questionCardRefreshState = _turnAdvancementService.StartQuestionCardRefreshTracking(playerIndex);
			_completedTurnsSinceQuestionCardRefresh = questionCardRefreshState.CompletedTurnsSinceQuestionCardRefresh;
			_questionCardRefreshAnchorPlayerIndex = questionCardRefreshState.QuestionCardRefreshAnchorPlayerIndex;
		}

		if (awardScore)
		{
			BuildAdventureCaptureDamageCardPointLists(
				captureScoringTableCards,
				captureStartIndex,
				scorePlayerIndex,
				extraTableCardIndexes,
				adventureCaptureRawPoints,
				adventureCaptureResolvedPoints);
			AdventureClubCaptureScoringResult clubCaptureScoring = AdventureUpgradeEffectService.ResolveClubCaptureScoring(
				GetAdventureUpgradeRunState()?.GetAcquiredUpgradeIds() ?? Array.Empty<string>());
			if (playerIndex == MainPlayerIndex
				&& clubCaptureScoring.IncludeCaptureEndpointsInScoring
				&& captureStartIndex >= 0
				&& captureStartIndex < _tableCards.Count)
			{
				TableCard captureStartTableCard = _tableCards[captureStartIndex];
				if (!captureStartTableCard.IsBuriedInSand && captureStartTableCard.Card.HasValue)
				{
					int rawPoint = Math.Max(0, GetCardScoreValue(captureStartTableCard.Card.Value));
					if (rawPoint > 0)
					{
						adventureCaptureRawPoints.Add(rawPoint);
						adventureCaptureResolvedPoints.Add(GetCapturedTableCardScoreValueForPlayer(
							captureStartTableCard,
							scorePlayerIndex,
							captureFlow.Score.HasDoubleEffect));
					}
				}

				int captureTailTableCardIndex = GetCaptureTailTableCardIndex(captureStartIndex);
				if (captureTailTableCardIndex >= 0
					&& captureTailTableCardIndex != captureStartIndex
					&& captureTailTableCardIndex < _tableCards.Count)
				{
					TableCard captureTailTableCard = _tableCards[captureTailTableCardIndex];
					if (!captureTailTableCard.IsBuriedInSand && captureTailTableCard.Card.HasValue)
					{
						int rawPoint = Math.Max(0, GetCardScoreValue(captureTailTableCard.Card.Value));
						if (rawPoint > 0)
						{
							adventureCaptureRawPoints.Add(rawPoint);
							adventureCaptureResolvedPoints.Add(GetCapturedTableCardScoreValueForPlayer(
								captureTailTableCard,
								scorePlayerIndex,
								captureFlow.Score.HasDoubleEffect));
						}
					}
				}
			}
			int adventureOldTableDamageDelta = adventureFlatDamageDelta;
			if (prePlayTableCards != null && prePlayTableCards.Count > 0)
			{
				GetCapturedOldTableResolvedPointTotals(
					prePlayTableCards,
					captureStartIndex,
					scorePlayerIndex,
					extraTableCardIndexes,
					out int totalResolvedOldTablePoints,
					out int currentResolvedOldTableScoringPoints);
				adventureOldTableDamageDelta += AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
					GetAdventureUpgradeRunState()?.GetAcquiredUpgradeIds() ?? Array.Empty<string>(),
					playerIndex == MainPlayerIndex,
					CountCapturedOldScoringTableCards(prePlayTableCards, captureStartIndex, extraTableCardIndexes),
					IsCapturedTableCardReference(_adventureTraceHuntMarkedTableCard, captureStartIndex, extraTableCardIndexes),
					IsCapturedTableCardReference(_adventureHuntOrderMarkedTableCard, captureStartIndex, extraTableCardIndexes));
				adventureOldTableDamageDelta += AdventureUpgradeEffectService.ResolveBlackLedgerDamageDelta(
					AdventureUpgradeEffectService.HasBlackLedger(
						GetAdventureUpgradeRunState()?.GetAcquiredUpgradeIds() ?? Array.Empty<string>()),
					playerIndex == MainPlayerIndex,
					totalResolvedOldTablePoints,
					currentResolvedOldTableScoringPoints);
			}
			bool isFirstCaptureThisBattle = playerIndex == MainPlayerIndex
				&& _adventureMainPlayerCaptureCountThisBattle == 0;
			int resolvedDamage = ResolveAdventureCaptureDamage(
				playerIndex,
				new AdventureCaptureDamageInput(
					BaseDamage: captureFlow.Score.Points,
					FlatDamageDelta: adventureOldTableDamageDelta,
					IsFirstCaptureThisBattle: isFirstCaptureThisBattle,
					IncludesQuestionReward: questionRewardAnimation.HasValue,
					QuestionRewardEchoActive: _adventureMainPlayerQuestionRewardEchoActive,
					ScoringCardRawPoints: adventureCaptureRawPoints,
					ScoringCardResolvedPoints: adventureCaptureResolvedPoints));
			AddPlayerScore(scorePlayerIndex, resolvedDamage);
			if (playerIndex == MainPlayerIndex)
			{
				adventurePostCaptureEffects = ResolveAdventurePostCaptureEffects(
					playerIndex,
					resolvedDamage,
					_adventureMainPlayerSuccessfulCapturesThisTurn + 1,
					attachedEffectTriggered: playedCardUsedAttachedEffect,
					capturedOnlyOuterVisibleCards: prePlayTableCards != null
						&& CapturedOnlyOuterVisibleCards(prePlayTableCards, captureStartIndex, extraTableCardIndexes));
			}
		}
		if (playerIndex == MainPlayerIndex && captureStateResult.CollectedCardCount > 0)
		{
			if (capturedTraceMark)
			{
				_adventureTraceHuntMarkedTableCard = null;
			}

			if (capturedHuntMark)
			{
				_adventureHuntOrderMarkedTableCard = null;
			}

			if (prePlayTableCards != null)
			{
				RefreshAdventureTraceHuntMarkAfterMainPlayerCapture(prePlayTableCards);
			}
			RefreshAdventureCanopyAnchorAfterTableMutation();

			AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
			if (runState?.IsActive == true && runState.EffectState.ClubMissClimbGrowthStacks > 0)
			{
				runState.EffectState.ClubMissClimbGrowthStacks = 0;
				GetAdventureRunSessionStore()?.ReplaceEffectState(runState.EffectState.Clone());
			}

			_adventureMainPlayerSuccessfulCapturesThisTurn++;
			_adventureMainPlayerCaptureCountThisBattle++;
			await ApplyAdventureLinkedStemsQuestionRewardIfNeededAsync(playerIndex, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return new CaptureTableCardsResult(collectedCardCount, removedTableCardCount, questionRewardAnimation, adventureQuestionRewardEffects, adventurePostCaptureEffects);
			}
		}

			bool shouldQueueBigCaptureUpgrade = captureFlow.CapturedScoringCards
				&& ShouldQueueAdventureBigCaptureUpgrade(playerIndex, captureFlow.TotalCapturedCardCount);
			await CheckForWinnerAsync(turnSequenceGeneration, shouldQueueBigCaptureUpgrade);
			if (turnSequenceGeneration != _turnSequenceGeneration || _isLeavingGame)
			{
				return new CaptureTableCardsResult(collectedCardCount, removedTableCardCount, questionRewardAnimation, adventureQuestionRewardEffects, adventurePostCaptureEffects);
			}
		if (!IsGameOver() && removedTableCardCount > 0)
		{
			if (captureFlow.CapturedScoringCards && !suppressGardenEelCaptureRecord)
			{
				RecordGardenEelCaptureForSandBite(playerIndex);
			}

			if (captureFlow.CapturedScoringCards)
			{
				await ApplyJellyfishBubbleAfterCaptureAsync(playerIndex, turnSequenceGeneration);
			}
			await ApplyNautilusBombAfterMainPlayerCaptureAsync(playerIndex, turnSequenceGeneration);
			if (captureFlow.CapturedScoringCards)
			{
				RecordNarwhalCaptureForDouble(playerIndex);
			}
			await ApplyDragonQueenAndKingAfterEnemyCaptureAsync(playerIndex, removedTableCardCount, turnSequenceGeneration);
		}

		return new CaptureTableCardsResult(collectedCardCount, removedTableCardCount, questionRewardAnimation, adventureQuestionRewardEffects, adventurePostCaptureEffects);
	}
}
