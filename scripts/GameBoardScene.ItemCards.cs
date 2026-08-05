using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Deckland.Rules;
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
				GetNetworkManager()?.CommitAuthorityCheckpointFromBoard();
			}
		}
		SendBoardSnapshotIfHost();
	}

	private void ClearPendingItemSelectionState()
	{
		MandatorySelectionState pending = CurrentMandatorySelection;
		if (pending.Kind != MandatorySelectionKind.None && _authorityRuleSession != null)
		{
			TrySubmitClearMandatorySelectionRuleCommand(
				pending.Seat,
				pending.Kind,
				pending.TurnGeneration,
				out _);
		}
		ReleasePendingItemSelectionPresentation();
		_isAnimatingMagnetAttachment = false;
	}

	private void ReleasePendingItemSelectionPresentation()
	{
		TaskCompletionSource<bool>? magnetAttachmentCompletion = _pendingMagnetAttachmentCompletion;
		TaskCompletionSource<int>? handLimitDiscardCompletion = _pendingHandLimitDiscardCompletion;
		_pendingMagnetAttachmentCompletion = null;
		_pendingHandLimitDiscardCompletion = null;
		ClearMagnetAttachmentCountdown();
		_networkPendingSelectionRequestInFlight = false;
		DeferNetworkAuthoritySelectionWaiters(
			magnetAttachmentCompletion,
			handLimitDiscardCompletion);
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

	private bool TryBeginHandLimitMandatorySelection(
		int playerIndex,
		int turnSequenceGeneration,
		IReadOnlySet<int>? excludedDisplayIndexes = null)
	{
		var legalIndexes = ImmutableArray.CreateBuilder<int>();
		for (int displayIndex = 0; displayIndex < GetTotalHandCardCount(playerIndex); displayIndex++)
		{
			if (!(excludedDisplayIndexes?.Contains(displayIndex) ?? false))
			{
				legalIndexes.Add(displayIndex);
			}
		}

		ImmutableArray<int> candidates = legalIndexes.ToImmutable();
		MandatorySelectionState pending = CurrentMandatorySelection;
		if (pending.Kind == MandatorySelectionKind.HandLimitDiscard
			&& pending.Seat == playerIndex
			&& pending.TurnGeneration == turnSequenceGeneration
			&& (excludedDisplayIndexes == null
				|| pending.NormalizedLegalSelectionIndexes.SequenceEqual(candidates)))
		{
			return true;
		}

		return candidates.Length > 0
			&& TrySubmitBeginMandatorySelectionRuleCommand(
				playerIndex,
				MandatorySelectionKind.HandLimitDiscard,
				sourceHandIndex: -1,
				itemId: string.Empty,
				candidates,
				turnSequenceGeneration,
				out _);
	}

	private bool TryResolveHandLimitMandatorySelection(
		int playerIndex,
		int displayIndex,
		int turnSequenceGeneration)
	{
		if (!TryGetHandDisplayIdentity(
				playerIndex,
				displayIndex,
				out long expectedCardRuntimeInstanceId,
				out string expectedItemId)
			|| !TrySubmitResolveMandatorySelectionRuleCommand(
				playerIndex,
				MandatorySelectionKind.HandLimitDiscard,
				displayIndex,
				expectedCardRuntimeInstanceId,
				expectedItemId,
				turnSequenceGeneration,
				out RuleCommandResult result))
		{
			return false;
		}

		ShiftHandDisplayStateAfterRemovedCard(playerIndex, result.Mutation.SourceIndex);
		return true;
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
				: PickRandomHandDisplayIndex(playerIndex, excludedDisplayIndexes);
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
		if (!TryBeginHandLimitMandatorySelection(
				playerIndex,
				turnSequenceGeneration,
				excludedDisplayIndexes))
		{
			return null;
		}
		if (_networkAuthorityRuleTransactionActive)
		{
			_isProcessingCardPlay = true;
			_pendingHandLimitDiscardCompletion = null;
			SyncNetworkAuthorityActionWithPendingSelection();
			return null;
		}

		bool wasProcessingCardPlay = _isProcessingCardPlay;
		var completionSource = new TaskCompletionSource<int>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		_isProcessingCardPlay = true;
		_pendingHandLimitDiscardCompletion = completionSource;
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
		|| !HasAnyHandCard(ownerPlayerIndex)
		|| !TryBeginHandLimitMandatorySelection(ownerPlayerIndex, turnSequenceGeneration))
	{
		return false;
	}

	bool wasProcessingCardPlay = _isProcessingCardPlay;
	var completionSource = new TaskCompletionSource<int>(
		TaskCreationOptions.RunContinuationsAsynchronously);
	bool discarded = false;
	_isProcessingCardPlay = true;
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
		if (_networkAuthorityRuleTransactionActive)
		{
			_isProcessingCardPlay = true;
			_pendingHandLimitDiscardCompletion = null;
			return false;
		}

			if (!TryResolveHandLimitMandatorySelection(
					ownerPlayerIndex,
					displayIndex,
					turnSequenceGeneration))
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
			if (!IsValidHandLimitDiscardSelection(ownerPlayerIndex, handIndex))
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
			if (!IsValidHandLimitDiscardSelection(ownerPlayerIndex, selectedDisplayIndex))
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
		if (IsNetworkMultiplayerMode() && IsNetworkHost())
		{
			TryStartNetworkPendingItemSelectionFromHost(
				_pendingItemOwnerIndex,
				displayIndex,
				_pendingItemTurnSequenceGeneration,
				out _);
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
			&& IsCurrentHandLimitDiscardCandidate(playerIndex, displayIndex);
	}

	private bool IsValidHandLimitDiscardSelection(
		PlayerStateSnapshot player,
		int displayIndex)
	{
		return displayIndex >= 0
			&& displayIndex < player.Hand.Length + player.ItemHand.Length
			&& IsCurrentHandLimitDiscardCandidate(player.Seat, displayIndex);
	}

	private bool IsCurrentHandLimitDiscardCandidate(
		int playerIndex,
		int displayIndex)
	{
		MandatorySelectionState pending = CurrentMandatorySelection;
		return pending.Kind == MandatorySelectionKind.HandLimitDiscard
			&& pending.Seat == playerIndex
			&& pending.TurnGeneration == _turnSequenceGeneration
			&& pending.NormalizedLegalSelectionIndexes.Contains(displayIndex);
	}

	private int PickRandomHandDisplayIndex(
		int playerIndex,
		IReadOnlySet<int>? excludedDisplayIndexes = null)
	{
		var excludedIndexes = ImmutableArray.CreateBuilder<int>(
			excludedDisplayIndexes?.Count ?? 0);
		if (excludedDisplayIndexes != null)
		{
			foreach (int excludedIndex in excludedDisplayIndexes)
			{
				excludedIndexes.Add(excludedIndex);
			}
		}

		return TrySubmitChooseRandomHandDisplayIndexRuleCommand(
			playerIndex,
			playerIndex,
			excludedIndexes.MoveToImmutable(),
			_turnSequenceGeneration,
			out int displayIndex)
				? displayIndex
				: -1;
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

		long expectedCardRuntimeInstanceId = 0;
		string expectedItemId = string.Empty;
		if (displayIndex < _playerHands[playerIndex].Count)
		{
			expectedCardRuntimeInstanceId = _playerHands[playerIndex][displayIndex].InstanceId;
		}
		else
		{
			int itemHandIndex = displayIndex - _playerHands[playerIndex].Count;
			expectedItemId = _playerItemHands[playerIndex][itemHandIndex].Id;
		}

		if (!TrySubmitDiscardHandCardRuleCommand(
			playerIndex,
			displayIndex,
			expectedCardRuntimeInstanceId,
			expectedItemId,
			_turnSequenceGeneration,
			out RuleCommandResult result))
		{
			return false;
		}

		ShiftHandDisplayStateAfterRemovedCard(playerIndex, result.Mutation.SourceIndex);
		return true;
	}

	private bool TryGetHandDisplayIdentity(
		int playerIndex,
		int displayIndex,
		out long cardRuntimeInstanceId,
		out string itemId)
	{
		cardRuntimeInstanceId = 0;
		itemId = string.Empty;
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
			cardRuntimeInstanceId = _playerHands[playerIndex][displayIndex].InstanceId;
		}
		else
		{
			int itemHandIndex = displayIndex - _playerHands[playerIndex].Count;
			itemId = _playerItemHands[playerIndex][itemHandIndex].Id;
		}

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
