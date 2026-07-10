using System.Globalization;

public static class DiceRollPresentationRules
{
	public const int MinResult = 1;
	public const int MaxResult = 6;
	public const int RollingPathFrameCount = 16;
	public const double RollingPathFrameSeconds = 0.07;
	public const double ResultHoldSeconds = 0.64;
	public const string RollingPathFramePattern = "res://art/props/dice/neutral_tabletop/run-002/rolling_path/frame-{0:000}.png";
	public const string ResultMotionFramePattern = "res://art/props/dice/neutral_tabletop/run-002/result_motion_frames/result-{0}.png";
	public const string RollingPathPreviewGifPath = "res://art/props/dice/neutral_tabletop/run-002/rolling_path/animation.gif";

	public static bool IsValidResult(int result)
	{
		return result is >= MinResult and <= MaxResult;
	}

	public static string GetRollingPathFramePath(int frameIndex)
	{
		return frameIndex is >= 1 and <= RollingPathFrameCount
			? string.Format(CultureInfo.InvariantCulture, RollingPathFramePattern, frameIndex)
			: string.Empty;
	}

	public static bool TryGetResultMotionFramePath(int result, out string path)
	{
		if (!IsValidResult(result))
		{
			path = string.Empty;
			return false;
		}

		path = string.Format(CultureInfo.InvariantCulture, ResultMotionFramePattern, result);
		return true;
	}
}
