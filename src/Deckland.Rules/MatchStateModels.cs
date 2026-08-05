using System.Collections.Immutable;

namespace Deckland.Rules;

public enum MatchOutcome
{
	InProgress,
	Won,
	Draw,
	Ended,
}

public enum MandatorySelectionKind
{
	None,
	HandLimitDiscard,
	AttachmentHandCard,
}

public sealed record MandatorySelectionState(
	MandatorySelectionKind Kind,
	int Seat,
	int SourceHandIndex,
	int TurnGeneration,
	string ItemId,
	ImmutableArray<int> LegalSelectionIndexes)
{
	public static MandatorySelectionState None { get; } = new(
		MandatorySelectionKind.None,
		-1,
		-1,
		-1,
		string.Empty,
		ImmutableArray<int>.Empty);

	public ImmutableArray<int> NormalizedLegalSelectionIndexes =>
		LegalSelectionIndexes.IsDefault
			? ImmutableArray<int>.Empty
			: LegalSelectionIndexes;
}

public readonly record struct LogicalCardState(
	long RuntimeInstanceId,
	string Code,
	string Suit,
	string Rank,
	ImmutableArray<string> AttachedEffectIds)
{
	public LogicalCardState(long runtimeInstanceId, string code, string suit, string rank)
		: this(runtimeInstanceId, code, suit, rank, ImmutableArray<string>.Empty)
	{
	}

	public ImmutableArray<string> NormalizedAttachedEffectIds => AttachedEffectIds.IsDefault
		? ImmutableArray<string>.Empty
		: AttachedEffectIds;
}

public readonly record struct LogicalItemState(string Id);

public readonly record struct PendingAttachmentRewardState(
	int Seat,
	string ItemId);

public enum ExtraActionReentryPolicy
{
	DisallowSameSource,
	AllowSameSource,
}

public readonly record struct ExtraActionGrantState(
	string SourceId,
	long RootChainId,
	bool ResumeNormalTurnAfterChain,
	ExtraActionReentryPolicy ReentryPolicy,
	int ChainDepth);

public enum ExtraActionGrantStatus
{
	Granted,
	InvalidSource,
	OncePerRealTurnBlocked,
	ReentryBlocked,
	DerivationStopped,
	ExtraActionBudgetExhausted,
	RootEventBudgetExhausted,
}

public enum ExtraActionAcceptStatus
{
	NoGrant,
	Accepted,
	ActionInProgress,
	QueueMismatch,
	ExtraActionBudgetExhausted,
}

public enum ExtraActionCompletionKind
{
	FollowActionResult,
	ContinueChain,
	ResumeNormalTurn,
}

public sealed record ExtraActionQueueSnapshot(
	long NextRootChainId,
	long ActiveRootChainId,
	bool RootResumesNormalTurn,
	bool RootDerivationStopped,
	int RootEventCount,
	int RootMaxDepth,
	int ExtraActionsExecutedThisRealTurn,
	bool ContinueBranchUsedThisRealTurn,
	ExtraActionGrantState? CurrentGrant,
	ImmutableArray<ExtraActionGrantState> QueuedGrants,
	ImmutableArray<string> SourcesInRootChain,
	ImmutableArray<string> PendingRealTurnEndMarkerIds)
{
	public static ExtraActionQueueSnapshot Empty { get; } = new(
		1,
		0,
		false,
		false,
		0,
		0,
		0,
		false,
		null,
		ImmutableArray<ExtraActionGrantState>.Empty,
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty);

	public bool HasPendingGrant => !QueuedGrants.IsDefaultOrEmpty;
	public bool HasPendingRealTurnEndMarker(string markerId) =>
		!string.IsNullOrWhiteSpace(markerId)
		&& !PendingRealTurnEndMarkerIds.IsDefaultOrEmpty
		&& PendingRealTurnEndMarkerIds.Contains(markerId, StringComparer.Ordinal);
}

public sealed record ExtraActionCommandOutcome(
	ExtraActionGrantStatus? GrantStatus = null,
	ExtraActionAcceptStatus? AcceptStatus = null,
	ExtraActionCompletionKind? CompletionKind = null,
	ExtraActionGrantState? Grant = null,
	bool HadActiveRootChain = false,
	bool ResumeNormalTurn = false,
	long RootChainId = 0,
	int RootEventCount = 0,
	int ExtraActionsExecuted = 0,
	int QueuedGrantCount = 0,
	int ClearedGrantCount = 0,
	ImmutableArray<string> RealTurnEndMarkerIds = default)
{
	public ImmutableArray<string> NormalizedRealTurnEndMarkerIds =>
		RealTurnEndMarkerIds.IsDefault
			? ImmutableArray<string>.Empty
			: RealTurnEndMarkerIds;
}

