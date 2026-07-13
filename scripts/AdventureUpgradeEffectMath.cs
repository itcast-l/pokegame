using System;
using System.Collections.Generic;

internal static class AdventureUpgradeEffectMath
{
	internal const int FullPayoutScoringCardThreshold = 6;

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

internal static AdventureUpgradeEffectState CloneState(AdventureUpgradeEffectState? state)
	{
		return state?.Clone() ?? new AdventureUpgradeEffectState();
	}

internal static long ApplySmallInsurance(long damage)
	{
		return damage is >= 1 and <= 4 ? 5 : damage;
	}

internal static long GetDamageEventCap(int targetMaxHealth)
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

internal static int SaturatingAddToInt(int value, int delta)
	{
		return ToSafeNonNegativeInt(SaturatingAdd(Math.Max(0L, value), Math.Max(0L, delta)));
	}

internal static int ToSafeNonNegativeInt(long value)
	{
		return (int)Math.Clamp(value, 0L, int.MaxValue);
	}

internal static long SaturatingAdd(long left, long right)
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

internal static long SaturatingMultiply(long left, long right)
	{
		if (left <= 0 || right <= 0)
		{
			return 0;
		}

		return left > long.MaxValue / right ? long.MaxValue : left * right;
	}
}
