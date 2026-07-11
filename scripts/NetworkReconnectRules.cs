using System;

internal enum SteamLobbyJoinCallbackAction
{
	Ignore,
	LeaveUnexpectedLobby,
	RetryExpectedJoin,
	FailExpectedJoin,
	AcceptExpectedJoin,
}

internal static class NetworkReconnectRules
{
	private const int SteamChatRoomEnterResponseSuccess = 1;
	private const int SteamChatRoomEnterResponseError = 5;

	internal static bool ShouldSendReconnectCredentials(bool isHost, NetworkSessionState state)
	{
		return isHost
			&& (state == NetworkSessionState.Starting
				|| state == NetworkSessionState.LoadingScene
				|| state == NetworkSessionState.InGame);
	}

	internal static bool ShouldBroadcastHostEndedMatch(bool isHost, NetworkSessionState state)
	{
		return isHost && state == NetworkSessionState.InGame;
	}

	internal static bool ShouldHandOffDisconnectedSeatToAi(NetworkSessionState state, bool disconnectedPlayerIsHost)
	{
		return state == NetworkSessionState.InGame
			&& !disconnectedPlayerIsHost;
	}

	internal static bool ShouldAttemptReconnectAfterServerDisconnect(
		NetworkSessionState state,
		bool isHost,
		bool canAttemptReconnectClaim,
		bool hostEndedMatch)
	{
		return !hostEndedMatch
			&& state == NetworkSessionState.InGame
			&& !isHost
			&& canAttemptReconnectClaim;
	}

	internal static bool ShouldIgnoreLateServerDisconnectAfterReconnect(
		NetworkSessionState state,
		bool isHost,
		bool hasActivePeer,
		bool peerIsConnected)
	{
		return state == NetworkSessionState.InGame
			&& !isHost
			&& hasActivePeer
			&& peerIsConnected;
	}

	internal static bool ShouldDeferReconnectClaimWhileSeatStillConnected(
		SeatControlMode currentMode,
		int currentPeerId,
		int requestPeerId,
		string currentReconnectToken,
		string requestReconnectToken,
		string currentStableUserId,
		string requestStableUserId)
	{
		if (!string.Equals(currentReconnectToken, requestReconnectToken, System.StringComparison.Ordinal)
			|| !string.Equals(currentStableUserId, requestStableUserId, System.StringComparison.Ordinal))
		{
			return false;
		}

		if (currentMode == SeatControlMode.HumanConnected)
		{
			return true;
		}

		return currentPeerId > 0
			&& currentPeerId != requestPeerId
			&& currentMode == SeatControlMode.HumanReclaimPending;
	}

	internal static bool ShouldSendRealtimeBoardEventToPeer(
		bool isHost,
		NetworkSessionState state,
		bool playerIsConnected,
		bool playerIsHost)
	{
		return isHost
			&& state == NetworkSessionState.InGame
			&& playerIsConnected
			&& !playerIsHost;
	}

	internal static bool ShouldQueueRealtimeBoardEventForClient(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		NetworkSessionState networkState)
	{
		return isNetworkMultiplayer
			&& !isNetworkHost
			&& networkState == NetworkSessionState.InGame;
	}

	internal static bool ShouldApplyBoardSnapshotImmediatelyForClient(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		NetworkSessionState networkState)
	{
		return isNetworkMultiplayer
			&& !isNetworkHost
			&& networkState == NetworkSessionState.Reconnecting;
	}

	internal static bool ShouldCompleteReconnectCatchupForClient(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		NetworkSessionState networkState,
		bool isVisualQueueProcessing)
	{
		return isNetworkMultiplayer
			&& !isNetworkHost
			&& networkState == NetworkSessionState.Reconnecting
			&& !isVisualQueueProcessing;
	}

	internal static bool IsSteamLobbyJoinSuccessful(int response)
	{
		return response == SteamChatRoomEnterResponseSuccess;
	}

