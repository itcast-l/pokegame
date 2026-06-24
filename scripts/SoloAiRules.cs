using System;
using System.Collections.Generic;

internal static class SoloAiRules
{
	internal enum Difficulty
	{
		Low = 0,
		Medium = 1,
		High = 2,
	}

	internal enum TargetPreferenceKind
	{
		PlayerHand,
		PlayerDeck,
		PlayerScore,
	}

	internal readonly record struct Profile(
		int BestCaptureChancePercent,
		int AnyCaptureChancePercent,
		bool UsePointScoring,
		bool AvoidPairOnlyCaptures);

	internal readonly record struct PlayerTargetCandidate(
		int PlayerIndex,
		int TotalHandCardCount,
		int DeckCount,
		int Score);

	internal readonly record struct HandCardDecisionCandidate(
		int Index,
		int ScoreValue,
		bool CanCaptureNow);

	internal static Difficulty NormalizeDifficulty(int difficultyValue)
	{
		return difficultyValue switch
		{
			<= 0 => Difficulty.Low,
			1 => Difficulty.Medium,
			_ => Difficulty.High,
		};
	}

	internal static Profile GetProfile(Difficulty difficulty)
	{
		return difficulty switch
		{
			Difficulty.Low => new Profile(25, 25, false, false),
			Difficulty.Medium => new Profile(55, 20, false, true),
			_ => new Profile(78, 12, true, true),
		};
	}

	internal static bool TryPickCaptureCardIndex(
		IReadOnlyList<CaptureScoringRules.TableCard> tableCards,
		IReadOnlyList<PotentialCaptureRules.Card> handCards,
		CaptureScoringRules.Context context,
		Difficulty difficulty,
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

		Profile profile = GetProfile(difficulty);
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

	internal static int PickItemTargetPlayer(
		Difficulty difficulty,
		TargetPreferenceKind targetKind,
		IReadOnlyList<PlayerTargetCandidate> candidates,
		int strategyRollPercent,
		int tieBreakOrdinal)
	{
		if (candidates.Count == 0)
		{
			return -1;
		}

		if (difficulty == Difficulty.Low)
		{
			return candidates[NormalizeOrdinal(tieBreakOrdinal, candidates.Count)].PlayerIndex;
		}

		if (difficulty == Difficulty.Medium && Math.Clamp(strategyRollPercent, 0, 99) >= 50)
		{
			return candidates[NormalizeOrdinal(tieBreakOrdinal, candidates.Count)].PlayerIndex;
		}

		int bestMetric = int.MinValue;
		var bestIndexes = new List<int>();
		for (int index = 0; index < candidates.Count; index++)
		{
			PlayerTargetCandidate candidate = candidates[index];
			int metric = targetKind switch
			{
				TargetPreferenceKind.PlayerHand => candidate.TotalHandCardCount,
				TargetPreferenceKind.PlayerDeck => candidate.DeckCount,
				_ => candidate.Score,
			};

			if (metric > bestMetric)
			{
				bestMetric = metric;
				bestIndexes.Clear();
				bestIndexes.Add(index);
			}
			else if (metric == bestMetric)
			{
				bestIndexes.Add(index);
			}
		}

		int bestIndex = bestIndexes[NormalizeOrdinal(tieBreakOrdinal, bestIndexes.Count)];
		return candidates[bestIndex].PlayerIndex;
	}

	internal static int PickDiscardDisplayIndex(
		Difficulty difficulty,
		IReadOnlyList<HandCardDecisionCandidate> normalHandCards,
		int totalDisplayCount,
		int tieBreakOrdinal)
	{
		if (totalDisplayCount <= 0)
		{
			return -1;
		}

		if (difficulty == Difficulty.Low)
		{
			return NormalizeOrdinal(tieBreakOrdinal, totalDisplayCount);
		}

		if (difficulty == Difficulty.High)
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
		Difficulty difficulty,
		IReadOnlyList<HandCardDecisionCandidate> legalHandCards,
		int tieBreakOrdinal)
	{
		if (legalHandCards.Count == 0)
		{
			return -1;
		}

		if (difficulty == Difficulty.Low)
		{
			return legalHandCards[NormalizeOrdinal(tieBreakOrdinal, legalHandCards.Count)].Index;
		}

		if (difficulty == Difficulty.High)
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
		IReadOnlyList<CaptureScoringRules.TableCard> tableCards,
		IReadOnlyList<PotentialCaptureRules.Card> handCards,
		CaptureScoringRules.Context context,
		Profile profile,
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
			if (captureCount <= 0 || (profile.AvoidPairOnlyCaptures && PotentialCaptureRules.IsPairOnlyCapture(tableCards, card)))
			{
				continue;
			}

			int primaryScore = profile.UsePointScoring
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
		Profile profile,
		int randomCaptureOrdinal,
		out int handIndex)
	{
		handIndex = -1;
		var candidateIndexes = new List<int>();
		for (int index = 0; index < handCards.Count; index++)
		{
			PotentialCaptureRules.Card card = handCards[index];
			if (PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card) <= 0)
			{
				continue;
			}

			if (profile.AvoidPairOnlyCaptures && PotentialCaptureRules.IsPairOnlyCapture(tableCards, card))
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
		IReadOnlyList<HandCardDecisionCandidate> candidates,
		Func<HandCardDecisionCandidate, bool> predicate,
		int tieBreakOrdinal)
	{
		int bestScore = int.MaxValue;
		var bestIndexes = new List<int>();
		foreach (HandCardDecisionCandidate candidate in candidates)
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
