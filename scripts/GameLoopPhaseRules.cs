internal enum GameLoopPhase
{
	OpeningDeal,
	AwaitingMainPlayerAction,
	ResolvingMainPlayerAction,
	ResolvingOpponentTurns,
	PendingMainPlayerSelection,
	GameOver,
	LeavingGame,
}

internal readonly record struct GameLoopPhaseState(
	bool IsLeavingGame,
	bool IsGameOver,
	bool IsOpeningDeal,
	bool HasPendingItemSelection,
	bool IsProcessingCardPlay,
	bool IsResolvingTurnSequence,
	bool IsMainPlayerTurn);

internal static class GameLoopPhaseRules
{
	internal static GameLoopPhase GetPhase(GameLoopPhaseState state)
	{
		if (state.IsLeavingGame)
		{
			return GameLoopPhase.LeavingGame;
		}

		if (state.IsGameOver)
		{
			return GameLoopPhase.GameOver;
		}

		if (state.IsOpeningDeal)
		{
			return GameLoopPhase.OpeningDeal;
		}

		if (state.HasPendingItemSelection)
		{
			return GameLoopPhase.PendingMainPlayerSelection;
		}

		if (state.IsProcessingCardPlay)
		{
			return GameLoopPhase.ResolvingMainPlayerAction;
		}

		if (state.IsResolvingTurnSequence || !state.IsMainPlayerTurn)
		{
			return GameLoopPhase.ResolvingOpponentTurns;
		}

		return GameLoopPhase.AwaitingMainPlayerAction;
	}
}
