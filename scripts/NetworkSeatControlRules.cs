internal static class NetworkSeatControlRules
{
	internal static bool CanAcceptRemotePlayerAction(SeatControlMode mode)
	{
		return mode == SeatControlMode.HumanConnected;
	}

	internal static bool CanSendPendingSelectionRequest(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		NetworkSessionState networkState,
		bool requestInFlight)
	{
		return isNetworkMultiplayer
			&& !isNetworkHost
			&& networkState == NetworkSessionState.InGame
			&& !requestInFlight;
	}
}
