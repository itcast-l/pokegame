using System.Collections.Generic;
using System.Linq;

internal enum MultiplayerBoardActionRejectReason
{
	None,
	UnknownSender,
	GameOver,
	ActionInProgress,
	WrongTurn,
	InvalidHandIndex,
	StaleTurnGeneration,
}

internal readonly record struct MultiplayerBoardActionValidation(
	bool Accepted,
	MultiplayerBoardActionRejectReason RejectReason)
{
	internal static MultiplayerBoardActionValidation Accept()
	{
		return new MultiplayerBoardActionValidation(true, MultiplayerBoardActionRejectReason.None);
	}

	internal static MultiplayerBoardActionValidation Reject(MultiplayerBoardActionRejectReason reason)
	{
		return new MultiplayerBoardActionValidation(false, reason);
	}
}

internal static class MultiplayerBoardSyncRules
{
	internal const int ProtocolVersion = 1;

	internal static bool TryResolveSeatFromPeer(
		IReadOnlyCollection<NetworkPlayerPublicState> players,
		int senderPeerId,
		out int seatIndex)
	{
		NetworkPlayerPublicState player = players.FirstOrDefault(player => player.PeerId == senderPeerId);
		seatIndex = player.PeerId == 0 ? -1 : player.SeatIndex;
		return seatIndex >= 0;
	}

	internal static bool CanAcceptSceneLoadAck(
		IReadOnlyCollection<NetworkPlayerPublicState> players,
		int senderPeerId,
		int reportedSeatIndex,
		out int senderSeatIndex)
	{
		if (!TryResolveSeatFromPeer(players, senderPeerId, out senderSeatIndex))
		{
			return false;
		}

		return senderSeatIndex == reportedSeatIndex;
	}

	internal static MultiplayerBoardActionValidation ValidatePlayHandCardRequest(
		int senderSeatIndex,
		int currentTurnPlayerIndex,
		int requestedHandIndex,
		int handCount,
		int requestTurnSequenceGeneration,
		int currentTurnSequenceGeneration,
		bool isGameOver,
		bool isActionInProgress)
	{
		if (senderSeatIndex < 0)
		{
			return MultiplayerBoardActionValidation.Reject(MultiplayerBoardActionRejectReason.UnknownSender);
		}

		if (isGameOver)
		{
			return MultiplayerBoardActionValidation.Reject(MultiplayerBoardActionRejectReason.GameOver);
		}

		if (isActionInProgress)
		{
			return MultiplayerBoardActionValidation.Reject(MultiplayerBoardActionRejectReason.ActionInProgress);
		}

		if (senderSeatIndex != currentTurnPlayerIndex)
		{
			return MultiplayerBoardActionValidation.Reject(MultiplayerBoardActionRejectReason.WrongTurn);
		}

		if (requestedHandIndex < 0 || requestedHandIndex >= handCount)
		{
			return MultiplayerBoardActionValidation.Reject(MultiplayerBoardActionRejectReason.InvalidHandIndex);
		}

		if (requestTurnSequenceGeneration != currentTurnSequenceGeneration)
		{
			return MultiplayerBoardActionValidation.Reject(MultiplayerBoardActionRejectReason.StaleTurnGeneration);
		}

		return MultiplayerBoardActionValidation.Accept();
	}

	internal static bool ShouldIncludePrivateHandCards(int cardOwnerSeatIndex, int recipientSeatIndex)
	{
		return cardOwnerSeatIndex == recipientSeatIndex;
	}
}
