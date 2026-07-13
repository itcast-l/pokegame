using System;
using System.Globalization;

internal static class AdventureUpgradeDescriptionRules
{
	internal const string QuestionMarkInterestUpgradeId = "diamonds_question_mark_interest";
	internal const string CurrentItemCountToken = "{current_item_count}";

	internal static string Format(
		string upgradeId,
		string description,
		int currentItemCount)
	{
		string normalizedDescription = description?.Trim() ?? string.Empty;
		if (!string.Equals(
			upgradeId?.Trim(),
			QuestionMarkInterestUpgradeId,
			StringComparison.Ordinal))
		{
			return normalizedDescription;
		}

		string countText = Math.Max(0, currentItemCount).ToString(CultureInfo.InvariantCulture);
		if (normalizedDescription.Contains(CurrentItemCountToken, StringComparison.Ordinal))
		{
			return normalizedDescription.Replace(
				CurrentItemCountToken,
				countText,
				StringComparison.Ordinal);
		}

		return normalizedDescription.Length == 0
			? countText
			: $"{normalizedDescription} ({countText})";
	}
}
