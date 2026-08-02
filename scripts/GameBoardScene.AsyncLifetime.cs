using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private BoardAsyncLifetime? _boardAsyncLifetime;

	private int BoardLifetimeGeneration => _boardAsyncLifetime?.Generation ?? -1;
	private bool IsBoardStopping => _boardAsyncLifetime?.IsStopping ?? true;

	private Tween CreateBoardTween()
	{
		return TrackBoardTween(CreateTween());
	}

	private Tween CreateBoardTween(Node owner)
	{
		return TrackBoardTween(owner.CreateTween());
	}

	private Tween TrackBoardTween(Tween tween)
	{
		return _boardAsyncLifetime?.TrackTween(tween) ?? tween;
	}

	private Task WaitForBoardTweenAsync(Tween tween)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return Task.CompletedTask;
		}

		return _boardAsyncLifetime?.WaitForTweenAsync(tween) ?? Task.CompletedTask;
	}

	private Task WaitForBoardDelayAsync(double seconds)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return Task.CompletedTask;
		}

		return _boardAsyncLifetime?.DelayAsync(seconds) ?? Task.CompletedTask;
	}

	private Task WaitForNextBoardFrameAsync()
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return Task.CompletedTask;
		}

		return _boardAsyncLifetime?.NextFrameAsync() ?? Task.CompletedTask;
	}

	private void TrackBoardTask(Task task, string label)
	{
		_boardAsyncLifetime?.Track(task, label);
	}

	private bool IsBoardLifetimeCurrent(int generation)
	{
		return _boardAsyncLifetime?.IsCurrent(generation) ?? false;
	}

	public bool DebugIsBoardAsyncStopping()
	{
		return IsBoardStopping;
	}

	public int DebugGetBoardTrackedTweenCount()
	{
		return _boardAsyncLifetime?.TrackedTweenCount ?? 0;
	}

	public int DebugGetBoardTrackedTaskCount()
	{
		return _boardAsyncLifetime?.TrackedTaskCount ?? 0;
	}

	public void DebugKillTrackedBoardTweens()
	{
		_boardAsyncLifetime?.DebugKillTrackedTweens();
	}

	public static int DebugGetActiveBoardLifetimeCount()
	{
		return BoardAsyncLifetime.ActiveLifetimeCount;
	}

	public bool DebugStartBoardAsyncLifetimeShutdownProbe()
	{
		if (_boardAsyncLifetime == null || _boardAsyncLifetime.IsStopping)
		{
			return false;
		}

		Tween tween = CreateBoardTween();
		tween.TweenInterval(60.0);
		TrackBoardTask(
			WaitForBoardTweenAsync(tween),
			"DebugBoardLifetimeTweenShutdownProbe");
		TrackBoardTask(
			WaitForBoardDelayAsync(60.0),
			"DebugBoardLifetimeTimerShutdownProbe");
		return true;
	}
}
