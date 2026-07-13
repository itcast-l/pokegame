using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal enum AdventureEffectStateScope
{
	Action = 0,
	Turn = 1,
	NextTurnCarry = 2,
	Battle = 3,
	Run = 4,
}

internal readonly record struct AdventureUpgradeEffectStateFieldMetadata(
	AdventureEffectStateScope Scope,
	int MinimumValue,
	int MaximumValue);

internal sealed class AdventureUpgradeEffectState
{
	internal static int MaxQuestionRewardCount =>
		AdventureUpgradeRuntimeValues.GetPositiveInt("diamonds_question_mark_windfall", "maximum");
	internal static int MaxPermanentCaptureDamageBonus =>
		AdventureUpgradeRuntimeValues.GetPositiveInt("diamonds_question_mark_interest", "maximum");
	internal static int MaxHealingOverflowRemainder =>
		AdventureUpgradeRuntimeValues.GetPositiveInt("hearts_overflow_growth", "stack_limit");
	internal static int MaxClubMissClimbGrowthStacks =>
		AdventureUpgradeRuntimeValues.GetPositiveInt("clubs_miss_climb_growth", "stack_limit");

	private static readonly IReadOnlyDictionary<string, AdventureUpgradeEffectStateFieldMetadata> Metadata =
		new ReadOnlyDictionary<string, AdventureUpgradeEffectStateFieldMetadata>(
			new Dictionary<string, AdventureUpgradeEffectStateFieldMetadata>(StringComparer.Ordinal)
			{
				[nameof(PermanentCaptureDamageBonus)] = RunField(MaxPermanentCaptureDamageBonus),
				[nameof(QuestionRewardCount)] = RunField(MaxQuestionRewardCount),
				[nameof(FinalDoubleQuestionRewardCountThisBattle)] = BattleField(0),
				[nameof(FinalDoubleCaptureDamageActive)] = RunField(1),
				[nameof(HealingOverflowRemainder)] = RunField(MaxHealingOverflowRemainder),
				[nameof(NextCaptureDamageBonus)] = BattleField(int.MaxValue),
				[nameof(NextTurnHealAmount)] = NextTurnCarryField(int.MaxValue),
				[nameof(NextTurnDrawCount)] = NextTurnCarryField(int.MaxValue),
				[nameof(ClubShortBranchReturnUsedThisTurn)] = TurnField(1),
				[nameof(ClubMissClimbGrowthStacks)] = BattleField(MaxClubMissClimbGrowthStacks),
				[nameof(ClubBorrowSuitSetArmed)] = TurnField(1),
				[nameof(ClubGapRootingUsedThisTurn)] = TurnField(1),
			});

	internal static IReadOnlyDictionary<string, AdventureUpgradeEffectStateFieldMetadata> FieldMetadata => Metadata;

	internal int PermanentCaptureDamageBonus { get; set; }
	internal int QuestionRewardCount { get; set; }

	// ABI placeholder for v1-v4 snapshots. Final-double progress is derived from the run counter.
	internal int FinalDoubleQuestionRewardCountThisBattle
	{
		get => 0;
		set { }
	}
	internal bool FinalDoubleCaptureDamageActive { get; set; }
	internal int HealingOverflowRemainder { get; set; }
	internal int NextCaptureDamageBonus { get; set; }
	internal int NextTurnHealAmount { get; set; }
	internal int NextTurnDrawCount { get; set; }
	internal bool ClubShortBranchReturnUsedThisTurn { get; set; }
	internal int ClubMissClimbGrowthStacks { get; set; }
	internal bool ClubBorrowSuitSetArmed { get; set; }
	internal bool ClubGapRootingUsedThisTurn { get; set; }

