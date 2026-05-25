using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayLensCardSplitAnimationAsync(
		Texture2D lensTexture,
		Vector2 startGlobalPosition,
		Vector2 splitGlobalCenter,
		float startRotationDegrees)
	{
		if (!IsInsideTree())
		{
			return;
		}

		var flightCard = new TextureRect
		{
			Name = "LensCardFlight",
			Texture = lensTexture,
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

		Vector2 splitGlobalPosition = splitGlobalCenter - CardVisualSize / 2.0f;
		try
		{
			Tween flightTween = CreateTween();
			flightTween.SetParallel(true);
			flightTween.SetTrans(Tween.TransitionType.Sine);
			flightTween.SetEase(Tween.EaseType.InOut);
			flightTween.TweenMethod(
				Callable.From<float>(progress => UpdateLensCardFlight(
					flightCard,
					startGlobalPosition,
					splitGlobalPosition,
					startRotationDegrees,
					progress)),
				0.0f,
				1.0f,
				LensCardFlightSeconds);
			TweenFlightPulseScale(flightTween, flightCard, LensCardFlightSeconds, 1.12f);
			await ToSignal(flightTween, Tween.SignalName.Finished);

			if (!GodotObject.IsInstanceValid(flightCard))
			{
				return;
			}

			Tween splitTween = CreateTween();
			splitTween.SetParallel(true);
			splitTween.SetTrans(Tween.TransitionType.Cubic);
			splitTween.SetEase(Tween.EaseType.In);
			splitTween.TweenProperty(flightCard, "scale", new Vector2(0.12f, 1.22f), LensCardSplitSeconds);
			flightCard.Modulate = new Color(0.7f, 1.0f, 0.96f, 1.0f);
			splitTween.TweenProperty(flightCard, "modulate:a", 0.0f, LensCardSplitSeconds);
			splitTween.TweenProperty(flightCard, "rotation_degrees", 0.0f, LensCardSplitSeconds);
			await ToSignal(splitTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(flightCard))
			{
				flightCard.QueueFree();
			}
		}
	}

	private static void UpdateLensCardFlight(
		TextureRect flightCard,
		Vector2 startGlobalPosition,
		Vector2 splitGlobalPosition,
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
		flightCard.GlobalPosition = startGlobalPosition.Lerp(splitGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		flightCard.RotationDegrees = Mathf.Lerp(startRotationDegrees, 0.0f, easedProgress) + pulse * 10.0f;
	}

	private async Task PlayLensRevealWispFlightAsync(Vector2 startGlobalCenter, Vector2 targetGlobalCenter, int targetNumber, int tableCardIndex)
	{
		if (!IsInsideTree() || _lensRevealWispFrames.Count == 0)
		{
			return;
		}

		if (targetNumber > 0)
		{
			await ToSignal(GetTree().CreateTimer(LensRevealWispStaggerSeconds * targetNumber), SceneTreeTimer.SignalName.Timeout);
		}

		var wisp = new TextureRect
		{
			Name = "LensRevealWisp",
			Texture = _lensRevealWispFrames[0],
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = LensRevealWispVisualSize,
			PivotOffset = LensRevealWispVisualSize / 2.0f,
			ZIndex = PlayedCardFlightZIndex + tableCardIndex * 2 + 720 + targetNumber,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			Scale = Vector2.One * 0.72f,
		};
		AddChild(wisp);
		wisp.GlobalPosition = startGlobalCenter - LensRevealWispVisualSize / 2.0f;

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			float sideOffset = targetNumber % 2 == 0 ? -18.0f : 18.0f;
			tween.TweenMethod(
			Callable.From<float>(progress => UpdateLensRevealWispFlight(
				wisp,
				startGlobalCenter,
				targetGlobalCenter,
				sideOffset,
				targetNumber,
				progress)),
				0.0f,
				1.0f,
				LensRevealWispFlightSeconds);
			tween.TweenProperty(wisp, "scale", Vector2.One * 1.08f, LensRevealWispFlightSeconds)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(wisp, "modulate:a", 1.0f, LensRevealWispFlightSeconds * 0.16f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(wisp, "modulate:a", 0.0f, LensRevealWispFlightSeconds * 0.22f)
				.SetDelay(LensRevealWispFlightSeconds * 0.78f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				wisp,
				_lensRevealWispFrames,
				LensRevealWispFrameSeconds,
				LensRevealWispFlightSeconds);
			tween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(wisp))
				{
					wisp.QueueFree();
				}
			};
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(wisp))
			{
				wisp.QueueFree();
			}
		}
	}

	private static void UpdateLensRevealWispFlight(
		TextureRect wisp,
		Vector2 startGlobalCenter,
		Vector2 targetGlobalCenter,
		float sideOffset,
		int targetNumber,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(wisp))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		Vector2 baseCenter = startGlobalCenter.Lerp(targetGlobalCenter, easedProgress);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * LensRevealWispArcHeight;
		float swirl = Mathf.Sin(clampedProgress * Mathf.Pi * 2.0f + targetNumber * Mathf.Pi * 0.42f) * sideOffset;

		wisp.GlobalPosition = baseCenter + new Vector2(swirl, arcY) - LensRevealWispVisualSize / 2.0f;
		wisp.RotationDegrees = Mathf.Lerp(-22.0f, 32.0f, clampedProgress) * (targetNumber % 2 == 0 ? 1.0f : -1.0f);
	}
}
