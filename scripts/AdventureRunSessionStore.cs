using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Deckland.Rules;

public sealed partial class AdventureRunSessionStore : Node
{
	private static readonly HashSet<string> CanonicalCurrentPropertyNames = new(
		StringComparer.Ordinal)
	{
		nameof(AdventureUpgradeRunSnapshot.SchemaVersion),
		nameof(AdventureUpgradeRunSnapshot.CampaignId),
		nameof(AdventureUpgradeRunSnapshot.MainStageIndex),
		nameof(AdventureUpgradeRunSnapshot.EnemyAvatarId),
		nameof(AdventureUpgradeRunSnapshot.ShouldGrantRunStartUpgrade),
		nameof(AdventureUpgradeRunSnapshot.ShouldGrantVictoryUpgrade),
		nameof(AdventureUpgradeRunSnapshot.AcquiredUpgradeIds),
		nameof(AdventureUpgradeRunSnapshot.CurrentHealth),
		nameof(AdventureUpgradeRunSnapshot.MaxHealth),
		nameof(AdventureUpgradeRunSnapshot.PermanentCaptureDamageBonus),
		nameof(AdventureUpgradeRunSnapshot.QuestionRewardCount),
		nameof(AdventureUpgradeRunSnapshot.HealingOverflowRemainder),
		nameof(AdventureUpgradeRunSnapshot.HasUsedRevive),
	};

	private AuthorityRuleSession _authoritySession = CreateDetachedAuthoritySession();
	private AuthorityRuleSession? _boundBoardAuthoritySession;
	private AdventureUpgradeManifest? _manifest;
	private long _nextAdventureActionId;
	private GameModePolicy _gameModePolicy = GameModePolicy.AdventureMain;
	private string? _deferredPayload;
	private string? _preservedFailurePayload;
	private string? _lastObservedPayload;
	private string? _lastWarnedInvalidPayload;
	private int _debugPersistFailuresRemaining;
	private int _debugPersistAttemptCount;
	private AdventureRunWriteResult? _debugNextTransactionalWriteResult;

	public bool HasActiveRun => _gameModePolicy.CanReadAdventureRun
		&& _authoritySession.AdventureReadModel.IsActive;
	public bool RequiresRunRecovery => !string.IsNullOrEmpty(_preservedFailurePayload);
	internal GameModePolicy ModePolicy => _gameModePolicy;
	internal AdventureUpgradeRestoreResult LastRestoreResult { get; private set; } =
		AdventureUpgradeRestoreResult.NoSnapshot;

	public override void _Ready()
	{
		Node? pandora = GetNodeOrNull<Node>("/root/Pandora");
		if (pandora != null)
		{
			if (pandora.Call("is_loaded").AsBool())
			{
				_ = AdventureSuitUpgradeCatalog.Load(this);
			}
			else
			{
				Callable dataLoadedCallback = Callable.From(OnPandoraDataLoaded);
				if (!pandora.IsConnected("data_loaded", dataLoadedCallback))
				{
					pandora.Connect("data_loaded", dataLoadedCallback);
				}
			}
		}

	}

	internal void ConfigureForPolicy(GameModePolicy policy)
	{
		_gameModePolicy = policy ?? GameModePolicy.LocalStandard;
		if (!_gameModePolicy.CanReadAdventureRun)
		{
			_deferredPayload = null;
			_preservedFailurePayload = null;
			LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
		}
	}

	private void OnPandoraDataLoaded()
	{
		_ = AdventureSuitUpgradeCatalog.Load(this);
	}

	internal AdventureRunReadModel GetReadModel()
	{
		return _authoritySession.AdventureReadModel;
	}

	internal bool BindAuthorityRuleSession(AuthorityRuleSession authoritySession)
	{
		ArgumentNullException.ThrowIfNull(authoritySession);
		if (_boundBoardAuthoritySession == authoritySession)
		{
			return true;
		}
		if (_manifest?.IsReady != true
			|| !authoritySession.ConfigureAdventureManifest(_manifest))
		{
			return false;
		}

		AdventureAuthoritySnapshot snapshot = _authoritySession.CaptureAdventureAuthority();
		AdventureUpgradeRestoreResult restoreResult =
			authoritySession.RestoreAdventureAuthority(snapshot);
		if (restoreResult is AdventureUpgradeRestoreResult.InvalidSnapshotPreserved
			or AdventureUpgradeRestoreResult.DeferredManifestUnavailable)
		{
			return false;
		}

		_authoritySession = authoritySession;
		_boundBoardAuthoritySession = authoritySession;
		SynchronizeNextActionId(snapshot);
		return true;
	}

	internal void UnbindAuthorityRuleSession(AuthorityRuleSession authoritySession)
	{
		if (_boundBoardAuthoritySession != authoritySession)
		{
			return;
		}

		AdventureAuthoritySnapshot snapshot = authoritySession.CaptureAdventureAuthority();
		AuthorityRuleSession detached = CreateDetachedAuthoritySession();
		if (_manifest?.IsReady == true)
		{
			_ = detached.ConfigureAdventureManifest(_manifest);
			_ = detached.RestoreAdventureAuthority(snapshot);
		}
		_authoritySession = detached;
		_boundBoardAuthoritySession = null;
		SynchronizeNextActionId(snapshot);
	}

