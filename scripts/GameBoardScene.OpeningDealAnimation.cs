using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayOpeningIntroThenDealSequenceAsync(int turnSequenceGeneration)
	{
		await PlayOpeningIntroAnimationAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		await PlayOpeningDealAnimationAsync(turnSequenceGeneration);
	}

	private async Task PlayOpeningIntroAnimationAsync(int turnSequenceGeneration)
	{
		if (!IsInsideTree())
		{
			return;
		}

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		Vector2 viewportSize = GetViewportRect().Size;
		(string leftText, string rightText) = SplitOpeningIntroText(Localize("GAME_START"));
		int fontSize = Mathf.RoundToInt(Mathf.Clamp(Mathf.Min(viewportSize.X, viewportSize.Y) * 0.085f, 34.0f, 58.0f));
		float labelHeight = Mathf.Clamp(fontSize * 1.55f, 58.0f, 92.0f);
		float pieceWidth = Mathf.Clamp(fontSize * Math.Max(2.2f, Math.Max(leftText.Length, rightText.Length) * 0.68f), 118.0f, viewportSize.X * 0.42f);
		Vector2 labelSize = new(pieceWidth, labelHeight);
		float centerX = viewportSize.X * 0.5f;
		float labelY = viewportSize.Y * 0.5f - labelHeight * 0.5f;

		var introRoot = new Control
		{
			Name = "OpeningIntroAnimation",
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = OpeningDealZIndex + 1000,
			ZAsRelative = false,
		};
		introRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(introRoot);

		Label leftLabel = CreateOpeningIntroTextLabel("OpeningIntroLeftText", leftText, fontSize, HorizontalAlignment.Right, labelSize);
		Label rightLabel = CreateOpeningIntroTextLabel("OpeningIntroRightText", rightText, fontSize, HorizontalAlignment.Left, labelSize);
		introRoot.AddChild(leftLabel);
		introRoot.AddChild(rightLabel);

		Vector2 leftStart = new(-pieceWidth - 64.0f, labelY);
		Vector2 leftEnd = new(centerX - pieceWidth, labelY);
		Vector2 rightStart = new(viewportSize.X + 64.0f, labelY);
		Vector2 rightEnd = new(centerX, labelY);
		leftLabel.Position = leftStart;
		rightLabel.Position = rightStart;

		try
		{
			Tween flyInTween = CreateTween();
			flyInTween.SetParallel(true);
			flyInTween.SetTrans(Tween.TransitionType.Cubic);
			flyInTween.SetEase(Tween.EaseType.Out);
			flyInTween.TweenProperty(leftLabel, "position", leftEnd, OpeningIntroFlyInSeconds);
			flyInTween.TweenProperty(rightLabel, "position", rightEnd, OpeningIntroFlyInSeconds);
			flyInTween.TweenProperty(leftLabel, "modulate:a", 1.0f, OpeningIntroFlyInSeconds * 0.72f);
			flyInTween.TweenProperty(rightLabel, "modulate:a", 1.0f, OpeningIntroFlyInSeconds * 0.72f);
			await ToSignal(flyInTween, Tween.SignalName.Finished);

			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			await ToSignal(GetTree().CreateTimer(OpeningIntroHoldSeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			Tween fadeOutTween = CreateTween();
			fadeOutTween.SetParallel(true);
			fadeOutTween.SetTrans(Tween.TransitionType.Sine);
			fadeOutTween.SetEase(Tween.EaseType.In);
			fadeOutTween.TweenProperty(leftLabel, "modulate:a", 0.0f, OpeningIntroFadeOutSeconds);
			fadeOutTween.TweenProperty(rightLabel, "modulate:a", 0.0f, OpeningIntroFadeOutSeconds);
			await ToSignal(fadeOutTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(introRoot))
			{
				introRoot.QueueFree();
			}
		}
	}

	private static (string Left, string Right) SplitOpeningIntroText(string introText)
	{
		string text = introText.Trim();
		if (text.Length <= 1)
		{
			return (text, string.Empty);
		}

		int spaceIndex = text.IndexOf(' ');
		if (spaceIndex > 0 && spaceIndex < text.Length - 1)
		{
			return (text[..spaceIndex], text[spaceIndex..]);
		}

		int splitIndex = Math.Max(1, text.Length / 2);
		return (text[..splitIndex], text[splitIndex..]);
	}

	private static Label CreateOpeningIntroTextLabel(string nodeName, string text, int fontSize, HorizontalAlignment horizontalAlignment, Vector2 size)
	{
		var label = new Label
		{
			Name = nodeName,
			Text = text,
			HorizontalAlignment = horizontalAlignment,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = size,
			PivotOffset = size / 2.0f,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", new Color(0.96f, 1.0f, 0.98f));
		label.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.12f, 0.16f, 0.92f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.90f));
		label.AddThemeConstantOverride("outline_size", 5);
		label.AddThemeConstantOverride("shadow_offset_x", 0);
		label.AddThemeConstantOverride("shadow_offset_y", 4);
		return label;
	}

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
				float targetVisualScale = GetHandDisplayCardVisualScale(playerIndex, handIndex);
				await PlayOpeningDealCardFlightAsync(startGlobalPosition, targetGlobalPosition, playerIndex, dealNumber, targetVisualScale);
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
		int dealNumber,
		float targetVisualScale)
	{
		if (_cardBackTexture == null || !IsInsideTree())
		{
			return;
		}

		float clampedTargetVisualScale = Mathf.Max(0.01f, targetVisualScale);
		Vector2 targetVisualSize = CardVisualSize * clampedTargetVisualScale;
		Vector2 targetVisualBasePosition = GetCardVisualBasePosition(clampedTargetVisualScale);
		float startRotationDegrees = -8.0f + dealNumber % 5 * 4.0f;
		var cardRoot = new Control
		{
			Name = "OpeningDealCardFlight",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = OpeningDealZIndex + dealNumber,
			ZAsRelative = false,
		};
		var cardVisual = new TextureRect
		{
			Name = "OpeningDealCardVisual",
			Texture = _cardBackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = targetVisualBasePosition,
			Size = targetVisualSize,
			PivotOffset = targetVisualSize / 2.0f,
			RotationDegrees = startRotationDegrees,
		};
		MarkAnimatedCardBack(cardVisual);
		cardRoot.AddChild(cardVisual);
		AddChild(cardRoot);
		cardRoot.GlobalPosition = startGlobalPosition;

		try
		{
			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateOpeningDealSlotCardFlight(
					cardRoot,
					cardVisual,
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
			if (GodotObject.IsInstanceValid(cardRoot))
			{
				cardRoot.QueueFree();
			}
		}
	}

	private async Task PlayOpeningDealPileCardFlightAsync(Vector2 startGlobalPosition, Vector2 targetGlobalPosition, int playerIndex, int dealIndex, float flightSeconds)
	{
		if (_cardBackTexture == null || !IsInsideTree())
		{
			return;
		}

		float startRotationDegrees = -6.0f + dealIndex % 4 * 4.0f;
		var cardRoot = new Control
		{
			Name = "OpeningPileCardFlight",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = OpeningDealZIndex + 200 + dealIndex,
			ZAsRelative = false,
		};
		var cardVisual = new TextureRect
		{
			Name = "OpeningPileCardVisual",
			Texture = _cardBackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			RotationDegrees = startRotationDegrees,
		};
		MarkAnimatedCardBack(cardVisual);
		cardRoot.AddChild(cardVisual);
		AddChild(cardRoot);
		cardRoot.GlobalPosition = startGlobalPosition;

		try
		{
			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateOpeningDealSlotCardFlight(
					cardRoot,
					cardVisual,
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
			if (GodotObject.IsInstanceValid(cardRoot))
			{
				cardRoot.QueueFree();
			}
		}
	}

	private static void UpdateOpeningDealSlotCardFlight(
		Control cardRoot,
		TextureRect cardVisual,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(cardRoot) || !GodotObject.IsInstanceValid(cardVisual))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * 34.0f;
		float pulse = Mathf.Sin(clampedProgress * Mathf.Pi);
		cardRoot.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		cardVisual.RotationDegrees = Mathf.Lerp(startRotationDegrees, targetRotationDegrees, easedProgress) + pulse * 8.0f;
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
