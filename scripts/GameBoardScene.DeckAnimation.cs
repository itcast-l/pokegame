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
	private const float QuestionCardToTransformAreaFlightSeconds = 0.36f;
	private const float QuestionCardTransformSeconds = 0.58f;
	private const float QuestionCardRewardInsertSeconds = 0.22f;
	private const float SpecialCardTriggerEffectSeconds = 0.72f;
	private const float SpecialCardTriggerFxFrameSeconds = 0.14f;
	private const float FullCircleRadians = 6.2831855f;
	private const int PlayedCardFlightZIndex = 2000;
	private static readonly Vector2 DeckShuffleVisualSize = new(96.0f, 192.0f);
	private static readonly Vector2 QuestionCardTransformFxVisualSize = new(128.0f, 128.0f);
	private static readonly Vector2 SpecialCardTriggerSparkSize = new(20.0f, 20.0f);
	private static readonly Vector2 SpecialCardTriggerRingSize = new(188.0f, 188.0f);
	private static readonly Vector2 SpecialCardTriggerFxVisualSize = new(256.0f, 256.0f);

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
		TextureRect? magnetOverlay = null;
		bool startShowsMagnetOverlay = playedCard.HasMagnetEffect
			&& _magnetCardOverlayTexture != null
			&& (playerIndex == MainPlayerIndex || playerIndex == _revealedPlayerHandIndex || _cardBackTexture == null);
		if (startShowsMagnetOverlay)
		{
			magnetOverlay = new TextureRect
			{
				Name = "PlayedCardMagnetOverlay",
				Texture = _magnetCardOverlayTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
				Size = CardVisualSize,
				PivotOffset = CardVisualSize / 2.0f,
				ZIndex = 1,
			};
			flightCard.AddChild(magnetOverlay);
		}

		try
		{
			UpdateCardToTableAnimation(
				flightCard,
				cardVisual,
				magnetOverlay,
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
					cardVisual,
					magnetOverlay,
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

	private async Task PlaySpecialCardTriggerEffectAsync(Texture2D cardTexture, Vector2 globalPosition, int zIndex, float rotationDegrees = 0.0f)
	{
		if (!IsInsideTree())
		{
			return;
		}

		var effectRoot = new Control
		{
			Name = "SpecialCardTriggerEffect",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = PlayedCardFlightZIndex + zIndex + 760,
			ZAsRelative = false,
			RotationDegrees = rotationDegrees,
			Scale = Vector2.One,
		};
		AddChild(effectRoot);
		effectRoot.GlobalPosition = globalPosition;

		var ring = new SpecialCardTriggerRing
		{
			Name = "SpecialCardTriggerRing",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardVisualSize - SpecialCardTriggerRingSize) / 2.0f,
			Size = SpecialCardTriggerRingSize,
			PivotOffset = SpecialCardTriggerRingSize / 2.0f,
			ZIndex = 0,
		};
		effectRoot.AddChild(ring);

		var card = new TextureRect
		{
			Name = "SpecialCardTriggerCard",
			Texture = cardTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = 2,
		};
		effectRoot.AddChild(card);

		var triggerFx = new TextureRect
		{
			Name = "SpecialCardTriggerFx",
			Texture = _specialCardTriggerEffectFrames.Count > 0 ? _specialCardTriggerEffectFrames[0] : null,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardVisualSize - SpecialCardTriggerFxVisualSize) / 2.0f,
			Size = SpecialCardTriggerFxVisualSize,
			PivotOffset = SpecialCardTriggerFxVisualSize / 2.0f,
			ZIndex = 20,
			Visible = _specialCardTriggerEffectFrames.Count > 0,
		};
		card.AddChild(triggerFx);

		Vector2[] sparkPositions =
		{
			new(-36.0f, -34.0f),
			new(CardVisualSize.X + 17.0f, -28.0f),
			new(-30.0f, CardVisualSize.Y + 16.0f),
			new(CardVisualSize.X + 20.0f, CardVisualSize.Y + 15.0f),
		};
		foreach (Vector2 sparkPosition in sparkPositions)
		{
			var spark = new ColorRect
			{
				Name = "SpecialCardTriggerSpark",
				Color = new Color(1.0f, 0.96f, 0.56f, 0.95f),
				MouseFilter = MouseFilterEnum.Ignore,
				Position = sparkPosition,
				Size = SpecialCardTriggerSparkSize,
				PivotOffset = SpecialCardTriggerSparkSize / 2.0f,
				RotationDegrees = 45.0f,
				ZIndex = 4,
				Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			};
			effectRoot.AddChild(spark);
		}

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(effectRoot, "scale", Vector2.One * 1.82f, SpecialCardTriggerEffectSeconds * 0.42f);
			tween.TweenProperty(card, "scale", Vector2.One * 1.10f, SpecialCardTriggerEffectSeconds * 0.42f);
			tween.TweenProperty(ring, "ring_progress", 0.58f, SpecialCardTriggerEffectSeconds * 0.42f);
			tween.TweenProperty(ring, "ring_alpha", 1.0f, SpecialCardTriggerEffectSeconds * 0.18f);
			tween.TweenProperty(triggerFx, "modulate", Colors.White, SpecialCardTriggerEffectSeconds * 0.12f);
			tween.TweenProperty(triggerFx, "scale", Vector2.One * 1.18f, SpecialCardTriggerEffectSeconds * 0.42f);
			for (int sparkIndex = 0; sparkIndex < effectRoot.GetChildCount(); sparkIndex++)
			{
				if (effectRoot.GetChild(sparkIndex) is ColorRect spark)
				{
					tween.TweenProperty(spark, "modulate", Colors.White, SpecialCardTriggerEffectSeconds * 0.24f);
					tween.TweenProperty(spark, "scale", Vector2.One * 2.35f, SpecialCardTriggerEffectSeconds * 0.42f);
				}
			}

			tween.Chain().SetParallel(true);
			tween.TweenProperty(effectRoot, "scale", Vector2.One * 1.16f, SpecialCardTriggerEffectSeconds * 0.58f);
			tween.TweenProperty(card, "scale", Vector2.One, SpecialCardTriggerEffectSeconds * 0.58f);
			tween.TweenProperty(ring, "ring_progress", 1.0f, SpecialCardTriggerEffectSeconds * 0.58f);
			tween.TweenProperty(ring, "ring_alpha", 0.0f, SpecialCardTriggerEffectSeconds * 0.58f);
			tween.TweenProperty(triggerFx, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), SpecialCardTriggerEffectSeconds * 0.58f);
			tween.TweenProperty(triggerFx, "scale", Vector2.One * 1.34f, SpecialCardTriggerEffectSeconds * 0.58f);
			for (int sparkIndex = 0; sparkIndex < effectRoot.GetChildCount(); sparkIndex++)
			{
				if (effectRoot.GetChild(sparkIndex) is ColorRect spark)
				{
					tween.TweenProperty(spark, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), SpecialCardTriggerEffectSeconds * 0.58f);
					tween.TweenProperty(spark, "scale", Vector2.One * 0.68f, SpecialCardTriggerEffectSeconds * 0.58f);
				}
			}

			Task fxTask = PlaySpecialCardTriggerFxFramesAsync(triggerFx);
			await ToSignal(tween, Tween.SignalName.Finished);
			await fxTask;
		}
		finally
		{
			if (GodotObject.IsInstanceValid(effectRoot))
			{
				effectRoot.QueueFree();
			}
		}
	}

	private async Task PlaySpecialCardTriggerFxFramesAsync(TextureRect fx)
	{
		if (_specialCardTriggerEffectFrames.Count == 0)
		{
			return;
		}

		foreach (Texture2D frame in _specialCardTriggerEffectFrames)
		{
			if (!GodotObject.IsInstanceValid(fx))
			{
				return;
			}

			fx.Texture = frame;
			await ToSignal(GetTree().CreateTimer(SpecialCardTriggerFxFrameSeconds), SceneTreeTimer.SignalName.Timeout);
		}
	}

	private static void UpdateCardToTableAnimation(
		Control flightCard,
		TextureRect cardVisual,
		TextureRect? magnetOverlay,
		Texture2D startTexture,
		Texture2D targetTexture,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		bool flipToCardBack,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard) || !GodotObject.IsInstanceValid(cardVisual))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, clampedProgress);
		flightCard.RotationDegrees = Mathf.Lerp(startRotationDegrees, 0.0f, clampedProgress);

		if (!flipToCardBack)
		{
			cardVisual.Texture = startTexture;
			if (magnetOverlay != null)
			{
				magnetOverlay.Visible = true;
			}

			flightCard.Scale = Vector2.One;
			return;
		}

		bool showingBack = clampedProgress >= 0.5f;
		cardVisual.Texture = showingBack ? targetTexture : startTexture;
		if (magnetOverlay != null)
		{
			magnetOverlay.Visible = !showingBack;
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

	private async Task PlayQuestionCardRewardAnimationAsync(
		int playerIndex,
		QuestionCardRewardAnimation? rewardAnimation,
		int turnSequenceGeneration)
	{
		if (!rewardAnimation.HasValue
			|| playerIndex != MainPlayerIndex
			|| !IsInsideTree()
			|| !_itemCardTextures.TryGetValue(QuestionMarkItemCardId, out Texture2D? questionTexture))
		{
			return;
		}

		QuestionCardRewardAnimation reward = rewardAnimation.Value;
		Vector2 targetGlobalPosition = GetQuestionCardRewardTargetGlobalPosition(playerIndex, reward);
		Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(reward.StartGlobalPosition);
		var animationRoot = new Control
		{
			Name = "QuestionCardRewardTransform",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = PlayedCardFlightZIndex + 900,
			ZAsRelative = false,
		};
		var fx = new TextureRect
		{
			Name = "QuestionCardTransformFx",
			Texture = _questionCardTransformFrames.Count > 0 ? _questionCardTransformFrames[0] : null,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardVisualSize - QuestionCardTransformFxVisualSize) / 2.0f,
			Size = QuestionCardTransformFxVisualSize,
			PivotOffset = QuestionCardTransformFxVisualSize / 2.0f,
			ZIndex = 0,
			Visible = _questionCardTransformFrames.Count > 0,
		};
		var card = new TextureRect
		{
			Name = "QuestionCardRewardCard",
			Texture = questionTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = GetItemCardVisualPosition(QuestionMarkItemCardId),
			Size = GetItemCardVisualSize(QuestionMarkItemCardId),
			PivotOffset = GetItemCardVisualSize(QuestionMarkItemCardId) / 2.0f,
			ZIndex = 1,
		};

		animationRoot.AddChild(fx);
		animationRoot.AddChild(card);
		AddChild(animationRoot);
		animationRoot.GlobalPosition = reward.StartGlobalPosition;

		try
		{
			UpdateQuestionCardToTransformAreaFlight(
				animationRoot,
				fx,
				_questionCardTransformFrames,
				reward.StartGlobalPosition,
				transformAreaGlobalPosition,
				0.0f);

			Tween areaFlightTween = CreateTween();
			areaFlightTween.SetTrans(Tween.TransitionType.Sine);
			areaFlightTween.SetEase(Tween.EaseType.InOut);
			areaFlightTween.TweenMethod(
				Callable.From<float>(progress => UpdateQuestionCardToTransformAreaFlight(
					animationRoot,
					fx,
					_questionCardTransformFrames,
					reward.StartGlobalPosition,
					transformAreaGlobalPosition,
					progress)),
				0.0f,
				1.0f,
				QuestionCardToTransformAreaFlightSeconds);

			await ToSignal(areaFlightTween, Tween.SignalName.Finished);
			if (!GodotObject.IsInstanceValid(animationRoot) || turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			animationRoot.GlobalPosition = transformAreaGlobalPosition;
			Tween transformTween = CreateTween();
			transformTween.SetTrans(Tween.TransitionType.Sine);
			transformTween.SetEase(Tween.EaseType.InOut);
			transformTween.TweenMethod(
				Callable.From<float>(progress => UpdateQuestionCardTransform(
					animationRoot,
					card,
					fx,
					_questionCardTransformFrames,
					questionTexture,
					reward.ItemCard.Texture,
					progress)),
				0.0f,
				1.0f,
				QuestionCardTransformSeconds);

			await ToSignal(transformTween, Tween.SignalName.Finished);
			if (!GodotObject.IsInstanceValid(animationRoot) || turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			card.Texture = reward.ItemCard.Texture;
			Vector2 rewardVisualSize = GetItemCardVisualSize(reward.ItemCard.Id);
			card.Position = GetItemCardVisualPosition(reward.ItemCard.Id);
			card.Size = rewardVisualSize;
			card.PivotOffset = rewardVisualSize / 2.0f;

			Vector2 insertStartGlobalPosition = targetGlobalPosition + new Vector2(0.0f, -CardVisualSize.Y * 0.82f);
			animationRoot.GlobalPosition = insertStartGlobalPosition;
			animationRoot.RotationDegrees = 0.0f;
			Tween insertTween = CreateTween();
			insertTween.SetTrans(Tween.TransitionType.Sine);
			insertTween.SetEase(Tween.EaseType.Out);
			insertTween.TweenMethod(
				Callable.From<float>(progress => UpdateQuestionCardRewardInsert(
					animationRoot,
					fx,
					_questionCardTransformFrames,
					insertStartGlobalPosition,
					targetGlobalPosition,
					progress)),
				0.0f,
				1.0f,
				QuestionCardRewardInsertSeconds);

			await ToSignal(insertTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(animationRoot))
			{
				animationRoot.QueueFree();
			}
		}
	}

	private Vector2 GetQuestionCardRewardTargetGlobalPosition(int playerIndex, QuestionCardRewardAnimation reward)
	{
		if (playerIndex < 0 || playerIndex >= _playerHandAreas.Count || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return reward.StartGlobalPosition;
		}

		Control handArea = _playerHandAreas[playerIndex];
		if (reward.RequiresMagnetAttachment)
		{
			return handArea.GlobalPosition + (handArea.Size - CardVisualSize) / 2.0f;
		}

		int displayCardCount = _playerHands[playerIndex].Count + _playerItemHands[playerIndex].Count;
		int displayIndex = _playerHands[playerIndex].Count + reward.ItemHandIndex;
		if (displayCardCount <= 0 || displayIndex < 0 || displayIndex >= displayCardCount)
		{
			return handArea.GlobalPosition + (handArea.Size - CardVisualSize) / 2.0f;
		}

		Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
		return handArea.GlobalPosition + firstCardPosition + displayIndex * cardOffset;
	}

	private Vector2 GetQuestionCardTransformAreaCardGlobalPosition(Vector2 fallbackGlobalPosition)
	{
		if (_questionCardTransformArea == null || !GodotObject.IsInstanceValid(_questionCardTransformArea))
		{
			return fallbackGlobalPosition;
		}

		return _questionCardTransformArea.GlobalPosition + (_questionCardTransformArea.Size - CardVisualSize) / 2.0f;
	}

	private static void UpdateQuestionCardToTransformAreaFlight(
		Control animationRoot,
		TextureRect fx,
		IReadOnlyList<Texture2D> fxFrames,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(animationRoot))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * TableCaptureFlightArcHeight;
		animationRoot.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		animationRoot.Scale = Vector2.One * Mathf.Lerp(0.92f, 1.08f, Mathf.Sin(clampedProgress * Mathf.Pi));
		animationRoot.RotationDegrees = Mathf.Lerp(0.0f, 18.0f, Mathf.Sin(clampedProgress * Mathf.Pi));

		UpdateQuestionCardFx(fx, fxFrames, clampedProgress * 0.35f, Mathf.Lerp(0.0f, 0.42f, clampedProgress));
	}

	private static void UpdateQuestionCardTransform(
		Control animationRoot,
		TextureRect card,
		TextureRect fx,
		IReadOnlyList<Texture2D> fxFrames,
		Texture2D questionTexture,
		Texture2D rewardTexture,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(animationRoot) || !GodotObject.IsInstanceValid(card))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		bool showingReward = clampedProgress >= 0.5f;
		float scale = clampedProgress < 0.48f
			? Mathf.Lerp(1.0f, 1.32f, clampedProgress / 0.48f)
			: Mathf.Lerp(1.32f, 1.04f, (clampedProgress - 0.48f) / 0.52f);
		float flipDistance = Mathf.Abs(clampedProgress - 0.5f) / 0.5f;

		animationRoot.Scale = new Vector2(scale, scale);
		animationRoot.RotationDegrees = Mathf.Lerp(0.0f, 360.0f, easedProgress);
		card.Texture = showingReward ? rewardTexture : questionTexture;
		card.Scale = new Vector2(Mathf.Lerp(0.12f, 1.0f, flipDistance), 1.0f);

		UpdateQuestionCardFx(fx, fxFrames, clampedProgress, 1.0f);
	}

	private static void UpdateQuestionCardRewardInsert(
		Control animationRoot,
		TextureRect fx,
		IReadOnlyList<Texture2D> fxFrames,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(animationRoot))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		animationRoot.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, easedProgress);
		animationRoot.Scale = Vector2.One * Mathf.Lerp(1.04f, 1.0f, easedProgress);
		animationRoot.RotationDegrees = 0.0f;

		UpdateQuestionCardFx(fx, fxFrames, clampedProgress, Mathf.Lerp(0.75f, 0.0f, easedProgress));
	}

	private static void UpdateQuestionCardFx(TextureRect fx, IReadOnlyList<Texture2D> fxFrames, float progress, float alpha)
	{
		if (!GodotObject.IsInstanceValid(fx) || fxFrames.Count == 0)
		{
			return;
		}

		int frameIndex = Mathf.Clamp(Mathf.FloorToInt(progress * fxFrames.Count), 0, fxFrames.Count - 1);
		fx.Texture = fxFrames[frameIndex];
		fx.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
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

	private partial class SpecialCardTriggerRing : Control
	{
		private float _ringProgress;
		private float _ringAlpha;

		public float ring_progress
		{
			get => _ringProgress;
			set
			{
				_ringProgress = Mathf.Clamp(value, 0.0f, 1.0f);
				QueueRedraw();
			}
		}

		public float ring_alpha
		{
			get => _ringAlpha;
			set
			{
				_ringAlpha = Mathf.Clamp(value, 0.0f, 1.0f);
				QueueRedraw();
			}
		}

		public override void _Draw()
		{
			if (_ringAlpha <= 0.0f)
			{
				return;
			}

			Vector2 center = Size / 2.0f;
			float radius = Mathf.Lerp(42.0f, 78.0f, _ringProgress);
			float outerAlpha = 0.38f * _ringAlpha;
			float innerAlpha = 0.95f * _ringAlpha;
			DrawArc(center, radius + 8.0f, 0.0f, FullCircleRadians, 96, new Color(0.48f, 1.0f, 0.92f, outerAlpha), 14.0f, true);
			DrawArc(center, radius, 0.0f, FullCircleRadians, 96, new Color(1.0f, 0.96f, 0.42f, innerAlpha), 6.0f, true);
			DrawArc(center, radius - 7.0f, 0.0f, FullCircleRadians, 96, new Color(1.0f, 1.0f, 1.0f, 0.55f * _ringAlpha), 2.5f, true);
		}
	}

}
