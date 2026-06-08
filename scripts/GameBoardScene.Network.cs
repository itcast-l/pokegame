using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private readonly Dictionary<string, PlayingCard> _networkCardsByCode = new(StringComparer.Ordinal);
	private readonly Queue<Godot.Collections.Dictionary> _networkVisualEventQueue = new();
	private Godot.Collections.Dictionary? _networkQueuedSnapshot;
	private bool _networkVisualQueueProcessing;
	private int _networkSnapshotId;
	private int _networkLastAppliedSnapshotId;
	private int _networkPendingHandIndex = -1;
	private int _networkPendingTurnSequenceGeneration = -1;

	internal IPlayerInputHandler CreatePlayerInputHandler()
	{
		if (!IsNetworkMultiplayerMode())
		{
			return new LocalPlayerInputHandler(this);
		}

		return IsNetworkHost()
			? new NetworkHostInputHandler(this)
			: new NetworkPlayerInputHandler(this);
	}

	internal void ConfirmLocalHandCardPlay(int handIndex, int turnSequenceGeneration, Vector2? startGlobalPosition)
	{
		_ = PlayMainPlayerCardAndAdvanceAsync(handIndex, turnSequenceGeneration, startGlobalPosition);
	}

	internal void RequestNetworkHandCardPlay(int handIndex, int turnSequenceGeneration)
	{
		if (!IsNetworkMultiplayerMode() || IsNetworkHost())
		{
			ConfirmLocalHandCardPlay(handIndex, turnSequenceGeneration, TryGetHandCardStartGlobalPosition(GetLocalHandPlayerIndex(), handIndex));
			return;
		}

		if (_networkPendingHandIndex >= 0)
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			return;
		}

		_networkPendingHandIndex = handIndex;
		_networkPendingTurnSequenceGeneration = turnSequenceGeneration;
		RenderPlayerHands();
		GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.RequestPlayHandCard(handIndex, turnSequenceGeneration);
	}

	internal void ConfirmNetworkHostHandCardPlay(int handIndex, int turnSequenceGeneration)
	{
		if (!TryStartNetworkHandCardPlayFromHost(
			GetLocalHandPlayerIndex(),
			handIndex,
			turnSequenceGeneration,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			RejectNetworkBoardAction(rejectReason.ToString(), turnSequenceGeneration, handIndex);
		}
	}

	internal bool IsNetworkHandCardLocked(int playerIndex, int handIndex)
	{
		return IsNetworkMultiplayerMode()
			&& !IsNetworkHost()
			&& playerIndex == GetLocalHandPlayerIndex()
			&& handIndex == _networkPendingHandIndex;
	}

	internal bool TryStartNetworkHandCardPlayFromHost(
		int actorSeatIndex,
		int handIndex,
		int turnSequenceGeneration,
		out MultiplayerBoardActionRejectReason rejectReason)
	{
		rejectReason = MultiplayerBoardActionRejectReason.None;
		int handCount = actorSeatIndex >= 0 && actorSeatIndex < _playerHands.Count
			? _playerHands[actorSeatIndex].Count
			: 0;
		MultiplayerBoardActionValidation validation = MultiplayerBoardSyncRules.ValidatePlayHandCardRequest(
			actorSeatIndex,
			_currentTurnPlayerIndex,
			handIndex,
			handCount,
			turnSequenceGeneration,
			_turnSequenceGeneration,
			IsGameOver(),
			_isProcessingCardPlay || _isResolvingTurnSequence || _isOpeningDealAnimationPlaying || HasPendingItemSelection());

		if (!validation.Accepted)
		{
			rejectReason = validation.RejectReason;
			return false;
		}

		_ = PlayNetworkHostHandCardAndBroadcastAsync(actorSeatIndex, handIndex, turnSequenceGeneration);
		return true;
	}

	private async Task PlayNetworkHostHandCardAndBroadcastAsync(int actorSeatIndex, int handIndex, int turnSequenceGeneration)
	{
		if (actorSeatIndex < 0 || actorSeatIndex >= _playerHands.Count || handIndex < 0 || handIndex >= _playerHands[actorSeatIndex].Count)
		{
			return;
		}

		int snapshotId = CreateNextNetworkSnapshotId();
		PlayingCard playedCard = _playerHands[actorSeatIndex][handIndex];
		string playedCardCode = ToNetworkCardCode(playedCard);
		bool played = false;

		_isProcessingCardPlay = true;
		try
		{
			Vector2? startGlobalPosition = TryGetHandCardStartGlobalPosition(actorSeatIndex, handIndex);
			PlayCardResult result = await PlayCardFromHandAsync(actorSeatIndex, handIndex, turnSequenceGeneration, startGlobalPosition);
			played = result.Played;
			if (result.Played && !IsGameOver() && result.CompletesTurn)
			{
				await AdvanceTurnAfterPlayedResultAsync(actorSeatIndex, result);
			}
		}
		finally
		{
			if (!IsGameOver())
			{
				_isProcessingCardPlay = false;
				RenderPlayerHands();
			}

			if (played)
			{
				GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.BroadcastCardPlayedEventFromHost(
					actorSeatIndex,
					handIndex,
					playedCardCode,
					turnSequenceGeneration,
					snapshotId);
			}

			GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.SendBoardSnapshotsFromHost(this, snapshotId);
		}
	}

	internal int CreateNextNetworkSnapshotId()
	{
		_networkSnapshotId++;
		return _networkSnapshotId;
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
			["table_cards"] = BuildTableCardsArray(recipientSeatIndex),
			["own_hand_cards"] = BuildOwnHandCardsArray(recipientSeatIndex),
		};
	}

	internal void ApplyNetworkBoardSnapshot(Godot.Collections.Dictionary snapshot)
	{
		if (!IsUsableNetworkSnapshot(snapshot, out int snapshotId))
		{
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

	internal void QueueNetworkCardPlayedEvent(Godot.Collections.Dictionary payload)
	{
		int snapshotId = ReadNetworkInt(payload, "snapshot_id", -1);
		if (snapshotId >= 0 && snapshotId <= _networkLastAppliedSnapshotId)
		{
			return;
		}

		_networkVisualEventQueue.Enqueue(payload);
		if (!_networkVisualQueueProcessing)
		{
			_ = ProcessNetworkVisualQueueAsync();
		}
	}

	internal void RejectNetworkBoardAction(string reason, int turnSequenceGeneration, int rejectedHandIndex)
	{
		if (rejectedHandIndex == _networkPendingHandIndex
			&& turnSequenceGeneration == _networkPendingTurnSequenceGeneration)
		{
			_networkPendingHandIndex = -1;
			_networkPendingTurnSequenceGeneration = -1;
		}

		PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
		RenderPlayerHands();
	}

	private async Task ProcessNetworkVisualQueueAsync()
	{
		_networkVisualQueueProcessing = true;
		try
		{
			while (_networkVisualEventQueue.Count > 0)
			{
				Godot.Collections.Dictionary payload = _networkVisualEventQueue.Dequeue();
				int snapshotId = ReadNetworkInt(payload, "snapshot_id", -1);
				if (snapshotId >= 0 && snapshotId <= _networkLastAppliedSnapshotId)
				{
					continue;
				}

				if (_networkQueuedSnapshot != null
					&& ReadNetworkInt(_networkQueuedSnapshot, "snapshot_id", -1) > snapshotId)
				{
					continue;
				}

				await PlayNetworkCardPlayedEventAsync(payload);
			}
		}
		finally
		{
			_networkVisualQueueProcessing = false;
			if (_networkQueuedSnapshot != null)
			{
				Godot.Collections.Dictionary snapshot = _networkQueuedSnapshot;
				_networkQueuedSnapshot = null;
				if (IsUsableNetworkSnapshot(snapshot, out int snapshotId))
				{
					ApplyNetworkBoardSnapshotNow(snapshot, snapshotId);
				}
			}
		}
	}

	private async Task PlayNetworkCardPlayedEventAsync(Godot.Collections.Dictionary payload)
	{
		int actorSeatIndex = ReadNetworkInt(payload, "actor_seat_index", -1);
		int handIndex = ReadNetworkInt(payload, "hand_index", -1);
		string playedCardCode = ReadNetworkString(payload, "played_card_code", string.Empty);
		if (actorSeatIndex < 0 || actorSeatIndex >= player_count)
		{
			return;
		}

		PlayingCard playedCard = CreateNetworkPlayingCard(playedCardCode);
		int tableCardIndex = _tableCards.Count;
		_tableCards.Add(new TableCard(playedCard, isFaceUp: true, cardOwnerPlayerIndex: actorSeatIndex));
		if (actorSeatIndex == GetLocalHandPlayerIndex()
			&& handIndex == _networkPendingHandIndex)
		{
			HideHandDisplaySlot(actorSeatIndex, handIndex);
		}

		Vector2 startGlobalPosition = TryGetHandCardStartGlobalPosition(actorSeatIndex, handIndex)
			?? GetVisibleCardPileTopGlobalPosition(actorSeatIndex);
		await PlayCardToTableAnimationAsync(actorSeatIndex, playedCard, startGlobalPosition, tableCardIndex, tableCardIsFaceUp: true);
		RenderTableCards();
	}

	private void ApplyNetworkBoardSnapshotNow(Godot.Collections.Dictionary snapshot, int snapshotId)
	{
		_networkLastAppliedSnapshotId = snapshotId;
		_networkBoardInitialized = true;
		_networkPendingHandIndex = -1;
		_networkPendingTurnSequenceGeneration = -1;
		_hiddenHandDisplaySlots.Clear();
		_shiftedHiddenHandDisplaySlots.Clear();

		EnsureNetworkStateLists();
		_turnSequenceGeneration = ReadNetworkInt(snapshot, "turn_sequence_generation", _turnSequenceGeneration);
		_turnStateMachine.SyncGeneration(_turnSequenceGeneration);
		_currentTurnPlayerIndex = ReadNetworkInt(snapshot, "current_turn_player_index", _currentTurnPlayerIndex);
		_winnerPlayerIndex = ReadNetworkInt(snapshot, "winner_player_index", _winnerPlayerIndex);
		ApplyIntArray(snapshot, "scores", _playerScores, player_count);
		ApplyCountArray(snapshot, "deck_counts", _playerDecks, player_count, CreatePlaceholderPlayingCard);
		ApplyHandsFromSnapshot(snapshot);
		ApplyItemHandCounts(snapshot);
		ApplyTableCardsFromSnapshot(snapshot);
		_isOpeningDealAnimationPlaying = false;
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
		if (_winnerPlayerIndex >= 0)
		{
			ShowWinnerSequence(_winnerPlayerIndex);
		}
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
						_playerHands[playerIndex].Add(CreateNetworkPlayingCard(ReadNetworkString(card, "code", string.Empty)));
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

	private void ApplyItemHandCounts(Godot.Collections.Dictionary snapshot)
	{
		int[] itemHandCounts = ReadIntArray(snapshot, "item_hand_counts", player_count);
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			_playerItemHands[playerIndex].Clear();
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

			string cardCode = ReadNetworkString(tableCard, "code", string.Empty);
			bool isFaceUp = ReadNetworkBool(tableCard, "is_face_up", false);
			PlayingCard? card = string.IsNullOrWhiteSpace(cardCode) && isFaceUp
				? null
				: CreateNetworkPlayingCard(cardCode);
			var appliedCard = new TableCard(
				card,
				isFaceUp,
				cardOwnerPlayerIndex: ReadNetworkInt(tableCard, "card_owner_player_index", -1),
				removeCardOnCapture: ReadNetworkBool(tableCard, "remove_card_on_capture", false))
			{
				IsBuriedInSand = ReadNetworkBool(tableCard, "is_buried_in_sand", false),
				IsSlimeCoated = ReadNetworkBool(tableCard, "is_slime_coated", false),
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
			result.Add(new Godot.Collections.Dictionary
			{
				["code"] = canSeeCard ? ToNetworkCardCode(tableCard.Card!.Value) : string.Empty,
				["is_face_up"] = tableCard.IsFaceUp,
				["is_buried_in_sand"] = tableCard.IsBuriedInSand,
				["is_slime_coated"] = tableCard.IsSlimeCoated,
				["card_owner_player_index"] = tableCard.CardOwnerPlayerIndex,
				["remove_card_on_capture"] = tableCard.RemoveCardOnCapture,
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
			result.Add(new Godot.Collections.Dictionary
			{
				["code"] = ToNetworkCardCode(card),
			});
		}

		return result;
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

	private bool IsNetworkMultiplayerMode()
	{
		return !string.IsNullOrWhiteSpace(_networkSessionId);
	}

	private bool IsNetworkHost()
	{
		return GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.IsHost ?? false;
	}

	private int GetLocalHandPlayerIndex()
	{
		return IsNetworkMultiplayerMode() ? Mathf.Clamp(_localPlayerIndex, 0, MaxPlayers - 1) : MainPlayerIndex;
	}

	private bool IsLocalHandPlayer(int playerIndex)
	{
		return playerIndex == GetLocalHandPlayerIndex();
	}

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

	private PlayingCard CreatePlaceholderPlayingCard()
	{
		return new PlayingCard(string.Empty, string.Empty, GetNetworkPlaceholderTexture());
	}

	private ItemCard CreatePlaceholderItemCard()
	{
		return new ItemCard(string.Empty, GetNetworkPlaceholderTexture());
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
