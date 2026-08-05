using System;

internal sealed class GameBoardLifecycleCoordinator
{
	private bool _started;
	private bool _stopped;

	internal void Start(
		Action createRuleSession,
		Action bindNodes,
		Action createControllers,
		Action registerNetworkEndpoint,
		Action startMatch)
	{
		ArgumentNullException.ThrowIfNull(createRuleSession);
		ArgumentNullException.ThrowIfNull(bindNodes);
		ArgumentNullException.ThrowIfNull(createControllers);
		ArgumentNullException.ThrowIfNull(registerNetworkEndpoint);
		ArgumentNullException.ThrowIfNull(startMatch);
		if (_started || _stopped)
		{
			throw new InvalidOperationException("The board lifecycle can only be started once.");
		}

		_started = true;
		createRuleSession();
		bindNodes();
		createControllers();
		registerNetworkEndpoint();
		startMatch();
	}

	internal void Stop(
		Action stopInputAndNetworkRequests,
		Action unregisterNetworkEndpoint,
		Action cancelAsyncLifetime,
		Action releaseControllers,
		Action clearPresentationState)
	{
		ArgumentNullException.ThrowIfNull(stopInputAndNetworkRequests);
		ArgumentNullException.ThrowIfNull(unregisterNetworkEndpoint);
		ArgumentNullException.ThrowIfNull(cancelAsyncLifetime);
		ArgumentNullException.ThrowIfNull(releaseControllers);
		ArgumentNullException.ThrowIfNull(clearPresentationState);
		if (_stopped)
		{
			return;
		}

		_stopped = true;
		stopInputAndNetworkRequests();
		unregisterNetworkEndpoint();
		cancelAsyncLifetime();
		releaseControllers();
		clearPresentationState();
	}
}
