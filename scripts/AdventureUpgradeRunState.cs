using System;
using System.Collections.Generic;

internal sealed class AdventureUpgradeRunState
{
	private const int DefaultAdventurePlayerHealth = 100;
	private const int MaximumAdventurePlayerHealth = 150;
	private const int MaximumQuestionRewardCount = 10;
	private const int MaximumPermanentCaptureDamageBonus = 5;
	private const int MaximumHealingOverflowRemainder = 3;
	private const string QuestionMarkInterestUpgradeId = "diamonds_question_mark_interest";
	private const string QuestionMarkEchoUpgradeId = "diamonds_question_mark_echo";
	private const string FinalDoubleUpgradeId = "diamonds_final_double";
	private const string OverflowGrowthUpgradeId = "hearts_overflow_growth";

	private readonly HashSet<string> AcquiredUpgradeIds = new(StringComparer.Ordinal);
	private readonly AdventureUpgradeEffectState _effectState = new();
	private AdventureUpgradeManifest? _manifest;

	internal AdventureUpgradeRunState()
	{
	}

	internal AdventureUpgradeRunState(AdventureUpgradeManifest manifest)
	{
		SetManifest(manifest);
	}

	internal bool IsActive { get; private set; }
	internal bool HasReadyManifest => _manifest?.IsReady == true;
	internal int CurrentAvatarStageNumber { get; private set; }
	internal int CurrentRunStageIndex { get; private set; }
	internal bool ShouldGrantRunStartUpgrade { get; private set; }
	internal bool ShouldGrantVictoryUpgrade { get; private set; }
	internal int CurrentHealth { get; private set; } = DefaultAdventurePlayerHealth;
	internal int MaxHealth { get; private set; } = DefaultAdventurePlayerHealth;
	internal AdventureUpgradeEffectState EffectState => _effectState;
	internal bool IsQuestionRewardEchoActive =>
		HasAcquiredUpgrade(QuestionMarkEchoUpgradeId) && _effectState.QuestionRewardCount > 0;

	internal void SetManifest(AdventureUpgradeManifest manifest)
	{
		if (manifest == null || !manifest.IsReady)
		{
			return;
		}

		_manifest = manifest;
		AcquiredUpgradeIds.RemoveWhere(id => !manifest.ContainsEnabledId(id));
		NormalizePersistentState();
	}

	internal void BeginNewRun(int avatarStageNumber)
	{
		Clear();
		if (!AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(avatarStageNumber))
		{
			return;
		}

		IsActive = true;
		CurrentAvatarStageNumber = avatarStageNumber;
		CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndex(avatarStageNumber);
		ShouldGrantRunStartUpgrade = CurrentRunStageIndex == 1;
		ShouldGrantVictoryUpgrade = true;
	}

	internal void AdvanceToStage(int avatarStageNumber)
	{
		if (!IsActive)
		{
			BeginNewRun(avatarStageNumber);
			return;
		}

		if (!AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(avatarStageNumber))
		{
			return;
		}

		CurrentAvatarStageNumber = avatarStageNumber;
		CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndex(avatarStageNumber);
		ShouldGrantRunStartUpgrade = false;
		ShouldGrantVictoryUpgrade = true;
	}

	internal void MarkRunStartUpgradeGranted()
	{
		ShouldGrantRunStartUpgrade = false;
	}

	internal void MarkVictoryUpgradeGranted()
	{
		ShouldGrantVictoryUpgrade = false;
	}

	internal AdventureUpgradeAcquireResult AcquireUpgrade(string upgradeId)
	{
		if (!HasReadyManifest)
		{
			return AdventureUpgradeAcquireResult.DeferredManifestUnavailable;
		}

		if (!_manifest!.ContainsEnabledId(upgradeId))
		{
			return AdventureUpgradeAcquireResult.RejectedUnknownUpgrade;
		}

		string normalizedId = upgradeId.Trim();
		if (!AcquiredUpgradeIds.Add(normalizedId))
		{
			return AdventureUpgradeAcquireResult.AlreadyAcquired;
		}

		NormalizePersistentState();
		return AdventureUpgradeAcquireResult.Acquired;
	}

	internal bool HasAcquiredUpgrade(string upgradeId)
	{
		return !string.IsNullOrWhiteSpace(upgradeId) && AcquiredUpgradeIds.Contains(upgradeId.Trim());
	}

	internal IReadOnlyCollection<string> GetAcquiredUpgradeIds()
	{
		return AcquiredUpgradeIds;
	}

