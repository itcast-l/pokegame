using System;
using System.Collections.Generic;

using System;
using System.Collections.Generic;

internal readonly record struct AdventureQuestionRewardEffectResult(
	int BonusDirectDamage,
	int HealAmount,
	int DrawCount);

internal readonly record struct AdventureUpgradeAcquisitionEffectResult(
	int MaxHealthDelta,
	int HealAmount,
	int ImmediateDrawCount);

internal readonly record struct AdventureHealingResult(
	int CurrentHealth,
	int MaxHealth,
	int ActualHealAmount,
	int MaxHealthIncrease,
	int OverflowRemainder);

internal readonly record struct AdventureTurnStartEffectResult(int HealAmount, int DrawCount);

internal readonly record struct AdventureCaptureDamageModifierSnapshot(
	int PermanentCaptureDamageBonus,
	bool QuestionRewardEchoActive,
	bool FinalDoubleCaptureDamageActive);

internal readonly record struct AdventureCaptureDamageInput(
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
	CaptureResolutionContext? CaptureContext = null);

internal readonly record struct AdventureCaptureDamageResult(
	int FinalDamage,
	int WholeCaptureMultiplier = 1,
	bool DamageCapped = false);

internal readonly record struct AdventurePostCaptureEffectResult(
	int HealAmount,
	int DrawCount = 0,
	bool RevealRemainingHiddenCards = false);

internal readonly record struct AdventureCaptureRewardApplicationPlan(
	int HealAmount,
	int DrawCount,
	int BonusDirectDamage,
	bool RevealRemainingHiddenCards);

internal readonly record struct AdventureLowHealthRescueResult(
	int HealAmount,
	int DrawCount,
	bool ConsumedHalfThreshold,
	bool ConsumedQuarterThreshold);

internal readonly record struct AdventureMainPlayerMissEffectResult(
	bool GrantExtraBaseCardAction,
	bool InsertPlayedCardAtLeftmost = false,
	bool ShuffleTableAfterMiss = false,
	bool SetCanopyRevealAnchor = false);
internal readonly record struct AdventureClubCaptureEffectResult(
	bool ReturnPlayedCardToHand,
	bool ReturnTwoCapturedCardsToHand,
	string PlayedCardReturnSourceUpgradeId = "");
internal readonly record struct AdventureClubPlayAdjustmentResult(
	string PrimaryRank,
	IReadOnlyList<string> AlternateRanks,
	bool CollectBySuitOnce = false,
	bool SuitMatchIsFallbackOnly = false);
internal readonly record struct AdventureClubCaptureScoringResult(
	bool IncludeCaptureEndpointsInScoring);
internal readonly record struct AdventureClubCaptureExtensionResult(
	bool CaptureLeftAdjacentBaseCard);
internal readonly record struct AdventureEnemyCaptureEffectResult(
	bool NextCardFacesUp,
	AdventureEnemyNextCardInsertMode InsertMode);

internal enum AdventureEnemyNextCardInsertMode
{
	None = 0,
	Leftmost = 1,
	BeforeTrackedOldest = 2,
}

internal readonly record struct AdventureEnemyMissEffectResult(
	bool NextCardFacesUp,
	AdventureEnemyNextCardInsertMode InsertMode,
	int NextCaptureDamageDelta,
	int DirectDamageToEnemy,
	bool GrantMainPlayerExtraBaseAction = false);

internal enum AdventureQuestionRejudgeRuleChoice
{
	Keep = 0,
	Reroll = 1,
}

internal readonly record struct AdventureQuestionRejudgeRuleResult(
	bool Offered,
	bool ConsumesOpportunity,
	bool ShouldReroll);
