using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayMantaRayTideShiftCycloneAttackAsync(
		int attackerPlayerIndex,
		int targetPlayerIndex,
		int turnSequenceGeneration,
		Action? onHit)
	{
		EnsureAdventureModeResourcesLoaded();

		if (_mantaRayTideShiftCycloneFxFrames.Count == 0 || !IsInsideTree())
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}

			return;
		}

		Texture2D texture = _mantaRayTideShiftCycloneFxFrames[0];
		Vector2 startCenter = GetPlayerScoreEffectTargetGlobalCenter(attackerPlayerIndex);
		Vector2 targetCenter = GetPlayerScoreEffectTargetGlobalCenter(targetPlayerIndex);
		Vector2 travel = targetCenter - startCenter;
		float distance = travel.Length();
		if (distance <= 1.0f)
		{
			onHit?.Invoke();
			return;
		}

		PlayAdventureAttackStyleSfx(attackerPlayerIndex, AdventureAttackEffectStyles.MantaRayCyclone);
		Vector2 direction = travel / distance;
		Vector2 projectileScale = GetSpriteScaleForVisualSize(texture, MantaRayTideShiftProjectileVisualSize);

		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		Sprite2D cyclone = RentSpriteFx(
			"MantaRayTideShiftCycloneFx",
			texture,
			AdventureAttackEffectTravelZIndex,
			startCenter,
			projectileScale * 0.86f,
			new Color(1.0f, 1.0f, 1.0f, 0.0f),
			direction.Angle());

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

		bool cycloneQueued = false;
		void QueueCyclone()
		{
			if (cycloneQueued)
			{
				return;
			}

			cycloneQueued = true;
			if (GodotObject.IsInstanceValid(cyclone))
			{
				_animationPresenter.Release(cyclone);
			}
		}

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		tween.Finished += QueueCyclone;
		FrameAnimationHelpers.TweenTextureFramesForDuration(
			tween,
			cyclone,
			_mantaRayTideShiftCycloneFxFrames,
			MantaRayTideShiftCycloneFrameSeconds,
			MantaRayTideShiftCycloneSeconds);
		tween.TweenProperty(cyclone, "modulate:a", 0.92f, MantaRayTideShiftCycloneSeconds * 0.18f);
		tween.TweenProperty(cyclone, "modulate:a", 0.0f, MantaRayTideShiftCycloneSeconds * 0.22f)
			.SetDelay(MantaRayTideShiftCycloneSeconds * 0.78f);
		tween.TweenProperty(cyclone, "global_position", targetCenter, MantaRayTideShiftCycloneSeconds);
		tween.TweenProperty(cyclone, "scale", projectileScale * 1.06f, MantaRayTideShiftCycloneSeconds * 0.42f);
		tween.TweenProperty(cyclone, "scale", projectileScale * 0.96f, MantaRayTideShiftCycloneSeconds * 0.58f)
			.SetDelay(MantaRayTideShiftCycloneSeconds * 0.42f);
		tween.TweenMethod(
			Callable.From<float>(_ => UpdateSpriteRotation(cyclone, direction.Angle())),
			0.0f,
			1.0f,
			MantaRayTideShiftCycloneSeconds);

		await ToSignal(tween, Tween.SignalName.Finished);
		InvokeHitOnce();
		if (!cycloneQueued)
		{
			QueueCyclone();
		}
	}

}