	internal void SetHealth(int currentHealth, int maxHealth)
	{
		int normalizedMaxHealth = Math.Clamp(maxHealth, 1, MaximumAdventurePlayerHealth);
		MaxHealth = normalizedMaxHealth;
		CurrentHealth = Math.Clamp(currentHealth, 0, normalizedMaxHealth);
	}

	internal void ReplaceEffectState(AdventureUpgradeEffectState? effectState)
	{
		_effectState.CopyFrom(effectState);
		NormalizePersistentState();
	}

	internal AdventureUpgradeRunSnapshot ToSnapshot()
	{
		NormalizePersistentState();
		var sortedUpgradeIds = new List<string>(AcquiredUpgradeIds);
		sortedUpgradeIds.Sort(StringComparer.Ordinal);
		return new AdventureUpgradeRunSnapshot(
			AdventureUpgradeRunSnapshot.CurrentSchemaVersion,
			CurrentAvatarStageNumber,
			CurrentRunStageIndex: 0,
			ShouldGrantRunStartUpgrade,
			ShouldGrantVictoryUpgrade,
			sortedUpgradeIds,
			CurrentHealth,
			MaxHealth,
			_effectState.PermanentCaptureDamageBonus,
			_effectState.QuestionRewardCount,
			FinalDoubleCaptureDamageActive: false,
			_effectState.HealingOverflowRemainder);
	}

	internal AdventureUpgradeRestoreResult RestoreFromSnapshot(AdventureUpgradeRunSnapshot snapshot)
	{
		if (!HasReadyManifest)
		{
			return AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
		}

		Clear();
		if (!IsSupportedSchemaVersion(snapshot.SchemaVersion)
			|| !AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(snapshot.CurrentAvatarStageNumber)
			|| snapshot.AcquiredUpgradeIds == null)
		{
			return AdventureUpgradeRestoreResult.InvalidSnapshotPreserved;
		}

		bool migrated = snapshot.SchemaVersion != AdventureUpgradeRunSnapshot.CurrentSchemaVersion;
		var normalizedUpgradeIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (string? upgradeId in snapshot.AcquiredUpgradeIds)
		{
			if (string.IsNullOrWhiteSpace(upgradeId)
				|| !_manifest!.CanRestoreUpgradeId(upgradeId, snapshot.SchemaVersion))
			{
				migrated = true;
				continue;
			}

			string normalizedId = upgradeId.Trim();
			if (!normalizedUpgradeIds.Add(normalizedId)
				|| !string.Equals(upgradeId, normalizedId, StringComparison.Ordinal))
			{
				migrated = true;
			}
		}

		var sortedUpgradeIds = new List<string>(normalizedUpgradeIds);
		sortedUpgradeIds.Sort(StringComparer.Ordinal);
		if (!SequenceMatches(snapshot.AcquiredUpgradeIds, sortedUpgradeIds))
		{
			migrated = true;
		}

		IsActive = true;
		CurrentAvatarStageNumber = snapshot.CurrentAvatarStageNumber;
		CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndex(snapshot.CurrentAvatarStageNumber);
		ShouldGrantRunStartUpgrade = snapshot.ShouldGrantRunStartUpgrade;
		ShouldGrantVictoryUpgrade = snapshot.ShouldGrantVictoryUpgrade;
		foreach (string upgradeId in sortedUpgradeIds)
		{
			AcquiredUpgradeIds.Add(upgradeId);
		}

		int requestedMaxHealth = snapshot.SchemaVersion >= 2
			? snapshot.MaxHealth
			: DefaultAdventurePlayerHealth;
		int requestedCurrentHealth = snapshot.SchemaVersion >= 2
			? snapshot.CurrentHealth
			: DefaultAdventurePlayerHealth;
		SetHealth(requestedCurrentHealth, requestedMaxHealth);
		if (MaxHealth != requestedMaxHealth || CurrentHealth != requestedCurrentHealth)
		{
			migrated = true;
		}

		int requestedQuestionRewardCount = snapshot.SchemaVersion >= 2
			? snapshot.QuestionRewardCount
			: 0;
		_effectState.QuestionRewardCount = Math.Clamp(
			requestedQuestionRewardCount,
			0,
			MaximumQuestionRewardCount);
		if (_effectState.QuestionRewardCount != requestedQuestionRewardCount)
		{
			migrated = true;
		}

		int requestedPermanentBonus = snapshot.SchemaVersion >= 2
			? snapshot.PermanentCaptureDamageBonus
			: 0;
		int maximumPermanentBonus = Math.Min(
			_effectState.QuestionRewardCount,
			MaximumPermanentCaptureDamageBonus);
		_effectState.PermanentCaptureDamageBonus = HasAcquiredUpgrade(QuestionMarkInterestUpgradeId)
			? Math.Clamp(requestedPermanentBonus, 0, maximumPermanentBonus)
			: 0;
		if (_effectState.PermanentCaptureDamageBonus != requestedPermanentBonus)
		{
			migrated = true;
		}

		int requestedOverflowRemainder = snapshot.SchemaVersion >= 2
			? snapshot.HealingOverflowRemainder
			: 0;
		_effectState.HealingOverflowRemainder = HasAcquiredUpgrade(OverflowGrowthUpgradeId)
			? Math.Clamp(requestedOverflowRemainder, 0, MaximumHealingOverflowRemainder)
			: 0;
		if (_effectState.HealingOverflowRemainder != requestedOverflowRemainder)
		{
			migrated = true;
		}

		_effectState.FinalDoubleCaptureDamageActive =
			HasAcquiredUpgrade(FinalDoubleUpgradeId)
			&& _effectState.QuestionRewardCount >= MaximumQuestionRewardCount;
		if (HasLegacyTransientState(snapshot))
		{
			migrated = true;
		}

		return migrated
			? AdventureUpgradeRestoreResult.Migrated
			: AdventureUpgradeRestoreResult.Restored;
	}

