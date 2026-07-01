using System;
using System.Collections.Generic;

internal static class AdventureUpgradeRunState
{
	private static readonly HashSet<string> AcquiredUpgradeIds = new(StringComparer.Ordinal);

	internal static bool IsActive { get; private set; }
	internal static int CurrentAvatarStageNumber { get; private set; }
	internal static int CurrentRunStageIndex { get; private set; }
	internal static bool ShouldGrantRunStartUpgrade { get; private set; }
	internal static bool ShouldGrantVictoryUpgrade { get; private set; }

	internal static void BeginNewRun(int avatarStageNumber)
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

	internal static void AdvanceToStage(int avatarStageNumber)
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

	internal static void MarkRunStartUpgradeGranted()
	{
		ShouldGrantRunStartUpgrade = false;
	}

	internal static void MarkVictoryUpgradeGranted()
	{
		ShouldGrantVictoryUpgrade = false;
	}

	internal static void AcquireUpgrade(string upgradeId)
	{
		if (string.IsNullOrWhiteSpace(upgradeId))
		{
			return;
		}

		AcquiredUpgradeIds.Add(upgradeId.Trim());
	}

	internal static bool HasAcquiredUpgrade(string upgradeId)
	{
		return !string.IsNullOrWhiteSpace(upgradeId) && AcquiredUpgradeIds.Contains(upgradeId.Trim());
	}

	internal static IReadOnlyCollection<string> GetAcquiredUpgradeIds()
	{
		return AcquiredUpgradeIds;
	}

	internal static void Clear()
	{
		IsActive = false;
		CurrentAvatarStageNumber = 0;
		CurrentRunStageIndex = 0;
		ShouldGrantRunStartUpgrade = false;
		ShouldGrantVictoryUpgrade = false;
		AcquiredUpgradeIds.Clear();
	}
}
