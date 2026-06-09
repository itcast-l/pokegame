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
			IsLocalHandPlayer(winnerPlayerIndex) ? GameMusic.Victory : GameMusic.Defeat,
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

	private void PlayPlayerAvatarActionSfx(int playerIndex, CharacterAvatar.AvatarAction action)
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
				GameAudio?.PlayCharacterActionSfx(GetCharacterAudioConfigForPlayer(playerIndex), action, -4.0f, 0.97f, 1.04f, 0.08);
				break;
			case CharacterAvatar.AvatarAction.Attack:
				GameAudio?.PlayCharacterActionSfx(
					GetCharacterAudioConfigForPlayer(playerIndex),
					action,
					ShouldPlayCatAdventureScratchAttack(playerIndex) ? -8.0f : -3.0f,
					0.97f,
					1.03f,
					0.10);
				break;
			case CharacterAvatar.AvatarAction.Hit:
				GameAudio?.PlayCharacterActionSfx(GetCharacterAudioConfigForPlayer(playerIndex), action, -2.0f, 0.97f, 1.03f, 0.08);
				break;
			case CharacterAvatar.AvatarAction.Victory:
				GameAudio?.PlayCharacterActionSfx(GetCharacterAudioConfigForPlayer(playerIndex), action, -1.5f, 0.98f, 1.02f, 0.50);
				break;
			case CharacterAvatar.AvatarAction.Defeat:
				GameAudio?.PlayCharacterActionSfx(GetCharacterAudioConfigForPlayer(playerIndex), action, -1.5f, 0.98f, 1.02f, 0.50);
				break;
		}
	}

	private CharacterAudioConfig? GetCharacterAudioConfigForPlayer(int playerIndex)
	{
		if (playerIndex >= 0
			&& playerIndex < _playerAvatars.Length
			&& _playerAvatars[playerIndex] is CharacterAvatar avatar
			&& GodotObject.IsInstanceValid(avatar)
			&& avatar.AudioConfig != null)
		{
			return avatar.AudioConfig;
		}

		if (!adventure_mode && TryGetNetworkAvatarId(playerIndex, out string avatarId))
		{
			return CharacterAvatarCatalog.GetById(avatarId).AudioConfig;
		}

		if (adventure_mode)
		{
			if (playerIndex == MainPlayerIndex)
			{
				return CharacterAvatarCatalog.Default.AudioConfig;
			}

			if (IsAdventureEnemyPlayer(playerIndex))
			{
				return CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id).AudioConfig;
			}
		}

		return CharacterAvatarCatalog.LoadSelectedAvatar().AudioConfig;
	}

	private void PlayAdventureAttackFlightSfx(int attackerPlayerIndex)
	{
		if (adventure_mode)
		{
			GameAudio?.PlayCharacterAttackFlightSfx(
				GetCharacterAudioConfigForPlayer(attackerPlayerIndex),
				-4.0f,
				0.96f,
				1.05f,
				0.08);
		}
	}

	private void PlayAdventureAttackImpactSfx(int attackerPlayerIndex)
	{
		if (adventure_mode)
		{
			GameAudio?.PlayCharacterAttackImpactSfx(
				GetCharacterAudioConfigForPlayer(attackerPlayerIndex),
				-2.5f,
				0.97f,
				1.04f,
				0.05);
		}
	}

	private void PlayAdventureAttackStyleSfx(int attackerPlayerIndex, string attackEffectStyle)
	{
		if (!adventure_mode)
		{
			return;
		}

		CharacterAudioConfig? audioConfig = GetCharacterAudioConfigForPlayer(attackerPlayerIndex);
		switch (attackEffectStyle)
		{
			case AdventureAttackEffectStyles.FishBeam:
				GameAudio?.PlayCharacterAttackStyleSfx(audioConfig, attackEffectStyle, -2.0f, 0.98f, 1.03f, 0.12);
				break;
			case AdventureAttackEffectStyles.CrabPincer:
				GameAudio?.PlayCharacterAttackStyleSfx(audioConfig, attackEffectStyle, -2.0f, 0.97f, 1.04f, 0.12);
				break;
			case AdventureAttackEffectStyles.JellyfishTentacle:
				GameAudio?.PlayCharacterAttackStyleSfx(audioConfig, attackEffectStyle, -2.0f, 0.97f, 1.04f, 0.12);
				break;
			case AdventureAttackEffectStyles.MantaRayCyclone:
				GameAudio?.PlayCharacterAttackStyleSfx(audioConfig, attackEffectStyle, -1.5f, 0.98f, 1.02f, 0.18);
				break;
			case AdventureAttackEffectStyles.SeaSlugMarker:
				GameAudio?.PlayCharacterAttackStyleSfx(audioConfig, attackEffectStyle, -2.0f, 0.97f, 1.04f, 0.12);
				break;
			case AdventureAttackEffectStyles.GardenEelBurrow:
				GameAudio?.PlayCharacterAttackStyleSfx(audioConfig, attackEffectStyle, -2.0f, 0.97f, 1.04f, 0.16);
				break;
			case AdventureAttackEffectStyles.LeafySeadragonSlash:
				GameAudio?.PlayCharacterAttackStyleSfx(audioConfig, attackEffectStyle, -2.0f, 0.97f, 1.04f, 0.12);
				break;
			case AdventureAttackEffectStyles.DragonPrinceHit:
				GameAudio?.PlayCharacterAttackStyleSfx(audioConfig, attackEffectStyle, -2.0f, 0.97f, 1.04f, 0.12);
				break;
			case AdventureAttackEffectStyles.DragonQueenJaw:
				GameAudio?.PlayCharacterAttackStyleSfx(audioConfig, attackEffectStyle, -1.5f, 0.97f, 1.03f, 0.16);
				break;
			case AdventureAttackEffectStyles.BossKRoyalSeizure:
				GameAudio?.PlayCharacterAttackStyleSfx(audioConfig, attackEffectStyle, -1.0f, 0.96f, 1.02f, 0.20);
				break;
		}
	}

	private void PlayAdventureAbilitySfx(int playerIndex, string abilityId)
	{
		if (!adventure_mode)
		{
			return;
		}

		CharacterAudioConfig? audioConfig = GetCharacterAudioConfigForPlayer(playerIndex);
		switch (abilityId)
		{
			case AdventureEnemyAbilities.TideGuard:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -1.5f, 0.98f, 1.03f, 0.18);
				break;
			case AdventureEnemyAbilities.BubbleCover:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -1.5f, 0.98f, 1.03f, 0.18);
				break;
			case AdventureEnemyAbilities.SandAmbush:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -2.0f, 0.97f, 1.04f, 0.16);
				break;
			case AdventureEnemyAbilities.ColorCurrent:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -2.0f, 0.97f, 1.04f, 0.16);
				break;
			case AdventureEnemyAbilities.SpiralShell:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -1.5f, 0.98f, 1.02f, 0.18);
				break;
			case AdventureEnemyAbilities.TuskBreak:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -1.5f, 0.98f, 1.03f, 0.18);
				break;
			case AdventureEnemyAbilities.TideShift:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -1.5f, 0.98f, 1.02f, 0.18);
				break;
			case AdventureEnemyAbilities.LeafyMirage:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -2.0f, 0.97f, 1.04f, 0.12);
				break;
			case AdventureEnemyAbilities.DragonPrince:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -1.5f, 0.97f, 1.03f, 0.18);
				break;
			case AdventureEnemyAbilities.DragonQueen:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -1.5f, 0.97f, 1.03f, 0.18);
				break;
			case AdventureEnemyAbilities.DragonKing:
				GameAudio?.PlayCharacterAbilitySfx(audioConfig, abilityId, -1.0f, 0.96f, 1.02f, 0.22);
				break;
		}
	}
}
