using System;
using System.Collections.Generic;

internal static class AdventureUpgradeDefinitionDescriptionFormatter
{
	internal static string Format(
		AdventureSuitUpgradeDefinition definition,
		AdventureUpgradeManifestEntry? manifestEntry,
		string? localizedDescription,
		int currentItemCount)
	{
		var values = new Dictionary<string, int>(StringComparer.Ordinal);
		if (manifestEntry?.EffectValues != null)
		{
			foreach (KeyValuePair<string, int> pair in manifestEntry.EffectValues)
			{
				values[pair.Key] = pair.Value;
			}
		}

		if (string.Equals(
			definition.Id,
			AdventureUpgradeDescriptionRules.QuestionMarkInterestUpgradeId,
			StringComparison.Ordinal))
		{
			values["current_item_count"] = Math.Max(0, currentItemCount);
		}

		return AdventureUpgradeDescriptionRules.Format(
			definition.Id,
			localizedDescription,
			values);
	}
}
