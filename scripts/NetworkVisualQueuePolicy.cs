internal enum NetworkVisualQueueAdmission
{
	Enqueue,
	DropWhileFastForwarding,
	EnterFastForward,
}

internal static class NetworkVisualQueuePolicy
{
	internal const int MaxPendingEventCount = 64;

	internal static NetworkVisualQueueAdmission ClassifyAdmission(
		int pendingEventCount,
		bool fastForwarding)
	{
		if (fastForwarding)
		{
			return NetworkVisualQueueAdmission.DropWhileFastForwarding;
		}

		return pendingEventCount < MaxPendingEventCount
			? NetworkVisualQueueAdmission.Enqueue
			: NetworkVisualQueueAdmission.EnterFastForward;
	}

	internal static int SaturatingAdd(int current, int increment)
	{
		if (increment <= 0)
		{
			return current;
		}

		if (current >= int.MaxValue - increment)
		{
			return int.MaxValue;
		}

		return current + increment;
	}
}
