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

				await AdvanceTurnAfterPlayedResultAsync(playerIndex, result);
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

			await AdvanceTurnAfterPlayedResultAsync(MainPlayerIndex, result);

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

			await AdvanceTurnAfterPlayedResultAsync(MainPlayerIndex, result);
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

	private async Task<PlayCardResult> PlayCardFromHandAsync(int playerIndex, int handIndex, int turnSequenceGeneration, Vector2? startGlobalPositionOverride = null)
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
		Control handArea = _playerHandAreas[playerIndex];
		Vector2 cardOffset = GetHandCardOffset(playerIndex, hand.Count, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, hand.Count, handArea.Size, cardOffset);
		Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{handIndex}");
		TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
		Vector2 startGlobalPosition = startGlobalPositionOverride
			?? sourceCardVisual?.GlobalPosition
			?? handArea.GlobalPosition + firstCardPosition + handIndex * cardOffset;
		if (sourceCardSlot != null)
		{
			sourceCardSlot.Visible = false;
		}

		hand.RemoveAt(handIndex);
		int captureStartIndex = AddPlayedCardToTable(playedCard);
		int playedTableCardIndex = _tableCards.Count - 1;
		bool playedTableCardIsFaceUp = _tableCards[playedTableCardIndex].IsFaceUp;

		await PlayCardToTableAnimationAsync(
			playerIndex,
			playedCard,
			startGlobalPosition,
			playedTableCardIndex,
			playedTableCardIsFaceUp);

		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerHands();
		RenderTableCards();
		if (captureStartIndex >= 0)
		{
			await ToSignal(GetTree().CreateTimer(TableCaptureDelaySeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			await RevealCapturedBaseCardsFromBottomAsync(captureStartIndex, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			TableSpecialCardTrigger? specialTrigger = GetCapturedSpecialCardTrigger(captureStartIndex);
			if (specialTrigger.HasValue)
			{
				await PlayTableSpecialCardTriggerEffectAsync(specialTrigger.Value);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new PlayCardResult(false, false);
				}
			}

			List<CapturedTableCardVisual> capturedCardVisuals = BuildCapturedTableCardVisuals(captureStartIndex, false);
			int collectedPoints = GetCapturedTablePoints(captureStartIndex);
			int scorePlayerIndex = GetCapturedTableScorePlayerIndex(captureStartIndex, playerIndex);
			if (collectedPoints > 0)
			{
				List<CapturedTableCardVisual> scoringCardVisuals = BuildCapturedTableCardVisuals(captureStartIndex, false, true);
				await PlayScoreFillCollectionAsync(
					scorePlayerIndex,
					scoringCardVisuals,
					turnSequenceGeneration,
					() => AddPlayerScore(scorePlayerIndex, collectedPoints));
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new PlayCardResult(false, false);
				}
			}

			await PlayCapturedCardsToDeckAnimationAsync(playerIndex, captureStartIndex, capturedCardVisuals, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			int collectedCardCount = CaptureTableCards(
				playerIndex,
				captureStartIndex,
				true,
				false,
				out QuestionCardRewardAnimation? questionRewardAnimation);
			if (IsGameOver())
			{
				return new PlayCardResult(true, true, collectedCardCount > 0);
			}

			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
			DrawReplacementCard(playerIndex);
			RenderPlayerCardPiles();
			RenderTableCards();
			await PlayQuestionCardRewardAnimationAsync(playerIndex, questionRewardAnimation, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			await ResolvePendingMagnetAttachmentsAsync(turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			RenderPlayerHands();
			if (collectedCardCount > 0)
			{
				await PlayPhysicalDeckShuffleAsync(playerIndex);
			}
		}
		else
		{
			DrawReplacementCard(playerIndex);
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
		}

		CheckForWinner();
		return new PlayCardResult(true, captureStartIndex >= 0, captureStartIndex >= 0);
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

		if (itemCard.Id == BombItemCardId)
		{
			return await PlayBombCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
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

		Vector2? itemGlobalPosition = GetHandItemCardGlobalPosition(playerIndex, itemHandIndex);
		itemHand.RemoveAt(itemHandIndex);
		RenderPlayerHands();
		await PlaySpecialCardTriggerEffectAsync(itemCard.Texture, itemGlobalPosition ?? Vector2.Zero, 0, GetCardRotationDegreesForPlayer(playerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		PlayCardResult result = ResolveItemCardEffect(itemCard.Id, playerIndex, targetPlayerIndex, targetHandIndex);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerCardPiles();
		RenderTableCards();
		await PlayQuestionCardRewardAnimationAsync(playerIndex, result.QuestionRewardAnimation, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		await ResolvePendingMagnetAttachmentsAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerHands();
		if (result.DeckCollected)
		{
			await PlayPhysicalDeckShuffleAsync(playerIndex);
		}

		CheckForWinner();
		return result;
	}

	private async Task<PlayCardResult> PlayBombCardFromHandAsync(int playerIndex, int itemHandIndex, int turnSequenceGeneration)
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

		ItemCard bombCard = itemHand[itemHandIndex];
		int displayCardCount = _playerHands[playerIndex].Count + itemHand.Count;
		int displayIndex = _playerHands[playerIndex].Count + itemHandIndex;
		Control handArea = _playerHandAreas[playerIndex];
		Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
		Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{displayIndex}");
		TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
		Vector2 startGlobalPosition = sourceCardVisual?.GlobalPosition
			?? handArea.GlobalPosition + firstCardPosition + displayIndex * cardOffset;
		if (sourceCardSlot != null)
		{
			sourceCardSlot.Visible = false;
		}

		itemHand.RemoveAt(itemHandIndex);
		int tableCardIndex = AddBombCardToTable(bombCard, playerIndex);
		await PlayItemCardToTableAnimationAsync(playerIndex, bombCard, startGlobalPosition, tableCardIndex, false);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerHands();
		RenderTableCards();
		await ResolveBombPairAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderTableCards();
		CheckForWinner();
		return new PlayCardResult(true, false);
	}

	private async Task ResolveBombPairAsync(int turnSequenceGeneration)
	{
		if (!TryGetLastTableBombPair(out int firstBombIndex, out int secondBombIndex))
		{
			return;
		}

		await RevealBombPairAsync(firstBombIndex, secondBombIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		await PlayBombPairTriggerEffectsAsync(firstBombIndex, secondBombIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		await PlayBombPairDestroySequenceAsync(firstBombIndex, secondBombIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		RemoveBombPairAndCardsBetween(firstBombIndex, secondBombIndex);
	}

	private bool TryGetLastTableBombPair(out int firstBombIndex, out int secondBombIndex)
	{
		firstBombIndex = -1;
		secondBombIndex = -1;
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			if (!IsBombTableCard(_tableCards[tableCardIndex]))
			{
				continue;
			}

			if (secondBombIndex < 0)
			{
				secondBombIndex = tableCardIndex;
				continue;
			}

			firstBombIndex = tableCardIndex;
			return true;
		}

		return false;
	}

	private async Task RevealBombPairAsync(int firstBombIndex, int secondBombIndex, int turnSequenceGeneration)
	{
		if (firstBombIndex < 0
			|| secondBombIndex < 0
			|| firstBombIndex >= _tableCards.Count
			|| secondBombIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard firstBomb = _tableCards[firstBombIndex];
		TableCard secondBomb = _tableCards[secondBombIndex];
		Texture2D? firstTexture = firstBomb.ItemOverlay?.Texture;
		Texture2D? secondTexture = secondBomb.ItemOverlay?.Texture;
		var revealTasks = new List<Task>();
		if (!firstBomb.IsFaceUp && firstTexture != null)
		{
			revealTasks.Add(PlayTableCardRevealAnimationAsync(firstBombIndex, firstTexture));
		}

		if (!secondBomb.IsFaceUp && secondTexture != null)
		{
			revealTasks.Add(PlayTableCardRevealAnimationAsync(secondBombIndex, secondTexture));
		}

		if (revealTasks.Count > 0)
		{
			await Task.WhenAll(revealTasks);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}

		firstBomb.IsFaceUp = true;
		secondBomb.IsFaceUp = true;
		RenderTableCards();
		await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
	}

	private async Task PlayBombPairTriggerEffectsAsync(int firstBombIndex, int secondBombIndex, int turnSequenceGeneration)
	{
		await PlayBombTableCardTriggerEffectAsync(firstBombIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		await PlayBombTableCardTriggerEffectAsync(secondBombIndex, turnSequenceGeneration);
	}

	private async Task PlayBombTableCardTriggerEffectAsync(int tableCardIndex, int turnSequenceGeneration)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= cardPositions.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.ItemOverlay.HasValue || tableCard.ItemOverlay.Value.Id != BombItemCardId)
		{
			return;
		}

		ItemCard bombCard = tableCard.ItemOverlay.Value;
		await PlaySpecialCardTriggerEffectAsync(
			bombCard.Texture,
			_centerArena.GlobalPosition + cardPositions[tableCardIndex] + GetItemCardVisualPosition(bombCard.Id),
			tableCardIndex * 2 + 1);
	}

	private void RemoveBombPairAndCardsBetween(int firstBombIndex, int secondBombIndex)
	{
		if (firstBombIndex < 0 || secondBombIndex >= _tableCards.Count || firstBombIndex > secondBombIndex)
		{
			return;
		}

		_tableCards.RemoveRange(firstBombIndex, secondBombIndex - firstBombIndex + 1);
	}

	private async Task PlayBombPairDestroySequenceAsync(int firstBombIndex, int secondBombIndex, int turnSequenceGeneration)
	{
		if (firstBombIndex < 0 || secondBombIndex >= _tableCards.Count || firstBombIndex >= secondBombIndex)
		{
			return;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (secondBombIndex >= cardPositions.Count)
		{
			return;
		}

		for (int tableCardIndex = secondBombIndex; tableCardIndex >= firstBombIndex; tableCardIndex--)
		{
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			if (tableCardIndex >= cardPositions.Count)
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			Texture2D? startTexture = GetVisibleTableCardTexture(tableCard);
			if (startTexture == null)
			{
				continue;
			}

			tableCard.IsHiddenDuringCapture = true;
			RenderTableCards();
			await PlayBombPairDestroyCardAsync(tableCardIndex, cardPositions[tableCardIndex]);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			await ToSignal(GetTree().CreateTimer(BombDestroyCardStaggerSeconds), SceneTreeTimer.SignalName.Timeout);
		}
	}

	private Texture2D? GetVisibleTableCardTexture(TableCard tableCard)
	{
		if (tableCard.Card.HasValue)
		{
			return tableCard.IsFaceUp || _cardBackTexture == null
				? tableCard.Card.Value.Texture
				: _cardBackTexture;
		}

		if (tableCard.ItemOverlay.HasValue)
		{
			return tableCard.IsFaceUp || _cardBackTexture == null
				? tableCard.ItemOverlay.Value.Texture
				: _cardBackTexture;
		}

		return null;
	}

	private async Task PlayBombPairDestroyCardAsync(int tableCardIndex, Vector2 centerArenaPosition)
	{
		TextureRect? fx = CreateBombPairDestroyFx(centerArenaPosition + CardVisualSize / 2.0f, tableCardIndex);
		if (fx == null)
		{
			return;
		}

		try
		{
			for (int frameIndex = 0; frameIndex < _bombPairDestroyEffectFrames.Count; frameIndex++)
			{
				if (!GodotObject.IsInstanceValid(fx))
				{
					return;
				}

				fx.Texture = _bombPairDestroyEffectFrames[frameIndex];
				await ToSignal(GetTree().CreateTimer(BombPairDestroyFxFrameSeconds), SceneTreeTimer.SignalName.Timeout);
			}
		}
		finally
		{
			if (GodotObject.IsInstanceValid(fx))
			{
				fx.QueueFree();
			}
		}
	}

	private TextureRect? CreateBombPairDestroyFx(Vector2 localCenter, int tableCardIndex)
	{
		if (_bombPairDestroyEffectFrames.Count == 0)
		{
			return null;
		}

		var fx = new TextureRect
		{
			Name = "BombPairDestroyFx",
			Texture = _bombPairDestroyEffectFrames[0],
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = localCenter - new Vector2(BombPairDestroyFxVisualSize, BombPairDestroyFxVisualSize) / 2.0f,
			Size = new Vector2(BombPairDestroyFxVisualSize, BombPairDestroyFxVisualSize),
			PivotOffset = new Vector2(BombPairDestroyFxVisualSize, BombPairDestroyFxVisualSize) / 2.0f,
			ZIndex = PlayedCardFlightZIndex + tableCardIndex * 2,
		};
		_centerArena.AddChild(fx);
		return fx;
	}

	private Vector2? GetTableCardGlobalCenter(int tableCardIndex)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return null;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= cardPositions.Count)
		{
			return null;
		}

		return _centerArena.GlobalPosition + cardPositions[tableCardIndex] + CardVisualSize / 2.0f;
	}

	private static bool IsBombTableCard(TableCard tableCard)
	{
		return tableCard.ItemOverlay.HasValue && tableCard.ItemOverlay.Value.Id == BombItemCardId;
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

	private int AddBombCardToTable(ItemCard bombCard, int ownerPlayerIndex)
	{
		_tableCards.Add(new TableCard(null, false, bombCard, ownerPlayerIndex));
		return _tableCards.Count - 1;
	}

	private async Task RevealCapturedBaseCardsFromBottomAsync(int captureStartIndex, int turnSequenceGeneration)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return;
		}

		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsFaceUp)
			{
				continue;
			}

			Texture2D? revealTexture = null;
			int? revealedCardPoints = null;
			if (tableCard.Card.HasValue)
			{
				revealTexture = tableCard.Card.Value.Texture;
				revealedCardPoints = GetCardScoreValue(tableCard.Card.Value);
			}
			else if (tableCard.ItemOverlay.HasValue)
			{
				revealTexture = tableCard.ItemOverlay.Value.Texture;
			}

			if (revealTexture == null)
			{
				continue;
			}

			await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			tableCard.IsFaceUp = true;
			RenderTableCards();
			if (revealedCardPoints.HasValue)
			{
				_ = PlayTableCardPointPopupAsync(tableCardIndex, revealedCardPoints.Value);
			}

			if (tableCard.Card.HasValue && tableCard.Card.Value.HasMagnetEffect)
			{
				await RevealRandomMagnetExtraTableCardAsync(captureStartIndex, turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return;
				}
			}

			await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}
	}

	private async Task RevealRandomMagnetExtraTableCardAsync(int captureStartIndex, int turnSequenceGeneration)
	{
		int tableCardIndex = PickRandomMagnetExtraRevealIndex(captureStartIndex);
		if (tableCardIndex < 0)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || tableCard.IsFaceUp)
		{
			return;
		}

		Texture2D revealTexture = tableCard.Card.Value.Texture;
		int revealedCardPoints = GetCardScoreValue(tableCard.Card.Value);
		await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		tableCard.IsFaceUp = true;
		RenderTableCards();
		_ = PlayTableCardPointPopupAsync(tableCardIndex, revealedCardPoints);
		await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
	}

	private int PickRandomMagnetExtraRevealIndex(int captureStartIndex)
	{
		var candidates = new List<int>();
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (tableCardIndex >= captureStartIndex)
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.IsFaceUp && tableCard.Card.HasValue)
			{
				candidates.Add(tableCardIndex);
			}
		}

		return candidates.Count == 0 ? -1 : candidates[_rng.RandiRange(0, candidates.Count - 1)];
	}

	private async Task PlayTableCardRevealAnimationAsync(int tableCardIndex, Texture2D faceTexture)
	{
		if (_cardBackTexture == null || tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= cardPositions.Count)
		{
			return;
		}

		var revealCard = new TextureRect
		{
			Name = "TableCardReveal",
			Texture = _cardBackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = cardPositions[tableCardIndex],
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = tableCardIndex * 2 + 100,
		};
		_centerArena.AddChild(revealCard);

		try
		{
			Tween closeTween = CreateTween();
			closeTween.SetTrans(Tween.TransitionType.Sine);
			closeTween.SetEase(Tween.EaseType.In);
			closeTween.TweenProperty(revealCard, "scale", new Vector2(TableCardRevealMinScaleX, 1.0f), TableCardRevealHalfFlipSeconds);
			await ToSignal(closeTween, Tween.SignalName.Finished);

			if (!GodotObject.IsInstanceValid(revealCard))
			{
				return;
			}

			revealCard.Texture = faceTexture;

			Tween openTween = CreateTween();
			openTween.SetTrans(Tween.TransitionType.Sine);
			openTween.SetEase(Tween.EaseType.Out);
			openTween.TweenProperty(revealCard, "scale", Vector2.One, TableCardRevealHalfFlipSeconds);
			await ToSignal(openTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(revealCard))
			{
				revealCard.QueueFree();
			}
		}
	}

	private async Task PlayTableCardPointPopupAsync(int tableCardIndex, int points)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= cardPositions.Count)
		{
			return;
		}

		var pointLabel = new Label
		{
			Name = "TableCardPointPopup",
			Text = $"+{points}",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = new Vector2(CardVisualSize.X, 28.0f),
			ZIndex = TableCardPointPopupZIndex,
		};
		pointLabel.AddThemeFontSizeOverride("font_size", TableCardPointPopupFontSize);
		pointLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.56f));
		pointLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.82f));
		pointLabel.AddThemeConstantOverride("shadow_offset_x", 2);
		pointLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		AddChild(pointLabel);

		pointLabel.GlobalPosition = _centerArena.GlobalPosition
			+ cardPositions[tableCardIndex]
			+ new Vector2(0.0f, -TableCardPointPopupTopOffset);
		Vector2 endPosition = pointLabel.Position + new Vector2(0.0f, -TableCardPointPopupRise);

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(pointLabel, "position", endPosition, TableCardPointPopupSeconds);
			tween.TweenProperty(pointLabel, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), TableCardPointPopupSeconds);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(pointLabel))
			{
				pointLabel.QueueFree();
			}
		}
	}

	private List<CapturedTableCardVisual> BuildCapturedTableCardVisuals(
		int captureStartIndex,
		bool includeQuestionCard,
		bool scoringCardsOnly = false)
	{
		var visuals = new List<CapturedTableCardVisual>();
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return visuals;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		for (int tableCardIndex = Math.Min(_tableCards.Count, cardPositions.Count) - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (scoringCardsOnly && (!IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex) || !tableCard.Card.HasValue))
			{
				continue;
			}

			bool isQuestionCard = tableCard.ItemOverlay.HasValue && tableCard.ItemOverlay.Value.Id == QuestionMarkItemCardId;
			if (!tableCard.Card.HasValue && tableCard.ItemOverlay.HasValue)
			{
				continue;
			}

			if (isQuestionCard && !includeQuestionCard && !tableCard.Card.HasValue)
			{
				continue;
			}

			Texture2D? startTexture = null;
			if (tableCard.Card.HasValue)
			{
				startTexture = tableCard.IsFaceUp || _cardBackTexture == null
					? tableCard.Card.Value.Texture
					: _cardBackTexture;
			}
			else if (tableCard.ItemOverlay.HasValue)
			{
				startTexture = !tableCard.IsFaceUp && _cardBackTexture != null
					? _cardBackTexture
					: tableCard.ItemOverlay.Value.Texture;
			}

			if (startTexture == null)
			{
				continue;
			}

			Texture2D? itemOverlayTexture = null;
			if (tableCard.Card.HasValue && tableCard.ItemOverlay.HasValue && (includeQuestionCard || !isQuestionCard))
			{
				itemOverlayTexture = tableCard.ItemOverlay.Value.Texture;
			}
			else if (tableCard.Card.HasValue && tableCard.Card.Value.HasMagnetEffect && tableCard.IsFaceUp)
			{
				itemOverlayTexture = _magnetCardOverlayTexture;
			}

			visuals.Add(new CapturedTableCardVisual(
				startTexture,
				itemOverlayTexture,
				_centerArena.GlobalPosition + cardPositions[tableCardIndex],
				tableCardIndex * 2));
		}

		return visuals;
	}

	private async Task PlayCapturedCardsToDeckAnimationAsync(
		int playerIndex,
		int captureStartIndex,
		List<CapturedTableCardVisual> capturedCardVisuals,
		int turnSequenceGeneration)
	{
		if (_cardBackTexture == null
			|| playerIndex < 0
			|| playerIndex >= _playerCardPiles.Count
			|| captureStartIndex < 0
			|| captureStartIndex >= _tableCards.Count
			|| capturedCardVisuals.Count == 0)
		{
			return;
		}

		Control targetPile = _playerCardPiles[playerIndex];
		if (!GodotObject.IsInstanceValid(targetPile))
		{
			return;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			_tableCards[tableCardIndex].IsHiddenDuringCapture = true;
		}
		RenderTableCards();

		var flightTasks = new List<Task>();
		Vector2 targetGlobalPosition = GetCardPileCaptureTargetGlobalPosition(targetPile);
		for (int visualIndex = 0; visualIndex < capturedCardVisuals.Count; visualIndex++)
		{
			flightTasks.Add(PlayCapturedCardFlightAsync(
				capturedCardVisuals[visualIndex],
				targetGlobalPosition,
				playerIndex,
				visualIndex * TableCaptureFlightStaggerSeconds));
		}

		await Task.WhenAll(flightTasks);
	}

	private Vector2 GetCardPileCaptureTargetGlobalPosition(Control targetPile)
	{
		return targetPile.GlobalPosition + (CardPileSize - CardVisualSize * TableCaptureFlightEndScale) / 2.0f;
	}

	private async Task PlayCapturedCardFlightAsync(
		CapturedTableCardVisual capturedCardVisual,
		Vector2 targetGlobalPosition,
		int playerIndex,
		float startDelaySeconds)
	{
		var flightCard = new TextureRect
		{
			Name = "CapturedCardFlight",
			Texture = capturedCardVisual.StartTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = PlayedCardFlightZIndex + capturedCardVisual.ZIndex,
			ZAsRelative = false,
		};
		AddChild(flightCard);
		flightCard.GlobalPosition = capturedCardVisual.GlobalPosition;

		TextureRect? itemOverlay = null;
		if (capturedCardVisual.ItemOverlayTexture != null)
		{
			itemOverlay = new TextureRect
			{
				Name = "CapturedCardItemOverlayFlight",
				Texture = capturedCardVisual.ItemOverlayTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore,
				Position = Vector2.Zero,
				Size = CardVisualSize,
				PivotOffset = CardVisualSize / 2.0f,
				ZIndex = 1,
			};
			flightCard.AddChild(itemOverlay);
		}

		try
		{
			if (startDelaySeconds > 0.0f)
			{
				await ToSignal(GetTree().CreateTimer(startDelaySeconds), SceneTreeTimer.SignalName.Timeout);
				if (!GodotObject.IsInstanceValid(flightCard))
				{
					return;
				}
			}

			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateCapturedCardFlight(
					flightCard,
					itemOverlay,
					capturedCardVisual.StartTexture,
					_cardBackTexture!,
					capturedCardVisual.GlobalPosition,
					targetGlobalPosition,
					GetCardRotationDegreesForPlayer(playerIndex),
					progress)),
				0.0f,
				1.0f,
				TableCaptureFlightDuration);

			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(flightCard))
			{
				flightCard.QueueFree();
			}
		}
	}

	private static void UpdateCapturedCardFlight(
		TextureRect flightCard,
		TextureRect? itemOverlay,
		Texture2D startTexture,
		Texture2D cardBackTexture,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float targetRotationDegrees,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * TableCaptureFlightArcHeight;
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, clampedProgress) + new Vector2(0.0f, arcY);
		flightCard.RotationDegrees = Mathf.Lerp(0.0f, targetRotationDegrees, clampedProgress);

		bool showingBack = clampedProgress >= 0.5f;
		flightCard.Texture = showingBack ? cardBackTexture : startTexture;
		if (itemOverlay != null)
		{
			itemOverlay.Visible = !showingBack;
		}

		float halfProgress = showingBack
			? (clampedProgress - 0.5f) * 2.0f
			: clampedProgress * 2.0f;
		float scaleX = showingBack
			? Mathf.Lerp(PlayedCardFlipMinScaleX, 1.0f, halfProgress)
			: Mathf.Lerp(1.0f, PlayedCardFlipMinScaleX, halfProgress);
		float scale = Mathf.Lerp(1.0f, TableCaptureFlightEndScale, clampedProgress);
		flightCard.Scale = new Vector2(scaleX * scale, scale);
	}

	private int AdvanceQuestionCardRefreshTurnCounter()
	{
		if (HasTableSpecialCard())
		{
			return -1;
		}

		_completedTurnsSinceQuestionCardRefresh++;
		if (_completedTurnsSinceQuestionCardRefresh < GetQuestionCardRefreshTurnCount())
		{
			return -1;
		}

		_completedTurnsSinceQuestionCardRefresh = 0;
		return PlaceQuestionCardOnNextTableSlot();
	}

	private int PlaceQuestionCardOnNextTableSlot()
	{
		if (HasTableSpecialCard())
		{
			return -1;
		}

		ItemCard? itemOverlay = CreateItemCard(QuestionMarkItemCardId);
		if (!itemOverlay.HasValue)
		{
			return -1;
		}

		_tableCards.Add(new TableCard(null, true, itemOverlay));
		return _tableCards.Count - 1;
	}

	private int GetQuestionCardRefreshTurnCount()
	{
		return Math.Max(1, player_count * QuestionCardRefreshCooldownRounds);
	}

	private bool HasTableSpecialCard()
	{
		foreach (TableCard tableCard in _tableCards)
		{
			if (tableCard.ItemOverlay.HasValue)
			{
				return true;
			}
		}

		return false;
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

	private int GetCapturedTablePoints(int captureStartIndex)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return 0;
		}

		int collectedPoints = 0;
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (!IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex))
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.Card.HasValue)
			{
				collectedPoints += GetCardScoreValue(tableCard.Card.Value);
			}
		}

		return collectedPoints;
	}

	private TableSpecialCardTrigger? GetCapturedSpecialCardTrigger(int captureStartIndex)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return null;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			if (tableCardIndex >= cardPositions.Count)
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.ItemOverlay.HasValue)
			{
				continue;
			}

			if (tableCard.ItemOverlay.Value.Id == QuestionMarkItemCardId)
			{
				continue;
			}

			return new TableSpecialCardTrigger(
				tableCardIndex,
				tableCard.ItemOverlay.Value.Texture,
				_centerArena.GlobalPosition + cardPositions[tableCardIndex] + GetItemCardVisualPosition(tableCard.ItemOverlay.Value.Id),
				tableCardIndex * 2 + 1);
		}

		return null;
	}

	private TableSpecialCardTrigger? GetTableSpecialCardTrigger(int tableCardIndex)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return null;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= cardPositions.Count)
		{
			return null;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.ItemOverlay.HasValue)
		{
			return null;
		}

		ItemCard itemOverlay = tableCard.ItemOverlay.Value;
		if (itemOverlay.Id == QuestionMarkItemCardId)
		{
			return null;
		}

		return new TableSpecialCardTrigger(
			tableCardIndex,
			itemOverlay.Texture,
			_centerArena.GlobalPosition + cardPositions[tableCardIndex] + GetItemCardVisualPosition(itemOverlay.Id),
			tableCardIndex * 2 + 1);
	}

	private async Task PlayTableSpecialCardTriggerEffectAsync(TableSpecialCardTrigger trigger)
	{
		if (trigger.TableCardIndex < 0 || trigger.TableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[trigger.TableCardIndex];
		bool wasHidden = tableCard.IsHiddenDuringCapture;
		tableCard.IsHiddenDuringCapture = true;
		RenderTableCards();
		try
		{
			await PlaySpecialCardTriggerEffectAsync(trigger.Texture, trigger.GlobalPosition, trigger.ZIndex);
		}
		finally
		{
			if (trigger.TableCardIndex >= 0 && trigger.TableCardIndex < _tableCards.Count)
			{
				_tableCards[trigger.TableCardIndex].IsHiddenDuringCapture = wasHidden;
				RenderTableCards();
			}
		}
	}

	private int GetCapturedTableScorePlayerIndex(int captureStartIndex, int fallbackPlayerIndex)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return fallbackPlayerIndex;
		}

		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.ItemOverlay.HasValue || tableCard.ItemOverlay.Value.Id != BombItemCardId)
			{
				continue;
			}

			if (tableCard.ItemOwnerPlayerIndex >= 0 && tableCard.ItemOwnerPlayerIndex < player_count)
			{
				return tableCard.ItemOwnerPlayerIndex;
			}
		}

		return fallbackPlayerIndex;
	}

	private bool IsCapturedTableCardScoringIndex(int tableCardIndex, int captureStartIndex)
	{
		return tableCardIndex > captureStartIndex && tableCardIndex < _tableCards.Count - 1;
	}

	private int CaptureTableCards(int playerIndex, int captureStartIndex, bool reserveReplacementCard = false, bool awardScore = true)
	{
		return CaptureTableCards(playerIndex, captureStartIndex, reserveReplacementCard, awardScore, out _);
	}

	private int CaptureTableCards(int playerIndex, int captureStartIndex, out QuestionCardRewardAnimation? questionRewardAnimation)
	{
		return CaptureTableCards(playerIndex, captureStartIndex, false, true, out questionRewardAnimation);
	}

	private int CaptureTableCards(
		int playerIndex,
		int captureStartIndex,
		bool reserveReplacementCard,
		bool awardScore,
		out QuestionCardRewardAnimation? questionRewardAnimation)
	{
		questionRewardAnimation = null;
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return 0;
		}

		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return 0;
		}

		int captureCount = _tableCards.Count - captureStartIndex;
		int collectedCardCount = 0;
		int collectedPoints = awardScore ? GetCapturedTablePoints(captureStartIndex) : 0;
		int scorePlayerIndex = awardScore ? GetCapturedTableScorePlayerIndex(captureStartIndex, playerIndex) : playerIndex;
		bool capturedSpecialCard = false;
		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.Card.HasValue)
			{
				AddCardToPlayerDeck(playerIndex, tableCard.Card.Value);
				collectedCardCount++;
			}

			if (tableCard.ItemOverlay.HasValue && tableCard.ItemOverlay.Value.Id == QuestionMarkItemCardId)
			{
				QuestionCardRewardGrant? rewardGrant = GrantQuestionCardReward(playerIndex, reserveReplacementCard);
				if (rewardGrant.HasValue && playerIndex == MainPlayerIndex && tableCardIndex < cardPositions.Count)
				{
					questionRewardAnimation = new QuestionCardRewardAnimation(
						rewardGrant.Value.ItemCard,
						rewardGrant.Value.ItemHandIndex,
						_centerArena.GlobalPosition + cardPositions[tableCardIndex],
						rewardGrant.Value.RequiresMagnetAttachment);
				}
			}

			if (tableCard.ItemOverlay.HasValue)
			{
				capturedSpecialCard = true;
			}
		}

		_tableCards.RemoveRange(captureStartIndex, captureCount);
		if (capturedSpecialCard)
		{
			_completedTurnsSinceQuestionCardRefresh = 0;
		}

		if (awardScore)
		{
			AddPlayerScore(scorePlayerIndex, collectedPoints);
		}

		CheckForWinner();
		return collectedCardCount;
	}

	private QuestionCardRewardGrant? GrantQuestionCardReward(int playerIndex, bool reserveReplacementCard)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count || QuestionRewardItemCardIds.Length == 0)
		{
			return null;
		}

		string itemCardId = QuestionRewardItemCardIds[_rng.RandiRange(0, QuestionRewardItemCardIds.Length - 1)];
		ItemCard? itemCard = CreateItemCard(itemCardId);
		if (!itemCard.HasValue)
		{
			return null;
		}

		if (itemCardId == MagnetItemCardId)
		{
			_pendingMagnetAttachmentRewardPlayers.Enqueue(playerIndex);
			return new QuestionCardRewardGrant(itemCard.Value, -1, true);
		}

		if (!HasRoomForItemCard(playerIndex, reserveReplacementCard ? 1 : 0))
		{
			return null;
		}

		int itemHandIndex = _playerItemHands[playerIndex].Count;
		_playerItemHands[playerIndex].Add(itemCard.Value);
		return new QuestionCardRewardGrant(itemCard.Value, itemHandIndex);
	}

	private async Task AdvanceTurnAfterPlayedResultAsync(int playerIndex, PlayCardResult result)
	{
		_currentTurnPlayerIndex = result.Captured ? playerIndex : GetNextTurnPlayerIndex(playerIndex);
		if (!result.Captured)
		{
			int refreshedQuestionCardIndex = AdvanceQuestionCardRefreshTurnCounter();
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

	private void DeclareWinner(int playerIndex)
	{
		_winnerPlayerIndex = playerIndex;
		_turnSequenceGeneration++;
		_isResolvingTurnSequence = false;
		_isProcessingCardPlay = false;
		_currentTurnPlayerIndex = playerIndex;

		_winnerLabel.Text = $"玩家 {playerIndex + 1} 获胜";
		_winnerLabel.Visible = true;
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
