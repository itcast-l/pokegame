using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private bool RequiresPlayerTarget(string itemCardId)
	{
		return IsItemEffectType(itemCardId, CardEffectTypes.StealCard)
			|| IsItemEffectType(itemCardId, CardEffectTypes.StealDeckCards);
	}

	private bool HasPendingItemSelection()
	{
		return _pendingItemSelectionMode != PendingItemSelectionMode.None;
	}

	private bool HasPlayableTargetForItemCard(string itemCardId, int ownerPlayerIndex)
	{
		return BuildItemTargetPlayerCandidates(itemCardId, ownerPlayerIndex).Count > 0;
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
		ShowTargetPlayerSelection();
	}

	private void CancelPendingItemSelection()
	{
		ClearSelectionOverlay();
		ClearPendingItemSelectionState();
		_isProcessingCardPlay = false;
		RenderPlayerHands();
	}

	private void ClearPendingItemSelectionState()
	{
		TaskCompletionSource<bool>? magnetAttachmentCompletion = _pendingMagnetAttachmentCompletion;
		TaskCompletionSource<int>? handLimitDiscardCompletion = _pendingHandLimitDiscardCompletion;
		ClearMagnetAttachmentCountdown();
		_isAnimatingMagnetAttachment = false;
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

		if (playerIndex == MainPlayerIndex)
		{
			return await ResolveMainPlayerHandLimitDiscardAsync(turnSequenceGeneration);
		}

		return RemoveHandCardFromGame(playerIndex, PickRandomHandDisplayIndex(playerIndex));
	}

	private async Task<bool> ResolveMainPlayerHandLimitDiscardAsync(int turnSequenceGeneration)
	{
		if (_pendingHandLimitDiscardCompletion != null || !HasAnyHandCard(MainPlayerIndex))
		{
			return false;
		}

		bool wasProcessingCardPlay = _isProcessingCardPlay;
		_isProcessingCardPlay = true;
		_pendingItemOwnerIndex = MainPlayerIndex;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = turnSequenceGeneration;
		_pendingItemCardId = string.Empty;
		_pendingItemSelectionMode = PendingItemSelectionMode.HandLimitDiscard;
		_pendingHandLimitDiscardCompletion = new TaskCompletionSource<int>();
		RenderPlayerHands();
		ShowHandLimitDiscardSelection();

		try
		{
			int displayIndex = await _pendingHandLimitDiscardCompletion.Task;
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return false;
			}

			if (!RemoveHandCardFromGame(MainPlayerIndex, displayIndex))
			{
				return false;
			}

			ClearSelectionOverlay();
			ClearPendingItemSelectionState();
			RenderPlayerHands();
			return true;
		}
		finally
		{
			if (_pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard)
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
		if (!HasAnyHandCard(MainPlayerIndex))
		{
			_pendingHandLimitDiscardCompletion?.TrySetResult(-1);
			return;
		}

		var overlay = CreateSelectionOverlay();
		var panel = CreateSelectionPanel(Localize("GAME_SELECT_DISCARD_CARD"));
		for (int handIndex = 0; handIndex < _playerHands[MainPlayerIndex].Count; handIndex++)
		{
			PlayingCard card = _playerHands[MainPlayerIndex][handIndex];
			int selectedDisplayIndex = handIndex;
			Button button = CreateSelectionButton($"{card.Suit} {card.Rank}");
			button.Pressed += () => CompleteHandLimitDiscardSelection(selectedDisplayIndex);
			panel.AddChild(button);
		}

		for (int itemIndex = 0; itemIndex < _playerItemHands[MainPlayerIndex].Count; itemIndex++)
		{
			ItemCard itemCard = _playerItemHands[MainPlayerIndex][itemIndex];
			int selectedDisplayIndex = _playerHands[MainPlayerIndex].Count + itemIndex;
			Button button = CreateSelectionButton(GetSpecialCardSelectionLabel(itemCard.Id));
			button.Pressed += () => CompleteHandLimitDiscardSelection(selectedDisplayIndex);
			panel.AddChild(button);
		}

		overlay.AddChild(panel);
		AddChild(overlay);
		_selectionOverlay = overlay;
	}

	private void CompleteHandLimitDiscardSelection(int displayIndex)
	{
		if (_pendingItemSelectionMode != PendingItemSelectionMode.HandLimitDiscard
			|| _pendingItemOwnerIndex != MainPlayerIndex
			|| _pendingItemTurnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		_pendingHandLimitDiscardCompletion?.TrySetResult(displayIndex);
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
