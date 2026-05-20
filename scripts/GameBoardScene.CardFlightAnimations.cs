using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
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
		bool showMainPlayerPreview = playerIndex == MainPlayerIndex && !tableCardIsFaceUp;
		bool flipToCardBack = !showMainPlayerPreview && playerIndex == MainPlayerIndex && !tableCardIsFaceUp && _cardBackTexture != null;
		Texture2D targetTexture = flipToCardBack ? _cardBackTexture! : startTexture;
		float targetAlpha = showMainPlayerPreview ? MainPlayerTablePreviewAlpha : 1.0f;
		float startRotationDegrees = GetCardRotationDegreesForPlayer(playerIndex);
		var flightCard = new Control
		{
			Name = "PlayedCardFlight",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			RotationDegrees = startRotationDegrees,
			ZIndex = PlayedCardFlightZIndex,
			ZAsRelative = false,
		};
		AddChild(flightCard);
		flightCard.GlobalPosition = startGlobalPosition;
		var cardVisual = new TextureRect
		{
			Name = "PlayedCardFlightVisual",
			Texture = startTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = 0,
		};
		flightCard.AddChild(cardVisual);
		TextureRect? attachmentOverlay = null;
		Texture2D? attachmentOverlayTexture = GetCardAttachmentOverlayTexture(playedCard);
		bool startShowsAttachmentOverlay = attachmentOverlayTexture != null
			&& (playerIndex == MainPlayerIndex || playerIndex == _revealedPlayerHandIndex || _cardBackTexture == null);
		if (startShowsAttachmentOverlay)
		{
			attachmentOverlay = new TextureRect
			{
				Name = "PlayedCardAttachmentOverlay",
				Texture = attachmentOverlayTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
				Size = CardVisualSize,
				PivotOffset = CardVisualSize / 2.0f,
				ZIndex = 1,
			};
			flightCard.AddChild(attachmentOverlay);
		}

		try
		{
			UpdateCardToTableAnimation(
				flightCard,
				cardVisual,
				attachmentOverlay,
				startTexture,
				targetTexture,
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees,
				flipToCardBack,
				targetAlpha,
				0.0f);

			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateCardToTableAnimation(
					flightCard,
					cardVisual,
					attachmentOverlay,
					startTexture,
					targetTexture,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					flipToCardBack,
					targetAlpha,
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

	private async Task PlayItemCardToTableAnimationAsync(
		int playerIndex,
		ItemCard itemCard,
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

		bool flipToCardBack = playerIndex == MainPlayerIndex && !tableCardIsFaceUp && _cardBackTexture != null;
		Texture2D? startTexture = flipToCardBack ? _cardBackTexture : GetVisibleHandItemCardTexture(playerIndex, itemCard);
		if (startTexture == null)
		{
			return;
		}

		Vector2 targetGlobalPosition = _centerArena.GlobalPosition + tableCardPositions[tableCardIndex];
		Texture2D targetTexture = flipToCardBack ? _cardBackTexture! : startTexture;
		float startRotationDegrees = GetCardRotationDegreesForPlayer(playerIndex);
		var flightCard = new TextureRect
		{
			Name = "PlayedItemCardFlight",
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
			UpdateItemCardToTableAnimation(
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
				Callable.From<float>(progress => UpdateItemCardToTableAnimation(
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

	private Texture2D? GetVisibleHandItemCardTexture(int playerIndex, ItemCard itemCard)
	{
		if (playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null)
		{
			return _cardBackTexture;
		}

		return itemCard.Texture;
	}

	private async Task PlayItemCardToTransformAreaAnimationAsync(
		Texture2D itemTexture,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees)
	{
		if (!IsInsideTree())
		{
			return;
		}

		var flightCard = new TextureRect
		{
			Name = "ItemCardTransformAreaFlight",
			Texture = itemTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			RotationDegrees = startRotationDegrees,
			ZIndex = PlayedCardFlightZIndex + 820,
			ZAsRelative = false,
		};
		AddChild(flightCard);
		flightCard.GlobalPosition = startGlobalPosition;

		try
		{
			Tween flightTween = CreateTween();
			flightTween.SetTrans(Tween.TransitionType.Sine);
			flightTween.SetEase(Tween.EaseType.InOut);
			flightTween.TweenMethod(
				Callable.From<float>(progress => UpdateItemCardToTransformAreaFlight(
					flightCard,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					progress)),
				0.0f,
				1.0f,
				LensCardFlightSeconds);
			await ToSignal(flightTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(flightCard))
			{
				flightCard.QueueFree();
			}
		}
	}

	private static void UpdateItemCardToTransformAreaFlight(
		TextureRect flightCard,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * TableCaptureFlightArcHeight;
		float pulse = Mathf.Sin(clampedProgress * Mathf.Pi);
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		flightCard.RotationDegrees = Mathf.Lerp(startRotationDegrees, 0.0f, easedProgress) + pulse * 10.0f;
		flightCard.Scale = Vector2.One * Mathf.Lerp(1.0f, 1.12f, pulse);
		flightCard.Modulate = Colors.White;
	}

	private static void UpdateCardToTableAnimation(
		Control flightCard,
		TextureRect cardVisual,
		TextureRect? attachmentOverlay,
		Texture2D startTexture,
		Texture2D targetTexture,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		bool flipToCardBack,
		float targetAlpha,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard) || !GodotObject.IsInstanceValid(cardVisual))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, clampedProgress);
		flightCard.RotationDegrees = Mathf.Lerp(startRotationDegrees, 0.0f, clampedProgress);
		Color targetModulate = new(1.0f, 1.0f, 1.0f, targetAlpha);

		if (!flipToCardBack)
		{
			cardVisual.Texture = startTexture;
			cardVisual.Modulate = Colors.White.Lerp(targetModulate, clampedProgress);
			if (attachmentOverlay != null)
			{
				attachmentOverlay.Visible = true;
				attachmentOverlay.Modulate = cardVisual.Modulate;
			}

			flightCard.Scale = Vector2.One;
			return;
		}

		bool showingBack = clampedProgress >= 0.5f;
		cardVisual.Texture = showingBack ? targetTexture : startTexture;
		cardVisual.Modulate = Colors.White;
		if (attachmentOverlay != null)
		{
			attachmentOverlay.Visible = !showingBack;
			attachmentOverlay.Modulate = Colors.White;
		}

		float halfProgress = showingBack
			? (clampedProgress - 0.5f) * 2.0f
			: clampedProgress * 2.0f;
		float scaleX = showingBack
			? Mathf.Lerp(PlayedCardFlipMinScaleX, 1.0f, halfProgress)
			: Mathf.Lerp(1.0f, PlayedCardFlipMinScaleX, halfProgress);
		flightCard.Scale = new Vector2(scaleX, 1.0f);
	}

	private static void UpdateItemCardToTableAnimation(
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
}
