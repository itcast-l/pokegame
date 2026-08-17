using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

public static partial class GameSaveStore
{
	private const string AsyncSaveFinishedSignal = "async_save_finished";
	private const string AsyncFlushFinishedSignal = "async_flush_finished";
	private static long _nextAsyncRequestId;

	public static Error SetValueDeferred(string key, Variant value)
		=> GameSaveLegacyAdapter.SetValueDeferred(key, value);

	public static Error SetValuesDeferred(params SaveValue[] values)
		=> GameSaveLegacyAdapter.SetValuesDeferred(values);

	public static Task<Error> FlushDeferredValuesAsync()
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Task.FromResult(Error.Unavailable);
		}

		GameSaveMigration.EnsureLegacyMigration(saveManager);
		return QueueFlushAsync(saveManager, "flush_dirty_async");
	}

	public static Task<Error> SaveActiveProfileAsync(GameProgressData progressData)
	{
		return SaveActiveProfileThroughCoordinatorAsync(_ => progressData, forcePersist: true);
	}

	public static Task<Error> UpdateActiveProfileAsync(
		Func<GameProgressData, GameProgressData> update)
	{
		ArgumentNullException.ThrowIfNull(update);
		return SaveActiveProfileThroughCoordinatorAsync(update, forcePersist: false);
	}

	public static Task<Error> FlushPendingWritesAsync()
	{
		Node? saveManager = GetSaveManager();
		return saveManager == null
			? Task.FromResult(Error.Unavailable)
			: QueueFlushAsync(saveManager, "queue_flush_barrier");
	}

	public static void BeginShutdown()
	{
		GetSaveManager()?.Call("begin_async_shutdown");
	}

	internal static Task<Error> QueueSlotSaveAsync(
		Node saveManager,
		string slotId,
		Godot.Collections.Dictionary data)
	{
		ArgumentNullException.ThrowIfNull(saveManager);
		long requestId = NextAsyncRequestId();
		var completion = new TaskCompletionSource<Error>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		Callable callback = default;
		callback = Callable.From<long, StringName, long>((finishedRequestId, _, error) =>
		{
			if (finishedRequestId != requestId)
			{
				return;
			}

			try
			{
				DisconnectIfConnected(saveManager, AsyncSaveFinishedSignal, callback);
			}
			catch (Exception disconnectError)
			{
				GD.PushWarning($"Failed to disconnect async profile save callback: {disconnectError.Message}");
			}
			finally
			{
				completion.TrySetResult((Error)error);
			}
		});

		Error connectResult;
		try
		{
			connectResult = saveManager.Connect(AsyncSaveFinishedSignal, callback);
		}
		catch (Exception connectError)
		{
			TryDisconnect(saveManager, AsyncSaveFinishedSignal, callback, "async profile save");
			GD.PushWarning($"Failed to connect async profile save callback: {connectError.Message}");
			return Task.FromResult(Error.Failed);
		}
		if (connectResult != Error.Ok)
		{
			return Task.FromResult(connectResult);
		}

		Error queueResult;
		try
		{
			queueResult = (Error)saveManager.Call(
				"queue_save_to_slot",
				requestId,
				new StringName(slotId),
				data).AsInt32();
		}
		catch (Exception queueError)
		{
			TryDisconnect(saveManager, AsyncSaveFinishedSignal, callback, "async profile save");
			GD.PushWarning($"Failed to queue async profile save: {queueError.Message}");
			return Task.FromResult(Error.Failed);
		}
		if (queueResult != Error.Ok)
		{
			TryDisconnect(saveManager, AsyncSaveFinishedSignal, callback, "async profile save");
			return Task.FromResult(queueResult);
		}

		return completion.Task;
	}

	private static Task<Error> QueueFlushAsync(Node saveManager, string method)
	{
		long requestId = NextAsyncRequestId();
		var completion = new TaskCompletionSource<Error>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		Callable callback = default;
		callback = Callable.From<long, long>((finishedRequestId, error) =>
		{
			if (finishedRequestId != requestId)
			{
				return;
			}

			try
			{
				DisconnectIfConnected(saveManager, AsyncFlushFinishedSignal, callback);
			}
			catch (Exception disconnectError)
			{
				GD.PushWarning($"Failed to disconnect async flush callback: {disconnectError.Message}");
			}
			finally
			{
				completion.TrySetResult((Error)error);
			}
		});

		Error connectResult;
		try
		{
			connectResult = saveManager.Connect(AsyncFlushFinishedSignal, callback);
		}
		catch (Exception connectError)
		{
			TryDisconnect(saveManager, AsyncFlushFinishedSignal, callback, "async flush");
			GD.PushWarning($"Failed to connect async flush callback: {connectError.Message}");
			return Task.FromResult(Error.Failed);
		}
		if (connectResult != Error.Ok)
		{
			return Task.FromResult(connectResult);
		}

		Error queueResult;
		try
		{
			queueResult = (Error)saveManager.Call(method, requestId).AsInt32();
		}
		catch (Exception queueError)
		{
			TryDisconnect(saveManager, AsyncFlushFinishedSignal, callback, "async flush");
			GD.PushWarning($"Failed to queue async flush: {queueError.Message}");
			return Task.FromResult(Error.Failed);
		}
		if (queueResult != Error.Ok)
		{
			TryDisconnect(saveManager, AsyncFlushFinishedSignal, callback, "async flush");
			return Task.FromResult(queueResult);
		}

		return completion.Task;
	}

	private static async Task<Error> SaveActiveProfileThroughCoordinatorAsync(
		Func<GameProgressData, GameProgressData> update,
		bool forcePersist)
	{
		ProfileWriteLease? lease = await GameSaveProfileWriteCoordinator.AcquireAsync(
			update,
			forcePersist);
		if (lease == null)
		{
			return Error.Unavailable;
		}

		try
		{
			Error result = await lease.PersistAttemptAsync();
			if (result == Error.Ok)
			{
				lease.CompleteSuccess();
			}
			return result;
		}
		catch (Exception error)
		{
			GD.PushWarning($"Async profile save failed unexpectedly: {error.Message}");
			return Error.Failed;
		}
		finally
		{
			lease.Abort();
		}
	}

	private static void TryDisconnect(
		Node saveManager,
		StringName signal,
		Callable callback,
		string operation)
	{
		try
		{
			DisconnectIfConnected(saveManager, signal, callback);
		}
		catch (Exception error)
		{
			GD.PushWarning($"Failed to disconnect {operation} callback: {error.Message}");
		}
	}

	private static void DisconnectIfConnected(
		Node saveManager,
		StringName signal,
		Callable callback)
	{
		if (saveManager.IsConnected(signal, callback))
		{
			saveManager.Disconnect(signal, callback);
		}
	}

	private static long NextAsyncRequestId()
	{
		long requestId = Interlocked.Increment(ref _nextAsyncRequestId);
		if (requestId > 0)
		{
			return requestId;
		}

		Interlocked.Exchange(ref _nextAsyncRequestId, 1);
		return 1;
	}
}
