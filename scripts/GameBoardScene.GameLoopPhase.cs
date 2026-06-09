public partial class GameBoardScene
{
	private GameLoopPhase GetGameLoopPhase()
	{
		return _turnStateMachine.GetPhase(BuildGameLoopPhaseState());
	}

	private GameLoopPhaseState BuildGameLoopPhaseState()
	{
		return new GameLoopPhaseState(
			_isLeavingGame,
			IsGameOver(),
			_isOpeningDealAnimationPlaying,
			HasPendingItemSelection(),
			_isProcessingCardPlay,
			_isResolvingTurnSequence,
			IsLocalHandPlayer(_currentTurnPlayerIndex) && IsLocalSeatHumanConnected());
	}

	private TurnContext GetCurrentTurnContext()
	{
		return _turnStateMachine.CurrentContext;
	}

	private int AdvanceTurnSequenceGeneration()
	{
		_turnSequenceGeneration = _turnStateMachine.AdvanceGeneration();
		_networkAiBlockedItem = NetworkAiBlockedItemState.None;
		return _turnSequenceGeneration;
	}

	private bool IsCurrentTurnContext(TurnContext context)
	{
		return context.IsCurrent(_turnSequenceGeneration);
	}
}
