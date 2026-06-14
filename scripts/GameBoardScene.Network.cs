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
	private int _networkAuthoritativeVisualSnapshotId = -1;
	private int _networkLastAppliedSnapshotId;
	private int _networkPendingHandIndex = -1;
	private int _networkPendingItemHandIndex = -1;
	private int _networkPendingTurnSequenceGeneration = -1;
	private bool _networkPendingSelectionRequestInFlight;
	private bool _networkAiProxyTurnRunning;
	private bool _networkPredictedHandVisualActive;
	private int _networkPredictedHandTableCardIndex = -1;
	private int _networkPredictedHandMatchedTableCardIndex = -1;
	private bool _networkPredictedHandMatchedTableCardFlipped;
	private bool _networkPredictedItemVisualActive;
	private int _networkPredictedItemDisplayIndex = -1;
	private string _networkPredictedItemCardId = string.Empty;
	private string _networkPredictedItemPlayPresentation = string.Empty;
	private int _networkPredictedItemTargetTableCardIndex = -1;
	private bool _networkLocallyAdvancedPendingTargetSelection;
	private PendingItemSelectionMode _networkLocallyCommittedPendingSelectionMode = PendingItemSelectionMode.None;
	private int _networkLocallyCommittedPendingSelectionDisplayIndex = -1;
	private int _networkLocallyCommittedPendingSelectionHandIndex = -1;
	private string _networkLocallyCommittedPendingSelectionItemCardId = string.Empty;
	private PlayingCard _networkLocallyCommittedPendingSelectionOriginalCard;

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

	internal async void RequestNetworkItemCardPlay(int itemHandIndex, int turnSequenceGeneration)
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
		if (!await BeginPredictedNetworkItemCardPlayAsync(itemHandIndex, turnSequenceGeneration))
		{
			ClearNetworkPendingActionState();
			ResetNetworkPredictedItemVisualState();
			RenderPlayerHands();
			RenderTableCards();
			return;
		}

		GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.RequestPlayItemCard(itemHandIndex, turnSequenceGeneration);
	}

	internal void RequestNetworkPendingItemSelection(int targetPlayerIndex, int targetHandIndex, int turnSequenceGeneration)
	{
		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (networkManager == null)
		{
			return;
		}

		bool allowImmediateFollowupSelection = _networkLocallyAdvancedPendingTargetSelection
			&& _pendingItemSelectionMode == PendingItemSelectionMode.TargetHandCard
			&& targetHandIndex >= 0;
		if (!allowImmediateFollowupSelection && !NetworkSeatControlRules.CanSendPendingSelectionRequest(
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

	internal async void RequestNetworkHandCardPlay(int handIndex, int turnSequenceGeneration)
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
		if (!await BeginPredictedNetworkHandCardPlayAsync(handIndex, turnSequenceGeneration))
		{
			ClearNetworkPendingActionState();
			ResetNetworkPredictedHandVisualState();
			RenderPlayerHands();
			RenderTableCards();
			return;
		}

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

	private void ResetNetworkPredictedHandVisualState()
	{
		_networkPredictedHandVisualActive = false;
		_networkPredictedHandTableCardIndex = -1;
		_networkPredictedHandMatchedTableCardIndex = -1;
		_networkPredictedHandMatchedTableCardFlipped = false;
	}

	private void ResetNetworkPredictedItemVisualState()
	{
		_networkPredictedItemVisualActive = false;
		_networkPredictedItemDisplayIndex = -1;
		_networkPredictedItemCardId = string.Empty;
		_networkPredictedItemPlayPresentation = string.Empty;
		_networkPredictedItemTargetTableCardIndex = -1;
	}

	private void ResetNetworkLocallyCommittedPendingSelectionState()
	{
		_networkLocallyAdvancedPendingTargetSelection = false;
		_networkLocallyCommittedPendingSelectionMode = PendingItemSelectionMode.None;
		_networkLocallyCommittedPendingSelectionDisplayIndex = -1;
		_networkLocallyCommittedPendingSelectionHandIndex = -1;
		_networkLocallyCommittedPendingSelectionItemCardId = string.Empty;
		_networkLocallyCommittedPendingSelectionOriginalCard = default;
	}

	private void ResetNetworkPredictedClientState()
	{
		ResetNetworkPredictedHandVisualState();
		ResetNetworkPredictedItemVisualState();
		ResetNetworkLocallyCommittedPendingSelectionState();
	}

	private void ClearNetworkPendingActionState()
	{
		_networkPendingHandIndex = -1;
		_networkPendingItemHandIndex = -1;
		_networkPendingTurnSequenceGeneration = -1;
		_networkPendingSelectionRequestInFlight = false;
	}

	private bool TryGetPredictedHandPlayVisual(
		int playerIndex,
		int handIndex,
		out PlayingCard playedCard,
		out int tableCardIndex,
		out bool tableCardIsFaceUp)
	{
		playedCard = default;
		tableCardIndex = -1;
		tableCardIsFaceUp = false;
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[playerIndex].Count)
		{
			return false;
		}

		playedCard = PrepareAdventureEnemyPlayedCard(playerIndex, _playerHands[playerIndex][handIndex]);
		CardPlayPlan plan = ResolveCardPlayPlan(playedCard);
		tableCardIndex = _tableCards.Count;
		tableCardIsFaceUp = plan.MatchingTableCardIndex >= 0 || plan.PlayedCardCollectsTable;
		_networkPredictedHandMatchedTableCardIndex = plan.MatchingTableCardIndex;
		_networkPredictedHandMatchedTableCardFlipped = plan.MatchedTableCardFlipped;
		return true;
	}

	private async Task<bool> BeginPredictedNetworkHandCardPlayAsync(int handIndex, int turnSequenceGeneration)
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (!TryGetPredictedHandPlayVisual(
			localPlayerIndex,
			handIndex,
			out PlayingCard playedCard,
			out int tableCardIndex,
			out bool tableCardIsFaceUp))
		{
			return false;
		}

		Vector2 startGlobalPosition = TryGetHandCardStartGlobalPosition(localPlayerIndex, handIndex)
			?? GetVisibleCardPileTopGlobalPosition(localPlayerIndex);
		if (_networkPredictedHandMatchedTableCardFlipped
			&& _networkPredictedHandMatchedTableCardIndex >= 0
			&& _networkPredictedHandMatchedTableCardIndex < _tableCards.Count)
		{
			_tableCards[_networkPredictedHandMatchedTableCardIndex].IsFaceUp = true;
		}

		_tableCards.Add(new TableCard(playedCard, tableCardIsFaceUp, cardOwnerPlayerIndex: localPlayerIndex));
		HideHandDisplaySlot(localPlayerIndex, handIndex);
		RenderPlayerHands();
		_networkPredictedHandVisualActive = true;
		_networkPredictedHandTableCardIndex = tableCardIndex;
		await PlayCardToTableAnimationAsync(localPlayerIndex, playedCard, startGlobalPosition, tableCardIndex, tableCardIsFaceUp);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return false;
		}

		RenderTableCards();
		return true;
	}

	private async Task<bool> BeginPredictedNetworkItemCardPlayAsync(int itemHandIndex, int turnSequenceGeneration)
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (localPlayerIndex < 0
			|| localPlayerIndex >= _playerItemHands.Count
			|| itemHandIndex < 0
			|| itemHandIndex >= _playerItemHands[localPlayerIndex].Count)
		{
			return false;
		}

		ItemCard itemCard = _playerItemHands[localPlayerIndex][itemHandIndex];
		if (!_itemCardCatalog.TryGetDefinition(itemCard.Id, out ItemCardDefinition definition)
			|| !definition.PlayableFromHand
			|| !definition.EnabledInGame
			|| !CanPlayItemCard(localPlayerIndex, itemCard.Id))
		{
			return false;
		}

		int displayIndex = _playerHands[localPlayerIndex].Count + itemHandIndex;
		Vector2 startGlobalPosition = GetHandItemCardGlobalPosition(localPlayerIndex, itemHandIndex)
			?? GetVisibleCardPileTopGlobalPosition(localPlayerIndex);
		_networkPredictedItemVisualActive = true;
		_networkPredictedItemDisplayIndex = displayIndex;
		_networkPredictedItemCardId = itemCard.Id;
		_networkPredictedItemPlayPresentation = definition.PlayPresentation;
		_networkPredictedItemTargetTableCardIndex = -1;
		HideHandDisplaySlot(localPlayerIndex, displayIndex);
		RenderPlayerHands();
		PlaySpecialItemCardPlaySfx();

		switch (definition.PlayPresentation)
		{
			case CardPlayPresentationTypes.TableOverlay:
				_networkPredictedItemTargetTableCardIndex = _tableCards.Count;
				await PlayItemCardToTableAnimationAsync(localPlayerIndex, itemCard, startGlobalPosition, _networkPredictedItemTargetTableCardIndex, false);
				InsertNetworkTableOverlayIfMissing(localPlayerIndex, _networkPredictedItemTargetTableCardIndex, itemCard);
				break;
			case CardPlayPresentationTypes.LensSplit:
				Vector2 splitGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
				await PlayLensCardSplitAnimationAsync(
					itemCard.Texture,
					startGlobalPosition,
					splitGlobalPosition + CardVisualSize / 2.0f,
					GetCardRotationDegreesForPlayer(localPlayerIndex));
				break;
			case CardPlayPresentationTypes.TransformAreaTrigger:
				Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
				await PlayItemCardToTransformAreaAnimationAsync(
					itemCard.Texture,
					startGlobalPosition,
					transformAreaGlobalPosition,
					GetCardRotationDegreesForPlayer(localPlayerIndex));
				await PlaySpecialCardTriggerEffectAsync(itemCard.Texture, transformAreaGlobalPosition, 0);
				break;
			case CardPlayPresentationTypes.TargetedTrigger:
				StartPendingItemSelection(itemCard.Id, localPlayerIndex, itemHandIndex, turnSequenceGeneration);
				break;
			default:
				await PlaySpecialCardTriggerEffectAsync(
					itemCard.Texture,
					startGlobalPosition,
					0,
					GetCardRotationDegreesForPlayer(localPlayerIndex));
				break;
		}

		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return false;
		}

		if (definition.PlayPresentation != CardPlayPresentationTypes.TargetedTrigger)
		{
			RenderTableCards();
		}

		return true;
	}

	private void RollbackPredictedNetworkHandCardPlay()
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (_networkPendingHandIndex >= 0)
		{
			ShowHandDisplaySlot(localPlayerIndex, _networkPendingHandIndex);
		}

		if (_networkPredictedHandMatchedTableCardFlipped
			&& _networkPredictedHandMatchedTableCardIndex >= 0
			&& _networkPredictedHandMatchedTableCardIndex < _tableCards.Count)
		{
			_tableCards[_networkPredictedHandMatchedTableCardIndex].IsFaceUp = false;
		}

		if (_networkPredictedHandTableCardIndex >= 0
			&& _networkPredictedHandTableCardIndex < _tableCards.Count)
		{
			_tableCards.RemoveAt(_networkPredictedHandTableCardIndex);
		}

		RenderPlayerHands();
		RenderTableCards();
		ResetNetworkPredictedHandVisualState();
	}

	private void RollbackPredictedNetworkItemCardPlay()
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (_networkPredictedItemDisplayIndex >= 0)
		{
			ShowHandDisplaySlot(localPlayerIndex, _networkPredictedItemDisplayIndex);
		}

		if (_networkPredictedItemPlayPresentation == CardPlayPresentationTypes.TableOverlay
			&& _networkPredictedItemTargetTableCardIndex >= 0
			&& _networkPredictedItemTargetTableCardIndex < _tableCards.Count)
		{
			_tableCards.RemoveAt(_networkPredictedItemTargetTableCardIndex);
		}

		if (_networkPredictedItemVisualActive
			&& _networkPredictedItemPlayPresentation == CardPlayPresentationTypes.TargetedTrigger
			&& HasPendingItemSelection()
			&& _pendingItemOwnerIndex == localPlayerIndex
			&& _pendingItemCardId == _networkPredictedItemCardId)
		{
			ClearSelectionOverlay();
			ClearPendingItemSelectionState();
			_isProcessingCardPlay = false;
		}

		switch (_networkLocallyCommittedPendingSelectionMode)
		{
			case PendingItemSelectionMode.HandLimitDiscard:
				if (_networkLocallyCommittedPendingSelectionDisplayIndex >= 0)
				{
					ShowHandDisplaySlot(localPlayerIndex, _networkLocallyCommittedPendingSelectionDisplayIndex);
				}
				break;
			case PendingItemSelectionMode.MagnetHandCard:
				if (_networkLocallyCommittedPendingSelectionHandIndex >= 0
					&& _networkLocallyCommittedPendingSelectionHandIndex < _playerHands[localPlayerIndex].Count)
				{
					_playerHands[localPlayerIndex][_networkLocallyCommittedPendingSelectionHandIndex] = _networkLocallyCommittedPendingSelectionOriginalCard;
				}
				break;
		}

		RenderPlayerHands();
		RenderTableCards();
		ResetNetworkPredictedItemVisualState();
		ResetNetworkLocallyCommittedPendingSelectionState();
	}

	private void BeginPredictedNetworkHandLimitDiscardSelection(int displayIndex)
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		_networkLocallyCommittedPendingSelectionMode = PendingItemSelectionMode.HandLimitDiscard;
		_networkLocallyCommittedPendingSelectionDisplayIndex = displayIndex;
		HideHandDisplaySlot(localPlayerIndex, displayIndex);
		ClearSelectionOverlay();
		RenderPlayerHands();
	}

	private async Task BeginPredictedNetworkMagnetAttachmentSelectionAsync(int handIndex)
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (localPlayerIndex < 0
			|| localPlayerIndex >= _playerHands.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[localPlayerIndex].Count)
		{
			return;
		}

		_networkLocallyCommittedPendingSelectionMode = PendingItemSelectionMode.MagnetHandCard;
		_networkLocallyCommittedPendingSelectionHandIndex = handIndex;
		_networkLocallyCommittedPendingSelectionItemCardId = _pendingItemCardId;
		_networkLocallyCommittedPendingSelectionOriginalCard = _playerHands[localPlayerIndex][handIndex];
		ClearSelectionOverlay();
		ClearMagnetAttachmentCountdown();
		_isAnimatingMagnetAttachment = true;
		try
		{
			await PlayMagnetAttachmentToHandCardAnimationAsync(localPlayerIndex, handIndex, _pendingItemCardId);
			AttachMagnetToHandCard(localPlayerIndex, handIndex, _pendingItemCardId);
		}
		finally
		{
			_isAnimatingMagnetAttachment = false;
			RenderPlayerHands();
		}
	}

	private async Task BeginPredictedNetworkTargetedItemCommitAsync()
	{
		if (!_networkPredictedItemVisualActive
			|| _networkPredictedItemPlayPresentation != CardPlayPresentationTypes.TargetedTrigger)
		{
			return;
		}

		int localPlayerIndex = GetLocalHandPlayerIndex();
		Vector2 startGlobalPosition = GetHandItemCardGlobalPosition(localPlayerIndex, _networkPendingItemHandIndex)
			?? GetVisibleCardPileTopGlobalPosition(localPlayerIndex);
		ItemCard itemCard = CreateNetworkVisualItemCard(_networkPredictedItemCardId);
		await PlaySpecialCardTriggerEffectAsync(
			itemCard.Texture,
			startGlobalPosition,
			0,
			GetCardRotationDegreesForPlayer(localPlayerIndex));
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
			PlayCardResult result = new(false, false);

			_networkAuthoritativeVisualSnapshotId = snapshotId;
			_isProcessingCardPlay = true;
			try
			{
				Vector2? startGlobalPosition = TryGetHandCardStartGlobalPosition(actorSeatIndex, handIndex);
				result = await PlayCardFromHandAsync(actorSeatIndex, handIndex, turnSequenceGeneration, startGlobalPosition);
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
				if (result.CaptureVisualSync.HasValue)
				{
					GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.BroadcastCaptureVisualEventFromHost(
						result.CaptureVisualSync.Value,
						turnSequenceGeneration,
						snapshotId);
				}
			}

			GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.SendBoardSnapshotsFromHost(this, snapshotId);
			ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded();
			_networkAuthoritativeVisualSnapshotId = -1;
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
		PlayCardResult result = new(false, false);

		_networkAuthoritativeVisualSnapshotId = snapshotId;
		_isProcessingCardPlay = true;
		try
		{
			result = await PlayItemCardFromHandAsync(actorSeatIndex, itemHandIndex, turnSequenceGeneration);
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
				if (played && result.ItemPlayVisualSync.HasValue)
				{
					GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.BroadcastItemCardPlayedVisualEventFromHost(
						actorSeatIndex,
						result.ItemPlayVisualSync.Value,
						turnSequenceGeneration,
						snapshotId);
				}
				GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.SendBoardSnapshotsFromHost(this, snapshotId);
			}

			ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded();
			_networkAuthoritativeVisualSnapshotId = -1;
		}
	}

	internal int CreateNextNetworkSnapshotId()
	{
		_networkSnapshotId++;
		return _networkSnapshotId;
	}

	internal bool TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId)
	{
		snapshotId = _networkAuthoritativeVisualSnapshotId;
		return snapshotId >= 0;
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
			RollbackPredictedNetworkHandCardPlay();
			_networkPendingHandIndex = -1;
			_networkPendingTurnSequenceGeneration = -1;
		}

		if (rejectedHandIndex == _networkPendingItemHandIndex
			&& turnSequenceGeneration == _networkPendingTurnSequenceGeneration)
		{
			RollbackPredictedNetworkItemCardPlay();
			_networkPendingItemHandIndex = -1;
			_networkPendingTurnSequenceGeneration = -1;
		}

		if (rejectedHandIndex < 0
			&& turnSequenceGeneration == _networkPendingTurnSequenceGeneration
			&& _networkLocallyCommittedPendingSelectionMode != PendingItemSelectionMode.None)
		{
			RollbackPredictedNetworkItemCardPlay();
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
		string eventType = ReadNetworkString(payload, NetworkBoardVisualEventTypes.EventTypeKey, NetworkBoardVisualEventTypes.PlayHandCard);
		switch (eventType)
		{
			case NetworkBoardVisualEventTypes.PlayItemCard:
				await PlayNetworkItemCardVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.CaptureToDeck:
				await PlayNetworkCaptureVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.DrawCard:
				await PlayNetworkDrawCardVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.DeckShuffle:
				await PlayNetworkDeckShuffleVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.StealHandCard:
				await PlayNetworkStealHandCardVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.StealDeckCards:
				await PlayNetworkStealDeckCardsVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.LensReveal:
				await PlayNetworkLensRevealVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.TableShuffle:
				await PlayNetworkTableShuffleVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.QuestionRefresh:
				await PlayNetworkQuestionRefreshVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.QuestionReward:
				await PlayNetworkQuestionRewardVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.BombPair:
				await PlayNetworkBombPairVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.MantaTideShift:
				await PlayNetworkMantaTideShiftVisualEventAsync(payload);
				return;
			default:
				break;
		}

		int actorSeatIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int handIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.HandIndexKey, -1);
		string playedCardCode = ReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayedCardCodeKey, string.Empty);
		if (actorSeatIndex < 0 || actorSeatIndex >= player_count)
		{
			return;
		}

		PlayingCard playedCard = CreateNetworkPlayingCard(playedCardCode);
		int tableCardIndex = _tableCards.Count;
		bool matchesLocalPrediction = actorSeatIndex == GetLocalHandPlayerIndex()
			&& handIndex == _networkPendingHandIndex
			&& _networkPredictedHandVisualActive;
		if (!matchesLocalPrediction)
		{
			_tableCards.Add(new TableCard(playedCard, isFaceUp: true, cardOwnerPlayerIndex: actorSeatIndex));
			if (actorSeatIndex == GetLocalHandPlayerIndex()
				&& handIndex == _networkPendingHandIndex)
			{
				HideHandDisplaySlot(actorSeatIndex, handIndex);
			}
		}

		if (!matchesLocalPrediction)
		{
			Vector2 startGlobalPosition = TryGetHandCardStartGlobalPosition(actorSeatIndex, handIndex)
				?? GetVisibleCardPileTopGlobalPosition(actorSeatIndex);
			double debugPlaybackDelaySeconds = GetDebugNetworkVisualPlaybackDelaySeconds();
			if (debugPlaybackDelaySeconds > 0.0)
			{
				await ToSignal(GetTree().CreateTimer(debugPlaybackDelaySeconds), SceneTreeTimer.SignalName.Timeout);
			}

			await PlayCardToTableAnimationAsync(actorSeatIndex, playedCard, startGlobalPosition, tableCardIndex, tableCardIsFaceUp: true);
		}
		else
		{
			ClearNetworkPendingActionState();
			ResetNetworkPredictedHandVisualState();
		}
		RenderTableCards();
	}

	private async Task PlayNetworkItemCardVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int actorSeatIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int itemHandIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.HandIndexKey, -1);
		string itemCardId = ReadNetworkString(payload, NetworkBoardVisualEventTypes.ItemCardIdKey, string.Empty);
		string playPresentation = ReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayPresentationKey, CardPlayPresentationTypes.DirectTrigger);
		int targetTableCardIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TargetTableCardIndexKey, -1);
		if (actorSeatIndex < 0 || actorSeatIndex >= player_count || string.IsNullOrWhiteSpace(itemCardId))
		{
			return;
		}

		ItemCard itemCard = CreateNetworkVisualItemCard(itemCardId);
		bool matchesLocalPrediction = actorSeatIndex == GetLocalHandPlayerIndex()
			&& itemHandIndex == _networkPendingItemHandIndex
			&& _networkPredictedItemVisualActive
			&& playPresentation != CardPlayPresentationTypes.TargetedTrigger;
		if (!matchesLocalPrediction
			&& actorSeatIndex == GetLocalHandPlayerIndex()
			&& itemHandIndex == _networkPendingItemHandIndex)
		{
			HideHandDisplaySlot(actorSeatIndex, _playerHands[actorSeatIndex].Count + itemHandIndex);
			RenderPlayerHands();
		}

		if (matchesLocalPrediction)
		{
			ClearNetworkPendingActionState();
			ResetNetworkPredictedItemVisualState();
			ResetNetworkLocallyCommittedPendingSelectionState();
			RenderTableCards();
			return;
		}

		if (actorSeatIndex == GetLocalHandPlayerIndex()
			&& itemHandIndex == _networkPendingItemHandIndex
			&& _networkPredictedItemVisualActive
			&& playPresentation == CardPlayPresentationTypes.TargetedTrigger)
		{
			ClearSelectionOverlay();
			ClearPendingItemSelectionState();
			ClearNetworkPendingActionState();
			ResetNetworkPredictedItemVisualState();
			RenderPlayerHands();
		}

		bool matchesLocalCommittedSelection = actorSeatIndex == GetLocalHandPlayerIndex()
			&& _networkLocallyCommittedPendingSelectionMode != PendingItemSelectionMode.None
			&& playPresentation == CardPlayPresentationTypes.TargetedTrigger;
		if (matchesLocalCommittedSelection)
		{
			ResetNetworkLocallyCommittedPendingSelectionState();
		}

		Vector2 startGlobalPosition = GetHandItemCardGlobalPosition(actorSeatIndex, itemHandIndex)
			?? GetVisibleCardPileTopGlobalPosition(actorSeatIndex);
		if (playPresentation == CardPlayPresentationTypes.TableOverlay && targetTableCardIndex >= 0)
		{
			InsertNetworkTableOverlayIfMissing(actorSeatIndex, targetTableCardIndex, itemCard);
			await PlayItemCardToTableAnimationAsync(actorSeatIndex, itemCard, startGlobalPosition, targetTableCardIndex, tableCardIsFaceUp: false);
			RenderTableCards();
			return;
		}

		if (playPresentation == CardPlayPresentationTypes.TargetedTrigger)
		{
			await PlaySpecialCardTriggerEffectAsync(
				itemCard.Texture,
				startGlobalPosition,
				0,
				GetCardRotationDegreesForPlayer(actorSeatIndex));
			return;
		}

		Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
		await PlayItemCardToTransformAreaAnimationAsync(
			itemCard.Texture,
			startGlobalPosition,
			transformAreaGlobalPosition,
			GetCardRotationDegreesForPlayer(actorSeatIndex));
		if (actorSeatIndex == GetLocalHandPlayerIndex())
		{
			await PlaySpecialCardTriggerEffectAsync(itemCard.Texture, transformAreaGlobalPosition, 0);
		}
	}

	private async Task PlayNetworkDrawCardVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int playerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int displayIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.DisplayIndexKey, -1);
		string drawnCardCode = ReadNetworkString(payload, NetworkBoardVisualEventTypes.DrawnCardCodeKey, string.Empty);
		if (playerIndex < 0 || playerIndex >= player_count || displayIndex < 0)
		{
			return;
		}

		EnsureNetworkHandCardPlaceholder(playerIndex, displayIndex, drawnCardCode);
		DrawnHandCard drawnCard = new(
			string.IsNullOrWhiteSpace(drawnCardCode)
				? CreatePlaceholderPlayingCard()
				: CreateNetworkPlayingCard(drawnCardCode),
			displayIndex);
		await PlayReplacementDrawAnimationAsync(playerIndex, drawnCard, _turnSequenceGeneration);
	}

	private async Task PlayNetworkDeckShuffleVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int playerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		if (playerIndex < 0 || playerIndex >= player_count)
		{
			return;
		}

		await PlayPhysicalDeckShuffleAsync(playerIndex);
	}

	private async Task PlayNetworkStealHandCardVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int ownerPlayerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int targetPlayerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TargetSeatIndexKey, -1);
		int targetHandIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TargetHandIndexKey, -1);
		string itemCardId = ReadNetworkString(payload, NetworkBoardVisualEventTypes.ItemCardIdKey, string.Empty);
		string stolenCardCode = ReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayedCardCodeKey, string.Empty);
		string[] attachedItemCardIds = ReadStringArray(payload, NetworkBoardVisualEventTypes.AttachedItemCardIdsKey);
		if (ownerPlayerIndex != GetLocalHandPlayerIndex()
			|| ownerPlayerIndex < 0
			|| ownerPlayerIndex >= player_count
			|| targetPlayerIndex < 0
			|| targetPlayerIndex >= player_count
			|| targetHandIndex < 0)
		{
			return;
		}

		bool stealsItemCard = !string.IsNullOrWhiteSpace(itemCardId);
		if (stealsItemCard)
		{
			int sourceItemHandIndex = targetHandIndex - _playerHands[targetPlayerIndex].Count;
			ItemCard stolenItemCard = sourceItemHandIndex >= 0 && sourceItemHandIndex < _playerItemHands[targetPlayerIndex].Count
				? _playerItemHands[targetPlayerIndex][sourceItemHandIndex]
				: CreateNetworkVisualItemCard(itemCardId);
			PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
			await PlayStealHandCardAnimationAsync(
				ownerPlayerIndex,
				targetPlayerIndex,
				targetHandIndex,
				GetVisibleStolenItemCardTexture(targetPlayerIndex, stolenItemCard),
				null,
				GetVisibleStolenItemCardScale(targetPlayerIndex, stolenItemCard),
				GetStolenItemCardTargetDisplayIndex(ownerPlayerIndex));
			if (_turnSequenceGeneration != ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TurnSequenceGenerationKey, _turnSequenceGeneration)
				|| IsGameOver())
			{
				RenderPlayerHands();
				return;
			}

			if (sourceItemHandIndex >= 0 && sourceItemHandIndex < _playerItemHands[targetPlayerIndex].Count)
			{
				_playerItemHands[targetPlayerIndex].RemoveAt(sourceItemHandIndex);
				ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
			}

			_playerItemHands[ownerPlayerIndex].Add(stolenItemCard);
		}
		else
		{
			PlayingCard stolenCard = targetHandIndex >= 0 && targetHandIndex < _playerHands[targetPlayerIndex].Count
				? _playerHands[targetPlayerIndex][targetHandIndex]
				: CreateNetworkPlayingCard(stolenCardCode, attachedItemCardIds);
			PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
			await PlayStealHandCardAnimationAsync(
				ownerPlayerIndex,
				targetPlayerIndex,
				targetHandIndex,
				GetVisibleStolenPlayingCardTexture(targetPlayerIndex, stolenCard),
				GetVisibleStolenPlayingCardAttachmentOverlayTexture(targetPlayerIndex, stolenCard),
				1.0f,
				GetStolenPlayingCardTargetDisplayIndex(ownerPlayerIndex));
			if (_turnSequenceGeneration != ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TurnSequenceGenerationKey, _turnSequenceGeneration)
				|| IsGameOver())
			{
				RenderPlayerHands();
				return;
			}

			if (targetHandIndex >= 0 && targetHandIndex < _playerHands[targetPlayerIndex].Count)
			{
				_playerHands[targetPlayerIndex].RemoveAt(targetHandIndex);
				ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
			}

			_playerHands[ownerPlayerIndex].Add(stolenCard);
		}

		PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		RenderPlayerHands();
	}

	private async Task PlayNetworkStealDeckCardsVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int ownerPlayerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int targetPlayerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TargetSeatIndexKey, -1);
		int movedCardCount = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.MovedCardCountKey, 0);
		int turnSequenceGeneration = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TurnSequenceGenerationKey, _turnSequenceGeneration);
		if (ownerPlayerIndex != GetLocalHandPlayerIndex()
			|| ownerPlayerIndex < 0
			|| ownerPlayerIndex >= player_count
			|| targetPlayerIndex < 0
			|| targetPlayerIndex >= player_count
			|| movedCardCount <= 0)
		{
			return;
		}

		PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
		Vector2 startGlobalPosition = GetDrawCardStartGlobalPosition(targetPlayerIndex);
		Vector2 targetGlobalPosition = GetDrawCardStartGlobalPosition(ownerPlayerIndex);
		float startRotationDegrees = GetCardPileRotationDegreesForPlayer(targetPlayerIndex);
		float targetRotationDegrees = GetCardPileRotationDegreesForPlayer(ownerPlayerIndex);
		int appliedMoveCount = Mathf.Min(movedCardCount, _playerDecks[targetPlayerIndex].Count);
		for (int cardIndex = 0; cardIndex < appliedMoveCount; cardIndex++)
		{
			await PlayStolenDeckCardFlightAsync(
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees,
				targetRotationDegrees);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				RenderPlayerCardPiles();
				return;
			}

			PlayingCard topCard = _playerDecks[targetPlayerIndex][0];
			_playerDecks[targetPlayerIndex].RemoveAt(0);
			AddCardToPlayerDeck(ownerPlayerIndex, topCard);
		}

		if (appliedMoveCount > 0)
		{
			Shuffle(_playerDecks[ownerPlayerIndex]);
			Shuffle(_playerDecks[targetPlayerIndex]);
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		}

		RenderPlayerCardPiles();
	}

	private async Task PlayNetworkLensRevealVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int ownerPlayerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		float splitCenterX = ReadNetworkFloat(payload, NetworkBoardVisualEventTypes.SplitCenterXKey, 0.0f);
		float splitCenterY = ReadNetworkFloat(payload, NetworkBoardVisualEventTypes.SplitCenterYKey, 0.0f);
		Godot.Collections.Array targetIndexesArray = ReadArray(payload, NetworkBoardVisualEventTypes.TargetTableCardIndexesKey);
		int[] targetIndexes = ReadIntArray(payload, NetworkBoardVisualEventTypes.TargetTableCardIndexesKey, targetIndexesArray.Count);
		if (ownerPlayerIndex < 0 || ownerPlayerIndex >= player_count || targetIndexes.Length == 0)
		{
			return;
		}

		await RevealLensTargetCardsAsync(targetIndexes, new Vector2(splitCenterX, splitCenterY), _turnSequenceGeneration);
	}

	private async Task PlayNetworkTableShuffleVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		Godot.Collections.Array targetIndexesArray = ReadArray(payload, NetworkBoardVisualEventTypes.ShuffleTargetIndexesKey);
		int[] targetIndexes = ReadIntArray(payload, NetworkBoardVisualEventTypes.ShuffleTargetIndexesKey, targetIndexesArray.Count);
		List<TableCard> shuffledTableCards = BuildNetworkShuffledTableCards(targetIndexes);
		if (shuffledTableCards.Count != _tableCards.Count)
		{
			return;
		}

		await PlayTableCardsShuffleAnimationAsync(shuffledTableCards, _turnSequenceGeneration);
	}

	private async Task PlayNetworkQuestionRefreshVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int tableCardIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TableCardIndexKey, -1);
		if (tableCardIndex < 0)
		{
			return;
		}

		EnsureNetworkQuestionTableCard(tableCardIndex);
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		await PlayQuestionCardRefreshAnimationAsync(tableCardIndex);
		RenderTableCards();
	}

	private async Task PlayNetworkQuestionRewardVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int playerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		string itemCardId = ReadNetworkString(payload, NetworkBoardVisualEventTypes.ItemCardIdKey, string.Empty);
		int itemHandIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.HandIndexKey, -1);
		float startGlobalX = ReadNetworkFloat(payload, NetworkBoardVisualEventTypes.StartGlobalXKey, 0.0f);
		float startGlobalY = ReadNetworkFloat(payload, NetworkBoardVisualEventTypes.StartGlobalYKey, 0.0f);
		bool requiresMagnetAttachment = ReadNetworkBool(payload, NetworkBoardVisualEventTypes.RequiresMagnetAttachmentKey, false);
		if (playerIndex != GetLocalHandPlayerIndex() || string.IsNullOrWhiteSpace(itemCardId))
		{
			return;
		}

		ItemCard rewardItem = CreateNetworkVisualItemCard(itemCardId);
		if (!requiresMagnetAttachment && playerIndex >= 0 && playerIndex < _playerItemHands.Count && itemHandIndex >= 0)
		{
			while (_playerItemHands[playerIndex].Count <= itemHandIndex)
			{
				_playerItemHands[playerIndex].Add(rewardItem);
			}

			HideHandDisplaySlot(playerIndex, _playerHands[playerIndex].Count + itemHandIndex);
			RenderPlayerHands();
		}

		await PlayQuestionCardRewardAnimationAsync(
			playerIndex,
			new QuestionCardRewardAnimation(
				rewardItem,
				itemHandIndex,
				new Vector2(startGlobalX, startGlobalY),
				requiresMagnetAttachment),
			_turnSequenceGeneration);
	}

	private async Task PlayNetworkBombPairVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		await ResolveBombPairAsync(_turnSequenceGeneration);
		RenderTableCards();
	}

	private async Task PlayNetworkMantaTideShiftVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		if (_tableCards.Count <= 1)
		{
			return;
		}

		await PlayMantaRayTideShiftTableMoveAnimationAsync(_turnSequenceGeneration);
		if (_tableCards.Count > 0)
		{
			TableCard tideShiftedCard = _tableCards[0];
			_tableCards.RemoveAt(0);
			_tableCards.Add(tideShiftedCard);
			RenderTableCards();
		}
	}

	private async Task PlayNetworkCaptureVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int playerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int captureStartIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.CaptureStartIndexKey, -1);
		Godot.Collections.Array extraIndexesArray = ReadArray(payload, NetworkBoardVisualEventTypes.ExtraTableCardIndexesKey);
		int[] extraIndexes = ReadIntArray(payload, NetworkBoardVisualEventTypes.ExtraTableCardIndexesKey, extraIndexesArray.Count);
		float flightStaggerSeconds = ReadNetworkFloat(payload, NetworkBoardVisualEventTypes.FlightStaggerSecondsKey, TableCaptureFlightStaggerSeconds);
		if (playerIndex < 0
			|| playerIndex >= player_count
			|| captureStartIndex < 0
			|| captureStartIndex >= _tableCards.Count)
		{
			return;
		}

		HashSet<int>? extraTableCardIndexes = extraIndexes.Length == 0 ? null : new HashSet<int>(extraIndexes);
		List<CapturedTableCardVisual> capturedCardVisuals = BuildCapturedTableCardVisuals(
			captureStartIndex,
			false,
			false,
			extraTableCardIndexes);
		if (capturedCardVisuals.Count == 0)
		{
			return;
		}

		await PlayCapturedCardsToDeckAnimationAsync(
			playerIndex,
			captureStartIndex,
			capturedCardVisuals,
			_turnSequenceGeneration,
			extraTableCardIndexes,
			flightStaggerSeconds);
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

		GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.SendQuestionRewardVisualEventToSeatFromHost(
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
