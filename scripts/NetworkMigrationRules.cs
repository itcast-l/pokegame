using System;
using System.Collections.Generic;
using System.Linq;

public enum NetworkMigrationCoordinatorState
{
	Disabled,
	SelectingCandidate,
	KeyAgreement,
	AwaitingCheckpointAcknowledgement,
	AwaitingPlanAcknowledgement,
	Ready,
	SourceHostLost,
	CandidateStarting,
	FollowersRecovering,
	Completed,
	Failed,
}

public enum AuthorityCheckpointCommitStatus
{
	Committed,
	RulesStateUnavailable,
	DataValidationFailed,
	EncodingFailed,
	PayloadTooLarge,
	SigningFailed,
	SendFailed,
}

public enum MigrationCheckpointDispatchStatus
{
	NotRequested,
	AwaitingAcknowledgement,
	Ready,
	CandidateUnavailable,
	EncryptionFailed,
	SigningFailed,
	SendFailed,
}

public enum SteamLobbyOwnerConvergenceAction
{
	Ready,
	RequestTransfer,
	Wait,
	Fail,
}

public readonly record struct AuthorityCheckpointCommitResult(
	AuthorityCheckpointCommitStatus CommitStatus,
	MigrationCheckpointDispatchStatus DispatchStatus,
	long CheckpointId,
	string Detail)
{
	public bool IsCheckpointCommitted => CommitStatus == AuthorityCheckpointCommitStatus.Committed;
	public bool IsMigrationReady => DispatchStatus == MigrationCheckpointDispatchStatus.Ready;

	public static AuthorityCheckpointCommitResult Rejected(
		AuthorityCheckpointCommitStatus status,
		string detail = "")
	{
		return new AuthorityCheckpointCommitResult(
			status,
			MigrationCheckpointDispatchStatus.NotRequested,
			-1,
			detail ?? string.Empty);
	}

	public static AuthorityCheckpointCommitResult Committed(
		long checkpointId,
		MigrationCheckpointDispatchStatus dispatchStatus,
		string detail = "")
	{
		return new AuthorityCheckpointCommitResult(
			AuthorityCheckpointCommitStatus.Committed,
			dispatchStatus,
			checkpointId,
			detail ?? string.Empty);
	}
}

public readonly record struct NetworkMigrationRetryWindow(
	double FirstAttemptDelaySeconds,
	double RetryIntervalSeconds,
	double RetryWindowSeconds,
	double TotalTimeoutSeconds);

public static class NetworkMigrationRules
{
	public static bool CanTransitionCoordinatorState(
		NetworkMigrationCoordinatorState current,
		NetworkMigrationCoordinatorState next)
	{
		if (current == next || next is NetworkMigrationCoordinatorState.Disabled or NetworkMigrationCoordinatorState.Failed)
		{
			return true;
		}

		return current switch
		{
			NetworkMigrationCoordinatorState.Disabled => next == NetworkMigrationCoordinatorState.SelectingCandidate,
			NetworkMigrationCoordinatorState.SelectingCandidate => next is NetworkMigrationCoordinatorState.KeyAgreement
				or NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement,
			NetworkMigrationCoordinatorState.KeyAgreement => next is NetworkMigrationCoordinatorState.SelectingCandidate
				or NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement,
			NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement => next is NetworkMigrationCoordinatorState.SelectingCandidate
				or NetworkMigrationCoordinatorState.KeyAgreement
				or NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement,
			NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement => next is NetworkMigrationCoordinatorState.SelectingCandidate
				or NetworkMigrationCoordinatorState.KeyAgreement
				or NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement
				or NetworkMigrationCoordinatorState.Ready,
			NetworkMigrationCoordinatorState.Ready => next is NetworkMigrationCoordinatorState.SelectingCandidate
				or NetworkMigrationCoordinatorState.KeyAgreement
				or NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement
				or NetworkMigrationCoordinatorState.SourceHostLost,
			NetworkMigrationCoordinatorState.SourceHostLost => next is NetworkMigrationCoordinatorState.CandidateStarting
				or NetworkMigrationCoordinatorState.FollowersRecovering,
			NetworkMigrationCoordinatorState.CandidateStarting => next == NetworkMigrationCoordinatorState.FollowersRecovering,
			NetworkMigrationCoordinatorState.FollowersRecovering => next == NetworkMigrationCoordinatorState.Completed,
			NetworkMigrationCoordinatorState.Completed => next == NetworkMigrationCoordinatorState.SelectingCandidate,
			NetworkMigrationCoordinatorState.Failed => next is NetworkMigrationCoordinatorState.Disabled
				or NetworkMigrationCoordinatorState.SelectingCandidate,
			_ => false,
		};
	}

