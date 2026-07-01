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

		PlayGameSfx(GameSfx.OpeningIntro, -1.5f, 0.98f, 1.02f);
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

		bool shouldResolveRunStartUpgrade = false;
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
				QueueAdventureRunStartUpgradeIfNeeded();
				shouldResolveRunStartUpgrade = _pendingAdventureUpgradeTriggers.Count > 0;
			}
		}

		if (shouldResolveRunStartUpgrade)
		{
			await ResolvePendingAdventureUpgradeTriggersAsync(turnSequenceGeneration);
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

}
