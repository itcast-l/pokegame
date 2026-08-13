using System;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
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
			if (_networkAuthorityRuleTransactionActive
				&& _networkAuthorityActionState.IsActive
				&& !HasPendingItemSelection())
			{
				if (_networkAuthorityActionState.Kind == AuthorityActionKind.QuestionReward
					&& _networkAuthorityActionState.Phase == AuthorityActionPhase.Applying)
				{
					await ResumeQuestionRewardAuthorityActionAsync(_networkAuthorityActionState);
				}
				else if (_networkAuthorityActionState.Phase == AuthorityActionPhase.Finalizing)
				{
					await ResumeFinalizingAuthorityActionAsync(_networkAuthorityActionState);
				}
			}
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
			else if (TryResolveHandLimitMandatorySelection(
				state.ActorSeat,
				state.SelectionIndex,
				state.TurnGeneration))
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

		if (state.PendingRewardRequiresAttachment || !GetPendingAttachmentRewards().IsEmpty)
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
				&& GetPendingAttachmentRewards().IsEmpty)
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

			if (!TrySubmitGrantItemToHandRuleCommand(
				state.PendingRewardSeat,
				rewardCard.Value.Id,
				MaxTotalHandCards,
				discardDisplayIndex: -1,
				expectedDiscardCardRuntimeInstanceId: 0,
				expectedDiscardItemId: string.Empty,
				state.TurnGeneration,
				out RuleCommandResult grantResult))
			{
				CompleteNetworkAuthorityAction();
				return;
			}

			int handIndex = grantResult.Mutation.TargetIndex;
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
		int pendingOwnerIndex = isAttachmentReward && state.PendingRewardSeat >= 0
			? state.PendingRewardSeat
			: state.ActorSeat;
		string pendingItemCardId = isAttachmentReward && !string.IsNullOrEmpty(state.PendingRewardItemId)
			? state.PendingRewardItemId
			: state.ItemOrRewardId;
		bool pendingStateReady = state.Phase switch
		{
			AuthorityActionPhase.WaitingDiscard => TryBeginHandLimitMandatorySelection(
				pendingOwnerIndex,
				state.TurnGeneration),
			AuthorityActionPhase.WaitingAttachment => TryBeginAttachmentMandatorySelection(
				pendingOwnerIndex,
				pendingItemCardId,
				state.TurnGeneration),
			_ => false,
		};
		if (!pendingStateReady)
		{
			return;
		}
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
		if (_authorityRuleSession == null
			|| !_authorityRuleSession.Capabilities.Contains(MatchCapability.ItemRules))
		{
			return;
		}

		TrySubmitSetPendingAttachmentRewardsRuleCommand(
			BuildPendingAttachmentRewardsFromAuthorityAction(state),
			_turnSequenceGeneration);
	}
}
