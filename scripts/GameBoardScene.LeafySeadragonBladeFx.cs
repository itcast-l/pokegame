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
		if (_leafySeadragonBladeProjectileFrames.Count == 0
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
			await ToSignal(
				GetTree().CreateTimer(LeafySeadragonBladeProjectileStaggerSeconds * projectileIndex),
				SceneTreeTimer.SignalName.Timeout);
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
		Texture2D? hitFrame = _leafySeadragonBladeProjectileFrames.Count > 2
			? _leafySeadragonBladeProjectileFrames[2]
			: null;
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
		var flightBlade = new Node2D { Name = "FlightBlade" };
		flightBlade.AddChild(CreateLeafySeadragonBladePolygon(
			"TrailGlow",
			new[]
			{
				new Vector2(-58.0f, -6.0f),
				new Vector2(12.0f, -3.0f),
				new Vector2(28.0f, 0.0f),
				new Vector2(12.0f, 3.0f),
				new Vector2(-58.0f, 6.0f),
			},
			new Color(0.28f, 0.88f, 1.0f, 0.30f),
			0));
		flightBlade.AddChild(CreateLeafySeadragonBladePolygon(
			"UpperLeafFin",
			new[]
			{
				new Vector2(-8.0f, -7.0f),
				new Vector2(24.0f, -22.0f),
				new Vector2(46.0f, -10.0f),
				new Vector2(18.0f, -3.0f),
			},
			new Color(0.23f, 0.92f, 0.86f, 0.78f),
			1));
		flightBlade.AddChild(CreateLeafySeadragonBladePolygon(
			"LowerLeafFin",
			new[]
			{
				new Vector2(-10.0f, 7.0f),
				new Vector2(22.0f, 22.0f),
				new Vector2(44.0f, 9.0f),
				new Vector2(16.0f, 3.0f),
			},
			new Color(0.20f, 0.82f, 0.72f, 0.72f),
			1));
		flightBlade.AddChild(CreateLeafySeadragonBladePolygon(
			"BladeBody",
			new[]
			{
				new Vector2(-14.0f, -10.0f),
				new Vector2(48.0f, -6.0f),
				new Vector2(78.0f, 0.0f),
				new Vector2(48.0f, 6.0f),
				new Vector2(-14.0f, 10.0f),
				new Vector2(4.0f, 0.0f),
			},
			new Color(0.54f, 0.98f, 1.0f, 0.94f),
			2));
		flightBlade.AddChild(CreateLeafySeadragonBladePolygon(
			"BladeCore",
			new[]
			{
				new Vector2(-2.0f, -3.0f),
				new Vector2(56.0f, -2.0f),
				new Vector2(82.0f, 0.0f),
				new Vector2(56.0f, 2.0f),
				new Vector2(-2.0f, 3.0f),
				new Vector2(10.0f, 0.0f),
			},
			new Color(1.0f, 1.0f, 1.0f, 0.92f),
			3));
		projectile.AddChild(flightBlade);

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
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(projectile))
				{
					flightBlade.Visible = false;
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

			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(projectile))
			{
				projectile.QueueFree();
			}
		}
	}

	private static Polygon2D CreateLeafySeadragonBladePolygon(
		string name,
		Vector2[] polygon,
		Color color,
		int zIndex)
	{
		return new Polygon2D
		{
			Name = name,
			Polygon = polygon,
			Color = color,
			ZIndex = zIndex,
		};
	}

}
