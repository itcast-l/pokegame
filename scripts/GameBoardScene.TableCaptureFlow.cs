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
	bool suppressGardenEelCaptureRecord = false,
	IReadOnlyList<TableCard>? expectedTableCardReferences = null,
	Action? commitImmediatelyBeforeApply = null,
	int blackLedgerRedirectTableCardIndex = -2)
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
	int scorePlayerIndex = awardScore
		? ScoreService.GetCapturedScorePlayerIndex(
			captureScoringTableCards,
			captureStartIndex,
			playerIndex,
			player_count)
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
	var adventureCaptureScoringTableIndexes = new List<int>();
	var capturedBaseCards = new List<PlayingCard>();
	bool capturedTraceMark = false;
	bool capturedHuntMark = false;
	TableCard? clubGapRootingTargetTableCard = null;
	AdventureUpgradeEffectState?
		plannedClubCaptureEffectState = null;
	Vector2? questionRewardStartGlobalPosition = null;

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
				adventurePostCaptureEffects);
		RecordDebugCaptureOwnershipResult(result);
		return result;
	}

	if (awardScore
		&& captureFlow.Capture.CapturedMiddleCards
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

	Rect2 tableLayoutRect = GetTableCardLayoutRect();
	foreach (int tableCardIndex in capturePlan.BaseTableCardIndexes)
	{
		if (tableCardIndex < 0
			|| tableCardIndex >= _tableCards.Count)
		{
			return CaptureTableCardsResult.NotApplied;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (tableCard.Card.HasValue
			&& !tableCard.RemoveCardOnCapture)
		{
			capturedBaseCards.Add(tableCard.Card.Value);
		}

		if (questionRewardStartGlobalPosition == null
			&& captureFlow.Capture.CapturedMiddleCards
			&& IsQuestionTableCard(tableCard))
		{
			questionRewardStartGlobalPosition =
				_centerArena.GlobalPosition
				+ GetTableCardPosition(
					tableCardIndex,
					tableLayoutRect);
		}
	}

	capturedTraceMark = IsCapturedTableCardReference(
		_adventureTraceHuntMarkedTableCard,
		captureStartIndex,
		extraTableCardIndexes);
	capturedHuntMark = IsCapturedTableCardReference(
		_adventureHuntOrderMarkedTableCard,
		captureStartIndex,
		extraTableCardIndexes);
	if (playerIndex == MainPlayerIndex)
	{
		bool hasMiddleScoringCards = false;
		for (int tableCardIndex = captureStartIndex + 1;
			tableCardIndex < _tableCards.Count - 1;
			tableCardIndex++)
		{
			TableCard middleTableCard = _tableCards[tableCardIndex];
			if (!middleTableCard.IsBuriedInSand
				&& middleTableCard.Card.HasValue
				&& GetCardScoreValue(
					middleTableCard.Card.Value) > 0)
			{
				hasMiddleScoringCards = true;
				break;
			}
		}

		AdventureUpgradeRunState? runState =
			GetAdventureUpgradeRunState();
		bool leftAdjacentBaseCardAvailable =
			captureStartIndex > 0
			&& !_tableCards[captureStartIndex - 1]
				.IsBuriedInSand
			&& _tableCards[captureStartIndex - 1]
				.Card.HasValue;
		if (runState?.IsActive == true)
		{
			AdventureUpgradeEffectState effectStatePreview =
				runState.EffectState.Clone();
			AdventureClubCaptureExtensionResult
				clubCaptureExtensionResult =
					AdventureUpgradeEffectService
						.ResolveClubCaptureExtensions(
							effectStatePreview,
							runState.GetAcquiredUpgradeIds(),
							hasMiddleScoringCards,
							leftAdjacentBaseCardAvailable);
			plannedClubCaptureEffectState =
				effectStatePreview;
			if (clubCaptureExtensionResult
				.CaptureLeftAdjacentBaseCard)
			{
				clubGapRootingTargetTableCard =
					_tableCards[captureStartIndex - 1];
			}
		}
	}

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

	if (plannedClubCaptureEffectState != null)
	{
		GetAdventureRunSessionStore()?.ReplaceEffectState(
			plannedClubCaptureEffectState.Clone());
	}
	AdventureAllGainsConvergeResult allGainsConverge = default;
	if (awardScore)
	{
		BuildAdventureCaptureDamageCardPointLists(
			captureScoringTableCards,
			captureStartIndex,
			scorePlayerIndex,
			extraTableCardIndexes,
			adventureCaptureRawPoints,
			adventureCaptureResolvedPoints,
			adventureCaptureScoringTableIndexes);
		allGainsConverge = AdventureUpgradeEffectService
			.ResolveAllGainsConverge(
				GetAdventureUpgradeRunState()
					?.GetAcquiredUpgradeIds()
					?? Array.Empty<string>(),
				new AdventureCaptureDamageInput(
					BaseDamage: captureFlow.Score.Points,
					QuestionRewardEchoActive:
						_adventureMainPlayerQuestionRewardEchoActive,
					ScoringCardRawPoints:
						adventureCaptureRawPoints,
					ScoringCardResolvedPoints:
						adventureCaptureResolvedPoints,
					ScoringCardTableIndexes:
						adventureCaptureScoringTableIndexes,
					CapturerIsMainPlayer:
						playerIndex == MainPlayerIndex));
	}
	if (allGainsConverge.BonusDamage > 0
		&& allGainsConverge.ScoringCardTableIndex >= 0)
	{
		_ = PlayTableCardPointPopupAsync(
			allGainsConverge.ScoringCardTableIndex,
			allGainsConverge.BonusDamage);
	}
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
	collectedCardCount =
		captureStateResult.CollectedCardCount;
	removedTableCardCount =
		captureStateResult.RemovedTableCardCount;
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
	bool CanCommitQuestionReward()
	{
		return !_isLeavingGame
			&& !IsGameOver()
			&& IsCaptureTableReferenceSnapshotCurrent(
				turnSequenceGeneration,
				postApplyTableCardReferences);
	}

	if (questionRewardStartGlobalPosition.HasValue)
	{
		QuestionCardRewardGrant? rewardGrant =
			await GrantQuestionCardRewardAsync(
				playerIndex,
				reserveReplacementCard,
				turnSequenceGeneration,
				CanCommitQuestionReward);
		if (!CanCommitQuestionReward())
		{
			return BuildAppliedResult(aborted: true);
		}

		if (rewardGrant.HasValue)
		{
			adventureQuestionRewardEffects =
				ResolveAdventureQuestionRewardEffects(
					playerIndex);
			questionRewardAnimation =
				new QuestionCardRewardAnimation(
					rewardGrant.Value.ItemCard,
					rewardGrant.Value.ItemHandIndex,
					questionRewardStartGlobalPosition.Value,
					rewardGrant.Value
						.RequiresMagnetAttachment);
		}
	}

	await ApplyAdventureClubCaptureReturnsAsync(
		playerIndex,
		capturedBaseCards,
		playedCardForAdventureReturns,
		playedCardUsedAttachedEffect,
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

	if (clubGapRootingTargetTableCard != null
		&& _tableCards.Contains(
			clubGapRootingTargetTableCard)
		&& clubGapRootingTargetTableCard.Card.HasValue)
	{
		_tableCards.Remove(
			clubGapRootingTargetTableCard);
		AddCardToPlayerDeck(
			playerIndex,
			clubGapRootingTargetTableCard.Card.Value);
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

	if (awardScore)
	{
		int adventureOldTableDamageDelta =
			adventureFlatDamageDelta;
		if (prePlayTableCards != null
			&& prePlayTableCards.Count > 0)
		{
			adventureOldTableDamageDelta +=
				AdventureUpgradeEffectService
					.ResolveOldTableCaptureDamageDelta(
						GetAdventureUpgradeRunState()
							?.GetAcquiredUpgradeIds()
							?? Array.Empty<string>(),
						playerIndex == MainPlayerIndex,
						CountCapturedOldScoringTableCards(
							prePlayTableCards,
							captureStartIndex,
							extraTableCardIndexes),
						capturedTraceMark,
						capturedHuntMark);
		}

		bool isFirstCaptureThisBattle =
			playerIndex == MainPlayerIndex
			&& _adventureMainPlayerCaptureCountThisBattle
				== 0;
		AdventureCaptureDamageResult resolvedCaptureDamage =
			ResolveAdventureCaptureDamage(
				playerIndex,
				new AdventureCaptureDamageInput(
					BaseDamage:
						captureFlow.Score.Points,
					FlatDamageDelta:
						adventureOldTableDamageDelta,
					IsFirstCaptureThisBattle:
						isFirstCaptureThisBattle,
					IncludesQuestionReward:
						questionRewardAnimation.HasValue,
					QuestionRewardEchoActive:
						_adventureMainPlayerQuestionRewardEchoActive,
					ScoringCardRawPoints:
						adventureCaptureRawPoints,
					ScoringCardResolvedPoints:
						adventureCaptureResolvedPoints,
					ScoringCardTableIndexes:
						adventureCaptureScoringTableIndexes,
					CapturerIsMainPlayer:
						playerIndex == MainPlayerIndex));
		if (resolvedCaptureDamage.FinalDamage > 0)
		{
			AddPlayerScore(
				scorePlayerIndex,
				resolvedCaptureDamage.FinalDamage);
		}
		if (playerIndex == MainPlayerIndex)
		{
			adventurePostCaptureEffects =
				ResolveAdventurePostCaptureEffects(
					playerIndex,
					resolvedCaptureDamage.FinalDamage,
					_adventureMainPlayerSuccessfulCapturesThisTurn
						+ 1,
					attachedEffectTriggered:
						playedCardUsedAttachedEffect,
					capturedOnlyOuterVisibleCards:
						prePlayTableCards != null
						&& CapturedOnlyOuterVisibleCards(
							prePlayTableCards,
							captureStartIndex,
							extraTableCardIndexes));
		}
	}

	if (capturedTraceMark)
	{
		SetAdventureTraceHuntMarkedTableCard(
			null,
			playIntro: false);
	}

	if (playerIndex == MainPlayerIndex
		&& captureStateResult.CollectedCardCount > 0)
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
		AdventureUpgradeRunState? runState =
			GetAdventureUpgradeRunState();
		if (runState?.IsActive == true
			&& runState.EffectState
				.ClubMissClimbGrowthStacks > 0)
		{
			runState.EffectState
				.ClubMissClimbGrowthStacks = 0;
			GetAdventureRunSessionStore()
				?.ReplaceEffectState(
					runState.EffectState.Clone());
		}

		_adventureMainPlayerSuccessfulCapturesThisTurn++;
		_adventureMainPlayerCaptureCountThisBattle++;
		await ApplyAdventureLinkedStemsQuestionRewardIfNeededAsync(
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
	}

	bool shouldQueueBigCaptureUpgrade =
		captureFlow.CapturedScoringCards
		&& ShouldQueueAdventureBigCaptureUpgrade(
			playerIndex,
			captureFlow.TotalCapturedCardCount);
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
