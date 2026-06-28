using System;

internal static class CardVisualTuningRules
{
	private const float DefaultHandVisualScale = 1.0f;
	private const float JackHandVisualScale = 1.08f;

	internal static float GetPlayingCardHandVisualScale(string rank)
	{
		return string.Equals(rank, "J", StringComparison.Ordinal)
			? JackHandVisualScale
			: DefaultHandVisualScale;
	}

	internal static float GetItemCardHandVisualScale(string itemCardId)
	{
		return string.Equals(itemCardId, "J", StringComparison.Ordinal)
			? JackHandVisualScale
			: DefaultHandVisualScale;
	}
}
