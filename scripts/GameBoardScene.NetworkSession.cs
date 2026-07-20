using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private NetworkManager? _cachedNetworkManager;

	private NetworkManager? GetNetworkManager()
	{
		if (_cachedNetworkManager != null && GodotObject.IsInstanceValid(_cachedNetworkManager))
		{
			return _cachedNetworkManager;
		}

		_cachedNetworkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		return _cachedNetworkManager;
	}

	private void ConnectNetworkManagerSignalsForBoard()
	{
		if (_networkManagerSignalsConnected || !IsNetworkMultiplayerMode())
		{
			return;
		}

		NetworkManager? networkManager = GetNetworkManager();
		if (networkManager == null)
		{
			return;
		}

		networkManager.ErrorRaised += HandleNetworkManagerBoardError;
		networkManager.StateChanged += HandleNetworkManagerStateChanged;
		networkManager.SeatControlStatesUpdated += HandleNetworkManagerSeatControlStatesUpdated;
		_networkManagerSignalsConnected = true;
	}

	private void DisconnectNetworkManagerSignalsForBoard()
	{
		if (!_networkManagerSignalsConnected)
		{
			return;
		}

		NetworkManager? networkManager = GetNetworkManager();
		if (networkManager != null && GodotObject.IsInstanceValid(networkManager))
		{
			networkManager.ErrorRaised -= HandleNetworkManagerBoardError;
			networkManager.StateChanged -= HandleNetworkManagerStateChanged;
			networkManager.SeatControlStatesUpdated -= HandleNetworkManagerSeatControlStatesUpdated;
		}

		_networkManagerSignalsConnected = false;
	}

	private void HandleNetworkManagerBoardError(string message)
	{
		if (_isLeavingGame || !IsNetworkMultiplayerMode())
		{
			return;
		}

		NetworkManager? networkManager = GetNetworkManager();
		if (networkManager == null)
		{
			return;
		}

		if (networkManager.State != NetworkSessionState.Failed)
		{
			ShowNetworkNotice(message);
			return;
		}

		if (networkManager.IsMigrationFailureReturnPending)
		{
			ShowNetworkNotice(NetworkReconnectPresentationRules.GetMigrationFailureText(UsesChineseLocale()));
			return;
		}

		TrackBoardTask(ExitNetworkMatchToMainMenuAsync(), nameof(ExitNetworkMatchToMainMenuAsync));
	}

	private void HandleNetworkManagerSeatControlStatesUpdated()
	{
		MarkSeatControlIndicatorsDirty();
		MarkActivePlayerStatusTooltipForRefresh();
	}

	private void HandleNetworkManagerStateChanged(int state)
	{
		RefreshNetworkReconnectOverlayIfNeeded((NetworkSessionState)state);
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
