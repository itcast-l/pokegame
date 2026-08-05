using System.Threading;

internal readonly struct PlayingCard
{
	internal PlayingCard(string suit, string rank, string attachedItemCardIdsCsv = "")
		: this($"{suit}:{rank}", suit, rank, attachedItemCardIdsCsv)
	{
	}

	internal PlayingCard(
		string code,
		string suit,
		string rank,
		string attachedItemCardIdsCsv = "")
	{
		InstanceId = Interlocked.Increment(ref _nextInstanceId);
		Code = code ?? string.Empty;
		Suit = suit;
		Rank = rank;
		AttachedItemCardIdsCsv = attachedItemCardIdsCsv;
		_attachedItemCardIds = ParseAttachedItemCardIds(attachedItemCardIdsCsv);
	}

	private PlayingCard(
		long instanceId,
		string code,
		string suit,
		string rank,
		string[] attachedItemCardIds)
	{
		string[] normalizedAttachedItemCardIds = NormalizeAttachedItemCardIds(attachedItemCardIds);
		InstanceId = instanceId;
		Code = code ?? string.Empty;
		Suit = suit;
		Rank = rank;
		_attachedItemCardIds = normalizedAttachedItemCardIds;
		AttachedItemCardIdsCsv = normalizedAttachedItemCardIds.Length == 0
			? string.Empty
			: string.Join(",", normalizedAttachedItemCardIds);
	}

	private static long _nextInstanceId;
	private readonly string[]? _attachedItemCardIds;

	internal long InstanceId { get; }
	internal string Code { get; }
	internal string Suit { get; }
	internal string Rank { get; }
	internal string AttachedItemCardIdsCsv { get; }
	internal bool HasItemEffect => AttachedItemCardIds.Count > 0;

	private IReadOnlyList<string> AttachedItemCardIds =>
		_attachedItemCardIds ?? Array.Empty<string>();

	internal PlayingCard WithAttachedItemEffect(string itemCardId)
	{
		if (string.IsNullOrWhiteSpace(itemCardId))
		{
			return this;
		}

		string normalizedItemCardId = itemCardId.Trim();
		if (AttachedItemCardIds.Count == 1
			&& string.Equals(
				AttachedItemCardIds[0],
				normalizedItemCardId,
				StringComparison.Ordinal))
		{
			return this;
		}

		return new PlayingCard(
			InstanceId,
			Code,
			Suit,
			Rank,
			new[] { normalizedItemCardId });
	}

	internal PlayingCard WithoutAttachedItemEffect(string itemCardId)
	{
		if (string.IsNullOrWhiteSpace(itemCardId)
			|| !HasAttachedItemEffect(itemCardId))
		{
			return this;
		}

		IReadOnlyList<string> existingIds = AttachedItemCardIds;
		if (existingIds.Count == 1)
		{
			return new PlayingCard(
				InstanceId,
				Code,
				Suit,
				Rank,
				Array.Empty<string>());
		}

		var remainingIds = new string[existingIds.Count - 1];
		int nextIndex = 0;
		foreach (string attachedItemCardId in existingIds)
		{
			if (attachedItemCardId != itemCardId)
			{
				remainingIds[nextIndex++] = attachedItemCardId;
			}
		}

		return nextIndex == 0
			? new PlayingCard(
				InstanceId,
				Code,
				Suit,
				Rank,
				Array.Empty<string>())
			: new PlayingCard(
				InstanceId,
				Code,
				Suit,
				Rank,
				remainingIds);
	}

	internal bool HasAttachedItemEffect(string itemCardId)
	{
		foreach (string attachedItemCardId in AttachedItemCardIds)
		{
			if (attachedItemCardId == itemCardId)
			{
				return true;
			}
		}

		return false;
	}

	internal IReadOnlyList<string> GetAttachedItemCardIds()
	{
		return AttachedItemCardIds;
	}

	internal PlayingCard WithoutItemEffects()
	{
		return HasItemEffect
			? new PlayingCard(
				InstanceId,
				Code,
				Suit,
				Rank,
				Array.Empty<string>())
			: this;
	}

	internal PlayingCard CloneWithNewRuntimeInstanceId()
	{
		return new PlayingCard(Code, Suit, Rank, AttachedItemCardIdsCsv);
	}

	internal static PlayingCard Restore(
		long instanceId,
		string code,
		string suit,
		string rank,
		IReadOnlyList<string> attachedItemCardIds)
	{
		AdvanceNextInstanceId(instanceId);
		return new PlayingCard(
			instanceId,
			code,
			suit,
			rank,
			attachedItemCardIds.ToArray());
	}

	internal static PlayingCard Restore(
		long instanceId,
		string suit,
		string rank,
		IReadOnlyList<string> attachedItemCardIds)
	{
		return Restore(
			instanceId,
			$"{suit}:{rank}",
			suit,
			rank,
			attachedItemCardIds);
	}

	private static void AdvanceNextInstanceId(long instanceId)
	{
		long observed = Volatile.Read(ref _nextInstanceId);
		while (observed < instanceId)
		{
			long previous = Interlocked.CompareExchange(
				ref _nextInstanceId,
				instanceId,
				observed);
			if (previous == observed)
			{
				return;
			}

			observed = previous;
		}
	}

	private static string[] ParseAttachedItemCardIds(string attachedItemCardIdsCsv)
	{
		return string.IsNullOrWhiteSpace(attachedItemCardIdsCsv)
			? Array.Empty<string>()
			: NormalizeAttachedItemCardIds(attachedItemCardIdsCsv.Split(
				',',
				StringSplitOptions.RemoveEmptyEntries
					| StringSplitOptions.TrimEntries));
	}

	private static string[] NormalizeAttachedItemCardIds(
		IReadOnlyList<string> attachedItemCardIds)
	{
		for (int index = attachedItemCardIds.Count - 1; index >= 0; index--)
		{
			string itemCardId = attachedItemCardIds[index];
			if (!string.IsNullOrWhiteSpace(itemCardId))
			{
				return new[] { itemCardId.Trim() };
			}
		}

		return Array.Empty<string>();
	}
}

