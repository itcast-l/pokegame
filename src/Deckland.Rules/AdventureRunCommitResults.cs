using System.Collections.Generic;

public enum AdventureRunWriteResult
{
	Succeeded,
	WriteNotAllowed,
	RecoveryRequired,
	InvalidStage,
	SaveFailed,
}

public enum AdventureVictoryCommitResult
{
	Succeeded,
	AlreadyCommitted,
	InvalidStage,
	OutOfSequence,
	SaveFailed,
}

public readonly record struct AdventureDefeatCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureDefeatResolution? Resolution);

public readonly record struct AdventureUpgradeGrantCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureUpgradeAcquireResult AcquireResult,
	AdventureUpgradeAcquisitionEffectResult EffectResult,
	AdventureHealingResult? HealingResult,
	string? ReplacedUpgradeId);

public readonly record struct AdventureHealingCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureHealingResult HealingResult);

public readonly record struct AdventureQuestionRewardCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureQuestionRewardEffectResult EffectResult,
	AdventureHealingResult? HealingResult);

public readonly record struct AdventureTurnStartCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureTurnStartEffectResult EffectResult,
	AdventureHealingResult? HealingResult);

public readonly record struct AdventureDirectDamageCommitInput(
	int DamageAmount,
	bool HalfHealthRescueUsed = false,
	bool QuarterHealthRescueUsed = false);

public readonly record struct AdventureDirectDamageCommitResult(
	AdventureRunWriteResult WriteResult,
	int PreviousHealth,
	int DamagedHealth,
	AdventureLowHealthRescueResult RescueResult,
	AdventureHealingResult HealingResult);

public readonly record struct AdventureMissCommitInput(
	string PlayedCardRank,
	int TableCardCountAfterMiss,
	IReadOnlyCollection<string>? TriggeredAttachedItemCardIds = null);

public readonly record struct AdventureMissCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureMainPlayerMissEffectResult EffectResult,
	AdventurePostCaptureEffectResult RecoveryEffectResult,
	AdventureHealingResult? HealingResult,
	bool CommitBandageAtRealTurnEnd);

public readonly record struct AdventureClubCaptureCommitInput(
	int CompleteBaseCaptureCardCount,
	int UniqueShortReturnCandidateCount,
	int UniqueCombinedReturnCandidateCount,
	int AvailableHandSlots,
	bool PlayedCardWasBaseCard);

public readonly record struct AdventureCaptureCommitInput(
	bool CapturerIsMainPlayer,
	string PlayedCardRank,
	bool ConsumeBorrowSuitSet,
	bool ConsumeGapRooting,
	AdventureCaptureDamageInput DamageInput,
	int SuccessfulCapturesThisTurn,
	AdventureCaptureResolutionFacts CaptureContext,
	AdventureClubCaptureCommitInput ClubCapture,
	int PlannedQuestionRewardCount = 0,
	bool HalfHealthRescueUsed = false,
	bool QuarterHealthRescueUsed = false);

public readonly record struct AdventureCaptureCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureCaptureDamageResult DamageResult,
	AdventurePostCaptureEffectResult PostCaptureEffectResult,
	AdventureClubCaptureEffectResult ClubCaptureEffectResult,
	AdventureQuestionRewardEffectResult QuestionRewardEffectResult,
	AdventureHealingResult? HealingResult,
	AdventureLowHealthRescueResult RescueResult,
	int CurrentHealth,
	bool ConsumedHalfHealthRescue,
	bool ConsumedQuarterHealthRescue);
