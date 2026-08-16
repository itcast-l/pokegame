using System.Collections.Generic;
using Deckland.Rules;

namespace Deckland.Rules
{
	public readonly record struct PendingBasePlayCaptureState(
		int Seat,
		long CardRuntimeInstanceId,
		long PlayActionId,
		int TurnGeneration)
	{
		public static PendingBasePlayCaptureState None { get; }
		public bool IsPending => CardRuntimeInstanceId > 0;
	}
}

internal sealed class GameBoardState
{
	private const int DefaultPlayerCount = 2;
	private const int MaxPlayerCount = 4;
	private const int DefaultWinningScore = 100;
	private const int MainPlayerIndex = 0;

	public int PlayerCount { get; set; } = DefaultPlayerCount;
	public List<List<PlayingCard>> PlayerDecks { get; } = new();
	public List<List<PlayingCard>> PlayerHands { get; } = new();
	public List<List<ItemCard>> PlayerItemHands { get; } = new();
	public List<TableCard> TableCards { get; } = new();
	public List<Deckland.Rules.PendingAttachmentRewardState> PendingAttachmentRewards { get; } = new();
	public int[] PlayerScores { get; } = new int[MaxPlayerCount];
	public int BasePlayCaptureThreshold { get; set; }
	public PendingBasePlayCaptureState PendingBasePlayCapture { get; set; } =
		PendingBasePlayCaptureState.None;
	public int WinningScore { get; set; } = DefaultWinningScore;
	public int CurrentTurnPlayerIndex { get; set; } = MainPlayerIndex;
	public int TurnSequenceGeneration { get; set; }
	public int CompletedTurnsSinceQuestionCardRefresh { get; set; }
	public int QuestionCardRefreshAnchorPlayerIndex { get; set; } = -1;
	public ExtraActionQueueState ExtraActions { get; } = new();
	public MatchFlowState MatchFlow { get; set; } = MatchFlowState.Empty;
	public MatchOutcomeKind MatchOutcomeKind { get; set; } = MatchOutcomeKind.InProgress;
	public int WinnerPlayerIndex { get; set; } = -1;
	public Deckland.Rules.MandatorySelectionState MandatorySelection { get; set; } = Deckland.Rules.MandatorySelectionState.None;
	internal bool RuleSessionInitialized { get; set; }
	internal bool RuleSessionEnded { get; set; }
	internal long RuleRevision { get; set; }
	internal int RuleLifecycleGeneration { get; set; }
	internal long RulePendingPresentationBarrierId { get; set; }
	internal List<long> RulePendingTableOrderRuntimeInstanceIds { get; } = new();
}
