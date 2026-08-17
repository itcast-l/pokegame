using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Deckland.Rules;

public sealed partial class AdventureRunSessionStore : Node
{
	internal void ConfigurePersistFailuresForDebug(int failureCount)
	{
		_debugPersistFailuresRemaining = Math.Max(0, failureCount);
		_debugPersistAttemptCount = 0;
	}

	internal int GetPersistAttemptCountForDebug()
	{
		return _debugPersistAttemptCount;
	}

	internal bool ConsumeDebugPersistFailure()
	{
		_debugPersistAttemptCount++;
		if (_debugPersistFailuresRemaining <= 0)
		{
			return false;
		}

		_debugPersistFailuresRemaining--;
		GD.PushWarning("Debug-forced adventure run persistence failure.");
		return true;
	}

	internal void ConfigureNextTransactionalWriteResultForDebug(
		AdventureRunWriteResult writeResult)
	{
		_debugNextTransactionalWriteResult = writeResult;
	}

	internal bool HasNextTransactionalWriteResultForDebug()
	{
		return _debugNextTransactionalWriteResult.HasValue;
	}


	private AdventureRunWriteResult ValidateTransactionalWrite(
		int mainStageIndex,
		bool allowRecoveryReplacement = false)
	{
		if (_debugNextTransactionalWriteResult.HasValue)
		{
			AdventureRunWriteResult debugResult =
				_debugNextTransactionalWriteResult.Value;
			_debugNextTransactionalWriteResult = null;
			return debugResult;
		}
		if (!_gameModePolicy.CanWriteAdventureRun)
		{
			return AdventureRunWriteResult.WriteNotAllowed;
		}
		if (!allowRecoveryReplacement && HasRetainedPayload)
		{
			return AdventureRunWriteResult.RecoveryRequired;
		}
		return mainStageIndex >= 1 && mainStageIndex <= AdventureCampaignCatalog.MainStageCount
			? AdventureRunWriteResult.Succeeded
			: AdventureRunWriteResult.InvalidStage;
	}

	private AdventureRunWriteResult ValidateActiveWrite()
	{
		if (!_gameModePolicy.CanWriteAdventureRun)
		{
			return AdventureRunWriteResult.WriteNotAllowed;
		}
		if (HasRetainedPayload)
		{
			return AdventureRunWriteResult.RecoveryRequired;
		}
		return _authoritySession.AdventureReadModel.IsActive
			? AdventureRunWriteResult.Succeeded
			: AdventureRunWriteResult.InvalidStage;
	}

	private static AdventureCaptureCommitResult FailedCaptureCommit(
		AdventureRunWriteResult writeResult)
	{
		return new AdventureCaptureCommitResult(
			writeResult,
			default,
			default,
			default,
			default,
			null,
			default,
			0,
			false,
			false);
	}


	private bool PersistAdventureRequest(AdventurePersistenceRequest request)
	{
		string candidatePayload = SerializeRunSnapshot(request.CandidateSnapshot);
		string previousPayload = SerializeRunSnapshot(request.PreviousSnapshot);
		if (!request.ForcePersist
			&& string.Equals(candidatePayload, previousPayload, StringComparison.Ordinal))
		{
			return true;
		}

		return TryPersistRunPayload(
			candidatePayload,
			string.IsNullOrWhiteSpace(request.UnlockedUpgradeId)
				? null
				: request.UnlockedUpgradeId);
	}

	private bool PersistMainAdventureVictory(
		GameSaveStore.GameProgressData profile,
		AdventurePersistenceRequest request,
		bool alreadyCommitted)
	{
		string nextRunJson = SerializeRunSnapshot(request.CandidateSnapshot);
		GameSaveStore.GameProgressData committedProfile = alreadyCommitted
			? profile with
			{
				PendingAdventureRunJson = nextRunJson,
			}
			: CharacterAvatarCatalog.CreateMainAdventureVictoryProfile(
				profile,
				request.CompletedMainStageIndex,
				request.EnemyAvatarId,
				nextRunJson);
		Error saveResult = GameSaveStore.SaveActiveProfile(committedProfile);
		if (saveResult != Error.Ok)
		{
			string action = alreadyCommitted
				? "align an already committed adventure run"
				: "atomically commit adventure victory";
			GD.PushWarning($"Failed to {action}: {saveResult}");
			return false;
		}

		ObservePayload(nextRunJson);
		return true;
	}

	private static string SerializeRunSnapshot(AdventureUpgradeRunSnapshot? snapshot)
	{
		return snapshot.HasValue
			? JsonSerializer.Serialize(snapshot.Value)
			: string.Empty;
	}

	private bool TryPersistRunPayload(string json, string? unlockedUpgradeId = null)
	{
		_debugPersistAttemptCount++;
		if (_debugPersistFailuresRemaining > 0)
		{
			_debugPersistFailuresRemaining--;
			GD.PushWarning("Debug-forced adventure run persistence failure.");
			return false;
		}

		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		Error saveResult = GameSaveStore.SaveActiveProfile(profile with
		{
			PendingAdventureRunJson = json,
			UnlockedSkillCodexUpgradeIdsCsv = SkillCodexUnlockRules.AddUnlockedUpgradeId(
				profile.UnlockedSkillCodexUpgradeIdsCsv,
				unlockedUpgradeId),
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to persist adventure upgrade run session: {saveResult}");
			return false;
		}

		ObservePayload(json);
		return true;
	}

}
