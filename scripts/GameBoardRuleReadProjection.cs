using System.Collections.Generic;
using System.Collections.Immutable;
using Deckland.Rules;

internal sealed class GameBoardRuleReadProjection
{
	private const int CompatibilityPlayerCapacity = 4;

	private MatchReadModel? _source;

	internal GameBoardRuleReadProjection()
	{
		PlayerDecks = CreateEmptyCardZones();
		PlayerHands = CreateEmptyCardZones();
		PlayerItemHands = CreateEmptyItemZones();
		PlayerScores = ImmutableArray.CreateRange(new int[CompatibilityPlayerCapacity]);
	}

	internal IReadOnlyList<IReadOnlyList<PlayingCard>> PlayerDecks { get; private set; }
	internal IReadOnlyList<IReadOnlyList<PlayingCard>> PlayerHands { get; private set; }
	internal IReadOnlyList<IReadOnlyList<ItemCard>> PlayerItemHands { get; private set; }
	internal ImmutableArray<TableCard> TableCards { get; private set; } = ImmutableArray<TableCard>.Empty;
	internal ImmutableArray<int> PlayerScores { get; private set; }

	internal void Synchronize(MatchReadModel readModel)
	{
		if (ReferenceEquals(_source, readModel))
		{
			return;
		}

		var decks = ImmutableArray.CreateBuilder<IReadOnlyList<PlayingCard>>(CompatibilityPlayerCapacity);
		var hands = ImmutableArray.CreateBuilder<IReadOnlyList<PlayingCard>>(CompatibilityPlayerCapacity);
		var itemHands = ImmutableArray.CreateBuilder<IReadOnlyList<ItemCard>>(CompatibilityPlayerCapacity);
		var scores = ImmutableArray.CreateBuilder<int>(CompatibilityPlayerCapacity);
		for (int seat = 0; seat < CompatibilityPlayerCapacity; seat++)
		{
			if (seat < readModel.Players.Length)
			{
				PlayerStateSnapshot player = readModel.Players[seat];
				decks.Add(ProjectCards(player.Deck));
				hands.Add(ProjectCards(player.Hand));
				itemHands.Add(ProjectItems(player.ItemHand));
				scores.Add(player.Score);
			}
			else
			{
				decks.Add(ImmutableArray<PlayingCard>.Empty);
				hands.Add(ImmutableArray<PlayingCard>.Empty);
				itemHands.Add(ImmutableArray<ItemCard>.Empty);
				scores.Add(0);
			}
		}

		bool canReuseTableCardReferences = _source != null
			&& _source.LifecycleGeneration == readModel.LifecycleGeneration;
		var existingTableCards = new Dictionary<long, TableCard>(TableCards.Length);
		var previousTableCardStates = new Dictionary<long, TableCardState>(
			_source?.TableCards.Length ?? 0);
		if (canReuseTableCardReferences)
		{
			foreach (TableCard tableCard in TableCards)
			{
				existingTableCards.TryAdd(tableCard.InstanceId, tableCard);
			}
			foreach (TableCardState tableCard in _source!.TableCards)
			{
				previousTableCardStates.TryAdd(
					tableCard.RuntimeInstanceId,
					tableCard);
			}
		}

		var tableCards = ImmutableArray.CreateBuilder<TableCard>(readModel.TableCards.Length);
		foreach (TableCardState source in readModel.TableCards)
		{
			bool reused = existingTableCards.TryGetValue(
				source.RuntimeInstanceId,
				out TableCard? projected);
			projected ??= TableCard.Restore(
					source.RuntimeInstanceId,
					null,
					source.IsFaceUp,
					null,
					source.ItemOwnerSeat,
					source.CardOwnerSeat,
					source.RemoveOnCapture);
			if (!reused
				|| !previousTableCardStates.TryGetValue(
					source.RuntimeInstanceId,
					out TableCardState? previous)
				|| previous == null
				|| !AreEquivalent(previous, source))
			{
				projected.SynchronizeProjection(
					source.Card.HasValue ? ProjectCard(source.Card.Value) : null,
					source.IsFaceUp,
					source.IsHiddenDuringCapture,
					source.IsBuried,
					source.IsScoreSuppressed,
					source.ItemOverlay.HasValue
						? new ItemCard(source.ItemOverlay.Value.Id)
						: null,
					source.ItemOwnerSeat,
					source.CardOwnerSeat,
					source.RemoveOnCapture);
			}
			tableCards.Add(projected);
		}

		PlayerDecks = decks.MoveToImmutable();
		PlayerHands = hands.MoveToImmutable();
		PlayerItemHands = itemHands.MoveToImmutable();
		TableCards = tableCards.MoveToImmutable();
		PlayerScores = scores.MoveToImmutable();
		_source = readModel;
	}

