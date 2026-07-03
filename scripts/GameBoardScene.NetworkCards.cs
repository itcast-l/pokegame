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

	private PlayingCard CreateNetworkPlayingCard(Godot.Collections.Dictionary cardDictionary)
	{
		return CreateNetworkPlayingCard(
			ReadNetworkString(cardDictionary, "code", string.Empty),
			ReadStringArray(cardDictionary, "attached_item_card_ids"));
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

	private void EnsureNetworkHandCardPlaceholder(int playerIndex, int handIndex, string cardCode)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || handIndex < 0)
		{
			return;
		}

		while (_playerHands[playerIndex].Count <= handIndex)
		{
			_playerHands[playerIndex].Add(string.IsNullOrWhiteSpace(cardCode)
				? CreatePlaceholderPlayingCard()
				: CreateNetworkPlayingCard(cardCode));
		}
	}

	private void InsertNetworkTableOverlayIfMissing(int actorSeatIndex, int targetTableCardIndex, ItemCard itemCard)
	{
		if (targetTableCardIndex < 0)
		{
			return;
		}

		ItemCard? existingItemOverlay = targetTableCardIndex < _tableCards.Count
			? _tableCards[targetTableCardIndex].ItemOverlay
			: null;
		if (existingItemOverlay.HasValue
			&& string.Equals(existingItemOverlay.Value.Id, itemCard.Id, StringComparison.Ordinal))
		{
			return;
		}

		int insertIndex = Mathf.Clamp(targetTableCardIndex, 0, _tableCards.Count);
		_tableCards.Insert(insertIndex, new TableCard(null, false, itemCard, actorSeatIndex));
	}

	private void EnsureNetworkQuestionTableCard(int tableCardIndex)
	{
		ItemCard questionMarkCard = CreateNetworkVisualItemCard(QuestionMarkItemCardId);
		while (_tableCards.Count <= tableCardIndex)
		{
			_tableCards.Add(new TableCard(null, true, questionMarkCard));
		}
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
