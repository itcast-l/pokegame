using System;
using System.Globalization;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private AuthorityActionState _networkAuthorityActionState = AuthorityActionState.Empty;
	private long _nextNetworkAuthorityActionId;
	private TaskCompletionSource<bool>? _authorityTransactionFaultWaiter;

	internal AuthorityActionState NetworkAuthorityActionState => _networkAuthorityActionState;
	internal bool CanPublishNetworkBoardSnapshot =>
		AuthorityActionStateRules.IsStableCheckpointState(_networkAuthorityActionState);

	private Task WaitForAuthorityTransactionFaultAsync(string point)
	{
		string requestedPoint = OS.GetEnvironment("CODEX_AUTHORITY_FAULT_POINT");
		if (string.IsNullOrWhiteSpace(requestedPoint)
			|| !string.Equals(requestedPoint.Trim(), point, StringComparison.Ordinal)
			|| !IsNetworkMultiplayerMode()
			|| !IsNetworkHost()
			|| _networkAuthorityEpoch != 0
			|| !_networkAuthorityActionState.IsActive)
		{
			return Task.CompletedTask;
		}

		if (_authorityTransactionFaultWaiter != null)
		{
			return _authorityTransactionFaultWaiter.Task;
		}

		_authorityTransactionFaultWaiter = new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		GD.Print(
			$"AUTHORITY_FAULT_REACHED point={point} checkpoint={_networkCheckpointId} "
			+ $"action={_networkAuthorityActionState.ActionId} "
			+ $"client_action={_networkAuthorityActionState.ClientActionId} "
			+ $"generation={_turnSequenceGeneration} "
			+ $"phase={_networkAuthorityActionState.Phase}");
		return _authorityTransactionFaultWaiter.Task;
	}

	internal AuthorityActionState BeginNetworkAuthorityAction(
		AuthorityActionKind kind,
		int actorSeat,
		int turnGeneration,
		long clientActionId,
		int baseHandIndex = -1,
		int itemHandIndex = -1,
		string itemOrRewardId = "",
		string baseCardCode = "",
		bool reserveReplacement = false)
	{
		_nextNetworkAuthorityActionId++;
		_networkAuthorityActionState = new AuthorityActionState
		{
			ActionId = _nextNetworkAuthorityActionId,
			ClientActionId = Math.Max(0, clientActionId),
			Kind = kind,
			Phase = AuthorityActionPhase.Prepared,
			ActorSeat = actorSeat,
			TurnGeneration = turnGeneration,
			BaseHandIndex = baseHandIndex,
			ItemHandIndex = itemHandIndex,
			ItemOrRewardId = itemOrRewardId ?? string.Empty,
			BaseCardCode = baseCardCode ?? string.Empty,
			RngStateBeforeAction = _rng.State.ToString(CultureInfo.InvariantCulture),
			TargetSeat = -1,
			TargetHandIndex = -1,
			SelectionIndex = -1,
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

	internal void MarkNetworkAuthorityActionApplying()
	{
		if (_networkAuthorityActionState.IsActive
			&& AuthorityActionStateRules.CanAdvancePhase(
				_networkAuthorityActionState.Phase,
				AuthorityActionPhase.Applying))
		{
			_networkAuthorityActionState = _networkAuthorityActionState with
			{
				Phase = AuthorityActionPhase.Applying,
			};
		}
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
		int selectionIndex = -1)
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
		_networkAuthorityActionState = currentState.WithSelection(
			phase,
			selectionIndex);
		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			ContinuationStep = phase == AuthorityActionPhase.WaitingAttachment
				? AuthorityActionContinuationStep.AttachmentQueue
				: currentState.Kind == AuthorityActionKind.CardTransfer
					? AuthorityActionContinuationStep.CardTransfer
					: AuthorityActionContinuationStep.QuestionRewardDelivery,
			DiscardMutationApplied = phase == AuthorityActionPhase.WaitingDiscard
				? false
				: currentState.DiscardMutationApplied,
			AttachmentMutationApplied = phase == AuthorityActionPhase.WaitingAttachment
				? false
				: currentState.AttachmentMutationApplied,
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	internal void CommitNetworkAuthorityCardTransferTarget(int targetSeat, int targetHandIndex)
	{
		if (!_networkAuthorityActionState.IsActive
			|| targetSeat < 0)
		{
			return;
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			Kind = AuthorityActionKind.CardTransfer,
			Phase = AuthorityActionPhase.Applying,
			TargetSeat = targetSeat,
			TargetHandIndex = targetHandIndex,
			SelectionIndex = -1,
			ContinuationStep = AuthorityActionContinuationStep.CardTransfer,
			TransferMutationApplied = false,
		};
		CommitNetworkAuthorityActionCheckpoint();
	}

	private bool TryGetRecordedNetworkAuthorityCardTransferTarget(
		string itemCardId,
		int actorSeat,
		out int targetSeat,
		out int targetHandIndex)
	{
		AuthorityActionState state = _networkAuthorityActionState;
		if (state.IsActive
			&& state.Kind == AuthorityActionKind.CardTransfer
			&& state.ActorSeat == actorSeat
			&& string.Equals(state.ItemOrRewardId, itemCardId, StringComparison.Ordinal)
			&& state.TargetSeat >= 0)
		{
			targetSeat = state.TargetSeat;
			targetHandIndex = state.TargetHandIndex;
			return true;
		}

		targetSeat = -1;
		targetHandIndex = -1;
		return false;
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
			SelectionIndex = -1,
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
					: _networkAuthorityActionState.Kind == AuthorityActionKind.CardTransfer
						? AuthorityActionContinuationStep.CardTransfer
						: AuthorityActionContinuationStep.QuestionRewardDelivery)
				: (result.CompletesTurn
					? AuthorityActionContinuationStep.TurnAdvancement
					: AuthorityActionContinuationStep.Complete),
			Phase = pendingSelection
				? GetAuthorityPhaseForPendingSelection()
				: AuthorityActionPhase.Finalizing,
			RemainingAttachmentRewardQueue = GetRemainingAttachmentRewardIds(),
			RemainingAttachmentRewardQueueSeats = GetRemainingAttachmentRewardQueueSeats(),
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
			PendingItemSelectionMode.HandLimitDiscard => AuthorityActionPhase.WaitingDiscard,
			PendingItemSelectionMode.AttachmentRewardHandCard => AuthorityActionPhase.WaitingAttachment,
			_ => AuthorityActionPhase.Applying,
		};
		SetNetworkAuthorityActionSelection(phase);
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
		if (state.Phase == AuthorityActionPhase.Prepared)
		{
			if (!ulong.TryParse(
					state.RngStateBeforeAction,
					NumberStyles.None,
					CultureInfo.InvariantCulture,
					out ulong rngState)
				|| rngState == 0)
			{
				GD.PushError($"Prepared authority action {state.ActionId} has invalid RNG state.");
				CompleteNetworkAuthorityAction();
				return;
			}

			_rng.State = rngState;
			MarkNetworkAuthorityActionApplying();
			await ResumeApplyingAuthorityActionAsync(_networkAuthorityActionState);
			return;
		}
		if (state.Phase == AuthorityActionPhase.Applying && state.PrimaryMutationApplied)
		{
			if (state.Kind == AuthorityActionKind.CardTransfer && !state.TransferMutationApplied)
			{
				await ResumeCardTransferAuthorityActionAsync(state);
				return;
			}
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

		if (state.Phase is AuthorityActionPhase.WaitingDiscard
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
			&& state.BaseHandIndex < _playerHands[state.ActorSeat].Count
			&& string.Equals(
				state.BaseCardCode,
				ToNetworkCardCode(_playerHands[state.ActorSeat][state.BaseHandIndex]),
				StringComparison.Ordinal))
		{
			await PlayNetworkHostHandCardAndBroadcastAsync(
				state.ActorSeat,
				state.BaseHandIndex,
				state.TurnGeneration);
			return;
		}

		if (state.Kind is AuthorityActionKind.ItemCard or AuthorityActionKind.CardTransfer
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

	private async Task ResumeCardTransferAuthorityActionAsync(AuthorityActionState state)
	{
		if (state.TransferMutationApplied)
		{
			_networkAuthorityActionState = state with { Phase = AuthorityActionPhase.Finalizing };
			CommitNetworkAuthorityActionCheckpoint();
			await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
			return;
		}

		ItemCard? restoredItemCard = CreateItemCard(state.ItemOrRewardId);
		if (!restoredItemCard.HasValue)
		{
			CompleteNetworkAuthorityAction();
			return;
		}

		PlayCardResult result = (await ResolveItemCardOnPlayAsync(
			restoredItemCard.Value,
			state.ActorSeat,
			state.ItemHandIndex,
			state.TargetSeat,
			state.TargetHandIndex,
			Vector2.Zero,
			state.TurnGeneration)) with { CompletesTurn = false };
		SetNetworkAuthorityActionMutationResult(result);
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		CheckForWinner();
		await FinishPendingItemCardAsync(result, state.ActorSeat, state.TurnGeneration);
	}

	private async Task FinishPendingItemCardAsync(
		PlayCardResult result,
		int ownerPlayerIndex,
		int turnSequenceGeneration)
	{
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			_isProcessingCardPlay = false;
			RenderPlayerHands();
			return;
		}

		await ResolveActionCompletionAsync(ownerPlayerIndex, result);

		_isProcessingCardPlay = false;
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
		CompleteNetworkAuthorityAction();
		if (IsNetworkMultiplayerMode())
		{
			ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded();
			return;
		}

		if (result.CompletesTurn && !result.Captured)
		{
			await ResolveOpponentTurnsAsync(turnSequenceGeneration);
		}
	}

	private async Task ResumeCommittedAuthoritySelectionAsync(AuthorityActionState state)
	{
		if (state.Phase == AuthorityActionPhase.WaitingAttachment)
		{
			if (state.AttachmentMutationApplied)
			{
				CommitNetworkAuthorityAttachmentMutation();
				await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
				return;
			}

			await CompleteMagnetAttachmentSelectionAsync(state.SelectionIndex);
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
			else if (RemoveHandCardFromGame(state.ActorSeat, state.SelectionIndex))
			{
				CommitNetworkAuthorityDiscardMutation();
			}
			else
			{
				CompleteNetworkAuthorityAction();
			}

			DisposeActiveSelectionOverlay();
			ClearPendingItemSelectionState();
			RenderPlayerHands();

			if (_networkAuthorityActionState.IsActive
				&& _networkAuthorityActionState.Kind == AuthorityActionKind.CardTransfer
				&& _networkAuthorityActionState.Phase == AuthorityActionPhase.Applying
				&& !_networkAuthorityActionState.TransferMutationApplied)
			{
				await ResumeCardTransferAuthorityActionAsync(_networkAuthorityActionState);
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
			if (AuthorityActionStateRules.HasCommittedSelection(_networkAuthorityActionState))
			{
				await ResumeCommittedAuthoritySelectionAsync(_networkAuthorityActionState);
				return;
			}

			RefreshNetworkPendingSelectionOverlay();
			BeginNetworkAiProxyTurnForPendingSelectionIfNeeded();
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
					await CompleteMagnetAttachmentSelectionAsync(_networkAuthorityActionState.SelectionIndex);
					return;
				}

				RefreshNetworkPendingSelectionOverlay();
				BeginNetworkAiProxyTurnForPendingSelectionIfNeeded();
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
			&& !IsGameOver()
			&& _currentTurnPlayerIndex == state.ActorSeat)
		{
			await WaitForAuthorityTransactionFaultAsync("before_turn_advance");
			_networkAuthorityActionState = state with { Phase = AuthorityActionPhase.Applying };
			await ResolveActionCompletionAsync(
				state.ActorSeat,
				new PlayCardResult(
					state.Played,
					state.Captured,
					state.DeckCollected,
					CompletesTurn: state.CompletesTurn));
			await WaitForAuthorityTransactionFaultAsync("after_turn_advance");
		}

		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			Phase = AuthorityActionPhase.Finalizing,
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
		await WaitForNextBoardFrameAsync();
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
			AuthorityActionPhase.WaitingDiscard => PendingItemSelectionMode.HandLimitDiscard,
			AuthorityActionPhase.WaitingAttachment => PendingItemSelectionMode.AttachmentRewardHandCard,
			_ => PendingItemSelectionMode.None,
		};
		_isProcessingCardPlay = true;

		if (state.Phase == AuthorityActionPhase.WaitingDiscard && _pendingHandLimitDiscardCompletion == null)
		{
			var completionSource = new TaskCompletionSource<int>(
				TaskCreationOptions.RunContinuationsAsynchronously);
			_pendingHandLimitDiscardCompletion = completionSource;
			TrackBoardTask(ResumeRestoredHandLimitDiscardSelectionAsync(
				completionSource,
				state.ActionId,
				state.TurnGeneration), nameof(ResumeRestoredHandLimitDiscardSelectionAsync));
		}

		if (state.Phase == AuthorityActionPhase.WaitingAttachment && _pendingMagnetAttachmentCompletion == null)
		{
			var completionSource = new TaskCompletionSource<bool>(
				TaskCreationOptions.RunContinuationsAsynchronously);
			_pendingMagnetAttachmentCompletion = completionSource;
			TrackBoardTask(ResumeRestoredAttachmentSelectionAsync(
				completionSource,
				state.ActionId,
				state.TurnGeneration), nameof(ResumeRestoredAttachmentSelectionAsync));
		}
	}

	private async Task ResumeRestoredHandLimitDiscardSelectionAsync(
		TaskCompletionSource<int> completionSource,
		long actionId,
		int turnGeneration)
	{
		try
		{
			int selectionIndex = await completionSource.Task;
			await WaitForNextBoardFrameAsync();
			AuthorityActionState state = _networkAuthorityActionState;
			if (!IsNetworkHost()
				|| !ReferenceEquals(_pendingHandLimitDiscardCompletion, completionSource)
				|| selectionIndex < 0
				|| state.ActionId != actionId
				|| state.TurnGeneration != turnGeneration
				|| state.Phase != AuthorityActionPhase.WaitingDiscard
				|| state.SelectionIndex != selectionIndex
				|| !AuthorityActionStateRules.HasCommittedSelection(state))
			{
				return;
			}

			await ResumeCommittedAuthoritySelectionAsync(state);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !IsBoardStopping)
		{
			GD.PushError($"Restored hand-limit discard resume failed: {ex}");
		}
	}

	private async Task ResumeRestoredAttachmentSelectionAsync(
		TaskCompletionSource<bool> completionSource,
		long actionId,
		int turnGeneration)
	{
		try
		{
			await completionSource.Task;
			await WaitForNextBoardFrameAsync();
			AuthorityActionState state = _networkAuthorityActionState;
			if (!IsNetworkHost()
				|| state.ActionId != actionId
				|| state.TurnGeneration != turnGeneration
				|| !state.AttachmentMutationApplied
				|| state.Phase == AuthorityActionPhase.WaitingAttachment)
			{
				return;
			}

			await ResumeAuthorityActionAsync();
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !IsBoardStopping)
		{
			GD.PushError($"Restored attachment selection resume failed: {ex}");
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
		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& AuthorityActionStateRules.IsStableCheckpointState(_networkAuthorityActionState))
		{
			GetNetworkManager()?.CommitAuthorityCheckpointFromBoard(this);
		}
	}
}
