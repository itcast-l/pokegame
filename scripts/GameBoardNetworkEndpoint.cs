using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

internal sealed class GameBoardNetworkEndpoint : INetworkAuthorityEndpoint, INetworkReplicaEndpoint, INetworkPresentationEndpoint
{
	private readonly GameBoardScene _board;
	private bool _active = true;

	public GameBoardNetworkEndpoint(GameBoardScene board)
	{
		_board = board ?? throw new ArgumentNullException(nameof(board));
	}

	public bool IsAlive => _active && GodotObject.IsInstanceValid(_board) && _board.IsInsideTree();
	public bool IsGameOver => IsAlive && _board.IsNetworkGameOver();
	public bool IsMatchClaimable => IsAlive && _board.IsNetworkMatchClaimable();
	public bool IsMigrationInputLocked => IsAlive && _board.IsNetworkMigrationInputLocked;
	public bool CanPublishSnapshot => IsAlive && _board.CanPublishNetworkBoardSnapshot;
	public long AuthorityEpoch => IsAlive ? _board.NetworkAuthorityEpoch : 0;
	public long CheckpointId => IsAlive ? _board.NetworkCheckpointId : -1;
	public long CurrentAuthorityActionId => IsAlive ? _board.CurrentNetworkAuthorityActionId : 0;

	public void Deactivate()
	{
		_active = false;
	}

	public int CreateNextSnapshotId() => IsAlive ? _board.CreateNextNetworkSnapshotId() : -1;

	public bool TryStartHandCardPlay(int actorSeatIndex, int handIndex, int turnSequenceGeneration, long clientActionId, out MultiplayerBoardActionRejectReason rejectReason)
	{
		if (!IsAlive)
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}
		return _board.TryStartNetworkHandCardPlayFromHost(actorSeatIndex, handIndex, turnSequenceGeneration, clientActionId, out rejectReason);
	}

	public bool TryStartItemCardPlay(int actorSeatIndex, int itemHandIndex, int turnSequenceGeneration, long clientActionId, out MultiplayerBoardActionRejectReason rejectReason)
	{
		if (!IsAlive)
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}
		return _board.TryStartNetworkItemCardPlayFromHost(actorSeatIndex, itemHandIndex, turnSequenceGeneration, clientActionId, out rejectReason);
	}

	public bool TryStartPendingItemSelection(int actorSeatIndex, int selectionIndex, int turnSequenceGeneration, long clientActionId, out MultiplayerBoardActionRejectReason rejectReason)
	{
		if (!IsAlive)
		{
			rejectReason = MultiplayerBoardActionRejectReason.ActionInProgress;
			return false;
		}
		return _board.TryStartNetworkPendingItemSelectionFromHost(actorSeatIndex, selectionIndex, turnSequenceGeneration, clientActionId, out rejectReason);
	}

	public bool TryBuildCheckpoint(IReadOnlyDictionary<int, long> lastAcceptedClientActionIds, long candidateCheckpointId, out AuthorityCheckpointEnvelope envelope, out string error)
	{
		if (!IsAlive)
		{
			envelope = AuthorityCheckpointEnvelope.Empty;
			error = "board_endpoint_unavailable";
			return false;
		}
		return _board.TryBuildAuthorityCheckpointEnvelope(lastAcceptedClientActionIds, candidateCheckpointId, out envelope, out error);
	}

	public void CommitCheckpoint(AuthorityCheckpointEnvelope envelope) => _board.CommitNetworkCheckpointEnvelope(envelope);
	public void AdvanceCheckpointSequence(long checkpointId) => _board.AdvanceNetworkCheckpointSequence(checkpointId);
	public Godot.Collections.Dictionary BuildSnapshotForSeat(int recipientSeatIndex, int snapshotId) => _board.BuildNetworkBoardSnapshotForSeat(recipientSeatIndex, snapshotId);
	public bool TryStageVisualDispatch(int peerId, Godot.Collections.Dictionary payload) => IsAlive && _board.TryStageNetworkAuthorityVisualDispatch(peerId, payload);
	public bool TryStageHostVisualEvent(Godot.Collections.Dictionary payload) => IsAlive && _board.TryStageNetworkAuthorityHostVisualEvent(payload);
	public Task ResumeAuthorityActionAsync() => IsAlive ? _board.ResumeAuthorityActionAsync() : Task.CompletedTask;
	public void BeginAiProxyTurnIfNeeded() => _board.BeginNetworkAiProxyTurnIfNeeded();
	public bool TryInitialize(NetworkBoardInitialization initialization) => IsAlive && _board.TryApplyNetworkBoardInitialize(initialization);
	public void ApplySnapshot(Godot.Collections.Dictionary snapshot) => _board.ApplyNetworkBoardSnapshot(snapshot);
	public void QueueVisualEvent(Godot.Collections.Dictionary payload) => _board.QueueNetworkCardPlayedEvent(payload);
	public void RejectAction(string reason, int turnSequenceGeneration, int rejectedHandIndex) => _board.RejectNetworkBoardAction(reason, turnSequenceGeneration, rejectedHandIndex);
	public void ConfirmAction(long actionId, long clientActionId, long checkpointId, int snapshotId, int turnSequenceGeneration) => _board.ConfirmNetworkBoardActionCommitted(actionId, clientActionId, checkpointId, snapshotId, turnSequenceGeneration);
	public bool TryRestoreCheckpoint(ReadOnlySpan<byte> payload, string expectedHash, out string error) => _board.TryRestoreAuthorityCheckpoint(payload, expectedHash, out error);
	public void AdvanceAuthorityEpoch(long authorityEpoch) => _board.AdvanceNetworkAuthorityEpoch(authorityEpoch);
	public void BeginHostMigration() => _board.BeginNetworkHostMigrationVisualState();
	public void CompleteHostMigration() => _board.CompleteNetworkHostMigrationVisualState();
}
