using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static void GameSaveProgressRoundTripsPendingAdventureRunJson()
	{
		const string pendingRunJson = "{\"CurrentAvatarStageNumber\":3}";
		GameSaveStore.GameProgressData progress = GameSaveStore.GameProgressData.CreateDefault() with
		{
			PendingAdventureRunJson = pendingRunJson,
		};

		GameSaveStore.GameProgressData roundTripped = GameSaveStore.GameProgressData.FromDictionary(
			progress.ToDictionary(),
			GameSaveStore.GameProgressData.CreateDefault());

		Equal(pendingRunJson, roundTripped.PendingAdventureRunJson);
	}

	private static void DeleteActiveProfileSwitchesReplacementBeforeRemovingOriginalSlot()
	{
		ResetGameSaveStoreTestState();
		string originalSlotId = GameSaveStore.DefaultProfileSlotId;
		string replacementSlotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(originalSlotId);
		SeedProfile(saveManager, originalSlotId, CreateProgressData(playerName: "Active"));

		string activeSlotDuringDelete = string.Empty;
		Godot.DirAccess.BeforeRemoveAbsolute = path =>
		{
			if (path.EndsWith($"{originalSlotId}.json", StringComparison.Ordinal))
			{
				activeSlotDuringDelete = saveManager.GetStoredString(GameSaveStore.ActiveProfileSlotKey);
			}
		};

		Equal(Godot.Error.Ok, GameSaveStore.DeleteProfile(originalSlotId));
		Equal(replacementSlotId, activeSlotDuringDelete);
		Equal(replacementSlotId, saveManager.GetStoredString(GameSaveStore.ActiveProfileSlotKey));
		False(saveManager.HasSlotFile(originalSlotId));
		True(saveManager.HasSlotFile(replacementSlotId));
		Equal(1, saveManager.UnregisteredSlots.Count);
		Equal(originalSlotId, saveManager.UnregisteredSlots[0]);
	}

	private static void DeleteActiveProfileKeepsOriginalSlotWhenReplacementCreationFails()
	{
		ResetGameSaveStoreTestState();
		string originalSlotId = GameSaveStore.DefaultProfileSlotId;
		string replacementSlotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(originalSlotId);
		SeedProfile(saveManager, originalSlotId, CreateProgressData(playerName: "Active"));
		saveManager.SaveSlotResults[replacementSlotId] = Godot.Error.Failed;

		Equal(Godot.Error.Failed, GameSaveStore.DeleteProfile(originalSlotId));
		Equal(originalSlotId, saveManager.GetStoredString(GameSaveStore.ActiveProfileSlotKey));
		True(saveManager.HasSlotFile(originalSlotId));
		False(saveManager.HasSlotFile(replacementSlotId));
		Equal(0, saveManager.UnregisteredSlots.Count);
	}

	private static void DeleteActiveProfileKeepsOriginalSlotWhenActiveSwitchFails()
	{
		ResetGameSaveStoreTestState();
		string originalSlotId = GameSaveStore.DefaultProfileSlotId;
		string replacementSlotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(originalSlotId);
		SeedProfile(saveManager, originalSlotId, CreateProgressData(playerName: "Active"));
		SeedProfile(saveManager, replacementSlotId, CreateProgressData(playerName: "Replacement"));
		saveManager.PersistResult = Godot.Error.Failed;

		Equal(Godot.Error.Failed, GameSaveStore.DeleteProfile(originalSlotId));
		Equal(originalSlotId, saveManager.GetStoredString(GameSaveStore.ActiveProfileSlotKey));
		True(saveManager.HasSlotFile(originalSlotId));
		True(saveManager.HasSlotFile(replacementSlotId));
		Equal(0, saveManager.UnregisteredSlots.Count);
	}

	private static void DamagedPendingAdventureRunJsonStaysPreservedInActiveProfile()
	{
		ResetGameSaveStoreTestState();
		string activeSlotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(activeSlotId);
		SeedProfile(saveManager, activeSlotId, CreateProgressData(
			playerName: "Runner",
			pendingAdventureRunJson: "{bad json"));

		var store = new AdventureRunSessionStore();
		Equal(AdventureUpgradeRestoreResult.InvalidJsonPreserved, store.ReloadFromActiveProfile());

		False(store.HasActiveRun);
		Equal("{bad json", LoadStoredProfile(saveManager, activeSlotId).PendingAdventureRunJson);
		Equal(1, Godot.GD.WarningMessages.Count);
		Equal(0, saveManager.SaveToSlotCallCount);

		Equal(AdventureUpgradeRestoreResult.InvalidJsonPreserved, store.ReloadFromActiveProfile());
		Equal(1, Godot.GD.WarningMessages.Count);
		Equal("{bad json", LoadStoredProfile(saveManager, activeSlotId).PendingAdventureRunJson);
		Equal(0, saveManager.SaveToSlotCallCount);
	}

	private static void ValidPendingAdventureRunJsonReloadsActiveRun()
	{
		ResetGameSaveStoreTestState();
		string activeSlotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(activeSlotId);
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var runState = new AdventureUpgradeRunState(manifest);
		runState.BeginNewRun(3);
		runState.AcquireUpgrade("hearts_thick_blood");
		string pendingRunJson = JsonSerializer.Serialize(runState.ToSnapshot());
		SeedProfile(saveManager, activeSlotId, CreateProgressData(
			playerName: "Runner",
			pendingAdventureRunJson: pendingRunJson));

		var store = new AdventureRunSessionStore();
		store.SetManifest(manifest);
		Equal(AdventureUpgradeRestoreResult.Restored, store.ReloadFromActiveProfile());

		True(store.HasActiveRun);
		True(store.TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot));
		Equal(3, snapshot.CurrentAvatarStageNumber);
		Equal(0, snapshot.CurrentRunStageIndex);
		Equal(1, store.GetRunState().CurrentRunStageIndex);
		Equal(pendingRunJson, LoadStoredProfile(saveManager, activeSlotId).PendingAdventureRunJson);
		Equal(0, Godot.GD.WarningMessages.Count);
	}

	private static void AdventureRunStoreRetriesDeferredRestoreFromPandoraDataLoadedSignalOnce()
	{
		ResetGameSaveStoreTestState();
		string activeSlotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(activeSlotId);
		var legacy = new AdventureUpgradeRunSnapshot(
			4,
			3,
			CurrentRunStageIndex: 1,
			ShouldGrantRunStartUpgrade: false,
			ShouldGrantVictoryUpgrade: true,
			new List<string> { "hearts_thick_blood", "clubs_lush_growth" },
			CurrentHealth: 75,
			MaxHealth: 120,
			ClubFlourishStacks: 4);
		string legacyJson = JsonSerializer.Serialize(legacy);
		SeedProfile(saveManager, activeSlotId, CreateProgressData(
			playerName: "Runner",
			pendingAdventureRunJson: legacyJson));

		var pandora = new Godot.TestPandoraNode();
		foreach (AdventureSuitUpgradeDefinition definition in ReadEnabledAdventureUpgradeDefinitions())
		{
			pandora.Entities.Add(new Godot.TestPandoraEntity(
				definition.Id,
				definition.DisplayName,
				definition.Suit,
				definition.Rarity,
				definition.TriggerText,
				definition.EffectText,
				definition.DescriptionKey,
				definition.Notes,
				definition.Enabled,
				definition.SortOrder,
				definition.EffectValuesRaw,
				definition.DescriptionValueKeysCsv));
		}

		var store = new AdventureRunSessionStore();
		store.ConfigureForPolicy(GameModePolicy.AdventureMain);
		Equal(AdventureUpgradeRestoreResult.DeferredManifestUnavailable, store.ReloadFromActiveProfile());
		store.AddNamedChild("/root/Pandora", pandora);
		store.AddNamedChild("/root/AdventureRunSessionStore", store);
		store._Ready();

		Equal(1, pandora.GetSignalConnectionCount("data_loaded"));
		Equal(AdventureUpgradeRestoreResult.DeferredManifestUnavailable, store.LastRestoreResult);
		False(store.HasActiveRun);
		Equal(legacyJson, LoadStoredProfile(saveManager, activeSlotId).PendingAdventureRunJson);
		Equal(0, saveManager.SaveToSlotCallCount);

		store._Ready();
		Equal(1, pandora.GetSignalConnectionCount("data_loaded"));
		Equal(0, saveManager.SaveToSlotCallCount);

		pandora.IsLoaded = true;
		pandora.EmitSignal("data_loaded");

		True(store.HasActiveRun);
		Equal(AdventureUpgradeRestoreResult.Migrated, store.LastRestoreResult);
		Equal(1, saveManager.SaveToSlotCallCount);
		string migratedJson = LoadStoredProfile(saveManager, activeSlotId).PendingAdventureRunJson;
		AssertCanonicalV5Json(migratedJson);
		False(migratedJson.Contains("clubs_lush_growth", StringComparison.Ordinal));

		pandora.EmitSignal("data_loaded");
		Equal(1, pandora.GetSignalConnectionCount("data_loaded"));
		Equal(1, saveManager.SaveToSlotCallCount);
		Equal(0, Godot.GD.WarningMessages.Count);
	}

	private static void AdventureRunTransactionalWritesPreserveMemoryOnSaveFailure()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();

		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		True(store.TryGetSnapshot(out AdventureUpgradeRunSnapshot before));
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		Equal(AdventureRunWriteResult.SaveFailed, store.AdvanceToMainStage(2, AdventureCampaignCatalog.GetMainAvatarId(2)));
		True(store.TryGetSnapshot(out AdventureUpgradeRunSnapshot after));
		Equal(before.MainStageIndex, after.MainStageIndex);
		Equal(before.EnemyAvatarId, after.EnemyAvatarId);
		Equal(1, ReadStoredRunSnapshot(saveManager, slotId).MainStageIndex);
	}

	private static void AdventureVictoryCommitsProgressRewardAndNextRunAtomically()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		string firstAvatarId = AdventureCampaignCatalog.GetMainAvatarId(1);
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, firstAvatarId));

		Equal(AdventureVictoryCommitResult.Succeeded, store.CommitMainAdventureVictory(firstAvatarId));
		GameSaveStore.GameProgressData committed = LoadStoredProfile(saveManager, slotId);
		Equal(1, committed.HighestCompletedMainAdventureStage);
		True(committed.PendingAvatarUnlockQueue.Contains(firstAvatarId, StringComparison.Ordinal));
		Equal(2, ReadStoredRunSnapshot(saveManager, slotId).MainStageIndex);
		True(store.TryGetSnapshot(out AdventureUpgradeRunSnapshot memorySnapshot));
		Equal(2, memorySnapshot.MainStageIndex);
		int savesAfterCommit = saveManager.SaveToSlotCallCount;

		Equal(AdventureVictoryCommitResult.AlreadyCommitted, store.CommitMainAdventureVictory(firstAvatarId));
		Equal(savesAfterCommit, saveManager.SaveToSlotCallCount);
	}

	private static void AdventureVictorySaveFailurePreservesCurrentRun()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		string firstAvatarId = AdventureCampaignCatalog.GetMainAvatarId(1);
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, firstAvatarId));
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		Equal(AdventureVictoryCommitResult.SaveFailed, store.CommitMainAdventureVictory(firstAvatarId));
		Equal(0, LoadStoredProfile(saveManager, slotId).HighestCompletedMainAdventureStage);
		Equal(1, ReadStoredRunSnapshot(saveManager, slotId).MainStageIndex);
		True(store.TryGetSnapshot(out AdventureUpgradeRunSnapshot memorySnapshot));
		Equal(1, memorySnapshot.MainStageIndex);
	}

	private static void AdventureFinalVictoryClearsRunInAtomicCommit()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner") with
		{
			HighestCompletedMainAdventureStage = AdventureCampaignCatalog.MainStageCount - 1,
		});
		var store = new AdventureRunSessionStore();
		int finalStage = AdventureCampaignCatalog.MainStageCount;
		string finalAvatarId = AdventureCampaignCatalog.GetMainAvatarId(finalStage);
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(finalStage, finalAvatarId));

		Equal(AdventureVictoryCommitResult.Succeeded, store.CommitMainAdventureVictory(finalAvatarId));
		GameSaveStore.GameProgressData committed = LoadStoredProfile(saveManager, slotId);
		Equal(finalStage, committed.HighestCompletedMainAdventureStage);
		Equal(string.Empty, committed.PendingAdventureRunJson);
		False(store.HasActiveRun);
	}

	private static void AdventureVictoryRejectsOutOfSequenceStage()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		string thirdAvatarId = AdventureCampaignCatalog.GetMainAvatarId(3);
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(3, thirdAvatarId));
		int savesBeforeCommit = saveManager.SaveToSlotCallCount;

		Equal(AdventureVictoryCommitResult.OutOfSequence, store.CommitMainAdventureVictory(thirdAvatarId));
		Equal(savesBeforeCommit, saveManager.SaveToSlotCallCount);
		Equal(0, LoadStoredProfile(saveManager, slotId).HighestCompletedMainAdventureStage);
	}

	private static void DamagedAdventureRunRecoveryClearsOnlyRunPayload()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		GameSaveStore.GameProgressData original = CreateProgressData("Runner", "{bad json") with
		{
			SelectedAvatarId = "fox",
			HighestCompletedMainAdventureStage = 4,
			CompletedAdventureTutorialIdsCsv = AdventureCampaignCatalog.Tutorial[0].EnemyAvatarId,
			PendingAvatarUnlockQueue = "unlock_a|unlock_b",
		};
		SeedProfile(saveManager, slotId, original);
		var store = new AdventureRunSessionStore();
		Equal(AdventureUpgradeRestoreResult.InvalidJsonPreserved, store.ReloadFromActiveProfile());
		True(store.RequiresRunRecovery);
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		Equal(AdventureRunWriteResult.SaveFailed, store.ClearRun());
		True(store.RequiresRunRecovery);
		Equal("{bad json", LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);

		Equal(AdventureRunWriteResult.Succeeded, store.ClearRun());
		False(store.RequiresRunRecovery);
		GameSaveStore.GameProgressData recovered = LoadStoredProfile(saveManager, slotId);
		Equal(string.Empty, recovered.PendingAdventureRunJson);
		Equal(original.SelectedAvatarId, recovered.SelectedAvatarId);
		Equal(original.HighestCompletedMainAdventureStage, recovered.HighestCompletedMainAdventureStage);
		Equal(original.CompletedAdventureTutorialIdsCsv, recovered.CompletedAdventureTutorialIdsCsv);
		Equal(original.PendingAvatarUnlockQueue, recovered.PendingAvatarUnlockQueue);
	}

	private static AdventureUpgradeRunSnapshot ReadStoredRunSnapshot(
		Godot.TestSaveManagerNode saveManager,
		string slotId)
	{
		string json = LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson;
		True(AdventureUpgradeRunSnapshot.TryDeserialize(json, out AdventureUpgradeRunSnapshot snapshot, out _, out _));
		return snapshot;
	}


}
