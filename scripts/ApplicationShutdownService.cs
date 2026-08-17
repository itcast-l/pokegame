using System;
using System.Threading.Tasks;
using Godot;

internal static class ApplicationShutdownService
{
	private const double ShutdownDeadlineSeconds = 10.0;
	private static readonly object StateLock = new();
	private static Task? _shutdownTask;

	internal static void Request(Node context, Action markApplicationExiting)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(markApplicationExiting);

		lock (StateLock)
		{
			if (_shutdownTask != null)
			{
				return;
			}

			markApplicationExiting();
			_shutdownTask = RunShutdownAsync(context.GetTree());
			_ = ObserveShutdownTaskAsync(_shutdownTask);
		}
	}

	private static async Task RunShutdownAsync(SceneTree tree)
	{
		DateTime deadline = DateTime.UtcNow.AddSeconds(ShutdownDeadlineSeconds);
		bool coordinatorDrained = await AwaitUntilDeadlineAsync(
			GameSaveProfileWriteCoordinator.SealAndDrainAsync(),
			deadline);
		if (!coordinatorDrained)
		{
			GD.PushError(
				"Timed out draining admitted profile writes during shutdown; "
				+ $"{GameSaveProfileWriteCoordinator.AdmittedOperationCount} operation(s) remain.");
		}

		GameSaveStore.BeginShutdown();
		(bool deferredCompleted, Error deferredResult) = await AwaitSaveTaskUntilDeadlineAsync(
			GameSaveStore.FlushDeferredValuesAsync(),
			deadline);
		ReportSaveStep(
			"deferred settings flush",
			deferredCompleted,
			deferredResult);

		(bool barrierCompleted, Error barrierResult) = await AwaitSaveTaskUntilDeadlineAsync(
			GameSaveStore.FlushPendingWritesAsync(),
			deadline);
		ReportSaveStep(
			"queued save barrier",
			barrierCompleted,
			barrierResult);

		tree.Quit();
	}

	private static async Task<bool> AwaitUntilDeadlineAsync(
		Task task,
		DateTime deadline)
	{
		TimeSpan remaining = deadline - DateTime.UtcNow;
		if (remaining <= TimeSpan.Zero)
		{
			return task.IsCompletedSuccessfully;
		}

		Task completedTask = await Task.WhenAny(task, Task.Delay(remaining));
		if (!ReferenceEquals(completedTask, task))
		{
			return false;
		}

		try
		{
			await task;
			return true;
		}
		catch (Exception error)
		{
			GD.PushError($"Profile coordinator drain failed during shutdown: {error.Message}");
			return false;
		}
	}

	private static async Task<(bool Completed, Error Result)> AwaitSaveTaskUntilDeadlineAsync(
		Task<Error> saveTask,
		DateTime deadline)
	{
		TimeSpan remaining = deadline - DateTime.UtcNow;
		if (remaining <= TimeSpan.Zero)
		{
			return (false, Error.Failed);
		}

		Task completedTask = await Task.WhenAny(saveTask, Task.Delay(remaining));
		if (!ReferenceEquals(completedTask, saveTask))
		{
			return (false, Error.Failed);
		}

		try
		{
			return (true, await saveTask);
		}
		catch (Exception error)
		{
			GD.PushError($"Save task failed during shutdown: {error.Message}");
			return (true, Error.Failed);
		}
	}

	private static void ReportSaveStep(
		string step,
		bool completed,
		Error result)
	{
		if (!completed)
		{
			GD.PushError($"Timed out waiting for the {step} during shutdown.");
		}
		else if (result != Error.Ok && result != Error.Unavailable)
		{
			GD.PushError($"Shutdown {step} failed: {result}");
		}
	}

	private static async Task ObserveShutdownTaskAsync(Task shutdownTask)
	{
		try
		{
			await shutdownTask;
		}
		catch (Exception error)
		{
			GD.PushError($"Application shutdown failed: {error}");
		}
	}
}
