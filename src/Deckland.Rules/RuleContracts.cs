using System.Collections.Immutable;

namespace Deckland.Rules;

public enum RuleRejectionCode
{
	None,
	SessionNotInitialized,
	SessionAlreadyInitialized,
	SessionEnded,
	InvalidConfiguration,
	InvalidCommand,
	InvalidSeat,
	InvalidIndex,
	WrongTurn,
	StaleRevision,
	StaleLifecycleGeneration,
	StaleTurnGeneration,
	DuplicateAction,
	PresentationBarrierPending,
	PresentationBarrierMissing,
	PresentationBarrierMismatch,
	MandatorySelectionPending,
	MandatorySelectionMissing,
	MandatorySelectionMismatch,
	ModuleRejected,
	NoLegalAction,
}

public readonly record struct RuleRejection(RuleRejectionCode Code, string Detail)
{
	public static RuleRejection None => new(RuleRejectionCode.None, string.Empty);
}

public enum PresentationIntentKind
{
	MatchInitialized,
	CardPlayed,
	CardDrawn,
	CardReturnedToHand,
	ItemPlayed,
	ItemGranted,
	HandCardDiscarded,
	ItemAttached,
	HandCardTransferred,
	DeckCardsTransferred,
	TableItemPlaced,
	TableCardsSelected,
	TableCardStateChanged,
	TableCardsRemoved,
	TableCardMoved,
	TableShufflePlanned,
	MandatorySelectionBegan,
	MandatorySelectionResolved,
	MandatorySelectionCleared,
	CardsCaptured,
	ScoreChanged,
	TurnChanged,
	MatchEnded,
	SemanticEffect,
}

public sealed record PresentationIntent(
	PresentationIntentKind Kind,
	string SemanticEffectId,
	long LogicalEntityId = 0,
	int Seat = -1,
	int SourceIndex = -1,
	int TargetIndex = -1,
	int Value = 0,
	long BarrierId = 0,
	string LogicalItemId = "");

public abstract record MatchCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration);

public sealed record InitializeMatchCommand(
	long ActionId,
	int LifecycleGeneration,
	int PlayerCount,
	int WinningScore,
	int StartingHandSize,
	bool DealEqualRanks,
	ImmutableArray<LogicalCardState> Deck,
	bool CreateOpeningPresentationBarrier = false,
	ImmutableArray<int> StartingScores = default,
	int MaximumHandSize = int.MaxValue,
	ImmutableArray<int> AdditionalStartingDraws = default,
	ImmutableArray<int> SeatCardSupplyLimits = default,
	int StartingTurnSeat = 0)
	: MatchCommand(ActionId, 0, LifecycleGeneration, 0);

public sealed record RestartMatchCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int PlayerCount,
	int WinningScore,
	int StartingHandSize,
	bool DealEqualRanks,
	ImmutableArray<LogicalCardState> Deck,
	bool CreateOpeningPresentationBarrier = false,
	ImmutableArray<int> StartingScores = default,
	int MaximumHandSize = int.MaxValue,
	ImmutableArray<int> AdditionalStartingDraws = default,
	ImmutableArray<int> SeatCardSupplyLimits = default,
	int StartingTurnSeat = 0)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record InitializeScriptedMatchCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int PlayerCount,
	int WinningScore,
	ImmutableArray<PlayerStateSnapshot> Players,
	ImmutableArray<TableCardState> TableCards,
	int CurrentTurnSeat = 0)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record EndMatchSessionCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record CompletePresentationBarrierCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	long BarrierId)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record PlayCardFromHandCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	int HandIndex,
	string PrimaryMatchRank,
	ImmutableArray<string> AlternateMatchRanks = default,
	bool CollectEntireTable = false,
	bool CollectBySuit = false,
	bool SuitMatchIsFallbackOnly = false,
	bool InsertAtTableStart = false,
	int TableInsertIndexOverride = -1,
	bool PlayedCardFacesUp = false)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public enum RuleScoreMutationKind
{
	Add,
	Subtract,
	Set,
}

public readonly record struct RuleScoreMutation(
	int Seat,
	RuleScoreMutationKind Kind,
	int Value);

public readonly record struct RuleScoreChange(
	int Seat,
	int PreviousValue,
	int CurrentValue)
{
	public int Delta => CurrentValue - PreviousValue;
}

