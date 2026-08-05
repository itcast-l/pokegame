using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;

internal static partial class Program
{
	private static void RunRuntimeMultiplayerArchitectureRemediationTests()
	{
		Run("maintenance schedule enforces deadlines and invalidates stale generations", MaintenanceScheduleEnforcesDeadlinesAndInvalidatesStaleGenerations);
		Run("match endpoint registration rejects stale unregistration", MatchEndpointRegistrationRejectsStaleUnregistration);
		Run("session close policy separates graceful and immediate reasons", SessionClosePolicySeparatesGracefulAndImmediateReasons);
		Run("checkpoint envelope takes ownership without plaintext copies", CheckpointEnvelopeTakesOwnershipWithoutPlaintextCopies);
	}

	private static void MaintenanceScheduleEnforcesDeadlinesAndInvalidatesStaleGenerations()
	{
		var generation = new NetworkMaintenanceScheduleGeneration();
		long first = generation.Advance();
		True(generation.IsCurrent(first));
		False(NetworkMaintenanceScheduleRules.IsDue(9.998, 10.0));
		True(NetworkMaintenanceScheduleRules.IsDue(9.999, 10.0));
		Equal(0.001, NetworkMaintenanceScheduleRules.GetTimerDelay(10.0, 9.0));
		Equal(0.25, NetworkMaintenanceScheduleRules.GetTimerDelay(10.0, 10.25));

		long second = generation.Advance();
		False(generation.IsCurrent(first));
		True(generation.IsCurrent(second));
	}

	private static void MatchEndpointRegistrationRejectsStaleUnregistration()
	{
		var endpoint = new TestNetworkMatchEndpoint();
		var store = new NetworkMatchEndpointRegistrationStore();
		NetworkMatchEndpointRegistrationToken first = store.Register("session", "match", 1, endpoint, endpoint, endpoint);
		NetworkMatchEndpointRegistrationToken replacement = store.Register("session", "match", 2, endpoint, endpoint, endpoint);

		False(store.Unregister(first));
		True(store.Active != null);
		Equal(replacement.RegistrationGeneration, store.Active!.RegistrationGeneration);
		True(store.Unregister(replacement));
		True(store.Active == null);
		False(store.Unregister(replacement));
	}

	private static void SessionClosePolicySeparatesGracefulAndImmediateReasons()
	{
		NetworkSessionClosePolicy hostUser = NetworkSessionClosePolicy.Resolve(
			NetworkSessionCloseReason.UserRequested,
			shouldBroadcastHostEndedMatch: true);
		True(hostUser.BroadcastHostEndedMatch);
		True(hostUser.WaitOneProcessFrame);
		Equal(NetworkSessionClosePolicy.HostReliablePacketGraceSeconds, hostUser.ReliablePacketGraceSeconds);
		False(hostUser.Immediate);

		NetworkSessionClosePolicy replacementWithoutMatch = NetworkSessionClosePolicy.Resolve(
			NetworkSessionCloseReason.SessionReplacement,
			shouldBroadcastHostEndedMatch: false);
		False(replacementWithoutMatch.BroadcastHostEndedMatch);
		True(replacementWithoutMatch.Immediate);

		foreach (NetworkSessionCloseReason reason in new[]
		{
			NetworkSessionCloseReason.ClientRequested,
			NetworkSessionCloseReason.ConnectionCreationFailed,
			NetworkSessionCloseReason.ProtocolRejected,
			NetworkSessionCloseReason.MigrationHandoff,
			NetworkSessionCloseReason.InternalFailure,
			NetworkSessionCloseReason.ApplicationExit,
		})
		{
			NetworkSessionClosePolicy policy = NetworkSessionClosePolicy.Resolve(
				reason,
				shouldBroadcastHostEndedMatch: true);
			False(policy.BroadcastHostEndedMatch);
			False(policy.WaitOneProcessFrame);
			Equal(0.0, policy.ReliablePacketGraceSeconds);
			True(policy.Immediate);
		}
	}

