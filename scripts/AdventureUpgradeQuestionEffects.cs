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
		return new AdventureQuestionRejudgeRuleResult(
			offered,
			ConsumesOpportunity: offered,
			ShouldReroll: offered && choice == AdventureQuestionRejudgeRuleChoice.Reroll);
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
}

