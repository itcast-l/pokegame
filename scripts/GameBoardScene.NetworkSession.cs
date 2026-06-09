using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private void ConnectNetworkManagerSignalsForBoard()
	{
		if (_networkManagerSignalsConnected || !IsNetworkMultiplayerMode())
		{
			return;
		}

		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (networkManager == null)
		{
			return;
		}

		networkManager.ErrorRaised += HandleNetworkManagerBoardError;
		_networkManagerSignalsConnected = true;
	}

	private void DisconnectNetworkManagerSignalsForBoard()
	{
		if (!_networkManagerSignalsConnected)
		{
			return;
		}

		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (networkManager != null && GodotObject.IsInstanceValid(networkManager))
		{
			networkManager.ErrorRaised -= HandleNetworkManagerBoardError;
		}

		_networkManagerSignalsConnected = false;
	}

	private void HandleNetworkManagerBoardError(string message)
	{
		if (_isLeavingGame || !IsNetworkMultiplayerMode())
		{
			return;
		}

		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (networkManager == null)
		{
			return;
		}

		if (networkManager.State != NetworkSessionState.Failed)
		{
			ShowNetworkNotice(message);
			return;
		}

		_ = ExitNetworkMatchToMainMenuAsync();
	}

	private async Task ExitNetworkMatchToMainMenuAsync()
	{
		if (_isLeavingGame)
		{
			return;
		}

		await ReturnToMenuSceneAsync(MainMenuScenePath);
	}
}