	private static void CheckpointEnvelopeTakesOwnershipWithoutPlaintextCopies()
	{
		byte[] ownedPayload = { 1, 2, 3, 4 };
		var envelope = new AuthorityCheckpointEnvelope(2, 7, ownedPayload, "hash");
		True(MemoryMarshal.TryGetArray(envelope.PayloadMemory, out ArraySegment<byte> segment));
		True(ReferenceEquals(ownedPayload, segment.Array));
		Equal(ownedPayload.Length, envelope.EncodedByteCount);
		Equal(0L, envelope.ExplicitPlaintextCopyCount);
		envelope.Dispose();
		False(envelope.HasPayload);
		Equal(0, envelope.EncodedByteCount);
		True(ownedPayload.All(value => value == 0));

		AuthorityCheckpointTelemetrySnapshot before = AuthorityCheckpointTelemetry.GetSnapshot();
		True(AuthorityCheckpointEnvelope.TryCreate(CreateCheckpoint(), out AuthorityCheckpointEnvelope encoded, out string error));
		Equal(string.Empty, error);
		AuthorityCheckpointTelemetrySnapshot after = AuthorityCheckpointTelemetry.GetSnapshot();
		Equal(before.EncodeCount + 1, after.EncodeCount);
		Equal(encoded.EncodedByteCount, after.LastEncodedBytes);
		Equal(before.ExplicitPlaintextCopyCount, after.ExplicitPlaintextCopyCount);
		encoded.Dispose();

		string root = FindRepositoryRoot();
		string envelopeSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"AuthorityCheckpointEnvelope.cs"));
		True(envelopeSource.Contains(": IDisposable", StringComparison.Ordinal));
		True(envelopeSource.Contains(
			"CryptographicOperations.ZeroMemory(_payload);",
			StringComparison.Ordinal));
		string networkManagerSources = string.Join(
			"\n",
			Directory.GetFiles(Path.Combine(root, "scripts"), "NetworkManager*.cs")
				.Select(File.ReadAllText));
		Equal(2, Regex.Matches(
			networkManagerSources,
			@"_latestAuthorityCheckpointEnvelope\s*=",
			RegexOptions.CultureInvariant).Count);
		True(networkManagerSources.Contains(
			"ReplaceLatestAuthorityCheckpointEnvelope(AuthorityCheckpointEnvelope.Empty);",
			StringComparison.Ordinal));
		False(networkManagerSources.Contains(
			"_standbyPendingCheckpointEnvelope",
			StringComparison.Ordinal));
	}

	private sealed class TestNetworkMatchEndpoint :
		INetworkAuthorityEndpoint,
		INetworkReplicaEndpoint,
		INetworkPresentationEndpoint
	{
		public bool IsAlive => true;
		public bool IsGameOver => false;
		public bool IsMatchClaimable => true;
		public bool IsMigrationInputLocked => false;
		public bool CanPublishSnapshot => true;
		public long AuthorityEpoch => 1;
		public long CheckpointId => 1;
		public long CurrentAuthorityActionId => 1;

		public int CreateNextSnapshotId() => 1;
		public bool TryStartHandCardPlay(int actorSeatIndex, int handIndex, int turnSequenceGeneration, long clientActionId, out MultiplayerBoardActionRejectReason rejectReason)
		{
			rejectReason = default;
			return true;
		}
		public bool TryStartItemCardPlay(int actorSeatIndex, int itemHandIndex, int turnSequenceGeneration, long clientActionId, out MultiplayerBoardActionRejectReason rejectReason)
		{
			rejectReason = default;
			return true;
		}
		public bool TryStartPendingItemSelection(int actorSeatIndex, int selectionIndex, int turnSequenceGeneration, long clientActionId, out MultiplayerBoardActionRejectReason rejectReason)
		{
			rejectReason = default;
			return true;
		}
		public bool TryBuildCheckpoint(IReadOnlyDictionary<int, long> lastAcceptedClientActionIds, long candidateCheckpointId, out AuthorityCheckpointEnvelope envelope, out string error)
		{
			envelope = AuthorityCheckpointEnvelope.Empty;
			error = string.Empty;
			return false;
			}
			public void CommitCheckpoint(AuthorityCheckpointEnvelope envelope) { }
			public void AdvanceCheckpointSequence(long checkpointId) { }
			public Godot.Collections.Dictionary BuildSnapshotForSeat(int recipientSeatIndex, int snapshotId) => new();
		public bool TryStageVisualDispatch(int peerId, Godot.Collections.Dictionary payload) => true;
		public bool TryStageHostVisualEvent(Godot.Collections.Dictionary payload) => true;
		public Task ResumeAuthorityActionAsync() => Task.CompletedTask;
		public void BeginAiProxyTurnIfNeeded() { }
		public bool TryInitialize(NetworkBoardInitialization initialization) => true;
		public void ApplySnapshot(Godot.Collections.Dictionary snapshot) { }
		public void QueueVisualEvent(Godot.Collections.Dictionary payload) { }
		public void RejectAction(string reason, int turnSequenceGeneration, int rejectedHandIndex) { }
		public void ConfirmAction(long actionId, long clientActionId, long checkpointId, int snapshotId, int turnSequenceGeneration) { }
		public bool TryRestoreCheckpoint(ReadOnlySpan<byte> payload, string expectedHash, out string error)
		{
			error = string.Empty;
			return true;
		}
		public void AdvanceAuthorityEpoch(long authorityEpoch) { }
		public void BeginHostMigration() { }
		public void CompleteHostMigration() { }
	}
}
