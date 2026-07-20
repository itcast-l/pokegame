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
	CredentialMismatch,
	PlayerSessionIdMismatch,
	SeatNotReclaimable,
	AlreadyClaimedByDifferentPeer,
}

internal readonly record struct ReconnectClaimRecord(
	string MatchId,
	int SeatIndex,
	long CredentialGeneration,
	string PlayerSessionId,
	int ClaimedPeerId,
	ReconnectClaimState ClaimState)
{
	public ReconnectClaimRecord(
		string MatchId,
		int SeatIndex,
		string Credential,
		string PlayerSessionId,
		int ClaimedPeerId,
		ReconnectClaimState ClaimState)
		: this(
			MatchId,
			SeatIndex,
			SeatCredentialRules.InitialGeneration,
			PlayerSessionId,
			ClaimedPeerId,
			ClaimState)
	{
	}
}

internal readonly record struct ReconnectClaimResult(
	bool Accepted,
	bool StateChanged,
	bool ShouldSendSnapshot,
	SeatControlState SeatControlState,
	ReconnectClaimRecord ClaimRecord,
	ReconnectClaimRejectReason RejectReason);

internal static class SeatControlRules
{
	internal static SeatControlState CreateInitialState(
		NetworkPlayerPublicState player,
		string credentialVerifier,
		long credentialGeneration = SeatCredentialRules.InitialGeneration)
	{
		return new SeatControlState(
			player.SeatIndex,
			SeatControlMode.HumanConnected,
			player.PeerId,
			player.PlayerSessionId,
			credentialVerifier,
			credentialGeneration,
			string.Empty,
			0,
			0);
	}

	internal static ReconnectClaimRecord CreateInitialClaimRecord(string matchId, SeatControlState state)
	{
		return new ReconnectClaimRecord(
			matchId,
			state.SeatIndex,
			state.CredentialGeneration,
			state.PlayerSessionId,
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

	internal static SeatControlState RedactCredentialVerifiersForSharedSnapshot(SeatControlState current)
	{
		return current with
		{
			CredentialVerifier = string.Empty,
			PendingCredentialVerifier = string.Empty,
			PendingCredentialGeneration = 0,
			PendingCredentialPeerId = 0,
		};
	}

	internal static ReconnectClaimResult ApplyReconnectClaim(
		string activeMatchId,
		bool isMatchActive,
		SeatControlState current,
		ReconnectClaimRecord? currentClaim,
		string requestMatchId,
		string credential,
		string playerSessionId,
		int senderPeerId)
	{
		return ApplyReconnectClaim(
			activeMatchId,
			isMatchActive,
			current,
			currentClaim,
			requestMatchId,
			current.CredentialGeneration,
			credential,
			playerSessionId,
			senderPeerId);
	}

	internal static ReconnectClaimResult ApplyReconnectClaim(
		string activeMatchId,
		bool isMatchActive,
		SeatControlState current,
		ReconnectClaimRecord? currentClaim,
		string requestMatchId,
		long credentialGeneration,
		string credential,
		string playerSessionId,
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

		if (credentialGeneration != current.CredentialGeneration
			|| !SeatCredentialRules.Verify(credential, current.CredentialVerifier))
		{
			return Reject(current, claimRecord, ReconnectClaimRejectReason.CredentialMismatch);
		}

		if (!string.Equals(current.PlayerSessionId, playerSessionId, StringComparison.Ordinal))
		{
			return Reject(current, claimRecord, ReconnectClaimRejectReason.PlayerSessionIdMismatch);
		}
		if (current.PendingCredentialGeneration > 0)
		{
			if (current.PeerId == senderPeerId
				&& current.Mode == SeatControlMode.HumanReclaimPending)
			{
				return Accept(current, claimRecord, stateChanged: false, shouldSendSnapshot: false);
			}

			return Reject(current, claimRecord, ReconnectClaimRejectReason.AlreadyClaimedByDifferentPeer);
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
