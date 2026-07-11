using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private string ToNetworkCardCode(PlayingCard card)
	{
		return $"{card.Suit}:{card.Rank}";
	}

	private PlayingCard CreateNetworkPlayingCard(string cardCode)
	{
		EnsureNetworkCardCache();
		if (_networkCardsByCode.TryGetValue(cardCode, out PlayingCard card))
		{
			return card;
		}

		return CreatePlaceholderPlayingCard();
	}

	private PlayingCard CreateNetworkPlayingCard(string cardCode, IReadOnlyList<string> attachedItemCardIds)
	{
		PlayingCard card = CreateNetworkPlayingCard(cardCode);
		foreach (string itemCardId in attachedItemCardIds)
		{
			if (!string.IsNullOrWhiteSpace(itemCardId))
			{
				card = card.WithAttachedItemEffect(itemCardId);
			}
		}

		return card;
	}

	private Godot.Collections.Dictionary BuildPlayingCardArrayEntry(PlayingCard card)
	{
		return new Godot.Collections.Dictionary
		{
			["code"] = ToNetworkCardCode(card),
			["attached_item_card_ids"] = BuildStringArray(card.GetAttachedItemCardIds()),
		};
	}

	private bool ShouldRevealTargetHandToSeat(int recipientSeatIndex)
	{
		return _pendingItemSelectionMode == PendingItemSelectionMode.TargetHandCard
			&& recipientSeatIndex == _pendingItemOwnerIndex
			&& TryGetSeatControlStateForUi(recipientSeatIndex, out SeatControlState state)
			&& NetworkSeatControlRules.CanRevealPendingTargetHand(state.Mode)
			&& _revealedPlayerHandIndex >= 0
			&& _revealedPlayerHandIndex != _pendingItemOwnerIndex;
	}

	private PlayingCard CreatePlaceholderPlayingCard()
	{
		return new PlayingCard(string.Empty, string.Empty, GetNetworkPlaceholderTexture());
	}

	private ItemCard CreatePlaceholderItemCard()
	{
		return new ItemCard(string.Empty, GetNetworkPlaceholderTexture());
	}

	private ItemCard CreateNetworkVisualItemCard(string itemCardId)
	{
		ItemCard? itemCard = CreateItemCard(itemCardId);
		return itemCard ?? new ItemCard(itemCardId, GetNetworkPlaceholderTexture());
	}

	private bool CanExpandNetworkPlayingHandToIndex(int playerIndex, int handIndex)
	{
		return playerIndex >= 0 && playerIndex < _playerHands.Count
			&& playerIndex < _playerItemHands.Count
			&& handIndex >= 0 && handIndex < MaxTotalHandCards
			&& handIndex <= _playerHands[playerIndex].Count
			&& GetTotalHandCardCount(playerIndex) + (handIndex == _playerHands[playerIndex].Count ? 1 : 0) <= MaxTotalHandCards;
	}

	private bool CanExpandNetworkItemHandToIndex(int playerIndex, int itemHandIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count
			|| itemHandIndex < 0 || itemHandIndex >= MaxTotalHandCards || itemHandIndex > _playerItemHands[playerIndex].Count)
		{
			return false;
		}
		int added = itemHandIndex == _playerItemHands[playerIndex].Count ? 1 : 0;
		return GetTotalHandCardCount(playerIndex) + added <= MaxTotalHandCards;
	}

	private bool EnsureNetworkQuestionRewardItem(int playerIndex, int itemHandIndex, ItemCard item)
	{
		if (!CanExpandNetworkItemHandToIndex(playerIndex, itemHandIndex)) return false;
		if (itemHandIndex == _playerItemHands[playerIndex].Count) _playerItemHands[playerIndex].Add(item);
		return true;
	}

	private bool EnsureNetworkHandCardPlaceholder(int playerIndex, int handIndex, string cardCode)
	{
		if (!CanExpandNetworkPlayingHandToIndex(playerIndex, handIndex))
		{
			return false;
		}

		if (handIndex == _playerHands[playerIndex].Count)
		{
			_playerHands[playerIndex].Add(string.IsNullOrWhiteSpace(cardCode)
				? CreatePlaceholderPlayingCard()
				: CreateNetworkPlayingCard(cardCode));
		}
		return true;
	}

	private bool InsertNetworkTableOverlayIfMissing(int actorSeatIndex, int targetTableCardIndex, ItemCard itemCard)
	{
		if (!CanApplyNetworkTableOverlay(actorSeatIndex, targetTableCardIndex, itemCard.Id)) return false;
		if (targetTableCardIndex == _tableCards.Count) _tableCards.Add(new TableCard(null, false, itemCard, actorSeatIndex));
		return true;
	}

	private bool CanApplyNetworkTableOverlay(int actorSeatIndex, int targetTableCardIndex, string itemCardId)
	{
		if (actorSeatIndex < 0 || actorSeatIndex >= player_count || targetTableCardIndex < 0 || targetTableCardIndex > _tableCards.Count || !IsValidNetworkItemCardId(itemCardId, true)) return false;
		if (targetTableCardIndex == _tableCards.Count) return true;
		TableCard existing = _tableCards[targetTableCardIndex];
		return existing.ItemOverlay.HasValue && string.Equals(existing.ItemOverlay.Value.Id, itemCardId, StringComparison.Ordinal) && existing.ItemOwnerPlayerIndex == actorSeatIndex;
	}

	private bool MatchesNetworkPredictedItemVisual(int actorSeatIndex, int itemHandIndex, string itemCardId, string presentation, int targetTableCardIndex)
	{
		return _networkPredictedItemVisualActive
			&& actorSeatIndex == GetLocalHandPlayerIndex()
			&& itemHandIndex == _networkPendingItemHandIndex
			&& string.Equals(itemCardId, _networkPredictedItemCardId, StringComparison.Ordinal)
			&& string.Equals(presentation, _networkPredictedItemPlayPresentation, StringComparison.Ordinal)
			&& targetTableCardIndex == _networkPredictedItemTargetTableCardIndex;
	}

	private bool EnsureNetworkQuestionTableCard(int tableCardIndex)
	{
		if (tableCardIndex < 0 || tableCardIndex > _tableCards.Count) return false;
		if (tableCardIndex < _tableCards.Count) return true;
		ItemCard questionMarkCard = CreateNetworkVisualItemCard(QuestionMarkItemCardId);
		_tableCards.Add(new TableCard(null, true, questionMarkCard));
		return true;
	}

	private List<TableCard> BuildNetworkShuffledTableCards(IReadOnlyList<int> targetIndexes)
	{
		if (targetIndexes.Count != _tableCards.Count)
		{
			return new List<TableCard>();
		}

		var shuffledTableCards = new List<TableCard>(_tableCards);
		for (int sourceIndex = 0; sourceIndex < targetIndexes.Count; sourceIndex++)
		{
			int targetIndex = targetIndexes[sourceIndex];
			if (targetIndex < 0 || targetIndex >= _tableCards.Count)
			{
				return new List<TableCard>();
			}

			shuffledTableCards[targetIndex] = _tableCards[sourceIndex];
		}

		return shuffledTableCards;
	}

	private void SendQuestionRewardVisualEventIfNeeded(int playerIndex, QuestionCardRewardAnimation? rewardAnimation, int turnSequenceGeneration)
	{
		if (!rewardAnimation.HasValue
			|| !IsNetworkMultiplayerMode()
			|| !IsNetworkHost()
			|| IsLocalHandPlayer(playerIndex)
			|| !TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
		{
			return;
		}

		GetNetworkManager()?.SendQuestionRewardVisualEventToSeatFromHost(
			playerIndex,
			rewardAnimation.Value,
			turnSequenceGeneration,
			snapshotId);
	}

	private Texture2D GetNetworkPlaceholderTexture()
	{
		if (_cardBackTexture != null)
		{
			return _cardBackTexture;
		}

		EnsureNetworkCardCache();
		foreach (PlayingCard card in _networkCardsByCode.Values)
		{
			return card.Texture;
		}

		return null!;
	}

	private void EnsureNetworkCardCache()
	{
		if (_networkCardsByCode.Count > 0)
		{
			return;
		}

		foreach (PlayingCard card in BuildDeckForPlayerCount(player_count))
		{
			_networkCardsByCode.TryAdd(ToNetworkCardCode(card), card);
		}
	}

	private bool TryGetNetworkPlayerName(int playerIndex, out string playerName)
	{
		playerName = string.Empty;
		if (string.IsNullOrWhiteSpace(_playerNamesCsv))
		{
			return false;
		}

		string[] names = SplitNetworkCsv(_playerNamesCsv);
		if (playerIndex < 0 || playerIndex >= names.Length)
		{
			return false;
		}

		playerName = PlayerNameRules.Normalize(names[playerIndex]);
		return true;
	}

	private bool TryGetNetworkAvatarId(int playerIndex, out string avatarId)
	{
		avatarId = string.Empty;
		if (string.IsNullOrWhiteSpace(_playerAvatarIdsCsv))
		{
			return false;
		}

		string[] avatarIds = SplitNetworkCsv(_playerAvatarIdsCsv);
		if (playerIndex < 0 || playerIndex >= avatarIds.Length)
		{
			return false;
		}

		avatarId = CharacterAvatarCatalog.NormalizeAvatarId(avatarIds[playerIndex]);
		return !string.IsNullOrWhiteSpace(avatarId);
	}

	private static string[] SplitNetworkCsv(string csv)
	{
		return csv.Split(',', StringSplitOptions.TrimEntries);
	}
}
