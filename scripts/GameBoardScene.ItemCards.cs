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
		ClearMagnetAttachmentCountdown();
		_isAnimatingMagnetAttachment = false;
		_pendingItemSelectionMode = PendingItemSelectionMode.None;
		_pendingItemOwnerIndex = -1;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = -1;
		_pendingItemCardId = string.Empty;
		_revealedPlayerHandIndex = -1;
		magnetAttachmentCompletion?.TrySetResult(true);
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

	private bool HasRoomForItemCard(int playerIndex, int reservedHandCards = 0)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		return GetTotalHandCardCount(playerIndex) + reservedHandCards < MaxTotalHandCards;
	}

}