public sealed record CaptureCardsCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	int CaptureStartIndex,
	ImmutableArray<int> ExtraTableCardIndexes = default,
	bool PreserveCardEffectsWhenEnteringDeck = true,
	ImmutableArray<int> PreservedTableCardIndexes = default,
	int RedirectedTableCardIndex = -1,
	int RedirectedSeat = -1,
	ImmutableArray<RuleScoreMutation> ScoreMutations = default,
	bool RestartQuestionRefreshTracking = false)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ApplyScoreMutationsCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	ImmutableArray<RuleScoreMutation> Mutations)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record DrawCardsCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	int Count,
	int MaximumHandSize)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ReturnCardsToHandCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	ImmutableArray<long> CardRuntimeInstanceIds,
	int MaximumHandSize)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public enum RuleItemPlayDestination
{
	Consume,
	Table,
}

public sealed record PlayItemFromHandCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	int ItemHandIndex,
	string ExpectedItemId,
	RuleItemPlayDestination Destination = RuleItemPlayDestination.Consume,
	int TableIndex = -1,
	bool TableItemFacesUp = false)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record DiscardHandCardCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	int DisplayIndex,
	long ExpectedCardRuntimeInstanceId = 0,
	string ExpectedItemId = "")
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record GrantItemToHandCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	string ItemId,
	int MaximumHandSize,
	int DiscardDisplayIndex = -1,
	long ExpectedDiscardCardRuntimeInstanceId = 0,
	string ExpectedDiscardItemId = "")
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ReplaceItemInHandCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	int ItemHandIndex,
	string ExpectedItemId,
	string ReplacementItemId)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record AttachItemToHandCardCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	int HandIndex,
	string ItemId,
	ImmutableArray<int> RandomCandidateHandIndexes = default)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ChooseRandomHandDisplayIndexCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int HandSeat,
	ImmutableArray<int> ExcludedDisplayIndexes = default)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ChooseRandomHandCardIndexCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	ImmutableArray<int> CandidateHandIndexes)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public readonly record struct RuleWeightedOption(string Id, int Weight);

public sealed record ChooseRandomTargetSeatCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ImmutableArray<int> CandidateSeats)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ChooseWeightedOptionCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ImmutableArray<RuleWeightedOption> Options)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ChooseRandomTableCardIndexCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ImmutableArray<RuleTableCardReference> Candidates)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ChooseRandomIndexCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int MinimumInclusive,
	int MaximumInclusive)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ChooseReservoirHandDisplayIndexCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int HandSeat,
	ImmutableArray<int> CandidateDisplayIndexes)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record TransferHandCardCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int SourceSeat,
	int SourceDisplayIndex,
	int MaximumHandSize,
	long ExpectedCardRuntimeInstanceId = 0,
	string ExpectedItemId = "")
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record TransferDeckCardsCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int SourceSeat,
	int MaximumCount,
	bool PreserveCardEffectsWhenEnteringDeck,
	ImmutableArray<long> ExpectedSourceTopCardRuntimeInstanceIds)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record TransferTopDeckCardCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int SourceSeat,
	long ExpectedRuntimeInstanceId,
	bool PreserveCardEffectsWhenEnteringDeck)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ShuffleDeckCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int DeckSeat,
	bool CutAfterShuffle)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public readonly record struct RuleTableCardReference(
	int Index,
	long RuntimeInstanceId);

