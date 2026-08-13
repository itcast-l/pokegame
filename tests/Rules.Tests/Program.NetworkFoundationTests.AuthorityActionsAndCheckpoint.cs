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
