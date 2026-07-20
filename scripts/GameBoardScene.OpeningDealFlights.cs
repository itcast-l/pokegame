using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayOpeningDealCardFlightAsync(
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		int playerIndex,
		int dealNumber,
		float targetVisualScale)
	{
		if (_cardBackTexture == null || !IsInsideTree())
		{
			return;
		}

		PlayGameSfx(GameSfx.CardDeal, -4.0f, 0.94f, 1.08f, 0.03);
		float clampedTargetVisualScale = Mathf.Max(0.01f, targetVisualScale);
		Vector2 targetVisualSize = CardVisualSize * clampedTargetVisualScale;
		Vector2 targetVisualBasePosition = GetCardVisualBasePosition(clampedTargetVisualScale);
		float startRotationDegrees = -8.0f + dealNumber % 5 * 4.0f;
		Control cardRoot = _animationPresenter.RentControl(
			this,
			"OpeningDealCardFlight",
			CardVisualSize,
			OpeningDealZIndex + dealNumber);
		TextureRect cardVisual = _animationPresenter.RentTextureRect(
			cardRoot,
			"OpeningDealCardVisual",
			_cardBackTexture,
			targetVisualSize,
			0,
			CanvasItem.TextureFilterEnum.Nearest);
		cardVisual.Position = targetVisualBasePosition;
		cardVisual.RotationDegrees = startRotationDegrees;
		MarkAnimatedCardBack(cardVisual);
		cardRoot.GlobalPosition = startGlobalPosition;

		try
		{
			Tween tween = CreateBoardTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateOpeningDealSlotCardFlight(
					cardRoot,
					cardVisual,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					GetCardRotationDegreesForPlayer(playerIndex),
					progress)),
				0.0f,
				1.0f,
				OpeningDealCardFlightSeconds);
			await WaitForBoardTweenAsync(tween);
			await WaitForBoardDelayAsync(OpeningDealCardStaggerSeconds);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(cardRoot))
			{
				_animationPresenter.Release(cardRoot);
			}
		}
	}

	private async Task PlayOpeningDealPileCardFlightAsync(Vector2 startGlobalPosition, Vector2 targetGlobalPosition, int playerIndex, int dealIndex, float flightSeconds)
	{
		if (_cardBackTexture == null || !IsInsideTree())
		{
			return;
		}

		PlayGameSfx(GameSfx.CardDeal, -5.0f, 0.92f, 1.06f, 0.03);
		float startRotationDegrees = -6.0f + dealIndex % 4 * 4.0f;
		Control cardRoot = _animationPresenter.RentControl(
			this,
			"OpeningPileCardFlight",
			CardVisualSize,
			OpeningDealZIndex + 200 + dealIndex);
		TextureRect cardVisual = _animationPresenter.RentTextureRect(
			cardRoot,
			"OpeningPileCardVisual",
			_cardBackTexture,
			CardVisualSize,
			0,
			CanvasItem.TextureFilterEnum.Nearest);
		cardVisual.RotationDegrees = startRotationDegrees;
		MarkAnimatedCardBack(cardVisual);
		cardRoot.GlobalPosition = startGlobalPosition;

		try
		{
			Tween tween = CreateBoardTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateOpeningDealSlotCardFlight(
					cardRoot,
					cardVisual,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					GetCardPileRotationDegreesForPlayer(playerIndex),
					progress)),
				0.0f,
				1.0f,
				flightSeconds);
			await WaitForBoardTweenAsync(tween);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(cardRoot))
			{
				_animationPresenter.Release(cardRoot);
			}
		}
	}

	private static void UpdateOpeningDealSlotCardFlight(
		Control cardRoot,
		TextureRect cardVisual,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(cardRoot) || !GodotObject.IsInstanceValid(cardVisual))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * 34.0f;
		float pulse = Mathf.Sin(clampedProgress * Mathf.Pi);
		cardRoot.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		cardVisual.RotationDegrees = Mathf.Lerp(startRotationDegrees, targetRotationDegrees, easedProgress) + pulse * 8.0f;
	}
}
