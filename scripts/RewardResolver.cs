using System;
using System.Collections.Generic;

internal readonly record struct RewardCandidate(string ItemCardId, int Weight);

internal readonly record struct RewardPlan(string ItemCardId)
{
	public bool HasReward => !string.IsNullOrEmpty(ItemCardId);
}

internal static class RewardResolver
{
	internal static RewardPlan PickWeightedReward(
		IEnumerable<RewardCandidate> candidates,
		Func<int, int> rollInclusive)
	{
		string selectedItemCardId = string.Empty;
		int totalWeight = 0;
		foreach (RewardCandidate candidate in candidates)
		{
			int weight = Math.Max(0, candidate.Weight);
			if (weight == 0)
			{
				continue;
			}

			totalWeight += weight;
			if (rollInclusive(totalWeight) <= weight)
			{
				selectedItemCardId = candidate.ItemCardId;
			}
		}

		return new RewardPlan(selectedItemCardId);
	}
}
