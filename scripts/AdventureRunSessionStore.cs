using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

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

	private readonly AdventureUpgradeRunState _runState = new();
	private GameModePolicy _gameModePolicy = GameModePolicy.AdventureMain;
	private string? _deferredPayload;
	private string? _preservedFailurePayload;
	private string? _lastObservedPayload;
	private string? _lastWarnedInvalidPayload;
	private int _debugPersistFailuresRemaining;
	private int _debugPersistAttemptCount;
	private AdventureRunWriteResult? _debugNextTransactionalWriteResult;

	public bool HasActiveRun => _runState.IsActive;
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
			_runState.Clear();
			_deferredPayload = null;
			_preservedFailurePayload = null;
			LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
		}
	}

	private void OnPandoraDataLoaded()
	{
		_ = AdventureSuitUpgradeCatalog.Load(this);
	}

	internal AdventureUpgradeRunState GetRunState()
	{
		return _runState.DeepClone();
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
		if (!_gameModePolicy.CanReadAdventureRun || !_runState.IsActive)
		{
			snapshot = default;
			return false;
		}

		snapshot = _runState.ToSnapshot();
		return true;
	}

	internal AdventureUpgradeRestoreResult SetManifest(AdventureUpgradeManifest manifest)
	{
		if (manifest == null || !manifest.IsReady)
		{
			return AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
		}

		_runState.SetManifest(manifest);
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

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		candidate.BeginNewMainRun(mainStageIndex, enemyAvatarId);
		return CommitCandidate(candidate);
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

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		candidate.AdvanceToMainStage(mainStageIndex, enemyAvatarId);
		return CommitCandidate(candidate);
	}

	internal AdventureDefeatCommitResult ResolveMainAdventureDefeat()
	{
		if (!_gameModePolicy.IsAdventureMain
			|| !_gameModePolicy.CanWriteAdventureRun
			|| HasRetainedPayload
			|| !_runState.IsActive)
		{
			return new AdventureDefeatCommitResult(
				!_gameModePolicy.IsAdventureMain
					? AdventureRunWriteResult.WriteNotAllowed
					: ValidateActiveWrite(),
				null);
		}

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		AdventureDefeatResolution resolution = candidate.ResolveMainAdventureDefeat();
		candidate.EffectState.ClearBattleScope();
		AdventureRunWriteResult writeResult = CommitCandidate(candidate);
		return new AdventureDefeatCommitResult(
			writeResult,
			writeResult == AdventureRunWriteResult.Succeeded ? resolution : null);
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

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		AdventureUpgradeAcquireResult acquireResult = candidate.AcquireUpgrade(upgradeId);
		if (acquireResult != AdventureUpgradeAcquireResult.Acquired)
		{
			return new AdventureUpgradeGrantCommitResult(
				AdventureRunWriteResult.Succeeded,
				acquireResult,
				default,
				null);
		}

		switch (triggerSource)
		{
			case AdventureUpgradeTriggerSource.RunStart:
				candidate.MarkRunStartUpgradeGranted();
				break;
			case AdventureUpgradeTriggerSource.Victory:
				candidate.MarkVictoryUpgradeGranted();
				break;
		}

		AdventureUpgradeAcquisitionEffectResult effectResult =
			AdventureUpgradeEffectService.OnUpgradeAcquired(
				candidate.EffectState,
				upgradeId,
				triggerSource == AdventureUpgradeTriggerSource.RunStart);
		AdventureHealingResult? healingResult = null;
		if (effectResult.MaxHealthDelta > 0 || effectResult.HealAmount > 0)
		{
			int nextMaxHealth = Math.Max(1, candidate.MaxHealth + Math.Max(0, effectResult.MaxHealthDelta));
			AdventureHealingResult appliedHealing = AdventureUpgradeEffectService.ApplyHealing(
				candidate.CurrentHealth,
				nextMaxHealth,
				candidate.EffectState,
				candidate.GetAcquiredUpgradeIds(),
				effectResult.HealAmount);
			candidate.SetHealth(appliedHealing.CurrentHealth, appliedHealing.MaxHealth);
			healingResult = appliedHealing;
		}

		AdventureRunWriteResult writeResult = CommitCandidate(candidate, unlockedUpgradeId: upgradeId);
		return writeResult == AdventureRunWriteResult.Succeeded
			? new AdventureUpgradeGrantCommitResult(
				writeResult,
				acquireResult,
				effectResult,
				healingResult)
			: new AdventureUpgradeGrantCommitResult(
				writeResult,
				AdventureUpgradeAcquireResult.None,
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

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		candidate.SetHealth(currentHealth, maxHealth);
		return CommitCandidate(candidate);
	}

	internal AdventureRunWriteResult ReplaceEffectState(AdventureUpgradeEffectState effectState)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		candidate.ReplaceEffectState(effectState);
		return CommitCandidate(candidate);
	}

	internal AdventureRunWriteResult UpdateVitalsAndEffects(
		int currentHealth,
		int maxHealth,
		AdventureUpgradeEffectState effectState)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		candidate.ReplaceEffectState(effectState);
		candidate.SetHealth(currentHealth, maxHealth);
		return CommitCandidate(candidate);
	}

	internal AdventureHealingCommitResult ApplyHealing(int healAmount, int maxHealthDelta = 0)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureHealingCommitResult(validation, default);
		}

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		int nextMaxHealth = Math.Max(1, candidate.MaxHealth + Math.Max(0, maxHealthDelta));
		AdventureHealingResult healingResult = AdventureUpgradeEffectService.ApplyHealing(
			candidate.CurrentHealth,
			nextMaxHealth,
			candidate.EffectState,
			candidate.GetAcquiredUpgradeIds(),
			healAmount);
		candidate.SetHealth(healingResult.CurrentHealth, healingResult.MaxHealth);
		AdventureRunWriteResult writeResult = CommitCandidate(candidate);
		return new AdventureHealingCommitResult(
			writeResult,
			writeResult == AdventureRunWriteResult.Succeeded ? healingResult : default);
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

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		candidate.SetHealth(currentHealth, candidate.MaxHealth);
		AdventureHealingResult healingResult = AdventureUpgradeEffectService.ApplyHealing(
			candidate.CurrentHealth,
			candidate.MaxHealth,
			candidate.EffectState,
			candidate.GetAcquiredUpgradeIds(),
			healAmount);
		candidate.SetHealth(healingResult.CurrentHealth, healingResult.MaxHealth);
		AdventureRunWriteResult writeResult = CommitCandidate(candidate);
		return new AdventureHealingCommitResult(
			writeResult,
			writeResult == AdventureRunWriteResult.Succeeded ? healingResult : default);
	}

	internal AdventureQuestionRewardCommitResult ResolveQuestionReward()
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureQuestionRewardCommitResult(validation, default, null);
		}

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		AdventureQuestionRewardEffectResult effectResult = AdventureUpgradeEffectService.OnQuestionRewardGranted(
			candidate.EffectState,
			candidate.GetAcquiredUpgradeIds());
		AdventureHealingResult? healingResult = ApplyCandidateHealing(candidate, effectResult.HealAmount);
		AdventureRunWriteResult writeResult = CommitCandidate(candidate);
		return new AdventureQuestionRewardCommitResult(
			writeResult,
			writeResult == AdventureRunWriteResult.Succeeded ? effectResult : default,
			writeResult == AdventureRunWriteResult.Succeeded ? healingResult : null);
	}

	internal AdventureTurnStartCommitResult ResolveTurnStartEffects()
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureTurnStartCommitResult(validation, default, null);
		}

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		AdventureTurnStartEffectResult effectResult =
			AdventureUpgradeEffectService.ConsumeMainPlayerTurnStartEffects(candidate.EffectState);
		AdventureHealingResult? healingResult = ApplyCandidateHealing(candidate, effectResult.HealAmount);
		AdventureRunWriteResult writeResult = CommitCandidate(candidate);
		return new AdventureTurnStartCommitResult(
			writeResult,
			writeResult == AdventureRunWriteResult.Succeeded ? effectResult : default,
			writeResult == AdventureRunWriteResult.Succeeded ? healingResult : null);
	}

	internal AdventureDirectDamageCommitResult ApplyDirectDamage(
		AdventureDirectDamageCommitInput input)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureDirectDamageCommitResult(validation, 0, 0, default, default);
		}

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		int previousHealth = candidate.CurrentHealth;
		int damagedHealth = Math.Max(0, previousHealth - Math.Max(0, input.DamageAmount));
		AdventureLowHealthRescueResult rescueResult =
			AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
				previousHealth,
				damagedHealth,
				candidate.MaxHealth,
				candidate.GetAcquiredUpgradeIds(),
				input.HalfHealthRescueUsed,
				input.QuarterHealthRescueUsed);
		candidate.SetHealth(damagedHealth, candidate.MaxHealth);
		AdventureHealingResult healingResult = ApplyCandidateHealing(candidate, rescueResult.HealAmount)
			?? new AdventureHealingResult(
				candidate.CurrentHealth,
				candidate.MaxHealth,
				0,
				0,
				candidate.EffectState.HealingOverflowRemainder);
		AdventureRunWriteResult writeResult = CommitCandidate(candidate, forcePersist: true);
		return writeResult == AdventureRunWriteResult.Succeeded
			? new AdventureDirectDamageCommitResult(
				writeResult,
				previousHealth,
				damagedHealth,
				rescueResult,
				healingResult)
			: new AdventureDirectDamageCommitResult(writeResult, 0, 0, default, default);
	}

	internal AdventureMissCommitResult ResolveMissAction(AdventureMissCommitInput input)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return new AdventureMissCommitResult(validation, default, default, null, false);
		}

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		IReadOnlyCollection<string> acquiredUpgradeIds = candidate.GetAcquiredUpgradeIds();
		AdventureUpgradeEffectService.ConsumeBorrowSuitSetAfterNumericBaseCardPlay(
			candidate.EffectState,
			input.PlayedCardRank);
		AdventurePostCaptureEffectResult recoveryEffect =
			AdventureUpgradeEffectService.ResolveEnchantedRecoveryActionEffects(
				acquiredUpgradeIds,
				input.TriggeredAttachedItemCardIds,
				successfulCapture: false);
		AdventureHealingResult? healingResult = ApplyCandidateHealing(
			candidate,
			recoveryEffect.HealAmount);

		int nextTurnHealBeforeMiss = candidate.EffectState.NextTurnHealAmount;
		int nextTurnDrawBeforeMiss = candidate.EffectState.NextTurnDrawCount;
		AdventureMainPlayerMissEffectResult effectResult =
			AdventureUpgradeEffectService.OnMainPlayerMissResolved(
				candidate.EffectState,
				acquiredUpgradeIds,
				input.TableCardCountAfterMiss);
		bool commitBandageAtRealTurnEnd = candidate.HasAcquiredUpgrade("hearts_miss_bandage");
		if (commitBandageAtRealTurnEnd)
		{
			candidate.EffectState.NextTurnHealAmount = nextTurnHealBeforeMiss;
			candidate.EffectState.NextTurnDrawCount = nextTurnDrawBeforeMiss;
		}

		AdventureRunWriteResult writeResult = CommitCandidate(candidate, forcePersist: true);
		return writeResult == AdventureRunWriteResult.Succeeded
			? new AdventureMissCommitResult(
				writeResult,
				effectResult,
				recoveryEffect,
				healingResult,
				commitBandageAtRealTurnEnd)
			: new AdventureMissCommitResult(writeResult, default, default, null, false);
	}

	internal AdventureCaptureCommitResult ResolveCaptureAction(AdventureCaptureCommitInput input)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return FailedCaptureCommit(validation);
		}
		ArgumentNullException.ThrowIfNull(input.CaptureContext);

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		IReadOnlyCollection<string> acquiredUpgradeIds = candidate.GetAcquiredUpgradeIds();
		if (input.ConsumeBorrowSuitSet)
		{
			AdventureUpgradeEffectService.ConsumeBorrowSuitSetAfterNumericBaseCardPlay(
				candidate.EffectState,
				input.PlayedCardRank);
		}
		if (input.ConsumeGapRooting)
		{
			_ = AdventureUpgradeEffectService.ResolveClubCaptureExtensions(
				candidate.EffectState,
				acquiredUpgradeIds,
				hasMiddleScoringCards: false,
				leftAdjacentBaseCardAvailable: true);
		}

		AdventureCaptureDamageResult damageResult =
			AdventureUpgradeEffectService.ApplyCaptureDamage(
				candidate.EffectState,
				acquiredUpgradeIds,
				input.DamageInput);
		AdventureLowHealthRescueResult rescueResult = default;
		bool consumedHalfHealthRescue = false;
		bool consumedQuarterHealthRescue = false;
		AdventureHealingResult? healingResult = null;
		if (!input.CapturerIsMainPlayer && damageResult.FinalDamage > 0)
		{
			int previousHealth = candidate.CurrentHealth;
			int damagedHealth = Math.Max(0, previousHealth - damageResult.FinalDamage);
			rescueResult = AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
				previousHealth,
				damagedHealth,
				candidate.MaxHealth,
				acquiredUpgradeIds,
				input.HalfHealthRescueUsed,
				input.QuarterHealthRescueUsed);
			candidate.SetHealth(damagedHealth, candidate.MaxHealth);
			healingResult = ApplyCandidateHealing(candidate, rescueResult.HealAmount);
			consumedHalfHealthRescue = rescueResult.ConsumedHalfThreshold;
			consumedQuarterHealthRescue = rescueResult.ConsumedQuarterThreshold;
		}

		AdventurePostCaptureEffectResult postCaptureEffectResult = default;
		AdventureClubCaptureEffectResult clubCaptureEffectResult = default;
		if (input.CapturerIsMainPlayer)
		{
			postCaptureEffectResult = AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
				candidate.EffectState,
				acquiredUpgradeIds,
				damageResult.FinalDamage,
				input.SuccessfulCapturesThisTurn,
				input.CaptureContext);
			AdventureHealingResult? postCaptureHealing = ApplyCandidateHealing(
				candidate,
				postCaptureEffectResult.HealAmount);
			healingResult = postCaptureHealing ?? healingResult;
			clubCaptureEffectResult = AdventureUpgradeEffectService.ResolveClubCaptureEffects(
				candidate.EffectState,
				acquiredUpgradeIds,
				input.ClubCapture.CompleteBaseCaptureCardCount,
				input.ClubCapture.UniqueShortReturnCandidateCount,
				input.ClubCapture.UniqueCombinedReturnCandidateCount,
				input.ClubCapture.AvailableHandSlots,
				input.ClubCapture.PlayUsedAttachedEffect,
				input.ClubCapture.PlayedCardWasBaseCard);
			candidate.EffectState.ClubMissClimbGrowthStacks = 0;
		}

		AdventureQuestionRewardEffectResult questionRewardEffectResult = default;
		for (int rewardIndex = 0; rewardIndex < Math.Max(0, input.PlannedQuestionRewardCount); rewardIndex++)
		{
			AdventureQuestionRewardEffectResult rewardEffect =
				AdventureUpgradeEffectService.OnQuestionRewardGranted(
					candidate.EffectState,
					acquiredUpgradeIds);
			questionRewardEffectResult = new AdventureQuestionRewardEffectResult(
				AdventureUpgradeEffectMath.SaturatingAddToInt(
					questionRewardEffectResult.BonusDirectDamage,
					rewardEffect.BonusDirectDamage),
				AdventureUpgradeEffectMath.SaturatingAddToInt(
					questionRewardEffectResult.HealAmount,
					rewardEffect.HealAmount),
				AdventureUpgradeEffectMath.SaturatingAddToInt(
					questionRewardEffectResult.DrawCount,
					rewardEffect.DrawCount));
			AdventureHealingResult? questionHealing = ApplyCandidateHealing(
				candidate,
				rewardEffect.HealAmount);
			healingResult = questionHealing ?? healingResult;
		}

		AdventureRunWriteResult writeResult = CommitCandidate(candidate, forcePersist: true);
		return writeResult == AdventureRunWriteResult.Succeeded
			? new AdventureCaptureCommitResult(
				writeResult,
				damageResult,
				postCaptureEffectResult,
				clubCaptureEffectResult,
				questionRewardEffectResult,
				healingResult,
				rescueResult,
				candidate.CurrentHealth,
				consumedHalfHealthRescue,
				consumedQuarterHealthRescue)
			: FailedCaptureCommit(writeResult);
	}

	internal AdventureRunWriteResult ResolveRealTurnEnd(bool commitMissBandage)
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		if (commitMissBandage && candidate.HasAcquiredUpgrade("hearts_miss_bandage"))
		{
			candidate.EffectState.NextTurnHealAmount = AdventureUpgradeEffectMath.SaturatingAddToInt(
				candidate.EffectState.NextTurnHealAmount,
				AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_miss_bandage",
					"amount",
					3));
			candidate.EffectState.NextTurnDrawCount = AdventureUpgradeEffectMath.SaturatingAddToInt(
				candidate.EffectState.NextTurnDrawCount,
				AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_miss_bandage",
					"count",
					1));
		}
		AdventureUpgradeEffectService.ClearTurnScopedState(candidate.EffectState);
		return CommitCandidate(candidate, forcePersist: true);
	}

	internal AdventureRunWriteResult ResetBattleScopedEffects()
	{
		AdventureRunWriteResult validation = ValidateActiveWrite();
		if (validation != AdventureRunWriteResult.Succeeded)
		{
			return validation;
		}

		AdventureUpgradeRunState candidate = _runState.DeepClone();
		AdventureUpgradeEffectService.ClearBattleScopedState(candidate.EffectState);
		return CommitCandidate(candidate, forcePersist: true);
	}

	internal AdventureRunWriteResult ConfigureEffectStateForDebug(AdventureUpgradeEffectState effectState)
	{
		return ReplaceEffectState(effectState);
	}

	internal AdventureRunWriteResult ClearRun()
	{
		if (!_gameModePolicy.CanWriteAdventureRun)
		{
			return AdventureRunWriteResult.WriteNotAllowed;
		}
		if (!TryPersistRunPayload(string.Empty))
		{
			return AdventureRunWriteResult.SaveFailed;
		}

		_deferredPayload = null;
		_preservedFailurePayload = null;
		_runState.Clear();
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
			|| !_runState.IsActive
			|| _runState.MainStageIndex != completedStageIndex
			|| !string.Equals(_runState.EnemyAvatarId, normalizedEnemyAvatarId, StringComparison.Ordinal))
		{
			return AdventureVictoryCommitResult.InvalidStage;
		}
		if (completedStageIndex != previousHighestStage + 1)
		{
			return AdventureVictoryCommitResult.OutOfSequence;
		}

		bool isFinalStage = completedStageIndex >= AdventureCampaignCatalog.MainStageCount;
		string nextRunJson = string.Empty;
		if (!isFinalStage)
		{
			AdventureUpgradeRunSnapshot nextSnapshot = CreateAdvancedSnapshot(completedStageIndex + 1);
			nextRunJson = JsonSerializer.Serialize(nextSnapshot);
		}

		GameSaveStore.GameProgressData committedProfile =
			CharacterAvatarCatalog.CreateMainAdventureVictoryProfile(
				profile,
				completedStageIndex,
				normalizedEnemyAvatarId,
				nextRunJson);
		Error saveResult = GameSaveStore.SaveActiveProfile(committedProfile);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to atomically commit adventure victory: {saveResult}");
			return AdventureVictoryCommitResult.SaveFailed;
		}

		if (isFinalStage)
		{
			_runState.Clear();
		}
		else
		{
			_runState.AdvanceToMainStage(completedStageIndex + 1, AdventureCampaignCatalog.GetMainAvatarId(completedStageIndex + 1));
			_runState.EffectState.ClearBattleScope();
		}
		_deferredPayload = null;
		_preservedFailurePayload = null;
		LastRestoreResult = isFinalStage
			? AdventureUpgradeRestoreResult.NoSnapshot
			: AdventureUpgradeRestoreResult.Restored;
		ObservePayload(nextRunJson);
		return AdventureVictoryCommitResult.Succeeded;
	}

	private AdventureVictoryCommitResult AlignAlreadyCommittedRunIfNeeded(
		GameSaveStore.GameProgressData profile,
		int completedStageIndex,
		string enemyAvatarId)
	{
		if (!_runState.IsActive
			|| _runState.MainStageIndex != completedStageIndex
			|| !string.Equals(_runState.EnemyAvatarId, enemyAvatarId, StringComparison.Ordinal))
		{
			return AdventureVictoryCommitResult.AlreadyCommitted;
		}

		bool isFinalStage = completedStageIndex >= AdventureCampaignCatalog.MainStageCount;
		string nextRunJson = isFinalStage
			? string.Empty
			: JsonSerializer.Serialize(CreateAdvancedSnapshot(completedStageIndex + 1));
		Error saveResult = GameSaveStore.SaveActiveProfile(profile with
		{
			PendingAdventureRunJson = nextRunJson,
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to align an already committed adventure run: {saveResult}");
			return AdventureVictoryCommitResult.SaveFailed;
		}

		if (isFinalStage)
		{
			_runState.Clear();
		}
		else
		{
			_runState.AdvanceToMainStage(
				completedStageIndex + 1,
				AdventureCampaignCatalog.GetMainAvatarId(completedStageIndex + 1));
			_runState.EffectState.ClearBattleScope();
		}
		ObservePayload(nextRunJson);
		return AdventureVictoryCommitResult.AlreadyCommitted;
	}

	internal AdventureUpgradeRestoreResult ReloadFromActiveProfile()
	{
		if (!_gameModePolicy.CanReadAdventureRun)
		{
			_runState.Clear();
			_deferredPayload = null;
			_preservedFailurePayload = null;
			LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
			return LastRestoreResult;
		}

		_runState.Clear();
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
			_runState.Clear();
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
			_runState.Clear();
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
			_runState.Clear();
			_deferredPayload = null;
			_preservedFailurePayload = json;
			LastRestoreResult = AdventureUpgradeRestoreResult.InvalidSnapshotPreserved;
			WarnPreservedPayloadOnce(
				json,
				$"Failed to restore adventure upgrade run session snapshot; the original payload was preserved: {failure}");
			return LastRestoreResult;
		}

		AdventureUpgradeRestoreResult result = _runState.RestoreFromSnapshot(snapshot);
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
				_runState.Clear();
				_deferredPayload = null;
				_preservedFailurePayload = null;
				return result;
		}
	}

	private void PersistMigratedSnapshot(GameSaveStore.GameProgressData profile)
	{
		if (!_gameModePolicy.CanWriteAdventureRun || !_runState.IsActive)
		{
			return;
		}

		string json = JsonSerializer.Serialize(_runState.ToSnapshot());
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
		return AdventureUpgradeChoiceRules.IsMainAdventureStageIndex(mainStageIndex)
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
		return _runState.IsActive
			? AdventureRunWriteResult.Succeeded
			: AdventureRunWriteResult.InvalidStage;
	}

	private AdventureRunWriteResult CommitCandidate(
		AdventureUpgradeRunState candidate,
		bool forcePersist = false,
		string? unlockedUpgradeId = null)
	{
		string candidatePayload = candidate.IsActive
			? JsonSerializer.Serialize(candidate.ToSnapshot())
			: string.Empty;
		string currentPayload = _runState.IsActive
			? JsonSerializer.Serialize(_runState.ToSnapshot())
			: string.Empty;
		if ((forcePersist || !string.Equals(candidatePayload, currentPayload, StringComparison.Ordinal))
			&& !TryPersistRunPayload(candidatePayload, unlockedUpgradeId))
		{
			return AdventureRunWriteResult.SaveFailed;
		}

		_runState.CopyFrom(candidate);
		return AdventureRunWriteResult.Succeeded;
	}

	private static AdventureHealingResult? ApplyCandidateHealing(
		AdventureUpgradeRunState candidate,
		int healAmount)
	{
		if (healAmount <= 0)
		{
			return null;
		}

		AdventureHealingResult healingResult = AdventureUpgradeEffectService.ApplyHealing(
			candidate.CurrentHealth,
			candidate.MaxHealth,
			candidate.EffectState,
			candidate.GetAcquiredUpgradeIds(),
			healAmount);
		candidate.SetHealth(healingResult.CurrentHealth, healingResult.MaxHealth);
		return healingResult;
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

	private AdventureUpgradeRunSnapshot CreateAdvancedSnapshot(int mainStageIndex)
	{
		AdventureUpgradeRunSnapshot current = _runState.ToSnapshot();
		return current with
		{
			CampaignId = AdventureCampaignCatalog.MainCampaignId,
			MainStageIndex = mainStageIndex,
			EnemyAvatarId = AdventureCampaignCatalog.GetMainAvatarId(mainStageIndex),
			ShouldGrantRunStartUpgrade = false,
			ShouldGrantVictoryUpgrade = true,
		};
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
