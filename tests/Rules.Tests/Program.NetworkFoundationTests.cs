using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
		Run("standby checkpoint sends are serialized and rate bounded", StandbyCheckpointSendsAreSerializedAndRateBounded);
		Run("migration plan rejects stale or unsafe endpoints", MigrationPlanRejectsStaleOrUnsafeEndpoints);
		Run("seat credentials issue verify rotate and reject replay", SeatCredentialsIssueVerifyRotateAndRejectReplay);
		Run("seat credential pending rotation cancels back to current generation", SeatCredentialPendingRotationCancelsBackToCurrentGeneration);
		Run("peer rate limiter enforces category isolation refill cleanup and disconnect threshold", PeerRateLimiterEnforcesCategoryIsolationRefillCleanupAndDisconnectThreshold);
		Run("lobby migration recovery tracks claims and ten second expiry", LobbyMigrationRecoveryTracksClaimsAndTenSecondExpiry);
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
		Equal(source.RoomPassword, restored.RoomPassword);
		Equal(source.PlayerCount, restored.PlayerCount);
		Equal(source.SnapshotId, restored.SnapshotId);
		Equal(source.AuthorityEpoch, restored.AuthorityEpoch);
		Equal(source.CheckpointId, restored.CheckpointId);
		Equal("secret", restored.RoomPassword);
		Equal(SeatCredentialRules.ComputeVerifier("credential-0"), restored.SeatControls[0].CredentialVerifier);
		Equal(19L, restored.LastAcceptedClientActionIds[1]);
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
						Card = new NetworkCheckpointCard { Code = null!, AttachedItemCardIds = Array.Empty<string>() },
					},
				},
			},
			source with
			{
				Hands = new List<List<NetworkCheckpointCard>>
				{
					new() { new NetworkCheckpointCard { Code = "card", AttachedItemCardIds = null! } },
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
		byte[] exposedPayload = committed.GetPayloadCopy();
		exposedPayload[0] ^= 0xff;
		False(exposedPayload.SequenceEqual(committed.GetPayloadCopy()));

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
		True(committed.GetPayloadCopy().SequenceEqual(current.GetPayloadCopy()));
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
		Equal(8, NetworkProtocol.CurrentVersion);
		Equal(3, NetworkProtocol.AuthorityCheckpointSchemaVersion);
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

	private static void MigrationPlanRejectsStaleOrUnsafeEndpoints()
	{
		NetworkMigrationPlan valid = new()
		{
			SessionId = "session",
			MatchId = "match",
			SourceEpoch = 3,
			TargetEpoch = 4,
			CheckpointId = 7,
			CheckpointHash = new string('a', 64),
			CandidatePlayerSessionId = "seat-1",
			CandidateSeat = 1,
			ObservedHostAddress = "192.168.1.10",
			CandidateHostAddress = "192.168.1.11",
			Port = NetworkProtocol.LanHostMigrationPort,
			SourcePlayerSessionId = "host",
			Phase = NetworkAuthorityPhase.InGame,
		};
		True(NetworkMigrationRules.IsPlanVisibleToClients(valid));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with { TargetEpoch = 3 }));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with { CheckpointHash = new string('A', 64) }));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with { CandidateHostAddress = "bad address" }));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with { Port = NetworkSessionDefaults.LanGamePort + 1 }));
		True(NetworkMigrationRules.CanCandidateStart(valid, "seat-1", NetworkSessionState.MigratingHost, hasCheckpoint: true, isSourceHost: false));
		False(NetworkMigrationRules.CanCandidateStart(valid, "host", NetworkSessionState.MigratingHost, hasCheckpoint: true, isSourceHost: false));
		False(NetworkMigrationRules.ShouldFollowerConnect(valid, "seat-1", NetworkSessionState.MigratingHost, isCandidate: true, elapsedTimeout: false));
	}

	private static void StandbyCheckpointSendsAreSerializedAndRateBounded()
	{
		True(NetworkMigrationRules.CanSendStandbyCheckpoint(10.0, -1.0, hasPendingCheckpoint: false));
		False(NetworkMigrationRules.CanSendStandbyCheckpoint(10.1, 10.0, hasPendingCheckpoint: false));
		True(NetworkMigrationRules.CanSendStandbyCheckpoint(10.5, 10.0, hasPendingCheckpoint: false));
		False(NetworkMigrationRules.CanSendStandbyCheckpoint(11.0, 10.0, hasPendingCheckpoint: true));
		False(NetworkMigrationRules.CanSendStandbyCheckpoint(double.NaN, 10.0, hasPendingCheckpoint: false));
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

	private static NetworkAuthorityCheckpoint CreateCheckpoint()
	{
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
				new() { Code = $"card-{seat}", AttachedItemCardIds = Array.Empty<string>() },
			});
			hands.Add(new List<NetworkCheckpointCard>());
			items.Add(new List<string>());
		}

		return new NetworkAuthorityCheckpoint
		{
			SessionId = "session",
			MatchId = "match",
			RoomName = "room",
			RoomPassword = "secret",
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
