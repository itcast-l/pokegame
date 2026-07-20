using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private bool RequiresPlayerTarget(string itemCardId)
	{
		return _itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			&& (definition.EffectValues.GetString(CardEffectValueKeys.Target) == CardEffectTargets.PlayerHand
				|| definition.EffectValues.GetString(CardEffectValueKeys.Target) == CardEffectTargets.PlayerDeck);
	}

	private bool IsPlayerHandTargetItemCard(string itemCardId)
	{
		return _itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			&& definition.EffectValues.GetString(CardEffectValueKeys.Target) == CardEffectTargets.PlayerHand;
	}

	private bool IsPlayerDeckTargetItemCard(string itemCardId)
	{
		return _itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			&& definition.EffectValues.GetString(CardEffectValueKeys.Target) == CardEffectTargets.PlayerDeck;
	}

	private bool HasPendingItemSelection()
	{
		return _pendingItemSelectionMode != PendingItemSelectionMode.None;
	}

	private bool HasPlayableTargetForItemCard(string itemCardId, int ownerPlayerIndex)
	{
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition))
		{
			return false;
		}

		string target = definition.EffectValues.GetString(CardEffectValueKeys.Target);
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (IsResolvedItemTargetPlayerCandidate(target, ownerPlayerIndex, playerIndex))
			{
				return true;
			}
		}

		return false;
	}

	private void CancelPendingItemSelection()
	{
		bool hadPendingSelection = HasPendingItemSelection();
		bool hadAuthorityAction = _networkAuthorityActionState.IsActive;
		if (ShouldSendNetworkPendingItemSelectionRequest() && _networkPredictedItemVisualActive)
		{
			RollbackPredictedNetworkItemCardPlay();
			ClearNetworkPendingActionState();
		}

		DisposeActiveSelectionOverlay();
		ClearPendingItemSelectionState();
		ResetNetworkLocallyCommittedPendingSelectionState();
		ResetNetworkPredictedItemVisualState();
		ClearNetworkPendingActionState();
		_isProcessingCardPlay = false;
		RenderPlayerHands();
		if (!IsNetworkMultiplayerMode() || IsNetworkHost())
		{
			CompleteNetworkAuthorityAction();
			if (IsNetworkMultiplayerMode() && !hadAuthorityAction && hadPendingSelection)
			{
				GetNetworkManager()?.CommitAuthorityCheckpointFromBoard(this);
			}
		}
		SendBoardSnapshotIfHost();
	}

	private void ClearPendingItemSelectionState()
	{
		TaskCompletionSource<bool>? magnetAttachmentCompletion = _pendingMagnetAttachmentCompletion;
		TaskCompletionSource<int>? handLimitDiscardCompletion = _pendingHandLimitDiscardCompletion;
		_pendingMagnetAttachmentCompletion = null;
		_pendingHandLimitDiscardCompletion = null;
		ClearMagnetAttachmentCountdown();
		_isAnimatingMagnetAttachment = false;
		_networkPendingSelectionRequestInFlight = false;
		_pendingItemSelectionMode = PendingItemSelectionMode.None;
		_pendingItemOwnerIndex = -1;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = -1;
		_pendingItemCardId = string.Empty;
		magnetAttachmentCompletion?.TrySetResult(true);
		handLimitDiscardCompletion?.TrySetResult(-1);
	}

	private bool IsAttachmentItemCard(string itemCardId)
	{
		return _itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			&& definition.RewardDelivery == RewardDeliveryMode.ImmediateAttachment;
	}

	private int GetTotalHandCardCount(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return 0;
		}

		return _playerHands[playerIndex].Count + _playerItemHands[playerIndex].Count;
	}

	private bool HasRoomForNewHandCard(int playerIndex, int reservedHandCards = 0)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		return GetTotalHandCardCount(playerIndex) + reservedHandCards < MaxTotalHandCards;
	}

	private bool HasAnyHandCard(int playerIndex)
	{
		return GetTotalHandCardCount(playerIndex) > 0;
	}

	private async Task<bool> DiscardHandCardForNewItemCardAsync(
	int playerIndex,
	int turnSequenceGeneration,
	Func<bool>? canCommit = null)
{
	bool CanCommitDiscard()
	{
		return turnSequenceGeneration == _turnSequenceGeneration
			&& !_isLeavingGame
			&& !IsGameOver()
			&& (canCommit?.Invoke() ?? true);
	}

	if (!CanCommitDiscard()
		|| playerIndex < 0
		|| playerIndex >= _playerHands.Count
		|| playerIndex >= _playerItemHands.Count
		|| !HasAnyHandCard(playerIndex))
	{
		return false;
	}

	if (ShouldUseInteractiveHumanItemFollowup(playerIndex))
	{
		return await ResolveHumanHandLimitDiscardAsync(
			playerIndex,
			turnSequenceGeneration,
			CanCommitDiscard);
	}

	if (!CanCommitDiscard())
	{
		return false;
	}

	if (IsOfflineSoloAiPlayer(playerIndex))
	{
		bool removed = RemoveHandCardFromGame(
			playerIndex,
			PickOfflineSoloAiDiscardDisplayIndex(playerIndex));
		if (removed)
		{
			CommitNetworkAuthorityDiscardMutation();
		}

		return removed;
	}

	bool discarded = RemoveHandCardFromGame(
		playerIndex,
		PickRandomHandDisplayIndex(playerIndex));
	if (discarded)
	{
		CommitNetworkAuthorityDiscardMutation();
	}

		return discarded;
	}

	private async Task<int?> PlanHandLimitDiscardForQuestionRewardAsync(
		int playerIndex,
		int turnSequenceGeneration,
		IReadOnlySet<int>? excludedDisplayIndexes = null)
	{
		if (turnSequenceGeneration != _turnSequenceGeneration
			|| _isLeavingGame
			|| IsGameOver()
			|| playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count
			|| !HasAnyHandCard(playerIndex)
			|| GetTotalHandCardCount(playerIndex) <= (excludedDisplayIndexes?.Count ?? 0))
		{
			return null;
		}

		if (!ShouldUseInteractiveHumanItemFollowup(playerIndex))
		{
			int preferredDisplayIndex = IsOfflineSoloAiPlayer(playerIndex)
				? PickOfflineSoloAiDiscardDisplayIndex(playerIndex)
				: PickRandomHandDisplayIndex(playerIndex);
			if (preferredDisplayIndex >= 0
				&& !(excludedDisplayIndexes?.Contains(preferredDisplayIndex) ?? false))
			{
				return preferredDisplayIndex;
			}

			for (int displayIndex = 0; displayIndex < GetTotalHandCardCount(playerIndex); displayIndex++)
			{
				if (!(excludedDisplayIndexes?.Contains(displayIndex) ?? false))
				{
					return displayIndex;
				}
			}
			return null;
		}

		bool wasProcessingCardPlay = _isProcessingCardPlay;
		var completionSource = new TaskCompletionSource<int>();
		_isProcessingCardPlay = true;
		_pendingItemOwnerIndex = playerIndex;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = turnSequenceGeneration;
		_pendingItemCardId = string.Empty;
		_pendingItemSelectionMode = PendingItemSelectionMode.HandLimitDiscard;
		_pendingHandLimitDiscardCompletion = completionSource;
		_plannedQuestionRewardDiscardExclusions.Clear();
		if (excludedDisplayIndexes != null)
		{
			_plannedQuestionRewardDiscardExclusions.UnionWith(excludedDisplayIndexes);
		}
		RenderPlayerHands();
		ShowHandLimitDiscardSelection();
		try
		{
			int displayIndex = await completionSource.Task;
			return turnSequenceGeneration == _turnSequenceGeneration
				&& !_isLeavingGame
				&& !IsGameOver()
				&& IsValidHandLimitDiscardSelection(playerIndex, displayIndex)
					? displayIndex
					: null;
		}
		finally
		{
			if (ReferenceEquals(_pendingHandLimitDiscardCompletion, completionSource))
			{
				DisposeActiveSelectionOverlay();
				ClearPendingItemSelectionState();
				_plannedQuestionRewardDiscardExclusions.Clear();
				_isProcessingCardPlay = wasProcessingCardPlay;
				RenderPlayerHands();
			}
		}
	}

	private bool ShouldUseInteractiveHumanItemFollowup(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= player_count)
		{
			return false;
		}

		if (!IsNetworkMultiplayerMode())
		{
			return playerIndex == MainPlayerIndex;
		}

		return !(GetNetworkManager()?.IsSeatAiControlled(playerIndex) ?? false);
	}

	private Task<bool> ResolveMainPlayerHandLimitDiscardAsync(int turnSequenceGeneration)
	{
		return ResolveHumanHandLimitDiscardAsync(MainPlayerIndex, turnSequenceGeneration);
	}

	private async Task<bool> ResolveHumanHandLimitDiscardAsync(
	int ownerPlayerIndex,
	int turnSequenceGeneration,
	Func<bool>? canCommit = null)
{
	bool CanCommitDiscard()
	{
		return turnSequenceGeneration == _turnSequenceGeneration
			&& !_isLeavingGame
			&& !IsGameOver()
			&& (canCommit?.Invoke() ?? true);
	}

	if (!CanCommitDiscard()
		|| _pendingHandLimitDiscardCompletion != null
		|| !HasAnyHandCard(ownerPlayerIndex))
	{
		return false;
	}

	bool wasProcessingCardPlay = _isProcessingCardPlay;
	var completionSource = new TaskCompletionSource<int>();
	bool discarded = false;
	_isProcessingCardPlay = true;
	_pendingItemOwnerIndex = ownerPlayerIndex;
	_pendingItemHandIndex = -1;
	_pendingItemTurnSequenceGeneration = turnSequenceGeneration;
	_pendingItemCardId = string.Empty;
		_pendingItemSelectionMode = PendingItemSelectionMode.HandLimitDiscard;
		_pendingHandLimitDiscardCompletion = completionSource;
		SyncNetworkAuthorityActionWithPendingSelection();
		RenderPlayerHands();
	SendBoardSnapshotIfHost();
	ShowHandLimitDiscardSelection();
	BeginNetworkAiProxyTurnForPendingSelectionIfNeeded();

	try
	{
		int displayIndex = await completionSource.Task;
		if (!CanCommitDiscard())
		{
			return false;
		}

			if (!RemoveHandCardFromGame(ownerPlayerIndex, displayIndex))
			{
				return false;
			}

			CommitNetworkAuthorityDiscardMutation();
			discarded = true;
		return true;
	}
	finally
	{
		if (ReferenceEquals(
			_pendingHandLimitDiscardCompletion,
			completionSource))
		{
			DisposeActiveSelectionOverlay();
			ClearPendingItemSelectionState();
			_isProcessingCardPlay = wasProcessingCardPlay;
			RenderPlayerHands();
			if (discarded)
			{
				SendBoardSnapshotIfHost();
			}
		}
	}
}

	private void ShowHandLimitDiscardSelection()
	{
		DisposeActiveSelectionOverlay();
		if (!IsMainPlayerHandLimitDiscardSelectionActive())
		{
			return;
		}

		int ownerPlayerIndex = GetPendingSelectionLocalOwnerIndex();
		if (!HasAnyHandCard(ownerPlayerIndex))
		{
			if (!ShouldSendNetworkPendingItemSelectionRequest())
			{
				_pendingHandLimitDiscardCompletion?.TrySetResult(-1);
			}

			return;
		}

		var options = new List<(string Text, Action Action)>();
		for (int handIndex = 0; handIndex < _playerHands[ownerPlayerIndex].Count; handIndex++)
		{
			if (_plannedQuestionRewardDiscardExclusions.Contains(handIndex))
			{
				continue;
			}
			PlayingCard card = _playerHands[ownerPlayerIndex][handIndex];
			int selectedDisplayIndex = handIndex;
			options.Add(($"{card.Suit} {card.Rank}", () => CompleteHandLimitDiscardSelection(selectedDisplayIndex)));
		}

		for (int itemIndex = 0; itemIndex < _playerItemHands[ownerPlayerIndex].Count; itemIndex++)
		{
			int selectedDisplayIndex = _playerHands[ownerPlayerIndex].Count + itemIndex;
			if (_plannedQuestionRewardDiscardExclusions.Contains(selectedDisplayIndex))
			{
				continue;
			}
			ItemCard itemCard = _playerItemHands[ownerPlayerIndex][itemIndex];
			options.Add((GetSpecialCardSelectionLabel(itemCard.Id), () => CompleteHandLimitDiscardSelection(selectedDisplayIndex)));
		}

		SelectionOverlayCreationResult creationResult = ShowSelectionOverlay(
			Localize("GAME_SELECT_DISCARD_CARD"),
			options,
			showCancel: false);
		ResolveRequiredItemSelectionCreationFailure(creationResult, options);
	}

	private void CompleteHandLimitDiscardSelection(int displayIndex)
	{
		if (_pendingItemSelectionMode != PendingItemSelectionMode.HandLimitDiscard
			|| _pendingItemOwnerIndex != GetPendingSelectionLocalOwnerIndex()
			|| _pendingItemTurnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		if (!IsValidHandLimitDiscardSelection(_pendingItemOwnerIndex, displayIndex))
		{
			return;
		}

		if (ShouldSendNetworkPendingItemSelectionRequest())
		{
			BeginPredictedNetworkHandLimitDiscardSelection(displayIndex);
			RequestNetworkPendingItemSelection(displayIndex, _pendingItemTurnSequenceGeneration);
			return;
		}

		_pendingHandLimitDiscardCompletion?.TrySetResult(displayIndex);
	}

	private bool IsValidHandLimitDiscardSelection(int playerIndex, int displayIndex)
	{
		return playerIndex >= 0
			&& playerIndex < _playerHands.Count
			&& playerIndex < _playerItemHands.Count
			&& displayIndex >= 0
			&& displayIndex < GetTotalHandCardCount(playerIndex)
			&& !_plannedQuestionRewardDiscardExclusions.Contains(displayIndex);
	}

	private int PickRandomHandDisplayIndex(int playerIndex)
	{
		int totalHandCards = GetTotalHandCardCount(playerIndex);
		return totalHandCards <= 0 ? -1 : _rng.RandiRange(0, totalHandCards - 1);
	}

	private bool RemoveHandCardFromGame(int playerIndex, int displayIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count
			|| displayIndex < 0
			|| displayIndex >= GetTotalHandCardCount(playerIndex))
		{
			return false;
		}

		if (displayIndex < _playerHands[playerIndex].Count)
		{
			_playerHands[playerIndex].RemoveAt(displayIndex);
		}
		else
		{
			int itemHandIndex = displayIndex - _playerHands[playerIndex].Count;
			_playerItemHands[playerIndex].RemoveAt(itemHandIndex);
		}

		ShiftHandDisplayStateAfterRemovedCard(playerIndex, displayIndex);
		return true;
	}

	private string GetSpecialCardSelectionLabel(string itemCardId)
	{
		if (_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition))
		{
			string displayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? itemCardId : definition.DisplayName;
			return LocalizeFormat("GAME_SPECIAL_CARD", displayName);
		}

		return LocalizeFormat("GAME_SPECIAL_CARD", itemCardId);
	}

}
