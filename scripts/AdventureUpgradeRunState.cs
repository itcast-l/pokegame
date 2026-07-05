using System;
using System.Collections.Generic;

internal sealed class AdventureUpgradeRunState
{
	private const int DefaultAdventurePlayerHealth = 100;
	private readonly HashSet<string> AcquiredUpgradeIds = new(StringComparer.Ordinal);
	private readonly AdventureUpgradeEffectState _effectState = new();

	internal bool IsActive { get; private set; }
	internal int CurrentAvatarStageNumber { get; private set; }
	internal int CurrentRunStageIndex { get; private set; }
	internal bool ShouldGrantRunStartUpgrade { get; private set; }
	internal bool ShouldGrantVictoryUpgrade { get; private set; }
	internal int CurrentHealth { get; private set; } = DefaultAdventurePlayerHealth;
	internal int MaxHealth { get; private set; } = DefaultAdventurePlayerHealth;
	internal AdventureUpgradeEffectState EffectState => _effectState;

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

	internal void AcquireUpgrade(string upgradeId)
	{
		if (string.IsNullOrWhiteSpace(upgradeId))
		{
			return;
		}

		AcquiredUpgradeIds.Add(upgradeId.Trim());
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
		int normalizedMaxHealth = Math.Max(1, maxHealth);
		MaxHealth = normalizedMaxHealth;
		CurrentHealth = Math.Clamp(currentHealth, 0, normalizedMaxHealth);
	}

	internal void ReplaceEffectState(AdventureUpgradeEffectState? effectState)
	{
		_effectState.CopyFrom(effectState);
	}

	internal AdventureUpgradeRunSnapshot ToSnapshot()
	{
		return new AdventureUpgradeRunSnapshot(
			AdventureUpgradeRunSnapshot.CurrentSchemaVersion,
			CurrentAvatarStageNumber,
			CurrentRunStageIndex,
			ShouldGrantRunStartUpgrade,
			ShouldGrantVictoryUpgrade,
			new List<string>(AcquiredUpgradeIds),
			CurrentHealth,
			MaxHealth,
			_effectState.PermanentCaptureDamageBonus,
			_effectState.QuestionRewardCount,
			_effectState.ThirdQuestionDamageDoubleActive,
			_effectState.HealingOverflowRemainder,
			_effectState.NextCaptureDamageBonus,
			_effectState.NextTurnHealAmount,
			_effectState.NextTurnDrawCount,
			_effectState.ClubShortBranchReturnUsedThisTurn,
			_effectState.ClubMissClimbGrowthStacks,
			_effectState.ClubBorrowSuitSetArmed,
			_effectState.ClubGapRootingUsedThisTurn);
	}

	internal void RestoreFromSnapshot(AdventureUpgradeRunSnapshot snapshot)
	{
		Clear();
		if ((snapshot.SchemaVersion != 1 && snapshot.SchemaVersion != AdventureUpgradeRunSnapshot.CurrentSchemaVersion)
			|| !AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(snapshot.CurrentAvatarStageNumber))
		{
			return;
		}

		IsActive = true;
		CurrentAvatarStageNumber = snapshot.CurrentAvatarStageNumber;
		CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndex(snapshot.CurrentAvatarStageNumber);
		ShouldGrantRunStartUpgrade = snapshot.ShouldGrantRunStartUpgrade;
		ShouldGrantVictoryUpgrade = snapshot.ShouldGrantVictoryUpgrade;
		foreach (string upgradeId in snapshot.AcquiredUpgradeIds)
		{
			AcquireUpgrade(upgradeId);
		}

		if (snapshot.SchemaVersion >= AdventureUpgradeRunSnapshot.CurrentSchemaVersion)
		{
			SetHealth(snapshot.CurrentHealth, snapshot.MaxHealth);
			_effectState.PermanentCaptureDamageBonus = snapshot.PermanentCaptureDamageBonus;
			_effectState.QuestionRewardCount = snapshot.QuestionRewardCount;
			_effectState.ThirdQuestionDamageDoubleActive = snapshot.ThirdQuestionDamageDoubleActive;
			_effectState.HealingOverflowRemainder = snapshot.HealingOverflowRemainder;
			_effectState.NextCaptureDamageBonus = snapshot.NextCaptureDamageBonus;
			_effectState.NextTurnHealAmount = snapshot.NextTurnHealAmount;
			_effectState.NextTurnDrawCount = snapshot.NextTurnDrawCount;
			_effectState.ClubShortBranchReturnUsedThisTurn = snapshot.ClubShortBranchReturnUsedThisTurn;
			_effectState.ClubMissClimbGrowthStacks = snapshot.ClubMissClimbGrowthStacks;
			_effectState.ClubBorrowSuitSetArmed = snapshot.ClubBorrowSuitSetArmed;
			_effectState.ClubGapRootingUsedThisTurn = snapshot.ClubGapRootingUsedThisTurn;
			return;
		}

		SetHealth(DefaultAdventurePlayerHealth, DefaultAdventurePlayerHealth);
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
}
