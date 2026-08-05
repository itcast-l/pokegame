using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private async Task ResolvePendingMagnetAttachmentsAsync(int turnSequenceGeneration)
	{
		while (!GetPendingAttachmentRewards().IsEmpty)
		{
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			if (!TryTakePendingAttachmentReward(
				turnSequenceGeneration,
				out PendingAttachmentRewardState reward))
			{
				return;
			}
			MarkNetworkAuthorityAttachmentRewardInFlight(reward);
			if (!PlayerHasAttachableMagnetCard(reward.Seat, reward.ItemId))
			{
				CommitNetworkAuthorityAttachmentMutation();
				continue;
			}

			if (ShouldUseInteractiveHumanItemFollowup(reward.Seat))
			{
				await ResolveHumanMagnetAttachmentAsync(reward.Seat, reward.ItemId, turnSequenceGeneration);
				if (HasPendingItemSelection())
				{
					return;
				}
			}
			else
			{
				if (AttachMagnetToAutoSelectedHandCard(reward.Seat, reward.ItemId))
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
			|| !PlayerHasAttachableMagnetCard(ownerPlayerIndex, itemCardId)
			|| !TryBeginAttachmentMandatorySelection(
				ownerPlayerIndex,
				itemCardId,
				turnSequenceGeneration))
		{
			return;
		}
		if (_networkAuthorityRuleTransactionActive)
		{
			_isResolvingMagnetAttachmentSelection = false;
			_isProcessingCardPlay = true;
			_pendingMagnetAttachmentCompletion = null;
			SyncNetworkAuthorityActionWithPendingSelection();
			return;
		}

		bool wasProcessingCardPlay = _isProcessingCardPlay;
		var completionSource = new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		_isResolvingMagnetAttachmentSelection = true;
		_isProcessingCardPlay = true;
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
				TrackBoardTask(CompleteMagnetAttachmentSelectionAsync(-1), nameof(CompleteMagnetAttachmentSelectionAsync));
			}
			else
			{
				ClearMagnetAttachmentCountdown();
			}

			return;
		}

		RenderPlayerHands();
		ShowMagnetAttachmentCountdown();
	}

	private async Task CompleteMagnetAttachmentSelectionAsync(int handIndex)
	{
		TaskCompletionSource<bool>? completionSource = _pendingMagnetAttachmentCompletion;
		int ownerPlayerIndex = _pendingItemOwnerIndex;
		string itemCardId = _pendingItemCardId;
		int turnSequenceGeneration = _pendingItemTurnSequenceGeneration;
		bool transactionOwnsSelection = _networkAuthorityRuleTransactionActive
			&& turnSequenceGeneration == _turnSequenceGeneration
			&& _pendingItemSelectionMode == PendingItemSelectionMode.AttachmentRewardHandCard
			&& ownerPlayerIndex >= 0
			&& !string.IsNullOrWhiteSpace(itemCardId);
		if (!transactionOwnsSelection
			&& (completionSource == null
				|| !OwnsPendingMagnetAttachmentSelection(
					completionSource,
					ownerPlayerIndex,
					itemCardId,
					turnSequenceGeneration))
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
			DisposeActiveSelectionOverlay();
			ClearPendingItemSelectionState();
			CommitNetworkAuthorityAttachmentMutation();
			RenderPlayerHands();
			SendBoardSnapshotIfHost();
			return;
		}

		_isAnimatingMagnetAttachment = true;
		ClearMagnetAttachmentCountdown();
		await PlayMagnetAttachmentToHandCardAnimationAsync(ownerPlayerIndex, attachHandIndex, itemCardId);
		if (!transactionOwnsSelection
			&& !OwnsPendingMagnetAttachmentSelection(
				completionSource!,
				ownerPlayerIndex,
				itemCardId,
				turnSequenceGeneration))
		{
			return;
		}

		if (!TryResolveAttachmentMandatorySelection(
				ownerPlayerIndex,
				attachHandIndex,
				itemCardId,
				turnSequenceGeneration))
		{
			DisposeActiveSelectionOverlay();
			ClearPendingItemSelectionState();
			RenderPlayerHands();
			SendBoardSnapshotIfHost();
			return;
		}
		CommitNetworkAuthorityAttachmentMutation();

		DisposeActiveSelectionOverlay();
		ClearPendingItemSelectionState();
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
	}

	private bool TryBeginAttachmentMandatorySelection(
		int playerIndex,
		string itemCardId,
		int turnSequenceGeneration)
	{
		var legalIndexes = ImmutableArray.CreateBuilder<int>();
		if (playerIndex >= 0 && playerIndex < _playerHands.Count)
		{
			for (int handIndex = 0; handIndex < _playerHands[playerIndex].Count; handIndex++)
			{
				if (IsAttachableMagnetCard(_playerHands[playerIndex][handIndex], itemCardId))
				{
					legalIndexes.Add(handIndex);
				}
			}
		}

		ImmutableArray<int> candidates = legalIndexes.ToImmutable();
		MandatorySelectionState pending = CurrentMandatorySelection;
		if (pending.Kind == MandatorySelectionKind.AttachmentHandCard
			&& pending.Seat == playerIndex
			&& pending.TurnGeneration == turnSequenceGeneration
			&& string.Equals(pending.ItemId, itemCardId, StringComparison.Ordinal)
			&& pending.NormalizedLegalSelectionIndexes.SequenceEqual(candidates))
		{
			return true;
		}

		return candidates.Length > 0
			&& TrySubmitBeginMandatorySelectionRuleCommand(
				playerIndex,
				MandatorySelectionKind.AttachmentHandCard,
				sourceHandIndex: -1,
				itemCardId,
				candidates,
				turnSequenceGeneration,
				out _);
	}

	private bool TryResolveAttachmentMandatorySelection(
		int playerIndex,
		int handIndex,
		string itemCardId,
		int turnSequenceGeneration)
	{
		if (!IsValidMagnetAttachmentHandIndex(playerIndex, handIndex, itemCardId))
		{
			return false;
		}

		return TrySubmitResolveMandatorySelectionRuleCommand(
			playerIndex,
			MandatorySelectionKind.AttachmentHandCard,
			handIndex,
			_playerHands[playerIndex][handIndex].InstanceId,
			string.Empty,
			turnSequenceGeneration,
			out _);
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

		return TrySubmitAttachItemToHandCardRuleCommand(
			playerIndex,
			handIndex,
			itemCardId,
			ImmutableArray<int>.Empty,
			_turnSequenceGeneration,
			out _);
	}

	private bool AttachMagnetToRandomHandCard(int playerIndex, string itemCardId)
	{
		return AttachMagnetToRandomCandidateHandCard(playerIndex, itemCardId);
	}

	private bool AttachMagnetToAutoSelectedHandCard(int playerIndex, string itemCardId)
	{
		if (IsOfflineSoloAiPlayer(playerIndex))
		{
			int handIndex = PickOfflineSoloAiMagnetHandIndex(playerIndex, itemCardId);
			if (IsValidMagnetAttachmentHandIndex(playerIndex, handIndex, itemCardId))
			{
				return AttachMagnetToHandCard(playerIndex, handIndex, itemCardId);
			}
		}

		return AttachMagnetToRandomCandidateHandCard(playerIndex, itemCardId);
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

		var candidateIndexes = ImmutableArray.CreateBuilder<int>();
		for (int handIndex = 0; handIndex < _playerHands[playerIndex].Count; handIndex++)
		{
			if (IsAttachableMagnetCard(_playerHands[playerIndex][handIndex], itemCardId))
			{
				candidateIndexes.Add(handIndex);
			}
		}

		if (candidateIndexes.Count == 0
			|| !TrySubmitChooseRandomHandCardIndexRuleCommand(
				playerIndex,
				candidateIndexes.ToImmutable(),
				_turnSequenceGeneration,
				out int selectedHandIndex))
		{
			return -1;
		}

		return selectedHandIndex;
	}

	private bool AttachMagnetToRandomCandidateHandCard(int playerIndex, string itemCardId)
	{
		int handIndex = PickRandomAttachableMagnetHandIndex(playerIndex, itemCardId);
		return handIndex >= 0
			&& AttachMagnetToHandCard(playerIndex, handIndex, itemCardId);
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
