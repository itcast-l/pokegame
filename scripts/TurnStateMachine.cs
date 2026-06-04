internal readonly record struct TurnContext(int Generation)
{
	internal bool IsCurrent(int currentGeneration)
	{
		return Generation == currentGeneration;
	}
}

internal sealed class TurnStateMachine
{
	private int _generation;

	internal int Generation => _generation;

	internal TurnContext CurrentContext => new(_generation);

	internal int AdvanceGeneration()
	{
		_generation++;
		return _generation;
	}

	internal void SyncGeneration(int generation)
	{
		_generation = generation;
	}

	internal GameLoopPhase GetPhase(GameLoopPhaseState state)
	{
		return GameLoopPhaseRules.GetPhase(state);
	}

	internal bool CanBeginMainPlayerAction(GameLoopPhaseState state)
	{
		return GetPhase(state) == GameLoopPhase.AwaitingMainPlayerAction;
	}

	internal bool CanBeginOpponentSequence(GameLoopPhaseState state)
	{
		return !state.IsResolvingTurnSequence
			&& !state.IsGameOver
			&& !state.IsLeavingGame
			&& !state.IsOpeningDeal
			&& !state.HasPendingItemSelection
			&& !state.IsMainPlayerTurn;
	}
}
