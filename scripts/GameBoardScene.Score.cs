using System;
using Godot;

public partial class GameBoardScene
{
	private const int WinningScore = 200;
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

			meter.TooltipText = $"玩家 {playerIndex + 1}: 0/{WinningScore}";
			meter.Configure(AvatarScoreFillColors[playerIndex], AvatarScoreGlowColors[playerIndex]);
			meter.SetScore(0, WinningScore, false);
			_playerScoreMeters[playerIndex] = meter;
			_playerScoreEffectTargets[playerIndex] = GetNodeOrNull<ScoreEffectTarget>($"%{ScoreEffectTargetNodeNames[playerIndex]}");
		}
	}

	private void ResetPlayerScores()
	{
		Array.Clear(_playerScores, 0, _playerScores.Length);
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			UpdatePlayerScoreMeter(playerIndex, false);
		}
	}

	private void AddPlayerScore(int playerIndex, int points)
	{
		if (playerIndex < 0 || playerIndex >= _playerScores.Length || points <= 0)
		{
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

		meter.TooltipText = $"玩家 {playerIndex + 1}: {_playerScores[playerIndex]}/{WinningScore}";
		meter.SetScore(_playerScores[playerIndex], WinningScore, animate);
	}

	private int GetScoreWinnerPlayerIndex()
	{
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (_playerScores[playerIndex] >= WinningScore)
			{
				return playerIndex;
			}
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
}
