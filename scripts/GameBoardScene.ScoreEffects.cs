using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private delegate Task AdventureAttackEffectHandler(
		int attackerPlayerIndex,
		int targetPlayerIndex,
		Vector2 startCenter,
		Vector2 targetCenter,
		IReadOnlyList<Texture2D> attackFrames,
		int turnSequenceGeneration,
		Action? onHit);

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
	private const float BossKBorrowedAbilityAvatarFxTotalSeconds = 0.62f;
	private const float BossKBorrowedAbilityAvatarFxPeakAlpha = 0.54f;
	private const float BossKBorrowedAbilityAvatarFxPortraitScale = 0.90f;
	private const float BossKBorrowedAbilityAvatarFxStartScale = 0.82f;
	private const float BossKBorrowedAbilityAvatarFxPeakScale = 1.06f;
	private const float BossKBorrowedAbilityAvatarFxEndScale = 0.96f;
	private const int BossKBorrowedAbilityAvatarFxZIndex = EnemySpecialAbilityAvatarFxZIndex + 1;
	private const float MantaRayTideShiftCycloneSeconds = 0.72f;
	private const float MantaRayTideShiftCycloneFrameSeconds = 0.06f;
	private const float MantaRayTideShiftProjectileVisualSize = 188.0f;
	private const float AdventureAttackEffectFlightSeconds = 0.48f;
	private const int DragonPrinceDamageHitFlightFrameCount = 5;
	private const float DragonPrinceDamageHitFlightSeconds = 0.72f;
	private const float DragonPrinceDamageHitImpactSeconds = 0.70f;
	private const double DragonPrinceDamageHitImpactFrameSeconds = 0.10d;
	private const double DragonPrinceDamageHitImpactHitDelaySeconds = DragonPrinceDamageHitImpactFrameSeconds;
	private const float DragonPrinceDamageHitVisualSize = 132.0f;
	private const float DragonPrinceDamageHitImpactVisualSize = 148.0f;
	private const float DragonPrinceDamageHitStartInset = 34.0f;
	private const float DragonPrinceDamageHitTargetInset = 20.0f;
	private const float DragonPrinceDamageHitRotationOffset = 0.7853982f;
	private const float DragonQueenJawSlashVisualScale = 1.02f;
	private const float DragonQueenJawSlashStartScale = 0.90f;
	private const float DragonQueenJawSlashPeakScale = 1.08f;
	private const float DragonQueenJawSlashEndScale = 0.98f;
	private const double DragonQueenJawSlashFrameSeconds = 0.10d;
	private const double DragonQueenJawSlashTotalSeconds = 0.44d;
	private const double DragonQueenJawSlashHitDelaySeconds = DragonQueenJawSlashFrameSeconds * 2.0d;
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
	private static readonly double[] CatAdventureScratchFrameKeyTimes =
	{
		0.0d,
		CatAdventureScratchFrame2DelaySeconds,
		CatAdventureScratchFrame3DelaySeconds,
		CatAdventureScratchFrame4DelaySeconds,
	};
	private const float CrabAdventurePincerVisualSize = 150.0f;
	private const float CrabAdventurePincerTotalSeconds = 0.72f;
	private const double CrabAdventurePincerFrame2DelaySeconds = 0.16d;
	private const double CrabAdventurePincerFrame3DelaySeconds = 0.32d;
	private const double CrabAdventurePincerFrame4DelaySeconds = 0.48d;
	private const double CrabAdventurePincerHitDelaySeconds = CrabAdventurePincerFrame3DelaySeconds;
	private static readonly double[] CrabAdventurePincerFrameKeyTimes =
	{
		0.0d,
		CrabAdventurePincerFrame2DelaySeconds,
		CrabAdventurePincerFrame3DelaySeconds,
		CrabAdventurePincerFrame4DelaySeconds,
	};
	private const float JellyfishAdventureTentacleLocalVisualScale = 1.44f;
	private const float JellyfishAdventureTentacleLocalStartScale = 0.92f;
	private const float JellyfishAdventureTentacleLocalEndScale = 1.04f;
	private const float JellyfishAdventureTentacleLocalRotationDegrees = -6.0f;
	private const float JellyfishAdventureTentacleTotalSeconds = 0.78f;
	private const double JellyfishAdventureTentacleFrame2DelaySeconds = 0.18d;
	private const double JellyfishAdventureTentacleFrame3DelaySeconds = 0.38d;
	private const double JellyfishAdventureTentacleFrame4DelaySeconds = 0.58d;
	private const double JellyfishAdventureTentacleHitDelaySeconds = JellyfishAdventureTentacleFrame3DelaySeconds;
	private static readonly double[] JellyfishAdventureTentacleFrameKeyTimes =
	{
		0.0d,
		JellyfishAdventureTentacleFrame2DelaySeconds,
		JellyfishAdventureTentacleFrame3DelaySeconds,
		JellyfishAdventureTentacleFrame4DelaySeconds,
	};
	private const float SeaSlugHitMarkerLocalVisualScale = 0.92f;
	private const float SeaSlugHitMarkerStartScale = 0.88f;
	private const float SeaSlugHitMarkerEndScale = 1.06f;
	private const double SeaSlugHitMarkerFrameSeconds = 0.115d;
	private const double SeaSlugHitMarkerTotalSeconds = SeaSlugHitMarkerFrameSeconds * AdventureDamageEffectFrameCount;
	private const double SeaSlugHitMarkerHitDelaySeconds = SeaSlugHitMarkerFrameSeconds * 2.0d;
	private const float GardenEelBurrowAmbushVisualScale = 1.28f;
	private const float GardenEelBurrowAmbushStartScale = 0.92f;
	private const float GardenEelBurrowAmbushEmergeScale = 1.08f;
	private const float GardenEelBurrowAmbushStartUpOffsetRatio = 0.16f;
	private const float GardenEelBurrowAmbushTargetUpOffsetRatio = 0.16f;
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
	private const double LeafySeadragonCollectAttackDissolveFrameDelaySeconds = LeafySeadragonCollectAttackImpactSeconds * 0.44d;
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
	private const float BossKRoyalSeizureLanceFlightSeconds = 0.56f;
	private const float BossKRoyalSeizureLanceVisualSize = 136.0f;
	private const float BossKRoyalSeizureLanceStartInset = 42.0f;
	private const float BossKRoyalSeizureLanceTargetInset = 20.0f;
	private const float BossKRoyalSeizureLanceArcHeight = 32.0f;
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
		Task[] flightTasks = new Task[Math.Min(capturedCardVisuals.Count, MaxScoreFillEffectFlights)];
		int flightTaskCount = 0;
		bool scoreAwarded = false;
		for (int visualIndex = 0; visualIndex < capturedCardVisuals.Count && flightTaskCount < flightTasks.Length; visualIndex++)
		{
			Vector2 cardCenter = capturedCardVisuals[visualIndex].GlobalPosition + CardVisualSize / 2.0f;
			flightTasks[flightTaskCount] = PlayScoreFillEffectFlightAsync(
				cardCenter,
				targetCenter,
				scoreFillEffectFrames,
				flightTaskCount,
				visualIndex * ScoreFillEffectStaggerSeconds,
				() =>
				{
					if (scoreAwarded || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return;
					}

					scoreAwarded = true;
					onArrival?.Invoke();
				});
			flightTaskCount++;
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
		if (collectionFrames.Count > 0 && capturedCardVisuals.Count > 0)
		{
			Task[] collectionTasks = new Task[Math.Min(capturedCardVisuals.Count, MaxScoreFillEffectFlights)];
			int collectionTaskCount = 0;
			for (int visualIndex = 0; visualIndex < capturedCardVisuals.Count && collectionTaskCount < collectionTasks.Length; visualIndex++)
			{
				Vector2 cardCenter = capturedCardVisuals[visualIndex].GlobalPosition + CardVisualSize / 2.0f;
				collectionTasks[collectionTaskCount] = PlayScoreFillEffectFlightAsync(
					cardCenter,
					attackerCenter,
					collectionFrames,
					collectionTaskCount,
					visualIndex * ScoreFillEffectStaggerSeconds,
					() => { });
				collectionTaskCount++;
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

		if (TryGetAdventureAttackEffectHandler(attackerPlayerIndex, targetPlayerIndex, out AdventureAttackEffectHandler attackEffectHandler))
		{
			await attackEffectHandler(
				attackerPlayerIndex,
				targetPlayerIndex,
				startCenter,
				targetCenter,
				attackFrames,
				turnSequenceGeneration,
				onHit);
			return;
		}

		await PlayAdventureAttackEffectFlightAsync(
			attackerPlayerIndex,
			startCenter,
			targetCenter,
			attackFrames,
			turnSequenceGeneration,
			onHit,
			GetAdventureAttackEffectFlightSeconds(attackerPlayerIndex));

		if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
		{
			await PlayAdventureAttackImpactPulseAsync(attackerPlayerIndex, targetCenter, attackFrames);
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

	private Dictionary<string, AdventureAttackEffectHandler> CreateAdventureAttackEffectHandlers()
	{
		return new Dictionary<string, AdventureAttackEffectHandler>(StringComparer.Ordinal)
		{
			[AdventureAttackEffectStyles.FishBeam] = (attackerPlayerIndex, _, startCenter, targetCenter, frames, turnSequenceGeneration, onHit) =>
				PlayFishAdventureAttackBeamAsync(attackerPlayerIndex, startCenter, targetCenter, frames, turnSequenceGeneration, onHit),
			[AdventureAttackEffectStyles.CrabPincer] = (attackerPlayerIndex, _, _, targetCenter, frames, turnSequenceGeneration, onHit) =>
				PlayCrabAdventurePincerAttackAsync(attackerPlayerIndex, targetCenter, frames, turnSequenceGeneration, onHit),
			[AdventureAttackEffectStyles.JellyfishTentacle] = (attackerPlayerIndex, targetPlayerIndex, _, targetCenter, frames, turnSequenceGeneration, onHit) =>
				PlayJellyfishAdventureTentacleAttackAsync(attackerPlayerIndex, targetPlayerIndex, targetCenter, frames, turnSequenceGeneration, onHit),
			[AdventureAttackEffectStyles.MantaRayCyclone] = (attackerPlayerIndex, targetPlayerIndex, _, _, _, turnSequenceGeneration, onHit) =>
				PlayMantaRayTideShiftCycloneAttackAsync(attackerPlayerIndex, targetPlayerIndex, turnSequenceGeneration, onHit),
			[AdventureAttackEffectStyles.SeaSlugMarker] = (attackerPlayerIndex, targetPlayerIndex, _, _, frames, turnSequenceGeneration, onHit) =>
				PlaySeaSlugHitMarkerAttackAsync(attackerPlayerIndex, targetPlayerIndex, frames, turnSequenceGeneration, onHit),
			[AdventureAttackEffectStyles.GardenEelBurrow] = (attackerPlayerIndex, targetPlayerIndex, _, _, frames, turnSequenceGeneration, onHit) =>
				PlayGardenEelBurrowAmbushAttackAsync(attackerPlayerIndex, targetPlayerIndex, frames, turnSequenceGeneration, onHit),
			[AdventureAttackEffectStyles.LeafySeadragonSlash] = (attackerPlayerIndex, _, startCenter, targetCenter, frames, turnSequenceGeneration, onHit) =>
				PlayLeafySeadragonCollectAttackAsync(attackerPlayerIndex, startCenter, targetCenter, frames, turnSequenceGeneration, onHit),
			[AdventureAttackEffectStyles.BossKRoyalSeizure] = (attackerPlayerIndex, _, _, targetCenter, _, turnSequenceGeneration, onHit) =>
				PlayBossKRoyalSeizureLanceAttackAsync(attackerPlayerIndex, targetCenter, turnSequenceGeneration, onHit),
			[AdventureAttackEffectStyles.DragonPrinceHit] = (attackerPlayerIndex, _, startCenter, targetCenter, frames, turnSequenceGeneration, onHit) =>
				PlayDragonPrinceDamageHitAttackAsync(attackerPlayerIndex, startCenter, targetCenter, frames, turnSequenceGeneration, onHit),
			[AdventureAttackEffectStyles.DragonQueenJaw] = (attackerPlayerIndex, targetPlayerIndex, _, _, frames, turnSequenceGeneration, onHit) =>
				PlayDragonQueenJawSlashAttackAsync(attackerPlayerIndex, targetPlayerIndex, frames, turnSequenceGeneration, onHit),
		};
	}

	private bool TryGetAdventureAttackEffectHandler(
		int attackerPlayerIndex,
		int targetPlayerIndex,
		out AdventureAttackEffectHandler attackEffectHandler)
	{
		attackEffectHandler = null!;
		if (!IsAdventureEnemyPlayer(attackerPlayerIndex)
			|| targetPlayerIndex != MainPlayerIndex
			|| attackerPlayerIndex == MainPlayerIndex)
		{
			return false;
		}

		string attackEffectStyle = CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id).AttackEffectStyle;
		if (string.Equals(attackEffectStyle, AdventureAttackEffectStyles.MantaRayCyclone, StringComparison.Ordinal)
			&& _mantaRayTideShiftCycloneFxFrames.Count == 0)
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(attackEffectStyle)
			|| !_adventureAttackEffectHandlers.TryGetValue(attackEffectStyle, out AdventureAttackEffectHandler? resolvedHandler))
		{
			return false;
		}

		attackEffectHandler = resolvedHandler;
		return true;
	}

	private float GetAdventureAttackEffectFlightSeconds(int attackerPlayerIndex)
	{
		if (!IsAdventureEnemyPlayer(attackerPlayerIndex))
		{
			return AdventureAttackEffectFlightSeconds;
		}

		CharacterAvatarDefinition enemyDefinition = CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id);
		return enemyDefinition.AttackEffectFlightSeconds > 0.0f
			? enemyDefinition.AttackEffectFlightSeconds
			: AdventureAttackEffectFlightSeconds;
	}

}
