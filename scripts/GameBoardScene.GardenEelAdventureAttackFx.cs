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
		var ambush = new Sprite2D
		{
			Name = "GardenEelBurrowAmbushAttack",
			Texture = firstFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = startCenter,
			Scale = baseScale * GardenEelBurrowAmbushStartScale,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(ambush);

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

			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (attackerAvatar != null && GodotObject.IsInstanceValid(attackerAvatar))
			{
				attackerAvatar.Modulate = originalAvatarModulate;
			}

			if (GodotObject.IsInstanceValid(ambush))
			{
				ambush.QueueFree();
			}
		}
	}

	private static void PlayGardenEelBurrowAmbushFrames(
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

		var animationPlayer = new AnimationPlayer
		{
			Name = "GardenEelAmbushFrameAnimationPlayer",
			RootNode = new NodePath(".."),
		};
		ambush.AddChild(animationPlayer);

		AnimationLibrary library = new();
		Animation animation = new()
		{
			Length = (float)GardenEelBurrowAmbushReturnDelaySeconds,
			LoopMode = Animation.LoopModeEnum.None,
		};
		int textureTrack = AddDiscreteAnimationTrack(animation, new NodePath(".:texture"));
		int positionTrack = AddDiscreteAnimationTrack(animation, new NodePath(".:global_position"));
		int scaleTrack = AddDiscreteAnimationTrack(animation, new NodePath(".:scale"));

		InsertGardenEelBurrowAmbushFrame(animation, textureTrack, positionTrack, scaleTrack, frames, 0, 0.0d, startCenter, baseScale * GardenEelBurrowAmbushStartScale);
		InsertGardenEelBurrowAmbushFrame(animation, textureTrack, positionTrack, scaleTrack, frames, 1, GardenEelBurrowAmbushFrameSeconds, startCenter, baseScale * 0.94f);
		InsertGardenEelBurrowAmbushFrame(animation, textureTrack, positionTrack, scaleTrack, frames, 2, GardenEelBurrowAmbushFrameSeconds * 2.0d, targetCenter, baseScale);
		InsertGardenEelBurrowAmbushFrame(animation, textureTrack, positionTrack, scaleTrack, frames, 3, GardenEelBurrowAmbushFrameSeconds * 3.0d, targetCenter, baseScale * GardenEelBurrowAmbushEmergeScale);
		InsertGardenEelBurrowAmbushFrame(animation, textureTrack, positionTrack, scaleTrack, frames, 4, GardenEelBurrowAmbushFrameSeconds * 4.0d, targetCenter, baseScale * GardenEelBurrowAmbushEmergeScale);
		InsertGardenEelBurrowAmbushFrame(animation, textureTrack, positionTrack, scaleTrack, frames, 5, GardenEelBurrowAmbushReturnDelaySeconds, startCenter, baseScale * GardenEelBurrowAmbushStartScale);

		library.AddAnimation("frames", animation);
		animationPlayer.AddAnimationLibrary("", library);
		animationPlayer.AnimationFinished += _ =>
		{
			if (GodotObject.IsInstanceValid(animationPlayer))
			{
				animationPlayer.QueueFree();
			}
		};
		animationPlayer.Play("frames");
	}

	private static void InsertGardenEelBurrowAmbushFrame(
		Animation animation,
		int textureTrack,
		int positionTrack,
		int scaleTrack,
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

		animation.TrackInsertKey(textureTrack, timeSeconds, frames[frameIndex]);
		animation.TrackInsertKey(positionTrack, timeSeconds, position);
		animation.TrackInsertKey(scaleTrack, timeSeconds, scale);
	}

	private static int AddDiscreteAnimationTrack(Animation animation, NodePath path)
	{
		int trackIndex = animation.AddTrack(Animation.TrackType.Value);
		animation.TrackSetPath(trackIndex, path);
		animation.TrackSetInterpolationType(trackIndex, Animation.InterpolationType.Nearest);
		animation.ValueTrackSetUpdateMode(trackIndex, Animation.UpdateMode.Discrete);
		return trackIndex;
	}
}
