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
					await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
					continue;
				}

				await RefillPlayerBaseHandAsync(playerIndex, turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

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
					await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
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

		int playableItemCount = CountPlayableItemIndexes(playerIndex);
		if (TryPickBossKBestCaptureHandIndex(playerIndex, out int bossKHandIndex))
		{
			return await PlayCardFromHandAsync(playerIndex, bossKHandIndex, turnSequenceGeneration);
		}

		int playableCount = _playerHands[playerIndex].Count + playableItemCount;
		if (playableCount == 0)
		{
			return new PlayCardResult(false, false);
		}

		int selectedIndex = _rng.RandiRange(0, playableCount - 1);
		if (selectedIndex < _playerHands[playerIndex].Count)
		{
			return await PlayCardFromHandAsync(playerIndex, selectedIndex, turnSequenceGeneration);
		}

		if (!TryGetPlayableItemIndexAt(playerIndex, selectedIndex - _playerHands[playerIndex].Count, out int itemHandIndex))
		{
			return new PlayCardResult(false, false);
		}

		return await PlayItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
	}

	private bool TryPickBossKBestCaptureHandIndex(int playerIndex, out int handIndex)
	{
		handIndex = -1;
		if (!IsBossKEnemy(playerIndex) || playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return false;
		}

		int bestScore = 0;
		int bestCaptureCount = 0;
		for (int index = 0; index < _playerHands[playerIndex].Count; index++)
		{
			PlayingCard card = _playerHands[playerIndex][index];
			int captureCount = GetPotentialNormalCaptureCount(card);
			if (captureCount <= 0)
			{
				continue;
			}

			int score = GetPotentialNormalCapturePoints(playerIndex, card);
			if (captureCount >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5))
			{
				score += 1000 + captureCount;
			}

			if (score > bestScore || (score == bestScore && captureCount > bestCaptureCount))
			{
				bestScore = score;
				bestCaptureCount = captureCount;
				handIndex = index;
			}
		}

		return handIndex >= 0;
	}

	private int GetPotentialNormalCaptureCount(PlayingCard card)
	{
		if (ShouldPlayedCardCollectTable(card))
		{
			return _tableCards.Count > 0 ? CountCollectibleTableCards(_tableCards) + 1 : 0;
		}

		int matchingIndex = FindMatchingTableCardIndex(_tableCards, card);
		return matchingIndex >= 0 ? CountCollectibleTableCards(_tableCards, matchingIndex) + 1 : 0;
	}

	private int GetPotentialNormalCapturePoints(int playerIndex, PlayingCard card)
	{
		int matchingIndex = ShouldPlayedCardCollectTable(card) && _tableCards.Count > 0
			? 0
			: FindMatchingTableCardIndex(_tableCards, card);
		if (matchingIndex < 0 || !HasPotentialCapturedMiddleTableCards(matchingIndex))
		{
			return 0;
		}

		bool hasDoubleEffect = ShouldApplyNarwhalDoubleToPlayedCard(playerIndex, card);
		int points = 0;
		for (int tableCardIndex = matchingIndex + 1; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue)
			{
				points += GetCapturedCardScoreValueForPlayer(tableCard.Card.Value, playerIndex, hasDoubleEffect);
			}
		}

		return points;
	}

	private bool HasPotentialCapturedMiddleTableCards(int captureStartIndex)
	{
		return CountCollectibleTableCards(_tableCards, captureStartIndex) + 1 > 2;
	}

	private async Task ResolveAdventureEnemyTurnStartAbilityAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (IsAdventureEnemyWithMantaRayTideShift(playerIndex))
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

		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.TideShift);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !ShouldResolveMantaRayTideShift(playerIndex))
		{
			return;
		}

		TableCard tideShiftedCard = _tableCards[0];
		Texture2D? triggerTexture = GetVisibleTableCardTexture(tideShiftedCard);
		if (triggerTexture != null)
		{
			if (TryGetTableCardPosition(0, out Vector2 cardPosition))
			{
				await PlaySpecialCardTriggerEffectAsync(triggerTexture, _centerArena.GlobalPosition + cardPosition, 0);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !ShouldResolveMantaRayTideShift(playerIndex))
				{
					return;
				}
			}
		}

		await PlayMantaRayTideShiftTableMoveAnimationAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !ShouldResolveMantaRayTideShift(playerIndex))
		{
			return;
		}

		_tableCards.RemoveAt(0);
		_tableCards.Add(tideShiftedCard);
		MarkBossKActiveControlUsedIfNeeded(playerIndex);
		RenderTableCards();
	}

	private bool ShouldResolveMantaRayTideShift(int playerIndex)
	{
		return adventure_mode
			&& playerIndex == 1
			&& _mantaRayTideShiftCheckedThisTurn
			&& IsMantaRayTideShiftEnemyTurn(playerIndex)
			&& _tableCards.Count >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.TideShiftMinTableCards, 4)
			&& (!IsBossKEnemy(playerIndex) || ShouldResolveBossKTideShift(playerIndex));
	}

	private bool ShouldResolveBossKTideShift(int playerIndex)
	{
		if (!BossKCanUseActiveControl(playerIndex) || ShouldResolveDragonPrinceJack(playerIndex))
		{
			return false;
		}

		int bossCaptureBefore = GetBestPotentialNormalCaptureCount(playerIndex, _tableCards);
		int bossCaptureAfter = GetBestPotentialNormalCaptureCountAfterTideShift(playerIndex);
		if (bossCaptureAfter > bossCaptureBefore
			&& bossCaptureAfter >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5))
		{
			return true;
		}

		int playerCaptureBefore = GetBestPotentialNormalCaptureCount(MainPlayerIndex, _tableCards);
		int playerCaptureAfter = GetBestPotentialNormalCaptureCountAfterTideShift(MainPlayerIndex);
		return playerCaptureBefore >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5)
			&& playerCaptureAfter < playerCaptureBefore;
	}

	private int GetBestPotentialNormalCaptureCountAfterTideShift(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || _tableCards.Count == 0)
		{
			return 0;
		}

		int bestCaptureCount = 0;
		foreach (PlayingCard handCard in _playerHands[playerIndex])
		{
			if (ShouldPlayedCardCollectTable(handCard))
			{
				bestCaptureCount = Math.Max(bestCaptureCount, CountTideShiftedCollectibleTableCards() + 1);
				continue;
			}

			int matchingIndex = FindTideShiftedMatchingTableCardIndex(handCard);
			if (matchingIndex >= 0)
			{
				bestCaptureCount = Math.Max(bestCaptureCount, CountTideShiftedCollectibleTableCards(matchingIndex) + 1);
			}
		}

		return bestCaptureCount;
	}

	private int GetBestPotentialNormalCaptureCount(int playerIndex, IReadOnlyList<TableCard> tableCards)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || tableCards.Count == 0)
		{
			return 0;
		}

		int bestCaptureCount = 0;
		foreach (PlayingCard handCard in _playerHands[playerIndex])
		{
			if (ShouldPlayedCardCollectTable(handCard))
			{
				bestCaptureCount = Math.Max(bestCaptureCount, CountCollectibleTableCards(tableCards) + 1);
				continue;
			}

			int matchingIndex = FindMatchingTableCardIndex(tableCards, handCard);
			if (matchingIndex >= 0)
			{
				bestCaptureCount = Math.Max(bestCaptureCount, CountCollectibleTableCards(tableCards, matchingIndex) + 1);
			}
		}

		return bestCaptureCount;
	}

	private static int CountCollectibleTableCards(IReadOnlyList<TableCard> tableCards, int startIndex = 0)
	{
		int count = 0;
		for (int tableCardIndex = Math.Max(0, startIndex); tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			if (!tableCards[tableCardIndex].IsBuriedInSand)
			{
				count++;
			}
		}

		return count;
	}

	private static int FindMatchingTableCardIndex(IReadOnlyList<TableCard> tableCards, PlayingCard playedCard)
	{
		for (int tableCardIndex = tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			PlayingCard? tableCard = tableCards[tableCardIndex].Card;
			if (!tableCards[tableCardIndex].IsBuriedInSand && tableCard.HasValue && tableCard.Value.Rank == playedCard.Rank)
			{
				return tableCardIndex;
			}
		}

		return -1;
	}

	private int CountTideShiftedCollectibleTableCards(int startIndex = 0)
	{
		int count = 0;
		for (int shiftedIndex = Math.Max(0, startIndex); shiftedIndex < _tableCards.Count; shiftedIndex++)
		{
			if (!GetTideShiftedTableCard(shiftedIndex).IsBuriedInSand)
			{
				count++;
			}
		}

		return count;
	}

	private int FindTideShiftedMatchingTableCardIndex(PlayingCard playedCard)
	{
		for (int shiftedIndex = _tableCards.Count - 1; shiftedIndex >= 0; shiftedIndex--)
		{
			TableCard shiftedTableCard = GetTideShiftedTableCard(shiftedIndex);
			PlayingCard? tableCard = shiftedTableCard.Card;
			if (!shiftedTableCard.IsBuriedInSand && tableCard.HasValue && tableCard.Value.Rank == playedCard.Rank)
			{
				return shiftedIndex;
			}
		}

		return -1;
	}

	private TableCard GetTideShiftedTableCard(int shiftedIndex)
	{
		int tableCardIndex = shiftedIndex == _tableCards.Count - 1 ? 0 : shiftedIndex + 1;
		return _tableCards[tableCardIndex];
	}

	private bool IsMantaRayTideShiftEnemyTurn(int playerIndex)
	{
		return playerIndex == 1
			&& IsAdventureEnemyWithMantaRayTideShift(playerIndex)
			&& _currentTurnPlayerIndex == playerIndex;
	}

	private bool IsAdventureEnemyWithMantaRayTideShift(int playerIndex)
	{
		return playerIndex == 1
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.TideShift);
	}

	private void ResetMantaRayTideShiftTurnStateIfNeeded(int previousPlayerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.TideShift)
			|| previousPlayerIndex == 1
			|| _currentTurnPlayerIndex != 1)
		{
			return;
		}

		_mantaRayTideShiftCheckedThisTurn = false;
		_bossKActiveControlUsedThisTurn = false;
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
		await ResolveAdventureEnemyTurnEndAbilityAsync(playerIndex, result, _turnSequenceGeneration);
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

		await RefillCurrentTurnPlayerBaseHandAsync(_turnSequenceGeneration);
	}

	private async Task RefillCurrentTurnPlayerBaseHandAsync(int turnSequenceGeneration)
	{
		await RefillPlayerBaseHandAsync(_currentTurnPlayerIndex, turnSequenceGeneration);
	}

	private async Task RefillPlayerBaseHandAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0
			|| playerIndex >= player_count
			|| playerIndex >= _playerDecks.Count
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count)
		{
			return;
		}

		while (turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver()
			&& _playerHands[playerIndex].Count < StartingHandCards
			&& GetTotalHandCardCount(playerIndex) < MaxTotalHandCards
			&& _playerDecks[playerIndex].Count > 0)
		{
			DrawnHandCard? drawnCard = DrawReplacementCard(playerIndex);
			if (!drawnCard.HasValue)
			{
				break;
			}

			await PlayReplacementDrawAnimationAsync(playerIndex, drawnCard, turnSequenceGeneration);
		}

		RenderPlayerCardPiles();
		RenderPlayerHands();
	}

	private bool PlayerHasPlayableHandCards(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		return _playerHands[playerIndex].Count > 0 || HasPlayableItemCard(playerIndex);
	}

	private bool HasPlayableItemCard(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			if (CanPlayItemCard(playerIndex, itemHand[itemIndex].Id))
			{
				return true;
			}
		}

		return false;
	}

	private int CountPlayableItemIndexes(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return 0;
		}

		int playableCount = 0;
		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			if (CanPlayItemCard(playerIndex, itemHand[itemIndex].Id))
			{
				playableCount++;
			}
		}

		return playableCount;
	}

	private bool TryGetPlayableItemIndexAt(int playerIndex, int playableOrdinal, out int itemHandIndex)
	{
		itemHandIndex = -1;
		if (playableOrdinal < 0 || playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		int currentOrdinal = 0;
		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			if (!CanPlayItemCard(playerIndex, itemHand[itemIndex].Id))
			{
				continue;
			}

			if (currentOrdinal == playableOrdinal)
			{
				itemHandIndex = itemIndex;
				return true;
			}

			currentOrdinal++;
		}

		return false;
	}

	private bool CanPlayItemCard(int playerIndex, string itemCardId)
	{
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			|| !definition.EnabledInGame
			|| !definition.PlayableFromHand)
		{
			return false;
		}

		if (_itemPlayPresentationValidators.TryGetValue(definition.PlayPresentation, out CanPlayItemCardPresentation? presentationValidator)
			&& !presentationValidator(playerIndex, definition))
		{
			return false;
		}

		if (RequiresPlayerTarget(itemCardId) && !HasPlayableTargetForItemCard(itemCardId, playerIndex))
		{
			return false;
		}

		if (definition.EffectValues.GetString(CardEffectValueKeys.Target) == CardEffectTargets.PlayerDeck
			&& (playerIndex < 0 || playerIndex >= _playerDecks.Count || _playerDecks[playerIndex].Count == 0))
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

	private void ShowWinnerSequence(int playerIndex)
	{
		_ = ShowWinnerSequenceAsync(playerIndex);
	}

	private async Task ShowWinnerSequenceAsync(int playerIndex)
	{
		await PlayDefeatFinisherIfNeededAsync(playerIndex);
		if (_winnerPlayerIndex != playerIndex)
		{
			return;
		}

		ShowWinnerBanner(playerIndex);
	}

	private bool PlayerHasCards(int playerIndex)
	{
		return _playerHands[playerIndex].Count > 0 || _playerDecks[playerIndex].Count > 0 || HasPlayableItemCard(playerIndex);
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

		for (int avatarPlayerIndex = 0; avatarPlayerIndex < player_count; avatarPlayerIndex++)
		{
			PlayPlayerAvatarAction(
				avatarPlayerIndex,
				avatarPlayerIndex == playerIndex ? CharacterAvatar.AvatarAction.Victory : CharacterAvatar.AvatarAction.Defeat);
		}

		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		ShowWinnerSequence(playerIndex);
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

	private bool IsMainPlayerHandLimitDiscardSelectionActive()
	{
		return !IsGameOver()
			&& _pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard
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
