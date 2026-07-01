using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private int AdvanceQuestionCardRefreshTurnCounter(int nextTurnPlayerIndex)
	{
		QuestionCardRefreshAdvanceResult refreshResult = _turnAdvancementService.AdvanceQuestionCardRefresh(
			_state,
			item_mode,
			HasTableSpecialCard(),
			nextTurnPlayerIndex,
			QuestionCardRefreshCyclesAfterCapture);

		return refreshResult.ShouldRefreshQuestionCard ? PlaceQuestionCardOnNextTableSlot() : -1;
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

		string itemCardId = PickQuestionRewardItemCardId();
		if (string.IsNullOrEmpty(itemCardId))
		{
			return null;
		}

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

	private string PickQuestionRewardItemCardId()
	{
		// Temporary test override: question cards always grant Suit Collect.
		return ItemCardCatalog.SuitCollectItemCardId;
	}
}
