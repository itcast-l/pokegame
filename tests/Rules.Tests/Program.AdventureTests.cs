using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static void AdventureUpgradeRulesMapMainStagesAndWeights()
	{
		False(AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(2));
		True(AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(3));
		True(AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(14));
		False(AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(15));

		Equal(1, AdventureUpgradeChoiceRules.ToRunStageIndex(3));
		Equal(4, AdventureUpgradeChoiceRules.ToRunStageIndex(6));
		Equal(12, AdventureUpgradeChoiceRules.ToRunStageIndex(14));

		AdventureUpgradeRarityWeights early = AdventureUpgradeChoiceRules.GetRarityWeights(1);
		AdventureUpgradeRarityWeights mid = AdventureUpgradeChoiceRules.GetRarityWeights(5);
		AdventureUpgradeRarityWeights late = AdventureUpgradeChoiceRules.GetRarityWeights(9);

		Equal(70, early.CommonWeight);
		Equal(25, early.RareWeight);
		Equal(5, early.LegendaryWeight);
		Equal(50, mid.CommonWeight);
		Equal(35, mid.RareWeight);
		Equal(15, mid.LegendaryWeight);
		Equal(30, late.CommonWeight);
		Equal(45, late.RareWeight);
		Equal(25, late.LegendaryWeight);

		False(AdventureUpgradeChoiceRules.ShouldGrantBigCapture(6));
		True(AdventureUpgradeChoiceRules.ShouldGrantBigCapture(7));
	}

	private static void AdventureUpgradeChoiceSkipsAcquiredIdsAndDeduplicatesOffer()
	{
		var definitions = new[]
		{
			Upgrade("common_a", "common"),
			Upgrade("common_b", "common"),
			Upgrade("common_c", "common"),
			Upgrade("rare_a", "rare"),
		};
		var acquiredIds = new HashSet<string>(StringComparer.Ordinal)
		{
			"common_a",
		};
		int[] rolls = { 0, 0, 0, 0, 0, 0 };
		int rollIndex = 0;

		IReadOnlyList<AdventureUpgradeDefinitionModel> offer = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			acquiredIds,
			runStageIndex: 1,
			maxExclusive => rolls[rollIndex++] % maxExclusive);

		Equal(3, offer.Count);
		False(ContainsUpgradeId(offer, "common_a"));
		True(ContainsUpgradeId(offer, "common_b"));
		True(ContainsUpgradeId(offer, "common_c"));
		True(ContainsUpgradeId(offer, "rare_a"));
		True(HasUniqueUpgradeIds(offer));
	}

	private static void AdventureUpgradeChoiceFallsBackWhenRolledRarityUnavailable()
	{
		var definitions = new[]
		{
			Upgrade("common_a", "common"),
			Upgrade("common_b", "common"),
			Upgrade("rare_a", "rare"),
			Upgrade("rare_b", "rare"),
		};
		int[] rolls = { 99, 1, 0, 0, 99, 0 };
		int rollIndex = 0;

		IReadOnlyList<AdventureUpgradeDefinitionModel> offer = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			acquiredIds: Array.Empty<string>(),
			runStageIndex: 1,
			maxExclusive => rolls[rollIndex++] % maxExclusive);

		Equal(3, offer.Count);
		Equal("rare_b", offer[0].Id);
		Equal("common_a", offer[1].Id);
		Equal("rare_a", offer[2].Id);
	}

	private static void AdventureUpgradeChoiceReturnsRemainingPoolWhenBelowOfferSize()
	{
		var definitions = new[]
		{
			Upgrade("legendary_a", "legendary"),
			Upgrade("rare_a", "rare"),
		};

		IReadOnlyList<AdventureUpgradeDefinitionModel> offer = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			acquiredIds: Array.Empty<string>(),
			runStageIndex: 12,
			maxExclusive => throw new InvalidOperationException("random should not be used when fewer than three upgrades remain"));

		Equal(2, offer.Count);
		Equal("legendary_a", offer[0].Id);
		Equal("rare_a", offer[1].Id);
	}

	private static void AdventureUpgradeTriggerOrderingPrefersBigCaptureBeforeVictory()
	{
		IReadOnlyList<AdventureUpgradeTriggerSource> triggerOrder = AdventureUpgradeTriggerRules.BuildTriggerOrder(
			runStartTriggered: false,
			bigCaptureTriggered: true,
			victoryTriggered: true);

		Equal(2, triggerOrder.Count);
		Equal(AdventureUpgradeTriggerSource.BigCapture, triggerOrder[0]);
		Equal(AdventureUpgradeTriggerSource.Victory, triggerOrder[1]);
	}

	private static void AdventureUpgradeTriggerOrderingIncludesRunStartFirst()
	{
		IReadOnlyList<AdventureUpgradeTriggerSource> triggerOrder = AdventureUpgradeTriggerRules.BuildTriggerOrder(
			runStartTriggered: true,
			bigCaptureTriggered: true,
			victoryTriggered: true);

		Equal(3, triggerOrder.Count);
		Equal(AdventureUpgradeTriggerSource.RunStart, triggerOrder[0]);
		Equal(AdventureUpgradeTriggerSource.BigCapture, triggerOrder[1]);
		Equal(AdventureUpgradeTriggerSource.Victory, triggerOrder[2]);
	}

	private static void AdventureUpgradeRunStateTracksStageProgressionAndAcquiredIds()
	{
		var runState = new AdventureUpgradeRunState(
			AdventureUpgradeManifest.FromEnabledIds(new[] { "common_a" }));
		runState.BeginNewRun(avatarStageNumber: 3);

		True(runState.IsActive);
		Equal(1, runState.CurrentRunStageIndex);
		True(runState.ShouldGrantRunStartUpgrade);
		True(runState.ShouldGrantVictoryUpgrade);

		runState.MarkRunStartUpgradeGranted();
		Equal(AdventureUpgradeAcquireResult.Acquired, runState.AcquireUpgrade("common_a"));
		False(runState.ShouldGrantRunStartUpgrade);
		True(runState.HasAcquiredUpgrade("common_a"));

		runState.MarkVictoryUpgradeGranted();
		False(runState.ShouldGrantVictoryUpgrade);

		runState.AdvanceToStage(avatarStageNumber: 4);
		Equal(2, runState.CurrentRunStageIndex);
		False(runState.ShouldGrantRunStartUpgrade);
		True(runState.ShouldGrantVictoryUpgrade);
		True(runState.HasAcquiredUpgrade("common_a"));

		runState.Clear();
		False(runState.IsActive);
		Equal(0, runState.CurrentRunStageIndex);
		False(runState.HasAcquiredUpgrade("common_a"));
	}

	private static void AdventureUpgradeIdsExposeCurrentActiveUpgradesAcrossSuits()
	{
		string repoRoot = FindRepositoryRoot();
		string idsPath = Path.Combine(repoRoot, "pandora", "adventure_suit_upgrade_ids.gd");
		True(File.Exists(idsPath));
		string text = File.ReadAllText(idsPath);

		MatchCollection constants = Regex.Matches(text, @"(?m)^const ([A-Z0-9_]+) = ""([a-z0-9_]+)""$");
		Equal(53, constants.Count);

		var activePerSuit = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["hearts"] = 0,
			["diamonds"] = 0,
			["spades"] = 0,
			["clubs"] = 0,
		};

		foreach (Match constant in constants)
		{
			string id = constant.Groups[2].Value;
			string suit = id.Split('_')[0];
			activePerSuit[suit] = activePerSuit[suit] + 1;
		}

		Equal(14, activePerSuit["hearts"]);
		Equal(13, activePerSuit["diamonds"]);
		Equal(12, activePerSuit["spades"]);
		Equal(14, activePerSuit["clubs"]);
	}

	private static void AdventureSuitUpgradeCatalogExposesOnlyActiveUpgrades()
	{
		string dataPath = Path.Combine(FindRepositoryRoot(), "data.pandora");
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(dataPath));
		JsonElement entities = document.RootElement
			.GetProperty("_entity_data")
			.GetProperty("_entities");

		var suitCounts = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["hearts"] = 0,
			["diamonds"] = 0,
			["spades"] = 0,
			["clubs"] = 0,
		};
		var rarityCounts = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["common"] = 0,
			["rare"] = 0,
			["legendary"] = 0,
		};
		var enabledIds = new HashSet<string>(StringComparer.Ordinal);
		int enabledCount = 0;

		foreach (JsonElement entity in entities.EnumerateArray())
		{
			if (!string.Equals(
				entity.GetProperty("_category_id").GetString(),
				"adventure_suit_upgrades",
				StringComparison.Ordinal))
			{
				continue;
			}

			JsonElement properties = entity.GetProperty("_property_overrides");
			bool enabled = properties.GetProperty("enabled")
				.GetProperty("value").GetBoolean();
			if (!enabled)
			{
				continue;
			}

			string suit = properties.GetProperty("suit")
				.GetProperty("value").GetString() ?? string.Empty;
			string rarity = properties.GetProperty("rarity")
				.GetProperty("value").GetString() ?? string.Empty;
			True(suitCounts.ContainsKey(suit));
			True(rarityCounts.ContainsKey(rarity));
			suitCounts[suit]++;
			rarityCounts[rarity]++;
			enabledCount++;
			enabledIds.Add(entity.GetProperty("_id").GetString() ?? string.Empty);
		}

		Equal(53, enabledCount);
		Equal(14, suitCounts["hearts"]);
		Equal(13, suitCounts["diamonds"]);
		Equal(12, suitCounts["spades"]);
		Equal(14, suitCounts["clubs"]);
		Equal(24, rarityCounts["common"]);
		Equal(19, rarityCounts["rare"]);
		Equal(10, rarityCounts["legendary"]);
		False(enabledIds.Contains("hearts_blood_tide_resonance"));
		False(enabledIds.Contains("diamonds_all_gains_converge"));
		False(enabledIds.Contains("diamonds_high_return_bonus"));
		False(enabledIds.Contains("spades_black_tide_dominion"));
		False(enabledIds.Contains("clubs_lush_growth"));
	}

	private static void AdventureSuitCountingDeduplicatesActiveIds()
	{
		Equal(0, AdventureUpgradeEffectService.CountAcquiredSuitUpgrades(null, "hearts"));
		Equal(0, AdventureUpgradeEffectService.CountAcquiredSuitUpgrades(
			new[] { string.Empty, "diamonds_small_insurance" },
			"hearts"));

		int heartCount = AdventureUpgradeEffectService.CountAcquiredSuitUpgrades(
			new[]
			{
				"hearts_overflow_growth",
				"hearts_warm_current_supply",
				"hearts_warm_current_supply",
				"hearts_long_capture_recovery",
				"hearts_miss_bandage",
				"hearts_question_mark_first_aid",
				"diamonds_small_insurance",
			},
			"hearts");

		Equal(5, heartCount);
	}

	private static void AdventureUpgradeRunSnapshotV3RestoresLegacyV1Saves()
	{
		AdventureUpgradeRunSnapshot legacy = new(
			1,
			3,
			1,
			true,
			true,
			new List<string> { "hearts_thick_blood" });

		var state = new AdventureUpgradeRunState(CreateFullAdventureUpgradeManifest());
		Equal(AdventureUpgradeRestoreResult.Migrated, state.RestoreFromSnapshot(legacy));

		True(state.IsActive);
		Equal(100, state.CurrentHealth);
		Equal(100, state.MaxHealth);
		True(state.HasAcquiredUpgrade("hearts_thick_blood"));
		Equal(0, state.EffectState.PermanentCaptureDamageBonus);
		Equal(0, state.EffectState.QuestionRewardCount);
		Equal(0, state.EffectState.FinalDoubleQuestionRewardCountThisBattle);
		False(state.EffectState.FinalDoubleCaptureDamageActive);
	}

	private static void AdventureUpgradeRunStatePersistsEffectCountersAndHealth()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var state = new AdventureUpgradeRunState(manifest);
		state.BeginNewRun(3);
		state.AcquireUpgrade("diamonds_final_double");
		state.AcquireUpgrade("diamonds_question_mark_interest");
		state.AcquireUpgrade("hearts_overflow_growth");
		state.SetHealth(92, 108);
		state.ReplaceEffectState(new AdventureUpgradeEffectState
		{
			PermanentCaptureDamageBonus = 2,
			QuestionRewardCount = 3,
			FinalDoubleQuestionRewardCountThisBattle = 10,
			FinalDoubleCaptureDamageActive = true,
			HealingOverflowRemainder = 1,
			NextCaptureDamageBonus = 4,
			NextTurnHealAmount = 3,
			NextTurnDrawCount = 1,
		});

		AdventureUpgradeRunSnapshot snapshot = state.ToSnapshot();
		var restored = new AdventureUpgradeRunState(manifest);
		Equal(AdventureUpgradeRestoreResult.Restored, restored.RestoreFromSnapshot(snapshot));

		Equal(92, restored.CurrentHealth);
		Equal(108, restored.MaxHealth);
		Equal(2, restored.EffectState.PermanentCaptureDamageBonus);
		Equal(3, restored.EffectState.QuestionRewardCount);
		Equal(0, restored.EffectState.FinalDoubleQuestionRewardCountThisBattle);
		False(restored.EffectState.FinalDoubleCaptureDamageActive);
		Equal(1, restored.EffectState.HealingOverflowRemainder);
		Equal(0, restored.EffectState.NextCaptureDamageBonus);
		Equal(0, restored.EffectState.NextTurnHealAmount);
		Equal(0, restored.EffectState.NextTurnDrawCount);
	}

	private static void AdventureUpgradeRunSnapshotV4FiltersRetiredFlourishState()
	{
		AdventureUpgradeRunSnapshot legacySnapshot = new(
			4,
			3,
			1,
			false,
			true,
			new List<string>
			{
				"clubs_lush_growth",
				"diamonds_all_gains_converge",
				"hearts_blood_tide_resonance",
				"hearts_thick_blood",
				"spades_black_tide_dominion",
			},
			ClubFlourishStacks: 7);
		var restored = new AdventureUpgradeRunState(CreateFullAdventureUpgradeManifest());
		Equal(AdventureUpgradeRestoreResult.Migrated, restored.RestoreFromSnapshot(legacySnapshot));
		True(restored.HasAcquiredUpgrade("hearts_thick_blood"));
		Equal(1, restored.GetAcquiredUpgradeIds().Count);
		Equal(AdventureUpgradeRunSnapshot.CurrentSchemaVersion, restored.ToSnapshot().SchemaVersion);
	}

	private static void AdventureQuestionRewardSideEffectsTrackCountPermanentBonusAndFinalDouble()
	{
		var state = new AdventureUpgradeEffectState();
		string[] upgrades =
		{
			"diamonds_question_mark_windfall",
			"diamonds_question_mark_interest",
			"diamonds_final_double",
		};

		for (int rewardCount = 1; rewardCount <= 10; rewardCount++)
		{
			AdventureQuestionRewardEffectResult result = AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);
			Equal(Math.Min(5, rewardCount), result.BonusDirectDamage);
			Equal(rewardCount, state.QuestionRewardCount);
			Equal(0, state.FinalDoubleQuestionRewardCountThisBattle);
			Equal(Math.Min(5, rewardCount), state.PermanentCaptureDamageBonus);
			Equal(rewardCount == 10, state.FinalDoubleCaptureDamageActive);
		}

		AdventureQuestionRewardEffectResult capped = AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);

		Equal(5, capped.BonusDirectDamage);
		Equal(10, state.QuestionRewardCount);
		Equal(0, state.FinalDoubleQuestionRewardCountThisBattle);
		Equal(5, state.PermanentCaptureDamageBonus);
		True(state.FinalDoubleCaptureDamageActive);
	}

	private static void AdventureFinalDoublePersistsAcrossEnemies()
	{
		var state = new AdventureUpgradeEffectState();
		string[] upgrades = { "diamonds_final_double" };

		for (int rewardCount = 0; rewardCount < 10; rewardCount++)
		{
			_ = AdventureUpgradeEffectService.OnQuestionRewardGranted(
				state,
				upgrades);
		}

		AdventureCaptureDamageResult firstEnemyResult = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			upgrades,
			new AdventureCaptureDamageInput(BaseDamage: 1));
		Equal(2, firstEnemyResult.FinalDamage);

		AdventureUpgradeEffectService.ClearBattleScopedState(state);
		Equal(10, state.QuestionRewardCount);
		True(state.FinalDoubleCaptureDamageActive);
		AdventureCaptureDamageResult secondEnemyResult = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			upgrades,
			new AdventureCaptureDamageInput(BaseDamage: 1));
		Equal(2, secondEnemyResult.FinalDamage);
	}

	private static void AdventureFinalDoubleSnapshotPreservesBattleProgressAndClearsLegacyV2Status()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var runState = new AdventureUpgradeRunState(manifest);
		runState.BeginNewRun(3);
		runState.AcquireUpgrade("diamonds_final_double");
		for (int rewardCount = 0; rewardCount < 9; rewardCount++)
		{
			_ = AdventureUpgradeEffectService.OnQuestionRewardGranted(
				runState.EffectState,
				runState.GetAcquiredUpgradeIds());
		}

		var restoredCurrentBattle = new AdventureUpgradeRunState(manifest);
		restoredCurrentBattle.RestoreFromSnapshot(runState.ToSnapshot());
		_ = AdventureUpgradeEffectService.OnQuestionRewardGranted(
			restoredCurrentBattle.EffectState,
			restoredCurrentBattle.GetAcquiredUpgradeIds());
		AdventureCaptureDamageResult restoredCurrentBattleResult = AdventureUpgradeEffectService.ApplyCaptureDamage(
			restoredCurrentBattle.EffectState,
			restoredCurrentBattle.GetAcquiredUpgradeIds(),
			new AdventureCaptureDamageInput(BaseDamage: 1));
		Equal(2, restoredCurrentBattleResult.FinalDamage);

		const string legacyV2Json = """
		{
		  "SchemaVersion": 2,
		  "CurrentAvatarStageNumber": 3,
		  "CurrentRunStageIndex": 1,
		  "ShouldGrantRunStartUpgrade": false,
		  "ShouldGrantVictoryUpgrade": true,
		  "AcquiredUpgradeIds": ["diamonds_final_double"],
		  "CurrentHealth": 100,
		  "MaxHealth": 100,
		  "PermanentCaptureDamageBonus": 0,
		  "QuestionRewardCount": 10,
		  "ThirdQuestionDamageDoubleActive": true,
		  "HealingOverflowRemainder": 0,
		  "NextCaptureDamageBonus": 0,
		  "NextTurnHealAmount": 0,
		  "NextTurnDrawCount": 0,
		  "ClubShortBranchReturnUsedThisTurn": false,
		  "ClubMissClimbGrowthStacks": 0,
		  "ClubBorrowSuitSetArmed": false,
		  "ClubGapRootingUsedThisTurn": false
		}
		""";
		AdventureUpgradeRunSnapshot legacyV2Snapshot = JsonSerializer.Deserialize<AdventureUpgradeRunSnapshot>(legacyV2Json);
		var restoredLegacyV2 = new AdventureUpgradeRunState(manifest);
		restoredLegacyV2.RestoreFromSnapshot(legacyV2Snapshot);
		Equal(10, restoredLegacyV2.EffectState.QuestionRewardCount);

		AdventureCaptureDamageResult restoredLegacyV2Result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			restoredLegacyV2.EffectState,
			restoredLegacyV2.GetAcquiredUpgradeIds(),
			new AdventureCaptureDamageInput(BaseDamage: 1));
		Equal(2, restoredLegacyV2Result.FinalDamage);
	}

	private static void AdventureQuestionRewardSupportUpgradesAddHealDrawAndOneTimeRejudgeAccess()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureQuestionRewardEffectResult result = AdventureUpgradeEffectService.OnQuestionRewardGranted(
			state,
			new[]
			{
				"diamonds_question_mark_raise",
				"hearts_question_mark_first_aid",
				"hearts_supply_cache_return",
			});

		Equal(7, result.HealAmount);
		Equal(2, result.DrawCount);
		True(AdventureUpgradeEffectService.ShouldOfferQuestionRewardRejudge(
			new[] { "diamonds_rejudge_once" },
			questionRejudgeUsedThisBattle: false));
		False(AdventureUpgradeEffectService.ShouldOfferQuestionRewardRejudge(
			new[] { "diamonds_rejudge_once" },
			questionRejudgeUsedThisBattle: true));
	}

	private static void QuestionRewardTableRenderOnlyWaitsForLocalHandPlayerAnimation()
	{
		False(QuestionRewardPresentationRules.ShouldDelayTableRenderForQuestionReward(
			hasQuestionRewardAnimation: false,
			isLocalHandPlayer: true));
		False(QuestionRewardPresentationRules.ShouldDelayTableRenderForQuestionReward(
			hasQuestionRewardAnimation: true,
			isLocalHandPlayer: false));
		True(QuestionRewardPresentationRules.ShouldDelayTableRenderForQuestionReward(
			hasQuestionRewardAnimation: true,
			isLocalHandPlayer: true));
	}

	private static void AdventureQuestionRewardDirectDamageReusesPlayerAttackAnimation()
	{
		string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "GameBoardScene.AdventureUpgrades.cs"));
		int methodStart = source.IndexOf("private async Task ApplyAdventureCaptureRewardEffectsAsync(", StringComparison.Ordinal);
		int methodEnd = source.IndexOf("private async Task ApplyAdventureUpgradeAcquisitionEffectsAsync(", methodStart, StringComparison.Ordinal);

		True(methodStart >= 0);
		True(methodEnd > methodStart);

		string method = source[methodStart..methodEnd];
		int attackAnimationIndex = method.IndexOf("await PlayAdventureAvatarAttackAsync(", StringComparison.Ordinal);
		int damageCallbackIndex = method.IndexOf("() => AddPlayerScore(", StringComparison.Ordinal);
		int damageAmountIndex = method.IndexOf("plan.BonusDirectDamage", damageCallbackIndex, StringComparison.Ordinal);

		True(attackAnimationIndex >= 0);
		True(damageCallbackIndex > attackAnimationIndex);
		True(damageAmountIndex > damageCallbackIndex);
	}

	private static void AdventureUpgradeAcquireAppliesImmediateMaxHealthHealAndDrawBonuses()
	{
		AdventureUpgradeAcquisitionEffectResult thickBlood = AdventureUpgradeEffectService.OnUpgradeAcquired(
			new AdventureUpgradeEffectState(),
			"hearts_thick_blood",
			triggerBattleStartBonusImmediately: false);
		AdventureUpgradeAcquisitionEffectResult deepTideLung = AdventureUpgradeEffectService.OnUpgradeAcquired(
			new AdventureUpgradeEffectState(),
			"hearts_deep_tide_lung",
			triggerBattleStartBonusImmediately: true);

		Equal(4, thickBlood.MaxHealthDelta);
		Equal(4, thickBlood.HealAmount);
		Equal(0, thickBlood.ImmediateDrawCount);
		Equal(8, deepTideLung.MaxHealthDelta);
		Equal(0, deepTideLung.HealAmount);
		Equal(1, deepTideLung.ImmediateDrawCount);
	}

	private static void AdventureBattleStartDrawBonusesStackAcrossAcquiredHeartUpgrades()
	{
		int drawCount = AdventureUpgradeEffectService.GetBattleStartDrawCount(new[]
		{
			"hearts_shoal_preparation",
			"hearts_deep_tide_lung",
		});

		Equal(2, drawCount);
	}

	private static void AdventureHealingOverflowIncreasesMaxHealthEveryFourOverflow()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureHealingResult result = AdventureUpgradeEffectService.ApplyHealing(
			currentHealth: 100,
			maxHealth: 100,
			state,
			new[] { "hearts_overflow_growth" },
			healAmount: 9);

		Equal(102, result.CurrentHealth);
		Equal(102, result.MaxHealth);
		Equal(2, result.MaxHealthIncrease);
		Equal(1, result.OverflowRemainder);
		Equal(1, state.HealingOverflowRemainder);
	}

	private static void AdventureMissEffectsQueueTurnStartRecoveryAndNextCaptureBonus()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureMainPlayerMissEffectResult result = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			state,
			new[] { "hearts_miss_bandage", "diamonds_miss_discount" },
			tableCardCountAfterMiss: 1);

		Equal(3, state.NextTurnHealAmount);
		Equal(1, state.NextTurnDrawCount);
		Equal(1, state.NextCaptureDamageBonus);
		False(result.GrantExtraBaseCardAction);
	}

	private static void AdventureMissEffectsIncludeClubTableRewrites()
	{
		AdventureMainPlayerMissEffectResult result = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			new AdventureUpgradeEffectState(),
			new[] { "clubs_hanging_reverse", "clubs_wild_branch", "clubs_canopy_reveal" },
			tableCardCountAfterMiss: 4);

		True(result.InsertPlayedCardAtLeftmost);
		True(result.ShuffleTableAfterMiss);
		True(result.SetCanopyRevealAnchor);
	}

	private static void AdventureTurnStartEffectsAreConsumedOnce()
	{
		var state = new AdventureUpgradeEffectState
		{
			NextTurnHealAmount = 3,
			NextTurnDrawCount = 1,
			ClubShortBranchReturnUsedThisTurn = true,
		};

		AdventureTurnStartEffectResult first = AdventureUpgradeEffectService.ConsumeMainPlayerTurnStartEffects(state);
		AdventureTurnStartEffectResult second = AdventureUpgradeEffectService.ConsumeMainPlayerTurnStartEffects(state);

		Equal(3, first.HealAmount);
		Equal(1, first.DrawCount);
		False(state.ClubShortBranchReturnUsedThisTurn);
		Equal(0, second.HealAmount);
		Equal(0, second.DrawCount);
	}

	private static void AdventureCaptureDamageAppliesMinimumAndFinalDoubleInOrder()
	{
		var state = new AdventureUpgradeEffectState
		{
			QuestionRewardCount = 10,
			FinalDoubleCaptureDamageActive = true,
		};

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_small_insurance", "diamonds_final_double" },
			new AdventureCaptureDamageInput(BaseDamage: 4));

		Equal(10, result.FinalDamage);
		Equal(0, state.NextCaptureDamageBonus);
	}

	private static void AdventureCaptureDamageAppliesFirstCapturePressureOncePerBattle()
	{
		var state = new AdventureUpgradeEffectState();

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_first_capture_pressure" },
			new AdventureCaptureDamageInput(BaseDamage: 4, IsFirstCaptureThisBattle: true));

		Equal(6, result.FinalDamage);
	}

	private static void AdventureCaptureDamageAppliesMultiCardPremiumAndTopPrizeRepeat()
	{
		var state = new AdventureUpgradeEffectState();
		PlayingCard firstCard = new("diamonds", "3", null!);
		PlayingCard secondCard = new("diamonds", "4", null!);
		PlayingCard thirdCard = new("diamonds", "5", null!);
		var first = new TableCard(firstCard, isFaceUp: true);
		var second = new TableCard(secondCard, isFaceUp: true);
		var third = new TableCard(thirdCard, isFaceUp: true);
		var captureContext = new CaptureResolutionContext(
			CreateActionContext(firstCard),
			new[]
			{
				CaptureEntry(first, 0, AdventureCaptureEntrySource.PrimaryRange, isPlayedCard: true),
				CaptureEntry(second, 1, AdventureCaptureEntrySource.PrimaryRange),
				CaptureEntry(third, 2, AdventureCaptureEntrySource.PrimaryRange),
			},
			new[]
			{
				ScoreItem(first, 3, 3),
				ScoreItem(second, 4, 4),
				ScoreItem(third, 5, 5),
				new AdventureCaptureScoreItem(
					third.InstanceId,
					thirdCard.InstanceId,
					5,
					5,
					IsExplicitDuplicate: true,
					SourceUpgradeId: "diamonds_top_prize_repeat"),
			},
			AdventureCaptureMatchSource.Rank);

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_multi_card_premium", "diamonds_top_prize_repeat" },
			new AdventureCaptureDamageInput(
				BaseDamage: 12,
				CaptureContext: captureContext));

		Equal(21, result.FinalDamage);
	}

	private static void AdventureCaptureDamageDoesNotApplyFullPayoutBelowSixScoringCards()
	{
		var state = new AdventureUpgradeEffectState();

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_full_payout" },
			new AdventureCaptureDamageInput(
				BaseDamage: 12,
				ScoringCardRawPoints: new[] { 1, 1, 1, 1, 1 },
				ScoringCardResolvedPoints: new[] { 1, 1, 1, 1, 1 }));

		Equal(12, result.FinalDamage);
	}

	private static void AdventureCaptureDamageDoublesFullPayoutAtSixScoringCardsAfterFinalAdditions()
	{
		var state = new AdventureUpgradeEffectState
		{
			PermanentCaptureDamageBonus = 1,
			NextCaptureDamageBonus = 2,
		};

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_full_payout" },
			new AdventureCaptureDamageInput(
				BaseDamage: 12,
				FlatDamageDelta: 3,
				ScoringCardRawPoints: new[] { 1, 1, 1, 1, 1, 1 },
				ScoringCardResolvedPoints: new[] { 1, 1, 1, 1, 1, 1 }));

		Equal(36, result.FinalDamage);
		Equal(0, state.NextCaptureDamageBonus);
	}

	private static void AdventureCaptureDamageStacksFullPayoutWithFinalDouble()
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
				BaseDamage: 3,
				ScoringCardRawPoints: new[] { 1, 1, 1, 1, 1, 1 },
				ScoringCardResolvedPoints: new[] { 1, 1, 1, 1, 1, 1 }));

		Equal(9, result.FinalDamage);
		Equal(3, result.WholeCaptureMultiplier);
		Equal(0, state.NextCaptureDamageBonus);
	}

	private static void AdventureCaptureDamageAddsBountyAmplifyWhenRewardQuestionIsCaptured()
	{
		var state = new AdventureUpgradeEffectState();

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_bounty_amplify" },
			new AdventureCaptureDamageInput(
				BaseDamage: 6,
				IncludesQuestionReward: true,
				ScoringCardRawPoints: new[] { 2, 4 },
				ScoringCardResolvedPoints: new[] { 2, 4 }));

		Equal(9, result.FinalDamage);
	}

	private static void AdventureCaptureDamageAddsQuestionEchoPointBonusPerScoringCard()
	{
		var state = new AdventureUpgradeEffectState();

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_question_mark_echo" },
			new AdventureCaptureDamageInput(
				BaseDamage: 9,
				QuestionRewardEchoActive: true,
				ScoringCardRawPoints: new[] { 2, 3, 4 },
				ScoringCardResolvedPoints: new[] { 2, 3, 4 }));

		Equal(12, result.FinalDamage);
	}

	private static void AdventurePostCaptureHealTriggersAfterSixDamage()
	{
		AdventurePostCaptureEffectResult result = AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
			new AdventureUpgradeEffectState(),
			new[] { "hearts_long_capture_recovery" },
			finalDamage: 6,
			successfulCapturesThisTurn: 1,
			CreatePostCaptureTestContext());

		Equal(2, result.HealAmount);
	}

	private static void AdventurePostCaptureBonusesStackByCaptureOrdinal()
	{
		AdventurePostCaptureEffectResult first = AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
			new AdventureUpgradeEffectState(),
			new[] { "hearts_warm_current_supply", "hearts_return_tide_heart" },
			finalDamage: 4,
			successfulCapturesThisTurn: 1,
			CreatePostCaptureTestContext());
		AdventurePostCaptureEffectResult second = AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
			new AdventureUpgradeEffectState(),
			new[] { "hearts_chain_tide_breath", "hearts_return_tide_heart" },
			finalDamage: 4,
			successfulCapturesThisTurn: 2,
			CreatePostCaptureTestContext());

		Equal(2, first.DrawCount);
		Equal(2, first.HealAmount);
		Equal(2, second.DrawCount);
		Equal(5, second.HealAmount);
	}

	private static void AdventurePostCaptureIncludesEnchantedRecoveryAndHollowBurst()
	{
		AdventurePostCaptureEffectResult result = AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
			new AdventureUpgradeEffectState(),
			new[] { "hearts_enchanted_recovery", "clubs_hollow_burst" },
			finalDamage: 4,
			successfulCapturesThisTurn: 1,
			CreatePostCaptureTestContext(
				capturedOnlyVisibleEndpoints: true,
				triggeredAttachment: true));

		Equal(3, result.HealAmount);
		Equal(1, result.DrawCount);
		True(result.RevealRemainingHiddenCards);
	}

	private static void AdventureLowHealthRescueTriggersAtHalfAndQuarterThresholdsOnceEach()
	{
		AdventureLowHealthRescueResult half = AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
			previousHealth: 60,
			currentHealth: 45,
			maxHealth: 100,
			new[] { "hearts_red_tide_return", "hearts_revival_draw" },
			halfThresholdUsed: false,
			quarterThresholdUsed: false);
		AdventureLowHealthRescueResult quarter = AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
			previousHealth: 30,
			currentHealth: 20,
			maxHealth: 100,
			new[] { "hearts_red_tide_return", "hearts_revival_draw" },
			halfThresholdUsed: true,
			quarterThresholdUsed: false);
		AdventureLowHealthRescueResult none = AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
			previousHealth: 20,
			currentHealth: 10,
			maxHealth: 100,
			new[] { "hearts_red_tide_return", "hearts_revival_draw" },
			halfThresholdUsed: true,
			quarterThresholdUsed: true);

		Equal(5, half.HealAmount);
		Equal(2, half.DrawCount);
		True(half.ConsumedHalfThreshold);
		Equal(8, quarter.HealAmount);
		Equal(2, quarter.DrawCount);
		True(quarter.ConsumedQuarterThreshold);
		Equal(0, none.HealAmount);
		Equal(0, none.DrawCount);
	}

	private static void AdventureEnemyMissDoesNotQueueCaptureTriggeredRevealAndLineup()
	{
		AdventureEnemyMissEffectResult result = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			new[]
			{
				"spades_dark_harbor_reveal",
				"spades_wrong_shore_lineup",
			},
			tableCardCountAfterMiss: 4,
			hadOldTableCardsBeforeMiss: true);

		False(result.NextCardFacesUp);
		Equal(AdventureEnemyNextCardInsertMode.None, result.InsertMode);
		Equal(0, result.NextCaptureDamageDelta);
		Equal(0, result.DirectDamageToEnemy);
	}

	private static void AdventureEnemyCaptureQueuesRevealAndLineup()
	{
		AdventureEnemyCaptureEffectResult result = AdventureUpgradeEffectService.ResolveEnemyCaptureEffects(
			new[]
			{
				"spades_dark_harbor_reveal",
				"spades_wrong_shore_lineup",
			});

		True(result.NextCardFacesUp);
		Equal(AdventureEnemyNextCardInsertMode.Leftmost, result.InsertMode);
	}

	private static void AdventureEnemyMissEffectsQueueInsertPenaltyAndBleedDamage()
	{
		AdventureEnemyMissEffectResult belowThreshold = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			new[] { "spades_miss_bleed" },
			tableCardCountAfterMiss: 5,
			hadOldTableCardsBeforeMiss: true);
		AdventureEnemyMissEffectResult result = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			new[]
			{
				"spades_dark_harbor_reveal",
				"spades_missed_beat_blunt_hurt",
				"spades_miss_bleed",
				"spades_return_tide_misorder",
			},
			tableCardCountAfterMiss: 6,
			hadOldTableCardsBeforeMiss: true);

		Equal(0, belowThreshold.DirectDamageToEnemy);
		False(result.NextCardFacesUp);
		Equal(AdventureEnemyNextCardInsertMode.BeforeTrackedOldest, result.InsertMode);
		Equal(-3, result.NextCaptureDamageDelta);
		Equal(2, result.DirectDamageToEnemy);
	}

	private static void AdventureEnemyMissMisorderOnlyArmsTrackedAnchorWhenOldCardsExisted()
	{
		AdventureEnemyMissEffectResult armed = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			new[] { "spades_return_tide_misorder" },
			tableCardCountAfterMiss: 2,
			hadOldTableCardsBeforeMiss: true);
		AdventureEnemyMissEffectResult notArmed = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			new[] { "spades_return_tide_misorder" },
			tableCardCountAfterMiss: 1,
			hadOldTableCardsBeforeMiss: false);

		Equal(AdventureEnemyNextCardInsertMode.BeforeTrackedOldest, armed.InsertMode);
		Equal(AdventureEnemyNextCardInsertMode.None, notArmed.InsertMode);
	}

	private static void AdventureEnemyMissGrantsExtraBaseActionFromInterceptBeat()
	{
		AdventureEnemyMissEffectResult result = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			new[] { "spades_intercept_beat" },
			tableCardCountAfterMiss: 2,
			hadOldTableCardsBeforeMiss: false);

		True(result.GrantMainPlayerExtraBaseAction);
	}

	private static void AdventureMainPlayerMissGrantsContinueBranchExtraActionOnlyAtSixCards()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureMainPlayerMissEffectResult granted = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			state,
			new[] { "clubs_continue_branch" },
			tableCardCountAfterMiss: 6);
		AdventureMainPlayerMissEffectResult notGranted = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			new AdventureUpgradeEffectState(),
			new[] { "clubs_continue_branch" },
			tableCardCountAfterMiss: 5);

		True(granted.GrantExtraBaseCardAction);
		False(notGranted.GrantExtraBaseCardAction);
	}

	private static void AdventureBlackLedgerRedirectsHighestScoringMiddleCardAfterEnemyCapture()
	{
		int redirectedIndex = AdventureUpgradeEffectService.ResolveBlackLedgerRedirectTableCardIndex(
			blackLedgerActive: true,
			capturingPlayerIsMain: false,
			tableCardScoreValues: new[] { 13, 4, 10, 2, 12 },
			captureStartIndex: 0);
		int tieIndex = AdventureUpgradeEffectService.ResolveBlackLedgerRedirectTableCardIndex(
			blackLedgerActive: true,
			capturingPlayerIsMain: false,
			tableCardScoreValues: new[] { 0, 8, 8, 0 },
			captureStartIndex: 0);
		int mainCaptureIndex = AdventureUpgradeEffectService.ResolveBlackLedgerRedirectTableCardIndex(
			blackLedgerActive: true,
			capturingPlayerIsMain: true,
			tableCardScoreValues: new[] { 0, 8, 0 },
			captureStartIndex: 0);
		int inactiveIndex = AdventureUpgradeEffectService.ResolveBlackLedgerRedirectTableCardIndex(
			blackLedgerActive: false,
			capturingPlayerIsMain: false,
			tableCardScoreValues: new[] { 0, 8, 0 },
			captureStartIndex: 0);
		int noScoringCardIndex = AdventureUpgradeEffectService.ResolveBlackLedgerRedirectTableCardIndex(
			blackLedgerActive: true,
			capturingPlayerIsMain: false,
			tableCardScoreValues: new[] { 12, 0, 0, 11 },
			captureStartIndex: 0);

		Equal(2, redirectedIndex);
		Equal(1, tieIndex);
		Equal(-1, mainCaptureIndex);
		Equal(-1, inactiveIndex);
		Equal(-1, noScoringCardIndex);
	}

	private static void AdventureEnemyTailPressurePreservesPlayedTailCardOnlyAtSevenCapturedCards()
	{
		True(AdventureUpgradeEffectService.ShouldPreserveEnemyCaptureTailCard(
			new[] { "spades_tail_pressure" },
			totalCapturedCardCount: 7));
		False(AdventureUpgradeEffectService.ShouldPreserveEnemyCaptureTailCard(
			new[] { "spades_tail_pressure" },
			totalCapturedCardCount: 6));
		False(AdventureUpgradeEffectService.ShouldPreserveEnemyCaptureTailCard(
			new[] { "spades_tail_pressure" },
			totalCapturedCardCount: 8));
	}

	private static void AdventureOldTableDamageDeltaAppliesPlayerMarkBonuses()
	{
		int traceHuntDelta = AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
			new[] { "spades_trace_hunt" },
			capturingPlayerIsMain: true,
			capturedOldScoringCardCount: 1,
			capturedTraceMark: true,
			capturedHuntMark: false);
		int huntOrderDelta = AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
			new[] { "spades_hunt_order" },
			capturingPlayerIsMain: true,
			capturedOldScoringCardCount: 1,
			capturedTraceMark: false,
			capturedHuntMark: true);

		Equal(4, traceHuntDelta);
		Equal(6, huntOrderDelta);
	}

	private static void AdventureOldTableDamageDeltaAppliesEnemyTaxAndExaction()
	{
		int taxDelta = AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
			new[] { "spades_reverse_capture_tax" },
			capturingPlayerIsMain: false,
			capturedOldScoringCardCount: 4,
			capturedTraceMark: false,
			capturedHuntMark: false);
		int exactionDelta = AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
			new[] { "spades_exaction" },
			capturingPlayerIsMain: false,
			capturedOldScoringCardCount: 1,
			capturedTraceMark: false,
			capturedHuntMark: false);

		Equal(-4, taxDelta);
		Equal(-4, exactionDelta);
	}

	private static void AdventureClubsPrimaryRootReturnGivesPlayedBaseCardBack()
	{
		AdventureClubCaptureEffectResult result = AdventureUpgradeEffectService.ResolveClubCaptureEffects(
			new AdventureUpgradeEffectState(),
			new[] { "clubs_primary_root_return" },
			completeBaseCaptureCardCount: 3,
			uniqueShortReturnCandidateCount: 0,
			uniqueCombinedReturnCandidateCount: 1,
			availableHandSlots: 6,
			playUsedAttachedEffect: false,
			playedCardWasBaseCard: true);

		True(result.ReturnPlayedCardToHand);
		False(result.ReturnTwoCapturedCardsToHand);
	}

	private static void AdventureClubsShortBranchReturnOnlyAppliesToTwoCardCaptureOnce()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureClubCaptureEffectResult first = AdventureUpgradeEffectService.ResolveClubCaptureEffects(
			state,
			new[] { "clubs_short_branch_return" },
			completeBaseCaptureCardCount: 2,
			uniqueShortReturnCandidateCount: 2,
			uniqueCombinedReturnCandidateCount: 2,
			availableHandSlots: 6,
			playUsedAttachedEffect: false,
			playedCardWasBaseCard: true);
		AdventureClubCaptureEffectResult second = AdventureUpgradeEffectService.ResolveClubCaptureEffects(
			state,
			new[] { "clubs_short_branch_return" },
			completeBaseCaptureCardCount: 2,
			uniqueShortReturnCandidateCount: 2,
			uniqueCombinedReturnCandidateCount: 2,
			availableHandSlots: 6,
			playUsedAttachedEffect: false,
			playedCardWasBaseCard: true);

		True(first.ReturnTwoCapturedCardsToHand);
		False(second.ReturnTwoCapturedCardsToHand);
	}

	private static void AdventureClubsClimbCanopyReturnsPlayedBaseCardAfterAttachedEffectTrigger()
	{
		AdventureClubCaptureEffectResult result = AdventureUpgradeEffectService.ResolveClubCaptureEffects(
			new AdventureUpgradeEffectState(),
			new[] { "clubs_climb_canopy" },
			completeBaseCaptureCardCount: 3,
			uniqueShortReturnCandidateCount: 0,
			uniqueCombinedReturnCandidateCount: 1,
			availableHandSlots: 6,
			playUsedAttachedEffect: true,
			playedCardWasBaseCard: true);

		True(result.ReturnPlayedCardToHand);
	}

	private static void AdventureClubsCrownCanopyIncludesCaptureEndpointsInScoring()
	{
		AdventureClubCaptureScoringResult result = AdventureUpgradeEffectService.ResolveClubCaptureScoring(
			new[] { "clubs_crown_canopy" });

		True(result.IncludeCaptureEndpointsInScoring);
	}

	private static void AdventureClubsGapRootingCapturesLeftAdjacentBaseOncePerTurn()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureClubCaptureExtensionResult first = AdventureUpgradeEffectService.ResolveClubCaptureExtensions(
			state,
			new[] { "clubs_gap_rooting" },
			hasMiddleScoringCards: false,
			leftAdjacentBaseCardAvailable: true);
		AdventureClubCaptureExtensionResult second = AdventureUpgradeEffectService.ResolveClubCaptureExtensions(
			state,
			new[] { "clubs_gap_rooting" },
			hasMiddleScoringCards: false,
			leftAdjacentBaseCardAvailable: true);

		True(first.CaptureLeftAdjacentBaseCard);
		False(second.CaptureLeftAdjacentBaseCard);
	}

	private static void AdventureClubsLinkedStemsGrantsQuestionRewardOnSecondCapture()
	{
		True(AdventureUpgradeEffectService.ShouldGrantLinkedStemsQuestionReward(
			new[] { "clubs_linked_stems" },
			successfulCapturesThisTurn: 2));
		False(AdventureUpgradeEffectService.ShouldGrantLinkedStemsQuestionReward(
			new[] { "clubs_linked_stems" },
			successfulCapturesThisTurn: 1));
	}

	private static void AdventureClubsMissClimbGrowthPreservesPrimaryAndAddsClimbRank()
	{
		var state = new AdventureUpgradeEffectState
		{
			ClubMissClimbGrowthStacks = 2,
		};

		AdventureClubPlayAdjustmentResult result = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_miss_climb_growth" },
			playedCardRank: "4");

		Equal("4", result.PrimaryRank);
		Equal(1, result.AlternateRanks.Count);
		Equal("6", result.AlternateRanks[0]);
	}

	private static void AdventureClubsMissClimbGrowthPrioritizesPrimaryBeforeClimbRank()
	{
		var state = new AdventureUpgradeEffectState
		{
			ClubMissClimbGrowthStacks = 2,
		};
		AdventureClubPlayAdjustmentResult result = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_miss_climb_growth" },
			playedCardRank: "4");

		CardPlayPlan primaryPlan = CardPlayResolver.Resolve(
			new[]
			{
				new CardPlayTableCard("6", IsFaceUp: true, IsBuriedInSand: false),
				new CardPlayTableCard("4", IsFaceUp: true, IsBuriedInSand: false),
			},
			new CardPlayCard(result.PrimaryRank, CollectsTable: false),
			result.AlternateRanks,
			insertAtTableStart: false);
		Equal(1, primaryPlan.MatchingTableCardIndex);
		Equal("4", primaryPlan.EffectiveRank);

		CardPlayPlan climbPlan = CardPlayResolver.Resolve(
			new[]
			{
				new CardPlayTableCard("6", IsFaceUp: true, IsBuriedInSand: false),
			},
			new CardPlayCard(result.PrimaryRank, CollectsTable: false),
			result.AlternateRanks,
			insertAtTableStart: false);
		Equal(0, climbPlan.MatchingTableCardIndex);
		Equal("6", climbPlan.EffectiveRank);
	}

	private static void AdventureClubsMissClimbGrowthAndCrossGraftAdjustMatchRanks()
	{
		var state = new AdventureUpgradeEffectState
		{
			ClubMissClimbGrowthStacks = 2,
		};

		AdventureClubPlayAdjustmentResult result = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_miss_climb_growth", "clubs_cross_graft" },
			playedCardRank: "4");

		Equal("4", result.PrimaryRank);
		Equal(4, result.AlternateRanks.Count);
		Equal("6", result.AlternateRanks[0]);
		Equal("3", result.AlternateRanks[1]);
		Equal("5", result.AlternateRanks[2]);
		Equal("7", result.AlternateRanks[3]);
	}

	private static void AdventureClubsMissClimbGrowthDeduplicatesAndClampsRanks()
	{
		var state = new AdventureUpgradeEffectState
		{
			ClubMissClimbGrowthStacks = 1,
		};
		AdventureClubPlayAdjustmentResult oneStack = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_miss_climb_growth", "clubs_cross_graft" },
			playedCardRank: "4");
		Equal(3, oneStack.AlternateRanks.Count);
		Equal("5", oneStack.AlternateRanks[0]);
		Equal("3", oneStack.AlternateRanks[1]);
		Equal("6", oneStack.AlternateRanks[2]);

		AdventureClubPlayAdjustmentResult lowerBoundary = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_miss_climb_growth", "clubs_cross_graft" },
			playedCardRank: "1");
		Equal(2, lowerBoundary.AlternateRanks.Count);
		Equal("2", lowerBoundary.AlternateRanks[0]);
		Equal("3", lowerBoundary.AlternateRanks[1]);

		AdventureClubPlayAdjustmentResult upperBoundary = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_miss_climb_growth", "clubs_cross_graft" },
			playedCardRank: "10");
		Equal(1, upperBoundary.AlternateRanks.Count);
		Equal("9", upperBoundary.AlternateRanks[0]);
	}

	private static void AdventureClubsMatchAdjustmentsPreserveUnchangedCases()
	{
		var state = new AdventureUpgradeEffectState
		{
			ClubMissClimbGrowthStacks = 2,
		};

		AdventureClubPlayAdjustmentResult noGrowth = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			Array.Empty<string>(),
			playedCardRank: "4");
		Equal("4", noGrowth.PrimaryRank);
		Equal(0, noGrowth.AlternateRanks.Count);

		AdventureClubPlayAdjustmentResult nonNumeric = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_miss_climb_growth", "clubs_cross_graft" },
			playedCardRank: "Q");
		Equal("Q", nonNumeric.PrimaryRank);
		Equal(0, nonNumeric.AlternateRanks.Count);

		AdventureClubPlayAdjustmentResult crossGraftOnly = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_cross_graft" },
			playedCardRank: "4");
		Equal("4", crossGraftOnly.PrimaryRank);
		Equal(2, crossGraftOnly.AlternateRanks.Count);
		Equal("3", crossGraftOnly.AlternateRanks[0]);
		Equal("5", crossGraftOnly.AlternateRanks[1]);
	}

	private static void AdventureClubsBorrowSuitSetArmsAndIsConsumedByNextNumericBaseCard()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureQuestionRewardEffectResult _ = AdventureUpgradeEffectService.OnQuestionRewardGranted(
			state,
			new[] { "clubs_borrow_suit_set" });

		AdventureClubPlayAdjustmentResult armed = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_borrow_suit_set" },
			playedCardRank: "4");
		AdventureUpgradeEffectService.ConsumeBorrowSuitSetAfterNumericBaseCardPlay(state, "4");
		AdventureClubPlayAdjustmentResult consumed = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_borrow_suit_set" },
			playedCardRank: "4");

		True(armed.CollectBySuitOnce);
		False(consumed.CollectBySuitOnce);
	}

	private static void AdventureDisabledUpgradeStaysOutOfEnabledPool()
	{
		var definitions = new[]
		{
			new AdventureUpgradeDefinitionModel(
				"spades_disabled_upgrade",
				"Disabled",
				"spades",
				"rare",
				"",
				"",
				Enabled: false),
			new AdventureUpgradeDefinitionModel(
				"spades_dark_harbor_reveal",
				"Reveal",
				"spades",
				"common",
				"",
				"",
				Enabled: true),
		};

		IReadOnlyList<AdventureUpgradeDefinitionModel> offer = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			Array.Empty<string>(),
			runStageIndex: 1,
			nextIntExclusive: _ => 0);

		Equal(1, offer.Count);
		Equal("spades_dark_harbor_reveal", offer[0].Id);
	}


}
