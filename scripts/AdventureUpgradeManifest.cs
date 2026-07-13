using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
		AdventureUpgradeLegacyIdMigrationPolicy.KeepWhenEnabled);

internal sealed record AdventureUpgradeRuntimeMetadata(
	bool HasRuntimeHandler,
	AdventureUpgradeStateScope StateScope,
	AdventureUpgradeStackingMode StackingMode,
	AdventureUpgradeTriggerPolicy TriggerPolicy,
	int MaximumValue,
	string ConflictGroup,
	int Priority);

internal static class AdventureUpgradeRuntimeContract
{
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
		Add(metadata, "diamonds_high_return_bonus", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.OncePerAction, 2);
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
		Add(metadata, "clubs_primary_root_return", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1, "played_card_return", 30);
		Add(metadata, "clubs_miss_climb_growth", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Additive, AdventureUpgradeTriggerPolicy.None, AdventureUpgradeEffectState.MaxClubMissClimbGrowthStacks);
		Add(metadata, "clubs_borrow_suit_set", AdventureUpgradeStateScope.Turn, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.OncePerTurn, 1);
		Add(metadata, "clubs_hollow_burst", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1);
		Add(metadata, "clubs_hanging_reverse", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1, "main_miss_table_order", 20);
		Add(metadata, "clubs_wild_branch", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1, "main_miss_table_order", 10);
		Add(metadata, "clubs_canopy_reveal", AdventureUpgradeStateScope.Battle, AdventureUpgradeStackingMode.Refresh, AdventureUpgradeTriggerPolicy.None, 1);
		Add(metadata, "clubs_continue_branch", AdventureUpgradeStateScope.RootChain, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerTurn, 1);
		Add(metadata, "clubs_linked_stems", AdventureUpgradeStateScope.Turn, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerTurn, 1);
		Add(metadata, "clubs_climb_canopy", AdventureUpgradeStateScope.Action, AdventureUpgradeStackingMode.Unique, AdventureUpgradeTriggerPolicy.OncePerAction, 1, "played_card_return", 20);
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
		metadata.Add(id, new AdventureUpgradeRuntimeMetadata(
			HasRuntimeHandler: true,
			stateScope,
			stackingMode,
			triggerPolicy,
			maximumValue,
			conflictGroup,
			priority));
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

		return sourceSchemaVersion >= AdventureUpgradeRunSnapshot.CurrentSchemaVersion
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
}
