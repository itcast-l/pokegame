using System;
using System.Threading.Tasks;

internal enum AdventureSaveGateState
{
	Idle,
	Saving,
	Retrying,
	Aborting,
}

internal readonly record struct AdventureSaveGateResult<T>(bool Succeeded, T Result);

internal sealed class AdventureSaveGateCoordinator
{
	private static readonly double[] RetryDelays = { 0.5, 1.0, 2.0 };
	private const string SavingStatusKey = "GAME_ADVENTURE_SAVING";
	private const string RetryingStatusKey = "GAME_ADVENTURE_SAVE_RETRYING";
	private const string FailedStatusKey = "GAME_ADVENTURE_SAVE_FAILED_REPLAY";
	private const string ReturningStatusKey = "GAME_ADVENTURE_RETURNING_TO_MENU";

	private readonly Func<bool> _isStopping;
	private readonly Func<double, Task> _delayAsync;
	private readonly Action<string> _presentStatus;
	private readonly Action _onSucceeded;
	private readonly Action _onAbortStarted;
	private readonly Func<Task> _returnToMenuAsync;

	internal AdventureSaveGateCoordinator(
		Func<bool> isStopping,
		Func<double, Task> delayAsync,
		Action<string> presentStatus,
		Action onSucceeded,
		Action onAbortStarted,
		Func<Task> returnToMenuAsync)
	{
		_isStopping = isStopping;
		_delayAsync = delayAsync;
		_presentStatus = presentStatus;
		_onSucceeded = onSucceeded;
		_onAbortStarted = onAbortStarted;
		_returnToMenuAsync = returnToMenuAsync;
	}

	internal AdventureSaveGateState State { get; private set; }
	internal bool IsBlocking => State != AdventureSaveGateState.Idle;
	internal bool IsInProgress => IsBlocking;
	internal int RetryCount { get; private set; }
	internal string StatusLocalizationKey { get; private set; } = string.Empty;

	internal Task<AdventureSaveGateResult<T>> ExecuteAsync<T>(
		Func<Task<T>> attemptAsync,
		Func<T, bool> succeeded,
		Func<T, bool> retryable,
		Action? abortOperation = null,
		Func<bool>? onRetryableFailure = null,
		Func<bool>? onSucceeded = null,
		Action<Exception>? reportPresentationException = null,
		Action? reportPresentationFailure = null)
	{
		return ExecuteCoreAsync(
			attemptAsync,
			succeeded,
			retryable,
			abortOperation,
			onRetryableFailure,
			onSucceeded,
			attemptExceptionResult: null,
			reportAttemptException: null,
			reportPresentationException,
			reportPresentationFailure);
	}

	internal async Task<AdventureSaveGateResult<T>> ExecutePreparedAsync<T>(
		AdventureRunSessionStore.PreparedAdventureOperation<T> operation,
		Func<T, bool> succeeded,
		Func<T, bool> retryable,
		Func<bool>? onRetryableFailure = null,
		Func<bool>? onSucceeded = null,
		Action<Exception>? reportAttemptException = null,
		Action<Exception>? reportPresentationException = null,
		Action? reportPresentationFailure = null)
	{
		if (!operation.IsPrepared)
		{
			T initialResult = operation.InitialResult;
			if (succeeded(initialResult))
			{
				return new AdventureSaveGateResult<T>(true, initialResult);
			}
			if (!IsBlocking && !_isStopping())
			{
				await AbortAsync();
			}
			return new AdventureSaveGateResult<T>(false, initialResult);
		}
		if (IsBlocking || _isStopping())
		{
			operation.Abort();
			return new AdventureSaveGateResult<T>(false, operation.InitialResult);
		}

		return await ExecuteCoreAsync(
			operation.PersistAttemptAsync,
			succeeded,
			retryable,
			operation.Abort,
			onRetryableFailure,
			onSucceeded,
			_ => operation.PersistenceFailureResult,
			reportAttemptException,
			reportPresentationException,
			reportPresentationFailure);
	}

	private async Task<AdventureSaveGateResult<T>> ExecuteCoreAsync<T>(
		Func<Task<T>> attemptAsync,
		Func<T, bool> succeeded,
		Func<T, bool> retryable,
		Action? abortOperation,
		Func<bool>? onRetryableFailure,
		Func<bool>? onSucceeded,
		Func<Exception, T>? attemptExceptionResult,
		Action<Exception>? reportAttemptException,
		Action<Exception>? reportPresentationException,
		Action? reportPresentationFailure)
	{
		if (IsBlocking || _isStopping())
		{
			abortOperation?.Invoke();
			return new AdventureSaveGateResult<T>(false, default!);
		}

		BeginSaving();
		T result = default!;
		for (int attemptIndex = 0; attemptIndex <= RetryDelays.Length; attemptIndex++)
		{
			if (_isStopping())
			{
				abortOperation?.Invoke();
				FinishStopped();
				return new AdventureSaveGateResult<T>(false, result);
			}

			try
			{
				result = await attemptAsync();
			}
			catch (Exception error) when (attemptExceptionResult != null)
			{
				reportAttemptException?.Invoke(error);
				abortOperation?.Invoke();
				result = attemptExceptionResult(error);
				await AbortAsync();
				return new AdventureSaveGateResult<T>(false, result);
			}

			if (succeeded(result))
			{
				bool presentationSucceeded = true;
				try
				{
					if (!_isStopping() && onSucceeded != null)
					{
						presentationSucceeded = onSucceeded();
					}
				}
				catch (Exception error)
				{
					presentationSucceeded = false;
					reportPresentationException?.Invoke(error);
				}

				FinishSucceeded();
				if (!presentationSucceeded)
				{
					reportPresentationFailure?.Invoke();
					await AbortAsync();
					return new AdventureSaveGateResult<T>(false, result);
				}
				return new AdventureSaveGateResult<T>(true, result);
			}

			if (!retryable(result))
			{
				abortOperation?.Invoke();
				await AbortAsync();
				return new AdventureSaveGateResult<T>(false, result);
			}
			if (onRetryableFailure != null && !onRetryableFailure())
			{
				abortOperation?.Invoke();
				await AbortAsync();
				return new AdventureSaveGateResult<T>(false, result);
			}
			if (attemptIndex < RetryDelays.Length)
			{
				RetryCount++;
				State = AdventureSaveGateState.Retrying;
				SetStatus(RetryingStatusKey);
				await _delayAsync(RetryDelays[attemptIndex]);
				State = AdventureSaveGateState.Saving;
			}
		}

		abortOperation?.Invoke();
		await AbortAsync();
		return new AdventureSaveGateResult<T>(false, result);
	}

	private void BeginSaving()
	{
		State = AdventureSaveGateState.Saving;
		RetryCount = 0;
		SetStatus(SavingStatusKey);
	}

	private void FinishSucceeded()
	{
		State = AdventureSaveGateState.Idle;
		StatusLocalizationKey = string.Empty;
		_onSucceeded();
	}

	private void FinishStopped()
	{
		State = AdventureSaveGateState.Idle;
	}

	private async Task AbortAsync()
	{
		State = AdventureSaveGateState.Aborting;
		_onAbortStarted();
		SetStatus(FailedStatusKey);
		await _delayAsync(0.75);
		if (_isStopping())
		{
			return;
		}

		SetStatus(ReturningStatusKey);
		await _delayAsync(0.75);
		if (!_isStopping())
		{
			await _returnToMenuAsync();
		}
	}

	private void SetStatus(string localizationKey)
	{
		StatusLocalizationKey = localizationKey;
		_presentStatus(localizationKey);
	}
}
