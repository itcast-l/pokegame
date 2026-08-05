using System;
using System.Diagnostics;
using Godot;

public sealed record NetworkMaintenanceSchedulerSnapshot(
	long WakeCount,
	long StaleCallbackCount,
	long BudgetExhaustionCount,
	double MaximumTickMilliseconds,
	double MaximumDriftMilliseconds,
	bool IsArmed,
	long Generation);

public sealed partial class NetworkMaintenanceScheduler : Node
{
	private const double TickBudgetMilliseconds = 4.0;
	private Godot.Timer? _timer;
	private Func<double, double?>? _tick;
	private readonly NetworkMaintenanceScheduleGeneration _scheduleGeneration = new();
	private long _armedGeneration;
	private double _armedDeadline = -1.0;
	private long _wakeCount;
	private long _staleCallbackCount;
	private long _budgetExhaustionCount;
	private long _tickManagedAllocationBytes;
	private long _rearmManagedAllocationBytes;
	private double _maximumTickMilliseconds;
	private double _maximumDriftMilliseconds;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_timer = new Godot.Timer
		{
			Name = "MaintenanceTimer",
			OneShot = true,
			Autostart = false,
			ProcessMode = ProcessModeEnum.Always,
		};
		_timer.Timeout += OnTimeout;
		AddChild(_timer);
	}

	public override void _ExitTree()
	{
		Invalidate();
		if (_timer != null)
		{
			_timer.Timeout -= OnTimeout;
		}
		_tick = null;
	}

	public void Configure(Func<double, double?> tick)
	{
		_tick = tick ?? throw new ArgumentNullException(nameof(tick));
	}

	public void Reschedule(double? absoluteDeadlineSeconds)
	{
		long generation = _scheduleGeneration.Advance();
		_timer?.Stop();
		_armedDeadline = -1.0;
		if (!absoluteDeadlineSeconds.HasValue || _timer == null || _tick == null)
		{
			return;
		}

		Arm(generation, absoluteDeadlineSeconds.Value);
	}

	public void Invalidate()
	{
		_scheduleGeneration.Advance();
		_timer?.Stop();
		_armedDeadline = -1.0;
	}

	public NetworkMaintenanceSchedulerSnapshot GetSnapshot()
	{
		return new NetworkMaintenanceSchedulerSnapshot(
			_wakeCount,
			_staleCallbackCount,
			_budgetExhaustionCount,
			_maximumTickMilliseconds,
			_maximumDriftMilliseconds,
			_timer != null && !_timer.IsStopped(),
			_scheduleGeneration.Current);
	}

	public long GetTickManagedAllocationBytes()
	{
		return _tickManagedAllocationBytes;
	}

	public long GetRearmManagedAllocationBytes()
	{
		return _rearmManagedAllocationBytes;
	}

	private void Arm(long generation, double absoluteDeadlineSeconds)
	{
		if (_timer == null)
		{
			return;
		}

		double now = GetNowSeconds();
		_armedGeneration = generation;
		_armedDeadline = Math.Max(now, absoluteDeadlineSeconds);
		_timer.Start(NetworkMaintenanceScheduleRules.GetTimerDelay(now, _armedDeadline));
	}

	private void OnTimeout()
	{
		long callbackGeneration = _armedGeneration;
		if (!_scheduleGeneration.IsCurrent(callbackGeneration) || _tick == null)
		{
			_staleCallbackCount++;
			return;
		}

		double now = GetNowSeconds();
		if (_armedDeadline >= 0.0 && !NetworkMaintenanceScheduleRules.IsDue(now, _armedDeadline))
		{
			Arm(callbackGeneration, _armedDeadline);
			return;
		}

		_wakeCount++;
		if (_armedDeadline >= 0.0)
		{
			_maximumDriftMilliseconds = Math.Max(
				_maximumDriftMilliseconds,
				Math.Max(0.0, now - _armedDeadline) * 1000.0);
		}

		long startedAt = Stopwatch.GetTimestamp();
		long allocationStartedAt = GC.GetAllocatedBytesForCurrentThread();
		double? nextDeadline = _tick(now);
		_tickManagedAllocationBytes += GC.GetAllocatedBytesForCurrentThread() - allocationStartedAt;
		double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
		_maximumTickMilliseconds = Math.Max(_maximumTickMilliseconds, elapsedMilliseconds);
		if (elapsedMilliseconds > TickBudgetMilliseconds)
		{
			_budgetExhaustionCount++;
		}

		if (!_scheduleGeneration.IsCurrent(callbackGeneration))
		{
			return;
		}

		_armedDeadline = -1.0;
		if (nextDeadline.HasValue)
		{
			allocationStartedAt = GC.GetAllocatedBytesForCurrentThread();
			Arm(callbackGeneration, nextDeadline.Value);
			_rearmManagedAllocationBytes += GC.GetAllocatedBytesForCurrentThread() - allocationStartedAt;
		}
	}

	private static double GetNowSeconds()
	{
		return Time.GetTicksMsec() / 1000.0;
	}
}
