using System.Collections.Generic;

internal readonly record struct AdventureUpgradeRunSnapshot(
	int SchemaVersion,
	int CurrentAvatarStageNumber,
	int CurrentRunStageIndex,
	bool ShouldGrantRunStartUpgrade,
	bool ShouldGrantVictoryUpgrade,
	IReadOnlyList<string> AcquiredUpgradeIds,
	int CurrentHealth = 100,
	int MaxHealth = 100,
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
	bool ClubGapRootingUsedThisTurn = false,
	int FinalDoubleQuestionRewardCountThisBattle = 0)
{
	internal const int CurrentSchemaVersion = 3;
}
