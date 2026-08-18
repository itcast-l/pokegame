using System;
using System.Collections.Generic;

internal readonly record struct PotentialCaptureEvaluation(
	CardPlayPlan PlayPlan,
	int NormalCaptureCount,
	bool HasCapturedMiddleTableCards)
{
	public bool CanCapture => NormalCaptureCount > 0;
	public bool IsPairOnlyCapture => CanCapture && !HasCapturedMiddleTableCards;
}

internal static class PotentialCaptureRules
{
	internal static bool TryPickBestCaptureCardIndex(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards,
		CaptureScoringContext context,
		int royalCaptureThreshold,
		out int handIndex)
	{
		handIndex = -1;
		int bestScore = 0;
		int bestCaptureCount = 0;
		for (int index = 0; index < handCards.Count; index++)
		{
			PotentialCaptureCard card = handCards[index];
			PotentialCaptureEvaluation evaluation = EvaluatePotentialNormalCapture(tableCards, card);
			if (!evaluation.CanCapture)
			{
				continue;
			}

			int captureCount = evaluation.NormalCaptureCount;
			int score = GetPotentialNormalCapturePoints(tableCards, card, context, evaluation);
			if (captureCount >= royalCaptureThreshold)
			{
				score += 1000 + captureCount;
			}

			if (score > bestScore || (score == bestScore && captureCount > bestCaptureCount))
			{
				bestScore = score;
				bestCaptureCount = captureCount;
				handIndex = index;
			}
		}

		return handIndex >= 0;
	}

	internal static bool IsPairOnlyCapture(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		PotentialCaptureCard card)
	{
		return EvaluatePotentialNormalCapture(tableCards, card).IsPairOnlyCapture;
	}

	internal static int GetPotentialNormalCaptureCount(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		PotentialCaptureCard card)
	{
		return EvaluatePotentialNormalCapture(tableCards, card).NormalCaptureCount;
	}

	internal static int GetPotentialNormalCapturePoints(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		PotentialCaptureCard card,
		CaptureScoringContext context)
	{
		PotentialCaptureEvaluation evaluation = EvaluatePotentialNormalCapture(tableCards, card);
		return GetPotentialNormalCapturePoints(tableCards, card, context, evaluation);
	}

	internal static PotentialCaptureEvaluation EvaluatePotentialNormalCapture(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		PotentialCaptureCard card)
	{
		CardPlayPlan playPlan = CardPlayResolver.Resolve(
			ToCardPlayTableCards(tableCards),
			new CardPlayCard(
				card.Rank,
				card.CollectsTable,
				card.Suit,
				card.CollectBySuit,
				card.SuitMatchIsFallbackOnly),
			card.AlternateRanks,
			insertAtTableStart: false,
			basePlayCaptureThreshold: card.BasePlayCaptureThreshold);
		if (playPlan.CaptureStartIndex < 0)
		{
			return new PotentialCaptureEvaluation(playPlan, 0, false);
		}

		int captureCount = CountCollectibleTableCards(tableCards, playPlan.CaptureStartIndex) + 1;
		return new PotentialCaptureEvaluation(
			playPlan,
			captureCount,
			captureCount > 2);
	}

	internal static int GetPotentialNormalCapturePoints(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		PotentialCaptureCard card,
		CaptureScoringContext context,
		PotentialCaptureEvaluation evaluation)
	{
		if (!evaluation.HasCapturedMiddleTableCards)
		{
			return 0;
		}

		int points = 0;
		for (int tableCardIndex = evaluation.PlayPlan.CaptureStartIndex + 1;
			tableCardIndex < tableCards.Count;
			tableCardIndex++)
		{
			CaptureScoringTableCard tableCard = tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue)
			{
				points += CaptureScoringRules.ApplyCapturedCardScoreModifiers(
					tableCard.Card.Value.ScoreValue,
					context,
					card.HasDoubleEffect);
			}
		}

		return points;
	}

	internal static bool HasPotentialCapturedMiddleTableCards(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex)
	{
		return CountCollectibleTableCards(tableCards, captureStartIndex) + 1 > 2;
	}

	internal static int GetBestPotentialNormalCaptureCount(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards)
	{
		if (tableCards.Count == 0)
		{
			return 0;
		}

		int bestCaptureCount = 0;
		foreach (PotentialCaptureCard handCard in handCards)
		{
			bestCaptureCount = Math.Max(bestCaptureCount, GetPotentialNormalCaptureCount(tableCards, handCard));
		}

		return bestCaptureCount;
	}

	private static CardPlayTableCard[] ToCardPlayTableCards(
		IReadOnlyList<CaptureScoringTableCard> tableCards)
	{
		var result = new CardPlayTableCard[tableCards.Count];
		for (int tableCardIndex = 0; tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			CaptureScoringTableCard tableCard = tableCards[tableCardIndex];
			result[tableCardIndex] = new CardPlayTableCard(
				tableCard.Card?.Rank,
				IsFaceUp: true,
				tableCard.IsBuriedInSand,
				tableCard.Card?.Suit,
				tableCard.CountsTowardBasePlayCaptureThreshold);
		}

		return result;
	}

	private static int CountCollectibleTableCards(IReadOnlyList<CaptureScoringTableCard> tableCards, int startIndex = 0)
	{
		int count = 0;
		for (int tableCardIndex = Math.Max(0, startIndex); tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			if (!tableCards[tableCardIndex].IsBuriedInSand)
			{
				count++;
			}
		}

		return count;
	}

}
