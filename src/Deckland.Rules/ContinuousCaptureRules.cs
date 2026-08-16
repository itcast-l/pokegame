public readonly record struct ContinuousCaptureResult(
	int FinalTableCaptureCount,
	int NextBasePlayCaptureThreshold,
	bool BigTideTriggered)
{
	public int NextMinimumTableCaptureCount =>
		ContinuousCaptureRules.GetMinimumTableCaptureCount(NextBasePlayCaptureThreshold);
}

public static class ContinuousCaptureRules
{
	public const int MinimumThreshold = 0;
	public const int MaximumThreshold = 4;
	public const int BigTideTableCaptureCount = 5;

	public static bool IsValidThreshold(int threshold) =>
		threshold is >= MinimumThreshold and <= MaximumThreshold;

	public static int GetMinimumTableCaptureCount(int threshold)
	{
		if (!IsValidThreshold(threshold))
		{
			throw new ArgumentOutOfRangeException(nameof(threshold));
		}

		return threshold + 1;
	}

	public static bool MeetsThreshold(int normalTableCaptureCount, int threshold) =>
		normalTableCaptureCount >= 0
		&& IsValidThreshold(threshold)
		&& normalTableCaptureCount > threshold;

	public static ContinuousCaptureResult RecordCapture(int finalTableCaptureCount)
	{
		if (finalTableCaptureCount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(finalTableCaptureCount));
		}

		bool bigTideTriggered = finalTableCaptureCount >= BigTideTableCaptureCount;
		return new ContinuousCaptureResult(
			finalTableCaptureCount,
			bigTideTriggered ? MinimumThreshold : finalTableCaptureCount,
			bigTideTriggered);
	}
}
