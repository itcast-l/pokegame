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
	bool ReArmedNextCaptureDamageBonus,
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

internal static class AdventureUpgradeEffectService
{
	internal const int MaxRunHealth = 150;
	private const int FullPayoutScoringCardThreshold = 6;
	private const int HighReturnDamageThreshold = 7;

	internal static AdventureCaptureRewardApplicationPlan
		BuildCaptureRewardApplicationPlan(
			AdventureQuestionRewardEffectResult? questionRewardEffects,
			AdventurePostCaptureEffectResult? postCaptureEffects,
			int currentHandCount,
			int maximumHandCount,
			int availableDeckCardCount)
	{
		AdventureQuestionRewardEffectResult question =
			questionRewardEffects ?? default;
		AdventurePostCaptureEffectResult post =
			postCaptureEffects ?? default;
		int healAmount = (int)Math.Min(
			int.MaxValue,
			Math.Max(0L, question.HealAmount)
				+ Math.Max(0L, post.HealAmount));
		long requestedDrawCount = Math.Min(
			int.MaxValue,
			Math.Max(0L, question.DrawCount)
				+ Math.Max(0L, post.DrawCount));
		int remainingHandCapacity = Math.Max(
			0,
			Math.Max(0, maximumHandCount)
				- Math.Max(0, currentHandCount));
		int drawCount = (int)Math.Min(
			requestedDrawCount,
			Math.Min(
				remainingHandCapacity,
				Math.Max(0, availableDeckCardCount)));
		return new AdventureCaptureRewardApplicationPlan(
			healAmount,
			drawCount,
			Math.Max(0, question.BonusDirectDamage),
			post.RevealRemainingHiddenCards);
	}

	internal static int CountAcquiredSuitUpgrades(
		IEnumerable<string>? acquiredUpgradeIds,
		string suit)
	{
		if (string.IsNullOrWhiteSpace(suit))
		{
			return 0;
		}

		string prefix = $"{suit.Trim().ToLowerInvariant()}_";
		var matchingIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (string? upgradeId in acquiredUpgradeIds ?? Array.Empty<string>())
		{
			if (string.IsNullOrWhiteSpace(upgradeId))
			{
				continue;
			}

			string normalizedId = upgradeId.Trim();
			if (normalizedId.StartsWith(prefix, StringComparison.Ordinal))
			{
				matchingIds.Add(normalizedId);
			}
		}

		return matchingIds.Count;
	}

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

	internal static AdventureQuestionRejudgeRuleResult ResolveQuestionRewardRejudgeChoice(
		IEnumerable<string> acquiredUpgradeIds,
		bool questionRejudgeUsedThisBattle,
		AdventureQuestionRejudgeRuleChoice choice)
	{
		bool offered = ShouldOfferQuestionRewardRejudge(
			acquiredUpgradeIds,
			questionRejudgeUsedThisBattle);
		return new AdventureQuestionRejudgeRuleResult(
			offered,
			ConsumesOpportunity: offered,
			ShouldReroll: offered && choice == AdventureQuestionRejudgeRuleChoice.Reroll);
	}

	internal static AdventureUpgradeEffectState CloneState(AdventureUpgradeEffectState? state)
	{
		return state?.Clone() ?? new AdventureUpgradeEffectState();
	}

	internal static AdventureCaptureDamageModifierSnapshot FreezeCaptureDamageModifiers(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds)
	{
		ArgumentNullException.ThrowIfNull(state);
		state.Normalize();
		var acquired = new HashSet<string>(
			acquiredUpgradeIds ?? Array.Empty<string>(),
			StringComparer.Ordinal);
		return new AdventureCaptureDamageModifierSnapshot(
			state.PermanentCaptureDamageBonus,
			QuestionRewardEchoActive: acquired.Contains("diamonds_question_mark_echo")
				&& state.QuestionRewardCount > 0,
			FinalDoubleCaptureDamageActive: acquired.Contains("diamonds_final_double")
				&& state.FinalDoubleCaptureDamageActive);
	}

