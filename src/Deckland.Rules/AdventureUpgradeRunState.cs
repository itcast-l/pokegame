using System;
using System.Collections.Generic;

public enum AdventureDefeatResolution
{
	RetryCurrentStage,
	RestartJourney,
}

internal sealed class AdventureUpgradeRunState
{
	private const int DefaultAdventurePlayerHealth = 100;
	private const int MaximumAdventurePlayerHealth = 150;
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
	internal string CampaignId { get; private set; } = string.Empty;
	internal int MainStageIndex { get; private set; }
	internal string EnemyAvatarId { get; private set; } = string.Empty;
	internal int CurrentAvatarStageNumber => AdventureCampaignCatalog.LegacyAvatarStageForMainStage(MainStageIndex);
	internal int CurrentRunStageIndex { get; private set; }
	internal bool ShouldGrantRunStartUpgrade { get; private set; }
	internal bool ShouldGrantVictoryUpgrade { get; private set; }
	internal int CurrentHealth { get; private set; } = DefaultAdventurePlayerHealth;
	internal int MaxHealth { get; private set; } = DefaultAdventurePlayerHealth;
	internal bool HasUsedRevive { get; private set; }
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
		AdventureUpgradeRuntimeValues.SetManifest(manifest);
		AcquiredUpgradeIds.RemoveWhere(id => !manifest.ContainsEnabledId(id));
		NormalizePersistentState();
	}

	internal void BeginNewRun(int avatarStageNumber)
	{
		if (AdventureCampaignCatalog.TryMapLegacyRunStage(avatarStageNumber, out int mainStageIndex))
		{
			BeginNewMainRun(mainStageIndex, AdventureCampaignCatalog.GetMainAvatarId(mainStageIndex));
			return;
		}

		BeginNewMainRun(avatarStageNumber, AdventureCampaignCatalog.GetMainAvatarId(avatarStageNumber));
	}

	internal void BeginNewMainRun(int mainStageIndex, string enemyAvatarId)
	{
		Clear();
		if (!AdventureUpgradeChoiceRules.IsMainAdventureStageIndex(mainStageIndex))
		{
			return;
		}

		string expectedAvatarId = AdventureCampaignCatalog.GetMainAvatarId(mainStageIndex);
		string normalizedEnemyAvatarId = enemyAvatarId?.Trim() ?? string.Empty;
		if (!string.Equals(normalizedEnemyAvatarId, expectedAvatarId, StringComparison.Ordinal))
		{
			normalizedEnemyAvatarId = expectedAvatarId;
		}

		IsActive = true;
		CampaignId = AdventureCampaignCatalog.MainCampaignId;
		MainStageIndex = mainStageIndex;
		EnemyAvatarId = normalizedEnemyAvatarId;
		CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndexFromMainStageIndex(mainStageIndex);
		ShouldGrantRunStartUpgrade = CurrentRunStageIndex == 1;
		ShouldGrantVictoryUpgrade = true;
	}

	internal void AdvanceToStage(int avatarStageNumber)
	{
		if (AdventureCampaignCatalog.TryMapLegacyRunStage(avatarStageNumber, out int mainStageIndex))
		{
			AdvanceToMainStage(mainStageIndex, AdventureCampaignCatalog.GetMainAvatarId(mainStageIndex));
			return;
		}

		AdvanceToMainStage(avatarStageNumber, AdventureCampaignCatalog.GetMainAvatarId(avatarStageNumber));
	}

	internal void AdvanceToMainStage(int mainStageIndex, string enemyAvatarId)
	{
		if (!IsActive)
		{
			BeginNewMainRun(mainStageIndex, enemyAvatarId);
			return;
		}

		if (!AdventureUpgradeChoiceRules.IsMainAdventureStageIndex(mainStageIndex))
		{
			return;
		}

		CampaignId = AdventureCampaignCatalog.MainCampaignId;
		MainStageIndex = mainStageIndex;
		EnemyAvatarId = AdventureCampaignCatalog.GetMainAvatarId(mainStageIndex);
		CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndexFromMainStageIndex(mainStageIndex);
		ShouldGrantRunStartUpgrade = false;
		ShouldGrantVictoryUpgrade = true;
	}

	internal AdventureDefeatResolution ResolveMainAdventureDefeat()
	{
		if (!IsActive)
		{
			BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1));
			return AdventureDefeatResolution.RestartJourney;
		}

		if (!HasUsedRevive)
		{
			HasUsedRevive = true;
			CurrentHealth = Math.Max(1, MaxHealth / 2);
			return AdventureDefeatResolution.RetryCurrentStage;
		}

		BeginNewMainRun(1, AdventureCampaignCatalog.GetMainAvatarId(1));
		return AdventureDefeatResolution.RestartJourney;
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
		return new List<string>(AcquiredUpgradeIds);
	}

	internal AdventureUpgradeRunState DeepClone()
	{
		var clone = new AdventureUpgradeRunState();
		clone.CopyFrom(this);
		return clone;
	}

	internal void CopyFrom(AdventureUpgradeRunState? other)
	{
		if (other == null)
		{
			Clear();
			return;
		}

		_manifest = other._manifest;
		IsActive = other.IsActive;
		CampaignId = other.CampaignId;
		MainStageIndex = other.MainStageIndex;
		EnemyAvatarId = other.EnemyAvatarId;
		CurrentRunStageIndex = other.CurrentRunStageIndex;
		ShouldGrantRunStartUpgrade = other.ShouldGrantRunStartUpgrade;
		ShouldGrantVictoryUpgrade = other.ShouldGrantVictoryUpgrade;
		CurrentHealth = other.CurrentHealth;
		MaxHealth = other.MaxHealth;
		HasUsedRevive = other.HasUsedRevive;
		AcquiredUpgradeIds.Clear();
		foreach (string upgradeId in other.AcquiredUpgradeIds)
		{
			AcquiredUpgradeIds.Add(upgradeId);
		}
		_effectState.CopyFrom(other._effectState);
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
			CampaignId,
			MainStageIndex,
			EnemyAvatarId,
			ShouldGrantRunStartUpgrade,
			ShouldGrantVictoryUpgrade,
			sortedUpgradeIds,
			CurrentHealth,
			MaxHealth,
			_effectState.PermanentCaptureDamageBonus,
			_effectState.QuestionRewardCount,
			_effectState.HealingOverflowRemainder,
			HasUsedRevive);
	}

	internal AdventureUpgradeRestoreResult RestoreFromSnapshot(AdventureUpgradeRunSnapshot snapshot)
	{
		if (!HasReadyManifest)
		{
			return AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
		}

		Clear();
		if (!IsSupportedSchemaVersion(snapshot.SchemaVersion)
			|| !TryResolveSnapshotIdentity(snapshot, out int mainStageIndex, out string enemyAvatarId)
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
		CampaignId = AdventureCampaignCatalog.MainCampaignId;
		MainStageIndex = mainStageIndex;
		EnemyAvatarId = enemyAvatarId;
		CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndexFromMainStageIndex(mainStageIndex);
		ShouldGrantRunStartUpgrade = snapshot.ShouldGrantRunStartUpgrade;
		ShouldGrantVictoryUpgrade = snapshot.ShouldGrantVictoryUpgrade;
		HasUsedRevive = snapshot.SchemaVersion >= 7 && snapshot.HasUsedRevive;
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
			AdventureUpgradeEffectState.MaxQuestionRewardCount);
		if (_effectState.QuestionRewardCount != requestedQuestionRewardCount)
		{
			migrated = true;
		}

		int requestedPermanentBonus = snapshot.SchemaVersion >= 2
			? snapshot.PermanentCaptureDamageBonus
			: 0;
		int maximumPermanentBonus = Math.Min(
			_effectState.QuestionRewardCount,
			AdventureUpgradeEffectState.MaxPermanentCaptureDamageBonus);
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
			? Math.Clamp(requestedOverflowRemainder, 0, AdventureUpgradeEffectState.MaxHealingOverflowRemainder)
			: 0;
		if (_effectState.HealingOverflowRemainder != requestedOverflowRemainder)
		{
			migrated = true;
		}

		_effectState.FinalDoubleCaptureDamageActive =
			HasAcquiredUpgrade(FinalDoubleUpgradeId)
			&& _effectState.QuestionRewardCount >= AdventureUpgradeEffectState.MaxQuestionRewardCount;
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
		CampaignId = string.Empty;
		MainStageIndex = 0;
		EnemyAvatarId = string.Empty;
		CurrentRunStageIndex = 0;
		ShouldGrantRunStartUpgrade = false;
		ShouldGrantVictoryUpgrade = false;
		HasUsedRevive = false;
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
			AdventureUpgradeEffectState.MaxQuestionRewardCount);
		int maximumPermanentBonus = Math.Min(
			_effectState.QuestionRewardCount,
			AdventureUpgradeEffectState.MaxPermanentCaptureDamageBonus);
		_effectState.PermanentCaptureDamageBonus = HasAcquiredUpgrade(QuestionMarkInterestUpgradeId)
			? Math.Clamp(_effectState.PermanentCaptureDamageBonus, 0, maximumPermanentBonus)
			: 0;
		_effectState.HealingOverflowRemainder = HasAcquiredUpgrade(OverflowGrowthUpgradeId)
			? Math.Clamp(_effectState.HealingOverflowRemainder, 0, AdventureUpgradeEffectState.MaxHealingOverflowRemainder)
			: 0;
		_effectState.FinalDoubleCaptureDamageActive =
			HasAcquiredUpgrade(FinalDoubleUpgradeId)
			&& _effectState.QuestionRewardCount >= AdventureUpgradeEffectState.MaxQuestionRewardCount;
	}

	private static bool IsSupportedSchemaVersion(int schemaVersion)
	{
		return schemaVersion >= 1
			&& schemaVersion <= AdventureUpgradeRunSnapshot.CurrentSchemaVersion;
	}

	private static bool TryResolveSnapshotIdentity(
		AdventureUpgradeRunSnapshot snapshot,
		out int mainStageIndex,
		out string enemyAvatarId)
	{
		mainStageIndex = 0;
		enemyAvatarId = string.Empty;
		if (snapshot.SchemaVersion < 6)
		{
			if (!AdventureCampaignCatalog.TryMapLegacyRunStage(
				snapshot.LegacyCurrentAvatarStageNumber != 0
					? snapshot.LegacyCurrentAvatarStageNumber
					: snapshot.CurrentAvatarStageNumber,
				out mainStageIndex))
			{
				return false;
			}

			enemyAvatarId = AdventureCampaignCatalog.GetMainAvatarId(mainStageIndex);
			return true;
		}

		if (!string.Equals(snapshot.CampaignId, AdventureCampaignCatalog.MainCampaignId, StringComparison.Ordinal)
			|| !AdventureUpgradeChoiceRules.IsMainAdventureStageIndex(snapshot.MainStageIndex))
		{
			return false;
		}

		string expectedAvatarId = AdventureCampaignCatalog.GetMainAvatarId(snapshot.MainStageIndex);
		if (!string.Equals(snapshot.EnemyAvatarId, expectedAvatarId, StringComparison.Ordinal))
		{
			return false;
		}

		mainStageIndex = snapshot.MainStageIndex;
		enemyAvatarId = expectedAvatarId;
		return true;
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
