using System;

internal static partial class Program
{
	private static void RunAdventureUpgradeEffectRemediationTests()
	{
		Run("adventure capture multiplier bucket caps combined card multiplier at six", CaptureMultiplierBucketCapsAtSix);
		Run("EndpointScoringIdentityDedup", EndpointScoringIdentityDedup);
		Run("adventure top prize repeat breaks ties by leftmost pre-capture position", TopPrizeRepeatBreaksTiesByLeftmostPreCapturePosition);
		Run("adventure damage event caps at target max health 150 percent", DamageEventCapsAtTargetMaxHealth150Percent);
		Run("adventure question run progress caps and applies on the next capture", QuestionRunProgressCapsAndAppliesNextCapture);
		Run("QuestionRewardModifiersApplyFromNextCapture", QuestionRewardModifiersApplyFromNextCapture);
		Run("adventure effect arithmetic saturates instead of wrapping", SaturatingArithmetic);
		Run("adventure stronger enemy damage reduction wins", StrongerEnemyDamageReductionWins);
		Run("adventure low health rescues aggregate from one damage snapshot", LowHealthRescuesAggregateFromSameDamageSnapshot);
		Run("adventure effect state metadata declares scopes and caps", EffectStateMetadataDeclaresScopesAndCaps);
		Run("adventure battle scope reset preserves run state", BattleScopeStateReset);
		Run("adventure borrow suit set prefers rank before suit fallback", BorrowSuitSetRankFirstSuitFallback);
		Run("adventure borrow suit set expires at real turn end", BorrowSuitSetExpiresAtRealTurnEnd);
		Run("adventure borrow suit set refreshes without stacking", BorrowSuitSetRefreshesWithoutStacking);
		Run("adventure rejudge keep preserves opportunity until reroll", RejudgeKeepPreservesOpportunityUntilReroll);
		Run("adventure short return capacity uses unique cards", ShortReturnCapacityUsesUniqueCards);
		Run("adventure short return counts capture extensions", ShortReturnCountsCaptureExtensions);
		Run("adventure post capture effects consume the resolution context", PostCaptureEffectsConsumeResolutionContext);
	}

	private static void EndpointScoringIdentityDedup()
	{
		PlayingCard playedCard = new("clubs", "6", null!);
		var endpoint = new TableCard(playedCard, isFaceUp: true);
		var context = new CaptureResolutionContext(
			CreateActionContext(playedCard),
			new[]
			{
				CaptureEntry(endpoint, 0, AdventureCaptureEntrySource.PrimaryRange, isPlayedCard: true),
			},
			new[]
			{
				ScoreItem(endpoint, 6, 6),
				ScoreItem(endpoint, 6, 6),
				new AdventureCaptureScoreItem(
					endpoint.InstanceId,
					playedCard.InstanceId,
					6,
					6,
					IsExplicitDuplicate: true,
					SourceUpgradeId: "diamonds_top_prize_repeat"),
			},
			AdventureCaptureMatchSource.Rank,
			new[] { "endpoint_crown" });

		Equal(1, context.NormalScoreItems.Count);
		Equal(1, context.ExplicitDuplicateScoreItems.Count);
		AdventureCaptureDamageResult damage = AdventureUpgradeEffectService.ApplyCaptureDamage(
			new AdventureUpgradeEffectState(),
			new[] { "clubs_crown_canopy", "diamonds_top_prize_repeat" },
			new AdventureCaptureDamageInput(BaseDamage: 6, CaptureContext: context.ToRuleFacts()));
		Equal(12, damage.FinalDamage);
	}

	private static void TopPrizeRepeatBreaksTiesByLeftmostPreCapturePosition()
	{
		var scoreItems = new[]
		{
			new AdventureCaptureScoreItem(1, 11, 4, 4, false, string.Empty),
			new AdventureCaptureScoreItem(2, 12, 9, 9, false, string.Empty),
			new AdventureCaptureScoreItem(99, 13, 9, 9, false, string.Empty),
		};

		Equal(
			2,
			AdventureCaptureScoreItemRules.ResolveTopPrizeRepeatIndex(
				scoreItems,
				new[] { 4, 7, 2 }));
		Equal(
			-1,
			AdventureCaptureScoreItemRules.ResolveTopPrizeRepeatIndex(
				scoreItems[..2],
				new[] { 4, 7 }));
	}

