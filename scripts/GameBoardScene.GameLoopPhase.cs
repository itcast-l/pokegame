public partial class GameBoardScene
{
	private GameLoopPhase GetGameLoopPhase()
	{
		return GameLoopPhaseRules.GetPhase(new GameLoopPhaseState(
			_isLeavingGame,
			IsGameOver(),
			_isOpeningDealAnimationPlaying,
			HasPendingItemSelection(),
			_isProcessingCardPlay,
			_isResolvingTurnSequence,
			_currentTurnPlayerIndex == MainPlayerIndex));
	}
}
