using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private int AdvanceQuestionCardRefreshTurnCounter(int nextTurnPlayerIndex)
	{
		if (!item_mode)
		{
			return -1;
		}

		if (HasTableSpecialCard())
		{
			return -1;
		}

		if (_questionCardRefreshAnchorPlayerIndex < 0)
		{
			return -1;
		}

		_completedTurnsSinceQuestionCardRefresh++;
		if (nextTurnPlayerIndex != _questionCardRefreshAnchorPlayerIndex
			|| _completedTurnsSinceQuestionCardRefresh < GetQuestionCardRefreshTurnCount())
		{
			return -1;
		}

		_completedTurnsSinceQuestionCardRefresh = 0;
		_questionCardRefreshAnchorPlayerIndex = -1;
		return PlaceQuestionCardOnNextTableSlot();
	}

	private int PlaceQuestionCardOnNextTableSlot()
	{
		if (!item_mode)
		{
			return -1;
		}

		if (HasTableSpecialCard())
		{
			return -1;
		}

		ItemCard? itemOverlay = CreateItemCard(QuestionMarkItemCardId);
		if (!itemOverlay.HasValue)
		{
			return -1;
		}

		_tableCards.Add(new TableCard(null, true, itemOverlay));
		return _tableCards.Count - 1;
	}

	private int GetQuestionCardRefreshTurnCount()
	{
		return Math.Max(1, player_count * QuestionCardRefreshCyclesAfterCapture);
	}

	private bool HasTableSpecialCard()
	{
		foreach (TableCard tableCard in _tableCards)
		{
			if (tableCard.ItemOverlay.HasValue)
			{
				return true;
			}
		}

		return false;
	}

	private async Task<QuestionCardRewardGrant?> GrantQuestionCardRewardAsync(
		int playerIndex,
		bool reserveReplacementCard,
		int turnSequenceGeneration)
	{
		if (!item_mode)
		{
			return null;
		}

		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return null;
		}

		List<string> rewardItemCardIds = BuildEnabledQuestionRewardItemCardIds();
		if (rewardItemCardIds.Count == 0)
		{
			return null;
		}

		string itemCardId = rewardItemCardIds[_rng.RandiRange(0, rewardItemCardIds.Count - 1)];

		ItemCard? itemCard = CreateItemCard(itemCardId);
		if (!itemCard.HasValue)
		{
			return null;
		}

		if (IsAttachmentItemCard(itemCardId))
		{
			_pendingItemAttachmentRewards.Enqueue(new PendingItemAttachmentReward(playerIndex, itemCardId));
			return new QuestionCardRewardGrant(itemCard.Value, -1, true);
		}

		if (!HasRoomForNewHandCard(playerIndex, reserveReplacementCard ? 1 : 0)
			&& !await DiscardHandCardForNewItemCardAsync(playerIndex, turnSequenceGeneration))
		{
			return null;
		}

		int itemHandIndex = _playerItemHands[playerIndex].Count;
		_playerItemHands[playerIndex].Add(itemCard.Value);
		return new QuestionCardRewardGrant(itemCard.Value, itemHandIndex);
	}

	private List<string> BuildEnabledQuestionRewardItemCardIds()
	{
		if (debug_random_item_cards_are_magnet)
		{
			return IsSpecialItemCardEnabled(MagnetItemCardId)
				? new List<string> { MagnetItemCardId }
				: new List<string>();
		}

		var rewardItemCardIds = new List<string>();
		foreach (string itemCardId in _itemCardCatalog.RewardIds)
		{
			if (IsSpecialItemCardEnabled(itemCardId))
			{
				rewardItemCardIds.Add(itemCardId);
			}
		}

		return rewardItemCardIds;
	}
}
