using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private List<TableCard> BuildShuffledTableCards()
	{
		var shuffledTableCards = new List<TableCard>(_tableCards);
		_tableShuffleIndexes.Clear();
		_tableShuffleMovableCards.Clear();
		for (int index = 0; index < _tableCards.Count; index++)
		{
			if (IsFixedDuringTableShuffle(_tableCards[index]))
			{
				continue;
			}

			_tableShuffleIndexes.Add(index);
			_tableShuffleMovableCards.Add(_tableCards[index]);
		}

		for (int index = _tableShuffleMovableCards.Count - 1; index > 0; index--)
		{
			int swapIndex = _rng.RandiRange(0, index);
			(_tableShuffleMovableCards[index], _tableShuffleMovableCards[swapIndex]) = (_tableShuffleMovableCards[swapIndex], _tableShuffleMovableCards[index]);
		}

		if (IsSameMovableTableCardOrder(_tableShuffleIndexes, _tableShuffleMovableCards) && _tableShuffleMovableCards.Count > 1)
		{
			(_tableShuffleMovableCards[^1], _tableShuffleMovableCards[^2]) = (_tableShuffleMovableCards[^2], _tableShuffleMovableCards[^1]);
		}

		for (int index = 0; index < _tableShuffleIndexes.Count; index++)
		{
			shuffledTableCards[_tableShuffleIndexes[index]] = _tableShuffleMovableCards[index];
		}

		_tableShuffleIndexes.Clear();
		_tableShuffleMovableCards.Clear();
		return shuffledTableCards;
	}

	private bool IsSameMovableTableCardOrder(IReadOnlyList<int> shuffleIndexes, IReadOnlyList<TableCard> movableCards)
	{
		if (shuffleIndexes.Count != movableCards.Count)
		{
			return false;
		}

		for (int index = 0; index < shuffleIndexes.Count; index++)
		{
			if (!ReferenceEquals(movableCards[index], _tableCards[shuffleIndexes[index]]))
			{
				return false;
			}
		}

		return true;
	}

	private void ApplyShuffledTableCards(IReadOnlyList<TableCard> shuffledTableCards)
	{
		_tableCards.Clear();
		_tableCards.AddRange(shuffledTableCards);
	}

	private bool IsFixedDuringTableShuffle(TableCard tableCard)
	{
		return IsFixedDuringTableShuffleByData(tableCard);
	}
}
