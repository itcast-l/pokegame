using System;
using System.Collections.Generic;
using Godot;

public partial class NetworkManager
{
	private void RpcRequestPlayHandCard(int handIndex, int turnSequenceGeneration)
	{
		NetworkRpcContext context = GetCurrentRpcContext();
		RpcRequestPlayHandCard(context.SessionId, context.MatchId, context.AuthorityEpoch, handIndex, turnSequenceGeneration, 0);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPlayHandCard(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int handIndex,
		int turnSequenceGeneration,
		long clientActionId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}
		if (!AllowPeerRequest(NetworkRequestCategory.BoardAction))
		{
			return;
		}

		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, handIndex);
			return;
		}

		if (IsDuplicateOrOlderClientAction(actorSeatIndex, clientActionId))
		{
			if (GetTree().CurrentScene is GameBoardScene duplicateBoard
				&& !duplicateBoard.IsNetworkGameOver())
			{
				SendCorrectiveBoardSnapshotIfAllowed(duplicateBoard, senderPeerId, actorSeatIndex);
			}

			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, handIndex);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat
				&& !gameBoardForRejectedSeat.IsNetworkGameOver())
			{
				SendCorrectiveBoardSnapshotIfAllowed(gameBoardForRejectedSeat, senderPeerId, actorSeatIndex);
			}
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, handIndex);
			return;
		}

		if (!gameBoard.TryStartNetworkHandCardPlayFromHost(
			actorSeatIndex,
			handIndex,
			turnSequenceGeneration,
			clientActionId,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, handIndex);
			if (!gameBoard.IsNetworkGameOver())
			{
				SendCorrectiveBoardSnapshotIfAllowed(gameBoard, senderPeerId, actorSeatIndex);
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPlayItemCard(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int itemHandIndex,
		int turnSequenceGeneration,
		long clientActionId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}
		if (!AllowPeerRequest(NetworkRequestCategory.BoardAction))
		{
			return;
		}

		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, itemHandIndex);
			return;
		}

		if (IsDuplicateOrOlderClientAction(actorSeatIndex, clientActionId))
		{
			if (GetTree().CurrentScene is GameBoardScene duplicateBoard
				&& !duplicateBoard.IsNetworkGameOver())
			{
				SendCorrectiveBoardSnapshotIfAllowed(duplicateBoard, senderPeerId, actorSeatIndex);
			}

			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, itemHandIndex);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat
				&& !gameBoardForRejectedSeat.IsNetworkGameOver())
			{
				SendCorrectiveBoardSnapshotIfAllowed(gameBoardForRejectedSeat, senderPeerId, actorSeatIndex);
			}
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, itemHandIndex);
			return;
		}

		if (!gameBoard.TryStartNetworkItemCardPlayFromHost(
			actorSeatIndex,
			itemHandIndex,
			turnSequenceGeneration,
			clientActionId,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, itemHandIndex);
			if (!gameBoard.IsNetworkGameOver())
			{
				SendCorrectiveBoardSnapshotIfAllowed(gameBoard, senderPeerId, actorSeatIndex);
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPendingItemSelection(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int selectionIndex,
		int turnSequenceGeneration,
		long clientActionId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}
		if (!AllowPeerRequest(NetworkRequestCategory.BoardAction))
		{
			return;
		}

		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, -1);
			return;
		}

		if (IsDuplicateOrOlderClientAction(actorSeatIndex, clientActionId))
		{
			if (GetTree().CurrentScene is GameBoardScene duplicateBoard
				&& !duplicateBoard.IsNetworkGameOver())
			{
				SendCorrectiveBoardSnapshotIfAllowed(duplicateBoard, senderPeerId, actorSeatIndex);
			}

			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, -1);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat
				&& !gameBoardForRejectedSeat.IsNetworkGameOver())
			{
				SendCorrectiveBoardSnapshotIfAllowed(gameBoardForRejectedSeat, senderPeerId, actorSeatIndex);
			}
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, -1);
			return;
		}

		if (!gameBoard.TryStartNetworkPendingItemSelectionFromHost(
			actorSeatIndex,
			selectionIndex,
			turnSequenceGeneration,
			clientActionId,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, -1);
			if (!gameBoard.IsNetworkGameOver())
			{
				SendCorrectiveBoardSnapshotIfAllowed(gameBoard, senderPeerId, actorSeatIndex);
			}
		}
	}

	private void SendCorrectiveBoardSnapshotIfAllowed(
		GameBoardScene gameBoard,
		int peerId,
		int seatIndex)
	{
		double now = Time.GetTicksMsec() / 1000.0;
		if (_lastCorrectiveSnapshotAtByPeer.TryGetValue(peerId, out double lastSentAt)
			&& now - lastSentAt < 0.5)
		{
			return;
		}

		_lastCorrectiveSnapshotAtByPeer[peerId] = now;
		SendBoardSnapshotToPeerFromHost(
			gameBoard,
			peerId,
			seatIndex,
			gameBoard.CreateNextNetworkSnapshotId());
	}

	private void SendRejectBoardAction(
		int peerId,
		MultiplayerBoardActionRejectReason reason,
		int turnSequenceGeneration,
		int rejectedHandIndex)
	{
		if (peerId <= 0)
		{
			return;
		}

		NetworkRpcContext context = GetCurrentRpcContext();
		RpcId(peerId, MethodName.RpcRejectBoardAction, context.SessionId, context.MatchId, context.AuthorityEpoch, reason.ToString(), turnSequenceGeneration, rejectedHandIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRejectBoardAction(string sessionId, string matchId, long authorityEpoch, string reason, int turnSequenceGeneration, int rejectedHandIndex)
	{
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.RejectNetworkBoardAction(reason, turnSequenceGeneration, rejectedHandIndex);
		}
	}


}
