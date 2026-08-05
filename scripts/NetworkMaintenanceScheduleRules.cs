using System;

public sealed class NetworkMaintenanceScheduleGeneration
{
	private long _generation;

	public long Current => _generation;

	public long Advance()
	{
		return checked(++_generation);
	}

	public bool IsCurrent(long candidate)
	{
		return candidate == _generation;
	}
}

public static class NetworkMaintenanceScheduleRules
{
	public const double MinimumTimerDelaySeconds = 0.001;

	public static double GetTimerDelay(double now, double absoluteDeadline)
	{
		return Math.Max(MinimumTimerDelaySeconds, absoluteDeadline - now);
	}

	public static bool IsDue(double now, double absoluteDeadline)
	{
		return now + MinimumTimerDelaySeconds >= absoluteDeadline;
	}
}
