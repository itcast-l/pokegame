using System;

internal readonly record struct QuestionCardRefreshState(
	int CompletedTurnsSinceQuestionCardRefresh,
	int QuestionCardRefreshAnchorPlayerIndex);

internal readonly record struct QuestionCardRefreshAdvanceResult(
	QuestionCardRefreshState State,
	bool ShouldRefreshQuestionCard);

internal static class TurnProgressRules
{
	internal static QuestionCardRefreshState StartQuestionCardRefreshTracking(int playerIndex)
	{
		return new QuestionCardRefreshState(0, playerIndex);
	}

	internal static QuestionCardRefreshState ResetQuestionCardRefreshTracking()
	{
		return new QuestionCardRefreshState(0, -1);
	}

	internal static QuestionCardRefreshAdvanceResult AdvanceQuestionCardRefresh(
		bool itemMode,
		bool hasTableSpecialCard,
		QuestionCardRefreshState currentState,
		int nextTurnPlayerIndex,
		int playerCount,
		int refreshCyclesAfterCapture)
	{
		if (!itemMode || hasTableSpecialCard || currentState.QuestionCardRefreshAnchorPlayerIndex < 0)
		{
			return new QuestionCardRefreshAdvanceResult(currentState, false);
		}

		int nextCompletedTurnsSinceQuestionCardRefresh = currentState.CompletedTurnsSinceQuestionCardRefresh + 1;
		if (nextTurnPlayerIndex != currentState.QuestionCardRefreshAnchorPlayerIndex
			|| nextCompletedTurnsSinceQuestionCardRefresh < GetQuestionCardRefreshTurnCount(playerCount, refreshCyclesAfterCapture))
		{
			return new QuestionCardRefreshAdvanceResult(
				new QuestionCardRefreshState(
					nextCompletedTurnsSinceQuestionCardRefresh,
					currentState.QuestionCardRefreshAnchorPlayerIndex),
				false);
		}

		return new QuestionCardRefreshAdvanceResult(ResetQuestionCardRefreshTracking(), true);
	}

	internal static int GetQuestionCardRefreshTurnCount(int playerCount, int refreshCyclesAfterCapture)
	{
		return Math.Max(1, playerCount * refreshCyclesAfterCapture);
	}
}
