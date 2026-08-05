using System.Collections.Generic;

public readonly record struct CaptureFlowPlan(
	CapturePlan Capture,
	ScoreBreakdown Score,
	bool CapturedScoringCards,
	bool ShouldTrackQuestionCardRefresh,
	int CaptureCount,
	int ExtraCaptureCount)
{
	public int TotalCapturedCardCount => CaptureCount + ExtraCaptureCount;
}

internal static class CaptureFlowPlanner
{
	public static CaptureFlowPlan Build(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		int scorePlayerIndex,
		CaptureScoringContext context,
		IReadOnlySet<int>? extraTableCardIndexes = null)
	{
		CapturePlan capturePlan = CaptureResolver.Resolve(tableCards, captureStartIndex);
		int captureCount = capturePlan.Captured ? tableCards.Count - captureStartIndex : 0;
		int extraCaptureCount = extraTableCardIndexes?.Count ?? 0;
		bool capturedScoringCards = capturePlan.CapturedMiddleCards
			&& CaptureScoringRules.HasCapturedScoringTableCards(tableCards, captureStartIndex, extraTableCardIndexes);
		ScoreBreakdown score = capturePlan.CapturedMiddleCards
			? ScoreService.GetCapturedScore(tableCards, captureStartIndex, scorePlayerIndex, context, extraTableCardIndexes)
			: new ScoreBreakdown(scorePlayerIndex, 0, false);

		return new CaptureFlowPlan(
			capturePlan,
			score,
			capturedScoringCards,
			capturePlan.CapturedMiddleCards && captureCount + extraCaptureCount > 0,
			captureCount,
			extraCaptureCount);
	}
}