public sealed record PlaceTableItemCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	string ItemId,
	int TableIndex,
	bool IsFaceUp,
	int ItemOwnerSeat = -1)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record PlaceTableCardCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	LogicalCardState Card,
	int TableIndex,
	bool IsFaceUp,
	int CardOwnerSeat = -1,
	bool RemoveOnCapture = false)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ChooseTableCardsCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ImmutableArray<RuleTableCardReference> Candidates,
	int MaximumCount)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record SetTableCardsFaceUpCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ImmutableArray<RuleTableCardReference> TableCards,
	bool ExpectedFaceUp,
	bool FaceUp)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record SetTableCardsHiddenCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ImmutableArray<RuleTableCardReference> TableCards,
	bool ExpectedHidden,
	bool Hidden)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record SetTableCardsScoreSuppressedCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ImmutableArray<RuleTableCardReference> TableCards,
	bool ExpectedScoreSuppressed,
	bool ScoreSuppressed)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record RemoveTableCardAttachmentCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int TableIndex,
	long ExpectedTableCardRuntimeInstanceId,
	string ItemId)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record RemoveTableCardRangeCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int StartIndex,
	ImmutableArray<long> ExpectedRuntimeInstanceIds)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record MoveTableCardCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int SourceIndex,
	int TargetIndex,
	long ExpectedRuntimeInstanceId)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record MoveTableCardToDeckCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	int TableIndex,
	long ExpectedTableCardRuntimeInstanceId,
	int DeckSeat,
	bool PreserveCardEffectsWhenEnteringDeck)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record PlanTableShuffleCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ImmutableArray<RuleTableCardReference> TableCards,
	ImmutableArray<int> FixedTableIndexes)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record BeginMandatorySelectionCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	MandatorySelectionKind Kind,
	int SourceHandIndex,
	string ItemId,
	ImmutableArray<int> LegalSelectionIndexes)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ResolveMandatorySelectionCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	MandatorySelectionKind ExpectedKind,
	int SelectionIndex,
	long ExpectedCardRuntimeInstanceId = 0,
	string ExpectedItemId = "")
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ClearMandatorySelectionCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	MandatorySelectionKind ExpectedKind)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record EnqueuePendingAttachmentRewardCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	string ItemId)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record TakePendingAttachmentRewardCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ExpectedSeat,
	string ExpectedItemId)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record RemovePendingAttachmentRewardCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	string ItemId)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ReplacePendingAttachmentRewardCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int Seat,
	string ExpectedItemId,
	string ReplacementItemId)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record SetPendingAttachmentRewardsCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	ImmutableArray<PendingAttachmentRewardState> Rewards)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record BeginRealTurnCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record EndRealTurnCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record RegisterRealTurnEndMarkerCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	string MarkerId)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record GrantExtraActionCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	string SourceId,
	bool ResumeNormalTurnAfterChain,
	ExtraActionReentryPolicy ReentryPolicy,
	bool OncePerRealTurn)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record AcceptExtraActionCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ExtraActionGrantState? ExpectedGrant)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record FinishExtraActionExecutionCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ExtraActionGrantState? AcceptedGrant)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record CompleteExtraActionCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	ExtraActionGrantState ConsumedGrant)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record AbortExtraActionChainCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record AdvanceTurnCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ActingSeat,
	bool Captured,
	int CandidateNextSeat,
	bool AdvanceQuestionRefresh = false,
	bool HasTableSpecialCard = false,
	int QuestionRefreshCyclesAfterCapture = 0,
	bool HasQuestionCard = false,
	int QuestionCardLifetimeTurns = 0)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record AdvanceTurnGenerationCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record EvaluateMatchOutcomeCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	int ScoreOrLifeWinnerSeat = -1,
	ImmutableArray<bool> HasPlayableItemActions = default,
	bool SettleRegardlessOfAvailableActions = false)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public readonly record struct RuleMutationSummary(
	long LogicalEntityId,
	int SourceSeat,
	int SourceIndex,
	int TargetIndex,
	int CaptureStartIndex,
	int CollectedCardCount,
	int RemovedTableCardCount,
	ImmutableArray<RuleScoreChange> ScoreChanges,
	bool Captured,
	bool MatchOutcomeChanged,
	string LogicalItemId = "",
	bool ShouldRefreshQuestionCard = false,
	bool ShouldExpireQuestionCard = false)
{
	public static RuleMutationSummary Empty => new(
		0,
		-1,
		-1,
		-1,
		-1,
		0,
		0,
		ImmutableArray<RuleScoreChange>.Empty,
		false,
		false);

	public ImmutableArray<RuleScoreChange> NormalizedScoreChanges => ScoreChanges.IsDefault
		? ImmutableArray<RuleScoreChange>.Empty
		: ScoreChanges;
}

public sealed record RuleCommandResult(
	bool Accepted,
	RuleRejection Rejection,
	long Revision,
	int TurnGeneration,
	ImmutableArray<PresentationIntent> PresentationIntents,
	MatchReadModel ReadModel,
	RuleMutationSummary Mutation,
	ExtraActionCommandOutcome? ExtraActionOutcome = null)
{
	public static RuleCommandResult Rejected(
		RuleRejection rejection,
		MatchReadModel readModel)
	{
		return new RuleCommandResult(
			false,
			rejection,
			readModel.Revision,
			readModel.TurnGeneration,
			ImmutableArray<PresentationIntent>.Empty,
			readModel,
			RuleMutationSummary.Empty);
	}
}

public readonly record struct SnapshotApplyResult(
	bool Applied,
	RuleRejection Rejection,
	MatchReadModel ReadModel);
