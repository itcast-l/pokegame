using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task RevealCapturedBaseCardsFromBottomAsync(
		int captureStartIndex,
		int scorePlayerIndex,
		int turnSequenceGeneration,
		HashSet<int> pendingFaceUpMagnetTriggerIndexes,
		HashSet<int> resolvedMagnetTriggerIndexes,
		HashSet<int> magnetExtraCaptureIndexes,
		HashSet<int> resolvedDoubleTriggerIndexes,
		HashSet<int> resolvedOvertimeTriggerIndexes)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return;
		}

		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand)
			{
				continue;
			}

			if (tableCard.IsFaceUp)
			{
				if (pendingFaceUpMagnetTriggerIndexes.Contains(tableCardIndex))
				{
					await ResolveMagnetTableCardTriggerAsync(
						tableCardIndex,
						captureStartIndex,
						scorePlayerIndex,
						turnSequenceGeneration,
						resolvedMagnetTriggerIndexes,
						magnetExtraCaptureIndexes,
						resolvedDoubleTriggerIndexes,
						resolvedOvertimeTriggerIndexes);
					if (turnSequenceGeneration != _turnSequenceGeneration)
					{
						return;
					}
				}

				if (tableCard.Card.HasValue && IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Double))
				{
					await ResolveDoubleTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedDoubleTriggerIndexes);
					if (turnSequenceGeneration != _turnSequenceGeneration)
					{
						return;
					}
				}

				if (tableCard.Card.HasValue && IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Overtime))
				{
					await ResolveOvertimeTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedOvertimeTriggerIndexes);
					if (turnSequenceGeneration != _turnSequenceGeneration)
					{
						return;
					}
				}

				continue;
			}

			Texture2D? revealTexture = null;
			PlayingCard? revealedCard = null;
			if (tableCard.Card.HasValue)
			{
				revealTexture = tableCard.Card.Value.Texture;
				revealedCard = tableCard.Card.Value;
			}
			else if (tableCard.ItemOverlay.HasValue)
			{
				revealTexture = tableCard.ItemOverlay.Value.Texture;
			}

			if (revealTexture == null)
			{
				continue;
			}

			await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			tableCard.IsFaceUp = true;
			RenderTableCards();
			if (revealedCard.HasValue)
			{
				bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, magnetExtraCaptureIndexes);
				int popupPoints = GetCapturedCardScoreValueForPlayer(revealedCard.Value, scorePlayerIndex, hasDoubleEffect);
				_ = PlayTableCardPointPopupAsync(tableCardIndex, popupPoints, hasDoubleEffect);
			}

			if (tableCard.Card.HasValue && IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Magnet))
			{
				await ResolveMagnetTableCardTriggerAsync(
					tableCardIndex,
					captureStartIndex,
					scorePlayerIndex,
					turnSequenceGeneration,
					resolvedMagnetTriggerIndexes,
					magnetExtraCaptureIndexes,
					resolvedDoubleTriggerIndexes,
					resolvedOvertimeTriggerIndexes);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return;
				}
			}

			if (tableCard.Card.HasValue && IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Double))
			{
				await ResolveDoubleTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedDoubleTriggerIndexes);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return;
				}
			}

			if (tableCard.Card.HasValue && IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Overtime))
			{
				await ResolveOvertimeTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedOvertimeTriggerIndexes);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return;
				}
			}

			await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}
	}

	private int GetCapturedTablePoints(int captureStartIndex, int scorePlayerIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return 0;
		}

		int collectedPoints = 0;
		bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, extraTableCardIndexes);
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand)
			{
				continue;
			}

			if (!IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex))
			{
				continue;
			}

			if (tableCard.Card.HasValue)
			{
				collectedPoints += GetCapturedCardScoreValueForPlayer(tableCard.Card.Value, scorePlayerIndex, hasDoubleEffect);
			}
		}

		if (extraTableCardIndexes != null)
		{
			foreach (int tableCardIndex in extraTableCardIndexes)
			{
				if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
				{
					continue;
				}

				TableCard tableCard = _tableCards[tableCardIndex];
				if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue)
				{
					collectedPoints += GetCapturedCardScoreValueForPlayer(tableCard.Card.Value, scorePlayerIndex, hasDoubleEffect);
				}
			}
		}

		return collectedPoints;
	}

	private bool HasCapturedDoubleScoreEffect(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return false;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (!_tableCards[tableCardIndex].IsBuriedInSand && HasDoubleScoreEffect(_tableCards[tableCardIndex]))
			{
				return true;
			}
		}

		if (extraTableCardIndexes == null)
		{
			return false;
		}

		foreach (int tableCardIndex in extraTableCardIndexes)
		{
			if (tableCardIndex >= 0
				&& tableCardIndex < _tableCards.Count
				&& !_tableCards[tableCardIndex].IsBuriedInSand
				&& HasDoubleScoreEffect(_tableCards[tableCardIndex]))
			{
				return true;
			}
		}

		return false;
	}

	private bool HasDoubleScoreEffect(TableCard tableCard)
	{
		return (tableCard.Card.HasValue && IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Double))
			|| IsItemEffectType(tableCard, CardEffectTypes.DoubleCollect);
	}

	private int GetCapturedTableScorePlayerIndex(int captureStartIndex, int fallbackPlayerIndex)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return fallbackPlayerIndex;
		}

		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!IsItemEffectType(tableCard, CardEffectTypes.PlaceTablePairDestroy))
			{
				continue;
			}

			if (tableCard.ItemOwnerPlayerIndex >= 0 && tableCard.ItemOwnerPlayerIndex < player_count)
			{
				return tableCard.ItemOwnerPlayerIndex;
			}
		}

		return fallbackPlayerIndex;
	}

	private bool IsCapturedTableCardScoringIndex(int tableCardIndex, int captureStartIndex)
	{
		return tableCardIndex > captureStartIndex && tableCardIndex < _tableCards.Count - 1;
	}

	private async Task<CaptureTableCardsResult> CaptureTableCardsAsync(
		int playerIndex,
		int captureStartIndex,
		bool reserveReplacementCard = false,
		bool awardScore = true,
		int turnSequenceGeneration = 0,
		HashSet<int>? extraTableCardIndexes = null)
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
		int scorePlayerIndex = awardScore ? GetCapturedTableScorePlayerIndex(captureStartIndex, playerIndex) : playerIndex;
		int collectedPoints = awardScore ? GetCapturedTablePoints(captureStartIndex, scorePlayerIndex, extraTableCardIndexes) : 0;
		List<int> validExtraCaptureIndexes = BuildValidExtraCaptureIndexes(captureStartIndex, extraTableCardIndexes);
		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand)
			{
				continue;
			}

			removedTableCardCount++;
			if (tableCard.Card.HasValue)
			{
				AddCardToPlayerDeck(playerIndex, tableCard.Card.Value);
				collectedCardCount++;
			}

			if (IsQuestionTableCard(tableCard))
			{
				QuestionCardRewardGrant? rewardGrant = GrantQuestionCardReward(playerIndex, reserveReplacementCard);
				if (rewardGrant.HasValue && playerIndex == MainPlayerIndex && tableCardIndex < cardPositions.Count)
				{
					questionRewardAnimation = new QuestionCardRewardAnimation(
						rewardGrant.Value.ItemCard,
						rewardGrant.Value.ItemHandIndex,
						_centerArena.GlobalPosition + cardPositions[tableCardIndex],
						rewardGrant.Value.RequiresMagnetAttachment);
				}
			}

		}

		foreach (int tableCardIndex in validExtraCaptureIndexes)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue)
			{
				removedTableCardCount++;
				AddCardToPlayerDeck(playerIndex, tableCard.Card.Value);
				collectedCardCount++;
			}
		}

		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			if (!_tableCards[tableCardIndex].IsBuriedInSand)
			{
				_tableCards.RemoveAt(tableCardIndex);
			}
		}
		for (int index = validExtraCaptureIndexes.Count - 1; index >= 0; index--)
		{
			_tableCards.RemoveAt(validExtraCaptureIndexes[index]);
		}
		RefreshGardenEelBuriedCardIndex();
		if (captureCount + validExtraCaptureIndexes.Count > 0)
		{
			_completedTurnsSinceQuestionCardRefresh = 0;
			_questionCardRefreshAnchorPlayerIndex = playerIndex;
		}

		if (awardScore)
		{
			AddPlayerScore(scorePlayerIndex, collectedPoints);
		}

		CheckForWinner();
		if (!IsGameOver())
		{
			RecordGardenEelCaptureForSandAmbush(playerIndex);
			ApplyJellyfishBubbleAfterCapture(playerIndex);
			await ApplyNautilusBombAfterMainPlayerCaptureAsync(playerIndex, turnSequenceGeneration);
			RecordNarwhalCaptureForDouble(playerIndex);
			ApplyDragonQueenAndKingAfterEnemyCapture(playerIndex, removedTableCardCount);
		}

		return new CaptureTableCardsResult(collectedCardCount, removedTableCardCount, questionRewardAnimation);
	}

	private List<int> BuildValidExtraCaptureIndexes(int captureStartIndex, HashSet<int>? extraTableCardIndexes)
	{
		var validIndexes = new List<int>();
		if (extraTableCardIndexes == null)
		{
			return validIndexes;
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

			validIndexes.Add(tableCardIndex);
		}

		validIndexes.Sort();
		return validIndexes;
	}
}