public sealed record TableCardState(
	long RuntimeInstanceId,
	LogicalCardState? Card,
	bool IsFaceUp,
	bool IsHiddenDuringCapture,
	bool IsBuried,
	bool IsScoreSuppressed,
	LogicalItemState? ItemOverlay,
	int ItemOwnerSeat,
	int CardOwnerSeat,
	bool RemoveOnCapture);

public sealed record PlayerStateSnapshot(
	int Seat,
	ImmutableArray<LogicalCardState> Deck,
	ImmutableArray<LogicalCardState> Hand,
	ImmutableArray<LogicalItemState> ItemHand,
	int Score);

public sealed record MatchStateSnapshot(
	int SchemaVersion,
	long Revision,
	int LifecycleGeneration,
	int TurnGeneration,
	int PlayerCount,
	int WinningScore,
	int CurrentTurnSeat,
	MatchOutcome Outcome,
	int WinnerSeat,
	ImmutableArray<PlayerStateSnapshot> Players,
	ImmutableArray<TableCardState> TableCards,
	RuleRandomState RandomState,
	long PendingPresentationBarrierId,
	ImmutableArray<long> AppliedActionIds,
	ImmutableArray<long> PendingTableOrderRuntimeInstanceIds = default,
	MandatorySelectionState? MandatorySelection = null,
	ImmutableArray<PendingAttachmentRewardState> PendingAttachmentRewards = default,
	ExtraActionQueueSnapshot? ExtraActions = null,
	int CompletedTurnsSinceQuestionRefresh = 0,
	int QuestionRefreshAnchorSeat = -1,
	MatchFlowState? MatchFlow = null)
{
	public const int CurrentSchemaVersion = 1;

	public ImmutableArray<PendingAttachmentRewardState> NormalizedPendingAttachmentRewards =>
		PendingAttachmentRewards.IsDefault
			? ImmutableArray<PendingAttachmentRewardState>.Empty
			: PendingAttachmentRewards;
	public ExtraActionQueueSnapshot NormalizedExtraActions =>
		ExtraActions ?? ExtraActionQueueSnapshot.Empty;
	public MatchFlowState NormalizedMatchFlow => MatchFlow ?? MatchFlowState.Empty;
}

public sealed partial record MatchReadModel(
	long Revision,
	int LifecycleGeneration,
	int TurnGeneration,
	int PlayerCount,
	int WinningScore,
	int CurrentTurnSeat,
	MatchOutcome Outcome,
	int WinnerSeat,
	ImmutableArray<PlayerStateSnapshot> Players,
	ImmutableArray<TableCardState> TableCards,
	long PendingPresentationBarrierId,
	ImmutableArray<long> PendingTableOrderRuntimeInstanceIds = default,
	MandatorySelectionState? MandatorySelection = null,
	ImmutableArray<PendingAttachmentRewardState> PendingAttachmentRewards = default,
	ExtraActionQueueSnapshot? ExtraActions = null,
	int CompletedTurnsSinceQuestionRefresh = 0,
	int QuestionRefreshAnchorSeat = -1,
	MatchFlowState? MatchFlow = null)
{
	public bool IsInitialized => PlayerCount > 0;
	public bool HasPendingPresentationBarrier => PendingPresentationBarrierId > 0;
	public ImmutableArray<long> NormalizedPendingTableOrderRuntimeInstanceIds =>
		PendingTableOrderRuntimeInstanceIds.IsDefault
			? ImmutableArray<long>.Empty
			: PendingTableOrderRuntimeInstanceIds;
	public MandatorySelectionState NormalizedMandatorySelection =>
		MandatorySelection ?? MandatorySelectionState.None;
	public ImmutableArray<PendingAttachmentRewardState> NormalizedPendingAttachmentRewards =>
		PendingAttachmentRewards.IsDefault
			? ImmutableArray<PendingAttachmentRewardState>.Empty
			: PendingAttachmentRewards;
	public ExtraActionQueueSnapshot NormalizedExtraActions =>
		ExtraActions ?? ExtraActionQueueSnapshot.Empty;
	public MatchFlowState NormalizedMatchFlow => MatchFlow ?? MatchFlowState.Empty;
}
