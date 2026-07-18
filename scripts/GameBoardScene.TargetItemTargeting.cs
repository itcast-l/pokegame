using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition))
		{
			return -1;
		}

		string target = definition.EffectValues.GetString(CardEffectValueKeys.Target);
		if (IsOfflineSoloAiPlayer(ownerPlayerIndex)
			&& definition.PlayPresentation != CardPlayPresentationTypes.DirectTrigger)
		{
			return PickOfflineSoloAiTargetPlayer(target, ownerPlayerIndex);
		}

		int selectedPlayerIndex = -1;
		int candidateCount = 0;
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (!IsResolvedItemTargetPlayerCandidate(target, ownerPlayerIndex, playerIndex))
			{
				continue;
			}

			candidateCount++;
			if (_rng.RandiRange(1, candidateCount) == 1)
			{
				selectedPlayerIndex = playerIndex;
			}
		}

		return selectedPlayerIndex;
	}

	private bool IsItemTargetPlayerCandidate(string itemCardId, int ownerPlayerIndex, int targetPlayerIndex)
	{
		return _itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			&& IsResolvedItemTargetPlayerCandidate(
				definition.EffectValues.GetString(CardEffectValueKeys.Target),
				ownerPlayerIndex,
				targetPlayerIndex);
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

	private int PickTargetHandIndex(int targetPlayerIndex)
	{
		if (IsNetworkMultiplayerMode() && !IsNetworkHost())
		{
			return -1;
		}
		if (targetPlayerIndex < 0 || targetPlayerIndex >= _playerHands.Count || targetPlayerIndex >= _playerItemHands.Count)
		{
			return -1;
		}

		int totalHandCards = GetTotalHandCardCount(targetPlayerIndex);
		if (totalHandCards == 0)
		{
			return -1;
		}

		return _rng.RandiRange(0, totalHandCards - 1);
	}
}
