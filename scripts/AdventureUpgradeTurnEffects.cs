using System;
using System.Collections.Generic;

internal static class AdventureUpgradeTurnEffects
{
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
			drawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_shoal_preparation",
				"count",
				1);
		}

		if (acquired.Contains("hearts_deep_tide_lung"))
		{
			drawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_deep_tide_lung",
				"count",
				1);
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
			state.NextTurnHealAmount = AdventureUpgradeEffectMath.SaturatingAddToInt(
				state.NextTurnHealAmount,
				AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_miss_bandage",
					"amount",
					3));
			state.NextTurnDrawCount = AdventureUpgradeEffectMath.SaturatingAddToInt(
				state.NextTurnDrawCount,
				AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"hearts_miss_bandage",
					"count",
					1));
		}

		if (acquired.Contains("diamonds_miss_discount"))
		{
			state.NextCaptureDamageBonus = AdventureUpgradeEffectMath.SaturatingAddToInt(
				state.NextCaptureDamageBonus,
				AdventureUpgradeRuntimeValues.GetNonNegativeInt(
					"diamonds_miss_discount",
					"amount",
					1));
		}

		if (acquired.Contains("clubs_miss_climb_growth"))
		{
			int growthAmount = AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"clubs_miss_climb_growth",
				"amount",
				1);
			int growthStackLimit = AdventureUpgradeRuntimeValues.GetPositiveInt(
				"clubs_miss_climb_growth",
				"stack_limit",
				AdventureUpgradeEffectState.MaxClubMissClimbGrowthStacks);
			state.ClubMissClimbGrowthStacks = Math.Min(
				growthStackLimit,
				state.ClubMissClimbGrowthStacks + growthAmount);
		}

		if (acquired.Contains("clubs_continue_branch")
			&& tableCardCountAfterMiss >= AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"clubs_continue_branch",
				"threshold",
				6))
		{
			grantExtraBaseCardAction = true;
		}

		return new AdventureMainPlayerMissEffectResult(
			grantExtraBaseCardAction,
			insertPlayedCardAtLeftmost,
			shuffleTableAfterMiss,
			setCanopyRevealAnchor);
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

		int normalizedMaxHealth = Math.Clamp(
			maxHealth,
			1,
			AdventureUpgradeRuntimeValues.GetPositiveInt(
				"hearts_overflow_growth",
				"maximum",
				AdventureUpgradeEffectService.MaxRunHealth));
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
				AdventureUpgradeEffectMath.SaturatingAdd(normalizedCurrentHealth, normalizedHealAmount));
			return new AdventureHealingResult(
				healedHealth,
				normalizedMaxHealth,
				healedHealth - normalizedCurrentHealth,
				0,
				state.HealingOverflowRemainder);
		}

		long rawHealedHealth = AdventureUpgradeEffectMath.SaturatingAdd(normalizedCurrentHealth, normalizedHealAmount);
		long overflowAmount = Math.Max(0L, rawHealedHealth - normalizedMaxHealth);
		long overflowTotal = AdventureUpgradeEffectMath.SaturatingAdd(state.HealingOverflowRemainder, overflowAmount);
		int maxHealthLimit = AdventureUpgradeRuntimeValues.GetPositiveInt(
			"hearts_overflow_growth",
			"maximum",
			AdventureUpgradeEffectService.MaxRunHealth);
		int overflowStep = AdventureUpgradeRuntimeValues.GetPositiveInt(
			"hearts_overflow_growth",
			"step",
			4);
		int overflowGrowthAmount = AdventureUpgradeRuntimeValues.GetPositiveInt(
			"hearts_overflow_growth",
			"amount",
			1);
		int availableMaxHealthIncrease = Math.Max(0, maxHealthLimit - normalizedMaxHealth);
		int maxHealthIncrease = (int)Math.Min(
			availableMaxHealthIncrease,
			AdventureUpgradeEffectMath.SaturatingMultiply(overflowTotal / overflowStep, overflowGrowthAmount));
		state.HealingOverflowRemainder = (int)(overflowTotal % overflowStep);

		int nextMaxHealth = normalizedMaxHealth + maxHealthIncrease;
		int nextCurrentHealth = (int)Math.Min(rawHealedHealth, nextMaxHealth);
		return new AdventureHealingResult(
			nextCurrentHealth,
			nextMaxHealth,
			nextCurrentHealth - normalizedCurrentHealth,
			maxHealthIncrease,
			state.HealingOverflowRemainder);
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

		int halfThresholdPercent = AdventureUpgradeRuntimeValues.GetNonNegativeInt(
			"hearts_red_tide_return",
			"threshold",
			50);
		int quarterThresholdPercent = AdventureUpgradeRuntimeValues.GetNonNegativeInt(
			"hearts_revival_draw",
			"threshold",
			25);
		if (!halfThresholdUsed
			&& previousRatio >= halfThresholdPercent / 100.0f
			&& currentRatio < halfThresholdPercent / 100.0f
			&& acquired.Contains("hearts_red_tide_return"))
		{
			healAmount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_red_tide_return",
				"amount",
				5);
			drawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_red_tide_return",
				"count",
				2);
			consumedHalfThreshold = true;
		}

		if (!quarterThresholdUsed
			&& previousRatio >= quarterThresholdPercent / 100.0f
			&& currentRatio < quarterThresholdPercent / 100.0f
			&& acquired.Contains("hearts_revival_draw"))
		{
			healAmount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_revival_draw",
				"amount",
				8);
			drawCount += AdventureUpgradeRuntimeValues.GetNonNegativeInt(
				"hearts_revival_draw",
				"count",
				2);
			consumedQuarterThreshold = true;
		}

		return new AdventureLowHealthRescueResult(
			healAmount,
			drawCount,
			consumedHalfThreshold,
			consumedQuarterThreshold);
	}
}
