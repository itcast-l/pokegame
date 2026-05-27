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
		bool tableCardIsFaceUp,
		Texture2D? visibleTextureOverride = null)
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

		Texture2D? startTexture = visibleTextureOverride ?? GetVisibleHandCardTexture(playerIndex, playedCard);
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
			UpdateCardToTableMotion(
				flightCard,
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees,
				0.0f);

			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateCardToTableMotion(
					flightCard,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					progress)),
				0.0f,
				1.0f,
				PlayedCardFlightDuration);
			TweenPlayedCardVisuals(
				tween,
				flightCard,
				cardVisual,
				attachmentOverlay,
				startTexture,
				targetTexture,
				flipToCardBack,
				targetAlpha);
			tween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(flightCard))
				{
					flightCard.QueueFree();
				}
			};

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
			UpdateItemCardToTableMotion(
				flightCard,
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees,
				0.0f);

			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateItemCardToTableMotion(
					flightCard,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					progress)),
				0.0f,
				1.0f,
				PlayedCardFlightDuration);
			TweenPlayedItemCardVisuals(
				tween,
				flightCard,
				startTexture,
				targetTexture,
				flipToCardBack);
			tween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(flightCard))
				{
					flightCard.QueueFree();
				}
			};

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
			flightTween.SetParallel(true);
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
			TweenFlightPulseScale(flightTween, flightCard, LensCardFlightSeconds, 1.12f);
			flightTween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(flightCard))
				{
					flightCard.QueueFree();
				}
			};
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

	private async Task PlayStolenHandCardFlightAsync(
		Texture2D cardTexture,
		Texture2D? attachmentOverlayTexture,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees,
		float visualScale)
	{
		if (!IsInsideTree())
		{
			return;
		}

		Vector2 visualSize = CardVisualSize * visualScale;
		var flightCard = new Control
		{
			Name = "StolenHandCardFlight",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			RotationDegrees = startRotationDegrees,
			ZIndex = PlayedCardFlightZIndex + 840,
			ZAsRelative = false,
		};
		AddChild(flightCard);
		flightCard.GlobalPosition = startGlobalPosition;

		var cardVisual = new TextureRect
		{
			Name = "StolenHandCardFlightVisual",
			Texture = cardTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardVisualSize - visualSize) / 2.0f,
			Size = visualSize,
			PivotOffset = visualSize / 2.0f,
		};
		flightCard.AddChild(cardVisual);

		if (attachmentOverlayTexture != null)
		{
			var attachmentOverlay = new TextureRect
			{
				Name = "StolenHandCardAttachmentOverlay",
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
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateStolenHandCardFlight(
					flightCard,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					targetRotationDegrees,
					progress)),
				0.0f,
				1.0f,
				StolenCardFlightSeconds);
			TweenFlightPulseScale(tween, flightCard, StolenCardFlightSeconds, StolenCardFlightPeakScale);
			tween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(flightCard))
				{
					flightCard.QueueFree();
				}
			};
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

	private async Task PlayStolenDeckCardFlightAsync(
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees)
	{
		Texture2D? cardTexture = _cardBackTexture;
		if (cardTexture == null || !IsInsideTree())
		{
			return;
		}

		await PlayStolenHandCardFlightAsync(
			cardTexture,
			null,
			startGlobalPosition,
			targetGlobalPosition,
			startRotationDegrees,
			targetRotationDegrees,
			1.0f);
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
	}

	private static void UpdateStolenHandCardFlight(
		Control flightCard,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * StolenCardFlightArcHeight;
		float theftWobble = Mathf.Sin(clampedProgress * Mathf.Pi) * 8.0f;
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		flightCard.RotationDegrees = Mathf.Lerp(startRotationDegrees, targetRotationDegrees, easedProgress) + theftWobble;
	}

	private static void UpdateCardToTableMotion(
		Control flightCard,
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
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, clampedProgress);
		flightCard.RotationDegrees = Mathf.Lerp(startRotationDegrees, 0.0f, clampedProgress);
	}

	private static void TweenPlayedCardVisuals(
		Tween tween,
		Control flightCard,
		TextureRect cardVisual,
		TextureRect? attachmentOverlay,
		Texture2D startTexture,
		Texture2D targetTexture,
		bool flipToCardBack,
		float targetAlpha)
	{
		if (!flipToCardBack)
		{
			cardVisual.Texture = startTexture;
			if (attachmentOverlay != null)
			{
				attachmentOverlay.Visible = true;
				tween.TweenProperty(attachmentOverlay, "modulate:a", targetAlpha, PlayedCardFlightDuration)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			}

			flightCard.Scale = Vector2.One;
			tween.TweenProperty(cardVisual, "modulate:a", targetAlpha, PlayedCardFlightDuration)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			return;
		}

		cardVisual.Texture = startTexture;
		cardVisual.Modulate = Colors.White;
		if (attachmentOverlay != null)
		{
			attachmentOverlay.Visible = true;
			attachmentOverlay.Modulate = Colors.White;
		}

		float halfDuration = PlayedCardFlightDuration * 0.5f;
		tween.TweenProperty(flightCard, "scale", new Vector2(PlayedCardFlipMinScaleX, 1.0f), halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(cardVisual))
			{
				return;
			}

			cardVisual.Texture = targetTexture;
			if (attachmentOverlay != null && GodotObject.IsInstanceValid(attachmentOverlay))
			{
				attachmentOverlay.Visible = false;
			}
		})).SetDelay(halfDuration);
		tween.TweenProperty(flightCard, "scale", Vector2.One, halfDuration)
			.SetDelay(halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}

	private static void UpdateItemCardToTableMotion(
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
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, clampedProgress);
		flightCard.RotationDegrees = Mathf.Lerp(startRotationDegrees, 0.0f, clampedProgress);
	}

	private static void TweenPlayedItemCardVisuals(
		Tween tween,
		TextureRect flightCard,
		Texture2D startTexture,
		Texture2D targetTexture,
		bool flipToCardBack)
	{
		if (!flipToCardBack)
		{
			flightCard.Texture = startTexture;
			flightCard.Scale = Vector2.One;
			return;
		}

		flightCard.Texture = startTexture;
		float halfDuration = PlayedCardFlightDuration * 0.5f;
		tween.TweenProperty(flightCard, "scale", new Vector2(PlayedCardFlipMinScaleX, 1.0f), halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(flightCard))
			{
				flightCard.Texture = targetTexture;
			}
		})).SetDelay(halfDuration);
		tween.TweenProperty(flightCard, "scale", Vector2.One, halfDuration)
			.SetDelay(halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}

	private static void TweenFlightPulseScale(Tween tween, CanvasItem target, float duration, float peakScale)
	{
		float halfDuration = duration * 0.5f;
		tween.TweenProperty(target, "scale", Vector2.One * peakScale, halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(target, "scale", Vector2.One, halfDuration)
			.SetDelay(halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}
}
