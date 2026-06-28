using System;
using System.Collections.Generic;

internal static class CaptureScoringRules
{
	internal readonly record struct Card(
		string Rank,
		int ScoreValue,
		bool HasDoubleCollectEffect,
		string Suit = "");

	internal readonly record struct TableCard(
		Card? Card,
		bool IsBuriedInSand,
		bool IsSlimeCoated,
		bool HasDoubleCollectItemEffect,
		bool HasPairDestroyItemEffect,
		int ItemOwnerPlayerIndex);

	internal readonly record struct Context(
		bool SuppressSlimeCoatedScore,
		bool ApplyScoreReductionPerCard,
		int ScoreReductionPerCard);

	internal static int GetCapturedTablePoints(
		IReadOnlyList<TableCard> tableCards,
		int captureStartIndex,
		Context context,
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
			TableCard tableCard = tableCards[tableCardIndex];
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

				TableCard tableCard = tableCards[tableCardIndex];
				if (!tableCard.IsBuriedInSand)
				{
					collectedPoints += GetCapturedTableCardScoreValue(tableCard, context, hasDoubleEffect);
				}
			}
		}

		return collectedPoints;
	}

	internal static int GetCapturedTableCardScoreValue(TableCard tableCard, Context context, bool hasDoubleEffect)
	{
		if (!tableCard.Card.HasValue || IsSlimeScoreSuppressed(tableCard.IsSlimeCoated, context))
		{
			return 0;
		}

		return ApplyCapturedCardScoreModifiers(tableCard.Card.Value.ScoreValue, context, hasDoubleEffect);
	}

	internal static int ApplyCapturedCardScoreModifiers(int points, Context context, bool hasDoubleEffect)
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
		IReadOnlyList<TableCard> tableCards,
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
		IReadOnlyList<TableCard> tableCards,
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
			TableCard tableCard = tableCards[tableCardIndex];
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
		IReadOnlyList<TableCard> tableCards,
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

	internal static bool HasCapturedMiddleTableCards(IReadOnlyList<TableCard> tableCards, int captureStartIndex)
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

	internal static bool IsScoringTableCard(TableCard tableCard)
	{
		return !tableCard.IsBuriedInSand
			&& tableCard.Card.HasValue
			&& tableCard.Card.Value.ScoreValue > 0;
	}

	private static bool IsValidCaptureStart(IReadOnlyList<TableCard> tableCards, int captureStartIndex)
	{
		return captureStartIndex >= 0 && captureStartIndex < tableCards.Count;
	}

	private static bool HasDoubleScoreEffect(TableCard tableCard)
	{
		return (tableCard.Card.HasValue && tableCard.Card.Value.HasDoubleCollectEffect)
			|| tableCard.HasDoubleCollectItemEffect;
	}

	internal static bool IsSlimeScoreSuppressed(bool isSlimeCoated, Context context)
	{
		return context.SuppressSlimeCoatedScore && isSlimeCoated;
	}
}
