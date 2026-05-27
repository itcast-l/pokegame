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
			Tween revealTween = CreateTween();
			revealTween.SetParallel(true);
			revealTween.SetTrans(Tween.TransitionType.Cubic);
			revealTween.SetEase(Tween.EaseType.Out);
			revealTween.TweenProperty(revealCard, "scale", new Vector2(TableCardRevealMinScaleX, 1.0f), TableCardRevealHalfFlipSeconds);
			revealTween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(revealCard))
				{
					revealCard.Texture = faceTexture;
				}
			})).SetDelay(TableCardRevealHalfFlipSeconds);
			revealTween.TweenProperty(revealCard, "scale", Vector2.One, TableCardRevealHalfFlipSeconds)
				.SetDelay(TableCardRevealHalfFlipSeconds);
			revealTween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(revealCard))
				{
					revealCard.QueueFree();
				}
			};
			await ToSignal(revealTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(revealCard))
			{
				revealCard.QueueFree();
			}
		}
	}

	private async Task PlayTableCardPointPopupAsync(int tableCardIndex, int points, bool isDoubleScore = false, bool isDamage = false)
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
			Text = points == 0 ? "0" : $"{(isDamage ? "-" : "+")}{points}",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = new Vector2(CardVisualSize.X, isDoubleScore ? 34.0f : 28.0f),
			ZIndex = TableCardPointPopupZIndex,
		};
		pointLabel.AddThemeFontSizeOverride("font_size", isDoubleScore ? TableCardPointPopupDoubleFontSize : TableCardPointPopupFontSize);
		pointLabel.AddThemeColorOverride("font_color", isDamage ? new Color(1.0f, 0.42f, 0.32f) : new Color(1.0f, 0.94f, 0.56f));
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
			tween.TweenProperty(pointLabel, "modulate:a", 0.0f, TableCardPointPopupSeconds);
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

	private sealed class PairCaptureEdgeFloatHold
	{
		public PairCaptureEdgeFloatHold(List<int> tableCardIndexes, List<TextureRect> overlays)
		{
			TableCardIndexes = tableCardIndexes;
			Overlays = overlays;
		}

		public List<int> TableCardIndexes { get; }
		public List<TextureRect> Overlays { get; }

		public Dictionary<int, Vector2> BuildFlightStartPositionOverrides()
		{
			var positions = new Dictionary<int, Vector2>();
			for (int index = 0; index < TableCardIndexes.Count && index < Overlays.Count; index++)
			{
				TextureRect overlay = Overlays[index];
				if (GodotObject.IsInstanceValid(overlay))
				{
					positions[TableCardIndexes[index]] = overlay.GlobalPosition;
				}
			}

			return positions;
		}

		public void QueueFreeOverlays()
		{
			foreach (TextureRect overlay in Overlays)
			{
				if (GodotObject.IsInstanceValid(overlay))
				{
					overlay.QueueFree();
				}
			}
		}
	}

	private async Task<PairCaptureEdgeFloatHold?> PlayPairCaptureEdgeFloatHoldAnimationAsync(int captureStartIndex, int playedTableCardIndex)
	{
		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		var tableCardIndexes = new List<int>();
		var overlays = new List<TextureRect>();
		foreach (int tableCardIndex in BuildPairCaptureEdgeIndexes(captureStartIndex, playedTableCardIndex))
		{
			if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count || tableCardIndex >= cardPositions.Count)
			{
				continue;
			}

			Texture2D? texture = GetVisibleTableCardTexture(_tableCards[tableCardIndex]);
			if (texture == null)
			{
				continue;
			}

			var overlay = new TextureRect
			{
				Name = "PairCaptureEdgeFloat",
				Texture = texture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore,
				Size = CardVisualSize,
				PivotOffset = CardVisualSize / 2.0f,
				ZIndex = PlayedCardFlightZIndex + tableCardIndex * 2 + 220,
				ZAsRelative = false,
			};
			AddChild(overlay);
			overlay.GlobalPosition = _centerArena.GlobalPosition + cardPositions[tableCardIndex];
			_tableCards[tableCardIndex].IsHiddenDuringCapture = true;
			tableCardIndexes.Add(tableCardIndex);
			overlays.Add(overlay);
		}

		if (overlays.Count == 0)
		{
			return null;
		}

		RenderTableCards();
		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		foreach (TextureRect overlay in overlays)
		{
			Vector2 startGlobalPosition = overlay.GlobalPosition;
			tween.TweenProperty(overlay, "global_position", startGlobalPosition + new Vector2(0.0f, -PairCaptureEdgeFloatRise), PairCaptureEdgeFloatSeconds);
			tween.TweenProperty(overlay, "scale", Vector2.One * PairCaptureEdgeFloatScale, PairCaptureEdgeFloatSeconds);
		}

		await ToSignal(tween, Tween.SignalName.Finished);
		return new PairCaptureEdgeFloatHold(tableCardIndexes, overlays);
	}

	private void RestorePairCaptureEdgeFloatHold(PairCaptureEdgeFloatHold hold)
	{
		foreach (int tableCardIndex in hold.TableCardIndexes)
		{
			if (tableCardIndex >= 0 && tableCardIndex < _tableCards.Count)
			{
				_tableCards[tableCardIndex].IsHiddenDuringCapture = false;
			}
		}

		hold.QueueFreeOverlays();
		RenderTableCards();
	}

	private static List<int> BuildPairCaptureEdgeIndexes(int captureStartIndex, int playedTableCardIndex)
	{
		var indexes = new List<int> { captureStartIndex };
		if (playedTableCardIndex != captureStartIndex)
		{
			indexes.Add(playedTableCardIndex);
		}

		return indexes;
	}

	private List<CapturedTableCardVisual> BuildCapturedTableCardVisuals(
		int captureStartIndex,
		bool includeQuestionCard,
		bool scoringCardsOnly = false,
		HashSet<int>? extraTableCardIndexes = null,
		Dictionary<int, Vector2>? globalPositionOverrides = null,
		bool includeCaptureStartScoring = false,
		int scorePlayerIndex = -1)
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
			if (tableCard.IsBuriedInSand)
			{
				continue;
			}

			bool isScoringCardIndex = IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex)
				|| isExtraCapturedIndex
				|| (includeCaptureStartScoring && tableCardIndex == captureStartIndex);
			if (scoringCardsOnly
				&& (!isScoringCardIndex
					|| !tableCard.Card.HasValue
					|| IsSeaSlugSlimeScoreSuppressed(tableCard, scorePlayerIndex)))
			{
				continue;
			}

			bool isQuestionCard = IsQuestionTableCard(tableCard);
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
			if (tableCard.Card.HasValue && tableCard.IsSlimeCoated)
			{
				itemOverlayTexture = _slimeCardOverlayTexture;
			}
			else if (tableCard.Card.HasValue && tableCard.ItemOverlay.HasValue && (includeQuestionCard || !isQuestionCard))
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
				globalPositionOverrides != null && globalPositionOverrides.TryGetValue(tableCardIndex, out Vector2 overrideGlobalPosition)
					? overrideGlobalPosition
					: _centerArena.GlobalPosition + cardPositions[tableCardIndex],
				tableCardIndex * 2));
		}

		return visuals;
	}

	private async Task PlayCapturedCardsToDeckAnimationAsync(
		int playerIndex,
		int captureStartIndex,
		List<CapturedTableCardVisual> capturedCardVisuals,
		int turnSequenceGeneration,
		HashSet<int>? extraTableCardIndexes = null,
		float flightStaggerSeconds = TableCaptureFlightStaggerSeconds)
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
			if (_tableCards[tableCardIndex].IsBuriedInSand)
			{
				continue;
			}

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
					if (_tableCards[tableCardIndex].IsBuriedInSand)
					{
						continue;
					}

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
				visualIndex * flightStaggerSeconds));
		}

		await Task.WhenAll(flightTasks);
	}

	private bool IsQuestionOnlyTableCard(TableCard tableCard)
	{
		return !tableCard.Card.HasValue
			&& IsQuestionTableCard(tableCard);
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
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateCapturedCardFlightMotion(
					flightCard,
					capturedCardVisual.GlobalPosition,
					targetGlobalPosition,
					GetCardRotationDegreesForPlayer(playerIndex),
					progress)),
				0.0f,
				1.0f,
				TableCaptureFlightDuration);
			TweenCapturedCardFlightVisuals(
				tween,
				flightCard,
				itemOverlay,
				capturedCardVisual.StartTexture,
				_cardBackTexture!);
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

	private static void UpdateCapturedCardFlightMotion(
		TextureRect flightCard,
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
	}

	private static void TweenCapturedCardFlightVisuals(
		Tween tween,
		TextureRect flightCard,
		TextureRect? itemOverlay,
		Texture2D startTexture,
		Texture2D cardBackTexture)
	{
		flightCard.Texture = startTexture;
		float halfDuration = TableCaptureFlightDuration * 0.5f;
		float midScale = Mathf.Lerp(1.0f, TableCaptureFlightEndScale, 0.5f);
		tween.TweenProperty(flightCard, "scale", new Vector2(PlayedCardFlipMinScaleX * midScale, midScale), halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(flightCard))
			{
				return;
			}

			flightCard.Texture = cardBackTexture;
			if (itemOverlay != null && GodotObject.IsInstanceValid(itemOverlay))
			{
				itemOverlay.Visible = false;
			}
		})).SetDelay(halfDuration);
		tween.TweenProperty(flightCard, "scale", Vector2.One * TableCaptureFlightEndScale, halfDuration)
			.SetDelay(halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}
}
