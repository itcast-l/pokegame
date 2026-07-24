using System;
using System.Collections.Generic;

public static class SkillCodexUnlockRules
{
	public static IReadOnlyList<string> ParseUpgradeIds(string? rawUpgradeIds)
	{
		var upgradeIds = new SortedSet<string>(StringComparer.Ordinal);
		if (string.IsNullOrWhiteSpace(rawUpgradeIds))
		{
			return Array.Empty<string>();
		}

		foreach (string rawUpgradeId in rawUpgradeIds.Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			string upgradeId = rawUpgradeId.Trim();
			if (!string.IsNullOrEmpty(upgradeId))
			{
				upgradeIds.Add(upgradeId);
			}
		}

		return new List<string>(upgradeIds);
	}

	public static string NormalizeUpgradeIdsCsv(string? rawUpgradeIds)
	{
		return string.Join("|", ParseUpgradeIds(rawUpgradeIds));
	}

	public static bool IsUnlocked(string? rawUpgradeIds, string? upgradeId)
	{
		string normalizedUpgradeId = upgradeId?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(normalizedUpgradeId))
		{
			return false;
		}

		foreach (string unlockedUpgradeId in ParseUpgradeIds(rawUpgradeIds))
		{
			if (string.Equals(unlockedUpgradeId, normalizedUpgradeId, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	public static string AddUnlockedUpgradeId(string? rawUpgradeIds, string? upgradeId)
	{
		var upgradeIds = new SortedSet<string>(ParseUpgradeIds(rawUpgradeIds), StringComparer.Ordinal);
		string normalizedUpgradeId = upgradeId?.Trim() ?? string.Empty;
		if (!string.IsNullOrEmpty(normalizedUpgradeId))
		{
			upgradeIds.Add(normalizedUpgradeId);
		}

		return string.Join("|", upgradeIds);
	}
}