	internal static AdventureQuestionRewardEffectResult OnQuestionRewardGranted(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds)
	{
		ArgumentNullException.ThrowIfNull(state);

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		state.Normalize();
		state.QuestionRewardCount = Math.Min(
			AdventureUpgradeEffectState.MaxQuestionRewardCount,
			state.QuestionRewardCount + 1);

		int bonusDirectDamage = 0;
		int healAmount = 0;
		int drawCount = 0;

		if (acquired.Contains("diamonds_question_mark_windfall"))
		{
			bonusDirectDamage = Math.Min(5, state.QuestionRewardCount);
		}

		if (acquired.Contains("diamonds_question_mark_interest"))
		{
			state.PermanentCaptureDamageBonus = Math.Min(
				AdventureUpgradeEffectState.MaxPermanentCaptureDamageBonus,
				state.PermanentCaptureDamageBonus + 1);
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
			&& state.QuestionRewardCount >= AdventureUpgradeEffectState.MaxQuestionRewardCount)
		{
			state.FinalDoubleCaptureDamageActive = true;
		}
		state.Normalize();

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
		state.Normalize();

		string normalizedUpgradeId = upgradeId?.Trim() ?? string.Empty;
		int maxHealthDelta = 0;
		int healAmount = 0;
		int immediateDrawCount = 0;

		switch (normalizedUpgradeId)
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
			case "diamonds_final_double":
				state.Normalize();
				if (state.QuestionRewardCount >= AdventureUpgradeEffectState.MaxQuestionRewardCount)
				{
					state.FinalDoubleCaptureDamageActive = true;
				}
				break;
		}

