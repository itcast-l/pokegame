using System.Collections.Immutable;

namespace Deckland.Rules;

public readonly record struct AdventureEffectStateSnapshot(
	int PermanentCaptureDamageBonus = 0,
	int QuestionRewardCount = 0,
	bool FinalDoubleCaptureDamageActive = false,
	int HealingOverflowRemainder = 0,
	int NextCaptureDamageBonus = 0,
	int NextTurnHealAmount = 0,
	int NextTurnDrawCount = 0,
	bool ClubShortBranchReturnUsedThisTurn = false,
	int ClubMissClimbGrowthStacks = 0,
	bool ClubBorrowSuitSetArmed = false,
	bool ClubGapRootingUsedThisTurn = false);

public sealed record AdventureAuthoritySnapshot(
	long Revision,
	global::AdventureUpgradeRunSnapshot? RunSnapshot,
	ImmutableArray<long> AppliedActionIds,
	AdventureEffectStateSnapshot? Effects = null,
	MatchFlowState? MatchFlow = null)
{
	public ImmutableArray<long> NormalizedAppliedActionIds => AppliedActionIds.IsDefault
		? ImmutableArray<long>.Empty
		: AppliedActionIds;
	public MatchFlowState NormalizedMatchFlow => MatchFlow ?? MatchFlowState.Empty;
}

public enum AdventurePersistenceKind
{
	RunState,
	MainAdventureVictory,
}

public sealed record AdventurePersistenceRequest(
	global::AdventureUpgradeRunSnapshot? PreviousSnapshot,
	global::AdventureUpgradeRunSnapshot? CandidateSnapshot,
	bool ForcePersist,
	string UnlockedUpgradeId = "",
	AdventurePersistenceKind Kind = AdventurePersistenceKind.RunState,
	int CompletedMainStageIndex = 0,
	string EnemyAvatarId = "");

public delegate bool AdventurePersistenceHandler(AdventurePersistenceRequest request);

public abstract record AdventureCommand(long ActionId, long ExpectedRevision);

public sealed record BeginAdventureRunCommand(
	long ActionId,
	long ExpectedRevision,
	int MainStageIndex,
	string EnemyAvatarId)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record AdvanceAdventureStageCommand(
	long ActionId,
	long ExpectedRevision,
	int MainStageIndex,
	string EnemyAvatarId)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ResolveAdventureDefeatCommand(long ActionId, long ExpectedRevision)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record GrantAdventureUpgradeCommand(
	long ActionId,
	long ExpectedRevision,
	string UpgradeId,
	global::AdventureUpgradeTriggerSource TriggerSource)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record SetAdventureHealthCommand(
	long ActionId,
	long ExpectedRevision,
	int CurrentHealth,
	int MaxHealth)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ReplaceAdventureEffectStateCommand(
	long ActionId,
	long ExpectedRevision,
	AdventureEffectStateSnapshot EffectState)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record UpdateAdventureVitalsAndEffectsCommand(
	long ActionId,
	long ExpectedRevision,
	int CurrentHealth,
	int MaxHealth,
	AdventureEffectStateSnapshot EffectState)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ApplyAdventureHealingCommand(
	long ActionId,
	long ExpectedRevision,
	int HealAmount,
	int MaxHealthDelta = 0)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record UpdateAdventureHealthAndHealCommand(
	long ActionId,
	long ExpectedRevision,
	int CurrentHealth,
	int HealAmount)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ResolveAdventureQuestionRewardCommand(long ActionId, long ExpectedRevision)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ResolveAdventureTurnStartCommand(long ActionId, long ExpectedRevision)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ApplyAdventureDirectDamageCommand(
	long ActionId,
	long ExpectedRevision,
	global::AdventureDirectDamageCommitInput Input)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ResolveAdventureMissCommand(
	long ActionId,
	long ExpectedRevision,
	global::AdventureMissCommitInput Input)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ResolveAdventureCaptureCommand(
	long ActionId,
	long ExpectedRevision,
	global::AdventureCaptureCommitInput Input)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ResolveAdventureRealTurnEndCommand(
	long ActionId,
	long ExpectedRevision,
	bool CommitMissBandage)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ResetAdventureBattleEffectsCommand(long ActionId, long ExpectedRevision)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record ClearAdventureRunCommand(long ActionId, long ExpectedRevision)
	: AdventureCommand(ActionId, ExpectedRevision);

public sealed record CommitAdventureVictoryCommand(
	long ActionId,
	long ExpectedRevision,
	int CompletedMainStageIndex,
	string EnemyAvatarId)
	: AdventureCommand(ActionId, ExpectedRevision);

public abstract record AdventureCommandOutcome;

public sealed record AdventureDefeatCommandOutcome(global::AdventureDefeatResolution Resolution)
	: AdventureCommandOutcome;

public sealed record AdventureUpgradeGrantCommandOutcome(
	global::AdventureUpgradeAcquireResult AcquireResult,
	global::AdventureUpgradeAcquisitionEffectResult EffectResult,
	global::AdventureHealingResult? HealingResult)
	: AdventureCommandOutcome;

public sealed record AdventureHealingCommandOutcome(global::AdventureHealingResult HealingResult)
	: AdventureCommandOutcome;

public sealed record AdventureQuestionRewardCommandOutcome(
	global::AdventureQuestionRewardEffectResult EffectResult,
	global::AdventureHealingResult? HealingResult)
	: AdventureCommandOutcome;

public sealed record AdventureTurnStartCommandOutcome(
	global::AdventureTurnStartEffectResult EffectResult,
	global::AdventureHealingResult? HealingResult)
	: AdventureCommandOutcome;

public sealed record AdventureDirectDamageCommandOutcome(
	int PreviousHealth,
	int DamagedHealth,
	global::AdventureLowHealthRescueResult RescueResult,
	global::AdventureHealingResult HealingResult)
	: AdventureCommandOutcome;

public sealed record AdventureMissCommandOutcome(
	global::AdventureMainPlayerMissEffectResult EffectResult,
	global::AdventurePostCaptureEffectResult RecoveryEffectResult,
	global::AdventureHealingResult? HealingResult,
	bool CommitBandageAtRealTurnEnd)
	: AdventureCommandOutcome;

public sealed record AdventureCaptureCommandOutcome(
	global::AdventureCaptureDamageResult DamageResult,
	global::AdventurePostCaptureEffectResult PostCaptureEffectResult,
	global::AdventureClubCaptureEffectResult ClubCaptureEffectResult,
	global::AdventureQuestionRewardEffectResult QuestionRewardEffectResult,
	global::AdventureHealingResult? HealingResult,
	global::AdventureLowHealthRescueResult RescueResult,
	int CurrentHealth,
	bool ConsumedHalfHealthRescue,
	bool ConsumedQuarterHealthRescue)
	: AdventureCommandOutcome;

public sealed record AdventureRuleCommandResult(
	bool Accepted,
	RuleRejection Rejection,
	global::AdventureRunWriteResult WriteResult,
	long Revision,
	AdventureRunReadModel ReadModel,
	AdventureCommandOutcome? Outcome = null);
