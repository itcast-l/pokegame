using System.Collections.Generic;

internal readonly record struct CardPlayCard(string Rank, bool CollectsTable);

internal readonly record struct CardPlayTableCard(string? Rank, bool IsFaceUp, bool IsBuriedInSand);

internal readonly record struct CardPlayPlan(
	int MatchingTableCardIndex,
	bool PlayedCardCollectsTable,
	int CaptureStartIndex,
	bool MatchedTableCardFlipped);

internal static class CardPlayResolver
{
	internal static CardPlayPlan Resolve(IReadOnlyList<CardPlayTableCard> tableCards, CardPlayCard playedCard)
	{
		int matchingIndex = FindMatchingTableCardIndex(tableCards, playedCard.Rank);
		bool playedCardCollectsTable = playedCard.CollectsTable && tableCards.Count > 0;
		int captureStartIndex = playedCardCollectsTable ? 0 : matchingIndex;
		bool matchedTableCardFlipped = matchingIndex >= 0 && !tableCards[matchingIndex].IsFaceUp;
		return new CardPlayPlan(
			matchingIndex,
			playedCardCollectsTable,
			captureStartIndex,
			matchedTableCardFlipped);
	}

	internal static int FindMatchingTableCardIndex(IReadOnlyList<CardPlayTableCard> tableCards, string rank)
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
}
