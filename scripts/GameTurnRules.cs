using System;

internal static class GameTurnRules
{
	internal readonly record struct PlayerCardState(int HandCardCount, int DeckCardCount, bool HasPlayableItemCard)
	{
		public bool HasCards => HandCardCount > 0 || DeckCardCount > 0 || HasPlayableItemCard;
	}

	internal static int GetNextTurnPlayerIndex(
		ReadOnlySpan<int> turnOrder,
		int currentPlayerIndex,
		int playerCount,
		int fallbackPlayerIndex)
	{
		int currentOrderIndex = IndexOf(turnOrder, currentPlayerIndex);
		if (currentOrderIndex < 0)
		{
			return fallbackPlayerIndex;
		}

		for (int offset = 1; offset <= turnOrder.Length; offset++)
		{
			int nextPlayerIndex = turnOrder[(currentOrderIndex + offset) % turnOrder.Length];
			if (nextPlayerIndex >= 0 && nextPlayerIndex < playerCount)
			{
				return nextPlayerIndex;
			}
		}

		return fallbackPlayerIndex;
	}

	internal static bool TryGetSingleRemainingPlayer(
		ReadOnlySpan<PlayerCardState> playerStates,
		int playerCount,
		out int remainingPlayerIndex)
	{
		remainingPlayerIndex = -1;
		int checkedPlayerCount = Math.Min(playerCount, playerStates.Length);
		for (int playerIndex = 0; playerIndex < checkedPlayerCount; playerIndex++)
		{
			if (!playerStates[playerIndex].HasCards)
			{
				continue;
			}

			if (remainingPlayerIndex >= 0)
			{
				remainingPlayerIndex = -1;
				return false;
			}

			remainingPlayerIndex = playerIndex;
		}

		return remainingPlayerIndex >= 0;
	}

	private static int IndexOf(ReadOnlySpan<int> values, int value)
	{
		for (int index = 0; index < values.Length; index++)
		{
			if (values[index] == value)
			{
				return index;
			}
		}

		return -1;
	}
}
