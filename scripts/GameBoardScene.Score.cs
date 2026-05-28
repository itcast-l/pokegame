using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private const int DefaultWinningScore = 100;
	private const int AdventurePlayerStartingHealth = 100;
	private const int AdventureEnemyNoAbilityStartingHealth = 50;
	private const int AdventureEnemyAbilityStartingHealth = 100;
	private const int AdventureEnemyRoyalStartingHealth = 150;
	private const int AdventureEnemyBossStartingHealth = 200;
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

	private readonly int[] _playerScores = new int[MaxPlayers];
	private readonly ScoreAvatarMeter?[] _playerScoreMeters = new ScoreAvatarMeter?[MaxPlayers];
	private readonly ScoreEffectTarget?[] _playerScoreEffectTargets = new ScoreEffectTarget?[MaxPlayers];
	private int _winningScore = DefaultWinningScore;

	private void BindPlayerScoreMeters()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			ScoreAvatarMeter? meter = GetNodeOrNull<ScoreAvatarMeter>($"%{ScoreMeterNodeNames[playerIndex]}");
			if (meter == null)
			{
				GD.PushWarning($"Missing score meter node: {ScoreMeterNodeNames[playerIndex]}.");
				continue;
			}

			meter.TooltipText = GetPlayerScoreTooltip(playerIndex);
			meter.Configure(AvatarScoreFillColors[playerIndex], AvatarScoreGlowColors[playerIndex]);
			meter.SetScore(GetStartingPlayerScore(playerIndex), GetScoreLimit(playerIndex), false);
			_playerScoreMeters[playerIndex] = meter;
			_playerScoreEffectTargets[playerIndex] = GetNodeOrNull<ScoreEffectTarget>($"%{ScoreEffectTargetNodeNames[playerIndex]}");
		}
	}

	private void ResetPlayerScores()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerScores[playerIndex] = GetStartingPlayerScore(playerIndex);
			UpdatePlayerScoreMeter(playerIndex, false);
		}
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

			_playerScores[damageTargetPlayerIndex] = Math.Max(0, _playerScores[damageTargetPlayerIndex] - points);
			UpdatePlayerScoreMeter(damageTargetPlayerIndex, true);
			PlayPlayerAvatarAction(playerIndex, CharacterAvatar.AvatarAction.Score);
			if (damageTargetPlayerIndex != playerIndex)
			{
				PlayPlayerAvatarAction(damageTargetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
			}
			return;
		}

		_playerScores[playerIndex] += points;
		UpdatePlayerScoreMeter(playerIndex, true);
		PlayPlayerAvatarAction(playerIndex, CharacterAvatar.AvatarAction.Score);
	}

	private void UpdatePlayerScoreMeter(int playerIndex, bool animate)
	{
		if (playerIndex < 0 || playerIndex >= _playerScoreMeters.Length)
		{
			return;
		}

		ScoreAvatarMeter? meter = _playerScoreMeters[playerIndex];
		if (meter == null || !GodotObject.IsInstanceValid(meter))
		{
			return;
		}

		meter.TooltipText = GetPlayerScoreTooltip(playerIndex);
		meter.SetScore(_playerScores[playerIndex], GetScoreLimit(playerIndex), animate);
		UpdatePlayerAvatarTooltip(playerIndex);
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
		if (!adventure_mode || playerIndex >= player_count)
		{
			return 0;
		}

		return GetAdventureStartingHealth(playerIndex);
	}

	private int GetScoreLimit(int playerIndex)
	{
		return adventure_mode ? GetAdventureStartingHealth(playerIndex) : _winningScore;
	}

	private int GetAdventureStartingHealth(int playerIndex)
	{
		if (playerIndex != AdventureEnemyPlayerIndex)
		{
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

		return GetNextTurnPlayerIndex(sourcePlayerIndex);
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
		if (hasDoubleEffect)
		{
			points *= 2;
		}

		if (ShouldApplyCrabScoreReduction(scorePlayerIndex))
		{
			points = Math.Max(
				0,
				points - GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.ScoreReductionPerCard, 2));
		}

		return points;
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
