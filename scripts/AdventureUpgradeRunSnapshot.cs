using System.Collections.Generic;
using System.Text.Json.Serialization;

internal enum AdventureUpgradeRestoreResult
{
	NoSnapshot,
	Restored,
	Migrated,
	DeferredManifestUnavailable,
	InvalidSnapshotPreserved,
	InvalidJsonPreserved,
}

internal enum AdventureUpgradeAcquireResult
{
	Acquired,
	AlreadyAcquired,
	DeferredManifestUnavailable,
	RejectedUnknownUpgrade,
}

internal readonly record struct AdventureUpgradeRunSnapshot(
	int SchemaVersion,
	int CurrentAvatarStageNumber,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int CurrentRunStageIndex,
	bool ShouldGrantRunStartUpgrade,
	bool ShouldGrantVictoryUpgrade,
	IReadOnlyList<string> AcquiredUpgradeIds,
	int CurrentHealth = 100,
	int MaxHealth = 100,
	int PermanentCaptureDamageBonus = 0,
	int QuestionRewardCount = 0,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool FinalDoubleCaptureDamageActive = false,
	int HealingOverflowRemainder = 0,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int NextCaptureDamageBonus = 0,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int NextTurnHealAmount = 0,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int NextTurnDrawCount = 0,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ClubShortBranchReturnUsedThisTurn = false,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ClubMissClimbGrowthStacks = 0,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ClubBorrowSuitSetArmed = false,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ClubGapRootingUsedThisTurn = false,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int FinalDoubleQuestionRewardCountThisBattle = 0,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ClubFlourishStacks = 0)
{
	internal const int CurrentSchemaVersion = 5;
}
