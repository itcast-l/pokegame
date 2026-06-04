using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayDragonPrinceDamageHitAttackAsync(
		int attackerPlayerIndex,
		Vector2 startCenter,
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

		Vector2 travel = targetCenter - startCenter;
		float distance = travel.Length();
		if (distance <= 1.0f)
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}

			return;
		}

		Vector2 direction = travel / distance;
		Vector2 side = new(-direction.Y, direction.X);
		Vector2 projectileStart = startCenter + direction * DragonPrinceDamageHitStartInset;
		Vector2 projectileEnd = targetCenter - direction * DragonPrinceDamageHitTargetInset;
		Vector2 controlCenter = (projectileStart + projectileEnd) / 2.0f
			+ new Vector2(0.0f, -30.0f)
			+ side * _rng.RandfRange(-14.0f, 14.0f);
		IReadOnlyList<Texture2D> flightFrames = _dragonPrinceDamageHitFlightFrames.Count > 0
			? _dragonPrinceDamageHitFlightFrames
			: frames;
		IReadOnlyList<Texture2D> impactFrames = _dragonPrinceDamageHitImpactFrames.Count > 0
			? _dragonPrinceDamageHitImpactFrames
			: flightFrames;

		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		PlayAdventureAttackFlightSfx(attackerPlayerIndex);
		Sprite2D projectile = RentSpriteFx(
			"DragonPrinceDamageHitFlight",
			flightFrames[0],
			AdventureAttackEffectTravelZIndex,
			projectileStart,
			Vector2.One,
			new Color(1.0f, 1.0f, 1.0f, 0.0f),
			direction.Angle() + DragonPrinceDamageHitRotationOffset);

		try
		{
			Tween flightTween = CreateTween();
			flightTween.SetParallel(true);
			flightTween.SetTrans(Tween.TransitionType.Cubic);
			flightTween.SetEase(Tween.EaseType.Out);
			ApplyAdventureAttackEffectTexture(projectile, flightFrames[0], DragonPrinceDamageHitVisualSize, 0.84f);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				flightTween,
				projectile,
				flightFrames,
				DragonPrinceDamageHitFlightSeconds / Math.Max(1, flightFrames.Count),
				DragonPrinceDamageHitFlightSeconds);
			flightTween.TweenProperty(projectile, "modulate:a", 1.0f, DragonPrinceDamageHitFlightSeconds * 0.14f);
			flightTween.TweenProperty(projectile, "modulate:a", 0.92f, DragonPrinceDamageHitFlightSeconds * 0.18f)
				.SetDelay(DragonPrinceDamageHitFlightSeconds * 0.82f);
			flightTween.TweenProperty(
				projectile,
				"scale",
				GetSpriteScaleForVisualSize(flightFrames[0], DragonPrinceDamageHitVisualSize) * 1.06f,
				DragonPrinceDamageHitFlightSeconds * 0.55f);
			flightTween.TweenProperty(
				projectile,
				"scale",
				GetSpriteScaleForVisualSize(flightFrames[0], DragonPrinceDamageHitVisualSize) * 0.92f,
				DragonPrinceDamageHitFlightSeconds * 0.45f)
				.SetDelay(DragonPrinceDamageHitFlightSeconds * 0.55f);
			flightTween.TweenMethod(
				Callable.From<float>(progress => UpdateSpriteBezierFlight(
					projectile,
					projectileStart,
					controlCenter,
					projectileEnd,
					progress,
					DragonPrinceDamageHitRotationOffset)),
				0.0f,
				1.0f,
				DragonPrinceDamageHitFlightSeconds);

			await ToSignal(flightTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(projectile))
			{
				_animationPresenter.Release(projectile);
			}
		}

		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		Sprite2D impact = RentSpriteFx(
			"DragonPrinceDamageHitImpact",
			impactFrames[0],
			AdventureAttackEffectTravelZIndex,
			targetCenter,
			Vector2.One,
			new Color(1.0f, 1.0f, 1.0f, 0.0f),
			direction.Angle() + DragonPrinceDamageHitRotationOffset);
		PlayAdventureAttackStyleSfx(attackerPlayerIndex, AdventureAttackEffectStyles.DragonPrinceHit);

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
			Vector2 baseScale = GetSpriteScaleForVisualSize(impactFrames[0], DragonPrinceDamageHitImpactVisualSize);
			impact.Scale = baseScale * 0.78f;

			Tween impactTween = CreateTween();
			impactTween.SetParallel(true);
			impactTween.SetTrans(Tween.TransitionType.Sine);
			impactTween.SetEase(Tween.EaseType.Out);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				impactTween,
				impact,
				impactFrames,
				DragonPrinceDamageHitImpactFrameSeconds,
				DragonPrinceDamageHitImpactSeconds);
			impactTween.TweenProperty(impact, "modulate:a", 1.0f, DragonPrinceDamageHitImpactSeconds * 0.12f);
			impactTween.TweenProperty(impact, "modulate:a", 0.0f, DragonPrinceDamageHitImpactSeconds * 0.28f)
				.SetDelay(DragonPrinceDamageHitImpactSeconds * 0.72f);
			impactTween.TweenProperty(impact, "scale", baseScale * 1.12f, DragonPrinceDamageHitImpactSeconds * 0.36f);
			impactTween.TweenProperty(impact, "scale", baseScale * 0.82f, DragonPrinceDamageHitImpactSeconds * 0.64f)
				.SetDelay(DragonPrinceDamageHitImpactSeconds * 0.36f);
			impactTween.TweenMethod(
				Callable.From<float>(_ => UpdateSpriteRotation(impact, direction.Angle() + DragonPrinceDamageHitRotationOffset)),
				0.0f,
				1.0f,
				DragonPrinceDamageHitImpactSeconds);
			impactTween.TweenCallback(Callable.From(InvokeHitOnce))
				.SetDelay(DragonPrinceDamageHitImpactHitDelaySeconds);

			await ToSignal(impactTween, Tween.SignalName.Finished);
			InvokeHitOnce();
		}
		finally
		{
			if (GodotObject.IsInstanceValid(impact))
			{
				_animationPresenter.Release(impact);
			}
		}
	}

	private async Task PlayDragonQueenJawSlashAttackAsync(
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
		Vector2 targetCenter = GetPlayerAvatarGlobalCenter(targetPlayerIndex);
		Vector2 baseScale = GetSpriteScaleForVisualSize(
			firstFrame,
			GetPlayerAvatarFxVisualSize(targetPlayerIndex) * DragonQueenJawSlashVisualScale);

		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		PlayAdventureAttackStyleSfx(attackerPlayerIndex, AdventureAttackEffectStyles.DragonQueenJaw);
		Sprite2D jawSlash = RentSpriteFx(
			"DragonQueenJawSlashAttack",
			firstFrame,
			AdventureAttackEffectTravelZIndex,
			targetCenter,
			baseScale * DragonQueenJawSlashStartScale,
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
			FrameAnimationHelpers.TweenTextureFrames(tween, jawSlash, frames, DragonQueenJawSlashFrameSeconds);
			tween.TweenProperty(jawSlash, "modulate:a", 0.96f, DragonQueenJawSlashTotalSeconds * 0.14d);
			tween.TweenProperty(jawSlash, "modulate:a", 0.0f, DragonQueenJawSlashTotalSeconds * 0.32d)
				.SetDelay(DragonQueenJawSlashTotalSeconds * 0.68d);
			tween.TweenProperty(jawSlash, "scale", baseScale * DragonQueenJawSlashPeakScale, DragonQueenJawSlashHitDelaySeconds);
			tween.TweenProperty(jawSlash, "scale", baseScale * DragonQueenJawSlashEndScale, DragonQueenJawSlashTotalSeconds - DragonQueenJawSlashHitDelaySeconds)
				.SetDelay(DragonQueenJawSlashHitDelaySeconds);
			tween.TweenMethod(
				Callable.From<float>(_ =>
				{
					if (GodotObject.IsInstanceValid(jawSlash))
					{
						jawSlash.GlobalPosition = GetPlayerAvatarGlobalCenter(targetPlayerIndex);
					}
				}),
				0.0f,
				1.0f,
				DragonQueenJawSlashTotalSeconds);
			tween.TweenCallback(Callable.From(InvokeHitOnce)).SetDelay(DragonQueenJawSlashHitDelaySeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
			InvokeHitOnce();
		}
		finally
		{
			if (GodotObject.IsInstanceValid(jawSlash))
			{
				_animationPresenter.Release(jawSlash);
			}
		}
	}

}
