using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Deckland.Rules;

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

	private static void DeferredSaveValuesMarkDirtyOnlyForChangesAndFlushOnce()
	{
		ResetGameSaveStoreTestState();
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(
			GameSaveStore.DefaultProfileSlotId);

		Equal(Godot.Error.Ok, GameSaveStore.SetValueDeferred("settings.test.value", 1));
		Equal(1, saveManager.MarkDirtyCallCount);
		Equal(0, saveManager.PersistCallCount);
		Equal(Godot.Error.Ok, GameSaveStore.SetValueDeferred("settings.test.value", 1));
		Equal(1, saveManager.MarkDirtyCallCount);
		Equal(Godot.Error.Ok, GameSaveStore.SetValuesDeferred(
			new GameSaveStore.SaveValue("settings.test.value", 2),
			new GameSaveStore.SaveValue("settings.test.other", true)));
		Equal(2, saveManager.MarkDirtyCallCount);
		Equal(0, saveManager.PersistCallCount);

		Equal(
			Godot.Error.Ok,
			GameSaveStore.FlushDeferredValuesAsync().GetAwaiter().GetResult());
		Equal(1, saveManager.PersistCallCount);
		False(saveManager.Dirty);
		Equal(
			Godot.Error.Ok,
			GameSaveStore.FlushDeferredValuesAsync().GetAwaiter().GetResult());
		Equal(1, saveManager.PersistCallCount);
		Equal(0, saveManager.GetSignalConnectionCount("async_flush_finished"));
	}

	private static void DeferredSaveValuesRejectSealedChangesAtomically()
	{
		ResetGameSaveStoreTestState();
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(
			GameSaveStore.DefaultProfileSlotId);
		saveManager.SeedValue("settings.test.single", 1);
		saveManager.SeedValue("settings.test.batch_a", 10);
		saveManager.SeedValue("settings.test.batch_b", "before");
		saveManager.MarkDirtyResult = Godot.Error.Unavailable;

		Equal(
			Godot.Error.Unavailable,
			GameSaveStore.SetValueDeferred("settings.test.single", 2));
		Equal(1, saveManager.MarkDirtyCallCount);
		False(saveManager.Dirty);
		Equal(1, saveManager.Call(
			"get_value",
			new Godot.StringName("settings.test.single"),
			(Godot.Variant)0).AsInt32());

		Equal(
			Godot.Error.Unavailable,
			GameSaveStore.SetValuesDeferred(
				new GameSaveStore.SaveValue("settings.test.batch_a", 20),
				new GameSaveStore.SaveValue("settings.test.batch_b", "after")));
		Equal(2, saveManager.MarkDirtyCallCount);
		False(saveManager.Dirty);
		Equal(10, saveManager.Call(
			"get_value",
			new Godot.StringName("settings.test.batch_a"),
			(Godot.Variant)0).AsInt32());
		Equal("before", saveManager.GetStoredString("settings.test.batch_b"));

		Equal(
			Godot.Error.Ok,
			GameSaveStore.SetValueDeferred("settings.test.single", 1));
		Equal(
			Godot.Error.Ok,
			GameSaveStore.SetValuesDeferred(
				new GameSaveStore.SaveValue("settings.test.batch_a", 10),
				new GameSaveStore.SaveValue("settings.test.batch_b", "before")));
		Equal(2, saveManager.MarkDirtyCallCount);
		False(saveManager.Dirty);
	}

	private static void AsyncProfileSaveCorrelatesRequestAndDisconnectsHandlers()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Before"));

		GameSaveStore.GameProgressData candidate = CreateProgressData(playerName: "After") with
		{
			HighestCompletedMainAdventureStage = 2,
		};
		Equal(
			Godot.Error.Ok,
			GameSaveStore.SaveActiveProfileAsync(candidate).GetAwaiter().GetResult());
		Equal(1, saveManager.SaveToSlotCallCount);
		Equal(1, saveManager.LoadFromSlotCallCount);
		Equal("After", LoadStoredProfile(saveManager, slotId).PlayerName);
		Equal(2, LoadStoredProfile(saveManager, slotId).HighestCompletedMainAdventureStage);
		Equal(0, saveManager.GetSignalConnectionCount("async_save_finished"));
		Equal(
			Godot.Error.Ok,
			GameSaveStore.FlushPendingWritesAsync().GetAwaiter().GetResult());
		Equal(0, saveManager.GetSignalConnectionCount("async_flush_finished"));
	}

	private static void SynchronousProfileSavesShareCoordinatorAndCommittedCandidate()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Before"));
		ProfileWriteLease pendingLease = GameSaveProfileWriteCoordinator.AcquireAsync(
			profile => profile with { PlayerName = "Pending" })
			.GetAwaiter()
			.GetResult()!;
		GameSaveStore.GameProgressData synchronousCandidate = CreateProgressData(
			playerName: "Synchronous") with
		{
			PendingAvatarUnlockQueue = "shrimp",
			PlaytimeSeconds = 42.5,
		};
		Godot.Time.UnixTimeFromSystem = 1_800_000_000;
		Godot.Time.UnixTimeIncrement = 1;

		Equal(Godot.Error.Busy, GameSaveStore.SaveActiveProfile(synchronousCandidate));
		Equal(Godot.Error.Busy, GameSaveStore.SaveProfile(slotId, synchronousCandidate));
		Equal(0, saveManager.SaveToSlotCallCount);
		Equal("Before", LoadStoredProfile(saveManager, slotId).PlayerName);

		pendingLease.Abort();
		Equal(Godot.Error.Ok, GameSaveStore.SaveActiveProfile(synchronousCandidate));
		GameSaveStore.GameProgressData storedCandidate = LoadStoredProfile(saveManager, slotId);
		ProfileWriteLease cachedLease = GameSaveProfileWriteCoordinator.AcquireAsync(
			static profile => profile,
			forcePersist: false).GetAwaiter().GetResult()!;
		Equal(storedCandidate, cachedLease.BaseProfile);
		cachedLease.Abort();

		Equal(
			Godot.Error.Ok,
			GameSaveStore.UpdateActiveProfileAsync(profile => profile with
			{
				HighestCompletedMainAdventureStage = 2,
			}).GetAwaiter().GetResult());
		GameSaveStore.GameProgressData finalProfile = LoadStoredProfile(saveManager, slotId);
		Equal("Synchronous", finalProfile.PlayerName);
		Equal("shrimp", finalProfile.PendingAvatarUnlockQueue);
		Equal(42.5, finalProfile.PlaytimeSeconds);
		Equal(2, finalProfile.HighestCompletedMainAdventureStage);
	}

	private static void AsyncSaveInteropExceptionsReleaseCoordinatorAndCallbacks()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Before"));
		saveManager.NextQueueSaveException = new InvalidOperationException("queue save failed");

		Equal(
			Godot.Error.Failed,
			GameSaveStore.SaveActiveProfileAsync(CreateProgressData(playerName: "Failed"))
				.GetAwaiter()
				.GetResult());
		Equal(0, GameSaveProfileWriteCoordinator.AdmittedOperationCount);
		Equal(0, saveManager.GetSignalConnectionCount("async_save_finished"));
		Equal("Before", LoadStoredProfile(saveManager, slotId).PlayerName);

		Equal(
			Godot.Error.Ok,
			GameSaveStore.UpdateActiveProfileAsync(profile => profile with
			{
				PlayerName = "Recovered",
			}).GetAwaiter().GetResult());
		Equal("Recovered", LoadStoredProfile(saveManager, slotId).PlayerName);
		Equal(0, GameSaveProfileWriteCoordinator.AdmittedOperationCount);

		saveManager.NextQueueFlushException = new InvalidOperationException("queue flush failed");
		Equal(
			Godot.Error.Failed,
			GameSaveStore.FlushPendingWritesAsync().GetAwaiter().GetResult());
		Equal(0, saveManager.GetSignalConnectionCount("async_flush_finished"));
	}

	private static void PreparedOperationFailuresRetryOrAbortCleanly()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Retry"));
		var retryStore = new AdventureRunSessionStore();
		retryStore.SetManifest(CreateFullAdventureUpgradeManifest());
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> retryOperation =
			retryStore.PrepareBeginNewMainRun(
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1));
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		Equal(
			AdventureRunWriteResult.SaveFailed,
			retryOperation.PersistAttemptAsync().GetAwaiter().GetResult());
		Equal(1, GameSaveProfileWriteCoordinator.AdmittedOperationCount);
		Equal(
			AdventureRunWriteResult.Succeeded,
			retryOperation.PersistAttemptAsync().GetAwaiter().GetResult());
		Equal(0, GameSaveProfileWriteCoordinator.AdmittedOperationCount);

		ResetGameSaveStoreTestState();
		slotId = GameSaveStore.GetProfileSlotId(2);
		saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Abort"));
		var abortStore = new AdventureRunSessionStore();
		abortStore.SetManifest(CreateFullAdventureUpgradeManifest());
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> abortOperation =
			abortStore.PrepareBeginNewMainRun(
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1));
		FieldInfo preconditionField = abortOperation.GetType().GetField(
			"_profilePrecondition",
			BindingFlags.Instance | BindingFlags.NonPublic)!;
		preconditionField.SetValue(
			abortOperation,
			new Func<ProfileWriteLease, bool>(_ => throw new InvalidOperationException("unknown failure")));

		bool threw = false;
		try
		{
			_ = abortOperation.PersistAttemptAsync().GetAwaiter().GetResult();
		}
		catch (InvalidOperationException)
		{
			threw = true;
			abortOperation.Abort();
		}

		True(threw);
		Equal(0, GameSaveProfileWriteCoordinator.AdmittedOperationCount);
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> nextOperation =
			abortStore.PrepareBeginNewMainRun(
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1));
		True(nextOperation.IsPrepared);
		nextOperation.Abort();

		string scriptsDirectory = Path.Combine(FindRepositoryRoot(), "scripts");
		string gateSource = File.ReadAllText(Path.Combine(scriptsDirectory, "AdventureSaveGateCoordinator.cs"));
		AssertSourceOrder(
			gateSource,
			"result = await attemptAsync();",
			"catch (Exception error)",
			"abortOperation?.Invoke();",
			"result = attemptExceptionResult(error);",
			"await AbortAsync();");
		string menuSource = File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"AdventureAvatarMenuScene.Navigation.cs"));
		AssertSourceOrder(
			menuSource,
			"result = await operation.PersistAttemptAsync();",
			"catch (System.Exception error)",
			"operation.Abort();",
			"return AdventureRunWriteResult.SaveFailed;");
	}

	private static void AdventureSaveGateRetriesThenSucceeds()
	{
		var statuses = new List<string>();
		var delays = new List<double>();
		int attempts = 0;
		int succeededCount = 0;
		var gate = new AdventureSaveGateCoordinator(
			isStopping: () => false,
			delayAsync: seconds =>
			{
				delays.Add(seconds);
				return Task.CompletedTask;
			},
			presentStatus: statuses.Add,
			onSucceeded: () => succeededCount++,
			onAbortStarted: () => throw new InvalidOperationException("Unexpected abort."),
			returnToMenuAsync: () => throw new InvalidOperationException("Unexpected return."));

		AdventureSaveGateResult<int> result = gate.ExecuteAsync(
			() => Task.FromResult(++attempts),
			value => value == 3,
			value => value < 3).GetAwaiter().GetResult();

		True(result.Succeeded);
		Equal(3, result.Result);
		Equal(3, attempts);
		Equal(2, gate.RetryCount);
		Equal(AdventureSaveGateState.Idle, gate.State);
		Equal(string.Empty, gate.StatusLocalizationKey);
		Equal(1, succeededCount);
		Equal(2, delays.Count);
		Equal(0.5, delays[0]);
		Equal(1.0, delays[1]);
		Equal("GAME_ADVENTURE_SAVING", statuses[0]);
		Equal("GAME_ADVENTURE_SAVE_RETRYING", statuses[1]);
		Equal("GAME_ADVENTURE_SAVE_RETRYING", statuses[2]);
	}

	private static void AdventureSaveGateExhaustionAbortsAndReturns()
	{
		var statuses = new List<string>();
		var delays = new List<double>();
		int attempts = 0;
		int operationAbortCount = 0;
		int abortStartedCount = 0;
		int returnCount = 0;
		var gate = new AdventureSaveGateCoordinator(
			isStopping: () => false,
			delayAsync: seconds =>
			{
				delays.Add(seconds);
				return Task.CompletedTask;
			},
			presentStatus: statuses.Add,
			onSucceeded: () => throw new InvalidOperationException("Unexpected success."),
			onAbortStarted: () => abortStartedCount++,
			returnToMenuAsync: () =>
			{
				returnCount++;
				return Task.CompletedTask;
			});

		AdventureSaveGateResult<int> result = gate.ExecuteAsync(
			() => Task.FromResult(++attempts),
			_ => false,
			_ => true,
			abortOperation: () => operationAbortCount++).GetAwaiter().GetResult();

		False(result.Succeeded);
		Equal(4, attempts);
		Equal(3, gate.RetryCount);
		Equal(1, operationAbortCount);
		Equal(1, abortStartedCount);
		Equal(1, returnCount);
		Equal(AdventureSaveGateState.Aborting, gate.State);
		Equal("GAME_ADVENTURE_RETURNING_TO_MENU", gate.StatusLocalizationKey);
		Equal(5, delays.Count);
		Equal("GAME_ADVENTURE_SAVE_FAILED_REPLAY", statuses[^2]);
		Equal("GAME_ADVENTURE_RETURNING_TO_MENU", statuses[^1]);
	}

	private static void AdventureSaveGateRejectsConcurrentExecution()
	{
		var pendingAttempt = new TaskCompletionSource<int>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		int secondAttemptCount = 0;
		var gate = new AdventureSaveGateCoordinator(
			isStopping: () => false,
			delayAsync: _ => Task.CompletedTask,
			presentStatus: _ => { },
			onSucceeded: () => { },
			onAbortStarted: () => throw new InvalidOperationException("Unexpected abort."),
			returnToMenuAsync: () => throw new InvalidOperationException("Unexpected return."));

		Task<AdventureSaveGateResult<int>> first = gate.ExecuteAsync(
			() => pendingAttempt.Task,
			value => value == 1,
			_ => false);
		True(gate.IsBlocking);
		AdventureSaveGateResult<int> rejected = gate.ExecuteAsync(
			() => Task.FromResult(++secondAttemptCount),
			_ => true,
			_ => false).GetAwaiter().GetResult();
		False(rejected.Succeeded);
		Equal(0, secondAttemptCount);

		pendingAttempt.SetResult(1);
		True(first.GetAwaiter().GetResult().Succeeded);
		False(gate.IsBlocking);
	}

	private static void AvatarUnlockPromptFailurePathsRestoreInteractionSourceContract()
	{
		string scriptsDirectory = Path.Combine(FindRepositoryRoot(), "scripts");
		string mainMenuSource = File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"MainMenuScene.AvatarUnlockPrompt.cs"));
		string adventureMenuSource = File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"AdventureAvatarMenuScene.AvatarUnlockPrompt.cs"));

		AssertSourceOrder(
			mainMenuSource,
			"PeekPendingAdventureAvatarUnlockAsync()",
			"TryShowAvatarUnlockPrompt(unlockedAvatar)",
			"_isConsumingPendingAvatarUnlock = true;",
			"ConsumePendingAdventureAvatarUnlockAsync(unlockedAvatarId)");
		True(mainMenuSource.Contains("finally", StringComparison.Ordinal));
		int probeStart = mainMenuSource.IndexOf(
			"private async void ShowPendingAdventureAvatarUnlockPrompt()",
			StringComparison.Ordinal);
		int modalStart = mainMenuSource.IndexOf(
			"_isConsumingPendingAvatarUnlock = true;",
			probeStart,
			StringComparison.Ordinal);
		True(probeStart >= 0 && modalStart > probeStart);
		False(mainMenuSource[probeStart..modalStart].Contains(
			"SetMainMenuButtonsDisabled(true)",
			StringComparison.Ordinal));

		AssertSourceOrder(
			adventureMenuSource,
			"if (!TryEnsureAvatarUnlockPrompt())",
			"RestoreAdventureMenuAfterAvatarUnlockPromptFailure();",
			"_isConsumingPendingAvatarUnlock = true;",
			"ConsumePendingAdventureAvatarUnlockAsync()");
		True(adventureMenuSource.Contains("finally", StringComparison.Ordinal));
		True(adventureMenuSource.Contains(
			"if (!TryShowAvatarUnlockPrompt(unlockedAvatar))",
			StringComparison.Ordinal));
	}

	private static void PendingAvatarUnlockQueuePeekAndExpectedConsume()
	{
		string[] queue = { "invalid", "BOSS_ONE", "boss_two" };
		static string Normalize(string value) => value.Trim().ToLowerInvariant();
		static bool IsEligible(string value) => value.StartsWith("boss_", StringComparison.Ordinal);

		True(PendingAvatarUnlockQueueRules.TryPeek(queue, Normalize, IsEligible, out string candidate));
		Equal("boss_one", candidate);
		Equal(3, queue.Length);

		False(PendingAvatarUnlockQueueRules.TryConsumeExpected(
			queue,
			"boss_two",
			Normalize,
			IsEligible,
			out List<string> staleRemaining));
		Equal(3, staleRemaining.Count);
		Equal("invalid", staleRemaining[0]);

		True(PendingAvatarUnlockQueueRules.TryConsumeExpected(
			queue,
			"boss_one",
			Normalize,
			IsEligible,
			out List<string> remaining));
		Equal(1, remaining.Count);
		Equal("boss_two", remaining[0]);
	}

	private static void PendingAvatarUnlockStorageCoordination()
	{
		static string Normalize(string value) => value.Trim().ToLowerInvariant();
		static bool IsEligible(string value) => value.StartsWith("boss_", StringComparison.Ordinal);
		static List<string> Parse(string value) => new(
			value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Queue") with
		{
			PendingAvatarUnlockQueue = "boss_one|boss_two",
		});

		string peekedCandidate = string.Empty;
		Equal(Godot.Error.Ok, GameSaveStore.UpdateActiveProfileAsync(profile =>
		{
			PendingAvatarUnlockQueueRules.TryPeek(
				Parse(profile.PendingAvatarUnlockQueue),
				Normalize,
				IsEligible,
				out peekedCandidate);
			return profile;
		}).GetAwaiter().GetResult());
		Equal("boss_one", peekedCandidate);
		Equal(0, saveManager.SaveToSlotCallCount);
		Equal("boss_one|boss_two", LoadStoredProfile(saveManager, slotId).PendingAvatarUnlockQueue);

		ProfileWriteLease heldLease = GameSaveProfileWriteCoordinator.AcquireAsync(profile => profile with
		{
			PendingAvatarUnlockQueue = "boss_two",
		}).GetAwaiter().GetResult()!;
		string candidateAfterWait = string.Empty;
		Task<Godot.Error> waitingPeek = GameSaveStore.UpdateActiveProfileAsync(profile =>
		{
			PendingAvatarUnlockQueueRules.TryPeek(
				Parse(profile.PendingAvatarUnlockQueue),
				Normalize,
				IsEligible,
				out candidateAfterWait);
			return profile;
		});
		False(waitingPeek.IsCompleted);
		Equal(Godot.Error.Ok, heldLease.PersistAttemptAsync().GetAwaiter().GetResult());
		heldLease.CompleteSuccess();
		Equal(Godot.Error.Ok, waitingPeek.GetAwaiter().GetResult());
		Equal("boss_two", candidateAfterWait);
		Equal(1, saveManager.SaveToSlotCallCount);

		Equal(Godot.Error.Ok, GameSaveStore.UpdateActiveProfileAsync(profile =>
		{
			List<string> queue = Parse(profile.PendingAvatarUnlockQueue);
			return PendingAvatarUnlockQueueRules.TryConsumeExpected(
				queue,
				"boss_one",
				Normalize,
				IsEligible,
				out List<string> remaining)
					? profile with { PendingAvatarUnlockQueue = string.Join('|', remaining) }
					: profile;
		}).GetAwaiter().GetResult());
		Equal(1, saveManager.SaveToSlotCallCount);
		Equal("boss_two", LoadStoredProfile(saveManager, slotId).PendingAvatarUnlockQueue);

		ResetGameSaveStoreTestState();
		saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Failure") with
		{
			PendingAvatarUnlockQueue = "boss_one|boss_two",
		});
		saveManager.SaveSlotResults[slotId] = Godot.Error.Failed;
		Equal(Godot.Error.Failed, GameSaveStore.UpdateActiveProfileAsync(profile =>
		{
			List<string> queue = Parse(profile.PendingAvatarUnlockQueue);
			True(PendingAvatarUnlockQueueRules.TryConsumeExpected(
				queue,
				"boss_one",
				Normalize,
				IsEligible,
				out List<string> remaining));
			return profile with { PendingAvatarUnlockQueue = string.Join('|', remaining) };
		}).GetAwaiter().GetResult());
		Equal("boss_one|boss_two", LoadStoredProfile(saveManager, slotId).PendingAvatarUnlockQueue);
	}

	private static void ProfileCoordinatorUpdatesLatestCandidateAndSkipsNoOp()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Before"));

		Equal(
			Godot.Error.Ok,
			GameSaveStore.UpdateActiveProfileAsync(profile => profile with
			{
				PlayerName = "Updated",
			}).GetAwaiter().GetResult());
		bool secondUpdateSawFirstCandidate = false;
		Equal(
			Godot.Error.Ok,
			GameSaveStore.UpdateActiveProfileAsync(profile =>
			{
				secondUpdateSawFirstCandidate = profile.PlayerName == "Updated";
				return profile with { HighestCompletedMainAdventureStage = 2 };
			}).GetAwaiter().GetResult());

		True(secondUpdateSawFirstCandidate);
		Equal(2, saveManager.SaveToSlotCallCount);
		Equal(1, saveManager.LoadFromSlotCallCount);
		GameSaveStore.GameProgressData committed = LoadStoredProfile(saveManager, slotId);
		Equal("Updated", committed.PlayerName);
		Equal(2, committed.HighestCompletedMainAdventureStage);

		Equal(
			Godot.Error.Ok,
			GameSaveStore.UpdateActiveProfileAsync(profile => profile).GetAwaiter().GetResult());
		Equal(2, saveManager.SaveToSlotCallCount);
		Equal(0, saveManager.GetSignalConnectionCount("async_save_finished"));
	}

	private static void ProfileTopologyRejectsChangesWhileWriteLeaseHeld()
	{
		ResetGameSaveStoreTestState();
		string firstSlotId = GameSaveStore.GetProfileSlotId(1);
		string secondSlotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(firstSlotId);
		SeedProfile(saveManager, firstSlotId, CreateProgressData(playerName: "First"));
		SeedProfile(saveManager, secondSlotId, CreateProgressData(playerName: "Second"));
		ProfileWriteLease lease = GameSaveProfileWriteCoordinator.AcquireAsync(
			profile => profile with { PlayerName = "Pending" })
			.GetAwaiter()
			.GetResult()!;

		Equal(Godot.Error.Busy, GameSaveStore.SetActiveProfile(secondSlotId));
		Equal(Godot.Error.Busy, GameSaveStore.DeleteProfile(firstSlotId));
		Equal(
			Godot.Error.Busy,
			GameSaveStore.CreateProfile(
				GameSaveStore.DefaultProfileSlotId,
				"Created",
				CharacterAvatarCatalog.DefaultAvatarId));
		Equal(firstSlotId, saveManager.GetStoredString(GameSaveStore.ActiveProfileSlotKey));
		True(saveManager.HasSlotFile(firstSlotId));
		True(saveManager.HasSlotFile(secondSlotId));

		lease.Abort();
		Equal(Godot.Error.Ok, GameSaveStore.SetActiveProfile(secondSlotId));
	}

	private static void ProfileCoordinatorSealDrainsAdmittedWaiters()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Before"));
		ProfileWriteLease firstLease = GameSaveProfileWriteCoordinator.AcquireAsync(
			profile => profile with { PlayerName = "First" })
			.GetAwaiter()
			.GetResult()!;
		Task<ProfileWriteLease?> waitingLeaseTask =
			GameSaveProfileWriteCoordinator.AcquireAsync(
				profile => profile with { HighestCompletedMainAdventureStage = 2 });
		False(waitingLeaseTask.IsCompleted);
		Equal(2, GameSaveProfileWriteCoordinator.AdmittedOperationCount);

		Task drainTask = GameSaveProfileWriteCoordinator.SealAndDrainAsync();
		False(drainTask.IsCompleted);
		Equal(
			Godot.Error.Unavailable,
			GameSaveStore.UpdateActiveProfileAsync(profile => profile)
				.GetAwaiter()
				.GetResult());
		Equal(2, GameSaveProfileWriteCoordinator.AdmittedOperationCount);

		Equal(Godot.Error.Ok, firstLease.PersistAttemptAsync().GetAwaiter().GetResult());
		firstLease.CompleteSuccess();
		ProfileWriteLease secondLease = waitingLeaseTask.GetAwaiter().GetResult()!;
		False(drainTask.IsCompleted);
		Equal(Godot.Error.Ok, secondLease.PersistAttemptAsync().GetAwaiter().GetResult());
		secondLease.CompleteSuccess();
		drainTask.GetAwaiter().GetResult();
		Equal(0, GameSaveProfileWriteCoordinator.AdmittedOperationCount);
		Equal("First", LoadStoredProfile(saveManager, slotId).PlayerName);
		Equal(2, LoadStoredProfile(saveManager, slotId).HighestCompletedMainAdventureStage);
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
		Equal(1, store.GetReadModel().CurrentRunStageIndex);
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
		store.SetManifest(CreateFullAdventureUpgradeManifest());

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

		AdventureRunReadModel detached = store.GetReadModel();
		_ = detached with
		{
			CurrentHealth = 1,
			MaxHealth = 150,
			Effects = detached.Effects with { QuestionRewardCount = 9 },
		};

		AdventureRunReadModel current = store.GetReadModel();
		Equal(100, current.CurrentHealth);
		Equal(100, current.MaxHealth);
		Equal(0, current.Effects.QuestionRewardCount);
		Equal(100, ReadStoredRunSnapshot(saveManager, slotId).CurrentHealth);
	}

	private static void AdventureDefeatTransactionRetriesWithoutDoubleRevive()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureDefeatCommitResult failed = store.ResolveMainAdventureDefeat();
		Equal(AdventureRunWriteResult.SaveFailed, failed.WriteResult);
		False(failed.Resolution.HasValue);
		False(store.GetReadModel().HasUsedRevive);
		Equal(100, store.GetReadModel().CurrentHealth);
		False(ReadStoredRunSnapshot(saveManager, slotId).HasUsedRevive);

		AdventureDefeatCommitResult retried = store.ResolveMainAdventureDefeat();
		Equal(AdventureRunWriteResult.Succeeded, retried.WriteResult);
		Equal(AdventureDefeatResolution.RetryCurrentStage, retried.Resolution);
		True(store.GetReadModel().HasUsedRevive);
		Equal(50, store.GetReadModel().CurrentHealth);
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
		AdventureRunReadModel unchanged = store.GetReadModel();
		False(unchanged.HasAcquiredUpgrade("hearts_thick_blood"));
		True(unchanged.ShouldGrantRunStartUpgrade);
		Equal(100, unchanged.CurrentHealth);
		Equal(100, unchanged.MaxHealth);
		Equal(string.Empty, LoadStoredProfile(saveManager, slotId).UnlockedSkillCodexUpgradeIdsCsv);

		AdventureUpgradeGrantCommitResult committed = store.GrantUpgrade(
			"hearts_thick_blood",
			AdventureUpgradeTriggerSource.RunStart);
		Equal(AdventureRunWriteResult.Succeeded, committed.WriteResult);
		Equal(AdventureUpgradeAcquireResult.Acquired, committed.AcquireResult);
		AdventureRunReadModel current = store.GetReadModel();
		True(current.HasAcquiredUpgrade("hearts_thick_blood"));
		False(current.ShouldGrantRunStartUpgrade);
		Equal(104, current.CurrentHealth);
		Equal(104, current.MaxHealth);
		Equal(savesBeforeGrant + 2, saveManager.SaveToSlotCallCount);
		GameSaveStore.GameProgressData committedProfile = LoadStoredProfile(saveManager, slotId);
		Equal("hearts_thick_blood", committedProfile.UnlockedSkillCodexUpgradeIdsCsv);
		AdventureUpgradeRunSnapshot stored = ReadStoredRunSnapshot(saveManager, slotId);
		Equal(104, stored.CurrentHealth);
		False(stored.ShouldGrantRunStartUpgrade);
		True(stored.AcquiredUpgradeIds.Contains("hearts_thick_blood"));

		int savesBeforeDuplicate = saveManager.SaveToSlotCallCount;
		AdventureUpgradeGrantCommitResult duplicate = store.GrantUpgrade(
			"hearts_thick_blood",
			AdventureUpgradeTriggerSource.None);
		Equal(AdventureRunWriteResult.Succeeded, duplicate.WriteResult);
		Equal(AdventureUpgradeAcquireResult.AlreadyAcquired, duplicate.AcquireResult);
		Equal(savesBeforeDuplicate, saveManager.SaveToSlotCallCount);
		Equal("hearts_thick_blood", LoadStoredProfile(saveManager, slotId).UnlockedSkillCodexUpgradeIdsCsv);

		foreach (string rejectedUpgradeId in new[] { "hearts_forged_unknown", "hearts_blood_tide_resonance" })
		{
			AdventureUpgradeGrantCommitResult rejected = store.GrantUpgrade(
				rejectedUpgradeId,
				AdventureUpgradeTriggerSource.None);
			Equal(AdventureRunWriteResult.Succeeded, rejected.WriteResult);
			Equal(AdventureUpgradeAcquireResult.RejectedUnknownUpgrade, rejected.AcquireResult);
		}
		Equal(savesBeforeDuplicate, saveManager.SaveToSlotCallCount);
		Equal("hearts_thick_blood", LoadStoredProfile(saveManager, slotId).UnlockedSkillCodexUpgradeIdsCsv);
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
		Equal(50, store.GetReadModel().CurrentHealth);
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureQuestionRewardCommitResult failedQuestion = store.ResolveQuestionReward();
		Equal(AdventureRunWriteResult.SaveFailed, failedQuestion.WriteResult);
		Equal(default(AdventureQuestionRewardEffectResult), failedQuestion.EffectResult);
		False(failedQuestion.HealingResult.HasValue);
		Equal(0, store.GetReadModel().Effects.QuestionRewardCount);
		Equal(50, store.GetReadModel().CurrentHealth);

		AdventureQuestionRewardCommitResult committedQuestion = store.ResolveQuestionReward();
		Equal(AdventureRunWriteResult.Succeeded, committedQuestion.WriteResult);
		Equal(1, store.GetReadModel().Effects.QuestionRewardCount);
		True(store.GetReadModel().CurrentHealth > 50);

		AdventureEffectStateSnapshot withCarry = store.GetReadModel().Effects with
		{
			NextTurnHealAmount = 3,
			NextTurnDrawCount = 1,
		};
		int savesBeforeTransientCommit = saveManager.SaveToSlotCallCount;
		Equal(AdventureRunWriteResult.Succeeded, store.ReplaceEffectState(withCarry));
		Equal(savesBeforeTransientCommit, saveManager.SaveToSlotCallCount);
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		int healthBeforeTurnStart = store.GetReadModel().CurrentHealth;
		AdventureTurnStartCommitResult failedTurnStart = store.ResolveTurnStartEffects();
		Equal(AdventureRunWriteResult.SaveFailed, failedTurnStart.WriteResult);
		Equal(default(AdventureTurnStartEffectResult), failedTurnStart.EffectResult);
		False(failedTurnStart.HealingResult.HasValue);
		Equal(3, store.GetReadModel().Effects.NextTurnHealAmount);
		Equal(1, store.GetReadModel().Effects.NextTurnDrawCount);
		Equal(healthBeforeTurnStart, store.GetReadModel().CurrentHealth);

		AdventureTurnStartCommitResult committedTurnStart = store.ResolveTurnStartEffects();
		Equal(AdventureRunWriteResult.Succeeded, committedTurnStart.WriteResult);
		Equal(0, store.GetReadModel().Effects.NextTurnHealAmount);
		Equal(0, store.GetReadModel().Effects.NextTurnDrawCount);
		True(store.GetReadModel().CurrentHealth > healthBeforeTurnStart);

		int healthBeforeDamage = store.GetReadModel().CurrentHealth;
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);
		AdventureHealingCommitResult failedDamageRecovery =
			store.UpdateHealthAndApplyHealing(healthBeforeDamage - 10, 3);
		Equal(AdventureRunWriteResult.SaveFailed, failedDamageRecovery.WriteResult);
		Equal(default(AdventureHealingResult), failedDamageRecovery.HealingResult);
		Equal(healthBeforeDamage, store.GetReadModel().CurrentHealth);

		AdventureHealingCommitResult committedDamageRecovery =
			store.UpdateHealthAndApplyHealing(healthBeforeDamage - 10, 3);
		Equal(AdventureRunWriteResult.Succeeded, committedDamageRecovery.WriteResult);
		Equal(healthBeforeDamage - 7, store.GetReadModel().CurrentHealth);
	}

	private static void AdventureUpgradeReplacementRollsBackOnSaveFailure()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		string[] activeIds =
		{
			"hearts_warm_current_supply",
			"hearts_long_capture_recovery",
			"hearts_miss_bandage",
			"hearts_question_mark_first_aid",
			"diamonds_small_insurance",
			"diamonds_question_mark_raise",
			"spades_dark_harbor_reveal",
			"clubs_short_branch_return",
		};
		foreach (string id in activeIds)
		{
			Equal(
				AdventureUpgradeAcquireResult.Acquired,
				store.GrantUpgrade(id, AdventureUpgradeTriggerSource.None).AcquireResult);
		}
		string payloadBefore = LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson;
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureUpgradeGrantCommitResult failed = store.GrantUpgrade(
			"clubs_gap_rooting",
			AdventureUpgradeTriggerSource.None,
			activeIds[2]);
		Equal(AdventureRunWriteResult.SaveFailed, failed.WriteResult);
		Equal(AdventureUpgradeAcquireResult.None, failed.AcquireResult);
		AdventureRunReadModel unchanged = store.GetReadModel();
		True(unchanged.HasAcquiredUpgrade(activeIds[2]));
		False(unchanged.HasAcquiredUpgrade("clubs_gap_rooting"));
		False(unchanged.HasRetiredUpgrade(activeIds[2]));
		Equal(payloadBefore, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);

		AdventureUpgradeGrantCommitResult committed = store.GrantUpgrade(
			"clubs_gap_rooting",
			AdventureUpgradeTriggerSource.None,
			activeIds[2]);
		Equal(AdventureRunWriteResult.Succeeded, committed.WriteResult);
		Equal(AdventureUpgradeAcquireResult.Acquired, committed.AcquireResult);
		Equal(activeIds[2], committed.ReplacedUpgradeId);
		True(store.GetReadModel().HasRetiredUpgrade(activeIds[2]));
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
		AdventureEffectStateSnapshot configured = store.GetReadModel().Effects with
		{
			ClubBorrowSuitSetArmed = true,
			ClubMissClimbGrowthStacks = 3,
		};
		Equal(AdventureRunWriteResult.Succeeded, store.ReplaceEffectState(configured));
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
		AdventureRunReadModel unchanged = store.GetReadModel();
		True(unchanged.Effects.ClubBorrowSuitSetArmed);
		False(unchanged.Effects.ClubGapRootingUsedThisTurn);
		Equal(3, unchanged.Effects.ClubMissClimbGrowthStacks);
		Equal(storedBefore, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);

		AdventureCaptureCommitResult committed = store.ResolveCaptureAction(input);
		Equal(AdventureRunWriteResult.Succeeded, committed.WriteResult);
		True(committed.DamageResult.FinalDamage > 0);
		AdventureRunReadModel current = store.GetReadModel();
		False(current.Effects.ClubBorrowSuitSetArmed);
		True(current.Effects.ClubGapRootingUsedThisTurn);
		Equal(0, current.Effects.ClubMissClimbGrowthStacks);
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
		AdventureEffectStateSnapshot configured = store.GetReadModel().Effects with
		{
			ClubBorrowSuitSetArmed = true,
		};
		Equal(AdventureRunWriteResult.Succeeded, store.ReplaceEffectState(configured));
		AdventureMissCommitInput input = new("4", 6, Array.Empty<string>());
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureMissCommitResult failed = store.ResolveMissAction(input);
		Equal(AdventureRunWriteResult.SaveFailed, failed.WriteResult);
		Equal(default(AdventureMainPlayerMissEffectResult), failed.EffectResult);
		Equal(default(AdventurePostCaptureEffectResult), failed.RecoveryEffectResult);
		False(failed.HealingResult.HasValue);
		False(failed.CommitBandageAtRealTurnEnd);
		AdventureRunReadModel unchanged = store.GetReadModel();
		True(unchanged.Effects.ClubBorrowSuitSetArmed);
		Equal(0, unchanged.Effects.NextCaptureDamageBonus);
		Equal(0, unchanged.Effects.NextTurnHealAmount);

		AdventureMissCommitResult committed = store.ResolveMissAction(input);
		Equal(AdventureRunWriteResult.Succeeded, committed.WriteResult);
		True(committed.CommitBandageAtRealTurnEnd);
		AdventureRunReadModel current = store.GetReadModel();
		False(current.Effects.ClubBorrowSuitSetArmed);
		Equal(1, current.Effects.NextCaptureDamageBonus);
		Equal(0, current.Effects.NextTurnHealAmount);
		Equal(0, current.Effects.NextTurnDrawCount);
	}

	private static void AdventureDirectDamageTransactionRetriesOnce()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1)));
		string storedBefore = LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson;
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		AdventureDirectDamageCommitResult failed = store.ApplyDirectDamage(new AdventureDirectDamageCommitInput(30));
		Equal(AdventureRunWriteResult.SaveFailed, failed.WriteResult);
		Equal(0, failed.PreviousHealth);
		Equal(0, failed.DamagedHealth);
		Equal(default(AdventureLowHealthRescueResult), failed.RescueResult);
		Equal(default(AdventureHealingResult), failed.HealingResult);
		Equal(100, store.GetReadModel().CurrentHealth);
		Equal(storedBefore, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);

		AdventureDirectDamageCommitResult committed = store.ApplyDirectDamage(new AdventureDirectDamageCommitInput(30));
		Equal(AdventureRunWriteResult.Succeeded, committed.WriteResult);
		Equal(100, committed.PreviousHealth);
		Equal(70, committed.DamagedHealth);
		Equal(70, committed.HealingResult.CurrentHealth);
		Equal(70, store.GetReadModel().CurrentHealth);
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
		AdventureEffectStateSnapshot configured = store.GetReadModel().Effects with
		{
			ClubBorrowSuitSetArmed = true,
			ClubGapRootingUsedThisTurn = true,
			ClubShortBranchReturnUsedThisTurn = true,
		};
		Equal(AdventureRunWriteResult.Succeeded, store.ReplaceEffectState(configured));
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);

		Equal(AdventureRunWriteResult.SaveFailed, store.ResolveRealTurnEnd(commitMissBandage: true));
		AdventureRunReadModel unchanged = store.GetReadModel();
		True(unchanged.Effects.ClubBorrowSuitSetArmed);
		True(unchanged.Effects.ClubGapRootingUsedThisTurn);
		True(unchanged.Effects.ClubShortBranchReturnUsedThisTurn);
		Equal(0, unchanged.Effects.NextTurnHealAmount);

		Equal(AdventureRunWriteResult.Succeeded, store.ResolveRealTurnEnd(commitMissBandage: true));
		AdventureRunReadModel current = store.GetReadModel();
		False(current.Effects.ClubBorrowSuitSetArmed);
		False(current.Effects.ClubGapRootingUsedThisTurn);
		False(current.Effects.ClubShortBranchReturnUsedThisTurn);
		Equal(3, current.Effects.NextTurnHealAmount);
		Equal(1, current.Effects.NextTurnDrawCount);
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
			new AdventureCaptureDamageInput(BaseDamage: 5, CaptureContext: context.ToRuleFacts()),
			SuccessfulCapturesThisTurn: 1,
			context.ToRuleFacts(),
			new AdventureClubCaptureCommitInput(0, 0, 0, 4, true));
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

		const string legacyWriteMethodNames =
			"BeginNewRun|BeginNewMainRun|AdvanceToStage|AdvanceToMainStage|ResolveMainAdventureDefeat|GrantUpgrade|SetHealth|ReplaceEffectState|UpdateVitalsAndEffects|ApplyHealing|UpdateHealthAndApplyHealing|ResolveQuestionReward|ResolveTurnStartEffects|ApplyDirectDamage|ResolveMissAction|ResolveCaptureAction|ResolveRealTurnEnd|ResetBattleScopedEffects|ClearRun|CommitMainAdventureVictory|RecoverDamagedRun";
		const string preparedMethodNames =
			"PrepareBeginNewMainRun|PrepareAdvanceToMainStage|PrepareResolveMainAdventureDefeat|PrepareGrantUpgrade|PrepareSetHealth|PrepareReplaceEffectState|PrepareUpdateVitalsAndEffects|PrepareApplyHealing|PrepareUpdateHealthAndApplyHealing|PrepareResolveQuestionReward|PrepareResolveTurnStartEffects|PrepareApplyDirectDamage|PrepareResolveMissAction|PrepareResolveCaptureAction|PrepareResolveRealTurnEnd|PrepareResetBattleScopedEffects|PrepareClearRun|PrepareRestartMainRun|PrepareCommitMainAdventureVictory";
		string legacyPattern = @"\bstore[!?]?\.(?:" + legacyWriteMethodNames + @")\b";
		foreach (string boardPath in boardPaths)
		{
			if (Path.GetFileName(boardPath).Contains(".Debug.", StringComparison.Ordinal))
			{
				continue;
			}

			string productionSource = File.ReadAllText(boardPath);
			False(Regex.IsMatch(productionSource, legacyPattern));
		}

		string[] adventureMenuPaths = Directory.GetFiles(
			scriptsDirectory,
			"AdventureAvatarMenuScene*.cs",
			SearchOption.TopDirectoryOnly);
		string adventureMenuSource = string.Join(
			"\n",
			Array.ConvertAll(adventureMenuPaths, File.ReadAllText));
		False(Regex.IsMatch(
			adventureMenuSource,
			@"\.(?:" + legacyWriteMethodNames + @")\s*\("));
		True(adventureMenuSource.Contains("PrepareBeginNewMainRun(", StringComparison.Ordinal));
		True(adventureMenuSource.Contains("PrepareClearRun(", StringComparison.Ordinal));
		True(adventureMenuSource.Contains("PrepareRestartMainRun(", StringComparison.Ordinal));
		Equal(
			1,
			Regex.Matches(adventureMenuSource, @"GameSaveStore\.LoadActiveProfile\s*\(").Count);
		False(File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"GameBoardScene.TurnEndState.cs")).Contains(
			"RecordAdventureVictory(",
			StringComparison.Ordinal));

		True(Regex.IsMatch(boardSource, @"\bstore[!?]?\.(?:" + preparedMethodNames + @")\b"));
		False(Regex.IsMatch(
			boardSource,
			@"(?m)^\s*store[!?]?\.(?:" + legacyWriteMethodNames + @")\s*\([^\r\n]*\);\s*$"));

		string captureSource = File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"GameBoardScene.TableCaptureFlow.cs"));
		AssertSourceOrder(
			captureSource,
			"PlanQuestionCardRewardAsync(",
			"ResolveCaptureAction(",
			"TrySubmitCaptureCardsRuleCommand(",
			"ApplyQuestionCardRewardPlan(");
		False(captureSource.Contains(
			"_tableCards.Remove(clubGapRootingTargetTableCard)",
			StringComparison.Ordinal));
		False(Regex.IsMatch(
			captureSource,
			@"_tableCards\s*\.(?:Add|AddRange|Insert|Remove|RemoveAt|RemoveRange|Clear)\s*\("));

		string upgradeSource = string.Concat(
			Directory.GetFiles(
				scriptsDirectory,
				"GameBoardScene.AdventureUpgrades*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));
		False(Regex.IsMatch(
			upgradeSource,
			@"_playerScores\s*\[[^\]]+\]\s*="));
		False(Regex.IsMatch(
			upgradeSource,
			@"_playerDecks\s*\[[^\]]+\]\.RemoveAt\s*\("));

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
			"store.PrepareResolveRealTurnEnd(commitMissBandage)",
			"ExecutePreparedAdventureSaveGateAsync(",
			"TrySubmitEndRealTurnRuleCommand(");
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
			"TrySubmitAdvanceTurnRuleCommand(");

		string boardMainSource = File.ReadAllText(Path.Combine(
			scriptsDirectory,
			"GameBoardScene.cs"));
		AssertSourceOrder(
			boardMainSource,
			"await InitializeAdventureUpgradeRunForCurrentBoardAsync()",
			"await PrepareAdventureBattleForDealAsync()",
			"DealCardsToPlayers(playOpeningDealAnimation: true");
		AssertSourceOrder(
			upgradeSource,
			"store.PrepareResetBattleScopedEffects()",
			"ExecutePreparedAdventureSaveGateAsync(",
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
		store.SetManifest(CreateFullAdventureUpgradeManifest());
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

	private static void PreparedAdventureRestartPersistsOneAtomicCandidate()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> begin =
			store.PrepareBeginNewMainRun(3, AdventureCampaignCatalog.GetMainAvatarId(3));
		True(begin.IsPrepared);
		False(store.HasActiveRun);
		Equal(
			AdventureRunWriteResult.Succeeded,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());
		Equal(3, store.GetReadModel().MainStageIndex);

		int savesBeforeRestart = saveManager.SaveToSlotCallCount;
		int attemptsBeforeRestart = saveManager.SaveAttemptData.Count;
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> restart =
			store.PrepareRestartMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1));
		True(restart.IsPrepared);
		Equal(3, store.GetReadModel().MainStageIndex);
		Equal(savesBeforeRestart, saveManager.SaveToSlotCallCount);

		Equal(
			AdventureRunWriteResult.Succeeded,
			restart.PersistAttemptAsync().GetAwaiter().GetResult());
		Equal(savesBeforeRestart + 1, saveManager.SaveToSlotCallCount);
		Equal(attemptsBeforeRestart + 1, saveManager.SaveAttemptData.Count);
		Equal(1, store.GetReadModel().MainStageIndex);
		Equal(1, ReadStoredRunSnapshot(saveManager, slotId).MainStageIndex);
		GameSaveStore.GameProgressData persistedCandidate = GameSaveStore.GameProgressData.FromDictionary(
			saveManager.SaveAttemptData[^1],
			GameSaveStore.GameProgressData.CreateDefault());
		False(string.IsNullOrWhiteSpace(persistedCandidate.PendingAdventureRunJson));
	}

	private static void PreparedAdventureRestartReplacesDamagedPayloadAtomically()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		const string damagedPayload = "{damaged adventure run";
		SeedProfile(saveManager, slotId, CreateProgressData("Runner", damagedPayload));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());

		Equal(
			AdventureUpgradeRestoreResult.InvalidJsonPreserved,
			store.ReloadFromActiveProfile());
		True(store.RequiresRunRecovery);
		int savesBeforeRestart = saveManager.SaveToSlotCallCount;
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> restart =
			store.PrepareRestartMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1));
		True(restart.IsPrepared);
		True(store.RequiresRunRecovery);
		Equal(damagedPayload, LoadStoredProfile(saveManager, slotId).PendingAdventureRunJson);

		Equal(
			AdventureRunWriteResult.Succeeded,
			restart.PersistAttemptAsync().GetAwaiter().GetResult());
		False(store.RequiresRunRecovery);
		True(store.HasActiveRun);
		Equal(savesBeforeRestart + 1, saveManager.SaveToSlotCallCount);
		Equal(1, ReadStoredRunSnapshot(saveManager, slotId).MainStageIndex);
	}

	private static void PreparedAdventurePendingUnbindDetachesAfterSettlement()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var store = new AdventureRunSessionStore();
		store.SetManifest(manifest);
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> begin =
			store.PrepareBeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1));
		Equal(
			AdventureRunWriteResult.Succeeded,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());

		var random = new TestRuleRandomSource(99101);
		var boardAuthority = new AuthorityRuleSession(random, MatchRuleProfile.Empty);
		True(store.BindAuthorityRuleSession(boardAuthority));
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> health =
			store.PrepareSetHealth(55, 100);
		True(health.IsPrepared);
		True(boardAuthority.HasPreparedAdventure);
		var replacementAuthority = new AuthorityRuleSession(
			new TestRuleRandomSource(99103),
			MatchRuleProfile.Empty);
		False(store.BindAuthorityRuleSession(replacementAuthority));
		True(boardAuthority.HasPreparedAdventure);
		store.UnbindAuthorityRuleSession(boardAuthority);
		True(boardAuthority.HasPreparedAdventure);

		Equal(
			AdventureRunWriteResult.Succeeded,
			health.PersistAttemptAsync().GetAwaiter().GetResult());
		False(boardAuthority.HasPreparedAdventure);
		Equal(55, store.GetReadModel().CurrentHealth);

		AdventureRuleCommandResult laterBoardMutation = boardAuthority.SubmitAdventure(
			new SetAdventureHealthCommand(
				999,
				boardAuthority.AdventureReadModel.Revision,
				33,
				100),
			_ => true);
		True(laterBoardMutation.Accepted);
		Equal(33, boardAuthority.AdventureReadModel.CurrentHealth);
		Equal(55, store.GetReadModel().CurrentHealth);
	}

	private static void PreparedAdventureCommitFailureRecoversPersistedAuthority()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var store = new AdventureRunSessionStore();
		store.SetManifest(manifest);
		var random = new TestRuleRandomSource(99102);
		var boardAuthority = new AuthorityRuleSession(random, MatchRuleProfile.Empty);
		True(store.BindAuthorityRuleSession(boardAuthority));

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> begin =
			store.PrepareBeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1));
		True(begin.IsPrepared);
		_ = random.NextIntInclusive(0, 100);
		Equal(
			AdventureRunWriteResult.Succeeded,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());

		False(boardAuthority.HasPreparedAdventure);
		True(store.HasActiveRun);
		Equal(1, store.GetReadModel().MainStageIndex);
		Equal(1, ReadStoredRunSnapshot(saveManager, slotId).MainStageIndex);
		Equal(1L, boardAuthority.AdventureReadModel.Revision);
		True(Godot.GD.ErrorMessages.Exists(message => message.Contains(
			"recovered from the committed profile candidate",
			StringComparison.Ordinal)));
	}

	private static void PreparedVictoryRetriesOneStableProfileCandidate()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		string existingUnlock = AdventureCampaignCatalog.GetMainAvatarId(3);
		GameSaveStore.GameProgressData original = CreateProgressData(playerName: "Runner") with
		{
			PendingAvatarUnlockQueue = existingUnlock,
			UnlockedSkillCodexUpgradeIdsCsv = "hearts_thick_blood",
			PlaytimeSeconds = 123.5,
		};
		SeedProfile(saveManager, slotId, original);
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		string firstAvatarId = AdventureCampaignCatalog.GetMainAvatarId(1);
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> begin =
			store.PrepareBeginNewMainRun(1, firstAvatarId);
		Equal(
			AdventureRunWriteResult.Succeeded,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());
		Equal(1, saveManager.LoadFromSlotCallCount);

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureVictoryCommitResult> victory =
			store.PrepareCommitMainAdventureVictory(firstAvatarId);
		True(victory.IsPrepared);
		int attemptsBeforeVictory = saveManager.SaveAttemptData.Count;
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);
		Equal(
			AdventureVictoryCommitResult.SaveFailed,
			victory.PersistAttemptAsync().GetAwaiter().GetResult());
		Equal(1, store.GetReadModel().MainStageIndex);
		Equal(0, LoadStoredProfile(saveManager, slotId).HighestCompletedMainAdventureStage);
		Equal(
			AdventureVictoryCommitResult.Succeeded,
			victory.PersistAttemptAsync().GetAwaiter().GetResult());

		Equal(attemptsBeforeVictory + 2, saveManager.SaveAttemptData.Count);
		GameSaveStore.GameProgressData firstCandidate = GameSaveStore.GameProgressData.FromDictionary(
			saveManager.SaveAttemptData[^2],
			GameSaveStore.GameProgressData.CreateDefault());
		GameSaveStore.GameProgressData secondCandidate = GameSaveStore.GameProgressData.FromDictionary(
			saveManager.SaveAttemptData[^1],
			GameSaveStore.GameProgressData.CreateDefault());
		Equal(firstCandidate, secondCandidate);
		Equal(1, saveManager.LoadFromSlotCallCount);

		GameSaveStore.GameProgressData committed = LoadStoredProfile(saveManager, slotId);
		Equal(1, committed.HighestCompletedMainAdventureStage);
		True(committed.PendingAvatarUnlockQueue.Contains(existingUnlock, StringComparison.Ordinal));
		True(committed.PendingAvatarUnlockQueue.Contains(firstAvatarId, StringComparison.Ordinal));
		Equal("hearts_thick_blood", committed.UnlockedSkillCodexUpgradeIdsCsv);
		Equal(original.PlaytimeSeconds, committed.PlaytimeSeconds);
		Equal(2, ReadStoredRunSnapshot(saveManager, slotId).MainStageIndex);
		Equal(2, store.GetReadModel().MainStageIndex);

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureVictoryCommitResult> duplicate =
			store.PrepareCommitMainAdventureVictory(firstAvatarId);
		False(duplicate.IsPrepared);
		Equal(AdventureVictoryCommitResult.AlreadyCommitted, duplicate.InitialResult);
	}

	private static void PreparedFinalVictoryClearsRunAndPreservesProfileFields()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		int finalStage = AdventureCampaignCatalog.MainStageCount;
		GameSaveStore.GameProgressData original = CreateProgressData(playerName: "Final Runner") with
		{
			HighestCompletedMainAdventureStage = finalStage - 1,
			PendingAvatarUnlockQueue = AdventureCampaignCatalog.GetMainAvatarId(2),
			UnlockedSkillCodexUpgradeIdsCsv = "clubs_borrow_suit_set",
			PlaytimeSeconds = 456.25,
		};
		SeedProfile(saveManager, slotId, original);
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		string finalAvatarId = AdventureCampaignCatalog.GetMainAvatarId(finalStage);
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> begin =
			store.PrepareBeginNewMainRun(finalStage, finalAvatarId);
		Equal(
			AdventureRunWriteResult.Succeeded,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureVictoryCommitResult> victory =
			store.PrepareCommitMainAdventureVictory(finalAvatarId);
		Equal(
			AdventureVictoryCommitResult.Succeeded,
			victory.PersistAttemptAsync().GetAwaiter().GetResult());
		GameSaveStore.GameProgressData committed = LoadStoredProfile(saveManager, slotId);
		Equal(finalStage, committed.HighestCompletedMainAdventureStage);
		Equal(string.Empty, committed.PendingAdventureRunJson);
		Equal("clubs_borrow_suit_set", committed.UnlockedSkillCodexUpgradeIdsCsv);
		Equal(original.PlaytimeSeconds, committed.PlaytimeSeconds);
		False(store.HasActiveRun);
		Equal(1, saveManager.LoadFromSlotCallCount);
	}

	private static void PreparedVictoryRejectsActiveProfileSwitch()
	{
		ResetGameSaveStoreTestState();
		string firstSlotId = GameSaveStore.GetProfileSlotId(1);
		string secondSlotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(firstSlotId);
		SeedProfile(saveManager, firstSlotId, CreateProgressData(playerName: "First"));
		SeedProfile(saveManager, secondSlotId, CreateProgressData(playerName: "Second"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		string firstAvatarId = AdventureCampaignCatalog.GetMainAvatarId(1);
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> begin =
			store.PrepareBeginNewMainRun(1, firstAvatarId);
		Equal(
			AdventureRunWriteResult.Succeeded,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureVictoryCommitResult> victory =
			store.PrepareCommitMainAdventureVictory(firstAvatarId);
		int savesBeforeSwitch = saveManager.SaveToSlotCallCount;
		saveManager.SeedValue(GameSaveStore.ActiveProfileSlotKey, secondSlotId);

		Equal(
			AdventureVictoryCommitResult.InvalidStage,
			victory.PersistAttemptAsync().GetAwaiter().GetResult());
		Equal(savesBeforeSwitch, saveManager.SaveToSlotCallCount);
		Equal(0, LoadStoredProfile(saveManager, secondSlotId).HighestCompletedMainAdventureStage);
		Equal(1, store.GetReadModel().MainStageIndex);
	}

	private static void PreparedAdventureOperationsRejectActiveProfileSwitches()
	{
		ResetGameSaveStoreTestState();
		string firstSlotId = GameSaveStore.GetProfileSlotId(1);
		string secondSlotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(firstSlotId);
		GameSaveStore.GameProgressData firstBefore = CreateProgressData(playerName: "First");
		GameSaveStore.GameProgressData secondBefore = CreateProgressData(playerName: "Second");
		SeedProfile(saveManager, firstSlotId, firstBefore);
		SeedProfile(saveManager, secondSlotId, secondBefore);
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		string firstAvatarId = AdventureCampaignCatalog.GetMainAvatarId(1);

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> conflictedBegin =
			store.PrepareBeginNewMainRun(1, firstAvatarId);
		Equal(firstSlotId, conflictedBegin.ExpectedProfileSlotId);
		Equal(Godot.Error.Ok, GameSaveStore.SetActiveProfile(secondSlotId));
		Equal(
			AdventureRunWriteResult.InvalidStage,
			conflictedBegin.PersistAttemptAsync().GetAwaiter().GetResult());
		False(store.HasActiveRun);
		Equal(firstBefore, LoadStoredProfile(saveManager, firstSlotId));
		Equal(secondBefore, LoadStoredProfile(saveManager, secondSlotId));

		Equal(Godot.Error.Ok, GameSaveStore.SetActiveProfile(firstSlotId));
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> begin =
			store.PrepareBeginNewMainRun(1, firstAvatarId);
		Equal(
			AdventureRunWriteResult.Succeeded,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());
		GameSaveStore.GameProgressData firstWithRun = LoadStoredProfile(saveManager, firstSlotId);
		AdventureRunReadModel authorityBeforeConflict = store.GetReadModel();

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> health =
			store.PrepareSetHealth(55, 100);
		Equal(firstSlotId, health.ExpectedProfileSlotId);
		Equal(Godot.Error.Ok, GameSaveStore.SetActiveProfile(secondSlotId));
		Equal(
			AdventureRunWriteResult.InvalidStage,
			health.PersistAttemptAsync().GetAwaiter().GetResult());
		Equal(authorityBeforeConflict, store.GetReadModel());
		Equal(firstWithRun, LoadStoredProfile(saveManager, firstSlotId));
		Equal(secondBefore, LoadStoredProfile(saveManager, secondSlotId));

		Equal(Godot.Error.Ok, GameSaveStore.SetActiveProfile(firstSlotId));
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureUpgradeGrantCommitResult> grant =
			store.PrepareGrantUpgrade(
				"hearts_thick_blood",
				AdventureUpgradeTriggerSource.None);
		Equal(firstSlotId, grant.ExpectedProfileSlotId);
		Equal(Godot.Error.Ok, GameSaveStore.SetActiveProfile(secondSlotId));
		Equal(
			AdventureRunWriteResult.InvalidStage,
			grant.PersistAttemptAsync().GetAwaiter().GetResult().WriteResult);
		Equal(authorityBeforeConflict, store.GetReadModel());
		Equal(firstWithRun, LoadStoredProfile(saveManager, firstSlotId));
		Equal(secondBefore, LoadStoredProfile(saveManager, secondSlotId));
		True(Godot.GD.WarningMessages.Exists(message => message.Contains(
			"profile slot changed",
			StringComparison.Ordinal)));
	}

	private static void PreparedAdventureObserverKeepsCommittedLeaseSlot()
	{
		ResetGameSaveStoreTestState();
		string firstSlotId = GameSaveStore.GetProfileSlotId(1);
		string secondSlotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(firstSlotId);
		SeedProfile(saveManager, firstSlotId, CreateProgressData(playerName: "First"));
		SeedProfile(saveManager, secondSlotId, CreateProgressData(playerName: "Second"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> begin =
			store.PrepareBeginNewMainRun(
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1));
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);
		Equal(
			AdventureRunWriteResult.SaveFailed,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());
		Equal(Godot.Error.Busy, GameSaveStore.SetActiveProfile(secondSlotId));
		saveManager.SeedValue(GameSaveStore.ActiveProfileSlotKey, secondSlotId);
		Equal(
			AdventureRunWriteResult.Succeeded,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());

		Equal(
			Godot.Error.Ok,
			GameSaveStore.UpdateActiveProfileAsync(profile => profile with
			{
				PlayerName = "Second Updated",
			}).GetAwaiter().GetResult());
		GameSaveStore.GameProgressData first = LoadStoredProfile(saveManager, firstSlotId);
		GameSaveStore.GameProgressData second = LoadStoredProfile(saveManager, secondSlotId);
		False(string.IsNullOrWhiteSpace(first.PendingAdventureRunJson));
		Equal("First", first.PlayerName);
		Equal(string.Empty, second.PendingAdventureRunJson);
		Equal("Second Updated", second.PlayerName);
	}

	private static void ExternalAdventureSnapshotDoesNotReplaceCommittedProfileBaseline()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		string pendingAvatarId = AdventureCampaignCatalog.GetMainAvatarId(2);
		GameSaveStore.GameProgressData committedBefore = CreateProgressData(
			playerName: "Committed Player") with
		{
			HighestCompletedMainAdventureStage = 2,
			PendingAvatarUnlockQueue = string.Empty,
			UnlockedSkillCodexUpgradeIdsCsv = "hearts_thick_blood",
			PlaytimeSeconds = 987.5,
		};
		SeedProfile(saveManager, slotId, committedBefore);
		Equal(Godot.Error.Ok, GameSaveStore.SaveActiveProfile(committedBefore));

		GameSaveStore.GameProgressData staleExternalSnapshot = committedBefore with
		{
			PlayerName = "Stale Player",
			PendingAvatarUnlockQueue = pendingAvatarId,
			UnlockedSkillCodexUpgradeIdsCsv = string.Empty,
			PlaytimeSeconds = 12.0,
		};
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		store.ReloadFromProfileSnapshot(staleExternalSnapshot);

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> begin =
			store.PrepareBeginNewMainRun(
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1));
		Equal(
			AdventureRunWriteResult.Succeeded,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());

		GameSaveStore.GameProgressData candidate = GameSaveStore.GameProgressData.FromDictionary(
			saveManager.SaveAttemptData[^1],
			GameSaveStore.GameProgressData.CreateDefault());
		Equal("Committed Player", candidate.PlayerName);
		Equal(string.Empty, candidate.PendingAvatarUnlockQueue);
		Equal("hearts_thick_blood", candidate.UnlockedSkillCodexUpgradeIdsCsv);
		Equal(987.5, candidate.PlaytimeSeconds);
		False(string.IsNullOrWhiteSpace(candidate.PendingAdventureRunJson));

		GameSaveStore.GameProgressData persisted = LoadStoredProfile(saveManager, slotId);
		Equal("Committed Player", persisted.PlayerName);
		Equal(string.Empty, persisted.PendingAvatarUnlockQueue);
		Equal("hearts_thick_blood", persisted.UnlockedSkillCodexUpgradeIdsCsv);
		Equal(987.5, persisted.PlaytimeSeconds);
		False(string.IsNullOrWhiteSpace(persisted.PendingAdventureRunJson));

		ProfileWriteLease? committedLease = GameSaveProfileWriteCoordinator.AcquireAsync(
			static profile => profile,
			forcePersist: false).GetAwaiter().GetResult();
		True(committedLease != null);
		Equal("Committed Player", committedLease!.BaseProfile.PlayerName);
		Equal(string.Empty, committedLease.BaseProfile.PendingAvatarUnlockQueue);
		Equal("hearts_thick_blood", committedLease.BaseProfile.UnlockedSkillCodexUpgradeIdsCsv);
		Equal(987.5, committedLease.BaseProfile.PlaytimeSeconds);
		False(string.IsNullOrWhiteSpace(committedLease.BaseProfile.PendingAdventureRunJson));
		committedLease.Abort();
	}

	private static void PreparedAdventureActionsReuseCommittedProfileSnapshot()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> begin =
			store.PrepareBeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1));
		Equal(
			AdventureRunWriteResult.Succeeded,
			begin.PersistAttemptAsync().GetAwaiter().GetResult());
		Equal(1, saveManager.LoadFromSlotCallCount);

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> health =
			store.PrepareSetHealth(80, 100);
		Equal(
			AdventureRunWriteResult.Succeeded,
			health.PersistAttemptAsync().GetAwaiter().GetResult());
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureHealingCommitResult> healing =
			store.PrepareApplyHealing(5);
		Equal(
			AdventureRunWriteResult.Succeeded,
			healing.PersistAttemptAsync().GetAwaiter().GetResult().WriteResult);

		Equal(1, saveManager.LoadFromSlotCallCount);
		Equal(85, store.GetReadModel().CurrentHealth);
		Equal(85, ReadStoredRunSnapshot(saveManager, slotId).CurrentHealth);
	}

	private static void AdventureVictorySaveFailurePreservesCurrentRun()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
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
		store.SetManifest(CreateFullAdventureUpgradeManifest());
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
		store.SetManifest(CreateFullAdventureUpgradeManifest());
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
		store.SetManifest(CreateFullAdventureUpgradeManifest());
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
