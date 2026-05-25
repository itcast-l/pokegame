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
				if (playerIndex >= player_count)
				{
					_currentTurnPlayerIndex = GetNextTurnPlayerIndex(playerIndex);
					continue;
				}

				ClearGardenEelBuriedCardIfNeeded(playerIndex);
				await ResolveAdventureEnemyTurnStartAbilityAsync(playerIndex, turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				PlayCardResult turnStartResult = await ResolveDragonPrinceJackTurnStartAsync(playerIndex, turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				if (turnStartResult.Played)
				{
					if (turnStartResult.CompletesTurn)
					{
						await AdvanceTurnAfterPlayedResultAsync(playerIndex, turnStartResult);
					}

					continue;
				}

				if (!PlayerHasPlayableHandCards(playerIndex))
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

				if (result.CompletesTurn)
				{
					await AdvanceTurnAfterPlayedResultAsync(playerIndex, result);
				}
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

	private async Task PlayMainPlayerCardAndAdvanceAsync(int handIndex, int turnSequenceGeneration, Vector2? startGlobalPositionOverride = null)
	{
		if (_isProcessingCardPlay || IsGameOver())
		{
			return;
		}

		_isProcessingCardPlay = true;
		try
		{
			PlayCardResult result = await PlayCardFromHandAsync(MainPlayerIndex, handIndex, turnSequenceGeneration, startGlobalPositionOverride);
			if (!result.Played)
			{
				return;
			}

			if (IsGameOver())
			{
				return;
			}

			if (result.CompletesTurn)
			{
				await AdvanceTurnAfterPlayedResultAsync(MainPlayerIndex, result);
				_isProcessingCardPlay = false;
				if (!result.Captured)
				{
					await ResolveOpponentTurnsAsync(turnSequenceGeneration);
				}
			}
			else
			{
				_isProcessingCardPlay = false;
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

			if (result.CompletesTurn)
			{
				await AdvanceTurnAfterPlayedResultAsync(MainPlayerIndex, result);
				_isProcessingCardPlay = false;
				if (!result.Captured)
				{
					await ResolveOpponentTurnsAsync(turnSequenceGeneration);
				}
			}
			else
			{
				_isProcessingCardPlay = false;
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

		if (ShouldGardenEelBuryNextPlayedCard(playerIndex) && _playerHands[playerIndex].Count > 0)
		{
			return await PlayCardFromHandAsync(
				playerIndex,
				_rng.RandiRange(0, _playerHands[playerIndex].Count - 1),
				turnSequenceGeneration);
		}

		int selectedIndex = _rng.RandiRange(0, playableCount - 1);
		if (selectedIndex < _playerHands[playerIndex].Count)
		{
			return await PlayCardFromHandAsync(playerIndex, selectedIndex, turnSequenceGeneration);
		}

		int itemHandIndex = playableItemIndexes[selectedIndex - _playerHands[playerIndex].Count];
		return await PlayItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
	}

	private async Task ResolveAdventureEnemyTurnStartAbilityAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (IsMantaRayTideShiftEnemyTurn(playerIndex))
		{
			if (_mantaRayTideShiftCheckedThisTurn)
			{
				return;
			}

			_mantaRayTideShiftCheckedThisTurn = true;
		}

		if (!ShouldResolveMantaRayTideShift(playerIndex))
		{
			return;
		}

		TableCard tideShiftedCard = _tableCards[0];
		Texture2D? triggerTexture = GetVisibleTableCardTexture(tideShiftedCard);
		if (triggerTexture != null)
		{
			PlayPlayerAvatarAction(playerIndex, CharacterAvatar.AvatarAction.Score);
			List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
			if (cardPositions.Count > 0)
			{
				await PlaySpecialCardTriggerEffectAsync(triggerTexture, _centerArena.GlobalPosition + cardPositions[0], 0);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !ShouldResolveMantaRayTideShift(playerIndex))
				{
					return;
				}
			}
		}

		_tableCards.RemoveAt(0);
		_tableCards.Add(tideShiftedCard);
		RenderTableCards();
	}

	private bool ShouldResolveMantaRayTideShift(int playerIndex)
	{
		return adventure_mode
			&& playerIndex == 1
			&& _mantaRayTideShiftCheckedThisTurn
			&& IsMantaRayTideShiftEnemyTurn(playerIndex)
			&& _tableCards.Count >= 4;
	}

	private bool IsMantaRayTideShiftEnemyTurn(int playerIndex)
	{
		return playerIndex == 1
			&& CharacterAvatarCatalog.GetAdventureStageNumber(adventure_enemy_avatar_id) == 6
			&& _currentTurnPlayerIndex == playerIndex;
	}

	private void ResetMantaRayTideShiftTurnStateIfNeeded(int previousPlayerIndex)
	{
		if (!adventure_mode
			|| CharacterAvatarCatalog.GetAdventureStageNumber(adventure_enemy_avatar_id) != 6
			|| previousPlayerIndex == 1
			|| _currentTurnPlayerIndex != 1)
		{
			return;
		}

		_mantaRayTideShiftCheckedThisTurn = false;
	}

	private int AddBombCardToTable(ItemCard bombCard, int ownerPlayerIndex)
	{
		_tableCards.Add(new TableCard(null, false, bombCard, ownerPlayerIndex));
		return _tableCards.Count - 1;
	}

	private int InsertBombCardIntoTable(ItemCard bombCard, int ownerPlayerIndex, int tableCardIndex)
	{
		int clampedIndex = Mathf.Clamp(tableCardIndex, 0, _tableCards.Count);
		_tableCards.Insert(clampedIndex, new TableCard(null, false, bombCard, ownerPlayerIndex));
		return clampedIndex;
	}

	private async Task AdvanceTurnAfterPlayedResultAsync(int playerIndex, PlayCardResult result)
	{
		if (!result.CompletesTurn)
		{
			return;
		}

		ClearJellyfishBubbleIfMainPlayerTurnCompleted(playerIndex, result);

		if (_questionCardRefreshAnchorPlayerIndex < 0)
		{
			_completedTurnsSinceQuestionCardRefresh = 0;
			_questionCardRefreshAnchorPlayerIndex = playerIndex;
		}

		_currentTurnPlayerIndex = result.Captured ? playerIndex : GetNextTurnPlayerIndex(playerIndex);
		await ResolveAdventureEnemyTurnEndAbilityAsync(playerIndex, result);
		if (IsGameOver())
		{
			return;
		}

		ResetMantaRayTideShiftTurnStateIfNeeded(playerIndex);
		ResetJellyfishBubbleTurnStateIfNeeded(playerIndex);
		if (!result.Captured)
		{
			int refreshedQuestionCardIndex = AdvanceQuestionCardRefreshTurnCounter(_currentTurnPlayerIndex);
			if (refreshedQuestionCardIndex >= 0 && refreshedQuestionCardIndex < _tableCards.Count)
			{
				_tableCards[refreshedQuestionCardIndex].IsHiddenDuringCapture = true;
			}

			RenderTableCards();
			if (refreshedQuestionCardIndex >= 0 && refreshedQuestionCardIndex < _tableCards.Count)
			{
				_tableCards[refreshedQuestionCardIndex].IsHiddenDuringCapture = false;
				await PlayQuestionCardRefreshAnimationAsync(refreshedQuestionCardIndex);
				RenderTableCards();
			}
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
			if (!CanPlayItemCard(playerIndex, itemHand[itemIndex].Id))
			{
				continue;
			}

			itemIndexes.Add(itemIndex);
		}

		return itemIndexes;
	}

	private bool CanPlayItemCard(int playerIndex, string itemCardId)
	{
		if (!_itemCardCatalog.IsPlayable(itemCardId))
		{
			return false;
		}

		if (RequiresPlayerTarget(itemCardId) && !HasPlayableTargetForItemCard(itemCardId, playerIndex))
		{
			return false;
		}

		if (IsItemEffectType(itemCardId, CardEffectTypes.RevealCards) && !HasLensRevealTargets(playerIndex))
		{
			return false;
		}

		if (IsItemEffectType(itemCardId, CardEffectTypes.DrawCard) && (playerIndex < 0 || playerIndex >= _playerDecks.Count || _playerDecks[playerIndex].Count == 0))
		{
			return false;
		}

		if (IsItemEffectType(itemCardId, CardEffectTypes.ShuffleTable) && GetMovableTableShuffleCardCount() < 2)
		{
			return false;
		}

		if (IsAttachmentItemCard(itemCardId) && !PlayerHasAttachableMagnetCard(playerIndex, itemCardId))
		{
			return false;
		}

		return true;
	}

	private void CheckForWinner()
	{
		if (IsGameOver())
		{
			return;
		}

		int scoreWinnerPlayerIndex = GetScoreWinnerPlayerIndex();
		if (scoreWinnerPlayerIndex >= 0)
		{
			DeclareWinner(scoreWinnerPlayerIndex);
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

	private int GetMovableTableShuffleCardCount()
	{
		int movableCardCount = 0;
		foreach (TableCard tableCard in _tableCards)
		{
			if (!IsFixedDuringTableShuffle(tableCard))
			{
				movableCardCount++;
			}
		}

		return movableCardCount;
	}

	private void DeclareWinner(int playerIndex)
	{
		_winnerPlayerIndex = playerIndex;
		if (adventure_mode && playerIndex == MainPlayerIndex)
		{
			CharacterAvatarCatalog.RecordAdventureVictory(adventure_enemy_avatar_id);
		}

		_turnSequenceGeneration++;
		_isResolvingTurnSequence = false;
		_isProcessingCardPlay = false;
		_currentTurnPlayerIndex = playerIndex;

		ShowWinnerBanner(playerIndex);
		for (int avatarPlayerIndex = 0; avatarPlayerIndex < player_count; avatarPlayerIndex++)
		{
			PlayPlayerAvatarAction(
				avatarPlayerIndex,
				avatarPlayerIndex == playerIndex ? CharacterAvatar.AvatarAction.Victory : CharacterAvatar.AvatarAction.Defeat);
		}

		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
	}

	private bool IsGameOver()
	{
		return _winnerPlayerIndex >= 0;
	}

	private bool CanMainPlayerAct()
	{
		return !IsGameOver()
			&& !HasPendingItemSelection()
			&& !_isOpeningDealAnimationPlaying
			&& !_isResolvingTurnSequence
			&& !_isProcessingCardPlay
			&& _currentTurnPlayerIndex == MainPlayerIndex;
	}

	private bool IsMainPlayerMagnetSelectionActive()
	{
		return !IsGameOver()
			&& _pendingItemSelectionMode == PendingItemSelectionMode.MagnetHandCard
			&& _pendingItemOwnerIndex == MainPlayerIndex;
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
