using System.Collections.Generic;

internal readonly record struct CardPlayCard(string Rank, bool CollectsTable);

internal readonly record struct CardPlayTableCard(string? Rank, bool IsFaceUp, bool IsBuriedInSand);

internal readonly record struct CardPlayPlan(
	int MatchingTableCardIndex,
	bool PlayedCardCollectsTable,
	int CaptureStartIndex,
	bool MatchedTableCardFlipped,
	int TableInsertIndex,
	string EffectiveRank);

internal static class CardPlayResolver
{
	internal static CardPlayPlan Resolve(IReadOnlyList<CardPlayTableCard> tableCards, CardPlayCard playedCard)
	{
		return Resolve(tableCards, playedCard, null, insertAtTableStart: false);
	}

	internal static CardPlayPlan Resolve(
		IReadOnlyList<CardPlayTableCard> tableCards,
		CardPlayCard playedCard,
		IReadOnlyList<string>? alternateRanks,
		bool insertAtTableStart)
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

		bool playedCardCollectsTable = playedCard.CollectsTable && tableCards.Count > 0;
		int captureStartIndex = playedCardCollectsTable ? 0 : matchingIndex;
		bool matchedTableCardFlipped = matchingIndex >= 0 && !tableCards[matchingIndex].IsFaceUp;
		int tableInsertIndex = insertAtTableStart ? 0 : tableCards.Count;
		return new CardPlayPlan(
			matchingIndex,
			playedCardCollectsTable,
			captureStartIndex,
			matchedTableCardFlipped,
			tableInsertIndex,
			effectiveRank);
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
