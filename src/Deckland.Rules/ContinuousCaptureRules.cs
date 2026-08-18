public readonly record struct ContinuousCaptureResult(
	int FinalMiddleTableCaptureCount,
	int NextBasePlayCaptureThreshold,
	bool BigTideTriggered)
{
	public int NextMinimumMiddleTableCaptureCount =>
		ContinuousCaptureRules.GetMinimumMiddleTableCaptureCount(NextBasePlayCaptureThreshold);
}

public static class ContinuousCaptureRules
{
	public const int MinimumThreshold = 0;
	public const int MaximumThreshold = 4;
	public const int BigTideMiddleTableCaptureCount = 5;

	public static bool IsValidThreshold(int threshold) =>
		threshold is >= MinimumThreshold and <= MaximumThreshold;

	public static int GetMinimumMiddleTableCaptureCount(int threshold)
	{
		if (!IsValidThreshold(threshold))
		{
			throw new ArgumentOutOfRangeException(nameof(threshold));
		}

		return threshold + 1;
	}

	public static bool MeetsThreshold(int middleTableCaptureCount, int threshold) =>
		middleTableCaptureCount >= 0
		&& IsValidThreshold(threshold)
		&& middleTableCaptureCount > threshold;

	public static ContinuousCaptureResult RecordCapture(int finalMiddleTableCaptureCount)
	{
		if (finalMiddleTableCaptureCount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(finalMiddleTableCaptureCount));
		}

		bool bigTideTriggered = finalMiddleTableCaptureCount >= BigTideMiddleTableCaptureCount;
		return new ContinuousCaptureResult(
			finalMiddleTableCaptureCount,
			bigTideTriggered ? MinimumThreshold : finalMiddleTableCaptureCount,
			bigTideTriggered);
	}
}
