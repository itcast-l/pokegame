using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayGardenEelBurrowAmbushAttackAsync(
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

		Rect2 attackerAvatarRect = GetPlayerAvatarGlobalRect(attackerPlayerIndex);
		Vector2 startCenter = attackerAvatarRect.GetCenter() - new Vector2(0.0f, attackerAvatarRect.Size.Y * GardenEelBurrowAmbushStartUpOffsetRatio);
		Rect2 targetAvatarRect = GetPlayerAvatarGlobalRect(targetPlayerIndex);
		Vector2 targetCenter = targetAvatarRect.GetCenter();
		Vector2 ambushCenter = targetCenter - new Vector2(0.0f, targetAvatarRect.Size.Y * GardenEelBurrowAmbushTargetUpOffsetRatio);
		Texture2D firstFrame = frames[0];
		Vector2 baseScale = GetSpriteScaleForVisualSize(
			firstFrame,
			GetPlayerAvatarFxVisualSize(targetPlayerIndex) * GardenEelBurrowAmbushVisualScale);
		CharacterAvatar? attackerAvatar = attackerPlayerIndex >= 0 && attackerPlayerIndex < _playerAvatars.Length
			? _playerAvatars[attackerPlayerIndex]
			: null;
		Color originalAvatarModulate = attackerAvatar != null && GodotObject.IsInstanceValid(attackerAvatar)
			? attackerAvatar.Modulate
			: Colors.White;

		PlayAdventureAttackStyleSfx(attackerPlayerIndex, AdventureAttackEffectStyles.GardenEelBurrow);
		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		Sprite2D ambush = RentSpriteFx(
			"GardenEelBurrowAmbushAttack",
			firstFrame,
			AdventureAttackEffectTravelZIndex,
			startCenter,
			baseScale * GardenEelBurrowAmbushStartScale,
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
			Tween tween = CreateBoardTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(ambush, "modulate:a", 1.0f, 0.06f);
			tween.TweenProperty(ambush, "modulate:a", 0.0f, 0.12f)
				.SetDelay(GardenEelBurrowAmbushTotalSeconds - 0.12f);
			if (attackerAvatar != null && GodotObject.IsInstanceValid(attackerAvatar))
			{
				tween.TweenProperty(attackerAvatar, "modulate:a", 0.0f, 0.10f)
					.SetDelay(GardenEelBurrowAmbushFrameSeconds);
				tween.TweenProperty(attackerAvatar, "modulate:a", originalAvatarModulate.A, 0.14f)
					.SetDelay(GardenEelBurrowAmbushReturnDelaySeconds);
			}

			PlayGardenEelBurrowAmbushFrames(ambush, frames, startCenter, ambushCenter, baseScale);
			tween.TweenCallback(Callable.From(InvokeHitOnce)).SetDelay(GardenEelBurrowAmbushHitDelaySeconds);

			await WaitForBoardTweenAsync(tween);
		}
		finally
		{
			if (attackerAvatar != null && GodotObject.IsInstanceValid(attackerAvatar))
			{
				attackerAvatar.Modulate = originalAvatarModulate;
			}

			if (GodotObject.IsInstanceValid(ambush))
			{
				_animationPresenter.Release(ambush);
			}
		}
	}

	private void PlayGardenEelBurrowAmbushFrames(
		Sprite2D ambush,
		IReadOnlyList<Texture2D> frames,
		Vector2 startCenter,
		Vector2 targetCenter,
		Vector2 baseScale)
	{
		if (frames.Count == 0 || !GodotObject.IsInstanceValid(ambush))
		{
			return;
		}

		Tween frameTween = CreateBoardTween(ambush);
		frameTween.SetParallel(true);
		TweenGardenEelBurrowAmbushFrame(frameTween, ambush, frames, 0, 0.0d, startCenter, baseScale * GardenEelBurrowAmbushStartScale);
		TweenGardenEelBurrowAmbushFrame(frameTween, ambush, frames, 1, GardenEelBurrowAmbushFrameSeconds, startCenter, baseScale * 0.94f);
		TweenGardenEelBurrowAmbushFrame(frameTween, ambush, frames, 2, GardenEelBurrowAmbushFrameSeconds * 2.0d, targetCenter, baseScale);
		TweenGardenEelBurrowAmbushFrame(frameTween, ambush, frames, 3, GardenEelBurrowAmbushFrameSeconds * 3.0d, targetCenter, baseScale * GardenEelBurrowAmbushEmergeScale);
		TweenGardenEelBurrowAmbushFrame(frameTween, ambush, frames, 4, GardenEelBurrowAmbushFrameSeconds * 4.0d, targetCenter, baseScale * GardenEelBurrowAmbushEmergeScale);
		TweenGardenEelBurrowAmbushFrame(frameTween, ambush, frames, 5, GardenEelBurrowAmbushReturnDelaySeconds, startCenter, baseScale * GardenEelBurrowAmbushStartScale);
	}

	private static void TweenGardenEelBurrowAmbushFrame(
		Tween frameTween,
		Sprite2D ambush,
		IReadOnlyList<Texture2D> frames,
		int frameIndex,
		double timeSeconds,
		Vector2 position,
		Vector2 scale)
	{
		if (frameIndex >= frames.Count)
		{
			return;
		}

		Texture2D texture = frames[frameIndex];
		frameTween.TweenCallback(Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(ambush))
			{
				return;
			}

			ambush.Texture = texture;
			ambush.GlobalPosition = position;
			ambush.Scale = scale;
		})).SetDelay(timeSeconds);
	}
}
