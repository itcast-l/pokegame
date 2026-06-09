using System;

internal enum ReconnectClaimState
{
	Unclaimed,
	Claimed,
}

internal enum ReconnectClaimRejectReason
{
	None,
	MatchEnded,
	MatchMismatch,
	TokenMismatch,
	StableUserIdMismatch,
	SeatNotReclaimable,
	AlreadyClaimedByDifferentPeer,
}

internal readonly record struct ReconnectClaimRecord(
	string MatchId,
	int SeatIndex,
	string ReconnectToken,
	string StableUserId,
	int ClaimedPeerId,
	ReconnectClaimState ClaimState);

internal readonly record struct ReconnectClaimResult(
	bool Accepted,
	bool StateChanged,
	bool ShouldSendSnapshot,
	SeatControlState SeatControlState,
	ReconnectClaimRecord ClaimRecord,
	ReconnectClaimRejectReason RejectReason);

internal static class SeatControlRules
{
	internal static SeatControlState CreateInitialState(NetworkPlayerPublicState player, string reconnectToken)
	{
		return new SeatControlState(
			player.SeatIndex,
			SeatControlMode.HumanConnected,
			player.PeerId,
			player.StableUserId,
			reconnectToken);
	}

	internal static ReconnectClaimRecord CreateInitialClaimRecord(string matchId, SeatControlState state)
	{
		return new ReconnectClaimRecord(
			matchId,
			state.SeatIndex,
			state.ReconnectToken,
			state.StableUserId,
			0,
			ReconnectClaimState.Unclaimed);
	}

	internal static bool TryMarkDisconnected(
		SeatControlState current,
		int disconnectedPeerId,
		out SeatControlState next,
		out ReconnectClaimRecord? updatedClaim,
		ReconnectClaimRecord? currentClaim)
	{
		next = current;
		updatedClaim = currentClaim;
		if (current.PeerId != disconnectedPeerId)
		{
			return false;
		}

		if (current.Mode is not (SeatControlMode.HumanConnected or SeatControlMode.HumanReclaimPending))
		{
			return false;
		}

		next = current with
		{
			Mode = SeatControlMode.AiProxyReclaimable,
			PeerId = 0,
		};
		if (currentClaim.HasValue)
		{
			updatedClaim = currentClaim.Value with
			{
				ClaimedPeerId = 0,
				ClaimState = ReconnectClaimState.Unclaimed,
			};
		}

		return true;
	}

	internal static bool TryPromoteAtTurnStart(SeatControlState current, int seatIndex, out SeatControlState next)
	{
		next = current;
		if (current.SeatIndex != seatIndex || current.Mode != SeatControlMode.HumanReclaimPending)
		{
			return false;
		}

		next = current with { Mode = SeatControlMode.HumanConnected };
		return true;
	}

	internal static SeatControlState RedactReconnectTokenForSharedSnapshot(SeatControlState current)
	{
		return current with { ReconnectToken = string.Empty };
	}

	internal static ReconnectClaimResult ApplyReconnectClaim(
		string activeMatchId,
		bool isMatchActive,
		SeatControlState current,
		ReconnectClaimRecord? currentClaim,
		string requestMatchId,
		string reconnectToken,
		string stableUserId,
		int senderPeerId)
	{
		ReconnectClaimRecord claimRecord = currentClaim ?? CreateInitialClaimRecord(activeMatchId, current);
		if (!isMatchActive)
		{
			return Reject(current, claimRecord, ReconnectClaimRejectReason.MatchEnded);
		}

		if (!string.Equals(activeMatchId, requestMatchId, StringComparison.Ordinal))
		{
			return Reject(current, claimRecord, ReconnectClaimRejectReason.MatchMismatch);
		}

		if (!string.Equals(current.ReconnectToken, reconnectToken, StringComparison.Ordinal))
		{
			return Reject(current, claimRecord, ReconnectClaimRejectReason.TokenMismatch);
		}

		if (!string.Equals(current.StableUserId, stableUserId, StringComparison.Ordinal))
		{
			return Reject(current, claimRecord, ReconnectClaimRejectReason.StableUserIdMismatch);
		}

		if (current.Mode == SeatControlMode.HumanReclaimPending)
		{
			if (current.PeerId == senderPeerId)
			{
				return Accept(current, claimRecord with
				{
					ClaimedPeerId = senderPeerId,
					ClaimState = ReconnectClaimState.Claimed,
				}, stateChanged: false, shouldSendSnapshot: false);
			}

			return Reject(current, claimRecord, ReconnectClaimRejectReason.AlreadyClaimedByDifferentPeer);
		}

		if (current.Mode != SeatControlMode.AiProxyReclaimable)
		{
			return Reject(current, claimRecord, ReconnectClaimRejectReason.SeatNotReclaimable);
		}

		if (claimRecord.ClaimState == ReconnectClaimState.Claimed)
		{
			if (claimRecord.ClaimedPeerId == senderPeerId)
			{
				return Accept(current, claimRecord, stateChanged: false, shouldSendSnapshot: false);
			}

			return Reject(current, claimRecord, ReconnectClaimRejectReason.AlreadyClaimedByDifferentPeer);
		}

		SeatControlState nextState = current with
		{
			Mode = SeatControlMode.HumanReclaimPending,
			PeerId = senderPeerId,
		};
		ReconnectClaimRecord nextClaim = claimRecord with
		{
			ClaimedPeerId = senderPeerId,
			ClaimState = ReconnectClaimState.Claimed,
		};
		return Accept(nextState, nextClaim, stateChanged: true, shouldSendSnapshot: true);
	}

	private static ReconnectClaimResult Accept(
		SeatControlState state,
		ReconnectClaimRecord claimRecord,
		bool stateChanged,
		bool shouldSendSnapshot)
	{
		return new ReconnectClaimResult(
			Accepted: true,
			StateChanged: stateChanged,
			ShouldSendSnapshot: shouldSendSnapshot,
			SeatControlState: state,
			ClaimRecord: claimRecord,
			RejectReason: ReconnectClaimRejectReason.None);
	}

	private static ReconnectClaimResult Reject(
		SeatControlState state,
		ReconnectClaimRecord claimRecord,
		ReconnectClaimRejectReason reason)
	{
		return new ReconnectClaimResult(
			Accepted: false,
			StateChanged: false,
			ShouldSendSnapshot: false,
			SeatControlState: state,
			ClaimRecord: claimRecord,
			RejectReason: reason);
	}
}
