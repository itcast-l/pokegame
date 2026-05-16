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

			List<CapturedTableCardVisual> capturedCardVisuals = BuildCapturedTableCardVisuals(captureStartIndex);
			int collectedPoints = GetCapturedTablePoints(captureStartIndex);
			await PlayScoreFillCollectionAsync(
				playerIndex,
				capturedCardVisuals,
				turnSequenceGeneration,
				() => AddPlayerScore(playerIndex, collectedPoints));
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			await PlayCapturedCardsToDeckAnimationAsync(playerIndex, captureStartIndex, capturedCardVisuals, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			int collectedCardCount = CaptureTableCards(playerIndex, captureStartIndex, true, false);
			if (IsGameOver())
			{
				return new PlayCardResult(true, true, collectedCardCount > 0);
			}

			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
			DrawReplacementCard(playerIndex);
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
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
		if (result.DeckCollected)
		{
			await PlayPhysicalDeckShuffleAsync(playerIndex);
		}

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

	private async Task RevealCapturedBaseCardsFromBottomAsync(int captureStartIndex, int turnSequenceGeneration)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return;
		}

		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.Card.HasValue || tableCard.IsFaceUp)
			{
				continue;
			}

			await PlayTableCardRevealAnimationAsync(tableCardIndex, tableCard.Card.Value.Texture);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			tableCard.IsFaceUp = true;
			RenderTableCards();
			await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}
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

	private List<CapturedTableCardVisual> BuildCapturedTableCardVisuals(int captureStartIndex)
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
			Texture2D? startTexture = null;
			if (tableCard.Card.HasValue)
			{
				startTexture = tableCard.IsFaceUp || _cardBackTexture == null
					? tableCard.Card.Value.Texture
					: _cardBackTexture;
			}
			else if (tableCard.ItemOverlay.HasValue)
			{
				startTexture = tableCard.ItemOverlay.Value.Texture;
			}

			if (startTexture == null)
			{
				continue;
			}

			Texture2D? itemOverlayTexture = tableCard.Card.HasValue && tableCard.ItemOverlay.HasValue
				? tableCard.ItemOverlay.Value.Texture
				: null;
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

	private void AdvanceQuestionCardRefreshTurnCounter()
	{
		if (HasTableSpecialCard())
		{
			return;
		}

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
		if (HasTableSpecialCard())
		{
			return;
		}

		ItemCard? itemOverlay = CreateItemCard(QuestionMarkItemCardId);
		if (!itemOverlay.HasValue)
		{
			return;
		}

		_tableCards.Add(new TableCard(null, true, itemOverlay));
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
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.Card.HasValue)
			{
				collectedPoints += GetCardScoreValue(tableCard.Card.Value);
			}
		}

		return collectedPoints;
	}

	private int CaptureTableCards(int playerIndex, int captureStartIndex, bool reserveReplacementCard = false, bool awardScore = true)
	{
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
		int collectedPoints = 0;
		bool capturedSpecialCard = false;
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.Card.HasValue)
			{
				_playerDecks[playerIndex].Add(tableCard.Card.Value);
				collectedCardCount++;
				collectedPoints += GetCardScoreValue(tableCard.Card.Value);
			}

			if (tableCard.ItemOverlay.HasValue && tableCard.ItemOverlay.Value.Id == QuestionMarkItemCardId)
			{
				GrantQuestionCardReward(playerIndex, reserveReplacementCard);
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
			AddPlayerScore(playerIndex, collectedPoints);
		}

		CheckForWinner();
		return collectedCardCount;
	}

	private void GrantQuestionCardReward(int playerIndex, bool reserveReplacementCard)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count || QuestionRewardItemCardIds.Length == 0)
		{
			return;
		}

		if (!HasRoomForItemCard(playerIndex, reserveReplacementCard ? 1 : 0))
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
