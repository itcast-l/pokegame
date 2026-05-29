using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayCatAdventureScratchAttackAsync(
		int attackerPlayerIndex,
		Vector2 targetCenter,
		IReadOnlyList<Texture2D> frames,
		int turnSequenceGeneration,
		Action? onHit)
	{
		if (frames.Count == 0)
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}

			return;
		}

		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		Texture2D firstFrame = frames[0];
		Vector2 baseScale = GetSpriteScaleForVisualSize(firstFrame, CatAdventureScratchVisualSize);
		var scratch = new Sprite2D
		{
			Name = "CatAdventureScratchImpact",
			Texture = firstFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = targetCenter,
			Scale = baseScale * CatAdventureScratchStartScale,
			Modulate = Colors.White,
		};
		AddChild(scratch);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(scratch, "scale", baseScale * CatAdventureScratchEndScale, CatAdventureScratchTotalSeconds);
		tween.TweenProperty(scratch, "modulate:a", 0.0f, CatAdventureScratchTotalSeconds);
		FrameAnimationHelpers.PlayTextureKeyframes(scratch, frames, CatAdventureScratchFrameKeyTimes);
		tween.TweenCallback(Callable.From(() =>
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}
		})).SetDelay(CatAdventureScratchHitDelaySeconds);
		tween.Finished += () =>
		{
			if (GodotObject.IsInstanceValid(scratch))
			{
				scratch.QueueFree();
			}
		};

		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private async Task PlayCrabAdventurePincerAttackAsync(
		int attackerPlayerIndex,
		Vector2 targetCenter,
		IReadOnlyList<Texture2D> frames,
		int turnSequenceGeneration,
		Action? onHit)
	{
		if (frames.Count == 0)
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}

			return;
		}

		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		Texture2D firstFrame = frames[0];
		Vector2 baseScale = GetSpriteScaleForVisualSize(firstFrame, CrabAdventurePincerVisualSize);
		var pincer = new Sprite2D
		{
			Name = "CrabAdventurePincerImpact",
			Texture = firstFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = targetCenter,
			Scale = baseScale * 0.92f,
			Modulate = Colors.White,
		};
		AddChild(pincer);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(pincer, "scale", baseScale * 1.08f, CrabAdventurePincerTotalSeconds);
		tween.TweenProperty(pincer, "modulate:a", 0.0f, CrabAdventurePincerTotalSeconds * 0.22f)
			.SetDelay(CrabAdventurePincerFrame4DelaySeconds);
		FrameAnimationHelpers.PlayTextureKeyframes(pincer, frames, CrabAdventurePincerFrameKeyTimes);
		tween.TweenCallback(Callable.From(() =>
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}
		})).SetDelay(CrabAdventurePincerHitDelaySeconds);
		tween.Finished += () =>
		{
			if (GodotObject.IsInstanceValid(pincer))
			{
				pincer.QueueFree();
			}
		};

		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private async Task PlayJellyfishAdventureTentacleAttackAsync(
		int attackerPlayerIndex,
		int targetPlayerIndex,
		Vector2 targetCenter,
		IReadOnlyList<Texture2D> frames,
		int turnSequenceGeneration,
		Action? onHit)
	{
		if (frames.Count == 0)
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}

			return;
		}

		Texture2D firstFrame = frames[0];
		Vector2 baseScale = GetSpriteScaleForVisualSize(
			firstFrame,
			GetPlayerAvatarFxVisualSize(targetPlayerIndex) * JellyfishAdventureTentacleLocalVisualScale);
		float rotation = Mathf.DegToRad(JellyfishAdventureTentacleLocalRotationDegrees);
		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		var tentacles = new Sprite2D
		{
			Name = "JellyfishAdventureTentacleAttack",
			Texture = firstFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = targetCenter,
			Rotation = rotation,
			Scale = baseScale * JellyfishAdventureTentacleLocalStartScale,
			Modulate = Colors.White,
		};
		AddChild(tentacles);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(tentacles, "scale", baseScale * JellyfishAdventureTentacleLocalEndScale, JellyfishAdventureTentacleTotalSeconds * 0.18f)
			.SetDelay(JellyfishAdventureTentacleFrame3DelaySeconds);
		tween.TweenProperty(tentacles, "rotation", -rotation, JellyfishAdventureTentacleTotalSeconds * 0.52f);
		tween.TweenProperty(tentacles, "modulate:a", 0.0f, JellyfishAdventureTentacleTotalSeconds * 0.24f)
			.SetDelay(JellyfishAdventureTentacleFrame4DelaySeconds);
		FrameAnimationHelpers.PlayTextureKeyframes(tentacles, frames, JellyfishAdventureTentacleFrameKeyTimes);
		tween.TweenCallback(Callable.From(() =>
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}
		})).SetDelay(JellyfishAdventureTentacleHitDelaySeconds);
		tween.Finished += () =>
		{
			if (GodotObject.IsInstanceValid(tentacles))
			{
				tentacles.QueueFree();
			}
		};

		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private async Task PlaySeaSlugHitMarkerAttackAsync(
		int attackerPlayerIndex,
		int targetPlayerIndex,
		IReadOnlyList<Texture2D> frames,
		int turnSequenceGeneration,
		Action? onHit)
	{
		if (frames.Count == 0 || !IsInsideTree())
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}

			return;
		}

		Texture2D firstFrame = frames[0];
		Vector2 baseScale = GetSpriteScaleForVisualSize(
			firstFrame,
			GetPlayerAvatarFxVisualSize(targetPlayerIndex) * SeaSlugHitMarkerLocalVisualScale);
		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		var hitMarker = new Sprite2D
		{
			Name = "SeaSlugAdventureHitMarker",
			Texture = firstFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = GetPlayerAvatarGlobalCenter(targetPlayerIndex),
			Scale = baseScale * SeaSlugHitMarkerStartScale,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(hitMarker);

		bool hitInvoked = false;
		void InvokeHitOnce()
		{
			if (hitInvoked
				|| turnSequenceGeneration != _turnSequenceGeneration
				|| IsGameOver())
			{
				return;
			}

			hitInvoked = true;
			onHit?.Invoke();
		}

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				hitMarker,
				frames,
				SeaSlugHitMarkerFrameSeconds,
				SeaSlugHitMarkerTotalSeconds);
			tween.TweenProperty(hitMarker, "modulate:a", 0.86f, SeaSlugHitMarkerTotalSeconds * 0.18d);
			tween.TweenProperty(hitMarker, "modulate:a", 0.0f, SeaSlugHitMarkerTotalSeconds * 0.28d)
				.SetDelay(SeaSlugHitMarkerTotalSeconds * 0.72d);
			tween.TweenProperty(hitMarker, "scale", baseScale * SeaSlugHitMarkerEndScale, SeaSlugHitMarkerTotalSeconds);
			tween.TweenMethod(
				Callable.From<float>(_ =>
				{
					if (GodotObject.IsInstanceValid(hitMarker))
					{
						hitMarker.GlobalPosition = GetPlayerAvatarGlobalCenter(targetPlayerIndex);
					}
				}),
				0.0f,
				1.0f,
				SeaSlugHitMarkerTotalSeconds);
			tween.TweenCallback(Callable.From(InvokeHitOnce)).SetDelay(SeaSlugHitMarkerHitDelaySeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(hitMarker))
			{
				hitMarker.QueueFree();
			}
		}
	}

}
