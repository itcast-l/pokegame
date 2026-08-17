using System;

internal readonly record struct NetworkReconnectContext(
	string SessionId,
	string MatchId,
	long AuthorityEpoch);

internal enum NetworkReconnectCoordinatorState
{
	Inactive,
	WaitingToRetry,
	AttemptInFlight,
	AwaitingConfirmation,
	AwaitingSnapshot,
	AttemptTimedOut,
	WindowExpired,
	Completed,
}

internal enum NetworkReconnectPollAction
{
	Inactive,
	Waiting,
	StartAttempt,
	AttemptTimedOut,
	WindowExpired,
}

internal enum NetworkReconnectTransitionResult
{
	Accepted,
	Inactive,
	StaleContext,
	InvalidState,
	Expired,
}

internal sealed class NetworkReconnectCoordinator
{
	internal const double RetryDelaySeconds = 2.0;
	internal const double WindowDurationSeconds = 10.0;

	private NetworkReconnectContext _context;
	private double _windowDeadlineSeconds;
	private double _retryDeadlineSeconds;
	private double _attemptStartedAtSeconds = -1.0;

	internal NetworkReconnectCoordinatorState State { get; private set; }
	internal NetworkReconnectContext Context => _context;
	internal bool IsActive => State is NetworkReconnectCoordinatorState.WaitingToRetry
		or NetworkReconnectCoordinatorState.AttemptInFlight
		or NetworkReconnectCoordinatorState.AwaitingConfirmation
		or NetworkReconnectCoordinatorState.AwaitingSnapshot;
	internal bool IsAttemptInFlight => State == NetworkReconnectCoordinatorState.AttemptInFlight;
	internal double? WindowDeadlineSeconds => IsActive ? _windowDeadlineSeconds : null;
	internal double? RetryDeadlineSeconds => State == NetworkReconnectCoordinatorState.WaitingToRetry
		? _retryDeadlineSeconds
		: null;
	internal double AttemptStartedAtSeconds => _attemptStartedAtSeconds;

	internal bool Begin(NetworkReconnectContext context, double nowSeconds)
	{
		if (IsActive)
		{
			return false;
		}

		_context = context;
		_windowDeadlineSeconds = nowSeconds + WindowDurationSeconds;
		_retryDeadlineSeconds = nowSeconds;
		_attemptStartedAtSeconds = -1.0;
		State = NetworkReconnectCoordinatorState.WaitingToRetry;
		return true;
	}

	internal NetworkReconnectPollAction Poll(
		double nowSeconds,
		bool peerBlocksAttempt,
		bool enableAttemptTimeout,
		double attemptTimeoutSeconds)
	{
		if (!IsActive)
		{
			return NetworkReconnectPollAction.Inactive;
		}
		if (nowSeconds >= _windowDeadlineSeconds)
		{
			State = NetworkReconnectCoordinatorState.WindowExpired;
			return NetworkReconnectPollAction.WindowExpired;
		}
		if (State == NetworkReconnectCoordinatorState.AttemptInFlight)
		{
			if (enableAttemptTimeout
				&& _attemptStartedAtSeconds >= 0.0
				&& nowSeconds - _attemptStartedAtSeconds >= attemptTimeoutSeconds)
			{
				State = NetworkReconnectCoordinatorState.AttemptTimedOut;
				return NetworkReconnectPollAction.AttemptTimedOut;
			}

			return NetworkReconnectPollAction.Waiting;
		}
		if (State is NetworkReconnectCoordinatorState.AwaitingConfirmation
			or NetworkReconnectCoordinatorState.AwaitingSnapshot
			|| peerBlocksAttempt
			|| nowSeconds < _retryDeadlineSeconds)
		{
			return NetworkReconnectPollAction.Waiting;
		}

		State = NetworkReconnectCoordinatorState.AttemptInFlight;
		_attemptStartedAtSeconds = nowSeconds;
		return NetworkReconnectPollAction.StartAttempt;
	}

