using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
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
			if (reward.RequiresMagnetAttachment)
			{
				return;
			}

			Vector2 insertStartGlobalPosition = transformAreaGlobalPosition;
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

	private async Task PlayQuestionCardRefreshAnimationAsync(int tableCardIndex)
	{
		if (!IsInsideTree() || tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.ItemOverlay.HasValue || tableCard.ItemOverlay.Value.Id != QuestionMarkItemCardId)
		{
			return;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= cardPositions.Count)
		{
			return;
		}

		List<Texture2D> refreshFrames = LoadQuestionCardRefreshFrames();
		Texture2D questionTexture = tableCard.ItemOverlay.Value.Texture;
		var animationRoot = new Control
		{
			Name = "QuestionCardRefreshAnimation",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = cardPositions[tableCardIndex],
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = tableCardIndex * 2 + 120,
		};
		var fx = new TextureRect
		{
			Name = "QuestionCardRefreshFx",
			Texture = refreshFrames.Count > 0 ? refreshFrames[0] : null,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardVisualSize - QuestionCardRefreshFxVisualSize) / 2.0f,
			Size = QuestionCardRefreshFxVisualSize,
			PivotOffset = QuestionCardRefreshFxVisualSize / 2.0f,
			ZIndex = 0,
			Visible = refreshFrames.Count > 0,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		var card = new TextureRect
		{
			Name = "QuestionCardRefreshCard",
			Texture = questionTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = GetItemCardVisualPosition(QuestionMarkItemCardId),
			Size = GetItemCardVisualSize(QuestionMarkItemCardId),
			PivotOffset = GetItemCardVisualSize(QuestionMarkItemCardId) / 2.0f,
			ZIndex = 1,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			Scale = Vector2.One * 0.82f,
		};

		animationRoot.AddChild(fx);
		animationRoot.AddChild(card);
		_centerArena.AddChild(animationRoot);

		try
		{
			UpdateQuestionCardRefreshAnimation(animationRoot, card, fx, refreshFrames, 0.0f);
			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateQuestionCardRefreshAnimation(animationRoot, card, fx, refreshFrames, progress)),
				0.0f,
				1.0f,
				QuestionCardRefreshSeconds);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(animationRoot))
			{
				animationRoot.QueueFree();
			}
		}
	}

	private static void UpdateQuestionCardRefreshAnimation(
		Control animationRoot,
		TextureRect card,
		TextureRect fx,
		IReadOnlyList<Texture2D> fxFrames,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(animationRoot) || !GodotObject.IsInstanceValid(card))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float cardAlpha = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((clampedProgress - 0.18f) / 0.52f, 0.0f, 1.0f));
		float cardPop = Mathf.Sin(Mathf.Clamp(clampedProgress / 0.82f, 0.0f, 1.0f) * Mathf.Pi);
		card.Modulate = new Color(1.0f, 1.0f, 1.0f, cardAlpha);
		card.Scale = Vector2.One * (Mathf.Lerp(0.82f, 1.0f, easedProgress) + cardPop * 0.08f);
		card.RotationDegrees = Mathf.Lerp(-5.0f, 0.0f, easedProgress);
		animationRoot.Scale = Vector2.One * Mathf.Lerp(0.96f, 1.0f, easedProgress);

		if (!GodotObject.IsInstanceValid(fx) || fxFrames.Count == 0)
		{
			return;
		}

		int frameIndex = Mathf.Clamp(Mathf.FloorToInt(clampedProgress * fxFrames.Count), 0, fxFrames.Count - 1);
		float fxAlphaIn = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp(clampedProgress / 0.2f, 0.0f, 1.0f));
		float fxAlphaOut = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((clampedProgress - 0.68f) / 0.32f, 0.0f, 1.0f));
		fx.Texture = fxFrames[frameIndex];
		fx.Modulate = new Color(1.0f, 1.0f, 1.0f, fxAlphaIn * fxAlphaOut);
		fx.Scale = Vector2.One * Mathf.Lerp(0.9f, 1.08f, Mathf.Sin(clampedProgress * Mathf.Pi));
	}

	private static List<Texture2D> LoadQuestionCardRefreshFrames()
	{
		var frames = new List<Texture2D>();
		for (int frameIndex = 1; frameIndex <= QuestionCardRefreshFrameCount; frameIndex++)
		{
			string path = string.Format(QuestionCardRefreshFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing question card refresh frame: {path}");
				continue;
			}

			frames.Add(texture);
		}

		return frames;
	}
}
