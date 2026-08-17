using System;
using System.Collections.Generic;
using System.Text.Json;

internal static partial class Program
{
	private static void RunWave2Tests()
	{
		Run("wave 2 game mode policy uses network first precedence", Wave2GameModePolicyUsesNetworkFirstPrecedence);
		Run("wave 2 campaign catalog is the single stage contract", Wave2CampaignCatalogIsTheSingleStageContract);
		Run("wave 2 legacy profile mapping is bounded and idempotent", Wave2LegacyProfileMappingIsBoundedAndIdempotent);
		Run("wave 2 tutorial ids are independent and never enter main progress", Wave2TutorialIdsAreIndependentAndNeverEnterMainProgress);
		Run("wave 2 v5 run migrates only valid legacy stages", Wave2V5RunMigratesOnlyValidLegacyStages);
		Run("wave 2 network standard cannot touch adventure run", Wave2NetworkStandardCannotTouchAdventureRun);
		Run("wave 2 tutorial isolates valid main adventure run", Wave2TutorialIsolatesValidMainAdventureRun);
		Run("wave 2 tutorial isolates damaged main adventure run", Wave2TutorialIsolatesDamagedMainAdventureRun);
	}

	private static void Wave2GameModePolicyUsesNetworkFirstPrecedence()
	{
		Equal(GameModeKind.NetworkStandard, GameModePolicy.Derive(true, true, true, true).Kind);
		Equal(GameModeKind.AdventureTutorial, GameModePolicy.Derive(false, true, true, true).Kind);
		Equal(GameModeKind.AdventureMain, GameModePolicy.Derive(false, true, false, true).Kind);
		Equal(GameModeKind.SoloStandard, GameModePolicy.Derive(false, false, false, true).Kind);
		Equal(GameModeKind.LocalStandard, GameModePolicy.Derive(false, false, false, false).Kind);
		False(GameModePolicy.NetworkStandard.CanReadAdventureRun);
		True(GameModePolicy.AdventureMain.CanReadAdventureRun);
		True(GameModePolicy.AdventureMain.CanWriteAdventureRun);
		False(GameModePolicy.AdventureTutorial.CanReadAdventureRun);
		False(GameModePolicy.AdventureTutorial.CanWriteAdventureRun);
	}

	private static void Wave2CampaignCatalogIsTheSingleStageContract()
	{
		Equal(2, AdventureCampaignCatalog.Tutorial.Count);
		Equal("fish", AdventureCampaignCatalog.Tutorial[0].EnemyAvatarId);
		Equal("lanternfish", AdventureCampaignCatalog.Tutorial[1].EnemyAvatarId);
		Equal(AdventureCampaignCatalog.MainStageCount, AdventureCampaignCatalog.Main.Count);
		for (int index = 0; index < AdventureCampaignCatalog.Main.Count; index++)
		{
			string avatarId = AdventureCampaignCatalog.Main[index].EnemyAvatarId;
			Equal(avatarId, AdventureCampaignCatalog.GetMainAvatarId(index + 1));
			True(AdventureCampaignCatalog.TryGetMainStageIndex(avatarId, out int stageIndex));
			Equal(index + 1, stageIndex);
		}
	}

	private static void Wave2LegacyProfileMappingIsBoundedAndIdempotent()
	{
		Equal(0, AdventureCampaignCatalog.MapLegacyCompletedMainStage(0));
		Equal(0, AdventureCampaignCatalog.MapLegacyCompletedMainStage(2));
		Equal(1, AdventureCampaignCatalog.MapLegacyCompletedMainStage(3));
		Equal(12, AdventureCampaignCatalog.MapLegacyCompletedMainStage(14));
		Equal(0, AdventureCampaignCatalog.MapLegacyCompletedMainStage(-99));
		Equal(12, AdventureCampaignCatalog.MapLegacyCompletedMainStage(99));
		False(AdventureCampaignCatalog.TryMapLegacyRunStage(15, out _));

		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.DefaultProfileSlotId;
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		var legacyProfile = new Godot.Collections.Dictionary
		{
			["schema_version"] = 1,
			["highest_completed_stage"] = 14,
			["selected_avatar_id"] = "shrimp",
			["player_name"] = "Legacy",
			["pending_avatar_unlock_queue"] = "fish|shrimp|shrimp|lanternfish|crab",
			["pending_adventure_run_json"] = string.Empty,
			["playtime_seconds"] = 0.0,
			["created_unix_time"] = 1L,
			["updated_unix_time"] = 1L,
		};
		saveManager.SeedSlot(slotId, legacyProfile);

		GameSaveStore.GameProgressData migrated = GameSaveStore.LoadActiveProfile();
		Equal(12, migrated.HighestCompletedMainAdventureStage);
		Equal("shrimp|crab", migrated.PendingAvatarUnlockQueue);
		True(migrated.ToDictionary().ContainsKey("highest_completed_main_adventure_stage"));
		True(migrated.ToDictionary().ContainsKey("completed_adventure_tutorial_ids_csv"));
		int saveCountAfterFirstLoad = saveManager.SaveToSlotCallCount;
		True(saveCountAfterFirstLoad >= 1);

		GameSaveStore.GameProgressData secondLoad = GameSaveStore.LoadActiveProfile();
		Equal(12, secondLoad.HighestCompletedMainAdventureStage);
		Equal(saveCountAfterFirstLoad, saveManager.SaveToSlotCallCount);
	}

