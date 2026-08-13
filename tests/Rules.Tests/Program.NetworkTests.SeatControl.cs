using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Deckland.Rules;

internal static partial class Program
{
	private static void LocalSeatInputEnablesOnlyForInGameHumanConnectedState()
	{
		True(LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: false,
			networkState: NetworkSessionState.Idle,
			seatStateFound: false,
			seatMode: SeatControlMode.AiProxyReclaimable));

		True(LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: true,
			networkState: NetworkSessionState.InGame,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanConnected));

		False(LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: true,
			networkState: NetworkSessionState.Reconnecting,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanConnected));

		False(LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: true,
			networkState: NetworkSessionState.InGame,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanReclaimPending));

		False(LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: true,
			networkState: NetworkSessionState.InGame,
			seatStateFound: false,
			seatMode: SeatControlMode.HumanConnected));
	}

	private static void LocalSeatBoardControlsBlockUntilHumanControlIsRestored()
	{
		False(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: false,
			isNetworkHost: false,
			networkState: NetworkSessionState.Idle,
			seatStateFound: false,
			seatMode: SeatControlMode.AiProxyReclaimable));

		False(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			networkState: NetworkSessionState.InGame,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanReclaimPending));

		False(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanConnected));

		True(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.Reconnecting,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanConnected));

		True(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanReclaimPending));

		True(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			seatStateFound: false,
			seatMode: SeatControlMode.HumanConnected));
	}

	private static void NetworkSeatControlAcceptsRemoteActionsOnlyForConnectedHumans()
	{
		True(NetworkSeatControlRules.CanAcceptRemotePlayerAction(SeatControlMode.HumanConnected));
		False(NetworkSeatControlRules.CanAcceptRemotePlayerAction(SeatControlMode.AiProxyReclaimable));
		False(NetworkSeatControlRules.CanAcceptRemotePlayerAction(SeatControlMode.HumanReclaimPending));
	}

	private static void NetworkSeatControlSendsPendingSelectionRequestsOnlyWhenClientIsReady()
	{
		True(NetworkSeatControlRules.CanSendPendingSelectionRequest(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			requestInFlight: false));

		False(NetworkSeatControlRules.CanSendPendingSelectionRequest(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			requestInFlight: true));

		False(NetworkSeatControlRules.CanSendPendingSelectionRequest(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.Reconnecting,
			requestInFlight: false));

		False(NetworkSeatControlRules.CanSendPendingSelectionRequest(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			networkState: NetworkSessionState.InGame,
			requestInFlight: false));

		False(NetworkSeatControlRules.CanSendPendingSelectionRequest(
			isNetworkMultiplayer: false,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			requestInFlight: false));
	}


	private static void NetworkAiPlayableTurnActionIgnoresBlockedItemOnlyTurns()
	{
		True(NetworkAiFallbackRules.HasPlayableAiTurnAction(
			handCardCount: 1,
			playableItemCount: 0));

		True(NetworkAiFallbackRules.HasPlayableAiTurnAction(
			handCardCount: 0,
			playableItemCount: 1));

		False(NetworkAiFallbackRules.HasPlayableAiTurnAction(
			handCardCount: 0,
			playableItemCount: 0));
	}

	private static void NetworkAiProxyCaptureProfileMatchesWeakestAdventureAi()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("K", 12),
			TableCard("Q", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("K", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = NetworkAiFallbackRules.TryPickCaptureHandIndexForNetworkAiProxy(
			tableCards,
			handCards,
			DefaultScoringContext,
			decisionRollPercent: 30,
			randomCaptureOrdinal: 1,
			out int handIndex);

		True(picked);
		Equal(1, handIndex);
	}

	private static void NetworkAiResumesProxyTurnOnlyAfterAuthorityActionSettles()
	{
		True(NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			isGameOver: false,
			isProcessingCardPlay: false));

		False(NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			isGameOver: false,
			isProcessingCardPlay: true));

		False(NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			isGameOver: false,
			isProcessingCardPlay: false));

		False(NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer: false,
			isNetworkHost: true,
			isGameOver: false,
			isProcessingCardPlay: false));

		False(NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			isGameOver: true,
			isProcessingCardPlay: false));
	}

	private static void NetworkAiStartsProxyTurnForAiOwnedPendingSelection()
	{
		True(NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			hasAiOwnedPendingSelection: true));

		False(NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			hasAiOwnedPendingSelection: false));

		False(NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			hasAiOwnedPendingSelection: true));

		False(NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
			isNetworkMultiplayer: false,
			isNetworkHost: true,
			hasAiOwnedPendingSelection: true));
	}

	private static void NetworkAiRecoversOnlyForHostSideUnhandledFailures()
	{
		True(NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			isGameOver: false));

		False(NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			isGameOver: false));

		False(NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			isNetworkMultiplayer: false,
			isNetworkHost: true,
			isGameOver: false));

		False(NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			isGameOver: true));
	}

	private static void NetworkAiBlockedItemSkipIsScopedToExactTurnAndItem()
	{
		NetworkAiBlockedItemState blocked = new(
			OwnerSeatIndex: 2,
			ItemCardId: "queen");

		True(NetworkAiFallbackRules.ShouldSkipBlockedItem(
			blocked,
			ownerSeatIndex: 2,
			itemCardId: "queen"));

		False(NetworkAiFallbackRules.ShouldSkipBlockedItem(
			blocked,
			ownerSeatIndex: 1,
			itemCardId: "queen"));

		False(NetworkAiFallbackRules.ShouldSkipBlockedItem(
			blocked,
			ownerSeatIndex: 2,
			itemCardId: "king"));

		True(NetworkAiFallbackRules.ShouldClearBlockedItemForTurnChange(previousTurnPlayerIndex: 2, nextTurnPlayerIndex: 3));
		False(NetworkAiFallbackRules.ShouldClearBlockedItemForTurnChange(previousTurnPlayerIndex: 2, nextTurnPlayerIndex: 2));
	}

	private static void NetworkAiPendingChoiceFallbackPicksOnlyLegalCandidates()
	{
		Equal(1, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 4,
			isLegalCandidate: candidateIndex => candidateIndex is 1 or 3,
			randomOrdinal: 0));
		Equal(3, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 4,
			isLegalCandidate: candidateIndex => candidateIndex is 1 or 3,
			randomOrdinal: 1));
		Equal(1, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 4,
			isLegalCandidate: candidateIndex => candidateIndex is 1 or 3,
			randomOrdinal: 2));
		Equal(4, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 5,
			isLegalCandidate: candidateIndex => candidateIndex is 0 or 2 or 4,
			randomOrdinal: 5));
		Equal(2, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 6,
			isLegalCandidate: candidateIndex => candidateIndex >= 2 && candidateIndex <= 4,
			randomOrdinal: 0));
	}

	private static void NetworkAiPendingChoiceFallbackFailsWithoutLegalCandidates()
	{
		Equal(-1, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 0,
			isLegalCandidate: _ => true,
			randomOrdinal: 0));
		Equal(-1, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 4,
			isLegalCandidate: _ => false,
			randomOrdinal: 17));
	}

	private static void SeatControlDisconnectTransitionsToAiReclaimable()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanConnected,
			PeerId: 20,
			PlayerSessionId: "stable-20",
			Credential: "token-1");
		ReconnectClaimRecord claim = SeatControlRules.CreateInitialClaimRecord("match-1", state);

		True(SeatControlRules.TryMarkDisconnected(state, 20, out SeatControlState next, out ReconnectClaimRecord? updatedClaim, claim));
		Equal(SeatControlMode.AiProxyReclaimable, next.Mode);
		Equal(0, next.PeerId);
		True(updatedClaim.HasValue);
		ReconnectClaimRecord disconnectClaim = updatedClaim.GetValueOrDefault();
		Equal(ReconnectClaimState.Unclaimed, disconnectClaim.ClaimState);
		Equal(0, disconnectClaim.ClaimedPeerId);
	}

	private static void SeatControlReclaimPendingDisconnectFallsBackToAiReclaimable()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 33,
			PlayerSessionId: "stable-20",
			Credential: "token-1");
		ReconnectClaimRecord claim = new(
			"match-1",
			1,
			"token-1",
			"stable-20",
			33,
			ReconnectClaimState.Claimed);

		True(SeatControlRules.TryMarkDisconnected(state, 33, out SeatControlState next, out ReconnectClaimRecord? updatedClaim, claim));
		Equal(SeatControlMode.AiProxyReclaimable, next.Mode);
		Equal(0, next.PeerId);
		True(updatedClaim.HasValue);
		ReconnectClaimRecord disconnectClaim = updatedClaim.GetValueOrDefault();
		Equal(ReconnectClaimState.Unclaimed, disconnectClaim.ClaimState);
		Equal(0, disconnectClaim.ClaimedPeerId);
	}

	private static void SeatControlReconnectClaimIsIdempotentForSamePeer()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.AiProxyReclaimable,
			PeerId: 0,
			PlayerSessionId: "stable-20",
			Credential: "token-1");
		ReconnectClaimRecord claim = SeatControlRules.CreateInitialClaimRecord("match-1", state);

		ReconnectClaimResult first = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			state,
			claim,
			"match-1",
			"token-1",
			"stable-20",
			senderPeerId: 33);
		True(first.Accepted);
		True(first.StateChanged);
		True(first.ShouldSendSnapshot);
		Equal(SeatControlMode.HumanReclaimPending, first.SeatControlState.Mode);
		Equal(33, first.SeatControlState.PeerId);

		ReconnectClaimResult second = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			first.SeatControlState,
			first.ClaimRecord,
			"match-1",
			"token-1",
			"stable-20",
			senderPeerId: 33);
		True(second.Accepted);
		False(second.StateChanged);
		False(second.ShouldSendSnapshot);
		Equal(SeatControlMode.HumanReclaimPending, second.SeatControlState.Mode);
	}

	private static void SeatControlReconnectClaimRejectsConflictingPeer()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 33,
			PlayerSessionId: "stable-20",
			Credential: "token-1");
		ReconnectClaimRecord claim = new(
			"match-1",
			1,
			"token-1",
			"stable-20",
			33,
			ReconnectClaimState.Claimed);

		ReconnectClaimResult result = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			state,
			claim,
			"match-1",
			"token-1",
			"stable-20",
			senderPeerId: 44);
		False(result.Accepted);
		Equal(ReconnectClaimRejectReason.AlreadyClaimedByDifferentPeer, result.RejectReason);
	}

	private static void SeatControlPromotesReclaimedSeatAtTurnStart()
	{
		SeatControlState state = new(
			SeatIndex: 2,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 55,
			PlayerSessionId: "stable-55",
			Credential: "token-2");

		True(SeatControlRules.TryPromoteAtTurnStart(state, 2, out SeatControlState next));
		Equal(SeatControlMode.HumanConnected, next.Mode);
		Equal(55, next.PeerId);
		False(SeatControlRules.TryPromoteAtTurnStart(next, 2, out _));
	}

	private static void SeatControlStaysReclaimPendingUntilOwnTurnStart()
	{
		SeatControlState state = new(
			SeatIndex: 2,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 55,
			PlayerSessionId: "stable-55",
			Credential: "token-2");

		False(SeatControlRules.TryPromoteAtTurnStart(state, 1, out SeatControlState next));
		Equal(state, next);
	}

	private static void SeatControlAllowsReclaimAgainAfterReclaimPendingDisconnect()
	{
		SeatControlState reclaimPending = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 33,
			PlayerSessionId: "stable-20",
			Credential: "token-1");
		ReconnectClaimRecord claimed = new(
			"match-1",
			1,
			"token-1",
			"stable-20",
			33,
			ReconnectClaimState.Claimed);

		True(SeatControlRules.TryMarkDisconnected(reclaimPending, 33, out SeatControlState disconnected, out ReconnectClaimRecord? updatedClaim, claimed));
		True(updatedClaim.HasValue);
		ReconnectClaimRecord resetClaim = updatedClaim.GetValueOrDefault();
		Equal(SeatControlMode.AiProxyReclaimable, disconnected.Mode);
		Equal(ReconnectClaimState.Unclaimed, resetClaim.ClaimState);
		Equal(0, resetClaim.ClaimedPeerId);

		ReconnectClaimResult reconnected = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			disconnected,
			resetClaim,
			"match-1",
			"token-1",
			"stable-20",
			senderPeerId: 44);
		True(reconnected.Accepted);
		True(reconnected.StateChanged);
		True(reconnected.ShouldSendSnapshot);
		Equal(SeatControlMode.HumanReclaimPending, reconnected.SeatControlState.Mode);
		Equal(44, reconnected.SeatControlState.PeerId);
		Equal(ReconnectClaimState.Claimed, reconnected.ClaimRecord.ClaimState);
		Equal(44, reconnected.ClaimRecord.ClaimedPeerId);
	}

	private static void SeatControlRejectsInvalidReconnectMetadata()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.AiProxyReclaimable,
			PeerId: 0,
			PlayerSessionId: "stable-20",
			Credential: "token-1");
		ReconnectClaimRecord claim = SeatControlRules.CreateInitialClaimRecord("match-1", state);

		ReconnectClaimResult wrongMatch = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			state,
			claim,
			"match-2",
			"token-1",
			"stable-20",
			senderPeerId: 33);
		False(wrongMatch.Accepted);
		Equal(ReconnectClaimRejectReason.MatchMismatch, wrongMatch.RejectReason);

		ReconnectClaimResult wrongToken = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			state,
			claim,
			"match-1",
			"bad",
			"stable-20",
			senderPeerId: 33);
		False(wrongToken.Accepted);
		Equal(ReconnectClaimRejectReason.CredentialMismatch, wrongToken.RejectReason);

		ReconnectClaimResult wrongUser = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			state,
			claim,
			"match-1",
			"token-1",
			"other",
			senderPeerId: 33);
		False(wrongUser.Accepted);
		Equal(ReconnectClaimRejectReason.PlayerSessionIdMismatch, wrongUser.RejectReason);
	}

	private static void SeatControlRejectsReconnectClaimAfterMatchEnd()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.AiProxyReclaimable,
			PeerId: 0,
			PlayerSessionId: "stable-20",
			Credential: "token-1");
		ReconnectClaimRecord claim = SeatControlRules.CreateInitialClaimRecord("match-1", state);

		ReconnectClaimResult result = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: false,
			state,
			claim,
			"match-1",
			"token-1",
			"stable-20",
			senderPeerId: 33);
		False(result.Accepted);
		Equal(ReconnectClaimRejectReason.MatchEnded, result.RejectReason);
	}

	private static void SeatControlSharedSnapshotRedactsCredentialVerifier()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 33,
			PlayerSessionId: "stable-20",
			Credential: "token-1");

		SeatControlState shared = SeatControlRules.RedactCredentialVerifiersForSharedSnapshot(state);

		Equal(state.SeatIndex, shared.SeatIndex);
		Equal(state.Mode, shared.Mode);
		Equal(state.PeerId, shared.PeerId);
		Equal(state.PlayerSessionId, shared.PlayerSessionId);
		Equal(string.Empty, shared.CredentialVerifier);
	}

	private static void AssertRejected(
		MultiplayerBoardActionRejectReason expectedReason,
		int senderSeatIndex,
		int currentTurnPlayerIndex,
		int handIndex,
		int handCount,
		int requestGeneration,
		int currentGeneration,
		bool isGameOver = false,
		bool isActionInProgress = false)
	{
		MultiplayerBoardActionValidation validation = MultiplayerBoardSyncRules.ValidatePlayHandCardRequest(
			senderSeatIndex,
			currentTurnPlayerIndex,
			handIndex,
			handCount,
			requestGeneration,
			currentGeneration,
			isGameOver,
			isActionInProgress);

		False(validation.Accepted);
		Equal(expectedReason, validation.RejectReason);
	}

}
