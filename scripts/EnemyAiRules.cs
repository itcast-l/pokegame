using System;
using System.Collections.Generic;

internal static class EnemyAiRules
{
	internal readonly record struct AdventureProfile(
		int BestCaptureChancePercent,
		int AnyCaptureChancePercent,
		bool UsePointScoring,
		bool AvoidPairOnlyCaptures);

	internal static AdventureProfile GetAdventureProfile(int stageNumber)
	{
		return stageNumber switch
		{
			<= 1 => new AdventureProfile(25, 15, false, true),
			2 => new AdventureProfile(35, 20, false, true),
			3 => new AdventureProfile(45, 25, false, true),
			4 => new AdventureProfile(55, 25, false, true),
			5 => new AdventureProfile(65, 20, false, true),
			6 => new AdventureProfile(70, 20, false, true),
			7 => new AdventureProfile(75, 15, true, true),
			8 => new AdventureProfile(78, 15, true, true),
			9 => new AdventureProfile(85, 10, true, true),
			10 => new AdventureProfile(90, 5, true, true),
			11 => new AdventureProfile(92, 5, true, true),
			12 => new AdventureProfile(95, 5, true, true),
			13 => new AdventureProfile(97, 3, true, true),
			_ => new AdventureProfile(100, 0, true, true),
		};
	}

	internal static bool TryPickCaptureCardIndex(
		IReadOnlyList<CaptureScoringRules.TableCard> tableCards,
		IReadOnlyList<PotentialCaptureRules.Card> handCards,
		CaptureScoringRules.Context context,
		AdventureProfile profile,
		int royalCaptureThreshold,
		int decisionRollPercent,
		int randomCaptureOrdinal,
		out int handIndex)
	{
		handIndex = -1;
		if (tableCards.Count == 0 || handCards.Count == 0)
		{
			return false;
		}

		int rollPercent = Math.Clamp(decisionRollPercent, 0, 99);
		if (rollPercent < profile.BestCaptureChancePercent)
		{
			return TryPickBestCaptureCardIndex(
				tableCards,
				handCards,
				context,
				profile.UsePointScoring,
				profile.AvoidPairOnlyCaptures,
				royalCaptureThreshold,
				out handIndex);
		}

		if (rollPercent < profile.BestCaptureChancePercent + profile.AnyCaptureChancePercent)
		{
			return TryPickAnyCaptureCardIndex(
				tableCards,
				handCards,
				profile.AvoidPairOnlyCaptures,
				randomCaptureOrdinal,
				out handIndex);
		}

		return false;
	}

	private static bool TryPickBestCaptureCardIndex(
		IReadOnlyList<CaptureScoringRules.TableCard> tableCards,
		IReadOnlyList<PotentialCaptureRules.Card> handCards,
		CaptureScoringRules.Context context,
		bool usePointScoring,
		bool avoidPairOnlyCaptures,
		int royalCaptureThreshold,
		out int handIndex)
	{
		handIndex = -1;
		int bestPrimaryScore = 0;
		int bestCaptureCount = 0;
		for (int index = 0; index < handCards.Count; index++)
		{
			PotentialCaptureRules.Card card = handCards[index];
			int captureCount = PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card);
			if (captureCount <= 0 || (avoidPairOnlyCaptures && PotentialCaptureRules.IsPairOnlyCapture(tableCards, card)))
			{
				continue;
			}

			int primaryScore = usePointScoring
				? PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, context)
				: captureCount;
			if (captureCount >= royalCaptureThreshold)
			{
				primaryScore += 1000 + captureCount;
			}

			if (primaryScore > bestPrimaryScore || (primaryScore == bestPrimaryScore && captureCount > bestCaptureCount))
			{
				bestPrimaryScore = primaryScore;
				bestCaptureCount = captureCount;
				handIndex = index;
			}
		}

		return handIndex >= 0;
	}

	private static bool TryPickAnyCaptureCardIndex(
		IReadOnlyList<CaptureScoringRules.TableCard> tableCards,
		IReadOnlyList<PotentialCaptureRules.Card> handCards,
		bool avoidPairOnlyCaptures,
		int randomCaptureOrdinal,
		out int handIndex)
	{
		handIndex = -1;
		int preferredCaptureCardCount = 0;
		foreach (PotentialCaptureRules.Card card in handCards)
		{
			if (PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card) > 0
				&& (!avoidPairOnlyCaptures || !PotentialCaptureRules.IsPairOnlyCapture(tableCards, card)))
			{
				preferredCaptureCardCount++;
			}
		}

		if (preferredCaptureCardCount == 0)
		{
			return false;
		}

		int targetOrdinal = randomCaptureOrdinal % preferredCaptureCardCount;
		if (targetOrdinal < 0)
		{
			targetOrdinal += preferredCaptureCardCount;
		}

		int currentOrdinal = 0;
		for (int index = 0; index < handCards.Count; index++)
		{
			if (PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, handCards[index]) <= 0
				|| (avoidPairOnlyCaptures && PotentialCaptureRules.IsPairOnlyCapture(tableCards, handCards[index])))
			{
				continue;
			}

			if (currentOrdinal == targetOrdinal)
			{
				handIndex = index;
				return true;
			}

			currentOrdinal++;
		}

		return false;
	}
}
