using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private readonly List<ImmutableNetworkVisualDispatch> _networkAuthorityStagedVisualEvents = new();
	private readonly List<NetworkVisualEvent> _networkAuthorityStagedHostVisualEvents = new();
	private int _networkAuthorityHostVisualDecodeFailureCount;
	private int _networkAuthorityHostVisualPlaybackCount;
	private bool _networkAuthorityRuleTransactionActive;
	private Vector2 _networkAuthorityRuleVisualStartGlobalPosition;
	private Vector2 _networkAuthorityRuleCenterArenaGlobalPosition;
	private Rect2 _networkAuthorityRuleTableLayoutRect;
	private string _networkAuthorityDeferredFaultPoint = string.Empty;
	private TaskCompletionSource<bool>? _networkAuthorityDeferredMagnetCompletion;
	private TaskCompletionSource<int>? _networkAuthorityDeferredDiscardCompletion;

	internal bool IsNetworkAuthorityRuleTransactionActive => _networkAuthorityRuleTransactionActive;
	internal long CurrentNetworkAuthorityActionId => _networkAuthorityActionState.ActionId;

	internal bool TryStageNetworkAuthorityVisualDispatch(
		int peerId,
		Godot.Collections.Dictionary payload)
	{
		if (!_networkAuthorityRuleTransactionActive)
		{
			return false;
		}

		_networkAuthorityStagedVisualEvents.Add(new ImmutableNetworkVisualDispatch(peerId, payload));
		return true;
	}

	internal bool TryStageNetworkAuthorityHostVisualEvent(
		Godot.Collections.Dictionary payload)
	{
		if (!_networkAuthorityRuleTransactionActive)
		{
			return false;
		}

		Godot.Collections.Dictionary stampedPayload = payload.Duplicate(deep: true);
		stampedPayload[NetworkBoardVisualEventTypes.SessionIdKey] = network_session_id;
		stampedPayload[NetworkBoardVisualEventTypes.MatchIdKey] = network_match_id;
		stampedPayload[NetworkBoardVisualEventTypes.AuthorityEpochKey] = _networkAuthorityEpoch;
		stampedPayload[NetworkBoardVisualEventTypes.CheckpointIdKey] = _networkCheckpointId;
		stampedPayload[NetworkBoardVisualEventTypes.ActionIdKey] = CurrentNetworkAuthorityActionId;
		if (!TryDecodeNetworkVisualEvent(
			stampedPayload,
			out NetworkVisualEvent visualEvent,
			authorityHostVisualOnly: true))
		{
			_networkAuthorityHostVisualDecodeFailureCount = NetworkVisualQueuePolicy.SaturatingAdd(
				_networkAuthorityHostVisualDecodeFailureCount,
				1);
			return true;
		}

		_networkAuthorityStagedHostVisualEvents.Add(visualEvent with
		{
			IsAuthorityHostVisualOnly = true,
			AuthorityHostStartGlobalPosition = _networkAuthorityRuleVisualStartGlobalPosition,
		});
		return true;
	}

	private Vector2 GetNetworkAuthorityRuleVisualStartGlobalPosition(Vector2 fallback)
	{
		return _networkAuthorityRuleTransactionActive
			? _networkAuthorityRuleVisualStartGlobalPosition
			: fallback;
	}

	private Vector2 GetAuthorityRuleSafeCenterArenaGlobalPosition()
	{
		return _networkAuthorityRuleTransactionActive
			? _networkAuthorityRuleCenterArenaGlobalPosition
			: _centerArena.GlobalPosition;
	}

	private Rect2 GetAuthorityRuleSafeTableLayoutRect()
	{
		return _networkAuthorityRuleTransactionActive
			? _networkAuthorityRuleTableLayoutRect
			: GetTableCardLayoutRect();
	}

	private void BeginNetworkAuthorityRuleTransaction(Vector2 visualStartGlobalPosition)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			throw new InvalidOperationException("A network authority rule transaction is already active.");
		}

		_networkAuthorityStagedVisualEvents.Clear();
		_networkAuthorityStagedHostVisualEvents.Clear();
		_networkAuthorityDeferredFaultPoint = string.Empty;
		_networkAuthorityDeferredMagnetCompletion = null;
		_networkAuthorityDeferredDiscardCompletion = null;
		_networkAuthorityRuleVisualStartGlobalPosition = visualStartGlobalPosition;
		_networkAuthorityRuleCenterArenaGlobalPosition = _centerArena.GlobalPosition;
		_networkAuthorityRuleTableLayoutRect = GetTableCardLayoutRect();
		_networkAuthorityRuleTransactionActive = true;
	}

	private ImmutableNetworkVisualDispatch[] EndNetworkAuthorityRuleTransaction(
		out NetworkVisualEvent[] hostVisualEvents)
	{
		_networkAuthorityRuleTransactionActive = false;
		ImmutableNetworkVisualDispatch[] visualEvents = _networkAuthorityStagedVisualEvents.ToArray();
		hostVisualEvents = _networkAuthorityStagedHostVisualEvents.ToArray();
		_networkAuthorityStagedVisualEvents.Clear();
		_networkAuthorityStagedHostVisualEvents.Clear();
		return visualEvents;
	}

	private static T RequireSynchronousAuthorityRuleResult<T>(Task<T> task, string label)
	{
		if (!task.IsCompleted)
		{
			throw new InvalidOperationException(
				$"Authority rule transaction '{label}' attempted to suspend asynchronously.");
		}

		return task.GetAwaiter().GetResult();
	}

	private static void RequireSynchronousAuthorityRuleCompletion(Task task, string label)
	{
		if (!task.IsCompleted)
		{
			throw new InvalidOperationException(
				$"Authority rule transaction '{label}' attempted to suspend asynchronously.");
		}

		task.GetAwaiter().GetResult();
	}

	private AuthorityRuleTransactionResult CommitNetworkAuthorityRuleTransaction(
		long actionId,
		long clientActionId,
		int turnSequenceGeneration,
		int snapshotId,
		ImmutableNetworkVisualDispatch[] visualEvents)
	{
		NetworkManager? networkManager = GetNetworkManager();
		if (networkManager == null || !CanPublishNetworkBoardSnapshot)
		{
			return AuthorityRuleTransactionResult.Rejected;
		}

		AuthorityCheckpointCommitResult checkpoint = networkManager.CommitAuthorityCheckpointFromBoard();
		if (!checkpoint.IsCheckpointCommitted)
		{
			return AuthorityRuleTransactionResult.Rejected;
		}

		return new AuthorityRuleTransactionResult(
			true,
			actionId,
			clientActionId,
			_networkAuthorityEpoch,
			checkpoint.CheckpointId,
			snapshotId,
			turnSequenceGeneration,
			visualEvents,
			_networkAuthorityDeferredFaultPoint);
	}

	private void PublishNetworkAuthorityRuleTransaction(
		int actorSeatIndex,
		AuthorityRuleTransactionResult transaction,
		NetworkVisualEvent[] hostVisualEvents)
	{
		if (!transaction.Committed)
		{
			ReleaseDeferredNetworkAuthoritySelectionWaiters();
			return;
		}

		InvalidateRenderCaches();
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();

		NetworkManager? networkManager = GetNetworkManager();
		networkManager?.PublishCommittedBoardActionFromHost(actorSeatIndex, transaction);
		QueueCommittedNetworkAuthorityHostVisualEvents(transaction, hostVisualEvents);
		PresentCommittedNetworkAuthorityMatchOutcome();
		ReleaseDeferredNetworkAuthoritySelectionWaiters();
		RefreshNetworkAuthorityHostPendingSelection();
		ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded();

		if (!string.IsNullOrWhiteSpace(transaction.DeferredFaultPoint))
		{
			TrackBoardTask(
				WaitForCommittedAuthorityTransactionFaultAsync(transaction),
				nameof(WaitForCommittedAuthorityTransactionFaultAsync));
		}
	}

	private void QueueCommittedNetworkAuthorityHostVisualEvents(
		AuthorityRuleTransactionResult transaction,
		NetworkVisualEvent[] hostVisualEvents)
	{
		foreach (NetworkVisualEvent visualEvent in hostVisualEvents)
		{
			NetworkVisualPlayback.Enqueue(visualEvent with
			{
				AuthorityEpoch = transaction.AuthorityEpoch,
				CheckpointId = transaction.CheckpointId,
				ActionId = transaction.ActionId,
				SnapshotId = transaction.SnapshotId,
			});
		}
	}

	private bool HasLocalQuestionRewardHostVisualEvent(
		int actorSeatIndex,
		NetworkVisualEvent[] hostVisualEvents)
	{
		if (!IsLocalHandPlayer(actorSeatIndex))
		{
			return false;
		}

		foreach (NetworkVisualEvent visualEvent in hostVisualEvents)
		{
			if (visualEvent.Type == NetworkBoardVisualEventTypes.QuestionReward
				&& visualEvent.Actor == actorSeatIndex
				&& !visualEvent.RequiresMagnet)
			{
				return true;
			}
		}

		return false;
	}

	private void DeferNetworkAuthoritySelectionWaiters(
		TaskCompletionSource<bool>? magnetCompletion,
		TaskCompletionSource<int>? discardCompletion)
	{
		if (!_networkAuthorityRuleTransactionActive)
		{
			magnetCompletion?.TrySetResult(true);
			discardCompletion?.TrySetResult(-1);
			return;
		}

		_networkAuthorityDeferredMagnetCompletion ??= magnetCompletion;
		_networkAuthorityDeferredDiscardCompletion ??= discardCompletion;
	}

	private void ReleaseDeferredNetworkAuthoritySelectionWaiters()
	{
		TaskCompletionSource<bool>? magnetCompletion = _networkAuthorityDeferredMagnetCompletion;
		TaskCompletionSource<int>? discardCompletion = _networkAuthorityDeferredDiscardCompletion;
		_networkAuthorityDeferredMagnetCompletion = null;
		_networkAuthorityDeferredDiscardCompletion = null;
		magnetCompletion?.TrySetResult(true);
		discardCompletion?.TrySetResult(-1);
	}

	private async Task WaitForCommittedAuthorityTransactionFaultAsync(
		AuthorityRuleTransactionResult transaction)
	{
		NetworkManager? networkManager = GetNetworkManager();
		while (!IsBoardStopping
			&& IsInsideTree()
			&& networkManager != null
			&& !networkManager.IsStandbyCheckpointAcknowledged(transaction.CheckpointId))
		{
			await WaitForBoardDelayAsync(0.05);
		}

		if (IsBoardStopping
			|| !IsInsideTree()
			|| networkManager == null
			|| !networkManager.IsStandbyCheckpointAcknowledged(transaction.CheckpointId))
		{
			return;
		}

		_authorityTransactionFaultWaiter ??= new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		GD.Print(
			$"AUTHORITY_FAULT_REACHED point={transaction.DeferredFaultPoint} "
			+ $"checkpoint={transaction.CheckpointId} "
			+ $"action={transaction.ActionId} "
			+ $"client_action={transaction.ClientActionId} "
			+ $"generation={transaction.TurnSequenceGeneration} "
			+ "phase=Committed");
		await _authorityTransactionFaultWaiter.Task;
	}

	private void RefreshNetworkAuthorityHostPendingSelection()
	{
		if (!IsNetworkHost() || !HasPendingItemSelection())
		{
			return;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard)
		{
			ShowHandLimitDiscardSelection();
		}
		else if (_pendingItemSelectionMode == PendingItemSelectionMode.AttachmentRewardHandCard)
		{
			ShowMagnetHandSelection();
		}

		BeginNetworkAiProxyTurnForPendingSelectionIfNeeded();
	}

	private bool ExecuteNetworkHostPendingSelectionRuleTransaction(
		int actorSeatIndex,
		int selectionIndex,
		int turnSequenceGeneration,
		long clientActionId,
		AuthorityActionPhase expectedPhase)
	{
		if (!_networkAuthorityActionState.IsActive
			|| _networkAuthorityActionState.Phase != expectedPhase)
		{
			return false;
		}

		long actionId = _networkAuthorityActionState.ActionId;
		int snapshotId = CreateNextNetworkSnapshotId();
		BeginNetworkAuthorityRuleTransaction(Vector2.Zero);
		_networkAuthoritativeVisualSnapshotId = snapshotId;
		_isProcessingCardPlay = true;

		try
		{
			SetNetworkAuthorityActionSelection(expectedPhase, selectionIndex);
			RequireSynchronousAuthorityRuleCompletion(
				ResumeCommittedAuthoritySelectionAsync(_networkAuthorityActionState),
				nameof(ResumeCommittedAuthoritySelectionAsync));
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
				clientActionId,
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
}
