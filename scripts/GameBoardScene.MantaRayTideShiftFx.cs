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
		if (_mantaRayTideShiftCycloneFxFrames.Count == 0 || !IsInsideTree())
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}

			return;
		}

		Texture2D texture = _mantaRayTideShiftCycloneFxFrames[0];
		Vector2 startCenter = GetPlayerAvatarGlobalCenter(attackerPlayerIndex);
		Vector2 targetCenter = GetPlayerAvatarGlobalCenter(targetPlayerIndex);
		Vector2 travel = targetCenter - startCenter;
		float distance = travel.Length();
		if (distance <= 1.0f)
		{
			onHit?.Invoke();
			return;
		}

		PlayAdventureAttackStyleSfx(AdventureAttackEffectStyles.MantaRayCyclone);
		Vector2 direction = travel / distance;
		float effectLength = Math.Max(48.0f, distance - MantaRayTideShiftCycloneStartInset - MantaRayTideShiftCycloneTargetInset);
		Vector2 effectStart = startCenter + direction * MantaRayTideShiftCycloneStartInset;
		Vector2 effectEnd = effectStart + direction * effectLength;
		Vector2 effectCenter = (effectStart + effectEnd) / 2.0f;
		float visualHeight = Math.Max(96.0f, GetPlayerAvatarFxVisualSize(attackerPlayerIndex) * MantaRayTideShiftCycloneVisualHeightScale);
		Vector2 finalScale = new(
			effectLength / Math.Max(1.0f, texture.GetWidth()),
			visualHeight / Math.Max(1.0f, texture.GetHeight()));

		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		var cyclone = new Sprite2D
		{
			Name = "MantaRayTideShiftCycloneFx",
			Texture = texture,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = effectCenter - direction * 28.0f,
			Rotation = direction.Angle(),
			Scale = new Vector2(finalScale.X * 0.78f, finalScale.Y * 0.84f),
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(cyclone);

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
				cyclone.QueueFree();
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
		tween.TweenProperty(cyclone, "global_position", effectCenter + direction * 18.0f, MantaRayTideShiftCycloneSeconds);
		tween.TweenProperty(cyclone, "scale", finalScale, MantaRayTideShiftCycloneSeconds * 0.42f);
		tween.TweenProperty(cyclone, "scale", new Vector2(finalScale.X * 1.03f, finalScale.Y * 0.94f), MantaRayTideShiftCycloneSeconds * 0.58f)
			.SetDelay(MantaRayTideShiftCycloneSeconds * 0.42f);
		tween.TweenMethod(
			Callable.From<float>(_ => UpdateSpriteRotation(cyclone, direction.Angle())),
			0.0f,
			1.0f,
			MantaRayTideShiftCycloneSeconds);
		tween.TweenCallback(Callable.From(() =>
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}
		})).SetDelay(MantaRayTideShiftCycloneSeconds * 0.64f);

		await ToSignal(tween, Tween.SignalName.Finished);
		if (!cycloneQueued)
		{
			QueueCyclone();
		}
	}

}
