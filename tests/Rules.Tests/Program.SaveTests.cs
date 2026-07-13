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
				definition.SortOrder));
		}

		var store = new AdventureRunSessionStore();
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


}