	internal static SteamLobbyJoinCallbackAction ClassifySteamLobbyJoinCallback(
		NetworkSessionState state,
		bool pendingSteamHostLobby,
		ulong expectedLobbyId,
		ulong callbackLobbyId,
		ulong acceptedLobbyId,
		int response)
	{
		// LobbyEnter can accompany lobby creation. With no join target, this
		// handler cannot distinguish it safely from an older join callback.
		if (pendingSteamHostLobby)
		{
			return SteamLobbyJoinCallbackAction.Ignore;
		}

		bool expected = (state is NetworkSessionState.Joining
				or NetworkSessionState.Reconnecting)
			&& expectedLobbyId != 0
			&& callbackLobbyId == expectedLobbyId;
		if (!expected)
		{
			if (IsSteamLobbyJoinSuccessful(response)
				&& callbackLobbyId != 0
				&& acceptedLobbyId == callbackLobbyId)
			{
				return SteamLobbyJoinCallbackAction.Ignore;
			}

			return IsSteamLobbyJoinSuccessful(response)
				&& callbackLobbyId != 0
					? SteamLobbyJoinCallbackAction.LeaveUnexpectedLobby
					: SteamLobbyJoinCallbackAction.Ignore;
		}

		if (IsSteamLobbyJoinSuccessful(response))
		{
			return SteamLobbyJoinCallbackAction.AcceptExpectedJoin;
		}

		return ShouldRetrySteamLobbyJoinAfterFailure(state, response)
			? SteamLobbyJoinCallbackAction.RetryExpectedJoin
			: SteamLobbyJoinCallbackAction.FailExpectedJoin;
	}

	internal static SteamLobbyJoinCallbackAction DispatchSteamLobbyJoinCallback(
		NetworkSessionState state,
		bool pendingSteamHostLobby,
		ulong expectedLobbyId,
		ulong callbackLobbyId,
		ulong acceptedLobbyId,
		int response,
		Action<ulong> leaveUnexpectedLobby,
		Action retryExpectedJoin,
		Action failExpectedJoin,
		Action acceptExpectedJoin)
	{
		SteamLobbyJoinCallbackAction action = ClassifySteamLobbyJoinCallback(
			state,
			pendingSteamHostLobby,
			expectedLobbyId,
			callbackLobbyId,
			acceptedLobbyId,
			response);
		switch (action)
		{
			case SteamLobbyJoinCallbackAction.LeaveUnexpectedLobby:
				leaveUnexpectedLobby(callbackLobbyId);
				break;
			case SteamLobbyJoinCallbackAction.RetryExpectedJoin:
				retryExpectedJoin();
				break;
			case SteamLobbyJoinCallbackAction.FailExpectedJoin:
				failExpectedJoin();
				break;
			case SteamLobbyJoinCallbackAction.AcceptExpectedJoin:
				acceptExpectedJoin();
				break;
		}

		return action;
	}

	internal static bool ShouldRetrySteamLobbyJoinAfterFailure(NetworkSessionState state, int response)
	{
		return state == NetworkSessionState.Reconnecting
			&& response == SteamChatRoomEnterResponseError;
	}

	internal static bool ShouldAbortSteamReconnectForLobbyOwnerChange(
		NetworkSessionState state,
		string originalHostStableUserId,
		ulong currentLobbyOwnerSteamId)
	{
		if (state != NetworkSessionState.Reconnecting
			|| currentLobbyOwnerSteamId == 0
			|| !TryParseSteamStableUserId(originalHostStableUserId, out ulong originalHostSteamId))
		{
			return false;
		}

		return currentLobbyOwnerSteamId != originalHostSteamId;
	}

	private static bool TryParseSteamStableUserId(string stableUserId, out ulong steamId)
	{
		steamId = 0;
		const string prefix = "steam:";
		if (string.IsNullOrWhiteSpace(stableUserId)
			|| !stableUserId.StartsWith(prefix, StringComparison.Ordinal)
			|| !ulong.TryParse(stableUserId[prefix.Length..], out ulong parsed))
		{
			return false;
		}

		steamId = parsed;
		return true;
	}
}
