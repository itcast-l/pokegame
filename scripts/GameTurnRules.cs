using System;

internal static class GameTurnRules
{
	internal readonly record struct PlayerCardState(
		int HandCardCount,
		int DeckCardCount,
		int ItemCardCount,
		bool HasPlayableItemCard)
	{
		public bool HasResources => HandCardCount > 0 || DeckCardCount > 0 || ItemCardCount > 0;
		public bool CanActNow => HandCardCount > 0 || HasPlayableItemCard;
	}

	internal static int GetNextPlayerIndex(
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

	internal static bool TryGetNextTurnPlayerIndex(
		ReadOnlySpan<int> turnOrder,
		ReadOnlySpan<PlayerCardState> playerStates,
		int currentPlayerIndex,
		int playerCount,
		out int nextPlayerIndex)
	{
		nextPlayerIndex = -1;
		int currentOrderIndex = IndexOf(turnOrder, currentPlayerIndex);
		if (currentOrderIndex < 0)
		{
			return false;
		}

		int inspectedSeats = 0;
		for (int offset = 1; offset <= turnOrder.Length && inspectedSeats < playerCount; offset++)
		{
			int candidatePlayerIndex = turnOrder[(currentOrderIndex + offset) % turnOrder.Length];
			if (candidatePlayerIndex < 0 || candidatePlayerIndex >= playerCount)
			{
				continue;
			}

			inspectedSeats++;
			if (candidatePlayerIndex < playerStates.Length
				&& playerStates[candidatePlayerIndex].CanActNow)
			{
				nextPlayerIndex = candidatePlayerIndex;
				return true;
			}
		}

		return false;
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
			if (!playerStates[playerIndex].HasResources)
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
