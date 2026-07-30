using System.Collections.Generic;

internal enum AdventureRunWriteResult
{
	Succeeded,
	WriteNotAllowed,
	RecoveryRequired,
	InvalidStage,
	SaveFailed,
}

internal enum AdventureVictoryCommitResult
{
	Succeeded,
	AlreadyCommitted,
	InvalidStage,
	OutOfSequence,
	SaveFailed,
}

internal readonly record struct AdventureDefeatCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureDefeatResolution? Resolution);

internal readonly record struct AdventureUpgradeGrantCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureUpgradeAcquireResult AcquireResult,
	AdventureUpgradeAcquisitionEffectResult EffectResult,
	AdventureHealingResult? HealingResult);

internal readonly record struct AdventureHealingCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureHealingResult HealingResult);

internal readonly record struct AdventureQuestionRewardCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureQuestionRewardEffectResult EffectResult,
	AdventureHealingResult? HealingResult);

internal readonly record struct AdventureTurnStartCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureTurnStartEffectResult EffectResult,
	AdventureHealingResult? HealingResult);

internal readonly record struct AdventureDirectDamageCommitInput(
	int DamageAmount,
	bool HalfHealthRescueUsed = false,
	bool QuarterHealthRescueUsed = false);

internal readonly record struct AdventureDirectDamageCommitResult(
	AdventureRunWriteResult WriteResult,
	int PreviousHealth,
	int DamagedHealth,
	AdventureLowHealthRescueResult RescueResult,
	AdventureHealingResult HealingResult);

internal readonly record struct AdventureMissCommitInput(
	string PlayedCardRank,
	int TableCardCountAfterMiss,
	IReadOnlyCollection<string>? TriggeredAttachedItemCardIds = null);

internal readonly record struct AdventureMissCommitResult(
	AdventureRunWriteResult WriteResult,
	AdventureMainPlayerMissEffectResult EffectResult,
	AdventurePostCaptureEffectResult RecoveryEffectResult,
	AdventureHealingResult? HealingResult,
	bool CommitBandageAtRealTurnEnd);

internal readonly record struct AdventureClubCaptureCommitInput(
	int CompleteBaseCaptureCardCount,
	int UniqueShortReturnCandidateCount,
	int UniqueCombinedReturnCandidateCount,
	int AvailableHandSlots,
	bool PlayedCardWasBaseCard);

internal readonly record struct AdventureCaptureCommitInput(
	bool CapturerIsMainPlayer,
	string PlayedCardRank,
	bool ConsumeBorrowSuitSet,
	bool ConsumeGapRooting,
	AdventureCaptureDamageInput DamageInput,
	int SuccessfulCapturesThisTurn,
	CaptureResolutionContext CaptureContext,
	AdventureClubCaptureCommitInput ClubCapture,
	int PlannedQuestionRewardCount = 0,
	bool HalfHealthRescueUsed = false,
	bool QuarterHealthRescueUsed = false);

internal readonly record struct AdventureCaptureCommitResult(
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
