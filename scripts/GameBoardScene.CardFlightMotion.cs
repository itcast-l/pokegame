using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
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

	private static void TweenStolenHandCardScale(
		Tween tween,
		Control flightCard,
		float duration,
		float targetVisualScale)
	{
		float halfDuration = duration * 0.5f;
		tween.TweenProperty(flightCard, "scale", Vector2.One * targetVisualScale * StolenCardFlightPeakScale, halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(flightCard, "scale", Vector2.One * targetVisualScale, duration - halfDuration)
			.SetDelay(halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
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
