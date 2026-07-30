using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

internal readonly record struct AdventureUpgradeEffectValueContract(
	string Key,
	int DefaultValue,
	int MinimumValue,
	int MaximumValue)
{
	internal bool TryParse(string rawValue, out int value)
	{
		if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
		{
			return false;
		}

		return value >= MinimumValue && value <= MaximumValue;
	}
}

internal enum AdventureUpgradeStateScope
{
	None,
	Action,
	RootChain,
	Turn,
	Battle,
	Run,
}

internal enum AdventureUpgradeStackingMode
{
	Unique,
	Additive,
	Refresh,
	Max,
	MultiplierBucket,
}

[Flags]
internal enum AdventureUpgradeTriggerPolicy
{
	None = 0,
	OncePerAction = 1 << 0,
	OncePerTurn = 1 << 1,
	OncePerBattle = 1 << 2,
	AllowsExtraActionReentry = 1 << 3,
}

internal enum AdventureUpgradeLegacyIdMigrationPolicy
{
	KeepWhenEnabled,
	Discard,
}

internal sealed record AdventureUpgradeManifestEntry(
	string Id,
	string Suit,
	string Rarity,
	bool Enabled,
	bool HasRuntimeHandler,
	AdventureUpgradeStateScope StateScope,
	AdventureUpgradeStackingMode StackingMode,
	AdventureUpgradeTriggerPolicy TriggerPolicy,
	int MaximumValue,
	string ConflictGroup,
	int Priority,
	string DescriptionKey,
	AdventureUpgradeLegacyIdMigrationPolicy LegacyIdMigrationPolicy =
		AdventureUpgradeLegacyIdMigrationPolicy.KeepWhenEnabled,
	IReadOnlyDictionary<string, int>? EffectValues = null,
	IReadOnlyList<string>? DescriptionValueKeys = null);

internal sealed record AdventureUpgradeRuntimeMetadata(
	bool HasRuntimeHandler,
	AdventureUpgradeStateScope StateScope,
	AdventureUpgradeStackingMode StackingMode,
	AdventureUpgradeTriggerPolicy TriggerPolicy,
	int MaximumValue,
	string ConflictGroup,
	int Priority,
	IReadOnlyDictionary<string, AdventureUpgradeEffectValueContract> EffectValues,
	IReadOnlyList<string> DescriptionValueKeys);

internal static class AdventureUpgradeRuntimeContract
{
	private static readonly ReadOnlyDictionary<string, IReadOnlyDictionary<string, AdventureUpgradeEffectValueContract>> EffectValuesById =
		new(BuildEffectValues());

	private static readonly ReadOnlyDictionary<string, AdventureUpgradeRuntimeMetadata> MetadataById =
		new(BuildMetadata());

	internal static IReadOnlyDictionary<string, AdventureUpgradeRuntimeMetadata> All => MetadataById;

	internal static bool TryGetMetadata(
		string? upgradeId,
		out AdventureUpgradeRuntimeMetadata metadata)
	{
		if (string.IsNullOrWhiteSpace(upgradeId))
		{
			metadata = default!;
			return false;
		}

		return MetadataById.TryGetValue(upgradeId.Trim(), out metadata!);
	}

	internal static bool TryValidateEffectValues(
		string upgradeId,
		string rawValues,
		out IReadOnlyDictionary<string, int> values,
		out string errorMessage)
	{
		values = new Dictionary<string, int>(StringComparer.Ordinal);
		errorMessage = string.Empty;
		if (!MetadataById.TryGetValue(upgradeId?.Trim() ?? string.Empty, out AdventureUpgradeRuntimeMetadata? metadata))
		{
			errorMessage = $"Unknown adventure upgrade '{upgradeId}'.";
			return false;
		}

		if (!CardEffectValues.TryParse(rawValues ?? string.Empty, out CardEffectValues parsed))
		{
			errorMessage = $"Adventure upgrade '{upgradeId}' has malformed effect_values.";
			return false;
		}

		if (parsed.All.Count != metadata.EffectValues.Count)
		{
			errorMessage = $"Adventure upgrade '{upgradeId}' has an incomplete effect_values key set.";
			return false;
		}

		var normalized = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, string> pair in parsed.All)
		{
			if (!metadata.EffectValues.TryGetValue(pair.Key, out AdventureUpgradeEffectValueContract contract)
				|| !contract.TryParse(pair.Value, out int value))
			{
				errorMessage = $"Adventure upgrade '{upgradeId}' has unknown or out-of-range effect value '{pair.Key}'.";
				return false;
			}

			normalized[pair.Key] = value;
		}

