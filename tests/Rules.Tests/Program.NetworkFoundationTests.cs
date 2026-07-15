using System;
using System.Collections.Generic;

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
		Run("authority action json roundtrip keeps fixed fields", AuthorityActionJsonRoundtripKeepsFixedFields);
		Run("checkpoint codec roundtrips private deterministic state", CheckpointCodecRoundtripsPrivateDeterministicState);
		Run("checkpoint codec rejects tampered hash and oversized payload", CheckpointCodecRejectsTamperedHashAndOversizedPayload);
		Run("network protocol contexts fence epochs and snapshots", NetworkProtocolContextsFenceEpochsAndSnapshots);
		Run("migration plan selects the lowest eligible standby", MigrationPlanSelectsLowestEligibleStandby);
		Run("migration plan rejects stale or unsafe endpoints", MigrationPlanRejectsStaleOrUnsafeEndpoints);
	}

	private static void AuthorityActionPhasesAreOrderedAndResumable()
	{
		True(AuthorityActionStateRules.CanAdvancePhase(AuthorityActionPhase.Applying, AuthorityActionPhase.WaitingTargetPlayer));
		True(AuthorityActionStateRules.CanAdvancePhase(AuthorityActionPhase.WaitingTargetPlayer, AuthorityActionPhase.WaitingTargetHand));
		True(AuthorityActionStateRules.CanAdvancePhase(AuthorityActionPhase.WaitingAttachment, AuthorityActionPhase.Finalizing));
		False(AuthorityActionStateRules.CanAdvancePhase(AuthorityActionPhase.Finalizing, AuthorityActionPhase.Applying));

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
		AuthorityActionState transfer = state with
		{
			Kind = AuthorityActionKind.CardTransfer,
			ContinuationStep = AuthorityActionContinuationStep.CardTransfer,
		};
		Equal(AuthorityActionPhase.WaitingTargetHand, transfer.WithSelection(AuthorityActionPhase.WaitingTargetHand, 1).Phase);
	}

	private static void AuthorityActionValidationUsesKindPhaseAndPlayerCountSemantics()
	{
		AuthorityActionState targetPlayer = new()
		{
			ActionId = 1,
			ClientActionId = 1,
			Kind = AuthorityActionKind.CardTransfer,
			Phase = AuthorityActionPhase.WaitingTargetPlayer,
			ActorSeat = 0,
			TurnGeneration = 1,
			ItemHandIndex = 0,
			ItemOrRewardId = "queen",
			ContinuationStep = AuthorityActionContinuationStep.CardTransfer,
		};
		True(AuthorityActionStateRules.IsValid(targetPlayer, 2));
		False(AuthorityActionStateRules.IsValid(targetPlayer, 1));
		False(AuthorityActionStateRules.IsValid(targetPlayer, 5));

		False(AuthorityActionStateRules.IsValid(targetPlayer with
		{
			Kind = AuthorityActionKind.ItemCard,
		}, 2));
		False(AuthorityActionStateRules.IsValid(targetPlayer with
		{
			Phase = AuthorityActionPhase.WaitingTargetHand,
			TargetSeat = -1,
		}, 2));
		False(AuthorityActionStateRules.IsValid(targetPlayer with
		{
			TransferMutationApplied = true,
			PrimaryMutationApplied = false,
		}, 2));

		AuthorityActionState reward = targetPlayer with
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
		AuthorityActionState targetPlayer = new()
		{
			ActionId = 21,
			ClientActionId = 7,
			Kind = AuthorityActionKind.CardTransfer,
			Phase = AuthorityActionPhase.WaitingTargetPlayer,
			ActorSeat = 0,
			TurnGeneration = 3,
			ItemHandIndex = 0,
			ItemOrRewardId = "queen",
			TargetSeat = 1,
			ContinuationStep = AuthorityActionContinuationStep.CardTransfer,
			PrimaryMutationApplied = true,
		};
		True(AuthorityActionStateRules.IsValid(targetPlayer, 2));
		False(AuthorityActionStateRules.ShouldApplyMutation(targetPlayer, AuthorityActionMutationKind.Primary));

		AuthorityActionState targetHand = targetPlayer with
		{
			Phase = AuthorityActionPhase.WaitingTargetHand,
			TargetHandIndex = 2,
		};
		True(AuthorityActionStateRules.IsValid(targetHand, 2));
		False(AuthorityActionStateRules.ShouldApplyMutation(targetHand, AuthorityActionMutationKind.Primary));

		AuthorityActionState transferApplied = AuthorityActionStateRules.MarkMutationApplied(
			targetHand,
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
			TargetSeat = 0,
			TargetHandIndex = 1,
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
			ItemOrRewardId = "queen",
			TargetSeat = 1,
			TargetHandIndex = 3,
			DeferredTargetSeat = 1,
			DeferredTargetHandIndex = 2,
			DeferredTargetPhase = AuthorityActionPhase.WaitingTargetHand,
			ContinuationStep = AuthorityActionContinuationStep.CardTransfer,
			PrimaryMutationApplied = true,
		};
		True(AuthorityActionStateRules.IsValid(nestedDiscard, 2));

		AuthorityActionState resumed = AuthorityActionStateRules.AdvanceAfterDiscardMutation(nestedDiscard);
		True(AuthorityActionStateRules.IsValid(resumed, 2));
		Equal(AuthorityActionPhase.WaitingTargetHand, resumed.Phase);
		Equal(1, resumed.TargetSeat);
		Equal(2, resumed.TargetHandIndex);
		Equal(-1, resumed.DeferredTargetSeat);
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
		Equal("migration-secret", restored.SeatControls[0].SessionMigrationToken);
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

	private static void NetworkProtocolContextsFenceEpochsAndSnapshots()
	{
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

	private static void MigrationPlanSelectsLowestEligibleStandby()
	{
		var roster = new List<NetworkMigrationRosterEntry>
		{
			new() { StableUserId = "host", SeatIndex = 0, IsHost = true, IsConnected = true },
			new() { StableUserId = "seat-2", SeatIndex = 2, IsConnected = true },
			new() { StableUserId = "seat-1", SeatIndex = 1, IsConnected = true },
			new() { StableUserId = "seat-3", SeatIndex = 3, IsConnected = true },
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
			CandidateStableUserId = "seat-1",
			CandidateSeat = 1,
			ObservedHostAddress = "192.168.1.10",
			CandidateHostAddress = "192.168.1.11",
			Port = NetworkProtocol.LanHostMigrationPort,
			SourceStableUserId = "host",
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
				StableUserId = $"peer:{seat}",
				SeatIndex = seat,
				PeerId = seat + 1,
				IsHost = seat == 0,
				IsReady = true,
				IsConnected = true,
			});
			controls.Add(new NetworkAuthoritySeatControl
			{
				SeatIndex = seat,
				Mode = SeatControlMode.HumanConnected,
				PeerId = seat + 1,
				StableUserId = $"peer:{seat}",
				ReconnectToken = $"reconnect-{seat}",
				SessionMigrationToken = seat == 0 ? "migration-secret" : "migration-1",
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
			SessionMigrationTokens = new Dictionary<int, string>
			{
				[0] = "migration-secret",
				[1] = "migration-1",
			},
			ReconnectClaims = new List<NetworkAuthorityReconnectClaim>
			{
				new()
				{
					MatchId = "match",
					SeatIndex = 0,
					ReconnectToken = "reconnect-0",
					StableUserId = "peer:0",
					ClaimedPeerId = 0,
					ClaimState = nameof(ReconnectClaimState.Unclaimed),
				},
				new()
				{
					MatchId = "match",
					SeatIndex = 1,
					ReconnectToken = "reconnect-1",
					StableUserId = "peer:1",
					ClaimedPeerId = 0,
					ClaimState = nameof(ReconnectClaimState.Unclaimed),
				},
			},
		};
	}
}
