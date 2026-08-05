using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private List<int> BuildItemTargetPlayerCandidates(string itemCardId, int ownerPlayerIndex)
	{
		var candidates = new List<int>();
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition))
		{
			return candidates;
		}

		string target = definition.EffectValues.GetString(CardEffectValueKeys.Target);
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (IsResolvedItemTargetPlayerCandidate(target, ownerPlayerIndex, playerIndex))
			{
				candidates.Add(playerIndex);
			}
		}

		return candidates;
	}

	private int PickItemTargetPlayer(string itemCardId, int ownerPlayerIndex)
	{
		if (IsNetworkMultiplayerMode() && !IsNetworkHost())
		{
			return -1;
		}
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out _))
		{
			return -1;
		}

		List<int> candidates = BuildItemTargetPlayerCandidates(itemCardId, ownerPlayerIndex);
		return candidates.Count > 0
			&& TrySubmitChooseRandomTargetSeatRuleCommand(
				ownerPlayerIndex,
				candidates.ToImmutableArray(),
				_turnSequenceGeneration,
				out int targetPlayerIndex)
				? targetPlayerIndex
				: -1;
	}

	private bool IsResolvedItemTargetPlayerCandidate(string target, int ownerPlayerIndex, int targetPlayerIndex)
	{
		if (target == CardEffectTargets.PlayerHand)
		{
			int availableCardCount = targetPlayerIndex >= 0
				&& targetPlayerIndex < _playerHands.Count
				&& targetPlayerIndex < _playerItemHands.Count
				? GetTotalHandCardCount(targetPlayerIndex)
				: 0;
			return DirectItemTargetRules.IsEligibleOpponent(
				ownerPlayerIndex,
				targetPlayerIndex,
				player_count,
				availableCardCount);
		}

		if (target == CardEffectTargets.PlayerDeck)
		{
			int availableCardCount = targetPlayerIndex >= 0 && targetPlayerIndex < _playerDecks.Count
				? _playerDecks[targetPlayerIndex].Count
				: 0;
			return DirectItemTargetRules.IsEligibleOpponent(
				ownerPlayerIndex,
				targetPlayerIndex,
				player_count,
				availableCardCount);
		}

		return targetPlayerIndex >= 0
			&& targetPlayerIndex < player_count
			&& targetPlayerIndex != ownerPlayerIndex;
	}

	private int PickTargetHandIndex(int ownerPlayerIndex, int targetPlayerIndex)
	{
		if (IsNetworkMultiplayerMode() && !IsNetworkHost())
		{
			return -1;
		}
		if (targetPlayerIndex < 0 || targetPlayerIndex >= _playerHands.Count || targetPlayerIndex >= _playerItemHands.Count)
		{
			return -1;
		}

		return TrySubmitChooseRandomHandDisplayIndexRuleCommand(
			ownerPlayerIndex,
			targetPlayerIndex,
			ImmutableArray<int>.Empty,
			_turnSequenceGeneration,
			out int targetHandIndex)
				? targetHandIndex
				: -1;
	}
}
