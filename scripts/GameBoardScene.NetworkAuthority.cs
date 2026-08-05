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
		return TryStartNetworkHandCardPlayFromHost(
			actorSeatIndex,
			handIndex,
			turnSequenceGeneration,
			clientActionId: 0,
			out rejectReason);
	}

	internal bool TryStartNetworkHandCardPlayFromHost(
		int actorSeatIndex,
		int handIndex,
		int turnSequenceGeneration,
		long clientActionId,
		out MultiplayerBoardActionRejectReason rejectReason)
	{
		rejectReason = MultiplayerBoardActionRejectReason.None;
		if (_networkMigrationInputLocked)
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}

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
		if (!TryRegisterNetworkClientAction(actorSeatIndex, clientActionId))
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}

		if (!ExecuteNetworkHostHandCardRuleTransaction(
			actorSeatIndex,
			handIndex,
			turnSequenceGeneration,
			clientActionId,
			beginAction: true))
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}

		return true;
	}

	internal bool TryStartNetworkItemCardPlayFromHost(
		int actorSeatIndex,
		int itemHandIndex,
		int turnSequenceGeneration,
		out MultiplayerBoardActionRejectReason rejectReason)
	{
		return TryStartNetworkItemCardPlayFromHost(
			actorSeatIndex,
			itemHandIndex,
			turnSequenceGeneration,
			clientActionId: 0,
			out rejectReason);
	}

	internal bool TryStartNetworkItemCardPlayFromHost(
		int actorSeatIndex,
		int itemHandIndex,
		int turnSequenceGeneration,
		long clientActionId,
		out MultiplayerBoardActionRejectReason rejectReason)
	{
		if (!TryValidateNetworkItemCardPlayFromHost(
			actorSeatIndex,
			itemHandIndex,
			turnSequenceGeneration,
			out rejectReason))
		{
			return false;
		}
		if (!TryRegisterNetworkClientAction(actorSeatIndex, clientActionId))
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}

		if (!ExecuteNetworkHostItemCardRuleTransaction(
			actorSeatIndex,
			itemHandIndex,
			turnSequenceGeneration,
			clientActionId,
			beginAction: true))
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}

		return true;
	}

	private bool TryValidateNetworkItemCardPlayFromHost(
		int actorSeatIndex,
		int itemHandIndex,
		int turnSequenceGeneration,
		out MultiplayerBoardActionRejectReason rejectReason)
	{
		rejectReason = MultiplayerBoardActionRejectReason.None;
		if (_networkMigrationInputLocked)
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}

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

		return true;
	}

	private bool TryRegisterNetworkClientAction(int actorSeatIndex, long clientActionId)
	{
		return clientActionId == 0
			|| GetNetworkManager()?.TryAcceptClientAction(actorSeatIndex, clientActionId) == true;
	}

	internal bool TryStartNetworkPendingItemSelectionFromHost(
		int actorSeatIndex,
		int selectionIndex,
		int turnSequenceGeneration,
		out MultiplayerBoardActionRejectReason rejectReason)
	{
		return TryStartNetworkPendingItemSelectionFromHost(
			actorSeatIndex,
			selectionIndex,
			turnSequenceGeneration,
			clientActionId: 0,
			out rejectReason);
	}

	internal bool TryStartNetworkPendingItemSelectionFromHost(
		int actorSeatIndex,
		int selectionIndex,
		int turnSequenceGeneration,
		long clientActionId,
		out MultiplayerBoardActionRejectReason rejectReason)
	{
		rejectReason = MultiplayerBoardActionRejectReason.None;
		if (_networkMigrationInputLocked)
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}

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
			if (!IsValidHandLimitDiscardSelection(actorSeatIndex, selectionIndex)
				|| !CanCommitPendingNetworkSelection(
					AuthorityActionPhase.WaitingDiscard,
					actorSeatIndex,
					turnSequenceGeneration))
			{
				rejectReason = MultiplayerBoardActionRejectReason.InvalidHandIndex;
				return false;
			}
			if (!TryRegisterNetworkClientAction(actorSeatIndex, clientActionId))
			{
				rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
				return false;
			}

			return ExecuteNetworkHostPendingSelectionRuleTransaction(
				actorSeatIndex,
				selectionIndex,
				turnSequenceGeneration,
				clientActionId,
				AuthorityActionPhase.WaitingDiscard);
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.AttachmentRewardHandCard)
		{
			if (!IsValidMagnetAttachmentHandIndex(actorSeatIndex, selectionIndex, _pendingItemCardId)
				|| _isAnimatingMagnetAttachment
				|| !CanCommitPendingNetworkSelection(
					AuthorityActionPhase.WaitingAttachment,
					actorSeatIndex,
					turnSequenceGeneration))
			{
				rejectReason = MultiplayerBoardActionRejectReason.InvalidHandIndex;
				return false;
			}
			if (!TryRegisterNetworkClientAction(actorSeatIndex, clientActionId))
			{
				rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
				return false;
			}

			return ExecuteNetworkHostPendingSelectionRuleTransaction(
				actorSeatIndex,
				selectionIndex,
				turnSequenceGeneration,
				clientActionId,
				AuthorityActionPhase.WaitingAttachment);
		}

		rejectReason = MultiplayerBoardActionRejectReason.InvalidHandIndex;
		return false;
	}

	private bool CanCommitPendingNetworkSelection(
		AuthorityActionPhase expectedPhase,
		int actorSeatIndex,
		int turnSequenceGeneration)
	{
		return AuthorityActionStateRules.CanCommitPendingSelection(
			_networkAuthorityActionState,
			expectedPhase,
			actorSeatIndex,
			turnSequenceGeneration);
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
		ExecuteNetworkHostHandCardRuleTransaction(
			actorSeatIndex,
			handIndex,
			turnSequenceGeneration,
			_networkAuthorityActionState.ClientActionId,
			beginAction: false);
		await Task.CompletedTask;
	}

	private async Task PlayNetworkHostItemCardAndBroadcastAsync(
		int actorSeatIndex,
		int itemHandIndex,
		int turnSequenceGeneration)
	{
		ExecuteNetworkHostItemCardRuleTransaction(
			actorSeatIndex,
			itemHandIndex,
			turnSequenceGeneration,
			_networkAuthorityActionState.ClientActionId,
			beginAction: false);
		await Task.CompletedTask;
	}

	private bool ExecuteNetworkHostHandCardRuleTransaction(
		int actorSeatIndex,
		int handIndex,
		int turnSequenceGeneration,
		long clientActionId,
		bool beginAction)
	{
		if (actorSeatIndex < 0
			|| actorSeatIndex >= _playerHands.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[actorSeatIndex].Count)
		{
			return false;
		}

		Vector2 visualStartGlobalPosition = TryGetHandCardStartGlobalPosition(
			actorSeatIndex,
			handIndex) ?? Vector2.Zero;
		int snapshotId = CreateNextNetworkSnapshotId();
		BeginNetworkAuthorityRuleTransaction(visualStartGlobalPosition);
		_networkAuthoritativeVisualSnapshotId = snapshotId;
		_isProcessingCardPlay = true;

		try
		{
			if (beginAction)
			{
				BeginNetworkAuthorityAction(
					AuthorityActionKind.BaseCard,
					actorSeatIndex,
					turnSequenceGeneration,
					clientActionId,
					baseHandIndex: handIndex,
					baseCardCode: ToNetworkCardCode(_playerHands[actorSeatIndex][handIndex]));
			}
			MarkNetworkAuthorityActionApplying();

			long actionId = _networkAuthorityActionState.ActionId;
			long committedClientActionId = _networkAuthorityActionState.ClientActionId;
			PlayCardResult result = RequireSynchronousAuthorityRuleResult(
				PlayCardFromHandAsync(
					actorSeatIndex,
					handIndex,
					turnSequenceGeneration,
					visualStartGlobalPosition),
				nameof(PlayCardFromHandAsync));
			SetNetworkAuthorityActionMutationResult(result);
			if (!HasPendingItemSelection()
				&& _networkAuthorityActionState.Phase == AuthorityActionPhase.Finalizing)
			{
				RequireSynchronousAuthorityRuleCompletion(
					ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState),
					nameof(ResumeFinalizingAuthorityActionAsync));
			}
			if (!HasPendingItemSelection() && _networkAuthorityActionState.IsActive)
			{
				CompleteNetworkAuthorityAction();
			}

			_isProcessingCardPlay = false;
			ImmutableNetworkVisualDispatch[] visualEvents = EndNetworkAuthorityRuleTransaction(
				out NetworkVisualEvent[] hostVisualEvents);
			AuthorityRuleTransactionResult transaction = CommitNetworkAuthorityRuleTransaction(
				actionId,
				committedClientActionId,
				turnSequenceGeneration,
				snapshotId,
				visualEvents);
			PublishNetworkAuthorityRuleTransaction(actorSeatIndex, transaction, hostVisualEvents);
			return transaction.Committed;
		}
		finally
		{
			if (_networkAuthorityRuleTransactionActive)
			{
				EndNetworkAuthorityRuleTransaction(out _);
			}
			_isProcessingCardPlay = false;
			ClearNetworkAuthoritativeVisualSnapshotIfOwned(snapshotId);
		}
	}

	private bool ExecuteNetworkHostItemCardRuleTransaction(
		int actorSeatIndex,
		int itemHandIndex,
		int turnSequenceGeneration,
		long clientActionId,
		bool beginAction)
	{
		if (actorSeatIndex < 0
			|| actorSeatIndex >= _playerItemHands.Count
			|| itemHandIndex < 0
			|| itemHandIndex >= _playerItemHands[actorSeatIndex].Count)
		{
			return false;
		}

		Vector2 visualStartGlobalPosition = GetHandItemCardGlobalPosition(
			actorSeatIndex,
			itemHandIndex) ?? Vector2.Zero;
		int snapshotId = CreateNextNetworkSnapshotId();
		BeginNetworkAuthorityRuleTransaction(visualStartGlobalPosition);
		_networkAuthoritativeVisualSnapshotId = snapshotId;
		_isProcessingCardPlay = true;

		try
		{
			if (beginAction)
			{
				BeginNetworkAuthorityAction(
					AuthorityActionKind.ItemCard,
					actorSeatIndex,
					turnSequenceGeneration,
					clientActionId,
					itemHandIndex: itemHandIndex,
					itemOrRewardId: _playerItemHands[actorSeatIndex][itemHandIndex].Id);
			}
			MarkNetworkAuthorityActionApplying();

			long actionId = _networkAuthorityActionState.ActionId;
			long committedClientActionId = _networkAuthorityActionState.ClientActionId;
			PlayCardResult result = RequireSynchronousAuthorityRuleResult(
				PlayItemCardFromHandAsync(
					actorSeatIndex,
					itemHandIndex,
					turnSequenceGeneration),
				nameof(PlayItemCardFromHandAsync));
			SetNetworkAuthorityActionMutationResult(result);
			SyncNetworkAuthorityActionWithPendingSelection();
			if (result.Played && result.ItemPlayVisualSync.HasValue)
			{
				GetNetworkManager()?.BroadcastItemCardPlayedVisualEventFromHost(
					actorSeatIndex,
					result.ItemPlayVisualSync.Value,
					turnSequenceGeneration,
					snapshotId);
			}
			if (!HasPendingItemSelection()
				&& _networkAuthorityActionState.Phase == AuthorityActionPhase.Finalizing)
			{
				RequireSynchronousAuthorityRuleCompletion(
					ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState),
					nameof(ResumeFinalizingAuthorityActionAsync));
			}
			if (!HasPendingItemSelection() && _networkAuthorityActionState.IsActive)
			{
				CompleteNetworkAuthorityAction();
			}

			_isProcessingCardPlay = false;
			ImmutableNetworkVisualDispatch[] visualEvents = EndNetworkAuthorityRuleTransaction(
				out NetworkVisualEvent[] hostVisualEvents);
			AuthorityRuleTransactionResult transaction = CommitNetworkAuthorityRuleTransaction(
				actionId,
				committedClientActionId,
				turnSequenceGeneration,
				snapshotId,
				visualEvents);
			PublishNetworkAuthorityRuleTransaction(actorSeatIndex, transaction, hostVisualEvents);
			return transaction.Committed;
		}
		finally
		{
			if (_networkAuthorityRuleTransactionActive)
			{
				EndNetworkAuthorityRuleTransaction(out _);
			}
			_isProcessingCardPlay = false;
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
		if (_networkAuthorityRuleTransactionActive
			|| !IsNetworkMultiplayerMode()
			|| !IsNetworkHost())
		{
			return;
		}

		GetNetworkManager()?.SendBoardSnapshotsFromHost(CreateNextNetworkSnapshotId());
	}
}