		values = new ReadOnlyDictionary<string, int>(normalized);
		return true;
	}

	internal static bool TryValidateDescriptionValueKeys(
		string upgradeId,
		string rawKeys,
		out IReadOnlyList<string> keys,
		out string errorMessage)
	{
		errorMessage = string.Empty;
		string[] parsedKeys = string.IsNullOrWhiteSpace(rawKeys)
			? Array.Empty<string>()
			: rawKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (!MetadataById.TryGetValue(upgradeId?.Trim() ?? string.Empty, out AdventureUpgradeRuntimeMetadata? metadata)
			|| parsedKeys.Length != metadata.DescriptionValueKeys.Count)
		{
			keys = Array.Empty<string>();
			errorMessage = $"Adventure upgrade '{upgradeId}' has an invalid description_value_keys list.";
			return false;
		}

		for (int index = 0; index < parsedKeys.Length; index++)
		{
			if (!string.Equals(parsedKeys[index], metadata.DescriptionValueKeys[index], StringComparison.Ordinal))
			{
				keys = Array.Empty<string>();
				errorMessage = $"Adventure upgrade '{upgradeId}' has an invalid description_value_keys order.";
				return false;
			}
		}

		keys = parsedKeys;
		return true;
	}

	private static Dictionary<string, AdventureUpgradeRuntimeMetadata> BuildMetadata()
	{
		var metadata = new Dictionary<string, AdventureUpgradeRuntimeMetadata>(StringComparer.Ordinal);

		Add(metadata, "hearts_warm_current_supply", AdventureUpgradeStateScope.Turn, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerTurn, 1);
		Add(metadata, "hearts_long_capture_recovery", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "hearts_miss_bandage", AdventureUpgradeStateScope.Turn, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.OncePerTurn, 3);
		Add(metadata, "hearts_question_mark_first_aid", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 4);
		Add(metadata, "hearts_shoal_preparation", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerBattle, 1);
		Add(metadata, "hearts_thick_blood", AdventureUpgradeStateScope.Run, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.None, 150);
		Add(metadata, "hearts_chain_tide_breath", AdventureUpgradeStateScope.Turn, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerTurn, 1);
		Add(metadata, "hearts_red_tide_return", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerBattle, 1);
		Add(metadata, "hearts_supply_cache_return", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "hearts_enchanted_recovery", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Max, AdventureUpgradeTriggerPolicy.OncePerAction, 3);
		Add(metadata, "hearts_deep_tide_lung", AdventureUpgradeStateScope.Run, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerBattle, 150);
		Add(metadata, "hearts_return_tide_heart", AdventureUpgradeStateScope.Turn, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerTurn, 2);
		Add(metadata, "hearts_revival_draw", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerBattle, 1);
		Add(metadata, "hearts_overflow_growth", AdventureUpgradeStateScope.Run, AdventureUpgradeStackingMode.Additive, AdventureUpgradeTriggerPolicy.None, 3);

		Add(metadata, "diamonds_small_insurance", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Max, AdventureUpgradeTriggerPolicy.OncePerAction, 5);
		Add(metadata, "diamonds_question_mark_raise", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "diamonds_rejudge_once", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerBattle, 1);
		Add(metadata, "diamonds_miss_discount", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Additive, AdventureUpgradeTriggerPolicy.None, int.MaxValue);
		Add(metadata, "diamonds_first_capture_pressure", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerBattle, 1);
		Add(metadata, "diamonds_question_mark_windfall", AdventureUpgradeStateScope.Run, AdventureUpgradeStackingMode.Max, AdventureUpgradeTriggerPolicy.OncePerAction, 10);
		Add(metadata, "diamonds_bounty_amplify", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 3);
		Add(metadata, "diamonds_question_mark_echo", AdventureUpgradeStateScope.Run, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.None, 1);
		Add(metadata, "diamonds_multi_card_premium", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "diamonds_question_mark_interest", AdventureUpgradeStateScope.Run, AdventureUpgradeStackingMode.Additive, AdventureUpgradeTriggerPolicy.OncePerAction, 5);
		Add(metadata, "diamonds_top_prize_repeat", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "diamonds_full_payout", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.MultiplierBucket, AdventureUpgradeTriggerPolicy.OncePerAction, 1, "capture_multiplier_bucket");
		Add(metadata, "diamonds_final_double", AdventureUpgradeStateScope.Run, AdventureUpgradeStackingMode.MultiplierBucket, AdventureUpgradeTriggerPolicy.None, 1, "capture_multiplier_bucket");

		Add(metadata, "spades_dark_harbor_reveal", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "spades_wrong_shore_lineup", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.OncePerAction, 1, "enemy_insert_position", 10);
		Add(metadata, "spades_missed_beat_blunt_hurt", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.OncePerAction, 3);
		Add(metadata, "spades_miss_bleed", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 2);
		Add(metadata, "spades_trace_hunt", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.OncePerAction, 4);
		Add(metadata, "spades_reverse_capture_tax", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Max, AdventureUpgradeTriggerPolicy.OncePerAction, int.MaxValue, "enemy_capture_reduction", 10);
		Add(metadata, "spades_tail_pressure", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "spades_return_tide_misorder", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.OncePerAction, 1, "enemy_insert_position", 20);
		Add(metadata, "spades_exaction", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Max, AdventureUpgradeTriggerPolicy.OncePerAction, 4, "enemy_capture_reduction", 20);
		Add(metadata, "spades_hunt_order", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.OncePerTurn, 6);
		Add(metadata, "spades_intercept_beat", AdventureUpgradeStateScope.RootChain, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "spades_black_ledger", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);

		Add(metadata, "clubs_short_branch_return", AdventureUpgradeStateScope.Turn, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerTurn, 1);
		Add(metadata, "clubs_gap_rooting", AdventureUpgradeStateScope.Turn, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerTurn, 1);
		Add(metadata, "clubs_primary_root_return", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "clubs_miss_climb_growth", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Additive, AdventureUpgradeTriggerPolicy.None, 9);
		Add(metadata, "clubs_borrow_suit_set", AdventureUpgradeStateScope.Turn, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.OncePerTurn, 1);
		Add(metadata, "clubs_hollow_burst", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "clubs_hanging_reverse", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1, "main_miss_table_order", 20);
		Add(metadata, "clubs_wild_branch", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1, "main_miss_table_order", 10);
		Add(metadata, "clubs_canopy_reveal", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.None, 1);
		Add(metadata, "clubs_continue_branch", AdventureUpgradeStateScope.RootChain, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerTurn, 1);
		Add(metadata, "clubs_linked_stems", AdventureUpgradeStateScope.Turn, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerTurn, 1);
		Add(metadata, "clubs_cross_graft", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.None, 1);
		Add(metadata, "clubs_crown_canopy", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);

		return metadata;
	}

	private static void Add(
		IDictionary<string, AdventureUpgradeRuntimeMetadata> metadata,
		string id,
		AdventureUpgradeStateScope stateScope,
		AdventureUpgradeStackingMode stackingMode,
		AdventureUpgradeTriggerPolicy triggerPolicy,
		int maximumValue,
		string conflictGroup = "",
		int priority = 0)
	{
		IReadOnlyDictionary<string, AdventureUpgradeEffectValueContract> effectValues =
			EffectValuesById.TryGetValue(id, out IReadOnlyDictionary<string, AdventureUpgradeEffectValueContract>? configuredValues)
				? configuredValues
				: new ReadOnlyDictionary<string, AdventureUpgradeEffectValueContract>(
					new Dictionary<string, AdventureUpgradeEffectValueContract>(StringComparer.Ordinal));
		metadata.Add(id, new AdventureUpgradeRuntimeMetadata(
			HasRuntimeHandler: true,
			stateScope,
			stackingMode,
			triggerPolicy,
			maximumValue,
			conflictGroup,
			priority,
			effectValues,
			BuildDescriptionValueKeys(id, effectValues)));
	}

	private static Dictionary<string, IReadOnlyDictionary<string, AdventureUpgradeEffectValueContract>> BuildEffectValues()
	{
		var values = new Dictionary<string, IReadOnlyDictionary<string, AdventureUpgradeEffectValueContract>>(StringComparer.Ordinal);
		AddValues(values, "hearts_warm_current_supply", Value("count", 1, 0, 52));
		AddValues(values, "hearts_long_capture_recovery", Value("threshold", 6, 0, 52), Value("amount", 2, 0, 999));
		AddValues(values, "hearts_miss_bandage", Value("amount", 3, 0, 999), Value("count", 1, 0, 52));
		AddValues(values, "hearts_question_mark_first_aid", Value("amount", 4, 0, 999));
		AddValues(values, "hearts_shoal_preparation", Value("count", 1, 0, 52));
		AddValues(values, "hearts_thick_blood", Value("amount", 4, 0, 999), Value("maximum", 150, 1, 999));
		AddValues(values, "hearts_chain_tide_breath", Value("count", 1, 0, 52), Value("amount", 2, 0, 999));
		AddValues(values, "hearts_red_tide_return", Value("threshold", 50, 0, 52), Value("amount", 5, 0, 999), Value("count", 2, 0, 52));
		AddValues(values, "hearts_supply_cache_return", Value("amount", 3, 0, 999), Value("count", 1, 0, 52));
		AddValues(values, "hearts_enchanted_recovery", Value("amount", 3, 0, 999), Value("count", 1, 0, 52));
		AddValues(values, "hearts_deep_tide_lung", Value("amount", 8, 0, 999), Value("count", 1, 0, 52));
		AddValues(values, "hearts_return_tide_heart", Value("first_count", 1, 0, 52), Value("first_amount", 2, 0, 999), Value("second_count", 1, 0, 52), Value("second_amount", 3, 0, 999));
		AddValues(values, "hearts_revival_draw", Value("threshold", 25, 0, 52), Value("amount", 8, 0, 999), Value("count", 2, 0, 52));
		AddValues(values, "hearts_overflow_growth", Value("step", 4, 0, 52), Value("amount", 1, 0, 999), Value("maximum", 150, 1, 999), Value("stack_limit", 3, 1, 99));

		AddValues(values, "diamonds_small_insurance", Value("threshold", 4, 0, 52), Value("amount", 5, 0, 999));
		AddValues(values, "diamonds_question_mark_raise", Value("count", 1, 0, 52));
		AddValues(values, "diamonds_rejudge_once");
		AddValues(values, "diamonds_miss_discount", Value("amount", 1, 0, 999));
		AddValues(values, "diamonds_first_capture_pressure", Value("amount", 2, 0, 999));
		AddValues(values, "diamonds_question_mark_windfall", Value("maximum", 10, 1, 999), Value("amount", 1, 0, 999), Value("threshold", 5, 0, 52));
		AddValues(values, "diamonds_bounty_amplify", Value("amount", 3, 0, 999));
		AddValues(values, "diamonds_question_mark_echo", Value("amount", 1, 0, 999));
		AddValues(values, "diamonds_multi_card_premium", Value("threshold", 3, 0, 52), Value("amount", 1, 0, 999));
		AddValues(values, "diamonds_question_mark_interest", Value("amount", 1, 0, 999), Value("maximum", 5, 1, 999));
		AddValues(values, "diamonds_top_prize_repeat", Value("threshold", 3, 0, 52), Value("count", 1, 0, 52));
		AddValues(values, "diamonds_full_payout", Value("threshold", 6, 0, 52), Value("multiplier", 1, 1, 10));
		AddValues(values, "diamonds_final_double", Value("threshold", 10, 0, 52), Value("multiplier", 1, 1, 10));

		AddValues(values, "spades_dark_harbor_reveal");
		AddValues(values, "spades_wrong_shore_lineup");
		AddValues(values, "spades_missed_beat_blunt_hurt", Value("amount", 3, 0, 999));
		AddValues(values, "spades_miss_bleed", Value("threshold", 6, 0, 52), Value("amount", 2, 0, 999));
		AddValues(values, "spades_trace_hunt", Value("amount", 4, 0, 999));
		AddValues(values, "spades_reverse_capture_tax", Value("amount", 1, 0, 999));
		AddValues(values, "spades_tail_pressure", Value("threshold", 7, 0, 52));
		AddValues(values, "spades_return_tide_misorder");
		AddValues(values, "spades_exaction", Value("amount", 4, 0, 999));
		AddValues(values, "spades_hunt_order", Value("amount", 6, 0, 999));
		AddValues(values, "spades_intercept_beat", Value("count", 1, 0, 52));
		AddValues(values, "spades_black_ledger", Value("count", 1, 0, 52));

		AddValues(values, "clubs_short_branch_return", Value("count", 2, 0, 52));
		AddValues(values, "clubs_gap_rooting", Value("count", 1, 0, 52));
		AddValues(values, "clubs_primary_root_return", Value("count", 1, 0, 52));
		AddValues(values, "clubs_miss_climb_growth", Value("amount", 1, 0, 999), Value("stack_limit", 9, 1, 99));
		AddValues(values, "clubs_borrow_suit_set", Value("count", 1, 0, 52));
		AddValues(values, "clubs_hollow_burst");
		AddValues(values, "clubs_hanging_reverse");
		AddValues(values, "clubs_wild_branch");
		AddValues(values, "clubs_canopy_reveal");
		AddValues(values, "clubs_continue_branch", Value("count", 1, 0, 52), Value("threshold", 6, 0, 52));
		AddValues(values, "clubs_linked_stems", Value("count", 1, 0, 52), Value("threshold", 2, 0, 52));
		AddValues(values, "clubs_cross_graft", Value("step", 1, 0, 52));
		AddValues(values, "clubs_crown_canopy", Value("count", 1, 0, 52));
		return values;
	}

	private static AdventureUpgradeEffectValueContract Value(
		string key,
		int defaultValue,
		int minimumValue,
		int maximumValue)
	{
		return new AdventureUpgradeEffectValueContract(key, defaultValue, minimumValue, maximumValue);
	}

	private static void AddValues(
		IDictionary<string, IReadOnlyDictionary<string, AdventureUpgradeEffectValueContract>> values,
		string id,
		params AdventureUpgradeEffectValueContract[] contracts)
	{
		var byKey = new Dictionary<string, AdventureUpgradeEffectValueContract>(StringComparer.Ordinal);
		foreach (AdventureUpgradeEffectValueContract contract in contracts)
		{
			byKey.Add(contract.Key, contract);
		}

		values.Add(id, new ReadOnlyDictionary<string, AdventureUpgradeEffectValueContract>(byKey));
	}

	private static IReadOnlyList<string> BuildDescriptionValueKeys(
		string id,
		IReadOnlyDictionary<string, AdventureUpgradeEffectValueContract> effectValues)
	{
		if (string.Equals(id, "diamonds_question_mark_interest", StringComparison.Ordinal))
		{
			return new[] { "current_item_count" };
		}
		if (string.Equals(id, "diamonds_question_mark_windfall", StringComparison.Ordinal))
		{
			return new[] { "maximum", "threshold", "amount" };
		}
		if (string.Equals(id, "clubs_continue_branch", StringComparison.Ordinal))
		{
			return new[] { "threshold", "count" };
		}

		return new List<string>(effectValues.Keys).AsReadOnly();
	}
}

