using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const float ScoreFillEffectFlightSeconds = 0.62f;
	private const float ScoreFillEffectStaggerSeconds = 0.07f;
	private const int MaxScoreFillEffectFlights = 12;
	private const float ScoreFillEffectVisualSize = 64.0f;
	private const float ScoreFillPulseVisualSize = 92.0f;
	private const int ScoreFillEffectTravelZIndex = PlayedCardFlightZIndex + 850;
	private const int ScoreFillEffectBehindAvatarZIndex = 2;
	private const float ScoreFillEffectBehindAvatarProgress = 0.72f;
	private const float EnemySpecialAbilityAvatarFxFrameSeconds = 0.08f;
	private const float EnemySpecialAbilityAvatarFxTotalSeconds = EnemySpecialAbilityAvatarFxFrameSeconds * EnemySpecialAbilityAvatarFxFrameCount;
	private const float EnemySpecialAbilityAvatarFxVisualScale = 1.22f;
	private const int EnemySpecialAbilityAvatarFxZIndex = PlayedCardFlightZIndex + 980;
	private const float MantaRayTideShiftCycloneSeconds = 0.72f;
	private const float MantaRayTideShiftCycloneFrameSeconds = 0.06f;
	private const float MantaRayTideShiftCycloneStartInset = 76.0f;
	private const float MantaRayTideShiftCycloneTargetInset = 104.0f;
	private const float MantaRayTideShiftCycloneVisualHeightScale = 0.82f;
	private const float AdventureAttackEffectFlightSeconds = 0.48f;
	private const float ShrimpAdventureAttackEffectFlightSeconds = 0.72f;
	private const float NautilusAdventureAttackEffectFlightSeconds = 0.78f;
	private const float NarwhalAdventureAttackEffectFlightSeconds = 0.82f;
	private const float AdventureAttackEffectVisualSize = 104.0f;
	private const float AdventureAttackImpactVisualSize = 124.0f;
	private const float CatAdventureScratchVisualSize = 132.0f;
	private const float CatAdventureScratchStartScale = 0.86f;
	private const float CatAdventureScratchEndScale = 1.08f;
	private const float CatAdventureScratchTotalSeconds = 0.72f;
	private const double CatAdventureScratchFrame2DelaySeconds = 0.12d;
	private const double CatAdventureScratchFrame3DelaySeconds = 0.26d;
	private const double CatAdventureScratchFrame4DelaySeconds = 0.42d;
	private const double CatAdventureScratchHitDelaySeconds = CatAdventureScratchFrame3DelaySeconds;
	private const float CrabAdventurePincerVisualSize = 150.0f;
	private const float CrabAdventurePincerTotalSeconds = 0.72f;
	private const double CrabAdventurePincerFrame2DelaySeconds = 0.16d;
	private const double CrabAdventurePincerFrame3DelaySeconds = 0.32d;
	private const double CrabAdventurePincerFrame4DelaySeconds = 0.48d;
	private const double CrabAdventurePincerHitDelaySeconds = CrabAdventurePincerFrame3DelaySeconds;
	private const float JellyfishAdventureTentacleLocalVisualScale = 1.44f;
	private const float JellyfishAdventureTentacleLocalStartScale = 0.92f;
	private const float JellyfishAdventureTentacleLocalEndScale = 1.04f;
	private const float JellyfishAdventureTentacleLocalRotationDegrees = -6.0f;
	private const float JellyfishAdventureTentacleTotalSeconds = 0.78f;
	private const double JellyfishAdventureTentacleFrame2DelaySeconds = 0.18d;
	private const double JellyfishAdventureTentacleFrame3DelaySeconds = 0.38d;
	private const double JellyfishAdventureTentacleFrame4DelaySeconds = 0.58d;
	private const double JellyfishAdventureTentacleHitDelaySeconds = JellyfishAdventureTentacleFrame3DelaySeconds;
	private const float SeaSlugHitMarkerLocalVisualScale = 0.92f;
	private const float SeaSlugHitMarkerStartScale = 0.88f;
	private const float SeaSlugHitMarkerEndScale = 1.06f;
	private const double SeaSlugHitMarkerFrameSeconds = 0.115d;
	private const double SeaSlugHitMarkerTotalSeconds = SeaSlugHitMarkerFrameSeconds * AdventureDamageEffectFrameCount;
	private const double SeaSlugHitMarkerHitDelaySeconds = SeaSlugHitMarkerFrameSeconds * 2.0d;
	private const float GardenEelBurrowAmbushVisualScale = 1.28f;
	private const float GardenEelBurrowAmbushStartScale = 0.92f;
	private const float GardenEelBurrowAmbushEmergeScale = 1.08f;
	private const float GardenEelBurrowAmbushTotalSeconds = 0.78f;
	private const double GardenEelBurrowAmbushFrameSeconds = 0.12d;
	private const double GardenEelBurrowAmbushReturnDelaySeconds = GardenEelBurrowAmbushFrameSeconds * 5.0d;
	private const double GardenEelBurrowAmbushHitDelaySeconds = GardenEelBurrowAmbushFrameSeconds * 4.0d;
	private const float FishAdventureAttackBeamSeconds = 0.42f;
	private const float FishAdventureAttackBeamThickness = 96.0f;
	private const float FishAdventureAttackBeamStartInset = 24.0f;
	private const float FishAdventureAttackBeamEndInset = 34.0f;
	private const float LeafySeadragonCollectAttackFlightSeconds = 0.58f;
	private const float LeafySeadragonCollectAttackImpactSeconds = 0.24f;
	private const float LeafySeadragonCollectAttackVisualSize = 116.0f;
	private const float LeafySeadragonCollectAttackRotationOffset = -0.7853982f;
	private const float LeafySeadragonCollectAttackSpinRadians = 12.566371f;
	private const int LeafySeadragonBladeProjectileCount = 3;
	private const float LeafySeadragonBladeProjectileFlightSeconds = 0.42f;
	private const float LeafySeadragonBladeProjectileStaggerSeconds = 0.10f;
	private const float LeafySeadragonBladeProjectileVisualSize = 92.0f;
	private const float LeafySeadragonBladeProjectileStartInset = 48.0f;
	private const float LeafySeadragonBladeProjectileTargetInset = 12.0f;
	private const float LeafySeadragonBladeProjectileRotationOffset = 0.0f;
	private const int AdventureAttackEffectTravelZIndex = PlayedCardFlightZIndex + 920;

	private IReadOnlyList<Texture2D> GetScoreFillEffectFrames()
	{
		return _scoreFillEffectFrames;
	}

	private async Task PlayScoreFillCollectionAsync(
		int playerIndex,
		IReadOnlyList<CapturedTableCardVisual> capturedCardVisuals,
		int turnSequenceGeneration,
		Action? onArrival = null)
	{
		if (adventure_mode)
		{
			await PlayAdventureScoreAttackSequenceAsync(
				playerIndex,
				capturedCardVisuals,
				turnSequenceGeneration,
				onArrival);
			return;
		}

		IReadOnlyList<Texture2D> scoreFillEffectFrames = GetScoreFillEffectFrames();
		if (scoreFillEffectFrames.Count == 0 || capturedCardVisuals.Count == 0)
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onArrival?.Invoke();
			}

			return;
		}

		Vector2 targetCenter = GetScoreMeterGlobalCenter(playerIndex);
		var flightTasks = new List<Task>();
		bool scoreAwarded = false;
		for (int visualIndex = 0; visualIndex < capturedCardVisuals.Count && flightTasks.Count < MaxScoreFillEffectFlights; visualIndex++)
		{
			Vector2 cardCenter = capturedCardVisuals[visualIndex].GlobalPosition + CardVisualSize / 2.0f;
			flightTasks.Add(PlayScoreFillEffectFlightAsync(
				cardCenter,
				targetCenter,
				scoreFillEffectFrames,
				flightTasks.Count,
				visualIndex * ScoreFillEffectStaggerSeconds,
				() =>
				{
					if (scoreAwarded || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return;
					}

					scoreAwarded = true;
					onArrival?.Invoke();
				}));
		}

		await Task.WhenAll(flightTasks);
		if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
		{
			if (!scoreAwarded)
			{
				onArrival?.Invoke();
			}

			await PlayScoreFillArrivalPulseAsync(targetCenter, scoreFillEffectFrames);
		}
	}

	private async Task PlayAdventureScoreAttackSequenceAsync(
		int attackerPlayerIndex,
		IReadOnlyList<CapturedTableCardVisual> capturedCardVisuals,
		int turnSequenceGeneration,
		Action? onHit)
	{
		IReadOnlyList<Texture2D> collectionFrames = GetScoreFillEffectFrames();
		Vector2 attackerCenter = GetPlayerAvatarGlobalCenter(attackerPlayerIndex);
		var collectionTasks = new List<Task>();
		if (collectionFrames.Count > 0 && capturedCardVisuals.Count > 0)
		{
			for (int visualIndex = 0; visualIndex < capturedCardVisuals.Count && collectionTasks.Count < MaxScoreFillEffectFlights; visualIndex++)
			{
				Vector2 cardCenter = capturedCardVisuals[visualIndex].GlobalPosition + CardVisualSize / 2.0f;
				collectionTasks.Add(PlayScoreFillEffectFlightAsync(
					cardCenter,
					attackerCenter,
					collectionFrames,
					collectionTasks.Count,
					visualIndex * ScoreFillEffectStaggerSeconds,
					() => { }));
			}

			await Task.WhenAll(collectionTasks);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			await PlayScoreFillArrivalPulseAsync(attackerCenter, collectionFrames);
		}

		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		int targetPlayerIndex = GetAdventureDamageTargetPlayerIndex(attackerPlayerIndex);
		await PlayAdventureAvatarAttackAsync(
			attackerPlayerIndex,
			targetPlayerIndex,
			turnSequenceGeneration,
			onHit);
	}

	private async Task PlayAdventureAvatarAttackAsync(
		int attackerPlayerIndex,
		int targetPlayerIndex,
		int turnSequenceGeneration,
		Action? onHit)
	{
		Vector2 startCenter = GetPlayerAvatarGlobalCenter(attackerPlayerIndex);
		Vector2 targetCenter = GetPlayerAvatarGlobalCenter(targetPlayerIndex);
		if (ShouldPlayMantaRayTideShiftCycloneAttack(attackerPlayerIndex, targetPlayerIndex))
		{
			await PlayMantaRayTideShiftCycloneAttackAsync(
				attackerPlayerIndex,
				targetPlayerIndex,
				turnSequenceGeneration,
				onHit);
			return;
		}

		IReadOnlyList<Texture2D> attackFrames = GetAdventureAttackEffectFrames(attackerPlayerIndex);
		if (attackFrames.Count == 0)
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onHit?.Invoke();
			}

			return;
		}

		if (ShouldPlayCatAdventureScratchAttack(attackerPlayerIndex))
		{
			await PlayCatAdventureScratchAttackAsync(
				attackerPlayerIndex,
				targetCenter,
				attackFrames,
				turnSequenceGeneration,
				onHit);
			return;
		}

		if (IsAdventureEnemyPlayer(attackerPlayerIndex)
			&& adventure_enemy_avatar_id == FishAdventureEnemyAvatarId)
		{
			await PlayFishAdventureAttackBeamAsync(
				startCenter,
				targetCenter,
				attackFrames,
				turnSequenceGeneration,
				onHit);
			return;
		}

		if (ShouldPlayCrabAdventurePincerAttack(attackerPlayerIndex, targetPlayerIndex))
		{
			await PlayCrabAdventurePincerAttackAsync(
				attackerPlayerIndex,
				targetCenter,
				attackFrames,
				turnSequenceGeneration,
				onHit);
			return;
		}

		if (ShouldPlayJellyfishAdventureTentacleAttack(attackerPlayerIndex, targetPlayerIndex))
		{
			await PlayJellyfishAdventureTentacleAttackAsync(
				attackerPlayerIndex,
				targetPlayerIndex,
				targetCenter,
				attackFrames,
				turnSequenceGeneration,
				onHit);
			return;
		}

		if (ShouldPlaySeaSlugHitMarkerAttack(attackerPlayerIndex, targetPlayerIndex))
		{
			await PlaySeaSlugHitMarkerAttackAsync(
				attackerPlayerIndex,
				targetPlayerIndex,
				attackFrames,
				turnSequenceGeneration,
				onHit);
			return;
		}

		if (ShouldPlayGardenEelBurrowAmbushAttack(attackerPlayerIndex, targetPlayerIndex))
		{
			await PlayGardenEelBurrowAmbushAttackAsync(
				attackerPlayerIndex,
				targetPlayerIndex,
				attackFrames,
				turnSequenceGeneration,
				onHit);
			return;
		}

		if (ShouldPlayLeafySeadragonCollectAttack(attackerPlayerIndex, targetPlayerIndex))
		{
			await PlayLeafySeadragonCollectAttackAsync(
				attackerPlayerIndex,
				startCenter,
				targetCenter,
				attackFrames,
				turnSequenceGeneration,
				onHit);
			return;
		}

		await PlayAdventureAttackEffectFlightAsync(
			startCenter,
			targetCenter,
			attackFrames,
			turnSequenceGeneration,
			onHit,
			GetAdventureAttackEffectFlightSeconds(attackerPlayerIndex));

		if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
		{
			await PlayAdventureAttackImpactPulseAsync(targetCenter, attackFrames);
		}
	}

	private IReadOnlyList<Texture2D> GetAdventureAttackEffectFrames(int attackerPlayerIndex)
	{
		if (IsAdventureEnemyPlayer(attackerPlayerIndex) && _adventureEnemyDamageEffectFrames.Count > 0)
		{
			return _adventureEnemyDamageEffectFrames;
		}

		return _adventureDamageEffectFrames.Count > 0
			? _adventureDamageEffectFrames
			: _scoreFillEffectFrames;
	}

	private static bool IsAdventureEnemyPlayer(int playerIndex)
	{
		return playerIndex == 1;
	}

	private static bool ShouldPlayCatAdventureScratchAttack(int attackerPlayerIndex)
	{
		return attackerPlayerIndex == MainPlayerIndex;
	}

	private bool ShouldPlayCrabAdventurePincerAttack(int attackerPlayerIndex, int targetPlayerIndex)
	{
		return adventure_enemy_avatar_id == CrabAdventureEnemyAvatarId
			&& targetPlayerIndex == MainPlayerIndex
			&& attackerPlayerIndex != MainPlayerIndex;
	}

	private bool ShouldPlayJellyfishAdventureTentacleAttack(int attackerPlayerIndex, int targetPlayerIndex)
	{
		return adventure_enemy_avatar_id == JellyfishAdventureEnemyAvatarId
			&& targetPlayerIndex == MainPlayerIndex
			&& attackerPlayerIndex != MainPlayerIndex;
	}

	private bool ShouldPlayMantaRayTideShiftCycloneAttack(int attackerPlayerIndex, int targetPlayerIndex)
	{
		return adventure_enemy_avatar_id == MantaRayAdventureEnemyAvatarId
			&& targetPlayerIndex == MainPlayerIndex
			&& attackerPlayerIndex != MainPlayerIndex
			&& _mantaRayTideShiftCycloneFxFrames.Count > 0;
	}

	private bool ShouldPlaySeaSlugHitMarkerAttack(int attackerPlayerIndex, int targetPlayerIndex)
	{
		return adventure_enemy_avatar_id == SeaSlugAdventureEnemyAvatarId
			&& targetPlayerIndex == MainPlayerIndex
			&& attackerPlayerIndex != MainPlayerIndex;
	}

	private bool ShouldPlayGardenEelBurrowAmbushAttack(int attackerPlayerIndex, int targetPlayerIndex)
	{
		return adventure_enemy_avatar_id == GardenEelAdventureEnemyAvatarId
			&& targetPlayerIndex == MainPlayerIndex
			&& attackerPlayerIndex != MainPlayerIndex;
	}

	private bool ShouldPlayLeafySeadragonCollectAttack(int attackerPlayerIndex, int targetPlayerIndex)
	{
		return adventure_enemy_avatar_id == LeafySeadragonAdventureEnemyAvatarId
			&& targetPlayerIndex == MainPlayerIndex
			&& attackerPlayerIndex != MainPlayerIndex;
	}

	private float GetAdventureAttackEffectFlightSeconds(int attackerPlayerIndex)
	{
		return IsAdventureEnemyPlayer(attackerPlayerIndex)
			? adventure_enemy_avatar_id switch
				{
					ShrimpAdventureEnemyAvatarId => ShrimpAdventureAttackEffectFlightSeconds,
					NautilusAdventureEnemyAvatarId => NautilusAdventureAttackEffectFlightSeconds,
					NarwhalAdventureEnemyAvatarId => NarwhalAdventureAttackEffectFlightSeconds,
					_ => AdventureAttackEffectFlightSeconds,
				}
			: AdventureAttackEffectFlightSeconds;
	}

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
		var scratch = new Sprite2D
		{
			Name = "CatAdventureScratchImpact",
			Texture = firstFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = targetCenter,
			Scale = baseScale * CatAdventureScratchStartScale,
			Modulate = Colors.White,
		};
		AddChild(scratch);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(scratch, "scale", baseScale * CatAdventureScratchEndScale, CatAdventureScratchTotalSeconds);
		tween.TweenProperty(scratch, "modulate:a", 0.0f, CatAdventureScratchTotalSeconds);
		TweenCatAdventureScratchTexture(tween, scratch, frames, 1, CatAdventureScratchFrame2DelaySeconds);
		TweenCatAdventureScratchTexture(tween, scratch, frames, 2, CatAdventureScratchFrame3DelaySeconds);
		TweenCatAdventureScratchTexture(tween, scratch, frames, 3, CatAdventureScratchFrame4DelaySeconds);
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
				scratch.QueueFree();
			}
		};

		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private static void TweenCatAdventureScratchTexture(
		Tween tween,
		Sprite2D scratch,
		IReadOnlyList<Texture2D> frames,
		int frameIndex,
		double delaySeconds)
	{
		if (frameIndex >= frames.Count)
		{
			return;
		}

		Texture2D frame = frames[frameIndex];
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(scratch))
			{
				scratch.Texture = frame;
			}
		})).SetDelay(delaySeconds);
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
		var pincer = new Sprite2D
		{
			Name = "CrabAdventurePincerImpact",
			Texture = firstFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = targetCenter,
			Scale = baseScale * 0.92f,
			Modulate = Colors.White,
		};
		AddChild(pincer);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(pincer, "scale", baseScale * 1.08f, CrabAdventurePincerTotalSeconds);
		tween.TweenProperty(pincer, "modulate:a", 0.0f, CrabAdventurePincerTotalSeconds * 0.22f)
			.SetDelay(CrabAdventurePincerFrame4DelaySeconds);
		TweenCrabAdventurePincerTexture(tween, pincer, frames, 1, CrabAdventurePincerFrame2DelaySeconds);
		TweenCrabAdventurePincerTexture(tween, pincer, frames, 2, CrabAdventurePincerFrame3DelaySeconds);
		TweenCrabAdventurePincerTexture(tween, pincer, frames, 3, CrabAdventurePincerFrame4DelaySeconds);
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
				pincer.QueueFree();
			}
		};

		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private static void TweenCrabAdventurePincerTexture(
		Tween tween,
		Sprite2D pincer,
		IReadOnlyList<Texture2D> frames,
		int frameIndex,
		double delaySeconds)
	{
		if (frameIndex >= frames.Count)
		{
			return;
		}

		Texture2D frame = frames[frameIndex];
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(pincer))
			{
				pincer.Texture = frame;
			}
		})).SetDelay(delaySeconds);
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
		var tentacles = new Sprite2D
		{
			Name = "JellyfishAdventureTentacleAttack",
			Texture = firstFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = targetCenter,
			Rotation = rotation,
			Scale = baseScale * JellyfishAdventureTentacleLocalStartScale,
			Modulate = Colors.White,
		};
		AddChild(tentacles);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(tentacles, "scale", baseScale * JellyfishAdventureTentacleLocalEndScale, JellyfishAdventureTentacleTotalSeconds * 0.18f)
			.SetDelay(JellyfishAdventureTentacleFrame3DelaySeconds);
		tween.TweenProperty(tentacles, "rotation", -rotation, JellyfishAdventureTentacleTotalSeconds * 0.52f);
		tween.TweenProperty(tentacles, "modulate:a", 0.0f, JellyfishAdventureTentacleTotalSeconds * 0.24f)
			.SetDelay(JellyfishAdventureTentacleFrame4DelaySeconds);
		TweenJellyfishAdventureTentacleTexture(tween, tentacles, frames, 1, JellyfishAdventureTentacleFrame2DelaySeconds);
		TweenJellyfishAdventureTentacleTexture(tween, tentacles, frames, 2, JellyfishAdventureTentacleFrame3DelaySeconds);
		TweenJellyfishAdventureTentacleTexture(tween, tentacles, frames, 3, JellyfishAdventureTentacleFrame4DelaySeconds);
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
				tentacles.QueueFree();
			}
		};

		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private static void TweenJellyfishAdventureTentacleTexture(
		Tween tween,
		Sprite2D tentacles,
		IReadOnlyList<Texture2D> frames,
		int frameIndex,
		double delaySeconds)
	{
		if (frameIndex >= frames.Count)
		{
			return;
		}

		Texture2D frame = frames[frameIndex];
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(tentacles))
			{
				tentacles.Texture = frame;
			}
		})).SetDelay(delaySeconds);
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
		var hitMarker = new Sprite2D
		{
			Name = "SeaSlugAdventureHitMarker",
			Texture = firstFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = GetPlayerAvatarGlobalCenter(targetPlayerIndex),
			Scale = baseScale * SeaSlugHitMarkerStartScale,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(hitMarker);

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
				hitMarker.QueueFree();
			}
		}
	}

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

		Vector2 startCenter = GetPlayerAvatarGlobalCenter(attackerPlayerIndex);
		Vector2 targetCenter = GetPlayerAvatarGlobalCenter(targetPlayerIndex);
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

			TweenGardenEelBurrowAmbushFrame(tween, ambush, frames, 0, startCenter, baseScale * GardenEelBurrowAmbushStartScale, 0.0d);
			TweenGardenEelBurrowAmbushFrame(tween, ambush, frames, 1, startCenter, baseScale * 0.94f, GardenEelBurrowAmbushFrameSeconds);
			TweenGardenEelBurrowAmbushFrame(tween, ambush, frames, 2, targetCenter, baseScale, GardenEelBurrowAmbushFrameSeconds * 2.0d);
			TweenGardenEelBurrowAmbushFrame(tween, ambush, frames, 3, targetCenter, baseScale * GardenEelBurrowAmbushEmergeScale, GardenEelBurrowAmbushFrameSeconds * 3.0d);
			TweenGardenEelBurrowAmbushFrame(tween, ambush, frames, 4, targetCenter, baseScale * GardenEelBurrowAmbushEmergeScale, GardenEelBurrowAmbushFrameSeconds * 4.0d);
			TweenGardenEelBurrowAmbushFrame(tween, ambush, frames, 5, startCenter, baseScale * GardenEelBurrowAmbushStartScale, GardenEelBurrowAmbushReturnDelaySeconds);
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

	private static void TweenGardenEelBurrowAmbushFrame(
		Tween tween,
		Sprite2D ambush,
		IReadOnlyList<Texture2D> frames,
		int frameIndex,
		Vector2 position,
		Vector2 scale,
		double delaySeconds)
	{
		if (frameIndex >= frames.Count)
		{
			return;
		}

		Texture2D frame = frames[frameIndex];
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(ambush))
			{
				ambush.Texture = frame;
				ambush.GlobalPosition = position;
				ambush.Scale = scale;
			}
		})).SetDelay(delaySeconds);
	}

	private async Task PlayFishAdventureAttackBeamAsync(
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

		Vector2 direction = travel / distance;
		Vector2 side = new(-direction.Y, direction.X);
		Vector2 projectileStart = startCenter + direction * 34.0f;
		Vector2 projectileEnd = targetCenter - direction * 28.0f;
		Vector2 controlCenter = (projectileStart + projectileEnd) / 2.0f
			+ new Vector2(0.0f, -34.0f)
			+ side * _rng.RandfRange(-18.0f, 18.0f);
		Vector2 bladeScale = GetSpriteScaleForVisualSize(bladeFrame, LeafySeadragonCollectAttackVisualSize);
		var blade = new Sprite2D
		{
			Name = "LeafySeadragonCollectAttackBlade",
			Texture = bladeFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			GlobalPosition = projectileStart,
			Rotation = direction.Angle() + LeafySeadragonCollectAttackRotationOffset,
			Scale = bladeScale * 0.82f,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(blade);
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
			blade.Texture = impactFrame;
			blade.GlobalPosition = targetCenter;
			blade.Rotation = 0.0f;
			blade.Scale = impactScale * 0.78f;
			blade.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.92f);

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
					blade.Texture = dissolveFrame;
					blade.Rotation = _rng.RandfRange(-0.35f, 0.35f);
				}
			})).SetDelay(LeafySeadragonCollectAttackImpactSeconds * 0.44f);

			await ToSignal(impactTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(blade))
			{
				blade.QueueFree();
			}
		}
	}

	private async Task PlayEnemySpecialAbilityAvatarFxAsync(int enemyPlayerIndex, int turnSequenceGeneration)
	{
		if (!adventure_mode
			|| enemyPlayerIndex < 0
			|| enemyPlayerIndex >= player_count
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver())
		{
			return;
		}

		PlayPlayerAvatarAction(enemyPlayerIndex, CharacterAvatar.AvatarAction.Score);
		if (_enemySpecialAbilityAvatarFxFrames.Count == 0 || !IsInsideTree())
		{
			return;
		}

		Texture2D firstFrame = _enemySpecialAbilityAvatarFxFrames[0];
		Vector2 baseScale = GetSpriteScaleForVisualSize(firstFrame, GetPlayerAvatarFxVisualSize(enemyPlayerIndex));
		var abilityFx = new Sprite2D
		{
			Name = "EnemySpecialAbilityAvatarFx",
			Texture = firstFrame,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = EnemySpecialAbilityAvatarFxZIndex,
			ZAsRelative = false,
			GlobalPosition = GetPlayerAvatarGlobalCenter(enemyPlayerIndex),
			Scale = baseScale * 0.88f,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(abilityFx);

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				abilityFx,
				_enemySpecialAbilityAvatarFxFrames,
				EnemySpecialAbilityAvatarFxFrameSeconds,
				EnemySpecialAbilityAvatarFxTotalSeconds);
			tween.TweenProperty(abilityFx, "modulate:a", 0.88f, EnemySpecialAbilityAvatarFxTotalSeconds * 0.18f);
			tween.TweenProperty(abilityFx, "modulate:a", 0.0f, EnemySpecialAbilityAvatarFxTotalSeconds * 0.22f)
				.SetDelay(EnemySpecialAbilityAvatarFxTotalSeconds * 0.78f);
			tween.TweenProperty(abilityFx, "scale", baseScale * 1.08f, EnemySpecialAbilityAvatarFxTotalSeconds * 0.46f);
			tween.TweenProperty(abilityFx, "scale", baseScale * 0.96f, EnemySpecialAbilityAvatarFxTotalSeconds * 0.54f)
				.SetDelay(EnemySpecialAbilityAvatarFxTotalSeconds * 0.46f);
			tween.TweenMethod(
				Callable.From<float>(_ =>
				{
					if (GodotObject.IsInstanceValid(abilityFx))
					{
						abilityFx.GlobalPosition = GetPlayerAvatarGlobalCenter(enemyPlayerIndex);
					}
				}),
				0.0f,
				1.0f,
				EnemySpecialAbilityAvatarFxTotalSeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(abilityFx))
			{
				abilityFx.QueueFree();
			}
		}
	}

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

		PlayPlayerAvatarAction(attackerPlayerIndex, CharacterAvatar.AvatarAction.Attack);
		var projectileTasks = new List<Task>(LeafySeadragonBladeProjectileCount);
		for (int projectileIndex = 0; projectileIndex < LeafySeadragonBladeProjectileCount; projectileIndex++)
		{
			projectileTasks.Add(PlayLeafySeadragonBladeProjectileAsync(
				attackerPlayerIndex,
				targetCenter.Value,
				projectileIndex,
				turnSequenceGeneration));
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

	private float GetPlayerAvatarFxVisualSize(int playerIndex)
	{
		Rect2 avatarRect = GetPlayerAvatarGlobalRect(playerIndex);
		float side = Math.Max(1.0f, Math.Min(avatarRect.Size.X, avatarRect.Size.Y));
		return side * EnemySpecialAbilityAvatarFxVisualScale;
	}

	private Rect2 GetPlayerAvatarGlobalRect(int playerIndex)
	{
		if (playerIndex >= 0
			&& playerIndex < _playerAvatarAreas.Length
			&& _playerAvatarAreas[playerIndex] is DraggableAvatarArea area
			&& GodotObject.IsInstanceValid(area))
		{
			return area.GetGlobalRect();
		}

		if (playerIndex >= 0
			&& playerIndex < _playerAvatars.Length
			&& _playerAvatars[playerIndex] is CharacterAvatar avatar
			&& GodotObject.IsInstanceValid(avatar))
		{
			return avatar.GetGlobalRect();
		}

		Vector2 fallbackCenter = GetScoreMeterGlobalCenter(playerIndex);
		return new Rect2(fallbackCenter - Vector2.One * 78.0f, Vector2.One * 156.0f);
	}

	private Vector2 GetPlayerAvatarGlobalCenter(int playerIndex)
	{
		if (playerIndex >= 0
			&& playerIndex < _playerAvatarAreas.Length
			&& _playerAvatarAreas[playerIndex] is DraggableAvatarArea area
			&& GodotObject.IsInstanceValid(area))
		{
			return area.GetGlobalRect().GetCenter();
		}

		if (playerIndex >= 0
			&& playerIndex < _playerAvatars.Length
			&& _playerAvatars[playerIndex] is CharacterAvatar avatar
			&& GodotObject.IsInstanceValid(avatar))
		{
			return avatar.GetGlobalRect().GetCenter();
		}

		return GetScoreMeterGlobalCenter(playerIndex);
	}

	private Vector2 GetScoreMeterGlobalCenter(int playerIndex)
	{
		if (playerIndex >= 0
			&& playerIndex < _playerScoreEffectTargets.Length
			&& _playerScoreEffectTargets[playerIndex] is ScoreEffectTarget target
			&& GodotObject.IsInstanceValid(target))
		{
			return target.MarkerGlobalPosition;
		}

		if (playerIndex >= 0
			&& playerIndex < _playerScoreMeters.Length
			&& _playerScoreMeters[playerIndex] is ScoreAvatarMeter meter
			&& GodotObject.IsInstanceValid(meter))
		{
			return meter.GetGlobalRect().GetCenter();
		}

		if (playerIndex >= 0 && playerIndex < _playerCardPiles.Count && GodotObject.IsInstanceValid(_playerCardPiles[playerIndex]))
		{
			return _playerCardPiles[playerIndex].GlobalPosition + CardPileSize / 2.0f;
		}

		return Size / 2.0f;
	}

	private async Task PlayAdventureAttackEffectFlightAsync(
		Vector2 startCenter,
		Vector2 targetCenter,
		IReadOnlyList<Texture2D> frames,
		int turnSequenceGeneration,
		Action? onTouchTarget,
		float flightSeconds)
	{
		var effect = new Sprite2D
		{
			Name = "AdventureAttackEffectFlight",
			Texture = frames[0],
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(effect);

		Vector2 travel = targetCenter - startCenter;
		Vector2 side = travel.LengthSquared() > 0.001f
			? new Vector2(-travel.Y, travel.X).Normalized()
			: Vector2.Zero;
		Vector2 controlCenter = (startCenter + targetCenter) / 2.0f
			+ new Vector2(0.0f, -38.0f)
			+ side * _rng.RandfRange(-18.0f, 18.0f);

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			ApplyAdventureAttackEffectTexture(effect, frames[0], AdventureAttackEffectVisualSize, 0.78f);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				effect,
				frames,
				flightSeconds / Math.Max(1, frames.Count),
				flightSeconds);
			tween.TweenProperty(effect, "modulate:a", 1.0f, flightSeconds * 0.12f);
			tween.TweenProperty(effect, "modulate:a", 0.86f, flightSeconds * 0.18f)
				.SetDelay(flightSeconds * 0.82f);
			tween.TweenProperty(effect, "scale", GetSpriteScaleForVisualSize(frames[0], AdventureAttackEffectVisualSize) * 1.08f, flightSeconds * 0.5f);
			tween.TweenProperty(effect, "scale", GetSpriteScaleForVisualSize(frames[0], AdventureAttackEffectVisualSize) * 0.78f, flightSeconds * 0.5f)
				.SetDelay(flightSeconds * 0.5f);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateSpriteBezierFlight(
					effect,
					startCenter,
					controlCenter,
					targetCenter,
					progress)),
				0.0f,
				1.0f,
				flightSeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				onTouchTarget?.Invoke();
			}
		}
		finally
		{
			if (GodotObject.IsInstanceValid(effect))
			{
				effect.QueueFree();
			}
		}
	}

	private async Task PlayAdventureAttackImpactPulseAsync(Vector2 targetCenter, IReadOnlyList<Texture2D> frames)
	{
		if (frames.Count == 0)
		{
			return;
		}

		var pulse = new Sprite2D
		{
			Name = "AdventureAttackImpactPulse",
			Texture = frames[^1],
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = AdventureAttackEffectTravelZIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.76f),
		};
		AddChild(pulse);
		pulse.GlobalPosition = targetCenter;
		Vector2 baseScale = GetSpriteScaleForVisualSize(frames[^1], AdventureAttackImpactVisualSize) * 0.72f;
		pulse.Scale = baseScale;

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(pulse, "scale", baseScale * 1.34f, 0.22f);
			tween.TweenProperty(pulse, "modulate:a", 0.0f, 0.22f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(pulse))
			{
				pulse.QueueFree();
			}
		}
	}

	private async Task PlayScoreFillEffectFlightAsync(
		Vector2 cardCenter,
		Vector2 targetCenter,
		IReadOnlyList<Texture2D> frames,
		int effectIndex,
		float startDelaySeconds,
		Action onTouchTarget)
	{
		if (startDelaySeconds > 0.0f)
		{
			await ToSignal(GetTree().CreateTimer(startDelaySeconds), SceneTreeTimer.SignalName.Timeout);
		}

		var effect = new Sprite2D
		{
			Name = "ScoreFillEffectFlight",
			Texture = frames[0],
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = ScoreFillEffectTravelZIndex + effectIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(effect);

		Vector2 startCenter = cardCenter + RandomUnitVector() * _rng.RandfRange(4.0f, 16.0f);
		Vector2 travel = targetCenter - startCenter;
		Vector2 side = travel.LengthSquared() > 0.001f
			? new Vector2(-travel.Y, travel.X).Normalized()
			: Vector2.Zero;
		float arcHeight = _rng.RandfRange(58.0f, 96.0f);
		float sideDrift = _rng.RandfRange(-34.0f, 34.0f);
		Vector2 controlCenter = (startCenter + targetCenter) / 2.0f
			+ new Vector2(0.0f, -arcHeight)
			+ side * sideDrift;

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			ApplyScoreFillEffectTexture(effect, frames[0], ScoreFillEffectVisualSize, 0.92f);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				effect,
				frames,
				ScoreFillEffectFlightSeconds / Math.Max(1, frames.Count),
				ScoreFillEffectFlightSeconds);
			tween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(effect))
				{
					effect.ZIndex = ScoreFillEffectBehindAvatarZIndex;
				}
			})).SetDelay(ScoreFillEffectFlightSeconds * ScoreFillEffectBehindAvatarProgress);
			tween.TweenProperty(effect, "modulate:a", 1.0f, ScoreFillEffectFlightSeconds * 0.14f);
			tween.TweenProperty(effect, "scale", GetSpriteScaleForVisualSize(frames[0], ScoreFillEffectVisualSize) * 0.48f, ScoreFillEffectFlightSeconds);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateSpriteBezierFlight(
					effect,
					startCenter,
					controlCenter,
					targetCenter,
					progress)),
				0.0f,
				1.0f,
				ScoreFillEffectFlightSeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
			onTouchTarget.Invoke();
		}
		finally
		{
			if (GodotObject.IsInstanceValid(effect))
			{
				effect.QueueFree();
			}
		}
	}

	private async Task PlayScoreFillArrivalPulseAsync(Vector2 targetCenter, IReadOnlyList<Texture2D> frames)
	{
		if (frames.Count == 0)
		{
			return;
		}

		var pulse = new Sprite2D
		{
			Name = "ScoreFillEffectArrivalPulse",
			Texture = frames[^1],
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Centered = true,
			ZIndex = ScoreFillEffectBehindAvatarZIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.72f),
		};
		AddChild(pulse);
		pulse.GlobalPosition = targetCenter;
		Vector2 baseScale = GetSpriteScaleForVisualSize(frames[^1], ScoreFillPulseVisualSize);
		pulse.Scale = baseScale;

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(pulse, "scale", baseScale * 1.22f, 0.18f);
			tween.TweenProperty(pulse, "modulate:a", 0.0f, 0.18f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(pulse))
			{
				pulse.QueueFree();
			}
		}
	}

	private static void UpdateSpriteBezierFlight(
		Node2D effect,
		Vector2 startCenter,
		Vector2 controlCenter,
		Vector2 targetCenter,
		float progress,
		float rotationOffset = 0.0f)
	{
		if (!GodotObject.IsInstanceValid(effect))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		Vector2 center = QuadraticBezier(startCenter, controlCenter, targetCenter, easedProgress);
		Vector2 tangent = QuadraticBezierTangent(startCenter, controlCenter, targetCenter, easedProgress);
		effect.GlobalPosition = center;
		if (tangent.LengthSquared() > 0.001f)
		{
			effect.Rotation = tangent.Angle() + rotationOffset;
		}
	}

	private static void UpdateSpinningSpriteBezierFlight(
		Sprite2D effect,
		Vector2 startCenter,
		Vector2 controlCenter,
		Vector2 targetCenter,
		float progress,
		float rotationOffset,
		float spinRadians)
	{
		if (!GodotObject.IsInstanceValid(effect))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		Vector2 center = QuadraticBezier(startCenter, controlCenter, targetCenter, easedProgress);
		Vector2 tangent = QuadraticBezierTangent(startCenter, controlCenter, targetCenter, easedProgress);
		effect.GlobalPosition = center;
		if (tangent.LengthSquared() > 0.001f)
		{
			effect.Rotation = tangent.Angle() + rotationOffset + clampedProgress * spinRadians;
		}
	}

	private static void UpdateSpriteRotation(Sprite2D sprite, float rotation)
	{
		if (!GodotObject.IsInstanceValid(sprite))
		{
			return;
		}

		sprite.Rotation = rotation;
	}

	private static void ApplyScoreFillEffectTexture(Sprite2D effect, Texture2D texture, float visualSize, float scale)
	{
		effect.Texture = texture;
		effect.Scale = GetSpriteScaleForVisualSize(texture, visualSize) * scale;
	}

	private static void ApplyAdventureAttackEffectTexture(Sprite2D effect, Texture2D texture, float visualSize, float scale)
	{
		effect.Texture = texture;
		effect.Scale = GetSpriteScaleForVisualSize(texture, visualSize) * scale;
	}

	private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
	{
		float inverseT = 1.0f - t;
		return inverseT * inverseT * start + 2.0f * inverseT * t * control + t * t * end;
	}

	private static Vector2 QuadraticBezierTangent(Vector2 start, Vector2 control, Vector2 end, float t)
	{
		return 2.0f * (1.0f - t) * (control - start) + 2.0f * t * (end - control);
	}

	private static Vector2 GetSpriteScaleForVisualSize(Texture2D texture, float visualSize)
	{
		float textureWidth = Math.Max(1.0f, texture.GetWidth());
		float scale = visualSize / textureWidth;
		return new Vector2(scale, scale);
	}

	private Vector2 RandomUnitVector()
	{
		float angle = _rng.RandfRange(0.0f, Mathf.Tau);
		return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
	}
}