	internal NetworkReconnectTransitionResult ScheduleRetry(
		NetworkReconnectContext context,
		double nowSeconds)
	{
		NetworkReconnectTransitionResult validation = ValidateContext(context, nowSeconds);
		if (validation != NetworkReconnectTransitionResult.Accepted)
		{
			return validation;
		}
		if (State is not (NetworkReconnectCoordinatorState.AttemptInFlight
			or NetworkReconnectCoordinatorState.AwaitingConfirmation
			or NetworkReconnectCoordinatorState.WaitingToRetry))
		{
			return NetworkReconnectTransitionResult.InvalidState;
		}

		State = NetworkReconnectCoordinatorState.WaitingToRetry;
		_retryDeadlineSeconds = nowSeconds + RetryDelaySeconds;
		_attemptStartedAtSeconds = -1.0;
		return NetworkReconnectTransitionResult.Accepted;
	}

	internal NetworkReconnectTransitionResult MarkAwaitingConfirmation(
		NetworkReconnectContext context,
		double nowSeconds)
	{
		NetworkReconnectTransitionResult validation = ValidateContext(context, nowSeconds);
		if (validation != NetworkReconnectTransitionResult.Accepted)
		{
			return validation;
		}
		if (State != NetworkReconnectCoordinatorState.AttemptInFlight)
		{
			return NetworkReconnectTransitionResult.InvalidState;
		}

		State = NetworkReconnectCoordinatorState.AwaitingConfirmation;
		_attemptStartedAtSeconds = -1.0;
		return NetworkReconnectTransitionResult.Accepted;
	}

	internal NetworkReconnectTransitionResult ReceiveConfirmation(
		NetworkReconnectContext context,
		bool accepted,
		double nowSeconds)
	{
		NetworkReconnectTransitionResult validation = ValidateContext(context, nowSeconds);
		if (validation != NetworkReconnectTransitionResult.Accepted)
		{
			return validation;
		}
		if (State != NetworkReconnectCoordinatorState.AwaitingConfirmation)
		{
			return NetworkReconnectTransitionResult.InvalidState;
		}

		State = accepted
			? NetworkReconnectCoordinatorState.AwaitingSnapshot
			: NetworkReconnectCoordinatorState.WaitingToRetry;
		_attemptStartedAtSeconds = -1.0;
		return NetworkReconnectTransitionResult.Accepted;
	}

	internal NetworkReconnectTransitionResult CompleteSnapshot(
		NetworkReconnectContext context,
		double nowSeconds)
	{
		NetworkReconnectTransitionResult validation = ValidateContext(context, nowSeconds);
		if (validation != NetworkReconnectTransitionResult.Accepted)
		{
			return validation;
		}
		if (State != NetworkReconnectCoordinatorState.AwaitingSnapshot)
		{
			return NetworkReconnectTransitionResult.InvalidState;
		}

		State = NetworkReconnectCoordinatorState.Completed;
		_attemptStartedAtSeconds = -1.0;
		return NetworkReconnectTransitionResult.Accepted;
	}

	internal void Reset()
	{
		_context = default;
		_windowDeadlineSeconds = 0.0;
		_retryDeadlineSeconds = 0.0;
		_attemptStartedAtSeconds = -1.0;
		State = NetworkReconnectCoordinatorState.Inactive;
	}

	private NetworkReconnectTransitionResult ValidateContext(
		NetworkReconnectContext context,
		double nowSeconds)
	{
		if (!IsActive)
		{
			return NetworkReconnectTransitionResult.Inactive;
		}
		if (_context != context)
		{
			return NetworkReconnectTransitionResult.StaleContext;
		}
		if (nowSeconds >= _windowDeadlineSeconds)
		{
			State = NetworkReconnectCoordinatorState.WindowExpired;
			return NetworkReconnectTransitionResult.Expired;
		}

		return NetworkReconnectTransitionResult.Accepted;
	}
}
