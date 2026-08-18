using System;
using System.Collections.Generic;

internal static class SoloAiRules
{
	internal static SoloAiDifficulty NormalizeDifficulty(int difficultyValue)
	{
		return difficultyValue switch
		{
			<= 0 => SoloAiDifficulty.Low,
			1 => SoloAiDifficulty.Medium,
			_ => SoloAiDifficulty.High,
		};
	}

	internal static SoloAiProfile GetProfile(SoloAiDifficulty difficulty)
	{
		return difficulty switch
		{
			SoloAiDifficulty.Low => new SoloAiProfile(25, 25, false, false),
			SoloAiDifficulty.Medium => new SoloAiProfile(55, 20, false, true),
			_ => new SoloAiProfile(78, 12, true, true),
		};
	}

	internal static bool TryPickCaptureCardIndex(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards,
		CaptureScoringContext context,
		SoloAiDifficulty difficulty,
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

		SoloAiProfile profile = GetProfile(difficulty);
		int rollPercent = Math.Clamp(decisionRollPercent, 0, 99);
		if (rollPercent < profile.BestCaptureChancePercent)
		{
			return TryPickBestCaptureCardIndex(
				tableCards,
				handCards,
				context,
				profile,
				royalCaptureThreshold,
				out handIndex);
		}

		if (rollPercent < profile.BestCaptureChancePercent + profile.AnyCaptureChancePercent)
		{
			return TryPickAnyCaptureCardIndex(
				tableCards,
				handCards,
				profile,
				randomCaptureOrdinal,
				out handIndex);
		}

		return false;
	}

	internal static int PickDiscardDisplayIndex(
		SoloAiDifficulty difficulty,
		IReadOnlyList<SoloAiHandCardDecisionCandidate> normalHandCards,
		int totalDisplayCount,
		int tieBreakOrdinal)
	{
		if (totalDisplayCount <= 0)
		{
			return -1;
		}

		if (difficulty == SoloAiDifficulty.Low)
		{
			return NormalizeOrdinal(tieBreakOrdinal, totalDisplayCount);
		}

		if (difficulty == SoloAiDifficulty.High)
		{
			int protectedDiscardIndex = PickLowestScoreIndex(
				normalHandCards,
				candidate => !candidate.CanCaptureNow,
				tieBreakOrdinal);
			if (protectedDiscardIndex >= 0)
			{
				return protectedDiscardIndex;
			}
		}

		int mediumDiscardIndex = PickLowestScoreIndex(normalHandCards, static _ => true, tieBreakOrdinal);
		return mediumDiscardIndex >= 0
			? mediumDiscardIndex
			: NormalizeOrdinal(tieBreakOrdinal, totalDisplayCount);
	}

	internal static int PickMagnetHandIndex(
		SoloAiDifficulty difficulty,
		IReadOnlyList<SoloAiHandCardDecisionCandidate> legalHandCards,
		int tieBreakOrdinal)
	{
		if (legalHandCards.Count == 0)
		{
			return -1;
		}

		if (difficulty == SoloAiDifficulty.Low)
		{
			return legalHandCards[NormalizeOrdinal(tieBreakOrdinal, legalHandCards.Count)].Index;
		}

		if (difficulty == SoloAiDifficulty.High)
		{
			int protectedAttachIndex = PickLowestScoreIndex(
				legalHandCards,
				candidate => !candidate.CanCaptureNow,
				tieBreakOrdinal);
			if (protectedAttachIndex >= 0)
			{
				return protectedAttachIndex;
			}
		}

		return PickLowestScoreIndex(legalHandCards, static _ => true, tieBreakOrdinal);
	}

	private static bool TryPickBestCaptureCardIndex(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards,
		CaptureScoringContext context,
		SoloAiProfile profile,
		int royalCaptureThreshold,
		out int handIndex)
	{
		handIndex = -1;
		int bestPrimaryScore = 0;
		int bestCaptureCount = 0;
		for (int index = 0; index < handCards.Count; index++)
		{
			PotentialCaptureCard card = handCards[index];
			PotentialCaptureEvaluation evaluation =
				PotentialCaptureRules.EvaluatePotentialNormalCapture(tableCards, card);
			int captureCount = evaluation.NormalCaptureCount;
			if (!evaluation.CanCapture
				|| (profile.AvoidPairOnlyCaptures && evaluation.IsPairOnlyCapture))
			{
				continue;
			}

			int primaryScore = profile.UsePointScoring
				? PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, context, evaluation)
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
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards,
		SoloAiProfile profile,
		int randomCaptureOrdinal,
		out int handIndex)
	{
		handIndex = -1;
		var candidateIndexes = new List<int>();
		for (int index = 0; index < handCards.Count; index++)
		{
			PotentialCaptureCard card = handCards[index];
			PotentialCaptureEvaluation evaluation =
				PotentialCaptureRules.EvaluatePotentialNormalCapture(tableCards, card);
			if (!evaluation.CanCapture)
			{
				continue;
			}

			if (profile.AvoidPairOnlyCaptures && evaluation.IsPairOnlyCapture)
			{
				continue;
			}

			candidateIndexes.Add(index);
		}

		if (candidateIndexes.Count == 0)
		{
			return false;
		}

		handIndex = candidateIndexes[NormalizeOrdinal(randomCaptureOrdinal, candidateIndexes.Count)];
		return true;
	}

	private static int PickLowestScoreIndex(
		IReadOnlyList<SoloAiHandCardDecisionCandidate> candidates,
		Func<SoloAiHandCardDecisionCandidate, bool> predicate,
		int tieBreakOrdinal)
	{
		int bestScore = int.MaxValue;
		var bestIndexes = new List<int>();
		foreach (SoloAiHandCardDecisionCandidate candidate in candidates)
		{
			if (!predicate(candidate))
			{
				continue;
			}

			if (candidate.ScoreValue < bestScore)
			{
				bestScore = candidate.ScoreValue;
				bestIndexes.Clear();
				bestIndexes.Add(candidate.Index);
			}
			else if (candidate.ScoreValue == bestScore)
			{
				bestIndexes.Add(candidate.Index);
			}
		}

		return bestIndexes.Count == 0
			? -1
			: bestIndexes[NormalizeOrdinal(tieBreakOrdinal, bestIndexes.Count)];
	}

	private static int NormalizeOrdinal(int ordinal, int count)
	{
		if (count <= 0)
		{
			return -1;
		}

		int normalized = (ordinal & int.MaxValue) % count;
		return normalized < 0 ? normalized + count : normalized;
	}
}
