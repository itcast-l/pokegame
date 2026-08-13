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
		normalizedDescription = RemoveHiddenImplementationCopy(upgradeId, normalizedDescription);
		bool hadCurrentItemCountToken = normalizedDescription.Contains(
			CurrentItemCountToken,
			StringComparison.Ordinal);
		if (values == null || values.Count == 0)
		{
			return normalizedDescription;
		}

		// Parenthetical value tokens are implementation metadata, not player-facing card text.
		foreach (string key in values.Keys)
		{
			string token = $"{{{key}}}";
			normalizedDescription = normalizedDescription.Replace(
				$"({token})",
				string.Empty,
				StringComparison.Ordinal);
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

		return normalizedDescription.Trim();
	}

	private static string RemoveHiddenImplementationCopy(string? upgradeId, string description)
	{
		if (string.Equals(upgradeId?.Trim(), "hearts_overflow_growth", StringComparison.Ordinal))
		{
			description = description.Replace(
				", up to {maximum}; carry up to {stack_limit} overflow between heals.",
				".",
				StringComparison.Ordinal);
		}
		else if (string.Equals(upgradeId?.Trim(), "diamonds_question_mark_windfall", StringComparison.Ordinal))
		{
			description = description.Replace(
				"On the Nth question reward (up to {maximum}): immediately deal min(N, {threshold}) damage. ({amount})",
				"Whenever you gain a question reward: immediately deal damage based on question rewards gained.",
				StringComparison.Ordinal);
		}

		return RemoveParentheticalAnnotation(description, "maximum")
			.Replace("({stack_limit})", string.Empty, StringComparison.Ordinal);
	}

	private static string RemoveParentheticalAnnotation(string description, string tokenKey)
	{
		string token = $"{{{tokenKey}}}";
		int openingParenthesis = description.IndexOf('(');
		while (openingParenthesis >= 0)
		{
			int closingParenthesis = description.IndexOf(')', openingParenthesis + 1);
			if (closingParenthesis < 0)
			{
				break;
			}

			int tokenIndex = description.IndexOf(token, openingParenthesis, closingParenthesis - openingParenthesis, StringComparison.Ordinal);
			if (tokenIndex >= 0)
			{
				description = description.Remove(openingParenthesis, closingParenthesis - openingParenthesis + 1);
				openingParenthesis = description.IndexOf('(', openingParenthesis);
				continue;
			}

			openingParenthesis = description.IndexOf('(', closingParenthesis + 1);
		}

		return description;
	}
}
