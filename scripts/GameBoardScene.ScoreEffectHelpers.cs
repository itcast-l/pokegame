using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private float GetPlayerAvatarFxVisualSize(int playerIndex)
	{
		Rect2 avatarRect = GetPlayerAvatarGlobalRect(playerIndex);
		float side = Math.Max(1.0f, Math.Min(avatarRect.Size.X, avatarRect.Size.Y));
		return side * EnemySpecialAbilityAvatarFxVisualScale;
	}

	private Rect2 GetPlayerAvatarGlobalRect(int playerIndex)
	{
		if (playerIndex >= 0
			&& playerIndex < _playerAvatarAreas.Length
			&& _playerAvatarAreas[playerIndex] is DraggableAvatarArea area
			&& GodotObject.IsInstanceValid(area))
		{
			return area.GetGlobalRect();
		}

		if (playerIndex >= 0
			&& playerIndex < _playerAvatars.Length
			&& _playerAvatars[playerIndex] is CharacterAvatar avatar
			&& GodotObject.IsInstanceValid(avatar))
		{
			return avatar.GetGlobalRect();
		}

		Vector2 fallbackCenter = GetScoreMeterGlobalCenter(playerIndex);
		return new Rect2(fallbackCenter - Vector2.One * 78.0f, Vector2.One * 156.0f);
	}

	private Vector2 GetPlayerAvatarGlobalCenter(int playerIndex)
	{
		if (playerIndex >= 0
			&& playerIndex < _playerAvatarAreas.Length
			&& _playerAvatarAreas[playerIndex] is DraggableAvatarArea area
			&& GodotObject.IsInstanceValid(area))
		{
			return area.GetGlobalRect().GetCenter();
		}

		if (playerIndex >= 0
			&& playerIndex < _playerAvatars.Length
			&& _playerAvatars[playerIndex] is CharacterAvatar avatar
			&& GodotObject.IsInstanceValid(avatar))
		{
			return avatar.GetGlobalRect().GetCenter();
		}

		return GetScoreMeterGlobalCenter(playerIndex);
	}

	private Vector2 GetPlayerScoreEffectTargetGlobalCenter(int playerIndex)
	{
		if (playerIndex >= 0
			&& playerIndex < _playerScoreEffectTargets.Length
			&& _playerScoreEffectTargets[playerIndex] is ScoreEffectTarget target
			&& GodotObject.IsInstanceValid(target))
		{
			return target.MarkerGlobalPosition;
		}

		return GetPlayerAvatarGlobalCenter(playerIndex);
	}

	private void PlayAvatarDamagePopup(int playerIndex, int damage)
	{
		if (damage <= 0)
		{
			return;
		}

		Rect2 avatarRect = GetPlayerAvatarGlobalRect(playerIndex);
		var damageLabel = new Label
		{
			Name = "AvatarDamagePopup",
			Text = $"-{damage}",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = new Vector2(Math.Max(avatarRect.Size.X, 76.0f), 38.0f),
			ZIndex = AvatarDamagePopupZIndex,
		};
		damageLabel.AddThemeFontSizeOverride("font_size", AvatarDamagePopupFontSize);
		damageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.25f, 0.18f));
		damageLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.86f));
		damageLabel.AddThemeConstantOverride("shadow_offset_x", 2);
		damageLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		AddChild(damageLabel);

		damageLabel.GlobalPosition = new Vector2(
			avatarRect.GetCenter().X - damageLabel.Size.X / 2.0f,
			avatarRect.Position.Y - AvatarDamagePopupTopOffset);
		Vector2 endPosition = damageLabel.Position + new Vector2(0.0f, -AvatarDamagePopupRise);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Back);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(damageLabel, "position", endPosition, AvatarDamagePopupSeconds);
		tween.TweenProperty(damageLabel, "scale", new Vector2(1.12f, 1.12f), AvatarDamagePopupSeconds * 0.32f);
		tween.TweenProperty(damageLabel, "scale", Vector2.One, AvatarDamagePopupSeconds * 0.42f)
			.SetDelay(AvatarDamagePopupSeconds * 0.32f);
		tween.TweenProperty(damageLabel, "modulate:a", 0.0f, AvatarDamagePopupSeconds * 0.34f)
			.SetDelay(AvatarDamagePopupSeconds * 0.66f);
		tween.Finished += () =>
		{
			if (GodotObject.IsInstanceValid(damageLabel))
			{
				damageLabel.QueueFree();
			}
		};
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
			return GetVisibleCardPileTopGlobalPosition(playerIndex) + CardVisualSize / 2.0f;
		}

		return Size / 2.0f;
	}

	private Vector2 GetPlayerCardPileGlobalCenter(int playerIndex)
	{
		if (playerIndex >= 0
			&& playerIndex < _playerCardPiles.Count
			&& GodotObject.IsInstanceValid(_playerCardPiles[playerIndex]))
		{
			return GetVisibleCardPileTopGlobalPosition(playerIndex) + CardVisualSize / 2.0f;
		}

		return GetScoreMeterGlobalCenter(playerIndex);
	}

	private async Task PlayAdventureAttackEffectFlightAsync(
		int attackerPlayerIndex,
		Vector2 startCenter,
		Vector2 targetCenter,
		IReadOnlyList<Texture2D> frames,
		int turnSequenceGeneration,
		Action? onTouchTarget,
		float flightSeconds)
	{
		PlayAdventureAttackFlightSfx(attackerPlayerIndex);
		var effect = new Sprite2D
		{
			Name = "AdventureAttackEffectFlight",
			Texture = frames[0],
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(effect);

		Vector2 travel = targetCenter - startCenter;
		Vector2 side = travel.LengthSquared() > 0.001f
			? new Vector2(-travel.Y, travel.X).Normalized()
			: Vector2.Zero;
		Vector2 controlCenter = (startCenter + targetCenter) / 2.0f
			+ new Vector2(0.0f, -38.0f)
			+ side * _rng.RandfRange(-18.0f, 18.0f);

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			ApplyAdventureAttackEffectTexture(effect, frames[0], AdventureAttackEffectVisualSize, 0.78f);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				effect,
				frames,
				flightSeconds / Math.Max(1, frames.Count),
				flightSeconds);
			tween.TweenProperty(effect, "modulate:a", 1.0f, flightSeconds * 0.12f);
			tween.TweenProperty(effect, "modulate:a", 0.86f, flightSeconds * 0.18f)
				.SetDelay(flightSeconds * 0.82f);
			tween.TweenProperty(effect, "scale", GetSpriteScaleForVisualSize(frames[0], AdventureAttackEffectVisualSize) * 1.08f, flightSeconds * 0.5f);
			tween.TweenProperty(effect, "scale", GetSpriteScaleForVisualSize(frames[0], AdventureAttackEffectVisualSize) * 0.78f, flightSeconds * 0.5f)
				.SetDelay(flightSeconds * 0.5f);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateSpriteBezierFlight(
					effect,
					startCenter,
					controlCenter,
					targetCenter,
					progress)),
				0.0f,
				1.0f,
				flightSeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onTouchTarget?.Invoke();
			}
		}
		finally
		{
			if (GodotObject.IsInstanceValid(effect))
			{
				effect.QueueFree();
			}
		}
	}

	private async Task PlayAdventureAttackImpactPulseAsync(
		int attackerPlayerIndex,
		Vector2 targetCenter,
		IReadOnlyList<Texture2D> frames)
	{
		if (frames.Count == 0)
		{
			return;
		}

		PlayAdventureAttackImpactSfx(attackerPlayerIndex);
		var pulse = new Sprite2D
		{
			Name = "AdventureAttackImpactPulse",
			Texture = frames[^1],
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.76f),
		};
		AddChild(pulse);
		pulse.GlobalPosition = targetCenter;
		Vector2 baseScale = GetSpriteScaleForVisualSize(frames[^1], AdventureAttackImpactVisualSize) * 0.72f;
		pulse.Scale = baseScale;

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(pulse, "scale", baseScale * 1.34f, 0.22f);
			tween.TweenProperty(pulse, "modulate:a", 0.0f, 0.22f);
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

	private async Task PlayScoreFillEffectFlightAsync(
		Vector2 cardCenter,
		Vector2 targetCenter,
		IReadOnlyList<Texture2D> frames,
		int effectIndex,
		float startDelaySeconds,
		Action onTouchTarget)
	{
		if (startDelaySeconds > 0.0f)
		{
			await ToSignal(GetTree().CreateTimer(startDelaySeconds), SceneTreeTimer.SignalName.Timeout);
		}

		PlayGameSfx(GameSfx.ScoreFlight, -5.0f, 0.96f, 1.06f, 0.08);
		var effect = new Sprite2D
		{
			Name = "ScoreFillEffectFlight",
			Texture = frames[0],
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
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			ApplyScoreFillEffectTexture(effect, frames[0], ScoreFillEffectVisualSize, 0.92f);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				effect,
				frames,
				ScoreFillEffectFlightSeconds / Math.Max(1, frames.Count),
				ScoreFillEffectFlightSeconds);
			tween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(effect))
				{
					effect.ZIndex = ScoreFillEffectBehindAvatarZIndex;
				}
			})).SetDelay(ScoreFillEffectFlightSeconds * ScoreFillEffectBehindAvatarProgress);
			tween.TweenProperty(effect, "modulate:a", 1.0f, ScoreFillEffectFlightSeconds * 0.14f);
			tween.TweenProperty(effect, "scale", GetSpriteScaleForVisualSize(frames[0], ScoreFillEffectVisualSize) * 0.48f, ScoreFillEffectFlightSeconds);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateSpriteBezierFlight(
					effect,
					startCenter,
					controlCenter,
					targetCenter,
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

	private async Task PlayScoreFillArrivalPulseAsync(Vector2 targetCenter, IReadOnlyList<Texture2D> frames)
	{
		if (frames.Count == 0)
		{
			return;
		}

		PlayGameSfx(GameSfx.ScoreArrive, -2.0f, 0.97f, 1.04f, 0.04);
		var pulse = new Sprite2D
		{
			Name = "ScoreFillEffectArrivalPulse",
			Texture = frames[^1],
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = ScoreFillEffectBehindAvatarZIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.72f),
		};
		AddChild(pulse);
		pulse.GlobalPosition = targetCenter;
		Vector2 baseScale = GetSpriteScaleForVisualSize(frames[^1], ScoreFillPulseVisualSize);
		pulse.Scale = baseScale;

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(pulse, "scale", baseScale * 1.22f, 0.18f);
			tween.TweenProperty(pulse, "modulate:a", 0.0f, 0.18f);
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

	private static void UpdateSpriteBezierFlight(
		Node2D effect,
		Vector2 startCenter,
		Vector2 controlCenter,
		Vector2 targetCenter,
		float progress,
		float rotationOffset = 0.0f)
	{
		if (!GodotObject.IsInstanceValid(effect))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		Vector2 center = QuadraticBezier(startCenter, controlCenter, targetCenter, easedProgress);
		Vector2 tangent = QuadraticBezierTangent(startCenter, controlCenter, targetCenter, easedProgress);
		effect.GlobalPosition = center;
		if (tangent.LengthSquared() > 0.001f)
		{
			effect.Rotation = tangent.Angle() + rotationOffset;
		}
	}

	private static void UpdateSpinningSpriteBezierFlight(
		Sprite2D effect,
		Vector2 startCenter,
		Vector2 controlCenter,
		Vector2 targetCenter,
		float progress,
		float rotationOffset,
		float spinRadians)
	{
		if (!GodotObject.IsInstanceValid(effect))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		Vector2 center = QuadraticBezier(startCenter, controlCenter, targetCenter, easedProgress);
		Vector2 tangent = QuadraticBezierTangent(startCenter, controlCenter, targetCenter, easedProgress);
		effect.GlobalPosition = center;
		if (tangent.LengthSquared() > 0.001f)
		{
			effect.Rotation = tangent.Angle() + rotationOffset + clampedProgress * spinRadians;
		}
	}

	private static void UpdateSpriteRotation(Sprite2D sprite, float rotation)
	{
		if (!GodotObject.IsInstanceValid(sprite))
		{
			return;
		}

		sprite.Rotation = rotation;
	}

	private static void ApplyScoreFillEffectTexture(Sprite2D effect, Texture2D texture, float visualSize, float scale)
	{
		effect.Texture = texture;
		effect.Scale = GetSpriteScaleForVisualSize(texture, visualSize) * scale;
	}

	private static void ApplyAdventureAttackEffectTexture(Sprite2D effect, Texture2D texture, float visualSize, float scale)
	{
		effect.Texture = texture;
		effect.Scale = GetSpriteScaleForVisualSize(texture, visualSize) * scale;
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
	}}
