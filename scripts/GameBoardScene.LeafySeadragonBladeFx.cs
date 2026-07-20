using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayLeafySeadragonBladeProjectilesAsync(
		int attackerPlayerIndex,
		int targetTableCardIndex,
		int turnSequenceGeneration)
	{
		EnsureAdventureModeResourcesLoaded();

		if (_leafySeadragonBladeFlightFrames.Count == 0
			|| !IsInsideTree()
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver())
		{
			return;
		}

		Vector2? targetCenter = GetTableCardGlobalCenter(targetTableCardIndex);
		if (!targetCenter.HasValue)
		{
			return;
		}

		PlayAdventureAttackStyleSfx(attackerPlayerIndex, AdventureAttackEffectStyles.LeafySeadragonSlash);
		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		Task[] projectileTasks = new Task[LeafySeadragonBladeProjectileCount];
		for (int projectileIndex = 0; projectileIndex < LeafySeadragonBladeProjectileCount; projectileIndex++)
		{
			projectileTasks[projectileIndex] = PlayLeafySeadragonBladeProjectileAsync(
				attackerPlayerIndex,
				targetCenter.Value,
				projectileIndex,
				turnSequenceGeneration);
		}

		await Task.WhenAll(projectileTasks);
	}

	private async Task PlayLeafySeadragonBladeProjectileAsync(
		int attackerPlayerIndex,
		Vector2 targetCenter,
		int projectileIndex,
		int turnSequenceGeneration)
	{
		if (projectileIndex > 0)
		{
			await WaitForBoardDelayAsync(LeafySeadragonBladeProjectileStaggerSeconds * projectileIndex);
		}

		if (turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver())
		{
			return;
		}

		Vector2 startCenter = GetPlayerAvatarGlobalCenter(attackerPlayerIndex);
		Vector2 travel = targetCenter - startCenter;
		float distance = travel.Length();
		if (distance <= 1.0f)
		{
			return;
		}

		Vector2 direction = travel / distance;
		Vector2 side = new(-direction.Y, direction.X);
		float sideOffset = (projectileIndex - 1) * 22.0f;
		Vector2 projectileStart = startCenter + direction * LeafySeadragonBladeProjectileStartInset + side * sideOffset;
		Vector2 projectileEnd = targetCenter - direction * LeafySeadragonBladeProjectileTargetInset + side * sideOffset * 0.28f;
		Vector2 controlCenter = (projectileStart + projectileEnd) / 2.0f
			+ side * (sideOffset * 0.42f)
			+ new Vector2(0.0f, -16.0f);
		Texture2D? flightFrame = _leafySeadragonBladeFlightFrames.Count > 0 ? _leafySeadragonBladeFlightFrames[0] : null;
		Texture2D? hitFrame = _leafySeadragonBladeImpactFrames.Count > 0 ? _leafySeadragonBladeImpactFrames[0] : null;
		var projectile = new Node2D
		{
			Name = "LeafySeadragonBladeProjectile",
			ZIndex = AdventureAttackEffectTravelZIndex + projectileIndex,
			ZAsRelative = false,
			GlobalPosition = projectileStart,
			Rotation = direction.Angle(),
			Scale = Vector2.One * 0.78f,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		Sprite2D? flightBlade = null;
		if (flightFrame != null)
		{
			flightBlade = new Sprite2D
			{
				Name = "FlightBlade",
				Texture = flightFrame,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				Centered = true,
				Scale = GetSpriteScaleForVisualSize(flightFrame, LeafySeadragonBladeProjectileVisualSize),
			};
		}
		if (flightBlade != null)
		{
			projectile.AddChild(flightBlade);
		}

		Sprite2D? hitSlash = null;
		if (hitFrame != null)
		{
			hitSlash = new Sprite2D
			{
				Name = "HitSlash",
				Texture = hitFrame,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				Centered = true,
				Scale = GetSpriteScaleForVisualSize(hitFrame, LeafySeadragonBladeProjectileVisualSize * 1.16f),
				Visible = false,
			};
			projectile.AddChild(hitSlash);
		}

		AddChild(projectile);

		try
		{
			Tween tween = CreateBoardTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			if (flightBlade != null)
			{
				FrameAnimationHelpers.TweenTextureFramesForDuration(
					tween,
					flightBlade,
					_leafySeadragonBladeFlightFrames,
					LeafySeadragonBladeProjectileFlightSeconds / Mathf.Max(1, _leafySeadragonBladeFlightFrames.Count),
					LeafySeadragonBladeProjectileFlightSeconds * 0.82f);
			}
			if (hitSlash != null)
			{
				FrameAnimationHelpers.TweenTextureFramesForDuration(
					tween,
					hitSlash,
					_leafySeadragonBladeImpactFrames,
					Mathf.Max(0.04, (LeafySeadragonBladeProjectileFlightSeconds * 0.22f) / Mathf.Max(1, _leafySeadragonBladeImpactFrames.Count)),
					LeafySeadragonBladeProjectileFlightSeconds * 0.22f);
			}
			tween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(projectile))
				{
					if (flightBlade != null && GodotObject.IsInstanceValid(flightBlade))
					{
						flightBlade.Visible = false;
					}
					if (hitSlash != null && GodotObject.IsInstanceValid(hitSlash))
					{
						hitSlash.Visible = true;
					}
				}
			})).SetDelay(LeafySeadragonBladeProjectileFlightSeconds * 0.78f);
			tween.TweenProperty(projectile, "modulate:a", 1.0f, LeafySeadragonBladeProjectileFlightSeconds * 0.12f);
			tween.TweenProperty(projectile, "modulate:a", 0.0f, LeafySeadragonBladeProjectileFlightSeconds * 0.20f)
				.SetDelay(LeafySeadragonBladeProjectileFlightSeconds * 0.80f);
			tween.TweenProperty(projectile, "scale", Vector2.One * 1.04f, LeafySeadragonBladeProjectileFlightSeconds * 0.46f);
			tween.TweenProperty(projectile, "scale", Vector2.One * 0.90f, LeafySeadragonBladeProjectileFlightSeconds * 0.54f)
				.SetDelay(LeafySeadragonBladeProjectileFlightSeconds * 0.46f);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateSpriteBezierFlight(
					projectile,
					projectileStart,
					controlCenter,
					projectileEnd,
					progress,
					LeafySeadragonBladeProjectileRotationOffset)),
				0.0f,
				1.0f,
				LeafySeadragonBladeProjectileFlightSeconds);

			await WaitForBoardTweenAsync(tween);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(projectile))
			{
				projectile.QueueFree();
			}
		}
	}
}
