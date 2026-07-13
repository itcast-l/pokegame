using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

[Flags]
internal enum AdventureCaptureEntrySource
{
	None = 0,
	PrimaryRange = 1 << 0,
	MagnetExtension = 1 << 1,
	GapRootingExtension = 1 << 2,
	OtherExtension = 1 << 3,
}

internal enum AdventureCaptureMatchSource
{
	None,
	Rank,
	AlternateRank,
	SuitFallback,
	AttachedSuitCollect,
	CollectAll,
}

internal readonly record struct AdventureCapturedCardEntry(
	long TableCardInstanceId,
	long BaseCardInstanceId,
	TableCard TableCard,
	int PreCaptureTableIndex,
	bool WasFaceUp,
	bool WasBuried,
	bool IsPlayedCard,
	AdventureCaptureEntrySource Source);

internal readonly record struct AdventureCaptureScoreItem(
	long TableCardInstanceId,
	long BaseCardInstanceId,
	int RawPoints,
	int ResolvedPoints,
	bool IsExplicitDuplicate,
	string SourceUpgradeId);

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

internal sealed class CaptureResolutionContext
{
	private readonly ReadOnlyCollection<AdventureCapturedCardEntry> _capturedCards;
	private readonly ReadOnlyCollection<AdventureCaptureScoreItem> _normalScoreItems;
	private readonly ReadOnlyCollection<AdventureCaptureScoreItem> _explicitDuplicateScoreItems;
	private readonly ReadOnlyCollection<string> _triggeredAttachedItemCardIds;
	private readonly HashSet<long> _capturedTableCardIds;

	internal CaptureResolutionContext(
		BaseCardActionContext action,
		IEnumerable<AdventureCapturedCardEntry>? capturedCards,
		IEnumerable<AdventureCaptureScoreItem>? scoreItems,
		AdventureCaptureMatchSource matchSource,
		IEnumerable<string>? triggeredAttachedItemCardIds = null)
	{
		MatchSource = matchSource;
		_capturedTableCardIds = new HashSet<long>();
		var mergedCapturedCards = new Dictionary<long, AdventureCapturedCardEntry>();
		foreach (AdventureCapturedCardEntry entry in capturedCards ?? Array.Empty<AdventureCapturedCardEntry>())
		{
			if (entry.TableCardInstanceId <= 0 || entry.TableCard == null)
			{
				continue;
			}

			if (mergedCapturedCards.TryGetValue(entry.TableCardInstanceId, out AdventureCapturedCardEntry existing))
			{
				mergedCapturedCards[entry.TableCardInstanceId] = existing with
				{
					Source = existing.Source | entry.Source,
					IsPlayedCard = existing.IsPlayedCard || entry.IsPlayedCard,
				};
			}
			else
			{
				mergedCapturedCards.Add(entry.TableCardInstanceId, entry);
			}
		}

		var orderedCapturedCards = new List<AdventureCapturedCardEntry>(mergedCapturedCards.Values);
		orderedCapturedCards.Sort((left, right) =>
		{
			int indexComparison = left.PreCaptureTableIndex.CompareTo(right.PreCaptureTableIndex);
			return indexComparison != 0
				? indexComparison
				: left.TableCardInstanceId.CompareTo(right.TableCardInstanceId);
		});
		foreach (AdventureCapturedCardEntry entry in orderedCapturedCards)
		{
			_capturedTableCardIds.Add(entry.TableCardInstanceId);
		}
		_capturedCards = new ReadOnlyCollection<AdventureCapturedCardEntry>(orderedCapturedCards);

		var normalItemsByCardId = new Dictionary<long, AdventureCaptureScoreItem>();
		var duplicateItems = new List<AdventureCaptureScoreItem>();
		foreach (AdventureCaptureScoreItem item in scoreItems ?? Array.Empty<AdventureCaptureScoreItem>())
		{
			if (item.TableCardInstanceId <= 0 || !_capturedTableCardIds.Contains(item.TableCardInstanceId))
			{
				continue;
			}

			AdventureCaptureScoreItem normalized = item with
			{
				RawPoints = Math.Max(0, item.RawPoints),
				ResolvedPoints = Math.Max(0, item.ResolvedPoints),
				SourceUpgradeId = item.SourceUpgradeId?.Trim() ?? string.Empty,
			};
			if (normalized.IsExplicitDuplicate)
			{
				if (normalized.SourceUpgradeId.Length == 0)
				{
					throw new ArgumentException(
						"Explicit duplicate score items require a source upgrade ID.",
						nameof(scoreItems));
				}

				duplicateItems.Add(normalized);
				continue;
			}

			if (!normalItemsByCardId.TryAdd(normalized.TableCardInstanceId, normalized))
			{
				AdventureCaptureScoreItem existing = normalItemsByCardId[normalized.TableCardInstanceId];
				normalItemsByCardId[normalized.TableCardInstanceId] = existing with
				{
					RawPoints = Math.Max(existing.RawPoints, normalized.RawPoints),
					ResolvedPoints = Math.Max(existing.ResolvedPoints, normalized.ResolvedPoints),
				};
			}
		}

		var normalItems = new List<AdventureCaptureScoreItem>(normalItemsByCardId.Values);
		normalItems.Sort(CompareScoreItems);
		duplicateItems.Sort(CompareScoreItems);
		_normalScoreItems = new ReadOnlyCollection<AdventureCaptureScoreItem>(normalItems);
		_explicitDuplicateScoreItems = new ReadOnlyCollection<AdventureCaptureScoreItem>(duplicateItems);

		var normalizedAttachmentIds = new List<string>();
		var seenAttachmentIds = new HashSet<string>(StringComparer.Ordinal);
		void AddTriggeredAttachmentIds(IEnumerable<string>? attachmentIds)
		{
			foreach (string? attachmentId in attachmentIds ?? Array.Empty<string>())
			{
				string normalizedId = attachmentId?.Trim() ?? string.Empty;
				if (normalizedId.Length > 0 && seenAttachmentIds.Add(normalizedId))
				{
					normalizedAttachmentIds.Add(normalizedId);
				}
			}
		}

		AddTriggeredAttachmentIds(action.TriggeredAttachedItemCardIds);
		AddTriggeredAttachmentIds(triggeredAttachedItemCardIds);
		normalizedAttachmentIds.Sort(StringComparer.Ordinal);
		_triggeredAttachedItemCardIds = new ReadOnlyCollection<string>(normalizedAttachmentIds);
		Action = action with
		{
			TriggeredAttachedItemCardIds = _triggeredAttachedItemCardIds,
		};
	}

