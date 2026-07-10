using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const int GameplayWarmupBatchSize = 5;

	private static readonly string[] CoreWarmupSfx =
	{
		GameSfx.CardDeal,
		GameSfx.CardHover,
		GameSfx.CardPickup,
		GameSfx.CardFlight,
		GameSfx.CardLand,
		GameSfx.CaptureBite,
		GameSfx.CaptureLift,
		GameSfx.CaptureCollect,
		GameSfx.CardDraw,
		GameSfx.ScoreFlight,
		GameSfx.ScoreArrive,
		GameSfx.DeckShuffle,
	};

	private static readonly string[] ItemWarmupSfx =
	{
		GameSfx.SpecialItemCardPlay,
		GameSfx.SpecialTrigger,
		GameSfx.BombPlace,
		GameSfx.BombPairLock,
		GameSfx.BombWarning,
		GameSfx.BombExplosion,
		GameSfx.BombDestroyCard,
		GameSfx.LensScan,
		GameSfx.LensReveal,
		GameSfx.QuestionRewardTransform,
		GameSfx.QuestionRewardReveal,
		GameSfx.QuestionRewardInsert,
		GameSfx.EnchantTargetSelect,
		GameSfx.MagnetAttach,
		GameSfx.MagnetExtraCapture,
		GameSfx.DoubleCollect,
		GameSfx.ExtraTurn,
		GameSfx.ItemDrawCard,
		GameSfx.TableShuffle,
		GameSfx.StealCard,
	};

	private async Task WarmupGameplayResourcesAsync(TurnContext turnContext)
	{
		if (!await WaitForWarmupFrameAsync(turnContext))
		{
			return;
		}

		await WarmupSfxGroupAsync(CoreWarmupSfx, turnContext);
		if (item_mode)
		{
			await WarmupSfxGroupAsync(ItemWarmupSfx, turnContext);
		}

		if (adventure_mode)
		{
			await WarmupCharacterAudioConfigsAsync(turnContext);
		}

		if (!await WarmupOptionalFxResourcesAsync(turnContext))
		{
			return;
		}

		if (!await WaitForWarmupFrameAsync(turnContext))
		{
			return;
		}

		GameAudio?.WarmupMusic(GameMusic.GameplayTension);
	}

	private async Task WarmupSfxGroupAsync(string[] sfxIds, TurnContext turnContext)
	{
		for (int index = 0; index < sfxIds.Length; index++)
		{
			ulong sampleStart = BeginPerformanceSample("ResourceLoad");
			GameAudio?.WarmupSfx(sfxIds[index]);
			EndPerformanceSample($"WarmupSfx:{sfxIds[index]}", sampleStart);

			if ((index + 1) % GameplayWarmupBatchSize == 0
				&& !await WaitForWarmupFrameAsync(turnContext))
			{
				return;
			}
		}
	}

	private async Task WarmupCharacterAudioConfigsAsync(TurnContext turnContext)
	{
		int warmedCount = 0;
		foreach (CharacterAvatarDefinition avatarDefinition in CharacterAvatarCatalog.All)
		{
			ulong sampleStart = BeginPerformanceSample("ResourceLoad");
			GameAudio?.WarmupCharacterAudioConfig(avatarDefinition.AudioConfig);
			EndPerformanceSample($"WarmupCharacterAudio:{avatarDefinition.Id}", sampleStart);

			warmedCount++;
			if (warmedCount % GameplayWarmupBatchSize == 0
				&& !await WaitForWarmupFrameAsync(turnContext))
			{
				return;
			}
		}
	}

	private async Task<bool> WarmupOptionalFxResourcesAsync(TurnContext turnContext)
	{
		if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:CoreGameplay:ScoreFill", EnsureScoreFillEffectTexturesLoaded, turnContext))
		{
			return false;
		}

		if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:CoreGameplay:SpecialCardTrigger", EnsureSpecialCardTriggerEffectTexturesLoaded, turnContext))
		{
			return false;
		}

		_commonGameplayFxResourcesLoaded = true;

		if (item_mode)
		{
			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:ItemMode:QuestionCardTransform", EnsureQuestionCardTransformTexturesLoaded, turnContext))
			{
				return false;
			}

			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:ItemMode:BombPairDestroy", EnsureBombPairDestroyEffectTexturesLoaded, turnContext))
			{
				return false;
			}

			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:ItemMode:LensRevealWisp", EnsureLensRevealWispTexturesLoaded, turnContext))
			{
				return false;
			}

			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:ItemMode:MagnetCountdown", EnsureMagnetCountdownFxTexturesLoaded, turnContext))
			{
				return false;
			}

			_itemModeOptionalFxResourcesLoaded = true;
		}

		if (adventure_mode)
		{
			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:AdventureMode:EnemySpecialAbilityAvatar", EnsureEnemySpecialAbilityAvatarFxTexturesLoaded, turnContext))
			{
				return false;
			}

			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:AdventureMode:MantaRayTideShiftCyclone", EnsureMantaRayTideShiftCycloneFxTexturesLoaded, turnContext))
			{
				return false;
			}

			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:AdventureMode:LeafySeadragonBladeProjectile", EnsureLeafySeadragonBladeProjectileTexturesLoaded, turnContext))
			{
				return false;
			}

			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:AdventureMode:BossKRoyalSeizureLance", EnsureBossKRoyalSeizureLanceTexturesLoaded, turnContext))
			{
				return false;
			}

			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:AdventureMode:AdventureDamage", EnsureAdventureDamageEffectTexturesLoaded, turnContext))
			{
				return false;
			}

			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:AdventureMode:AdventureEnemyDamage", EnsureAdventureEnemyDamageEffectTexturesLoaded, turnContext))
			{
				return false;
			}

			if (!await WarmupOptionalFxGroupAsync("WarmupOptionalFx:AdventureMode:TraceHuntMarker", EnsureAdventureTraceHuntMarkerTexturesLoaded, turnContext))
			{
				return false;
			}

			ulong dragonPrinceSampleStart = BeginPerformanceSample("ResourceLoad");
			LoadDragonPrinceSpecialJackTexture();
			EndPerformanceSample("WarmupOptionalFx:AdventureMode:DragonPrinceSpecialJack", dragonPrinceSampleStart);
			if (!await WaitForWarmupFrameAsync(turnContext))
			{
				return false;
			}

			_adventureModeResourcesLoaded = true;
		}

		return true;
	}

	private async Task<bool> WarmupOptionalFxGroupAsync(string sampleName, Action ensureLoaded, TurnContext turnContext)
	{
		ulong sampleStart = BeginPerformanceSample("ResourceLoad");
		ensureLoaded();
		EndPerformanceSample(sampleName, sampleStart);
		return await WaitForWarmupFrameAsync(turnContext);
	}

	private async Task<bool> WaitForWarmupFrameAsync(TurnContext turnContext)
	{
		return await ResourcePreloadService.WaitForNextFrameAsync(
			this,
			turnContext,
			IsCurrentTurnContext,
			IsGameOver);
	}
}
