using System;

internal static partial class Program
{
	private static void RunSkillCodexTests()
	{
		Run("skill codex unlock ids normalize query and append idempotently", SkillCodexUnlockIdsNormalizeQueryAndAppendIdempotently);
		Run("skill codex progress defaults and dictionary roundtrip use schema v3", SkillCodexProgressDefaultsAndDictionaryRoundTripUseSchemaV3);
		Run("skill codex v1 and v2 migrations start empty without run backfill", SkillCodexLegacyMigrationsStartEmptyWithoutRunBackfill);
		Run("skill codex progress stays isolated across profile slots", SkillCodexProgressStaysIsolatedAcrossProfileSlots);
		Run("skill codex backup profile restores unlock ids", SkillCodexBackupProfileRestoresUnlockIds);
	}

	private static void SkillCodexUnlockIdsNormalizeQueryAndAppendIdempotently()
	{
		const string raw = " beta ||alpha| beta|Alpha |  ";
		Equal("Alpha|alpha|beta", SkillCodexUnlockRules.NormalizeUpgradeIdsCsv(raw));
		Equal(3, SkillCodexUnlockRules.ParseUpgradeIds(raw).Count);
		True(SkillCodexUnlockRules.IsUnlocked(raw, "alpha"));
		True(SkillCodexUnlockRules.IsUnlocked(raw, " beta "));
		False(SkillCodexUnlockRules.IsUnlocked(raw, "ALPHA"));
		False(SkillCodexUnlockRules.IsUnlocked(raw, " "));

		string appended = SkillCodexUnlockRules.AddUnlockedUpgradeId(raw, "clubs_gap_rooting");
		Equal("Alpha|alpha|beta|clubs_gap_rooting", appended);
		Equal(appended, SkillCodexUnlockRules.AddUnlockedUpgradeId(appended, " clubs_gap_rooting "));
		Equal(appended, SkillCodexUnlockRules.AddUnlockedUpgradeId(appended, string.Empty));
	}

	private static void SkillCodexProgressDefaultsAndDictionaryRoundTripUseSchemaV3()
	{
		GameSaveStore.GameProgressData defaults = GameSaveStore.GameProgressData.CreateDefault();
		Equal(3, defaults.SchemaVersion);
		Equal(string.Empty, defaults.UnlockedSkillCodexUpgradeIdsCsv);

		GameSaveStore.GameProgressData progress = defaults with
		{
			UnlockedSkillCodexUpgradeIdsCsv = "spades_high_card_fever|hearts_thick_blood|hearts_thick_blood",
		};
		GameSaveStore.GameProgressData roundTripped = GameSaveStore.GameProgressData.FromDictionary(
			progress.ToDictionary(),
			GameSaveStore.GameProgressData.CreateDefault());

		Equal("hearts_thick_blood|spades_high_card_fever", roundTripped.UnlockedSkillCodexUpgradeIdsCsv);
		True(roundTripped.ToDictionary().ContainsKey("unlocked_skill_codex_upgrade_ids_csv"));
	}

	private static void SkillCodexLegacyMigrationsStartEmptyWithoutRunBackfill()
	{
		foreach (int legacySchemaVersion in new[] { 1, 2 })
		{
			ResetGameSaveStoreTestState();
			string slotId = GameSaveStore.DefaultProfileSlotId;
			Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
			var legacyProfile = new Godot.Collections.Dictionary
			{
				["schema_version"] = legacySchemaVersion,
				["highest_completed_main_adventure_stage"] = 2,
				["completed_adventure_tutorial_ids_csv"] = string.Empty,
				["selected_avatar_id"] = "shrimp",
				["player_name"] = "Legacy",
				["pending_avatar_unlock_queue"] = string.Empty,
				["pending_adventure_run_json"] = "{\"AcquiredUpgradeIds\":[\"hearts_thick_blood\"]}",
				["unlocked_skill_codex_upgrade_ids_csv"] = "must_not_survive_legacy_migration",
				["playtime_seconds"] = 0.0,
				["created_unix_time"] = 1L,
				["updated_unix_time"] = 1L,
			};
			saveManager.SeedSlot(slotId, legacyProfile);

			GameSaveStore.GameProgressData migrated = GameSaveStore.LoadActiveProfile();
			Equal(3, migrated.SchemaVersion);
			Equal(string.Empty, migrated.UnlockedSkillCodexUpgradeIdsCsv);
			Equal(1, saveManager.SaveToSlotCallCount);
			Godot.Collections.Dictionary stored = saveManager.GetSlotData(slotId);
			Equal(3, stored["schema_version"].AsInt32());
			Equal(string.Empty, stored["unlocked_skill_codex_upgrade_ids_csv"].AsString());
		}
	}

	private static void SkillCodexProgressStaysIsolatedAcrossProfileSlots()
	{
		ResetGameSaveStoreTestState();
		string firstSlotId = GameSaveStore.GetProfileSlotId(0);
		string secondSlotId = GameSaveStore.GetProfileSlotId(1);
		string thirdSlotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(firstSlotId);
		SeedProfile(saveManager, firstSlotId, CreateProgressData("First") with
		{
			UnlockedSkillCodexUpgradeIdsCsv = "hearts_thick_blood",
		});
		SeedProfile(saveManager, secondSlotId, CreateProgressData("Second") with
		{
			UnlockedSkillCodexUpgradeIdsCsv = "diamonds_question_mark_interest",
		});

		Equal(Godot.Error.Ok, GameSaveStore.CreateProfile(thirdSlotId, "Third", "crab"));
		Equal(string.Empty, GameSaveStore.LoadActiveProfile().UnlockedSkillCodexUpgradeIdsCsv);
		Equal("hearts_thick_blood", GameSaveStore.LoadProfile(firstSlotId).UnlockedSkillCodexUpgradeIdsCsv);
		Equal("diamonds_question_mark_interest", GameSaveStore.LoadProfile(secondSlotId).UnlockedSkillCodexUpgradeIdsCsv);

		Equal(Godot.Error.Ok, GameSaveStore.DeleteProfile(secondSlotId));
		False(saveManager.HasSlotFile(secondSlotId));
		Equal("hearts_thick_blood", GameSaveStore.LoadProfile(firstSlotId).UnlockedSkillCodexUpgradeIdsCsv);
	}

	private static void SkillCodexBackupProfileRestoresUnlockIds()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		saveManager.SeedBackupSlot(slotId, (CreateProgressData("Backup") with
		{
			UnlockedSkillCodexUpgradeIdsCsv = "clubs_gap_rooting|hearts_thick_blood",
		}).ToDictionary());

		GameSaveStore.GameProgressData restored = GameSaveStore.LoadProfile(slotId);
		Equal("clubs_gap_rooting|hearts_thick_blood", restored.UnlockedSkillCodexUpgradeIdsCsv);
	}
}
