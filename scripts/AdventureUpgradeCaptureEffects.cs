using System;
using System.Collections.Generic;

internal static class AdventureUpgradeCaptureEffects
{
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

internal static bool HasBlackLedger(IEnumerable<string> acquiredUpgradeIds)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		return acquired.Contains("spades_black_ledger");
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
			long graftStep = AdventureUpgradeRuntimeValues.GetPositiveInt(
				"clubs_cross_graft",
				"step",
				1);
			AddAlternateRank(numericRank - graftStep);
			AddAlternateRank(numericRank + graftStep);
			if (hasClimbedRank)
			{
				AddAlternateRank(climbedRank - graftStep);
				AddAlternateRank(climbedRank + graftStep);
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
		return acquired.Contains("clubs_linked_stems")
			&& successfulCapturesThisTurn == AdventureUpgradeRuntimeValues.GetPositiveInt(
				"clubs_linked_stems",
				"threshold",
				2);
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

		bool playedCardReturnEligible = playedCardReturnSourceUpgradeId.Length > 0;
		bool returnPlayedCardToHand = playedCardReturnEligible
			&& normalizedAvailableHandSlots >= 1;
		bool returnTwoCapturedCardsToHand = false;
		int requiredShortReturnSlots = playedCardReturnEligible
			? normalizedUniqueCombinedReturnCandidateCount
			: normalizedUniqueShortReturnCandidateCount;

		int shortReturnCardCount = AdventureUpgradeRuntimeValues.GetPositiveInt(
			"clubs_short_branch_return",
			"count",
			2);
		if (acquired.Contains("clubs_short_branch_return")
			&& !state.ClubShortBranchReturnUsedThisTurn
			&& completeBaseCaptureCardCount == shortReturnCardCount
			&& normalizedUniqueShortReturnCandidateCount == shortReturnCardCount
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
				long perCardBonusDamage = AdventureUpgradeEffectMath.SaturatingMultiply(scoringCardCount, perCardPointBonus);
				damage = AdventureUpgradeEffectMath.SaturatingAdd(damage, perCardBonusDamage);
			}

			if (input.CaptureContext != null)
			{
				foreach (AdventureCaptureScoreItem duplicateItem in
					input.CaptureContext.ExplicitDuplicateScoreItems)
				{
					long duplicateDamage = AdventureUpgradeEffectMath.SaturatingAdd(
						duplicateItem.ResolvedPoints,
						perCardPointBonus);
					damage = AdventureUpgradeEffectMath.SaturatingAdd(damage, duplicateDamage);
				}
			}
			damage = AdventureUpgradeEffectMath.SaturatingAdd(damage, fixedDamageDelta);
			damage = AdventureUpgradeEffectMath.SaturatingAdd(damage, modifierSnapshot.PermanentCaptureDamageBonus);

			int pendingCaptureDamageBonus = state.NextCaptureDamageBonus;
			damage = AdventureUpgradeEffectMath.SaturatingAdd(damage, pendingCaptureDamageBonus);
			state.NextCaptureDamageBonus = 0;
			if (input.IsFirstCaptureThisBattle && acquired.Contains("diamonds_first_capture_pressure"))
			{
				damage = AdventureUpgradeEffectMath.SaturatingAdd(
					damage,
				AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"diamonds_first_capture_pressure",
					"amount",
					2));
			}

			if (input.IncludesQuestionReward && acquired.Contains("diamonds_bounty_amplify"))
			{
				damage = AdventureUpgradeEffectMath.SaturatingAdd(
					damage,
				AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"diamonds_bounty_amplify",
					"amount",
					3));
			}

			if (acquired.Contains("diamonds_small_insurance"))
			{
				damage = AdventureUpgradeEffectMath.ApplySmallInsurance(
					damage,
				AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"diamonds_small_insurance",
					"threshold",
					4),
				AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"diamonds_small_insurance",
					"amount",
					5));
			}

			int fullPayoutThreshold = AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"diamonds_full_payout",
				"threshold",
				AdventureUpgradeEffectMath.FullPayoutScoringCardThreshold);
			if (damage > 0
				&& scoringCardCount >= fullPayoutThreshold
				&& acquired.Contains("diamonds_full_payout"))
			{
				wholeCaptureMultiplier += AdventureUpgradeRuntimeValues.GetPositiveInt(
					"diamonds_full_payout",
					"multiplier",
					1);
			}

			if (damage > 0
				&& modifierSnapshot.FinalDoubleCaptureDamageActive
				&& acquired.Contains("diamonds_final_double"))
			{
				wholeCaptureMultiplier += AdventureUpgradeRuntimeValues.GetPositiveInt(
					"diamonds_final_double",
					"multiplier",
					1);
			}

			damage = AdventureUpgradeEffectMath.SaturatingMultiply(damage, wholeCaptureMultiplier);
		}
		else
		{
			damage = AdventureUpgradeEffectMath.SaturatingAdd(damage, fixedDamageDelta);
		}

		damage = Math.Max(0L, AdventureUpgradeEffectMath.SaturatingAdd(damage, reductionAfterMultiplier));
		long damageEventCap = AdventureUpgradeEffectMath.GetDamageEventCap(input.TargetMaxHealth);
		bool damageCapped = damage > damageEventCap;
		damage = Math.Min(damage, damageEventCap);

		return new AdventureCaptureDamageResult(
			AdventureUpgradeEffectMath.ToSafeNonNegativeInt(damage),
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
		if (scoringCardCount >= AdventureUpgradeRuntimeValues.GetNonNegativeInt(
			"diamonds_multi_card_premium",
			"threshold",
			3) && acquired.Contains("diamonds_multi_card_premium"))
		{
			perCardPointBonus += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"diamonds_multi_card_premium",
				"amount",
				1);
		}

		if (input.QuestionRewardEchoActive && acquired.Contains("diamonds_question_mark_echo"))
		{
			perCardPointBonus += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"diamonds_question_mark_echo",
				"amount",
				1);
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
		if (finalDamage >= AdventureUpgradeRuntimeValues.GetNonNegativeInt(
			"hearts_long_capture_recovery",
			"threshold",
			6) && acquired.Contains("hearts_long_capture_recovery"))
		{
			healAmount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_long_capture_recovery",
				"amount",
				2);
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
				drawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_warm_current_supply",
					"count",
					1);
			}

			if (acquired.Contains("hearts_return_tide_heart"))
			{
				drawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_return_tide_heart",
					"first_count",
					1);
				healAmount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_return_tide_heart",
					"first_amount",
					2);
			}
		}
		else if (successfulCapturesThisTurn == 2)
		{
			if (acquired.Contains("hearts_chain_tide_breath"))
			{
				drawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_chain_tide_breath",
					"count",
					1);
				healAmount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_chain_tide_breath",
					"amount",
					2);
			}

			if (acquired.Contains("hearts_return_tide_heart"))
			{
				drawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_return_tide_heart",
					"second_count",
					1);
				healAmount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_return_tide_heart",
					"second_amount",
					3);
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
			HealAmount: AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_enchanted_recovery",
				"amount",
				3),
			DrawCount: successfulCapture
				? AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_enchanted_recovery",
					"count",
					1)
				: 0);
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
}
