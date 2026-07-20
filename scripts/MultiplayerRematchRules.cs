using System;
using System.Collections.Generic;
using System.Linq;

internal static class MultiplayerRematchRules
{
	internal static bool CanIssueReturnRequest(
		NetworkSessionState state,
		NetworkRpcContext context,
		bool hasWinner)
	{
		return state == NetworkSessionState.InGame
			&& hasWinner
			&& context.IsValid
			&& !string.IsNullOrWhiteSpace(context.MatchId);
	}

	internal static bool CanHostAcceptReturnRequest(
		NetworkSessionState state,
		NetworkRpcContext incomingContext,
		NetworkSessionConfig currentConfig,
		long currentAuthorityEpoch,
		IReadOnlyList<NetworkPlayerPublicState> players,
		int requesterPeerId,
		bool hasWinner)
	{
		return requesterPeerId > 0
			&& CanIssueReturnRequest(state, incomingContext, hasWinner)
			&& NetworkProtocolRules.Matches(
				incomingContext,
				currentConfig.SessionId,
				currentConfig.MatchId,
				currentAuthorityEpoch,
				allowHigherEpoch: false)
			&& players.Any(player =>
				player.PeerId == requesterPeerId
					&& player.IsConnected);
	}

	internal static NetworkSessionConfig CreateReturnedLobbyConfig(NetworkSessionConfig currentConfig)
	{
		return currentConfig with
		{
			MatchId = string.Empty,
			MatchSeed = 0,
		};
	}

	internal static List<NetworkPlayerPublicState> RetainConnectedPlayers(
		IReadOnlyList<NetworkPlayerPublicState> players)
	{
		return players
			.Where(player => player.IsConnected)
			.OrderBy(player => player.SeatIndex)
			.Select((player, seatIndex) => player with { SeatIndex = seatIndex })
			.ToList();
	}

	internal static bool IsValidAuthorityReturn(
		NetworkSessionConfig completedMatchConfig,
		NetworkSessionConfig returnedLobbyConfig,
		IReadOnlyList<NetworkPlayerPublicState> returnedPlayers)
	{
		if (!string.Equals(returnedLobbyConfig.SessionId, completedMatchConfig.SessionId, StringComparison.Ordinal)
			|| !string.Equals(returnedLobbyConfig.RoomName, completedMatchConfig.RoomName, StringComparison.Ordinal)
			|| !string.Equals(returnedLobbyConfig.RoomPassword, completedMatchConfig.RoomPassword, StringComparison.Ordinal)
			|| !string.IsNullOrEmpty(returnedLobbyConfig.MatchId)
			|| returnedLobbyConfig.MatchSeed != 0
			|| returnedLobbyConfig.Transport != completedMatchConfig.Transport
			|| returnedLobbyConfig.PlayerCount != completedMatchConfig.PlayerCount
			|| returnedLobbyConfig.ItemMode != completedMatchConfig.ItemMode
			|| returnedLobbyConfig.WinningScore != completedMatchConfig.WinningScore
			|| returnedLobbyConfig.LobbyVisibility != completedMatchConfig.LobbyVisibility
			|| returnedPlayers.Count == 0
			|| returnedPlayers.Count > returnedLobbyConfig.PlayerCount
			|| returnedPlayers.Any(player =>
				!player.IsConnected
					|| player.PeerId <= 0
					|| player.SeatIndex < 0
					|| player.SeatIndex >= returnedLobbyConfig.PlayerCount
					|| string.IsNullOrWhiteSpace(player.PlayerSessionId)))
		{
			return false;
		}

		return returnedPlayers.Select(player => player.PeerId).Distinct().Count() == returnedPlayers.Count
			&& returnedPlayers.Select(player => player.SeatIndex).Distinct().Count() == returnedPlayers.Count
			&& returnedPlayers.Count(player => player.IsHost) == 1;
	}
}
