using Godot;

public partial class GameBoardScene
{
	private GameAudioManager? _gameAudio;

	private GameAudioManager? GameAudio => _gameAudio ??= GetNodeOrNull<GameAudioManager>("/root/GameAudio");

	private void PlayGameSfx(
		string sfxId,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		GameAudio?.PlaySfx(sfxId, volumeDb, pitchMin, pitchMax, cooldownSeconds);
	}

	private void PlayBoardMusic()
	{
		GameAudio?.SetGameplayTensionMusic(false, 0.0f);
		GameAudio?.PlayMusic(GameMusic.Gameplay, adventure_mode ? 1.2f : 1.6f);
		RefreshGameplayTensionMusic();
	}

	private void PlayResultMusic(int winnerPlayerIndex)
	{
		GameAudio?.PlayMusic(
			winnerPlayerIndex == MainPlayerIndex ? GameMusic.Victory : GameMusic.Defeat,
			0.8f,
			restartIfSame: true);
	}

	private void RefreshGameplayTensionMusic()
	{
		if (IsGameOver())
		{
			return;
		}

		GameAudio?.SetGameplayTensionMusic(ShouldUseGameplayTensionMusic(), 1.2f);
	}

	private bool ShouldUseGameplayTensionMusic()
	{
		if (adventure_mode)
		{
			for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
			{
				int limit = GetScoreLimit(playerIndex);
				if (limit > 0 && _playerScores[playerIndex] <= Mathf.CeilToInt(limit * 0.30f))
				{
					return true;
				}
			}

			return false;
		}

		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (_playerScores[playerIndex] >= Mathf.CeilToInt(_winningScore * 0.75f))
			{
				return true;
			}
		}

		return false;
	}

	private void PlaySpecialItemCardPlaySfx()
	{
		PlayGameSfx(GameSfx.SpecialItemCardPlay, -2.0f, 0.97f, 1.04f, 0.04);
	}

	private void PlayPlayerAvatarActionSfx(CharacterAvatar.AvatarAction action)
	{
		if (!adventure_mode
			&& action != CharacterAvatar.AvatarAction.Victory
			&& action != CharacterAvatar.AvatarAction.Defeat)
		{
			return;
		}

		switch (action)
		{
			case CharacterAvatar.AvatarAction.Score:
				PlayGameSfx(GameSfx.AvatarScore, -4.0f, 0.97f, 1.04f, 0.08);
				break;
			case CharacterAvatar.AvatarAction.Attack:
				PlayGameSfx(GameSfx.AvatarAttack, -3.0f, 0.97f, 1.03f, 0.10);
				break;
			case CharacterAvatar.AvatarAction.Hit:
				PlayGameSfx(GameSfx.AvatarHit, -2.0f, 0.97f, 1.03f, 0.08);
				break;
			case CharacterAvatar.AvatarAction.Victory:
				PlayGameSfx(GameSfx.AvatarVictory, -1.5f, 0.98f, 1.02f, 0.50);
				break;
			case CharacterAvatar.AvatarAction.Defeat:
				PlayGameSfx(GameSfx.AvatarDefeat, -1.5f, 0.98f, 1.02f, 0.50);
				break;
		}
	}

	private void PlayAdventureAttackFlightSfx()
	{
		if (adventure_mode)
		{
			PlayGameSfx(GameSfx.AdventureAttackFlight, -4.0f, 0.96f, 1.05f, 0.08);
		}
	}

	private void PlayAdventureAttackImpactSfx()
	{
		if (adventure_mode)
		{
			PlayGameSfx(GameSfx.AdventureAttackImpact, -2.5f, 0.97f, 1.04f, 0.05);
		}
	}

	private void PlayAdventureAttackStyleSfx(string attackEffectStyle)
	{
		if (!adventure_mode)
		{
			return;
		}

		switch (attackEffectStyle)
		{
			case AdventureAttackEffectStyles.FishBeam:
				PlayGameSfx(GameSfx.FishBeam, -2.0f, 0.98f, 1.03f, 0.12);
				break;
			case AdventureAttackEffectStyles.MantaRayCyclone:
				PlayGameSfx(GameSfx.MantaRayTideShift, -1.5f, 0.98f, 1.02f, 0.18);
				break;
			case AdventureAttackEffectStyles.GardenEelBurrow:
				PlayGameSfx(GameSfx.GardenEelAmbush, -2.0f, 0.97f, 1.04f, 0.16);
				break;
			case AdventureAttackEffectStyles.LeafySeadragonSlash:
				PlayGameSfx(GameSfx.LeafySeadragonBlade, -2.0f, 0.97f, 1.04f, 0.12);
				break;
		}
	}

	private void PlayAdventureAbilitySfx(string abilityId)
	{
		if (!adventure_mode)
		{
			return;
		}

		switch (abilityId)
		{
			case AdventureEnemyAbilities.BubbleCover:
				PlayGameSfx(GameSfx.JellyfishBubbleCover, -1.5f, 0.98f, 1.03f, 0.18);
				break;
			case AdventureEnemyAbilities.SandAmbush:
				PlayGameSfx(GameSfx.GardenEelAmbush, -2.0f, 0.97f, 1.04f, 0.16);
				break;
			case AdventureEnemyAbilities.SpiralShell:
				PlayGameSfx(GameSfx.NautilusSpiralShell, -1.5f, 0.98f, 1.02f, 0.18);
				break;
			case AdventureEnemyAbilities.TuskBreak:
				PlayGameSfx(GameSfx.NarwhalTuskBreak, -1.5f, 0.98f, 1.03f, 0.18);
				break;
			case AdventureEnemyAbilities.TideShift:
				PlayGameSfx(GameSfx.MantaRayTideShift, -1.5f, 0.98f, 1.02f, 0.18);
				break;
			case AdventureEnemyAbilities.LeafyMirage:
				PlayGameSfx(GameSfx.LeafySeadragonBlade, -2.0f, 0.97f, 1.04f, 0.12);
				break;
		}
	}
}
