using System.Collections.Immutable;

namespace Deckland.Rules;

public enum PendingCardPlacementMode
{
	None = 0,
	Leftmost = 1,
	BeforeTrackedAnchor = 2,
}

public enum PendingProgressionChoiceKind
{
	None = 0,
	SessionStart = 1,
	Milestone = 2,
	Victory = 3,
}

public sealed record MatchFlowState(
	int SuccessfulCapturesThisTurn = 0,
	int MainPlayerCapturesThisBattle = 0,
	bool MainPlayerRealTurnActive = false,
	bool MainPlayerRealTurnStartPending = false,
	bool QuestionRejudgeUsedThisBattle = false,
	bool HalfHealthRescueUsed = false,
	bool QuarterHealthRescueUsed = false,
	bool PendingBaseCardFacesUp = false,
	PendingCardPlacementMode PendingCardPlacement = PendingCardPlacementMode.None,
	int PendingCaptureDamageDelta = 0,
	long PendingPlacementAnchorRuntimeId = 0,
	long CaptureMarkRuntimeId = 0,
	long TurnCaptureMarkRuntimeId = 0,
	long SequentialRevealAnchorRuntimeId = 0,
	bool EnemyTurnStartControlChecked = false,
	bool HandDisableTriggeredThisTurn = false,
	int DisabledMainPlayerHandDisplayIndex = -1,
	int CaptureThresholdProgress = 0,
	bool CaptureThresholdReady = false,
	int PeriodicItemProgress = 0,
	int PeriodicItemActivationsThisMatch = 0,
	int DoubleEffectProgress = 0,
	bool DoubleEffectReady = false,
	int LimitedDoubleEffectActivationsThisMatch = 0,
	int DestructiveEffectCooldownTurns = 0,
	int MissThresholdProgress = 0,
	bool OneShotCardReplacementUsed = false,
	int StealThresholdProgress = 0,
	int LimitedStealThresholdProgress = 0,
	int LimitedStealActivationsThisMatch = 0,
	bool ActiveControlUsedThisTurn = false,
	int ScriptedSequenceStep = 0,
	int ScriptedTourIndex = 0,
	ImmutableArray<PendingProgressionChoiceKind> PendingProgressionChoices = default,
	int PendingWinnerSeat = -1,
	int BlockedAutomatedItemSeat = -1,
	string BlockedAutomatedItemId = "")
{
	public static MatchFlowState Empty { get; } = new();

	public static MatchFlowState NewMatch { get; } = Empty with
	{
		MainPlayerRealTurnActive = true,
	};

	public ImmutableArray<PendingProgressionChoiceKind> NormalizedPendingProgressionChoices =>
		PendingProgressionChoices.IsDefault
			? ImmutableArray<PendingProgressionChoiceKind>.Empty
			: PendingProgressionChoices;
}

public abstract record MatchFlowCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration)
	: MatchCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);

public sealed record ReplaceMatchFlowStateCommand(
	long ActionId,
	long ExpectedRevision,
	int LifecycleGeneration,
	int TurnGeneration,
	MatchFlowState State)
	: MatchFlowCommand(ActionId, ExpectedRevision, LifecycleGeneration, TurnGeneration);
