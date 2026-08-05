using System;
using System.Collections.Generic;

public readonly record struct NetworkAiBlockedItemState(
	int OwnerSeatIndex,
	string ItemCardId)
{
	public static NetworkAiBlockedItemState None => new(-1, string.Empty);

	public bool IsActive =>
		OwnerSeatIndex >= 0
		&& !string.IsNullOrWhiteSpace(ItemCardId);
}

internal static class NetworkAiFallbackRules
{
	public static bool TryPickCaptureHandIndexForNetworkAiProxy(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards,
		CaptureScoringContext context,
		int decisionRollPercent,
		int randomCaptureOrdinal,
		out int handIndex)
	{
		return EnemyAiRules.TryPickLegacyCaptureCardIndex(
			tableCards,
			handCards,
			context,
			bestCaptureChancePercent: 25,
			anyCaptureChancePercent: 15,
			usePointScoring: false,
			avoidPairOnlyCaptures: true,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent,
			randomCaptureOrdinal,
			out handIndex);
	}

	public static bool HasPlayableAiTurnAction(int handCardCount, int playableItemCount)
	{
		return handCardCount > 0 || playableItemCount > 0;
	}

	public static bool ShouldResumeAiProxyTurnAfterAuthorityAction(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		bool isGameOver,
		bool isProcessingCardPlay)
	{
		return isNetworkMultiplayer
			&& isNetworkHost
			&& !isGameOver
			&& !isProcessingCardPlay;
	}

	public static bool ShouldStartAiProxyForPendingSelection(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		bool hasAiOwnedPendingSelection)
	{
		return isNetworkMultiplayer
			&& isNetworkHost
			&& hasAiOwnedPendingSelection;
	}

	public static bool ShouldRecoverFromUnhandledAiTurnFailure(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		bool isGameOver)
	{
		return isNetworkMultiplayer
			&& isNetworkHost
			&& !isGameOver;
	}

	public static bool ShouldSkipBlockedItem(
		NetworkAiBlockedItemState blockedItem,
		int ownerSeatIndex,
		string itemCardId)
	{
		if (!blockedItem.IsActive
			|| blockedItem.OwnerSeatIndex != ownerSeatIndex)
		{
			return false;
		}

		return string.Equals(blockedItem.ItemCardId, itemCardId, StringComparison.Ordinal);
	}

	public static bool ShouldClearBlockedItemForTurnChange(int previousTurnPlayerIndex, int nextTurnPlayerIndex)
	{
		return previousTurnPlayerIndex != nextTurnPlayerIndex;
	}

	public static int PickRandomLegalCandidate(
		int totalCandidateCount,
		Func<int, bool> isLegalCandidate,
		int randomOrdinal)
	{
		if (totalCandidateCount <= 0)
		{
			return -1;
		}

		int legalCandidateCount = 0;
		for (int candidateIndex = 0; candidateIndex < totalCandidateCount; candidateIndex++)
		{
			if (isLegalCandidate(candidateIndex))
			{
				legalCandidateCount++;
			}
		}

		if (legalCandidateCount == 0)
		{
			return -1;
		}

		int targetLegalOrdinal = (randomOrdinal & int.MaxValue) % legalCandidateCount;
		int seenLegalCandidates = 0;
		for (int candidateIndex = 0; candidateIndex < totalCandidateCount; candidateIndex++)
		{
			if (!isLegalCandidate(candidateIndex))
			{
				continue;
			}

			if (seenLegalCandidates == targetLegalOrdinal)
			{
				return candidateIndex;
			}

			seenLegalCandidates++;
		}

		return -1;
	}
}