	public static bool IsReadyForMigration(NetworkMigrationCoordinatorState state)
	{
		return state == NetworkMigrationCoordinatorState.Ready;
	}

	public static AuthorityCheckpointCommitStatus ClassifyCheckpointBuildFailure(string error)
	{
		if (string.IsNullOrWhiteSpace(error))
		{
			return AuthorityCheckpointCommitStatus.DataValidationFailed;
		}
		if (error.Contains(NetworkAuthorityCheckpointCodec.PayloadSizeError, StringComparison.Ordinal))
		{
			return AuthorityCheckpointCommitStatus.PayloadTooLarge;
		}
		if (error.Contains(NetworkAuthorityCheckpointCodec.EncodeFailedError, StringComparison.Ordinal))
		{
			return AuthorityCheckpointCommitStatus.EncodingFailed;
		}
		if (error.Contains("authority_action_not_stable", StringComparison.Ordinal)
			|| error.Contains("board_not_authority", StringComparison.Ordinal)
			|| error.Contains("checkpoint_id_not_monotonic", StringComparison.Ordinal))
		{
			return AuthorityCheckpointCommitStatus.RulesStateUnavailable;
		}

		return AuthorityCheckpointCommitStatus.DataValidationFailed;
	}

	public static bool ShouldContinueAuthenticationAfterSecurityHandshake(
		NetworkSessionState state,
		bool localHandshakeIsRekey,
		bool authorityHandshakeIsRekey)
	{
		return state is NetworkSessionState.Reconnecting or NetworkSessionState.MigratingHost
			|| (!localHandshakeIsRekey && !authorityHandshakeIsRekey);
	}

	public const double StandbyCheckpointAckTimeoutSeconds = 1.0;
	public const double StandbyCheckpointMinimumSendIntervalSeconds = 0.5;
	public const int ConsecutiveUnackedCheckpointsBeforeSwitch = 2;
	public const double CandidateStartDelaySeconds = 0.25;
	public const double CandidateRetryIntervalSeconds = 0.25;
	public const double CandidateRetryWindowSeconds = 5.0;
	public const double FollowerStartDelaySeconds = 0.75;
	public const double FollowerConnectionAttemptTimeoutSeconds = 2.0;
	public const double FollowerRetryIntervalSeconds = 0.50;
	public const double FollowerTimeoutSeconds = 20.0;
	public const int LanPeerTimeoutFactor = 32;
	public const int LanPeerTimeoutMinimumMilliseconds = 1000;
	public const int LanPeerTimeoutMaximumMilliseconds = 2500;
	public const double ActiveMigrationConfirmationTimeoutSeconds = 2.0;
	public const double ActiveLeaveConfirmationTimeoutSeconds = 2.0;
	public const double LobbyRecoveryTimeoutSeconds = 10.0;
	public const double GameRecoveryTimeoutSeconds = 10.0;
	public const double SteamLobbyOwnerConvergenceTimeoutSeconds = 5.0;
	public const double MigrationLatencyProbeIntervalSeconds = 2.0;
	public const double MigrationLatencyProbeTimeoutSeconds = 4.0;