	internal void ConfigurePersistFailuresForDebug(int failureCount)
	{
		_debugPersistFailuresRemaining = Math.Max(0, failureCount);
		_debugPersistAttemptCount = 0;
	}

	internal int GetPersistAttemptCountForDebug()
	{
		return _debugPersistAttemptCount;
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

	internal bool TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot)
	{
		AdventureRunReadModel readModel = _authoritySession.AdventureReadModel;
		if (!_gameModePolicy.CanReadAdventureRun || !readModel.IsActive)
		{
			snapshot = default;
			return false;
		}

		snapshot = _authoritySession.CaptureAdventureAuthority().RunSnapshot!.Value;
		return true;
	}

	internal AdventureUpgradeRestoreResult SetManifest(AdventureUpgradeManifest manifest)
	{
		if (manifest == null || !manifest.IsReady)
		{
			return AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
		}

		_manifest = manifest;
		if (!_authoritySession.ConfigureAdventureManifest(manifest))
		{
			return AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
		}
		if (!_gameModePolicy.CanReadAdventureRun)
		{
			return AdventureUpgradeRestoreResult.NoSnapshot;
		}
		if (string.IsNullOrEmpty(_deferredPayload))
		{
			return LastRestoreResult;
		}

		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		string currentPayload = profile.PendingAdventureRunJson;
		if (!string.Equals(currentPayload, _deferredPayload, StringComparison.Ordinal))
		{
			return ReloadFromActiveProfile();
		}

		return RestorePayload(profile, _deferredPayload);
	}

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
		AdventureUpgradeTriggerSource triggerSource)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureUpgradeGrantCommitResult(
				validation,
				AdventureUpgradeAcquireResult.DeferredManifestUnavailable,
				default,
				null);
		}

		AdventureRuleCommandResult result = _authoritySession.SubmitAdventure(
			new GrantAdventureUpgradeCommand(
				NextAdventureActionId(),
				_authoritySession.AdventureReadModel.Revision,
				upgradeId,
				triggerSource),
			PersistAdventureRequest);
		if (result.Accepted && result.Outcome is AdventureUpgradeGrantCommandOutcome outcome)
		{
			return new AdventureUpgradeGrantCommitResult(
				result.WriteResult,
				outcome.AcquireResult,
				outcome.EffectResult,
				outcome.HealingResult);
		}

		return new AdventureUpgradeGrantCommitResult(
			result.WriteResult,
			result.WriteResult == AdventureRunWriteResult.SaveFailed
				? AdventureUpgradeAcquireResult.None
				: AdventureUpgradeAcquireResult.DeferredManifestUnavailable,
			default,
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

	internal AdventureUpgradeRestoreResult ReloadFromActiveProfile()
	{
		if (!_gameModePolicy.CanReadAdventureRun)
		{
			ResetAdventureAuthorityForRestore();
			_deferredPayload = null;
			_preservedFailurePayload = null;
			LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
			return LastRestoreResult;
		}

		ResetAdventureAuthorityForRestore();
		_deferredPayload = null;
		_preservedFailurePayload = null;
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		string json = profile.PendingAdventureRunJson;
		ObservePayload(json);
		if (string.IsNullOrWhiteSpace(json))
		{
			LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
			return LastRestoreResult;
		}

		return RestorePayload(profile, json);
	}

	private AdventureUpgradeRestoreResult RestorePayload(
		GameSaveStore.GameProgressData profile,
		string json)
	{
		if (!_gameModePolicy.CanReadAdventureRun)
		{
			ResetAdventureAuthorityForRestore();
			return AdventureUpgradeRestoreResult.NoSnapshot;
		}

		bool requiresCanonicalCurrentRewrite;
		bool migratedBySchema;
		try
		{
			using JsonDocument document = JsonDocument.Parse(json);
			requiresCanonicalCurrentRewrite = RequiresCanonicalCurrentRewrite(document.RootElement);
		}
		catch (JsonException error)
		{
			ResetAdventureAuthorityForRestore();
			_deferredPayload = null;
			_preservedFailurePayload = json;
			LastRestoreResult = AdventureUpgradeRestoreResult.InvalidJsonPreserved;
			WarnPreservedPayloadOnce(
				json,
				$"Failed to restore adventure upgrade run session JSON; the original payload was preserved: {error.Message}");
			return LastRestoreResult;
		}

		if (!AdventureUpgradeRunSnapshot.TryDeserialize(
			json,
			out AdventureUpgradeRunSnapshot snapshot,
			out migratedBySchema,
			out string failure))
		{
			ResetAdventureAuthorityForRestore();
			_deferredPayload = null;
			_preservedFailurePayload = json;
			LastRestoreResult = AdventureUpgradeRestoreResult.InvalidSnapshotPreserved;
			WarnPreservedPayloadOnce(
				json,
				$"Failed to restore adventure upgrade run session snapshot; the original payload was preserved: {failure}");
			return LastRestoreResult;
		}

		AdventureUpgradeRestoreResult result =
			_authoritySession.RestoreAdventurePersistedRun(snapshot);
		if (result == AdventureUpgradeRestoreResult.Restored
			&& (requiresCanonicalCurrentRewrite || migratedBySchema))
		{
			result = AdventureUpgradeRestoreResult.Migrated;
		}
		LastRestoreResult = result;
		switch (result)
		{
			case AdventureUpgradeRestoreResult.Restored:
				_deferredPayload = null;
				_preservedFailurePayload = null;
				return result;
			case AdventureUpgradeRestoreResult.Migrated:
				_deferredPayload = null;
				_preservedFailurePayload = null;
				PersistMigratedSnapshot(profile);
				return result;
			case AdventureUpgradeRestoreResult.DeferredManifestUnavailable:
				_deferredPayload = json;
				_preservedFailurePayload = null;
				return result;
			case AdventureUpgradeRestoreResult.InvalidSnapshotPreserved:
				_deferredPayload = null;
				_preservedFailurePayload = json;
				WarnPreservedPayloadOnce(
					json,
					"Failed to restore adventure upgrade run session snapshot; the original payload was preserved.");
				return result;
			default:
				ResetAdventureAuthorityForRestore();
				_deferredPayload = null;
				_preservedFailurePayload = null;
				return result;
		}
	}

	private void PersistMigratedSnapshot(GameSaveStore.GameProgressData profile)
	{
		if (!_gameModePolicy.CanWriteAdventureRun
			|| !TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot))
		{
			return;
		}

		string json = JsonSerializer.Serialize(snapshot);
		Error saveResult = GameSaveStore.SaveActiveProfile(profile with
		{
			PendingAdventureRunJson = json,
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to persist migrated adventure upgrade run session: {saveResult}");
			return;
		}

		ObservePayload(json);
	}

	private AdventureRunWriteResult ValidateTransactionalWrite(int mainStageIndex)
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
		if (HasRetainedPayload)
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

	private static AuthorityRuleSession CreateDetachedAuthoritySession()
	{
		return new AuthorityRuleSession(
			new DetachedAdventureRandomSource(),
			MatchRuleProfile.Empty);
	}

	private long NextAdventureActionId()
	{
		_nextAdventureActionId = checked(_nextAdventureActionId + 1);
		return _nextAdventureActionId;
	}

	private void SynchronizeNextActionId(AdventureAuthoritySnapshot snapshot)
	{
		foreach (long actionId in snapshot.NormalizedAppliedActionIds)
		{
			_nextAdventureActionId = Math.Max(_nextAdventureActionId, actionId);
		}
	}

	private void ResetAdventureAuthorityForRestore()
	{
		if (_manifest?.IsReady == true)
		{
			_ = _authoritySession.ConfigureAdventureManifest(_manifest);
			_ = _authoritySession.RestoreAdventurePersistedRun(null);
			return;
		}

		if (_boundBoardAuthoritySession == null)
		{
			_authoritySession = CreateDetachedAuthoritySession();
		}
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

	private void ObservePayload(string? payload)
	{
		string normalizedPayload = payload ?? string.Empty;
		if (string.Equals(_lastObservedPayload, normalizedPayload, StringComparison.Ordinal))
		{
			return;
		}

		_lastObservedPayload = normalizedPayload;
		_lastWarnedInvalidPayload = null;
	}

	private void WarnPreservedPayloadOnce(string payload, string warning)
	{
		if (string.Equals(_lastWarnedInvalidPayload, payload, StringComparison.Ordinal))
		{
			return;
		}

		GD.PushWarning(warning);
		_lastWarnedInvalidPayload = payload;
	}

	private bool HasRetainedPayload =>
		_deferredPayload != null || _preservedFailurePayload != null;

	private sealed class DetachedAdventureRandomSource : IRuleRandomSource
	{
		private RuleRandomState _state = new(1, 1);

		public RuleRandomState CaptureState() => _state;

		public void RestoreState(RuleRandomState state)
		{
			_state = state.State == 0 ? new RuleRandomState(1, 1) : state;
		}

		public int NextIntInclusive(int minimum, int maximum)
		{
			if (minimum > maximum)
			{
				throw new ArgumentOutOfRangeException(nameof(minimum));
			}
			return minimum;
		}
	}

	private static bool RequiresCanonicalCurrentRewrite(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Object)
		{
			return false;
		}

		if (!root.TryGetProperty(
			nameof(AdventureUpgradeRunSnapshot.SchemaVersion),
			out JsonElement schemaVersionElement)
			|| schemaVersionElement.ValueKind != JsonValueKind.Number
			|| !schemaVersionElement.TryGetInt32(out int schemaVersion)
			|| schemaVersion != AdventureUpgradeRunSnapshot.CurrentSchemaVersion)
		{
			return false;
		}

		var observedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
		foreach (JsonProperty property in root.EnumerateObject())
		{
			if (!CanonicalCurrentPropertyNames.Contains(property.Name)
				|| !observedPropertyNames.Add(property.Name))
			{
				return true;
			}
		}

		return observedPropertyNames.Count != CanonicalCurrentPropertyNames.Count;
	}
}
