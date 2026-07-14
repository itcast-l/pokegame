using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
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

	internal bool IsNetworkGameOver()
	{
		return IsGameOver();
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

		if (_pendingItemSelectionMode == PendingItemSelectionMode.AttachmentRewardHandCard)
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

	private bool CanFinalizeNetworkAuthorityAction(
	int snapshotId,
	int turnSequenceGeneration,
	bool played)
{
	return snapshotId >= 0
		&& _networkAuthoritativeVisualSnapshotId == snapshotId
		&& (turnSequenceGeneration == _turnSequenceGeneration
			|| (played && IsGameOver()));
}

private void ClearNetworkAuthoritativeVisualSnapshotIfOwned(
	int snapshotId)
{
	if (snapshotId >= 0
		&& _networkAuthoritativeVisualSnapshotId == snapshotId)
	{
		_networkAuthoritativeVisualSnapshotId = -1;
	}
}

private async Task PlayNetworkHostHandCardAndBroadcastAsync(
	int actorSeatIndex,
	int handIndex,
	int turnSequenceGeneration)
{
	if (actorSeatIndex < 0
		|| actorSeatIndex >= _playerHands.Count
		|| handIndex < 0
		|| handIndex >= _playerHands[actorSeatIndex].Count)
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
		Vector2? startGlobalPosition =
			TryGetHandCardStartGlobalPosition(
				actorSeatIndex,
				handIndex);
		result = await PlayCardFromHandAsync(
			actorSeatIndex,
			handIndex,
			turnSequenceGeneration,
			startGlobalPosition);
		played = result.Played;
		if (result.Played
			&& !IsGameOver()
			&& result.CompletesTurn)
		{
			await AdvanceTurnAfterPlayedResultAsync(
				actorSeatIndex,
				result);
		}
	}
	finally
	{
		if (CanFinalizeNetworkAuthorityAction(
			snapshotId,
			turnSequenceGeneration,
			played))
		{
			if (!IsGameOver())
			{
				_isProcessingCardPlay = false;
				RenderPlayerHands();
			}

			GetNetworkManager()?.SendBoardSnapshotsFromHost(
				this,
				snapshotId);
			ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded();
		}

		ClearNetworkAuthoritativeVisualSnapshotIfOwned(snapshotId);
	}
}

private async Task PlayNetworkHostItemCardAndBroadcastAsync(
	int actorSeatIndex,
	int itemHandIndex,
	int turnSequenceGeneration)
{
	if (actorSeatIndex < 0
		|| actorSeatIndex >= _playerItemHands.Count
		|| itemHandIndex < 0
		|| itemHandIndex
			>= _playerItemHands[actorSeatIndex].Count)
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
		result = await PlayItemCardFromHandAsync(
			actorSeatIndex,
			itemHandIndex,
			turnSequenceGeneration);
		played = result.Played;
		if (result.Played
			&& !IsGameOver()
			&& result.CompletesTurn
			&& !HasPendingItemSelection())
		{
			await AdvanceTurnAfterPlayedResultAsync(
				actorSeatIndex,
				result);
		}
	}
	finally
	{
		if (CanFinalizeNetworkAuthorityAction(
			snapshotId,
			turnSequenceGeneration,
			played))
		{
			if (!IsGameOver()
				&& !HasPendingItemSelection())
			{
				_isProcessingCardPlay = false;
				RenderPlayerHands();
			}

			if (played || HasPendingItemSelection())
			{
				if (played
					&& result.ItemPlayVisualSync.HasValue)
				{
					GetNetworkManager()
						?.BroadcastItemCardPlayedVisualEventFromHost(
							actorSeatIndex,
							result.ItemPlayVisualSync.Value,
							turnSequenceGeneration,
							snapshotId);
				}
				GetNetworkManager()?.SendBoardSnapshotsFromHost(
					this,
					snapshotId);
			}

			ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded();
		}

		ClearNetworkAuthoritativeVisualSnapshotIfOwned(snapshotId);
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

		GetNetworkManager()?.SendBoardSnapshotsFromHost(this, CreateNextNetworkSnapshotId());
	}
}
