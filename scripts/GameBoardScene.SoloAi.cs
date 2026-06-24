using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private bool IsOfflineSoloAiDifficultyActive()
	{
		return solo_mode && !adventure_mode && !IsNetworkMultiplayerMode();
	}

	private bool IsOfflineSoloAiPlayer(int playerIndex)
	{
		return IsOfflineSoloAiDifficultyActive()
			&& playerIndex != MainPlayerIndex
			&& playerIndex >= 0
			&& playerIndex < player_count;
	}

	private SoloAiRules.Difficulty GetSoloAiDifficulty()
	{
		return SoloAiRules.NormalizeDifficulty(solo_ai_difficulty);
	}

	private bool TryPickSoloAiCaptureHandIndex(int playerIndex, out int handIndex)
	{
		handIndex = -1;
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return false;
		}

		return SoloAiRules.TryPickCaptureCardIndex(
			ToCaptureScoringTableCards(),
			ToPotentialCaptureCards(playerIndex, _playerHands[playerIndex]),
			BuildCaptureScoringContext(playerIndex),
			GetSoloAiDifficulty(),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: _rng.RandiRange(0, 99),
			randomCaptureOrdinal: _rng.RandiRange(0, int.MaxValue),
			out handIndex);
	}

	private int PickOfflineSoloAiTargetPlayer(string target, int ownerPlayerIndex)
	{
		var candidates = new List<SoloAiRules.PlayerTargetCandidate>();
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (!IsResolvedItemTargetPlayerCandidate(target, ownerPlayerIndex, playerIndex))
			{
				continue;
			}

			candidates.Add(new SoloAiRules.PlayerTargetCandidate(
				PlayerIndex: playerIndex,
				TotalHandCardCount: GetTotalHandCardCount(playerIndex),
				DeckCount: playerIndex >= 0 && playerIndex < _playerDecks.Count ? _playerDecks[playerIndex].Count : 0,
				Score: playerIndex >= 0 && playerIndex < _playerScores.Length ? _playerScores[playerIndex] : 0));
		}

		return SoloAiRules.PickItemTargetPlayer(
			GetSoloAiDifficulty(),
			MapSoloAiTargetPreference(target),
			candidates,
			strategyRollPercent: _rng.RandiRange(0, 99),
			tieBreakOrdinal: _rng.RandiRange(0, int.MaxValue));
	}

	private int PickOfflineSoloAiDiscardDisplayIndex(int playerIndex)
	{
		var candidates = new List<SoloAiRules.HandCardDecisionCandidate>();
		for (int handIndex = 0; handIndex < _playerHands[playerIndex].Count; handIndex++)
		{
			PlayingCard card = _playerHands[playerIndex][handIndex];
			candidates.Add(new SoloAiRules.HandCardDecisionCandidate(
				Index: handIndex,
				ScoreValue: GetCardScoreValue(card),
				CanCaptureNow: GetPotentialNormalCaptureCount(card) > 0));
		}

		return SoloAiRules.PickDiscardDisplayIndex(
			GetSoloAiDifficulty(),
			candidates,
			GetTotalHandCardCount(playerIndex),
			_rng.RandiRange(0, int.MaxValue));
	}

	private int PickOfflineSoloAiMagnetHandIndex(int playerIndex, string itemCardId)
	{
		var candidates = new List<SoloAiRules.HandCardDecisionCandidate>();
		for (int handIndex = 0; handIndex < _playerHands[playerIndex].Count; handIndex++)
		{
			PlayingCard card = _playerHands[playerIndex][handIndex];
			if (!IsAttachableMagnetCard(card, itemCardId))
			{
				continue;
			}

			candidates.Add(new SoloAiRules.HandCardDecisionCandidate(
				Index: handIndex,
				ScoreValue: GetCardScoreValue(card),
				CanCaptureNow: GetPotentialNormalCaptureCount(card) > 0));
		}

		return SoloAiRules.PickMagnetHandIndex(
			GetSoloAiDifficulty(),
			candidates,
			_rng.RandiRange(0, int.MaxValue));
	}

	private static SoloAiRules.TargetPreferenceKind MapSoloAiTargetPreference(string target)
	{
		return target switch
		{
			CardEffectTargets.PlayerHand => SoloAiRules.TargetPreferenceKind.PlayerHand,
			CardEffectTargets.PlayerDeck => SoloAiRules.TargetPreferenceKind.PlayerDeck,
			_ => SoloAiRules.TargetPreferenceKind.PlayerScore,
		};
	}
}
