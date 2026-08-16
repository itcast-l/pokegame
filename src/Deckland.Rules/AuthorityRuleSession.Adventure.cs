using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
	private readonly global::AdventureUpgradeRunState _adventureState = new();
	private readonly HashSet<long> _appliedAdventureActionIds = new();
	private AdventureRunReadModel _adventureReadModel = AdventureRunReadModel.Empty;
	private global::AdventureUpgradeManifest? _adventureManifest;
	private long _adventureRevision;

	public AdventureRunReadModel AdventureReadModel => _adventureReadModel;

	public bool ConfigureAdventureManifest(global::AdventureUpgradeManifest manifest)
	{
		if (manifest == null || !manifest.IsReady)
		{
			return false;
		}

		_adventureManifest = manifest;
		global::AdventureUpgradeRuntimeValues.SetManifest(manifest);
		_adventureState.SetManifest(manifest);
		RefreshAdventureReadModel();
		return true;
	}

	public global::AdventureUpgradeRestoreResult RestoreAdventureAuthority(
		AdventureAuthoritySnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		if (_adventureManifest?.IsReady != true)
		{
			return global::AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
		}
		if (!MatchFlowStateValidation.IsValid(snapshot.NormalizedMatchFlow))
		{
			return global::AdventureUpgradeRestoreResult.InvalidSnapshotPreserved;
		}

		var candidate = new global::AdventureUpgradeRunState(_adventureManifest);
		global::AdventureUpgradeRestoreResult restoreResult =
			global::AdventureUpgradeRestoreResult.NoSnapshot;
		if (snapshot.RunSnapshot.HasValue)
		{
			restoreResult = candidate.RestoreFromSnapshot(snapshot.RunSnapshot.Value);
			if (restoreResult is not global::AdventureUpgradeRestoreResult.Restored
				and not global::AdventureUpgradeRestoreResult.Migrated)
			{
				return restoreResult;
			}
			if (snapshot.Effects.HasValue)
			{
				candidate.ReplaceEffectState(
					AdventureRunReadModel.CreateEffectState(snapshot.Effects.Value));
			}
		}

		_adventureState.CopyFrom(candidate);
		_state.MatchFlow = snapshot.NormalizedMatchFlow;
		_adventureRevision = Math.Max(0, snapshot.Revision);
		_appliedAdventureActionIds.Clear();
		_appliedAdventureActionIds.UnionWith(snapshot.NormalizedAppliedActionIds);
		RefreshAdventureReadModel();
		return restoreResult;
	}

	public global::AdventureUpgradeRestoreResult RestoreAdventurePersistedRun(
		global::AdventureUpgradeRunSnapshot? snapshot)
	{
		if (_adventureManifest?.IsReady != true)
		{
			return global::AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
		}

		var candidate = new global::AdventureUpgradeRunState(_adventureManifest);
		global::AdventureUpgradeRestoreResult restoreResult =
			global::AdventureUpgradeRestoreResult.NoSnapshot;
		if (snapshot.HasValue)
		{
			restoreResult = candidate.RestoreFromSnapshot(snapshot.Value);
			if (restoreResult is not global::AdventureUpgradeRestoreResult.Restored
				and not global::AdventureUpgradeRestoreResult.Migrated)
			{
				return restoreResult;
			}
		}

		_adventureState.CopyFrom(candidate);
		_state.MatchFlow = MatchFlowState.Empty;
		_adventureRevision = 0;
		_appliedAdventureActionIds.Clear();
		RefreshAdventureReadModel();
		return restoreResult;
	}

	public AdventureAuthoritySnapshot CaptureAdventureAuthority()
	{
		return new AdventureAuthoritySnapshot(
			_adventureRevision,
			_adventureState.IsActive ? _adventureState.ToSnapshot() : null,
			_appliedAdventureActionIds.Order().ToImmutableArray(),
			_adventureState.IsActive
				? AdventureRunReadModel.ToSnapshot(_adventureState.EffectState)
				: null,
			_state.MatchFlow);
	}

	public AdventureRuleCommandResult SubmitAdventure(
		AdventureCommand command,
		AdventurePersistenceHandler persistenceHandler)
	{
		ArgumentNullException.ThrowIfNull(command);
		ArgumentNullException.ThrowIfNull(persistenceHandler);

		RuleRejection? commonRejection = ValidateAdventureCommand(command);
		if (commonRejection.HasValue)
		{
			return RejectedAdventure(
				commonRejection.Value,
				global::AdventureRunWriteResult.InvalidStage);
		}

		global::AdventureUpgradeRunState candidate = _adventureState.DeepClone();
		AdventureEvaluation evaluation = EvaluateAdventureCommand(command, candidate);
		if (!evaluation.Allowed)
		{
			return RejectedAdventure(evaluation.Rejection, evaluation.WriteResult);
		}

		if (!evaluation.StateChanged)
		{
			_appliedAdventureActionIds.Add(command.ActionId);
			return AcceptedAdventure(evaluation.Outcome);
		}

		var persistenceRequest = new AdventurePersistenceRequest(
			_adventureState.IsActive ? _adventureState.ToSnapshot() : null,
			candidate.IsActive ? candidate.ToSnapshot() : null,
			evaluation.ForcePersist,
			evaluation.UnlockedUpgradeId,
			evaluation.PersistenceKind,
			evaluation.CompletedMainStageIndex,
			evaluation.EnemyAvatarId);
		if (!persistenceHandler(persistenceRequest))
		{
			return RejectedAdventure(
				new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Adventure persistence failed."),
				global::AdventureRunWriteResult.SaveFailed);
		}

		_adventureState.CopyFrom(candidate);
		_adventureRevision++;
		_appliedAdventureActionIds.Add(command.ActionId);
		RefreshAdventureReadModel();
		return AcceptedAdventure(evaluation.Outcome);
	}

	private RuleRejection? ValidateAdventureCommand(AdventureCommand command)
	{
		if (command.ActionId <= 0)
		{
			return new RuleRejection(RuleRejectionCode.InvalidCommand, "ActionId must be positive.");
		}
		if (_appliedAdventureActionIds.Contains(command.ActionId))
		{
			return new RuleRejection(RuleRejectionCode.DuplicateAction, command.ActionId.ToString());
		}
		if (command.ExpectedRevision != _adventureRevision)
		{
			return new RuleRejection(RuleRejectionCode.StaleRevision, command.ExpectedRevision.ToString());
		}
		if (_adventureRevision == long.MaxValue)
		{
			return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Adventure revision is exhausted.");
		}
		if (_adventureManifest?.IsReady != true)
		{
			return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Adventure manifest is unavailable.");
		}

		return null;
	}

	private AdventureEvaluation EvaluateAdventureCommand(
		AdventureCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		return command switch
		{
			BeginAdventureRunCommand begin => EvaluateBeginAdventureRun(begin, candidate),
			AdvanceAdventureStageCommand advance => EvaluateAdvanceAdventureStage(advance, candidate),
			ResolveAdventureDefeatCommand => EvaluateAdventureDefeat(candidate),
			GrantAdventureUpgradeCommand grant => EvaluateGrantAdventureUpgrade(grant, candidate),
			SetAdventureHealthCommand health => EvaluateSetAdventureHealth(health, candidate),
			ReplaceAdventureEffectStateCommand effects => EvaluateReplaceAdventureEffects(effects, candidate),
			UpdateAdventureVitalsAndEffectsCommand vitals => EvaluateUpdateAdventureVitals(vitals, candidate),
			ApplyAdventureHealingCommand healing => EvaluateAdventureHealing(healing, candidate),
			UpdateAdventureHealthAndHealCommand healthAndHeal => EvaluateAdventureHealthAndHeal(healthAndHeal, candidate),
			ResolveAdventureQuestionRewardCommand => EvaluateAdventureQuestionReward(candidate),
			ResolveAdventureTurnStartCommand => EvaluateAdventureTurnStart(candidate),
			ApplyAdventureDirectDamageCommand damage => EvaluateAdventureDirectDamage(damage, candidate),
			ResolveAdventureMissCommand miss => EvaluateAdventureMiss(miss, candidate),
			ResolveAdventureCaptureCommand capture => EvaluateAdventureCapture(capture, candidate),
			ResolveAdventureRealTurnEndCommand turnEnd => EvaluateAdventureRealTurnEnd(turnEnd, candidate),
			ResetAdventureBattleEffectsCommand => EvaluateResetAdventureBattleEffects(candidate),
			ClearAdventureRunCommand => EvaluateClearAdventureRun(candidate),
			CommitAdventureVictoryCommand victory => EvaluateAdventureVictory(victory, candidate),
			_ => AdventureEvaluation.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.GetType().Name)),
		};
	}

	private static AdventureEvaluation EvaluateBeginAdventureRun(
		BeginAdventureRunCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!IsValidMainStage(command.MainStageIndex))
		{
			return AdventureEvaluation.InvalidStage;
		}

		candidate.BeginNewMainRun(command.MainStageIndex, command.EnemyAvatarId);
		return AdventureEvaluation.Changed();
	}

	private static AdventureEvaluation EvaluateAdvanceAdventureStage(
		AdvanceAdventureStageCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!IsValidMainStage(command.MainStageIndex))
		{
			return AdventureEvaluation.InvalidStage;
		}

		candidate.AdvanceToMainStage(command.MainStageIndex, command.EnemyAvatarId);
		return AdventureEvaluation.Changed();
	}

	private static AdventureEvaluation EvaluateAdventureDefeat(
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}

		global::AdventureDefeatResolution resolution = candidate.ResolveMainAdventureDefeat();
		candidate.EffectState.ClearBattleScope();
		return AdventureEvaluation.Changed(new AdventureDefeatCommandOutcome(resolution));
	}

	private static AdventureEvaluation EvaluateGrantAdventureUpgrade(
		GrantAdventureUpgradeCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}

		global::AdventureUpgradeAcquireResult acquireResult = candidate.AcquireUpgrade(
			command.UpgradeId,
			command.ReplacedUpgradeId);
		if (acquireResult != global::AdventureUpgradeAcquireResult.Acquired)
		{
			return AdventureEvaluation.Unchanged(new AdventureUpgradeGrantCommandOutcome(
				acquireResult,
				default,
				null,
				null));
		}

		switch (command.TriggerSource)
		{
			case global::AdventureUpgradeTriggerSource.RunStart:
				candidate.MarkRunStartUpgradeGranted();
				break;
			case global::AdventureUpgradeTriggerSource.Victory:
				candidate.MarkVictoryUpgradeGranted();
				break;
		}

		global::AdventureUpgradeAcquisitionEffectResult effectResult =
			global::AdventureUpgradeEffectService.OnUpgradeAcquired(
				candidate.EffectState,
				command.UpgradeId,
				command.TriggerSource == global::AdventureUpgradeTriggerSource.RunStart);
		global::AdventureHealingResult? healingResult = null;
		if (effectResult.MaxHealthDelta > 0 || effectResult.HealAmount > 0)
		{
			int nextMaxHealth = Math.Max(
				1,
				candidate.MaxHealth + Math.Max(0, effectResult.MaxHealthDelta));
			global::AdventureHealingResult appliedHealing =
				global::AdventureUpgradeEffectService.ApplyHealing(
					candidate.CurrentHealth,
					nextMaxHealth,
					candidate.EffectState,
					candidate.GetAcquiredUpgradeIds(),
					effectResult.HealAmount);
			candidate.SetHealth(appliedHealing.CurrentHealth, appliedHealing.MaxHealth);
			healingResult = appliedHealing;
		}

		return AdventureEvaluation.Changed(
			new AdventureUpgradeGrantCommandOutcome(
				acquireResult,
				effectResult,
				healingResult,
				string.IsNullOrWhiteSpace(command.ReplacedUpgradeId)
					? null
					: command.ReplacedUpgradeId.Trim()),
			unlockedUpgradeId: command.UpgradeId);
	}

	private static AdventureEvaluation EvaluateSetAdventureHealth(
		SetAdventureHealthCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		candidate.SetHealth(command.CurrentHealth, command.MaxHealth);
		return AdventureEvaluation.Changed();
	}

	private static AdventureEvaluation EvaluateReplaceAdventureEffects(
		ReplaceAdventureEffectStateCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		candidate.ReplaceEffectState(AdventureRunReadModel.CreateEffectState(command.EffectState));
		return AdventureEvaluation.Changed();
	}

	private static AdventureEvaluation EvaluateUpdateAdventureVitals(
		UpdateAdventureVitalsAndEffectsCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		candidate.ReplaceEffectState(AdventureRunReadModel.CreateEffectState(command.EffectState));
		candidate.SetHealth(command.CurrentHealth, command.MaxHealth);
		return AdventureEvaluation.Changed();
	}

	private static AdventureEvaluation EvaluateAdventureHealing(
		ApplyAdventureHealingCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		int nextMaxHealth = Math.Max(1, candidate.MaxHealth + Math.Max(0, command.MaxHealthDelta));
		global::AdventureHealingResult result = global::AdventureUpgradeEffectService.ApplyHealing(
			candidate.CurrentHealth,
			nextMaxHealth,
			candidate.EffectState,
			candidate.GetAcquiredUpgradeIds(),
			command.HealAmount);
		candidate.SetHealth(result.CurrentHealth, result.MaxHealth);
		return AdventureEvaluation.Changed(new AdventureHealingCommandOutcome(result));
	}

	private static AdventureEvaluation EvaluateAdventureHealthAndHeal(
		UpdateAdventureHealthAndHealCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		candidate.SetHealth(command.CurrentHealth, candidate.MaxHealth);
		global::AdventureHealingResult result = global::AdventureUpgradeEffectService.ApplyHealing(
			candidate.CurrentHealth,
			candidate.MaxHealth,
			candidate.EffectState,
			candidate.GetAcquiredUpgradeIds(),
			command.HealAmount);
		candidate.SetHealth(result.CurrentHealth, result.MaxHealth);
		return AdventureEvaluation.Changed(new AdventureHealingCommandOutcome(result));
	}

	private static AdventureEvaluation EvaluateAdventureQuestionReward(
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		global::AdventureQuestionRewardEffectResult effectResult =
			global::AdventureUpgradeEffectService.OnQuestionRewardGranted(
				candidate.EffectState,
				candidate.GetAcquiredUpgradeIds());
		global::AdventureHealingResult? healingResult = ApplyCandidateHealing(
			candidate,
			effectResult.HealAmount);
		return AdventureEvaluation.Changed(
			new AdventureQuestionRewardCommandOutcome(effectResult, healingResult));
	}

	private static AdventureEvaluation EvaluateAdventureTurnStart(
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		global::AdventureTurnStartEffectResult effectResult =
			global::AdventureUpgradeEffectService.ConsumeMainPlayerTurnStartEffects(
				candidate.EffectState);
		global::AdventureHealingResult? healingResult = ApplyCandidateHealing(
			candidate,
			effectResult.HealAmount);
		return AdventureEvaluation.Changed(
			new AdventureTurnStartCommandOutcome(effectResult, healingResult));
	}

	private static AdventureEvaluation EvaluateAdventureDirectDamage(
		ApplyAdventureDirectDamageCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		int previousHealth = candidate.CurrentHealth;
		int damagedHealth = Math.Max(0, previousHealth - Math.Max(0, command.Input.DamageAmount));
		global::AdventureLowHealthRescueResult rescueResult =
			global::AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
				previousHealth,
				damagedHealth,
				candidate.MaxHealth,
				candidate.GetAcquiredUpgradeIds(),
				command.Input.HalfHealthRescueUsed,
				command.Input.QuarterHealthRescueUsed);
		candidate.SetHealth(damagedHealth, candidate.MaxHealth);
		global::AdventureHealingResult healingResult = ApplyCandidateHealing(
			candidate,
			rescueResult.HealAmount) ?? new global::AdventureHealingResult(
				candidate.CurrentHealth,
				candidate.MaxHealth,
				0,
				0,
				candidate.EffectState.HealingOverflowRemainder);
		return AdventureEvaluation.Changed(
			new AdventureDirectDamageCommandOutcome(
				previousHealth,
				damagedHealth,
				rescueResult,
				healingResult),
			forcePersist: true);
	}

	private static AdventureEvaluation EvaluateAdventureMiss(
		ResolveAdventureMissCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		IReadOnlyCollection<string> acquiredUpgradeIds = candidate.GetAcquiredUpgradeIds();
		global::AdventureUpgradeEffectService.ConsumeBorrowSuitSetAfterNumericBaseCardPlay(
			candidate.EffectState,
			command.Input.PlayedCardRank);
		global::AdventurePostCaptureEffectResult recoveryEffect =
			global::AdventureUpgradeEffectService.ResolveEnchantedRecoveryActionEffects(
				acquiredUpgradeIds,
				command.Input.TriggeredAttachedItemCardIds,
				successfulCapture: false);
		global::AdventureHealingResult? healingResult = ApplyCandidateHealing(
			candidate,
			recoveryEffect.HealAmount);
		int nextTurnHealBeforeMiss = candidate.EffectState.NextTurnHealAmount;
		int nextTurnDrawBeforeMiss = candidate.EffectState.NextTurnDrawCount;
		global::AdventureMainPlayerMissEffectResult effectResult =
			global::AdventureUpgradeEffectService.OnMainPlayerMissResolved(
				candidate.EffectState,
				acquiredUpgradeIds,
				command.Input.TableCardCountAfterMiss);
		bool commitBandageAtRealTurnEnd = candidate.HasAcquiredUpgrade("hearts_miss_bandage");
		if (commitBandageAtRealTurnEnd)
		{
			candidate.EffectState.NextTurnHealAmount = nextTurnHealBeforeMiss;
			candidate.EffectState.NextTurnDrawCount = nextTurnDrawBeforeMiss;
		}

		return AdventureEvaluation.Changed(
			new AdventureMissCommandOutcome(
				effectResult,
				recoveryEffect,
				healingResult,
				commitBandageAtRealTurnEnd),
			forcePersist: true);
	}

	private static AdventureEvaluation EvaluateAdventureCapture(
		ResolveAdventureCaptureCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive || command.Input.CaptureContext == null)
		{
			return AdventureEvaluation.InvalidStage;
		}
		global::AdventureCaptureCommitInput input = command.Input;
		IReadOnlyCollection<string> acquiredUpgradeIds = candidate.GetAcquiredUpgradeIds();
		if (input.ConsumeBorrowSuitSet)
		{
			global::AdventureUpgradeEffectService.ConsumeBorrowSuitSetAfterNumericBaseCardPlay(
				candidate.EffectState,
				input.PlayedCardRank);
		}
		if (input.ConsumeGapRooting)
		{
			_ = global::AdventureUpgradeEffectService.ResolveClubCaptureExtensions(
				candidate.EffectState,
				acquiredUpgradeIds,
				hasMiddleScoringCards: false,
				leftAdjacentBaseCardAvailable: true);
		}

		global::AdventureCaptureDamageResult damageResult =
			global::AdventureUpgradeEffectService.ApplyCaptureDamage(
				candidate.EffectState,
				acquiredUpgradeIds,
				input.DamageInput);
		global::AdventureLowHealthRescueResult rescueResult = default;
		bool consumedHalfHealthRescue = false;
		bool consumedQuarterHealthRescue = false;
		global::AdventureHealingResult? healingResult = null;
		if (!input.CapturerIsMainPlayer && damageResult.FinalDamage > 0)
		{
			int previousHealth = candidate.CurrentHealth;
			int damagedHealth = Math.Max(0, previousHealth - damageResult.FinalDamage);
			rescueResult = global::AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
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

		global::AdventurePostCaptureEffectResult postCaptureEffectResult = default;
		global::AdventureClubCaptureEffectResult clubCaptureEffectResult = default;
		if (input.CapturerIsMainPlayer)
		{
			postCaptureEffectResult = global::AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
				candidate.EffectState,
				acquiredUpgradeIds,
				damageResult.FinalDamage,
				input.SuccessfulCapturesThisTurn,
				input.CaptureContext);
			global::AdventureHealingResult? postCaptureHealing = ApplyCandidateHealing(
				candidate,
				postCaptureEffectResult.HealAmount);
			healingResult = postCaptureHealing ?? healingResult;
			clubCaptureEffectResult = global::AdventureUpgradeEffectService.ResolveClubCaptureEffects(
				candidate.EffectState,
				acquiredUpgradeIds,
				input.ClubCapture.CompleteBaseCaptureCardCount,
				input.ClubCapture.UniqueShortReturnCandidateCount,
				input.ClubCapture.UniqueCombinedReturnCandidateCount,
				input.ClubCapture.AvailableHandSlots,
				input.ClubCapture.PlayedCardWasBaseCard);
			candidate.EffectState.ClubMissClimbGrowthStacks = 0;
		}

		global::AdventureQuestionRewardEffectResult questionRewardEffectResult = default;
		for (int rewardIndex = 0; rewardIndex < Math.Max(0, input.PlannedQuestionRewardCount); rewardIndex++)
		{
			global::AdventureQuestionRewardEffectResult rewardEffect =
				global::AdventureUpgradeEffectService.OnQuestionRewardGranted(
					candidate.EffectState,
					acquiredUpgradeIds);
			questionRewardEffectResult = new global::AdventureQuestionRewardEffectResult(
				global::AdventureUpgradeEffectMath.SaturatingAddToInt(
					questionRewardEffectResult.BonusDirectDamage,
					rewardEffect.BonusDirectDamage),
				global::AdventureUpgradeEffectMath.SaturatingAddToInt(
					questionRewardEffectResult.HealAmount,
					rewardEffect.HealAmount),
				global::AdventureUpgradeEffectMath.SaturatingAddToInt(
					questionRewardEffectResult.DrawCount,
					rewardEffect.DrawCount));
			global::AdventureHealingResult? questionHealing = ApplyCandidateHealing(
				candidate,
				rewardEffect.HealAmount);
			healingResult = questionHealing ?? healingResult;
		}

		return AdventureEvaluation.Changed(
			new AdventureCaptureCommandOutcome(
				damageResult,
				postCaptureEffectResult,
				clubCaptureEffectResult,
				questionRewardEffectResult,
				healingResult,
				rescueResult,
				candidate.CurrentHealth,
				consumedHalfHealthRescue,
				consumedQuarterHealthRescue),
			forcePersist: true);
	}

	private static AdventureEvaluation EvaluateAdventureRealTurnEnd(
		ResolveAdventureRealTurnEndCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		if (command.CommitMissBandage && candidate.HasAcquiredUpgrade("hearts_miss_bandage"))
		{
			candidate.EffectState.NextTurnHealAmount =
				global::AdventureUpgradeEffectMath.SaturatingAddToInt(
					candidate.EffectState.NextTurnHealAmount,
					global::AdventureUpgradeRuntimeValues.GetNonNegativeInt(
						"hearts_miss_bandage",
						"amount",
						3));
			candidate.EffectState.NextTurnDrawCount =
				global::AdventureUpgradeEffectMath.SaturatingAddToInt(
					candidate.EffectState.NextTurnDrawCount,
					global::AdventureUpgradeRuntimeValues.GetNonNegativeInt(
						"hearts_miss_bandage",
						"count",
						1));
		}
		global::AdventureUpgradeEffectService.ClearTurnScopedState(candidate.EffectState);
		return AdventureEvaluation.Changed(forcePersist: true);
	}

	private static AdventureEvaluation EvaluateResetAdventureBattleEffects(
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive)
		{
			return AdventureEvaluation.InvalidStage;
		}
		global::AdventureUpgradeEffectService.ClearBattleScopedState(candidate.EffectState);
		return AdventureEvaluation.Changed(forcePersist: true);
	}

	private static AdventureEvaluation EvaluateClearAdventureRun(
		global::AdventureUpgradeRunState candidate)
	{
		candidate.Clear();
		return AdventureEvaluation.Changed(forcePersist: true);
	}

	private static AdventureEvaluation EvaluateAdventureVictory(
		CommitAdventureVictoryCommand command,
		global::AdventureUpgradeRunState candidate)
	{
		if (!candidate.IsActive
			|| !IsValidMainStage(command.CompletedMainStageIndex)
			|| candidate.MainStageIndex != command.CompletedMainStageIndex
			|| !string.Equals(candidate.EnemyAvatarId, command.EnemyAvatarId, StringComparison.Ordinal))
		{
			return AdventureEvaluation.InvalidStage;
		}

		if (command.CompletedMainStageIndex >= global::AdventureCampaignCatalog.MainStageCount)
		{
			candidate.Clear();
		}
		else
		{
			int nextStageIndex = command.CompletedMainStageIndex + 1;
			candidate.AdvanceToMainStage(
				nextStageIndex,
				global::AdventureCampaignCatalog.GetMainAvatarId(nextStageIndex));
			candidate.EffectState.ClearBattleScope();
		}

		return AdventureEvaluation.Changed(
			forcePersist: true,
			persistenceKind: AdventurePersistenceKind.MainAdventureVictory,
			completedMainStageIndex: command.CompletedMainStageIndex,
			enemyAvatarId: command.EnemyAvatarId);
	}

	private static global::AdventureHealingResult? ApplyCandidateHealing(
		global::AdventureUpgradeRunState candidate,
		int healAmount)
	{
		if (healAmount <= 0)
		{
			return null;
		}

		global::AdventureHealingResult result = global::AdventureUpgradeEffectService.ApplyHealing(
			candidate.CurrentHealth,
			candidate.MaxHealth,
			candidate.EffectState,
			candidate.GetAcquiredUpgradeIds(),
			healAmount);
		candidate.SetHealth(result.CurrentHealth, result.MaxHealth);
		return result;
	}

	private static bool IsValidMainStage(int mainStageIndex) =>
		mainStageIndex >= 1 && mainStageIndex <= global::AdventureCampaignCatalog.MainStageCount;

	private AdventureRuleCommandResult AcceptedAdventure(AdventureCommandOutcome? outcome)
	{
		return new AdventureRuleCommandResult(
			true,
			RuleRejection.None,
			global::AdventureRunWriteResult.Succeeded,
			_adventureRevision,
			_adventureReadModel,
			outcome);
	}

	private AdventureRuleCommandResult RejectedAdventure(
		RuleRejection rejection,
		global::AdventureRunWriteResult writeResult)
	{
		return new AdventureRuleCommandResult(
			false,
			rejection,
			writeResult,
			_adventureRevision,
			_adventureReadModel);
	}

	private void RefreshAdventureReadModel()
	{
		_adventureReadModel = AdventureRunReadModel.FromState(_adventureState, _adventureRevision);
		_readModel = _readModel with
		{
			Adventure = _adventureReadModel,
			MatchFlow = _state.MatchFlow,
		};
	}

	private sealed record AdventureEvaluation(
		bool Allowed,
		bool StateChanged,
		RuleRejection Rejection,
		global::AdventureRunWriteResult WriteResult,
		AdventureCommandOutcome? Outcome,
		bool ForcePersist,
		string UnlockedUpgradeId,
		AdventurePersistenceKind PersistenceKind,
		int CompletedMainStageIndex,
		string EnemyAvatarId)
	{
		public static AdventureEvaluation InvalidStage { get; } = Rejected(
			new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Adventure run is inactive or the stage is invalid."));

		public static AdventureEvaluation Rejected(RuleRejection rejection) => new(
			false,
			false,
			rejection,
			global::AdventureRunWriteResult.InvalidStage,
			null,
			false,
			string.Empty,
			AdventurePersistenceKind.RunState,
			0,
			string.Empty);

		public static AdventureEvaluation Unchanged(AdventureCommandOutcome? outcome = null) => new(
			true,
			false,
			RuleRejection.None,
			global::AdventureRunWriteResult.Succeeded,
			outcome,
			false,
			string.Empty,
			AdventurePersistenceKind.RunState,
			0,
			string.Empty);

		public static AdventureEvaluation Changed(
			AdventureCommandOutcome? outcome = null,
			bool forcePersist = false,
			string unlockedUpgradeId = "",
			AdventurePersistenceKind persistenceKind = AdventurePersistenceKind.RunState,
			int completedMainStageIndex = 0,
			string enemyAvatarId = "") => new(
			true,
			true,
			RuleRejection.None,
			global::AdventureRunWriteResult.Succeeded,
			outcome,
			forcePersist,
			unlockedUpgradeId,
			persistenceKind,
			completedMainStageIndex,
			enemyAvatarId);
	}
}
