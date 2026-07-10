using System;
using System.Collections.Generic;

public static class BorrowTideRules
{
	public static IReadOnlyList<string> GetAlternateRanks(bool cardHasBorrowTideEffect, string playedRank)
	{
		if (!cardHasBorrowTideEffect || !TryGetNumericRankValue(playedRank, out int rankValue))
		{
			return Array.Empty<string>();
		}

		var alternateRanks = new List<string>(2);
		if (rankValue > 1)
		{
			alternateRanks.Add((rankValue - 1).ToString());
		}

		if (rankValue < 10)
		{
			alternateRanks.Add((rankValue + 1).ToString());
		}

		return alternateRanks;
	}

	public static bool DidUseBorrowTide(bool cardHasBorrowTideEffect, string playedRank, string effectiveRank)
	{
		if (!cardHasBorrowTideEffect
			|| string.IsNullOrWhiteSpace(playedRank)
			|| string.IsNullOrWhiteSpace(effectiveRank)
			|| string.Equals(playedRank, effectiveRank, StringComparison.Ordinal))
		{
			return false;
		}

		foreach (string alternateRank in GetAlternateRanks(cardHasBorrowTideEffect, playedRank))
		{
			if (string.Equals(alternateRank, effectiveRank, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static bool TryGetNumericRankValue(string rank, out int rankValue)
	{
		return int.TryParse(rank, out rankValue) && rankValue >= 1 && rankValue <= 10;
	}
}
