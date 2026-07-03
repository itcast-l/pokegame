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
		ulong sampleStart = BeginPerformanceSample("ResourceLoad");
		EnsureCommonGameplayFxResourcesLoaded();
		EndPerformanceSample("WarmupOptionalFx:CoreGameplay", sampleStart);
		if (!await WaitForWarmupFrameAsync(turnContext))
		{
			return false;
		}

		if (item_mode)
		{
			ulong itemSampleStart = BeginPerformanceSample("ResourceLoad");
			EnsureItemModeOptionalFxResourcesLoaded();
			EndPerformanceSample("WarmupOptionalFx:ItemMode", itemSampleStart);
			if (!await WaitForWarmupFrameAsync(turnContext))
			{
				return false;
			}
		}

		if (adventure_mode)
		{
			ulong adventureSampleStart = BeginPerformanceSample("ResourceLoad");
			EnsureAdventureModeResourcesLoaded();
			EndPerformanceSample("WarmupOptionalFx:AdventureMode", adventureSampleStart);
			if (!await WaitForWarmupFrameAsync(turnContext))
			{
				return false;
			}
		}

		return true;
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
