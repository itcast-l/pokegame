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
			if (TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint duplicateBoard)
				&& !duplicateBoard.IsGameOver)
			{
				SendCorrectiveBoardSnapshotIfAllowed(duplicateBoard, senderPeerId, actorSeatIndex);
			}

			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, handIndex);
			if (TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoardForRejectedSeat)
				&& !gameBoardForRejectedSeat.IsGameOver)
			{
				SendCorrectiveBoardSnapshotIfAllowed(gameBoardForRejectedSeat, senderPeerId, actorSeatIndex);
			}
			return;
		}

		if (!TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoard))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, handIndex);
			return;
		}

		if (!gameBoard.TryStartHandCardPlay(
			actorSeatIndex,
			handIndex,
			turnSequenceGeneration,
			clientActionId,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, handIndex);
			if (!gameBoard.IsGameOver)
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
			if (TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint duplicateBoard)
				&& !duplicateBoard.IsGameOver)
			{
				SendCorrectiveBoardSnapshotIfAllowed(duplicateBoard, senderPeerId, actorSeatIndex);
			}

			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, itemHandIndex);
			if (TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoardForRejectedSeat)
				&& !gameBoardForRejectedSeat.IsGameOver)
			{
				SendCorrectiveBoardSnapshotIfAllowed(gameBoardForRejectedSeat, senderPeerId, actorSeatIndex);
			}
			return;
		}

		if (!TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoard))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, itemHandIndex);
			return;
		}

		if (!gameBoard.TryStartItemCardPlay(
			actorSeatIndex,
			itemHandIndex,
			turnSequenceGeneration,
			clientActionId,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, itemHandIndex);
			if (!gameBoard.IsGameOver)
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
			if (TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint duplicateBoard)
				&& !duplicateBoard.IsGameOver)
			{
				SendCorrectiveBoardSnapshotIfAllowed(duplicateBoard, senderPeerId, actorSeatIndex);
			}

			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, -1);
			if (TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoardForRejectedSeat)
				&& !gameBoardForRejectedSeat.IsGameOver)
			{
				SendCorrectiveBoardSnapshotIfAllowed(gameBoardForRejectedSeat, senderPeerId, actorSeatIndex);
			}
			return;
		}

		if (!TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoard))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, -1);
			return;
		}

		if (!gameBoard.TryStartPendingItemSelection(
			actorSeatIndex,
			selectionIndex,
			turnSequenceGeneration,
			clientActionId,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, -1);
			if (!gameBoard.IsGameOver)
			{
				SendCorrectiveBoardSnapshotIfAllowed(gameBoard, senderPeerId, actorSeatIndex);
			}
		}
	}

	private void SendCorrectiveBoardSnapshotIfAllowed(
		INetworkAuthorityEndpoint gameBoard,
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
			gameBoard.CreateNextSnapshotId());
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

		if (TryGetReplicaEndpoint(out INetworkReplicaEndpoint gameBoard))
		{
			gameBoard.RejectAction(reason, turnSequenceGeneration, rejectedHandIndex);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBoardActionCommitted(
		string sessionId,
		string matchId,
		long authorityEpoch,
		long actionId,
		long clientActionId,
		long checkpointId,
		int snapshotId,
		int turnSequenceGeneration)
	{
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch)
			|| actionId <= 0
			|| clientActionId <= 0
			|| checkpointId < 0
			|| snapshotId < 0
			|| turnSequenceGeneration < 0)
		{
			return;
		}

		if (TryGetReplicaEndpoint(out INetworkReplicaEndpoint gameBoard))
		{
			gameBoard.ConfirmAction(
				actionId,
				clientActionId,
				checkpointId,
				snapshotId,
				turnSequenceGeneration);
		}
	}


}
