internal sealed class TurnAdvancementService
{
	internal QuestionCardRefreshState StartQuestionCardRefreshTracking(int playerIndex)
	{
		return TurnProgressRules.StartQuestionCardRefreshTracking(playerIndex);
	}

	internal int ApplyPlayedResult(
		GameBoardState state,
		int playerIndex,
		bool captured,
		int nextTurnPlayerIndex)
	{
		if (state.QuestionCardRefreshAnchorPlayerIndex < 0)
		{
			state.CompletedTurnsSinceQuestionCardRefresh = 0;
			state.QuestionCardRefreshAnchorPlayerIndex = playerIndex;
		}

		state.CurrentTurnPlayerIndex = captured ? playerIndex : nextTurnPlayerIndex;
		return state.CurrentTurnPlayerIndex;
	}

	internal QuestionCardRefreshAdvanceResult AdvanceQuestionCardRefresh(
		GameBoardState state,
		bool itemMode,
		bool hasTableSpecialCard,
		bool hasQuestionCard,
		int nextTurnPlayerIndex,
		int refreshCyclesAfterCapture,
		int questionCardLifetimeTurns)
	{
		QuestionCardRefreshAdvanceResult refreshResult = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode,
			hasTableSpecialCard,
			hasQuestionCard,
			new QuestionCardRefreshState(
				state.CompletedTurnsSinceQuestionCardRefresh,
				state.QuestionCardRefreshAnchorPlayerIndex),
			nextTurnPlayerIndex,
			state.PlayerCount,
			refreshCyclesAfterCapture,
			questionCardLifetimeTurns);

		state.CompletedTurnsSinceQuestionCardRefresh = refreshResult.State.CompletedTurnsSinceQuestionCardRefresh;
		state.QuestionCardRefreshAnchorPlayerIndex = refreshResult.State.QuestionCardRefreshAnchorPlayerIndex;
		return refreshResult;
	}
}
