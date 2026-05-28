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
			if (rewardAnimation.HasValue)
			{
				ShowQuestionCardRewardHandSlot(playerIndex, rewardAnimation.Value);
			}

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
				reward.StartGlobalPosition,
				transformAreaGlobalPosition,
				0.0f);

			Tween areaFlightTween = CreateTween();
			areaFlightTween.SetParallel(true);
			areaFlightTween.SetTrans(Tween.TransitionType.Sine);
			areaFlightTween.SetEase(Tween.EaseType.InOut);
			areaFlightTween.TweenMethod(
				Callable.From<float>(progress => UpdateQuestionCardToTransformAreaFlight(
					animationRoot,
					reward.StartGlobalPosition,
					transformAreaGlobalPosition,
					progress)),
				0.0f,
				1.0f,
				QuestionCardToTransformAreaFlightSeconds);
			areaFlightTween.TweenProperty(animationRoot, "scale", Vector2.One * 1.08f, QuestionCardToTransformAreaFlightSeconds * 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			areaFlightTween.TweenProperty(animationRoot, "scale", Vector2.One, QuestionCardToTransformAreaFlightSeconds * 0.5f)
				.SetDelay(QuestionCardToTransformAreaFlightSeconds * 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			areaFlightTween.TweenProperty(fx, "modulate:a", 0.42f, QuestionCardToTransformAreaFlightSeconds)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				areaFlightTween,
				fx,
				_questionCardTransformFrames,
				QuestionCardToTransformAreaFlightSeconds / Math.Max(1, _questionCardTransformFrames.Count),
				QuestionCardToTransformAreaFlightSeconds);

			await ToSignal(areaFlightTween, Tween.SignalName.Finished);
			if (!GodotObject.IsInstanceValid(animationRoot) || turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			animationRoot.GlobalPosition = transformAreaGlobalPosition;
			Tween transformTween = CreateTween();
			transformTween.SetParallel(true);
			transformTween.SetTrans(Tween.TransitionType.Sine);
			transformTween.SetEase(Tween.EaseType.InOut);
			transformTween.TweenMethod(
				Callable.From<float>(progress => UpdateQuestionCardTransform(animationRoot, progress)),
				0.0f,
				1.0f,
				QuestionCardTransformSeconds);
			float transformPeakDelay = QuestionCardTransformSeconds * 0.48f;
			transformTween.TweenProperty(animationRoot, "scale", Vector2.One * 1.32f, transformPeakDelay)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			transformTween.TweenProperty(animationRoot, "scale", Vector2.One * 1.04f, QuestionCardTransformSeconds - transformPeakDelay)
				.SetDelay(transformPeakDelay)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			float cardFlipDelay = QuestionCardTransformSeconds * 0.5f;
			transformTween.TweenProperty(card, "scale", new Vector2(0.12f, 1.0f), cardFlipDelay)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			transformTween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(card))
				{
					card.Texture = reward.ItemCard.Texture;
				}
			})).SetDelay(cardFlipDelay);
			transformTween.TweenProperty(card, "scale", Vector2.One, QuestionCardTransformSeconds - cardFlipDelay)
				.SetDelay(cardFlipDelay)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			transformTween.TweenProperty(fx, "modulate:a", 1.0f, QuestionCardTransformSeconds)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				transformTween,
				fx,
				_questionCardTransformFrames,
				QuestionCardTransformSeconds / Math.Max(1, _questionCardTransformFrames.Count),
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
			animationRoot.Scale = Vector2.One * 1.04f;
			fx.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.75f);
			Tween insertTween = CreateTween();
			insertTween.SetParallel(true);
			insertTween.SetTrans(Tween.TransitionType.Sine);
			insertTween.SetEase(Tween.EaseType.Out);
			insertTween.TweenMethod(
				Callable.From<float>(progress => UpdateQuestionCardRewardInsert(
					animationRoot,
					insertStartGlobalPosition,
					targetGlobalPosition,
					progress)),
				0.0f,
				1.0f,
				QuestionCardRewardInsertSeconds);
			insertTween.TweenProperty(animationRoot, "scale", Vector2.One, QuestionCardRewardInsertSeconds)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			insertTween.TweenProperty(fx, "modulate:a", 0.0f, QuestionCardRewardInsertSeconds)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				insertTween,
				fx,
				_questionCardTransformFrames,
				QuestionCardRewardInsertSeconds / Math.Max(1, _questionCardTransformFrames.Count),
				QuestionCardRewardInsertSeconds);

			await ToSignal(insertTween, Tween.SignalName.Finished);
		}
		finally
		{
			ShowQuestionCardRewardHandSlot(playerIndex, reward);

			if (GodotObject.IsInstanceValid(animationRoot))
			{
				animationRoot.QueueFree();
			}
		}
	}

	private void ShowQuestionCardRewardHandSlot(int playerIndex, QuestionCardRewardAnimation reward)
	{
		if (reward.RequiresMagnetAttachment)
		{
			ClearHiddenHandDisplaySlots(playerIndex);
			return;
		}

		ShowHandDisplaySlot(playerIndex, _playerHands[playerIndex].Count + reward.ItemHandIndex);
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
		animationRoot.RotationDegrees = Mathf.Lerp(0.0f, 18.0f, Mathf.Sin(clampedProgress * Mathf.Pi));
	}

	private static void UpdateQuestionCardTransform(
		Control animationRoot,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(animationRoot))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		animationRoot.RotationDegrees = Mathf.Lerp(0.0f, 360.0f, easedProgress);
	}

	private static void UpdateQuestionCardRewardInsert(
		Control animationRoot,
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
		animationRoot.RotationDegrees = 0.0f;
	}

	private async Task PlayQuestionCardRefreshAnimationAsync(int tableCardIndex)
	{
		if (!IsInsideTree() || tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.ItemOverlay.HasValue || !IsQuestionTableCard(tableCard))
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return;
		}

		Texture2D questionTexture = tableCard.ItemOverlay.Value.Texture;
		var animationRoot = new Control
		{
			Name = "QuestionCardRefreshAnimation",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = cardPosition,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = tableCardIndex * 2 + 120,
		};
		var fx = new TextureRect
		{
			Name = "QuestionCardRefreshFx",
			Texture = QuestionCardRefreshFrames.Length > 0 ? QuestionCardRefreshFrames[0] : null,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardVisualSize - QuestionCardRefreshFxVisualSize) / 2.0f,
			Size = QuestionCardRefreshFxVisualSize,
			PivotOffset = QuestionCardRefreshFxVisualSize / 2.0f,
			ZIndex = 0,
			Visible = QuestionCardRefreshFrames.Length > 0,
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
			card.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
			card.Scale = Vector2.One * 0.82f;
			card.RotationDegrees = -5.0f;
			fx.Scale = Vector2.One * 0.9f;
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(card, "modulate:a", 1.0f, QuestionCardRefreshSeconds * 0.52f)
				.SetDelay(QuestionCardRefreshSeconds * 0.18f);
			tween.TweenProperty(card, "scale", Vector2.One * 1.08f, QuestionCardRefreshSeconds * 0.42f);
			tween.TweenProperty(card, "scale", Vector2.One, QuestionCardRefreshSeconds * 0.40f)
				.SetDelay(QuestionCardRefreshSeconds * 0.42f);
			tween.TweenProperty(card, "rotation_degrees", 0.0f, QuestionCardRefreshSeconds);
			tween.TweenProperty(animationRoot, "scale", Vector2.One, QuestionCardRefreshSeconds);
			tween.TweenProperty(fx, "modulate:a", 1.0f, QuestionCardRefreshSeconds * 0.2f);
			tween.TweenProperty(fx, "modulate:a", 0.0f, QuestionCardRefreshSeconds * 0.32f)
				.SetDelay(QuestionCardRefreshSeconds * 0.68f);
			tween.TweenProperty(fx, "scale", Vector2.One * 1.08f, QuestionCardRefreshSeconds);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				fx,
				QuestionCardRefreshFrames,
				QuestionCardRefreshSeconds / Math.Max(1, QuestionCardRefreshFrames.Length),
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

}
