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
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition))
		{
			return -1;
		}

		string target = definition.EffectValues.GetString(CardEffectValueKeys.Target);
		if (IsOfflineSoloAiPlayer(ownerPlayerIndex))
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
		if (targetPlayerIndex < 0 || targetPlayerIndex >= player_count || targetPlayerIndex == ownerPlayerIndex)
		{
			return false;
		}

		if (target == CardEffectTargets.PlayerHand)
		{
			return targetPlayerIndex < _playerHands.Count
				&& targetPlayerIndex < _playerItemHands.Count
				&& GetTotalHandCardCount(targetPlayerIndex) > 0;
		}

		if (target == CardEffectTargets.PlayerDeck)
		{
			return targetPlayerIndex < _playerDecks.Count && _playerDecks[targetPlayerIndex].Count > 0;
		}

		return true;
	}

	private int PickTargetHandIndex(int targetPlayerIndex)
	{
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
