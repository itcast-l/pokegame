using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

public sealed class BoardAsyncLifetime : IDisposable
{
	private static int _nextGeneration;
	private static int _activeLifetimeCount;
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
		Interlocked.Increment(ref _activeLifetimeCount);
	}

	public int Generation { get; }
	public bool IsStopping => _stopping;
	public CancellationToken Token => _cancellation.Token;
	public int TrackedTweenCount => _tweens.Count;
	public int TrackedTaskCount => _tasks.Count;
	public static int ActiveLifetimeCount => Volatile.Read(ref _activeLifetimeCount);

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
		if (_stopping || !GodotObject.IsInstanceValid(tween))
		{
			return Task.FromCanceled(new CancellationToken(canceled: true));
		}

		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		CancellationTokenRegistration registration = default;
		Action? finished = null;
		finished = () =>
		{
			if (GodotObject.IsInstanceValid(tween))
			{
				tween.Finished -= finished;
			}
			registration.Dispose();
			completion.TrySetResult();
		};
		tween.Finished += finished;
		registration = Token.Register(() =>
		{
			if (GodotObject.IsInstanceValid(tween))
			{
				tween.Finished -= finished;
				tween.Kill();
			}
			completion.TrySetCanceled(Token);
		});
		return completion.Task;
	}

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
		Interlocked.Decrement(ref _activeLifetimeCount);
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
