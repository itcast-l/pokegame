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
		if (isNetworkMultiplayer)
		{
			return true;
		}

		return shouldAdvanceAdventure || !isNetworkMultiplayer;
	}

	internal static bool ShouldEnableWinnerReturnAction(bool isAdventureSaveBlocking)
	{
		return !isAdventureSaveBlocking;
	}
}
