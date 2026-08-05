using System;
using System.Collections.Generic;

internal static class PotentialCaptureRules
{
	internal static bool TryPickBestCaptureCardIndex(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards,
		CaptureScoringContext context,
		int royalCaptureThreshold,
		out int handIndex)
	{
		handIndex = -1;
		int bestScore = 0;
		int bestCaptureCount = 0;
		for (int index = 0; index < handCards.Count; index++)
		{
			PotentialCaptureCard card = handCards[index];
			int captureCount = GetPotentialNormalCaptureCount(tableCards, card);
			if (captureCount <= 0)
			{
				continue;
			}

			int score = GetPotentialNormalCapturePoints(tableCards, card, context);
			if (captureCount >= royalCaptureThreshold)
			{
				score += 1000 + captureCount;
			}

			if (score > bestScore || (score == bestScore && captureCount > bestCaptureCount))
			{
				bestScore = score;
				bestCaptureCount = captureCount;
				handIndex = index;
			}
		}

		return handIndex >= 0;
	}

	internal static bool IsPairOnlyCapture(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		PotentialCaptureCard card)
	{
		if (card.CollectsTable)
		{
			return false;
		}

		int matchingIndex = FindMatchingTableCardIndex(tableCards, card);
		return matchingIndex >= 0 && !HasPotentialCapturedMiddleTableCards(tableCards, matchingIndex);
	}

	internal static int GetPotentialNormalCaptureCount(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		PotentialCaptureCard card)
	{
		if (card.CollectsTable)
		{
			return tableCards.Count > 0 ? CountCollectibleTableCards(tableCards) + 1 : 0;
		}

		int matchingIndex = FindMatchingTableCardIndex(tableCards, card);
		return matchingIndex >= 0 ? CountCollectibleTableCards(tableCards, matchingIndex) + 1 : 0;
	}

	internal static int GetPotentialNormalCapturePoints(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		PotentialCaptureCard card,
		CaptureScoringContext context)
	{
		int matchingIndex = card.CollectsTable && tableCards.Count > 0
			? 0
			: FindMatchingTableCardIndex(tableCards, card);
		if (matchingIndex < 0 || !HasPotentialCapturedMiddleTableCards(tableCards, matchingIndex))
		{
			return 0;
		}

		int points = 0;
		for (int tableCardIndex = matchingIndex + 1; tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			CaptureScoringTableCard tableCard = tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue)
			{
				points += CaptureScoringRules.ApplyCapturedCardScoreModifiers(
					tableCard.Card.Value.ScoreValue,
					context,
					card.HasDoubleEffect);
			}
		}

		return points;
	}

	internal static bool HasPotentialCapturedMiddleTableCards(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex)
	{
		return CountCollectibleTableCards(tableCards, captureStartIndex) + 1 > 2;
	}

	internal static int GetBestPotentialNormalCaptureCount(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards)
	{
		if (tableCards.Count == 0)
		{
			return 0;
		}

		int bestCaptureCount = 0;
		foreach (PotentialCaptureCard handCard in handCards)
		{
			bestCaptureCount = Math.Max(bestCaptureCount, GetPotentialNormalCaptureCount(tableCards, handCard));
		}

		return bestCaptureCount;
	}

	private static int CountCollectibleTableCards(IReadOnlyList<CaptureScoringTableCard> tableCards, int startIndex = 0)
	{
		int count = 0;
		for (int tableCardIndex = Math.Max(0, startIndex); tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			if (!tableCards[tableCardIndex].IsBuriedInSand)
			{
				count++;
			}
		}

		return count;
	}

	private static int FindMatchingTableCardIndex(IReadOnlyList<CaptureScoringTableCard> tableCards, PotentialCaptureCard playedCard)
	{
		if (playedCard.CollectBySuit && !string.IsNullOrWhiteSpace(playedCard.Suit))
		{
			for (int tableCardIndex = 0; tableCardIndex < tableCards.Count; tableCardIndex++)
			{
				CaptureScoringTableCard tableCard = tableCards[tableCardIndex];
				CaptureScoringCard? tableCardData = tableCard.Card;
				if (!tableCard.IsBuriedInSand
					&& tableCardData.HasValue
					&& string.Equals(tableCardData.Value.Suit, playedCard.Suit, StringComparison.Ordinal))
				{
					return tableCardIndex;
				}
			}
		}

		for (int tableCardIndex = tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			CaptureScoringCard? tableCard = tableCards[tableCardIndex].Card;
			if (!tableCards[tableCardIndex].IsBuriedInSand && tableCard.HasValue && tableCard.Value.Rank == playedCard.Rank)
			{
				return tableCardIndex;
			}
		}

		if (playedCard.AlternateRanks != null)
		{
			foreach (string alternateRank in playedCard.AlternateRanks)
			{
				if (string.IsNullOrWhiteSpace(alternateRank) || alternateRank == playedCard.Rank)
				{
					continue;
				}

				for (int tableCardIndex = tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
				{
					CaptureScoringCard? tableCard = tableCards[tableCardIndex].Card;
					if (!tableCards[tableCardIndex].IsBuriedInSand && tableCard.HasValue && tableCard.Value.Rank == alternateRank)
					{
						return tableCardIndex;
					}
				}
			}
		}

		return -1;
	}
}
