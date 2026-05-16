using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task ResolveOpponentTurnsAsync(int turnSequenceGeneration)
	{
		if (_isResolvingTurnSequence || IsGameOver())
		{
			return;
		}

		_isResolvingTurnSequence = true;
		try
		{
			while (turnSequenceGeneration == _turnSequenceGeneration && _currentTurnPlayerIndex != MainPlayerIndex && !IsGameOver())
			{
				int playerIndex = _currentTurnPlayerIndex;
				if (playerIndex >= player_count || !PlayerHasPlayableHandCards(playerIndex))
				{
					_currentTurnPlayerIndex = GetNextTurnPlayerIndex(playerIndex);
					continue;
				}

				await ToSignal(GetTree().CreateTimer(AiTurnDelaySeconds), SceneTreeTimer.SignalName.Timeout);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				PlayCardResult result = await PlayRandomHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
				if (!result.Played)
				{
					return;
				}

				if (IsGameOver())
				{
					return;
				}

				AdvanceTurnAfterPlayedResult(playerIndex, result);
			}
		}
		finally
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				_isResolvingTurnSequence = false;
				RenderPlayerHands();
			}
		}
	}

	private async Task PlayMainPlayerCardAndAdvanceAsync(int handIndex, int turnSequenceGeneration)
	{
		if (_isProcessingCardPlay || IsGameOver())
		{
			return;
		}

		_isProcessingCardPlay = true;
		RenderPlayerHands();
		try
		{
			PlayCardResult result = await PlayCardFromHandAsync(MainPlayerIndex, handIndex, turnSequenceGeneration);
			if (!result.Played)
			{
				return;
			}

			if (IsGameOver())
			{
				return;
			}

			AdvanceTurnAfterPlayedResult(MainPlayerIndex, result);

			_isProcessingCardPlay = false;
			if (!result.Captured)
			{
				await ResolveOpponentTurnsAsync(turnSequenceGeneration);
			}
		}
		finally
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				_isProcessingCardPlay = false;
				RenderPlayerHands();
			}
		}
	}

	private async Task PlayMainPlayerItemCardAndAdvanceAsync(int itemHandIndex, int turnSequenceGeneration)
	{
		if (_isProcessingCardPlay || IsGameOver())
		{
			return;
		}

		_isProcessingCardPlay = true;
		RenderPlayerHands();
		try
		{
			PlayCardResult result = await PlayItemCardFromHandAsync(MainPlayerIndex, itemHandIndex, turnSequenceGeneration);
			if (!result.Played || HasPendingItemSelection())
			{
				return;
			}

			if (IsGameOver())
			{
				return;
			}

			AdvanceTurnAfterPlayedResult(MainPlayerIndex, result);
			_isProcessingCardPlay = false;
			if (!result.Captured)
			{
				await ResolveOpponentTurnsAsync(turnSequenceGeneration);
			}
		}
		finally
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver() && !HasPendingItemSelection())
			{
				_isProcessingCardPlay = false;
				RenderPlayerHands();
			}
		}
	}

	private async Task<PlayCardResult> PlayRandomHandEntryForPlayerAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		List<int> playableItemIndexes = BuildPlayableItemIndexes(playerIndex);
		int playableCount = _playerHands[playerIndex].Count + playableItemIndexes.Count;
		if (playableCount == 0)
		{
			return new PlayCardResult(false, false);
		}

		int selectedIndex = _rng.RandiRange(0, playableCount - 1);
		if (selectedIndex < _playerHands[playerIndex].Count)
		{
			return await PlayCardFromHandAsync(playerIndex, selectedIndex, turnSequenceGeneration);
		}

		int itemHandIndex = playableItemIndexes[selectedIndex - _playerHands[playerIndex].Count];
		return await PlayItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
	}

	private async Task<PlayCardResult> PlayCardFromHandAsync(int playerIndex, int handIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		List<PlayingCard> hand = _playerHands[playerIndex];
		if (handIndex < 0 || handIndex >= hand.Count)
		{
			return new PlayCardResult(false, false);
		}

		PlayingCard playedCard = hand[handIndex];
		hand.RemoveAt(handIndex);
		int captureStartIndex = AddPlayedCardToTable(playedCard);

		RenderPlayerHands();
		RenderTableCards();
		if (captureStartIndex >= 0)
		{
			await ToSignal(GetTree().CreateTimer(TableCaptureDelaySeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			CaptureTableCards(playerIndex, captureStartIndex);
			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
			DrawReplacementCard(playerIndex);
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
		}
		else
		{
			DrawReplacementCard(playerIndex);
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
		}

		CheckForWinner();
		return new PlayCardResult(true, captureStartIndex >= 0);
	}

	private async Task<PlayCardResult> PlayItemCardFromHandAsync(int playerIndex, int itemHandIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		if (itemHandIndex < 0 || itemHandIndex >= itemHand.Count)
		{
			return new PlayCardResult(false, false);
		}

		ItemCard itemCard = itemHand[itemHandIndex];
		if (!PlayableSpecialItemCardIds.Contains(itemCard.Id))
		{
			return new PlayCardResult(false, false);
		}

		if (playerIndex == MainPlayerIndex && RequiresPlayerTarget(itemCard.Id))
		{
			if (!HasPlayableTargetForItemCard(itemCard.Id, playerIndex))
			{
				return new PlayCardResult(false, false);
			}

			StartPendingItemSelection(itemCard.Id, playerIndex, itemHandIndex, turnSequenceGeneration);
			return new PlayCardResult(true, false);
		}

		int targetPlayerIndex = -1;
		int targetHandIndex = -1;
		if (RequiresPlayerTarget(itemCard.Id))
		{
			targetPlayerIndex = PickItemTargetPlayer(itemCard.Id, playerIndex);
			targetHandIndex = itemCard.Id == QueenItemCardId ? PickTargetHandIndex(targetPlayerIndex) : -1;
			if (targetPlayerIndex < 0 || (itemCard.Id == QueenItemCardId && targetHandIndex < 0))
			{
				return new PlayCardResult(false, false);
			}
		}

		itemHand.RemoveAt(itemHandIndex);
		PlayCardResult result = ResolveItemCardEffect(itemCard.Id, playerIndex, targetPlayerIndex, targetHandIndex);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		CheckForWinner();
		return result;
	}

	private int AddPlayedCardToTable(PlayingCard playedCard)
	{
		int matchingIndex = FindMatchingTableCardIndex(playedCard);
		bool isFaceUp = matchingIndex >= 0;
		if (isFaceUp)
		{
			_tableCards[matchingIndex].IsFaceUp = true;
		}

		_tableCards.Add(new TableCard(playedCard, isFaceUp));
		return matchingIndex;
	}

	private void AdvanceQuestionCardRefreshTurnCounter()
	{
		_completedTurnsSinceQuestionCardRefresh++;
		if (_completedTurnsSinceQuestionCardRefresh < GetQuestionCardRefreshTurnCount())
		{
			return;
		}

		_completedTurnsSinceQuestionCardRefresh = 0;
		PlaceQuestionCardOnNextTableSlot();
	}

	private void PlaceQuestionCardOnNextTableSlot()
	{
		ItemCard? itemOverlay = CreateItemCard(QuestionMarkItemCardId);
		if (!itemOverlay.HasValue)
		{
			return;
		}

		_tableCards.Add(new TableCard(null, true, itemOverlay));
	}

	private int GetQuestionCardRefreshTurnCount()
	{
		int roundsPerQuestionCard = player_count == 2 ? 2 : 1;
		return Math.Max(1, player_count * roundsPerQuestionCard);
	}

	private int FindMatchingTableCardIndex(PlayingCard playedCard)
	{
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			PlayingCard? tableCard = _tableCards[tableCardIndex].Card;
			if (tableCard.HasValue && tableCard.Value.Rank == playedCard.Rank)
			{
				return tableCardIndex;
			}
		}

		return -1;
	}

	private void CaptureTableCards(int playerIndex, int captureStartIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return;
		}

		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return;
		}

		int captureCount = _tableCards.Count - captureStartIndex;
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.Card.HasValue)
			{
				_playerDecks[playerIndex].Add(tableCard.Card.Value);
			}

			if (tableCard.ItemOverlay.HasValue && tableCard.ItemOverlay.Value.Id == QuestionMarkItemCardId)
			{
				GrantQuestionCardReward(playerIndex);
			}
		}

		_tableCards.RemoveRange(captureStartIndex, captureCount);
	}

	private void GrantQuestionCardReward(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count || QuestionRewardItemCardIds.Length == 0)
		{
			return;
		}

		string itemCardId = QuestionRewardItemCardIds[_rng.RandiRange(0, QuestionRewardItemCardIds.Length - 1)];
		ItemCard? itemCard = CreateItemCard(itemCardId);
		if (!itemCard.HasValue)
		{
			return;
		}

		_playerItemHands[playerIndex].Add(itemCard.Value);
	}

	private void AdvanceTurnAfterPlayedResult(int playerIndex, PlayCardResult result)
	{
		_currentTurnPlayerIndex = result.Captured ? playerIndex : GetNextTurnPlayerIndex(playerIndex);
		if (!result.Captured)
		{
			AdvanceQuestionCardRefreshTurnCounter();
			RenderTableCards();
		}
	}

	private bool PlayerHasPlayableHandCards(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		return _playerHands[playerIndex].Count > 0 || BuildPlayableItemIndexes(playerIndex).Count > 0;
	}

	private List<int> BuildPlayableItemIndexes(int playerIndex)
	{
		var itemIndexes = new List<int>();
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return itemIndexes;
		}

		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			string itemCardId = itemHand[itemIndex].Id;
			if (!PlayableSpecialItemCardIds.Contains(itemCardId))
			{
				continue;
			}

			if (RequiresPlayerTarget(itemCardId) && !HasPlayableTargetForItemCard(itemCardId, playerIndex))
			{
				continue;
			}

			itemIndexes.Add(itemIndex);
		}

		return itemIndexes;
	}

	private void CheckForWinner()
	{
		if (IsGameOver())
		{
			return;
		}

		int remainingPlayerIndex = -1;
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (!PlayerHasCards(playerIndex))
			{
				continue;
			}

			if (remainingPlayerIndex >= 0)
			{
				return;
			}

			remainingPlayerIndex = playerIndex;
		}

		if (remainingPlayerIndex >= 0)
		{
			DeclareWinner(remainingPlayerIndex);
		}
	}

	private bool PlayerHasCards(int playerIndex)
	{
		return _playerHands[playerIndex].Count > 0 || _playerDecks[playerIndex].Count > 0 || BuildPlayableItemIndexes(playerIndex).Count > 0;
	}

	private void DeclareWinner(int playerIndex)
	{
		_winnerPlayerIndex = playerIndex;
		_turnSequenceGeneration++;
		_isResolvingTurnSequence = false;
		_isProcessingCardPlay = false;
		_currentTurnPlayerIndex = playerIndex;

		_winnerLabel.Text = $"玩家 {playerIndex + 1} 获胜";
		_winnerLabel.Visible = true;
		RenderPlayerHands();
	}

	private bool IsGameOver()
	{
		return _winnerPlayerIndex >= 0;
	}

	private bool CanMainPlayerAct()
	{
		return !IsGameOver() && !HasPendingItemSelection() && !_isResolvingTurnSequence && !_isProcessingCardPlay && _currentTurnPlayerIndex == MainPlayerIndex;
	}

	private int GetNextTurnPlayerIndex(int currentPlayerIndex)
	{
		int currentOrderIndex = Array.IndexOf(ClockwiseTurnOrder, currentPlayerIndex);
		if (currentOrderIndex < 0)
		{
			return MainPlayerIndex;
		}

		for (int offset = 1; offset <= ClockwiseTurnOrder.Length; offset++)
		{
			int nextPlayerIndex = ClockwiseTurnOrder[(currentOrderIndex + offset) % ClockwiseTurnOrder.Length];
			if (nextPlayerIndex < player_count)
			{
				return nextPlayerIndex;
			}
		}

		return MainPlayerIndex;
	}
}
