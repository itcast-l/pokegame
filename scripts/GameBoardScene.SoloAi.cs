using System.Collections.Generic;
using Deckland.Rules;
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

	private SoloAiDifficulty GetSoloAiDifficulty()
	{
		return MatchReadModel.NormalizeSoloAiDifficulty(solo_ai_difficulty);
	}

	private bool TryPickSoloAiCaptureHandIndex(int playerIndex, out int handIndex)
	{
		handIndex = -1;
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player))
		{
			return false;
		}
		if (!TrySubmitChooseRandomIndexRuleCommand(
				playerIndex,
				0,
				99,
				_turnSequenceGeneration,
				out int decisionRollPercent)
			|| !TrySubmitChooseRandomIndexRuleCommand(
				playerIndex,
				0,
				int.MaxValue,
				_turnSequenceGeneration,
				out int randomCaptureOrdinal))
		{
			return false;
		}

		return MatchReadModel.TryPickSoloAiCaptureCardIndex(
			ToCaptureScoringTableCards(readModel.TableCards),
			ToPotentialCaptureCards(playerIndex, player.Hand),
			BuildCaptureScoringContext(playerIndex),
			GetSoloAiDifficulty(),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent,
			randomCaptureOrdinal,
			out handIndex);
	}

	private int PickOfflineSoloAiDiscardDisplayIndex(int playerIndex)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player))
		{
			return -1;
		}

		var candidates = new List<SoloAiHandCardDecisionCandidate>();
		for (int handIndex = 0; handIndex < player.Hand.Length; handIndex++)
		{
			PlayingCard card = FromRuleCardState(player.Hand[handIndex]);
			candidates.Add(new SoloAiHandCardDecisionCandidate(
				Index: handIndex,
				ScoreValue: GetCardScoreValue(card),
				CanCaptureNow: GetPotentialNormalCaptureCount(readModel, card) > 0));
		}

		if (!TrySubmitChooseRandomIndexRuleCommand(
				playerIndex,
				0,
				int.MaxValue,
				_turnSequenceGeneration,
				out int tieBreakOrdinal))
		{
			return -1;
		}

		return MatchReadModel.PickSoloAiDiscardDisplayIndex(
			GetSoloAiDifficulty(),
			candidates,
			player.Hand.Length + player.ItemHand.Length,
			tieBreakOrdinal);
	}

	private int PickOfflineSoloAiMagnetHandIndex(int playerIndex, string itemCardId)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player))
		{
			return -1;
		}

		var candidates = new List<SoloAiHandCardDecisionCandidate>();
		for (int handIndex = 0; handIndex < player.Hand.Length; handIndex++)
		{
			PlayingCard card = FromRuleCardState(player.Hand[handIndex]);
			if (!IsAttachableMagnetCard(card, itemCardId))
			{
				continue;
			}

			candidates.Add(new SoloAiHandCardDecisionCandidate(
				Index: handIndex,
				ScoreValue: GetCardScoreValue(card),
				CanCaptureNow: GetPotentialNormalCaptureCount(readModel, card) > 0));
		}

		if (!TrySubmitChooseRandomIndexRuleCommand(
				playerIndex,
				0,
				int.MaxValue,
				_turnSequenceGeneration,
				out int tieBreakOrdinal))
		{
			return -1;
		}

		return MatchReadModel.PickSoloAiMagnetHandIndex(
			GetSoloAiDifficulty(),
			candidates,
			tieBreakOrdinal);
	}

}
