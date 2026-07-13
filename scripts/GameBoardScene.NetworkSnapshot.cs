using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private readonly record struct NetworkPlayingCardSnapshot(string CardCode, string[] AttachedItemCardIds);

	private sealed record ValidatedNetworkBoardSnapshot(
		int SnapshotId, int TurnSequenceGeneration, int CurrentTurnPlayerIndex,
		int WinnerPlayerIndex, int[] Scores, int[] DeckCounts, int[] HandCounts,
		int[] ItemHandCounts, NetworkPlayingCardSnapshot[] OwnHandCards,
		string[] OwnItemHandCardIds, bool RevealedTargetHandVisible,
		NetworkPlayingCardSnapshot[] RevealedHandCards,
		string[] RevealedItemHandCardIds, PendingItemSelectionMode PendingSelectionMode,
		int PendingSelectionOwner, string PendingItemCardId,
		int PendingTurnSequenceGeneration, int RevealedPlayerHandIndex,
		bool OpeningDealAnimationPlaying, NetworkTableCardSnapshot[] TableCards,
		SeatControlState[] SeatControlStates);

	private static bool TryReadNetworkArray(Godot.Collections.Dictionary dictionary, string key, out Godot.Collections.Array values)
	{
		values = default!;
		if (!dictionary.TryGetValue(key, out Variant rawValue) || rawValue.VariantType != Variant.Type.Array) return false;
		values = rawValue.AsGodotArray();
		return true;
	}

	private static bool TryReadNetworkDictionary(Variant rawValue, out Godot.Collections.Dictionary dictionary)
	{
		dictionary = default!;
		if (rawValue.VariantType != Variant.Type.Dictionary) return false;
		dictionary = rawValue.AsGodotDictionary();
		return true;
	}

	private static bool TryReadBoundedNetworkIntArray(Godot.Collections.Dictionary dictionary, string key, int minimumCount, int maximumCount, int minimumValue, int maximumValue, out int[] values)
	{
		values = Array.Empty<int>();
		if (!TryReadNetworkArray(dictionary, key, out Godot.Collections.Array rawValues) || rawValues.Count < minimumCount || rawValues.Count > maximumCount) return false;
		var decoded = new int[rawValues.Count];
		for (int index = 0; index < rawValues.Count; index++)
		{
			if (!TryConvertNetworkInt32(rawValues[index], out int value) || value < minimumValue || value > maximumValue) return false;
			decoded[index] = value;
		}
		values = decoded;
		return true;
	}

	private static bool TryReadBoundedNetworkStringArray(Godot.Collections.Dictionary dictionary, string key, int minimumCount, int maximumCount, int maximumElementLength, out string[] values)
	{
		values = Array.Empty<string>();
		if (!TryReadNetworkArray(dictionary, key, out Godot.Collections.Array rawValues) || rawValues.Count < minimumCount || rawValues.Count > maximumCount) return false;
		var decoded = new string[rawValues.Count];
		for (int index = 0; index < rawValues.Count; index++)
		{
			if (rawValues[index].VariantType != Variant.Type.String) return false;
			string value = rawValues[index].AsString();
			if (value.Length > maximumElementLength) return false;
			decoded[index] = value;
		}
		values = decoded;
		return true;
	}

	private static bool TryReadNetworkEnum<TEnum>(Godot.Collections.Dictionary dictionary, string key, out TEnum value) where TEnum : struct, Enum
	{
		value = default;
		return TryReadNetworkString(dictionary, key, out string rawValue) && Enum.TryParse(rawValue, true, out value) && Enum.IsDefined(typeof(TEnum), value);
	}

	private static bool TryReadExactNetworkBoolArray(Godot.Collections.Dictionary dictionary, string key, int expectedCount, out bool[] values)
	{
		values = Array.Empty<bool>();
		if (!TryReadNetworkArray(dictionary, key, out Godot.Collections.Array rawValues) || rawValues.Count != expectedCount) return false;
		var decoded = new bool[expectedCount];
		for (int index = 0; index < expectedCount; index++) { if (rawValues[index].VariantType != Variant.Type.Bool) return false; decoded[index] = rawValues[index].AsBool(); }
		values = decoded;
		return true;
	}

	private static bool HasUniqueValues(IReadOnlyList<int> values)
	{
		for (int index = 0; index < values.Count; index++) for (int previous = 0; previous < index; previous++) if (values[index] == values[previous]) return false;
		return true;
	}

	private bool IsValidNetworkItemCardId(string itemCardId, bool requirePlayableFromHand)
	{
		return !string.IsNullOrWhiteSpace(itemCardId) && itemCardId.Length <= MaxNetworkItemCardIdLength
			&& _itemCardCatalog.IsEnabled(itemCardId) && (!requirePlayableFromHand || _itemCardCatalog.IsPlayable(itemCardId))
			&& _itemCardTextures.ContainsKey(itemCardId);
	}

	private bool IsValidNetworkAttachmentItemCardId(string itemCardId) => IsValidNetworkItemCardId(itemCardId, false) && IsAttachmentItemCard(itemCardId);
	private bool HasValidNetworkAttachedItemCardIds(IReadOnlyList<string> ids) => ids.Count <= 1 && (ids.Count == 0 || IsValidNetworkAttachmentItemCardId(ids[0]));
	private bool TryParseNetworkAttachedItemCardIdCsv(string value, out string[] itemCardIds)
	{
		itemCardIds = Array.Empty<string>();
		if (string.IsNullOrEmpty(value)) return true;
		if (value.Length > MaxNetworkAttachmentCsvLength || value.IndexOf(',') >= 0) return false;
		string itemCardId = value.Trim();
		if (itemCardId.Length == 0 || !IsValidNetworkAttachmentItemCardId(itemCardId)) return false;
		itemCardIds = new[] { itemCardId };
		return true;
	}

	private bool TryValidateNetworkCaptureRevealEntry(int tableCardIndex, string cardCode, string attachmentCsv, int points, bool doubled, out string[] attachedIds)
	{
		attachedIds = Array.Empty<string>();
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count || cardCode.Length > MaxNetworkCardCodeLength || !TryParseNetworkAttachedItemCardIdCsv(attachmentCsv, out attachedIds) || (points == 0 && doubled)) return false;
		TableCard tableCard = _tableCards[tableCardIndex];
		if (tableCard.IsFaceUp || tableCard.IsBuriedInSand) return false;
		if (string.IsNullOrEmpty(cardCode)) return !tableCard.Card.HasValue && tableCard.ItemOverlay.HasValue && attachedIds.Length == 0 && points == 0 && !doubled;
		return tableCard.Card.HasValue && _networkCardsByCode.ContainsKey(cardCode);
	}

	private bool TryDecodeNetworkPlayingCards(Godot.Collections.Array rawCards, out NetworkPlayingCardSnapshot[] cards)
	{
		cards = Array.Empty<NetworkPlayingCardSnapshot>();
		if (rawCards.Count > MaxTotalHandCards) return false;
		var decoded = new NetworkPlayingCardSnapshot[rawCards.Count];
		for (int index = 0; index < rawCards.Count; index++)
		{
			if (!TryReadNetworkDictionary(rawCards[index], out Godot.Collections.Dictionary rawCard)
				|| !TryReadNetworkString(rawCard, "code", MaxNetworkCardCodeLength, out string cardCode)
				|| string.IsNullOrWhiteSpace(cardCode) || !_networkCardsByCode.ContainsKey(cardCode)
				|| !TryReadBoundedNetworkStringArray(rawCard, "attached_item_card_ids", 0, 1, MaxNetworkItemCardIdLength, out string[] ids)
				|| !HasValidNetworkAttachedItemCardIds(ids)) return false;
			decoded[index] = new NetworkPlayingCardSnapshot(cardCode, ids);
		}
		cards = decoded;
		return true;
	}

	private bool TryDecodeNetworkItemCards(Godot.Collections.Array rawCards, out string[] ids)
	{
		ids = Array.Empty<string>();
		if (rawCards.Count > MaxTotalHandCards) return false;
		var decoded = new string[rawCards.Count];
		for (int index = 0; index < rawCards.Count; index++)
		{
			if (!TryReadNetworkDictionary(rawCards[index], out Godot.Collections.Dictionary rawCard)
				|| !TryReadNetworkString(rawCard, "id", MaxNetworkItemCardIdLength, out string id)
				|| !IsValidNetworkItemCardId(id, true)) return false;
			decoded[index] = id;
		}
		ids = decoded;
		return true;
	}

	private bool TryDecodeNetworkTableCards(Godot.Collections.Array rawCards, int maximumCount, out NetworkTableCardSnapshot[] cards)
	{
		cards = Array.Empty<NetworkTableCardSnapshot>();
		if (rawCards.Count > maximumCount) return false;
		var decoded = new NetworkTableCardSnapshot[rawCards.Count];
		for (int index = 0; index < rawCards.Count; index++)
		{
			if (!TryReadNetworkDictionary(rawCards[index], out Godot.Collections.Dictionary rawCard)
				|| !TryReadNetworkString(rawCard, NetworkTableCardSnapshotCodec.CodeKey, MaxNetworkCardCodeLength, out string code)
				|| (!string.IsNullOrEmpty(code) && !_networkCardsByCode.ContainsKey(code))
				|| !TryReadBoundedNetworkStringArray(rawCard, NetworkTableCardSnapshotCodec.AttachedItemCardIdsKey, 0, 1, MaxNetworkItemCardIdLength, out string[] attachedIds)
				|| !HasValidNetworkAttachedItemCardIds(attachedIds) || (attachedIds.Length > 0 && string.IsNullOrEmpty(code))
				|| !TryReadNetworkBool(rawCard, NetworkTableCardSnapshotCodec.IsFaceUpKey, out bool faceUp)
				|| !TryReadNetworkBool(rawCard, NetworkTableCardSnapshotCodec.IsBuriedInSandKey, out bool buried)
				|| !TryReadNetworkBool(rawCard, NetworkTableCardSnapshotCodec.IsSlimeCoatedKey, out bool slime)
				|| !TryReadNetworkInt32(rawCard, NetworkTableCardSnapshotCodec.CardOwnerPlayerIndexKey, -1, player_count - 1, out int cardOwner)
				|| !TryReadNetworkBool(rawCard, NetworkTableCardSnapshotCodec.RemoveCardOnCaptureKey, out bool removeOnCapture)
				|| !TryReadNetworkString(rawCard, NetworkTableCardSnapshotCodec.ItemOverlayIdKey, MaxNetworkItemCardIdLength, out string overlayId)
				|| (!string.IsNullOrEmpty(overlayId) && !IsValidNetworkItemCardId(overlayId, false))
				|| !TryReadNetworkInt32(rawCard, NetworkTableCardSnapshotCodec.ItemOwnerPlayerIndexKey, -1, player_count - 1, out int itemOwner)
				|| (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(overlayId) && faceUp)
				|| (string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(overlayId) && cardOwner != -1)
				|| (string.IsNullOrEmpty(overlayId) && itemOwner != -1)) return false;
			decoded[index] = new NetworkTableCardSnapshot(code, attachedIds, faceUp, buried, slime, cardOwner, removeOnCapture, overlayId, itemOwner);
		}
		cards = decoded;
		return true;
	}

	private static bool TryDecodeNetworkSeatControlStates(Godot.Collections.Array rawStates, int playerCount, out SeatControlState[] states)
	{
		states = Array.Empty<SeatControlState>();
		if (rawStates.Count != playerCount) return false;
		var decoded = new SeatControlState[playerCount];
		var seen = new bool[playerCount];
		for (int index = 0; index < rawStates.Count; index++)
		{
			if (!TryReadNetworkDictionary(rawStates[index], out Godot.Collections.Dictionary rawState)
				|| !TryReadNetworkInt32(rawState, "seat_index", 0, playerCount - 1, out int seatIndex) || seen[seatIndex]
				|| !TryReadNetworkEnum(rawState, "mode", out SeatControlMode mode)
				|| !TryReadNetworkInt32(rawState, "peer_id", 0, int.MaxValue, out int peerId)
				|| !TryReadNetworkString(rawState, "stable_user_id", MaxNetworkOpaqueIdentityLength, out string stableUserId)
				|| !TryReadNetworkString(rawState, "reconnect_token", MaxNetworkOpaqueIdentityLength, out string reconnectToken)) return false;
			seen[seatIndex] = true;
			decoded[seatIndex] = new SeatControlState(seatIndex, mode, peerId, stableUserId, reconnectToken);
		}
		states = decoded;
		return true;
	}
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
		if (!TryDecodeNetworkBoardSnapshot(snapshot, out ValidatedNetworkBoardSnapshot decodedSnapshot))
		{
			return;
		}

		bool isReconnectCatchup = IsNetworkMultiplayerMode()
			&& !IsNetworkHost()
			&& GetNetworkManager()?.State == NetworkSessionState.Reconnecting;
		if (isReconnectCatchup)
		{
			_networkReconnectCatchupPendingCompletion = true;
		}
		NetworkVisualPlayback.SubmitSnapshot(decodedSnapshot);
	}

	private void ApplyNetworkBoardSnapshotNow(ValidatedNetworkBoardSnapshot snapshot)
	{
		bool isReconnectCatchup = IsNetworkMultiplayerMode()
			&& !IsNetworkHost()
			&& GetNetworkManager()?.State == NetworkSessionState.Reconnecting;
		if (isReconnectCatchup)
		{
			NetworkVisualPlayback.ClearEvents();
			NetworkVisualPlayback.ClearPendingSnapshot();
		}

		_networkBoardInitialized = true;
		ClearNetworkPendingActionState();
		ResetNetworkPredictedClientState();
		_hiddenHandDisplaySlots.Clear();
		_shiftedHiddenHandDisplaySlots.Clear();

		EnsureNetworkStateLists();
		_turnSequenceGeneration = snapshot.TurnSequenceGeneration;
		_turnStateMachine.SyncGeneration(_turnSequenceGeneration);
		_currentTurnPlayerIndex = snapshot.CurrentTurnPlayerIndex;
		_winnerPlayerIndex = snapshot.WinnerPlayerIndex;
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++) _playerScores[playerIndex] = snapshot.Scores[playerIndex];
		ApplyValidatedCountArray(snapshot.DeckCounts, _playerDecks, CreatePlaceholderPlayingCard);
		ApplyPendingSelectionFromSnapshot(snapshot);
		ApplyHandsFromSnapshot(snapshot);
		ApplyItemHandsFromSnapshot(snapshot);
		ApplyTableCardsFromSnapshot(snapshot);
		ApplySeatControlStatesFromSnapshot(snapshot);
		bool shouldResumeOpeningDealAnimation = !isReconnectCatchup
			&& snapshot.OpeningDealAnimationPlaying
			&& !_isOpeningDealAnimationPlaying;
		_isOpeningDealAnimationPlaying = snapshot.OpeningDealAnimationPlaying;
		_networkLastAppliedSnapshotId = snapshot.SnapshotId;
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
			NetworkVisualPlayback.IsProcessing))
		{
			return;
		}

		_networkReconnectCatchupPendingCompletion = false;
		networkManager?.NotifyReconnectSnapshotApplied(_networkMatchId);
		ShowReconnectRecoveryNoticeIfNeeded();
	}

	private bool IsUsableNetworkSnapshot(Godot.Collections.Dictionary raw, out int snapshotId)
	{
		bool decoded = TryDecodeNetworkBoardSnapshot(raw, out ValidatedNetworkBoardSnapshot snapshot);
		snapshotId = decoded ? snapshot.SnapshotId : -1;
		return decoded;
	}

	private bool TryDecodeNetworkBoardSnapshot(Godot.Collections.Dictionary raw, out ValidatedNetworkBoardSnapshot snapshot)
	{
		snapshot = default!;
		if (player_count < 2 || player_count > MaxPlayers
			|| !TryReadNetworkInt32(raw, "protocol_version", MultiplayerBoardSyncRules.ProtocolVersion, MultiplayerBoardSyncRules.ProtocolVersion, out _)
			|| !TryReadNetworkString(raw, "match_id", MaxNetworkSessionIdLength, out string matchId)
			|| (!string.IsNullOrWhiteSpace(_networkMatchId) && !string.Equals(matchId, _networkMatchId, StringComparison.Ordinal))
			|| !TryReadNetworkInt32(raw, "snapshot_id", 0, int.MaxValue, out int snapshotId) || snapshotId <= _networkLastAppliedSnapshotId
			|| !TryReadNetworkInt32(raw, "turn_sequence_generation", 0, int.MaxValue, out int generation)
			|| !TryReadNetworkInt32(raw, "current_turn_player_index", 0, player_count - 1, out int currentPlayer)
			|| !TryReadNetworkInt32(raw, "winner_player_index", -1, player_count - 1, out int winner)) return false;

		EnsureNetworkCardCache();
		int maxDeck = _networkCardsByCode.Count * (player_count > 2 ? 2 : 1);
		if (maxDeck <= 0 || !TryReadBoundedNetworkIntArray(raw, "scores", player_count, player_count, 0, int.MaxValue, out int[] scores)
			|| !TryReadBoundedNetworkIntArray(raw, "deck_counts", player_count, player_count, 0, maxDeck, out int[] decks)
			|| !TryReadBoundedNetworkIntArray(raw, "hand_counts", player_count, player_count, 0, MaxTotalHandCards, out int[] hands)
			|| !TryReadBoundedNetworkIntArray(raw, "item_hand_counts", player_count, player_count, 0, MaxTotalHandCards, out int[] items)) return false;
		for (int seat = 0; seat < player_count; seat++) if ((long)hands[seat] + items[seat] > MaxTotalHandCards) return false;

		if (!TryReadNetworkArray(raw, "own_hand_cards", out Godot.Collections.Array ownPlayingRaw)
			|| !TryReadNetworkArray(raw, "own_item_hand_cards", out Godot.Collections.Array ownItemsRaw)
			|| !TryReadNetworkArray(raw, "revealed_hand_cards", out Godot.Collections.Array revealedPlayingRaw)
			|| !TryReadNetworkArray(raw, "revealed_item_hand_cards", out Godot.Collections.Array revealedItemsRaw)
			|| !TryReadNetworkArray(raw, "table_cards", out Godot.Collections.Array tableRaw)
			|| !TryReadNetworkArray(raw, "seat_control_states", out Godot.Collections.Array seatsRaw)
			|| !TryDecodeNetworkPlayingCards(ownPlayingRaw, out NetworkPlayingCardSnapshot[] ownPlaying)
			|| !TryDecodeNetworkItemCards(ownItemsRaw, out string[] ownItems)
			|| !TryDecodeNetworkPlayingCards(revealedPlayingRaw, out NetworkPlayingCardSnapshot[] revealedPlaying)
			|| !TryDecodeNetworkItemCards(revealedItemsRaw, out string[] revealedItems)
			|| !TryDecodeNetworkTableCards(tableRaw, maxDeck * player_count + MaxTotalHandCards * player_count, out NetworkTableCardSnapshot[] table)
			|| !TryDecodeNetworkSeatControlStates(seatsRaw, player_count, out SeatControlState[] seats)
			|| !TryReadNetworkBool(raw, "revealed_target_hand_visible", out bool revealedVisible)
			|| !TryReadNetworkInt32(raw, "revealed_player_hand_index", -1, player_count - 1, out int revealedPlayer)
			|| !TryReadNetworkEnum(raw, "pending_selection_mode", out PendingItemSelectionMode pendingMode)
			|| !TryReadNetworkInt32(raw, "pending_selection_owner", -1, player_count - 1, out int pendingOwner)
			|| !TryReadNetworkString(raw, "pending_item_card_id", MaxNetworkItemCardIdLength, out string pendingId)
			|| !TryReadNetworkInt32(raw, "pending_turn_sequence_generation", -1, int.MaxValue, out int pendingGeneration)
			|| !TryReadNetworkBool(raw, "opening_deal_animation_playing", out bool opening)) return false;

		int localSeat = GetLocalHandPlayerIndex();
		if (localSeat < 0 || localSeat >= player_count || ownPlaying.Length != hands[localSeat] || ownItems.Length != items[localSeat]) return false;
		if (revealedVisible ? (pendingMode != PendingItemSelectionMode.TargetHandCard || pendingOwner != localSeat || revealedPlayer < 0 || revealedPlayer == localSeat || revealedPlaying.Length != hands[revealedPlayer] || revealedItems.Length != items[revealedPlayer]) : (revealedPlayer != -1 || revealedPlaying.Length != 0 || revealedItems.Length != 0)) return false;

		bool pendingValid = pendingMode switch { PendingItemSelectionMode.None => pendingOwner == -1 && pendingGeneration == -1 && string.IsNullOrEmpty(pendingId), PendingItemSelectionMode.HandLimitDiscard => pendingOwner >= 0 && pendingGeneration >= 0 && string.IsNullOrEmpty(pendingId), PendingItemSelectionMode.AttachmentRewardHandCard => pendingOwner >= 0 && pendingGeneration >= 0 && IsValidNetworkAttachmentItemCardId(pendingId), PendingItemSelectionMode.TargetPlayer or PendingItemSelectionMode.TargetHandCard => pendingOwner >= 0 && pendingGeneration >= 0 && IsValidNetworkItemCardId(pendingId, true), _ => false };
		if (!pendingValid) return false;
		snapshot = new ValidatedNetworkBoardSnapshot(snapshotId, generation, currentPlayer, winner, scores, decks, hands, items, ownPlaying, ownItems, revealedVisible, revealedPlaying, revealedItems, pendingMode, pendingOwner, pendingId, pendingGeneration, revealedPlayer, opening, table, seats);
		return true;
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

	private static void ApplyValidatedCountArray<T>(IReadOnlyList<int> values, IReadOnlyList<List<T>> target, Func<T> createPlaceholder)
	{
		for (int index = 0; index < values.Count && index < target.Count; index++)
		{
			target[index].Clear();
			for (int itemIndex = 0; itemIndex < values[index]; itemIndex++) target[index].Add(createPlaceholder());
		}
	}

	private void ApplyPendingSelectionFromSnapshot(ValidatedNetworkBoardSnapshot snapshot)
	{
		_pendingItemSelectionMode = snapshot.PendingSelectionMode;
		_pendingItemOwnerIndex = snapshot.PendingSelectionOwner;
		_pendingItemCardId = snapshot.PendingItemCardId;
		_pendingItemTurnSequenceGeneration = snapshot.PendingTurnSequenceGeneration;
		_revealedPlayerHandIndex = snapshot.RevealedTargetHandVisible ? snapshot.RevealedPlayerHandIndex : -1;
	}

	private void ApplyHandsFromSnapshot(ValidatedNetworkBoardSnapshot snapshot)
	{
		int localSeat = GetLocalHandPlayerIndex();
		for (int player = 0; player < player_count; player++)
		{
			List<PlayingCard> target = _playerHands[player]; target.Clear();
			if (player == localSeat) foreach (NetworkPlayingCardSnapshot card in snapshot.OwnHandCards) target.Add(CreateNetworkPlayingCard(card.CardCode, card.AttachedItemCardIds));
			else if (snapshot.RevealedTargetHandVisible && player == snapshot.RevealedPlayerHandIndex) foreach (NetworkPlayingCardSnapshot card in snapshot.RevealedHandCards) target.Add(CreateNetworkPlayingCard(card.CardCode, card.AttachedItemCardIds));
			else for (int card = 0; card < snapshot.HandCounts[player]; card++) target.Add(CreatePlaceholderPlayingCard());
		}
	}

	private void ApplyItemHandsFromSnapshot(ValidatedNetworkBoardSnapshot snapshot)
	{
		int localSeat = GetLocalHandPlayerIndex();
		for (int player = 0; player < player_count; player++)
		{
			List<ItemCard> target = _playerItemHands[player]; target.Clear();
			if (player == localSeat) foreach (string id in snapshot.OwnItemHandCardIds) target.Add(CreateValidatedNetworkItemCard(id));
			else if (snapshot.RevealedTargetHandVisible && player == snapshot.RevealedPlayerHandIndex) foreach (string id in snapshot.RevealedItemHandCardIds) target.Add(CreateValidatedNetworkItemCard(id));
			else for (int item = 0; item < snapshot.ItemHandCounts[player]; item++) target.Add(CreatePlaceholderItemCard());
		}
	}

	private ItemCard CreateValidatedNetworkItemCard(string itemCardId) => new(itemCardId, _itemCardTextures[itemCardId]);

	private void ApplyTableCardsFromSnapshot(ValidatedNetworkBoardSnapshot snapshot)
	{
		_tableCards.Clear();
		foreach (NetworkTableCardSnapshot tableCard in snapshot.TableCards)
		{
			PlayingCard? card = !string.IsNullOrEmpty(tableCard.CardCode) ? CreateNetworkPlayingCard(tableCard.CardCode, tableCard.AttachedItemCardIds) : !string.IsNullOrEmpty(tableCard.ItemOverlayId) ? null : CreatePlaceholderPlayingCard();
			ItemCard? overlay = string.IsNullOrEmpty(tableCard.ItemOverlayId) ? null : CreateValidatedNetworkItemCard(tableCard.ItemOverlayId);
			_tableCards.Add(new TableCard(card, tableCard.IsFaceUp, overlay, tableCard.ItemOwnerPlayerIndex, tableCard.CardOwnerPlayerIndex, tableCard.RemoveCardOnCapture) { IsBuriedInSand = tableCard.IsBuriedInSand, IsSlimeCoated = tableCard.IsSlimeCoated });
		}
	}

	private void ApplySeatControlStatesFromSnapshot(ValidatedNetworkBoardSnapshot snapshot)
	{
		bool tooltip = _activeStatusTooltipPlayerIndex >= 0;
		_networkSeatControlStates.Clear(); _networkSeatControlStates.AddRange(snapshot.SeatControlStates);
		if (tooltip) MarkActivePlayerStatusTooltipForRefresh();
		MarkSeatControlIndicatorsDirty();
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

}
