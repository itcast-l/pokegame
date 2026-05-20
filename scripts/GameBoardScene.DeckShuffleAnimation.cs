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

		List<Texture2D> shuffleFrames = LoadDeckShuffleFrames();
		if (shuffleFrames.Count == 0)
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
		};
		TextureRect currentSprite = CreateDeckShuffleFrameSprite("DeckShuffleCurrentFrame", shuffleFrames[0], 0);
		TextureRect nextSprite = CreateDeckShuffleFrameSprite("DeckShuffleNextFrame", shuffleFrames.Count > 1 ? shuffleFrames[1] : shuffleFrames[0], 1);
		nextSprite.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		shuffleRoot.AddChild(currentSprite);
		shuffleRoot.AddChild(nextSprite);
		pile.AddChild(shuffleRoot);

		try
		{
			await PlayDeckShuffleFramesAsync(shuffleRoot, currentSprite, nextSprite, shuffleFrames, shuffleBasePosition, shuffleBaseRotation);
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

	private async Task PlayDeckShuffleFramesAsync(
		Control shuffleRoot,
		TextureRect currentSprite,
		TextureRect nextSprite,
		IReadOnlyList<Texture2D> shuffleFrames,
		Vector2 basePosition,
		float baseRotationDegrees)
	{
		if (shuffleFrames.Count == 0)
		{
			return;
		}

		float animationSeconds = DeckShuffleRepeatCount * shuffleFrames.Count * DeckShuffleFrameSeconds;
		UpdateDeckShuffleAnimation(shuffleRoot, currentSprite, nextSprite, shuffleFrames, basePosition, baseRotationDegrees, 0.0f);
		Tween tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.InOut);
		tween.TweenMethod(
			Callable.From<float>(progress => UpdateDeckShuffleAnimation(
				shuffleRoot,
				currentSprite,
				nextSprite,
				shuffleFrames,
				basePosition,
				baseRotationDegrees,
				progress)),
			0.0f,
			1.0f,
			animationSeconds);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private static void UpdateDeckShuffleAnimation(
		Control shuffleRoot,
		TextureRect currentSprite,
		TextureRect nextSprite,
		IReadOnlyList<Texture2D> shuffleFrames,
		Vector2 basePosition,
		float baseRotationDegrees,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(shuffleRoot)
			|| !GodotObject.IsInstanceValid(currentSprite)
			|| !GodotObject.IsInstanceValid(nextSprite)
			|| shuffleFrames.Count == 0)
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		if (clampedProgress >= 0.999f)
		{
			shuffleRoot.Position = basePosition;
			shuffleRoot.RotationDegrees = baseRotationDegrees;
			shuffleRoot.Scale = Vector2.One;
			shuffleRoot.Modulate = Colors.White;
			currentSprite.Texture = shuffleFrames[shuffleFrames.Count - 1];
			currentSprite.Modulate = Colors.White;
			nextSprite.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
			return;
		}

		float totalSlots = DeckShuffleRepeatCount * shuffleFrames.Count;
		float timeline = clampedProgress * totalSlots;
		int slotIndex = Mathf.FloorToInt(timeline);
			float slotProgress = timeline - slotIndex;
			int currentFrameIndex = slotIndex % shuffleFrames.Count;
			int nextFrameIndex = (currentFrameIndex + 1) % shuffleFrames.Count;
			float blendAlpha = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((slotProgress - 0.18f) / 0.64f, 0.0f, 1.0f));
			float fadeIn = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp(clampedProgress / 0.12f, 0.0f, 1.0f));
			float fadeOut = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((clampedProgress - 0.9f) / 0.1f, 0.0f, 1.0f));
			float alpha = fadeIn * fadeOut;

		currentSprite.Texture = shuffleFrames[currentFrameIndex];
		nextSprite.Texture = shuffleFrames[nextFrameIndex];
			currentSprite.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha * (1.0f - blendAlpha));
			nextSprite.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha * blendAlpha);
			shuffleRoot.Modulate = Colors.White;
			shuffleRoot.Position = basePosition;
			shuffleRoot.RotationDegrees = baseRotationDegrees;
			shuffleRoot.Scale = Vector2.One;
		}
}
