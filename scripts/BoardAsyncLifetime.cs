using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

public sealed class BoardAsyncLifetime : IDisposable
{
	private static int _nextGeneration;
#if DECKLAND_TEST_HOOKS
	private static int _activeLifetimeCount;
#endif
	private readonly Node _owner;
	private readonly CancellationTokenSource _cancellation = new();
	private readonly HashSet<Tween> _tweens = new();
	private readonly HashSet<Task> _tasks = new();
	private bool _stopping;
	private bool _disposed;

	public BoardAsyncLifetime(Node owner)
	{
		_owner = owner ?? throw new ArgumentNullException(nameof(owner));
		Generation = Interlocked.Increment(ref _nextGeneration);
#if DECKLAND_TEST_HOOKS
		Interlocked.Increment(ref _activeLifetimeCount);
#endif
	}

	public int Generation { get; }
	public bool IsStopping => _stopping;
	public CancellationToken Token => _cancellation.Token;
#if DECKLAND_TEST_HOOKS
	public int TrackedTweenCount => _tweens.Count;
	public int TrackedTaskCount => _tasks.Count;
	public static int ActiveLifetimeCount => Volatile.Read(ref _activeLifetimeCount);
#endif

	public bool IsCurrent(int generation)
	{
		return !_stopping
			&& generation == Generation
			&& GodotObject.IsInstanceValid(_owner)
			&& _owner.IsInsideTree();
	}

	public void ThrowIfStopping(int generation)
	{
		if (!IsCurrent(generation))
		{
			throw new OperationCanceledException(Token);
		}
	}

	public Tween TrackTween(Tween tween)
	{
		if (!GodotObject.IsInstanceValid(tween))
		{
			return tween;
		}
		if (_stopping)
		{
			tween.Kill();
			return tween;
		}

		_tweens.Add(tween);
		ulong tweenId = tween.GetInstanceId();
		tween.Finished += () => ForgetTween(tweenId);
		return tween;
	}

	public Task WaitForTweenAsync(Tween tween)
	{
		if (_stopping
			|| !GodotObject.IsInstanceValid(tween)
			|| !tween.IsValid()
			|| !GodotObject.IsInstanceValid(_owner)
			|| !_owner.IsInsideTree())
		{
			return Task.FromCanceled(new CancellationToken(canceled: true));
		}

		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		SceneTree tree = _owner.GetTree();
		CancellationTokenRegistration registration = default;
		Action? finished = null;
		Action? processFrame = null;
		void Complete(bool canceled)
		{
			if (GodotObject.IsInstanceValid(tween))
			{
				tween.Finished -= finished;
			}
			tree.ProcessFrame -= processFrame;
			registration.Dispose();
			if (canceled)
			{
				completion.TrySetCanceled(Token);
			}
			else
			{
				completion.TrySetResult();
			}
		}
		finished = () => Complete(canceled: false);
		processFrame = () =>
		{
			if (!GodotObject.IsInstanceValid(tween) || !tween.IsValid())
			{
				Complete(canceled: false);
			}
		};
		tween.Finished += finished;
		tree.ProcessFrame += processFrame;
		registration = Token.Register(() =>
		{
			if (GodotObject.IsInstanceValid(tween))
			{
				tween.Kill();
			}
			Complete(canceled: true);
		});
		return completion.Task;
	}

#if DECKLAND_TEST_HOOKS
	public void DebugKillTrackedTweens()
	{
		foreach (Tween tween in new List<Tween>(_tweens))
		{
			if (GodotObject.IsInstanceValid(tween) && tween.IsValid())
			{
				tween.Kill();
			}
		}
	}
#endif

	public Task DelayAsync(double seconds, bool processAlways = true)
	{
		if (_stopping || !GodotObject.IsInstanceValid(_owner) || !_owner.IsInsideTree())
		{
			return Task.FromCanceled(new CancellationToken(canceled: true));
		}

		SceneTreeTimer timer = _owner.GetTree().CreateTimer(Math.Max(0.0, seconds), processAlways);
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		CancellationTokenRegistration registration = default;
		Action? timeout = null;
		timeout = () =>
		{
			if (GodotObject.IsInstanceValid(timer))
			{
				timer.Timeout -= timeout;
			}
			registration.Dispose();
			completion.TrySetResult();
		};
		timer.Timeout += timeout;
		registration = Token.Register(() =>
		{
			if (GodotObject.IsInstanceValid(timer))
			{
				timer.Timeout -= timeout;
			}
			completion.TrySetCanceled(Token);
		});
		return completion.Task;
	}

	public Task NextFrameAsync()
	{
		if (_stopping || !GodotObject.IsInstanceValid(_owner) || !_owner.IsInsideTree())
		{
			return Task.FromCanceled(new CancellationToken(canceled: true));
		}

		SceneTree tree = _owner.GetTree();
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		CancellationTokenRegistration registration = default;
		Action? processFrame = null;
		processFrame = () =>
		{
			tree.ProcessFrame -= processFrame;
			registration.Dispose();
			completion.TrySetResult();
		};
		tree.ProcessFrame += processFrame;
		registration = Token.Register(() =>
		{
			tree.ProcessFrame -= processFrame;
			completion.TrySetCanceled(Token);
		});
		return completion.Task;
	}

	public void Track(Task task, string label)
	{
		if (task == null)
		{
			return;
		}

		_tasks.Add(task);
		_ = ObserveAsync(task, label ?? string.Empty);
	}

	public void BeginStopping()
	{
		_stopping = true;
	}

	public void Shutdown()
	{
		if (_disposed)
		{
			return;
		}

		BeginStopping();
		_cancellation.Cancel();
		foreach (Tween tween in new List<Tween>(_tweens))
		{
			if (GodotObject.IsInstanceValid(tween))
			{
				tween.Kill();
			}
		}
		_tweens.Clear();
		Dispose();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_cancellation.Dispose();
#if DECKLAND_TEST_HOOKS
		Interlocked.Decrement(ref _activeLifetimeCount);
#endif
	}

	private async Task ObserveAsync(Task task, string label)
	{
		try
		{
			await task;
		}
		catch (OperationCanceledException) when (_stopping)
		{
		}
		catch (Exception error)
		{
			if (!_stopping && GodotObject.IsInstanceValid(_owner) && _owner.IsInsideTree())
			{
				GD.PushError($"Board async task failed ({label}): {error}");
			}
		}
		finally
		{
			_tasks.Remove(task);
		}
	}

	private void ForgetTween(ulong tweenId)
	{
		Tween? tween = GodotObject.InstanceFromId(tweenId) as Tween;
		if (tween != null)
		{
			_tweens.Remove(tween);
		}
	}
}
