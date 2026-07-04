using System;
using System.Collections.Generic;

internal static class AiCardSelectionRules
{
	internal const int EarlyTableNewCardPriorityMaxTableCards = 4;

	internal static bool TryPickEarlyTableNewCardIndex(
		IReadOnlyList<CardPlayTableCard> tableCards,
		IReadOnlyList<CardPlayCard> handCards,
		int randomOrdinal,
		out int handIndex)
	{
		return TryPickEarlyTableNewCardIndex(
			tableCards.Count,
			handCards.Count,
			handCardIndex => IsNewTableCardCandidate(tableCards, handCards[handCardIndex]),
			randomOrdinal,
			out handIndex);
	}

	internal static bool TryPickEarlyTableNewCardIndex(
		int tableCardCount,
		int handCardCount,
		Func<int, bool> isNewTableCardCandidate,
		int randomOrdinal,
		out int handIndex)
	{
		handIndex = -1;
		if (tableCardCount > EarlyTableNewCardPriorityMaxTableCards || handCardCount <= 0)
		{
			return false;
		}

		int candidateCount = 0;
		for (int index = 0; index < handCardCount; index++)
		{
			if (isNewTableCardCandidate(index))
			{
				candidateCount++;
			}
		}

		if (candidateCount == 0)
		{
			return false;
		}

		int targetOrdinal = NormalizeOrdinal(randomOrdinal, candidateCount);
		int currentOrdinal = 0;
		for (int index = 0; index < handCardCount; index++)
		{
			if (!isNewTableCardCandidate(index))
			{
				continue;
			}

			if (currentOrdinal == targetOrdinal)
			{
				handIndex = index;
				return true;
			}

			currentOrdinal++;
		}

		return false;
	}

	internal static bool IsNewTableCardCandidate(
		IReadOnlyList<CardPlayTableCard> tableCards,
		CardPlayCard handCard)
	{
		CardPlayPlan plan = CardPlayResolver.Resolve(tableCards, handCard);
		return plan.MatchingTableCardIndex < 0 && !plan.PlayedCardCollectsTable;
	}

	private static int NormalizeOrdinal(int ordinal, int count)
	{
		if (count <= 0)
		{
			return -1;
		}

		int normalized = (ordinal & int.MaxValue) % count;
		return normalized < 0 ? normalized + count : normalized;
	}
}
