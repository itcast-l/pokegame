using System;
using System.Collections.Generic;

using System;
using System.Collections.Generic;

public readonly record struct AdventureQuestionRewardEffectResult(
	int BonusDirectDamage,
	int HealAmount,
	int DrawCount);

public readonly record struct AdventureUpgradeAcquisitionEffectResult(
	int MaxHealthDelta,
	int HealAmount,
	int ImmediateDrawCount);

public readonly record struct AdventureHealingResult(
	int CurrentHealth,
	int MaxHealth,
	int ActualHealAmount,
	int MaxHealthIncrease,
	int OverflowRemainder);

public readonly record struct AdventureTurnStartEffectResult(int HealAmount, int DrawCount);

public readonly record struct AdventureCaptureDamageModifierSnapshot(
	int PermanentCaptureDamageBonus,
	bool QuestionRewardEchoActive,
	bool FinalDoubleCaptureDamageActive);

public readonly record struct AdventureCaptureDamageInput(
	int BaseDamage,
	int FlatDamageDelta = 0,
	bool IsFirstCaptureThisBattle = false,
	bool IncludesQuestionReward = false,
	bool QuestionRewardEchoActive = false,
	IReadOnlyList<int>? ScoringCardRawPoints = null,
	IReadOnlyList<int>? ScoringCardResolvedPoints = null,
	IReadOnlyList<int>? ScoringCardTableIndexes = null,
	bool CapturerIsMainPlayer = true,
	int TargetMaxHealth = 0,
	int EnemyDamageReduction = 0,
	AdventureCaptureDamageModifierSnapshot? FrozenModifiers = null,
	AdventureCaptureResolutionFacts? CaptureContext = null);

public readonly record struct AdventureCaptureDamageResult(
	int FinalDamage,
	int WholeCaptureMultiplier = 1,
	bool DamageCapped = false);

public readonly record struct AdventurePostCaptureEffectResult(
	int HealAmount,
	int DrawCount = 0,
	bool RevealRemainingHiddenCards = false);

public readonly record struct AdventureCaptureRewardApplicationPlan(
	int HealAmount,
	int DrawCount,
	int BonusDirectDamage,
	bool RevealRemainingHiddenCards);

public readonly record struct AdventureLowHealthRescueResult(
	int HealAmount,
	int DrawCount,
	bool ConsumedHalfThreshold,
	bool ConsumedQuarterThreshold);

public readonly record struct AdventureMainPlayerMissEffectResult(
	bool GrantExtraBaseCardAction,
	bool InsertPlayedCardAtLeftmost = false,
	bool ShuffleTableAfterMiss = false,
	bool SetCanopyRevealAnchor = false);
public readonly record struct AdventureClubCaptureEffectResult(
	bool ReturnPlayedCardToHand,
	bool ReturnTwoCapturedCardsToHand,
	string PlayedCardReturnSourceUpgradeId = "");
public readonly record struct AdventureClubPlayAdjustmentResult(
	string PrimaryRank,
	IReadOnlyList<string> AlternateRanks,
	bool CollectBySuitOnce = false,
	bool SuitMatchIsFallbackOnly = false);
public readonly record struct AdventureClubCaptureScoringResult(
	bool IncludeCaptureEndpointsInScoring);
public readonly record struct AdventureClubCaptureExtensionResult(
	bool CaptureLeftAdjacentBaseCard);
public readonly record struct AdventureEnemyCaptureEffectResult(
	bool NextCardFacesUp,
	AdventureEnemyNextCardInsertMode InsertMode);

public enum AdventureEnemyNextCardInsertMode
{
	None = 0,
	Leftmost = 1,
	BeforeTrackedOldest = 2,
}

public readonly record struct AdventureEnemyMissEffectResult(
	bool NextCardFacesUp,
	AdventureEnemyNextCardInsertMode InsertMode,
	int NextCaptureDamageDelta,
	int DirectDamageToEnemy,
	bool GrantMainPlayerExtraBaseAction = false);

public enum AdventureQuestionRejudgeRuleChoice
{
	Keep = 0,
	Reroll = 1,
}

public readonly record struct AdventureQuestionRejudgeRuleResult(
	bool Offered,
	bool ConsumesOpportunity,
	bool ShouldReroll);
