using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private void ResetNetworkPredictedHandVisualState()
	{
		_networkPredictedHandVisualActive = false;
		_networkPredictedHandTableCardIndex = -1;
		_networkPredictedHandMatchedTableCardIndex = -1;
		_networkPredictedHandMatchedTableCardFlipped = false;
		_networkPredictedHandBorrowTideAdjustedRank = string.Empty;
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
		DiscardNetworkReplicaPresentationState();
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
		out CardPlayPlan plan,
		out int tableCardIndex,
		out bool tableCardIsFaceUp)
	{
		playedCard = default;
		plan = default;
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
		plan = ResolveCardPlayPlan(playedCard);
		tableCardIndex = plan.TableInsertIndex;
		tableCardIsFaceUp = false;
		_networkPredictedHandMatchedTableCardIndex = plan.MatchingTableCardIndex;
		_networkPredictedHandMatchedTableCardFlipped = plan.MatchedTableCardFlipped;
		_networkPredictedHandBorrowTideAdjustedRank = plan.EffectiveRank;
		return true;
	}

	private bool BeginPredictedNetworkHandCardPlay(int handIndex, int turnSequenceGeneration)
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (!TryGetPredictedHandPlayVisual(
			localPlayerIndex,
			handIndex,
			out PlayingCard playedCard,
			out CardPlayPlan plan,
			out int tableCardIndex,
			out bool tableCardIsFaceUp))
		{
			return false;
		}

		if (!TryInsertNetworkPresentationTableCard(
			tableCardIndex,
			new TableCard(
				playedCard,
				tableCardIsFaceUp,
				cardOwnerPlayerIndex: localPlayerIndex)))
		{
			return false;
		}
		HideHandDisplaySlot(localPlayerIndex, handIndex);
		RenderPlayerHands();
		_networkPredictedHandVisualActive = true;
		_networkPredictedHandTableCardIndex = tableCardIndex;
		return true;
	}

	private async Task<bool> CompletePredictedNetworkHandCardPlayAsync(int handIndex, int turnSequenceGeneration)
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (!_networkPredictedHandVisualActive
			|| _networkPendingHandIndex != handIndex
			|| localPlayerIndex < 0
			|| localPlayerIndex >= _playerHands.Count
			|| _networkPredictedHandTableCardIndex < 0
			|| _networkPredictedHandTableCardIndex >= _tableCards.Count)
		{
			return false;
		}

		int tableCardIndex = _networkPredictedHandTableCardIndex;
		PlayingCard playedCard = _tableCards[_networkPredictedHandTableCardIndex].Card ?? default;
		bool tableCardIsFaceUp = _tableCards[_networkPredictedHandTableCardIndex].IsFaceUp;
		Vector2 startGlobalPosition = TryGetHandCardStartGlobalPosition(localPlayerIndex, handIndex)
			?? GetVisibleCardPileTopGlobalPosition(localPlayerIndex);
		await PlayCardToTableAnimationAsync(localPlayerIndex, playedCard, startGlobalPosition, tableCardIndex, tableCardIsFaceUp);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return false;
		}

		RenderTableCards();
		return true;
	}

	private bool BeginPredictedNetworkItemCardPlay(int itemHandIndex, int turnSequenceGeneration)
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
			|| !CanPlayItemCard(localPlayerIndex, itemCard.Id)
			|| !EnsureNetworkReplicaPresentationState())
		{
			return false;
		}

		int displayIndex = _playerHands[localPlayerIndex].Count + itemHandIndex;
		_ = GetHandItemCardGlobalPosition(localPlayerIndex, itemHandIndex)
			?? GetVisibleCardPileTopGlobalPosition(localPlayerIndex);
		_networkPredictedItemVisualActive = true;
		_networkPredictedItemDisplayIndex = displayIndex;
		_networkPredictedItemCardId = itemCard.Id;
		_networkPredictedItemPlayPresentation = definition.PlayPresentation;
		_networkPredictedItemTargetTableCardIndex = -1;
		HideHandDisplaySlot(localPlayerIndex, displayIndex);
		RenderPlayerHands();
		PlaySpecialItemCardPlaySfx();
		return true;
	}

	private async Task<bool> CompletePredictedNetworkItemCardPlayAsync(int itemHandIndex, int turnSequenceGeneration)
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (!_networkPredictedItemVisualActive
			|| _networkPendingItemHandIndex != itemHandIndex
			|| localPlayerIndex < 0
			|| localPlayerIndex >= _playerItemHands.Count
			|| itemHandIndex < 0
			|| itemHandIndex >= _playerItemHands[localPlayerIndex].Count)
		{
			return false;
		}

		ItemCard itemCard = _playerItemHands[localPlayerIndex][itemHandIndex];
		if (!_itemCardCatalog.TryGetDefinition(itemCard.Id, out ItemCardDefinition definition)
			|| _networkPredictedItemPlayPresentation != definition.PlayPresentation)
		{
			return false;
		}

		Vector2 startGlobalPosition = GetHandItemCardGlobalPosition(localPlayerIndex, itemHandIndex)
			?? GetVisibleCardPileTopGlobalPosition(localPlayerIndex);
		switch (definition.PlayPresentation)
		{
			case CardPlayPresentationTypes.TableOverlay:
				_networkPredictedItemTargetTableCardIndex = _tableCards.Count;
				await PlayItemCardToTableAnimationAsync(localPlayerIndex, itemCard, startGlobalPosition, _networkPredictedItemTargetTableCardIndex, false);
				TryAddNetworkTableOverlayPresentation(
					localPlayerIndex,
					_networkPredictedItemTargetTableCardIndex,
					itemCard);
				break;
			case CardPlayPresentationTypes.LensSplit:
				Vector2 splitGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
				await PlayLensCardSplitAnimationAsync(
					GetItemCardTexture(itemCard),
					startGlobalPosition,
					splitGlobalPosition + CardVisualSize / 2.0f,
					GetCardRotationDegreesForPlayer(localPlayerIndex));
				break;
			case CardPlayPresentationTypes.TransformAreaTrigger:
				Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
				await PlayItemCardToTransformAreaAnimationAsync(
					GetItemCardTexture(itemCard),
					startGlobalPosition,
					transformAreaGlobalPosition,
					GetCardRotationDegreesForPlayer(localPlayerIndex));
				await PlaySpecialCardTriggerEffectAsync(GetItemCardTexture(itemCard), transformAreaGlobalPosition, 0);
				break;
			default:
				await PlaySpecialCardTriggerEffectAsync(
					GetItemCardTexture(itemCard),
					startGlobalPosition,
					0,
					GetCardRotationDegreesForPlayer(localPlayerIndex));
				break;
		}

		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return false;
		}

		RenderTableCards();

		return true;
	}

	private void RollbackPredictedNetworkHandCardPlay()
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (_networkPendingHandIndex >= 0)
		{
			ShowHandDisplaySlot(localPlayerIndex, _networkPendingHandIndex);
		}

		TryRemoveNetworkPresentationTableCard(_networkPredictedHandTableCardIndex);
		DiscardNetworkReplicaPresentationState();

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

		switch (_networkLocallyCommittedPendingSelectionMode)
		{
			case PendingItemSelectionMode.HandLimitDiscard:
				if (_networkLocallyCommittedPendingSelectionDisplayIndex >= 0)
				{
					ShowHandDisplaySlot(localPlayerIndex, _networkLocallyCommittedPendingSelectionDisplayIndex);
				}
				break;
			case PendingItemSelectionMode.AttachmentRewardHandCard:
				if (_networkLocallyCommittedPendingSelectionHandIndex >= 0
					&& _networkLocallyCommittedPendingSelectionHandIndex < _playerHands[localPlayerIndex].Count)
				{
					TrySetNetworkPresentationHandCard(
						localPlayerIndex,
						_networkLocallyCommittedPendingSelectionHandIndex,
						_networkLocallyCommittedPendingSelectionOriginalCard);
				}
				break;
		}
		DiscardNetworkReplicaPresentationState();

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
		DisposeActiveSelectionOverlay();
		RenderPlayerHands();
	}

	private async Task BeginPredictedNetworkMagnetAttachmentSelectionAsync(int handIndex)
	{
		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (localPlayerIndex < 0
			|| localPlayerIndex >= _playerHands.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[localPlayerIndex].Count
			|| !EnsureNetworkReplicaPresentationState())
		{
			return;
		}

		_networkLocallyCommittedPendingSelectionMode = PendingItemSelectionMode.AttachmentRewardHandCard;
		_networkLocallyCommittedPendingSelectionHandIndex = handIndex;
		_networkLocallyCommittedPendingSelectionItemCardId = _pendingItemCardId;
		_networkLocallyCommittedPendingSelectionOriginalCard = _playerHands[localPlayerIndex][handIndex];
		DisposeActiveSelectionOverlay();
		ClearMagnetAttachmentCountdown();
		_isAnimatingMagnetAttachment = true;
		try
		{
			await PlayMagnetAttachmentToHandCardAnimationAsync(localPlayerIndex, handIndex, _pendingItemCardId);
			TryAttachNetworkPresentationItemToHandCard(
				localPlayerIndex,
				handIndex,
				_pendingItemCardId);
		}
		finally
		{
			_isAnimatingMagnetAttachment = false;
			RenderPlayerHands();
		}
	}

}
