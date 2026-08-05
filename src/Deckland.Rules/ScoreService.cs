using System.Collections.Generic;

public readonly record struct ScoreBreakdown(int ScorePlayerIndex, int Points, bool HasDoubleEffect);

internal static class ScoreService
{
	internal static ScoreBreakdown GetCapturedScore(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		int scorePlayerIndex,
		CaptureScoringContext context,
		IReadOnlySet<int>? extraTableCardIndexes = null)
	{
		bool hasDoubleEffect = CaptureScoringRules.HasCapturedDoubleScoreEffect(tableCards, captureStartIndex, extraTableCardIndexes);
		int points = CaptureScoringRules.GetCapturedTablePoints(tableCards, captureStartIndex, context, extraTableCardIndexes);
		return new ScoreBreakdown(scorePlayerIndex, points, hasDoubleEffect);
	}

	internal static int GetCapturedScorePlayerIndex(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		int fallbackPlayerIndex,
		int playerCount)
	{
		return CaptureScoringRules.GetCapturedTableScorePlayerIndex(
			tableCards,
			captureStartIndex,
			fallbackPlayerIndex,
			playerCount);
	}
}
