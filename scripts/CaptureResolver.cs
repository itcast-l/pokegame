using System.Collections.Generic;

internal readonly record struct CapturePlan(int CaptureStartIndex, bool Captured, bool CapturedMiddleCards);

internal static class CaptureResolver
{
	internal static CapturePlan Resolve(IReadOnlyList<CaptureScoringRules.TableCard> tableCards, int captureStartIndex)
	{
		bool captured = captureStartIndex >= 0 && captureStartIndex < tableCards.Count;
		bool capturedMiddleCards = captured && CaptureScoringRules.HasCapturedMiddleTableCards(tableCards, captureStartIndex);
		return new CapturePlan(captureStartIndex, captured, capturedMiddleCards);
	}
}
