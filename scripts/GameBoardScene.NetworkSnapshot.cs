using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	internal Godot.Collections.Dictionary BuildNetworkBoardSnapshotForSeat(int recipientSeatIndex, int snapshotId)
	{
		return new Godot.Collections.Dictionary
		{
			["protocol_version"] = MultiplayerBoardSyncRules.ProtocolVersion,
			["match_id"] = _networkMatchId,
			["snapshot_id"] = snapshotId,
			["turn_sequence_generation"] = _turnSequenceGeneration,
			["current_turn_player_index"] = _currentTurnPlayerIndex,
			["winner_player_index"] = _winnerPlayerIndex,
			["scores"] = BuildIntArray(_playerScores, player_count),
			["deck_counts"] = BuildListCountArray(_playerDecks, player_count),
			["hand_counts"] = BuildListCountArray(_playerHands, player_count),
			["item_hand_counts"] = BuildListCountArray(_playerItemHands, player_count),
			["own_item_hand_cards"] = BuildOwnItemHandCardsArray(recipientSeatIndex),
			["table_cards"] = BuildTableCardsArray(recipientSeatIndex),
			["own_hand_cards"] = BuildOwnHandCardsArray(recipientSeatIndex),
			["revealed_target_hand_visible"] = ShouldRevealTargetHandToSeat(recipientSeatIndex),
			["revealed_hand_cards"] = BuildRevealedHandCardsArray(recipientSeatIndex),
			["revealed_item_hand_cards"] = BuildRevealedItemHandCardsArray(recipientSeatIndex),
			["pending_selection_mode"] = _pendingItemSelectionMode.ToString(),
			["pending_selection_owner"] = _pendingItemOwnerIndex,
				["pending_item_card_id"] = _pendingItemCardId,
				["pending_turn_sequence_generation"] = _pendingItemTurnSequenceGeneration,
			["revealed_player_hand_index"] = ShouldRevealTargetHandToSeat(recipientSeatIndex) ? _revealedPlayerHandIndex : -1,
			["opening_deal_animation_playing"] = _isOpeningDealAnimationPlaying,
			["seat_control_states"] = BuildSeatControlStatesArray(),
		};
	}

	internal void ApplyNetworkBoardSnapshot(Godot.Collections.Dictionary snapshot)
	{
		if (!IsUsableNetworkSnapshot(snapshot, out int snapshotId))
		{
			return;
		}

		NetworkManager? networkManager = GetNetworkManager();
		if (NetworkReconnectRules.ShouldApplyBoardSnapshotImmediatelyForClient(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			networkManager?.State ?? NetworkSessionState.Idle))
		{
			ApplyNetworkBoardSnapshotNow(snapshot, snapshotId);
			return;
		}

		if (_networkVisualQueueProcessing || HasQueuedEventForSnapshot(snapshotId))
		{
			if (_networkQueuedSnapshot == null || ReadNetworkInt(_networkQueuedSnapshot, "snapshot_id", -1) <= snapshotId)
			{
				_networkQueuedSnapshot = snapshot;
			}
			return;
		}

		ApplyNetworkBoardSnapshotNow(snapshot, snapshotId);
	}

	private void ApplyNetworkBoardSnapshotNow(Godot.Collections.Dictionary snapshot, int snapshotId)
	{
		bool isReconnectCatchup = IsNetworkMultiplayerMode()
			&& !IsNetworkHost()
			&& GetNetworkManager()?.State == NetworkSessionState.Reconnecting;
		if (isReconnectCatchup)
		{
			_networkVisualEventQueue.Clear();
			_networkQueuedSnapshot = null;
		}

		_networkLastAppliedSnapshotId = snapshotId;
		_networkBoardInitialized = true;
		ClearNetworkPendingActionState();
		ResetNetworkPredictedClientState();
		_hiddenHandDisplaySlots.Clear();
		_shiftedHiddenHandDisplaySlots.Clear();

		EnsureNetworkStateLists();
		_turnSequenceGeneration = ReadNetworkInt(snapshot, "turn_sequence_generation", _turnSequenceGeneration);
		_turnStateMachine.SyncGeneration(_turnSequenceGeneration);
		_currentTurnPlayerIndex = ReadNetworkInt(snapshot, "current_turn_player_index", _currentTurnPlayerIndex);
		_winnerPlayerIndex = ReadNetworkInt(snapshot, "winner_player_index", _winnerPlayerIndex);
		ApplyIntArray(snapshot, "scores", _playerScores, player_count);
		ApplyCountArray(snapshot, "deck_counts", _playerDecks, player_count, CreatePlaceholderPlayingCard);
		ApplyPendingSelectionFromSnapshot(snapshot);
		ApplyHandsFromSnapshot(snapshot);
		ApplyItemHandsFromSnapshot(snapshot);
		ApplyTableCardsFromSnapshot(snapshot);
		ApplySeatControlStatesFromSnapshot(snapshot);
		bool shouldResumeOpeningDealAnimation = !isReconnectCatchup
			&& ReadNetworkBool(snapshot, "opening_deal_animation_playing", false)
			&& !_isOpeningDealAnimationPlaying;
		_isOpeningDealAnimationPlaying = ReadNetworkBool(snapshot, "opening_deal_animation_playing", false);
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		InvalidateRenderCaches();
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			UpdatePlayerScoreMeter(playerIndex, false);
		}

		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		RefreshNetworkPendingSelectionOverlay();
		RefreshNetworkReconnectOverlayIfNeeded();
		MarkSeatControlIndicatorsDirty();
		MarkActivePlayerStatusTooltipForRefresh();
		if (isReconnectCatchup)
		{
			_networkReconnectCatchupPendingCompletion = true;
		}

		CompleteReconnectCatchupIfReady();
		if (_winnerPlayerIndex >= 0)
		{
			ShowWinnerSequence(_winnerPlayerIndex);
		}

		if (shouldResumeOpeningDealAnimation)
		{
			_ = PlayOpeningDealAnimationAsync(_turnSequenceGeneration);
		}
	}

	private void CompleteReconnectCatchupIfReady()
	{
		if (!_networkReconnectCatchupPendingCompletion)
		{
			return;
		}

		NetworkManager? networkManager = GetNetworkManager();
		if (!NetworkReconnectRules.ShouldCompleteReconnectCatchupForClient(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			networkManager?.State ?? NetworkSessionState.Idle,
			_networkVisualQueueProcessing))
		{
			return;
		}

		_networkReconnectCatchupPendingCompletion = false;
		networkManager?.NotifyReconnectSnapshotApplied(_networkMatchId);
		ShowReconnectRecoveryNoticeIfNeeded();
	}

	private bool IsUsableNetworkSnapshot(Godot.Collections.Dictionary snapshot, out int snapshotId)
	{
		snapshotId = ReadNetworkInt(snapshot, "snapshot_id", -1);
		if (ReadNetworkInt(snapshot, "protocol_version", 0) != MultiplayerBoardSyncRules.ProtocolVersion)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(_networkMatchId)
			&& ReadNetworkString(snapshot, "match_id", _networkMatchId) != _networkMatchId)
		{
			return false;
		}

		return snapshotId > _networkLastAppliedSnapshotId;
	}

	private bool HasQueuedEventForSnapshot(int snapshotId)
	{
		foreach (Godot.Collections.Dictionary payload in _networkVisualEventQueue)
		{
			if (ReadNetworkInt(payload, "snapshot_id", -1) == snapshotId)
			{
				return true;
			}
		}

		return false;
	}

	private void InitializeNetworkClientBoardShell()
	{
		if (_networkBoardInitialized)
		{
			return;
		}

		_networkBoardInitialized = true;
		UpdatePlayerVisibility();
		EnsureNetworkStateLists();
		ResetPlayerScores();
		InvalidateRenderCaches();
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
	}

	private void EnsureNetworkStateLists()
	{
		EnsureListCount(_playerDecks, MaxPlayers);
		EnsureListCount(_playerHands, MaxPlayers);
		EnsureListCount(_playerItemHands, MaxPlayers);
		EnsureListCount(_openingVisibleHandCardCounts, MaxPlayers);
		EnsureListCount(_openingVisiblePileCardCounts, MaxPlayers);
	}

	private static void EnsureListCount<T>(List<T> list, int count) where T : new()
	{
		while (list.Count < count)
		{
			list.Add(new T());
		}
	}

	private void ApplyHandsFromSnapshot(Godot.Collections.Dictionary snapshot)
	{
		int localSeatIndex = GetLocalHandPlayerIndex();
		int revealedPlayerIndex = ReadNetworkInt(snapshot, "revealed_player_hand_index", -1);
		bool revealedTargetHandVisible = ReadNetworkBool(snapshot, "revealed_target_hand_visible", false);
		Godot.Collections.Array revealedHandCards = ReadArray(snapshot, "revealed_hand_cards");
		int[] handCounts = ReadIntArray(snapshot, "hand_counts", player_count);
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			_playerHands[playerIndex].Clear();
			if (playerIndex == localSeatIndex)
			{
				foreach (Variant cardVariant in ReadArray(snapshot, "own_hand_cards"))
				{
					if (cardVariant.AsGodotDictionary() is Godot.Collections.Dictionary card)
					{
						_playerHands[playerIndex].Add(CreateNetworkPlayingCard(card));
					}
				}
				continue;
			}

			if (revealedTargetHandVisible && playerIndex == revealedPlayerIndex)
			{
				foreach (Variant cardVariant in revealedHandCards)
				{
					if (cardVariant.AsGodotDictionary() is Godot.Collections.Dictionary card)
					{
						_playerHands[playerIndex].Add(CreateNetworkPlayingCard(card));
					}
				}
				continue;
			}

			int handCount = playerIndex < handCounts.Length ? handCounts[playerIndex] : 0;
			for (int cardIndex = 0; cardIndex < handCount; cardIndex++)
			{
				_playerHands[playerIndex].Add(CreatePlaceholderPlayingCard());
			}
		}
	}

	private void ApplyItemHandsFromSnapshot(Godot.Collections.Dictionary snapshot)
	{
		int localSeatIndex = GetLocalHandPlayerIndex();
		int revealedPlayerIndex = ReadNetworkInt(snapshot, "revealed_player_hand_index", -1);
		bool revealedTargetHandVisible = ReadNetworkBool(snapshot, "revealed_target_hand_visible", false);
		Godot.Collections.Array revealedItemHandCards = ReadArray(snapshot, "revealed_item_hand_cards");
		int[] itemHandCounts = ReadIntArray(snapshot, "item_hand_counts", player_count);
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			_playerItemHands[playerIndex].Clear();
			if (playerIndex == localSeatIndex)
			{
				foreach (Variant cardVariant in ReadArray(snapshot, "own_item_hand_cards"))
				{
					if (cardVariant.AsGodotDictionary() is not Godot.Collections.Dictionary itemCard)
					{
						continue;
					}

					string itemCardId = ReadNetworkString(itemCard, "id", string.Empty);
					ItemCard? createdCard = CreateItemCard(itemCardId);
					_playerItemHands[playerIndex].Add(createdCard ?? CreatePlaceholderItemCard());
				}
				continue;
			}

			if (revealedTargetHandVisible && playerIndex == revealedPlayerIndex)
			{
				foreach (Variant cardVariant in revealedItemHandCards)
				{
					if (cardVariant.AsGodotDictionary() is not Godot.Collections.Dictionary itemCard)
					{
						continue;
					}

					string itemCardId = ReadNetworkString(itemCard, "id", string.Empty);
					ItemCard? createdCard = CreateItemCard(itemCardId);
					_playerItemHands[playerIndex].Add(createdCard ?? CreatePlaceholderItemCard());
				}
				continue;
			}

			int itemHandCount = playerIndex < itemHandCounts.Length ? itemHandCounts[playerIndex] : 0;
			for (int itemIndex = 0; itemIndex < itemHandCount; itemIndex++)
			{
				_playerItemHands[playerIndex].Add(CreatePlaceholderItemCard());
			}
		}
	}

	private void ApplyTableCardsFromSnapshot(Godot.Collections.Dictionary snapshot)
	{
		_tableCards.Clear();
		foreach (Variant tableCardVariant in ReadArray(snapshot, "table_cards"))
		{
			if (tableCardVariant.AsGodotDictionary() is not Godot.Collections.Dictionary tableCard)
			{
				continue;
			}

			NetworkTableCardSnapshot tableCardSnapshot = new(
				ReadNetworkString(tableCard, NetworkTableCardSnapshotCodec.CodeKey, string.Empty),
				ReadStringArray(tableCard, NetworkTableCardSnapshotCodec.AttachedItemCardIdsKey),
				ReadNetworkBool(tableCard, NetworkTableCardSnapshotCodec.IsFaceUpKey, false),
				ReadNetworkBool(tableCard, NetworkTableCardSnapshotCodec.IsBuriedInSandKey, false),
				ReadNetworkBool(tableCard, NetworkTableCardSnapshotCodec.IsSlimeCoatedKey, false),
				ReadNetworkInt(tableCard, NetworkTableCardSnapshotCodec.CardOwnerPlayerIndexKey, -1),
				ReadNetworkBool(tableCard, NetworkTableCardSnapshotCodec.RemoveCardOnCaptureKey, false),
				ReadNetworkString(tableCard, NetworkTableCardSnapshotCodec.ItemOverlayIdKey, string.Empty),
				ReadNetworkInt(tableCard, NetworkTableCardSnapshotCodec.ItemOwnerPlayerIndexKey, -1));
			PlayingCard? card = BuildNetworkSnapshotTableCard(tableCard, tableCardSnapshot);
			ItemCard? itemOverlay = CreateNetworkSnapshotItemOverlay(tableCardSnapshot.ItemOverlayId);
			var appliedCard = new TableCard(
				card,
				tableCardSnapshot.IsFaceUp,
				itemOverlay,
				tableCardSnapshot.ItemOwnerPlayerIndex,
				tableCardSnapshot.CardOwnerPlayerIndex,
				tableCardSnapshot.RemoveCardOnCapture)
			{
				IsBuriedInSand = tableCardSnapshot.IsBuriedInSand,
				IsSlimeCoated = tableCardSnapshot.IsSlimeCoated,
			};
			_tableCards.Add(appliedCard);
		}
	}

	private Godot.Collections.Array BuildTableCardsArray(int recipientSeatIndex)
	{
		var result = new Godot.Collections.Array();
		foreach (TableCard tableCard in _tableCards)
		{
			bool canSeeCard = tableCard.Card.HasValue
				&& (tableCard.IsFaceUp || tableCard.CardOwnerPlayerIndex == recipientSeatIndex);
			NetworkTableCardSnapshot tableCardSnapshot = new(
				canSeeCard ? ToNetworkCardCode(tableCard.Card!.Value) : string.Empty,
				canSeeCard ? CopyStringArray(tableCard.Card!.Value.GetAttachedItemCardIds()) : Array.Empty<string>(),
				tableCard.IsFaceUp,
				tableCard.IsBuriedInSand,
				tableCard.IsSlimeCoated,
				tableCard.CardOwnerPlayerIndex,
				tableCard.RemoveCardOnCapture,
				tableCard.ItemOverlay.HasValue ? tableCard.ItemOverlay.Value.Id : string.Empty,
				tableCard.ItemOwnerPlayerIndex);
			result.Add(new Godot.Collections.Dictionary
			{
				[NetworkTableCardSnapshotCodec.CodeKey] = tableCardSnapshot.CardCode,
				[NetworkTableCardSnapshotCodec.AttachedItemCardIdsKey] = BuildStringArray(tableCardSnapshot.AttachedItemCardIds),
				[NetworkTableCardSnapshotCodec.IsFaceUpKey] = tableCardSnapshot.IsFaceUp,
				[NetworkTableCardSnapshotCodec.IsBuriedInSandKey] = tableCardSnapshot.IsBuriedInSand,
				[NetworkTableCardSnapshotCodec.IsSlimeCoatedKey] = tableCardSnapshot.IsSlimeCoated,
				[NetworkTableCardSnapshotCodec.CardOwnerPlayerIndexKey] = tableCardSnapshot.CardOwnerPlayerIndex,
				[NetworkTableCardSnapshotCodec.RemoveCardOnCaptureKey] = tableCardSnapshot.RemoveCardOnCapture,
				[NetworkTableCardSnapshotCodec.ItemOverlayIdKey] = tableCardSnapshot.ItemOverlayId,
				[NetworkTableCardSnapshotCodec.ItemOwnerPlayerIndexKey] = tableCardSnapshot.ItemOwnerPlayerIndex,
			});
		}

		return result;
	}

	private Godot.Collections.Array BuildOwnHandCardsArray(int recipientSeatIndex)
	{
		var result = new Godot.Collections.Array();
		if (recipientSeatIndex < 0 || recipientSeatIndex >= _playerHands.Count)
		{
			return result;
		}

		foreach (PlayingCard card in _playerHands[recipientSeatIndex])
		{
			result.Add(BuildPlayingCardArrayEntry(card));
		}

		return result;
	}

	private Godot.Collections.Array BuildRevealedHandCardsArray(int recipientSeatIndex)
	{
		var result = new Godot.Collections.Array();
		if (!ShouldRevealTargetHandToSeat(recipientSeatIndex)
			|| _revealedPlayerHandIndex < 0
			|| _revealedPlayerHandIndex >= _playerHands.Count)
		{
			return result;
		}

		foreach (PlayingCard card in _playerHands[_revealedPlayerHandIndex])
		{
			result.Add(BuildPlayingCardArrayEntry(card));
		}

		return result;
	}

	private Godot.Collections.Array BuildRevealedItemHandCardsArray(int recipientSeatIndex)
	{
		var result = new Godot.Collections.Array();
		if (!ShouldRevealTargetHandToSeat(recipientSeatIndex)
			|| _revealedPlayerHandIndex < 0
			|| _revealedPlayerHandIndex >= _playerItemHands.Count)
		{
			return result;
		}

		foreach (ItemCard card in _playerItemHands[_revealedPlayerHandIndex])
		{
			result.Add(new Godot.Collections.Dictionary
			{
				["id"] = card.Id,
			});
		}

		return result;
	}

	private Godot.Collections.Array BuildOwnItemHandCardsArray(int recipientSeatIndex)
	{
		var result = new Godot.Collections.Array();
		if (recipientSeatIndex < 0 || recipientSeatIndex >= _playerItemHands.Count)
		{
			return result;
		}

		foreach (ItemCard card in _playerItemHands[recipientSeatIndex])
		{
			result.Add(new Godot.Collections.Dictionary
			{
				["id"] = card.Id,
			});
		}

		return result;
	}

	private Godot.Collections.Array BuildSeatControlStatesArray()
	{
		return GetNetworkManager()?.GetSeatControlStatesVariant(includeReconnectTokens: false)
			?? new Godot.Collections.Array();
	}

	private static Godot.Collections.Array BuildIntArray(int[] values, int count)
	{
		var result = new Godot.Collections.Array();
		for (int index = 0; index < count && index < values.Length; index++)
		{
			result.Add(values[index]);
		}

		return result;
	}

	private static Godot.Collections.Array BuildStringArray(IReadOnlyList<string> values)
	{
		var result = new Godot.Collections.Array();
		foreach (string value in values)
		{
			result.Add(value);
		}

		return result;
	}

	private static string[] CopyStringArray(IReadOnlyList<string> values)
	{
		var result = new string[values.Count];
		for (int index = 0; index < values.Count; index++)
		{
			result[index] = values[index];
		}

		return result;
	}

	private static Godot.Collections.Array BuildListCountArray<T>(IReadOnlyList<List<T>> lists, int count)
	{
		var result = new Godot.Collections.Array();
		for (int index = 0; index < count && index < lists.Count; index++)
		{
			result.Add(lists[index].Count);
		}

		return result;
	}

	private static Godot.Collections.Array ReadArray(Godot.Collections.Dictionary dictionary, string key)
	{
		return dictionary.TryGetValue(key, out Variant value) && value.VariantType == Variant.Type.Array
			? value.AsGodotArray()
			: new Godot.Collections.Array();
	}

	private static int[] ReadIntArray(Godot.Collections.Dictionary dictionary, string key, int count)
	{
		Godot.Collections.Array array = ReadArray(dictionary, key);
		var result = new int[Math.Max(count, array.Count)];
		for (int index = 0; index < array.Count; index++)
		{
			result[index] = array[index].AsInt32();
		}

		return result;
	}

	private static string[] ReadStringArray(Godot.Collections.Dictionary dictionary, string key)
	{
		Godot.Collections.Array array = ReadArray(dictionary, key);
		var result = new List<string>(array.Count);
		foreach (Variant entry in array)
		{
			string value = entry.AsString();
			if (!string.IsNullOrWhiteSpace(value))
			{
				result.Add(value);
			}
		}

		return result.ToArray();
	}

	private static bool[] ReadBoolArray(Godot.Collections.Dictionary dictionary, string key, int count)
	{
		Godot.Collections.Array array = ReadArray(dictionary, key);
		var result = new bool[Math.Max(count, array.Count)];
		for (int index = 0; index < array.Count; index++)
		{
			result[index] = array[index].AsBool();
		}

		return result;
	}

	private static void ApplyIntArray(Godot.Collections.Dictionary dictionary, string key, int[] target, int count)
	{
		int[] values = ReadIntArray(dictionary, key, count);
		for (int index = 0; index < count && index < target.Length; index++)
		{
			target[index] = index < values.Length ? values[index] : 0;
		}
	}

	private static void ApplyCountArray<T>(
		Godot.Collections.Dictionary dictionary,
		string key,
		IReadOnlyList<List<T>> target,
		int count,
		Func<T> createPlaceholder)
	{
		int[] values = ReadIntArray(dictionary, key, count);
		for (int index = 0; index < count && index < target.Count; index++)
		{
			target[index].Clear();
			int itemCount = index < values.Length ? values[index] : 0;
			for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
			{
				target[index].Add(createPlaceholder());
			}
		}
	}

	private PlayingCard? BuildNetworkSnapshotTableCard(
		Godot.Collections.Dictionary tableCard,
		NetworkTableCardSnapshot tableCardSnapshot)
	{
		if (!string.IsNullOrWhiteSpace(tableCardSnapshot.CardCode))
		{
			return CreateNetworkPlayingCard(tableCard);
		}

		if (!string.IsNullOrWhiteSpace(tableCardSnapshot.ItemOverlayId))
		{
			return null;
		}

		return tableCardSnapshot.IsFaceUp ? null : CreatePlaceholderPlayingCard();
	}

	private ItemCard? CreateNetworkSnapshotItemOverlay(string itemCardId)
	{
		if (string.IsNullOrWhiteSpace(itemCardId))
		{
			return null;
		}

		ItemCard? itemOverlay = CreateItemCard(itemCardId);
		return itemOverlay ?? new ItemCard(itemCardId, GetNetworkPlaceholderTexture());
	}

	private void ApplyPendingSelectionFromSnapshot(Godot.Collections.Dictionary snapshot)
	{
			_pendingItemSelectionMode = ReadEnum(snapshot, "pending_selection_mode", PendingItemSelectionMode.None);
			_pendingItemOwnerIndex = ReadNetworkInt(snapshot, "pending_selection_owner", -1);
			_pendingItemCardId = ReadNetworkString(snapshot, "pending_item_card_id", string.Empty);
			_pendingItemTurnSequenceGeneration = ReadNetworkInt(snapshot, "pending_turn_sequence_generation", -1);
		_revealedPlayerHandIndex = ReadNetworkBool(snapshot, "revealed_target_hand_visible", false)
			? ReadNetworkInt(snapshot, "revealed_player_hand_index", -1)
			: -1;
	}

	private void ApplySeatControlStatesFromSnapshot(Godot.Collections.Dictionary snapshot)
	{
		bool wasActiveTooltipAffected = _activeStatusTooltipPlayerIndex >= 0;
		_networkSeatControlStates.Clear();
		foreach (Variant stateVariant in ReadArray(snapshot, "seat_control_states"))
		{
			if (stateVariant.AsGodotDictionary() is not Godot.Collections.Dictionary stateDictionary)
			{
				continue;
			}

			_networkSeatControlStates.Add(new SeatControlState(
				ReadNetworkInt(stateDictionary, "seat_index", -1),
				ReadEnum(stateDictionary, "mode", SeatControlMode.HumanConnected),
				ReadNetworkInt(stateDictionary, "peer_id", 0),
				ReadNetworkString(stateDictionary, "stable_user_id", string.Empty),
				ReadNetworkString(stateDictionary, "reconnect_token", string.Empty)));
		}

		if (wasActiveTooltipAffected)
		{
			MarkActivePlayerStatusTooltipForRefresh();
		}
		MarkSeatControlIndicatorsDirty();
	}
}
