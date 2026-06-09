using System;
using System.Collections.Generic;

internal readonly record struct NetworkAiBlockedItemState(
	int OwnerSeatIndex,
	string ItemCardId)
{
	internal static NetworkAiBlockedItemState None => new(-1, string.Empty);

	internal bool IsActive =>
		OwnerSeatIndex >= 0
		&& !string.IsNullOrWhiteSpace(ItemCardId);
}

internal static class NetworkAiFallbackRules
{
	internal static bool TryPickCaptureHandIndexForNetworkAiProxy(
		IReadOnlyList<CaptureScoringRules.TableCard> tableCards,
		IReadOnlyList<PotentialCaptureRules.Card> handCards,
		CaptureScoringRules.Context context,
		int decisionRollPercent,
		int randomCaptureOrdinal,
		out int handIndex)
	{
		return EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			context,
			EnemyAiRules.GetAdventureProfile(stageNumber: 1),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent,
			randomCaptureOrdinal,
			out handIndex);
	}

	internal static bool HasPlayableAiTurnAction(int handCardCount, int playableItemCount)
	{
		return handCardCount > 0 || playableItemCount > 0;
	}

	internal static bool ShouldResumeAiProxyTurnAfterAuthorityAction(
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

	internal static bool ShouldStartAiProxyForPendingSelection(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		bool hasAiOwnedPendingSelection)
	{
		return isNetworkMultiplayer
			&& isNetworkHost
			&& hasAiOwnedPendingSelection;
	}

	internal static bool ShouldRecoverFromUnhandledAiTurnFailure(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		bool isGameOver)
	{
		return isNetworkMultiplayer
			&& isNetworkHost
			&& !isGameOver;
	}

	internal static bool ShouldSkipBlockedItem(
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

	internal static bool ShouldClearBlockedItemForTurnChange(int previousTurnPlayerIndex, int nextTurnPlayerIndex)
	{
		return previousTurnPlayerIndex != nextTurnPlayerIndex;
	}

	internal static int PickRandomLegalCandidate(
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
