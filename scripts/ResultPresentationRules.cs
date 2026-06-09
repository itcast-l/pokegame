internal static class ResultPresentationRules
{
	internal static bool ShouldShowDefeatResult(
		int winnerPlayerIndex,
		int localPlayerIndex,
		bool isNetworkMultiplayer,
		bool adventureMode,
		bool soloMode)
	{
		if (winnerPlayerIndex == localPlayerIndex)
		{
			return false;
		}

		return isNetworkMultiplayer || adventureMode || soloMode;
	}

	internal static bool ShouldEnableWinnerPrimaryAction(bool isNetworkMultiplayer, bool shouldAdvanceAdventure)
	{
		return shouldAdvanceAdventure || !isNetworkMultiplayer;
	}
}
