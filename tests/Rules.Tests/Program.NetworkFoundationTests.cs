using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Deckland.Rules;

internal static partial class Program
{
	private static void RunNetworkFoundationTests()
	{
		Run("authority action phases are ordered and resumable", AuthorityActionPhasesAreOrderedAndResumable);
		Run("authority action validation uses kind phase and player count semantics", AuthorityActionValidationUsesKindPhaseAndPlayerCountSemantics);
		Run("authority action mutation recovery is idempotent", AuthorityActionMutationRecoveryIsIdempotent);
		Run("authority action nested discard preserves original target", AuthorityActionNestedDiscardPreservesOriginalTarget);
		Run("authority action finalizing phase can resume", AuthorityActionFinalizingPhaseCanResume);
		Run("authority action dedupe accepts only newer client ids", AuthorityActionDedupeAcceptsOnlyNewerClientIds);
		Run("authority action dedupe is isolated per seat", AuthorityActionDedupeIsIsolatedPerSeat);
		Run("authority pending selection can commit only once", AuthorityPendingSelectionCanCommitOnlyOnce);
		Run("authority action json roundtrip keeps fixed fields", AuthorityActionJsonRoundtripKeepsFixedFields);
		Run("checkpoint codec roundtrips private deterministic state", CheckpointCodecRoundtripsPrivateDeterministicState);
		Run("checkpoint codec validates opening dice by player count", CheckpointCodecValidatesOpeningDiceByPlayerCount);
		Run("checkpoint rule adapter roundtrips fields bytes hash and random continuation", CheckpointRuleAdapterRoundtripsFieldsBytesHashAndRandomContinuation);
		Run("checkpoint four player high load benchmark", CheckpointFourPlayerHighLoadBenchmark);
		Run("checkpoint telemetry bookkeeping is allocation free", CheckpointTelemetryBookkeepingIsAllocationFree);
		Run("checkpoint rule adapter rejects invalid state without mutation", CheckpointRuleAdapterRejectsInvalidStateWithoutMutation);
		Run("checkpoint restore source uses atomic rule session boundary", CheckpointRestoreSourceUsesAtomicRuleSessionBoundary);
		Run("checkpoint codec roundtrips draw outcome", CheckpointCodecRoundtripsDrawOutcome);
		Run("checkpoint codec rejects tampered hash and oversized payload", CheckpointCodecRejectsTamperedHashAndOversizedPayload);
		Run("checkpoint codec rejects malformed object graphs without throwing", CheckpointCodecRejectsMalformedObjectGraphsWithoutThrowing);
		Run("checkpoint codec accepts ownerless face-up special table cards", CheckpointCodecAcceptsOwnerlessFaceUpSpecialTableCards);
		Run("checkpoint codec rejects excessive json depth with fixed error", CheckpointCodecRejectsExcessiveJsonDepthWithFixedError);
		Run("checkpoint envelope keeps the previous atomic value on encode failure", CheckpointEnvelopeKeepsPreviousAtomicValueOnEncodeFailure);
		Run("network protocol contexts fence epochs and snapshots", NetworkProtocolContextsFenceEpochsAndSnapshots);
		Run("migration restore generation is strictly monotonic", MigrationRestoreGenerationIsStrictlyMonotonic);
		Run("multiplayer rematch accepts only completed current-match players", MultiplayerRematchAcceptsOnlyCompletedCurrentMatchPlayers);
		Run("multiplayer rematch returns connected ready players to preserved lobby", MultiplayerRematchReturnsConnectedReadyPlayersToPreservedLobby);
		Run("migration plan selects the lowest eligible standby", MigrationPlanSelectsLowestEligibleStandby);
		Run("migration candidate score prefers stable acknowledgements then latency", MigrationCandidateScorePrefersStableAcknowledgementsThenLatency);
		Run("standby checkpoint sends are serialized and rate bounded", StandbyCheckpointSendsAreSerializedAndRateBounded);
		Run("migration coordinator permits only ordered recovery transitions", MigrationCoordinatorPermitsOnlyOrderedRecoveryTransitions);
		Run("active host migration freezes checkpoint and standby maintenance", ActiveHostMigrationFreezesCheckpointAndStandbyMaintenance);
		Run("checkpoint commit results classify local and migration failures", CheckpointCommitResultsClassifyLocalAndMigrationFailures);
		Run("migration plan revocation is signed and tamper evident", MigrationPlanRevocationIsSignedAndTamperEvident);
		Run("revocation blocks plan replay and stale checkpoint eligibility", RevocationBlocksPlanReplayAndStaleCheckpointEligibility);
		Run("migration plan rejects stale or unsafe endpoints", MigrationPlanRejectsStaleOrUnsafeEndpoints);
		Run("Steam owner convergence follows the signed candidate", SteamOwnerConvergenceFollowsSignedCandidate);
		Run("migration peer rebuild waits for source disconnect", MigrationPeerRebuildWaitsForSourceDisconnect);
		Run("migration checkpoint chain is scoped to session match and epoch", MigrationCheckpointChainIsScopedToSessionMatchAndEpoch);
		Run("migration rekey handshake continues into seat authentication", MigrationRekeyHandshakeContinuesIntoSeatAuthentication);
		Run("room password challenge rejects plaintext replay substitutes", RoomPasswordChallengeRejectsPlaintextReplaySubstitutes);
		Run("migration key agreement derives peer unique session keys", MigrationKeyAgreementDerivesPeerUniqueSessionKeys);
		Run("encrypted migration checkpoint rejects tampering and wrong identities", EncryptedMigrationCheckpointRejectsTamperingAndWrongIdentities);
		Run("migration nonce replay guard rejects duplicates and resets per epoch", MigrationNonceReplayGuardRejectsDuplicatesAndResetsPerEpoch);
		Run("migration serialization excludes raw secrets", MigrationSerializationExcludesRawSecrets);
		Run("release debug api surface cannot export migration secrets", ReleaseDebugApiSurfaceCannotExportMigrationSecrets);
		Run("host migration capability is release-disabled with an isolated debug fixture", HostMigrationCapabilityIsReleaseDisabledWithDebugFixture);
		Run("lobby ready intent is consumed after credential acknowledgement", LobbyReadyIntentIsConsumedAfterCredentialAcknowledgement);
		Run("local lobby ready intent retries until authority echo", LocalLobbyReadyIntentRetriesUntilAuthorityEcho);
		Run("lobby initial seat credential delivery retries across sync ordering", LobbyInitialSeatCredentialDeliveryRetriesAcrossSyncOrdering);
		Run("client board fingerprint preserves redacted seat shape", ClientBoardFingerprintPreservesRedactedSeatShape);
		Run("seat credentials issue verify rotate and reject replay", SeatCredentialsIssueVerifyRotateAndRejectReplay);
		Run("seat credential pending rotation cancels back to current generation", SeatCredentialPendingRotationCancelsBackToCurrentGeneration);
		Run("peer rate limiter enforces category isolation refill cleanup and disconnect threshold", PeerRateLimiterEnforcesCategoryIsolationRefillCleanupAndDisconnectThreshold);
		Run("lobby migration recovery tracks claims and ten second expiry", LobbyMigrationRecoveryTracksClaimsAndTenSecondExpiry);
		Run("game migration recovery waits for snapshot acknowledgements", GameMigrationRecoveryWaitsForSnapshotAcknowledgements);
		Run("game migration recovery removes disconnected seats without extending deadline", GameMigrationRecoveryRemovesDisconnectedSeatsWithoutExtendingDeadline);
		Run("game migration recovery expires after fixed ten seconds", GameMigrationRecoveryExpiresAfterFixedTenSeconds);
		Run("late game migration readiness matches only the completed current epoch", LateGameMigrationReadinessMatchesOnlyTheCompletedCurrentEpoch);
	}

}
