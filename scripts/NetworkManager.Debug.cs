public partial class NetworkManager
{
	public ulong DebugGetCurrentSteamLobbyId()
	{
		return _steamLobbyId;
	}

	public bool DebugSimulateLocalReconnectDrop()
	{
		if (IsHost
			|| _state != NetworkSessionState.InGame
			|| !CanAttemptReconnectClaim())
		{
			return false;
		}

		EnterReconnectState();
		return true;
	}
}
