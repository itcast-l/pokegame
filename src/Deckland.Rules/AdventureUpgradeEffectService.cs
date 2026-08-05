using System;
using System.Collections.Generic;

internal static class AdventureUpgradeEffectService
{
	internal const int MaxRunHealth = 150;

internal static AdventureCaptureRewardApplicationPlan
		BuildCaptureRewardApplicationPlan(
			AdventureQuestionRewardEffectResult? questionRewardEffects,
			AdventurePostCaptureEffectResult? postCaptureEffects,
			int currentHandCount,
			int maximumHandCount,
			int availableDeckCardCount)
{
		return AdventureUpgradeCaptureEffects.BuildCaptureRewardApplicationPlan(questionRewardEffects, postCaptureEffects, currentHandCount, maximumHandCount, availableDeckCardCount);
	}

internal static int CountAcquiredSuitUpgrades(
		IEnumerable<string>? acquiredUpgradeIds,
		string suit)
{
		return AdventureUpgradeEffectMath.CountAcquiredSuitUpgrades(acquiredUpgradeIds, suit);
	}

internal static bool HasBlackLedger(IEnumerable<string> acquiredUpgradeIds)
{
		return AdventureUpgradeCaptureEffects.HasBlackLedger(acquiredUpgradeIds);
	}

internal static bool ShouldOfferQuestionRewardRejudge(
		IEnumerable<string> acquiredUpgradeIds,
		bool questionRejudgeUsedThisBattle)
{
		return AdventureUpgradeQuestionEffects.ShouldOfferQuestionRewardRejudge(acquiredUpgradeIds, questionRejudgeUsedThisBattle);
	}

internal static AdventureQuestionRejudgeRuleResult ResolveQuestionRewardRejudgeChoice(
		IEnumerable<string> acquiredUpgradeIds,
		bool questionRejudgeUsedThisBattle,
		AdventureQuestionRejudgeRuleChoice choice)
{
		return AdventureUpgradeQuestionEffects.ResolveQuestionRewardRejudgeChoice(acquiredUpgradeIds, questionRejudgeUsedThisBattle, choice);
	}

internal static AdventureUpgradeEffectState CloneState(AdventureUpgradeEffectState? state)
{
		return AdventureUpgradeEffectMath.CloneState(state);
	}

internal static AdventureCaptureDamageModifierSnapshot FreezeCaptureDamageModifiers(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds)
{
		return AdventureUpgradeCaptureEffects.FreezeCaptureDamageModifiers(state, acquiredUpgradeIds);
	}

internal static AdventureQuestionRewardEffectResult OnQuestionRewardGranted(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds)
{
		return AdventureUpgradeQuestionEffects.OnQuestionRewardGranted(state, acquiredUpgradeIds);
	}

internal static AdventureUpgradeAcquisitionEffectResult OnUpgradeAcquired(
		AdventureUpgradeEffectState state,
		string upgradeId,
		bool triggerBattleStartBonusImmediately)
{
		return AdventureUpgradeQuestionEffects.OnUpgradeAcquired(state, upgradeId, triggerBattleStartBonusImmediately);
	}

internal static void ClearBattleScopedState(AdventureUpgradeEffectState state)
{
		AdventureUpgradeTurnEffects.ClearBattleScopedState(state);
	}

internal static void ClearTurnScopedState(AdventureUpgradeEffectState state)
{
		AdventureUpgradeTurnEffects.ClearTurnScopedState(state);
	}

internal static int GetBattleStartDrawCount(IEnumerable<string> acquiredUpgradeIds)
{
		return AdventureUpgradeTurnEffects.GetBattleStartDrawCount(acquiredUpgradeIds);
	}

internal static AdventureMainPlayerMissEffectResult OnMainPlayerMissResolved(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		int tableCardCountAfterMiss)
{
		return AdventureUpgradeTurnEffects.OnMainPlayerMissResolved(state, acquiredUpgradeIds, tableCardCountAfterMiss);
	}

internal static AdventureClubPlayAdjustmentResult ResolveClubPlayAdjustments(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		string playedCardRank)
{
		return AdventureUpgradeCaptureEffects.ResolveClubPlayAdjustments(state, acquiredUpgradeIds, playedCardRank);
	}

internal static AdventureClubCaptureScoringResult ResolveClubCaptureScoring(
		IEnumerable<string> acquiredUpgradeIds)
{
		return AdventureUpgradeCaptureEffects.ResolveClubCaptureScoring(acquiredUpgradeIds);
	}

internal static AdventureClubCaptureExtensionResult ResolveClubCaptureExtensions(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		bool hasMiddleScoringCards,
		bool leftAdjacentBaseCardAvailable)
{
		return AdventureUpgradeCaptureEffects.ResolveClubCaptureExtensions(state, acquiredUpgradeIds, hasMiddleScoringCards, leftAdjacentBaseCardAvailable);
	}

internal static bool ShouldGrantLinkedStemsQuestionReward(
		IEnumerable<string> acquiredUpgradeIds,
		int successfulCapturesThisTurn)
{
		return AdventureUpgradeCaptureEffects.ShouldGrantLinkedStemsQuestionReward(acquiredUpgradeIds, successfulCapturesThisTurn);
	}

internal static void ConsumeBorrowSuitSetAfterNumericBaseCardPlay(
		AdventureUpgradeEffectState state,
		string playedCardRank)
{
		AdventureUpgradeCaptureEffects.ConsumeBorrowSuitSetAfterNumericBaseCardPlay(state, playedCardRank);
	}

internal static AdventureClubCaptureEffectResult ResolveClubCaptureEffects(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		int completeBaseCaptureCardCount,
		int uniqueShortReturnCandidateCount,
		int uniqueCombinedReturnCandidateCount,
		int availableHandSlots,
		bool playedCardWasBaseCard)
{
		return AdventureUpgradeCaptureEffects.ResolveClubCaptureEffects(state, acquiredUpgradeIds, completeBaseCaptureCardCount, uniqueShortReturnCandidateCount, uniqueCombinedReturnCandidateCount, availableHandSlots, playedCardWasBaseCard);
	}

internal static AdventureTurnStartEffectResult ConsumeMainPlayerTurnStartEffects(AdventureUpgradeEffectState state)
{
		return AdventureUpgradeTurnEffects.ConsumeMainPlayerTurnStartEffects(state);
	}

internal static AdventureHealingResult ApplyHealing(
		int currentHealth,
		int maxHealth,
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		int healAmount)
{
		return AdventureUpgradeTurnEffects.ApplyHealing(currentHealth, maxHealth, state, acquiredUpgradeIds, healAmount);
	}

internal static AdventureCaptureDamageResult ApplyCaptureDamage(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		AdventureCaptureDamageInput input)
{
		return AdventureUpgradeCaptureEffects.ApplyCaptureDamage(state, acquiredUpgradeIds, input);
	}

internal static AdventurePostCaptureEffectResult OnMainPlayerCaptureResolved(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		int finalDamage,
		int successfulCapturesThisTurn,
			AdventureCaptureResolutionFacts captureContext)
{
		return AdventureUpgradeCaptureEffects.OnMainPlayerCaptureResolved(state, acquiredUpgradeIds, finalDamage, successfulCapturesThisTurn, captureContext);
	}

internal static AdventurePostCaptureEffectResult ResolveEnchantedRecoveryActionEffects(
		IEnumerable<string> acquiredUpgradeIds,
		IReadOnlyCollection<string>? triggeredAttachedItemCardIds,
		bool successfulCapture)
{
		return AdventureUpgradeCaptureEffects.ResolveEnchantedRecoveryActionEffects(acquiredUpgradeIds, triggeredAttachedItemCardIds, successfulCapture);
	}

internal static AdventureLowHealthRescueResult ResolveMainPlayerLowHealthRescue(
		int previousHealth,
		int currentHealth,
		int maxHealth,
		IEnumerable<string> acquiredUpgradeIds,
		bool halfThresholdUsed,
		bool quarterThresholdUsed)
{
		return AdventureUpgradeTurnEffects.ResolveMainPlayerLowHealthRescue(previousHealth, currentHealth, maxHealth, acquiredUpgradeIds, halfThresholdUsed, quarterThresholdUsed);
	}

internal static AdventureEnemyMissEffectResult ResolveEnemyMissEffects(
		IEnumerable<string> acquiredUpgradeIds,
		int tableCardCountAfterMiss,
		bool hadOldTableCardsBeforeMiss)
{
		return AdventureUpgradeEnemyEffects.ResolveEnemyMissEffects(acquiredUpgradeIds, tableCardCountAfterMiss, hadOldTableCardsBeforeMiss);
	}

internal static AdventureEnemyCaptureEffectResult ResolveEnemyCaptureEffects(
		IEnumerable<string> acquiredUpgradeIds)
{
		return AdventureUpgradeEnemyEffects.ResolveEnemyCaptureEffects(acquiredUpgradeIds);
	}

internal static int ResolveOldTableCaptureDamageDelta(
		IEnumerable<string> acquiredUpgradeIds,
		bool capturingPlayerIsMain,
		int capturedOldScoringCardCount,
		bool capturedTraceMark,
		bool capturedHuntMark)
{
		return AdventureUpgradeEnemyEffects.ResolveOldTableCaptureDamageDelta(acquiredUpgradeIds, capturingPlayerIsMain, capturedOldScoringCardCount, capturedTraceMark, capturedHuntMark);
	}

internal static int ResolveBlackLedgerRedirectTableCardIndex(
		bool blackLedgerActive,
		bool capturingPlayerIsMain,
		IReadOnlyList<int> tableCardScoreValues,
		int captureStartIndex)
{
		return AdventureUpgradeCaptureEffects.ResolveBlackLedgerRedirectTableCardIndex(blackLedgerActive, capturingPlayerIsMain, tableCardScoreValues, captureStartIndex);
	}

internal static bool ShouldPreserveEnemyCaptureTailCard(
		IEnumerable<string> acquiredUpgradeIds,
		int totalCapturedCardCount)
{
		return AdventureUpgradeEnemyEffects.ShouldPreserveEnemyCaptureTailCard(acquiredUpgradeIds, totalCapturedCardCount);
	}
}
