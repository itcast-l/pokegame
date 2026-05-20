using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
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

	private async Task PlayTableCardPointPopupAsync(int tableCardIndex, int points, bool isDoubleScore = false)
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
			Size = new Vector2(CardVisualSize.X, isDoubleScore ? 34.0f : 28.0f),
			ZIndex = TableCardPointPopupZIndex,
		};
		pointLabel.AddThemeFontSizeOverride("font_size", isDoubleScore ? TableCardPointPopupDoubleFontSize : TableCardPointPopupFontSize);
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
		bool scoringCardsOnly = false,
		HashSet<int>? extraTableCardIndexes = null)
	{
		var visuals = new List<CapturedTableCardVisual>();
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return visuals;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		for (int tableCardIndex = Math.Min(_tableCards.Count, cardPositions.Count) - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			bool isBaseCapturedIndex = tableCardIndex >= captureStartIndex;
			bool isExtraCapturedIndex = extraTableCardIndexes != null && extraTableCardIndexes.Contains(tableCardIndex);
			if (!isBaseCapturedIndex && !isExtraCapturedIndex)
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			bool isScoringCardIndex = IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex) || isExtraCapturedIndex;
			if (scoringCardsOnly && (!isScoringCardIndex || !tableCard.Card.HasValue))
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
			else if (tableCard.Card.HasValue && tableCard.IsFaceUp)
			{
				itemOverlayTexture = GetCardAttachmentOverlayTexture(tableCard.Card.Value);
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
		int turnSequenceGeneration,
		HashSet<int>? extraTableCardIndexes = null)
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
			if (IsQuestionOnlyTableCard(_tableCards[tableCardIndex]))
			{
				continue;
			}

			_tableCards[tableCardIndex].IsHiddenDuringCapture = true;
		}
		if (extraTableCardIndexes != null)
		{
			foreach (int tableCardIndex in extraTableCardIndexes)
			{
				if (tableCardIndex >= 0 && tableCardIndex < _tableCards.Count)
				{
					if (IsQuestionOnlyTableCard(_tableCards[tableCardIndex]))
					{
						continue;
					}

					_tableCards[tableCardIndex].IsHiddenDuringCapture = true;
				}
			}
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

	private static bool IsQuestionOnlyTableCard(TableCard tableCard)
	{
		return !tableCard.Card.HasValue
			&& tableCard.ItemOverlay.HasValue
			&& tableCard.ItemOverlay.Value.Id == QuestionMarkItemCardId;
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
}
