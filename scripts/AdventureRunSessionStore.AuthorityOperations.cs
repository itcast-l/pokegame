using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Deckland.Rules;

public sealed partial class AdventureRunSessionStore : Node
{
	internal AdventureRunWriteResult BeginNewRun(int avatarStageNumber)
	{
		if (!AdventureCampaignCatalog.TryMapLegacyRunStage(avatarStageNumber, out int mainStageIndex))
		{
			return AdventureRunWriteResult.InvalidStage;
		}

		return BeginNewMainRun(mainStageIndex, AdventureCampaignCatalog.GetMainAvatarId(mainStageIndex));
	}

	internal AdventureRunWriteResult BeginNewMainRun(int mainStageIndex, string enemyAvatarId)
	{
		AdventureRunWriteResult validation = ValidateTransactionalWrite(mainStageIndex);
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new BeginAdventureRunCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				mainStageIndex,
				enemyAvatarId),
			PersistAdventureRequest);
		return result.WriteResult;
	}

	internal AdventureRunWriteResult AdvanceToStage(int avatarStageNumber)
	{
		if (!AdventureCampaignCatalog.TryMapLegacyRunStage(avatarStageNumber, out int mainStageIndex))
		{
			return AdventureRunWriteResult.InvalidStage;
		}

		return AdvanceToMainStage(mainStageIndex, AdventureCampaignCatalog.GetMainAvatarId(mainStageIndex));
	}

	internal AdventureRunWriteResult AdvanceToMainStage(int mainStageIndex, string enemyAvatarId)
	{
		AdventureRunWriteResult validation = ValidateTransactionalWrite(mainStageIndex);
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new AdvanceAdventureStageCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				mainStageIndex,
				enemyAvatarId),
			PersistAdventureRequest);
		return result.WriteResult;
	}

	internal AdventureDefeatCommitResult ResolveMainAdventureDefeat()
	{
		if (!_gameModePolicy.IsAdventureMain
			|| !_gameModePolicy.CanWriteAdventureRun
			|| HasRetainedPayload
			|| !_authoritySession.AdventureReadModel.IsActive)
		{
			return new AdventureDefeatCommitResult(
				!_gameModePolicy.IsAdventureMain
					? AdventureRunWriteResult.WriteNotAllowed
					: ValidateActiveWrite(),
				null);
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new ResolveAdventureDefeatCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision),
			PersistAdventureRequest);
		return result.Accepted && result.Outcome is AdventureDefeatCommandOutcome outcome
			? new AdventureDefeatCommitResult(result.WriteResult, outcome.Resolution)
			: new AdventureDefeatCommitResult(result.WriteResult, null);
	}

	internal AdventureUpgradeGrantCommitResult GrantUpgrade(
		string upgradeId,
		AdventureUpgradeTriggerSource triggerSource,
		string? replacedUpgradeId = null)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureUpgradeGrantCommitResult(
				validation,
				AdventureUpgradeAcquireResult.DeferredManifestUnavailable,
				default,
				null,
				null);
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new GrantAdventureUpgradeCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				upgradeId,
				triggerSource,
				replacedUpgradeId),
			PersistAdventureRequest);
		if (result.Accepted && result.Outcome is AdventureUpgradeGrantCommandOutcome outcome)
		{
			return new AdventureUpgradeGrantCommitResult(
				result.WriteResult,
				outcome.AcquireResult,
				outcome.EffectResult,
				outcome.HealingResult,
				outcome.ReplacedUpgradeId);
		}

		return new AdventureUpgradeGrantCommitResult(
			result.WriteResult,
			result.WriteResult == AdventureRunWriteResult.SaveFailed
				? AdventureUpgradeAcquireResult.None
				: AdventureUpgradeAcquireResult.DeferredManifestUnavailable,
			default,
			null,
			null);
	}

	internal AdventureRunWriteResult SetHealth(int currentHealth, int maxHealth)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		return _authoritySession.SubmitAdventure(
			new SetAdventureHealthCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				currentHealth,
				maxHealth),
			PersistAdventureRequest).WriteResult;
	}

	internal AdventureRunWriteResult ReplaceEffectState(AdventureEffectStateSnapshot effectState)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		return _authoritySession.SubmitAdventure(
			new ReplaceAdventureEffectStateCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				effectState),
			PersistAdventureRequest).WriteResult;
	}

	internal AdventureRunWriteResult UpdateVitalsAndEffects(
		int currentHealth,
		int maxHealth,
		AdventureEffectStateSnapshot effectState)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		return _authoritySession.SubmitAdventure(
			new UpdateAdventureVitalsAndEffectsCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				currentHealth,
				maxHealth,
				effectState),
			PersistAdventureRequest).WriteResult;
	}

	internal AdventureHealingCommitResult ApplyHealing(int healAmount, int maxHealthDelta = 0)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureHealingCommitResult(validation, default);
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new ApplyAdventureHealingCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				healAmount,
				maxHealthDelta),
			PersistAdventureRequest);
		return result.Accepted && result.Outcome is AdventureHealingCommandOutcome outcome
			? new AdventureHealingCommitResult(result.WriteResult, outcome.HealingResult)
			: new AdventureHealingCommitResult(result.WriteResult, default);
	}

	internal AdventureHealingCommitResult UpdateHealthAndApplyHealing(
		int currentHealth,
		int healAmount)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureHealingCommitResult(validation, default);
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new UpdateAdventureHealthAndHealCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				currentHealth,
				healAmount),
			PersistAdventureRequest);
		return result.Accepted && result.Outcome is AdventureHealingCommandOutcome outcome
			? new AdventureHealingCommitResult(result.WriteResult, outcome.HealingResult)
			: new AdventureHealingCommitResult(result.WriteResult, default);
	}

	internal AdventureQuestionRewardCommitResult ResolveQuestionReward()
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureQuestionRewardCommitResult(validation, default, null);
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new ResolveAdventureQuestionRewardCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision),
			PersistAdventureRequest);
		return result.Accepted && result.Outcome is AdventureQuestionRewardCommandOutcome outcome
			? new AdventureQuestionRewardCommitResult(
				result.WriteResult,
				outcome.EffectResult,
				outcome.HealingResult)
			: new AdventureQuestionRewardCommitResult(result.WriteResult, default, null);
	}

	internal AdventureTurnStartCommitResult ResolveTurnStartEffects()
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureTurnStartCommitResult(validation, default, null);
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new ResolveAdventureTurnStartCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision),
			PersistAdventureRequest);
		return result.Accepted && result.Outcome is AdventureTurnStartCommandOutcome outcome
			? new AdventureTurnStartCommitResult(
				result.WriteResult,
				outcome.EffectResult,
				outcome.HealingResult)
			: new AdventureTurnStartCommitResult(result.WriteResult, default, null);
	}

	internal AdventureDirectDamageCommitResult ApplyDirectDamage(
		AdventureDirectDamageCommitInput input)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureDirectDamageCommitResult(validation, 0, 0, default, default);
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new ApplyAdventureDirectDamageCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				input),
			PersistAdventureRequest);
		return result.Accepted && result.Outcome is AdventureDirectDamageCommandOutcome outcome
			? new AdventureDirectDamageCommitResult(
				result.WriteResult,
				outcome.PreviousHealth,
				outcome.DamagedHealth,
				outcome.RescueResult,
				outcome.HealingResult)
			: new AdventureDirectDamageCommitResult(result.WriteResult, 0, 0, default, default);
	}

	internal AdventureMissCommitResult ResolveMissAction(AdventureMissCommitInput input)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureMissCommitResult(validation, default, default, null, false);
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new ResolveAdventureMissCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				input),
			PersistAdventureRequest);
		return result.Accepted && result.Outcome is AdventureMissCommandOutcome outcome
			? new AdventureMissCommitResult(
				result.WriteResult,
				outcome.EffectResult,
				outcome.RecoveryEffectResult,
				outcome.HealingResult,
				outcome.CommitBandageAtRealTurnEnd)
			: new AdventureMissCommitResult(result.WriteResult, default, default, null, false);
	}

	internal AdventureCaptureCommitResult ResolveCaptureAction(AdventureCaptureCommitInput input)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return FailedCaptureCommit(validation);
		}
		ArgumentNullException.ThrowIfNull(input.CaptureContext);

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new ResolveAdventureCaptureCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				input),
			PersistAdventureRequest);
		return result.Accepted && result.Outcome is AdventureCaptureCommandOutcome outcome
			? new AdventureCaptureCommitResult(
				result.WriteResult,
				outcome.DamageResult,
				outcome.PostCaptureEffectResult,
				outcome.ClubCaptureEffectResult,
				outcome.QuestionRewardEffectResult,
				outcome.HealingResult,
				outcome.RescueResult,
				outcome.CurrentHealth,
				outcome.ConsumedHalfHealthRescue,
				outcome.ConsumedQuarterHealthRescue)
			: FailedCaptureCommit(result.WriteResult);
	}

	internal AdventureRunWriteResult ResolveRealTurnEnd(bool commitMissBandage)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		return _authoritySession.SubmitAdventure(
			new ResolveAdventureRealTurnEndCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				commitMissBandage),
			PersistAdventureRequest).WriteResult;
	}

	internal AdventureRunWriteResult ResetBattleScopedEffects()
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		return _authoritySession.SubmitAdventure(
			new ResetAdventureBattleEffectsCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision),
			PersistAdventureRequest).WriteResult;
	}

	internal AdventureRunWriteResult ConfigureEffectStateForDebug(AdventureEffectStateSnapshot effectState)
	{
		return ReplaceEffectState(effectState);
	}

	internal AdventureRunWriteResult ClearRun()
	{
		if (!_gameModePolicy.CanWriteAdventureRun)
		{
			return AdventureRunWriteResult.WriteNotAllowed;
		}
		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new ClearAdventureRunCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision),
			PersistAdventureRequest);
		if (!result.Accepted)
		{
			return result.WriteResult;
		}

		_deferredPayload = null;
		_preservedFailurePayload = null;
		LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
		return AdventureRunWriteResult.Succeeded;
	}

	internal AdventureVictoryCommitResult CommitMainAdventureVictory(string enemyAvatarId)
	{
		string normalizedEnemyAvatarId = enemyAvatarId?.Trim() ?? string.Empty;
		if (!AdventureCampaignCatalog.TryGetMainStageIndex(normalizedEnemyAvatarId, out int completedStageIndex))
		{
			return AdventureVictoryCommitResult.InvalidStage;
		}

		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		int previousHighestStage = Math.Clamp(
			profile.HighestCompletedMainAdventureStage,
			0,
			AdventureCampaignCatalog.MainStageCount);
		if (completedStageIndex <= previousHighestStage)
		{
			return AlignAlreadyCommittedRunIfNeeded(profile, completedStageIndex, normalizedEnemyAvatarId);
		}
		if (!_gameModePolicy.IsAdventureMain
			|| !_gameModePolicy.CanWriteAdventureRun
			|| RequiresRunRecovery
			|| !_authoritySession.AdventureReadModel.IsActive
			|| _authoritySession.AdventureReadModel.MainStageIndex != completedStageIndex
			|| !string.Equals(
				_authoritySession.AdventureReadModel.EnemyAvatarId,
				normalizedEnemyAvatarId,
				StringComparison.Ordinal))
		{
			return AdventureVictoryCommitResult.InvalidStage;
		}
		if (completedStageIndex != previousHighestStage + 1)
		{
			return AdventureVictoryCommitResult.OutOfSequence;
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new CommitAdventureVictoryCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				completedStageIndex,
				normalizedEnemyAvatarId),
			request => PersistMainAdventureVictory(profile, request, alreadyCommitted: false));
		if (!result.Accepted)
		{
			return result.WriteResult == AdventureRunWriteResult.SaveFailed
				? AdventureVictoryCommitResult.SaveFailed
				: AdventureVictoryCommitResult.InvalidStage;
		}

		_deferredPayload = null;
		_preservedFailurePayload = null;
		LastRestoreResult = completedStageIndex >= AdventureCampaignCatalog.MainStageCount
			? AdventureUpgradeRestoreResult.NoSnapshot
			: AdventureUpgradeRestoreResult.Restored;
		return AdventureVictoryCommitResult.Succeeded;
	}

	private AdventureVictoryCommitResult AlignAlreadyCommittedRunIfNeeded(
		GameSaveStore.GameProgressData profile,
		int completedStageIndex,
		string enemyAvatarId)
	{
		AdventureRunReadModel readModel = _authoritySession.AdventureReadModel;
		if (!readModel.IsActive
			|| readModel.MainStageIndex != completedStageIndex
			|| !string.Equals(readModel.EnemyAvatarId, enemyAvatarId, StringComparison.Ordinal))
		{
			return AdventureVictoryCommitResult.AlreadyCommitted;
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new CommitAdventureVictoryCommand(
				NextAdventureActionId(),
				readModel.Revision,
				completedStageIndex,
				enemyAvatarId),
			request => PersistMainAdventureVictory(profile, request, alreadyCommitted: true));
		if (!result.Accepted)
		{
			return result.WriteResult == AdventureRunWriteResult.SaveFailed
				? AdventureVictoryCommitResult.SaveFailed
				: AdventureVictoryCommitResult.InvalidStage;
		}

		return AdventureVictoryCommitResult.AlreadyCommitted;
	}

}