	private static void CaptureMultiplierBucketCapsAtSix()
	{
		var state = new AdventureUpgradeEffectState
		{
			QuestionRewardCount = 10,
			FinalDoubleCaptureDamageActive = true,
		};
		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_full_payout", "diamonds_final_double" },
			new AdventureCaptureDamageInput(
				BaseDamage: 20,
				ScoringCardRawPoints: new[] { 1, 1, 1, 1, 1, 1 },
				ScoringCardResolvedPoints: new[] { 2, 2, 2, 2, 2, 2 }));

		Equal(3, result.WholeCaptureMultiplier);
		Equal(60, result.FinalDamage);
	}

	private static void DamageEventCapsAtTargetMaxHealth150Percent()
	{
		AdventureCaptureDamageResult oddMaximum = AdventureUpgradeEffectService.ApplyCaptureDamage(
			new AdventureUpgradeEffectState(),
			Array.Empty<string>(),
			new AdventureCaptureDamageInput(BaseDamage: 500, TargetMaxHealth: 101));
		AdventureCaptureDamageResult evenMaximum = AdventureUpgradeEffectService.ApplyCaptureDamage(
			new AdventureUpgradeEffectState(),
			Array.Empty<string>(),
			new AdventureCaptureDamageInput(BaseDamage: 500, TargetMaxHealth: 100));

		Equal(152, oddMaximum.FinalDamage);
		True(oddMaximum.DamageCapped);
		Equal(150, evenMaximum.FinalDamage);
		True(evenMaximum.DamageCapped);
	}

	private static void QuestionRunProgressCapsAndAppliesNextCapture()
	{
		var state = new AdventureUpgradeEffectState
		{
			QuestionRewardCount = 9,
			PermanentCaptureDamageBonus = 4,
		};
		string[] upgrades =
		{
			"diamonds_question_mark_windfall",
			"diamonds_question_mark_interest",
			"diamonds_question_mark_echo",
			"diamonds_final_double",
		};
		AdventureCaptureDamageModifierSnapshot beforeTenth =
			AdventureUpgradeEffectService.FreezeCaptureDamageModifiers(state, upgrades);
		AdventureQuestionRewardEffectResult tenth =
			AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);

		Equal(5, tenth.BonusDirectDamage);
		Equal(10, state.QuestionRewardCount);
		Equal(5, state.PermanentCaptureDamageBonus);
		True(state.FinalDoubleCaptureDamageActive);

		AdventureCaptureDamageResult sameCapture = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			upgrades,
			new AdventureCaptureDamageInput(
				BaseDamage: 1,
				ScoringCardRawPoints: new[] { 1 },
				ScoringCardResolvedPoints: new[] { 1 },
				FrozenModifiers: beforeTenth));
		Equal(6, sameCapture.FinalDamage);

		AdventureCaptureDamageModifierSnapshot afterTenth =
			AdventureUpgradeEffectService.FreezeCaptureDamageModifiers(state, upgrades);
		AdventureCaptureDamageResult nextCapture = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			upgrades,
			new AdventureCaptureDamageInput(
				BaseDamage: 1,
				ScoringCardRawPoints: new[] { 1 },
				ScoringCardResolvedPoints: new[] { 1 },
				FrozenModifiers: afterTenth));
		Equal(14, nextCapture.FinalDamage);

		for (int index = 0; index < 5; index++)
		{
			AdventureQuestionRewardEffectResult capped =
				AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);
			Equal(5, capped.BonusDirectDamage);
		}
		Equal(10, state.QuestionRewardCount);
		Equal(5, state.PermanentCaptureDamageBonus);
	}

	private static void QuestionRewardModifiersApplyFromNextCapture()
	{
		var state = new AdventureUpgradeEffectState();
		string[] upgrades =
		{
			"diamonds_question_mark_interest",
			"diamonds_question_mark_echo",
		};
		AdventureCaptureDamageModifierSnapshot frozen =
			AdventureUpgradeEffectService.FreezeCaptureDamageModifiers(state, upgrades);
		_ = AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);

		AdventureCaptureDamageResult sameCapture = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			upgrades,
			new AdventureCaptureDamageInput(
				BaseDamage: 1,
				ScoringCardRawPoints: new[] { 1 },
				ScoringCardResolvedPoints: new[] { 1 },
				FrozenModifiers: frozen));
		AdventureCaptureDamageResult nextCapture = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			upgrades,
			new AdventureCaptureDamageInput(
				BaseDamage: 1,
				ScoringCardRawPoints: new[] { 1 },
				ScoringCardResolvedPoints: new[] { 1 },
				FrozenModifiers: AdventureUpgradeEffectService.FreezeCaptureDamageModifiers(state, upgrades)));

		Equal(1, sameCapture.FinalDamage);
		Equal(3, nextCapture.FinalDamage);
	}

	private static void SaturatingArithmetic()
	{
		var state = new AdventureUpgradeEffectState
		{
			NextCaptureDamageBonus = int.MaxValue,
			NextTurnHealAmount = int.MaxValue,
			NextTurnDrawCount = int.MaxValue,
		};
		_ = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			state,
			new[] { "hearts_miss_bandage", "diamonds_miss_discount" },
			tableCardCountAfterMiss: int.MaxValue);
		Equal(int.MaxValue, state.NextTurnHealAmount);
		Equal(int.MaxValue, state.NextTurnDrawCount);
		Equal(int.MaxValue, state.NextCaptureDamageBonus);

		AdventureCaptureDamageResult damage = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			Array.Empty<string>(),
			new AdventureCaptureDamageInput(
				BaseDamage: int.MaxValue,
				FlatDamageDelta: int.MaxValue));
		Equal(int.MaxValue, damage.FinalDamage);

		AdventureHealingResult healing = AdventureUpgradeEffectService.ApplyHealing(
			currentHealth: int.MaxValue,
			maxHealth: int.MaxValue,
			state,
			new[] { "hearts_overflow_growth" },
			healAmount: int.MaxValue);
		Equal(AdventureUpgradeEffectService.MaxRunHealth, healing.MaxHealth);
		Equal(AdventureUpgradeEffectService.MaxRunHealth, healing.CurrentHealth);
		Equal(0, healing.MaxHealthIncrease);
		Equal(3, healing.OverflowRemainder);
	}

	private static void StrongerEnemyDamageReductionWins()
	{
		Equal(-6, AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
			new[] { "spades_exaction", "spades_reverse_capture_tax" },
			capturingPlayerIsMain: false,
			capturedOldScoringCardCount: 6,
			capturedTraceMark: false,
			capturedHuntMark: false));
		Equal(-4, AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
			new[] { "spades_exaction", "spades_reverse_capture_tax" },
			capturingPlayerIsMain: false,
			capturedOldScoringCardCount: 2,
			capturedTraceMark: false,
			capturedHuntMark: false));

		AdventureCaptureDamageResult enemyDamage = AdventureUpgradeEffectService.ApplyCaptureDamage(
			new AdventureUpgradeEffectState(),
			Array.Empty<string>(),
			new AdventureCaptureDamageInput(
				BaseDamage: 10,
				FlatDamageDelta: -4,
				CapturerIsMainPlayer: false,
				EnemyDamageReduction: 6));
		AdventureCaptureDamageResult playerDamage = AdventureUpgradeEffectService.ApplyCaptureDamage(
			new AdventureUpgradeEffectState(),
			Array.Empty<string>(),
			new AdventureCaptureDamageInput(
				BaseDamage: 10,
				CapturerIsMainPlayer: true,
				EnemyDamageReduction: 6));
		Equal(4, enemyDamage.FinalDamage);
		Equal(10, playerDamage.FinalDamage);
	}

	private static void LowHealthRescuesAggregateFromSameDamageSnapshot()
	{
		AdventureLowHealthRescueResult result =
			AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
				previousHealth: 60,
				currentHealth: 20,
				maxHealth: 100,
				new[] { "hearts_red_tide_return", "hearts_revival_draw" },
				halfThresholdUsed: false,
				quarterThresholdUsed: false);

		Equal(13, result.HealAmount);
		Equal(4, result.DrawCount);
		True(result.ConsumedHalfThreshold);
		True(result.ConsumedQuarterThreshold);
	}

	private static void BattleScopeStateReset()
	{
		var state = new AdventureUpgradeEffectState
		{
			PermanentCaptureDamageBonus = 5,
			QuestionRewardCount = 10,
			FinalDoubleCaptureDamageActive = true,
			HealingOverflowRemainder = 3,
			NextCaptureDamageBonus = 7,
			NextTurnHealAmount = 3,
			NextTurnDrawCount = 1,
			ClubShortBranchReturnUsedThisTurn = true,
			ClubMissClimbGrowthStacks = 9,
			ClubBorrowSuitSetArmed = true,
			ClubGapRootingUsedThisTurn = true,
		};

		AdventureUpgradeEffectService.ClearBattleScopedState(state);

		Equal(5, state.PermanentCaptureDamageBonus);
		Equal(10, state.QuestionRewardCount);
		True(state.FinalDoubleCaptureDamageActive);
		Equal(3, state.HealingOverflowRemainder);
		Equal(0, state.NextCaptureDamageBonus);
		Equal(0, state.NextTurnHealAmount);
		Equal(0, state.NextTurnDrawCount);
		False(state.ClubShortBranchReturnUsedThisTurn);
		Equal(0, state.ClubMissClimbGrowthStacks);
		False(state.ClubBorrowSuitSetArmed);
		False(state.ClubGapRootingUsedThisTurn);
		Equal(0, state.FinalDoubleQuestionRewardCountThisBattle);
	}

	private static void EffectStateMetadataDeclaresScopesAndCaps()
	{
		var metadata = AdventureUpgradeEffectState.FieldMetadata;
		Equal(12, metadata.Count);
		Equal(AdventureEffectStateScope.Run, metadata[nameof(AdventureUpgradeEffectState.PermanentCaptureDamageBonus)].Scope);
		Equal(5, metadata[nameof(AdventureUpgradeEffectState.PermanentCaptureDamageBonus)].MaximumValue);
		Equal(AdventureEffectStateScope.Run, metadata[nameof(AdventureUpgradeEffectState.QuestionRewardCount)].Scope);
		Equal(10, metadata[nameof(AdventureUpgradeEffectState.QuestionRewardCount)].MaximumValue);
		Equal(AdventureEffectStateScope.Battle, metadata[nameof(AdventureUpgradeEffectState.NextCaptureDamageBonus)].Scope);
		Equal(AdventureEffectStateScope.NextTurnCarry, metadata[nameof(AdventureUpgradeEffectState.NextTurnHealAmount)].Scope);
		Equal(AdventureEffectStateScope.NextTurnCarry, metadata[nameof(AdventureUpgradeEffectState.NextTurnDrawCount)].Scope);
		Equal(AdventureEffectStateScope.Turn, metadata[nameof(AdventureUpgradeEffectState.ClubShortBranchReturnUsedThisTurn)].Scope);
		Equal(AdventureEffectStateScope.Battle, metadata[nameof(AdventureUpgradeEffectState.ClubMissClimbGrowthStacks)].Scope);
		Equal(9, metadata[nameof(AdventureUpgradeEffectState.ClubMissClimbGrowthStacks)].MaximumValue);
		Equal(AdventureEffectStateScope.Turn, metadata[nameof(AdventureUpgradeEffectState.ClubBorrowSuitSetArmed)].Scope);
		Equal(AdventureEffectStateScope.Turn, metadata[nameof(AdventureUpgradeEffectState.ClubGapRootingUsedThisTurn)].Scope);
		Equal(0, metadata[nameof(AdventureUpgradeEffectState.FinalDoubleQuestionRewardCountThisBattle)].MaximumValue);
	}

	private static void BorrowSuitSetRankFirstSuitFallback()
	{
		var state = new AdventureUpgradeEffectState
		{
			ClubBorrowSuitSetArmed = true,
		};
		AdventureClubPlayAdjustmentResult adjustment =
			AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
				state,
				new[] { "clubs_borrow_suit_set", "clubs_cross_graft" },
				playedCardRank: "4");
		var playedCard = new CardPlayCard(
			adjustment.PrimaryRank,
			CollectsTable: false,
			Suit: "clubs",
			CollectBySuit: adjustment.CollectBySuitOnce,
			SuitMatchIsFallbackOnly: adjustment.SuitMatchIsFallbackOnly);

		CardPlayPlan primaryRank = CardPlayResolver.Resolve(
			new[]
			{
				new CardPlayTableCard("9", IsFaceUp: true, IsBuriedInSand: false, Suit: "clubs"),
				new CardPlayTableCard("4", IsFaceUp: true, IsBuriedInSand: false, Suit: "hearts"),
				new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false, Suit: "spades"),
			},
			playedCard,
			adjustment.AlternateRanks,
			insertAtTableStart: false);
		Equal(1, primaryRank.MatchingTableCardIndex);
		Equal("4", primaryRank.EffectiveRank);

		CardPlayPlan alternateRank = CardPlayResolver.Resolve(
			new[]
			{
				new CardPlayTableCard("9", IsFaceUp: true, IsBuriedInSand: false, Suit: "clubs"),
				new CardPlayTableCard("5", IsFaceUp: true, IsBuriedInSand: false, Suit: "hearts"),
				new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false, Suit: "spades"),
			},
			playedCard,
			adjustment.AlternateRanks,
			insertAtTableStart: false);
		Equal(1, alternateRank.MatchingTableCardIndex);
		Equal("5", alternateRank.EffectiveRank);

		CardPlayPlan suitFallback = CardPlayResolver.Resolve(
			new[]
			{
				new CardPlayTableCard("9", IsFaceUp: true, IsBuriedInSand: false, Suit: "clubs"),
				new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false, Suit: "spades"),
			},
			playedCard,
			adjustment.AlternateRanks,
			insertAtTableStart: false);
		Equal(0, suitFallback.MatchingTableCardIndex);
		True(adjustment.SuitMatchIsFallbackOnly);
	}

	private static void BorrowSuitSetExpiresAtRealTurnEnd()
	{
		var state = new AdventureUpgradeEffectState
		{
			ClubBorrowSuitSetArmed = true,
		};
		AdventureUpgradeEffectService.ClearTurnScopedState(state);
		False(state.ClubBorrowSuitSetArmed);
	}

	private static void BorrowSuitSetRefreshesWithoutStacking()
	{
		var state = new AdventureUpgradeEffectState();
		string[] upgrades = { "clubs_borrow_suit_set" };
		_ = AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);
		_ = AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);
		True(state.ClubBorrowSuitSetArmed);

		AdventureUpgradeEffectService.ConsumeBorrowSuitSetAfterNumericBaseCardPlay(state, "4");
		False(state.ClubBorrowSuitSetArmed);
		AdventureUpgradeEffectService.ConsumeBorrowSuitSetAfterNumericBaseCardPlay(state, "4");
		False(state.ClubBorrowSuitSetArmed);
	}

	private static void RejudgeKeepPreservesOpportunityUntilReroll()
	{
		AdventureQuestionRejudgeRuleResult kept =
			AdventureUpgradeEffectService.ResolveQuestionRewardRejudgeChoice(
				new[] { "diamonds_rejudge_once" },
				questionRejudgeUsedThisBattle: false,
				AdventureQuestionRejudgeRuleChoice.Keep);
		AdventureQuestionRejudgeRuleResult rerolledLater =
			AdventureUpgradeEffectService.ResolveQuestionRewardRejudgeChoice(
				new[] { "diamonds_rejudge_once" },
				questionRejudgeUsedThisBattle: kept.ConsumesOpportunity,
				AdventureQuestionRejudgeRuleChoice.Reroll);
		AdventureQuestionRejudgeRuleResult alreadyUsed =
			AdventureUpgradeEffectService.ResolveQuestionRewardRejudgeChoice(
				new[] { "diamonds_rejudge_once" },
				questionRejudgeUsedThisBattle: true,
				AdventureQuestionRejudgeRuleChoice.Reroll);

		True(kept.Offered);
		False(kept.ConsumesOpportunity);
		False(kept.ShouldReroll);
		True(rerolledLater.Offered);
		True(rerolledLater.ConsumesOpportunity);
		True(rerolledLater.ShouldReroll);
		False(alreadyUsed.Offered);
		False(alreadyUsed.ConsumesOpportunity);
		False(alreadyUsed.ShouldReroll);
	}

	private static void ShortReturnCapacityUsesUniqueCards()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureClubCaptureEffectResult deduplicated =
			AdventureUpgradeEffectService.ResolveClubCaptureEffects(
				state,
				new[] { "clubs_short_branch_return", "clubs_primary_root_return" },
				completeBaseCaptureCardCount: 2,
				uniqueShortReturnCandidateCount: 2,
				uniqueCombinedReturnCandidateCount: 2,
				availableHandSlots: 2,
				playedCardWasBaseCard: true);
		True(deduplicated.ReturnPlayedCardToHand);
		True(deduplicated.ReturnTwoCapturedCardsToHand);

		var insufficientState = new AdventureUpgradeEffectState();
		AdventureClubCaptureEffectResult insufficient =
			AdventureUpgradeEffectService.ResolveClubCaptureEffects(
				insufficientState,
				new[] { "clubs_short_branch_return", "clubs_primary_root_return" },
				completeBaseCaptureCardCount: 2,
				uniqueShortReturnCandidateCount: 2,
				uniqueCombinedReturnCandidateCount: 3,
				availableHandSlots: 2,
				playedCardWasBaseCard: true);
		True(insufficient.ReturnPlayedCardToHand);
		False(insufficient.ReturnTwoCapturedCardsToHand);
		False(insufficientState.ClubShortBranchReturnUsedThisTurn);
	}

	private static void ShortReturnCountsCaptureExtensions()
	{
		AdventureClubCaptureEffectResult result =
			AdventureUpgradeEffectService.ResolveClubCaptureEffects(
				new AdventureUpgradeEffectState(),
				new[] { "clubs_short_branch_return" },
				completeBaseCaptureCardCount: 3,
				uniqueShortReturnCandidateCount: 2,
				uniqueCombinedReturnCandidateCount: 2,
				availableHandSlots: 2,
				playedCardWasBaseCard: true);
		False(result.ReturnTwoCapturedCardsToHand);
	}

	private static void PostCaptureEffectsConsumeResolutionContext()
	{
		PlayingCard playedCard = new("clubs", "4", null!);
		PlayingCard matchedCard = new("hearts", "4", null!);
		var first = new TableCard(matchedCard, isFaceUp: true);
		var second = new TableCard(playedCard, isFaceUp: true);
		var context = new CaptureResolutionContext(
			CreateActionContext(playedCard),
			new[]
			{
				CaptureEntry(first, 1, AdventureCaptureEntrySource.PrimaryRange),
				CaptureEntry(second, 2, AdventureCaptureEntrySource.PrimaryRange, isPlayedCard: true),
			},
			Array.Empty<AdventureCaptureScoreItem>(),
			AdventureCaptureMatchSource.Rank,
			new[] { "double" });

		AdventurePostCaptureEffectResult result =
			AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
				new AdventureUpgradeEffectState(),
				new[]
				{
					"hearts_long_capture_recovery",
					"hearts_enchanted_recovery",
					"clubs_hollow_burst",
				},
				finalDamage: 6,
				successfulCapturesThisTurn: 1,
				context.ToRuleFacts());
		Equal(5, result.HealAmount);
		Equal(1, result.DrawCount);
		True(result.RevealRemainingHiddenCards);

		var fallbackOnly = new CaptureResolutionContext(
			CreateActionContext(playedCard),
			new[]
			{
				CaptureEntry(first, 1, AdventureCaptureEntrySource.PrimaryRange),
				CaptureEntry(second, 2, AdventureCaptureEntrySource.PrimaryRange, isPlayedCard: true),
			},
			Array.Empty<AdventureCaptureScoreItem>(),
			AdventureCaptureMatchSource.SuitFallback);
		AdventurePostCaptureEffectResult noFakeAttachment =
			AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
				new AdventureUpgradeEffectState(),
					new[] { "hearts_enchanted_recovery" },
					finalDamage: 0,
					successfulCapturesThisTurn: 1,
					fallbackOnly.ToRuleFacts());
		Equal(0, noFakeAttachment.HealAmount);
		Equal(0, noFakeAttachment.DrawCount);
	}
}
