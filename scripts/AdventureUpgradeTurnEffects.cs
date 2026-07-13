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
			state.NextTurnHealAmount = AdventureUpgradeEffectMath.SaturatingAddToInt(state.NextTurnHealAmount, 3);
			state.NextTurnDrawCount = AdventureUpgradeEffectMath.SaturatingAddToInt(state.NextTurnDrawCount, 1);
		}

		if (acquired.Contains("diamonds_miss_discount"))
		{
			state.NextCaptureDamageBonus = AdventureUpgradeEffectMath.SaturatingAddToInt(state.NextCaptureDamageBonus, 1);
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

		int normalizedMaxHealth = Math.Clamp(maxHealth, 1, AdventureUpgradeEffectService.MaxRunHealth);
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
		int availableMaxHealthIncrease = AdventureUpgradeEffectService.MaxRunHealth - normalizedMaxHealth;
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
}

