using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> PlayShuffleItemCardFromHandAsync(int playerIndex, int itemHandIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count
			|| playerIndex >= _playerHandAreas.Count)
		{
			return new PlayCardResult(false, false);
		}

		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		if (itemHandIndex < 0 || itemHandIndex >= itemHand.Count)
		{
			return new PlayCardResult(false, false);
		}

		ItemCard shuffleCard = itemHand[itemHandIndex];
		if (!IsItemEffectType(shuffleCard.Id, CardEffectTypes.ShuffleTable))
		{
			return new PlayCardResult(false, false);
		}

		Vector2 startGlobalPosition = GetHandItemCardGlobalPosition(playerIndex, itemHandIndex)
			?? _playerHandAreas[playerIndex].GlobalPosition + (_playerHandAreas[playerIndex].Size - CardVisualSize) / 2.0f;
		Control? sourceCardSlot = _playerHandAreas[playerIndex].GetNodeOrNull<Control>($"HandCard_{_playerHands[playerIndex].Count + itemHandIndex}");
		if (sourceCardSlot != null)
		{
			sourceCardSlot.Visible = false;
		}

		Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
		itemHand.RemoveAt(itemHandIndex);
		ShiftJellyfishHiddenHandSlotAfterRemovedCard(playerIndex, _playerHands[playerIndex].Count + itemHandIndex);
		RenderPlayerHands();

		await PlayItemCardToTransformAreaAnimationAsync(
			shuffleCard.Texture,
			startGlobalPosition,
			transformAreaGlobalPosition,
			GetCardRotationDegreesForPlayer(playerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		await PlaySpecialCardTriggerEffectAsync(shuffleCard.Texture, transformAreaGlobalPosition, 0);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		PlayCardResult result = await ResolveItemCardOnPlayAsync(
			shuffleCard,
			playerIndex,
			itemHandIndex,
			-1,
			-1,
			transformAreaGlobalPosition,
			turnSequenceGeneration);
		return result with { CompletesTurn = false };
	}

	private List<TableCard> BuildShuffledTableCards()
	{
		var shuffledTableCards = new List<TableCard>(_tableCards);
		var shuffleIndexes = new List<int>();
		var movableCards = new List<TableCard>();
		for (int index = 0; index < _tableCards.Count; index++)
		{
			if (IsFixedDuringTableShuffle(_tableCards[index]))
			{
				continue;
			}

			shuffleIndexes.Add(index);
			movableCards.Add(_tableCards[index]);
		}

		for (int index = movableCards.Count - 1; index > 0; index--)
		{
			int swapIndex = _rng.RandiRange(0, index);
			(movableCards[index], movableCards[swapIndex]) = (movableCards[swapIndex], movableCards[index]);
		}

		if (IsSameMovableTableCardOrder(shuffleIndexes, movableCards) && movableCards.Count > 1)
		{
			(movableCards[^1], movableCards[^2]) = (movableCards[^2], movableCards[^1]);
		}

		for (int index = 0; index < shuffleIndexes.Count; index++)
		{
			shuffledTableCards[shuffleIndexes[index]] = movableCards[index];
		}

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
