using System;
using System.Text.Json;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public sealed partial class AdventureRunSessionStore
{
	private PreparedAdventureOperation<T> CreatePreparedOperation<T>(
		AdventureCommand command,
		Func<AdventureCommandOutcome?, T> successResult,
		Func<AdventureRunWriteResult, T> failureResult,
		Func<PreparedAdventureResult, Func<GameSaveStore.GameProgressData, GameSaveStore.GameProgressData>>? profileUpdateFactory = null,
		Func<ProfileWriteLease, bool>? profilePrecondition = null,
		Action<string, GameSaveStore.GameProgressData>? onProfileCommitted = null,
		Action<AdventureRuleCommandResult>? onCommitFailure = null)
	{
		Node? saveManager = GameSaveStore.GetSaveManager();
		if (saveManager == null)
		{
			return PreparedAdventureOperation<T>.Rejected(
				AdventureRunWriteResult.SaveFailed,
				failureResult);
		}

		GameSaveMigration.EnsureInitialized(saveManager);
		string expectedProfileSlotId = GameSaveProfileRepository.NormalizeProfileSlotId(
			GameSaveProfileRepository.GetActiveProfileSlotIdRaw(saveManager));
		PreparedAdventureResult prepared = _authoritySession.PrepareAdventure(command);
		return PreparedAdventureOperation<T>.Create(
			prepared,
			this,
			_authoritySession,
			successResult,
			failureResult,
			profileUpdateFactory?.Invoke(prepared),
			profilePrecondition,
			onProfileCommitted,
			onCommitFailure,
			expectedProfileSlotId);
	}

	private static PreparedAdventureOperation<T> CreateRejectedOperation<T>(
		AdventureRunWriteResult result,
		Func<AdventureRunWriteResult, T> failureResult)
	{
		return PreparedAdventureOperation<T>.Rejected(result, failureResult);
	}

	internal PreparedAdventureOperation<AdventureRunWriteResult> PrepareBeginNewMainRun(
		int mainStageIndex,
		string enemyAvatarId)
	{
		AdventureRunWriteResult validation = ValidateTransactionalWrite(mainStageIndex);
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(validation, static result => result);
		}

		return CreatePreparedOperation(
			new BeginAdventureRunCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				mainStageIndex,
				enemyAvatarId),
			_	=> AdventureRunWriteResult.Succeeded,
			static result => result,
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureRunWriteResult> PrepareAdvanceToMainStage(
		int mainStageIndex,
		string enemyAvatarId)
	{
		AdventureRunWriteResult validation = ValidateTransactionalWrite(mainStageIndex);
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(validation, static result => result);
		}

		return CreatePreparedOperation(
			new AdvanceAdventureStageCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				mainStageIndex,
				enemyAvatarId),
			_	=> AdventureRunWriteResult.Succeeded,
			static result => result,
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureDefeatCommitResult> PrepareResolveMainAdventureDefeat()
	{
		if (!_gameModePolicy.IsAdventureMain
			|| !_gameModePolicy.CanWriteAdventureRun
			|| HasRetainedPayload
			|| !_authoritySession.AdventureReadModel.IsActive)
		{
			AdventureRunWriteResult result = !_gameModePolicy.IsAdventureMain
				? AdventureRunWriteResult.WriteNotAllowed
				: ValidateActiveWrite();
			return CreateRejectedOperation(
				result,
				static writeResult => new AdventureDefeatCommitResult(writeResult, null));
		}

		return CreatePreparedOperation(
			new ResolveAdventureDefeatCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision),
			outcome => outcome is AdventureDefeatCommandOutcome defeat
				? new AdventureDefeatCommitResult(AdventureRunWriteResult.Succeeded, defeat.Resolution)
				: new AdventureDefeatCommitResult(AdventureRunWriteResult.Succeeded, null),
			static result => new AdventureDefeatCommitResult(result, null),
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureUpgradeGrantCommitResult> PrepareGrantUpgrade(
		string upgradeId,
		AdventureUpgradeTriggerSource triggerSource,
		string? replacedUpgradeId = null)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(
				validation,
				static result => new AdventureUpgradeGrantCommitResult(
					result,
					AdventureUpgradeAcquireResult.DeferredManifestUnavailable,
					default,
					null,
					null));
		}

		return CreatePreparedOperation(
			new GrantAdventureUpgradeCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				upgradeId,
				triggerSource,
				replacedUpgradeId),
			outcome => outcome is AdventureUpgradeGrantCommandOutcome grant
				? new AdventureUpgradeGrantCommitResult(
					AdventureRunWriteResult.Succeeded,
					grant.AcquireResult,
					grant.EffectResult,
					grant.HealingResult,
					grant.ReplacedUpgradeId)
				: new AdventureUpgradeGrantCommitResult(
					AdventureRunWriteResult.Succeeded,
					AdventureUpgradeAcquireResult.None,
					default,
					null,
					null),
			static result => new AdventureUpgradeGrantCommitResult(
				result,
				result == AdventureRunWriteResult.SaveFailed
					? AdventureUpgradeAcquireResult.None
					: AdventureUpgradeAcquireResult.DeferredManifestUnavailable,
				default,
				null,
				null),
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureRunWriteResult> PrepareSetHealth(
		int currentHealth,
		int maxHealth)
	{
		return PrepareSimpleActiveOperation(
			new SetAdventureHealthCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				currentHealth,
				maxHealth));
	}

	internal PreparedAdventureOperation<AdventureRunWriteResult> PrepareReplaceEffectState(
		AdventureEffectStateSnapshot effectState)
	{
		return PrepareSimpleActiveOperation(
			new ReplaceAdventureEffectStateCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				effectState));
	}

	internal PreparedAdventureOperation<AdventureRunWriteResult> PrepareUpdateVitalsAndEffects(
		int currentHealth,
		int maxHealth,
		AdventureEffectStateSnapshot effectState)
	{
		return PrepareSimpleActiveOperation(
			new UpdateAdventureVitalsAndEffectsCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				currentHealth,
				maxHealth,
				effectState));
	}

	internal PreparedAdventureOperation<AdventureHealingCommitResult> PrepareApplyHealing(
		int healAmount,
		int maxHealthDelta = 0)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(
				validation,
				static result => new AdventureHealingCommitResult(result, default));
		}

		return CreatePreparedOperation(
			new ApplyAdventureHealingCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				healAmount,
				maxHealthDelta),
			outcome => outcome is AdventureHealingCommandOutcome healing
				? new AdventureHealingCommitResult(
					AdventureRunWriteResult.Succeeded,
					healing.HealingResult)
				: new AdventureHealingCommitResult(AdventureRunWriteResult.Succeeded, default),
			static result => new AdventureHealingCommitResult(result, default),
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureHealingCommitResult> PrepareUpdateHealthAndApplyHealing(
		int currentHealth,
		int healAmount)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(
				validation,
				static result => new AdventureHealingCommitResult(result, default));
		}

		return CreatePreparedOperation(
			new UpdateAdventureHealthAndHealCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				currentHealth,
				healAmount),
			outcome => outcome is AdventureHealingCommandOutcome healing
				? new AdventureHealingCommitResult(AdventureRunWriteResult.Succeeded, healing.HealingResult)
				: new AdventureHealingCommitResult(AdventureRunWriteResult.Succeeded, default),
			static result => new AdventureHealingCommitResult(result, default),
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureQuestionRewardCommitResult> PrepareResolveQuestionReward()
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(
				validation,
				static result => new AdventureQuestionRewardCommitResult(result, default, null));
		}

		return CreatePreparedOperation(
			new ResolveAdventureQuestionRewardCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision),
			outcome => outcome is AdventureQuestionRewardCommandOutcome reward
				? new AdventureQuestionRewardCommitResult(
					AdventureRunWriteResult.Succeeded,
					reward.EffectResult,
					reward.HealingResult)
				: new AdventureQuestionRewardCommitResult(AdventureRunWriteResult.Succeeded, default, null),
			static result => new AdventureQuestionRewardCommitResult(result, default, null),
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureTurnStartCommitResult> PrepareResolveTurnStartEffects()
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(
				validation,
				static result => new AdventureTurnStartCommitResult(result, default, null));
		}

		return CreatePreparedOperation(
			new ResolveAdventureTurnStartCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision),
			outcome => outcome is AdventureTurnStartCommandOutcome turnStart
				? new AdventureTurnStartCommitResult(
					AdventureRunWriteResult.Succeeded,
					turnStart.EffectResult,
					turnStart.HealingResult)
				: new AdventureTurnStartCommitResult(AdventureRunWriteResult.Succeeded, default, null),
			static result => new AdventureTurnStartCommitResult(result, default, null),
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureDirectDamageCommitResult> PrepareApplyDirectDamage(
		AdventureDirectDamageCommitInput input)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(
				validation,
				static result => new AdventureDirectDamageCommitResult(result, 0, 0, default, default));
		}

		return CreatePreparedOperation(
			new ApplyAdventureDirectDamageCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				input),
			outcome => outcome is AdventureDirectDamageCommandOutcome damage
				? new AdventureDirectDamageCommitResult(
					AdventureRunWriteResult.Succeeded,
					damage.PreviousHealth,
					damage.DamagedHealth,
					damage.RescueResult,
					damage.HealingResult)
				: new AdventureDirectDamageCommitResult(AdventureRunWriteResult.Succeeded, 0, 0, default, default),
			static result => new AdventureDirectDamageCommitResult(result, 0, 0, default, default),
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureMissCommitResult> PrepareResolveMissAction(
		AdventureMissCommitInput input)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(
				validation,
				static result => new AdventureMissCommitResult(result, default, default, null, false));
		}

		return CreatePreparedOperation(
			new ResolveAdventureMissCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				input),
			outcome => outcome is AdventureMissCommandOutcome miss
				? new AdventureMissCommitResult(
					AdventureRunWriteResult.Succeeded,
					miss.EffectResult,
					miss.RecoveryEffectResult,
					miss.HealingResult,
					miss.CommitBandageAtRealTurnEnd)
				: new AdventureMissCommitResult(AdventureRunWriteResult.Succeeded, default, default, null, false),
			static result => new AdventureMissCommitResult(result, default, default, null, false),
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureCaptureCommitResult> PrepareResolveCaptureAction(
		AdventureCaptureCommitInput input)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded || input.CaptureContext == null)
		{
			return CreateRejectedOperation(
				validation == AdventureRunWriteResult.Succeeded
					? AdventureRunWriteResult.InvalidStage
					: validation,
				FailedCaptureCommit);
		}

		return CreatePreparedOperation(
			new ResolveAdventureCaptureCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				input),
			outcome => outcome is AdventureCaptureCommandOutcome capture
				? new AdventureCaptureCommitResult(
					AdventureRunWriteResult.Succeeded,
					capture.DamageResult,
					capture.PostCaptureEffectResult,
					capture.ClubCaptureEffectResult,
					capture.QuestionRewardEffectResult,
					capture.HealingResult,
					capture.RescueResult,
					capture.CurrentHealth,
					capture.ConsumedHalfHealthRescue,
					capture.ConsumedQuarterHealthRescue)
				: FailedCaptureCommit(AdventureRunWriteResult.Succeeded),
			FailedCaptureCommit,
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	internal PreparedAdventureOperation<AdventureRunWriteResult> PrepareResolveRealTurnEnd(
		bool commitMissBandage)
	{
		return PrepareSimpleActiveOperation(
			new ResolveAdventureRealTurnEndCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				commitMissBandage));
	}

	internal PreparedAdventureOperation<AdventureRunWriteResult> PrepareResetBattleScopedEffects()
	{
		return PrepareSimpleActiveOperation(
			new ResetAdventureBattleEffectsCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision));
	}

	internal PreparedAdventureOperation<AdventureRunWriteResult> PrepareClearRun()
	{
		if (!_gameModePolicy.CanWriteAdventureRun)
		{
			return CreateRejectedOperation(
				AdventureRunWriteResult.WriteNotAllowed,
				static result => result);
		}

		return CreatePreparedOperation(
			new ClearAdventureRunCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision),
			_ => AdventureRunWriteResult.Succeeded,
			static result => result,
			BuildRunProfileUpdateFactory,
			onProfileCommitted: (slotId, profile) =>
			{
				_deferredPayload = null;
				_preservedFailurePayload = null;
				LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
				ObserveCommittedAdventureProfile(slotId, profile);
			});
	}

	internal PreparedAdventureOperation<AdventureRunWriteResult> PrepareRestartMainRun(
		int mainStageIndex,
		string enemyAvatarId)
	{
		AdventureRunWriteResult validation = ValidateTransactionalWrite(
			mainStageIndex,
			allowRecoveryReplacement: true);
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(validation, static result => result);
		}

		return CreatePreparedOperation(
			new RestartAdventureRunCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				mainStageIndex,
				enemyAvatarId),
			_ => AdventureRunWriteResult.Succeeded,
			static result => result,
			BuildRunProfileUpdateFactory,
			onProfileCommitted: (slotId, profile) =>
			{
				_deferredPayload = null;
				_preservedFailurePayload = null;
				LastRestoreResult = AdventureUpgradeRestoreResult.Restored;
				ObserveCommittedAdventureProfile(slotId, profile);
			});
	}

	internal PreparedAdventureOperation<AdventureVictoryCommitResult> PrepareCommitMainAdventureVictory(
		string enemyAvatarId)
	{
		string normalizedEnemyAvatarId = enemyAvatarId?.Trim() ?? string.Empty;
		if (!AdventureCampaignCatalog.TryGetMainStageIndex(
			normalizedEnemyAvatarId,
			out int completedStageIndex))
		{
			return CreateRejectedVictory(AdventureVictoryCommitResult.InvalidStage);
		}
		if (!TryGetActiveProfileSnapshot(out GameSaveStore.GameProgressData profile))
		{
			return CreateRejectedVictory(AdventureVictoryCommitResult.SaveFailed);
		}

		int previousHighestStage = Math.Clamp(
			profile.HighestCompletedMainAdventureStage,
			0,
				AdventureCampaignCatalog.MainStageCount);
		string expectedRunPayload = profile.PendingAdventureRunJson;
		AdventureRunReadModel readModel = _authoritySession.AdventureReadModel;
		bool alreadyCommitted = completedStageIndex <= previousHighestStage;
		if (!alreadyCommitted
			&& (!_gameModePolicy.IsAdventureMain
				|| !_gameModePolicy.CanWriteAdventureRun
				|| RequiresRunRecovery
				|| !readModel.IsActive
				|| readModel.MainStageIndex != completedStageIndex
				|| !string.Equals(readModel.EnemyAvatarId, normalizedEnemyAvatarId, StringComparison.Ordinal)))
		{
			return CreateRejectedVictory(AdventureVictoryCommitResult.InvalidStage);
		}
		if (!alreadyCommitted && completedStageIndex != previousHighestStage + 1)
		{
			return CreateRejectedVictory(AdventureVictoryCommitResult.OutOfSequence);
		}
		if (alreadyCommitted
			&& (!readModel.IsActive
				|| readModel.MainStageIndex != completedStageIndex
				|| !string.Equals(readModel.EnemyAvatarId, normalizedEnemyAvatarId, StringComparison.Ordinal)))
		{
			return CreateRejectedVictory(AdventureVictoryCommitResult.AlreadyCommitted);
		}

		return CreatePreparedOperation(
			new CommitAdventureVictoryCommand(
				NextAdventureActionId(),
				readModel.Revision,
				completedStageIndex,
				normalizedEnemyAvatarId),
			_ => alreadyCommitted
				? AdventureVictoryCommitResult.AlreadyCommitted
				: AdventureVictoryCommitResult.Succeeded,
			result => result switch
			{
				AdventureRunWriteResult.SaveFailed => AdventureVictoryCommitResult.SaveFailed,
				AdventureRunWriteResult.InvalidStage => AdventureVictoryCommitResult.InvalidStage,
				_ => AdventureVictoryCommitResult.InvalidStage,
			},
			BuildVictoryProfileUpdateFactory(completedStageIndex, normalizedEnemyAvatarId, alreadyCommitted),
				profilePrecondition: lease =>
					lease.BaseProfile.HighestCompletedMainAdventureStage == previousHighestStage
					&& string.Equals(
						lease.BaseProfile.PendingAdventureRunJson,
						expectedRunPayload,
						StringComparison.Ordinal),
			onProfileCommitted: (slotId, committedProfile) =>
			{
				_deferredPayload = completedStageIndex >= AdventureCampaignCatalog.MainStageCount
					? null
					: _deferredPayload;
				_preservedFailurePayload = null;
				LastRestoreResult = completedStageIndex >= AdventureCampaignCatalog.MainStageCount
					? AdventureUpgradeRestoreResult.NoSnapshot
					: AdventureUpgradeRestoreResult.Restored;
				ObserveCommittedAdventureProfile(slotId, committedProfile);
			});
	}

	private PreparedAdventureOperation<AdventureVictoryCommitResult> CreateRejectedVictory(
		AdventureVictoryCommitResult result)
	{
		return PreparedAdventureOperation<AdventureVictoryCommitResult>.Rejected(
			result == AdventureVictoryCommitResult.SaveFailed
				? AdventureRunWriteResult.SaveFailed
				: AdventureRunWriteResult.InvalidStage,
			_ => result);
	}

	private Func<PreparedAdventureResult, Func<GameSaveStore.GameProgressData, GameSaveStore.GameProgressData>> BuildVictoryProfileUpdateFactory(
		int completedStageIndex,
		string enemyAvatarId,
		bool alreadyCommitted)
	{
		return prepared =>
		{
			string nextRunJson = SerializeRunSnapshot(
				prepared.PersistenceRequest?.CandidateSnapshot);
			return profile => alreadyCommitted
				? profile with { PendingAdventureRunJson = nextRunJson }
				: CharacterAvatarCatalog.CreateMainAdventureVictoryProfile(
					profile,
					completedStageIndex,
					enemyAvatarId,
					nextRunJson);
		};
	}

	private PreparedAdventureOperation<AdventureRunWriteResult> PrepareSimpleActiveOperation(
		AdventureCommand command)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return CreateRejectedOperation(validation, static result => result);
		}

		return CreatePreparedOperation(
			command,
			_	=> AdventureRunWriteResult.Succeeded,
			static result => result,
			BuildRunProfileUpdateFactory,
			onProfileCommitted: ObserveCommittedAdventureProfile);
	}

	private Func<GameSaveStore.GameProgressData, GameSaveStore.GameProgressData> BuildRunProfileUpdateFactory(
		PreparedAdventureResult prepared)
	{
		string candidatePayload = SerializeRunSnapshot(prepared.PersistenceRequest?.CandidateSnapshot);
		string? unlockedUpgradeId = prepared.PersistenceRequest?.UnlockedUpgradeId;
		return profile => profile with
		{
			PendingAdventureRunJson = candidatePayload,
			UnlockedSkillCodexUpgradeIdsCsv = SkillCodexUnlockRules.AddUnlockedUpgradeId(
				profile.UnlockedSkillCodexUpgradeIdsCsv,
				unlockedUpgradeId),
		};
	}

	private void ObserveCommittedAdventureProfile(
		string slotId,
		GameSaveStore.GameProgressData profile)
	{
		_profileSnapshot = profile;
		_profileSnapshotSlotId = GameSaveProfileRepository.NormalizeProfileSlotId(slotId);
		GameSaveProfileWriteCoordinator.ObserveCommitted(_profileSnapshotSlotId, profile);
		ObservePayload(profile.PendingAdventureRunJson);
	}

	internal sealed class PreparedAdventureOperation<T>
	{
		private readonly PreparedAdventureResult _prepared;
		private readonly Func<AdventureCommandOutcome?, T> _successResult;
		private readonly Func<AdventureRunWriteResult, T> _failureResult;
		private readonly Func<GameSaveStore.GameProgressData, GameSaveStore.GameProgressData>? _profileUpdate;
		private readonly Func<ProfileWriteLease, bool>? _profilePrecondition;
		private readonly Action<string, GameSaveStore.GameProgressData>? _onProfileCommitted;
		private readonly Action<AdventureRuleCommandResult>? _onCommitFailure;
		private readonly AdventureRunSessionStore? _owner;
		private readonly AuthorityRuleSession? _authorityProvider;
		private readonly string _candidatePayload;
		private readonly string _previousPayload;
		private readonly string _expectedProfileSlotId;
		private ProfileWriteLease? _profileLease;
		private bool _settled;
		private T? _settledResult;

		private PreparedAdventureOperation(
			PreparedAdventureResult prepared,
			AdventureRunSessionStore? owner,
			AuthorityRuleSession? authority,
			Func<AdventureCommandOutcome?, T> successResult,
			Func<AdventureRunWriteResult, T> failureResult,
			Func<GameSaveStore.GameProgressData, GameSaveStore.GameProgressData>? profileUpdate,
			Func<ProfileWriteLease, bool>? profilePrecondition,
			Action<string, GameSaveStore.GameProgressData>? onProfileCommitted,
			Action<AdventureRuleCommandResult>? onCommitFailure,
			string expectedProfileSlotId)
		{
			_prepared = prepared;
			_owner = owner;
			_authorityProvider = authority;
			_successResult = successResult;
			_failureResult = failureResult;
			_profileUpdate = profileUpdate;
			_profilePrecondition = profilePrecondition;
			_onProfileCommitted = onProfileCommitted;
			_onCommitFailure = onCommitFailure;
			_candidatePayload = SerializeRunSnapshot(prepared.PersistenceRequest?.CandidateSnapshot);
			_previousPayload = SerializeRunSnapshot(prepared.PersistenceRequest?.PreviousSnapshot);
			_expectedProfileSlotId = expectedProfileSlotId;
		}

		internal bool IsPrepared => _prepared.Accepted;
		internal T InitialResult => _failureResult(_prepared.WriteResult);
		internal T PersistenceFailureResult => _failureResult(AdventureRunWriteResult.SaveFailed);
		internal string ExpectedProfileSlotId => _expectedProfileSlotId;

		internal static PreparedAdventureOperation<T> Rejected(
			AdventureRunWriteResult result,
			Func<AdventureRunWriteResult, T> failureResult)
		{
			var rejected = new PreparedAdventureResult(
				false,
				new RuleRejection(RuleRejectionCode.InvalidConfiguration, result.ToString()),
				result,
				0,
				AdventureRunReadModel.Empty,
				AdventureRunReadModel.Empty,
				null,
				null,
				false);
			return new PreparedAdventureOperation<T>(
				rejected,
				null,
				null,
				_ => failureResult(result),
				failureResult,
				null,
				null,
				null,
				null,
				string.Empty);
		}

		internal async Task<T> PersistAttemptAsync()
		{
			if (_settled && _settledResult is T settled)
			{
				return settled;
			}
			if (!_prepared.Accepted)
			{
				_settled = true;
				_settledResult = InitialResult;
				return InitialResult;
			}

			bool requiresPersist = _prepared.StateChanged
				&& (_prepared.PersistenceRequest?.ForcePersist == true
					|| !string.Equals(_candidatePayload, _previousPayload, StringComparison.Ordinal));
			if (!requiresPersist)
			{
				return CommitPrepared();
			}

			if (_profileLease == null)
			{
				if (_profileUpdate == null)
				{
					AbortPreparedAuthority();
					_settled = true;
					_settledResult = _failureResult(AdventureRunWriteResult.SaveFailed);
					return _settledResult;
				}
				_profileLease = await GameSaveProfileWriteCoordinator.AcquireAsync(_profileUpdate);
				if (_profileLease == null)
				{
					AbortPreparedAuthority();
					_settled = true;
					_settledResult = _failureResult(AdventureRunWriteResult.SaveFailed);
					return _settledResult;
				}
				if (!string.Equals(
					_profileLease.SlotId,
					_expectedProfileSlotId,
					StringComparison.Ordinal))
				{
					GD.PushWarning(
						$"Prepared adventure profile slot changed from '{_expectedProfileSlotId}' "
						+ $"to '{_profileLease.SlotId}' before persistence.");
					_profileLease.Abort();
					_profileLease = null;
					AbortPreparedAuthority();
					_settled = true;
					_settledResult = _failureResult(AdventureRunWriteResult.InvalidStage);
					return _settledResult;
				}
				if (_profilePrecondition != null && !_profilePrecondition(_profileLease))
				{
					_profileLease.Abort();
					_profileLease = null;
					AbortPreparedAuthority();
					_settled = true;
					_settledResult = _failureResult(AdventureRunWriteResult.InvalidStage);
					return _settledResult;
				}
			}

			Error saveResult = _owner?.ConsumeDebugPersistFailure() == true
				? Error.Failed
				: await _profileLease.PersistAttemptAsync();
			if (saveResult != Error.Ok)
			{
				return _failureResult(AdventureRunWriteResult.SaveFailed);
			}

			string committedSlotId = _profileLease.SlotId;
			GameSaveStore.GameProgressData committedProfile = _profileLease.CandidateProfile;
			_profileLease.CompleteSuccess();
			_profileLease = null;
			return CommitPrepared(committedSlotId, committedProfile);
		}

		internal void Abort()
		{
			if (_settled)
			{
				return;
			}
			_profileLease?.Abort();
			_profileLease = null;
			if (_prepared.Accepted && _prepared.Handle != null)
			{
				AbortPreparedAuthority();
			}
			_settled = true;
			_settledResult = _failureResult(AdventureRunWriteResult.SaveFailed);
		}

		private void AbortPreparedAuthority()
		{
			if (_prepared.Handle == null || _authorityProvider == null)
			{
				return;
			}

			_ = _authorityProvider.AbortPreparedAdventure(_prepared.Handle);
			CompleteAuthorityUnbindIfSettled();
		}

		private T CommitPrepared(
			string committedSlotId = "",
			GameSaveStore.GameProgressData? committedProfile = null)
		{
			if (_prepared.Handle == null)
			{
				AbortPreparedAuthority();
				_settled = true;
				_settledResult = _failureResult(AdventureRunWriteResult.InvalidStage);
				return _settledResult;
			}
			if (_authorityProvider == null)
			{
				AbortPreparedAuthority();
				_settled = true;
				_settledResult = _failureResult(AdventureRunWriteResult.InvalidStage);
				return _settledResult;
			}
			AdventureRuleCommandResult committed = _authorityProvider.CommitPreparedAdventure(_prepared.Handle);
			if (!committed.Accepted)
			{
				NotifyCommitFailure(committed);
				if (committedProfile != null
					&& TryReadCommittedRunSnapshot(
						committedProfile,
						out AdventureUpgradeRunSnapshot? persistedSnapshot))
				{
					AdventureRuleCommandResult recovered =
						_authorityProvider.RecoverPreparedAdventureFromPersistence(
							_prepared.Handle,
							persistedSnapshot);
					if (recovered.Accepted)
					{
						GD.PushError(
							$"Adventure authority commit failed after persistence ({committed.Rejection.Code}); "
							+ "authority was recovered from the committed profile candidate.");
						_settled = true;
						CompleteAuthorityUnbindIfSettled();
						NotifyProfileCommitted(committedSlotId, committedProfile);
						_settledResult = _successResult(recovered.Outcome);
						return _settledResult;
					}

					GD.PushError(
						$"Adventure authority recovery failed after persistence: {recovered.Rejection.Code}. "
						+ "The committed profile remains authoritative.");
				}
				else if (committedProfile != null)
				{
					GD.PushError(
						"Adventure authority recovery could not parse the committed profile candidate. "
						+ "The committed profile remains authoritative.");
				}

				_settled = true;
				if (committedProfile == null)
				{
					AbortPreparedAuthority();
				}
				else
				{
					NotifyProfileCommitted(committedSlotId, committedProfile);
				}
				_settledResult = _failureResult(committed.WriteResult);
				return _settledResult;
			}
			_settled = true;
			CompleteAuthorityUnbindIfSettled();
			if (committedProfile != null)
			{
				NotifyProfileCommitted(committedSlotId, committedProfile);
			}
			_settledResult = _successResult(committed.Outcome);
			return _settledResult;
		}

		private void NotifyCommitFailure(AdventureRuleCommandResult committed)
		{
			try
			{
				_onCommitFailure?.Invoke(committed);
			}
			catch (Exception error)
			{
				GD.PushError($"Adventure commit failure callback threw: {error}");
			}
		}

		private void NotifyProfileCommitted(
			string committedSlotId,
			GameSaveStore.GameProgressData committedProfile)
		{
			try
			{
				_onProfileCommitted?.Invoke(committedSlotId, committedProfile);
			}
			catch (Exception error)
			{
				GD.PushError($"Adventure committed-profile observer threw: {error}");
			}
		}

		private static bool TryReadCommittedRunSnapshot(
			GameSaveStore.GameProgressData committedProfile,
			out AdventureUpgradeRunSnapshot? snapshot)
		{
			string payload = committedProfile.PendingAdventureRunJson;
			if (string.IsNullOrWhiteSpace(payload))
			{
				snapshot = null;
				return true;
			}

			if (AdventureUpgradeRunSnapshot.TryDeserialize(
				payload,
				out AdventureUpgradeRunSnapshot restored,
				out _,
				out _))
			{
				snapshot = restored;
				return true;
			}

			snapshot = null;
			return false;
		}

		private void CompleteAuthorityUnbindIfSettled()
		{
			if (_authorityProvider != null)
			{
				// The store only detaches when the prepared transaction has been
				// cleared, so this is safe for both commit and abort paths.
				_owner?.CompletePendingAuthorityUnbind(_authorityProvider);
			}
		}

		internal static PreparedAdventureOperation<T> Create(
			PreparedAdventureResult prepared,
			AdventureRunSessionStore owner,
			AuthorityRuleSession authority,
			Func<AdventureCommandOutcome?, T> successResult,
			Func<AdventureRunWriteResult, T> failureResult,
			Func<GameSaveStore.GameProgressData, GameSaveStore.GameProgressData>? profileUpdate,
			Func<ProfileWriteLease, bool>? profilePrecondition,
			Action<string, GameSaveStore.GameProgressData>? onProfileCommitted,
			Action<AdventureRuleCommandResult>? onCommitFailure,
			string expectedProfileSlotId)
		{
			var operation = new PreparedAdventureOperation<T>(
				prepared,
				owner,
				authority,
				successResult,
				failureResult,
				profileUpdate,
				profilePrecondition,
				onProfileCommitted,
				onCommitFailure,
				expectedProfileSlotId);
			return operation;
		}
	}
}
