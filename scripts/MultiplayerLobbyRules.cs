using System.Collections.Generic;
using System.Linq;

public static class MultiplayerLobbyRules
{
	public static bool CanAcceptJoinRequest(NetworkSessionState state)
	{
		return state == NetworkSessionState.Lobby;
	}

	public static bool CanJoinListedRoom(string status)
	{
		return System.Enum.TryParse(status, ignoreCase: true, out NetworkSessionState state)
			&& state == NetworkSessionState.Lobby;
	}

	public static bool CanAutoAcceptSteamJoinRequest(NetworkSessionState state)
	{
		return state is NetworkSessionState.Idle
			or NetworkSessionState.Searching
			or NetworkSessionState.Failed;
	}

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
			if (player.PeerId == 0
				|| !player.IsConnected
				|| !player.CredentialsAcknowledged
				|| !player.IsReady)
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
