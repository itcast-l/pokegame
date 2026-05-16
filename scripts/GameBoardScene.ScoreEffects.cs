using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const float ScoreBubbleGatherSeconds = 0.18f;
	private const float ScoreBubbleFlightSeconds = 0.42f;
	private const float ScoreBubbleStaggerSeconds = 0.045f;
	private const int MaxScoreBubbleFlights = 14;

	private async Task PlayScoreBubbleCollectionAsync(
		int playerIndex,
		IReadOnlyList<CapturedTableCardVisual> capturedCardVisuals,
		int turnSequenceGeneration,
		Action? onArrival = null)
	{
		if (_scoreBubbleFrames.Count == 0 || capturedCardVisuals.Count == 0)
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onArrival?.Invoke();
			}

			return;
		}

		Vector2 targetCenter = GetScoreMeterGlobalCenter(playerIndex);
		var flightTasks = new List<Task>();
		int bubbleIndex = 0;
		for (int visualIndex = 0; visualIndex < capturedCardVisuals.Count && bubbleIndex < MaxScoreBubbleFlights; visualIndex++)
		{
			Vector2 cardCenter = capturedCardVisuals[visualIndex].GlobalPosition + CardVisualSize / 2.0f;
			int bubblesForCard = capturedCardVisuals.Count <= 4 ? 2 : 1;
			for (int localIndex = 0; localIndex < bubblesForCard && bubbleIndex < MaxScoreBubbleFlights; localIndex++)
			{
				flightTasks.Add(PlayScoreBubbleFlightAsync(
					cardCenter,
					targetCenter,
					bubbleIndex,
					(visualIndex * ScoreBubbleStaggerSeconds) + localIndex * 0.025f));
				bubbleIndex++;
			}
		}

		await Task.WhenAll(flightTasks);
		if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
		{
			onArrival?.Invoke();
			await PlayScoreBubbleSplashAsync(targetCenter);
		}
	}

	private Vector2 GetScoreMeterGlobalCenter(int playerIndex)
	{
		if (playerIndex >= 0
			&& playerIndex < _playerScoreMeters.Length
			&& _playerScoreMeters[playerIndex] is ScoreAvatarMeter meter
			&& GodotObject.IsInstanceValid(meter))
		{
			return meter.GlobalPosition + meter.Size / 2.0f;
		}

		if (playerIndex >= 0 && playerIndex < _playerCardPiles.Count && GodotObject.IsInstanceValid(_playerCardPiles[playerIndex]))
		{
			return _playerCardPiles[playerIndex].GlobalPosition + CardPileSize / 2.0f;
		}

		return Size / 2.0f;
	}

	private async Task PlayScoreBubbleFlightAsync(Vector2 cardCenter, Vector2 targetCenter, int bubbleIndex, float startDelaySeconds)
	{
		if (startDelaySeconds > 0.0f)
		{
			await ToSignal(GetTree().CreateTimer(startDelaySeconds), SceneTreeTimer.SignalName.Timeout);
		}

		Texture2D startTexture = _scoreBubbleFrames[Math.Min(bubbleIndex % _scoreBubbleFrames.Count, _scoreBubbleFrames.Count - 1)];
		Texture2D arrivalTexture = _scoreBubbleFrames[^1];
		float spriteSize = _rng.RandfRange(38.0f, 58.0f);
		var bubble = new TextureRect
		{
			Name = "ScoreBubbleFlight",
			Texture = startTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = new Vector2(spriteSize, spriteSize),
			PivotOffset = new Vector2(spriteSize, spriteSize) / 2.0f,
			ZIndex = PlayedCardFlightZIndex + 800 + bubbleIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(bubble);

		Vector2 startCenter = cardCenter + RandomUnitVector() * _rng.RandfRange(22.0f, 44.0f);
		Vector2 gatherCenter = cardCenter + RandomUnitVector() * _rng.RandfRange(4.0f, 12.0f);
		float arcHeight = _rng.RandfRange(42.0f, 76.0f);
		float sideDrift = _rng.RandfRange(-22.0f, 22.0f);

		try
		{
			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateScoreBubbleFlight(
					bubble,
					startTexture,
					arrivalTexture,
					startCenter,
					gatherCenter,
					targetCenter,
					arcHeight,
					sideDrift,
					progress)),
				0.0f,
				1.0f,
				ScoreBubbleGatherSeconds + ScoreBubbleFlightSeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(bubble))
			{
				bubble.QueueFree();
			}
		}
	}

	private async Task PlayScoreBubbleSplashAsync(Vector2 targetCenter)
	{
		if (_scoreBubbleFrames.Count == 0)
		{
			return;
		}

		var splash = new TextureRect
		{
			Name = "ScoreBubbleSplash",
			Texture = _scoreBubbleFrames[^1],
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = new Vector2(70.0f, 70.0f),
			PivotOffset = new Vector2(35.0f, 35.0f),
			ZIndex = PlayedCardFlightZIndex + 900,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.82f),
		};
		AddChild(splash);
		splash.GlobalPosition = targetCenter - splash.Size / 2.0f;

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(splash, "scale", new Vector2(1.25f, 1.25f), 0.16f);
			tween.TweenProperty(splash, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), 0.16f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(splash))
			{
				splash.QueueFree();
			}
		}
	}

	private static void UpdateScoreBubbleFlight(
		TextureRect bubble,
		Texture2D startTexture,
		Texture2D arrivalTexture,
		Vector2 startCenter,
		Vector2 gatherCenter,
		Vector2 targetCenter,
		float arcHeight,
		float sideDrift,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(bubble))
		{
			return;
		}

		float gatherPortion = ScoreBubbleGatherSeconds / (ScoreBubbleGatherSeconds + ScoreBubbleFlightSeconds);
		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		Vector2 center;
		float scale;
		float alpha;

		if (clampedProgress < gatherPortion)
		{
			float localProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress / gatherPortion);
			center = startCenter.Lerp(gatherCenter, localProgress);
			scale = Mathf.Lerp(0.45f, 0.82f, localProgress);
			alpha = Mathf.Lerp(0.0f, 0.95f, localProgress);
		}
		else
		{
			float flightProgress = Mathf.SmoothStep(0.0f, 1.0f, (clampedProgress - gatherPortion) / (1.0f - gatherPortion));
			Vector2 travel = targetCenter - gatherCenter;
			Vector2 side = travel.LengthSquared() > 0.001f
				? new Vector2(-travel.Y, travel.X).Normalized()
				: Vector2.Zero;
			center = gatherCenter.Lerp(targetCenter, flightProgress)
				+ new Vector2(0.0f, -Mathf.Sin(flightProgress * Mathf.Pi) * arcHeight)
				+ side * Mathf.Sin(flightProgress * Mathf.Pi) * sideDrift;
			scale = Mathf.Lerp(0.82f, 0.32f, flightProgress);
			alpha = flightProgress < 0.82f ? 0.95f : Mathf.Lerp(0.95f, 0.0f, (flightProgress - 0.82f) / 0.18f);
			bubble.Texture = flightProgress > 0.78f ? arrivalTexture : startTexture;
		}

		bubble.GlobalPosition = center - bubble.Size / 2.0f;
		bubble.Scale = new Vector2(scale, scale);
		bubble.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
	}

	private Vector2 RandomUnitVector()
	{
		float angle = _rng.RandfRange(0.0f, Mathf.Tau);
		return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
	}
}