internal sealed class AdventureUpgradeManifest
{
	private readonly ReadOnlyCollection<AdventureUpgradeManifestEntry> _entries;
	private readonly ReadOnlyDictionary<string, AdventureUpgradeManifestEntry> _enabledById;
	private readonly ReadOnlyCollection<string> _enabledIds;

	internal AdventureUpgradeManifest(IEnumerable<AdventureUpgradeManifestEntry>? entries)
	{
		var normalizedEntries = new List<AdventureUpgradeManifestEntry>();
		var manifestIds = new HashSet<string>(StringComparer.Ordinal);
		var enabledById = new Dictionary<string, AdventureUpgradeManifestEntry>(StringComparer.Ordinal);
		if (entries != null)
		{
			foreach (AdventureUpgradeManifestEntry? entry in entries)
			{
				if (entry == null)
				{
					throw new ArgumentException(
						"Adventure upgrade manifest entries cannot be null.",
						nameof(entries));
				}

				string normalizedId = entry.Id?.Trim() ?? string.Empty;
				if (normalizedId.Length == 0)
				{
					throw new ArgumentException("Adventure upgrade manifest entries require a non-empty ID.", nameof(entries));
				}
				if (!manifestIds.Add(normalizedId))
				{
					throw new ArgumentException(
						$"Adventure upgrade manifest contains duplicate ID '{normalizedId}'.",
						nameof(entries));
				}

				AdventureUpgradeManifestEntry normalizedEntry = entry with
				{
					Id = normalizedId,
					Suit = entry.Suit?.Trim() ?? string.Empty,
					Rarity = entry.Rarity?.Trim() ?? string.Empty,
					MaximumValue = Math.Max(0, entry.MaximumValue),
					ConflictGroup = entry.ConflictGroup?.Trim() ?? string.Empty,
					DescriptionKey = entry.DescriptionKey?.Trim() ?? string.Empty,
					EffectValues = NormalizeEffectValues(entry.EffectValues),
					DescriptionValueKeys = NormalizeDescriptionValueKeys(entry.DescriptionValueKeys),
				};
				normalizedEntries.Add(normalizedEntry);
				if (!normalizedEntry.Enabled)
				{
					continue;
				}

				enabledById.Add(normalizedId, normalizedEntry);
			}
		}

		normalizedEntries.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
		var enabledIds = new List<string>(enabledById.Keys);
		enabledIds.Sort(StringComparer.Ordinal);
		_entries = new ReadOnlyCollection<AdventureUpgradeManifestEntry>(normalizedEntries);
		_enabledById = new ReadOnlyDictionary<string, AdventureUpgradeManifestEntry>(enabledById);
		_enabledIds = new ReadOnlyCollection<string>(enabledIds);
	}

