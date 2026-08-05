using System;
using System.Collections.Generic;
using System.Globalization;

internal static class AdventureUpgradeDescriptionRules
{
	internal const string QuestionMarkInterestUpgradeId = "diamonds_question_mark_interest";
	internal const string CurrentItemCountToken = "{current_item_count}";

	internal static string Format(
		string? upgradeId,
		string? description,
		int currentItemCount)
	{
		if (string.Equals(
			upgradeId?.Trim(),
			QuestionMarkInterestUpgradeId,
			StringComparison.Ordinal))
		{
			return Format(
				upgradeId,
				description,
				new Dictionary<string, int>(StringComparer.Ordinal)
				{
					["current_item_count"] = Math.Max(0, currentItemCount),
				});
		}

		return description?.Trim() ?? string.Empty;
	}

	internal static string Format(
		string? upgradeId,
		string? description,
		IReadOnlyDictionary<string, int>? values)
	{
		string normalizedDescription = description?.Trim() ?? string.Empty;
		bool hadCurrentItemCountToken = normalizedDescription.Contains(
			CurrentItemCountToken,
			StringComparison.Ordinal);
		if (values == null || values.Count == 0)
		{
			return normalizedDescription;
		}

		foreach (KeyValuePair<string, int> pair in values)
		{
			string token = $"{{{pair.Key}}}";
			normalizedDescription = normalizedDescription.Replace(
				token,
				Math.Max(0, pair.Value).ToString(CultureInfo.InvariantCulture),
				StringComparison.Ordinal);
		}

		if (string.Equals(
			upgradeId?.Trim(),
			QuestionMarkInterestUpgradeId,
			StringComparison.Ordinal)
			&& values.TryGetValue("current_item_count", out int currentItemCount)
			&& !hadCurrentItemCountToken)
		{
			string countText = Math.Max(0, currentItemCount).ToString(CultureInfo.InvariantCulture);
			return normalizedDescription.Length == 0
				? countText
				: $"{normalizedDescription} ({countText})";
		}

		return normalizedDescription;
	}
}
