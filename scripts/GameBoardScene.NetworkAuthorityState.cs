using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private AuthorityActionState _networkAuthorityActionState = AuthorityActionState.Empty;
	private long _nextNetworkAuthorityActionId;
	private TaskCompletionSource<bool>? _authorityTransactionFaultWaiter;

	internal AuthorityActionState NetworkAuthorityActionState => _networkAuthorityActionState;
	internal bool CanPublishNetworkBoardSnapshot =>
		AuthorityActionStateRules.IsStableCheckpointState(_networkAuthorityActionState);

	private Task WaitForAuthorityTransactionFaultAsync(
		string point,
		bool requireActiveAction = true)
	{
		string requestedPoint = OS.GetEnvironment("CODEX_AUTHORITY_FAULT_POINT");
		if (string.IsNullOrWhiteSpace(requestedPoint)
			|| !string.Equals(requestedPoint.Trim(), point, StringComparison.Ordinal)
			|| !IsNetworkMultiplayerMode()
			|| !IsNetworkHost()
			|| _networkAuthorityEpoch != 0
			|| requireActiveAction && !_networkAuthorityActionState.IsActive)
		{
			return Task.CompletedTask;
		}
		if (_networkAuthorityRuleTransactionActive)
		{
			_networkAuthorityDeferredFaultPoint = point;
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

		bool hasPendingAttachmentRewards = !GetPendingAttachmentRewards().IsEmpty;
		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			Phase = hasPendingAttachmentRewards
				? AuthorityActionPhase.Applying
				: AuthorityActionPhase.Finalizing,
			ContinuationStep = hasPendingAttachmentRewards
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

	internal void MarkNetworkAuthorityAttachmentRewardInFlight(PendingAttachmentRewardState reward)
	{
		if (!_networkAuthorityActionState.IsActive)
		{
			return;
		}

		_networkAuthorityActionState = _networkAuthorityActionState with
		{
			ContinuationStep = AuthorityActionContinuationStep.AttachmentQueue,
			PendingRewardItemId = reward.ItemId,
			PendingRewardSeat = reward.Seat,
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

	internal void RestoreNetworkAuthorityActionState(
		AuthorityActionState state,
		long lastAuthorityActionId = 0)
	{
		if (!AuthorityActionStateRules.IsValid(state, player_count)
			|| lastAuthorityActionId < 0
			|| lastAuthorityActionId < state.ActionId)
		{
			return;
		}

		_networkAuthorityActionState = state;
		_nextNetworkAuthorityActionId = Math.Max(
			_nextNetworkAuthorityActionId,
			Math.Max(state.ActionId, lastAuthorityActionId));
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

	private static ImmutableArray<PendingAttachmentRewardState>
		BuildPendingAttachmentRewardsFromAuthorityAction(AuthorityActionState state)
	{
		if (state is null)
		{
			return ImmutableArray<PendingAttachmentRewardState>.Empty;
		}

		bool hasPendingReward = !string.IsNullOrWhiteSpace(state.PendingRewardItemId)
			&& state.PendingRewardSeat >= 0;
		var rewards = ImmutableArray.CreateBuilder<PendingAttachmentRewardState>(
			state.RemainingAttachmentRewardQueue.Length + (hasPendingReward ? 1 : 0));
		if (hasPendingReward)
		{
			rewards.Add(new PendingAttachmentRewardState(
				state.PendingRewardSeat,
				state.PendingRewardItemId));
		}

		for (int index = 0; index < state.RemainingAttachmentRewardQueue.Length; index++)
		{
			int seat = state.RemainingAttachmentRewardQueueSeats.Length
				== state.RemainingAttachmentRewardQueue.Length
				? state.RemainingAttachmentRewardQueueSeats[index]
				: state.ActorSeat;
			rewards.Add(new PendingAttachmentRewardState(
				seat,
				state.RemainingAttachmentRewardQueue[index]));
		}

		return rewards.MoveToImmutable();
	}

	private string[] GetRemainingAttachmentRewardIds()
	{
		ImmutableArray<PendingAttachmentRewardState> rewards = GetPendingAttachmentRewards();
		if (rewards.IsEmpty)
		{
			return Array.Empty<string>();
		}

		var result = new string[rewards.Length];
		int index = 0;
		foreach (PendingAttachmentRewardState reward in rewards)
		{
			result[index++] = reward.ItemId;
		}

		return result;
	}

	private int[] GetRemainingAttachmentRewardQueueSeats()
	{
		ImmutableArray<PendingAttachmentRewardState> rewards = GetPendingAttachmentRewards();
		if (rewards.IsEmpty)
		{
			return Array.Empty<int>();
		}

		var result = new int[rewards.Length];
		int index = 0;
		foreach (PendingAttachmentRewardState reward in rewards)
		{
			result[index++] = reward.Seat;
		}

		return result;
	}

	private void CommitNetworkAuthorityActionCheckpoint()
	{
		if (!_networkAuthorityRuleTransactionActive
			&& IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& AuthorityActionStateRules.IsStableCheckpointState(_networkAuthorityActionState))
		{
			GetNetworkManager()?.CommitAuthorityCheckpointFromBoard();
		}
	}
}
