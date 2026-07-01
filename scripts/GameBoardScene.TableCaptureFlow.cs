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
		Rect2 tableLayoutRect = GetTableCardLayoutRect();
		if (awardScore
			&& captureFlow.Capture.CapturedMiddleCards
			&& ShouldPlayCrabScoreReductionAbility(scorePlayerIndex, captureStartIndex, extraTableCardIndexes))
		{
			await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(1, turnSequenceGeneration, AdventureEnemyAbilities.TideGuard);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return new CaptureTableCardsResult(0, 0, null);
			}
		}

		foreach (int tableCardIndex in capturePlan.BaseTableCardIndexes)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (captureFlow.Capture.CapturedMiddleCards && IsQuestionTableCard(tableCard))
			{
				QuestionCardRewardGrant? rewardGrant = await GrantQuestionCardRewardAsync(playerIndex, reserveReplacementCard, turnSequenceGeneration);
				if (rewardGrant.HasValue)
				{
					questionRewardAnimation = new QuestionCardRewardAnimation(
						rewardGrant.Value.ItemCard,
						rewardGrant.Value.ItemHandIndex,
						_centerArena.GlobalPosition + GetTableCardPosition(tableCardIndex, tableLayoutRect),
						rewardGrant.Value.RequiresMagnetAttachment);
				}
			}
		}

		CaptureStateResult captureStateResult = _captureStateService.ApplyCapture(
			_state,
			capturePlan,
			playerIndex,
			ShouldPreserveCardEffectsWhenEnteringDeck());
		collectedCardCount = captureStateResult.CollectedCardCount;
		removedTableCardCount = captureStateResult.RemovedTableCardCount;
		RefreshGardenEelBuriedCardIndex();
		if (captureFlow.ShouldTrackQuestionCardRefresh)
		{
			QuestionCardRefreshState questionCardRefreshState = _turnAdvancementService.StartQuestionCardRefreshTracking(playerIndex);
			_completedTurnsSinceQuestionCardRefresh = questionCardRefreshState.CompletedTurnsSinceQuestionCardRefresh;
			_questionCardRefreshAnchorPlayerIndex = questionCardRefreshState.QuestionCardRefreshAnchorPlayerIndex;
		}

		if (awardScore)
		{
			AddPlayerScore(scorePlayerIndex, captureFlow.Score.Points);
		}

		if (captureFlow.CapturedScoringCards)
		{
			await ResolveAdventureBigCaptureUpgradeAsync(playerIndex, captureFlow.TotalCapturedCardCount, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || _isLeavingGame)
			{
				return new CaptureTableCardsResult(collectedCardCount, removedTableCardCount, questionRewardAnimation);
			}
		}

		CheckForWinner();
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

		return new CaptureTableCardsResult(collectedCardCount, removedTableCardCount, questionRewardAnimation);
	}
}
