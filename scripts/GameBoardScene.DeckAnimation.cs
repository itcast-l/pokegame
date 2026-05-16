using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const string DeckShuffleFramePattern = "res://art/cards/deck_shuffle/overhand_drop/game_frames/frame-{0:000}.png";
	private const int DeckShuffleFrameCount = 6;
	private const int DeckShuffleRepeatCount = 3;
	private const float DeckShuffleFrameSeconds = 0.055f;
	private const float PlayedCardFlightDuration = 0.34f;
	private const float PlayedCardFlipMinScaleX = 0.06f;
	private const int PlayedCardFlightZIndex = 2000;
	private static readonly Vector2 DeckShuffleVisualSize = new(96.0f, 192.0f);

	private async Task PlayCardToTableAnimationAsync(
		int playerIndex,
		PlayingCard playedCard,
		Vector2 startGlobalPosition,
		int tableCardIndex,
		bool tableCardIsFaceUp)
	{
		if (!IsInsideTree() || tableCardIndex < 0)
		{
			return;
		}

		List<Vector2> tableCardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= tableCardPositions.Count)
		{
			return;
		}

		Texture2D? startTexture = GetVisibleHandCardTexture(playerIndex, playedCard);
		if (startTexture == null)
		{
			return;
		}

		Vector2 targetGlobalPosition = _centerArena.GlobalPosition + tableCardPositions[tableCardIndex];
		bool flipToCardBack = playerIndex == MainPlayerIndex && !tableCardIsFaceUp && _cardBackTexture != null;
		Texture2D targetTexture = flipToCardBack ? _cardBackTexture! : startTexture;
		float startRotationDegrees = GetCardRotationDegreesForPlayer(playerIndex);
		var flightCard = new TextureRect
		{
			Name = "PlayedCardFlight",
			Texture = startTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			RotationDegrees = startRotationDegrees,
			ZIndex = PlayedCardFlightZIndex,
			ZAsRelative = false,
		};
		AddChild(flightCard);
		flightCard.GlobalPosition = startGlobalPosition;

		try
		{
			UpdateCardToTableAnimation(
				flightCard,
				startTexture,
				targetTexture,
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees,
				flipToCardBack,
				0.0f);

			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateCardToTableAnimation(
					flightCard,
					startTexture,
					targetTexture,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					flipToCardBack,
					progress)),
				0.0f,
				1.0f,
				PlayedCardFlightDuration);

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

	private Texture2D? GetVisibleHandCardTexture(int playerIndex, PlayingCard playedCard)
	{
		if (playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null)
		{
			return _cardBackTexture;
		}

		return playedCard.Texture;
	}

	private static void UpdateCardToTableAnimation(
		TextureRect flightCard,
		Texture2D startTexture,
		Texture2D targetTexture,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		bool flipToCardBack,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, clampedProgress);
		flightCard.RotationDegrees = Mathf.Lerp(startRotationDegrees, 0.0f, clampedProgress);

		if (!flipToCardBack)
		{
			flightCard.Texture = startTexture;
			flightCard.Scale = Vector2.One;
			return;
		}

		bool showingBack = clampedProgress >= 0.5f;
		flightCard.Texture = showingBack ? targetTexture : startTexture;
		float halfProgress = showingBack
			? (clampedProgress - 0.5f) * 2.0f
			: clampedProgress * 2.0f;
		float scaleX = showingBack
			? Mathf.Lerp(PlayedCardFlipMinScaleX, 1.0f, halfProgress)
			: Mathf.Lerp(1.0f, PlayedCardFlipMinScaleX, halfProgress);
		flightCard.Scale = new Vector2(scaleX, 1.0f);
	}

	private async Task PlayPhysicalDeckShuffleAsync(int playerIndex)
	{
		if (playerIndex < 0 || _playerCardPiles.Count <= playerIndex)
		{
			return;
		}

		Control pile = _playerCardPiles[playerIndex];
		if (!GodotObject.IsInstanceValid(pile) || !pile.IsInsideTree())
		{
			return;
		}

		List<TextureRect> originalCards = GetVisiblePileCards(pile);
		if (originalCards.Count == 0)
		{
			return;
		}

		List<Texture2D> shuffleFrames = LoadDeckShuffleFrames();
		if (shuffleFrames.Count == 0)
		{
			return;
		}

		Label? countLabel = pile.GetNodeOrNull<Label>("CardCountLabel");
		bool countLabelVisible = countLabel != null && countLabel.Visible;
		SetOriginalPileVisualsVisible(originalCards, countLabel, false);

		var shuffleSprite = new TextureRect
		{
			Name = "DeckShuffleAnimation",
			Texture = shuffleFrames[0],
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardPileSize - DeckShuffleVisualSize) / 2.0f,
			Size = DeckShuffleVisualSize,
			PivotOffset = DeckShuffleVisualSize / 2.0f,
			RotationDegrees = GetCardRotationDegreesForPlayer(playerIndex),
			ZIndex = VisiblePileCards + 20,
		};
		pile.AddChild(shuffleSprite);

		try
		{
			await PlayDeckShuffleFramesAsync(shuffleSprite, shuffleFrames);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(shuffleSprite))
			{
				shuffleSprite.QueueFree();
			}

			SetOriginalPileVisualsVisible(originalCards, countLabel, true);
			if (countLabel != null)
			{
				countLabel.Visible = countLabelVisible;
			}
		}
	}

	private static List<Texture2D> LoadDeckShuffleFrames()
	{
		var frames = new List<Texture2D>();
		for (int frameIndex = 1; frameIndex <= DeckShuffleFrameCount; frameIndex++)
		{
			string path = string.Format(DeckShuffleFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing deck shuffle frame: {path}");
				continue;
			}

			frames.Add(texture);
		}

		return frames;
	}

	private static List<TextureRect> GetVisiblePileCards(Control pile)
	{
		var cards = new List<TextureRect>();
		foreach (Node child in pile.GetChildren())
		{
			if (child is TextureRect textureRect && textureRect.HasMeta(CardBackAnimatedMeta))
			{
				cards.Add(textureRect);
			}
		}

		return cards;
	}

	private static void SetOriginalPileVisualsVisible(IReadOnlyList<TextureRect> originalCards, Label? countLabel, bool visible)
	{
		foreach (TextureRect card in originalCards)
		{
			if (GodotObject.IsInstanceValid(card))
			{
				card.Visible = visible;
			}
		}

		if (countLabel != null)
		{
			countLabel.Visible = visible;
		}
	}

	private async Task PlayDeckShuffleFramesAsync(TextureRect shuffleSprite, IReadOnlyList<Texture2D> shuffleFrames)
	{
		for (int repeatIndex = 0; repeatIndex < DeckShuffleRepeatCount; repeatIndex++)
		{
			foreach (Texture2D frame in shuffleFrames)
			{
				if (!GodotObject.IsInstanceValid(shuffleSprite))
				{
					return;
				}

				shuffleSprite.Texture = frame;
				await ToSignal(GetTree().CreateTimer(DeckShuffleFrameSeconds), SceneTreeTimer.SignalName.Timeout);
			}
		}
	}
}