	public static bool IsCompletedGameRecoveryEpoch(
		long requestedEpoch,
		long lastCompletedEpoch,
		long currentAuthorityEpoch)
	{
		return requestedEpoch >= 0
			&& requestedEpoch == lastCompletedEpoch
			&& requestedEpoch == currentAuthorityEpoch;
	}

	public static readonly NetworkMigrationRetryWindow CandidateRetryWindow = new(
		CandidateStartDelaySeconds,
		CandidateRetryIntervalSeconds,
		CandidateRetryWindowSeconds,
		CandidateRetryWindowSeconds);

	public static readonly NetworkMigrationRetryWindow FollowerRetryWindow = new(
		FollowerStartDelaySeconds,
		FollowerRetryIntervalSeconds,
		FollowerTimeoutSeconds,
		FollowerTimeoutSeconds);

	public static int SelectHotStandbySeat(
		IReadOnlyCollection<NetworkMigrationRosterEntry> roster,
		IReadOnlyCollection<NetworkAuthoritySeatControl> seatControls,
		int hostSeat)
	{
		return SelectHotStandbySeat(roster, seatControls, hostSeat, Array.Empty<int>());
	}

	public static int SelectHotStandbySeat(
		IReadOnlyCollection<NetworkMigrationRosterEntry> roster,
		IReadOnlyCollection<NetworkAuthoritySeatControl> seatControls,
		int hostSeat,
		IReadOnlyCollection<int> excludedSeats)
	{
		return SelectHotStandbySeat(
			roster,
			seatControls,
			hostSeat,
			excludedSeats,
			stableAcknowledgedSeat: -1,
			latencyMillisecondsBySeat: null);
	}

	public static int SelectHotStandbySeat(
		IReadOnlyCollection<NetworkMigrationRosterEntry> roster,
		IReadOnlyCollection<NetworkAuthoritySeatControl> seatControls,
		int hostSeat,
		IReadOnlyCollection<int> excludedSeats,
		int stableAcknowledgedSeat,
		IReadOnlyDictionary<int, double>? latencyMillisecondsBySeat)
	{
		int selectedSeat = -1;
		bool selectedIsStable = false;
		double selectedLatency = double.MaxValue;
		foreach (NetworkMigrationRosterEntry entry in roster)
		{
			if (!entry.IsConnected
				|| !entry.CredentialsAcknowledged
				|| entry.SeatIndex == hostSeat
				|| ContainsSeat(excludedSeats, entry.SeatIndex)
				|| !IsConnectedHumanSeat(seatControls, entry.SeatIndex, hostSeat))
			{
				continue;
			}

			bool isStable = entry.SeatIndex == stableAcknowledgedSeat;
			double latency = GetCandidateLatency(entry.SeatIndex, latencyMillisecondsBySeat);
			if (selectedSeat < 0
				|| (isStable && !selectedIsStable)
				|| (isStable == selectedIsStable
					&& (latency < selectedLatency
						|| (latency == selectedLatency && entry.SeatIndex < selectedSeat))))
			{
				selectedSeat = entry.SeatIndex;
				selectedIsStable = isStable;
				selectedLatency = latency;
			}
		}
		return selectedSeat;
	}

