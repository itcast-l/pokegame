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

	private void StartPendingItemSelection(string itemCardId, int ownerPlayerIndex, int itemHandIndex, int turnSequenceGeneration)
	{
		_pendingItemCardId = itemCardId;
		_pendingItemOwnerIndex = ownerPlayerIndex;
		_pendingItemHandIndex = itemHandIndex;
		_pendingItemTurnSequenceGeneration = turnSequenceGeneration;
		_pendingItemSelectionMode = PendingItemSelectionMode.TargetPlayer;
		_revealedPlayerHandIndex = -1;
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
		ShowTargetPlayerSelection();
		BeginNetworkAiProxyTurnForPendingSelectionIfNeeded();
	}

	private void CancelPendingItemSelection()
	{
		ClearSelectionOverlay();
		ClearPendingItemSelectionState();
		_isProcessingCardPlay = false;
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
	}

	private void ClearPendingItemSelectionState()
	{
		TaskCompletionSource<bool>? magnetAttachmentCompletion = _pendingMagnetAttachmentCompletion;
		TaskCompletionSource<int>? handLimitDiscardCompletion = _pendingHandLimitDiscardCompletion;
		ClearMagnetAttachmentCountdown();
		_isAnimatingMagnetAttachment = false;
		_networkPendingSelectionRequestInFlight = false;
		_pendingItemSelectionMode = PendingItemSelectionMode.None;
		_pendingItemOwnerIndex = -1;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = -1;
		_pendingItemCardId = string.Empty;
		_revealedPlayerHandIndex = -1;
		_pendingHandLimitDiscardCompletion = null;
		magnetAttachmentCompletion?.TrySetResult(true);
		handLimitDiscardCompletion?.TrySetResult(-1);
	}

	private bool IsAttachmentItemCard(string itemCardId)
	{
		return IsItemEffectType(itemCardId, CardEffectTypes.AttachModifier);
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

	private async Task<bool> DiscardHandCardForNewItemCardAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count
			|| !HasAnyHandCard(playerIndex))
		{
			return false;
		}

		if (ShouldUseInteractiveHumanItemFollowup(playerIndex))
		{
			return await ResolveHumanHandLimitDiscardAsync(playerIndex, turnSequenceGeneration);
		}

		return RemoveHandCardFromGame(playerIndex, PickRandomHandDisplayIndex(playerIndex));
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

		return !(GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.IsSeatAiControlled(playerIndex) ?? false);
	}

	private Task<bool> ResolveMainPlayerHandLimitDiscardAsync(int turnSequenceGeneration)
	{
		return ResolveHumanHandLimitDiscardAsync(MainPlayerIndex, turnSequenceGeneration);
	}

	private async Task<bool> ResolveHumanHandLimitDiscardAsync(int ownerPlayerIndex, int turnSequenceGeneration)
	{
		if (_pendingHandLimitDiscardCompletion != null || !HasAnyHandCard(ownerPlayerIndex))
		{
			return false;
		}

		bool wasProcessingCardPlay = _isProcessingCardPlay;
		_isProcessingCardPlay = true;
		_pendingItemOwnerIndex = ownerPlayerIndex;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = turnSequenceGeneration;
		_pendingItemCardId = string.Empty;
		_pendingItemSelectionMode = PendingItemSelectionMode.HandLimitDiscard;
		_pendingHandLimitDiscardCompletion = new TaskCompletionSource<int>();
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
		ShowHandLimitDiscardSelection();
		BeginNetworkAiProxyTurnForPendingSelectionIfNeeded();

		try
		{
			int displayIndex = await _pendingHandLimitDiscardCompletion.Task;
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return false;
			}

			if (!RemoveHandCardFromGame(ownerPlayerIndex, displayIndex))
			{
				return false;
			}

			ClearSelectionOverlay();
			ClearPendingItemSelectionState();
			RenderPlayerHands();
			SendBoardSnapshotIfHost();
			return true;
		}
		finally
		{
			if (_pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard
				&& _pendingItemOwnerIndex == ownerPlayerIndex)
			{
				ClearSelectionOverlay();
				ClearPendingItemSelectionState();
			}

			_isProcessingCardPlay = wasProcessingCardPlay;
			RenderPlayerHands();
		}
	}

	private void ShowHandLimitDiscardSelection()
	{
		ClearSelectionOverlay();
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
			PlayingCard card = _playerHands[ownerPlayerIndex][handIndex];
			int selectedDisplayIndex = handIndex;
			options.Add(($"{card.Suit} {card.Rank}", () => CompleteHandLimitDiscardSelection(selectedDisplayIndex)));
		}

		for (int itemIndex = 0; itemIndex < _playerItemHands[ownerPlayerIndex].Count; itemIndex++)
		{
			ItemCard itemCard = _playerItemHands[ownerPlayerIndex][itemIndex];
			int selectedDisplayIndex = _playerHands[ownerPlayerIndex].Count + itemIndex;
			options.Add((GetSpecialCardSelectionLabel(itemCard.Id), () => CompleteHandLimitDiscardSelection(selectedDisplayIndex)));
		}

		ShowSelectionOverlay(Localize("GAME_SELECT_DISCARD_CARD"), options, showCancel: false);
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
			RequestNetworkPendingItemSelection(-1, displayIndex, _pendingItemTurnSequenceGeneration);
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
			&& displayIndex < GetTotalHandCardCount(playerIndex);
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
