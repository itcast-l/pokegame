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

	private async Task WarmupGameplayResourcesAsync(int turnSequenceGeneration)
	{
		if (!await WaitForWarmupFrameAsync(turnSequenceGeneration))
		{
			return;
		}

		await WarmupSfxGroupAsync(CoreWarmupSfx, turnSequenceGeneration);
		if (item_mode)
		{
			await WarmupSfxGroupAsync(ItemWarmupSfx, turnSequenceGeneration);
		}

		if (adventure_mode)
		{
			await WarmupCharacterAudioConfigsAsync(turnSequenceGeneration);
		}

		if (!await WaitForWarmupFrameAsync(turnSequenceGeneration))
		{
			return;
		}

		GameAudio?.WarmupMusic(GameMusic.GameplayTension);
	}

	private async Task WarmupSfxGroupAsync(string[] sfxIds, int turnSequenceGeneration)
	{
		for (int index = 0; index < sfxIds.Length; index++)
		{
			ulong sampleStart = BeginPerformanceSample();
			GameAudio?.WarmupSfx(sfxIds[index]);
			EndPerformanceSample($"WarmupSfx:{sfxIds[index]}", sampleStart);

			if ((index + 1) % GameplayWarmupBatchSize == 0
				&& !await WaitForWarmupFrameAsync(turnSequenceGeneration))
			{
				return;
			}
		}
	}

	private async Task WarmupCharacterAudioConfigsAsync(int turnSequenceGeneration)
	{
		int warmedCount = 0;
		foreach (CharacterAvatarDefinition avatarDefinition in CharacterAvatarCatalog.All)
		{
			ulong sampleStart = BeginPerformanceSample();
			GameAudio?.WarmupCharacterAudioConfig(avatarDefinition.AudioConfig);
			EndPerformanceSample($"WarmupCharacterAudio:{avatarDefinition.Id}", sampleStart);

			warmedCount++;
			if (warmedCount % GameplayWarmupBatchSize == 0
				&& !await WaitForWarmupFrameAsync(turnSequenceGeneration))
			{
				return;
			}
		}
	}

	private async Task<bool> WaitForWarmupFrameAsync(int turnSequenceGeneration)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return IsInsideTree() && turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver();
	}
}