	internal IReadOnlyList<AdventureUpgradeManifestEntry> Entries => _entries;
	internal IReadOnlyList<string> EnabledIds => _enabledIds;
	internal bool IsReady => _enabledIds.Count > 0;

	internal bool ContainsEnabledId(string? upgradeId)
	{
		return !string.IsNullOrWhiteSpace(upgradeId)
			&& _enabledById.ContainsKey(upgradeId.Trim());
	}

	internal bool TryGetEnabledEntry(
		string? upgradeId,
		out AdventureUpgradeManifestEntry entry)
	{
		if (string.IsNullOrWhiteSpace(upgradeId))
		{
			entry = default!;
			return false;
		}

		return _enabledById.TryGetValue(upgradeId.Trim(), out entry!);
	}

	internal bool CanRestoreUpgradeId(string? upgradeId, int sourceSchemaVersion)
	{
		if (!TryGetEnabledEntry(upgradeId, out AdventureUpgradeManifestEntry entry))
		{
			return false;
		}

		return sourceSchemaVersion >= 6
			|| entry.LegacyIdMigrationPolicy
				== AdventureUpgradeLegacyIdMigrationPolicy.KeepWhenEnabled;
	}

	internal static AdventureUpgradeManifest FromEnabledIds(IEnumerable<string>? enabledIds)
	{
		var entries = new List<AdventureUpgradeManifestEntry>();
		if (enabledIds != null)
		{
			foreach (string id in enabledIds)
			{
				if (AdventureUpgradeRuntimeContract.TryGetMetadata(id, out AdventureUpgradeRuntimeMetadata metadata))
				{
					var effectValues = new Dictionary<string, int>(StringComparer.Ordinal);
					foreach (KeyValuePair<string, AdventureUpgradeEffectValueContract> pair in metadata.EffectValues)
					{
						effectValues[pair.Key] = pair.Value.DefaultValue;
					}

					entries.Add(new AdventureUpgradeManifestEntry(
						id,
						string.Empty,
						string.Empty,
						Enabled: true,
						metadata.HasRuntimeHandler,
						metadata.StateScope,
						metadata.StackingMode,
						metadata.TriggerPolicy,
						metadata.MaximumValue,
						metadata.ConflictGroup,
						metadata.Priority,
						DescriptionKey: string.Empty,
						EffectValues: effectValues,
						DescriptionValueKeys: metadata.DescriptionValueKeys));
					continue;
				}

				entries.Add(new AdventureUpgradeManifestEntry(
					id,
					string.Empty,
					string.Empty,
					Enabled: true,
					HasRuntimeHandler: true,
					AdventureUpgradeStateScope.None,
					AdventureUpgradeStackingMode.Unique,
					AdventureUpgradeTriggerPolicy.None,
					MaximumValue: 1,
					ConflictGroup: string.Empty,
					Priority: 0,
					DescriptionKey: string.Empty));
			}
		}

		return new AdventureUpgradeManifest(entries);
	}

	private static IReadOnlyDictionary<string, int> NormalizeEffectValues(
		IReadOnlyDictionary<string, int>? values)
	{
		var copy = new Dictionary<string, int>(StringComparer.Ordinal);
		if (values != null)
		{
			foreach (KeyValuePair<string, int> pair in values)
			{
				copy[pair.Key?.Trim() ?? string.Empty] = pair.Value;
			}
		}

		return new ReadOnlyDictionary<string, int>(copy);
	}

	private static IReadOnlyList<string> NormalizeDescriptionValueKeys(
		IReadOnlyList<string>? keys)
	{
		var copy = new List<string>();
		if (keys != null)
		{
			foreach (string? key in keys)
			{
				copy.Add(key?.Trim() ?? string.Empty);
			}
		}

		return copy.AsReadOnly();
	}
}
