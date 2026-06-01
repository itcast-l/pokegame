using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayFishAdventureAttackBeamAsync(
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
			onHit?.Invoke();
			return;
		}

		PlayAdventureAttackStyleSfx(attackerPlayerIndex, AdventureAttackEffectStyles.FishBeam);
		Vector2 direction = travel / distance;
		float beamLength = Math.Max(24.0f, distance - FishAdventureAttackBeamStartInset - FishAdventureAttackBeamEndInset);
		Vector2 beamCenter = startCenter + direction * (FishAdventureAttackBeamStartInset + beamLength / 2.0f);
		var beam = new Sprite2D
		{
			Name = "FishAdventureAttackBeam",
			Texture = frames[0],
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = beamCenter,
			Rotation = direction.Angle(),
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(beam);

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			beam.GlobalPosition = beamCenter;
			beam.Rotation = direction.Angle();
			beam.Scale = new Vector2(
				beamLength / Math.Max(1.0f, frames[0].GetWidth()) * 0.76f,
				FishAdventureAttackBeamThickness / Math.Max(1.0f, frames[0].GetHeight()));
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				beam,
				frames,
				FishAdventureAttackBeamSeconds / Math.Max(1, frames.Count),
				FishAdventureAttackBeamSeconds);
			tween.TweenProperty(beam, "modulate:a", 1.0f, FishAdventureAttackBeamSeconds * 0.12f);
			tween.TweenProperty(beam, "modulate:a", 0.0f, FishAdventureAttackBeamSeconds * 0.18f)
				.SetDelay(FishAdventureAttackBeamSeconds * 0.82f);
			tween.TweenProperty(beam, "scale", new Vector2(
				beamLength / Math.Max(1.0f, frames[0].GetWidth()),
				FishAdventureAttackBeamThickness / Math.Max(1.0f, frames[0].GetHeight())),
				FishAdventureAttackBeamSeconds * 0.12f);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateSpriteRotation(beam, direction.Angle())),
				0.0f,
				1.0f,
				FishAdventureAttackBeamSeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}
		}
		finally
		{
			if (GodotObject.IsInstanceValid(beam))
			{
				beam.QueueFree();
			}
		}
	}
}
