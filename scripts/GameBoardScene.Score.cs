using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private const int DefaultWinningScore = 100;
	private const int AdventurePlayerStartingHealth = 100;
	private const int AdventureEnemyNoAbilityStartingHealth = 50;
	private const int AdventureEnemyAbilityStartingHealth = 100;
	private const int AdventureEnemyRoyalStartingHealth = 100;
	private const int AdventureEnemyBossStartingHealth = 100;
	private const int AdventureEnemyPlayerIndex = 1;
	private static readonly string[] ScoreMeterNodeNames =
	{
		"Player1ScoreMeter",
		"Player2ScoreMeter",
		"Player3ScoreMeter",
		"Player4ScoreMeter",
	};
	private static readonly string[] ScoreEffectTargetNodeNames =
	{
		"Player1ScoreEffectTarget",
		"Player2ScoreEffectTarget",
		"Player3ScoreEffectTarget",
		"Player4ScoreEffectTarget",
	};
	private static readonly Color[] AvatarScoreFillColors =
	{
		new(0.94f, 0.74f, 0.36f, 0.52f),
		new(0.36f, 0.80f, 0.94f, 0.52f),
		new(0.94f, 0.46f, 0.72f, 0.50f),
		new(0.40f, 0.88f, 0.62f, 0.50f),
	};
	private static readonly Color[] AvatarScoreGlowColors =
	{
		new(1.0f, 0.95f, 0.72f, 0.36f),
		new(0.82f, 0.98f, 1.0f, 0.36f),
		new(1.0f, 0.82f, 0.92f, 0.34f),
		new(0.82f, 1.0f, 0.90f, 0.34f),
	};

	private int[] _playerScores => _state.PlayerScores;
	private readonly ScoreAvatarMeter?[] _playerScoreMeters = new ScoreAvatarMeter?[MaxPlayers];
	private readonly ScoreEffectTarget?[] _playerScoreEffectTargets = new ScoreEffectTarget?[MaxPlayers];
	private int _winningScore
	{
		get => _state.WinningScore;
		set => _state.WinningScore = value;
	}

	private void BindPlayerScoreMeters()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			int viewSlotIndex = GetPlayerViewSlotIndex(playerIndex);
			ScoreAvatarMeter? meter = GetNodeOrNull<ScoreAvatarMeter>($"%{ScoreMeterNodeNames[viewSlotIndex]}");
			if (meter == null)
			{
				GD.PushWarning($"Missing score meter node: {ScoreMeterNodeNames[viewSlotIndex]}.");
				continue;
			}

				BindPlayerStatusTooltipHover(meter, playerIndex);
				meter.Configure(AvatarScoreFillColors[playerIndex], AvatarScoreGlowColors[playerIndex]);
				meter.SetScore(GetStartingPlayerScore(playerIndex), GetScoreLimit(playerIndex), false);
				_playerScoreMeters[playerIndex] = meter;
				_playerScoreEffectTargets[playerIndex] = GetNodeOrNull<ScoreEffectTarget>($"%{ScoreEffectTargetNodeNames[viewSlotIndex]}");
			}
	}

	private void ResetPlayerScores()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerScores[playerIndex] = GetStartingPlayerScore(playerIndex);
			UpdatePlayerScoreMeter(playerIndex, false);
		}

		RefreshGameplayTensionMusic();
	}

	private void AddPlayerScore(int playerIndex, int points)
	{
		if (playerIndex < 0 || playerIndex >= _playerScores.Length || points <= 0)
		{
			return;
		}

		if (adventure_mode)
		{
			int damageTargetPlayerIndex = GetAdventureDamageTargetPlayerIndex(playerIndex);
			if (damageTargetPlayerIndex < 0 || damageTargetPlayerIndex >= _playerScores.Length)
			{
				return;
			}

			if (damageTargetPlayerIndex == MainPlayerIndex && ShouldUseAdventureUpgradeSystem())
			{
				GD.PushError("Adventure player damage must be committed through the adventure save gate.");
				return;
			}
			ApplyAdventureDamageInMemory(playerIndex, damageTargetPlayerIndex, points);
			return;
		}

		_playerScores[playerIndex] += points;
		UpdatePlayerScoreMeter(playerIndex, true);
		PlayPlayerAvatarAction(playerIndex, CharacterAvatar.AvatarAction.Score);
		RefreshGameplayTensionMusic();
	}

	private void ApplyAdventureDamageInMemory(
		int sourcePlayerIndex,
		int targetPlayerIndex,
		int points)
	{
		if (targetPlayerIndex < 0
			|| targetPlayerIndex >= _playerScores.Length
			|| points <= 0)
		{
			return;
		}

		int previousScore = _playerScores[targetPlayerIndex];
		int damagedScore = Math.Max(0, previousScore - points);
		_playerScores[targetPlayerIndex] = damagedScore;
		PlayAvatarDamagePopup(targetPlayerIndex, previousScore - damagedScore);
		UpdatePlayerScoreMeter(targetPlayerIndex, true);
		PlayPlayerAvatarAction(sourcePlayerIndex, CharacterAvatar.AvatarAction.Score);
		if (targetPlayerIndex != sourcePlayerIndex)
		{
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		}
		RefreshGameplayTensionMusic();
	}

	private void ApplyCommittedAdventurePlayerVitals(
		int sourcePlayerIndex,
		int rawDamage,
		int currentHealth,
		AdventureLowHealthRescueResult rescueResult,
		bool consumedHalfHealthRescue,
		bool consumedQuarterHealthRescue)
	{
		int previousHealth = _playerScores[MainPlayerIndex];
		_playerScores[MainPlayerIndex] = currentHealth;
		if (rawDamage > 0)
		{
			PlayAvatarDamagePopup(MainPlayerIndex, Math.Min(previousHealth, rawDamage));
			PlayPlayerAvatarAction(sourcePlayerIndex, CharacterAvatar.AvatarAction.Score);
			if (sourcePlayerIndex != MainPlayerIndex)
			{
				PlayPlayerAvatarAction(MainPlayerIndex, CharacterAvatar.AvatarAction.Hit);
			}
		}
		_adventureMainPlayerHalfHealthRescueUsed |= consumedHalfHealthRescue;
		_adventureMainPlayerQuarterHealthRescueUsed |= consumedQuarterHealthRescue;
		if (rescueResult.DrawCount > 0)
		{
			DrawCardsFromDeckToHandForAdventureEffect(MainPlayerIndex, rescueResult.DrawCount);
		}
		UpdatePlayerScoreMeter(MainPlayerIndex, true);
		RefreshGameplayTensionMusic();
	}

	private readonly record struct AdventureDirectDamageSceneCommit(
		bool Succeeded,
		AdventureDirectDamageCommitResult CommitResult);

	private async Task<AdventureDirectDamageSceneCommit> CommitAdventureDirectDamageAsync(
		int damageAmount)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (!ShouldUseAdventureUpgradeSystem() || store?.HasActiveRun != true)
		{
			return new AdventureDirectDamageSceneCommit(false, default);
		}

		AdventureSaveGateResult<AdventureDirectDamageCommitResult> gate =
			await ExecuteAdventureSaveGateAsync(
				() => store.ApplyDirectDamage(new AdventureDirectDamageCommitInput(
					damageAmount,
					_adventureMainPlayerHalfHealthRescueUsed,
					_adventureMainPlayerQuarterHealthRescueUsed)),
				result => result.WriteResult == AdventureRunWriteResult.Succeeded,
				result => result.WriteResult == AdventureRunWriteResult.SaveFailed);
		if (!gate.Succeeded)
		{
			GD.PushWarning($"Failed to commit adventure direct damage: {gate.Result.WriteResult}");
			return new AdventureDirectDamageSceneCommit(false, default);
		}

		return new AdventureDirectDamageSceneCommit(true, gate.Result);
	}

	private void ApplyCommittedAdventureDirectDamage(
		int sourcePlayerIndex,
		AdventureDirectDamageCommitResult commitResult)
	{
		ApplyCommittedAdventurePlayerVitals(
			sourcePlayerIndex,
			commitResult.PreviousHealth - commitResult.DamagedHealth,
			commitResult.HealingResult.CurrentHealth,
			commitResult.RescueResult,
			commitResult.RescueResult.ConsumedHalfThreshold,
			commitResult.RescueResult.ConsumedQuarterThreshold);
	}

	private void UpdatePlayerScoreMeter(int playerIndex, bool animate)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return;
		}

		if (playerIndex < 0 || playerIndex >= _playerScoreMeters.Length)
		{
			return;
		}

		ScoreAvatarMeter? meter = _playerScoreMeters[playerIndex];
		if (meter == null || !GodotObject.IsInstanceValid(meter))
		{
			return;
		}

		meter.TooltipText = string.Empty;
		meter.SetScore(_playerScores[playerIndex], GetScoreLimit(playerIndex), animate);
		UpdatePlayerAvatarTooltip(playerIndex);
		if (_activeStatusTooltipPlayerIndex == playerIndex)
		{
			MarkActivePlayerStatusTooltipContentDirty();
		}
	}

	private int GetScoreWinnerPlayerIndex()
	{
		if (adventure_mode)
		{
			return GetAdventureWinnerPlayerIndex();
		}

		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (_playerScores[playerIndex] >= _winningScore)
			{
				return playerIndex;
			}
		}

		return -1;
	}

	private int GetStartingPlayerScore(int playerIndex)
	{
		if (playerIndex == MainPlayerIndex && debug_main_player_starting_score >= 0)
		{
			return debug_main_player_starting_score;
		}

		if (!adventure_mode || playerIndex >= player_count)
		{
			return 0;
		}

		if (playerIndex == MainPlayerIndex
			&& TryGetAdventureMainPlayerRunHealth(out int currentHealth, out _))
		{
			return currentHealth;
		}

		return GetAdventureStartingHealth(playerIndex);
	}

	private int GetScoreLimit(int playerIndex)
	{
		if (adventure_mode
			&& playerIndex == MainPlayerIndex
			&& TryGetAdventureMainPlayerRunHealth(out _, out int maxHealth))
		{
			return maxHealth;
		}

		return adventure_mode ? GetAdventureStartingHealth(playerIndex) : _winningScore;
	}

	private int GetAdventureStartingHealth(int playerIndex)
	{
		if (playerIndex != AdventureEnemyPlayerIndex)
		{
			if (playerIndex == MainPlayerIndex
				&& TryGetAdventureMainPlayerRunHealth(out _, out int maxHealth))
			{
				return maxHealth;
			}

			return AdventurePlayerStartingHealth;
		}

		CharacterAvatarDefinition enemyDefinition = CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id);
		return enemyDefinition.Id switch
		{
			"boss_k" => AdventureEnemyBossStartingHealth,
			"dragon_prince_j" or "dragon_queen_q" => AdventureEnemyRoyalStartingHealth,
			_ => enemyDefinition.AbilityIds.Count > 0
				? AdventureEnemyAbilityStartingHealth
				: AdventureEnemyNoAbilityStartingHealth,
		};
	}

	private int GetAdventureDamageTargetPlayerIndex(int sourcePlayerIndex)
	{
		if (sourcePlayerIndex < 0 || sourcePlayerIndex >= player_count || player_count <= 1)
		{
			return sourcePlayerIndex;
		}

		return GameTurnRules.GetNextPlayerIndex(
			ClockwiseTurnOrder,
			sourcePlayerIndex,
			player_count,
			MainPlayerIndex);
	}

	private int GetAdventureWinnerPlayerIndex()
	{
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (_playerScores[playerIndex] > 0)
			{
				continue;
			}

			int winnerPlayerIndex = GetAdventureDamageTargetPlayerIndex(playerIndex);
			return winnerPlayerIndex >= 0 && winnerPlayerIndex < player_count ? winnerPlayerIndex : -1;
		}

		return -1;
	}

	private bool TryGetAdventureMainPlayerRunHealth(out int currentHealth, out int maxHealth)
	{
		currentHealth = 0;
		maxHealth = 0;
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return false;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return false;
		}

		currentHealth = runState.CurrentHealth;
		maxHealth = runState.MaxHealth;
		return true;
	}

	private async Task<bool> ApplyAdventureMainPlayerHealingAsync(
		int healAmount,
		int maxHealthDelta = 0,
		bool animate = true)
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return false;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store?.HasActiveRun != true)
		{
			return false;
		}

		AdventureSaveGateResult<AdventureHealingCommitResult> gate =
			await ExecuteAdventureSaveGateAsync(
				() => store.ApplyHealing(healAmount, maxHealthDelta),
				result => result.WriteResult == AdventureRunWriteResult.Succeeded,
				result => result.WriteResult == AdventureRunWriteResult.SaveFailed);
		if (!gate.Succeeded)
		{
			GD.PushWarning($"Failed to commit adventure healing: {gate.Result.WriteResult}");
			return false;
		}
		AdventureHealingCommitResult commitResult = gate.Result;

		_playerScores[MainPlayerIndex] = commitResult.HealingResult.CurrentHealth;
		UpdatePlayerScoreMeter(MainPlayerIndex, animate);
		RefreshGameplayTensionMusic();
		return true;
	}

	private static int GetCardScoreValue(PlayingCard card)
	{
		return CardRankCatalog.GetScoreValue(card.Rank, !string.IsNullOrWhiteSpace(card.Suit));
	}

	private int GetCapturedCardScoreValueForPlayer(PlayingCard card, int scorePlayerIndex, bool hasDoubleEffect)
	{
		return ApplyCapturedCardScoreModifiers(GetCardScoreValue(card), scorePlayerIndex, hasDoubleEffect);
	}

	private int GetHalfCapturedCardScoreValueForPlayer(PlayingCard card, int scorePlayerIndex, bool hasDoubleEffect)
	{
		int points = Mathf.CeilToInt(GetCardScoreValue(card) / 2.0f);
		return ApplyCapturedCardScoreModifiers(points, scorePlayerIndex, hasDoubleEffect);
	}

	private int ApplyCapturedCardScoreModifiers(int points, int scorePlayerIndex, bool hasDoubleEffect)
	{
		return CaptureScoringRules.ApplyCapturedCardScoreModifiers(
			points,
			BuildCaptureScoringContext(scorePlayerIndex),
			hasDoubleEffect);
	}

	private bool ShouldApplyCrabScoreReduction(int scorePlayerIndex)
	{
		return adventure_mode
			&& scorePlayerIndex == MainPlayerIndex
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.TideGuard);
	}

	private bool ShouldPlayCrabScoreReductionAbility(
		int scorePlayerIndex,
		int captureStartIndex,
		HashSet<int>? extraTableCardIndexes = null)
	{
		if (!ShouldApplyCrabScoreReduction(scorePlayerIndex)
			|| captureStartIndex < 0
			|| captureStartIndex >= _tableCards.Count)
		{
			return false;
		}

		bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, extraTableCardIndexes);
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			bool isBaseScoringCard = tableCardIndex >= captureStartIndex
				&& IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex);
			bool isExtraScoringCard = extraTableCardIndexes != null && extraTableCardIndexes.Contains(tableCardIndex);
			if (!isBaseScoringCard && !isExtraScoringCard)
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand || !tableCard.Card.HasValue)
			{
				continue;
			}

			int rawPoints = GetCardScoreValue(tableCard.Card.Value);
			if (hasDoubleEffect)
			{
				rawPoints *= 2;
			}

			if (rawPoints > 0)
			{
				return true;
			}
		}

		return false;
	}
}
