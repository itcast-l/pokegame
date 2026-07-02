using System;
using System.Collections.Generic;

internal sealed class AdventureUpgradeRunState
{
	private readonly HashSet<string> AcquiredUpgradeIds = new(StringComparer.Ordinal);

	internal bool IsActive { get; private set; }
	internal int CurrentAvatarStageNumber { get; private set; }
	internal int CurrentRunStageIndex { get; private set; }
	internal bool ShouldGrantRunStartUpgrade { get; private set; }
	internal bool ShouldGrantVictoryUpgrade { get; private set; }

	internal void BeginNewRun(int avatarStageNumber)
	{
		Clear();
		if (!AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(avatarStageNumber))
		{
			return;
		}

		IsActive = true;
		CurrentAvatarStageNumber = avatarStageNumber;
		CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndex(avatarStageNumber);
		ShouldGrantRunStartUpgrade = CurrentRunStageIndex == 1;
		ShouldGrantVictoryUpgrade = true;
	}

	internal void AdvanceToStage(int avatarStageNumber)
	{
		if (!IsActive)
		{
			BeginNewRun(avatarStageNumber);
			return;
		}

		if (!AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(avatarStageNumber))
		{
			return;
		}

		CurrentAvatarStageNumber = avatarStageNumber;
		CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndex(avatarStageNumber);
		ShouldGrantRunStartUpgrade = false;
		ShouldGrantVictoryUpgrade = true;
	}

	internal void MarkRunStartUpgradeGranted()
	{
		ShouldGrantRunStartUpgrade = false;
	}

	internal void MarkVictoryUpgradeGranted()
	{
		ShouldGrantVictoryUpgrade = false;
	}

	internal void AcquireUpgrade(string upgradeId)
	{
		if (string.IsNullOrWhiteSpace(upgradeId))
		{
			return;
		}

		AcquiredUpgradeIds.Add(upgradeId.Trim());
	}

	internal bool HasAcquiredUpgrade(string upgradeId)
	{
		return !string.IsNullOrWhiteSpace(upgradeId) && AcquiredUpgradeIds.Contains(upgradeId.Trim());
	}

	internal IReadOnlyCollection<string> GetAcquiredUpgradeIds()
	{
		return AcquiredUpgradeIds;
	}

	internal AdventureUpgradeRunSnapshot ToSnapshot()
	{
		return new AdventureUpgradeRunSnapshot(
			AdventureUpgradeRunSnapshot.CurrentSchemaVersion,
			CurrentAvatarStageNumber,
			CurrentRunStageIndex,
			ShouldGrantRunStartUpgrade,
			ShouldGrantVictoryUpgrade,
			new List<string>(AcquiredUpgradeIds));
	}

	internal void RestoreFromSnapshot(AdventureUpgradeRunSnapshot snapshot)
	{
		Clear();
		if (snapshot.SchemaVersion != AdventureUpgradeRunSnapshot.CurrentSchemaVersion
			|| !AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(snapshot.CurrentAvatarStageNumber))
		{
			return;
		}

		IsActive = true;
		CurrentAvatarStageNumber = snapshot.CurrentAvatarStageNumber;
		CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndex(snapshot.CurrentAvatarStageNumber);
		ShouldGrantRunStartUpgrade = snapshot.ShouldGrantRunStartUpgrade;
		ShouldGrantVictoryUpgrade = snapshot.ShouldGrantVictoryUpgrade;
		foreach (string upgradeId in snapshot.AcquiredUpgradeIds)
		{
			AcquireUpgrade(upgradeId);
		}
	}

	internal void Clear()
	{
		IsActive = false;
		CurrentAvatarStageNumber = 0;
		CurrentRunStageIndex = 0;
		ShouldGrantRunStartUpgrade = false;
		ShouldGrantVictoryUpgrade = false;
		AcquiredUpgradeIds.Clear();
	}
}
