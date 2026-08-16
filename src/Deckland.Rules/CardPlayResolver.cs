using System;
using System.Collections.Generic;

public readonly record struct CardPlayCard(
	string Rank,
	bool CollectsTable,
	string Suit = "",
	bool CollectBySuit = false,
	bool SuitMatchIsFallbackOnly = false);

public readonly record struct CardPlayTableCard(
	string? Rank,
	bool IsFaceUp,
	bool IsBuriedInSand,
	string? Suit = null,
	bool CountsTowardBasePlayCaptureThreshold = true);

public readonly record struct CardPlayPlan(
	int MatchingTableCardIndex,
	bool PlayedCardCollectsTable,
	int CaptureStartIndex,
	bool MatchedTableCardFlipped,
	int TableInsertIndex,
	string EffectiveRank,
	bool PlayedCardFacesUp,
	bool CaptureThresholdBlocked = false,
	int NormalTableCaptureCount = 0,
	bool CaptureThresholdBlockedByHiddenMatch = false);

internal static class CardPlayResolver
{
	public static CardPlayPlan Resolve(IReadOnlyList<CardPlayTableCard> tableCards, CardPlayCard playedCard)
	{
		return Resolve(tableCards, playedCard, null, insertAtTableStart: false);
	}

	public static CardPlayPlan Resolve(
		IReadOnlyList<CardPlayTableCard> tableCards,
		CardPlayCard playedCard,
		IReadOnlyList<string>? alternateRanks,
		bool insertAtTableStart,
		int tableInsertIndexOverride = -1,
		bool playedCardFacesUp = false,
		int basePlayCaptureThreshold = 0)
	{
		string effectiveRank = playedCard.Rank;
		int matchingIndex = FindMatchingTableCardIndex(tableCards, playedCard.Rank);
		if (matchingIndex < 0 && alternateRanks != null)
		{
			foreach (string alternateRank in alternateRanks)
			{
				if (string.IsNullOrWhiteSpace(alternateRank) || alternateRank == playedCard.Rank)
				{
					continue;
				}

				int alternateMatchIndex = FindMatchingTableCardIndex(tableCards, alternateRank);
				if (alternateMatchIndex >= 0)
				{
					effectiveRank = alternateRank;
					matchingIndex = alternateMatchIndex;
					break;
				}
			}
		}

		if (playedCard.CollectBySuit
			&& !string.IsNullOrWhiteSpace(playedCard.Suit)
			&& (!playedCard.SuitMatchIsFallbackOnly || matchingIndex < 0))
		{
			int suitMatchingIndex = FindFirstMatchingSuitTableCardIndex(tableCards, playedCard.Suit);
			if (suitMatchingIndex >= 0)
			{
				matchingIndex = suitMatchingIndex;
			}
		}

		bool playedCardCollectsTable = playedCard.CollectsTable && tableCards.Count > 0;
		int captureStartIndex = playedCardCollectsTable ? 0 : matchingIndex;
		bool matchedTableCardFlipped = matchingIndex >= 0 && !tableCards[matchingIndex].IsFaceUp;
		int normalTableCaptureCount = CountNormalTableCaptureCards(tableCards, captureStartIndex);
		bool captureThresholdBlocked = captureStartIndex >= 0
			&& !ContinuousCaptureRules.MeetsThreshold(
				normalTableCaptureCount,
				basePlayCaptureThreshold);
		bool captureThresholdBlockedByHiddenMatch = captureThresholdBlocked
			&& !playedCardCollectsTable
			&& matchingIndex >= 0
			&& !tableCards[matchingIndex].IsFaceUp;
		if (captureThresholdBlocked)
		{
			matchingIndex = -1;
			playedCardCollectsTable = false;
			captureStartIndex = -1;
			matchedTableCardFlipped = false;
			effectiveRank = playedCard.Rank;
		}
		int tableInsertIndex = tableInsertIndexOverride >= 0
			? System.Math.Min(tableInsertIndexOverride, tableCards.Count)
			: (insertAtTableStart ? 0 : tableCards.Count);
		return new CardPlayPlan(
			matchingIndex,
			playedCardCollectsTable,
			captureStartIndex,
			matchedTableCardFlipped,
			tableInsertIndex,
			effectiveRank,
			playedCardFacesUp,
			captureThresholdBlocked,
			normalTableCaptureCount,
			captureThresholdBlockedByHiddenMatch);
	}

	private static int CountNormalTableCaptureCards(
		IReadOnlyList<CardPlayTableCard> tableCards,
		int captureStartIndex)
	{
		if (captureStartIndex < 0)
		{
			return 0;
		}

		int count = 0;
		for (int index = captureStartIndex; index < tableCards.Count; index++)
		{
			CardPlayTableCard tableCard = tableCards[index];
			if (!tableCard.IsBuriedInSand
				&& tableCard.Rank != null
				&& tableCard.CountsTowardBasePlayCaptureThreshold)
			{
				count++;
			}
		}

		return count;
	}

	public static int FindMatchingTableCardIndex(IReadOnlyList<CardPlayTableCard> tableCards, string rank)
	{
		for (int tableCardIndex = tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			CardPlayTableCard tableCard = tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand && tableCard.Rank == rank)
			{
				return tableCardIndex;
			}
		}

		return -1;
	}

	public static int FindFirstMatchingSuitTableCardIndex(IReadOnlyList<CardPlayTableCard> tableCards, string suit)
	{
		for (int tableCardIndex = 0; tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			CardPlayTableCard tableCard = tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand
				&& string.Equals(tableCard.Suit, suit, StringComparison.Ordinal))
			{
				return tableCardIndex;
			}
		}

		return -1;
	}
}
