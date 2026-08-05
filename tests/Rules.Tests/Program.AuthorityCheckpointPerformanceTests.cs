using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using Deckland.Rules;

internal static partial class Program
{
	private const int CheckpointBenchmarkWarmupIterations = 30;
	private const int CheckpointBenchmarkSampleIterations = 100;
	// 2026-08-04 reference baseline, captured before the checkpoint allocation changes.
	private const long BaselineProjectionAllocationBytes = 93_240;
	private const long BaselineEndToEndAllocationBytes = 334_042;
	private const double BaselineEndToEndP95Milliseconds = 1.263;
	private const string HighLoadCheckpointGoldenHash =
		"d549678ecae06a7e3e7f2924a8f45ed3641b028fc35a59c091d033ad7f070ae8";

	private static void CheckpointFourPlayerHighLoadBenchmark()
	{
		(MatchStateSnapshot snapshot, NetworkAuthorityCheckpoint template) =
			CreateFourPlayerHighLoadCheckpointFixture();
		byte[] sessionKey = new byte[HostMigrationSecurity.SessionKeyBytes];
		for (int index = 0; index < sessionKey.Length; index++)
		{
			sessionKey[index] = checked((byte)(index + 1));
		}

		using ECDsa signer = HostMigrationSecurity.CreateSigner();
		var context = new MigrationCheckpointSecurityContext(
			template.SessionId,
			template.MatchId,
			template.AuthorityEpoch,
			template.CheckpointId,
			"candidate-high-load",
			7,
			NetworkProtocol.CurrentVersion);
		string previousCheckpointHash = new('b', 64);

		for (int iteration = 0; iteration < CheckpointBenchmarkWarmupIterations; iteration++)
		{
			MeasureCheckpointProjection(template, snapshot);
			MeasureCheckpointEndToEnd(
				template,
				snapshot,
				sessionKey,
				context,
				previousCheckpointHash,
				signer);
		}

		var projectionAllocations = new long[CheckpointBenchmarkSampleIterations];
		var endToEndAllocations = new long[CheckpointBenchmarkSampleIterations];
		var endToEndElapsedTicks = new long[CheckpointBenchmarkSampleIterations];
		AuthorityCheckpointTelemetrySnapshot telemetryBefore = AuthorityCheckpointTelemetry.GetSnapshot();
		CheckpointBenchmarkResult lastResult = default;
		for (int iteration = 0; iteration < CheckpointBenchmarkSampleIterations; iteration++)
		{
			projectionAllocations[iteration] = MeasureCheckpointProjection(template, snapshot);
			lastResult = MeasureCheckpointEndToEnd(
				template,
				snapshot,
				sessionKey,
				context,
				previousCheckpointHash,
				signer);
			endToEndAllocations[iteration] = lastResult.AllocatedBytes;
			endToEndElapsedTicks[iteration] = lastResult.ElapsedTicks;
		}

		long projectionAllocationAverage = Average(projectionAllocations);
		long endToEndAllocationAverage = Average(endToEndAllocations);
		long endToEndP95Ticks = Percentile95(endToEndElapsedTicks);
		double endToEndP95Milliseconds =
			endToEndP95Ticks * 1000.0 / Stopwatch.Frequency;
		long maximumProjectionAllocation =
			BaselineProjectionAllocationBytes * 60 / 100;
		long maximumEndToEndAllocation =
			BaselineEndToEndAllocationBytes * 80 / 100;
		double maximumEndToEndP95Milliseconds =
			BaselineEndToEndP95Milliseconds * 1.10;
		Console.WriteLine(
			$"CHECKPOINT_HIGH_LOAD_BENCHMARK projection_alloc_avg={projectionAllocationAverage} " +
			$"end_to_end_alloc_avg={endToEndAllocationAverage} " +
			$"end_to_end_p95_ms={endToEndP95Milliseconds:F3} " +
			$"payload_bytes={lastResult.Payload.Length} hash={lastResult.Hash} " +
			$"limits={maximumProjectionAllocation}/{maximumEndToEndAllocation}/" +
			$"{maximumEndToEndP95Milliseconds:F3}");

		True(projectionAllocationAverage <= maximumProjectionAllocation);
		True(endToEndAllocationAverage <= maximumEndToEndAllocation);
		True(endToEndP95Milliseconds <= maximumEndToEndP95Milliseconds);
		True(lastResult.Payload.Length > 0);
		True(lastResult.Payload.Length <= NetworkAuthorityCheckpointCodec.MaxPayloadBytes);
		Equal(HighLoadCheckpointGoldenHash, lastResult.Hash);
		True(NetworkAuthorityCheckpointCodec.TryDecode(
			lastResult.Payload,
			lastResult.Hash,
			out NetworkAuthorityCheckpoint decoded,
			out string decodeError));
		Equal(string.Empty, decodeError);
		True(NetworkAuthorityCheckpointCodec.TryEncode(
			decoded,
			out byte[] roundtripPayload,
			out string roundtripHash,
			out string roundtripError));
		Equal(string.Empty, roundtripError);
		True(lastResult.Payload.AsSpan().SequenceEqual(roundtripPayload));
		Equal(lastResult.Hash, roundtripHash);

		byte[] tamperedCiphertext = lastResult.EncryptedEnvelope.GetCiphertextCopy();
		tamperedCiphertext[tamperedCiphertext.Length / 2] ^= 0x20;
		var tamperedEnvelope = new EncryptedAuthorityCheckpointEnvelope(
			lastResult.EncryptedEnvelope.Context,
			lastResult.EncryptedEnvelope.GetNonceCopy(),
			tamperedCiphertext,
			lastResult.EncryptedEnvelope.GetTagCopy(),
			lastResult.EncryptedEnvelope.PlaintextHash,
			lastResult.EncryptedEnvelope.CiphertextHash,
			lastResult.EncryptedEnvelope.PreviousCheckpointHash,
			lastResult.EncryptedEnvelope.SourceAuthorityKeyId,
			lastResult.EncryptedEnvelope.GetSignatureCopy());
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			tamperedEnvelope,
			sessionKey,
			HostMigrationSecurity.ExportPublicKey(signer),
			out _,
			out _));

		AuthorityCheckpointTelemetrySnapshot telemetryAfter = AuthorityCheckpointTelemetry.GetSnapshot();
		Equal(
			telemetryBefore.ProjectionCount + CheckpointBenchmarkSampleIterations * 2,
			telemetryAfter.ProjectionCount);
		Equal(
			telemetryBefore.EncodeCount + CheckpointBenchmarkSampleIterations + 1,
			telemetryAfter.EncodeCount);
		Equal(
			telemetryBefore.EncryptionCount + CheckpointBenchmarkSampleIterations,
			telemetryAfter.EncryptionCount);
		True(telemetryAfter.LastProjectionAllocatedBytes > 0);
		True(telemetryAfter.MaximumProjectionAllocatedBytes >=
			telemetryAfter.LastProjectionAllocatedBytes);
		True(telemetryAfter.LastProjectionMilliseconds > 0);
		True(telemetryAfter.MaximumProjectionMilliseconds >=
			telemetryAfter.LastProjectionMilliseconds);
		Equal(1_120, telemetryAfter.LastProjectedEntryCount);
		True(telemetryAfter.LastEncodeAllocatedBytes > 0);
		True(telemetryAfter.MaximumEncodeAllocatedBytes >=
			telemetryAfter.LastEncodeAllocatedBytes);
		True(telemetryAfter.LastEncodeMilliseconds > 0);
		True(telemetryAfter.MaximumEncodeMilliseconds >=
			telemetryAfter.LastEncodeMilliseconds);
		Equal(lastResult.Payload.Length, telemetryAfter.LastEncodedBytes);
		True(telemetryAfter.LastEncryptionAllocatedBytes > 0);
		True(telemetryAfter.MaximumEncryptionAllocatedBytes >=
			telemetryAfter.LastEncryptionAllocatedBytes);
		True(telemetryAfter.LastEncryptionMilliseconds > 0);
		True(telemetryAfter.MaximumEncryptionMilliseconds >=
			telemetryAfter.LastEncryptionMilliseconds);
		Equal(lastResult.Payload.Length, telemetryAfter.LastCiphertextBytes);
		Equal(
			telemetryBefore.ExplicitPlaintextCopyCount,
			telemetryAfter.ExplicitPlaintextCopyCount);
	}

	private static void CheckpointTelemetryBookkeepingIsAllocationFree()
	{
		AuthorityCheckpointTelemetry.RecordProjection(1, 1, 1);
		AuthorityCheckpointTelemetry.RecordEncoding(1, 1, 1);
		AuthorityCheckpointTelemetry.RecordEncryption(1, 1, 1);
		long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
		for (int iteration = 0; iteration < 100; iteration++)
		{
			AuthorityCheckpointTelemetry.RecordProjection(1, 1, 1);
			AuthorityCheckpointTelemetry.RecordEncoding(1, 1, 1);
			AuthorityCheckpointTelemetry.RecordEncryption(1, 1, 1);
		}
		long allocatedBytes =
			GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
		Equal(0L, allocatedBytes);
	}

	private static long MeasureCheckpointProjection(
		NetworkAuthorityCheckpoint template,
		MatchStateSnapshot snapshot)
	{
		long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
		NetworkAuthorityCheckpoint projected =
			NetworkAuthorityCheckpointRuleAdapter.ApplyRuleSnapshot(template, snapshot);
		long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
		Equal(snapshot.PlayerCount, projected.Decks.Count);
		Equal(snapshot.TableCards.Length, projected.TableCards.Count);
		GC.KeepAlive(projected);
		return allocatedBytes;
	}

	private static CheckpointBenchmarkResult MeasureCheckpointEndToEnd(
		NetworkAuthorityCheckpoint template,
		MatchStateSnapshot snapshot,
		ReadOnlySpan<byte> sessionKey,
		MigrationCheckpointSecurityContext context,
		string previousCheckpointHash,
		ECDsa signer)
	{
		long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
		long startedAt = Stopwatch.GetTimestamp();
		NetworkAuthorityCheckpoint projected =
			NetworkAuthorityCheckpointRuleAdapter.ApplyRuleSnapshot(template, snapshot);
		True(NetworkAuthorityCheckpointCodec.TryEncode(
			projected,
			out byte[] payload,
			out string hash,
			out string encodeError));
		Equal(string.Empty, encodeError);
		True(HostMigrationSecurity.TryEncryptCheckpoint(
			payload,
			hash,
			sessionKey,
			context,
			previousCheckpointHash,
			signer,
			out EncryptedAuthorityCheckpointEnvelope encryptedEnvelope,
			out string encryptionError));
		Equal(string.Empty, encryptionError);
		long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
		long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
		return new CheckpointBenchmarkResult(
			allocatedBytes,
			elapsedTicks,
			payload,
			hash,
			encryptedEnvelope);
	}

	private static (MatchStateSnapshot Snapshot, NetworkAuthorityCheckpoint Template)
		CreateFourPlayerHighLoadCheckpointFixture()
	{
		const int playerCount = 4;
		const int deckCardsPerPlayer = 208;
		const int handCardsPerPlayer = 4;
		const int itemCardsPerPlayer = 4;
		const int tableCardCount = 256;
		ImmutableArray<string> attachment = ImmutableArray.Create("magnet");
		long runtimeInstanceId = 1;
		var players = ImmutableArray.CreateBuilder<PlayerStateSnapshot>(playerCount);
		var roster = new List<NetworkMigrationRosterEntry>(playerCount);
		var controls = new List<NetworkAuthoritySeatControl>(playerCount);
		var claims = new List<NetworkAuthorityReconnectClaim>(playerCount);
		var actionIds = new Dictionary<int, long>(playerCount);
		for (int seat = 0; seat < playerCount; seat++)
		{
			var deck = ImmutableArray.CreateBuilder<LogicalCardState>(deckCardsPerPlayer);
			for (int index = 0; index < deckCardsPerPlayer; index++)
			{
				deck.Add(new LogicalCardState(
					runtimeInstanceId++,
					$"deck-{seat}-{index}",
					"spades",
					"ace",
					attachment));
			}

			var hand = ImmutableArray.CreateBuilder<LogicalCardState>(handCardsPerPlayer);
			for (int index = 0; index < handCardsPerPlayer; index++)
			{
				hand.Add(new LogicalCardState(
					runtimeInstanceId++,
					$"hand-{seat}-{index}",
					"hearts",
					"king",
					attachment));
			}

			var items = ImmutableArray.CreateBuilder<LogicalItemState>(itemCardsPerPlayer);
			for (int index = 0; index < itemCardsPerPlayer; index++)
			{
				items.Add(new LogicalItemState($"item-{seat}-{index}"));
			}

			players.Add(new PlayerStateSnapshot(
				seat,
				deck.MoveToImmutable(),
				hand.MoveToImmutable(),
				items.MoveToImmutable(),
				20 + seat));
			roster.Add(new NetworkMigrationRosterEntry
			{
				PlayerSessionId = $"peer:{seat}",
				DisplayName = $"Player {seat}",
				AvatarId = $"avatar-{seat}",
				SeatIndex = seat,
				PeerId = seat + 1,
				IsHost = seat == 0,
				IsReady = true,
				IsConnected = true,
				CredentialsAcknowledged = true,
			});
			controls.Add(new NetworkAuthoritySeatControl
			{
				SeatIndex = seat,
				Mode = SeatControlMode.HumanConnected,
				PeerId = seat + 1,
				PlayerSessionId = $"peer:{seat}",
				CredentialVerifier = SeatCredentialRules.ComputeVerifier($"credential-{seat}"),
				CredentialGeneration = SeatCredentialRules.InitialGeneration,
			});
			claims.Add(new NetworkAuthorityReconnectClaim
			{
				MatchId = "match-high-load",
				SeatIndex = seat,
				CredentialGeneration = SeatCredentialRules.InitialGeneration,
				PlayerSessionId = $"peer:{seat}",
				ClaimState = nameof(ReconnectClaimState.Unclaimed),
			});
			actionIds[seat] = 100 + seat;
		}

		var tableCards = ImmutableArray.CreateBuilder<TableCardState>(tableCardCount);
		for (int index = 0; index < tableCardCount; index++)
		{
			int ownerSeat = index % playerCount;
			tableCards.Add(new TableCardState(
				runtimeInstanceId++,
				new LogicalCardState(
					runtimeInstanceId++,
					$"table-{index}",
					"clubs",
					"queen",
					attachment),
				IsFaceUp: true,
				IsHiddenDuringCapture: false,
				IsBuried: index % 5 == 0,
				IsScoreSuppressed: index % 7 == 0,
				ItemOverlay: new LogicalItemState("magnet"),
				ItemOwnerSeat: ownerSeat,
				CardOwnerSeat: ownerSeat,
				RemoveOnCapture: index % 3 == 0));
		}

		var snapshot = new MatchStateSnapshot(
			MatchStateSnapshot.CurrentSchemaVersion,
			Revision: 77,
			LifecycleGeneration: 3,
			TurnGeneration: 9,
			PlayerCount: playerCount,
			WinningScore: 200,
			CurrentTurnSeat: 2,
			Outcome: Deckland.Rules.MatchOutcome.InProgress,
			WinnerSeat: -1,
			Players: players.MoveToImmutable(),
			TableCards: tableCards.MoveToImmutable(),
			RandomState: new RuleRandomState(20260804, 987654321),
			PendingPresentationBarrierId: 0,
			AppliedActionIds: ImmutableArray<long>.Empty,
			CompletedTurnsSinceQuestionRefresh: 3,
			QuestionRefreshAnchorSeat: 1);
		var template = new NetworkAuthorityCheckpoint
		{
			SessionId = "session-high-load",
			MatchId = "match-high-load",
			RoomName = "High Load Room",
			MatchSeed = 20260804,
			ItemMode = true,
			Transport = NetworkTransport.Lan,
			ManifestHash = new string('a', 64),
			Roster = roster,
			SeatControls = controls,
			LastAcceptedClientActionIds = actionIds,
			OpeningDealCompleted = true,
			LastAuthorityActionId = 0,
			AuthorityAction = AuthorityActionState.Empty,
			SnapshotId = 41,
			AuthorityEpoch = 6,
			CheckpointId = 88,
			ReconnectClaims = claims,
		};
		return (snapshot, template);
	}

	private static long Average(long[] values)
	{
		long total = 0;
		foreach (long value in values)
		{
			total += value;
		}
		return total / values.Length;
	}

	private static long Percentile95(long[] values)
	{
		long[] sorted = (long[])values.Clone();
		Array.Sort(sorted);
		return sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1];
	}

	private readonly record struct CheckpointBenchmarkResult(
		long AllocatedBytes,
		long ElapsedTicks,
		byte[] Payload,
		string Hash,
		EncryptedAuthorityCheckpointEnvelope EncryptedEnvelope);
}
