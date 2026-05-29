using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private Vector2 GetOpeningDealStartGlobalPosition()
	{
		return _tableCardLayoutArea.GlobalPosition + (_tableCardLayoutArea.Size - CardVisualSize) / 2.0f;
	}

	private void ShowOpeningHandCard(int playerIndex, int visibleCardCount)
	{
		if (playerIndex < 0 || playerIndex >= _openingVisibleHandCardCounts.Count)
		{
			return;
		}

		_openingVisibleHandCardCounts[playerIndex] = Mathf.Clamp(visibleCardCount, 0, _playerHands[playerIndex].Count);
		RenderPlayerHands();
	}

	private void ShowAllOpeningHandCards()
	{
		for (int playerIndex = 0; playerIndex < _openingVisibleHandCardCounts.Count && playerIndex < _playerHands.Count; playerIndex++)
		{
			_openingVisibleHandCardCounts[playerIndex] = _playerHands[playerIndex].Count;
		}
	}

	private void ShowOpeningPileCards(int playerIndex, int visibleCardCount)
	{
		if (playerIndex < 0 || playerIndex >= _openingVisiblePileCardCounts.Count)
		{
			return;
		}

		_openingVisiblePileCardCounts[playerIndex] = Mathf.Clamp(visibleCardCount, 0, _playerDecks[playerIndex].Count);
		RenderPlayerCardPiles();
	}

	private void ShowAllOpeningPileCards()
	{
		for (int playerIndex = 0; playerIndex < _openingVisiblePileCardCounts.Count && playerIndex < _playerDecks.Count; playerIndex++)
		{
			_openingVisiblePileCardCounts[playerIndex] = _playerDecks[playerIndex].Count;
		}
	}

	private Vector2 GetOpeningHandCardGlobalPosition(int playerIndex, int handIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHandAreas.Count)
		{
			return GetOpeningDealStartGlobalPosition();
		}

		Control handArea = _playerHandAreas[playerIndex];
		int displayCardCount = Math.Max(1, _playerHands[playerIndex].Count + _playerItemHands[playerIndex].Count);
		Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
		return handArea.GlobalPosition
			+ firstCardPosition
			+ handIndex * cardOffset;
	}

	private Vector2 GetOpeningPileGlobalPosition(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerCardPiles.Count)
		{
			return GetOpeningDealStartGlobalPosition();
		}

		int visibleCardsAfterDeal = Math.Min(
			Math.Min(_openingVisiblePileCardCounts[playerIndex] + 1, _playerDecks[playerIndex].Count),
			VisiblePileCards);
		return GetCardPileCardGlobalPosition(playerIndex, Math.Max(1, visibleCardsAfterDeal), Math.Max(0, visibleCardsAfterDeal - 1));
	}

	private List<int> BuildOpeningPileDealPlayerIndexes()
	{
		var playerIndexes = new List<int>();
		for (int playerIndex = 0; playerIndex < player_count && playerIndex < _playerDecks.Count; playerIndex++)
		{
			if (_playerDecks[playerIndex].Count > 0)
			{
				playerIndexes.Add(playerIndex);
			}
		}

		return playerIndexes;
	}

	private int GetOpeningTotalCardCount()
	{
		int totalCount = 0;
		for (int playerIndex = 0; playerIndex < player_count && playerIndex < _playerHands.Count && playerIndex < _playerDecks.Count; playerIndex++)
		{
			totalCount += _playerHands[playerIndex].Count + _playerDecks[playerIndex].Count;
		}

		return totalCount;
	}
}
