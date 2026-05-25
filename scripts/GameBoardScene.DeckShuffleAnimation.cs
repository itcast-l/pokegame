using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
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

		if (DeckShuffleFrames.Length == 0)
		{
			return;
		}

		Label? countLabel = pile.GetNodeOrNull<Label>("CardCountLabel");
		bool countLabelVisible = countLabel != null && countLabel.Visible;
		SetOriginalPileVisualsVisible(originalCards, countLabel, false);

		Vector2 shuffleBasePosition = (CardPileSize - DeckShuffleVisualSize) / 2.0f;
		float shuffleBaseRotation = GetCardRotationDegreesForPlayer(playerIndex);
		var shuffleRoot = new Control
		{
			Name = "DeckShuffleAnimation",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = shuffleBasePosition,
			Size = DeckShuffleVisualSize,
			PivotOffset = DeckShuffleVisualSize / 2.0f,
			RotationDegrees = shuffleBaseRotation,
			ZIndex = VisiblePileCards + 20,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		TextureRect currentSprite = CreateDeckShuffleFrameSprite("DeckShuffleCurrentFrame", DeckShuffleFrames[0], 0);
		TextureRect nextSprite = CreateDeckShuffleFrameSprite("DeckShuffleNextFrame", DeckShuffleFrames.Length > 1 ? DeckShuffleFrames[1] : DeckShuffleFrames[0], 1);
		nextSprite.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		shuffleRoot.AddChild(currentSprite);
		shuffleRoot.AddChild(nextSprite);
		pile.AddChild(shuffleRoot);

		try
		{
			await PlayDeckShuffleFramesAsync(shuffleRoot, currentSprite, nextSprite, DeckShuffleFrames, shuffleBasePosition, shuffleBaseRotation);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(shuffleRoot))
			{
				shuffleRoot.QueueFree();
			}

			SetOriginalPileVisualsVisible(originalCards, countLabel, true);
			if (countLabel != null)
			{
				countLabel.Visible = countLabelVisible;
			}
		}
	}

	private static TextureRect CreateDeckShuffleFrameSprite(string name, Texture2D texture, int zIndex)
	{
		return new TextureRect
		{
			Name = name,
			Texture = texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = Vector2.Zero,
			Size = DeckShuffleVisualSize,
			PivotOffset = DeckShuffleVisualSize / 2.0f,
			ZIndex = zIndex,
		};
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

	private async Task PlayDeckShuffleFramesAsync(
		Control shuffleRoot,
		TextureRect currentSprite,
		TextureRect nextSprite,
		IReadOnlyList<Texture2D> shuffleFrames,
		Vector2 basePosition,
		float baseRotationDegrees,
		int repeatCount = DeckShuffleRepeatCount,
		float frameSeconds = DeckShuffleFrameSeconds,
		bool useMotion = false)
	{
		if (shuffleFrames.Count == 0)
		{
			return;
		}

		float animationSeconds = repeatCount * shuffleFrames.Count * frameSeconds;
		ApplyDeckShuffleFramePair(currentSprite, nextSprite, shuffleFrames, 0, 1, 1.0f, 0.0f);
		shuffleRoot.Position = basePosition;
		shuffleRoot.RotationDegrees = baseRotationDegrees;
		shuffleRoot.Scale = Vector2.One;
		shuffleRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.SetEase(Tween.EaseType.Out);
		TweenDeckShuffleFramePairs(tween, currentSprite, nextSprite, shuffleFrames, repeatCount, frameSeconds);
		tween.TweenProperty(shuffleRoot, "modulate:a", 1.0f, animationSeconds * 0.16f);
		tween.TweenProperty(shuffleRoot, "modulate:a", 0.0f, animationSeconds * 0.14f).SetDelay(animationSeconds * 0.86f);
		if (useMotion)
		{
			tween.TweenProperty(shuffleRoot, "scale", Vector2.One * 1.035f, animationSeconds * 0.5f);
			tween.TweenProperty(shuffleRoot, "scale", Vector2.One, animationSeconds * 0.5f).SetDelay(animationSeconds * 0.5f);
		}

		tween.Finished += () =>
		{
			if (!GodotObject.IsInstanceValid(shuffleRoot)
				|| !GodotObject.IsInstanceValid(currentSprite)
				|| !GodotObject.IsInstanceValid(nextSprite))
			{
				return;
			}

			shuffleRoot.Position = basePosition;
			shuffleRoot.RotationDegrees = baseRotationDegrees;
			shuffleRoot.Scale = Vector2.One;
			shuffleRoot.Modulate = Colors.White;
			currentSprite.Texture = shuffleFrames[shuffleFrames.Count - 1];
			currentSprite.Modulate = Colors.White;
			nextSprite.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		};
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private static void TweenDeckShuffleFramePairs(
		Tween tween,
		TextureRect currentSprite,
		TextureRect nextSprite,
		IReadOnlyList<Texture2D> shuffleFrames,
		int repeatCount,
		float frameSeconds)
	{
		if (shuffleFrames.Count == 0)
		{
			return;
		}

		int totalSlots = repeatCount * shuffleFrames.Count;
		for (int slotIndex = 0; slotIndex < totalSlots; slotIndex++)
		{
			int currentFrameIndex = slotIndex % shuffleFrames.Count;
			int nextFrameIndex = (currentFrameIndex + 1) % shuffleFrames.Count;
			tween.TweenCallback(Callable.From(() =>
				ApplyDeckShuffleFramePair(currentSprite, nextSprite, shuffleFrames, currentFrameIndex, nextFrameIndex, 1.0f, 0.0f)))
				.SetDelay(slotIndex * frameSeconds);
			tween.TweenCallback(Callable.From(() =>
				ApplyDeckShuffleFramePair(currentSprite, nextSprite, shuffleFrames, currentFrameIndex, nextFrameIndex, 0.0f, 1.0f)))
				.SetDelay(slotIndex * frameSeconds + frameSeconds * 0.72f);
		}
	}

	private static void ApplyDeckShuffleFramePair(
		TextureRect currentSprite,
		TextureRect nextSprite,
		IReadOnlyList<Texture2D> shuffleFrames,
		int currentFrameIndex,
		int nextFrameIndex,
		float currentAlpha,
		float nextAlpha)
	{
		if (!GodotObject.IsInstanceValid(currentSprite)
			|| !GodotObject.IsInstanceValid(nextSprite)
			|| shuffleFrames.Count == 0)
		{
			return;
		}

		currentSprite.Texture = shuffleFrames[currentFrameIndex];
		nextSprite.Texture = shuffleFrames[nextFrameIndex];
		currentSprite.Modulate = new Color(1.0f, 1.0f, 1.0f, currentAlpha);
		nextSprite.Modulate = new Color(1.0f, 1.0f, 1.0f, nextAlpha);
	}
}
