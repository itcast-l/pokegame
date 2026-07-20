using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task ResolvePendingMagnetAttachmentsAsync(int turnSequenceGeneration)
	{
		while (_pendingItemAttachmentRewards.Count > 0)
		{
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			PendingItemAttachmentReward reward = _pendingItemAttachmentRewards.Dequeue();
			MarkNetworkAuthorityAttachmentRewardInFlight(reward);
			if (!PlayerHasAttachableMagnetCard(reward.PlayerIndex, reward.ItemCardId))
			{
				CommitNetworkAuthorityAttachmentMutation();
				continue;
			}

			if (ShouldUseInteractiveHumanItemFollowup(reward.PlayerIndex))
			{
				await ResolveHumanMagnetAttachmentAsync(reward.PlayerIndex, reward.ItemCardId, turnSequenceGeneration);
			}
			else
			{
				if (AttachMagnetToAutoSelectedHandCard(reward.PlayerIndex, reward.ItemCardId))
				{
					PlayGameSfx(GameSfx.MagnetAttach, -1.0f, 0.97f, 1.03f, 0.08);
				}

				CommitNetworkAuthorityAttachmentMutation();
			}

			RenderPlayerHands();
		}
	}

	private async Task ResolveMainPlayerMagnetAttachmentAsync(string itemCardId, int turnSequenceGeneration)
	{
		await ResolveHumanMagnetAttachmentAsync(MainPlayerIndex, itemCardId, turnSequenceGeneration);
	}

	private async Task ResolveHumanMagnetAttachmentAsync(int ownerPlayerIndex, string itemCardId, int turnSequenceGeneration)
	{
		if (turnSequenceGeneration != _turnSequenceGeneration
			|| _isResolvingMagnetAttachmentSelection
			|| !PlayerHasAttachableMagnetCard(ownerPlayerIndex, itemCardId))
		{
			return;
		}

		bool wasProcessingCardPlay = _isProcessingCardPlay;
		var completionSource = new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		_isResolvingMagnetAttachmentSelection = true;
		_isProcessingCardPlay = true;
		_pendingItemCardId = itemCardId;
		_pendingItemOwnerIndex = ownerPlayerIndex;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = turnSequenceGeneration;
		_pendingItemSelectionMode = PendingItemSelectionMode.AttachmentRewardHandCard;
		_pendingMagnetAttachmentCompletion = completionSource;
		SyncNetworkAuthorityActionWithPendingSelection();
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
		ShowMagnetHandSelection();
		BeginNetworkAiProxyTurnForPendingSelectionIfNeeded();

		try
		{
			await completionSource.Task;
		}
		finally
		{
			if (turnSequenceGeneration == _turnSequenceGeneration)
			{
				bool stillOwnsSelection = OwnsPendingMagnetAttachmentSelection(
					completionSource,
					ownerPlayerIndex,
					itemCardId,
					turnSequenceGeneration);
				if (stillOwnsSelection)
				{
					DisposeActiveSelectionOverlay();
					ClearPendingItemSelectionState();
				}

				bool completedWithoutReplacement = _pendingMagnetAttachmentCompletion == null
					&& completionSource.Task.IsCompleted
					&& _pendingItemSelectionMode == PendingItemSelectionMode.None;
				if (stillOwnsSelection || completedWithoutReplacement)
				{
					_isResolvingMagnetAttachmentSelection = false;
					_isProcessingCardPlay = wasProcessingCardPlay;
					RenderPlayerHands();
				}
			}
		}
	}

	private bool OwnsPendingMagnetAttachmentSelection(
		TaskCompletionSource<bool> completionSource,
		int ownerPlayerIndex,
		string itemCardId,
		int turnSequenceGeneration)
	{
		return ReferenceEquals(_pendingMagnetAttachmentCompletion, completionSource)
			&& turnSequenceGeneration == _turnSequenceGeneration
			&& _pendingItemTurnSequenceGeneration == turnSequenceGeneration
			&& _pendingItemSelectionMode == PendingItemSelectionMode.AttachmentRewardHandCard
			&& _pendingItemOwnerIndex == ownerPlayerIndex
			&& string.Equals(_pendingItemCardId, itemCardId, StringComparison.Ordinal);
	}

	private void ShowMagnetHandSelection()
	{
		DisposeActiveSelectionOverlay();
		if (!IsMainPlayerMagnetSelectionActive())
		{
			ClearMagnetAttachmentCountdown();
			return;
		}

		int ownerPlayerIndex = GetPendingSelectionLocalOwnerIndex();
		if (!PlayerHasAttachableMagnetCard(ownerPlayerIndex, _pendingItemCardId))
		{
			if (!ShouldSendNetworkPendingItemSelectionRequest())
			{
				_ = CompleteMagnetAttachmentSelectionAsync(-1);
			}
			else
			{
				ClearMagnetAttachmentCountdown();
			}

			return;
		}

		RenderPlayerHands();
	}

	private async Task CompleteMagnetAttachmentSelectionAsync(int handIndex)
	{
		TaskCompletionSource<bool>? completionSource = _pendingMagnetAttachmentCompletion;
		int ownerPlayerIndex = _pendingItemOwnerIndex;
		string itemCardId = _pendingItemCardId;
		int turnSequenceGeneration = _pendingItemTurnSequenceGeneration;
		if (completionSource == null
			|| !OwnsPendingMagnetAttachmentSelection(
				completionSource,
				ownerPlayerIndex,
				itemCardId,
				turnSequenceGeneration)
			|| _isAnimatingMagnetAttachment)
		{
			return;
		}

		int attachHandIndex = IsValidMagnetAttachmentHandIndex(
			ownerPlayerIndex,
			handIndex,
			itemCardId)
			? handIndex
			: PickAutoAttachableMagnetHandIndex(ownerPlayerIndex, itemCardId);
		if (attachHandIndex < 0)
		{
			CommitNetworkAuthorityAttachmentMutation();
			DisposeActiveSelectionOverlay();
			ClearPendingItemSelectionState();
			RenderPlayerHands();
			SendBoardSnapshotIfHost();
			return;
		}

		_isAnimatingMagnetAttachment = true;
		ClearMagnetAttachmentCountdown();
		await PlayMagnetAttachmentToHandCardAnimationAsync(ownerPlayerIndex, attachHandIndex, itemCardId);
		if (!OwnsPendingMagnetAttachmentSelection(
			completionSource,
			ownerPlayerIndex,
			itemCardId,
			turnSequenceGeneration))
		{
			return;
		}

		if (!AttachMagnetToHandCard(ownerPlayerIndex, attachHandIndex, itemCardId))
		{
			AttachMagnetToAutoSelectedHandCard(ownerPlayerIndex, itemCardId);
		}
		CommitNetworkAuthorityAttachmentMutation();

		DisposeActiveSelectionOverlay();
		ClearPendingItemSelectionState();
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
	}

	private bool PlayerHasAttachableMagnetCard(int playerIndex, string itemCardId)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return false;
		}

		foreach (PlayingCard card in _playerHands[playerIndex])
		{
			if (IsAttachableMagnetCard(card, itemCardId))
			{
				return true;
			}
		}

		return false;
	}

	private bool AttachMagnetToHandCard(int playerIndex, int handIndex, string itemCardId)
	{
		if (!IsValidMagnetAttachmentHandIndex(playerIndex, handIndex, itemCardId))
		{
			return false;
		}

		PlayingCard card = _playerHands[playerIndex][handIndex];
		_playerHands[playerIndex][handIndex] = card.WithAttachedItemEffect(itemCardId);
		return true;
	}

	private bool AttachMagnetToRandomHandCard(int playerIndex, string itemCardId)
	{
		int handIndex = PickRandomAttachableMagnetHandIndex(playerIndex, itemCardId);
		return AttachMagnetToHandCard(playerIndex, handIndex, itemCardId);
	}

	private bool AttachMagnetToAutoSelectedHandCard(int playerIndex, string itemCardId)
	{
		int handIndex = PickAutoAttachableMagnetHandIndex(playerIndex, itemCardId);
		return AttachMagnetToHandCard(playerIndex, handIndex, itemCardId);
	}

	private int PickAutoAttachableMagnetHandIndex(int playerIndex, string itemCardId)
	{
		if (IsOfflineSoloAiPlayer(playerIndex))
		{
			int handIndex = PickOfflineSoloAiMagnetHandIndex(playerIndex, itemCardId);
			if (IsValidMagnetAttachmentHandIndex(playerIndex, handIndex, itemCardId))
			{
				return handIndex;
			}
		}

		return PickRandomAttachableMagnetHandIndex(playerIndex, itemCardId);
	}

	private int PickRandomAttachableMagnetHandIndex(int playerIndex, string itemCardId)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return -1;
		}

		return NetworkAiFallbackRules.PickRandomLegalCandidate(
			_playerHands[playerIndex].Count,
			handIndex => IsAttachableMagnetCard(_playerHands[playerIndex][handIndex], itemCardId),
			_rng.RandiRange(0, int.MaxValue));
	}

	private bool IsValidMagnetAttachmentHandIndex(int playerIndex, int handIndex, string itemCardId)
	{
		return playerIndex >= 0
			&& playerIndex < _playerHands.Count
			&& handIndex >= 0
			&& handIndex < _playerHands[playerIndex].Count
			&& IsAttachableMagnetCard(_playerHands[playerIndex][handIndex], itemCardId);
	}

	private bool IsAttachableMagnetCard(PlayingCard card, string itemCardId)
	{
		if (IsExcludedDeckRank(card.Rank))
		{
			return false;
		}

		return !card.HasAttachedItemEffect(itemCardId);
	}

}
