using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task RevealCapturedBaseCardsEdgesBeforeMiddleAsync(
		int captureStartIndex,
		int scorePlayerIndex,
		int turnSequenceGeneration,
		PairCaptureEdgeFloatHold? pairCaptureEdgeFloat,
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
		foreach (int tableCardIndex in CaptureRevealOrder.BuildBaseRevealIndexes(captureStartIndex, _tableCards.Count))
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
				bool overlayRevealPlayed = pairCaptureEdgeFloat != null
					&& await PlayPairCaptureEdgeFloatRevealAnimationAsync(pairCaptureEdgeFloat, tableCardIndex, revealTexture);
				if (!overlayRevealPlayed)
				{
					await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
				}
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
				TrackBoardTask(PlayTableCardPointPopupAsync(tableCardIndex, popupPoints, hasDoubleEffect), nameof(PlayTableCardPointPopupAsync));
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

			await WaitForBoardDelayAsync(TableCardRevealPauseSeconds);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}
	}

	private bool ShouldSkipOwnerPreviewRevealAnimation(TableCard tableCard)
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
		return CaptureResolver.Resolve(ToCaptureScoringTableCards(), captureStartIndex).CapturedMiddleCards;
	}

}
