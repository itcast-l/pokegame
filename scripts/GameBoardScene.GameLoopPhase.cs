using Deckland.Rules;

public partial class GameBoardScene
{
	private GameLoopPhase GetGameLoopPhase()
	{
		return MatchReadModel.GetGameLoopPhase(BuildGameLoopPhaseState());
	}

	private GameLoopPhaseState BuildGameLoopPhaseState()
	{
		return new GameLoopPhaseState(
			_isLeavingGame || IsAdventureSaveGateBlocking(),
			IsGameOver(),
			_isOpeningDealAnimationPlaying,
			HasPendingItemSelection(),
			_isProcessingCardPlay,
			_isResolvingTurnSequence,
			IsLocalHandPlayer(_currentTurnPlayerIndex) && IsLocalSeatHumanConnected());
	}

	private TurnContext GetCurrentTurnContext()
	{
		return new TurnContext(_turnSequenceGeneration);
	}

	private int AdvanceTurnSequenceGeneration()
	{
		int generation = _turnSequenceGeneration;
		if (TrySubmitAdvanceTurnGenerationRuleCommand(out RuleCommandResult result))
		{
			generation = result.TurnGeneration;
		}
		else if (_replicaRuleSession != null)
		{
			generation = checked(_turnSequenceGeneration + 1);
			_replicaTurnSequenceGenerationOverride = generation;
		}
		return generation;
	}

	private bool IsCurrentTurnContext(TurnContext context)
	{
		return context.IsCurrent(_turnSequenceGeneration);
	}
}
