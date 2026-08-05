using System.Collections.Generic;

public readonly record struct CapturePlan(int CaptureStartIndex, bool Captured, bool CapturedMiddleCards);

internal static class CaptureResolver
{
	public static CapturePlan Resolve(IReadOnlyList<CaptureScoringTableCard> tableCards, int captureStartIndex)
	{
		bool captured = captureStartIndex >= 0 && captureStartIndex < tableCards.Count;
		bool capturedMiddleCards = captured && CaptureScoringRules.HasCapturedMiddleTableCards(tableCards, captureStartIndex);
		return new CapturePlan(captureStartIndex, captured, capturedMiddleCards);
	}
}
