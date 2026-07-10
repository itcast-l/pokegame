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

internal readonly record struct AdventureCaptureDamageInput(
	int BaseDamage,
	int FlatDamageDelta = 0,
	bool IsFirstCaptureThisBattle = false,
	bool IncludesQuestionReward = false,
	bool QuestionRewardEchoActive = false,
	IReadOnlyList<int>? ScoringCardRawPoints = null,
	IReadOnlyList<int>? ScoringCardResolvedPoints = null);

internal readonly record struct AdventureCaptureDamageResult(
	int FinalDamage,
	bool ReArmedNextCaptureDamageBonus);

internal readonly record struct AdventurePostCaptureEffectResult(
	int HealAmount,
	int DrawCount = 0,
	bool RevealRemainingHiddenCards = false);

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
	bool ReturnTwoCapturedCardsToHand);
internal readonly record struct AdventureClubPlayAdjustmentResult(
	string PrimaryRank,
	IReadOnlyList<string> AlternateRanks,
	bool CollectBySuitOnce = false);
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

internal static class AdventureUpgradeEffectService
{
	internal static bool HasBlackLedger(IEnumerable<string> acquiredUpgradeIds)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		return acquired.Contains("spades_black_ledger");
	}

	internal static bool ShouldOfferQuestionRewardRejudge(
		IEnumerable<string> acquiredUpgradeIds,
		bool questionRejudgeUsedThisBattle)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		return acquired.Contains("diamonds_rejudge_once") && !questionRejudgeUsedThisBattle;
	}

	internal static AdventureUpgradeEffectState CloneState(AdventureUpgradeEffectState? state)
	{
		return state?.Clone() ?? new AdventureUpgradeEffectState();
	}

	internal static AdventureQuestionRewardEffectResult OnQuestionRewardGranted(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds)
	{
		ArgumentNullException.ThrowIfNull(state);

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		state.QuestionRewardCount++;
		state.FinalDoubleQuestionRewardCountThisBattle++;

		int bonusDirectDamage = 0;
		int healAmount = 0;
		int drawCount = 0;

		if (acquired.Contains("diamonds_question_mark_windfall"))
		{
			bonusDirectDamage = state.QuestionRewardCount;
		}

		if (acquired.Contains("diamonds_question_mark_interest"))
		{
			state.PermanentCaptureDamageBonus++;
		}

		if (acquired.Contains("diamonds_question_mark_raise"))
		{
			drawCount++;
		}

		if (acquired.Contains("clubs_borrow_suit_set"))
		{
			state.ClubBorrowSuitSetArmed = true;
		}

		if (acquired.Contains("hearts_question_mark_first_aid"))
		{
			healAmount += 4;
		}

		if (acquired.Contains("hearts_supply_cache_return"))
		{
			healAmount += 3;
			drawCount++;
		}

		if (acquired.Contains("diamonds_final_double")
			&& state.FinalDoubleQuestionRewardCountThisBattle >= 10)
		{
			state.FinalDoubleCaptureDamageActive = true;
		}

		return new AdventureQuestionRewardEffectResult(
			bonusDirectDamage,
			healAmount,
			drawCount);
	}

	internal static AdventureUpgradeAcquisitionEffectResult OnUpgradeAcquired(
		AdventureUpgradeEffectState state,
		string upgradeId,
		bool triggerBattleStartBonusImmediately)
	{
		ArgumentNullException.ThrowIfNull(state);

		int maxHealthDelta = 0;
		int healAmount = 0;
		int immediateDrawCount = 0;

		switch (upgradeId)
		{
			case "hearts_thick_blood":
				maxHealthDelta += 4;
				healAmount += 4;
				break;
			case "hearts_deep_tide_lung":
				maxHealthDelta += 8;
				if (triggerBattleStartBonusImmediately)
				{
					immediateDrawCount++;
				}
				break;
			case "hearts_shoal_preparation":
				if (triggerBattleStartBonusImmediately)
				{
					immediateDrawCount++;
				}
				break;
		}

		return new AdventureUpgradeAcquisitionEffectResult(
			maxHealthDelta,
			healAmount,
			immediateDrawCount);
	}

	internal static int GetBattleStartDrawCount(IEnumerable<string> acquiredUpgradeIds)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		int drawCount = 0;
		if (acquired.Contains("hearts_shoal_preparation"))
		{
			drawCount++;
		}

		if (acquired.Contains("hearts_deep_tide_lung"))
		{
			drawCount++;
		}

		return drawCount;
	}

	internal static AdventureMainPlayerMissEffectResult OnMainPlayerMissResolved(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		int tableCardCountAfterMiss)
	{
		ArgumentNullException.ThrowIfNull(state);

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		bool grantExtraBaseCardAction = false;
		bool insertPlayedCardAtLeftmost = acquired.Contains("clubs_hanging_reverse");
		bool shuffleTableAfterMiss = acquired.Contains("clubs_wild_branch");
		bool setCanopyRevealAnchor = acquired.Contains("clubs_canopy_reveal");
		if (acquired.Contains("hearts_miss_bandage"))
		{
			state.NextTurnHealAmount += 3;
			state.NextTurnDrawCount += 1;
		}

		if (acquired.Contains("diamonds_miss_discount"))
		{
			state.NextCaptureDamageBonus += 1;
		}

		if (acquired.Contains("clubs_miss_climb_growth"))
		{
			state.ClubMissClimbGrowthStacks += 1;
		}

		if (acquired.Contains("clubs_continue_branch") && tableCardCountAfterMiss >= 4)
		{
			grantExtraBaseCardAction = true;
		}

		return new AdventureMainPlayerMissEffectResult(
			grantExtraBaseCardAction,
			insertPlayedCardAtLeftmost,
			shuffleTableAfterMiss,
			setCanopyRevealAnchor);
	}

	internal static AdventureClubPlayAdjustmentResult ResolveClubPlayAdjustments(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		string playedCardRank)
	{
		ArgumentNullException.ThrowIfNull(state);

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		string primaryRank = playedCardRank;
		var alternateRanks = new List<string>();
		bool collectBySuitOnce = acquired.Contains("clubs_borrow_suit_set")
			&& state.ClubBorrowSuitSetArmed;
		if (!int.TryParse(playedCardRank, out int numericRank) || numericRank < 1 || numericRank > 10)
		{
			return new AdventureClubPlayAdjustmentResult(primaryRank, alternateRanks, collectBySuitOnce);
		}

		int effectiveRank = numericRank;
		if (acquired.Contains("clubs_miss_climb_growth") && state.ClubMissClimbGrowthStacks > 0)
		{
			effectiveRank += state.ClubMissClimbGrowthStacks;
			primaryRank = effectiveRank.ToString();
		}

		if (acquired.Contains("clubs_cross_graft"))
		{
			if (effectiveRank > 1)
			{
				alternateRanks.Add((effectiveRank - 1).ToString());
			}

			if (effectiveRank < 10)
			{
				alternateRanks.Add((effectiveRank + 1).ToString());
			}
		}

		return new AdventureClubPlayAdjustmentResult(primaryRank, alternateRanks, collectBySuitOnce);
	}

	internal static AdventureClubCaptureScoringResult ResolveClubCaptureScoring(
		IEnumerable<string> acquiredUpgradeIds)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		return new AdventureClubCaptureScoringResult(
			acquired.Contains("clubs_crown_canopy"));
	}

	internal static AdventureClubCaptureExtensionResult ResolveClubCaptureExtensions(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		bool hasMiddleScoringCards,
		bool leftAdjacentBaseCardAvailable)
	{
		ArgumentNullException.ThrowIfNull(state);

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		bool captureLeftAdjacentBaseCard = acquired.Contains("clubs_gap_rooting")
			&& !state.ClubGapRootingUsedThisTurn
			&& !hasMiddleScoringCards
			&& leftAdjacentBaseCardAvailable;
		if (captureLeftAdjacentBaseCard)
		{
			state.ClubGapRootingUsedThisTurn = true;
		}

		return new AdventureClubCaptureExtensionResult(captureLeftAdjacentBaseCard);
	}

	internal static bool ShouldGrantLinkedStemsQuestionReward(
		IEnumerable<string> acquiredUpgradeIds,
		int successfulCapturesThisTurn)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		return acquired.Contains("clubs_linked_stems") && successfulCapturesThisTurn == 2;
	}

	internal static void ConsumeBorrowSuitSetAfterNumericBaseCardPlay(
		AdventureUpgradeEffectState state,
		string playedCardRank)
	{
		ArgumentNullException.ThrowIfNull(state);
		if (state.ClubBorrowSuitSetArmed
			&& int.TryParse(playedCardRank, out int numericRank)
			&& numericRank >= 1
			&& numericRank <= 10)
		{
			state.ClubBorrowSuitSetArmed = false;
		}
	}

	internal static AdventureClubCaptureEffectResult ResolveClubCaptureEffects(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		int baseCaptureCardCount,
		int handCountBeforeReturn,
		int maxTotalHandCards,
		bool playUsedAttachedEffect,
		bool playedCardWasBaseCard)
	{
		ArgumentNullException.ThrowIfNull(state);

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		bool returnPlayedCardToHand = false;
		bool returnTwoCapturedCardsToHand = false;

		if (playedCardWasBaseCard
			&& handCountBeforeReturn < maxTotalHandCards
			&& (acquired.Contains("clubs_primary_root_return")
				|| (playUsedAttachedEffect && acquired.Contains("clubs_climb_canopy"))))
		{
			returnPlayedCardToHand = true;
		}

		if (acquired.Contains("clubs_short_branch_return")
			&& !state.ClubShortBranchReturnUsedThisTurn
			&& baseCaptureCardCount == 2
			&& handCountBeforeReturn + (returnPlayedCardToHand ? 1 : 0) + 2 <= maxTotalHandCards)
		{
			returnTwoCapturedCardsToHand = true;
			state.ClubShortBranchReturnUsedThisTurn = true;
		}

		return new AdventureClubCaptureEffectResult(
			returnPlayedCardToHand,
			returnTwoCapturedCardsToHand);
	}

	internal static AdventureTurnStartEffectResult ConsumeMainPlayerTurnStartEffects(AdventureUpgradeEffectState state)
	{
		ArgumentNullException.ThrowIfNull(state);

		AdventureTurnStartEffectResult result = new(
			state.NextTurnHealAmount,
			state.NextTurnDrawCount);
		state.NextTurnHealAmount = 0;
		state.NextTurnDrawCount = 0;
		state.ClubShortBranchReturnUsedThisTurn = false;
		state.ClubGapRootingUsedThisTurn = false;
		return result;
	}

	internal static AdventureHealingResult ApplyHealing(
		int currentHealth,
		int maxHealth,
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		int healAmount)
	{
		ArgumentNullException.ThrowIfNull(state);

		int normalizedMaxHealth = Math.Max(1, maxHealth);
		int normalizedCurrentHealth = Math.Clamp(currentHealth, 0, normalizedMaxHealth);
		int normalizedHealAmount = Math.Max(0, healAmount);
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);

		if (normalizedHealAmount == 0)
		{
			return new AdventureHealingResult(
				normalizedCurrentHealth,
				normalizedMaxHealth,
				0,
				0,
				state.HealingOverflowRemainder);
		}

		if (!acquired.Contains("hearts_overflow_growth"))
		{
			int healedHealth = Math.Min(normalizedMaxHealth, normalizedCurrentHealth + normalizedHealAmount);
			return new AdventureHealingResult(
				healedHealth,
				normalizedMaxHealth,
				healedHealth - normalizedCurrentHealth,
				0,
				state.HealingOverflowRemainder);
		}

		int rawHealedHealth = normalizedCurrentHealth + normalizedHealAmount;
		int overflowAmount = Math.Max(0, rawHealedHealth - normalizedMaxHealth);
		int overflowTotal = state.HealingOverflowRemainder + overflowAmount;
		int maxHealthIncrease = overflowTotal / 4;
		state.HealingOverflowRemainder = overflowTotal % 4;

		int nextMaxHealth = normalizedMaxHealth + maxHealthIncrease;
		int nextCurrentHealth = Math.Min(rawHealedHealth, nextMaxHealth);
		return new AdventureHealingResult(
			nextCurrentHealth,
			nextMaxHealth,
			nextCurrentHealth - normalizedCurrentHealth,
			maxHealthIncrease,
			state.HealingOverflowRemainder);
	}

	internal static AdventureCaptureDamageResult ApplyCaptureDamage(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		AdventureCaptureDamageInput input)
	{
		ArgumentNullException.ThrowIfNull(state);

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		int damage = Math.Max(0, input.BaseDamage);
		IReadOnlyList<int> rawPoints = input.ScoringCardRawPoints ?? Array.Empty<int>();
		IReadOnlyList<int> resolvedPoints = input.ScoringCardResolvedPoints ?? Array.Empty<int>();
		int scoringCardCount = Math.Min(rawPoints.Count, resolvedPoints.Count);
		int perCardPointBonus = 0;

		if (scoringCardCount >= 3 && acquired.Contains("diamonds_multi_card_premium"))
		{
			perCardPointBonus++;
		}

		if (input.QuestionRewardEchoActive && acquired.Contains("diamonds_question_mark_echo"))
		{
			perCardPointBonus++;
		}

		if (perCardPointBonus > 0)
		{
			damage += scoringCardCount * perCardPointBonus;
		}

		if (scoringCardCount >= 3 && acquired.Contains("diamonds_top_prize_repeat"))
		{
			int highestAdjustedPoint = 0;
			for (int index = 0; index < scoringCardCount; index++)
			{
				highestAdjustedPoint = Math.Max(highestAdjustedPoint, resolvedPoints[index] + perCardPointBonus);
			}

			damage += highestAdjustedPoint;
		}

		damage += input.FlatDamageDelta;

		damage += state.PermanentCaptureDamageBonus;
		damage += state.NextCaptureDamageBonus;
		state.NextCaptureDamageBonus = 0;
		if (input.IsFirstCaptureThisBattle && acquired.Contains("diamonds_first_capture_pressure"))
		{
			damage += 2;
		}

		if (input.IncludesQuestionReward && acquired.Contains("diamonds_bounty_amplify"))
		{
			damage += 3;
		}

		if (damage >= 1
			&& damage <= 4
			&& acquired.Contains("diamonds_small_insurance"))
		{
			damage = 5;
		}

		if (damage > 0
			&& scoringCardCount >= 6
			&& acquired.Contains("diamonds_full_payout"))
		{
			damage *= 2;
		}

		if (damage > 0 && state.FinalDoubleCaptureDamageActive)
		{
			damage *= 2;
		}

		bool reArmedNextCaptureDamageBonus = false;
		if (damage >= 7 && acquired.Contains("diamonds_high_return_bonus"))
		{
			state.NextCaptureDamageBonus += 2;
			reArmedNextCaptureDamageBonus = true;
		}

		return new AdventureCaptureDamageResult(
			damage,
			reArmedNextCaptureDamageBonus);
	}

	internal static AdventurePostCaptureEffectResult OnMainPlayerCaptureResolved(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		int finalDamage,
		int successfulCapturesThisTurn = 1,
		bool attachedEffectTriggered = false,
		bool capturedOnlyOuterVisibleCards = false)
	{
		ArgumentNullException.ThrowIfNull(state);

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		int healAmount = 0;
		int drawCount = 0;
		bool revealRemainingHiddenCards = false;
		if (finalDamage >= 6 && acquired.Contains("hearts_long_capture_recovery"))
		{
			healAmount += 2;
		}

		if (attachedEffectTriggered && acquired.Contains("hearts_enchanted_recovery"))
		{
			healAmount += 3;
			drawCount += 1;
		}

		if (successfulCapturesThisTurn == 1)
		{
			if (acquired.Contains("hearts_warm_current_supply"))
			{
				drawCount++;
			}

			if (acquired.Contains("hearts_return_tide_heart"))
			{
				drawCount++;
				healAmount += 2;
			}
		}
		else if (successfulCapturesThisTurn == 2)
		{
			if (acquired.Contains("hearts_chain_tide_breath"))
			{
				drawCount++;
				healAmount += 2;
			}

			if (acquired.Contains("hearts_return_tide_heart"))
			{
				drawCount++;
				healAmount += 3;
			}
		}

		if (capturedOnlyOuterVisibleCards && acquired.Contains("clubs_hollow_burst"))
		{
			revealRemainingHiddenCards = true;
		}

		return new AdventurePostCaptureEffectResult(healAmount, drawCount, revealRemainingHiddenCards);
	}

	internal static AdventureLowHealthRescueResult ResolveMainPlayerLowHealthRescue(
		int previousHealth,
		int currentHealth,
		int maxHealth,
		IEnumerable<string> acquiredUpgradeIds,
		bool halfThresholdUsed,
		bool quarterThresholdUsed)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		int normalizedMaxHealth = Math.Max(1, maxHealth);
		float previousRatio = previousHealth / (float)normalizedMaxHealth;
		float currentRatio = currentHealth / (float)normalizedMaxHealth;
		int healAmount = 0;
		int drawCount = 0;
		bool consumedHalfThreshold = false;
		bool consumedQuarterThreshold = false;

		if (!halfThresholdUsed
			&& previousRatio >= 0.5f
			&& currentRatio < 0.5f
			&& acquired.Contains("hearts_red_tide_return"))
		{
			healAmount += 5;
			drawCount += 2;
			consumedHalfThreshold = true;
		}

		if (!quarterThresholdUsed
			&& previousRatio >= 0.25f
			&& currentRatio < 0.25f
			&& acquired.Contains("hearts_revival_draw"))
		{
			healAmount += 8;
			drawCount += 2;
			consumedQuarterThreshold = true;
		}

		return new AdventureLowHealthRescueResult(
			healAmount,
			drawCount,
			consumedHalfThreshold,
			consumedQuarterThreshold);
	}

	internal static AdventureEnemyMissEffectResult ResolveEnemyMissEffects(
		IEnumerable<string> acquiredUpgradeIds,
		int tableCardCountAfterMiss,
		bool hadOldTableCardsBeforeMiss)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		bool nextCardFacesUp = false;
		AdventureEnemyNextCardInsertMode insertMode = AdventureEnemyNextCardInsertMode.None;
		int nextCaptureDamageDelta = 0;
		int directDamageToEnemy = 0;

		if (acquired.Contains("spades_missed_beat_blunt_hurt"))
		{
			nextCaptureDamageDelta -= 3;
		}

		if (acquired.Contains("spades_miss_bleed") && tableCardCountAfterMiss >= 6)
		{
			directDamageToEnemy += 2;
		}

		if (acquired.Contains("spades_return_tide_misorder") && hadOldTableCardsBeforeMiss)
		{
			insertMode = AdventureEnemyNextCardInsertMode.BeforeTrackedOldest;
		}

		if (acquired.Contains("spades_missed_beat_calamity"))
		{
			nextCardFacesUp = true;
			if (insertMode == AdventureEnemyNextCardInsertMode.None)
			{
				insertMode = AdventureEnemyNextCardInsertMode.Leftmost;
			}

			nextCaptureDamageDelta -= 3;
		}

		return new AdventureEnemyMissEffectResult(
			nextCardFacesUp,
			insertMode,
			nextCaptureDamageDelta,
			directDamageToEnemy,
			acquired.Contains("spades_intercept_beat"));
	}

	internal static AdventureEnemyCaptureEffectResult ResolveEnemyCaptureEffects(
		IEnumerable<string> acquiredUpgradeIds)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		bool nextCardFacesUp = acquired.Contains("spades_dark_harbor_reveal");
		AdventureEnemyNextCardInsertMode insertMode = acquired.Contains("spades_wrong_shore_lineup")
			? AdventureEnemyNextCardInsertMode.Leftmost
			: AdventureEnemyNextCardInsertMode.None;

		return new AdventureEnemyCaptureEffectResult(nextCardFacesUp, insertMode);
	}

	internal static int ResolveOldTableCaptureDamageDelta(
		IEnumerable<string> acquiredUpgradeIds,
		bool capturingPlayerIsMain,
		int capturedOldScoringCardCount,
		bool capturedTraceMark,
		bool capturedHuntMark)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		if (capturingPlayerIsMain)
		{
			int delta = 0;
			if (capturedTraceMark && acquired.Contains("spades_trace_hunt"))
			{
				delta += 4;
			}

			if (capturedHuntMark && acquired.Contains("spades_hunt_order"))
			{
				delta += 6;
			}

			return delta;
		}

		if (capturedOldScoringCardCount <= 0)
		{
			return 0;
		}

		if (acquired.Contains("spades_exaction"))
		{
			return -4;
		}

		if (acquired.Contains("spades_reverse_capture_tax"))
		{
			return -capturedOldScoringCardCount;
		}

		return 0;
	}

	internal static int ResolveBlackLedgerDamageDelta(
		bool blackLedgerActive,
		bool capturingPlayerIsMain,
		int totalResolvedOldTableCardPoints,
		int currentResolvedOldTableScoringPoints)
	{
		if (!blackLedgerActive)
		{
			return 0;
		}

		if (capturingPlayerIsMain)
		{
			return Math.Max(0, totalResolvedOldTableCardPoints) - Math.Max(0, currentResolvedOldTableScoringPoints);
		}

		return -Math.Max(0, currentResolvedOldTableScoringPoints);
	}

	internal static bool ShouldPreserveEnemyCaptureTailCard(
		IEnumerable<string> acquiredUpgradeIds,
		int totalCapturedCardCount)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		return acquired.Contains("spades_tail_pressure") && totalCapturedCardCount == 7;
	}
}
