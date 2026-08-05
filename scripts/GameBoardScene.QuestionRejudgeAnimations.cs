using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayQuestionCardRewardRejudgeAnimationAsync(
		int playerIndex,
		Rect2 previewGlobalRect,
		AdventureQuestionRejudgeOverlay? sourceOverlay,
		QuestionCardRewardAnimation grantedReward,
		ItemCard rerolledItem,
		int targetItemHandIndex,
		bool rerolledRequiresAttachment,
		int turnSequenceGeneration)
	{
		if (!IsLocalHandPlayer(playerIndex) || !IsInsideTree())
		{
			return;
		}

		Vector2 fallbackStartSize = CardVisualSize * 2.1f;
		Vector2 startSize = previewGlobalRect.Size == Vector2.Zero ? fallbackStartSize : previewGlobalRect.Size;
		Vector2 startGlobalPosition = previewGlobalRect.Size == Vector2.Zero
			? GetQuestionCardTransformAreaCardGlobalPosition(grantedReward.StartGlobalPosition) - startSize / 2.0f
			: previewGlobalRect.Position;
		Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(grantedReward.StartGlobalPosition);
		Vector2 targetGlobalPosition = rerolledRequiresAttachment
			? _playerHandAreas[playerIndex].GlobalPosition + (_playerHandAreas[playerIndex].Size - CardVisualSize) / 2.0f
			: GetQuestionCardRewardTargetGlobalPosition(
				playerIndex,
				new QuestionCardRewardAnimation(rerolledItem, targetItemHandIndex, grantedReward.StartGlobalPosition));

		Control animationRoot = _animationPresenter.RentControl(
			this,
			"QuestionCardRewardRejudge",
			startSize,
			PlayedCardFlightZIndex + 950);
		TextureRect fx = _animationPresenter.RentTextureRect(
			animationRoot,
			"QuestionCardRejudgeTransformFx",
			_questionCardTransformFrames.Count > 0 ? _questionCardTransformFrames[0] : null,
			QuestionCardTransformFxVisualSize,
			0,
			CanvasItem.TextureFilterEnum.Linear);
		fx.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		fx.Position = (startSize - QuestionCardTransformFxVisualSize) / 2.0f;
		fx.Visible = _questionCardTransformFrames.Count > 0;
		TextureRect card = _animationPresenter.RentTextureRect(
			animationRoot,
			"QuestionCardRewardRejudgeCard",
			GetItemCardTexture(grantedReward.ItemCard),
			startSize,
			1,
			CanvasItem.TextureFilterEnum.Nearest);
		card.Position = Vector2.Zero;
		animationRoot.GlobalPosition = startGlobalPosition;
		animationRoot.Visible = false;

		try
		{
			animationRoot.Visible = true;
			if (sourceOverlay != null && GodotObject.IsInstanceValid(sourceOverlay))
			{
				sourceOverlay.Visible = false;
			}
			DisposeActiveSelectionOverlay();

			PlayGameSfx(GameSfx.QuestionRewardInsert, -2.0f, 0.97f, 1.04f, 0.08);
			Tween moveToTransformTween = CreateBoardTween();
			moveToTransformTween.SetParallel(true);
			moveToTransformTween.SetTrans(Tween.TransitionType.Cubic);
			moveToTransformTween.SetEase(Tween.EaseType.Out);
			moveToTransformTween.TweenProperty(
				animationRoot,
				"global_position",
				transformAreaGlobalPosition,
				QuestionCardToTransformAreaFlightSeconds);
			moveToTransformTween.TweenProperty(
				animationRoot,
				"size",
				CardVisualSize,
				QuestionCardToTransformAreaFlightSeconds);
			moveToTransformTween.TweenProperty(
				card,
				"size",
				CardVisualSize,
				QuestionCardToTransformAreaFlightSeconds);
			moveToTransformTween.TweenProperty(
				fx,
				"position",
				(CardVisualSize - QuestionCardTransformFxVisualSize) / 2.0f,
				QuestionCardToTransformAreaFlightSeconds);
			moveToTransformTween.TweenProperty(fx, "modulate:a", 0.42f, QuestionCardToTransformAreaFlightSeconds);

			await WaitForBoardTweenAsync(moveToTransformTween);
			if (!GodotObject.IsInstanceValid(animationRoot) || turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			animationRoot.GlobalPosition = transformAreaGlobalPosition;
			animationRoot.Size = CardVisualSize;
			card.Size = CardVisualSize;
			card.Position = Vector2.Zero;
			card.PivotOffset = CardVisualSize / 2.0f;
			PlayGameSfx(GameSfx.QuestionRewardTransform, -1.0f, 0.97f, 1.03f, 0.08);

			Tween transformTween = CreateBoardTween();
			transformTween.SetParallel(true);
			transformTween.SetTrans(Tween.TransitionType.Sine);
			transformTween.SetEase(Tween.EaseType.InOut);
			transformTween.TweenProperty(card, "scale", new Vector2(0.12f, 1.0f), QuestionCardTransformSeconds * 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			transformTween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(card))
				{
					PlayGameSfx(GameSfx.QuestionRewardReveal, -1.0f, 0.98f, 1.03f, 0.08);
					card.Texture = GetItemCardTexture(rerolledItem);
				}
			})).SetDelay(QuestionCardTransformSeconds * 0.5f);
			transformTween.TweenProperty(card, "scale", Vector2.One, QuestionCardTransformSeconds * 0.5f)
				.SetDelay(QuestionCardTransformSeconds * 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			transformTween.TweenProperty(fx, "modulate:a", 1.0f, QuestionCardTransformSeconds);

			await WaitForBoardTweenAsync(transformTween);
			if (!GodotObject.IsInstanceValid(animationRoot) || turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			if (rerolledRequiresAttachment)
			{
				return;
			}

			float rewardVisualScale = GetHandDisplayCardVisualScale(playerIndex, _playerHands[playerIndex].Count + targetItemHandIndex);
			Vector2 rewardVisualSize = CardVisualSize * rewardVisualScale;
			card.Position = GetCardVisualBasePosition(rewardVisualScale);
			card.Size = rewardVisualSize;
			card.PivotOffset = rewardVisualSize / 2.0f;
			animationRoot.Size = rewardVisualSize;
			PlayGameSfx(GameSfx.QuestionRewardInsert, -2.0f, 0.97f, 1.04f, 0.08);

			Tween insertTween = CreateBoardTween();
			insertTween.SetParallel(true);
			insertTween.SetTrans(Tween.TransitionType.Sine);
			insertTween.SetEase(Tween.EaseType.Out);
			insertTween.TweenMethod(
				Callable.From<float>(progress => UpdateQuestionCardRewardInsert(
					animationRoot,
					transformAreaGlobalPosition,
					targetGlobalPosition,
					progress)),
				0.0f,
				1.0f,
				QuestionCardRewardInsertSeconds);
			insertTween.TweenProperty(animationRoot, "scale", Vector2.One, QuestionCardRewardInsertSeconds);
			insertTween.TweenProperty(fx, "modulate:a", 0.0f, QuestionCardRewardInsertSeconds);

			await WaitForBoardTweenAsync(insertTween);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(animationRoot))
			{
				_animationPresenter.Release(animationRoot);
			}
		}
	}
}
