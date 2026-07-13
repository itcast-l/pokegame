using System.Collections.Generic;

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
	public int[] PlayerScores { get; } = new int[MaxPlayerCount];
	public int WinningScore { get; set; } = DefaultWinningScore;
	public int CurrentTurnPlayerIndex { get; set; } = MainPlayerIndex;
	public int TurnSequenceGeneration { get; set; }
	public int CompletedTurnsSinceQuestionCardRefresh { get; set; }
	public int QuestionCardRefreshAnchorPlayerIndex { get; set; } = -1;
	public int AdventureMainPlayerSuccessfulCapturesThisTurn { get; set; }
	public int AdventureMainPlayerCaptureCountThisBattle { get; set; }
	public AdventureExtraActionQueueState AdventureExtraActionQueue { get; } = new();
	public bool AdventureRealMainPlayerTurnActive { get; set; }
	public bool AdventureRealMainPlayerTurnStartPending { get; set; }
	public long AdventureNextBaseCardActionId { get; set; } = 1;
	public bool AdventureQuestionRejudgeUsedThisBattle { get; set; }
	public bool AdventureMainPlayerHalfHealthRescueUsed { get; set; }
	public bool AdventureMainPlayerQuarterHealthRescueUsed { get; set; }
	public bool AdventureEnemyNextBaseCardFacesUp { get; set; }
	public AdventureEnemyNextCardInsertMode AdventureEnemyNextCardInsertMode { get; set; } = AdventureEnemyNextCardInsertMode.None;
	public int AdventureEnemyNextCaptureDamageDelta { get; set; }
	public int WinnerPlayerIndex { get; set; } = -1;
	public bool IsLeavingGame { get; set; }
	public bool IsProcessingCardPlay { get; set; }
	public bool IsResolvingTurnSequence { get; set; }
	public bool IsOpeningDealAnimationPlaying { get; set; }
	public bool IsResolvingMagnetAttachmentSelection { get; set; }
	public bool IsAnimatingMagnetAttachment { get; set; }
	public int RevealedPlayerHandIndex { get; set; } = -1;
	public PendingItemSelectionMode PendingItemSelectionMode { get; set; } = PendingItemSelectionMode.None;
	public int PendingItemOwnerIndex { get; set; } = -1;
	public int PendingItemHandIndex { get; set; } = -1;
	public int PendingItemTurnSequenceGeneration { get; set; } = -1;
	public string PendingItemCardId { get; set; } = string.Empty;
}
