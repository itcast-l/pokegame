using System.Collections.Generic;

internal readonly record struct AdventureUpgradeRunSnapshot(
	int SchemaVersion,
	int CurrentAvatarStageNumber,
	int CurrentRunStageIndex,
	bool ShouldGrantRunStartUpgrade,
	bool ShouldGrantVictoryUpgrade,
	IReadOnlyList<string> AcquiredUpgradeIds)
{
	internal const int CurrentSchemaVersion = 1;
}
