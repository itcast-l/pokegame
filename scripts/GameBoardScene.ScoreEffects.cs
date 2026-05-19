using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const float ScoreFillEffectFlightSeconds = 0.62f;
	private const float ScoreFillEffectStaggerSeconds = 0.07f;
	private const int MaxScoreFillEffectFlights = 12;
	private const float ScoreFillEffectVisualSize = 64.0f;
	private const float ScoreFillPulseVisualSize = 92.0f;
	private const int ScoreFillEffectTravelZIndex = PlayedCardFlightZIndex + 850;
	private const int ScoreFillEffectBehindAvatarZIndex = 2;
	private const float ScoreFillEffectBehindAvatarProgress = 0.72f;

	private async Task PlayScoreFillCollectionAsync(
		int playerIndex,
		IReadOnlyList<CapturedTableCardVisual> capturedCardVisuals,
		int turnSequenceGeneration,
		Action? onArrival = null)
	{
		if (_scoreFillEffectFrames.Count == 0 || capturedCardVisuals.Count == 0)
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onArrival?.Invoke();
			}

			return;
		}

		Vector2 targetCenter = GetScoreMeterGlobalCenter(playerIndex);
		var flightTasks = new List<Task>();
		bool scoreAwarded = false;
		for (int visualIndex = 0; visualIndex < capturedCardVisuals.Count && flightTasks.Count < MaxScoreFillEffectFlights; visualIndex++)
		{
			Vector2 cardCenter = capturedCardVisuals[visualIndex].GlobalPosition + CardVisualSize / 2.0f;
			flightTasks.Add(PlayScoreFillEffectFlightAsync(
				cardCenter,
				targetCenter,
				flightTasks.Count,
				visualIndex * ScoreFillEffectStaggerSeconds,
				() =>
				{
					if (scoreAwarded || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return;
					}

					scoreAwarded = true;
					onArrival?.Invoke();
				}));
		}

		await Task.WhenAll(flightTasks);
		if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
		{
			if (!scoreAwarded)
			{
				onArrival?.Invoke();
			}

			await PlayScoreFillArrivalPulseAsync(targetCenter);
		}
	}

	private Vector2 GetScoreMeterGlobalCenter(int playerIndex)
	{
		if (playerIndex >= 0
			&& playerIndex < _playerScoreEffectTargets.Length
			&& _playerScoreEffectTargets[playerIndex] is ScoreEffectTarget target
			&& GodotObject.IsInstanceValid(target))
		{
			return target.MarkerGlobalPosition;
		}

		if (playerIndex >= 0
			&& playerIndex < _playerScoreMeters.Length
			&& _playerScoreMeters[playerIndex] is ScoreAvatarMeter meter
			&& GodotObject.IsInstanceValid(meter))
		{
			return meter.GetGlobalRect().GetCenter();
		}

		if (playerIndex >= 0 && playerIndex < _playerCardPiles.Count && GodotObject.IsInstanceValid(_playerCardPiles[playerIndex]))
		{
			return _playerCardPiles[playerIndex].GlobalPosition + CardPileSize / 2.0f;
		}

		return Size / 2.0f;
	}

	private async Task PlayScoreFillEffectFlightAsync(
		Vector2 cardCenter,
		Vector2 targetCenter,
		int effectIndex,
		float startDelaySeconds,
		Action onTouchTarget)
	{
		if (startDelaySeconds > 0.0f)
		{
			await ToSignal(GetTree().CreateTimer(startDelaySeconds), SceneTreeTimer.SignalName.Timeout);
		}

		var effect = new Sprite2D
		{
			Name = "ScoreFillEffectFlight",
			Texture = _scoreFillEffectFrames[0],
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = ScoreFillEffectTravelZIndex + effectIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(effect);

		Vector2 startCenter = cardCenter + RandomUnitVector() * _rng.RandfRange(4.0f, 16.0f);
		Vector2 travel = targetCenter - startCenter;
		Vector2 side = travel.LengthSquared() > 0.001f
			? new Vector2(-travel.Y, travel.X).Normalized()
			: Vector2.Zero;
		float arcHeight = _rng.RandfRange(58.0f, 96.0f);
		float sideDrift = _rng.RandfRange(-34.0f, 34.0f);
		Vector2 controlCenter = (startCenter + targetCenter) / 2.0f
			+ new Vector2(0.0f, -arcHeight)
			+ side * sideDrift;

		try
		{
			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateScoreFillEffectFlight(
					effect,
					_scoreFillEffectFrames,
					startCenter,
					controlCenter,
					targetCenter,
					ScoreFillEffectTravelZIndex + effectIndex,
					progress)),
				0.0f,
				1.0f,
				ScoreFillEffectFlightSeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
			onTouchTarget.Invoke();
		}
		finally
		{
			if (GodotObject.IsInstanceValid(effect))
			{
				effect.QueueFree();
			}
		}
	}

	private async Task PlayScoreFillArrivalPulseAsync(Vector2 targetCenter)
	{
		if (_scoreFillEffectFrames.Count == 0)
		{
			return;
		}

		var pulse = new Sprite2D
		{
			Name = "ScoreFillEffectArrivalPulse",
			Texture = _scoreFillEffectFrames[^1],
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = ScoreFillEffectBehindAvatarZIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.72f),
		};
		AddChild(pulse);
		pulse.GlobalPosition = targetCenter;
		Vector2 baseScale = GetSpriteScaleForVisualSize(_scoreFillEffectFrames[^1], ScoreFillPulseVisualSize);
		pulse.Scale = baseScale;

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(pulse, "scale", baseScale * 1.22f, 0.18f);
			tween.TweenProperty(pulse, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), 0.18f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(pulse))
			{
				pulse.QueueFree();
			}
		}
	}

	private static void UpdateScoreFillEffectFlight(
		Sprite2D effect,
		IReadOnlyList<Texture2D> frames,
		Vector2 startCenter,
		Vector2 controlCenter,
		Vector2 targetCenter,
		int travelZIndex,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(effect) || frames.Count == 0)
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		Vector2 center = QuadraticBezier(startCenter, controlCenter, targetCenter, easedProgress);
		Vector2 tangent = QuadraticBezierTangent(startCenter, controlCenter, targetCenter, easedProgress);
		int frameIndex = Mathf.Clamp(Mathf.FloorToInt(clampedProgress * frames.Count), 0, frames.Count - 1);
		float alpha = clampedProgress < 0.14f
			? Mathf.Lerp(0.0f, 1.0f, clampedProgress / 0.14f)
			: 1.0f;
		float scale = Mathf.Lerp(0.92f, 0.48f, easedProgress);

		Texture2D texture = frames[frameIndex];
		effect.Texture = texture;
		effect.ZIndex = clampedProgress >= ScoreFillEffectBehindAvatarProgress
			? ScoreFillEffectBehindAvatarZIndex
			: travelZIndex;
		effect.GlobalPosition = center;
		effect.Scale = GetSpriteScaleForVisualSize(texture, ScoreFillEffectVisualSize) * scale;
		effect.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
		if (tangent.LengthSquared() > 0.001f)
		{
			effect.Rotation = tangent.Angle();
		}
	}

	private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
	{
		float inverseT = 1.0f - t;
		return inverseT * inverseT * start + 2.0f * inverseT * t * control + t * t * end;
	}

	private static Vector2 QuadraticBezierTangent(Vector2 start, Vector2 control, Vector2 end, float t)
	{
		return 2.0f * (1.0f - t) * (control - start) + 2.0f * t * (end - control);
	}

	private static Vector2 GetSpriteScaleForVisualSize(Texture2D texture, float visualSize)
	{
		float textureWidth = Math.Max(1.0f, texture.GetWidth());
		float scale = visualSize / textureWidth;
		return new Vector2(scale, scale);
	}

	private Vector2 RandomUnitVector()
	{
		float angle = _rng.RandfRange(0.0f, Mathf.Tau);
		return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
	}
}