		return new AdventureUpgradeAcquisitionEffectResult(
			maxHealthDelta,
			healAmount,
			immediateDrawCount);
	}

	internal static void ClearBattleScopedState(AdventureUpgradeEffectState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		state.ClearBattleScope();
	}

	internal static void ClearTurnScopedState(AdventureUpgradeEffectState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		state.ClearTurnScope();
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
		state.Normalize();

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		bool grantExtraBaseCardAction = false;
		bool insertPlayedCardAtLeftmost = acquired.Contains("clubs_hanging_reverse");
		bool shuffleTableAfterMiss = acquired.Contains("clubs_wild_branch");
		bool setCanopyRevealAnchor = acquired.Contains("clubs_canopy_reveal");
		if (acquired.Contains("hearts_miss_bandage"))
		{
			state.NextTurnHealAmount = SaturatingAddToInt(state.NextTurnHealAmount, 3);
			state.NextTurnDrawCount = SaturatingAddToInt(state.NextTurnDrawCount, 1);
		}

		if (acquired.Contains("diamonds_miss_discount"))
		{
			state.NextCaptureDamageBonus = SaturatingAddToInt(state.NextCaptureDamageBonus, 1);
		}

		if (acquired.Contains("clubs_miss_climb_growth"))
		{
			state.ClubMissClimbGrowthStacks = Math.Min(
				AdventureUpgradeEffectState.MaxClubMissClimbGrowthStacks,
				state.ClubMissClimbGrowthStacks + 1);
		}

		if (acquired.Contains("clubs_continue_branch") && tableCardCountAfterMiss >= 6)
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
		state.Normalize();

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		string primaryRank = playedCardRank;
		var alternateRanks = new List<string>();
		bool collectBySuitOnce = acquired.Contains("clubs_borrow_suit_set")
			&& state.ClubBorrowSuitSetArmed;
		if (!int.TryParse(playedCardRank, out int numericRank) || numericRank < 1 || numericRank > 10)
		{
			return new AdventureClubPlayAdjustmentResult(
				primaryRank,
				alternateRanks,
				collectBySuitOnce,
				SuitMatchIsFallbackOnly: collectBySuitOnce);
		}

		long climbedRank = numericRank;
		bool hasClimbedRank = acquired.Contains("clubs_miss_climb_growth")
			&& state.ClubMissClimbGrowthStacks > 0;
		if (hasClimbedRank)
		{
			climbedRank += state.ClubMissClimbGrowthStacks;
		}

		void AddAlternateRank(long candidateRank)
		{
			if (candidateRank < 1 || candidateRank > 10)
			{
				return;
			}

			string candidate = candidateRank.ToString();
			if (candidateRank == numericRank || alternateRanks.Contains(candidate))
			{
				return;
			}

			alternateRanks.Add(candidate);
		}

		if (hasClimbedRank)
		{
			AddAlternateRank(climbedRank);
		}

		if (acquired.Contains("clubs_cross_graft"))
		{
			AddAlternateRank(numericRank - 1L);
			AddAlternateRank(numericRank + 1L);
			if (hasClimbedRank)
			{
				AddAlternateRank(climbedRank - 1L);
				AddAlternateRank(climbedRank + 1L);
			}
		}

		return new AdventureClubPlayAdjustmentResult(
			primaryRank,
			alternateRanks,
			collectBySuitOnce,
			SuitMatchIsFallbackOnly: collectBySuitOnce);
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
		state.Normalize();

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
		state.Normalize();
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
		int completeBaseCaptureCardCount,
		int uniqueShortReturnCandidateCount,
		int uniqueCombinedReturnCandidateCount,
		int availableHandSlots,
		bool playUsedAttachedEffect,
		bool playedCardWasBaseCard)
	{
		ArgumentNullException.ThrowIfNull(state);
		state.Normalize();

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		int normalizedAvailableHandSlots = Math.Max(0, availableHandSlots);
		int normalizedUniqueShortReturnCandidateCount = Math.Max(
			0,
			uniqueShortReturnCandidateCount);
		int normalizedUniqueCombinedReturnCandidateCount = Math.Max(
			normalizedUniqueShortReturnCandidateCount,
			uniqueCombinedReturnCandidateCount);
		string playedCardReturnSourceUpgradeId = string.Empty;
		if (playedCardWasBaseCard && acquired.Contains("clubs_primary_root_return"))
		{
			playedCardReturnSourceUpgradeId = "clubs_primary_root_return";
		}
		else if (playedCardWasBaseCard
			&& playUsedAttachedEffect
			&& acquired.Contains("clubs_climb_canopy"))
		{
			playedCardReturnSourceUpgradeId = "clubs_climb_canopy";
		}

		bool playedCardReturnEligible = playedCardReturnSourceUpgradeId.Length > 0;
		bool returnPlayedCardToHand = playedCardReturnEligible
			&& normalizedAvailableHandSlots >= 1;
		bool returnTwoCapturedCardsToHand = false;
		int requiredShortReturnSlots = playedCardReturnEligible
			? normalizedUniqueCombinedReturnCandidateCount
			: normalizedUniqueShortReturnCandidateCount;

		if (acquired.Contains("clubs_short_branch_return")
			&& !state.ClubShortBranchReturnUsedThisTurn
			&& completeBaseCaptureCardCount == 2
			&& normalizedUniqueShortReturnCandidateCount == 2
			&& requiredShortReturnSlots <= normalizedAvailableHandSlots)
		{
			returnTwoCapturedCardsToHand = true;
			state.ClubShortBranchReturnUsedThisTurn = true;
		}

		return new AdventureClubCaptureEffectResult(
			returnPlayedCardToHand,
			returnTwoCapturedCardsToHand,
			returnPlayedCardToHand ? playedCardReturnSourceUpgradeId : string.Empty);
	}

	internal static AdventureTurnStartEffectResult ConsumeMainPlayerTurnStartEffects(AdventureUpgradeEffectState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		state.Normalize();

		AdventureTurnStartEffectResult result = new(
			state.NextTurnHealAmount,
			state.NextTurnDrawCount);
		state.ClearNextTurnCarryScope();
		state.ClearTurnScope();
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
		state.Normalize();

		int normalizedMaxHealth = Math.Clamp(maxHealth, 1, MaxRunHealth);
		int normalizedCurrentHealth = Math.Clamp(currentHealth, 0, normalizedMaxHealth);
		long normalizedHealAmount = Math.Max(0L, healAmount);
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
			int healedHealth = (int)Math.Min(
				normalizedMaxHealth,
				SaturatingAdd(normalizedCurrentHealth, normalizedHealAmount));
			return new AdventureHealingResult(
				healedHealth,
				normalizedMaxHealth,
				healedHealth - normalizedCurrentHealth,
				0,
				state.HealingOverflowRemainder);
		}

		long rawHealedHealth = SaturatingAdd(normalizedCurrentHealth, normalizedHealAmount);
		long overflowAmount = Math.Max(0L, rawHealedHealth - normalizedMaxHealth);
		long overflowTotal = SaturatingAdd(state.HealingOverflowRemainder, overflowAmount);
		int availableMaxHealthIncrease = MaxRunHealth - normalizedMaxHealth;
		int maxHealthIncrease = (int)Math.Min(availableMaxHealthIncrease, overflowTotal / 4L);
		state.HealingOverflowRemainder = (int)(overflowTotal % 4L);

		int nextMaxHealth = normalizedMaxHealth + maxHealthIncrease;
		int nextCurrentHealth = (int)Math.Min(rawHealedHealth, nextMaxHealth);
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
		state.Normalize();
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		AdventureCaptureDamageModifierSnapshot modifierSnapshot = input.FrozenModifiers
			?? new AdventureCaptureDamageModifierSnapshot(
				state.PermanentCaptureDamageBonus,
				input.QuestionRewardEchoActive
					|| (acquired.Contains("diamonds_question_mark_echo")
						&& state.QuestionRewardCount > 0),
				state.FinalDoubleCaptureDamageActive);
		modifierSnapshot = modifierSnapshot with
		{
			PermanentCaptureDamageBonus = Math.Clamp(
				modifierSnapshot.PermanentCaptureDamageBonus,
				0,
				AdventureUpgradeEffectState.MaxPermanentCaptureDamageBonus),
		};
		AdventureCaptureDamageInput effectiveInput = input with
		{
			QuestionRewardEchoActive = modifierSnapshot.QuestionRewardEchoActive,
		};
		long damage = Math.Max(0L, input.BaseDamage);
		long rearmQualificationDamage = damage;
		IReadOnlyList<AdventureCaptureScoreItem>? normalScoreItems =
			input.CaptureContext?.NormalScoreItems;
		IReadOnlyList<int> rawPoints = normalScoreItems == null
			? input.ScoringCardRawPoints ?? Array.Empty<int>()
			: CopyScoreItemPoints(normalScoreItems, useResolvedPoints: false);
		IReadOnlyList<int> resolvedPoints = normalScoreItems == null
			? input.ScoringCardResolvedPoints ?? Array.Empty<int>()
			: CopyScoreItemPoints(normalScoreItems, useResolvedPoints: true);
		int scoringCardCount = Math.Min(rawPoints.Count, resolvedPoints.Count);
		int wholeCaptureMultiplier = 1;
		long reductionAfterMultiplier = input.CapturerIsMainPlayer
			? 0L
			: -Math.Max(0L, input.EnemyDamageReduction);
		long fixedDamageDelta = input.FlatDamageDelta;
		if (!input.CapturerIsMainPlayer && fixedDamageDelta < 0)
		{
			reductionAfterMultiplier = Math.Min(reductionAfterMultiplier, fixedDamageDelta);
			fixedDamageDelta = 0;
		}

		if (input.CapturerIsMainPlayer)
		{
			int perCardPointBonus = GetPerCardCapturePointBonus(acquired, effectiveInput, scoringCardCount);
			if (perCardPointBonus > 0)
			{
				long perCardBonusDamage = SaturatingMultiply(scoringCardCount, perCardPointBonus);
				damage = SaturatingAdd(damage, perCardBonusDamage);
				rearmQualificationDamage = SaturatingAdd(rearmQualificationDamage, perCardBonusDamage);
			}

			if (input.CaptureContext != null)
			{
				foreach (AdventureCaptureScoreItem duplicateItem in
					input.CaptureContext.ExplicitDuplicateScoreItems)
				{
					long duplicateDamage = SaturatingAdd(
						duplicateItem.ResolvedPoints,
						perCardPointBonus);
					damage = SaturatingAdd(damage, duplicateDamage);
					rearmQualificationDamage = SaturatingAdd(
						rearmQualificationDamage,
						duplicateDamage);
				}
			}
			damage = SaturatingAdd(damage, fixedDamageDelta);
			rearmQualificationDamage = SaturatingAdd(rearmQualificationDamage, fixedDamageDelta);
			damage = SaturatingAdd(damage, modifierSnapshot.PermanentCaptureDamageBonus);
			rearmQualificationDamage = SaturatingAdd(
				rearmQualificationDamage,
				modifierSnapshot.PermanentCaptureDamageBonus);

			int pendingCaptureDamageBonus = state.NextCaptureDamageBonus;
			int consumedHighReturnBonus = Math.Min(
				pendingCaptureDamageBonus,
				state.HighReturnBonusIncludedInNextCaptureDamageBonus);
			damage = SaturatingAdd(damage, pendingCaptureDamageBonus);
			rearmQualificationDamage = SaturatingAdd(
				rearmQualificationDamage,
				pendingCaptureDamageBonus - consumedHighReturnBonus);
			state.NextCaptureDamageBonus = 0;
			state.HighReturnBonusIncludedInNextCaptureDamageBonus = 0;
			if (input.IsFirstCaptureThisBattle && acquired.Contains("diamonds_first_capture_pressure"))
			{
				damage = SaturatingAdd(damage, 2);
				rearmQualificationDamage = SaturatingAdd(rearmQualificationDamage, 2);
			}

			if (input.IncludesQuestionReward && acquired.Contains("diamonds_bounty_amplify"))
			{
				damage = SaturatingAdd(damage, 3);
				rearmQualificationDamage = SaturatingAdd(rearmQualificationDamage, 3);
			}

			if (acquired.Contains("diamonds_small_insurance"))
			{
				damage = ApplySmallInsurance(damage);
				rearmQualificationDamage = ApplySmallInsurance(rearmQualificationDamage);
			}

			if (damage > 0
				&& scoringCardCount >= FullPayoutScoringCardThreshold
				&& acquired.Contains("diamonds_full_payout"))
			{
				wholeCaptureMultiplier++;
			}

			if (damage > 0
				&& modifierSnapshot.FinalDoubleCaptureDamageActive
				&& acquired.Contains("diamonds_final_double"))
			{
				wholeCaptureMultiplier++;
			}

			damage = SaturatingMultiply(damage, wholeCaptureMultiplier);
			rearmQualificationDamage = SaturatingMultiply(
				rearmQualificationDamage,
				wholeCaptureMultiplier);
		}
		else
		{
			damage = SaturatingAdd(damage, fixedDamageDelta);
			rearmQualificationDamage = damage;
		}

		damage = Math.Max(0L, SaturatingAdd(damage, reductionAfterMultiplier));
		rearmQualificationDamage = Math.Max(
			0L,
			SaturatingAdd(rearmQualificationDamage, reductionAfterMultiplier));
		long damageEventCap = GetDamageEventCap(input.TargetMaxHealth);
		bool damageCapped = damage > damageEventCap;
		damage = Math.Min(damage, damageEventCap);
		rearmQualificationDamage = Math.Min(rearmQualificationDamage, damageEventCap);

		bool reArmedNextCaptureDamageBonus = false;
		if (input.CapturerIsMainPlayer
			&& rearmQualificationDamage >= HighReturnDamageThreshold
			&& acquired.Contains("diamonds_high_return_bonus"))
		{
			state.NextCaptureDamageBonus = SaturatingAddToInt(
				state.NextCaptureDamageBonus,
				AdventureUpgradeEffectState.MaxHighReturnCarryBonus);
			state.HighReturnBonusIncludedInNextCaptureDamageBonus = Math.Min(
				AdventureUpgradeEffectState.MaxHighReturnCarryBonus,
				state.HighReturnBonusIncludedInNextCaptureDamageBonus
					+ AdventureUpgradeEffectState.MaxHighReturnCarryBonus);
			reArmedNextCaptureDamageBonus = true;
		}

		return new AdventureCaptureDamageResult(
			ToSafeNonNegativeInt(damage),
			reArmedNextCaptureDamageBonus,
			WholeCaptureMultiplier: wholeCaptureMultiplier,
			DamageCapped: damageCapped);
	}

	private static IReadOnlyList<int> CopyScoreItemPoints(
		IReadOnlyList<AdventureCaptureScoreItem> scoreItems,
		bool useResolvedPoints)
	{
		var points = new int[scoreItems.Count];
		for (int index = 0; index < scoreItems.Count; index++)
		{
			points[index] = useResolvedPoints
				? scoreItems[index].ResolvedPoints
				: scoreItems[index].RawPoints;
		}

		return points;
	}

	private static int GetPerCardCapturePointBonus(
		IReadOnlySet<string> acquired,
		AdventureCaptureDamageInput input,
		int scoringCardCount)
	{
		int perCardPointBonus = 0;
		if (scoringCardCount >= 3 && acquired.Contains("diamonds_multi_card_premium"))
		{
			perCardPointBonus++;
		}

		if (input.QuestionRewardEchoActive && acquired.Contains("diamonds_question_mark_echo"))
		{
			perCardPointBonus++;
		}

		return perCardPointBonus;
	}

	internal static AdventurePostCaptureEffectResult OnMainPlayerCaptureResolved(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds,
		int finalDamage,
		int successfulCapturesThisTurn,
		CaptureResolutionContext captureContext)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(captureContext);
		return ResolveMainPlayerCaptureEffects(
			acquiredUpgradeIds,
			finalDamage,
			successfulCapturesThisTurn,
			captureContext.TriggeredAttachedItemCardIds,
			captureContext.CapturedOnlyTwoVisibleEndpoints());
	}

	private static AdventurePostCaptureEffectResult ResolveMainPlayerCaptureEffects(
		IEnumerable<string> acquiredUpgradeIds,
		int finalDamage,
		int successfulCapturesThisTurn,
		IReadOnlyCollection<string> triggeredAttachedItemCardIds,
		bool capturedOnlyOuterVisibleCards)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		int healAmount = 0;
		int drawCount = 0;
		bool revealRemainingHiddenCards = false;
		if (finalDamage >= 6 && acquired.Contains("hearts_long_capture_recovery"))
		{
			healAmount += 2;
		}

		AdventurePostCaptureEffectResult enchantedRecovery =
			ResolveEnchantedRecoveryActionEffects(
				acquired,
				triggeredAttachedItemCardIds,
				successfulCapture: true);
		healAmount += enchantedRecovery.HealAmount;
		drawCount += enchantedRecovery.DrawCount;

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

	internal static AdventurePostCaptureEffectResult ResolveEnchantedRecoveryActionEffects(
		IEnumerable<string> acquiredUpgradeIds,
		IReadOnlyCollection<string>? triggeredAttachedItemCardIds,
		bool successfulCapture)
	{
		var acquired = new HashSet<string>(
			acquiredUpgradeIds ?? Array.Empty<string>(),
			StringComparer.Ordinal);
		if (!acquired.Contains("hearts_enchanted_recovery")
			|| triggeredAttachedItemCardIds == null
			|| triggeredAttachedItemCardIds.Count == 0)
		{
			return default;
		}

		return new AdventurePostCaptureEffectResult(
			HealAmount: 3,
			DrawCount: successfulCapture ? 1 : 0);
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

		int strongestReduction = 0;
		if (acquired.Contains("spades_exaction"))
		{
			strongestReduction = -4;
		}

		if (acquired.Contains("spades_reverse_capture_tax"))
		{
			int reverseCaptureTaxReduction = -Math.Max(0, capturedOldScoringCardCount);
			strongestReduction = Math.Min(strongestReduction, reverseCaptureTaxReduction);
		}

		return strongestReduction;
	}

	internal static int ResolveBlackLedgerRedirectTableCardIndex(
		bool blackLedgerActive,
		bool capturingPlayerIsMain,
		IReadOnlyList<int> tableCardScoreValues,
		int captureStartIndex)
	{
		if (!blackLedgerActive
			|| capturingPlayerIsMain
			|| tableCardScoreValues == null
			|| captureStartIndex < 0
			|| captureStartIndex >= tableCardScoreValues.Count)
		{
			return -1;
		}

		int selectedTableCardIndex = -1;
		int highestScoreValue = 0;
		for (int tableCardIndex = captureStartIndex + 1;
			tableCardIndex < tableCardScoreValues.Count - 1;
			tableCardIndex++)
		{
			int scoreValue = tableCardScoreValues[tableCardIndex];
			if (scoreValue > highestScoreValue)
			{
				highestScoreValue = scoreValue;
				selectedTableCardIndex = tableCardIndex;
			}
		}

		return selectedTableCardIndex;
	}

	internal static bool ShouldPreserveEnemyCaptureTailCard(
		IEnumerable<string> acquiredUpgradeIds,
		int totalCapturedCardCount)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		return acquired.Contains("spades_tail_pressure") && totalCapturedCardCount == 7;
	}

	private static long ApplySmallInsurance(long damage)
	{
		return damage is >= 1 and <= 4 ? 5 : damage;
	}

	private static long GetDamageEventCap(int targetMaxHealth)
	{
		if (targetMaxHealth <= 0)
		{
			return int.MaxValue;
		}

		long normalizedTargetMaxHealth = Math.Max(1L, targetMaxHealth);
		return SaturatingAdd(
			SaturatingMultiply(normalizedTargetMaxHealth, 3L),
			1L) / 2L;
	}

	private static int SaturatingAddToInt(int value, int delta)
	{
		return ToSafeNonNegativeInt(SaturatingAdd(Math.Max(0L, value), Math.Max(0L, delta)));
	}

	private static int ToSafeNonNegativeInt(long value)
	{
		return (int)Math.Clamp(value, 0L, int.MaxValue);
	}

	private static long SaturatingAdd(long left, long right)
	{
		if (right > 0 && left > long.MaxValue - right)
		{
			return long.MaxValue;
		}

		if (right < 0 && left < long.MinValue - right)
		{
			return long.MinValue;
		}

		return left + right;
	}

	private static long SaturatingMultiply(long left, long right)
	{
		if (left <= 0 || right <= 0)
		{
			return 0;
		}

		return left > long.MaxValue / right ? long.MaxValue : left * right;
	}
}
