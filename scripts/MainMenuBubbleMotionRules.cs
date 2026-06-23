using System;

public readonly record struct MainMenuBubbleMotionState(
	float Progress,
	float RiseOffsetY,
	float AlphaMultiplier,
	float SizeMultiplier);

public static class MainMenuBubbleMotionRules
{
	public static MainMenuBubbleMotionState Evaluate(float timeSeconds, float phase, float durationSeconds, float riseDistance)
	{
		if (durationSeconds <= 0.0f || riseDistance <= 0.0f)
		{
			return new MainMenuBubbleMotionState(0.0f, 0.0f, 0.0f, 1.0f);
		}

		float progress = PositiveModulo((timeSeconds / durationSeconds) + phase, 1.0f);
		float easedProgress = SmoothStep(progress);
		float fadeIn = Clamp01(progress / 0.16f);
		float fadeOut = Clamp01((1.0f - progress) / 0.28f);
		float alpha = MathF.Min(fadeIn, fadeOut);
		float size = 0.78f + progress * 0.34f;

		return new MainMenuBubbleMotionState(
			progress,
			-riseDistance * easedProgress,
			alpha,
			size);
	}

	public static float GetSurfaceAlphaMultiplier(float centerY, float topOffset, float waterlineY, float fadeDistance)
	{
		float topY = centerY - Math.Max(topOffset, 0.0f);
		float distanceBelowWaterline = topY - waterlineY;
		if (fadeDistance <= 0.0f)
		{
			return distanceBelowWaterline > 0.0f ? 1.0f : 0.0f;
		}

		return Clamp01(distanceBelowWaterline / fadeDistance);
	}

	private static float PositiveModulo(float value, float modulus)
	{
		float result = value % modulus;
		return result < 0.0f ? result + modulus : result;
	}

	private static float SmoothStep(float value)
	{
		float clamped = Clamp01(value);
		return clamped * clamped * (3.0f - 2.0f * clamped);
	}

	private static float Clamp01(float value)
	{
		return Math.Clamp(value, 0.0f, 1.0f);
	}
}