	internal void Clear()
	{
		IsActive = false;
		CurrentAvatarStageNumber = 0;
		CurrentRunStageIndex = 0;
		ShouldGrantRunStartUpgrade = false;
		ShouldGrantVictoryUpgrade = false;
		CurrentHealth = DefaultAdventurePlayerHealth;
		MaxHealth = DefaultAdventurePlayerHealth;
		AcquiredUpgradeIds.Clear();
		_effectState.Clear();
	}

	private void NormalizePersistentState()
	{
		SetHealth(CurrentHealth, MaxHealth);
		_effectState.Normalize();
		_effectState.QuestionRewardCount = Math.Clamp(
			_effectState.QuestionRewardCount,
			0,
			MaximumQuestionRewardCount);
		int maximumPermanentBonus = Math.Min(
			_effectState.QuestionRewardCount,
			MaximumPermanentCaptureDamageBonus);
		_effectState.PermanentCaptureDamageBonus = HasAcquiredUpgrade(QuestionMarkInterestUpgradeId)
			? Math.Clamp(_effectState.PermanentCaptureDamageBonus, 0, maximumPermanentBonus)
			: 0;
		_effectState.HealingOverflowRemainder = HasAcquiredUpgrade(OverflowGrowthUpgradeId)
			? Math.Clamp(_effectState.HealingOverflowRemainder, 0, MaximumHealingOverflowRemainder)
			: 0;
		_effectState.FinalDoubleCaptureDamageActive =
			HasAcquiredUpgrade(FinalDoubleUpgradeId)
			&& _effectState.QuestionRewardCount >= MaximumQuestionRewardCount;
	}

	private static bool IsSupportedSchemaVersion(int schemaVersion)
	{
		return schemaVersion >= 1
			&& schemaVersion <= AdventureUpgradeRunSnapshot.CurrentSchemaVersion;
	}

	private static bool SequenceMatches(
		IReadOnlyList<string> original,
		IReadOnlyList<string> normalized)
	{
		if (original.Count != normalized.Count)
		{
			return false;
		}

		for (int index = 0; index < original.Count; index++)
		{
			if (!string.Equals(original[index], normalized[index], StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;
	}

	private static bool HasLegacyTransientState(AdventureUpgradeRunSnapshot snapshot)
	{
		return snapshot.CurrentRunStageIndex != 0
			|| snapshot.FinalDoubleCaptureDamageActive
			|| snapshot.NextCaptureDamageBonus != 0
			|| snapshot.NextTurnHealAmount != 0
			|| snapshot.NextTurnDrawCount != 0
			|| snapshot.ClubShortBranchReturnUsedThisTurn
			|| snapshot.ClubMissClimbGrowthStacks != 0
			|| snapshot.ClubBorrowSuitSetArmed
			|| snapshot.ClubGapRootingUsedThisTurn
			|| snapshot.FinalDoubleQuestionRewardCountThisBattle != 0
			|| snapshot.ClubFlourishStacks != 0;
	}
}
