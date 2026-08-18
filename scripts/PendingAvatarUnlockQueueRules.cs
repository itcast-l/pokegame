using System;
using System.Collections.Generic;

internal static class PendingAvatarUnlockQueueRules
{
	internal static bool TryPeek(
		IReadOnlyList<string> queue,
		Func<string, string> normalize,
		Func<string, bool> isEligible,
		out string candidateAvatarId)
	{
		return TryFindCandidate(queue, normalize, isEligible, out _, out candidateAvatarId);
	}

	internal static bool TryConsumeExpected(
		IReadOnlyList<string> queue,
		string expectedAvatarId,
		Func<string, string> normalize,
		Func<string, bool> isEligible,
		out List<string> remainingQueue)
	{
		remainingQueue = new List<string>(queue);
		if (!TryFindCandidate(queue, normalize, isEligible, out int candidateIndex, out string candidateAvatarId)
			|| !string.Equals(candidateAvatarId, expectedAvatarId, StringComparison.Ordinal))
		{
			return false;
		}

		remainingQueue.RemoveRange(0, candidateIndex + 1);
		return true;
	}

	private static bool TryFindCandidate(
		IReadOnlyList<string> queue,
		Func<string, string> normalize,
		Func<string, bool> isEligible,
		out int candidateIndex,
		out string candidateAvatarId)
	{
		ArgumentNullException.ThrowIfNull(queue);
		ArgumentNullException.ThrowIfNull(normalize);
		ArgumentNullException.ThrowIfNull(isEligible);

		for (int index = 0; index < queue.Count; index++)
		{
			string normalizedAvatarId = normalize(queue[index]);
			if (!isEligible(normalizedAvatarId))
			{
				continue;
			}

			candidateIndex = index;
			candidateAvatarId = normalizedAvatarId;
			return true;
		}

		candidateIndex = -1;
		candidateAvatarId = string.Empty;
		return false;
	}
}
