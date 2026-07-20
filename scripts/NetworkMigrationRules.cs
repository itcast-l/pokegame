using System;
using System.Collections.Generic;
using System.Linq;

public readonly record struct NetworkMigrationRetryWindow(
	double FirstAttemptDelaySeconds,
	double RetryIntervalSeconds,
	double RetryWindowSeconds,
	double TotalTimeoutSeconds);

public static class NetworkMigrationRules
{
	public const double StandbyCheckpointAckTimeoutSeconds = 1.0;
	public const double StandbyCheckpointMinimumSendIntervalSeconds = 0.5;
	public const int ConsecutiveUnackedCheckpointsBeforeSwitch = 2;
	public const double CandidateStartDelaySeconds = 0.25;
	public const double CandidateRetryIntervalSeconds = 0.25;
	public const double CandidateRetryWindowSeconds = 5.0;
	public const double FollowerStartDelaySeconds = 0.75;
	public const double FollowerConnectionAttemptTimeoutSeconds = 2.0;
	public const double FollowerRetryIntervalSeconds = 0.50;
	public const double FollowerTimeoutSeconds = 10.0;
	public const int LanPeerTimeoutFactor = 32;
	public const int LanPeerTimeoutMinimumMilliseconds = 1000;
	public const int LanPeerTimeoutMaximumMilliseconds = 2500;
	public const double ActiveMigrationConfirmationTimeoutSeconds = 2.0;
	public const double ActiveLeaveConfirmationTimeoutSeconds = 2.0;
	public const double LobbyRecoveryTimeoutSeconds = 10.0;

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
		HashSet<int> excluded = excludedSeats.ToHashSet();
		HashSet<int> humanSeats = seatControls
			.Where(control => control.Mode == SeatControlMode.HumanConnected && control.SeatIndex != hostSeat)
			.Select(control => control.SeatIndex)
			.ToHashSet();
		return roster
			.Where(entry => entry.IsConnected
				&& entry.CredentialsAcknowledged
				&& entry.SeatIndex != hostSeat
				&& !excluded.Contains(entry.SeatIndex)
				&& humanSeats.Contains(entry.SeatIndex))
			.Select(entry => entry.SeatIndex)
			.DefaultIfEmpty(-1)
			.Min();
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

	public static bool IsPlanVisibleToClients(NetworkMigrationPlan plan)
	{
		return plan is not null
			&& !string.IsNullOrWhiteSpace(plan.SessionId)
			&& plan.Phase is NetworkAuthorityPhase.Lobby or NetworkAuthorityPhase.InGame
			&& (plan.Phase == NetworkAuthorityPhase.Lobby || !string.IsNullOrWhiteSpace(plan.MatchId))
			&& plan.SourceEpoch >= 0
			&& plan.TargetEpoch > plan.SourceEpoch
			&& plan.CheckpointId >= 0
			&& plan.CheckpointHash.Length == 64
			&& plan.CandidateSeat >= 0
			&& !string.IsNullOrWhiteSpace(plan.CandidatePlayerSessionId)
			&& !string.IsNullOrWhiteSpace(plan.ObservedHostAddress)
			&& IsValidHostAddress(plan.ObservedHostAddress)
			&& !string.IsNullOrWhiteSpace(plan.CandidateHostAddress)
			&& IsValidHostAddress(plan.CandidateHostAddress)
			&& plan.Port == NetworkProtocol.LanHostMigrationPort
			&& plan.CheckpointHash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
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
