using System;
using System.Collections.Generic;

internal static class AdventureUpgradeEnemyEffects
{
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

internal static bool ShouldPreserveEnemyCaptureTailCard(
		IEnumerable<string> acquiredUpgradeIds,
		int totalCapturedCardCount)
	{
		var acquired = new HashSet<string>(acquiredUpgradeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
		return acquired.Contains("spades_tail_pressure") && totalCapturedCardCount == 7;
	}
}

