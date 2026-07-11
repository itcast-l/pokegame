public partial class NetworkManager
{
	public bool DebugIsLoadHandshakeTimeoutActive()
	{
		return _state == NetworkSessionState.LoadingScene
			&& _loadHandshake != null;
	}

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