	private static void Wave2TutorialIdsAreIndependentAndNeverEnterMainProgress()
	{
		string completed = AdventureCampaignCatalog.AddCompletedTutorialId(string.Empty, "fish");
		Equal("fish", completed);
		completed = AdventureCampaignCatalog.AddCompletedTutorialId(completed, "lanternfish");
		Equal("fish|lanternfish", completed);
		Equal("fish|lanternfish", AdventureCampaignCatalog.AddCompletedTutorialId(completed, "fish"));
		Equal("fish|lanternfish", AdventureCampaignCatalog.NormalizeCompletedTutorialIdsCsv("fish|fish|lanternfish|shrimp"));
		False(AdventureCampaignCatalog.IsTutorialAvatar("shrimp"));
		False(AdventureCampaignCatalog.IsMainAvatar("fish"));
	}

	private static void Wave2V5RunMigratesOnlyValidLegacyStages()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		const string legacyJson = "{\"SchemaVersion\":5,\"CurrentAvatarStageNumber\":14,\"CurrentRunStageIndex\":99,\"ShouldGrantRunStartUpgrade\":false,\"ShouldGrantVictoryUpgrade\":true,\"AcquiredUpgradeIds\":[],\"CurrentHealth\":100,\"MaxHealth\":100,\"PermanentCaptureDamageBonus\":0,\"QuestionRewardCount\":0,\"HealingOverflowRemainder\":0}";
		SeedProfile(saveManager, slotId, CreateProgressData("Runner", legacyJson));
		AdventureUpgradeManifest manifest = AdventureUpgradeManifest.FromEnabledIds(AdventureUpgradeRuntimeContract.All.Keys);
		var store = new AdventureRunSessionStore();
		store.SetManifest(manifest);
		Equal(AdventureUpgradeRestoreResult.Migrated, store.ReloadFromActiveProfile());
		True(store.TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot));
		Equal(AdventureUpgradeRunSnapshot.CurrentSchemaVersion, snapshot.SchemaVersion);
		Equal(AdventureCampaignCatalog.MainCampaignId, snapshot.CampaignId);
		Equal(12, snapshot.MainStageIndex);
		Equal("boss_k", snapshot.EnemyAvatarId);
		string migratedJson = LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson;
		using (JsonDocument document = JsonDocument.Parse(migratedJson))
		{
			True(document.RootElement.TryGetProperty("CampaignId", out _));
			False(document.RootElement.TryGetProperty("CurrentAvatarStageNumber", out _));
		}
		int saveCount = saveManager.SaveToSlotCallCount;
		Equal(AdventureUpgradeRestoreResult.Restored, store.ReloadFromActiveProfile());
		Equal(saveCount, saveManager.SaveToSlotCallCount);

		const string invalidLegacyJson = "{\"SchemaVersion\":5,\"CurrentAvatarStageNumber\":2,\"ShouldGrantRunStartUpgrade\":false,\"ShouldGrantVictoryUpgrade\":true,\"AcquiredUpgradeIds\":[]}";
		SeedProfile(saveManager, slotId, CreateProgressData("Runner", invalidLegacyJson));
		store.InvalidateProfileSnapshot();
		Equal(AdventureUpgradeRestoreResult.InvalidSnapshotPreserved, store.ReloadFromActiveProfile());
		False(store.HasActiveRun);
		Equal(invalidLegacyJson, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);
	}

	private static void Wave2NetworkStandardCannotTouchAdventureRun()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		const string residualJson = "{\"SchemaVersion\":6,\"CampaignId\":\"deep_tide_throne\",\"MainStageIndex\":1,\"EnemyAvatarId\":\"shrimp\",\"ShouldGrantRunStartUpgrade\":true,\"ShouldGrantVictoryUpgrade\":true,\"AcquiredUpgradeIds\":[],\"CurrentHealth\":100,\"MaxHealth\":100,\"PermanentCaptureDamageBonus\":0,\"QuestionRewardCount\":0,\"HealingOverflowRemainder\":0}";
		SeedProfile(saveManager, slotId, CreateProgressData("Network", residualJson));
		var store = new AdventureRunSessionStore();
		store.ConfigureForPolicy(GameModePolicy.NetworkStandard);
		Equal(AdventureUpgradeRestoreResult.NoSnapshot, store.ReloadFromActiveProfile());
		False(store.HasActiveRun);
		False(store.TryGetSnapshot(out _));
		store.BeginNewMainRun(1, "shrimp");
		store.ClearRun();
		Equal(0, saveManager.SaveToSlotCallCount);
		Equal(residualJson, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);
	}

	private static void Wave2TutorialIsolatesValidMainAdventureRun()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData("Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		store.ConfigureForPolicy(GameModePolicy.AdventureMain);
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(3, "jellyfish"));
		Equal(
			AdventureRunWriteResult.Succeeded,
			store.GrantUpgrade("clubs_borrow_suit_set", AdventureUpgradeTriggerSource.None).WriteResult);
		Equal(AdventureRunWriteResult.Succeeded, store.SetHealth(63, 110));
		True(store.TryGetSnapshot(out AdventureUpgradeRunSnapshot original));
		string originalPayload = LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson;
		int saveCount = saveManager.SaveToSlotCallCount;

		store.ConfigureForPolicy(GameModePolicy.AdventureTutorial);
		False(store.HasActiveRun);
		False(store.RequiresRunRecovery);
		False(store.TryGetSnapshot(out _));
		Equal(AdventureUpgradeRestoreResult.NoSnapshot, store.ReloadFromActiveProfile());
		Equal(AdventureRunWriteResult.WriteNotAllowed, store.BeginNewMainRun(1, "shrimp"));
		Equal(AdventureRunWriteResult.WriteNotAllowed, store.AdvanceToMainStage(4, "manta_ray"));
		Equal(AdventureRunWriteResult.WriteNotAllowed, store.SetHealth(1, 1));
		Equal(AdventureRunWriteResult.WriteNotAllowed, store.ClearRun());
		Equal(saveCount, saveManager.SaveToSlotCallCount);
		Equal(originalPayload, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);

		store.ConfigureForPolicy(GameModePolicy.AdventureMain);
		Equal(AdventureUpgradeRestoreResult.Restored, store.ReloadFromActiveProfile());
		True(store.TryGetSnapshot(out AdventureUpgradeRunSnapshot restored));
		Equal(original.MainStageIndex, restored.MainStageIndex);
		Equal(original.EnemyAvatarId, restored.EnemyAvatarId);
		Equal(original.CurrentHealth, restored.CurrentHealth);
		Equal(original.MaxHealth, restored.MaxHealth);
		Equal(original.AcquiredUpgradeIds.Count, restored.AcquiredUpgradeIds.Count);
		True(store.GetReadModel().HasAcquiredUpgrade("clubs_borrow_suit_set"));
		Equal(originalPayload, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);
	}

	private static void Wave2TutorialIsolatesDamagedMainAdventureRun()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		const string damagedPayload = "{damaged main adventure run";
		SeedProfile(saveManager, slotId, CreateProgressData("Runner", damagedPayload));
		var store = new AdventureRunSessionStore();
		store.ConfigureForPolicy(GameModePolicy.AdventureMain);
		Equal(AdventureUpgradeRestoreResult.InvalidJsonPreserved, store.ReloadFromActiveProfile());
		True(store.RequiresRunRecovery);
		int saveCount = saveManager.SaveToSlotCallCount;

		store.ConfigureForPolicy(GameModePolicy.AdventureTutorial);
		False(store.HasActiveRun);
		False(store.RequiresRunRecovery);
		Equal(AdventureUpgradeRestoreResult.NoSnapshot, store.ReloadFromActiveProfile());
		Equal(AdventureRunWriteResult.WriteNotAllowed, store.ClearRun());
		Equal(saveCount, saveManager.SaveToSlotCallCount);
		Equal(damagedPayload, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);

		store.ConfigureForPolicy(GameModePolicy.AdventureMain);
		Equal(AdventureUpgradeRestoreResult.InvalidJsonPreserved, store.ReloadFromActiveProfile());
		False(store.HasActiveRun);
		True(store.RequiresRunRecovery);
		Equal(damagedPayload, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);
	}
}