	internal BaseCardActionContext Action { get; }
	internal AdventureCaptureMatchSource MatchSource { get; }
	internal IReadOnlyList<AdventureCapturedCardEntry> CapturedCards => _capturedCards;
	internal IReadOnlyList<AdventureCaptureScoreItem> NormalScoreItems => _normalScoreItems;
	internal IReadOnlyList<AdventureCaptureScoreItem> ExplicitDuplicateScoreItems => _explicitDuplicateScoreItems;
	internal IReadOnlyList<string> TriggeredAttachedItemCardIds => _triggeredAttachedItemCardIds;
	internal bool IncludesQuestionReward { get; init; }

	internal int CompleteBaseCardCount
	{
		get
		{
			var uniqueBaseCardIds = new HashSet<long>();
			foreach (AdventureCapturedCardEntry entry in _capturedCards)
			{
				if (entry.BaseCardInstanceId > 0)
				{
					uniqueBaseCardIds.Add(entry.BaseCardInstanceId);
				}
			}

			return uniqueBaseCardIds.Count;
		}
	}

	internal bool HasAnyTriggeredAttachedItem => _triggeredAttachedItemCardIds.Count > 0;

	internal bool HasTriggeredAttachedItem(string itemCardId)
	{
		if (string.IsNullOrWhiteSpace(itemCardId))
		{
			return false;
		}

		foreach (string triggeredId in _triggeredAttachedItemCardIds)
		{
			if (string.Equals(triggeredId, itemCardId.Trim(), StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	internal bool CapturedOnlyTwoVisibleEndpoints()
	{
		if (_capturedCards.Count != 2)
		{
			return false;
		}

		AdventureCapturedCardEntry first = _capturedCards[0];
		AdventureCapturedCardEntry last = _capturedCards[1];
		return first.WasFaceUp
			&& last.WasFaceUp
			&& !first.WasBuried
			&& !last.WasBuried
			&& first.PreCaptureTableIndex < last.PreCaptureTableIndex;
	}

	private static int CompareScoreItems(
		AdventureCaptureScoreItem left,
		AdventureCaptureScoreItem right)
	{
		int cardComparison = left.TableCardInstanceId.CompareTo(right.TableCardInstanceId);
		return cardComparison != 0
			? cardComparison
			: string.CompareOrdinal(left.SourceUpgradeId, right.SourceUpgradeId);
	}
}
