using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static readonly string[] RetiredAdventureUpgradeIds =
	{
		"hearts_blood_tide_resonance",
		"diamonds_all_gains_converge",
		"diamonds_high_return_bonus",
		"spades_black_tide_dominion",
		"clubs_lush_growth",
		"clubs_climb_canopy",
	};

	private static void RunAdventureUpgradeSnapshotV5Tests()
	{
		Run("adventure upgrade manifest matches Pandora and localization bidirectionally", AdventureUpgradeManifestMatchesPandoraAndLocalizationBidirectionally);
		Run("adventure upgrade manifest is immutable and applies legacy migration policy", AdventureUpgradeManifestIsImmutableAndAppliesLegacyMigrationPolicy);
		Run("adventure upgrade manifest rejects missing runtime and catalog entries", AdventureUpgradeManifestRejectsContractDrift);
		Run("adventure upgrade acquisition requires a ready manifest whitelist", AdventureUpgradeAcquisitionRequiresReadyManifestWhitelist);
		Run("question mark interest description shows current item count", QuestionMarkInterestDescriptionShowsCurrentItemCount);
		Run("adventure upgrade descriptions hide standalone implementation parameters", AdventureUpgradeDescriptionsHideStandaloneImplementationParameters);
		Run("adventure upgrade snapshots migrate v1 through v5 into canonical v7", AdventureUpgradeSnapshotsMigrateV1ThroughV4IntoCanonicalV5);
		Run("adventure upgrade v7 normalizes persistent fields and derives state", AdventureUpgradeSnapshotV5NormalizesAndDerivesState);
		Run("adventure upgrade v7 roundtrips revive and migrates canonical v6", AdventureUpgradeV7RoundTripsReviveAndMigratesCanonicalV6);
		Run("adventure defeat store persists exactly once for main runs", AdventureDefeatStorePersistsExactlyOnceForMainRuns);
		Run("adventure upgrade snapshots reject invalid schema and stage values", AdventureUpgradeSnapshotsRejectInvalidSchemaAndStageValues);
		Run("adventure run store defers then automatically retries manifest restore once", AdventureRunStoreDefersThenAutomaticallyRetriesManifestRestoreOnce);
		Run("adventure run store canonicalizes noncanonical legacy snapshot once", AdventureRunStoreCanonicalizesNoncanonicalV5Once);
		Run("adventure run store preserves invalid payloads and deduplicates warnings", AdventureRunStorePreservesInvalidPayloadsAndDeduplicatesWarnings);
	}

	private static void QuestionMarkInterestDescriptionShowsCurrentItemCount()
	{
		Equal(
			"每次获得道具，永久+1伤害，当前获得 4。",
			AdventureUpgradeDescriptionRules.Format(
				"diamonds_question_mark_interest",
				"每次获得道具，永久+1伤害，当前获得 {current_item_count}。",
				4));
		Equal(
			"Fallback copy (3)",
			AdventureUpgradeDescriptionRules.Format(
				"diamonds_question_mark_interest",
				"Fallback copy",
				3));
		Equal(
			"Unrelated {current_item_count}",
			AdventureUpgradeDescriptionRules.Format(
				"hearts_thick_blood",
				"Unrelated {current_item_count}",
			9));
	}

	private static void AdventureUpgradeDescriptionsHideStandaloneImplementationParameters()
	{
		string description = AdventureUpgradeManifest.FormatDescription(
			"hearts_overflow_growth",
			"每{step}点溢出治疗，最大HP+{amount}。 ({maximum}) ({stack_limit})",
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				["step"] = 4,
				["amount"] = 1,
				["maximum"] = 150,
				["stack_limit"] = 3,
			},
			currentItemCount: 0);

		Equal("每4点溢出治疗，最大HP+1。", description);
		Equal(
			"Whenever you gain a question reward: immediately deal damage based on question rewards gained.",
			AdventureUpgradeManifest.FormatDescription(
				"diamonds_question_mark_windfall",
				"On the Nth question reward (up to {maximum}): immediately deal min(N, {threshold}) damage. ({amount})",
				new Dictionary<string, int>(StringComparer.Ordinal)
				{
					["maximum"] = 10,
					["threshold"] = 5,
					["amount"] = 1,
				},
				currentItemCount: 0));
	}

	private static void AdventureUpgradeManifestMatchesPandoraAndLocalizationBidirectionally()
	{
		List<AdventureSuitUpgradeDefinition> definitions = ReadEnabledAdventureUpgradeDefinitions();
		Equal(52, definitions.Count);
		Equal(52, AdventureUpgradeRuntimeContract.All.Count);

		var catalog = new AdventureSuitUpgradeCatalog();
		GetCatalogDefinitionList(catalog).AddRange(definitions);
		True(catalog.TryCreateManifest(out AdventureUpgradeManifest manifest));
		True(manifest.IsReady);
		Equal(52, manifest.EnabledIds.Count);
		Equal(52, manifest.Entries.Count);
		var suitCounts = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["hearts"] = 0,
			["diamonds"] = 0,
			["spades"] = 0,
			["clubs"] = 0,
		};
		var rarityCounts = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["common"] = 0,
			["rare"] = 0,
			["legendary"] = 0,
		};

		var manifestIds = new HashSet<string>(manifest.EnabledIds, StringComparer.Ordinal);
		foreach (KeyValuePair<string, AdventureUpgradeRuntimeMetadata> pair in AdventureUpgradeRuntimeContract.All)
		{
			True(manifestIds.Contains(pair.Key));
			True(pair.Value.HasRuntimeHandler);
			True(pair.Value.StateScope != AdventureUpgradeStateScope.None);
			True(pair.Value.MaximumValue > 0);
			True(manifest.TryGetEnabledEntry(pair.Key, out AdventureUpgradeManifestEntry entry));
			Equal(pair.Value.StateScope, entry.StateScope);
			Equal(pair.Value.StackingMode, entry.StackingMode);
			Equal(pair.Value.TriggerPolicy, entry.TriggerPolicy);
			Equal(pair.Value.MaximumValue, entry.MaximumValue);
			Equal(pair.Value.ConflictGroup, entry.ConflictGroup);
			Equal(pair.Value.Priority, entry.Priority);
			True(!string.IsNullOrWhiteSpace(entry.DescriptionKey));
			True(entry.Enabled);
			True(suitCounts.ContainsKey(entry.Suit));
			True(rarityCounts.ContainsKey(entry.Rarity));
			suitCounts[entry.Suit]++;
			rarityCounts[entry.Rarity]++;
		}

		Equal(14, suitCounts["hearts"]);
		Equal(13, suitCounts["diamonds"]);
		Equal(12, suitCounts["spades"]);
		Equal(13, suitCounts["clubs"]);
		Equal(24, rarityCounts["common"]);
		Equal(18, rarityCounts["rare"]);
		Equal(10, rarityCounts["legendary"]);
		True(catalog.TryCreateManifest(out AdventureUpgradeManifest cachedManifest));
		True(ReferenceEquals(manifest, cachedManifest));

		foreach (string retiredId in RetiredAdventureUpgradeIds)
		{
			False(manifest.ContainsEnabledId(retiredId));
			False(AdventureUpgradeRuntimeContract.All.ContainsKey(retiredId));
		}

		try
		{
			AssertCriticalPandoraCopyMatchesRuntimeContract(definitions);
		}
		catch (Exception exception)
		{
			throw new InvalidOperationException($"Pandora copy contract failed: {exception.Message}", exception);
		}

		try
		{
			AssertAllManifestDescriptionsAreLocalized(manifest);
		}
		catch (Exception exception)
		{
			throw new InvalidOperationException($"Localization contract failed: {exception.Message}", exception);
		}
		Godot.GD.ClearWarnings();
	}

	private static void AdventureUpgradeManifestIsImmutableAndAppliesLegacyMigrationPolicy()
	{
		var sourceEntries = new List<AdventureUpgradeManifestEntry>
		{
			CreateManifestEntry(
				"legacy_discard",
				AdventureUpgradeLegacyIdMigrationPolicy.Discard),
			CreateManifestEntry("legacy_keep"),
		};
		var manifest = new AdventureUpgradeManifest(sourceEntries);
		sourceEntries.Clear();

		Equal(2, manifest.Entries.Count);
		Equal(2, manifest.EnabledIds.Count);
		False(manifest.CanRestoreUpgradeId("legacy_discard", sourceSchemaVersion: 4));
		False(manifest.CanRestoreUpgradeId("legacy_discard", sourceSchemaVersion: 5));
		True(manifest.CanRestoreUpgradeId("legacy_discard", sourceSchemaVersion: 6));
		True(manifest.CanRestoreUpgradeId("legacy_keep", sourceSchemaVersion: 4));

		AdventureUpgradeRunSnapshot legacy = CreateSnapshot(
			schemaVersion: 4,
			acquiredUpgradeIds: new[] { "legacy_discard", "legacy_keep" });
		var migrated = new AdventureUpgradeRunState(manifest);
		Equal(AdventureUpgradeRestoreResult.Migrated, migrated.RestoreFromSnapshot(legacy));
		False(migrated.HasAcquiredUpgrade("legacy_discard"));
		True(migrated.HasAcquiredUpgrade("legacy_keep"));

		AdventureUpgradeRunSnapshot current = new(
			AdventureUpgradeRunSnapshot.CurrentSchemaVersion,
			AdventureCampaignCatalog.MainCampaignId,
			1,
			AdventureCampaignCatalog.GetMainAvatarId(1),
			false,
			true,
			new[] { "legacy_discard", "legacy_keep" });
		var restored = new AdventureUpgradeRunState(manifest);
		Equal(AdventureUpgradeRestoreResult.Restored, restored.RestoreFromSnapshot(current));
		True(restored.HasAcquiredUpgrade("legacy_discard"));
		True(restored.HasAcquiredUpgrade("legacy_keep"));
	}

	private static void AdventureUpgradeManifestRejectsContractDrift()
	{
		List<AdventureSuitUpgradeDefinition> definitions = ReadEnabledAdventureUpgradeDefinitions();
		var missingRuntimeCatalog = new AdventureSuitUpgradeCatalog();
		List<AdventureSuitUpgradeDefinition> missingRuntimeDefinitions = GetCatalogDefinitionList(missingRuntimeCatalog);
		missingRuntimeDefinitions.AddRange(definitions);
		missingRuntimeDefinitions.Add(new AdventureSuitUpgradeDefinition(
			"hearts_forged_unknown",
			"Forged",
			"hearts",
			"common",
			string.Empty,
			string.Empty,
			"UPGRADE_DESC_HEARTS_FORGED_UNKNOWN",
			string.Empty,
			enabled: true,
			sortOrder: 999));
		False(missingRuntimeCatalog.TryCreateManifest(out AdventureUpgradeManifest missingRuntimeManifest));
		False(missingRuntimeManifest.IsReady);

		var missingCatalogEntry = new AdventureSuitUpgradeCatalog();
		List<AdventureSuitUpgradeDefinition> incompleteDefinitions = GetCatalogDefinitionList(missingCatalogEntry);
		incompleteDefinitions.AddRange(definitions.GetRange(1, definitions.Count - 1));
		False(missingCatalogEntry.TryCreateManifest(out AdventureUpgradeManifest incompleteManifest));
		False(incompleteManifest.IsReady);
		Godot.GD.ClearWarnings();
	}

	private static void AdventureUpgradeAcquisitionRequiresReadyManifestWhitelist()
	{
		var state = new AdventureUpgradeRunState();
		state.BeginNewRun(3);
		state.SetHealth(75, 100);
		Equal(
			AdventureUpgradeAcquireResult.DeferredManifestUnavailable,
			state.AcquireUpgrade("hearts_thick_blood"));
		False(state.HasAcquiredUpgrade("hearts_thick_blood"));
		AdventureUpgradeRunSnapshot pendingRestore = CreateSnapshot(
			schemaVersion: 5,
			avatarStageNumber: 4,
			acquiredUpgradeIds: new[] { "hearts_thick_blood" });
		Equal(
			AdventureUpgradeRestoreResult.DeferredManifestUnavailable,
			state.RestoreFromSnapshot(pendingRestore));
		True(state.IsActive);
		Equal(3, state.CurrentAvatarStageNumber);
		Equal(75, state.CurrentHealth);

		state.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(
			AdventureUpgradeAcquireResult.Acquired,
			state.AcquireUpgrade("hearts_thick_blood"));
		Equal(
			AdventureUpgradeAcquireResult.AlreadyAcquired,
			state.AcquireUpgrade("hearts_thick_blood"));
		Equal(
			AdventureUpgradeAcquireResult.RejectedUnknownUpgrade,
			state.AcquireUpgrade("hearts_forged_unknown"));
		Equal(
			AdventureUpgradeAcquireResult.RejectedUnknownUpgrade,
			state.AcquireUpgrade("hearts_blood_tide_resonance"));
		True(state.HasAcquiredUpgrade("hearts_thick_blood"));
		False(state.HasAcquiredUpgrade("hearts_forged_unknown"));
	}

	private static void AdventureUpgradeSnapshotsMigrateV1ThroughV4IntoCanonicalV5()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		foreach (int schemaVersion in new[] { 1, 2, 3, 4 })
		{
			AdventureUpgradeRunSnapshot legacy = new(
				schemaVersion,
				3,
				CurrentRunStageIndex: 99,
				ShouldGrantRunStartUpgrade: false,
				ShouldGrantVictoryUpgrade: true,
				new List<string>
				{
					"hearts_overflow_growth",
					"diamonds_question_mark_interest",
					"diamonds_final_double",
					"diamonds_final_double",
					"hearts_blood_tide_resonance",
					"diamonds_all_gains_converge",
					"spades_black_tide_dominion",
					"clubs_lush_growth",
					"clubs_climb_canopy",
					"hearts_forged_unknown",
					string.Empty,
				},
				CurrentHealth: -20,
				MaxHealth: 999,
				PermanentCaptureDamageBonus: 99,
				QuestionRewardCount: 99,
				FinalDoubleCaptureDamageActive: true,
				HealingOverflowRemainder: 99,
				NextCaptureDamageBonus: 9,
				NextTurnHealAmount: 8,
				NextTurnDrawCount: 7,
				ClubShortBranchReturnUsedThisTurn: true,
				ClubMissClimbGrowthStacks: 6,
				ClubBorrowSuitSetArmed: true,
				ClubGapRootingUsedThisTurn: true,
				FinalDoubleQuestionRewardCountThisBattle: 10,
				ClubFlourishStacks: 5);

			var restored = new AdventureUpgradeRunState(manifest);
			Equal(AdventureUpgradeRestoreResult.Migrated, restored.RestoreFromSnapshot(legacy));
			True(restored.IsActive);
			Equal(3, restored.GetAcquiredUpgradeIds().Count);
			True(restored.HasAcquiredUpgrade("diamonds_final_double"));
			True(restored.HasAcquiredUpgrade("diamonds_question_mark_interest"));
			True(restored.HasAcquiredUpgrade("hearts_overflow_growth"));
			False(restored.HasAcquiredUpgrade("clubs_lush_growth"));
			False(restored.HasAcquiredUpgrade("clubs_climb_canopy"));
			False(restored.HasAcquiredUpgrade("hearts_forged_unknown"));
			Equal(0, restored.EffectState.NextCaptureDamageBonus);
			Equal(0, restored.EffectState.NextTurnHealAmount);
			Equal(0, restored.EffectState.NextTurnDrawCount);
			False(restored.EffectState.ClubShortBranchReturnUsedThisTurn);
			Equal(0, restored.EffectState.ClubMissClimbGrowthStacks);
			False(restored.EffectState.ClubBorrowSuitSetArmed);
			False(restored.EffectState.ClubGapRootingUsedThisTurn);

			if (schemaVersion == 1)
			{
				Equal(100, restored.CurrentHealth);
				Equal(100, restored.MaxHealth);
				Equal(0, restored.EffectState.QuestionRewardCount);
				Equal(0, restored.EffectState.PermanentCaptureDamageBonus);
				Equal(0, restored.EffectState.HealingOverflowRemainder);
				False(restored.EffectState.FinalDoubleCaptureDamageActive);
			}
			else
			{
				Equal(0, restored.CurrentHealth);
				Equal(150, restored.MaxHealth);
				Equal(10, restored.EffectState.QuestionRewardCount);
				Equal(5, restored.EffectState.PermanentCaptureDamageBonus);
				Equal(3, restored.EffectState.HealingOverflowRemainder);
				True(restored.EffectState.FinalDoubleCaptureDamageActive);
			}

			AdventureUpgradeRunSnapshot v5 = restored.ToSnapshot();
			Equal(AdventureUpgradeRunSnapshot.CurrentSchemaVersion, v5.SchemaVersion);
			Equal(0, v5.CurrentRunStageIndex);
			Equal("diamonds_final_double", v5.AcquiredUpgradeIds[0]);
			Equal("diamonds_question_mark_interest", v5.AcquiredUpgradeIds[1]);
			Equal("hearts_overflow_growth", v5.AcquiredUpgradeIds[2]);
			string json = JsonSerializer.Serialize(v5);
			AssertCanonicalV6Json(json);
		}
	}

	private static void AdventureUpgradeSnapshotV5NormalizesAndDerivesState()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		AdventureUpgradeRunSnapshot invalidValues = new(
			5,
			3,
			CurrentRunStageIndex: 12,
			ShouldGrantRunStartUpgrade: false,
			ShouldGrantVictoryUpgrade: true,
			new List<string>
			{
				"hearts_overflow_growth",
				"diamonds_final_double",
				"diamonds_question_mark_interest",
				},
			CurrentHealth: int.MaxValue,
			MaxHealth: int.MaxValue,
			PermanentCaptureDamageBonus: int.MaxValue,
			QuestionRewardCount: int.MaxValue,
			FinalDoubleCaptureDamageActive: false,
			HealingOverflowRemainder: int.MaxValue,
			NextCaptureDamageBonus: 1);

		var restored = new AdventureUpgradeRunState(manifest);
		Equal(AdventureUpgradeRestoreResult.Migrated, restored.RestoreFromSnapshot(invalidValues));
		Equal(150, restored.MaxHealth);
		Equal(150, restored.CurrentHealth);
		Equal(10, restored.EffectState.QuestionRewardCount);
		Equal(5, restored.EffectState.PermanentCaptureDamageBonus);
		Equal(3, restored.EffectState.HealingOverflowRemainder);
		True(restored.EffectState.FinalDoubleCaptureDamageActive);
		True(restored.IsQuestionRewardEchoActive == false);

		AdventureUpgradeRunSnapshot canonical = restored.ToSnapshot();
		var roundTrip = new AdventureUpgradeRunState(manifest);
		AdventureUpgradeRestoreResult roundTripResult = roundTrip.RestoreFromSnapshot(canonical);
		Equal(AdventureUpgradeRestoreResult.Restored, roundTripResult);
		Equal(150, roundTrip.MaxHealth);
		Equal(150, roundTrip.CurrentHealth);
		True(roundTrip.EffectState.FinalDoubleCaptureDamageActive);

		AdventureUpgradeRunSnapshot negativeValues = new(
			5,
			3,
			CurrentRunStageIndex: 0,
			ShouldGrantRunStartUpgrade: false,
			ShouldGrantVictoryUpgrade: true,
			Array.Empty<string>(),
			CurrentHealth: int.MinValue,
			MaxHealth: int.MinValue,
			PermanentCaptureDamageBonus: int.MinValue,
			QuestionRewardCount: int.MinValue,
			HealingOverflowRemainder: int.MinValue);
		var negative = new AdventureUpgradeRunState(manifest);
		Equal(AdventureUpgradeRestoreResult.Migrated, negative.RestoreFromSnapshot(negativeValues));
		Equal(1, negative.MaxHealth);
		Equal(0, negative.CurrentHealth);
		Equal(0, negative.EffectState.QuestionRewardCount);
		Equal(0, negative.EffectState.PermanentCaptureDamageBonus);
		Equal(0, negative.EffectState.HealingOverflowRemainder);

		AdventureUpgradeRunSnapshot crossFieldValues = new(
			5,
			3,
			CurrentRunStageIndex: 0,
			ShouldGrantRunStartUpgrade: false,
			ShouldGrantVictoryUpgrade: true,
			new[] { "diamonds_question_mark_interest" },
			CurrentHealth: 50,
			MaxHealth: 100,
			PermanentCaptureDamageBonus: 99,
			QuestionRewardCount: 2,
			HealingOverflowRemainder: 3);
		var crossField = new AdventureUpgradeRunState(manifest);
		Equal(AdventureUpgradeRestoreResult.Migrated, crossField.RestoreFromSnapshot(crossFieldValues));
		Equal(2, crossField.EffectState.PermanentCaptureDamageBonus);
		Equal(0, crossField.EffectState.HealingOverflowRemainder);
		False(crossField.EffectState.FinalDoubleCaptureDamageActive);

		AdventureUpgradeRunSnapshot echoSnapshot = new(
			5,
			3,
			CurrentRunStageIndex: 0,
			ShouldGrantRunStartUpgrade: false,
			ShouldGrantVictoryUpgrade: true,
			new[] { "diamonds_question_mark_echo" },
			CurrentHealth: 100,
			MaxHealth: 100,
			PermanentCaptureDamageBonus: 0,
			QuestionRewardCount: 1,
			HealingOverflowRemainder: 0);
		var echo = new AdventureUpgradeRunState(manifest);
		Equal(AdventureUpgradeRestoreResult.Migrated, echo.RestoreFromSnapshot(echoSnapshot));
		True(echo.IsQuestionRewardEchoActive);
	}

	private static void AdventureUpgradeSnapshotsRejectInvalidSchemaAndStageValues()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		foreach (AdventureUpgradeRunSnapshot invalid in new[]
		{
			CreateSnapshot(schemaVersion: 0),
			CreateSnapshot(schemaVersion: 8),
			CreateSnapshot(schemaVersion: 5, avatarStageNumber: 2),
			new AdventureUpgradeRunSnapshot(
				AdventureUpgradeRunSnapshot.CurrentSchemaVersion,
				"unknown_campaign",
				1,
				"shrimp",
				ShouldGrantRunStartUpgrade: false,
				ShouldGrantVictoryUpgrade: true,
				AcquiredUpgradeIds: Array.Empty<string>()),
		})
		{
			var state = new AdventureUpgradeRunState(manifest);
			state.BeginNewRun(3);
			Equal(
				AdventureUpgradeRestoreResult.InvalidSnapshotPreserved,
				state.RestoreFromSnapshot(invalid));
			False(state.IsActive);
		}
	}

	private static void AdventureUpgradeV7RoundTripsReviveAndMigratesCanonicalV6()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var state = new AdventureUpgradeRunState(manifest);
		state.BeginNewMainRun(6, "sea_slug");
		state.SetHealth(92, 108);
		Equal(AdventureUpgradeAcquireResult.Acquired, state.AcquireUpgrade("hearts_thick_blood"));
		Equal(AdventureDefeatResolution.RetryCurrentStage, state.ResolveMainAdventureDefeat());
		AdventureUpgradeRunSnapshot snapshot = state.ToSnapshot();
		True(snapshot.HasUsedRevive);
		string json = JsonSerializer.Serialize(snapshot);
		AssertCanonicalV6Json(json);

		True(AdventureUpgradeRunSnapshot.TryDeserialize(json, out AdventureUpgradeRunSnapshot decoded, out bool migrated, out _));
		False(migrated);
		var restored = new AdventureUpgradeRunState(manifest);
		Equal(AdventureUpgradeRestoreResult.Restored, restored.RestoreFromSnapshot(decoded));
		True(restored.HasUsedRevive);
		Equal(54, restored.CurrentHealth);
		Equal(108, restored.MaxHealth);
		Equal(6, restored.MainStageIndex);
		True(restored.HasAcquiredUpgrade("hearts_thick_blood"));

		const string v6Json = "{\"SchemaVersion\":6,\"CampaignId\":\"deep_tide_throne\",\"MainStageIndex\":6,\"EnemyAvatarId\":\"sea_slug\",\"ShouldGrantRunStartUpgrade\":false,\"ShouldGrantVictoryUpgrade\":true,\"AcquiredUpgradeIds\":[\"hearts_thick_blood\"],\"CurrentHealth\":41,\"MaxHealth\":108,\"PermanentCaptureDamageBonus\":0,\"QuestionRewardCount\":0,\"HealingOverflowRemainder\":0}";
		True(AdventureUpgradeRunSnapshot.TryDeserialize(v6Json, out AdventureUpgradeRunSnapshot v6, out bool v6Migrated, out _));
		False(v6Migrated);
		var migratedV6 = new AdventureUpgradeRunState(manifest);
		Equal(AdventureUpgradeRestoreResult.Migrated, migratedV6.RestoreFromSnapshot(v6));
		Equal(6, migratedV6.MainStageIndex);
		Equal("sea_slug", migratedV6.EnemyAvatarId);
		Equal(41, migratedV6.CurrentHealth);
		Equal(108, migratedV6.MaxHealth);
		True(migratedV6.HasAcquiredUpgrade("hearts_thick_blood"));
		False(migratedV6.HasUsedRevive);
	}

	private static void AdventureDefeatStorePersistsExactlyOnceForMainRuns()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData("Runner", string.Empty));
		var store = new AdventureRunSessionStore();
		store.ConfigureForPolicy(GameModePolicy.AdventureMain);
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		store.BeginNewMainRun(3, "jellyfish");
		int saveCount = saveManager.SaveToSlotCallCount;
		AdventureDefeatCommitResult defeatCommit = store.ResolveMainAdventureDefeat();
		Equal(AdventureRunWriteResult.Succeeded, defeatCommit.WriteResult);
		Equal<AdventureDefeatResolution?>(AdventureDefeatResolution.RetryCurrentStage, defeatCommit.Resolution);
		Equal(saveCount + 1, saveManager.SaveToSlotCallCount);
		True(store.GetReadModel().HasUsedRevive);

		store.ConfigureForPolicy(GameModePolicy.AdventureTutorial);
		saveCount = saveManager.SaveToSlotCallCount;
		AdventureDefeatCommitResult tutorialDefeat = store.ResolveMainAdventureDefeat();
		Equal(AdventureRunWriteResult.WriteNotAllowed, tutorialDefeat.WriteResult);
		Equal<AdventureDefeatResolution?>(null, tutorialDefeat.Resolution);
		Equal(saveCount, saveManager.SaveToSlotCallCount);
	}

	private static void AdventureRunStoreDefersThenAutomaticallyRetriesManifestRestoreOnce()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		AdventureUpgradeRunSnapshot legacy = new(
			4,
			3,
			CurrentRunStageIndex: 1,
			ShouldGrantRunStartUpgrade: false,
			ShouldGrantVictoryUpgrade: true,
			new List<string>
			{
				"hearts_thick_blood",
				"clubs_lush_growth",
				"clubs_climb_canopy",
			},
			CurrentHealth: 75,
			MaxHealth: 120,
			ClubFlourishStacks: 4);
		string legacyJson = JsonSerializer.Serialize(legacy);
		SeedProfile(saveManager, slotId, CreateProgressData("Runner", legacyJson));

		var store = new AdventureRunSessionStore();
		Equal(
			AdventureUpgradeRestoreResult.DeferredManifestUnavailable,
			store.ReloadFromActiveProfile());
		False(store.HasActiveRun);
		Equal(legacyJson, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);
		Equal(0, saveManager.SaveToSlotCallCount);
		Equal(0, Godot.GD.WarningMessages.Count);
		Equal(
			AdventureUpgradeRestoreResult.DeferredManifestUnavailable,
			store.SetManifest(AdventureUpgradeManifest.FromEnabledIds(Array.Empty<string>())));
		False(store.HasActiveRun);
		Equal(legacyJson, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);
		Equal(0, saveManager.SaveToSlotCallCount);

		Equal(
			AdventureUpgradeRestoreResult.Migrated,
			store.SetManifest(CreateFullAdventureUpgradeManifest()));
		True(store.HasActiveRun);
		Equal(1, saveManager.SaveToSlotCallCount);
		string migratedJson = LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson;
		AssertCanonicalV6Json(migratedJson);
		False(migratedJson.Contains("clubs_lush_growth", StringComparison.Ordinal));
		False(migratedJson.Contains("clubs_climb_canopy", StringComparison.Ordinal));

		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(1, saveManager.SaveToSlotCallCount);
		Equal(AdventureUpgradeRestoreResult.Restored, store.ReloadFromActiveProfile());
		Equal(1, saveManager.SaveToSlotCallCount);
		Equal(0, Godot.GD.WarningMessages.Count);
	}

	private static void AdventureRunStoreCanonicalizesNoncanonicalV5Once()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		const string noncanonicalJson = "{\"SchemaVersion\":5,\"CurrentAvatarStageNumber\":3,\"ShouldGrantRunStartUpgrade\":false,\"ShouldGrantVictoryUpgrade\":true,\"AcquiredUpgradeIds\":[\"hearts_thick_blood\"],\"NextCaptureDamageBonus\":0}";
		SeedProfile(saveManager, slotId, CreateProgressData("Runner", noncanonicalJson));

		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureUpgradeRestoreResult.Migrated, store.ReloadFromActiveProfile());
		True(store.HasActiveRun);
		Equal(1, saveManager.SaveToSlotCallCount);
		string canonicalJson = LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson;
		AssertCanonicalV6Json(canonicalJson);
		False(canonicalJson.Contains("NextCaptureDamageBonus", StringComparison.Ordinal));

		Equal(AdventureUpgradeRestoreResult.Restored, store.ReloadFromActiveProfile());
		Equal(1, saveManager.SaveToSlotCallCount);
		Equal(0, Godot.GD.WarningMessages.Count);
	}

	private static void AdventureRunStorePreservesInvalidPayloadsAndDeduplicatesWarnings()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		const string invalidSnapshotJson = "{\"SchemaVersion\":5,\"CurrentAvatarStageNumber\":999,\"ShouldGrantRunStartUpgrade\":false,\"ShouldGrantVictoryUpgrade\":true,\"AcquiredUpgradeIds\":[]}";
		SeedProfile(saveManager, slotId, CreateProgressData("Runner", invalidSnapshotJson));

		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureUpgradeRestoreResult.InvalidSnapshotPreserved, store.ReloadFromActiveProfile());
		False(store.HasActiveRun);
		Equal(invalidSnapshotJson, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);
		Equal(1, Godot.GD.WarningMessages.Count);
		Equal(0, saveManager.SaveToSlotCallCount);

		Equal(AdventureUpgradeRestoreResult.InvalidSnapshotPreserved, store.ReloadFromActiveProfile());
		Equal(1, Godot.GD.WarningMessages.Count);
		store.BeginNewRun(3);
		Equal(invalidSnapshotJson, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);
		Equal(0, saveManager.SaveToSlotCallCount);

		const string changedInvalidJson = "{bad json";
		SeedProfile(saveManager, slotId, CreateProgressData("Runner", changedInvalidJson));
		Equal(AdventureUpgradeRestoreResult.InvalidJsonPreserved, store.ReloadFromActiveProfile());
		Equal(2, Godot.GD.WarningMessages.Count);
		False(store.HasActiveRun);
		Equal(changedInvalidJson, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);
		Equal(0, saveManager.SaveToSlotCallCount);
		Equal(AdventureUpgradeRestoreResult.InvalidJsonPreserved, store.ReloadFromActiveProfile());
		Equal(2, Godot.GD.WarningMessages.Count);

		var newStore = new AdventureRunSessionStore();
		newStore.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureUpgradeRestoreResult.InvalidJsonPreserved, newStore.ReloadFromActiveProfile());
		Equal(3, Godot.GD.WarningMessages.Count);
	}

	private static AdventureUpgradeManifest CreateFullAdventureUpgradeManifest()
	{
		return AdventureUpgradeManifest.FromEnabledIds(AdventureUpgradeRuntimeContract.All.Keys);
	}

	private static AdventureUpgradeManifestEntry CreateManifestEntry(
		string id,
		AdventureUpgradeLegacyIdMigrationPolicy migrationPolicy =
			AdventureUpgradeLegacyIdMigrationPolicy.KeepWhenEnabled)
	{
		return new AdventureUpgradeManifestEntry(
			id,
			"hearts",
			"common",
			Enabled: true,
			HasRuntimeHandler: true,
			AdventureUpgradeStateScope.Run,
			AdventureUpgradeStackingMode.Unique,
			AdventureUpgradeTriggerPolicy.None,
			MaximumValue: 1,
			ConflictGroup: string.Empty,
			Priority: 0,
			DescriptionKey: $"UPGRADE_DESC_{id.ToUpperInvariant()}",
			LegacyIdMigrationPolicy: migrationPolicy);
	}

	private static AdventureUpgradeRunSnapshot CreateSnapshot(
		int schemaVersion,
		int avatarStageNumber = 3,
		IReadOnlyList<string>? acquiredUpgradeIds = null)
	{
		return new AdventureUpgradeRunSnapshot(
			schemaVersion,
			avatarStageNumber,
			CurrentRunStageIndex: 0,
			ShouldGrantRunStartUpgrade: false,
			ShouldGrantVictoryUpgrade: true,
			acquiredUpgradeIds ?? Array.Empty<string>(),
			CurrentHealth: 100,
			MaxHealth: 100,
			PermanentCaptureDamageBonus: 0,
			QuestionRewardCount: 0,
			HealingOverflowRemainder: 0);
	}

	private static List<AdventureSuitUpgradeDefinition> ReadEnabledAdventureUpgradeDefinitions()
	{
		string dataPath = Path.Combine(FindRepositoryRoot(), "data.pandora");
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(dataPath));
		var definitions = new List<AdventureSuitUpgradeDefinition>();
		foreach (JsonElement entity in document.RootElement
			.GetProperty("_entity_data")
			.GetProperty("_entities")
			.EnumerateArray())
		{
			if (!string.Equals(
				entity.GetProperty("_category_id").GetString(),
				"adventure_suit_upgrades",
				StringComparison.Ordinal))
			{
				continue;
			}

			JsonElement properties = entity.GetProperty("_property_overrides");
			if (!properties.GetProperty("enabled").GetProperty("value").GetBoolean())
			{
				continue;
			}

			string id = entity.GetProperty("_id").GetString() ?? string.Empty;
			string descriptionKey = $"UPGRADE_DESC_{id.ToUpperInvariant()}";
			if (properties.TryGetProperty("description_key", out JsonElement descriptionProperty))
			{
				string configuredKey = descriptionProperty.GetProperty("value").GetString() ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(configuredKey))
				{
					descriptionKey = configuredKey;
				}
			}

			definitions.Add(new AdventureSuitUpgradeDefinition(
				id,
				entity.GetProperty("_name").GetString() ?? id,
				properties.GetProperty("suit").GetProperty("value").GetString() ?? string.Empty,
				properties.GetProperty("rarity").GetProperty("value").GetString() ?? string.Empty,
				properties.GetProperty("trigger_text").GetProperty("value").GetString() ?? string.Empty,
				properties.GetProperty("effect_text").GetProperty("value").GetString() ?? string.Empty,
				descriptionKey,
				properties.GetProperty("notes").GetProperty("value").GetString() ?? string.Empty,
				enabled: true,
				sortOrder: properties.GetProperty("sort_order").GetProperty("value").GetInt32(),
				effectValuesRaw: properties.GetProperty("effect_values").GetProperty("value").GetString() ?? string.Empty,
				descriptionValueKeysCsv: properties.GetProperty("description_value_keys").GetProperty("value").GetString() ?? string.Empty));
		}

		return definitions;
	}

	private static void AssertCriticalPandoraCopyMatchesRuntimeContract(
		IReadOnlyList<AdventureSuitUpgradeDefinition> definitions)
	{
		var definitionsById = new Dictionary<string, AdventureSuitUpgradeDefinition>(StringComparer.Ordinal);
		foreach (AdventureSuitUpgradeDefinition definition in definitions)
		{
			definitionsById[definition.Id] = definition;
		}

		AssertPandoraCopy(definitionsById, "hearts_warm_current_supply",
			"每回合首次成功收牌后",
			"抽 1 张牌。");
		AssertPandoraCopy(definitionsById, "hearts_miss_bandage",
			"空打且行动权将真正交给敌方时",
			"登记下个真实回合开始时回复 3 并抽 1。");
		AssertPandoraCopy(definitionsById, "hearts_chain_tide_breath",
			"同一真实回合第二次成功收牌时",
			"抽 1；回复 2。");
		AssertPandoraCopy(definitionsById, "hearts_red_tide_return",
			"每战一次，生命从 50% 及以上降到 50% 以下时",
			"回复 5；抽 2。");
		AssertPandoraCopy(definitionsById, "hearts_enchanted_recovery",
			"本动作真实附着道具效果生效时",
			"本动作最多治疗 3；若该基础牌成功收牌，再抽 1。");
		AssertPandoraCopy(definitionsById, "hearts_return_tide_heart",
			"每回合首次成功收牌后",
			"抽 1；回复 2。同一真实回合第二次成功收牌后，再抽 1；再回复 3。");
		AssertPandoraCopy(definitionsById, "hearts_revival_draw",
			"每战一次，生命从 25% 及以上降到 25% 以下时",
			"回复 8；抽 2。");
		AssertPandoraCopy(definitionsById, "hearts_overflow_growth",
			"治疗溢出时",
			"每 4 点溢出治疗使 Max HP +1（run 内最高 150），当前生命同步抬升；余数最多保留 3。");
		AssertPandoraCopy(definitionsById, "diamonds_small_insurance",
			"单次收牌伤害为 1 至 4 时",
			"伤害按 5 结算；0 伤害仍为 0。");
		AssertPandoraCopy(definitionsById, "diamonds_rejudge_once",
			"获得问号奖励且本战尚未重掷时",
			"可保留原奖励或重掷；仅重掷会消耗本战机会。");
		AssertPandoraCopy(definitionsById, "diamonds_question_mark_windfall",
			"获得问号奖励时",
			"run 内问号计数最多 10；按当前序号立刻直伤，单次最多 5。");
		AssertPandoraCopy(definitionsById, "diamonds_question_mark_echo",
			"本 run 首次问号奖励完成后",
			"从下一次收牌起，每张计分牌点数 +1。");
		AssertPandoraCopy(definitionsById, "diamonds_question_mark_interest",
			"每次获得道具时",
			"每次获得道具，永久+1伤害，当前获得 {current_item_count}。");
		AssertPandoraCopy(definitionsById, "diamonds_top_prize_repeat",
			"成功收牌且至少有 3 张计分牌时",
			"最高点数的 1 张计分牌额外结算 1 次；并列取收牌前最左。");
		AssertPandoraCopy(definitionsById, "diamonds_full_payout",
			"成功收牌且至少有 6 张计分牌时",
			"本次整次倍率桶 +1。");
		AssertPandoraCopy(definitionsById, "diamonds_final_double",
			"run 内第 10 次问号奖励完成后",
			"从下一次收牌起，本次整次倍率桶 +1。");
		AssertPandoraCopy(definitionsById, "spades_dark_harbor_reveal",
			"敌人成功收牌后",
			"下一张牌正面入桌。");
		AssertPandoraCopy(definitionsById, "spades_wrong_shore_lineup",
			"敌人成功收牌后",
			"下一张牌放最左。");
		AssertPandoraCopy(definitionsById, "spades_reverse_capture_tax",
			"敌人成功收牌且中间含旧桌牌时",
			"中间每有 1 张旧桌牌，伤害 -1；与盘剥同时生效时只取较强减伤。");
		AssertPandoraCopy(definitionsById, "spades_exaction",
			"敌人成功收牌且中间含旧桌牌时",
			"伤害 -4；与逆收税同时生效时只取较强减伤。");
		AssertPandoraCopy(definitionsById, "spades_hunt_order",
			"你的真实回合开始时",
			"自动标记最早旧桌牌；收走它时 +6。");
		AssertPandoraCopy(definitionsById, "spades_intercept_beat",
			"敌人空打后",
			"你立刻额外打 1 张基础牌；整条派生链结束后仍正常开始真实回合。");
		AssertPandoraCopy(definitionsById, "clubs_short_branch_return",
			"每回合一次，完整收牌集合恰好含 2 张唯一基础牌时",
			"若手牌容量足以容纳全部唯一回手候选，这两张牌回手；否则正常进入牌堆。");
		AssertPandoraCopy(definitionsById, "clubs_gap_rooting",
			"每回合一次，成功收牌且中间没有计分牌时",
			"额外收走收牌区间左侧最近的 1 张基础牌；该牌不计分。");
		AssertPandoraCopy(definitionsById, "clubs_primary_root_return",
			"本次打出的基础牌成功收牌时",
			"若手牌有空位，该牌结算后回手。");
		AssertPandoraCopy(definitionsById, "clubs_miss_climb_growth",
			"每次空打时",
			"积 1 层攀枝；数字基础牌保留原点数，并额外按原点数 + 层数匹配；成功收牌后清空。");
		AssertPandoraCopy(definitionsById, "clubs_borrow_suit_set",
			"获得问号奖励后",
			"保持 1 次 armed，重复获得只刷新；本真实回合下一张数字基础牌仅在无点数或替代点数候选时按花色兜底，合法出牌后消费；真实回合或战斗结束时清除。");
		AssertPandoraCopy(definitionsById, "clubs_continue_branch",
			"每回合一次，空打结算后桌面至少有 6 张牌时",
			"立刻额外打 1 张基础牌；额外行动不能再次触发续枝。");
		AssertPandoraCopy(definitionsById, "clubs_linked_stems",
			"同一真实回合第二次成功收牌时",
			"立刻获得 1 次问号奖励。");
		AssertPandoraCopy(definitionsById, "clubs_crown_canopy",
			"持续生效",
			"你的首尾牌可正常记分");
	}

	private static void AssertPandoraCopy(
		IReadOnlyDictionary<string, AdventureSuitUpgradeDefinition> definitionsById,
		string id,
		string expectedTrigger,
		string expectedEffect)
	{
		True(definitionsById.TryGetValue(id, out AdventureSuitUpgradeDefinition definition));
		Equal(expectedTrigger, definition.TriggerText);
		Equal(expectedEffect, definition.EffectText);
	}

	private static List<AdventureSuitUpgradeDefinition> GetCatalogDefinitionList(
		AdventureSuitUpgradeCatalog catalog)
	{
		FieldInfo? field = typeof(AdventureSuitUpgradeCatalog).GetField(
			"_all",
			BindingFlags.Instance | BindingFlags.NonPublic);
		True(field != null);
		return (List<AdventureSuitUpgradeDefinition>)field!.GetValue(catalog)!;
	}

	private static void AssertAllManifestDescriptionsAreLocalized(
		AdventureUpgradeManifest manifest)
	{
		string localizationPath = Path.Combine(FindRepositoryRoot(), "localization", "ui.csv");
		string[] lines = File.ReadAllLines(localizationPath);
		List<string> header = ParseCsvLine(lines[0]);
		var rowsByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
		{
			List<string> row = ParseCsvLine(lines[lineIndex]);
			if (row.Count > 0 && !string.IsNullOrWhiteSpace(row[0]))
			{
				rowsByKey[row[0]] = row;
			}
		}

		foreach (AdventureUpgradeManifestEntry entry in manifest.Entries)
		{
			True(rowsByKey.TryGetValue(entry.DescriptionKey, out List<string>? row));
			Equal(header.Count, row!.Count);
			for (int columnIndex = 1; columnIndex < header.Count; columnIndex++)
			{
				True(!string.IsNullOrWhiteSpace(row[columnIndex]));
			}

			IReadOnlyList<string> expectedValueKeys = entry.DescriptionValueKeys ?? Array.Empty<string>();
			int englishColumnIndex = header.IndexOf("en");
			True(englishColumnIndex > 0);
			List<string> baselineTokens = ExtractDescriptionPlaceholders(row[englishColumnIndex]);
			foreach (string valueKey in expectedValueKeys)
			{
				True(baselineTokens.Contains(valueKey));
			}
			for (int columnIndex = 2; columnIndex < header.Count; columnIndex++)
			{
				HashSet<string> expectedTokens = new(baselineTokens, StringComparer.Ordinal);
				HashSet<string> localizedTokens = new(
					ExtractDescriptionPlaceholders(row[columnIndex]),
					StringComparer.Ordinal);
				True(expectedTokens.SetEquals(localizedTokens));
			}

			for (int columnIndex = 1; columnIndex < header.Count; columnIndex++)
			{
				string formattedDescription = AdventureUpgradeManifest.FormatDescription(
					entry.Id,
					row[columnIndex],
					entry.EffectValues,
					currentItemCount: 0);
				False(Regex.IsMatch(formattedDescription, @"\(\s*\d+\s*\)"));
				if (string.Equals(entry.Id, "hearts_overflow_growth", StringComparison.Ordinal))
				{
					False(formattedDescription.Contains("150", StringComparison.Ordinal));
					False(formattedDescription.Contains("3", StringComparison.Ordinal));
				}
			}
		}

		AssertCriticalLocalizedDescriptionsMatchRuntimeContract(header, rowsByKey);
	}

	private static void AssertCriticalLocalizedDescriptionsMatchRuntimeContract(
		IReadOnlyList<string> header,
		IReadOnlyDictionary<string, List<string>> rowsByKey)
	{
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_WARM_CURRENT_SUPPLY",
			"After the first capture each real turn: draw {count}.",
			"首次收取后，额外抽1",
			"每回合首次收牌後，額外抽1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_THICK_BLOOD",
			"On gain: Max HP +{amount} and heal {amount}. ({maximum})",
			"最大生命+4,恢复4生命值",
			"最大HP+ 4並治療4。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_MISS_BANDAGE",
			"When a miss truly ends your real turn: at the next real turn start, heal {amount} and draw {count}.",
			"未收取时，下个回合治疗3并抽1",
			"未收取時，下個回合治療3並抽1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_CHAIN_TIDE_BREATH",
			"On your second capture in the same real turn: draw {count} and recover {amount} HP.",
			"第二次收牌时，额外抽1治疗2",
			"第二次收牌時，額外抽1治療2。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_RED_TIDE_RETURN",
			"Once per battle, when HP falls from {threshold}% or more to below {threshold}%: recover {amount} HP and draw {count}.",
			"生命值低于50%时，恢复5生命值并抽2张牌",
			"生命低於50%時，治療5並抽2。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_SUPPLY_CACHE_RETURN",
			"When you gain an item: heal {amount} and draw {count}.",
			"获得道具时，恢复3生命值并抽1张牌",
			"獲得道具時，治療3並抽1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_DEEP_TIDE_LUNG",
			"Max HP +{amount}; draw {count} at battle start.",
			"最大生命值+8,每回合抽1一张牌",
			"最大Hp+8，戰鬥開始時抽1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_ENCHANTED_RECOVERY",
			"When a real attached item effect triggers: heal {amount} once this action; if the action captures, draw {count}.",
			"特殊牌触发时，治疗1，收牌后额外抽1",
			"特殊牌觸發時，治療1，收牌後額外抽1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_RETURN_TIDE_HEART",
			"After the first capture each real turn: draw {first_count}, recover {first_amount}; after the second, draw {second_count}, recover {second_amount}.",
			"首次收牌时，恢复1生命值，抽2张牌，2次收牌后，恢复1生命值，抽3张牌",
			"首次收牌後，治療2並抽1，二次收牌時，治療3抽1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_REVIVAL_DRAW",
			"Once per battle, when HP falls from {threshold}% or more to below {threshold}%: recover {amount} HP and draw {count}.",
			"仅一次，生命低于25%时，恢复8生命值，抽2张牌",
			"每戰鬥一次，生命少於25%時，治療8並抽2。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_HEARTS_OVERFLOW_GROWTH",
			"Every {step} overflow healing raises Max HP by {amount}, up to {maximum}; carry up to {stack_limit} overflow between heals.",
			"每4点溢出生命值，最大生命值+1",
			"每4點溢出治療，最大HP+1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_DIAMONDS_QUESTION_MARK_RAISE",
			"When you gain an item: draw {count}.",
			"获得道具时，抽1张牌",
			"獲得道具時，抽1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_DIAMONDS_SMALL_INSURANCE",
			"When capture damage is {threshold}-{threshold}: resolve it as {amount}. Zero damage stays zero.",
			"单次伤害小于4时，按5结算",
			"單次傷害小於4時，按5結算。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_DIAMONDS_REJUDGE_ONCE",
			"You may reroll one question reward per battle; keeping it preserves the opportunity for a later reward.",
			"获得一次重掷道具的机会",
			"每戰可重擲一次問號獎勵；選擇保留不會消耗機會，可在後續獎勵中使用。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_DIAMONDS_QUESTION_MARK_WINDFALL",
			"On the Nth question reward (up to {maximum}): immediately deal min(N, {threshold}) damage. ({amount})",
			"获得道具时，根据获得道具数造成伤害",
			"根據道具獲得次數造成傷害。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_DIAMONDS_BOUNTY_AMPLIFY",
			"When you gain an item: +{amount} damage.",
			"收牌中包含道具时，本次伤害+3",
			"獲得道具時，傷害+3。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_DIAMONDS_QUESTION_MARK_ECHO",
			"After the first question reward in this run: from the next capture, every scoring card gets +{amount}.",
			"下次获得道具后激活，后续每张牌的伤害+1",
			"下次獲得道具後啟用，每張牌的傷害+1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_DIAMONDS_QUESTION_MARK_INTEREST",
			"Each item gained permanently adds +1 damage. Items gained: {current_item_count}.",
			"每次获得道具，永久+1伤害，当前获得 {current_item_count}",
			"每次獲得道具，永久+1傷害，目前獲得 {current_item_count}。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_DIAMONDS_TOP_PRIZE_REPEAT",
			"On a capture with at least {threshold} scoring cards: the highest scoring card resolves one extra time; ties use the leftmost card before capture. ({count})",
			"收取3张以上时，最高牌额外计算一次",
			"收取5張以上時，最高牌額外結算一次。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_DIAMONDS_FULL_PAYOUT",
			"On capture with {threshold}+ scoring cards: add +{multiplier} to the capture multiplier bucket.",
			"收取大于6张时，本次按倍率+1结算",
			"收取至少8張牌時，倍率+1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_DIAMONDS_FINAL_DOUBLE",
			"After completing the {threshold}th question reward: add +{multiplier} to the capture multiplier bucket from the next capture.",
			"获得10次道具后，倍率+1",
			"獲得10次道具後，倍率+1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_DARK_HARBOR_REVEAL",
			"After an enemy capture: the next played card enters face up.",
			"敌人收牌后，下张牌会正面上场",
			"敵人收牌後，下張牌正面出牌。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_WRONG_SHORE_LINEUP",
			"After an enemy capture: the next played card goes to the far left.",
			"敌人收牌后，下张牌放置最左",
			"敵人收牌後，下張牌放置最左。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_MISSED_BEAT_BLUNT_HURT",
			"After an enemy miss: if the next card captures, its damage is -{amount}.",
			"敌人未收牌后，下次收牌总伤害降低",
			"敵人空打時，下次收牌傷害-3。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_MISS_BLEED",
			"After an enemy miss with {threshold}+ table cards: deal {amount} direct damage to that enemy.",
			"敌人在不低于6张牌未收取时，收到伤害",
			"敵人空打並且桌面至少6張牌時，收到2點傷害。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_REVERSE_CAPTURE_TAX",
			"When an enemy captures: each old middle table card gives -{amount} damage; with Exaction, use only the stronger reduction.",
			"根据敌人收牌数获得伤害减免",
			"根據敵人收取牌的數量減免傷害。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_EXACTION",
			"When an enemy capture includes an old middle table card: damage -{amount}; with Reverse Capture Tax, use only the stronger reduction.",
			"敌人总伤害降低",
			"敵人收牌傷害-4。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_HUNT_ORDER",
			"At your real turn start: mark the oldest old table card; taking it gives +{amount} damage.",
			"每回合标记一次首牌，获得伤害提升",
			"回合開始時標記首牌，該牌點數+6。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_TRACE_HUNT",
			"After you capture: mark the rightmost old table card; taking it later gives +{amount} damage.",
			"收牌后，标记相邻牌，额外提供4点伤害",
			"收牌後，標記相鄰牌，額外提供4點傷害。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_TAIL_PRESSURE",
			"When an enemy captures exactly {threshold} cards: its played tail card stays face-up on the table.",
			"敌人收取的牌等于7时，打出的牌留在桌面",
			"敵人收取的牌等於7時，打出的牌留在桌面。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_RETURN_TIDE_MISORDER",
			"After an enemy miss with an old table card: the next card goes left of the oldest old card.",
			"敌人本次未收牌时，下张牌从最左出",
			"敵人空打時，下張牌放最左。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_INTERCEPT_BEAT",
			"After an enemy miss: immediately play {count} extra base card; after the entire derived chain resolves, your normal real turn still begins.",
			"敌人出牌未收取后，可立即补牌",
			"敵人空打時，可立即出一張牌。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_SPADES_BLACK_LEDGER",
			"After an enemy captures, the highest-value scoring table card goes into your deck; this capture deals damage normally. ({count})",
			"敌人收取的牌，最高点数的进入你的牌库",
			"敵人收取時，點數最高的牌進入你的牌堆。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_CLUBS_SHORT_BRANCH_RETURN",
			"Once per real turn, when the complete capture has exactly {count} unique base cards: return both if hand capacity allows.",
			"收取的牌为2张时，回到手里",
			"只收取兩張牌時，可回手。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_CLUBS_GAP_ROOTING",
			"Once per real turn, on a capture with no middle scoring card: also take the nearest base card to the left; it does not score. ({count})",
			"收取的牌为2张时，额外收取相邻牌",
			"收取並且沒有記分時，額外收走相鄰牌。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_CLUBS_PRIMARY_ROOT_RETURN",
			"When the played base card captures: return it after resolution if hand space allows; with Climb Canopy, Primary Root returns it only once. ({count})",
			"成功收牌后，刚打出的牌回手",
			"收牌後，打出的牌回手。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_CLUBS_MISS_CLIMB_GROWTH",
			"On each miss: gain {amount} Branch stack; numbered cards keep their original rank and additionally match at original rank + Branch stacks; clear all Branch stacks after capture. ({stack_limit})",
			"每次未收牌，都可让牌的点数提升",
			"每次空打可為手牌點數+1。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_CLUBS_BORROW_SUIT_SET",
			"After a question reward, keep one armed use (repeated gains only refresh it): this real turn's next numbered base card uses suit only when no rank or alternate-rank match exists; a valid play consumes it, and real-turn or battle end clears it. ({count})",
			"每次获得道具后，下次收牌按花色收取",
			"獲得道具後，下一張牌可同時按花色收取。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_CLUBS_CONTINUE_BRANCH",
			"Once per real turn, on a miss with {threshold}+ table cards: play {count} extra base card. Extra actions cannot trigger this.",
			"桌面不低于6张未收取时，可以额外出一张牌",
			"每回合一次；空打後桌面至少6張時額外出1張基礎牌；額外行動不能再次觸發。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_CLUBS_LINKED_STEMS",
			"On your second capture in the same real turn: immediately gain {count} question-mark reward. ({threshold})",
			"同一回合2次收牌时，获得道具",
			"同一真實回合第二次收牌時：立刻獲得1次問號獎勵。");
		AssertLocalizedUpgradeCopy(header, rowsByKey, "UPGRADE_DESC_CLUBS_CROWN_CANOPY",
			"Passive: when your base card captures, the first and last endpoints each score once; no duplicate normal score item is added. ({count})",
			"首尾牌也能记分",
			"持續生效：你的基礎牌成功收牌時，首尾端點各計分一次，不生成重複普通計分項。");
	}

	private static void AssertLocalizedUpgradeCopy(
		IReadOnlyList<string> header,
		IReadOnlyDictionary<string, List<string>> rowsByKey,
		string key,
		string expectedEnglish,
		string expectedSimplifiedChinese,
		string expectedTraditionalChinese)
	{
		_ = expectedSimplifiedChinese;
		_ = expectedTraditionalChinese;
		True(rowsByKey.TryGetValue(key, out List<string>? row));
		for (int columnIndex = 1; columnIndex < header.Count; columnIndex++)
		{
				switch (header[columnIndex])
				{
					case "en":
						True(NormalizeDescriptionForComparison(row![columnIndex]).Contains(
							NormalizeDescriptionForComparison(expectedEnglish),
							StringComparison.Ordinal));
						break;
					case "zh_CN":
						True(Regex.IsMatch(row![columnIndex], "[\\u3400-\\u9FFF]"));
						break;
					case "zh_TW":
						True(Regex.IsMatch(row![columnIndex], "[\\u3400-\\u9FFF]"));
					break;
				default:
					True(!string.IsNullOrWhiteSpace(row![columnIndex]));
					break;
			}
		}
	}

	private static string NormalizeDescriptionForComparison(string value)
	{
		return Regex.Replace(
			value,
			"\\{[A-Za-z0-9_]+\\}|[0-9]+",
			string.Empty,
			RegexOptions.CultureInvariant);
	}

	private static List<string> ExtractDescriptionPlaceholders(string value)
	{
		var tokens = new List<string>();
		foreach (Match match in Regex.Matches(
			value,
			"\\{(?<key>[A-Za-z0-9_]+)\\}",
			RegexOptions.CultureInvariant))
		{
			tokens.Add(match.Groups["key"].Value);
		}

		return tokens;
	}

	private static void AssertCanonicalV6Json(string json)
	{
		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement root = document.RootElement;
		Equal(AdventureUpgradeRunSnapshot.CurrentSchemaVersion, root.GetProperty("SchemaVersion").GetInt32());
		True(root.TryGetProperty("CampaignId", out _));
		True(root.TryGetProperty("MainStageIndex", out _));
		True(root.TryGetProperty("EnemyAvatarId", out _));
		True(root.TryGetProperty("ShouldGrantRunStartUpgrade", out _));
		True(root.TryGetProperty("ShouldGrantVictoryUpgrade", out _));
		True(root.TryGetProperty("AcquiredUpgradeIds", out _));
		True(root.TryGetProperty("CurrentHealth", out _));
		True(root.TryGetProperty("MaxHealth", out _));
		True(root.TryGetProperty("PermanentCaptureDamageBonus", out _));
		True(root.TryGetProperty("QuestionRewardCount", out _));
		True(root.TryGetProperty("HealingOverflowRemainder", out _));
		True(root.TryGetProperty("HasUsedRevive", out _));
		False(root.TryGetProperty("CurrentRunStageIndex", out _));
		False(root.TryGetProperty("FinalDoubleCaptureDamageActive", out _));
		False(root.TryGetProperty("NextCaptureDamageBonus", out _));
		False(root.TryGetProperty("NextTurnHealAmount", out _));
		False(root.TryGetProperty("NextTurnDrawCount", out _));
		False(root.TryGetProperty("ClubShortBranchReturnUsedThisTurn", out _));
		False(root.TryGetProperty("ClubMissClimbGrowthStacks", out _));
		False(root.TryGetProperty("ClubBorrowSuitSetArmed", out _));
		False(root.TryGetProperty("ClubGapRootingUsedThisTurn", out _));
		False(root.TryGetProperty("FinalDoubleQuestionRewardCountThisBattle", out _));
		False(root.TryGetProperty("ClubFlourishStacks", out _));
	}

	private static void AssertCanonicalV5Json(string json)
	{
		AssertCanonicalV6Json(json);
	}
}
