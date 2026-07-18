internal enum DirectItemHandCardSource
{
	Invalid,
	BaseHand,
	ItemHand,
}

internal static class DirectItemTargetRules
{
	internal static bool IsEligibleOpponent(
		int ownerPlayerIndex,
		int targetPlayerIndex,
		int playerCount,
		int availableCardCount)
	{
		return targetPlayerIndex >= 0
			&& targetPlayerIndex < playerCount
			&& targetPlayerIndex != ownerPlayerIndex
			&& availableCardCount > 0;
	}

	internal static DirectItemHandCardSource ResolveHandCardSource(
		int baseHandCount,
		int itemHandCount,
		int displayIndex)
	{
		if (displayIndex < 0 || displayIndex >= baseHandCount + itemHandCount)
		{
			return DirectItemHandCardSource.Invalid;
		}

		return displayIndex < baseHandCount
			? DirectItemHandCardSource.BaseHand
			: DirectItemHandCardSource.ItemHand;
	}

	internal static int GetDeckTransferCount(int availableCardCount, int requestedCardCount)
	{
		return System.Math.Min(
			System.Math.Max(availableCardCount, 0),
			System.Math.Max(requestedCardCount, 0));
	}
}
