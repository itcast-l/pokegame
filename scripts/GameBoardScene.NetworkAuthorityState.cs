using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private AuthorityActionState _networkAuthorityActionState = AuthorityActionState.Empty;
	private long _nextNetworkAuthorityActionId;

	internal AuthorityActionState NetworkAuthorityActionState => _networkAuthorityActionState;

	internal AuthorityActionState BeginNetworkAuthorityAction(
		AuthorityActionKind kind,
		int actorSeat,
		int turnGeneration,
		long clientActionId,
		int baseHandIndex = -1,
		int itemHandIndex = -1,
		string itemOrRewardId = "",
		bool reserveReplacement = false)
	{
		_nextNetworkAuthorityActionId++;
		_networkAuthorityActionState = new AuthorityActionState
		{
			ActionId = _nextNetworkAuthorityActionId,
			ClientActionId = Math.Max(0, clientActionId),
			Kind = kind,
			Phase = AuthorityActionPhase.Applying,
			ActorSeat = actorSeat,
			TurnGeneration = turnGeneration,
			BaseHandIndex = baseHandIndex,
			ItemHandIndex = itemHandIndex,
			ItemOrRewardId = itemOrRewardId ?? string.Empty,
			TargetSeat = -1,
			TargetHandIndex = -1,
			ReserveReplacement = reserveReplacement,
			QuestionRewardMutationApplied = false,
			DiscardMutationApplied = false,
			AttachmentMutationApplied = false,
			RemainingAttachmentRewardQueue = GetRemainingAttachmentRewardIds(),
			RemainingAttachmentRewardQueueSeats = GetRemainingAttachmentRewardQueueSeats(),
		};
		CommitNetworkAuthorityActionCheckpoint();
		return _networkAuthorityActionState;
	}

	internal void BeginNetworkAuthorityQuestionRewardTransaction(
		int playerIndex,
		string itemCardId,
		bool requiresAttachment,
		bool reserveReplacement,
		int turnGeneration)
	{
		if (!_networkAuthorityActionState.IsActive
			|| string.IsNullOrWhiteSpace(itemCardId))
		{
			return;
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			Kind = AuthorityActionKind.QuestionReward,
			Phase = AuthorityActionPhase.Applying,
			ActorSeat = playerIndex,
			TurnGeneration = turnGeneration,
			BaseHandIndex = -1,
			ItemHandIndex = -1,
			ItemOrRewardId = itemCardId.Trim(),
			ReserveReplacement = reserveReplacement,
			TargetSeat = -1,
			TargetHandIndex = -1,
			PendingRewardItemId = itemCardId.Trim(),
			PendingRewardSeat = playerIndex,
			PendingRewardHandIndex = -1,
			PendingRewardRequiresAttachment = requiresAttachment,
			ContinuationStep = AuthorityActionContinuationStep.QuestionRewardDelivery,
			TransferMutationApplied = false,
			QuestionRewardMutationApplied = false,
			DiscardMutationApplied = false,
			AttachmentMutationApplied = false,
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal void CommitNetworkAuthorityQuestionRewardQueued()
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			Phase = AuthorityActionPhase.Applying,
			ContinuationStep = AuthorityActionContinuationStep.AttachmentQueue,
			PendingRewardItemId = string.Empty,
			PendingRewardSeat = -1,
			PendingRewardHandIndex = -1,
			PendingRewardRequiresAttachment = false,
			QuestionRewardMutationApplied = true,
			RemainingAttachmentRewardQueue = GetRemainingAttachmentRewardIds(),
			RemainingAttachmentRewardQueueSeats = GetRemainingAttachmentRewardQueueSeats(),
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal void CommitNetworkAuthorityQuestionRewardHandMutation(int playerIndex, int itemHandIndex)
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			Phase = AuthorityActionPhase.Applying,
			ContinuationStep = AuthorityActionContinuationStep.QuestionRewardDelivery,
			PendingRewardSeat = playerIndex,
			PendingRewardHandIndex = itemHandIndex,
			PendingRewardRequiresAttachment = false,
			QuestionRewardMutationApplied = true,
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal bool TryAdvanceNetworkAuthorityActionPhase(AuthorityActionPhase nextPhase)
	{
		if (!_networkAuthorityActionState.IsActive
			|| !AuthorityActionStateRules.CanAdvancePhase(_networkAuthorityActionState.Phase, nextPhase))
		{
			return false;
		}

		_networkAuthorityActionState = _networkAuthorityActionState.WithPhase(nextPhase);
		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			RemainingAttachmentRewardQueue = GetRemainingAttachmentRewardIds(),
			RemainingAttachmentRewardQueueSeats = GetRemainingAttachmentRewardQueueSeats(),
		};
		CommitNetworkAuthorityActionCheckpoint();
		return true;
	}

	internal void SetNetworkAuthorityActionSelection(
		AuthorityActionPhase phase,
		int targetSeat,
		int targetHandIndex = -1)
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		if (!AuthorityActionStateRules.CanAdvancePhase(_networkAuthorityActionState.Phase, phase))
		{
			return;
		}

		AuthorityActionState currentState = _networkAuthorityActionState;
		bool preserveTargetForNestedDiscard = phase == AuthorityActionPhase.WaitingDiscard
			&& currentState.Kind == AuthorityActionKind.CardTransfer
			&& currentState.TargetSeat >= 0
			&& currentState.Phase is AuthorityActionPhase.WaitingTargetPlayer or AuthorityActionPhase.WaitingTargetHand;
		AuthorityActionKind selectionKind = phase is AuthorityActionPhase.WaitingTargetPlayer or AuthorityActionPhase.WaitingTargetHand
			? AuthorityActionKind.CardTransfer
			: currentState.Kind;
		_networkAuthorityActionState = currentState.WithSelection(
			phase,
			targetSeat,
			targetHandIndex);
		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			Kind = selectionKind,
			DeferredTargetSeat = preserveTargetForNestedDiscard ? currentState.TargetSeat : -1,
			DeferredTargetHandIndex = preserveTargetForNestedDiscard ? currentState.TargetHandIndex : -1,
			DeferredTargetPhase = preserveTargetForNestedDiscard ? currentState.Phase : AuthorityActionPhase.Applying,
			ContinuationStep = phase == AuthorityActionPhase.WaitingAttachment
				? AuthorityActionContinuationStep.AttachmentQueue
					: phase == AuthorityActionPhase.WaitingDiscard
						? AuthorityActionContinuationStep.QuestionRewardDelivery
						: AuthorityActionContinuationStep.CardTransfer,
			DiscardMutationApplied = phase == AuthorityActionPhase.WaitingDiscard
				? false
				: currentState.DiscardMutationApplied,
			AttachmentMutationApplied = phase == AuthorityActionPhase.WaitingAttachment
				? false
				: currentState.AttachmentMutationApplied,
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal void CommitNetworkAuthorityAttachmentMutation()
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			Phase = _pendingItemAttachmentRewards.Count > 0
				? AuthorityActionPhase.Applying
				: AuthorityActionPhase.Finalizing,
			ContinuationStep = _pendingItemAttachmentRewards.Count > 0
				? AuthorityActionContinuationStep.AttachmentQueue
				: AuthorityActionContinuationStep.TurnAdvancement,
			TargetSeat = -1,
			TargetHandIndex = -1,
			RemainingAttachmentRewardQueue = GetRemainingAttachmentRewardIds(),
			RemainingAttachmentRewardQueueSeats = GetRemainingAttachmentRewardQueueSeats(),
			PendingRewardItemId = string.Empty,
			PendingRewardSeat = -1,
			PendingRewardHandIndex = -1,
			PendingRewardRequiresAttachment = false,
			AttachmentMutationApplied = true,
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal void CommitNetworkAuthorityDiscardMutation()
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		_networkAuthorityActionState = AuthorityActionStateRules.AdvanceAfterDiscardMutation(
			_networkAuthorityActionState);
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal void SetNetworkAuthorityActionMutationResult(PlayCardResult result)
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		bool pendingSelection = HasPendingItemSelection();
		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			Played = result.Played,
			Captured = result.Captured,
			CompletesTurn = result.CompletesTurn,
			DeckCollected = result.DeckCollected,
			PrimaryMutationApplied = _networkAuthorityActionState.PrimaryMutationApplied || !pendingSelection && result.Played,
			TransferMutationApplied = _networkAuthorityActionState.TransferMutationApplied
				|| _networkAuthorityActionState.Kind == AuthorityActionKind.CardTransfer && !pendingSelection && result.Played,
			ContinuationStep = pendingSelection
				? (_pendingItemSelectionMode == PendingItemSelectionMode.AttachmentRewardHandCard
					? AuthorityActionContinuationStep.AttachmentQueue
					: AuthorityActionContinuationStep.QuestionRewardDelivery)
				: (result.CompletesTurn
					? AuthorityActionContinuationStep.TurnAdvancement
					: AuthorityActionContinuationStep.Complete),
			Phase = pendingSelection
				? GetAuthorityPhaseForPendingSelection()
				: AuthorityActionPhase.Finalizing,
			RemainingAttachmentRewardQueue = GetRemainingAttachmentRewardIds(),
			RemainingAttachmentRewardQueueSeats = GetRemainingAttachmentRewardQueueSeats(),
			TargetSeat = pendingSelection ? _networkAuthorityActionState.TargetSeat : -1,
			TargetHandIndex = pendingSelection ? _networkAuthorityActionState.TargetHandIndex : -1,
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal void SyncNetworkAuthorityActionWithPendingSelection()
	{
		if (!_networkAuthorityActionState.IsActive || !HasPendingItemSelection())
		{
			return;
		}

		AuthorityActionPhase phase = _pendingItemSelectionMode switch
		{
			PendingItemSelectionMode.TargetPlayer => AuthorityActionPhase.WaitingTargetPlayer,
			PendingItemSelectionMode.TargetHandCard => AuthorityActionPhase.WaitingTargetHand,
			PendingItemSelectionMode.HandLimitDiscard => AuthorityActionPhase.WaitingDiscard,
			PendingItemSelectionMode.AttachmentRewardHandCard => AuthorityActionPhase.WaitingAttachment,
			_ => AuthorityActionPhase.Applying,
		};
		SetNetworkAuthorityActionSelection(
			phase,
			phase == AuthorityActionPhase.WaitingTargetPlayer
				? -1
				: _pendingItemOwnerIndex,
			_pendingItemSelectionMode == PendingItemSelectionMode.TargetHandCard
				? _revealedPlayerHandIndex
				: -1);
	}

	internal void MarkNetworkAuthorityAttachmentRewardInFlight(PendingItemAttachmentReward reward)
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			ContinuationStep = AuthorityActionContinuationStep.AttachmentQueue,
			PendingRewardItemId = reward.ItemCardId,
			PendingRewardSeat = reward.PlayerIndex,
			PendingRewardHandIndex = -1,
			PendingRewardRequiresAttachment = true,
			AttachmentMutationApplied = false,
			RemainingAttachmentRewardQueue = GetRemainingAttachmentRewardIds(),
			RemainingAttachmentRewardQueueSeats = GetRemainingAttachmentRewardQueueSeats(),
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal void MarkNetworkAuthorityPrimaryMutationApplied()
	{
		if (!_networkAuthorityActionState.IsActive
			|| _networkAuthorityActionState.PrimaryMutationApplied)
		{
			return;
		}

			_networkAuthorityActionState = _networkAuthorityActionState with
			{
			Played = true,
			PrimaryMutationApplied = true,
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal void MarkNetworkAuthorityTransferMutationApplied()
	{
		if (!_networkAuthorityActionState.IsActive
			|| _networkAuthorityActionState.Kind != AuthorityActionKind.CardTransfer
			|| _networkAuthorityActionState.TransferMutationApplied)
		{
			return;
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			PrimaryMutationApplied = true,
			TransferMutationApplied = true,
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	private AuthorityActionPhase GetAuthorityPhaseForPendingSelection()
	{
		return _pendingItemSelectionMode switch
		{
			PendingItemSelectionMode.TargetPlayer => AuthorityActionPhase.WaitingTargetPlayer,
			PendingItemSelectionMode.TargetHandCard => AuthorityActionPhase.WaitingTargetHand,
			PendingItemSelectionMode.HandLimitDiscard => AuthorityActionPhase.WaitingDiscard,
			PendingItemSelectionMode.AttachmentRewardHandCard => AuthorityActionPhase.WaitingAttachment,
			_ => AuthorityActionPhase.Applying,
		};
	}

	internal void CompleteNetworkAuthorityAction()
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		_networkAuthorityActionState = AuthorityActionState.Empty;
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal void RestoreNetworkAuthorityActionState(AuthorityActionState state)
	{
		if (!AuthorityActionStateRules.IsValid(state, player_count))
		{
			return;
		}

		_networkAuthorityActionState = state;
		_nextNetworkAuthorityActionId = Math.Max(_nextNetworkAuthorityActionId, state.ActionId);
		RestoreNetworkAuthorityAttachmentQueue(state);
	}

	internal async Task ResumeAuthorityActionAsync()
	{
		if (!_networkAuthorityActionState.IsActive || !IsNetworkHost())
		{
			return;
		}

		AuthorityActionState state = _networkAuthorityActionState;
		if (state.Phase == AuthorityActionPhase.Applying && state.PrimaryMutationApplied)
		{
			if (HasPendingItemSelection())
			{
				SyncNetworkAuthorityActionWithPendingSelection();
			}
			else
			{
				_networkAuthorityActionState = state with { Phase = AuthorityActionPhase.Finalizing };
				CommitNetworkAuthorityActionCheckpoint();
			}

			state = _networkAuthorityActionState;
		}

		if (state.Kind == AuthorityActionKind.QuestionReward)
		{
			await ResumeQuestionRewardAuthorityActionAsync(_networkAuthorityActionState);
			return;
		}

		if (state.Phase == AuthorityActionPhase.Applying && HasPendingItemSelection())
		{
			SyncNetworkAuthorityActionWithPendingSelection();
			state = _networkAuthorityActionState;
		}

		if (state.Phase == AuthorityActionPhase.Applying)
		{
			await ResumeApplyingAuthorityActionAsync(state);
			return;
		}

		if (state.Phase is AuthorityActionPhase.WaitingTargetPlayer
			or AuthorityActionPhase.WaitingTargetHand
			or AuthorityActionPhase.WaitingDiscard
			or AuthorityActionPhase.WaitingAttachment)
		{
			EnsurePendingSelectionWaiterFromAuthorityAction();
			if (AuthorityActionStateRules.HasCommittedSelection(_networkAuthorityActionState))
			{
				await ResumeCommittedAuthoritySelectionAsync(_networkAuthorityActionState);
				return;
			}

			RefreshNetworkPendingSelectionOverlay();
			BeginNetworkAiProxyTurnForPendingSelectionIfNeeded();
		}

		if (_networkAuthorityActionState.Phase == AuthorityActionPhase.Finalizing)
		{
			await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
		}
	}

	private async Task ResumeApplyingAuthorityActionAsync(AuthorityActionState state)
	{
		if (state.Kind == AuthorityActionKind.BaseCard
			&& state.BaseHandIndex >= 0
			&& state.ActorSeat < _playerHands.Count
			&& state.BaseHandIndex < _playerHands[state.ActorSeat].Count)
		{
			await PlayNetworkHostHandCardAndBroadcastAsync(
				state.ActorSeat,
				state.BaseHandIndex,
				state.TurnGeneration);
			return;
		}

		if (state.Kind == AuthorityActionKind.ItemCard
			&& state.ItemHandIndex >= 0
			&& state.ActorSeat < _playerItemHands.Count
			&& state.ItemHandIndex < _playerItemHands[state.ActorSeat].Count
			&& string.Equals(
				state.ItemOrRewardId,
				_playerItemHands[state.ActorSeat][state.ItemHandIndex].Id,
				StringComparison.Ordinal))
		{
			await PlayNetworkHostItemCardAndBroadcastAsync(
				state.ActorSeat,
				state.ItemHandIndex,
				state.TurnGeneration);
			return;
		}

		// A checkpoint can be taken after the primary mutation but before the
		// final response is published. The card is then intentionally absent;
		// finish the recorded transaction instead of playing another card.
		_networkAuthorityActionState = state with { Phase = AuthorityActionPhase.Finalizing };
		CommitNetworkAuthorityActionCheckpoint();
		await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
	}

	private async Task ResumeCommittedAuthoritySelectionAsync(AuthorityActionState state)
	{
		if (state.Phase is AuthorityActionPhase.WaitingTargetPlayer or AuthorityActionPhase.WaitingTargetHand)
		{
			if (state.Kind == AuthorityActionKind.CardTransfer && state.PrimaryMutationApplied)
			{
					if (state.TransferMutationApplied)
					{
						_networkAuthorityActionState = state with
					{
						Phase = AuthorityActionPhase.Finalizing,
						TargetSeat = -1,
							TargetHandIndex = -1,
						};
						CommitNetworkAuthorityActionCheckpoint();
						await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
					return;
				}

				// The item card was consumed before the checkpoint. Re-enter the
				// resolver with the recorded target instead of consuming it again.
				await CompletePendingItemSelectionAuthorityAsync(
					state.ItemOrRewardId,
					state.ActorSeat,
					state.ItemHandIndex,
					state.TargetSeat,
					state.Phase == AuthorityActionPhase.WaitingTargetHand ? state.TargetHandIndex : -1,
					state.TurnGeneration);
				return;
			}

			await CompletePendingItemSelectionAuthorityAsync(
				state.ItemOrRewardId,
				state.ActorSeat,
				state.ItemHandIndex,
				state.TargetSeat,
				state.Phase == AuthorityActionPhase.WaitingTargetHand ? state.TargetHandIndex : -1,
				state.TurnGeneration);
			return;
		}

		if (state.Phase == AuthorityActionPhase.WaitingAttachment)
		{
			if (state.AttachmentMutationApplied)
			{
				CommitNetworkAuthorityAttachmentMutation();
				await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
				return;
			}

			await CompleteMagnetAttachmentSelectionAsync(state.TargetHandIndex);
			return;
		}

		if (state.Phase == AuthorityActionPhase.WaitingDiscard)
		{
			// The original TaskCompletionSource belongs to the old process. Apply
			// the already selected discard directly after restoration.
			if (state.DiscardMutationApplied)
			{
				CommitNetworkAuthorityDiscardMutation();
			}
			else if (RemoveHandCardFromGame(state.ActorSeat, state.TargetHandIndex))
			{
				CommitNetworkAuthorityDiscardMutation();
			}
			else
			{
				CompleteNetworkAuthorityAction();
			}

			if (_networkAuthorityActionState.IsActive
				&& _networkAuthorityActionState.Kind == AuthorityActionKind.CardTransfer
				&& _networkAuthorityActionState.Phase is AuthorityActionPhase.WaitingTargetPlayer or AuthorityActionPhase.WaitingTargetHand
				&& AuthorityActionStateRules.HasCommittedSelection(_networkAuthorityActionState))
			{
				await ResumeCommittedAuthoritySelectionAsync(_networkAuthorityActionState);
				return;
			}

			if (_networkAuthorityActionState.Kind == AuthorityActionKind.QuestionReward)
			{
				await ResumeQuestionRewardAuthorityActionAsync(_networkAuthorityActionState);
			}
			else
			{
				await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
			}
		}
	}

	private async Task ResumeQuestionRewardAuthorityActionAsync(AuthorityActionState state)
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		if (state.Phase == AuthorityActionPhase.WaitingDiscard)
		{
			EnsurePendingSelectionWaiterFromAuthorityAction();
			return;
		}

		if (state.PendingRewardRequiresAttachment || _pendingItemAttachmentRewards.Count > 0)
		{
			if (state.Phase == AuthorityActionPhase.WaitingAttachment)
			{
				if (state.AttachmentMutationApplied)
				{
					CommitNetworkAuthorityAttachmentMutation();
					await ResumeQuestionRewardAuthorityActionAsync(_networkAuthorityActionState);
					return;
				}

				EnsurePendingSelectionWaiterFromAuthorityAction();
				if (AuthorityActionStateRules.HasCommittedSelection(_networkAuthorityActionState))
				{
					await CompleteMagnetAttachmentSelectionAsync(_networkAuthorityActionState.TargetHandIndex);
				}
				return;
			}

			await ResolvePendingMagnetAttachmentsAsync(state.TurnGeneration);
			if (_networkAuthorityActionState.IsActive
				&& !HasPendingItemSelection()
				&& _pendingItemAttachmentRewards.Count == 0)
			{
				_networkAuthorityActionState = _networkAuthorityActionState with
				{
					Phase = AuthorityActionPhase.Finalizing,
					ContinuationStep = AuthorityActionContinuationStep.Complete,
				};
				CommitNetworkAuthorityActionCheckpoint();
				await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
			}
			return;
		}

		if (state.PendingRewardHandIndex < 0 && !string.IsNullOrWhiteSpace(state.PendingRewardItemId))
		{
			if (state.QuestionRewardMutationApplied)
			{
				_networkAuthorityActionState = state with
				{
					Phase = AuthorityActionPhase.Finalizing,
					ContinuationStep = AuthorityActionContinuationStep.Complete,
				};
				CommitNetworkAuthorityActionCheckpoint();
				await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
				return;
			}

			ItemCard? rewardCard = CreateItemCard(state.PendingRewardItemId);
			if (!rewardCard.HasValue)
			{
				CompleteNetworkAuthorityAction();
				return;
			}

			if (!HasRoomForNewHandCard(state.PendingRewardSeat))
			{
				await DiscardHandCardForNewItemCardAsync(state.PendingRewardSeat, state.TurnGeneration);
				if (HasPendingItemSelection())
				{
					return;
				}
			}

			if (!HasRoomForNewHandCard(state.PendingRewardSeat))
			{
				CompleteNetworkAuthorityAction();
				return;
			}

			int handIndex = _playerItemHands[state.PendingRewardSeat].Count;
			_playerItemHands[state.PendingRewardSeat].Add(rewardCard.Value);
			CommitNetworkAuthorityQuestionRewardHandMutation(state.PendingRewardSeat, handIndex);
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			Phase = AuthorityActionPhase.Finalizing,
			ContinuationStep = AuthorityActionContinuationStep.Complete,
		};
		CommitNetworkAuthorityActionCheckpoint();
		await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
	}

	private async Task ResumeFinalizingAuthorityActionAsync(AuthorityActionState state)
	{
		if (!state.TurnAdvanceApplied
			&& state.CompletesTurn
			&& !IsGameOver()
			&& _currentTurnPlayerIndex == state.ActorSeat)
		{
			await AdvanceTurnAfterPlayedResultAsync(
				state.ActorSeat,
				new PlayCardResult(
					state.Played,
					state.Captured,
					state.DeckCollected,
					CompletesTurn: true));
		}

		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			TurnAdvanceApplied = true,
			ContinuationStep = AuthorityActionContinuationStep.Complete,
		};
		CommitNetworkAuthorityActionCheckpoint();

		_isProcessingCardPlay = false;
		InvalidateRenderCaches();
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		CompleteNetworkAuthorityAction();
		SendBoardSnapshotIfHost();
		ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private void EnsurePendingSelectionWaiterFromAuthorityAction()
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		AuthorityActionState state = _networkAuthorityActionState;
		bool isAttachmentReward = state.Phase == AuthorityActionPhase.WaitingAttachment;
		_pendingItemOwnerIndex = isAttachmentReward && state.PendingRewardSeat >= 0
			? state.PendingRewardSeat
			: state.ActorSeat;
		_pendingItemHandIndex = state.ItemHandIndex;
		_pendingItemCardId = isAttachmentReward && !string.IsNullOrEmpty(state.PendingRewardItemId)
			? state.PendingRewardItemId
			: state.ItemOrRewardId;
		_pendingItemTurnSequenceGeneration = state.TurnGeneration;
		_pendingItemSelectionMode = state.Phase switch
		{
			AuthorityActionPhase.WaitingTargetPlayer => PendingItemSelectionMode.TargetPlayer,
			AuthorityActionPhase.WaitingTargetHand => PendingItemSelectionMode.TargetHandCard,
			AuthorityActionPhase.WaitingDiscard => PendingItemSelectionMode.HandLimitDiscard,
			AuthorityActionPhase.WaitingAttachment => PendingItemSelectionMode.AttachmentRewardHandCard,
			_ => PendingItemSelectionMode.None,
		};
		_revealedPlayerHandIndex = state.TargetSeat;
		if (state.Phase == AuthorityActionPhase.WaitingTargetPlayer)
		{
			_revealedPlayerHandIndex = -1;
		}
		_isProcessingCardPlay = true;

		if (state.Phase == AuthorityActionPhase.WaitingDiscard && _pendingHandLimitDiscardCompletion == null)
		{
			_pendingHandLimitDiscardCompletion = new TaskCompletionSource<int>(
				TaskCreationOptions.RunContinuationsAsynchronously);
		}

		if (state.Phase == AuthorityActionPhase.WaitingAttachment && _pendingMagnetAttachmentCompletion == null)
		{
			_pendingMagnetAttachmentCompletion = new TaskCompletionSource<bool>(
				TaskCreationOptions.RunContinuationsAsynchronously);
		}
	}

	private void RestoreNetworkAuthorityAttachmentQueue(AuthorityActionState state)
	{
		_pendingItemAttachmentRewards.Clear();
		if (state is null)
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(state.PendingRewardItemId) && state.PendingRewardSeat >= 0)
		{
			_pendingItemAttachmentRewards.Enqueue(
				new PendingItemAttachmentReward(state.PendingRewardSeat, state.PendingRewardItemId));
		}

		for (int index = 0; index < state.RemainingAttachmentRewardQueue.Length; index++)
		{
			int seat = state.RemainingAttachmentRewardQueueSeats.Length == state.RemainingAttachmentRewardQueue.Length
				? state.RemainingAttachmentRewardQueueSeats[index]
				: state.ActorSeat;
			_pendingItemAttachmentRewards.Enqueue(
				new PendingItemAttachmentReward(seat, state.RemainingAttachmentRewardQueue[index]));
		}
	}

	private string[] GetRemainingAttachmentRewardIds()
	{
		if (_pendingItemAttachmentRewards.Count == 0)
		{
			return Array.Empty<string>();
		}

		var result = new string[_pendingItemAttachmentRewards.Count];
		int index = 0;
		foreach (PendingItemAttachmentReward reward in _pendingItemAttachmentRewards)
		{
			result[index++] = reward.ItemCardId;
		}

		return result;
	}

	private int[] GetRemainingAttachmentRewardQueueSeats()
	{
		if (_pendingItemAttachmentRewards.Count == 0)
		{
			return Array.Empty<int>();
		}

		var result = new int[_pendingItemAttachmentRewards.Count];
		int index = 0;
		foreach (PendingItemAttachmentReward reward in _pendingItemAttachmentRewards)
		{
			result[index++] = reward.PlayerIndex;
		}

		return result;
	}

	private void CommitNetworkAuthorityActionCheckpoint()
	{
		if (IsNetworkMultiplayerMode() && IsNetworkHost())
		{
			GetNetworkManager()?.CommitAuthorityCheckpointFromBoard(this);
		}
	}
}
