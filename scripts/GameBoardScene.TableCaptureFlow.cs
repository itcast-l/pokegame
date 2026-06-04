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

		int captureCount = _tableCards.Count - captureStartIndex;
		CaptureStatePlan capturePlan = _captureStateService.CreatePlan(_state, captureStartIndex, extraTableCardIndexes);
		if (!capturePlan.IsValid)
		{
			return new CaptureTableCardsResult(0, 0, null);
		}

		int collectedCardCount = 0;
		int removedTableCardCount = 0;
		QuestionCardRewardAnimation? questionRewardAnimation = null;
		bool capturedMiddleCards = HasCapturedMiddleTableCards(captureStartIndex);
		int scorePlayerIndex = awardScore ? GetCapturedTableScorePlayerIndex(captureStartIndex, playerIndex) : playerIndex;
		int collectedPoints = awardScore && capturedMiddleCards
			? GetCapturedTablePoints(captureStartIndex, scorePlayerIndex, extraTableCardIndexes)
			: 0;
		bool capturedScoringCards = capturedMiddleCards && HasCapturedScoringTableCards(captureStartIndex, extraTableCardIndexes);
		Rect2 tableLayoutRect = GetTableCardLayoutRect();
		if (awardScore && capturedMiddleCards && ShouldPlayCrabScoreReductionAbility(scorePlayerIndex, captureStartIndex, extraTableCardIndexes))
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
			if (capturedMiddleCards && IsQuestionTableCard(tableCard))
			{
				QuestionCardRewardGrant? rewardGrant = await GrantQuestionCardRewardAsync(playerIndex, reserveReplacementCard, turnSequenceGeneration);
				if (rewardGrant.HasValue && playerIndex == MainPlayerIndex)
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
		if (capturedMiddleCards && captureCount + capturePlan.ExtraTableCardIndexes.Count > 0)
		{
			QuestionCardRefreshState questionCardRefreshState = _turnAdvancementService.StartQuestionCardRefreshTracking(playerIndex);
			_completedTurnsSinceQuestionCardRefresh = questionCardRefreshState.CompletedTurnsSinceQuestionCardRefresh;
			_questionCardRefreshAnchorPlayerIndex = questionCardRefreshState.QuestionCardRefreshAnchorPlayerIndex;
		}

		if (awardScore)
		{
			AddPlayerScore(scorePlayerIndex, collectedPoints);
		}

		CheckForWinner();
		if (!IsGameOver() && removedTableCardCount > 0)
		{
			if (capturedScoringCards && !suppressGardenEelCaptureRecord)
			{
				RecordGardenEelCaptureForSandBite(playerIndex);
			}

			if (capturedScoringCards)
			{
				await ApplyJellyfishBubbleAfterCaptureAsync(playerIndex, turnSequenceGeneration);
			}
			await ApplyNautilusBombAfterMainPlayerCaptureAsync(playerIndex, turnSequenceGeneration);
			if (capturedScoringCards)
			{
				RecordNarwhalCaptureForDouble(playerIndex);
			}
			await ApplyDragonQueenAndKingAfterEnemyCaptureAsync(playerIndex, removedTableCardCount, turnSequenceGeneration);
		}

		return new CaptureTableCardsResult(collectedCardCount, removedTableCardCount, questionRewardAnimation);
	}
}
