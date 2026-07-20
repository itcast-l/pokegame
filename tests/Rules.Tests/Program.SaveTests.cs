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

	private static void BackupOnlyProfileIsTreatedAsExistingAndLoadable()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		saveManager.SeedBackupSlot(slotId, CreateProgressData(playerName: "Backup Player").ToDictionary());

		IReadOnlyList<GameSaveStore.SaveSlotMetadata> profiles = GameSaveStore.ListProfiles();
		True(profiles[1].Exists);
		Equal("Backup Player", profiles[1].PlayerName);
		Equal(slotId, GameSaveProfileRepository.FindFirstExistingProfileSlotId(saveManager));
		False(string.Equals(slotId, GameSaveProfileRepository.FindFirstEmptyProfileSlotId(saveManager), StringComparison.Ordinal));

		GameSaveStore.GameProgressData loaded = GameSaveStore.LoadProfile(slotId);
		Equal("Backup Player", loaded.PlayerName);
		Equal(0, saveManager.SaveToSlotCallCount);
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

	private static void AdventureRunSnapshotsCannotMutateStoreState()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));

		AdventureUpgradeRunState detached = store.GetRunState();
		detached.SetHealth(1, 150);
		detached.EffectState.QuestionRewardCount = 9;

		AdventureUpgradeRunState current = store.GetRunState();
		Equal(100, current.CurrentHealth);
		Equal(100, current.MaxHealth);
		Equal(0, current.EffectState.QuestionRewardCount);
		Equal(100, ReadStoredRunSnapshot(saveManager, slotId).CurrentHealth);
	}

	private static void AdventureDefeatTransactionRetriesWithoutDoubleRevive()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureDefeatCommitResult failed = store.ResolveMainAdventureDefeat();
		Equal(AdventureRunWriteResult.SaveFailed, failed.WriteResult);
		False(failed.Resolution.HasValue);
		False(store.GetRunState().HasUsedRevive);
		Equal(100, store.GetRunState().CurrentHealth);
		False(ReadStoredRunSnapshot(saveManager, slotId).HasUsedRevive);

		AdventureDefeatCommitResult retried = store.ResolveMainAdventureDefeat();
		Equal(AdventureRunWriteResult.Succeeded, retried.WriteResult);
		Equal(AdventureDefeatResolution.RetryCurrentStage, retried.Resolution);
		True(store.GetRunState().HasUsedRevive);
		Equal(50, store.GetRunState().CurrentHealth);
		True(ReadStoredRunSnapshot(saveManager, slotId).HasUsedRevive);
	}

	private static void AdventureUpgradeGrantCommitsAllFieldsOnce()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		int savesBeforeGrant = saveManager.SaveToSlotCallCount;
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureUpgradeGrantCommitResult failed = store.GrantUpgrade(
			"hearts_thick_blood",
			AdventureUpgradeTriggerSource.RunStart);
		Equal(AdventureRunWriteResult.SaveFailed, failed.WriteResult);
		Equal(AdventureUpgradeAcquireResult.None, failed.AcquireResult);
		Equal(default(AdventureUpgradeAcquisitionEffectResult), failed.EffectResult);
		False(failed.HealingResult.HasValue);
		AdventureUpgradeRunState unchanged = store.GetRunState();
		False(unchanged.HasAcquiredUpgrade("hearts_thick_blood"));
		True(unchanged.ShouldGrantRunStartUpgrade);
		Equal(100, unchanged.CurrentHealth);
		Equal(100, unchanged.MaxHealth);

		AdventureUpgradeGrantCommitResult committed = store.GrantUpgrade(
			"hearts_thick_blood",
			AdventureUpgradeTriggerSource.RunStart);
		Equal(AdventureRunWriteResult.Succeeded, committed.WriteResult);
		Equal(AdventureUpgradeAcquireResult.Acquired, committed.AcquireResult);
		AdventureUpgradeRunState current = store.GetRunState();
		True(current.HasAcquiredUpgrade("hearts_thick_blood"));
		False(current.ShouldGrantRunStartUpgrade);
		Equal(104, current.CurrentHealth);
		Equal(104, current.MaxHealth);
		Equal(savesBeforeGrant + 2, saveManager.SaveToSlotCallCount);
		AdventureUpgradeRunSnapshot stored = ReadStoredRunSnapshot(saveManager, slotId);
		Equal(104, stored.CurrentHealth);
		False(stored.ShouldGrantRunStartUpgrade);
		True(stored.AcquiredUpgradeIds.Contains("hearts_thick_blood"));
	}

	private static void AdventureCompositeTransactionsRollBack()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		Equal(AdventureRunWriteResult.Succeeded, store.SetHealth(50, 100));
		Equal(
			AdventureRunWriteResult.Succeeded,
			store.GrantUpgrade("hearts_question_mark_first_aid", AdventureUpgradeTriggerSource.None).WriteResult);
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);
		AdventureHealingCommitResult failedHealing = store.ApplyHealing(3);
		Equal(AdventureRunWriteResult.SaveFailed, failedHealing.WriteResult);
		Equal(default(AdventureHealingResult), failedHealing.HealingResult);
		Equal(50, store.GetRunState().CurrentHealth);
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureQuestionRewardCommitResult failedQuestion = store.ResolveQuestionReward();
		Equal(AdventureRunWriteResult.SaveFailed, failedQuestion.WriteResult);
		Equal(default(AdventureQuestionRewardEffectResult), failedQuestion.EffectResult);
		False(failedQuestion.HealingResult.HasValue);
		Equal(0, store.GetRunState().EffectState.QuestionRewardCount);
		Equal(50, store.GetRunState().CurrentHealth);

		AdventureQuestionRewardCommitResult committedQuestion = store.ResolveQuestionReward();
		Equal(AdventureRunWriteResult.Succeeded, committedQuestion.WriteResult);
		Equal(1, store.GetRunState().EffectState.QuestionRewardCount);
		True(store.GetRunState().CurrentHealth > 50);

		AdventureUpgradeRunState withCarry = store.GetRunState();
		withCarry.EffectState.NextTurnHealAmount = 3;
		withCarry.EffectState.NextTurnDrawCount = 1;
		int savesBeforeTransientCommit = saveManager.SaveToSlotCallCount;
		Equal(AdventureRunWriteResult.Succeeded, store.ReplaceEffectState(withCarry.EffectState));
		Equal(savesBeforeTransientCommit, saveManager.SaveToSlotCallCount);
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		int healthBeforeTurnStart = store.GetRunState().CurrentHealth;
		AdventureTurnStartCommitResult failedTurnStart = store.ResolveTurnStartEffects();
		Equal(AdventureRunWriteResult.SaveFailed, failedTurnStart.WriteResult);
		Equal(default(AdventureTurnStartEffectResult), failedTurnStart.EffectResult);
		False(failedTurnStart.HealingResult.HasValue);
		Equal(3, store.GetRunState().EffectState.NextTurnHealAmount);
		Equal(1, store.GetRunState().EffectState.NextTurnDrawCount);
		Equal(healthBeforeTurnStart, store.GetRunState().CurrentHealth);

		AdventureTurnStartCommitResult committedTurnStart = store.ResolveTurnStartEffects();
		Equal(AdventureRunWriteResult.Succeeded, committedTurnStart.WriteResult);
		Equal(0, store.GetRunState().EffectState.NextTurnHealAmount);
		Equal(0, store.GetRunState().EffectState.NextTurnDrawCount);
		True(store.GetRunState().CurrentHealth > healthBeforeTurnStart);

		int healthBeforeDamage = store.GetRunState().CurrentHealth;
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);
		AdventureHealingCommitResult failedDamageRecovery =
			store.UpdateHealthAndApplyHealing(healthBeforeDamage - 10, 3);
		Equal(AdventureRunWriteResult.SaveFailed, failedDamageRecovery.WriteResult);
		Equal(default(AdventureHealingResult), failedDamageRecovery.HealingResult);
		Equal(healthBeforeDamage, store.GetRunState().CurrentHealth);

		AdventureHealingCommitResult committedDamageRecovery =
			store.UpdateHealthAndApplyHealing(healthBeforeDamage - 10, 3);
		Equal(AdventureRunWriteResult.Succeeded, committedDamageRecovery.WriteResult);
		Equal(healthBeforeDamage - 7, store.GetRunState().CurrentHealth);
	}

	private static void AdventureCaptureActionTransactionRetriesOnce()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		Equal(AdventureRunWriteResult.Succeeded, store.GrantUpgrade("clubs_borrow_suit_set", AdventureUpgradeTriggerSource.None).WriteResult);
		Equal(AdventureRunWriteResult.Succeeded, store.GrantUpgrade("clubs_gap_rooting", AdventureUpgradeTriggerSource.None).WriteResult);
		AdventureUpgradeRunState configured = store.GetRunState();
		configured.EffectState.ClubBorrowSuitSetArmed = true;
		configured.EffectState.ClubMissClimbGrowthStacks = 3;
		Equal(AdventureRunWriteResult.Succeeded, store.ReplaceEffectState(configured.EffectState));
		string storedBefore = LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson;
		AdventureCaptureCommitInput input = CreateAdventureCaptureCommitInput();
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureCaptureCommitResult failed = store.ResolveCaptureAction(input);
		Equal(AdventureRunWriteResult.SaveFailed, failed.WriteResult);
		Equal(default(AdventureCaptureDamageResult), failed.DamageResult);
		Equal(default(AdventurePostCaptureEffectResult), failed.PostCaptureEffectResult);
		Equal(default(AdventureClubCaptureEffectResult), failed.ClubCaptureEffectResult);
		Equal(default(AdventureQuestionRewardEffectResult), failed.QuestionRewardEffectResult);
		False(failed.HealingResult.HasValue);
		Equal(default(AdventureLowHealthRescueResult), failed.RescueResult);
		Equal(0, failed.CurrentHealth);
		False(failed.ConsumedHalfHealthRescue);
		False(failed.ConsumedQuarterHealthRescue);
		AdventureUpgradeRunState unchanged = store.GetRunState();
		True(unchanged.EffectState.ClubBorrowSuitSetArmed);
		False(unchanged.EffectState.ClubGapRootingUsedThisTurn);
		Equal(3, unchanged.EffectState.ClubMissClimbGrowthStacks);
		Equal(storedBefore, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);

		AdventureCaptureCommitResult committed = store.ResolveCaptureAction(input);
		Equal(AdventureRunWriteResult.Succeeded, committed.WriteResult);
		True(committed.DamageResult.FinalDamage > 0);
		AdventureUpgradeRunState current = store.GetRunState();
		False(current.EffectState.ClubBorrowSuitSetArmed);
		True(current.EffectState.ClubGapRootingUsedThisTurn);
		Equal(0, current.EffectState.ClubMissClimbGrowthStacks);
		Equal(storedBefore, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);
	}

	private static void AdventureMissActionTransactionRetriesOnce()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		foreach (string upgradeId in new[] { "clubs_borrow_suit_set", "hearts_miss_bandage", "diamonds_miss_discount" })
		{
			Equal(AdventureRunWriteResult.Succeeded, store.GrantUpgrade(upgradeId, AdventureUpgradeTriggerSource.None).WriteResult);
		}
		AdventureUpgradeRunState configured = store.GetRunState();
		configured.EffectState.ClubBorrowSuitSetArmed = true;
		Equal(AdventureRunWriteResult.Succeeded, store.ReplaceEffectState(configured.EffectState));
		AdventureMissCommitInput input = new("4", 6, Array.Empty<string>());
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureMissCommitResult failed = store.ResolveMissAction(input);
		Equal(AdventureRunWriteResult.SaveFailed, failed.WriteResult);
		Equal(default(AdventureMainPlayerMissEffectResult), failed.EffectResult);
		Equal(default(AdventurePostCaptureEffectResult), failed.RecoveryEffectResult);
		False(failed.HealingResult.HasValue);
		False(failed.CommitBandageAtRealTurnEnd);
		AdventureUpgradeRunState unchanged = store.GetRunState();
		True(unchanged.EffectState.ClubBorrowSuitSetArmed);
		Equal(0, unchanged.EffectState.NextCaptureDamageBonus);
		Equal(0, unchanged.EffectState.NextTurnHealAmount);

		AdventureMissCommitResult committed = store.ResolveMissAction(input);
		Equal(AdventureRunWriteResult.Succeeded, committed.WriteResult);
		True(committed.CommitBandageAtRealTurnEnd);
		AdventureUpgradeRunState current = store.GetRunState();
		False(current.EffectState.ClubBorrowSuitSetArmed);
		Equal(1, current.EffectState.NextCaptureDamageBonus);
		Equal(0, current.EffectState.NextTurnHealAmount);
		Equal(0, current.EffectState.NextTurnDrawCount);
	}

	private static void AdventureDirectDamageTransactionRetriesOnce()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		string storedBefore = LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson;
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureDirectDamageCommitResult failed = store.ApplyDirectDamage(new AdventureDirectDamageCommitInput(30));
		Equal(AdventureRunWriteResult.SaveFailed, failed.WriteResult);
		Equal(0, failed.PreviousHealth);
		Equal(0, failed.DamagedHealth);
		Equal(default(AdventureLowHealthRescueResult), failed.RescueResult);
		Equal(default(AdventureHealingResult), failed.HealingResult);
		Equal(100, store.GetRunState().CurrentHealth);
		Equal(storedBefore, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);

		AdventureDirectDamageCommitResult committed = store.ApplyDirectDamage(new AdventureDirectDamageCommitInput(30));
		Equal(AdventureRunWriteResult.Succeeded, committed.WriteResult);
		Equal(100, committed.PreviousHealth);
		Equal(70, committed.DamagedHealth);
		Equal(70, committed.HealingResult.CurrentHealth);
		Equal(70, store.GetRunState().CurrentHealth);
		Equal(70, ReadStoredRunSnapshot(saveManager, slotId).CurrentHealth);
	}

	private static void AdventureRealTurnEndTransactionRetriesOnce()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		Equal(AdventureRunWriteResult.Succeeded, store.GrantUpgrade("hearts_miss_bandage", AdventureUpgradeTriggerSource.None).WriteResult);
		AdventureUpgradeRunState configured = store.GetRunState();
		configured.EffectState.ClubBorrowSuitSetArmed = true;
		configured.EffectState.ClubGapRootingUsedThisTurn = true;
		configured.EffectState.ClubShortBranchReturnUsedThisTurn = true;
		Equal(AdventureRunWriteResult.Succeeded, store.ReplaceEffectState(configured.EffectState));
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		Equal(AdventureRunWriteResult.SaveFailed, store.ResolveRealTurnEnd(commitMissBandage: true));
		AdventureUpgradeRunState unchanged = store.GetRunState();
		True(unchanged.EffectState.ClubBorrowSuitSetArmed);
		True(unchanged.EffectState.ClubGapRootingUsedThisTurn);
		True(unchanged.EffectState.ClubShortBranchReturnUsedThisTurn);
		Equal(0, unchanged.EffectState.NextTurnHealAmount);

		Equal(AdventureRunWriteResult.Succeeded, store.ResolveRealTurnEnd(commitMissBandage: true));
		AdventureUpgradeRunState current = store.GetRunState();
		False(current.EffectState.ClubBorrowSuitSetArmed);
		False(current.EffectState.ClubGapRootingUsedThisTurn);
		False(current.EffectState.ClubShortBranchReturnUsedThisTurn);
		Equal(3, current.EffectState.NextTurnHealAmount);
		Equal(1, current.EffectState.NextTurnDrawCount);
	}

	private static AdventureCaptureCommitInput CreateAdventureCaptureCommitInput()
	{
		PlayingCard playedCard = new("clubs", "4", null!);
		var context = new CaptureResolutionContext(
			new BaseCardActionContext(
				1,
				1,
				"transaction_test",
				false,
				playedCard.InstanceId,
				playedCard,
				Array.Empty<string>()),
			Array.Empty<AdventureCapturedCardEntry>(),
			Array.Empty<AdventureCaptureScoreItem>(),
			AdventureCaptureMatchSource.Rank);
		return new AdventureCaptureCommitInput(
			CapturerIsMainPlayer: true,
			PlayedCardRank: playedCard.Rank,
			ConsumeBorrowSuitSet: true,
			ConsumeGapRooting: true,
			new AdventureCaptureDamageInput(BaseDamage: 5, CaptureContext: context),
			SuccessfulCapturesThisTurn: 1,
			context,
			new AdventureClubCaptureCommitInput(0, 0, 0, 4, false, true));
	}

	private static void AdventureProductionTransactionSourceContractStaysAtomic()
	{
		string scriptsDirectory = Path.Combine(FindRepositoryRoot(), "scripts");
		string[] boardPaths = Directory.GetFiles(
			scriptsDirectory,
			"GameBoardScene*.cs",
			SearchOption.TopDirectoryOnly);
		string boardSource = string.Join(
			"\n",
			Array.ConvertAll(boardPaths, File.ReadAllText));

		False(boardSource.Contains(".ReplaceEffectState(", StringComparison.Ordinal));
		False(boardSource.Contains("PersistAdventureEffectState(", StringComparison.Ordinal));
		False(boardSource.Contains("MarkRunStartUpgradeGranted(", StringComparison.Ordinal));
		False(boardSource.Contains("MarkVictoryUpgradeGranted(", StringComparison.Ordinal));

		const string writeMethodNames =
			"BeginNewRun|BeginNewMainRun|AdvanceToStage|AdvanceToMainStage|ResolveMainAdventureDefeat|GrantUpgrade|SetHealth|ReplaceEffectState|UpdateVitalsAndEffects|ApplyHealing|UpdateHealthAndApplyHealing|ResolveQuestionReward|ResolveTurnStartEffects|ApplyDirectDamage|ResolveMissAction|ResolveCaptureAction|ResolveRealTurnEnd|ResetBattleScopedEffects|ClearRun|CommitMainAdventureVictory|RecoverDamagedRun";
		string writeMethodPattern =
			@"\bstore[!?]?\.(?<method>" + writeMethodNames + @")\b";
		var expectedWriteCallCounts = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["BeginNewRun"] = 3,
			["BeginNewMainRun"] = 4,
			["AdvanceToMainStage"] = 1,
			["ResolveMainAdventureDefeat"] = 1,
			["GrantUpgrade"] = 5,
			["ApplyHealing"] = 1,
			["ResolveTurnStartEffects"] = 1,
			["ApplyDirectDamage"] = 2,
			["ResolveMissAction"] = 2,
			["ResolveCaptureAction"] = 1,
			["ResolveRealTurnEnd"] = 1,
			["ResetBattleScopedEffects"] = 2,
			["ClearRun"] = 1,
			["CommitMainAdventureVictory"] = 1,
		};
		var actualWriteCallCounts = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (Match match in Regex.Matches(boardSource, writeMethodPattern))
		{
			string methodName = match.Groups["method"].Value;
			actualWriteCallCounts[methodName] =
				actualWriteCallCounts.GetValueOrDefault(methodName) + 1;
		}
		Equal(expectedWriteCallCounts.Count, actualWriteCallCounts.Count);
		foreach ((string methodName, int expectedCount) in expectedWriteCallCounts)
		{
			Equal(expectedCount, actualWriteCallCounts.GetValueOrDefault(methodName));
		}
		False(Regex.IsMatch(
			boardSource,
			@"(?m)^\s*store[!?]?\.(?:" + writeMethodNames + @")\s*\([^\r\n]*\);\s*$"));

		string captureSource = File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"GameBoardScene.TableCaptureFlow.cs"));
		AssertSourceOrder(
			captureSource,
			"PlanQuestionCardRewardAsync(",
			"ResolveCaptureAction(",
			"_captureStateService.ApplyCapture(",
			"ApplyQuestionCardRewardPlan(");

		string bossSource = File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"GameBoardScene.BossTableAbilities.cs"));
		AssertSourceOrder(
			bossSource,
			"await CommitAdventureDirectDamageAsync(damagePoints)",
			"await PlayAdventureAvatarAttackAsync(",
			"() => ApplyCommittedAdventureDirectDamage(");

		string turnSource = File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"GameBoardScene.TurnAdvancement.cs"));
		AssertSourceOrder(
			turnSource,
			"store.ResolveRealTurnEnd(commitMissBandage)",
			"_state.AdventureExtraActionQueue.EndRealTurn()");
		int turnSetterStart = turnSource.IndexOf(
			"private async Task<bool> SetCurrentTurnPlayerIndexAsync(",
			StringComparison.Ordinal);
		int turnSetterEnd = turnSource.IndexOf(
			"private async Task BeginRealMainPlayerTurnIfNeededAsync(",
			turnSetterStart,
			StringComparison.Ordinal);
		True(turnSetterStart >= 0);
		True(turnSetterEnd > turnSetterStart);
		string turnSetterSource = turnSource[turnSetterStart..turnSetterEnd];
		AssertSourceOrder(
			turnSetterSource,
			"await EndRealMainPlayerTurnAsync()",
			"_currentTurnPlayerIndex = nextTurnPlayerIndex");

		string boardMainSource = File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"GameBoardScene.cs"));
		AssertSourceOrder(
			boardMainSource,
			"await InitializeAdventureUpgradeRunForCurrentBoardAsync()",
			"await PrepareAdventureBattleForDealAsync()",
			"DealCardsToPlayers(playOpeningDealAnimation: true");
		string upgradeSource = File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"GameBoardScene.AdventureUpgrades.cs"));
		AssertSourceOrder(
			upgradeSource,
			"store.ResetBattleScopedEffects",
			"DealCardsToPlayers(playOpeningDealAnimation");
	}

	private static void AssertSourceOrder(string source, params string[] fragments)
	{
		int previousIndex = -1;
		foreach (string fragment in fragments)
		{
			int index = source.IndexOf(fragment, previousIndex + 1, StringComparison.Ordinal);
			True(index > previousIndex);
			previousIndex = index;
		}
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
