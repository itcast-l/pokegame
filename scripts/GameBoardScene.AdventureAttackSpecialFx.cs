using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayLeafySeadragonCollectAttackAsync(
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

		Texture2D bladeFrame = frames.Count > 1 ? frames[1] : frames[0];
		Texture2D impactFrame = frames.Count > 2 ? frames[2] : frames[^1];
		Texture2D dissolveFrame = frames.Count > 3 ? frames[3] : impactFrame;
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

		PlayAdventureAttackStyleSfx(attackerPlayerIndex, AdventureAttackEffectStyles.LeafySeadragonSlash);
		Vector2 direction = travel / distance;
		Vector2 side = new(-direction.Y, direction.X);
		Vector2 projectileStart = startCenter + direction * 34.0f;
		Vector2 projectileEnd = targetCenter - direction * 28.0f;
		Vector2 controlCenter = (projectileStart + projectileEnd) / 2.0f
			+ new Vector2(0.0f, -34.0f)
			+ side * _rng.RandfRange(-18.0f, 18.0f);
		Vector2 bladeScale = GetSpriteScaleForVisualSize(bladeFrame, LeafySeadragonCollectAttackVisualSize);
		Sprite2D blade = RentSpriteFx(
			"LeafySeadragonCollectAttackBlade",
			bladeFrame,
			AdventureAttackEffectTravelZIndex,
			projectileStart,
			bladeScale * 0.82f,
			new Color(1.0f, 1.0f, 1.0f, 0.0f),
			direction.Angle() + LeafySeadragonCollectAttackRotationOffset);
		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);

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
			Tween flightTween = CreateTween();
			flightTween.SetParallel(true);
			flightTween.SetTrans(Tween.TransitionType.Cubic);
			flightTween.SetEase(Tween.EaseType.Out);
			flightTween.TweenProperty(blade, "modulate:a", 1.0f, LeafySeadragonCollectAttackFlightSeconds * 0.12f);
			flightTween.TweenProperty(blade, "modulate:a", 0.84f, LeafySeadragonCollectAttackFlightSeconds * 0.18f)
				.SetDelay(LeafySeadragonCollectAttackFlightSeconds * 0.82f);
			flightTween.TweenProperty(blade, "scale", bladeScale * 1.08f, LeafySeadragonCollectAttackFlightSeconds * 0.48f);
			flightTween.TweenProperty(blade, "scale", bladeScale * 0.94f, LeafySeadragonCollectAttackFlightSeconds * 0.52f)
				.SetDelay(LeafySeadragonCollectAttackFlightSeconds * 0.48f);
			flightTween.TweenMethod(
				Callable.From<float>(progress => UpdateSpinningSpriteBezierFlight(
					blade,
					projectileStart,
					controlCenter,
					projectileEnd,
					progress,
					LeafySeadragonCollectAttackRotationOffset,
					LeafySeadragonCollectAttackSpinRadians)),
				0.0f,
				1.0f,
				LeafySeadragonCollectAttackFlightSeconds);

			await ToSignal(flightTween, Tween.SignalName.Finished);
			InvokeHitOnce();
			if (turnSequenceGeneration != _turnSequenceGeneration
				|| IsGameOver()
				|| !GodotObject.IsInstanceValid(blade))
			{
				return;
			}

			Vector2 impactScale = GetSpriteScaleForVisualSize(impactFrame, AdventureAttackImpactVisualSize * 1.08f);
			blade.GlobalPosition = targetCenter;
			blade.Rotation = 0.0f;
			blade.Scale = impactScale * 0.78f;
			blade.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.92f);
			FrameAnimationHelpers.PlayTextureKeyframes(
				blade,
				impactFrame,
				dissolveFrame,
				LeafySeadragonCollectAttackDissolveFrameDelaySeconds);

			Tween impactTween = CreateTween();
			impactTween.SetParallel(true);
			impactTween.SetTrans(Tween.TransitionType.Sine);
			impactTween.SetEase(Tween.EaseType.Out);
			impactTween.TweenProperty(blade, "scale", impactScale * 1.22f, LeafySeadragonCollectAttackImpactSeconds);
			impactTween.TweenProperty(blade, "modulate:a", 0.0f, LeafySeadragonCollectAttackImpactSeconds * 0.62f)
				.SetDelay(LeafySeadragonCollectAttackImpactSeconds * 0.38f);
			impactTween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(blade))
				{
					blade.Rotation = _rng.RandfRange(-0.35f, 0.35f);
				}
			})).SetDelay(LeafySeadragonCollectAttackImpactSeconds * 0.44f);

			await ToSignal(impactTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(blade))
			{
				_animationPresenter.Release(blade);
			}
		}
	}

	private async Task PlayBossKRoyalSeizureLanceAttackAsync(
		int attackerPlayerIndex,
		Vector2 targetCenter,
		int turnSequenceGeneration,
		Action? onHit)
	{
		EnsureAdventureModeResourcesLoaded();

		await PlayBossKRoyalSeizureLanceFlightAsync(attackerPlayerIndex, targetCenter, turnSequenceGeneration, onHit);
		if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
		{
			await PlayAdventureAttackImpactPulseAsync(attackerPlayerIndex, targetCenter, _bossKRoyalSeizureLanceFrames);
		}
	}

	private async Task PlayBossKRoyalSeizureLanceFlightAsync(
		int attackerPlayerIndex,
		Vector2 targetCenter,
		int turnSequenceGeneration,
		Action? onTouchTarget = null)
	{
		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		if (_bossKRoyalSeizureLanceFrames.Count == 0
			|| !IsInsideTree()
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver())
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onTouchTarget?.Invoke();
			}

			return;
		}

		Vector2 startCenter = GetPlayerAvatarGlobalCenter(attackerPlayerIndex);
		Vector2 travel = targetCenter - startCenter;
		float distance = travel.Length();
		if (distance <= 1.0f)
		{
			onTouchTarget?.Invoke();
			return;
		}

		Vector2 direction = travel / distance;
		Vector2 side = new(-direction.Y, direction.X);
		Vector2 projectileStart = startCenter + direction * BossKRoyalSeizureLanceStartInset;
		Vector2 projectileEnd = targetCenter - direction * BossKRoyalSeizureLanceTargetInset;
		Vector2 controlCenter = (projectileStart + projectileEnd) / 2.0f
			+ new Vector2(0.0f, -BossKRoyalSeizureLanceArcHeight)
			+ side * _rng.RandfRange(-14.0f, 14.0f);
		Texture2D firstFrame = _bossKRoyalSeizureLanceFrames[0];
		Vector2 baseScale = GetSpriteScaleForVisualSize(firstFrame, BossKRoyalSeizureLanceVisualSize);
		Sprite2D lance = RentSpriteFx(
			"BossKRoyalSeizureLance",
			firstFrame,
			AdventureAttackEffectTravelZIndex + 8,
			projectileStart,
			baseScale * 0.82f,
			new Color(1.0f, 1.0f, 1.0f, 0.0f),
			direction.Angle());

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				lance,
				_bossKRoyalSeizureLanceFrames,
				BossKRoyalSeizureLanceFlightSeconds / Math.Max(1, _bossKRoyalSeizureLanceFrames.Count),
				BossKRoyalSeizureLanceFlightSeconds);
			tween.TweenProperty(lance, "modulate:a", 1.0f, BossKRoyalSeizureLanceFlightSeconds * 0.12f);
			tween.TweenProperty(lance, "modulate:a", 0.0f, BossKRoyalSeizureLanceFlightSeconds * 0.18f)
				.SetDelay(BossKRoyalSeizureLanceFlightSeconds * 0.82f);
			tween.TweenProperty(lance, "scale", baseScale * 1.08f, BossKRoyalSeizureLanceFlightSeconds * 0.48f);
			tween.TweenProperty(lance, "scale", baseScale * 0.94f, BossKRoyalSeizureLanceFlightSeconds * 0.52f)
				.SetDelay(BossKRoyalSeizureLanceFlightSeconds * 0.48f);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateSpriteBezierFlight(
					lance,
					projectileStart,
					controlCenter,
					projectileEnd,
					progress)),
				0.0f,
				1.0f,
				BossKRoyalSeizureLanceFlightSeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onTouchTarget?.Invoke();
			}
		}
		finally
		{
			if (GodotObject.IsInstanceValid(lance))
			{
				_animationPresenter.Release(lance);
			}
		}
	}
}
