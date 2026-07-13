using System;
using System.Collections.Generic;

internal static class AdventureUpgradeQuestionEffects
{
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
		bool shouldReroll = offered && choice == AdventureQuestionRejudgeRuleChoice.Reroll;
		return new AdventureQuestionRejudgeRuleResult(
			offered,
			ConsumesOpportunity: shouldReroll,
			ShouldReroll: shouldReroll);
	}

internal static AdventureQuestionRewardEffectResult OnQuestionRewardGranted(
		AdventureUpgradeEffectState state,
		IEnumerable<string> acquiredUpgradeIds)
	{
		ArgumentNullException.ThrowIfNull(state);

		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		state.Normalize();
		int windfallMaximum = AdventureUpgradeRuntimeValues.GetPositiveInt(
			"diamonds_question_mark_windfall",
			"maximum",
			AdventureUpgradeEffectState.MaxQuestionRewardCount);
		int questionRewardIncrement = AdventureUpgradeRuntimeValues.GetPositiveInt(
			"diamonds_question_mark_windfall",
			"amount",
			1);
		state.QuestionRewardCount = Math.Min(
			windfallMaximum,
			state.QuestionRewardCount + questionRewardIncrement);

		int bonusDirectDamage = 0;
		int healAmount = 0;
		int drawCount = 0;

		if (acquired.Contains("diamonds_question_mark_windfall"))
		{
			int directDamageCap = AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"diamonds_question_mark_windfall",
				"threshold",
				5);
			bonusDirectDamage = Math.Min(
				directDamageCap,
				AdventureUpgradeRuntimeValues.GetPositiveInt(
					"diamonds_question_mark_windfall",
					"amount",
					1) * state.QuestionRewardCount);
		}

		if (acquired.Contains("diamonds_question_mark_interest"))
		{
			state.PermanentCaptureDamageBonus = Math.Min(
				AdventureUpgradeEffectState.MaxPermanentCaptureDamageBonus,
				state.PermanentCaptureDamageBonus + AdventureUpgradeRuntimeValues.GetPositiveInt(
					"diamonds_question_mark_interest",
					"amount",
					1));
		}

		if (acquired.Contains("diamonds_question_mark_raise"))
		{
			drawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
			"diamonds_question_mark_raise",
			"count",
			1);
		}

		if (acquired.Contains("clubs_borrow_suit_set"))
		{
			state.ClubBorrowSuitSetArmed = true;
		}

		if (acquired.Contains("hearts_question_mark_first_aid"))
		{
			healAmount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_question_mark_first_aid",
				"amount",
				4);
		}

		if (acquired.Contains("hearts_supply_cache_return"))
		{
			healAmount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_supply_cache_return",
				"amount",
				3);
			drawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_supply_cache_return",
				"count",
				1);
		}

		if (acquired.Contains("diamonds_final_double")
			&& state.QuestionRewardCount >= AdventureUpgradeRuntimeValues.GetPositiveInt(
				"diamonds_final_double",
				"threshold",
				AdventureUpgradeEffectState.MaxQuestionRewardCount))
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
				maxHealthDelta += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_thick_blood",
					"amount",
					4);
				healAmount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_thick_blood",
					"amount",
					4);
				break;
			case "hearts_deep_tide_lung":
				maxHealthDelta += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_deep_tide_lung",
					"amount",
					8);
				if (triggerBattleStartBonusImmediately)
				{
					immediateDrawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
						"hearts_deep_tide_lung",
						"count",
						1);
				}
				break;
			case "hearts_shoal_preparation":
				if (triggerBattleStartBonusImmediately)
				{
					immediateDrawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
						"hearts_shoal_preparation",
						"count",
						1);
				}
				break;
			case "diamonds_final_double":
				state.Normalize();
				if (state.QuestionRewardCount >= AdventureUpgradeRuntimeValues.GetPositiveInt(
					"diamonds_final_double",
					"threshold",
					AdventureUpgradeEffectState.MaxQuestionRewardCount))
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
}