	internal void Normalize()
	{
		PermanentCaptureDamageBonus = Math.Clamp(
			PermanentCaptureDamageBonus,
			0,
			MaxPermanentCaptureDamageBonus);
		QuestionRewardCount = Math.Clamp(QuestionRewardCount, 0, MaxQuestionRewardCount);
		FinalDoubleCaptureDamageActive = FinalDoubleCaptureDamageActive
			&& QuestionRewardCount >= MaxQuestionRewardCount;
		HealingOverflowRemainder = Math.Clamp(
			HealingOverflowRemainder,
			0,
			MaxHealingOverflowRemainder);
		NextCaptureDamageBonus = Math.Max(0, NextCaptureDamageBonus);
		NextTurnHealAmount = Math.Max(0, NextTurnHealAmount);
		NextTurnDrawCount = Math.Max(0, NextTurnDrawCount);
		ClubMissClimbGrowthStacks = Math.Clamp(
			ClubMissClimbGrowthStacks,
			0,
			MaxClubMissClimbGrowthStacks);
	}

	internal void ClearActionScope()
	{
	}

	internal void ClearTurnScope()
	{
		ClubShortBranchReturnUsedThisTurn = false;
		ClubBorrowSuitSetArmed = false;
		ClubGapRootingUsedThisTurn = false;
	}

	internal void ClearNextTurnCarryScope()
	{
		NextTurnHealAmount = 0;
		NextTurnDrawCount = 0;
	}

	internal void ClearBattleScope()
	{
		ClearActionScope();
		ClearTurnScope();
		ClearNextTurnCarryScope();
		NextCaptureDamageBonus = 0;
		ClubMissClimbGrowthStacks = 0;
	}

	internal void Clear()
	{
		PermanentCaptureDamageBonus = 0;
		QuestionRewardCount = 0;
		FinalDoubleCaptureDamageActive = false;
		HealingOverflowRemainder = 0;
		ClearBattleScope();
	}

	internal void ResetFinalDoubleProgressForNewEnemy()
	{
		// Final-double progress is run scoped. Stage changes must not clear it.
		Normalize();
	}

	internal void CopyFrom(AdventureUpgradeEffectState? other)
	{
		if (other == null)
		{
			Clear();
			return;
		}

		PermanentCaptureDamageBonus = other.PermanentCaptureDamageBonus;
		QuestionRewardCount = other.QuestionRewardCount;
		FinalDoubleCaptureDamageActive = other.FinalDoubleCaptureDamageActive;
		HealingOverflowRemainder = other.HealingOverflowRemainder;
		NextCaptureDamageBonus = other.NextCaptureDamageBonus;
		NextTurnHealAmount = other.NextTurnHealAmount;
		NextTurnDrawCount = other.NextTurnDrawCount;
		ClubShortBranchReturnUsedThisTurn = other.ClubShortBranchReturnUsedThisTurn;
		ClubMissClimbGrowthStacks = other.ClubMissClimbGrowthStacks;
		ClubBorrowSuitSetArmed = other.ClubBorrowSuitSetArmed;
		ClubGapRootingUsedThisTurn = other.ClubGapRootingUsedThisTurn;
		Normalize();
	}

	internal AdventureUpgradeEffectState Clone()
	{
		var copy = new AdventureUpgradeEffectState();
		copy.CopyFrom(this);
		return copy;
	}

	private static AdventureUpgradeEffectStateFieldMetadata RunField(int maximumValue)
	{
		return new AdventureUpgradeEffectStateFieldMetadata(AdventureEffectStateScope.Run, 0, maximumValue);
	}

	private static AdventureUpgradeEffectStateFieldMetadata BattleField(int maximumValue)
	{
		return new AdventureUpgradeEffectStateFieldMetadata(AdventureEffectStateScope.Battle, 0, maximumValue);
	}

	private static AdventureUpgradeEffectStateFieldMetadata TurnField(int maximumValue)
	{
		return new AdventureUpgradeEffectStateFieldMetadata(AdventureEffectStateScope.Turn, 0, maximumValue);
	}

	private static AdventureUpgradeEffectStateFieldMetadata NextTurnCarryField(int maximumValue)
	{
		return new AdventureUpgradeEffectStateFieldMetadata(AdventureEffectStateScope.NextTurnCarry, 0, maximumValue);
	}
}
