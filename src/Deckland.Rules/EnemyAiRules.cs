using System;
using System.Collections.Generic;

internal static class EnemyAiRules
{
	private static readonly EnemyAiAdventureSelectionResult NoAdventureSelection =
		new(EnemyAiAdventureSelectionKind.None, -1);

	internal static EnemyAiAdventureProfile GetAdventureProfile(int mainStageIndex)
	{
		return mainStageIndex switch
		{
			<= 1 => new EnemyAiAdventureProfile(45, 25, 55, 55, false),
			2 => new EnemyAiAdventureProfile(55, 25, 65, 55, false),
			3 => new EnemyAiAdventureProfile(65, 20, 70, 55, false),
			4 => new EnemyAiAdventureProfile(70, 20, 75, 35, false),
			5 => new EnemyAiAdventureProfile(75, 15, 80, 35, true),
			6 => new EnemyAiAdventureProfile(78, 15, 85, 35, true),
			7 => new EnemyAiAdventureProfile(85, 10, 90, 20, true),
			8 => new EnemyAiAdventureProfile(90, 5, 92, 20, true),
			9 => new EnemyAiAdventureProfile(92, 5, 94, 20, true),
			10 => new EnemyAiAdventureProfile(95, 5, 96, 10, true),
			11 => new EnemyAiAdventureProfile(97, 3, 98, 10, true),
			_ => new EnemyAiAdventureProfile(65, 10, 90, 10, true),
		};
	}

	internal static EnemyAiAdventureSelectionResult SelectAdventureCard(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards,
		CaptureScoringContext context,
		EnemyAiAdventureProfile profile,
		int royalCaptureThreshold,
		int decisionRollPercent,
		int randomCandidateOrdinal)
	{
		if (handCards.Count == 0)
		{
			return NoAdventureSelection;
		}

		var nonPairCaptureIndexes = new List<int>();
		var pairOnlyCaptureIndexes = new List<int>();
		for (int handIndex = 0; handIndex < handCards.Count; handIndex++)
		{
			PotentialCaptureCard card = handCards[handIndex];
			PotentialCaptureEvaluation evaluation =
				PotentialCaptureRules.EvaluatePotentialNormalCapture(tableCards, card);
			if (!evaluation.CanCapture)
			{
				continue;
			}

			if (evaluation.IsPairOnlyCapture)
			{
				pairOnlyCaptureIndexes.Add(handIndex);
			}
			else
			{
				nonPairCaptureIndexes.Add(handIndex);
			}
		}

		int rollPercent = Math.Clamp(decisionRollPercent, 0, 99);
		if (nonPairCaptureIndexes.Count > 0)
		{
			if (rollPercent < profile.BestCaptureChancePercent)
			{
				return new EnemyAiAdventureSelectionResult(
					EnemyAiAdventureSelectionKind.Capture,
					PickBestCaptureCardIndex(
						tableCards,
						handCards,
						context,
						profile.UsePointScoring,
						royalCaptureThreshold,
						nonPairCaptureIndexes));
			}

			if (rollPercent < profile.BestCaptureChancePercent + profile.AnyCaptureChancePercent)
			{
				return new EnemyAiAdventureSelectionResult(
					EnemyAiAdventureSelectionKind.Capture,
					PickCandidateByOrdinal(nonPairCaptureIndexes, randomCandidateOrdinal));
			}

			return NoAdventureSelection;
		}

		if (pairOnlyCaptureIndexes.Count > 0)
		{
			return rollPercent < profile.PairOnlyCaptureChancePercent
				? new EnemyAiAdventureSelectionResult(
					EnemyAiAdventureSelectionKind.Capture,
					PickCandidateByOrdinal(pairOnlyCaptureIndexes, randomCandidateOrdinal))
				: NoAdventureSelection;
		}

		if (tableCards.Count <= 2 && rollPercent < profile.EarlyBuildChancePercent)
		{
			var buildIndexes = new List<int>(handCards.Count);
			for (int handIndex = 0; handIndex < handCards.Count; handIndex++)
			{
				if (PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, handCards[handIndex]) == 0)
				{
					buildIndexes.Add(handIndex);
				}
			}

			if (buildIndexes.Count > 0)
			{
				return new EnemyAiAdventureSelectionResult(
					EnemyAiAdventureSelectionKind.Build,
					PickCandidateByOrdinal(buildIndexes, randomCandidateOrdinal));
			}
		}

		return NoAdventureSelection;
	}

	internal static bool TryPickLegacyCaptureCardIndex(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards,
		CaptureScoringContext context,
		int bestCaptureChancePercent,
		int anyCaptureChancePercent,
		bool usePointScoring,
		bool avoidPairOnlyCaptures,
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

		var captureIndexes = new List<int>();
		for (int index = 0; index < handCards.Count; index++)
		{
			PotentialCaptureCard card = handCards[index];
			PotentialCaptureEvaluation evaluation =
				PotentialCaptureRules.EvaluatePotentialNormalCapture(tableCards, card);
			if (evaluation.CanCapture
				&& (!avoidPairOnlyCaptures || !evaluation.IsPairOnlyCapture))
			{
				captureIndexes.Add(index);
			}
		}

		if (captureIndexes.Count == 0)
		{
			return false;
		}

		int rollPercent = Math.Clamp(decisionRollPercent, 0, 99);
		if (rollPercent < bestCaptureChancePercent)
		{
			handIndex = PickBestCaptureCardIndex(
				tableCards,
				handCards,
				context,
				usePointScoring,
				royalCaptureThreshold,
				captureIndexes);
			return true;
		}

		if (rollPercent < bestCaptureChancePercent + anyCaptureChancePercent)
		{
			handIndex = PickCandidateByOrdinal(captureIndexes, randomCaptureOrdinal);
			return true;
		}

		return false;
	}

	private static int PickBestCaptureCardIndex(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards,
		CaptureScoringContext context,
		bool usePointScoring,
		int royalCaptureThreshold,
		IReadOnlyList<int> candidateIndexes)
	{
		int bestHandIndex = candidateIndexes[0];
		int bestPrimaryScore = int.MinValue;
		int bestCaptureCount = int.MinValue;
		foreach (int handIndex in candidateIndexes)
		{
			PotentialCaptureCard card = handCards[handIndex];
			PotentialCaptureEvaluation evaluation =
				PotentialCaptureRules.EvaluatePotentialNormalCapture(tableCards, card);
			int captureCount = evaluation.NormalCaptureCount;
			int primaryScore = usePointScoring
				? PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, context, evaluation)
				: captureCount;
			if (captureCount >= royalCaptureThreshold)
			{
				primaryScore += 1000 + captureCount;
			}

			if (primaryScore > bestPrimaryScore
				|| (primaryScore == bestPrimaryScore && captureCount > bestCaptureCount))
			{
				bestPrimaryScore = primaryScore;
				bestCaptureCount = captureCount;
				bestHandIndex = handIndex;
			}
		}

		return bestHandIndex;
	}

	private static int PickCandidateByOrdinal(IReadOnlyList<int> candidateIndexes, int ordinal)
	{
		int normalizedOrdinal = ordinal % candidateIndexes.Count;
		if (normalizedOrdinal < 0)
		{
			normalizedOrdinal += candidateIndexes.Count;
		}

		return candidateIndexes[normalizedOrdinal];
	}
}
