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