	internal bool TryReplaceTableCardPresentationReference(
		int tableCardIndex,
		TableCard replacement)
	{
		if (tableCardIndex < 0 || tableCardIndex >= TableCards.Length)
		{
			return false;
		}

		TableCards = TableCards.SetItem(tableCardIndex, replacement);
		return true;
	}

	private static IReadOnlyList<IReadOnlyList<PlayingCard>> CreateEmptyCardZones()
	{
		var zones = ImmutableArray.CreateBuilder<IReadOnlyList<PlayingCard>>(CompatibilityPlayerCapacity);
		for (int seat = 0; seat < CompatibilityPlayerCapacity; seat++)
		{
			zones.Add(ImmutableArray<PlayingCard>.Empty);
		}
		return zones.MoveToImmutable();
	}

	private static IReadOnlyList<IReadOnlyList<ItemCard>> CreateEmptyItemZones()
	{
		var zones = ImmutableArray.CreateBuilder<IReadOnlyList<ItemCard>>(CompatibilityPlayerCapacity);
		for (int seat = 0; seat < CompatibilityPlayerCapacity; seat++)
		{
			zones.Add(ImmutableArray<ItemCard>.Empty);
		}
		return zones.MoveToImmutable();
	}

	private static ImmutableArray<PlayingCard> ProjectCards(ImmutableArray<LogicalCardState> cards)
	{
		var result = ImmutableArray.CreateBuilder<PlayingCard>(cards.Length);
		foreach (LogicalCardState card in cards)
		{
			result.Add(ProjectCard(card));
		}
		return result.MoveToImmutable();
	}

	private static ImmutableArray<ItemCard> ProjectItems(ImmutableArray<LogicalItemState> items)
	{
		var result = ImmutableArray.CreateBuilder<ItemCard>(items.Length);
		foreach (LogicalItemState item in items)
		{
			result.Add(new ItemCard(item.Id));
		}
		return result.MoveToImmutable();
	}

	private static PlayingCard ProjectCard(LogicalCardState card)
	{
		return PlayingCard.Restore(
			card.RuntimeInstanceId,
			card.Code,
			card.Suit,
			card.Rank,
			card.NormalizedAttachedEffectIds);
	}

	private static bool AreEquivalent(TableCardState left, TableCardState right)
	{
		return left.RuntimeInstanceId == right.RuntimeInstanceId
			&& AreEquivalent(left.Card, right.Card)
			&& left.IsFaceUp == right.IsFaceUp
			&& left.IsHiddenDuringCapture == right.IsHiddenDuringCapture
			&& left.IsBuried == right.IsBuried
			&& left.IsScoreSuppressed == right.IsScoreSuppressed
			&& left.ItemOverlay == right.ItemOverlay
			&& left.ItemOwnerSeat == right.ItemOwnerSeat
			&& left.CardOwnerSeat == right.CardOwnerSeat
			&& left.RemoveOnCapture == right.RemoveOnCapture;
	}

	private static bool AreEquivalent(
		LogicalCardState? left,
		LogicalCardState? right)
	{
		if (!left.HasValue || !right.HasValue)
		{
			return left.HasValue == right.HasValue;
		}

		LogicalCardState leftCard = left.Value;
		LogicalCardState rightCard = right.Value;
		return leftCard.RuntimeInstanceId == rightCard.RuntimeInstanceId
			&& string.Equals(leftCard.Code, rightCard.Code, StringComparison.Ordinal)
			&& string.Equals(leftCard.Suit, rightCard.Suit, StringComparison.Ordinal)
			&& string.Equals(leftCard.Rank, rightCard.Rank, StringComparison.Ordinal)
			&& leftCard.NormalizedAttachedEffectIds.SequenceEqual(
				rightCard.NormalizedAttachedEffectIds);
	}
}
