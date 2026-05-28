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

		bool shouldResolveAttachedTableCardTriggers = HasCapturedMiddleTableCards(captureStartIndex);
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand)
			{
				continue;
			}

			if (tableCard.IsFaceUp)
			{
				if (shouldResolveAttachedTableCardTriggers && pendingFaceUpMagnetTriggerIndexes.Contains(tableCardIndex))
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

				if (shouldResolveAttachedTableCardTriggers && tableCard.Card.HasValue && IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.DoubleCollect))
				{
					await ResolveDoubleTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedDoubleTriggerIndexes);
					if (turnSequenceGeneration != _turnSequenceGeneration)
					{
						return;
					}
				}

				if (shouldResolveAttachedTableCardTriggers && tableCard.Card.HasValue && IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.ExtraTurn))
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

			if (ShouldSkipOwnerPreviewRevealAnimation(tableCard))
			{
				tableCard.IsFaceUp = true;
				RenderTableCards();
			}
			else
			{
				await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return;
				}

				tableCard.IsFaceUp = true;
				RenderTableCards();
			}
			if (revealedCard.HasValue && IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex))
			{
				bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, magnetExtraCaptureIndexes);
				int popupPoints = GetCapturedTableCardScoreValueForPlayer(tableCard, scorePlayerIndex, hasDoubleEffect);
				_ = PlayTableCardPointPopupAsync(tableCardIndex, popupPoints, hasDoubleEffect);
			}

			if (shouldResolveAttachedTableCardTriggers && tableCard.Card.HasValue && IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.MagnetExtraCapture))
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

			if (shouldResolveAttachedTableCardTriggers && tableCard.Card.HasValue && IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.DoubleCollect))
			{
				await ResolveDoubleTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedDoubleTriggerIndexes);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return;
				}
			}

			if (shouldResolveAttachedTableCardTriggers && tableCard.Card.HasValue && IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.ExtraTurn))
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

	private static bool ShouldSkipOwnerPreviewRevealAnimation(TableCard tableCard)
	{
		return ShouldShowMainPlayerTablePreview(tableCard);
	}

	private bool ShouldPlayPairCaptureEdgeFloat(int captureStartIndex, int tailTableCardIndex, bool playedRankCollectsTable)
	{
		if (playedRankCollectsTable)
		{
			return false;
		}

		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return false;
		}

		int clampedTailIndex = Mathf.Clamp(tailTableCardIndex, captureStartIndex, _tableCards.Count - 1);
		int capturedVisibleCardCount = 0;
		for (int tableCardIndex = captureStartIndex; tableCardIndex <= clampedTailIndex; tableCardIndex++)
		{
			if (!_tableCards[tableCardIndex].IsBuriedInSand)
			{
				capturedVisibleCardCount++;
			}
		}

		return capturedVisibleCardCount > 2;
	}

	private bool HasCapturedMiddleTableCards(int captureStartIndex)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return false;
		}

		int capturedVisibleCardCount = 0;
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (_tableCards[tableCardIndex].IsBuriedInSand)
			{
				continue;
			}

			capturedVisibleCardCount++;
			if (capturedVisibleCardCount > 2)
			{
				return true;
			}
		}

		return false;
	}

	private int GetCapturedTablePoints(int captureStartIndex, int scorePlayerIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count || !HasCapturedMiddleTableCards(captureStartIndex))
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
				collectedPoints += GetCapturedTableCardScoreValueForPlayer(tableCard, scorePlayerIndex, hasDoubleEffect);
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
					collectedPoints += GetCapturedTableCardScoreValueForPlayer(tableCard, scorePlayerIndex, hasDoubleEffect);
				}
			}
		}

		return collectedPoints;
	}

	private int GetCapturedTableCardScoreValueForPlayer(TableCard tableCard, int scorePlayerIndex, bool hasDoubleEffect)
	{
		if (!tableCard.Card.HasValue || IsSeaSlugSlimeScoreSuppressed(tableCard, scorePlayerIndex))
		{
			return 0;
		}

		return GetCapturedCardScoreValueForPlayer(tableCard.Card.Value, scorePlayerIndex, hasDoubleEffect);
	}

	private bool IsSeaSlugSlimeScoreSuppressed(TableCard tableCard, int scorePlayerIndex)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.ColorCurrent)
			&& scorePlayerIndex == MainPlayerIndex
			&& tableCard.IsSlimeCoated;
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
		return (tableCard.Card.HasValue && IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.DoubleCollect))
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

	private bool HasCapturedScoringTableCards(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count || !HasCapturedMiddleTableCards(captureStartIndex))
		{
			return false;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex)
				&& IsScoringTableCard(_tableCards[tableCardIndex]))
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
				&& IsScoringTableCard(_tableCards[tableCardIndex]))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsScoringTableCard(TableCard tableCard)
	{
		return !tableCard.IsBuriedInSand
			&& tableCard.Card.HasValue
			&& GetCardScoreValue(tableCard.Card.Value) > 0;
	}

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
			if (tableCard.Card.HasValue)
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
					AddCardToPlayerDeck(playerIndex, tableCard.Card.Value);
					collectedCardCount++;
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
			_completedTurnsSinceQuestionCardRefresh = 0;
			_questionCardRefreshAnchorPlayerIndex = playerIndex;
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
