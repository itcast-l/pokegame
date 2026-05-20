using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task RevealCapturedBaseCardsFromBottomAsync(
		int captureStartIndex,
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
			if (tableCard.IsFaceUp)
			{
				if (pendingFaceUpMagnetTriggerIndexes.Contains(tableCardIndex))
				{
					await ResolveMagnetTableCardTriggerAsync(
						tableCardIndex,
						captureStartIndex,
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

				if (tableCard.Card.HasValue && tableCard.Card.Value.HasDoubleEffect)
				{
					await ResolveDoubleTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedDoubleTriggerIndexes);
					if (turnSequenceGeneration != _turnSequenceGeneration)
					{
						return;
					}
				}

				if (tableCard.Card.HasValue && tableCard.Card.Value.HasOvertimeEffect)
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
			int? revealedCardPoints = null;
			if (tableCard.Card.HasValue)
			{
				revealTexture = tableCard.Card.Value.Texture;
				revealedCardPoints = GetCardScoreValue(tableCard.Card.Value);
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
			if (revealedCardPoints.HasValue)
			{
				bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, magnetExtraCaptureIndexes);
				int popupPoints = hasDoubleEffect ? revealedCardPoints.Value * 2 : revealedCardPoints.Value;
				_ = PlayTableCardPointPopupAsync(tableCardIndex, popupPoints, hasDoubleEffect);
			}

			if (tableCard.Card.HasValue && tableCard.Card.Value.HasMagnetEffect)
			{
				await ResolveMagnetTableCardTriggerAsync(
					tableCardIndex,
					captureStartIndex,
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

			if (tableCard.Card.HasValue && tableCard.Card.Value.HasDoubleEffect)
			{
				await ResolveDoubleTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedDoubleTriggerIndexes);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return;
				}
			}

			if (tableCard.Card.HasValue && tableCard.Card.Value.HasOvertimeEffect)
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

	private int GetCapturedTablePoints(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return 0;
		}

		int collectedPoints = 0;
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex))
			{
				continue;
			}

			if (tableCard.Card.HasValue)
			{
				collectedPoints += GetCardScoreValue(tableCard.Card.Value);
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
				if (tableCard.Card.HasValue)
				{
					collectedPoints += GetCardScoreValue(tableCard.Card.Value);
				}
			}
		}

		return HasCapturedDoubleScoreEffect(captureStartIndex, extraTableCardIndexes)
			? collectedPoints * 2
			: collectedPoints;
	}

	private bool HasCapturedDoubleScoreEffect(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return false;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (HasDoubleScoreEffect(_tableCards[tableCardIndex]))
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
				&& HasDoubleScoreEffect(_tableCards[tableCardIndex]))
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasDoubleScoreEffect(TableCard tableCard)
	{
		return (tableCard.Card.HasValue && tableCard.Card.Value.HasDoubleEffect)
			|| (tableCard.ItemOverlay.HasValue && tableCard.ItemOverlay.Value.Id == DoubleItemCardId);
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
			if (!tableCard.ItemOverlay.HasValue || tableCard.ItemOverlay.Value.Id != BombItemCardId)
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

	private int CaptureTableCards(int playerIndex, int captureStartIndex, bool reserveReplacementCard = false, bool awardScore = true)
	{
		return CaptureTableCards(playerIndex, captureStartIndex, reserveReplacementCard, awardScore, out _);
	}

	private int CaptureTableCards(int playerIndex, int captureStartIndex, out QuestionCardRewardAnimation? questionRewardAnimation)
	{
		return CaptureTableCards(playerIndex, captureStartIndex, false, true, out questionRewardAnimation);
	}

	private int CaptureTableCards(
		int playerIndex,
		int captureStartIndex,
		bool reserveReplacementCard,
		bool awardScore,
		out QuestionCardRewardAnimation? questionRewardAnimation,
		HashSet<int>? extraTableCardIndexes = null)
	{
		questionRewardAnimation = null;
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return 0;
		}

		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return 0;
		}

		int captureCount = _tableCards.Count - captureStartIndex;
		int collectedCardCount = 0;
		int collectedPoints = awardScore ? GetCapturedTablePoints(captureStartIndex, extraTableCardIndexes) : 0;
		int scorePlayerIndex = awardScore ? GetCapturedTableScorePlayerIndex(captureStartIndex, playerIndex) : playerIndex;
		List<int> validExtraCaptureIndexes = BuildValidExtraCaptureIndexes(captureStartIndex, extraTableCardIndexes);
		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.Card.HasValue)
			{
				AddCardToPlayerDeck(playerIndex, tableCard.Card.Value);
				collectedCardCount++;
			}

			if (tableCard.ItemOverlay.HasValue && tableCard.ItemOverlay.Value.Id == QuestionMarkItemCardId)
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
			if (tableCard.Card.HasValue)
			{
				AddCardToPlayerDeck(playerIndex, tableCard.Card.Value);
				collectedCardCount++;
			}
		}

		_tableCards.RemoveRange(captureStartIndex, captureCount);
		for (int index = validExtraCaptureIndexes.Count - 1; index >= 0; index--)
		{
			_tableCards.RemoveAt(validExtraCaptureIndexes[index]);
		}
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
		return collectedCardCount;
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

			validIndexes.Add(tableCardIndex);
		}

		validIndexes.Sort();
		return validIndexes;
	}
}
