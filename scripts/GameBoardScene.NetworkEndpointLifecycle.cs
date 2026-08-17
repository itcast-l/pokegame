public partial class GameBoardScene
{
	private GameBoardNetworkEndpoint? _networkEndpoint;
	private NetworkMatchEndpointRegistrationToken _networkEndpointRegistrationToken;

	private void RegisterNetworkMatchEndpoint()
	{
		if (!IsNetworkMultiplayerMode() || GetNetworkManager() is not { } networkManager)
		{
			return;
		}

		_networkEndpoint = new GameBoardNetworkEndpoint(this);
		_networkEndpointRegistrationToken = networkManager.RegisterMatchEndpoint(
			network_session_id,
			network_match_id,
			_networkAuthorityEpoch,
			_networkEndpoint,
			_networkEndpoint,
			_networkEndpoint);
	}

	private void StopAndUnregisterNetworkMatchEndpoint()
	{
		_networkEndpoint?.Deactivate();
		if (_networkEndpointRegistrationToken.IsValid
			&& GetNetworkManager() is { } networkManager)
		{
			networkManager.UnregisterMatchEndpoint(_networkEndpointRegistrationToken);
		}
		_networkEndpointRegistrationToken = default;
		_networkEndpoint = null;
	}
}
