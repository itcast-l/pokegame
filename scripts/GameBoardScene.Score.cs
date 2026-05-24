using System;
using Godot;

public partial class GameBoardScene
{
	private const int DefaultWinningScore = 100;
	private const int AdventureStartingHealth = 100;
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
		new(1.0f, 0.86f, 0.52f, 0.46f),
		new(0.56f, 0.92f, 1.0f, 0.46f),
		new(1.0f, 0.62f, 0.84f, 0.44f),
		new(0.60f, 1.0f, 0.78f, 0.44f),
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
			meter.SetScore(GetStartingPlayerScore(playerIndex), GetScoreLimit(), false);
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
		meter.SetScore(_playerScores[playerIndex], GetScoreLimit(), animate);
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
		return adventure_mode && playerIndex < player_count ? AdventureStartingHealth : 0;
	}

	private int GetScoreLimit()
	{
		return adventure_mode ? AdventureStartingHealth : _winningScore;
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
		if (int.TryParse(card.Rank, out int numericRank))
		{
			return Math.Max(0, numericRank);
		}

		return card.Rank switch
		{
			"A" => 1,
			"J" or "Q" or "K" => 10,
			_ => 0,
		};
	}

	private int GetCapturedCardScoreValueForPlayer(PlayingCard card, int scorePlayerIndex, bool hasDoubleEffect)
	{
		int points = GetCardScoreValue(card);
		if (hasDoubleEffect)
		{
			points *= 2;
		}

		if (ShouldApplyCrabScoreReduction(scorePlayerIndex))
		{
			points = Math.Max(0, points - CrabScoreReductionPerCard);
		}

		return points;
	}

	private bool ShouldApplyCrabScoreReduction(int scorePlayerIndex)
	{
		return adventure_mode
			&& scorePlayerIndex == MainPlayerIndex
			&& adventure_enemy_avatar_id == CrabAdventureEnemyAvatarId;
	}
}
