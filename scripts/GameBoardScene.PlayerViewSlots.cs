using Godot;

public partial class GameBoardScene
{
	private int GetPlayerViewSlotIndex(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= MaxPlayers || !IsNetworkMultiplayerMode())
		{
			return playerIndex;
		}

		int localSeatIndex = GetLocalHandPlayerIndex();
		if (localSeatIndex < 0 || localSeatIndex >= player_count)
		{
			return playerIndex;
		}

		int localOrderIndex = GetActiveTurnOrderIndex(localSeatIndex);
		int playerOrderIndex = GetActiveTurnOrderIndex(playerIndex);
		int activePlayerCount = GetActiveTurnOrderCount();
		if (localOrderIndex < 0 || playerOrderIndex < 0 || activePlayerCount <= 0)
		{
			return playerIndex;
		}

		int relativeOrderIndex = PositiveModulo(playerOrderIndex - localOrderIndex, activePlayerCount);
		return GetActiveViewSlotIndex(relativeOrderIndex);
	}

	private int GetActiveTurnOrderCount()
	{
		int count = 0;
		for (int orderIndex = 0; orderIndex < ClockwiseTurnOrder.Length; orderIndex++)
		{
			if (ClockwiseTurnOrder[orderIndex] < player_count)
			{
				count++;
			}
		}

		return count;
	}

	private int GetActiveTurnOrderIndex(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= player_count)
		{
			return -1;
		}

		int activeIndex = 0;
		for (int orderIndex = 0; orderIndex < ClockwiseTurnOrder.Length; orderIndex++)
		{
			int candidatePlayerIndex = ClockwiseTurnOrder[orderIndex];
			if (candidatePlayerIndex >= player_count)
			{
				continue;
			}

			if (candidatePlayerIndex == playerIndex)
			{
				return activeIndex;
			}

			activeIndex++;
		}

		return -1;
	}

	private int GetActiveViewSlotIndex(int activeViewSlotOrdinal)
	{
		int activeIndex = 0;
		for (int orderIndex = 0; orderIndex < ClockwiseTurnOrder.Length; orderIndex++)
		{
			int candidateViewSlotIndex = ClockwiseTurnOrder[orderIndex];
			if (candidateViewSlotIndex >= player_count)
			{
				continue;
			}

			if (activeIndex == activeViewSlotOrdinal)
			{
				return candidateViewSlotIndex;
			}

			activeIndex++;
		}

		return MainPlayerIndex;
	}

	public Control GetPlayerArea(int playerIndex)
	{
		return GetPlayerAreaForViewSlot(GetPlayerViewSlotIndex(playerIndex));
	}

	private Control GetPlayerAreaForViewSlot(int viewSlotIndex)
	{
		return viewSlotIndex switch
		{
			0 => _bottomPlayerArea,
			1 => _topPlayerArea,
			2 => _leftPlayerArea,
			3 => _rightPlayerArea,
			_ => HandleInvalidViewSlotIndex(),
		};
	}

	public Control get_player_area(int playerIndex)
	{
		return GetPlayerArea(playerIndex);
	}

	private Control HandleInvalidViewSlotIndex()
	{
		GD.PushError("Player view slot index must be between 0 and 3.");
		return _bottomPlayerArea;
	}
}
