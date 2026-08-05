using System;
using System.Collections.Generic;
using System.Collections.Immutable;

public readonly record struct AdventureCaptureScoreItem(
	long TableCardInstanceId,
	long BaseCardInstanceId,
	int RawPoints,
	int ResolvedPoints,
	bool IsExplicitDuplicate,
	string SourceUpgradeId);

public sealed record AdventureCaptureResolutionFacts(
	ImmutableArray<AdventureCaptureScoreItem> NormalScoreItems,
	ImmutableArray<AdventureCaptureScoreItem> ExplicitDuplicateScoreItems,
	ImmutableArray<string> TriggeredAttachedItemCardIds,
	bool CapturedOnlyTwoVisibleEndpoints)
{
	public ImmutableArray<AdventureCaptureScoreItem> NormalizedNormalScoreItems =>
		NormalScoreItems.IsDefault ? ImmutableArray<AdventureCaptureScoreItem>.Empty : NormalScoreItems;

	public ImmutableArray<AdventureCaptureScoreItem> NormalizedExplicitDuplicateScoreItems =>
		ExplicitDuplicateScoreItems.IsDefault
			? ImmutableArray<AdventureCaptureScoreItem>.Empty
			: ExplicitDuplicateScoreItems;

	public ImmutableArray<string> NormalizedTriggeredAttachedItemCardIds =>
		TriggeredAttachedItemCardIds.IsDefault ? ImmutableArray<string>.Empty : TriggeredAttachedItemCardIds;
}

internal static class AdventureCaptureScoreItemRules
{
	internal static int ResolveTopPrizeRepeatIndex(
		IReadOnlyList<AdventureCaptureScoreItem>? scoreItems,
		IReadOnlyList<int>? preCaptureTableIndexes,
		int minimumScoreItemCount = 3)
	{
		if (scoreItems == null
			|| preCaptureTableIndexes == null
			|| scoreItems.Count != preCaptureTableIndexes.Count)
		{
			return -1;
		}

		int selectedIndex = -1;
		int selectedTableIndex = int.MaxValue;
		int normalScoreItemCount = 0;
		for (int index = 0; index < scoreItems.Count; index++)
		{
			AdventureCaptureScoreItem candidate = scoreItems[index];
			if (candidate.IsExplicitDuplicate)
			{
				continue;
			}

			normalScoreItemCount++;
			int candidateTableIndex = preCaptureTableIndexes[index] >= 0
				? preCaptureTableIndexes[index]
				: int.MaxValue;
			if (selectedIndex < 0
				|| candidate.ResolvedPoints > scoreItems[selectedIndex].ResolvedPoints
				|| (candidate.ResolvedPoints == scoreItems[selectedIndex].ResolvedPoints
					&& candidateTableIndex < selectedTableIndex))
			{
				selectedIndex = index;
				selectedTableIndex = candidateTableIndex;
			}
		}

		return normalScoreItemCount >= Math.Max(0, minimumScoreItemCount) ? selectedIndex : -1;
	}
}
