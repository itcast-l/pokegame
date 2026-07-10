internal sealed class AdventureUpgradeEffectState
{
	internal int PermanentCaptureDamageBonus { get; set; }
	internal int QuestionRewardCount { get; set; }
	internal int FinalDoubleQuestionRewardCountThisBattle { get; set; }
	internal bool FinalDoubleCaptureDamageActive { get; set; }
	internal int HealingOverflowRemainder { get; set; }
	internal int NextCaptureDamageBonus { get; set; }
	internal int NextTurnHealAmount { get; set; }
	internal int NextTurnDrawCount { get; set; }
	internal bool ClubShortBranchReturnUsedThisTurn { get; set; }
	internal int ClubMissClimbGrowthStacks { get; set; }
	internal bool ClubBorrowSuitSetArmed { get; set; }
	internal bool ClubGapRootingUsedThisTurn { get; set; }

	internal void Clear()
	{
		PermanentCaptureDamageBonus = 0;
		QuestionRewardCount = 0;
		FinalDoubleQuestionRewardCountThisBattle = 0;
		FinalDoubleCaptureDamageActive = false;
		HealingOverflowRemainder = 0;
		NextCaptureDamageBonus = 0;
		NextTurnHealAmount = 0;
		NextTurnDrawCount = 0;
		ClubShortBranchReturnUsedThisTurn = false;
		ClubMissClimbGrowthStacks = 0;
		ClubBorrowSuitSetArmed = false;
		ClubGapRootingUsedThisTurn = false;
	}

	internal void ResetFinalDoubleProgressForNewEnemy()
	{
		FinalDoubleQuestionRewardCountThisBattle = 0;
		FinalDoubleCaptureDamageActive = false;
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
		FinalDoubleQuestionRewardCountThisBattle = other.FinalDoubleQuestionRewardCountThisBattle;
		FinalDoubleCaptureDamageActive = other.FinalDoubleCaptureDamageActive;
		HealingOverflowRemainder = other.HealingOverflowRemainder;
		NextCaptureDamageBonus = other.NextCaptureDamageBonus;
		NextTurnHealAmount = other.NextTurnHealAmount;
		NextTurnDrawCount = other.NextTurnDrawCount;
		ClubShortBranchReturnUsedThisTurn = other.ClubShortBranchReturnUsedThisTurn;
		ClubMissClimbGrowthStacks = other.ClubMissClimbGrowthStacks;
		ClubBorrowSuitSetArmed = other.ClubBorrowSuitSetArmed;
		ClubGapRootingUsedThisTurn = other.ClubGapRootingUsedThisTurn;
	}

	internal AdventureUpgradeEffectState Clone()
	{
		var copy = new AdventureUpgradeEffectState();
		copy.CopyFrom(this);
		return copy;
	}
}
