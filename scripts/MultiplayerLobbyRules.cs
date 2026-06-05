using System.Collections.Generic;
using System.Linq;

public static class MultiplayerLobbyRules
{
	public static bool CanStart(IReadOnlyList<NetworkPlayerPublicState> players, int playerCount)
	{
		int expectedPlayerCount = NetworkSessionDefaults.ClampPlayerCount(playerCount);
		if (players.Count != expectedPlayerCount)
		{
			return false;
		}

		for (int seatIndex = 0; seatIndex < expectedPlayerCount; seatIndex++)
		{
			NetworkPlayerPublicState player = players.FirstOrDefault(candidate => candidate.SeatIndex == seatIndex);
			if (player.PeerId == 0 || !player.IsConnected || !player.IsReady)
			{
				return false;
			}
		}

		return players.Any(player => player.IsHost && player.IsConnected);
	}

	public static int GetNextSeatIndex(IReadOnlyList<NetworkPlayerPublicState> players, int playerCount)
	{
		int expectedPlayerCount = NetworkSessionDefaults.ClampPlayerCount(playerCount);
		for (int seatIndex = 0; seatIndex < expectedPlayerCount; seatIndex++)
		{
			bool occupied = players.Any(player => player.SeatIndex == seatIndex);
			if (!occupied)
			{
				return seatIndex;
			}
		}

		return -1;
	}
}