internal readonly record struct ItemCard(string Id);

internal enum PendingItemSelectionMode
{
	None,
	AttachmentRewardHandCard,
	HandLimitDiscard,
}

internal sealed class TableCard
{
	internal TableCard(
		PlayingCard? card,
		bool isFaceUp,
		ItemCard? itemOverlay = null,
		int itemOwnerPlayerIndex = -1,
		int cardOwnerPlayerIndex = -1,
		bool removeCardOnCapture = false)
	{
		InstanceId = Interlocked.Increment(ref _nextInstanceId);
		Card = card;
		IsFaceUp = isFaceUp;
		ItemOverlay = itemOverlay;
		ItemOwnerPlayerIndex = itemOwnerPlayerIndex;
		CardOwnerPlayerIndex = cardOwnerPlayerIndex;
		RemoveCardOnCapture = removeCardOnCapture;
	}

	private TableCard(
		long instanceId,
		PlayingCard? card,
		bool isFaceUp,
		ItemCard? itemOverlay,
		int itemOwnerPlayerIndex,
		int cardOwnerPlayerIndex,
		bool removeCardOnCapture)
	{
		InstanceId = instanceId;
		Card = card;
		IsFaceUp = isFaceUp;
		ItemOverlay = itemOverlay;
		ItemOwnerPlayerIndex = itemOwnerPlayerIndex;
		CardOwnerPlayerIndex = cardOwnerPlayerIndex;
		RemoveCardOnCapture = removeCardOnCapture;
	}

	private static long _nextInstanceId;

	internal long InstanceId { get; }
	internal PlayingCard? Card { get; private set; }
	internal bool IsFaceUp { get; set; }
	internal bool IsHiddenDuringCapture { get; set; }
	internal bool IsBuriedInSand { get; set; }
	internal bool IsSlimeCoated { get; set; }
	internal ItemCard? ItemOverlay { get; private set; }
	internal int ItemOwnerPlayerIndex { get; private set; }
	internal int CardOwnerPlayerIndex { get; private set; }
	internal bool RemoveCardOnCapture { get; private set; }

	internal void SynchronizeProjection(
		PlayingCard? card,
		bool isFaceUp,
		bool isHiddenDuringCapture,
		bool isBuriedInSand,
		bool isSlimeCoated,
		ItemCard? itemOverlay,
		int itemOwnerPlayerIndex,
		int cardOwnerPlayerIndex,
		bool removeCardOnCapture)
	{
		Card = card;
		IsFaceUp = isFaceUp;
		IsHiddenDuringCapture = isHiddenDuringCapture;
		IsBuriedInSand = isBuriedInSand;
		IsSlimeCoated = isSlimeCoated;
		ItemOverlay = itemOverlay;
		ItemOwnerPlayerIndex = itemOwnerPlayerIndex;
		CardOwnerPlayerIndex = cardOwnerPlayerIndex;
		RemoveCardOnCapture = removeCardOnCapture;
	}

	internal void ClearAttachedItemEffect(string itemCardId)
	{
		if (Card.HasValue)
		{
			Card = Card.Value.WithoutAttachedItemEffect(itemCardId);
		}
	}

	internal static TableCard Restore(
		long instanceId,
		PlayingCard? card,
		bool isFaceUp,
		ItemCard? itemOverlay,
		int itemOwnerPlayerIndex,
		int cardOwnerPlayerIndex,
		bool removeCardOnCapture)
	{
		AdvanceNextInstanceId(instanceId);
		return new TableCard(
			instanceId,
			card,
			isFaceUp,
			itemOverlay,
			itemOwnerPlayerIndex,
			cardOwnerPlayerIndex,
			removeCardOnCapture);
	}

	private static void AdvanceNextInstanceId(long instanceId)
	{
		long observed = Volatile.Read(ref _nextInstanceId);
		while (observed < instanceId)
		{
			long previous = Interlocked.CompareExchange(
				ref _nextInstanceId,
				instanceId,
				observed);
			if (previous == observed)
			{
				return;
			}

			observed = previous;
		}
	}
}
