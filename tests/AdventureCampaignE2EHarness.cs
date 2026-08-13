using System;
using System.Collections.Generic;
using Godot;

public partial class AdventureCampaignE2EHarness : RefCounted
{
	public string[] GetMainAvatarIds()
	{
		var avatarIds = new string[AdventureCampaignCatalog.MainStageCount];
		for (int index = 0; index < avatarIds.Length; index++)
		{
			avatarIds[index] = AdventureCampaignCatalog.GetMainAvatarId(index + 1);
		}

		return avatarIds;
	}

	public bool IsMainStageUnlocked(int stageNumber)
	{
		return CharacterAvatarCatalog.IsAdventureStageUnlocked(stageNumber);
	}

	public bool IsAvatarUnlocked(string avatarId)
	{
		return CharacterAvatarCatalog.IsAvatarUnlocked(avatarId);
	}

	public string ConsumePendingAvatarUnlock()
	{
		return CharacterAvatarCatalog.ConsumePendingAdventureAvatarUnlock();
	}

	public int GetUnlockedSkillCount(string rawUpgradeIds)
	{
		return SkillCodexUnlockRules.ParseUpgradeIds(rawUpgradeIds).Count;
	}

	public bool HasDuplicateUnlockedSkills(string rawUpgradeIds)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		if (string.IsNullOrWhiteSpace(rawUpgradeIds))
		{
			return false;
		}

		foreach (string rawUpgradeId in rawUpgradeIds.Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			string upgradeId = rawUpgradeId.Trim();
			if (!string.IsNullOrEmpty(upgradeId) && !seen.Add(upgradeId))
			{
				return true;
			}
		}

		return false;
	}
}
