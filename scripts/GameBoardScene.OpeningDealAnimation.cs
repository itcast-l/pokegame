using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayOpeningDealAnimationAsync(int turnSequenceGeneration)
	{
		if (!IsInsideTree())
		{
			_isOpeningDealAnimationPlaying = false;
			return;
		}

		try
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			Vector2 dealStartGlobalPosition = GetOpeningDealStartGlobalPosition();
			Control? centerDeck = CreateOpeningCenterDeckVisual(GetOpeningTotalCardCount());
			try
			{
				await ToSignal(GetTree().CreateTimer(0.18f), SceneTreeTimer.SignalName.Timeout);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				await PlayOpeningDealCardsToHandsAsync(dealStartGlobalPosition, turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				await PlayOpeningDealPilesAsync(dealStartGlobalPosition, turnSequenceGeneration);
			}
			finally
			{
				if (GodotObject.IsInstanceValid(centerDeck))
				{
					centerDeck.QueueFree();
				}
			}
		}
		finally
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				ShowAllOpeningHandCards();
				ShowAllOpeningPileCards();
				_isOpeningDealAnimationPlaying = false;
				RenderPlayerCardPiles();
				RenderPlayerHands();
			}
		}
	}

	private Control? CreateOpeningCenterDeckVisual(int cardCount)
	{
		if (_cardBackTexture == null || !IsInsideTree() || cardCount <= 0)
		{
			return null;
		}

		var deckRoot = new Control
		{
			Name = "OpeningCenterDeck",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardPileSize,
			PivotOffset = CardPileSize / 2.0f,
			ZIndex = OpeningDealZIndex - 10,
			ZAsRelative = false,
		};
		AddChild(deckRoot);
		deckRoot.GlobalPosition = GetOpeningDealStartGlobalPosition() + CardVisualSize / 2.0f - CardPileSize / 2.0f;

		var cardBack = new TextureRect
		{
			Name = "OpeningCenterDeckCard",
			Texture = _cardBackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardPileSize - CardVisualSize) / 2.0f,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
		};
		MarkAnimatedCardBack(cardBack);
		deckRoot.AddChild(cardBack);

		return deckRoot;
	}

	private async Task PlayOpeningDealCardsToHandsAsync(Vector2 startGlobalPosition, int turnSequenceGeneration)
	{
		if (_cardBackTexture == null)
		{
			return;
		}

		int maxHandCards = 0;
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			maxHandCards = Math.Max(maxHandCards, _playerHands[playerIndex].Count);
		}

		int dealNumber = 0;
		for (int handIndex = 0; handIndex < maxHandCards; handIndex++)
		{
			for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
			{
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				if (handIndex >= _playerHands[playerIndex].Count)
				{
					continue;
				}

				Vector2 targetGlobalPosition = GetOpeningHandCardGlobalPosition(playerIndex, handIndex);
				await PlayOpeningDealCardFlightAsync(startGlobalPosition, targetGlobalPosition, playerIndex, dealNumber);
				ShowOpeningHandCard(playerIndex, handIndex + 1);
				dealNumber++;
			}
		}
	}

	private async Task PlayOpeningDealPilesAsync(Vector2 startGlobalPosition, int turnSequenceGeneration)
	{
		if (_cardBackTexture == null)
		{
			return;
		}

		List<int> activePlayerIndexes = BuildOpeningPileDealPlayerIndexes();
		if (activePlayerIndexes.Count == 0)
		{
			return;
		}

		int visualDealCount = Math.Max(activePlayerIndexes.Count, Mathf.CeilToInt(OpeningDealPilesTotalSeconds / OpeningDealPileCardFlightSeconds));
		float dealIntervalSeconds = OpeningDealPilesTotalSeconds / visualDealCount;
		float flightSeconds = Math.Min(OpeningDealPileCardFlightSeconds, dealIntervalSeconds);
		int[] visiblePileDealCounts = new int[MaxPlayers];
		for (int dealIndex = 0; dealIndex < visualDealCount; dealIndex++)
		{
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			int playerIndex = activePlayerIndexes[dealIndex % activePlayerIndexes.Count];
			Vector2 targetGlobalPosition = GetOpeningPileGlobalPosition(playerIndex);
			await PlayOpeningDealPileCardFlightAsync(startGlobalPosition, targetGlobalPosition, playerIndex, dealIndex, flightSeconds);
			visiblePileDealCounts[playerIndex]++;
			int targetVisibleCardCount = visiblePileDealCounts[playerIndex];
			ShowOpeningPileCards(playerIndex, Math.Max(1, targetVisibleCardCount));
			if (dealIndex < visualDealCount - 1 && dealIntervalSeconds > flightSeconds)
			{
				await ToSignal(GetTree().CreateTimer(dealIntervalSeconds - flightSeconds), SceneTreeTimer.SignalName.Timeout);
			}
		}

		ShowAllOpeningPileCards();
		RenderPlayerCardPiles();
	}

	private async Task PlayOpeningDealCardFlightAsync(
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		int playerIndex,
		int dealNumber)
	{
		if (_cardBackTexture == null || !IsInsideTree())
		{
			return;
		}

		var card = new TextureRect
		{
			Name = "OpeningDealCardFlight",
			Texture = _cardBackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			RotationDegrees = -8.0f + dealNumber % 5 * 4.0f,
			ZIndex = OpeningDealZIndex + dealNumber,
			ZAsRelative = false,
			Scale = Vector2.One * 0.82f,
		};
		float startRotationDegrees = card.RotationDegrees;
		MarkAnimatedCardBack(card);
		AddChild(card);
		card.GlobalPosition = startGlobalPosition;

		try
		{
			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateOpeningDealCardFlight(
					card,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					GetCardRotationDegreesForPlayer(playerIndex),
					progress)),
				0.0f,
				1.0f,
				OpeningDealCardFlightSeconds);
			await ToSignal(tween, Tween.SignalName.Finished);
			await ToSignal(GetTree().CreateTimer(OpeningDealCardStaggerSeconds), SceneTreeTimer.SignalName.Timeout);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(card))
			{
				card.QueueFree();
			}
		}
	}

	private async Task PlayOpeningDealPileCardFlightAsync(Vector2 startGlobalPosition, Vector2 targetGlobalPosition, int playerIndex, int dealIndex, float flightSeconds)
	{
		if (_cardBackTexture == null || !IsInsideTree())
		{
			return;
		}

		var card = new TextureRect
		{
			Name = "OpeningPileCardFlight",
			Texture = _cardBackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			RotationDegrees = -6.0f + dealIndex % 4 * 4.0f,
			ZIndex = OpeningDealZIndex + 200 + dealIndex,
			ZAsRelative = false,
			Scale = Vector2.One * 0.82f,
		};
		float startRotationDegrees = card.RotationDegrees;
		MarkAnimatedCardBack(card);
		AddChild(card);
		card.GlobalPosition = startGlobalPosition;

		try
		{
			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateOpeningDealCardFlight(
					card,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					GetCardPileRotationDegreesForPlayer(playerIndex),
					progress)),
				0.0f,
				1.0f,
				flightSeconds);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(card))
			{
				card.QueueFree();
			}
		}
	}

	private static void UpdateOpeningDealCardFlight(
		TextureRect card,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(card))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * 34.0f;
		float pulse = Mathf.Sin(clampedProgress * Mathf.Pi);
		card.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		card.RotationDegrees = Mathf.Lerp(startRotationDegrees, targetRotationDegrees, easedProgress) + pulse * 8.0f;
		card.Scale = Vector2.One * Mathf.Lerp(0.82f, 1.0f, easedProgress);
	}

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
		return handArea.GlobalPosition + firstCardPosition + handIndex * cardOffset;
	}

	private Vector2 GetOpeningPileGlobalPosition(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerCardPiles.Count)
		{
			return GetOpeningDealStartGlobalPosition();
		}

		return _playerCardPiles[playerIndex].GlobalPosition;
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
