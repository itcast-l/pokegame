using System;
using System.Collections.Generic;

internal static class CaptureScoringRules
{
	internal static ScoreBreakdown GetCapturedScore(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		int scorePlayerIndex,
		CaptureScoringContext context,
		IReadOnlySet<int>? extraTableCardIndexes = null)
	{
		return ScoreService.GetCapturedScore(
			tableCards,
			captureStartIndex,
			scorePlayerIndex,
			context,
			extraTableCardIndexes);
	}

	internal static int GetCapturedScorePlayerIndex(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		int fallbackPlayerIndex,
		int playerCount)
	{
		return ScoreService.GetCapturedScorePlayerIndex(
			tableCards,
			captureStartIndex,
			fallbackPlayerIndex,
			playerCount);
	}

	internal static int GetCapturedTablePoints(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		CaptureScoringContext context,
		IReadOnlySet<int>? extraTableCardIndexes = null)
	{
		if (!IsValidCaptureStart(tableCards, captureStartIndex) || !HasCapturedMiddleTableCards(tableCards, captureStartIndex))
		{
			return 0;
		}

		int collectedPoints = 0;
		bool hasDoubleEffect = HasCapturedDoubleScoreEffect(tableCards, captureStartIndex, extraTableCardIndexes);
		for (int tableCardIndex = captureStartIndex; tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			CaptureScoringTableCard tableCard = tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand)
			{
				continue;
			}

			if (!IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex, tableCards.Count))
			{
				continue;
			}

			collectedPoints += GetCapturedTableCardScoreValue(tableCard, context, hasDoubleEffect);
		}

		if (extraTableCardIndexes != null)
		{
			foreach (int tableCardIndex in extraTableCardIndexes)
			{
				if (tableCardIndex < 0 || tableCardIndex >= tableCards.Count)
				{
					continue;
				}

				CaptureScoringTableCard tableCard = tableCards[tableCardIndex];
				if (!tableCard.IsBuriedInSand)
				{
					collectedPoints += GetCapturedTableCardScoreValue(tableCard, context, hasDoubleEffect);
				}
			}
		}

		return collectedPoints;
	}

	internal static int GetCapturedTableCardScoreValue(CaptureScoringTableCard tableCard, CaptureScoringContext context, bool hasDoubleEffect)
	{
		if (!tableCard.Card.HasValue || IsSlimeScoreSuppressed(tableCard.IsSlimeCoated, context))
		{
			return 0;
		}

		return ApplyCapturedCardScoreModifiers(tableCard.Card.Value.ScoreValue, context, hasDoubleEffect);
	}

	internal static int ApplyCapturedCardScoreModifiers(int points, CaptureScoringContext context, bool hasDoubleEffect)
	{
		if (hasDoubleEffect)
		{
			points *= 2;
		}

		if (context.ApplyScoreReductionPerCard)
		{
			points = Math.Max(0, points - context.ScoreReductionPerCard);
		}

		return points;
	}

	internal static bool HasCapturedDoubleScoreEffect(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		IReadOnlySet<int>? extraTableCardIndexes = null)
	{
		if (!IsValidCaptureStart(tableCards, captureStartIndex))
		{
			return false;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			if (!tableCards[tableCardIndex].IsBuriedInSand && HasDoubleScoreEffect(tableCards[tableCardIndex]))
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
				&& tableCardIndex < tableCards.Count
				&& !tableCards[tableCardIndex].IsBuriedInSand
				&& HasDoubleScoreEffect(tableCards[tableCardIndex]))
			{
				return true;
			}
		}

		return false;
	}

	internal static int GetCapturedTableScorePlayerIndex(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		int fallbackPlayerIndex,
		int playerCount)
	{
		if (!IsValidCaptureStart(tableCards, captureStartIndex))
		{
			return fallbackPlayerIndex;
		}

		for (int tableCardIndex = tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			CaptureScoringTableCard tableCard = tableCards[tableCardIndex];
			if (!tableCard.HasPairDestroyItemEffect)
			{
				continue;
			}

			if (tableCard.ItemOwnerPlayerIndex >= 0 && tableCard.ItemOwnerPlayerIndex < playerCount)
			{
				return tableCard.ItemOwnerPlayerIndex;
			}
		}

		return fallbackPlayerIndex;
	}

	internal static bool IsCapturedTableCardScoringIndex(int tableCardIndex, int captureStartIndex, int tableCardCount)
	{
		return tableCardIndex > captureStartIndex && tableCardIndex < tableCardCount - 1;
	}

	internal static bool HasCapturedScoringTableCards(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		IReadOnlySet<int>? extraTableCardIndexes = null)
	{
		if (!IsValidCaptureStart(tableCards, captureStartIndex) || !HasCapturedMiddleTableCards(tableCards, captureStartIndex))
		{
			return false;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			if (IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex, tableCards.Count)
				&& IsScoringTableCard(tableCards[tableCardIndex]))
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
				&& tableCardIndex < tableCards.Count
				&& IsScoringTableCard(tableCards[tableCardIndex]))
			{
				return true;
			}
		}

		return false;
	}

	internal static bool HasCapturedMiddleTableCards(IReadOnlyList<CaptureScoringTableCard> tableCards, int captureStartIndex)
	{
		if (!IsValidCaptureStart(tableCards, captureStartIndex))
		{
			return false;
		}

		int capturedVisibleCardCount = 0;
		for (int tableCardIndex = captureStartIndex; tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			if (tableCards[tableCardIndex].IsBuriedInSand)
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

	internal static bool IsScoringTableCard(CaptureScoringTableCard tableCard)
	{
		return !tableCard.IsBuriedInSand
			&& tableCard.Card.HasValue
			&& tableCard.Card.Value.ScoreValue > 0;
	}

	private static bool IsValidCaptureStart(IReadOnlyList<CaptureScoringTableCard> tableCards, int captureStartIndex)
	{
		return captureStartIndex >= 0 && captureStartIndex < tableCards.Count;
	}

	private static bool HasDoubleScoreEffect(CaptureScoringTableCard tableCard)
	{
		return (tableCard.Card.HasValue && tableCard.Card.Value.HasDoubleCollectEffect)
			|| tableCard.HasDoubleCollectItemEffect;
	}

	internal static bool IsSlimeScoreSuppressed(bool isSlimeCoated, CaptureScoringContext context)
	{
		return context.SuppressSlimeCoatedScore && isSlimeCoated;
	}
}
