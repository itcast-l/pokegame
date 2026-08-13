using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
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
		foreach (int tableCardIndex in MatchReadModel.BuildBaseCaptureRevealIndexes(captureStartIndex, _tableCards.Count))
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
				revealTexture = GetPlayingCardTexture(tableCard.Card.Value);
				revealedCard = tableCard.Card.Value;
			}
			else if (tableCard.ItemOverlay.HasValue)
			{
				revealTexture = GetItemCardTexture(tableCard.ItemOverlay.Value);
			}

			if (revealTexture == null)
			{
				continue;
			}

			if (ShouldSkipOwnerPreviewRevealAnimation(tableCard))
			{
				// The capture presentation can outlive the rule turn. Do not submit a
				// face-up mutation after authority has already advanced to another seat.
				if (turnSequenceGeneration != _turnSequenceGeneration
					|| _currentTurnPlayerIndex != scorePlayerIndex
					|| IsGameOver())
				{
					return;
				}
				if (!TrySubmitSetTableCardsFaceUpRuleCommand(
					scorePlayerIndex,
					ImmutableArray.Create(new RuleTableCardReference(tableCardIndex, tableCard.InstanceId)),
					expectedFaceUp: false,
					faceUp: true,
					turnSequenceGeneration,
					out _))
				{
					return;
				}
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
				if (_currentTurnPlayerIndex != scorePlayerIndex || IsGameOver())
				{
					return;
				}

				if (!TrySubmitSetTableCardsFaceUpRuleCommand(
					scorePlayerIndex,
					ImmutableArray.Create(new RuleTableCardReference(tableCardIndex, tableCard.InstanceId)),
					expectedFaceUp: false,
					faceUp: true,
					turnSequenceGeneration,
					out _))
				{
					return;
				}
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
		return MatchReadModel.ResolveCapture(ToCaptureScoringTableCards(), captureStartIndex).CapturedMiddleCards;
	}

}
