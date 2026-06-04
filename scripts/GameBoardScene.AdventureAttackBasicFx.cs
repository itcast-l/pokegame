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
		Sprite2D scratch = RentSpriteFx(
			"CatAdventureScratchImpact",
			firstFrame,
			AdventureAttackEffectTravelZIndex,
			targetCenter,
			baseScale * CatAdventureScratchStartScale,
			Colors.White);

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
				_animationPresenter.Release(scratch);
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
		Sprite2D pincer = RentSpriteFx(
			"CrabAdventurePincerImpact",
			firstFrame,
			AdventureAttackEffectTravelZIndex,
			targetCenter,
			baseScale * 0.92f,
			Colors.White);

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
				_animationPresenter.Release(pincer);
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
		Sprite2D tentacles = RentSpriteFx(
			"JellyfishAdventureTentacleAttack",
			firstFrame,
			AdventureAttackEffectTravelZIndex,
			targetCenter,
			baseScale * JellyfishAdventureTentacleLocalStartScale,
			Colors.White,
			rotation);

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
				_animationPresenter.Release(tentacles);
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
		Sprite2D hitMarker = RentSpriteFx(
			"SeaSlugAdventureHitMarker",
			firstFrame,
			AdventureAttackEffectTravelZIndex,
			GetPlayerAvatarGlobalCenter(targetPlayerIndex),
			baseScale * SeaSlugHitMarkerStartScale,
			new Color(1.0f, 1.0f, 1.0f, 0.0f));

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
				_animationPresenter.Release(hitMarker);
			}
		}
	}

}
