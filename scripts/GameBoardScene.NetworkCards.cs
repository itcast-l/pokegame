using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private sealed class NetworkReplicaPresentationState
	{
		private NetworkReplicaPresentationState(
			List<List<PlayingCard>> playerDecks,
			List<List<PlayingCard>> playerHands,
			List<List<ItemCard>> playerItemHands,
			List<TableCard> tableCards)
		{
			PlayerDecks = playerDecks;
			PlayerHands = playerHands;
			PlayerItemHands = playerItemHands;
			TableCards = tableCards;
		}

		internal List<List<PlayingCard>> PlayerDecks { get; }
		internal List<List<PlayingCard>> PlayerHands { get; }
		internal List<List<ItemCard>> PlayerItemHands { get; }
		internal List<TableCard> TableCards { get; }

			internal static NetworkReplicaPresentationState CloneFrom(
				IReadOnlyList<IReadOnlyList<PlayingCard>> playerDeckSource,
				IReadOnlyList<IReadOnlyList<PlayingCard>> playerHandSource,
				IReadOnlyList<IReadOnlyList<ItemCard>> playerItemHandSource,
				IReadOnlyList<TableCard> tableCardSource)
			{
				var playerDecks = CloneNestedLists(playerDeckSource);
				var playerHands = CloneNestedLists(playerHandSource);
				var playerItemHands = CloneNestedLists(playerItemHandSource);
				var tableCards = new List<TableCard>(tableCardSource.Count);
				foreach (TableCard sourceCard in tableCardSource)
			{
				TableCard clone = TableCard.Restore(
					sourceCard.InstanceId,
					sourceCard.Card,
					sourceCard.IsFaceUp,
					sourceCard.ItemOverlay,
					sourceCard.ItemOwnerPlayerIndex,
					sourceCard.CardOwnerPlayerIndex,
					sourceCard.RemoveCardOnCapture);
				clone.IsHiddenDuringCapture = sourceCard.IsHiddenDuringCapture;
				clone.IsBuriedInSand = sourceCard.IsBuriedInSand;
				clone.IsSlimeCoated = sourceCard.IsSlimeCoated;
				tableCards.Add(clone);
			}

			return new NetworkReplicaPresentationState(
				playerDecks,
				playerHands,
				playerItemHands,
				tableCards);
		}

		internal void EnsureSeatCapacity(int capacity)
		{
			EnsureNestedListCount(PlayerDecks, capacity);
			EnsureNestedListCount(PlayerHands, capacity);
			EnsureNestedListCount(PlayerItemHands, capacity);
		}

			private static List<List<T>> CloneNestedLists<T>(IReadOnlyList<IReadOnlyList<T>> source)
			{
				var result = new List<List<T>>(source.Count);
				foreach (IReadOnlyList<T> items in source)
			{
				result.Add(new List<T>(items));
			}

			return result;
		}

		private static void EnsureNestedListCount<T>(List<List<T>> target, int count)
		{
			while (target.Count < count)
			{
				target.Add(new List<T>());
			}
		}
	}

	private bool EnsureNetworkReplicaPresentationState()
	{
		if (_networkReplicaPresentationState != null)
		{
			return true;
		}

		if (_replicaRuleSession == null || IsNetworkHost())
		{
			return false;
		}

		_networkReplicaPresentationState = NetworkReplicaPresentationState.CloneFrom(
			_playerDecks,
			_playerHands,
			_playerItemHands,
			_tableCards);
		_networkReplicaPresentationState.EnsureSeatCapacity(MaxPlayers);
		InvalidateRenderCaches();
		return true;
	}

	private void DiscardNetworkReplicaPresentationState()
	{
		if (_networkReplicaPresentationState == null)
		{
			return;
		}

		_networkReplicaPresentationState = null;
		_presentationHiddenTableCardRuntimeInstanceIds.Clear();
		InvalidateRenderCaches();
	}

	private string ToNetworkCardCode(PlayingCard card)
	{
		return card.Code;
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

	private PlayingCard CreatePlaceholderPlayingCard()
	{
		return new PlayingCard(string.Empty, string.Empty);
	}

	private ItemCard CreatePlaceholderItemCard()
	{
		return new ItemCard(string.Empty);
	}

	private ItemCard CreateNetworkVisualItemCard(string itemCardId)
	{
		ItemCard? itemCard = CreateItemCard(itemCardId);
		return itemCard ?? new ItemCard(itemCardId);
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

	private static bool MatchesVisibleNetworkPlayingCardIdentity(
		PlayingCard card,
		string expectedCardCode,
		string expectedAttachedItemCardIdsCsv)
	{
		return string.IsNullOrEmpty(card.Code)
			|| card.Code.StartsWith("redacted:", StringComparison.Ordinal)
			|| string.Equals(card.Code, expectedCardCode, StringComparison.Ordinal)
				&& string.Equals(
					card.AttachedItemCardIdsCsv,
					expectedAttachedItemCardIdsCsv,
					StringComparison.Ordinal);
	}

	private static bool MatchesVisibleNetworkItemCardIdentity(
		ItemCard itemCard,
		string expectedItemCardId)
	{
		return string.IsNullOrEmpty(itemCard.Id)
			|| itemCard.Id.StartsWith("redacted:", StringComparison.Ordinal)
			|| string.Equals(itemCard.Id, expectedItemCardId, StringComparison.Ordinal);
	}

	private bool TryAddNetworkQuestionRewardPresentationItem(
		int playerIndex,
		int itemHandIndex,
		ItemCard item)
	{
		if (!EnsureNetworkReplicaPresentationState()
			|| !CanExpandNetworkItemHandToIndex(playerIndex, itemHandIndex))
		{
			return false;
		}

		List<ItemCard> itemHand = _networkReplicaPresentationState!.PlayerItemHands[playerIndex];
		if (itemHandIndex == itemHand.Count)
		{
			itemHand.Add(item);
		}
		return true;
	}

	private bool TryAddNetworkHandCardPresentationPlaceholder(
		int playerIndex,
		int handIndex,
		string cardCode)
	{
		if (!EnsureNetworkReplicaPresentationState()
			|| !CanExpandNetworkPlayingHandToIndex(playerIndex, handIndex))
		{
			return false;
		}

		List<PlayingCard> hand = _networkReplicaPresentationState!.PlayerHands[playerIndex];
		if (handIndex == hand.Count)
		{
			hand.Add(string.IsNullOrWhiteSpace(cardCode)
				? CreatePlaceholderPlayingCard()
				: CreateNetworkPlayingCard(cardCode));
		}
		return true;
	}

	private bool TryAddNetworkTableOverlayPresentation(
		int actorSeatIndex,
		int targetTableCardIndex,
		ItemCard itemCard)
	{
		if (!EnsureNetworkReplicaPresentationState()
			|| !CanApplyNetworkTableOverlay(actorSeatIndex, targetTableCardIndex, itemCard.Id))
		{
			return false;
		}

		List<TableCard> tableCards = _networkReplicaPresentationState!.TableCards;
		if (targetTableCardIndex == tableCards.Count)
		{
			tableCards.Add(new TableCard(null, false, itemCard, actorSeatIndex));
		}
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

	private bool TryAddNetworkQuestionTablePresentationCard(int tableCardIndex)
	{
		if (!EnsureNetworkReplicaPresentationState()
			|| tableCardIndex < 0
			|| tableCardIndex > _tableCards.Count)
		{
			return false;
		}
		if (tableCardIndex < _tableCards.Count)
		{
			return true;
		}

		ItemCard questionMarkCard = CreateNetworkVisualItemCard(QuestionMarkItemCardId);
		_networkReplicaPresentationState!.TableCards.Add(new TableCard(null, true, questionMarkCard));
		return true;
	}

	private bool TryInsertNetworkPresentationTableCard(int tableCardIndex, TableCard tableCard)
	{
		if (!EnsureNetworkReplicaPresentationState()
			|| tableCardIndex < 0
			|| tableCardIndex > _networkReplicaPresentationState!.TableCards.Count)
		{
			return false;
		}

		_networkReplicaPresentationState.TableCards.Insert(tableCardIndex, tableCard);
		return true;
	}

	private bool TryRemoveNetworkPresentationTableCard(int tableCardIndex)
	{
		if (_networkReplicaPresentationState == null
			|| tableCardIndex < 0
			|| tableCardIndex >= _networkReplicaPresentationState.TableCards.Count)
		{
			return false;
		}

		_networkReplicaPresentationState.TableCards.RemoveAt(tableCardIndex);
		return true;
	}

	private bool TryReplaceNetworkPresentationTableCard(int tableCardIndex, TableCard tableCard)
	{
		if (_networkReplicaPresentationState == null
			|| tableCardIndex < 0
			|| tableCardIndex >= _networkReplicaPresentationState.TableCards.Count)
		{
			return false;
		}

		_networkReplicaPresentationState.TableCards[tableCardIndex] = tableCard;
		return true;
	}

	private bool TrySetNetworkPresentationTableCardFaceUp(int tableCardIndex, bool faceUp)
	{
		if (_networkReplicaPresentationState == null
			|| tableCardIndex < 0
			|| tableCardIndex >= _networkReplicaPresentationState.TableCards.Count)
		{
			return false;
		}

		_networkReplicaPresentationState.TableCards[tableCardIndex].IsFaceUp = faceUp;
		return true;
	}

	private bool TryMoveFirstNetworkPresentationTableCardToEnd()
	{
		if (_networkReplicaPresentationState == null
			|| _networkReplicaPresentationState.TableCards.Count <= 1)
		{
			return false;
		}

		TableCard tableCard = _networkReplicaPresentationState.TableCards[0];
		_networkReplicaPresentationState.TableCards.RemoveAt(0);
		_networkReplicaPresentationState.TableCards.Add(tableCard);
		return true;
	}

	private bool TryRemoveNetworkPresentationTableCardRange(int startIndex, int count)
	{
		if (_networkReplicaPresentationState == null
			|| startIndex < 0
			|| count < 0
			|| startIndex > _networkReplicaPresentationState.TableCards.Count - count)
		{
			return false;
		}

		_networkReplicaPresentationState.TableCards.RemoveRange(startIndex, count);
		return true;
	}

	private bool TrySetNetworkPresentationHandCard(
		int playerIndex,
		int handIndex,
		PlayingCard card)
	{
		if (_networkReplicaPresentationState == null
			|| playerIndex < 0
			|| playerIndex >= _networkReplicaPresentationState.PlayerHands.Count
			|| handIndex < 0
			|| handIndex >= _networkReplicaPresentationState.PlayerHands[playerIndex].Count)
		{
			return false;
		}

		_networkReplicaPresentationState.PlayerHands[playerIndex][handIndex] = card;
		return true;
	}

	private bool TryAttachNetworkPresentationItemToHandCard(
		int playerIndex,
		int handIndex,
		string itemCardId)
	{
		if (!EnsureNetworkReplicaPresentationState()
			|| !IsValidMagnetAttachmentHandIndex(playerIndex, handIndex, itemCardId))
		{
			return false;
		}

		PlayingCard card = _networkReplicaPresentationState!.PlayerHands[playerIndex][handIndex];
		_networkReplicaPresentationState.PlayerHands[playerIndex][handIndex] =
			card.WithAttachedItemEffect(itemCardId);
		return true;
	}

	private bool TryTransferNetworkPresentationHandCard(
		int sourcePlayerIndex,
		int sourceHandIndex,
		int targetPlayerIndex,
		PlayingCard transferredCard)
	{
		if (_networkReplicaPresentationState == null
			|| sourcePlayerIndex < 0
			|| sourcePlayerIndex >= _networkReplicaPresentationState.PlayerHands.Count
			|| targetPlayerIndex < 0
			|| targetPlayerIndex >= _networkReplicaPresentationState.PlayerHands.Count
			|| sourceHandIndex < 0
			|| sourceHandIndex >= _networkReplicaPresentationState.PlayerHands[sourcePlayerIndex].Count
			|| GetTotalHandCardCount(targetPlayerIndex) >= MaxTotalHandCards)
		{
			return false;
		}

		List<PlayingCard> sourceHand = _networkReplicaPresentationState.PlayerHands[sourcePlayerIndex];
		sourceHand.RemoveAt(sourceHandIndex);
		_networkReplicaPresentationState.PlayerHands[targetPlayerIndex].Add(transferredCard);
		return true;
	}

	private bool TryTransferNetworkPresentationItemCard(
		int sourcePlayerIndex,
		int sourceItemHandIndex,
		int targetPlayerIndex,
		ItemCard transferredCard)
	{
		if (_networkReplicaPresentationState == null
			|| sourcePlayerIndex < 0
			|| sourcePlayerIndex >= _networkReplicaPresentationState.PlayerItemHands.Count
			|| targetPlayerIndex < 0
			|| targetPlayerIndex >= _networkReplicaPresentationState.PlayerItemHands.Count
			|| sourceItemHandIndex < 0
			|| sourceItemHandIndex >= _networkReplicaPresentationState.PlayerItemHands[sourcePlayerIndex].Count
			|| GetTotalHandCardCount(targetPlayerIndex) >= MaxTotalHandCards)
		{
			return false;
		}

		List<ItemCard> sourceHand = _networkReplicaPresentationState.PlayerItemHands[sourcePlayerIndex];
		sourceHand.RemoveAt(sourceItemHandIndex);
		_networkReplicaPresentationState.PlayerItemHands[targetPlayerIndex].Add(transferredCard);
		return true;
	}

	private bool TryTransferNetworkPresentationDeckCards(
		int sourcePlayerIndex,
		int targetPlayerIndex,
		int count)
	{
		if (_networkReplicaPresentationState == null
			|| sourcePlayerIndex < 0
			|| sourcePlayerIndex >= _networkReplicaPresentationState.PlayerDecks.Count
			|| targetPlayerIndex < 0
			|| targetPlayerIndex >= _networkReplicaPresentationState.PlayerDecks.Count
			|| count <= 0
			|| count > _networkReplicaPresentationState.PlayerDecks[sourcePlayerIndex].Count)
		{
			return false;
		}

		List<PlayingCard> sourceDeck = _networkReplicaPresentationState.PlayerDecks[sourcePlayerIndex];
		List<PlayingCard> targetDeck = _networkReplicaPresentationState.PlayerDecks[targetPlayerIndex];
		for (int cardIndex = 0; cardIndex < count; cardIndex++)
		{
			targetDeck.Add(sourceDeck[cardIndex]);
		}
		sourceDeck.RemoveRange(0, count);
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
		foreach (Texture2D texture in _playingCardTexturesByCode.Values)
		{
			return texture;
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

	private bool TryGetConfiguredPlayerName(int playerIndex, out string playerName)
	{
		playerName = string.Empty;
		if (string.IsNullOrWhiteSpace(player_names_csv))
		{
			return false;
		}

		string[] names = SplitNetworkCsv(player_names_csv);
		if (playerIndex < 0 || playerIndex >= names.Length)
		{
			return false;
		}

		string configuredName = names[playerIndex];
		if (string.IsNullOrWhiteSpace(configuredName))
		{
			return false;
		}

		playerName = PlayerNameRules.Normalize(configuredName);
		return true;
	}

	private bool TryGetNetworkAvatarId(int playerIndex, out string avatarId)
	{
		avatarId = string.Empty;
		if (string.IsNullOrWhiteSpace(player_avatar_ids_csv))
		{
			return false;
		}

		string[] avatarIds = SplitNetworkCsv(player_avatar_ids_csv);
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
