internal static class LocalSeatControlRules
{
	internal static bool IsLocalHumanInputEnabled(
		bool isNetworkMultiplayer,
		NetworkSessionState networkState,
		bool seatStateFound,
		SeatControlMode seatMode)
	{
		if (!isNetworkMultiplayer)
		{
			return true;
		}

		return networkState == NetworkSessionState.InGame
			&& seatStateFound
			&& seatMode == SeatControlMode.HumanConnected;
	}

	internal static bool ShouldBlockBoardControlInput(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		NetworkSessionState networkState,
		bool seatStateFound,
		SeatControlMode seatMode)
	{
		if (!isNetworkMultiplayer || isNetworkHost)
		{
			return false;
		}

		return !IsLocalHumanInputEnabled(
			isNetworkMultiplayer,
			networkState,
			seatStateFound,
			seatMode);
	}
}
