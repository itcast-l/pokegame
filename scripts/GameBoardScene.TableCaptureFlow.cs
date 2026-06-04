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

		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand)
			{
				continue;
			}

			removedTableCardCount++;
			if (tableCard.Card.HasValue && !tableCard.RemoveCardOnCapture)
			{
				AddCardToPlayerDeck(playerIndex, tableCard.Card.Value);
				collectedCardCount++;
			}

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

		int validExtraCaptureCount = BuildValidExtraCaptureIndexes(captureStartIndex, extraTableCardIndexes);
		if (validExtraCaptureCount > 0)
		{
			for (int index = 0; index < validExtraCaptureCount; index++)
			{
				int tableCardIndex = _validExtraCaptureIndexes[index];
				TableCard tableCard = _tableCards[tableCardIndex];
				if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue)
				{
					removedTableCardCount++;
					if (!tableCard.RemoveCardOnCapture)
					{
						AddCardToPlayerDeck(playerIndex, tableCard.Card.Value);
						collectedCardCount++;
					}
				}
			}
		}

		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			if (!_tableCards[tableCardIndex].IsBuriedInSand)
			{
				_tableCards.RemoveAt(tableCardIndex);
			}
		}
		if (validExtraCaptureCount > 0)
		{
			for (int index = validExtraCaptureCount - 1; index >= 0; index--)
			{
				_tableCards.RemoveAt(_validExtraCaptureIndexes[index]);
			}
		}
		RefreshGardenEelBuriedCardIndex();
		if (capturedMiddleCards && captureCount + validExtraCaptureCount > 0)
		{
			QuestionCardRefreshState questionCardRefreshState = TurnProgressRules.StartQuestionCardRefreshTracking(playerIndex);
			_completedTurnsSinceQuestionCardRefresh = questionCardRefreshState.CompletedTurnsSinceQuestionCardRefresh;
			_questionCardRefreshAnchorPlayerIndex = questionCardRefreshState.QuestionCardRefreshAnchorPlayerIndex;
		}
		_validExtraCaptureIndexes.Clear();

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

	private int BuildValidExtraCaptureIndexes(int captureStartIndex, HashSet<int>? extraTableCardIndexes)
	{
		_validExtraCaptureIndexes.Clear();
		if (extraTableCardIndexes == null)
		{
			return 0;
		}

		foreach (int tableCardIndex in extraTableCardIndexes)
		{
			if (tableCardIndex < 0 || tableCardIndex >= captureStartIndex || tableCardIndex >= _tableCards.Count)
			{
				continue;
			}

			if (!_tableCards[tableCardIndex].Card.HasValue)
			{
				continue;
			}

			if (_tableCards[tableCardIndex].IsBuriedInSand)
			{
				continue;
			}

			_validExtraCaptureIndexes.Add(tableCardIndex);
		}

		_validExtraCaptureIndexes.Sort();
		return _validExtraCaptureIndexes.Count;
	}
}