	private static bool ContainsSeat(IReadOnlyCollection<int> seats, int seatIndex)
	{
		foreach (int seat in seats)
		{
			if (seat == seatIndex)
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsConnectedHumanSeat(
		IReadOnlyCollection<NetworkAuthoritySeatControl> seatControls,
		int seatIndex,
		int hostSeat)
	{
		foreach (NetworkAuthoritySeatControl control in seatControls)
		{
			if (control.SeatIndex == seatIndex)
			{
				return control.SeatIndex != hostSeat
					&& control.Mode == SeatControlMode.HumanConnected;
			}
		}
		return false;
	}

	private static double GetCandidateLatency(
		int seatIndex,
		IReadOnlyDictionary<int, double>? latencyMillisecondsBySeat)
	{
		return latencyMillisecondsBySeat is not null
			&& latencyMillisecondsBySeat.TryGetValue(seatIndex, out double latency)
			&& double.IsFinite(latency)
			&& latency >= 0.0
			? latency
			: double.MaxValue;
	}

	public static bool ShouldSwitchToNextCandidate(
		bool candidateConnected,
		int consecutiveUnackedCheckpoints)
	{
		return !candidateConnected
			|| consecutiveUnackedCheckpoints >= ConsecutiveUnackedCheckpointsBeforeSwitch;
	}

	public static bool CanSendStandbyCheckpoint(
		double now,
		double lastSentAt,
		bool hasPendingCheckpoint)
	{
		return double.IsFinite(now)
			&& now >= 0.0
			&& !hasPendingCheckpoint
			&& (lastSentAt < 0.0
				|| now - lastSentAt >= StandbyCheckpointMinimumSendIntervalSeconds);
	}

	public static bool CanDispatchMigrationCheckpoint(
		long latestCheckpointId,
		long eligibleCheckpointId)
	{
		return latestCheckpointId >= 0
			&& eligibleCheckpointId >= 0
			&& latestCheckpointId == eligibleCheckpointId;
	}

	public static bool CanAcceptCheckpointChainAdvance(
		string previousCheckpointHash,
		string confirmedCheckpointHash,
		long checkpointId,
		long latestAcceptedCheckpointId,
		long planRevision,
		long latestAcceptedPlanRevision)
	{
		return string.Equals(
				previousCheckpointHash ?? string.Empty,
				confirmedCheckpointHash ?? string.Empty,
				StringComparison.Ordinal)
			&& checkpointId > latestAcceptedCheckpointId
			&& planRevision > latestAcceptedPlanRevision;
	}

	public static bool ArePlanAcknowledgementsComplete(
		IEnumerable<int> requiredSeatIndices,
		IReadOnlyCollection<int> acknowledgedSeatIndices)
	{
		if (requiredSeatIndices is null || acknowledgedSeatIndices is null)
		{
			return false;
		}

		int[] required = requiredSeatIndices.Distinct().ToArray();
		return required.Length > 0
			&& required.All(acknowledgedSeatIndices.Contains);
	}

	public static bool IsPlanVisibleToClients(NetworkMigrationPlan plan)
	{
		return IsUnsignedPlanValid(plan)
			&& !string.IsNullOrWhiteSpace(plan.SourceSignature);
	}

	public static bool IsPlanRevocationVisibleToClients(NetworkMigrationPlanRevocation revocation)
	{
		return IsUnsignedPlanRevocationValid(revocation)
			&& !string.IsNullOrWhiteSpace(revocation.SourceSignature);
	}

	public static bool IsUnsignedPlanRevocationValid(NetworkMigrationPlanRevocation revocation)
	{
		return revocation is not null
			&& revocation.ProtocolVersion == NetworkProtocol.CurrentVersion
			&& revocation.PlanRevision > revocation.RevokedPlanRevision
			&& revocation.RevokedPlanRevision > 0
			&& revocation.RevokedCheckpointId >= 0
			&& !string.IsNullOrWhiteSpace(revocation.SessionId)
			&& revocation.SessionId.Length <= NetworkProtocol.MaxSessionIdLength
			&& revocation.MatchId is not null
			&& revocation.MatchId.Length <= NetworkProtocol.MaxMatchIdLength
			&& revocation.SourceEpoch >= 0
			&& revocation.SourceAuthorityKeyId.Length == 64
			&& revocation.SourceAuthorityKeyId.All(IsLowerHex);
	}

	public static bool IsUnsignedPlanValid(NetworkMigrationPlan plan)
	{
		return plan is not null
			&& plan.ProtocolVersion == NetworkProtocol.CurrentVersion
			&& plan.PlanRevision > 0
			&& !string.IsNullOrWhiteSpace(plan.SessionId)
			&& plan.Phase is NetworkAuthorityPhase.Lobby or NetworkAuthorityPhase.InGame
			&& (plan.Phase == NetworkAuthorityPhase.Lobby || !string.IsNullOrWhiteSpace(plan.MatchId))
			&& plan.SourceEpoch >= 0
			&& plan.TargetEpoch > plan.SourceEpoch
			&& plan.CheckpointId >= 0
			&& plan.CheckpointHash.Length == 64
			&& plan.CheckpointCiphertextHash.Length == 64
			&& (string.IsNullOrEmpty(plan.PreviousCheckpointHash) || plan.PreviousCheckpointHash.Length == 64)
			&& plan.CandidateSeat >= 0
			&& !string.IsNullOrWhiteSpace(plan.CandidatePlayerSessionId)
			&& !string.IsNullOrWhiteSpace(plan.CandidateSuccessorPublicKey)
			&& plan.CandidateSuccessorKeyId.Length == 64
			&& HostMigrationSecurity.IsSigningPublicKeyBindingValid(
				plan.CandidateSuccessorPublicKey,
				plan.CandidateSuccessorKeyId)
			&& plan.SourceAuthorityKeyId.Length == 64
			&& Enum.IsDefined(plan.Transport)
			&& IsMigrationTransportDescriptorValid(plan)
			&& plan.CheckpointHash.All(IsLowerHex)
			&& plan.CheckpointCiphertextHash.All(IsLowerHex)
			&& (string.IsNullOrEmpty(plan.PreviousCheckpointHash) || plan.PreviousCheckpointHash.All(IsLowerHex))
			&& plan.CandidateSuccessorKeyId.All(IsLowerHex)
			&& plan.SourceAuthorityKeyId.All(IsLowerHex);
	}

	public static bool IsMigrationTransportDescriptorValid(NetworkMigrationPlan plan)
	{
		if (plan?.TransportDescriptor is not { } descriptor)
		{
			return false;
		}

		return plan.Transport switch
		{
			NetworkTransport.Lan => descriptor.Steam is null
				&& IsLanMigrationDescriptorValid(descriptor.Lan),
			NetworkTransport.Steam => descriptor.Lan is null
				&& IsSteamMigrationDescriptorValid(descriptor.Steam),
			_ => false,
		};
	}

	public static bool IsLanMigrationDescriptorValid(NetworkLanMigrationDescriptor? descriptor)
	{
		return descriptor is { } lan
			&& IsValidHostAddress(lan.ObservedHostAddress)
			&& IsValidHostAddress(lan.CandidateHostAddress)
			&& lan.Port == NetworkProtocol.LanHostMigrationPort
			&& lan.MaxClients == NetworkSessionDefaults.MaxPlayers - 1
			&& lan.PeerRebuildMode == NetworkMigrationPeerRebuildMode.EnetServer;
	}

	public static bool IsSteamMigrationDescriptorValid(NetworkSteamMigrationDescriptor? descriptor)
	{
		return descriptor is { } steam
			&& steam.LobbyId > 0
			&& steam.SourceLobbyOwnerSteamId > 0
			&& steam.CandidateSteamId > 0
			&& steam.CandidateSteamId != steam.SourceLobbyOwnerSteamId
			&& steam.ExpectedLobbyOwnerSteamId == steam.CandidateSteamId
			&& steam.PeerRebuildMode == NetworkMigrationPeerRebuildMode.SteamLobby;
	}

	public static SteamLobbyOwnerConvergenceAction GetSteamLobbyOwnerConvergenceAction(
		NetworkSteamMigrationDescriptor descriptor,
		long localSteamId,
		long currentLobbyOwnerSteamId,
		bool timedOut)
	{
		if (!IsSteamMigrationDescriptorValid(descriptor)
			|| descriptor.LobbyId <= 0
			|| localSteamId <= 0)
		{
			return SteamLobbyOwnerConvergenceAction.Fail;
		}
		if (currentLobbyOwnerSteamId <= 0)
		{
			return timedOut
				? SteamLobbyOwnerConvergenceAction.Fail
				: SteamLobbyOwnerConvergenceAction.Wait;
		}
		if (currentLobbyOwnerSteamId == descriptor.ExpectedLobbyOwnerSteamId)
		{
			return SteamLobbyOwnerConvergenceAction.Ready;
		}
		if (timedOut)
		{
			return SteamLobbyOwnerConvergenceAction.Fail;
		}

		return localSteamId == currentLobbyOwnerSteamId
			? SteamLobbyOwnerConvergenceAction.RequestTransfer
			: SteamLobbyOwnerConvergenceAction.Wait;
	}

	public static bool CanRebuildMigrationPeer(
		bool transportPrepared,
		bool awaitingSourceDisconnect)
	{
		return transportPrepared && !awaitingSourceDisconnect;
	}

	public static bool ShouldAcceptPlanUpdate(NetworkMigrationPlan? currentPlan, NetworkMigrationPlan incomingPlan)
	{
		return incomingPlan is not null
			&& (currentPlan is null
				|| incomingPlan.TargetEpoch > currentPlan.TargetEpoch
				|| (incomingPlan.TargetEpoch == currentPlan.TargetEpoch
						&& incomingPlan.PlanRevision > currentPlan.PlanRevision));
	}

	public static bool CanAcceptPlanRevisionAfterRevocation(
		long lastObservedPlanRevision,
		long pendingRevocationRevision,
		NetworkMigrationPlan incomingPlan)
	{
		return incomingPlan is not null
			&& (incomingPlan.PlanRevision > lastObservedPlanRevision
				|| (incomingPlan.PlanRevision == lastObservedPlanRevision
					&& incomingPlan.PlanRevision == pendingRevocationRevision));
	}

	public static bool IsSameCheckpointChain(
		NetworkAuthorityCheckpoint? checkpoint,
		string sessionId,
		string matchId,
		long authorityEpoch)
	{
		return checkpoint is not null
			&& string.Equals(checkpoint.SessionId, sessionId, StringComparison.Ordinal)
			&& string.Equals(checkpoint.MatchId, matchId, StringComparison.Ordinal)
			&& checkpoint.AuthorityEpoch == authorityEpoch;
	}

	private static bool IsLowerHex(char character)
	{
		return character is >= '0' and <= '9' or >= 'a' and <= 'f';
	}

	public static bool CanCandidateStart(
		NetworkMigrationPlan plan,
		string localPlayerSessionId,
		NetworkSessionState currentState,
		bool hasCheckpoint,
		bool isSourceHost)
	{
		return !isSourceHost
			&& currentState == NetworkSessionState.MigratingHost
			&& hasCheckpoint
			&& IsPlanVisibleToClients(plan)
			&& string.Equals(plan.CandidatePlayerSessionId, localPlayerSessionId, StringComparison.Ordinal);
	}

	public static bool ShouldFollowerConnect(
		NetworkMigrationPlan plan,
		string localPlayerSessionId,
		NetworkSessionState currentState,
		bool isCandidate,
		bool elapsedTimeout)
	{
		return !isCandidate
			&& !elapsedTimeout
			&& currentState == NetworkSessionState.MigratingHost
			&& IsPlanVisibleToClients(plan)
			&& !string.Equals(plan.CandidatePlayerSessionId, localPlayerSessionId, StringComparison.Ordinal);
	}

	public static bool ShouldFailMigration(
		NetworkSessionState currentState,
		bool hasValidPlan,
		bool elapsedTimeout,
		bool peerConnected)
	{
		return currentState == NetworkSessionState.MigratingHost
			&& (!hasValidPlan || (elapsedTimeout && !peerConnected));
	}

	private static bool IsValidHostAddress(string address)
	{
		return !string.IsNullOrWhiteSpace(address)
			&& address.Length <= 64
			&& !address.Any(char.IsWhiteSpace);
	}

	public static bool ShouldPreserveSeatOnInGameMigration(
		int seatIndex,
		int originalHostSeat,
		bool inGame)
	{
		return inGame && seatIndex >= 0 && seatIndex != originalHostSeat;
	}
}
