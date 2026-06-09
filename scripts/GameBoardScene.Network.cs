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
	private int _networkPendingItemHandIndex = -1;
	private int _networkPendingTurnSequenceGeneration = -1;
	private bool _networkPendingSelectionRequestInFlight;
	private bool _networkAiProxyTurnRunning;

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

	internal void RequestNetworkItemCardPlay(int itemHandIndex, int turnSequenceGeneration)
	{
		if (!IsNetworkMultiplayerMode() || IsNetworkHost())
		{
			ConfirmNetworkHostItemCardPlay(itemHandIndex, turnSequenceGeneration);
			return;
		}

		if (_networkPendingHandIndex >= 0 || _networkPendingItemHandIndex >= 0)
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			return;
		}

		_networkPendingItemHandIndex = itemHandIndex;
		_networkPendingTurnSequenceGeneration = turnSequenceGeneration;
		RenderPlayerHands();
		GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.RequestPlayItemCard(itemHandIndex, turnSequenceGeneration);
	}

	internal void RequestNetworkPendingItemSelection(int targetPlayerIndex, int targetHandIndex, int turnSequenceGeneration)
	{
		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (networkManager == null)
		{
			return;
		}

		if (!NetworkSeatControlRules.CanSendPendingSelectionRequest(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			networkManager.State,
			_networkPendingSelectionRequestInFlight))
		{
			if (_networkPendingSelectionRequestInFlight)
			{
				PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			}
			return;
		}

		_networkPendingSelectionRequestInFlight = true;
		ClearSelectionOverlay();
		networkManager.RequestPendingItemSelection(targetPlayerIndex, targetHandIndex, turnSequenceGeneration);
	}

	internal void RequestNetworkHandCardPlay(int handIndex, int turnSequenceGeneration)
	{
		if (!IsNetworkMultiplayerMode() || IsNetworkHost())
		{
			ConfirmLocalHandCardPlay(handIndex, turnSequenceGeneration, TryGetHandCardStartGlobalPosition(GetLocalHandPlayerIndex(), handIndex));
			return;
		}

		if (_networkPendingHandIndex >= 0 || _networkPendingItemHandIndex >= 0)
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			return;
		}

		_networkPendingHandIndex = handIndex;
		_networkPendingTurnSequenceGeneration = turnSequenceGeneration;
		RenderPlayerHands();
		GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.RequestPlayHandCard(handIndex, turnSequenceGeneration);
	}

	internal void ConfirmNetworkHostItemCardPlay(int itemHandIndex, int turnSequenceGeneration)
	{
		if (!TryStartNetworkItemCardPlayFromHost(
			GetLocalHandPlayerIndex(),
			itemHandIndex,
			turnSequenceGeneration,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			RejectNetworkBoardAction(rejectReason.ToString(), turnSequenceGeneration, itemHandIndex);
		}
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
			&& (_networkPendingItemHandIndex >= 0 || handIndex == _networkPendingHandIndex);
	}

	internal bool IsNetworkItemCardLocked(int playerIndex, int itemHandIndex)
	{
		return IsNetworkMultiplayerMode()
			&& !IsNetworkHost()
			&& playerIndex == GetLocalHandPlayerIndex()
			&& (_networkPendingHandIndex >= 0 || itemHandIndex == _networkPendingItemHandIndex);
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

	internal bool TryStartNetworkItemCardPlayFromHost(
		int actorSeatIndex,
		int itemHandIndex,
		int turnSequenceGeneration,
		out MultiplayerBoardActionRejectReason rejectReason)
	{
		rejectReason = MultiplayerBoardActionRejectReason.None;
		int itemHandCount = actorSeatIndex >= 0 && actorSeatIndex < _playerItemHands.Count
			? _playerItemHands[actorSeatIndex].Count
			: 0;
		MultiplayerBoardActionValidation validation = MultiplayerBoardSyncRules.ValidatePlayHandCardRequest(
			actorSeatIndex,
			_currentTurnPlayerIndex,
			itemHandIndex,
			itemHandCount,
			turnSequenceGeneration,
			_turnSequenceGeneration,
			IsGameOver(),
			_isProcessingCardPlay || _isResolvingTurnSequence || _isOpeningDealAnimationPlaying || HasPendingItemSelection());
		if (!validation.Accepted)
		{
			rejectReason = validation.RejectReason;
			return false;
		}

		if (actorSeatIndex < 0
			|| actorSeatIndex >= _playerItemHands.Count
			|| itemHandIndex < 0
			|| itemHandIndex >= _playerItemHands[actorSeatIndex].Count
			|| !CanPlayItemCard(actorSeatIndex, _playerItemHands[actorSeatIndex][itemHandIndex].Id))
		{
			rejectReason = MultiplayerBoardActionRejectReason.InvalidHandIndex;
			return false;
		}

		_ = PlayNetworkHostItemCardAndBroadcastAsync(actorSeatIndex, itemHandIndex, turnSequenceGeneration);
		return true;
	}

	internal bool TryStartNetworkPendingItemSelectionFromHost(
		int actorSeatIndex,
		int targetPlayerIndex,
		int targetHandIndex,
		int turnSequenceGeneration,
		out MultiplayerBoardActionRejectReason rejectReason)
	{
		rejectReason = MultiplayerBoardActionRejectReason.None;
		if (IsGameOver())
		{
			rejectReason = MultiplayerBoardActionRejectReason.GameOver;
			return false;
		}

		if (!HasPendingItemSelection()
			|| _pendingItemOwnerIndex != actorSeatIndex
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| turnSequenceGeneration != _pendingItemTurnSequenceGeneration)
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard)
		{
			if (!IsValidHandLimitDiscardSelection(actorSeatIndex, targetHandIndex) || _pendingHandLimitDiscardCompletion == null)
			{
				rejectReason = MultiplayerBoardActionRejectReason.InvalidHandIndex;
				return false;
			}

			_pendingHandLimitDiscardCompletion.TrySetResult(targetHandIndex);
			return true;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.MagnetHandCard)
		{
			if (!IsValidMagnetAttachmentHandIndex(actorSeatIndex, targetHandIndex, _pendingItemCardId)
				|| _pendingMagnetAttachmentCompletion == null)
			{
				rejectReason = MultiplayerBoardActionRejectReason.InvalidHandIndex;
				return false;
			}

			_ = CompleteMagnetAttachmentSelectionAsync(targetHandIndex);
			return true;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.TargetPlayer)
		{
			if (!IsItemTargetPlayerCandidate(_pendingItemCardId, _pendingItemOwnerIndex, targetPlayerIndex))
			{
				rejectReason = MultiplayerBoardActionRejectReason.InvalidHandIndex;
				return false;
			}

			if (IsPlayerDeckTargetItemCard(_pendingItemCardId))
			{
				_ = CompletePendingItemSelectionAuthorityAsync(
					_pendingItemCardId,
					_pendingItemOwnerIndex,
					_pendingItemHandIndex,
					targetPlayerIndex,
					-1,
					turnSequenceGeneration);
				return true;
			}

			_pendingItemSelectionMode = PendingItemSelectionMode.TargetHandCard;
			_revealedPlayerHandIndex = targetPlayerIndex;
			RenderPlayerHands();
			SendBoardSnapshotIfHost();
			return true;
		}

		if (_pendingItemSelectionMode != PendingItemSelectionMode.TargetHandCard
			|| targetPlayerIndex != _revealedPlayerHandIndex
			|| !IsPendingItemTargetValid(_pendingItemCardId, _pendingItemOwnerIndex, targetPlayerIndex, targetHandIndex))
		{
			rejectReason = MultiplayerBoardActionRejectReason.InvalidHandIndex;
			return false;
		}

		_ = CompletePendingItemSelectionAuthorityAsync(
			_pendingItemCardId,
			_pendingItemOwnerIndex,
			_pendingItemHandIndex,
			targetPlayerIndex,
			targetHandIndex,
			turnSequenceGeneration);
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
			ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded();
		}
	}

	private async Task PlayNetworkHostItemCardAndBroadcastAsync(int actorSeatIndex, int itemHandIndex, int turnSequenceGeneration)
	{
		if (actorSeatIndex < 0
			|| actorSeatIndex >= _playerItemHands.Count
			|| itemHandIndex < 0
			|| itemHandIndex >= _playerItemHands[actorSeatIndex].Count)
		{
			return;
		}

		int snapshotId = CreateNextNetworkSnapshotId();
		bool played = false;

		_isProcessingCardPlay = true;
		try
		{
			PlayCardResult result = await PlayItemCardFromHandAsync(actorSeatIndex, itemHandIndex, turnSequenceGeneration);
			played = result.Played;
			if (result.Played && !IsGameOver() && result.CompletesTurn && !HasPendingItemSelection())
			{
				await AdvanceTurnAfterPlayedResultAsync(actorSeatIndex, result);
			}
		}
		finally
		{
			if (!IsGameOver() && !HasPendingItemSelection())
			{
				_isProcessingCardPlay = false;
				RenderPlayerHands();
			}

			if (played || HasPendingItemSelection())
			{
				GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.SendBoardSnapshotsFromHost(this, snapshotId);
			}

			ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded();
		}
	}

	internal int CreateNextNetworkSnapshotId()
	{
		_networkSnapshotId++;
		return _networkSnapshotId;
	}

	internal void SendBoardSnapshotIfHost()
	{
		if (!IsNetworkMultiplayerMode() || !IsNetworkHost())
		{
			return;
		}

		GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.SendBoardSnapshotsFromHost(this, CreateNextNetworkSnapshotId());
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
			["seat_control_states"] = BuildSeatControlStatesArray(),
		};
	}

	internal void ApplyNetworkBoardSnapshot(Godot.Collections.Dictionary snapshot)
	{
		if (!IsUsableNetworkSnapshot(snapshot, out int snapshotId))
		{
			return;
		}

		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
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

	internal void QueueNetworkCardPlayedEvent(Godot.Collections.Dictionary payload)
	{
		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (!NetworkReconnectRules.ShouldQueueRealtimeBoardEventForClient(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			networkManager?.State ?? NetworkSessionState.Idle))
		{
			return;
		}

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
		_networkPendingSelectionRequestInFlight = false;

		if (rejectedHandIndex == _networkPendingHandIndex
			&& turnSequenceGeneration == _networkPendingTurnSequenceGeneration)
		{
			_networkPendingHandIndex = -1;
			_networkPendingTurnSequenceGeneration = -1;
		}

		if (rejectedHandIndex == _networkPendingItemHandIndex
			&& turnSequenceGeneration == _networkPendingTurnSequenceGeneration)
		{
			_networkPendingItemHandIndex = -1;
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
			CompleteReconnectCatchupIfReady();
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
		double debugPlaybackDelaySeconds = GetDebugNetworkVisualPlaybackDelaySeconds();
		if (debugPlaybackDelaySeconds > 0.0)
		{
			await ToSignal(GetTree().CreateTimer(debugPlaybackDelaySeconds), SceneTreeTimer.SignalName.Timeout);
		}

		await PlayCardToTableAnimationAsync(actorSeatIndex, playedCard, startGlobalPosition, tableCardIndex, tableCardIsFaceUp: true);
		RenderTableCards();
	}

	private void ApplyNetworkBoardSnapshotNow(Godot.Collections.Dictionary snapshot, int snapshotId)
	{
		bool isReconnectCatchup = IsNetworkMultiplayerMode()
			&& !IsNetworkHost()
			&& GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.State == NetworkSessionState.Reconnecting;
		if (isReconnectCatchup)
		{
			_networkVisualEventQueue.Clear();
			_networkQueuedSnapshot = null;
		}

		_networkLastAppliedSnapshotId = snapshotId;
		_networkBoardInitialized = true;
		_networkPendingHandIndex = -1;
		_networkPendingItemHandIndex = -1;
		_networkPendingTurnSequenceGeneration = -1;
		_networkPendingSelectionRequestInFlight = false;
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
		RefreshNetworkPendingSelectionOverlay();
		RefreshAllPlayerStatusTooltips();
		if (isReconnectCatchup)
		{
			_networkReconnectCatchupPendingCompletion = true;
		}

		CompleteReconnectCatchupIfReady();
		if (_winnerPlayerIndex >= 0)
		{
			ShowWinnerSequence(_winnerPlayerIndex);
		}
	}

	private void CompleteReconnectCatchupIfReady()
	{
		if (!_networkReconnectCatchupPendingCompletion)
		{
			return;
		}

		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
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
		UpdateReconnectOverlay();
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

			string cardCode = ReadNetworkString(tableCard, "code", string.Empty);
			bool isFaceUp = ReadNetworkBool(tableCard, "is_face_up", false);
			PlayingCard? card = string.IsNullOrWhiteSpace(cardCode) && isFaceUp
				? null
				: CreateNetworkPlayingCard(tableCard);
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
				["attached_item_card_ids"] = canSeeCard ? BuildStringArray(tableCard.Card!.Value.GetAttachedItemCardIds()) : new Godot.Collections.Array(),
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
		return GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.GetSeatControlStatesVariant(includeReconnectTokens: false)
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
	}

	internal bool IsNetworkMatchClaimable()
	{
		return !IsGameOver();
	}

	private bool IsLocalSeatHumanConnected()
	{
		if (!IsNetworkMultiplayerMode())
		{
			return true;
		}

		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		NetworkSessionState networkState = networkManager?.State ?? NetworkSessionState.Idle;
		int localSeatIndex = GetLocalHandPlayerIndex();
		bool seatStateFound = TryGetSeatControlStateForUi(localSeatIndex, out SeatControlState state);

		return LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: true,
			networkState,
			seatStateFound,
			state.Mode);
	}

	private void PromoteReclaimedSeatAtCurrentTurnStartIfNeeded()
	{
		if (!IsNetworkMultiplayerMode() || !IsNetworkHost())
		{
			return;
		}

		if (GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.TryPromoteReclaimedSeatAtTurnStart(_currentTurnPlayerIndex) == true)
		{
			GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.SendBoardSnapshotsFromHost(this, CreateNextNetworkSnapshotId());
		}
	}

	internal void BeginNetworkAiProxyTurnIfNeeded()
	{
		if (!ShouldRunNetworkAiProxyTurn())
		{
			return;
		}

		_ = ResolveNetworkAiProxyTurnsAsync(_turnSequenceGeneration);
	}

	private void ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded()
	{
		if (!NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			IsGameOver(),
			_isProcessingCardPlay))
		{
			return;
		}

		BeginNetworkAiProxyTurnIfNeeded();
	}

	private void BeginNetworkAiProxyTurnForPendingSelectionIfNeeded()
	{
		if (!NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			ShouldResolvePendingNetworkAiProxySelection()))
		{
			return;
		}

		BeginNetworkAiProxyTurnIfNeeded();
	}

	private bool ShouldRunNetworkAiProxyTurn()
	{
		if (!IsNetworkMultiplayerMode()
			|| !IsNetworkHost()
			|| _networkAiProxyTurnRunning
			|| IsGameOver()
			|| _isOpeningDealAnimationPlaying)
		{
			return false;
		}

		if (ShouldResolvePendingNetworkAiProxySelection())
		{
			return true;
		}

		if (_isProcessingCardPlay)
		{
			return false;
		}

		if (HasPendingItemSelection())
		{
			return false;
		}

		return GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.IsSeatAiControlled(_currentTurnPlayerIndex) ?? false;
	}

	private async Task ResolveNetworkAiProxyTurnsAsync(int turnSequenceGeneration)
	{
		if (_networkAiProxyTurnRunning)
		{
			return;
		}

		_networkAiProxyTurnRunning = true;
		bool wasResolvingTurnSequence = _isResolvingTurnSequence;
		_isResolvingTurnSequence = true;
		try
		{
			while (turnSequenceGeneration == _turnSequenceGeneration
				&& !IsGameOver()
				&& (ShouldResolvePendingNetworkAiProxySelection()
					|| (GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.IsSeatAiControlled(_currentTurnPlayerIndex) ?? false)))
			{
				int failedSeatIndex = ShouldResolvePendingNetworkAiProxySelection()
					? _pendingItemOwnerIndex
					: _currentTurnPlayerIndex;
				try
				{
					if (await TryResolvePendingNetworkAiProxySelectionAsync(turnSequenceGeneration))
					{
						continue;
					}

					int playerIndex = _currentTurnPlayerIndex;
					if (playerIndex < 0 || playerIndex >= player_count)
					{
						return;
					}

					await RefillPlayerBaseHandAsync(playerIndex, turnSequenceGeneration);
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return;
					}

					if (!PlayerHasPlayableHandCards(playerIndex))
					{
						SetCurrentTurnPlayerIndex(GetNextTurnPlayerIndex(playerIndex));
						PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
						await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
						continue;
					}

					await ToSignal(GetTree().CreateTimer(AiTurnDelaySeconds), SceneTreeTimer.SignalName.Timeout);
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return;
					}

					PlayCardResult result = await PlayNetworkAiProxyHandEntryAsync(playerIndex, turnSequenceGeneration);
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return;
					}

					if (!result.Played)
					{
						SetCurrentTurnPlayerIndex(GetNextTurnPlayerIndex(playerIndex));
						PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
						await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
						continue;
					}

					if (result.CompletesTurn)
					{
						await AdvanceTurnAfterPlayedResultAsync(playerIndex, result);
					}
				}
				catch (Exception ex)
				{
					GD.PushWarning($"AI proxy turn loop failed for seat {failedSeatIndex}: {ex.Message}");
					if (!await RecoverNetworkAiProxyTurnAfterFailureAsync(failedSeatIndex, turnSequenceGeneration))
					{
						return;
					}
				}
			}
		}
		finally
		{
			_networkAiProxyTurnRunning = false;
			_isResolvingTurnSequence = wasResolvingTurnSequence;
			RenderPlayerHands();
			if (IsNetworkHost())
			{
				GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.SendBoardSnapshotsFromHost(this, CreateNextNetworkSnapshotId());
			}
		}
	}

	private async Task<bool> RecoverNetworkAiProxyTurnAfterFailureAsync(int failedSeatIndex, int turnSequenceGeneration)
	{
		if (!NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			IsGameOver()))
		{
			return false;
		}

		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return true;
		}

		if (ShouldResolvePendingNetworkAiProxySelection())
		{
			int ownerSeatIndex = _pendingItemOwnerIndex;
			CancelPendingNetworkAiProxySelectionForFailedItem(ownerSeatIndex);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			return true;
		}

		_isProcessingCardPlay = false;
		if (failedSeatIndex < 0 || failedSeatIndex >= player_count)
		{
			return false;
		}

		SetCurrentTurnPlayerIndex(GetNextTurnPlayerIndex(failedSeatIndex));
		PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
		try
		{
			await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
		}
		catch (Exception recoveryEx)
		{
			GD.PushWarning($"AI proxy turn recovery refill failed after seat {failedSeatIndex}: {recoveryEx.Message}");
		}

		return true;
	}

	private bool ShouldResolvePendingNetworkAiProxySelection()
	{
		if (!HasPendingItemSelection()
			|| _pendingItemTurnSequenceGeneration != _turnSequenceGeneration
			|| _pendingItemOwnerIndex < 0
			|| _pendingItemOwnerIndex >= player_count)
		{
			return false;
		}

		if (_pendingItemSelectionMode is not (
			PendingItemSelectionMode.TargetPlayer
			or PendingItemSelectionMode.TargetHandCard
			or PendingItemSelectionMode.HandLimitDiscard
			or PendingItemSelectionMode.MagnetHandCard))
		{
			return false;
		}

		return GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.IsSeatAiControlled(_pendingItemOwnerIndex) ?? false;
	}

	private async Task<bool> TryResolvePendingNetworkAiProxySelectionAsync(int turnSequenceGeneration)
	{
		if (!ShouldResolvePendingNetworkAiProxySelection())
		{
			return false;
		}

		while (turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver()
			&& ShouldResolvePendingNetworkAiProxySelection())
		{
			int ownerSeatIndex = _pendingItemOwnerIndex;
			try
			{
				int targetPlayerIndex;
				int targetHandIndex = -1;
				switch (_pendingItemSelectionMode)
				{
						case PendingItemSelectionMode.TargetPlayer:
							targetPlayerIndex = PickPendingItemTargetPlayerForNetworkAiProxy();
							if (targetPlayerIndex < 0)
							{
								GD.PushWarning($"AI proxy could not find a legal target player for pending item '{_pendingItemCardId}'.");
								await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
								return true;
							}
							break;
						case PendingItemSelectionMode.TargetHandCard:
							targetPlayerIndex = _revealedPlayerHandIndex;
							targetHandIndex = PickPendingItemTargetHandIndexForNetworkAiProxy(targetPlayerIndex);
							if (targetHandIndex < 0)
							{
								GD.PushWarning($"AI proxy could not find a legal target hand index for pending item '{_pendingItemCardId}'.");
								await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
								return true;
							}
							break;
						case PendingItemSelectionMode.HandLimitDiscard:
							targetPlayerIndex = -1;
							targetHandIndex = PickPendingHandLimitDiscardIndexForNetworkAiProxy(ownerSeatIndex);
							if (targetHandIndex < 0)
							{
								GD.PushWarning($"AI proxy could not find a legal discard index for seat {ownerSeatIndex}.");
								await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
								return true;
							}
							break;
					case PendingItemSelectionMode.MagnetHandCard:
						targetPlayerIndex = -1;
						targetHandIndex = PickRandomAttachableMagnetHandIndex(ownerSeatIndex, _pendingItemCardId);
						if (targetHandIndex < 0)
						{
							await CompleteMagnetAttachmentSelectionAsync(-1);
							await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
							continue;
						}
						break;
					default:
						return false;
				}

					if (!TryStartNetworkPendingItemSelectionFromHost(
						ownerSeatIndex,
						targetPlayerIndex,
						targetHandIndex,
						turnSequenceGeneration,
						out MultiplayerBoardActionRejectReason rejectReason))
					{
						GD.PushWarning($"AI proxy pending item selection rejected: {rejectReason}.");
						await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
						return true;
					}

				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			catch (Exception ex)
			{
				GD.PushWarning($"AI proxy pending item selection failed for seat {ownerSeatIndex}: {ex.Message}");
				await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
				return true;
			}
		}

		while (turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver()
			&& (_isProcessingCardPlay || HasPendingItemSelection()))
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		return true;
	}

	private async Task ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(int ownerSeatIndex, int turnSequenceGeneration)
	{
		if (!HasPendingItemSelection()
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| ownerSeatIndex != _pendingItemOwnerIndex)
		{
			return;
		}

		try
		{
			int targetPlayerIndex;
			int targetHandIndex;
			switch (_pendingItemSelectionMode)
			{
				case PendingItemSelectionMode.TargetPlayer:
					targetPlayerIndex = PickRandomLegalPendingItemTargetPlayer(ownerSeatIndex);
					if (targetPlayerIndex >= 0
						&& TryStartNetworkPendingItemSelectionFromHost(
							ownerSeatIndex,
							targetPlayerIndex,
							-1,
							turnSequenceGeneration,
							out _))
					{
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						return;
					}
					break;
				case PendingItemSelectionMode.TargetHandCard:
					targetPlayerIndex = _revealedPlayerHandIndex;
					targetHandIndex = PickRandomLegalPendingItemTargetHandIndex(ownerSeatIndex, targetPlayerIndex);
					if (targetHandIndex >= 0
						&& TryStartNetworkPendingItemSelectionFromHost(
							ownerSeatIndex,
							targetPlayerIndex,
							targetHandIndex,
							turnSequenceGeneration,
							out _))
					{
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						return;
					}
					break;
				case PendingItemSelectionMode.HandLimitDiscard:
					targetHandIndex = PickPendingHandLimitDiscardIndexForNetworkAiProxy(ownerSeatIndex);
					if (targetHandIndex >= 0
						&& TryStartNetworkPendingItemSelectionFromHost(
							ownerSeatIndex,
							-1,
							targetHandIndex,
							turnSequenceGeneration,
							out _))
					{
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						return;
					}
					break;
				case PendingItemSelectionMode.MagnetHandCard:
					targetHandIndex = PickRandomAttachableMagnetHandIndex(ownerSeatIndex, _pendingItemCardId);
					if (targetHandIndex >= 0
						&& TryStartNetworkPendingItemSelectionFromHost(
							ownerSeatIndex,
							-1,
							targetHandIndex,
							turnSequenceGeneration,
							out _))
					{
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						return;
					}

					await CompleteMagnetAttachmentSelectionAsync(-1);
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					return;
			}
		}
		catch (Exception fallbackEx)
		{
			GD.PushWarning($"AI proxy pending item fallback failed for seat {ownerSeatIndex}: {fallbackEx.Message}");
		}

		CancelPendingNetworkAiProxySelectionForFailedItem(ownerSeatIndex);
	}

	private void RememberBlockedPendingNetworkAiItem(int ownerSeatIndex)
	{
		if (string.IsNullOrWhiteSpace(_pendingItemCardId))
		{
			return;
		}

		_networkAiBlockedItem = new NetworkAiBlockedItemState(
			ownerSeatIndex,
			_pendingItemCardId);
	}

	private void CancelPendingNetworkAiProxySelectionForFailedItem(int ownerSeatIndex)
	{
		RememberBlockedPendingNetworkAiItem(ownerSeatIndex);
		CancelPendingItemSelection();
	}

	private void CancelPendingItemSelectionForAuthorityFailure(int ownerSeatIndex)
	{
		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& ownerSeatIndex >= 0
			&& (GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.IsSeatAiControlled(ownerSeatIndex) ?? false))
		{
			CancelPendingNetworkAiProxySelectionForFailedItem(ownerSeatIndex);
			return;
		}

		CancelPendingItemSelection();
	}

	private async Task<PlayCardResult> PlayNetworkAiProxyHandEntryAsync(int playerIndex, int turnSequenceGeneration)
	{
		try
		{
			if (playerIndex >= 0
				&& playerIndex < _playerHands.Count
				&& NetworkAiFallbackRules.TryPickCaptureHandIndexForNetworkAiProxy(
					ToCaptureScoringTableCards(),
					ToPotentialCaptureCards(playerIndex, _playerHands[playerIndex]),
					BuildCaptureScoringContext(playerIndex),
					_rng.RandiRange(0, 99),
					_rng.RandiRange(0, 9999),
					out int captureHandIndex))
			{
				return await PlayCardFromHandAsync(playerIndex, captureHandIndex, turnSequenceGeneration);
			}

			return await PlayRandomPlayableHandEntryWithoutCapturePriorityAsync(playerIndex, turnSequenceGeneration);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"AI proxy turn heuristic failed for seat {playerIndex}: {ex.Message}");
		}

		try
		{
			return await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"AI proxy fallback action failed for seat {playerIndex}: {ex.Message}");
			return new PlayCardResult(false, false);
		}
	}

	private int PickPendingItemTargetPlayerForNetworkAiProxy()
	{
		try
		{
			if (ConsumeDebugForceNextPendingTargetPlayerHeuristicFailure())
			{
				throw new InvalidOperationException("debug forced target-player heuristic failure");
			}

			int targetPlayerIndex = PickItemTargetPlayer(_pendingItemCardId, _pendingItemOwnerIndex);
			if (IsItemTargetPlayerCandidate(_pendingItemCardId, _pendingItemOwnerIndex, targetPlayerIndex))
			{
				return targetPlayerIndex;
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning($"AI proxy target-player heuristic failed for pending item '{_pendingItemCardId}': {ex.Message}");
		}

		return PickRandomLegalPendingItemTargetPlayer(_pendingItemOwnerIndex);
	}

	private int PickPendingItemTargetHandIndexForNetworkAiProxy(int targetPlayerIndex)
	{
		try
		{
			if (ConsumeDebugForceNextPendingTargetHandHeuristicFailure())
			{
				throw new InvalidOperationException("debug forced target-hand heuristic failure");
			}

			int targetHandIndex = PickTargetHandIndex(targetPlayerIndex);
			if (IsPendingItemTargetValid(_pendingItemCardId, _pendingItemOwnerIndex, targetPlayerIndex, targetHandIndex))
			{
				return targetHandIndex;
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning($"AI proxy target-hand heuristic failed for pending item '{_pendingItemCardId}': {ex.Message}");
		}

		return PickRandomLegalPendingItemTargetHandIndex(_pendingItemOwnerIndex, targetPlayerIndex);
	}

	private int PickPendingHandLimitDiscardIndexForNetworkAiProxy(int ownerSeatIndex)
	{
		if (ownerSeatIndex < 0
			|| ownerSeatIndex >= _playerHands.Count
			|| ownerSeatIndex >= _playerItemHands.Count)
		{
			return -1;
		}

		try
		{
			if (ConsumeDebugForceNextPendingHandLimitDiscardHeuristicFailure())
			{
				throw new InvalidOperationException("debug forced hand-limit discard heuristic failure");
			}

			int targetHandIndex = PickRandomHandDisplayIndex(ownerSeatIndex);
			if (IsValidHandLimitDiscardSelection(ownerSeatIndex, targetHandIndex))
			{
				return targetHandIndex;
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning($"AI proxy discard heuristic failed for seat {ownerSeatIndex}: {ex.Message}");
		}

		int totalHandCardCount = GetTotalHandCardCount(ownerSeatIndex);
		return NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalHandCardCount,
			displayIndex => IsValidHandLimitDiscardSelection(ownerSeatIndex, displayIndex),
			_rng.RandiRange(0, int.MaxValue));
	}

	private int PickRandomLegalPendingItemTargetPlayer(int ownerSeatIndex)
	{
		return NetworkAiFallbackRules.PickRandomLegalCandidate(
			player_count,
			targetPlayerIndex => IsItemTargetPlayerCandidate(_pendingItemCardId, ownerSeatIndex, targetPlayerIndex),
			_rng.RandiRange(0, int.MaxValue));
	}

	private int PickRandomLegalPendingItemTargetHandIndex(int ownerSeatIndex, int targetPlayerIndex)
	{
		int totalHandCardCount = GetTotalHandCardCount(targetPlayerIndex);
		return NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalHandCardCount,
			handIndex => IsPendingItemTargetValid(_pendingItemCardId, ownerSeatIndex, targetPlayerIndex, handIndex),
			_rng.RandiRange(0, int.MaxValue));
	}

	private static TEnum ReadEnum<TEnum>(Godot.Collections.Dictionary dictionary, string key, TEnum fallback)
		where TEnum : struct, Enum
	{
		return dictionary.TryGetValue(key, out Variant value)
			&& Enum.TryParse(value.AsString(), ignoreCase: true, out TEnum parsed)
			? parsed
			: fallback;
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

	private PlayingCard CreateNetworkPlayingCard(Godot.Collections.Dictionary cardDictionary)
	{
		PlayingCard card = CreateNetworkPlayingCard(ReadNetworkString(cardDictionary, "code", string.Empty));
		foreach (Variant itemVariant in ReadArray(cardDictionary, "attached_item_card_ids"))
		{
			string itemCardId = itemVariant.AsString();
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
