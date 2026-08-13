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

	private static void AuthorityActionPhasesAreOrderedAndResumable()
	{
		True(AuthorityActionStateRules.CanAdvancePhase(AuthorityActionPhase.Prepared, AuthorityActionPhase.Applying));
		True(AuthorityActionStateRules.CanAdvancePhase(AuthorityActionPhase.Applying, AuthorityActionPhase.WaitingDiscard));
		True(AuthorityActionStateRules.CanAdvancePhase(AuthorityActionPhase.WaitingDiscard, AuthorityActionPhase.Applying));
		True(AuthorityActionStateRules.CanAdvancePhase(AuthorityActionPhase.WaitingAttachment, AuthorityActionPhase.Finalizing));
		False(AuthorityActionStateRules.CanAdvancePhase(AuthorityActionPhase.Finalizing, AuthorityActionPhase.Applying));
		AuthorityActionState prepared = new()
		{
			ActionId = 6,
			ClientActionId = 1,
			Kind = AuthorityActionKind.BaseCard,
			Phase = AuthorityActionPhase.Prepared,
			ActorSeat = 0,
			TurnGeneration = 4,
			BaseHandIndex = 1,
			BaseCardCode = "Hearts:5",
			RngStateBeforeAction = "123456",
		};
		True(AuthorityActionStateRules.IsValid(prepared, 2));
		True(AuthorityActionStateRules.IsStableCheckpointState(prepared));

		AuthorityActionState state = new()
		{
			ActionId = 7,
			ClientActionId = 1,
			Kind = AuthorityActionKind.ItemCard,
			Phase = AuthorityActionPhase.Applying,
			ActorSeat = 0,
			TurnGeneration = 4,
			ItemHandIndex = 1,
			ItemOrRewardId = "lens",
		};
		True(AuthorityActionStateRules.IsValid(state, 2));
		False(AuthorityActionStateRules.IsStableCheckpointState(state));
		AuthorityActionState transfer = state with
		{
			Kind = AuthorityActionKind.CardTransfer,
			TargetSeat = 1,
			TargetHandIndex = 2,
			ContinuationStep = AuthorityActionContinuationStep.CardTransfer,
		};
		AuthorityActionState waitingDiscard = transfer.WithSelection(AuthorityActionPhase.WaitingDiscard, 1);
		Equal(AuthorityActionPhase.WaitingDiscard, waitingDiscard.Phase);
		Equal(1, waitingDiscard.SelectionIndex);
		Equal(1, waitingDiscard.TargetSeat);
		Equal(2, waitingDiscard.TargetHandIndex);
	}

	private static void AuthorityActionValidationUsesKindPhaseAndPlayerCountSemantics()
	{
		AuthorityActionState transfer = new()
		{
			ActionId = 1,
			ClientActionId = 1,
			Kind = AuthorityActionKind.CardTransfer,
			Phase = AuthorityActionPhase.Applying,
			ActorSeat = 0,
			TurnGeneration = 1,
			ItemHandIndex = 0,
			ItemOrRewardId = "Q",
			TargetSeat = 1,
			TargetHandIndex = 2,
			ContinuationStep = AuthorityActionContinuationStep.CardTransfer,
		};
		True(AuthorityActionStateRules.IsValid(transfer, 2));
		False(AuthorityActionStateRules.IsValid(transfer, 1));
		False(AuthorityActionStateRules.IsValid(transfer, 5));

		False(AuthorityActionStateRules.IsValid(transfer with
		{
			Kind = AuthorityActionKind.ItemCard,
		}, 2));
		False(AuthorityActionStateRules.IsValid(transfer with
		{
			TargetSeat = -1,
		}, 2));
		False(AuthorityActionStateRules.IsValid(transfer with
		{
			TransferMutationApplied = true,
			PrimaryMutationApplied = false,
		}, 2));

		AuthorityActionState reward = transfer with
		{
			Kind = AuthorityActionKind.QuestionReward,
			Phase = AuthorityActionPhase.Applying,
			ItemHandIndex = -1,
			ItemOrRewardId = "bomb",
			TargetSeat = -1,
			TargetHandIndex = -1,
			ContinuationStep = AuthorityActionContinuationStep.QuestionRewardDelivery,
			PendingRewardItemId = "bomb",
			PendingRewardSeat = 0,
		};
		True(AuthorityActionStateRules.IsValid(reward, 2));
		False(AuthorityActionStateRules.IsValid(reward with { ItemHandIndex = 0 }, 2));
	}

	private static void AuthorityActionMutationRecoveryIsIdempotent()
	{
		AuthorityActionState transfer = new()
		{
			ActionId = 21,
			ClientActionId = 7,
			Kind = AuthorityActionKind.CardTransfer,
			Phase = AuthorityActionPhase.Applying,
			ActorSeat = 0,
			TurnGeneration = 3,
			ItemHandIndex = 0,
			ItemOrRewardId = "Q",
			TargetSeat = 1,
			TargetHandIndex = 2,
			ContinuationStep = AuthorityActionContinuationStep.CardTransfer,
			PrimaryMutationApplied = true,
		};
		True(AuthorityActionStateRules.IsValid(transfer, 2));
		False(AuthorityActionStateRules.ShouldApplyMutation(transfer, AuthorityActionMutationKind.Primary));

		AuthorityActionState transferApplied = AuthorityActionStateRules.MarkMutationApplied(
			transfer,
			AuthorityActionMutationKind.CardTransfer);
		False(AuthorityActionStateRules.ShouldApplyMutation(transferApplied, AuthorityActionMutationKind.CardTransfer));
		AuthorityActionState transferAppliedAgain = AuthorityActionStateRules.MarkMutationApplied(
			transferApplied,
			AuthorityActionMutationKind.CardTransfer);
		Equal(transferApplied.TransferMutationApplied, transferAppliedAgain.TransferMutationApplied);
		Equal(transferApplied.PrimaryMutationApplied, transferAppliedAgain.PrimaryMutationApplied);

		AuthorityActionState reward = new()
		{
			ActionId = 22,
			Kind = AuthorityActionKind.QuestionReward,
			Phase = AuthorityActionPhase.Applying,
			ActorSeat = 0,
			TurnGeneration = 3,
			ItemOrRewardId = "magnet",
			PendingRewardItemId = "magnet",
			PendingRewardSeat = 0,
			PendingRewardRequiresAttachment = true,
			ContinuationStep = AuthorityActionContinuationStep.QuestionRewardDelivery,
		};
		True(AuthorityActionStateRules.IsValid(reward, 2));
		AuthorityActionState rewardApplied = AuthorityActionStateRules.MarkMutationApplied(
			reward,
			AuthorityActionMutationKind.QuestionReward);
		False(AuthorityActionStateRules.ShouldApplyMutation(rewardApplied, AuthorityActionMutationKind.QuestionReward));
		Equal(
			rewardApplied.QuestionRewardMutationApplied,
			AuthorityActionStateRules.MarkMutationApplied(
				rewardApplied,
				AuthorityActionMutationKind.QuestionReward).QuestionRewardMutationApplied);

		AuthorityActionState attachment = new()
		{
			ActionId = 23,
			Kind = AuthorityActionKind.QuestionReward,
			Phase = AuthorityActionPhase.WaitingAttachment,
			ActorSeat = 0,
			TurnGeneration = 3,
			ItemOrRewardId = "magnet",
			SelectionIndex = 1,
			PendingRewardItemId = "magnet",
			PendingRewardSeat = 0,
			PendingRewardRequiresAttachment = true,
			ContinuationStep = AuthorityActionContinuationStep.AttachmentQueue,
		};
		True(AuthorityActionStateRules.IsValid(attachment, 2));
		AuthorityActionState attachmentApplied = AuthorityActionStateRules.MarkMutationApplied(
			attachment,
			AuthorityActionMutationKind.Attachment);
		False(AuthorityActionStateRules.ShouldApplyMutation(attachmentApplied, AuthorityActionMutationKind.Attachment));
	}

	private static void AuthorityActionNestedDiscardPreservesOriginalTarget()
	{
		AuthorityActionState nestedDiscard = new()
		{
			ActionId = 24,
			Kind = AuthorityActionKind.CardTransfer,
			Phase = AuthorityActionPhase.WaitingDiscard,
			ActorSeat = 0,
			TurnGeneration = 3,
			ItemHandIndex = 0,
			ItemOrRewardId = "Q",
			TargetSeat = 1,
			TargetHandIndex = 2,
			SelectionIndex = 3,
			ContinuationStep = AuthorityActionContinuationStep.CardTransfer,
			PrimaryMutationApplied = true,
		};
		True(AuthorityActionStateRules.IsValid(nestedDiscard, 2));

		AuthorityActionState resumed = AuthorityActionStateRules.AdvanceAfterDiscardMutation(nestedDiscard);
		True(AuthorityActionStateRules.IsValid(resumed, 2));
		Equal(AuthorityActionPhase.Applying, resumed.Phase);
		Equal(1, resumed.TargetSeat);
		Equal(2, resumed.TargetHandIndex);
		Equal(-1, resumed.SelectionIndex);
		False(AuthorityActionStateRules.ShouldApplyMutation(resumed, AuthorityActionMutationKind.Discard));
	}

	private static void AuthorityActionFinalizingPhaseCanResume()
	{
		AuthorityActionState finalizing = new()
		{
			ActionId = 25,
			Kind = AuthorityActionKind.BaseCard,
			Phase = AuthorityActionPhase.Finalizing,
			ActorSeat = 0,
			TurnGeneration = 3,
			BaseHandIndex = 1,
			ContinuationStep = AuthorityActionContinuationStep.TurnAdvancement,
			PrimaryMutationApplied = true,
			Played = true,
			CompletesTurn = true,
		};
		True(AuthorityActionStateRules.IsValid(finalizing, 2));
		True(AuthorityActionStateRules.CanResumeFinalizing(finalizing));
	}

	private static void AuthorityActionDedupeAcceptsOnlyNewerClientIds()
	{
		Equal(ClientActionAcceptance.Accepted, AuthorityActionStateRules.ClassifyClientAction(1, 0));
		Equal(ClientActionAcceptance.DuplicateOrOlder, AuthorityActionStateRules.ClassifyClientAction(1, 1));
		Equal(ClientActionAcceptance.DuplicateOrOlder, AuthorityActionStateRules.ClassifyClientAction(0, 1));
		Equal(ClientActionAcceptance.Invalid, AuthorityActionStateRules.ClassifyClientAction(-1, 0));
		True(AuthorityActionStateRules.TryAcceptClientAction(9, 8, out long next));
		Equal(9L, next);
		False(AuthorityActionStateRules.TryAcceptClientAction(8, 9, out next));
		Equal(9L, next);
	}

	private static void AuthorityActionDedupeIsIsolatedPerSeat()
	{
		var lastAcceptedBySeat = new Dictionary<int, long>
		{
			[0] = 12,
			[1] = 4,
		};

		Equal(
			ClientActionAcceptance.DuplicateOrOlder,
			AuthorityActionStateRules.ClassifyClientAction(12, lastAcceptedBySeat[0]));
		Equal(
			ClientActionAcceptance.Accepted,
			AuthorityActionStateRules.ClassifyClientAction(5, lastAcceptedBySeat[1]));
		Equal(
			ClientActionAcceptance.Accepted,
			AuthorityActionStateRules.ClassifyClientAction(13, lastAcceptedBySeat[0]));
	}

	private static void AuthorityActionJsonRoundtripKeepsFixedFields()
	{
		AuthorityActionState source = new()
		{
			ActionId = 12,
			ClientActionId = 33,
			Kind = AuthorityActionKind.CardTransfer,
			Phase = AuthorityActionPhase.WaitingDiscard,
			ActorSeat = 1,
			TurnGeneration = 5,
			BaseHandIndex = -1,
			ItemHandIndex = 2,
			ItemOrRewardId = "queen",
			TargetSeat = 0,
			TargetHandIndex = 3,
			ReserveReplacement = true,
			Played = true,
			Captured = false,
			CompletesTurn = true,
			DeckCollected = false,
			RemainingAttachmentRewardQueue = new[] { "magnet" },
			RemainingAttachmentRewardQueueSeats = new[] { 0 },
			ContinuationStep = AuthorityActionContinuationStep.CardTransfer,
			PrimaryMutationApplied = true,
			DiscardMutationApplied = true,
			BaseCardCode = string.Empty,
			RngStateBeforeAction = "987654321",
		};
		string json = AuthorityActionStateRules.Serialize(source);
		True(AuthorityActionStateRules.TryDeserialize(json, out AuthorityActionState restored));
		Equal(source.ActionId, restored.ActionId);
		Equal(source.ClientActionId, restored.ClientActionId);
		Equal(source.Kind, restored.Kind);
		Equal(source.Phase, restored.Phase);
		Equal(source.ActorSeat, restored.ActorSeat);
		Equal(source.TargetHandIndex, restored.TargetHandIndex);
		Equal(source.RemainingAttachmentRewardQueue[0], restored.RemainingAttachmentRewardQueue[0]);
		Equal(source.DiscardMutationApplied, restored.DiscardMutationApplied);
		Equal(source.RngStateBeforeAction, restored.RngStateBeforeAction);
	}

	private static void AuthorityPendingSelectionCanCommitOnlyOnce()
	{
		AuthorityActionState waiting = new()
		{
			ActionId = 10,
			ClientActionId = 2,
			Kind = AuthorityActionKind.ItemCard,
			Phase = AuthorityActionPhase.WaitingAttachment,
			ActorSeat = 1,
			TurnGeneration = 7,
			ItemHandIndex = 0,
			ItemOrRewardId = "magnet",
			ContinuationStep = AuthorityActionContinuationStep.AttachmentQueue,
		};
		True(AuthorityActionStateRules.CanCommitPendingSelection(
			waiting,
			AuthorityActionPhase.WaitingAttachment,
			actorSeat: 1,
			turnGeneration: 7));

		AuthorityActionState committed = waiting.WithSelection(
			AuthorityActionPhase.WaitingAttachment,
			selectionIndex: 3);
		False(AuthorityActionStateRules.CanCommitPendingSelection(
			committed,
			AuthorityActionPhase.WaitingAttachment,
			actorSeat: 1,
			turnGeneration: 7));
		False(AuthorityActionStateRules.CanCommitPendingSelection(
			waiting,
			AuthorityActionPhase.WaitingDiscard,
			actorSeat: 1,
			turnGeneration: 7));
	}

	private static void CheckpointCodecRoundtripsPrivateDeterministicState()
	{
		NetworkAuthorityCheckpoint source = CreateCheckpoint();
		True(NetworkAuthorityCheckpointCodec.TryEncode(source, out byte[] payload, out string hash, out string error));
		True(string.IsNullOrEmpty(error));
		True(payload.Length > 0);
		True(hash.Length == 64);
		True(NetworkAuthorityCheckpointCodec.TryDecode(payload, hash, out NetworkAuthorityCheckpoint restored, out error));
		True(string.IsNullOrEmpty(error));
		Equal(source.SessionId, restored.SessionId);
		Equal(source.MatchId, restored.MatchId);
		Equal(source.RoomPasswordVerifierVersion, restored.RoomPasswordVerifierVersion);
		Equal(source.RoomPasswordVerifierIterations, restored.RoomPasswordVerifierIterations);
		Equal(source.RoomPasswordVerifierSalt, restored.RoomPasswordVerifierSalt);
		Equal(source.RoomPasswordVerifier, restored.RoomPasswordVerifier);
		Equal(source.PlayerCount, restored.PlayerCount);
		Equal(source.SnapshotId, restored.SnapshotId);
		Equal(source.AuthorityEpoch, restored.AuthorityEpoch);
		Equal(source.CheckpointId, restored.CheckpointId);
		False(Encoding.UTF8.GetString(payload).Contains("secret", StringComparison.Ordinal));
		Equal(SeatCredentialRules.ComputeVerifier("credential-0"), restored.SeatControls[0].CredentialVerifier);
		Equal(19L, restored.LastAcceptedClientActionIds[1]);
	}

	private static void CheckpointRuleAdapterRoundtripsFieldsBytesHashAndRandomContinuation()
	{
		var sourceRandom = new TestRuleRandomSource(20260804);
		var sourceSession = new AuthorityRuleSession(sourceRandom, MatchRuleProfile.Empty);
		True(sourceSession.Submit(CreateInitializeCommand(actionId: 7001)).Accepted);
		MatchStateSnapshot sourceSnapshot = sourceSession.CaptureSnapshot() with
		{
			CompletedTurnsSinceQuestionRefresh = 4,
			QuestionRefreshAnchorSeat = 1,
		};
		NetworkAuthorityCheckpoint template = CreateCheckpoint() with
		{
			RoomPasswordVerifierVersion = 0,
			RoomPasswordVerifierIterations = 0,
			RoomPasswordVerifierSalt = string.Empty,
			RoomPasswordVerifier = string.Empty,
			CheckpointId = 19,
			SnapshotId = 23,
		};

		NetworkAuthorityCheckpoint first =
			NetworkAuthorityCheckpointRuleAdapter.ApplyRuleSnapshot(
				template,
				sourceSnapshot);
		Equal(sourceSnapshot.PlayerCount, first.PlayerCount);
		Equal(sourceSnapshot.WinningScore, first.WinningScore);
		Equal(sourceSnapshot.CurrentTurnSeat, first.CurrentTurnPlayerIndex);
		Equal(sourceSnapshot.TurnGeneration, first.TurnSequenceGeneration);
		Equal(
			sourceSnapshot.CompletedTurnsSinceQuestionRefresh,
			first.CompletedTurnsSinceQuestionRefresh);
		Equal(sourceSnapshot.QuestionRefreshAnchorSeat, first.QuestionRefreshAnchorPlayerIndex);
		Equal(unchecked((long)sourceSnapshot.RandomState.Seed), first.RngSeed);
		Equal(
			sourceSnapshot.RandomState.State.ToString(System.Globalization.CultureInfo.InvariantCulture),
			first.RngState);
		Equal(sourceSnapshot.Players.Length, first.Decks.Count);
		for (int seat = 0; seat < sourceSnapshot.Players.Length; seat++)
		{
			PlayerStateSnapshot expectedPlayer = sourceSnapshot.Players[seat];
			Equal(expectedPlayer.Deck.Length, first.Decks[seat].Count);
			Equal(expectedPlayer.Hand.Length, first.Hands[seat].Count);
			Equal(expectedPlayer.ItemHand.Length, first.ItemHands[seat].Count);
			Equal(expectedPlayer.Score, first.Scores[seat]);
			for (int index = 0; index < expectedPlayer.Deck.Length; index++)
			{
				Equal(expectedPlayer.Deck[index].Code, first.Decks[seat][index].Code);
				True(expectedPlayer.Deck[index].NormalizedAttachedEffectIds.SequenceEqual(
					first.Decks[seat][index].AttachedItemCardIds));
			}
			for (int index = 0; index < expectedPlayer.Hand.Length; index++)
			{
				Equal(expectedPlayer.Hand[index].Code, first.Hands[seat][index].Code);
				True(expectedPlayer.Hand[index].NormalizedAttachedEffectIds.SequenceEqual(
					first.Hands[seat][index].AttachedItemCardIds));
			}
		}

		True(NetworkAuthorityCheckpointRuleAdapter.TryCreateRuleSnapshot(
			first,
			sourceSnapshot.LifecycleGeneration,
			sourceSnapshot.TurnGeneration,
			sourceSnapshot.Players,
			sourceSnapshot.TableCards,
			new RuleRandomState(9, 9),
			ImmutableArray<PendingAttachmentRewardState>.Empty,
			out MatchStateSnapshot restoredSnapshot,
			out string restoreError));
		True(string.IsNullOrEmpty(restoreError));
		Equal(sourceSnapshot.PlayerCount, restoredSnapshot.PlayerCount);
		Equal(sourceSnapshot.WinningScore, restoredSnapshot.WinningScore);
		Equal(sourceSnapshot.CurrentTurnSeat, restoredSnapshot.CurrentTurnSeat);
		Equal(sourceSnapshot.TurnGeneration, restoredSnapshot.TurnGeneration);
		Equal(sourceSnapshot.Outcome, restoredSnapshot.Outcome);
		Equal(sourceSnapshot.WinnerSeat, restoredSnapshot.WinnerSeat);
		Equal(sourceSnapshot.RandomState, restoredSnapshot.RandomState);
		Equal(
			sourceSnapshot.CompletedTurnsSinceQuestionRefresh,
			restoredSnapshot.CompletedTurnsSinceQuestionRefresh);
		Equal(sourceSnapshot.QuestionRefreshAnchorSeat, restoredSnapshot.QuestionRefreshAnchorSeat);

		NetworkAuthorityCheckpoint second =
			NetworkAuthorityCheckpointRuleAdapter.ApplyRuleSnapshot(
				template,
				restoredSnapshot);
		True(NetworkAuthorityCheckpointCodec.TryEncode(
			first,
			out byte[] firstPayload,
			out string firstHash,
			out string firstEncodeError));
		True(string.IsNullOrEmpty(firstEncodeError));
		True(NetworkAuthorityCheckpointCodec.TryEncode(
			second,
			out byte[] secondPayload,
			out string secondHash,
			out string secondEncodeError));
		True(string.IsNullOrEmpty(secondEncodeError));
		True(firstPayload.AsSpan().SequenceEqual(secondPayload));
		Equal(firstHash, secondHash);
		Equal("51f4591f32a6f583d3c7ecb3214f655f80f0a6dd9d38d5ac194219bf3ecc8999", firstHash);

		int[] expectedContinuation =
		{
			sourceRandom.NextIntInclusive(0, 10_000),
			sourceRandom.NextIntInclusive(0, 10_000),
			sourceRandom.NextIntInclusive(0, 10_000),
		};
		var restoredRandom = new TestRuleRandomSource(1);
		AuthorityRuleSession restoredSession = AuthorityRuleSession.Restore(
			restoredSnapshot,
			restoredRandom,
			MatchRuleProfile.Empty);
		True(restoredSession.ReadModel.IsInitialized);
		foreach (int expected in expectedContinuation)
		{
			Equal(expected, restoredRandom.NextIntInclusive(0, 10_000));
		}
	}

	private static void CheckpointRuleAdapterRejectsInvalidStateWithoutMutation()
	{
		var protectedRandom = new TestRuleRandomSource(8102);
		var protectedSession = new AuthorityRuleSession(
			protectedRandom,
			MatchRuleProfile.Empty);
		True(protectedSession.Submit(CreateInitializeCommand(actionId: 7101)).Accepted);
		MatchReadModel readModelBefore = protectedSession.ReadModel;
		RuleRandomState randomBefore = protectedRandom.CaptureState();
		MatchStateSnapshot currentSnapshot = protectedSession.CaptureSnapshot();
		NetworkAuthorityCheckpoint checkpoint =
			NetworkAuthorityCheckpointRuleAdapter.ApplyRuleSnapshot(
				CreateCheckpoint(),
				currentSnapshot) with
			{
				RngState = "0",
			};

		False(NetworkAuthorityCheckpointRuleAdapter.TryCreateRuleSnapshot(
			checkpoint,
			currentSnapshot.LifecycleGeneration + 1,
			currentSnapshot.TurnGeneration + 1,
			currentSnapshot.Players,
			currentSnapshot.TableCards,
			randomBefore,
			ImmutableArray<PendingAttachmentRewardState>.Empty,
			out _,
			out string error));
		Equal("checkpoint RNG state is invalid", error);
		Equal(randomBefore, protectedRandom.CaptureState());
		AssertReadModelsEquivalent(readModelBefore, protectedSession.ReadModel);

		False(NetworkAuthorityCheckpointRuleAdapter.TryCreateRuleSnapshot(
			checkpoint with { RngState = currentSnapshot.RandomState.State.ToString() },
			currentSnapshot.LifecycleGeneration + 1,
			currentSnapshot.TurnGeneration + 1,
			default,
			currentSnapshot.TableCards,
			randomBefore,
			ImmutableArray<PendingAttachmentRewardState>.Empty,
			out _,
			out _));
		Equal(randomBefore, protectedRandom.CaptureState());
		AssertReadModelsEquivalent(readModelBefore, protectedSession.ReadModel);
	}

	private static void CheckpointRestoreSourceUsesAtomicRuleSessionBoundary()
	{
		string root = FindRepositoryRoot();
		string checkpointSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.NetworkCheckpoint.cs"));
		string adapterSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.RuleSessionAdapter.cs"));
		int restoreStart = checkpointSource.IndexOf(
			"internal bool TryRestoreAuthorityCheckpoint(",
			StringComparison.Ordinal);
		int restoreEnd = checkpointSource.IndexOf(
			"private bool TryBuildAuthorityRuleSnapshotFromCheckpoint(",
			restoreStart,
			StringComparison.Ordinal);
		True(restoreStart >= 0 && restoreEnd > restoreStart);
		string restoreBody = checkpointSource[restoreStart..restoreEnd];
		True(restoreBody.Contains("TryBuildRestoredState(", StringComparison.Ordinal));
		True(restoreBody.Contains("TryRestoreAuthorityRuleSnapshot(", StringComparison.Ordinal));
		False(Regex.IsMatch(
			restoreBody,
			@"_(?:playerDecks|playerHands|playerItemHands|tableCards)\s*(?:\[[^\]]+\])?\s*\.\s*(?:Clear|Add|AddRange|Insert|Remove|RemoveAt|RemoveRange)\s*\("));
		True(checkpointSource.Contains(
			"NetworkAuthorityCheckpointRuleAdapter.ApplyRuleSnapshot(",
			StringComparison.Ordinal));
		True(checkpointSource.Contains(
			"NetworkAuthorityCheckpointRuleAdapter.TryCreateRuleSnapshot(",
			StringComparison.Ordinal));

		int createRestored = adapterSource.IndexOf(
			"AuthorityRuleSession restored = AuthorityRuleSession.Restore(",
			StringComparison.Ordinal);
		int commitRestored = adapterSource.IndexOf(
			"_authorityRuleSession = restored;",
			createRestored,
			StringComparison.Ordinal);
		True(createRestored >= 0 && commitRestored > createRestored);
		True(adapterSource.Contains(
			"_ruleRandomSource.RestoreState(previousRandomState);",
			StringComparison.Ordinal));
	}

	private static void CheckpointCodecRejectsTamperedHashAndOversizedPayload()
	{
		NetworkAuthorityCheckpoint source = CreateCheckpoint();
		True(NetworkAuthorityCheckpointCodec.TryEncode(source, out byte[] payload, out string hash, out _));
		False(NetworkAuthorityCheckpointCodec.TryDecode(payload, new string('0', 64), out _, out string error));
		True(error.Contains("hash", StringComparison.OrdinalIgnoreCase));
		byte[] oversized = new byte[NetworkAuthorityCheckpointCodec.MaxPayloadBytes + 1];
		False(NetworkAuthorityCheckpointCodec.TryDecode(oversized, string.Empty, out _, out error));
		True(error.Contains("size", StringComparison.OrdinalIgnoreCase));
	}

	private static void CheckpointCodecRejectsMalformedObjectGraphsWithoutThrowing()
	{
		NetworkAuthorityCheckpoint source = CreateCheckpoint();
		var duplicateRoster = source.Roster.ToList();
		duplicateRoster[1] = duplicateRoster[1] with { SeatIndex = 0 };
		var nullDecks = source.Decks.ToList();
		nullDecks[0] = null!;

		NetworkAuthorityCheckpoint[] malformed =
		{
			source with { Roster = null! },
			source with { Roster = new List<NetworkMigrationRosterEntry> { null! } },
			source with { Roster = duplicateRoster },
			source with { Decks = nullDecks },
			source with { Transport = (NetworkTransport)99 },
			source with { WinningScore = 75 },
			source with { CompletedTurnsSinceQuestionRefresh = -1 },
			source with
			{
				TableCards = new List<NetworkCheckpointTableCard>
				{
					new()
					{
							Card = new NetworkCheckpointCard { Code = null!, AttachedItemCardIds = ImmutableArray<string>.Empty },
					},
				},
			},
			source with
			{
				Hands = new List<List<NetworkCheckpointCard>>
				{
					new() { new NetworkCheckpointCard { Code = "card", AttachedItemCardIds = default } },
					new(),
				},
			},
		};

		foreach (NetworkAuthorityCheckpoint checkpoint in malformed)
		{
			False(NetworkAuthorityCheckpointCodec.TryEncode(checkpoint, out _, out _, out string error));
			Equal(NetworkAuthorityCheckpointCodec.EncodeInvalidError, error);
		}
	}

	private static void CheckpointCodecRejectsExcessiveJsonDepthWithFixedError()
	{
		string json = string.Concat(Enumerable.Repeat("{\"nested\":", 70))
			+ "0"
			+ new string('}', 70);
		byte[] payload = Encoding.UTF8.GetBytes(json);
		string hash = NetworkAuthorityCheckpointCodec.ComputeHash(payload);
		False(NetworkAuthorityCheckpointCodec.TryDecode(payload, hash, out _, out string error));
		Equal(NetworkAuthorityCheckpointCodec.DecodeInvalidError, error);
	}

	private static void CheckpointCodecAcceptsOwnerlessFaceUpSpecialTableCards()
	{
		NetworkAuthorityCheckpoint source = CreateCheckpoint() with
		{
			TableCards = new List<NetworkCheckpointTableCard>
			{
				new()
				{
					Card = null,
					IsFaceUp = true,
					CardOwnerPlayerIndex = -1,
					ItemOverlayId = "question_mark",
					ItemOwnerPlayerIndex = -1,
				},
			},
		};
		True(NetworkAuthorityCheckpointCodec.TryEncode(source, out byte[] payload, out string hash, out _));
		True(NetworkAuthorityCheckpointCodec.TryDecode(payload, hash, out NetworkAuthorityCheckpoint restored, out _));
		Equal("question_mark", restored.TableCards[0].ItemOverlayId);
		False(NetworkAuthorityCheckpointCodec.TryEncode(
			source with
			{
				TableCards = new List<NetworkCheckpointTableCard>
				{
					new() { Card = null, IsFaceUp = true, ItemOwnerPlayerIndex = -1 },
				},
			},
			out _,
			out _,
			out _));
	}

	private static void CheckpointEnvelopeKeepsPreviousAtomicValueOnEncodeFailure()
	{
		NetworkAuthorityCheckpoint source = CreateCheckpoint();
		True(AuthorityCheckpointEnvelope.TryCreate(source, out AuthorityCheckpointEnvelope committed, out _));
		byte committedFirstByte = committed.PayloadSpan[0];

		AuthorityCheckpointEnvelope current = committed;
		NetworkAuthorityCheckpoint invalid = source with
		{
			CheckpointId = source.CheckpointId + 1,
			Roster = null!,
		};
		if (AuthorityCheckpointEnvelope.TryCreate(invalid, out AuthorityCheckpointEnvelope candidate, out _))
		{
			current = candidate;
		}

		True(ReferenceEquals(committed, current));
		Equal(source.CheckpointId, current.CheckpointId);
		Equal(committed.Hash, current.Hash);
		Equal(committedFirstByte, current.PayloadSpan[0]);
		True(committed.PayloadSpan.SequenceEqual(current.PayloadSpan));
	}

	private static void CheckpointCodecRoundtripsDrawOutcome()
	{
		NetworkAuthorityCheckpoint source = CreateCheckpoint() with
		{
			MatchOutcomeKind = MatchOutcomeKind.Draw,
			WinnerPlayerIndex = -1,
		};
		True(NetworkAuthorityCheckpointCodec.TryEncode(source, out byte[] payload, out string hash, out _));
		True(NetworkAuthorityCheckpointCodec.TryDecode(payload, hash, out NetworkAuthorityCheckpoint restored, out _));
		Equal(MatchOutcomeKind.Draw, restored.MatchOutcomeKind);
		Equal(-1, restored.WinnerPlayerIndex);
		False(NetworkAuthorityCheckpointCodec.Validate(
			source with { WinnerPlayerIndex = 0 },
			out _));
	}

	private static void NetworkProtocolContextsFenceEpochsAndSnapshots()
	{
		Equal(11, NetworkProtocol.CurrentVersion);
		Equal(5, NetworkProtocol.AuthorityCheckpointSchemaVersion);
		NetworkRpcContext current = new("session", "match", 3);
		True(current.IsValid);
		True(NetworkProtocolRules.Matches(current, "session", "match", 3, allowHigherEpoch: false));
		False(NetworkProtocolRules.Matches(new NetworkRpcContext("session", "match", 2), "session", "match", 3, allowHigherEpoch: false));
		True(NetworkProtocolRules.Matches(new NetworkRpcContext("session", "match", 4), "session", "match", 3, allowHigherEpoch: true));
		False(NetworkProtocolRules.Matches(new NetworkRpcContext("other", "match", 4), "session", "match", 3, allowHigherEpoch: true));

		True(NetworkProtocolRules.IsNewerFence(4, 0, 3, 99));
		True(NetworkProtocolRules.IsNewerFence(3, 100, 3, 99));
		False(NetworkProtocolRules.IsNewerFence(3, 99, 3, 99));
		False(NetworkProtocolRules.IsNewerFence(2, 100, 3, 99));
	}

	private static void MigrationRestoreGenerationIsStrictlyMonotonic()
	{
		True(TurnGenerationRules.TryGetNextAfterRestore(8, 3, out int fromNewerLocal));
		Equal(9, fromNewerLocal);
		True(TurnGenerationRules.TryGetNextAfterRestore(3, 8, out int fromNewerCheckpoint));
		Equal(9, fromNewerCheckpoint);
		False(TurnGenerationRules.TryGetNextAfterRestore(int.MaxValue, 8, out _));
	}

	private static void MultiplayerRematchAcceptsOnlyCompletedCurrentMatchPlayers()
	{
		NetworkSessionConfig config = new(
			"session",
			"room",
			"secret",
			"match",
			123,
			NetworkTransport.Lan,
			2,
			true,
			100,
			LobbyVisibility.FriendsOnly);
		var players = new List<NetworkPlayerPublicState>
		{
			new(1, "host", "Host", "dolphin", 0, true, true, true, true),
			new(42, "guest", "Guest", "turtle", 1, false, false, true, true),
		};
		NetworkRpcContext current = new("session", "match", 3);

		True(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.InGame,
			current,
			config,
			3,
			players,
			42,
			hasWinner: true));
		False(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.InGame,
			current,
			config,
			3,
			players,
			42,
			hasWinner: false));
		False(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.Lobby,
			current,
			config,
			3,
			players,
			42,
			hasWinner: true));
		False(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.InGame,
			new NetworkRpcContext("session", "old-match", 3),
			config,
			3,
			players,
			42,
			hasWinner: true));
		False(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.InGame,
			current,
			config,
			3,
			players,
			99,
			hasWinner: true));
		players[1] = players[1] with { IsConnected = false };
		False(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.InGame,
			current,
			config,
			3,
			players,
			42,
			hasWinner: true));
	}

	private static void MultiplayerRematchReturnsConnectedReadyPlayersToPreservedLobby()
	{
		NetworkSessionConfig matchConfig = new(
			"session",
			"room",
			"secret",
			"match",
			987,
			NetworkTransport.Steam,
			4,
			false,
			200,
			LobbyVisibility.Private);
		var players = new List<NetworkPlayerPublicState>
		{
			new(1, "host", "Host", "dolphin", 0, true, true, true, true),
			new(20, "offline", "Offline", "turtle", 1, false, false, true, false),
			new(30, "guest", "Guest", "whale", 2, false, false, true, true),
		};

		NetworkSessionConfig lobbyConfig = MultiplayerRematchRules.CreateReturnedLobbyConfig(matchConfig);
		List<NetworkPlayerPublicState> retained = MultiplayerRematchRules.RetainConnectedPlayers(players);

		Equal(matchConfig.SessionId, lobbyConfig.SessionId);
		Equal(matchConfig.RoomName, lobbyConfig.RoomName);
		Equal(matchConfig.RoomPassword, lobbyConfig.RoomPassword);
		Equal(matchConfig.Transport, lobbyConfig.Transport);
		Equal(matchConfig.PlayerCount, lobbyConfig.PlayerCount);
		Equal(matchConfig.ItemMode, lobbyConfig.ItemMode);
		Equal(matchConfig.WinningScore, lobbyConfig.WinningScore);
		Equal(string.Empty, lobbyConfig.MatchId);
		Equal(0L, lobbyConfig.MatchSeed);
		Equal(2, retained.Count);
		True(retained.All(player => player.IsReady && player.IsConnected));
		Equal(0, retained[0].SeatIndex);
		Equal(1, retained[1].SeatIndex);
		Equal("guest", retained[1].PlayerSessionId);
		False(retained.Any(player => player.PlayerSessionId == "offline"));
		True(MultiplayerRematchRules.IsValidAuthorityReturn(matchConfig, lobbyConfig, retained));
		False(MultiplayerRematchRules.IsValidAuthorityReturn(
			matchConfig,
			lobbyConfig with { WinningScore = 50 },
			retained));
	}

	private static void MigrationPlanSelectsLowestEligibleStandby()
	{
		var roster = new List<NetworkMigrationRosterEntry>
		{
			new() { PlayerSessionId = "host", SeatIndex = 0, IsHost = true, IsConnected = true, CredentialsAcknowledged = true },
			new() { PlayerSessionId = "seat-2", SeatIndex = 2, IsConnected = true, CredentialsAcknowledged = true },
			new() { PlayerSessionId = "seat-1", SeatIndex = 1, IsConnected = true, CredentialsAcknowledged = true },
			new() { PlayerSessionId = "seat-3", SeatIndex = 3, IsConnected = true, CredentialsAcknowledged = true },
		};
		var controls = new List<NetworkAuthoritySeatControl>
		{
			new() { SeatIndex = 0, Mode = SeatControlMode.HumanConnected },
			new() { SeatIndex = 1, Mode = SeatControlMode.HumanConnected },
			new() { SeatIndex = 2, Mode = SeatControlMode.AiProxyReclaimable },
			new() { SeatIndex = 3, Mode = SeatControlMode.HumanConnected },
		};

		Equal(1, NetworkMigrationRules.SelectHotStandbySeat(roster, controls, hostSeat: 0));
		Equal(3, NetworkMigrationRules.SelectHotStandbySeat(roster, controls, hostSeat: 0, excludedSeats: new[] { 1 }));
		True(NetworkMigrationRules.ShouldSwitchToNextCandidate(true, NetworkMigrationRules.ConsecutiveUnackedCheckpointsBeforeSwitch));
		True(NetworkMigrationRules.ShouldSwitchToNextCandidate(false, 0));
		False(NetworkMigrationRules.ShouldSwitchToNextCandidate(true, 1));
	}

	private static void MigrationCandidateScorePrefersStableAcknowledgementsThenLatency()
	{
		var roster = new List<NetworkMigrationRosterEntry>
		{
			new() { PlayerSessionId = "host", SeatIndex = 0, IsHost = true, IsConnected = true, CredentialsAcknowledged = true },
			new() { PlayerSessionId = "seat-1", SeatIndex = 1, IsConnected = true, CredentialsAcknowledged = true },
			new() { PlayerSessionId = "seat-2", SeatIndex = 2, IsConnected = true, CredentialsAcknowledged = true },
		};
		var controls = new List<NetworkAuthoritySeatControl>
		{
			new() { SeatIndex = 0, Mode = SeatControlMode.HumanConnected },
			new() { SeatIndex = 1, Mode = SeatControlMode.HumanConnected },
			new() { SeatIndex = 2, Mode = SeatControlMode.HumanConnected },
		};
		var latencyBySeat = new Dictionary<int, double>
		{
			[1] = 80.0,
			[2] = 20.0,
		};

		Equal(2, NetworkMigrationRules.SelectHotStandbySeat(
			roster,
			controls,
			hostSeat: 0,
			excludedSeats: Array.Empty<int>(),
			stableAcknowledgedSeat: -1,
			latencyMillisecondsBySeat: latencyBySeat));
		Equal(1, NetworkMigrationRules.SelectHotStandbySeat(
			roster,
			controls,
			hostSeat: 0,
			excludedSeats: Array.Empty<int>(),
			stableAcknowledgedSeat: 1,
			latencyMillisecondsBySeat: latencyBySeat));
	}

	private static void MigrationPlanRejectsStaleOrUnsafeEndpoints()
	{
		using ECDsa sourceSigner = HostMigrationSecurity.CreateSigner();
		using ECDsa successorSigner = HostMigrationSecurity.CreateSigner();
		byte[] sourcePublicKey = HostMigrationSecurity.ExportPublicKey(sourceSigner);
		byte[] successorPublicKey = HostMigrationSecurity.ExportPublicKey(successorSigner);
		NetworkMigrationPlan unsigned = new()
		{
			ProtocolVersion = NetworkProtocol.CurrentVersion,
			PlanRevision = 1,
			SessionId = "session",
			MatchId = "match",
			SourceEpoch = 3,
			TargetEpoch = 4,
			CheckpointId = 7,
			CheckpointHash = new string('a', 64),
			CheckpointCiphertextHash = new string('b', 64),
			CandidatePlayerSessionId = "seat-1",
			CandidateSeat = 1,
			CandidateSuccessorPublicKey = HostMigrationSecurity.EncodeBytes(successorPublicKey),
			CandidateSuccessorKeyId = HostMigrationSecurity.ComputeKeyId(successorPublicKey),
			SourcePlayerSessionId = "host",
			SourceAuthorityKeyId = HostMigrationSecurity.ComputeKeyId(sourcePublicKey),
			Transport = NetworkTransport.Lan,
			TransportDescriptor = new NetworkMigrationTransportDescriptor
			{
				Lan = new NetworkLanMigrationDescriptor
				{
					ObservedHostAddress = "192.168.1.10",
					CandidateHostAddress = "192.168.1.11",
					Port = NetworkProtocol.LanHostMigrationPort,
					MaxClients = NetworkSessionDefaults.MaxPlayers - 1,
					PeerRebuildMode = NetworkMigrationPeerRebuildMode.EnetServer,
				},
			},
			Phase = NetworkAuthorityPhase.InGame,
		};
		True(HostMigrationSecurity.TrySignMigrationPlan(unsigned, sourceSigner, out NetworkMigrationPlan valid));
		True(NetworkMigrationRules.IsPlanVisibleToClients(valid));
		True(HostMigrationSecurity.VerifyMigrationPlan(valid, sourcePublicKey));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with { TargetEpoch = 3 }));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with { CheckpointHash = new string('A', 64) }));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with
		{
			TransportDescriptor = valid.TransportDescriptor with
			{
				Lan = valid.TransportDescriptor.Lan! with { CandidateHostAddress = "bad address" },
			},
		}));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with
		{
			TransportDescriptor = valid.TransportDescriptor with
			{
				Lan = valid.TransportDescriptor.Lan! with { Port = NetworkSessionDefaults.LanGamePort + 1 },
			},
		}));
		False(HostMigrationSecurity.VerifyMigrationPlan(valid with { CandidateSeat = 2 }, sourcePublicKey));
		NetworkMigrationPlan steamUnsigned = unsigned with
		{
			Transport = NetworkTransport.Steam,
			TransportDescriptor = new NetworkMigrationTransportDescriptor
			{
				Steam = new NetworkSteamMigrationDescriptor
				{
					LobbyId = 109775241000000001,
					SourceLobbyOwnerSteamId = 76561198000000001,
					CandidateSteamId = 76561198000000002,
					ExpectedLobbyOwnerSteamId = 76561198000000002,
					PeerRebuildMode = NetworkMigrationPeerRebuildMode.SteamLobby,
				},
			},
		};
		True(HostMigrationSecurity.TrySignMigrationPlan(steamUnsigned, sourceSigner, out NetworkMigrationPlan steamPlan));
		True(NetworkMigrationRules.IsPlanVisibleToClients(steamPlan));
		True(HostMigrationSecurity.VerifyMigrationPlan(steamPlan, sourcePublicKey));
		string steamJson = JsonSerializer.Serialize(steamPlan);
		NetworkMigrationPlan? steamRoundtrip = JsonSerializer.Deserialize<NetworkMigrationPlan>(steamJson);
		True(steamRoundtrip is not null);
		True(steamRoundtrip!.TransportDescriptor.Lan is null);
		Equal(steamPlan.TransportDescriptor.Steam, steamRoundtrip.TransportDescriptor.Steam);
		True(HostMigrationSecurity.VerifyMigrationPlan(steamRoundtrip, sourcePublicKey));
		False(NetworkMigrationRules.IsPlanVisibleToClients(steamPlan with
		{
			TransportDescriptor = steamPlan.TransportDescriptor with
			{
				Steam = steamPlan.TransportDescriptor.Steam! with
				{
					ExpectedLobbyOwnerSteamId = 76561198000000003,
				},
			},
		}));
		False(HostMigrationSecurity.VerifyMigrationPlan(steamPlan with
		{
			TransportDescriptor = steamPlan.TransportDescriptor with
			{
				Steam = steamPlan.TransportDescriptor.Steam! with
				{
					CandidateSteamId = 76561198000000003,
					ExpectedLobbyOwnerSteamId = 76561198000000003,
				},
			},
			}, sourcePublicKey));
		False(HostMigrationSecurity.VerifyMigrationPlan(steamPlan with
		{
			TransportDescriptor = steamPlan.TransportDescriptor with
			{
				Steam = steamPlan.TransportDescriptor.Steam! with
				{
					LobbyId = 109775241000000002,
					SourceLobbyOwnerSteamId = 76561198000000004,
				},
			},
		}, sourcePublicKey));
		NetworkMigrationPlan mismatchedCandidateKey = unsigned with
		{
			CandidateSuccessorKeyId = new string('c', 64),
		};
		False(NetworkMigrationRules.IsUnsignedPlanValid(mismatchedCandidateKey));
		False(HostMigrationSecurity.TrySignMigrationPlan(mismatchedCandidateKey, sourceSigner, out _));
		False(HostMigrationSecurity.TrySignMigrationPlan(
			unsigned with { SourceAuthorityKeyId = new string('d', 64) },
			sourceSigner,
			out _));
		True(NetworkMigrationRules.ShouldAcceptPlanUpdate(null, valid));
		True(NetworkMigrationRules.ShouldAcceptPlanUpdate(valid, valid with { PlanRevision = 2 }));
		True(NetworkMigrationRules.ShouldAcceptPlanUpdate(valid, valid with { TargetEpoch = 5, PlanRevision = 1 }));
		False(NetworkMigrationRules.ShouldAcceptPlanUpdate(valid, valid));
		False(NetworkMigrationRules.ShouldAcceptPlanUpdate(valid, valid with { PlanRevision = 0 }));
		False(NetworkMigrationRules.ShouldAcceptPlanUpdate(valid, valid with { TargetEpoch = 3, PlanRevision = 99 }));
		True(NetworkMigrationRules.CanCandidateStart(valid, "seat-1", NetworkSessionState.MigratingHost, hasCheckpoint: true, isSourceHost: false));
		False(NetworkMigrationRules.CanCandidateStart(valid, "host", NetworkSessionState.MigratingHost, hasCheckpoint: true, isSourceHost: false));
		False(NetworkMigrationRules.ShouldFollowerConnect(valid, "seat-1", NetworkSessionState.MigratingHost, isCandidate: true, elapsedTimeout: false));
	}

	private static void SteamOwnerConvergenceFollowsSignedCandidate()
	{
		var descriptor = new NetworkSteamMigrationDescriptor
		{
			LobbyId = 109775241000000001,
			SourceLobbyOwnerSteamId = 76561198000000001,
			CandidateSteamId = 76561198000000002,
			ExpectedLobbyOwnerSteamId = 76561198000000002,
		};

		Equal(
			SteamLobbyOwnerConvergenceAction.Ready,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: 76561198000000003,
				currentLobbyOwnerSteamId: descriptor.ExpectedLobbyOwnerSteamId,
				timedOut: false));
		Equal(
			SteamLobbyOwnerConvergenceAction.RequestTransfer,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: 76561198000000003,
				currentLobbyOwnerSteamId: 76561198000000003,
				timedOut: false));
		Equal(
			SteamLobbyOwnerConvergenceAction.Wait,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: 76561198000000004,
				currentLobbyOwnerSteamId: 76561198000000003,
				timedOut: false));
		Equal(
			SteamLobbyOwnerConvergenceAction.Fail,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: 76561198000000004,
				currentLobbyOwnerSteamId: 76561198000000003,
				timedOut: true));
		Equal(
			SteamLobbyOwnerConvergenceAction.Wait,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: descriptor.CandidateSteamId,
				currentLobbyOwnerSteamId: 0,
				timedOut: false));
		Equal(
			SteamLobbyOwnerConvergenceAction.Fail,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: descriptor.CandidateSteamId,
				currentLobbyOwnerSteamId: 0,
				timedOut: true));
	}

	private static void MigrationPeerRebuildWaitsForSourceDisconnect()
	{
		False(NetworkMigrationRules.CanRebuildMigrationPeer(
			transportPrepared: false,
			awaitingSourceDisconnect: true));
		False(NetworkMigrationRules.CanRebuildMigrationPeer(
			transportPrepared: true,
			awaitingSourceDisconnect: true));
		False(NetworkMigrationRules.CanRebuildMigrationPeer(
			transportPrepared: false,
			awaitingSourceDisconnect: false));
		True(NetworkMigrationRules.CanRebuildMigrationPeer(
			transportPrepared: true,
			awaitingSourceDisconnect: false));
	}

	private static void StandbyCheckpointSendsAreSerializedAndRateBounded()
	{
		True(NetworkMigrationRules.CanSendStandbyCheckpoint(10.0, -1.0, hasPendingCheckpoint: false));
		False(NetworkMigrationRules.CanSendStandbyCheckpoint(10.1, 10.0, hasPendingCheckpoint: false));
		True(NetworkMigrationRules.CanSendStandbyCheckpoint(10.5, 10.0, hasPendingCheckpoint: false));
		False(NetworkMigrationRules.CanSendStandbyCheckpoint(11.0, 10.0, hasPendingCheckpoint: true));
		False(NetworkMigrationRules.CanSendStandbyCheckpoint(double.NaN, 10.0, hasPendingCheckpoint: false));
	}

	private static void MigrationCoordinatorPermitsOnlyOrderedRecoveryTransitions()
	{
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.Disabled,
			NetworkMigrationCoordinatorState.SelectingCandidate));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.SelectingCandidate,
			NetworkMigrationCoordinatorState.KeyAgreement));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.KeyAgreement,
			NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement,
			NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement,
			NetworkMigrationCoordinatorState.Ready));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.Ready,
			NetworkMigrationCoordinatorState.SourceHostLost));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.SourceHostLost,
			NetworkMigrationCoordinatorState.CandidateStarting));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.CandidateStarting,
			NetworkMigrationCoordinatorState.FollowersRecovering));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.FollowersRecovering,
			NetworkMigrationCoordinatorState.Completed));
		False(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.Disabled,
			NetworkMigrationCoordinatorState.Ready));
		False(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.SelectingCandidate,
			NetworkMigrationCoordinatorState.Completed));
		False(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.FollowersRecovering,
			NetworkMigrationCoordinatorState.Ready));
		True(NetworkMigrationRules.IsReadyForMigration(NetworkMigrationCoordinatorState.Ready));
		False(NetworkMigrationRules.IsReadyForMigration(NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement));
		False(NetworkMigrationRules.IsReadyForMigration(NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement));
		True(NetworkMigrationRules.ArePlanAcknowledgementsComplete(new[] { 1, 2 }, new[] { 1, 2 }));
		False(NetworkMigrationRules.ArePlanAcknowledgementsComplete(new[] { 1, 2 }, new[] { 1 }));
	}

	private static void ActiveHostMigrationFreezesCheckpointAndStandbyMaintenance()
	{
		string root = FindRepositoryRoot();
		string migrationSource = string.Concat(
			Directory.GetFiles(Path.Combine(root, "scripts"), "NetworkManager.Migration*.cs")
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(File.ReadAllText));
		string authoritySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.AuthorityActions.cs"));
		string maintenanceSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Maintenance.cs"));

		int handoffStart = migrationSource.IndexOf("private bool IsActiveMigrationHandoffInProgress()", StringComparison.Ordinal);
		int handoffEnd = migrationSource.IndexOf("private bool IsMigrationRecoveryHandoffInProgress()", handoffStart, StringComparison.Ordinal);
		True(handoffStart >= 0 && handoffEnd > handoffStart);
		string handoffBlock = migrationSource[handoffStart..handoffEnd];
		True(handoffBlock.Contains("_activeMigrationPending || _activeMigrationAwaitingSourceClose", StringComparison.Ordinal));

		int boardStart = authoritySource.IndexOf("CommitAuthorityCheckpointFromBoard()", StringComparison.Ordinal);
		int lobbyStart = authoritySource.IndexOf("CommitAuthorityCheckpointFromLobby()", boardStart, StringComparison.Ordinal);
		int lobbyEnd = authoritySource.IndexOf("private AuthorityCheckpointCommitResult CommitAuthoritySequence", lobbyStart, StringComparison.Ordinal);
		True(boardStart >= 0 && lobbyStart > boardStart && lobbyEnd > lobbyStart);
		string boardCommitBlock = authoritySource[boardStart..lobbyStart];
		string lobbyCommitBlock = authoritySource[lobbyStart..lobbyEnd];
		foreach (string commitBlock in new[] { boardCommitBlock, lobbyCommitBlock })
		{
			int freezeCheck = commitBlock.IndexOf("if (IsActiveMigrationHandoffInProgress())", StringComparison.Ordinal);
			int invalidation = commitBlock.IndexOf("TryInvalidateMigrationAvailability", StringComparison.Ordinal);
			True(freezeCheck >= 0 && invalidation > freezeCheck);
			True(commitBlock.Contains("active_migration_handoff_in_progress", StringComparison.Ordinal));
		}

		int pollStart = migrationSource.IndexOf("private void PollHotStandby", StringComparison.Ordinal);
		int pollEnd = migrationSource.IndexOf("private bool HasEligibleHotStandbyPeer", pollStart, StringComparison.Ordinal);
		True(pollStart >= 0 && pollEnd > pollStart);
		True(migrationSource[pollStart..pollEnd].Contains("IsActiveMigrationHandoffInProgress()", StringComparison.Ordinal));

		int invalidationStart = migrationSource.IndexOf("private bool TryInvalidateMigrationAvailability", StringComparison.Ordinal);
		int invalidationEnd = migrationSource.IndexOf("private MigrationCheckpointDispatchStatus TryBroadcastMigrationPlanRevocation", invalidationStart, StringComparison.Ordinal);
		True(invalidationStart >= 0 && invalidationEnd > invalidationStart);
		True(migrationSource[invalidationStart..invalidationEnd].Contains("if (IsActiveMigrationHandoffInProgress())", StringComparison.Ordinal));

		int revocationStart = migrationSource.IndexOf("private void RpcMigrationPlanRevoked", StringComparison.Ordinal);
		int revocationEnd = migrationSource.IndexOf("private void RpcDebugStaleMigrationPlan", revocationStart, StringComparison.Ordinal);
		True(revocationStart >= 0 && revocationEnd > revocationStart);
		True(migrationSource[revocationStart..revocationEnd].Contains("if (IsMigrationRecoveryHandoffInProgress())", StringComparison.Ordinal));

		int maintenanceStart = maintenanceSource.IndexOf("private bool IsHotStandbyMaintenanceActive", StringComparison.Ordinal);
		int maintenanceEnd = maintenanceSource.IndexOf("private static void ConsiderDeadline", maintenanceStart, StringComparison.Ordinal);
		True(maintenanceStart >= 0 && maintenanceEnd > maintenanceStart);
		True(maintenanceSource[maintenanceStart..maintenanceEnd].Contains("&& !IsActiveMigrationHandoffInProgress()", StringComparison.Ordinal));
	}

	private static void CheckpointCommitResultsClassifyLocalAndMigrationFailures()
	{
		AuthorityCheckpointCommitResult committed = AuthorityCheckpointCommitResult.Committed(
			12,
			MigrationCheckpointDispatchStatus.CandidateUnavailable);
		AuthorityCheckpointCommitResult signingFailed = AuthorityCheckpointCommitResult.Rejected(
			AuthorityCheckpointCommitStatus.SigningFailed,
			"revocation_signing_failed");
		True(committed.IsCheckpointCommitted);
		False(committed.IsMigrationReady);
		Equal(12L, committed.CheckpointId);
		Equal(MigrationCheckpointDispatchStatus.CandidateUnavailable, committed.DispatchStatus);
		False(signingFailed.IsCheckpointCommitted);
		Equal(AuthorityCheckpointCommitStatus.SigningFailed, signingFailed.CommitStatus);
		Equal(MigrationCheckpointDispatchStatus.NotRequested, signingFailed.DispatchStatus);
		Equal(
			AuthorityCheckpointCommitStatus.PayloadTooLarge,
			NetworkMigrationRules.ClassifyCheckpointBuildFailure(NetworkAuthorityCheckpointCodec.PayloadSizeError));
		Equal(
			AuthorityCheckpointCommitStatus.EncodingFailed,
			NetworkMigrationRules.ClassifyCheckpointBuildFailure(NetworkAuthorityCheckpointCodec.EncodeFailedError));
		Equal(
			AuthorityCheckpointCommitStatus.RulesStateUnavailable,
			NetworkMigrationRules.ClassifyCheckpointBuildFailure("authority_action_not_stable action=ItemCard"));
		Equal(
			AuthorityCheckpointCommitStatus.DataValidationFailed,
			NetworkMigrationRules.ClassifyCheckpointBuildFailure(NetworkAuthorityCheckpointCodec.EncodeInvalidError));
	}

	private static void MigrationPlanRevocationIsSignedAndTamperEvident()
	{
		using ECDsa sourceSigner = HostMigrationSecurity.CreateSigner();
		byte[] sourcePublicKey = HostMigrationSecurity.ExportPublicKey(sourceSigner);
		NetworkMigrationPlanRevocation unsigned = new()
		{
			ProtocolVersion = NetworkProtocol.CurrentVersion,
			PlanRevision = 8,
			RevokedPlanRevision = 7,
			RevokedCheckpointId = 21,
			SessionId = "session",
			MatchId = "match",
			SourceEpoch = 4,
			SourceAuthorityKeyId = HostMigrationSecurity.ComputeKeyId(sourcePublicKey),
		};

		True(HostMigrationSecurity.TrySignMigrationPlanRevocation(unsigned, sourceSigner, out NetworkMigrationPlanRevocation signed));
		True(NetworkMigrationRules.IsPlanRevocationVisibleToClients(signed));
		True(HostMigrationSecurity.VerifyMigrationPlanRevocation(signed, sourcePublicKey));
		False(HostMigrationSecurity.VerifyMigrationPlanRevocation(
			signed with { RevokedCheckpointId = 22 },
			sourcePublicKey));
		False(NetworkMigrationRules.IsPlanRevocationVisibleToClients(signed with { PlanRevision = signed.RevokedPlanRevision }));
	}

	private static void RevocationBlocksPlanReplayAndStaleCheckpointEligibility()
	{
		NetworkMigrationPlan stalePlan = new() { PlanRevision = 7 };
		NetworkMigrationPlan replacementPlan = new() { PlanRevision = 8 };
		NetworkMigrationPlan newerPlan = new() { PlanRevision = 9 };
		False(NetworkMigrationRules.CanAcceptPlanRevisionAfterRevocation(8, 8, stalePlan));
		True(NetworkMigrationRules.CanAcceptPlanRevisionAfterRevocation(8, 8, replacementPlan));
		True(NetworkMigrationRules.CanAcceptPlanRevisionAfterRevocation(8, 8, newerPlan));
		False(NetworkMigrationRules.CanDispatchMigrationCheckpoint(21, -1));
		False(NetworkMigrationRules.CanDispatchMigrationCheckpoint(21, 20));
		True(NetworkMigrationRules.CanDispatchMigrationCheckpoint(22, 22));
		True(NetworkMigrationRules.CanAcceptCheckpointChainAdvance(
			"confirmed",
			"confirmed",
			22,
			21,
			9,
			8));
		False(NetworkMigrationRules.CanAcceptCheckpointChainAdvance(
			"speculative",
			"confirmed",
			22,
			21,
			9,
			8));
		False(NetworkMigrationRules.CanAcceptCheckpointChainAdvance(
			"confirmed",
			"confirmed",
			21,
			21,
			9,
			8));
		False(NetworkMigrationRules.CanAcceptCheckpointChainAdvance(
			"confirmed",
			"confirmed",
			22,
			21,
			8,
			8));
	}

	private static void MigrationCheckpointChainIsScopedToSessionMatchAndEpoch()
	{
		NetworkAuthorityCheckpoint checkpoint = CreateCheckpoint() with
		{
			SessionId = "session-a",
			MatchId = "match-a",
			AuthorityEpoch = 4,
		};
		True(NetworkMigrationRules.IsSameCheckpointChain(checkpoint, "session-a", "match-a", 4));
		False(NetworkMigrationRules.IsSameCheckpointChain(checkpoint, "session-b", "match-a", 4));
		False(NetworkMigrationRules.IsSameCheckpointChain(checkpoint, "session-a", "match-b", 4));
		False(NetworkMigrationRules.IsSameCheckpointChain(checkpoint, "session-a", "match-a", 5));
		False(NetworkMigrationRules.IsSameCheckpointChain(null, "session-a", "match-a", 4));
	}

	private static void MigrationRekeyHandshakeContinuesIntoSeatAuthentication()
	{
		True(NetworkMigrationRules.ShouldContinueAuthenticationAfterSecurityHandshake(
			NetworkSessionState.Reconnecting,
			localHandshakeIsRekey: true,
			authorityHandshakeIsRekey: true));
		True(NetworkMigrationRules.ShouldContinueAuthenticationAfterSecurityHandshake(
			NetworkSessionState.MigratingHost,
			localHandshakeIsRekey: true,
			authorityHandshakeIsRekey: true));
		False(NetworkMigrationRules.ShouldContinueAuthenticationAfterSecurityHandshake(
			NetworkSessionState.Lobby,
			localHandshakeIsRekey: true,
			authorityHandshakeIsRekey: true));
		True(NetworkMigrationRules.ShouldContinueAuthenticationAfterSecurityHandshake(
			NetworkSessionState.Joining,
			localHandshakeIsRekey: false,
			authorityHandshakeIsRekey: false));
	}

	private static void RoomPasswordChallengeRejectsPlaintextReplaySubstitutes()
	{
		RoomPasswordVerifier verifier = RoomPasswordRules.CreateVerifier(" trusted-room ");
		byte[] challenge = RoomPasswordRules.CreateChallenge();
		True(verifier.HasPassword);
		True(RoomPasswordRules.IsVerifierValid(verifier));
		True(RoomPasswordRules.TryComputeChallengeProof(
			"trusted-room",
			verifier,
			challenge,
			"session-a",
			out byte[] proof));
		True(RoomPasswordRules.TryComputeChallengeProof(
			"trusted-room",
			verifier.Version,
			verifier.Iterations,
			verifier.Salt,
			challenge,
			"session-a",
			out byte[] parameterProof));
		True(proof.SequenceEqual(parameterProof));
		False(RoomPasswordRules.TryComputeChallengeProof(
			"trusted-room",
			verifier.Version + 1,
			verifier.Iterations,
			verifier.Salt,
			challenge,
			"session-a",
			out _));
		True(RoomPasswordRules.VerifyChallengeProof(verifier, challenge, "session-a", proof));
		False(RoomPasswordRules.VerifyChallengeProof(verifier, RoomPasswordRules.CreateChallenge(), "session-a", proof));
		False(RoomPasswordRules.VerifyChallengeProof(verifier, challenge, "session-b", proof));
		True(RoomPasswordRules.TryComputeChallengeProof(
			"wrong-password",
			verifier,
			challenge,
			"session-a",
			out byte[] wrongProof));
		False(RoomPasswordRules.VerifyChallengeProof(verifier, challenge, "session-a", wrongProof));
		False(Encoding.UTF8.GetString(proof).Contains("trusted-room", StringComparison.Ordinal));
	}

	private static void MigrationKeyAgreementDerivesPeerUniqueSessionKeys()
	{
		using ECDiffieHellman host = HostMigrationSecurity.CreateKeyAgreement();
		using ECDiffieHellman candidate = HostMigrationSecurity.CreateKeyAgreement();
		using ECDiffieHellman follower = HostMigrationSecurity.CreateKeyAgreement();
		True(HostMigrationSecurity.TryDeriveSessionKey(
			host,
			HostMigrationSecurity.ExportPublicKey(candidate),
			"session-a",
			out byte[] hostCandidateKey));
		True(HostMigrationSecurity.TryDeriveSessionKey(
			candidate,
			HostMigrationSecurity.ExportPublicKey(host),
			"session-a",
			out byte[] candidateKey));
		True(HostMigrationSecurity.TryDeriveSessionKey(
			host,
			HostMigrationSecurity.ExportPublicKey(follower),
			"session-a",
			out byte[] hostFollowerKey));
		True(hostCandidateKey.SequenceEqual(candidateKey));
		False(hostCandidateKey.SequenceEqual(hostFollowerKey));
	}

	private static void EncryptedMigrationCheckpointRejectsTamperingAndWrongIdentities()
	{
		using ECDiffieHellman hostAgreement = HostMigrationSecurity.CreateKeyAgreement();
		using ECDiffieHellman candidateAgreement = HostMigrationSecurity.CreateKeyAgreement();
		using ECDiffieHellman otherAgreement = HostMigrationSecurity.CreateKeyAgreement();
		using ECDsa sourceSigner = HostMigrationSecurity.CreateSigner();
		using ECDsa wrongSigner = HostMigrationSecurity.CreateSigner();
		True(HostMigrationSecurity.TryDeriveSessionKey(
			hostAgreement,
			HostMigrationSecurity.ExportPublicKey(candidateAgreement),
			"session-a",
			out byte[] hostSessionKey));
		True(HostMigrationSecurity.TryDeriveSessionKey(
			candidateAgreement,
			HostMigrationSecurity.ExportPublicKey(hostAgreement),
			"session-a",
			out byte[] candidateSessionKey));
		True(HostMigrationSecurity.TryDeriveSessionKey(
			otherAgreement,
			HostMigrationSecurity.ExportPublicKey(hostAgreement),
			"session-a",
			out byte[] wrongSessionKey));

		byte[] payload = Encoding.UTF8.GetBytes("private checkpoint payload");
		string payloadHash = NetworkAuthorityCheckpointCodec.ComputeHash(payload);
		var context = new MigrationCheckpointSecurityContext(
			"session-a",
			"match-a",
			4,
			9,
			"candidate-a",
			3,
			NetworkProtocol.CurrentVersion);
		True(HostMigrationSecurity.TryEncryptCheckpoint(
			payload,
			payloadHash,
			hostSessionKey,
			context,
			new string('a', 64),
			sourceSigner,
			out EncryptedAuthorityCheckpointEnvelope envelope,
			out _));
		True(HostMigrationSecurity.TryDecryptCheckpoint(
			envelope,
			candidateSessionKey,
			HostMigrationSecurity.ExportPublicKey(sourceSigner),
			out byte[] decrypted,
			out _));
		True(payload.SequenceEqual(decrypted));
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			envelope,
			wrongSessionKey,
			HostMigrationSecurity.ExportPublicKey(sourceSigner),
			out _,
			out _));
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			envelope,
			candidateSessionKey,
			HostMigrationSecurity.ExportPublicKey(wrongSigner),
			out _,
			out _));

		byte[] tamperedCiphertext = envelope.GetCiphertextCopy();
		tamperedCiphertext[0] ^= 0x40;
		var tampered = new EncryptedAuthorityCheckpointEnvelope(
			envelope.Context,
			envelope.GetNonceCopy(),
			tamperedCiphertext,
			envelope.GetTagCopy(),
			envelope.PlaintextHash,
			envelope.CiphertextHash,
			envelope.PreviousCheckpointHash,
			envelope.SourceAuthorityKeyId,
			envelope.GetSignatureCopy());
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			tampered,
			candidateSessionKey,
			HostMigrationSecurity.ExportPublicKey(sourceSigner),
			out _,
			out _));

		byte[] tamperedTag = envelope.GetTagCopy();
		tamperedTag[^1] ^= 0x01;
		var tagTampered = new EncryptedAuthorityCheckpointEnvelope(
			envelope.Context,
			envelope.GetNonceCopy(),
			envelope.GetCiphertextCopy(),
			tamperedTag,
			envelope.PlaintextHash,
			envelope.CiphertextHash,
			envelope.PreviousCheckpointHash,
			envelope.SourceAuthorityKeyId,
			envelope.GetSignatureCopy());
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			tagTampered,
			candidateSessionKey,
			HostMigrationSecurity.ExportPublicKey(sourceSigner),
			out _,
			out _));

		var wrongCandidate = new EncryptedAuthorityCheckpointEnvelope(
			envelope.Context with { CandidatePlayerSessionId = "candidate-b" },
			envelope.GetNonceCopy(),
			envelope.GetCiphertextCopy(),
			envelope.GetTagCopy(),
			envelope.PlaintextHash,
			envelope.CiphertextHash,
			envelope.PreviousCheckpointHash,
			envelope.SourceAuthorityKeyId,
			envelope.GetSignatureCopy());
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			wrongCandidate,
			candidateSessionKey,
			HostMigrationSecurity.ExportPublicKey(sourceSigner),
			out _,
			out _));
	}

	private static void MigrationNonceReplayGuardRejectsDuplicatesAndResetsPerEpoch()
	{
		var guard = new MigrationNonceReplayGuard();
		byte[] firstNonce = Enumerable.Range(0, HostMigrationSecurity.AesNonceBytes)
			.Select(value => (byte)value)
			.ToArray();
		byte[] secondNonce = Enumerable.Range(1, HostMigrationSecurity.AesNonceBytes)
			.Select(value => (byte)value)
			.ToArray();

		False(guard.HasSeen(4, firstNonce));
		True(guard.TryRegister(4, firstNonce));
		True(guard.HasSeen(4, firstNonce));
		False(guard.TryRegister(4, firstNonce));
		True(guard.TryRegister(4, secondNonce));
		Equal(2, guard.Count);
		True(guard.TryRegister(5, firstNonce));
		Equal(1, guard.Count);
		False(guard.HasSeen(4, firstNonce));
		True(guard.HasSeen(5, firstNonce));
		False(guard.TryRegister(5, firstNonce[..^1]));
		guard.Reset();
		Equal(0, guard.Count);
		False(guard.HasSeen(5, firstNonce));
	}

	private static void MigrationSerializationExcludesRawSecrets()
	{
		const string rawPassword = "raw-password-never-serialize";
		const string rawCredential = "raw-seat-credential-never-serialize";
		byte[] sessionKey = Enumerable.Repeat((byte)0x5a, HostMigrationSecurity.SessionKeyBytes).ToArray();
		string encodedSessionKey = HostMigrationSecurity.EncodeBytes(sessionKey);
		RoomPasswordVerifier passwordVerifier = RoomPasswordRules.CreateVerifier(rawPassword);
		NetworkAuthorityCheckpoint baseline = CreateCheckpoint();
		NetworkAuthorityCheckpoint checkpoint = baseline with
		{
			RoomPasswordVerifierVersion = passwordVerifier.Version,
			RoomPasswordVerifierIterations = passwordVerifier.Iterations,
			RoomPasswordVerifierSalt = passwordVerifier.Salt,
			RoomPasswordVerifier = passwordVerifier.Verifier,
			SeatControls = baseline.SeatControls
				.Select((control, index) => control with
				{
					CredentialVerifier = index == 0
						? SeatCredentialRules.ComputeVerifier(rawCredential)
						: control.CredentialVerifier,
				})
				.ToList(),
		};

		True(NetworkAuthorityCheckpointCodec.TryEncode(checkpoint, out byte[] payload, out _, out _));
		string checkpointJson = Encoding.UTF8.GetString(payload);
		string planJson = JsonSerializer.Serialize(new NetworkMigrationPlan());
		string serialized = checkpointJson + planJson;
		False(serialized.Contains(rawPassword, StringComparison.Ordinal));
		False(serialized.Contains(rawCredential, StringComparison.Ordinal));
		False(serialized.Contains(encodedSessionKey, StringComparison.Ordinal));
		False(serialized.Contains("\"room_password\"", StringComparison.Ordinal));
		False(serialized.Contains("\"credential\"", StringComparison.Ordinal));
		False(serialized.Contains("\"session_key\"", StringComparison.Ordinal));
		True(checkpointJson.Contains("\"room_password_verifier\"", StringComparison.Ordinal));
		True(checkpointJson.Contains("\"credential_verifier\"", StringComparison.Ordinal));
		False(typeof(NetworkMigrationPlan).GetProperties().Any(property =>
			property.Name is "RoomPassword" or "Credential" or "SessionKey"));
	}

	private static void ReleaseDebugApiSurfaceCannotExportMigrationSecrets()
	{
		string root = FindRepositoryRoot();
		string migrationSource = string.Concat(
			Directory.GetFiles(Path.Combine(root, "scripts"), "NetworkManager.Migration*.cs")
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(File.ReadAllText));
		string debugSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Debug.cs"));
		string source = migrationSource + debugSource;
		False(Regex.IsMatch(
			source,
			@"public\s+(?:byte\[\]|string|NetworkAuthorityCheckpoint\??|EncryptedAuthorityCheckpointEnvelope\??|AuthorityCheckpointEnvelope)\s+Debug[A-Za-z0-9_]*\s*\(",
			RegexOptions.CultureInvariant));
		False(Regex.IsMatch(
			source,
			@"public\s+[^\r\n{]+\s+Debug[A-Za-z0-9_]*(?:CheckpointPayload|SessionKey|Credential|Password)\s*\(",
			RegexOptions.CultureInvariant));

		string serializationSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Serialization.cs"));
		int sharedConfigStart = serializationSource.IndexOf("ToDictionary(NetworkSessionConfig config)", StringComparison.Ordinal);
		int sharedConfigEnd = serializationSource.IndexOf("ToDictionary(NetworkPlayerPublicState player)", sharedConfigStart, StringComparison.Ordinal);
		True(sharedConfigStart >= 0 && sharedConfigEnd > sharedConfigStart);
		string sharedConfigSerializer = serializationSource[sharedConfigStart..sharedConfigEnd];
		False(sharedConfigSerializer.Contains("config.RoomPassword", StringComparison.Ordinal));
		False(sharedConfigSerializer.Contains("RoomPasswordVerifier", StringComparison.Ordinal));
		False(sharedConfigSerializer.Contains("Credential", StringComparison.Ordinal));
		False(sharedConfigSerializer.Contains("SessionKey", StringComparison.Ordinal));
	}

	private static void HostMigrationCapabilityIsReleaseDisabledWithDebugFixture()
	{
		string root = FindRepositoryRoot();
		string scene = File.ReadAllText(Path.Combine(root, "scenes", "multiplayer_lobby_scene.tscn"));
		string lobbySource = File.ReadAllText(Path.Combine(root, "scripts", "MultiplayerLobbyScene.cs"));
		string securitySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Security.cs"));
		string migrationSource = string.Concat(
			Directory.GetFiles(Path.Combine(root, "scripts"), "NetworkManager.Migration*.cs")
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(File.ReadAllText));
		string maintenanceSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Maintenance.cs"));
		string authoritySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.AuthorityActions.cs"));
		string managerSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.cs"));
		string reconnectSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Reconnect.cs"));
		string recoverySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.SessionRecoveryRpc.cs"));
		string steamSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Steam.cs"));
		string boardSessionSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.NetworkSession.cs"));

		int capabilityStart = securitySource.IndexOf("private static bool HostMigrationCapabilityAvailable", StringComparison.Ordinal);
		int capabilityEnd = securitySource.IndexOf("private readonly HashSet<int> _authenticatedPeerIds", capabilityStart, StringComparison.Ordinal);
		True(capabilityStart >= 0 && capabilityEnd > capabilityStart);
		string capabilityBlock = securitySource[capabilityStart..capabilityEnd];
		True(capabilityBlock.Contains("#if DEBUG", StringComparison.Ordinal));
		True(capabilityBlock.Contains("OS.GetEnvironment(\"CODEX_LAN_MIGRATION_FIXTURE\") == \"1\"", StringComparison.Ordinal));
		True(capabilityBlock.Contains("#else", StringComparison.Ordinal));
		True(capabilityBlock.Contains("return false;", StringComparison.Ordinal));
		True(capabilityBlock.Contains("#endif", StringComparison.Ordinal));
		True(securitySource.Contains("public bool HostMigrationEnabled => HostMigrationCapabilityAvailable;", StringComparison.Ordinal));
		True(securitySource.Contains("public bool TrustedHotStandbyAccepted => HostMigrationCapabilityAvailable;", StringComparison.Ordinal));
		int configureStart = securitySource.IndexOf("public void ConfigureHostMigration", StringComparison.Ordinal);
		int configureEnd = securitySource.IndexOf("private void InitializeHostMigrationSecurity", configureStart, StringComparison.Ordinal);
		True(configureStart >= 0 && configureEnd > configureStart);
		string configureBlock = securitySource[configureStart..configureEnd];
		True(configureBlock.Contains("_ = enabled;", StringComparison.Ordinal));
		True(configureBlock.Contains("_ = trustedHotStandbyAccepted;", StringComparison.Ordinal));
		False(configureBlock.Contains("HostMigrationCapabilityAvailable =", StringComparison.Ordinal));

		foreach (string removedControl in new[]
		{
			"HostMigrationToggle",
			"TrustedHotStandbyToggle",
			"MigrationTrustNoticeLabel",
		})
		{
			False(scene.Contains(removedControl, StringComparison.Ordinal));
			False(lobbySource.Contains(removedControl, StringComparison.Ordinal));
		}
		False(lobbySource.Contains("ConfigureHostMigration(", StringComparison.Ordinal));

		Dictionary<string, List<string>> rows = ReadLocalizationRowsByKey(
			Path.Combine(root, "localization", "ui.csv"),
			out _);
		foreach (string key in new[]
		{
			"MULTIPLAYER_LOBBY_HOST_MIGRATION_TOGGLE",
			"MULTIPLAYER_LOBBY_TRUSTED_STANDBY_TOGGLE",
			"MULTIPLAYER_LOBBY_MIGRATION_TRUST_NOTICE",
		})
		{
			False(rows.ContainsKey(key));
		}

		True(maintenanceSource.Contains("HostMigrationCapabilityAvailable && IsHost && _activeMigrationPending", StringComparison.Ordinal));
		True(maintenanceSource.Contains("return HostMigrationCapabilityAvailable", StringComparison.Ordinal));
		True(authoritySource.Contains("if (!HostMigrationCapabilityAvailable)", StringComparison.Ordinal));
		True(authoritySource.Contains("return CommitAuthoritySequence(authority);", StringComparison.Ordinal));
		True(authoritySource.Contains("return CommitAuthoritySequence();", StringComparison.Ordinal));
		True(migrationSource.Contains("private IHostMigrationTransport? CreateMigrationTransport", StringComparison.Ordinal));
		True(migrationSource.Contains("return null;", StringComparison.Ordinal));
		True(migrationSource.Contains("TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Disabled);", StringComparison.Ordinal));
		int artifactStart = migrationSource.IndexOf("public bool DebugHasMigrationArtifacts", StringComparison.Ordinal);
		int artifactEnd = migrationSource.IndexOf("public int DebugGetLocalStandbyCheckpointCacheCount", artifactStart, StringComparison.Ordinal);
		True(artifactStart >= 0 && artifactEnd > artifactStart);
		string artifactBlock = migrationSource[artifactStart..artifactEnd];
		False(artifactBlock.Contains("_issuedSeatCredentialsBySeat", StringComparison.Ordinal));
		False(artifactBlock.Contains("_localSeatCredential", StringComparison.Ordinal));

		True(managerSource.Contains("private const double ClientReconnectWindowSeconds = 10.0;", StringComparison.Ordinal));
		int disconnectStart = managerSource.IndexOf("private void OnServerDisconnected()", StringComparison.Ordinal);
		int disconnectEnd = managerSource.IndexOf("private void InsertPlayerInSeatOrder", disconnectStart, StringComparison.Ordinal);
		True(disconnectStart >= 0 && disconnectEnd > disconnectStart);
		string disconnectBlock = managerSource[disconnectStart..disconnectEnd];
		True(disconnectBlock.Contains("EnterReconnectState();", StringComparison.Ordinal));
		True(disconnectBlock.Contains("HandleHostDisconnected();", StringComparison.Ordinal));
		True(disconnectBlock.Contains("TryAdvancePlannedMigrationAfterSourceDisconnect()", StringComparison.Ordinal));
		False(disconnectBlock.Contains("BeginMigrationAfterHostDisconnect", StringComparison.Ordinal));
		False(disconnectBlock.Contains("StartMigrationAttemptsAfterHostDisconnect", StringComparison.Ordinal));
		True(managerSource.Contains(
			"bool resumesAuthenticatedSeat = _state == NetworkSessionState.InGame",
			StringComparison.Ordinal));
		True(reconnectSource.Contains("now - _reconnectWindowStartedAt >= ClientReconnectWindowSeconds", StringComparison.Ordinal));
		True(maintenanceSource.Contains("_reconnectWindowStartedAt + ClientReconnectWindowSeconds", StringComparison.Ordinal));
		int handlerStart = reconnectSource.IndexOf("private void HandleHostDisconnected()", StringComparison.Ordinal);
		int handlerEnd = reconnectSource.IndexOf("private void TryStartReconnectAttempt", handlerStart, StringComparison.Ordinal);
		True(handlerStart >= 0 && handlerEnd > handlerStart);
		string handlerBlock = reconnectSource[handlerStart..handlerEnd];
		True(handlerBlock.Contains("_hostDisconnectReturnPending", StringComparison.Ordinal));
		True(handlerBlock.Contains("_hostDisconnectErrorPublished", StringComparison.Ordinal));
		True(handlerBlock.Contains("RequestSessionCloseAsync(NetworkSessionCloseReason.InternalFailure)", StringComparison.Ordinal));
		True(handlerBlock.Contains("_pendingLobbyErrorLocalizationKey = needsLobbySceneChange ? localizationKey : string.Empty;", StringComparison.Ordinal));
		True(handlerBlock.Contains("nameof(ReturnToMultiplayerLobbyScene)", StringComparison.Ordinal));
		False(handlerBlock.Contains("Host disconnected.", StringComparison.Ordinal));
		True(handlerBlock.Contains("NetworkUserErrorCategory.HostDisconnected", StringComparison.Ordinal));
		True(boardSessionSource.Contains("if (networkManager.IsHostDisconnectReturnPending)", StringComparison.Ordinal));
		True(recoverySource.Contains("NetworkUserErrorRules.NormalizeLocalizationKey(", StringComparison.Ordinal));
		True(recoverySource.Contains("_hostEndedMatchLocalizationKey = hostEndedMatchLocalizationKey;", StringComparison.Ordinal));
		True(recoverySource.Contains(
			"RequestSessionCloseAsync(NetworkSessionCloseReason.InternalFailure)",
			StringComparison.Ordinal));
		True(recoverySource.Contains("EmitSignal(SignalName.ErrorRaised, hostEndedMatchLocalizationKey);", StringComparison.Ordinal));
		True(steamSource.Contains("_lastObservedSteamLobbyOwnerId != _originalSteamLobbyOwnerId", StringComparison.Ordinal));
	}

	private static void LobbyReadyIntentIsConsumedAfterCredentialAcknowledgement()
	{
		string root = FindRepositoryRoot();
		string managerSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.cs"));
		string lobbySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.LobbyMembership.cs"));
		string recoverySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.SessionRecoveryRpc.cs"));
		string lifecycleSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.MatchLifecycle.cs"));

		True(managerSource.Contains("Dictionary<int, bool> _pendingLobbyReadyByPeerId", StringComparison.Ordinal));
		True(lobbySource.Contains("LobbyReadyIntentDisposition.DeferUntilCredentialsAcknowledged", StringComparison.Ordinal));
		True(lobbySource.Contains("_pendingLobbyReadyByPeerId[peerId] = isReady;", StringComparison.Ordinal));
		int credentialsCommitted = recoverySource.IndexOf("CredentialsAcknowledged = true", StringComparison.Ordinal);
		int credentialCommitRpc = recoverySource.IndexOf("MethodName.RpcSeatCredentialCommitted", StringComparison.Ordinal);
		int pendingReadyApplied = recoverySource.IndexOf("TryApplyPendingPlayerReadyIntent(senderPeerId);", StringComparison.Ordinal);
		True(credentialsCommitted >= 0
			&& credentialCommitRpc > credentialsCommitted
			&& pendingReadyApplied > credentialCommitRpc);
		True(managerSource.Contains("_pendingLobbyReadyByPeerId.Remove((int)peerId);", StringComparison.Ordinal));
		True(lifecycleSource.Contains("_pendingLobbyReadyByPeerId.Clear();", StringComparison.Ordinal));
	}

	private static void LocalLobbyReadyIntentRetriesUntilAuthorityEcho()
	{
		string root = FindRepositoryRoot();
		string managerSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.cs"));
		string maintenanceSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Maintenance.cs"));
		string lobbySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.LobbyMembership.cs"));
		string lifecycleSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.MatchLifecycle.cs"));

		True(managerSource.Contains("LocalReadyIntentRetrySeconds = 1.0", StringComparison.Ordinal));
		True(maintenanceSource.Contains("PollLocalReadyIntent(now);", StringComparison.Ordinal));
		True(lobbySource.Contains("private void PollLocalReadyIntent(double now)", StringComparison.Ordinal));
		True(lobbySource.Contains("RpcId(\n\t\t\t\t1,\n\t\t\t\tMethodName.RpcSetReady", StringComparison.Ordinal));
		True(lobbySource.Contains("IsLocalReadyIntentSatisfied(isReady)", StringComparison.Ordinal));
		True(lifecycleSource.Contains("ClearPendingLocalReadyIntent();", StringComparison.Ordinal));
	}

	private static void LobbyInitialSeatCredentialDeliveryRetriesAcrossSyncOrdering()
	{
		string root = FindRepositoryRoot();
		string managerSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.cs"));
		string maintenanceSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Maintenance.cs"));
		string reconnectSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Reconnect.cs"));
		string recoverySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.SessionRecoveryRpc.cs"));

		True(maintenanceSource.Contains("PollSeatCredentialDelivery(now);", StringComparison.Ordinal));
		True(managerSource.Contains("SeatCredentialRetrySeconds = 1.0", StringComparison.Ordinal));
		True(reconnectSource.Contains("private void PollSeatCredentialDelivery(double now)", StringComparison.Ordinal));
		True(reconnectSource.Contains("SendSeatCredentialsToUnacknowledgedPeers();", StringComparison.Ordinal));
		True(recoverySource.Contains("NetworkSessionState.Joining or NetworkSessionState.Lobby", StringComparison.Ordinal));
		True(recoverySource.Contains("TryGetCachedLocalPlayer(out NetworkPlayerPublicState localPlayer)", StringComparison.Ordinal));
		True(recoverySource.Contains("string.Equals(localPlayer.PlayerSessionId, playerSessionId, StringComparison.Ordinal)", StringComparison.Ordinal));
	}

	private static void ClientBoardFingerprintPreservesRedactedSeatShape()
	{
		string source = File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"scripts",
			"GameBoardScene.NetworkSnapshot.cs"));
		int methodStart = source.IndexOf("private Godot.Collections.Array BuildSeatControlStatesArray()", StringComparison.Ordinal);
		int methodEnd = source.IndexOf(
			"private static Godot.Collections.Array BuildRuleScoresArray",
			methodStart,
			StringComparison.Ordinal);
		True(methodStart >= 0 && methodEnd > methodStart);
		string method = source[methodStart..methodEnd];
		True(method.Contains("[\"credential_verifier\"] = string.Empty", StringComparison.Ordinal));
		False(method.Contains("state.CredentialVerifier", StringComparison.Ordinal));
	}

	private static void SeatCredentialsIssueVerifyRotateAndRejectReplay()
	{
		string playerSessionId = SeatCredentialRules.IssuePlayerSessionId();
		string credential = SeatCredentialRules.IssueCredential();
		string verifier = SeatCredentialRules.ComputeVerifier(credential);
		Equal(SeatCredentialRules.PlayerSessionIdBytes * 2, playerSessionId.Length);
		True(SeatCredentialRules.IsCredentialEncodingValid(credential));
		True(SeatCredentialRules.IsVerifierValid(verifier));
		True(SeatCredentialRules.Verify(credential, verifier));
		False(SeatCredentialRules.Verify(SeatCredentialRules.IssueCredential(), verifier));

		SeatControlState current = new(
			SeatIndex: 1,
			Mode: SeatControlMode.AiProxyReclaimable,
			PeerId: 0,
			PlayerSessionId: playerSessionId,
			CredentialVerifier: verifier,
			CredentialGeneration: SeatCredentialRules.InitialGeneration,
			PendingCredentialVerifier: string.Empty,
			PendingCredentialGeneration: 0,
			PendingCredentialPeerId: 0);
		True(SeatCredentialRules.TryBeginRotation(current with { PeerId = 42 }, 42, out SeatControlState pending, out string nextCredential));
		True(SeatCredentialRules.Verify(credential, pending.CredentialVerifier));
		False(SeatCredentialRules.TryAcknowledge(pending, 43, 2, nextCredential, out _));
		True(SeatCredentialRules.TryAcknowledge(pending, 42, 2, nextCredential, out SeatControlState committed));
		Equal(2L, committed.CredentialGeneration);
		True(SeatCredentialRules.Verify(nextCredential, committed.CredentialVerifier));
		False(SeatCredentialRules.Verify(credential, committed.CredentialVerifier));
	}

	private static void SeatCredentialPendingRotationCancelsBackToCurrentGeneration()
	{
		string credential = SeatCredentialRules.IssueCredential();
		SeatControlState current = new(
			0,
			SeatControlMode.HumanConnected,
			10,
			SeatCredentialRules.IssuePlayerSessionId(),
			SeatCredentialRules.ComputeVerifier(credential),
			1,
			string.Empty,
			0,
			0);
		True(SeatCredentialRules.TryBeginRotation(current, 10, out SeatControlState pending, out _));
		SeatControlState cancelled = SeatCredentialRules.CancelPendingRotation(pending);
		Equal(1L, cancelled.CredentialGeneration);
		Equal(0L, cancelled.PendingCredentialGeneration);
		True(SeatCredentialRules.Verify(credential, cancelled.CredentialVerifier));
	}

	private static void PeerRateLimiterEnforcesCategoryIsolationRefillCleanupAndDisconnectThreshold()
	{
		var limiter = new NetworkPeerRateLimiter();
		for (int index = 0; index < 4; index++)
		{
			True(limiter.TryConsume(2, NetworkRequestCategory.LobbyMutation, 0, out _));
		}
		False(limiter.TryConsume(2, NetworkRequestCategory.LobbyMutation, 0, out bool disconnect));
		False(disconnect);
		True(limiter.TryConsume(2, NetworkRequestCategory.BoardAction, 0, out _));
		True(limiter.TryConsume(3, NetworkRequestCategory.LobbyMutation, 0, out _));
		True(limiter.TryConsume(2, NetworkRequestCategory.LobbyMutation, 1, out _));

		for (int index = 0; index < 64; index++)
		{
			False(limiter.TryConsume(2, NetworkRequestCategory.LobbyMutation, 1, out disconnect));
		}
		True(disconnect);
		limiter.ClearPeer(2);
		True(limiter.TryConsume(2, NetworkRequestCategory.LobbyMutation, 2, out disconnect));
		False(disconnect);
		True(limiter.BucketCount <= 2);
	}

	private static void LobbyMigrationRecoveryTracksClaimsAndTenSecondExpiry()
	{
		var recovery = new LobbyMigrationRecoveryState(
			targetEpoch: 7,
			startedAt: 100,
			timeoutSeconds: NetworkMigrationRules.LobbyRecoveryTimeoutSeconds,
			expectedPlayerSessionIds: new[] { "p2", "p3" });
		False(recovery.IsComplete);
		True(recovery.TryMarkClaimed("p2"));
		False(recovery.TryMarkClaimed("p2"));
		False(recovery.HasExpired(109.999));
		True(recovery.HasExpired(110));
		True(recovery.TryMarkClaimed("p3"));
		True(recovery.IsComplete);
	}

	private static void GameMigrationRecoveryWaitsForSnapshotAcknowledgements()
	{
		var recovery = new GameMigrationRecoveryState(
			targetEpoch: 8,
			startedAt: 100,
			expectedSeatIndices: new[] { 1, 2, 2, -1 });
		Equal(8L, recovery.TargetEpoch);
		Equal(2, recovery.ExpectedSeatCount);
		Equal(2, recovery.PendingSeatCount);
		False(recovery.IsComplete);
		True(recovery.TryMarkSnapshotApplied(1));
		False(recovery.TryMarkSnapshotApplied(1));
		False(recovery.TryMarkSnapshotApplied(3));
		Equal(1, recovery.RecoveredSeatCount);
		True(recovery.TryMarkSnapshotApplied(2));
		True(recovery.IsComplete);
	}

	private static void GameMigrationRecoveryRemovesDisconnectedSeatsWithoutExtendingDeadline()
	{
		var recovery = new GameMigrationRecoveryState(
			targetEpoch: 3,
			startedAt: 25,
			expectedSeatIndices: new[] { 1, 2 });
		double deadline = recovery.Deadline;
		True(recovery.TryMarkSnapshotApplied(1));
		True(recovery.TryRemoveDisconnectedSeat(2));
		False(recovery.TryRemoveDisconnectedSeat(2));
		Equal(deadline, recovery.Deadline);
		Equal(1, recovery.RecoveredSeatCount);
		Equal(1, recovery.DisconnectedSeatCount);
		True(recovery.IsComplete);
	}

	private static void GameMigrationRecoveryExpiresAfterFixedTenSeconds()
	{
		var recovery = new GameMigrationRecoveryState(
			targetEpoch: 5,
			startedAt: 40,
			expectedSeatIndices: new[] { 1, 2 });
		Equal(50.0, recovery.Deadline);
		False(recovery.HasExpired(49.999));
		False(recovery.TryExpire(49.999));
		True(recovery.TryExpire(50.0));
		True(recovery.IsComplete);
	}

	private static void LateGameMigrationReadinessMatchesOnlyTheCompletedCurrentEpoch()
	{
		True(NetworkMigrationRules.IsCompletedGameRecoveryEpoch(7, 7, 7));
		False(NetworkMigrationRules.IsCompletedGameRecoveryEpoch(6, 7, 7));
		False(NetworkMigrationRules.IsCompletedGameRecoveryEpoch(7, 6, 7));
		False(NetworkMigrationRules.IsCompletedGameRecoveryEpoch(7, 7, 8));
		False(NetworkMigrationRules.IsCompletedGameRecoveryEpoch(-1, -1, -1));
	}

	private static NetworkAuthorityCheckpoint CreateCheckpoint()
	{
		RoomPasswordVerifier passwordVerifier = RoomPasswordRules.CreateVerifier("secret");
		var roster = new List<NetworkMigrationRosterEntry>();
		var controls = new List<NetworkAuthoritySeatControl>();
		var decks = new List<List<NetworkCheckpointCard>>();
		var hands = new List<List<NetworkCheckpointCard>>();
		var items = new List<List<string>>();
		for (int seat = 0; seat < 2; seat++)
		{
			roster.Add(new NetworkMigrationRosterEntry
			{
				PlayerSessionId = $"peer:{seat}",
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
			decks.Add(new List<NetworkCheckpointCard>
			{
					new() { Code = $"card-{seat}", AttachedItemCardIds = ImmutableArray<string>.Empty },
			});
			hands.Add(new List<NetworkCheckpointCard>());
			items.Add(new List<string>());
		}

		return new NetworkAuthorityCheckpoint
		{
			SessionId = "session",
			MatchId = "match",
			RoomName = "room",
			RoomPasswordVerifierVersion = passwordVerifier.Version,
			RoomPasswordVerifierIterations = passwordVerifier.Iterations,
			RoomPasswordVerifierSalt = passwordVerifier.Salt,
			RoomPasswordVerifier = passwordVerifier.Verifier,
			MatchSeed = 20260714,
			PlayerCount = 2,
			ItemMode = true,
			WinningScore = 50,
			ManifestHash = new string('a', 64),
			Roster = roster,
			SeatControls = controls,
			LastAcceptedClientActionIds = new Dictionary<int, long> { [0] = 18, [1] = 19 },
			Decks = decks,
			Hands = hands,
			ItemHands = items,
			Scores = new[] { 10, 12 },
			CurrentTurnPlayerIndex = 0,
			TurnSequenceGeneration = 2,
			AuthorityAction = AuthorityActionState.Empty,
			RngSeed = 20260714,
			RngState = "123456789",
			SnapshotId = 11,
			AuthorityEpoch = 0,
			CheckpointId = 4,
			ReconnectClaims = new List<NetworkAuthorityReconnectClaim>
			{
				new()
				{
					MatchId = "match",
					SeatIndex = 0,
					CredentialGeneration = SeatCredentialRules.InitialGeneration,
					PlayerSessionId = "peer:0",
					ClaimedPeerId = 0,
					ClaimState = nameof(ReconnectClaimState.Unclaimed),
				},
				new()
				{
					MatchId = "match",
					SeatIndex = 1,
					CredentialGeneration = SeatCredentialRules.InitialGeneration,
					PlayerSessionId = "peer:1",
					ClaimedPeerId = 0,
					ClaimState = nameof(ReconnectClaimState.Unclaimed),
				},
			},
		};
	}
}
