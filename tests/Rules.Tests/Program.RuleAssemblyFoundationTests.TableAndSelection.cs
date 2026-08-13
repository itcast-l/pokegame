using System.Collections.Immutable;
using System.Reflection;
using Deckland.Rules;

internal static partial class Program
{
	private static void SliceFourTableCommandsValidateAtomically()
	{
		var session = new AuthorityRuleSession(
			new TestRuleRandomSource(107),
			MatchRuleProfile.Empty);
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray.Create(new LogicalCardState(10, "clubs:2", "clubs", "2")),
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray.Create(
					new LogicalCardState(
						20,
						"hearts:3",
						"hearts",
						"3",
						ImmutableArray.Create("magnet")),
					new LogicalCardState(21, "spades:4", "spades", "4")),
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				0));
		var tableCards = ImmutableArray.Create(
			CreateRuleTableCardState(100, 30, "diamonds:5"),
			CreateRuleTableCardState(101, 31, "clubs:6") with
			{
				IsScoreSuppressed = true,
			});
		True(session.Submit(new InitializeScriptedMatchCommand(
			610,
			0,
			1,
			0,
			2,
			100,
			players,
			tableCards)).Accepted);

		MatchReadModel beforeInvalidSuppression = session.ReadModel;
		RuleCommandResult invalidSuppression = session.Submit(
			new SetTableCardsScoreSuppressedCommand(
				611,
				beforeInvalidSuppression.Revision,
				beforeInvalidSuppression.LifecycleGeneration,
				beforeInvalidSuppression.TurnGeneration,
				ActingSeat: 0,
				TableCards: ImmutableArray.Create(
					new RuleTableCardReference(0, 100),
					new RuleTableCardReference(1, 101)),
				ExpectedScoreSuppressed: false,
				ScoreSuppressed: true));
		False(invalidSuppression.Accepted);
		AssertReadModelsEquivalent(beforeInvalidSuppression, invalidSuppression.ReadModel);
		False(invalidSuppression.ReadModel.TableCards[0].IsScoreSuppressed);

		RuleCommandResult suppressed = session.Submit(
			new SetTableCardsScoreSuppressedCommand(
				612,
				beforeInvalidSuppression.Revision,
				beforeInvalidSuppression.LifecycleGeneration,
				beforeInvalidSuppression.TurnGeneration,
				ActingSeat: 0,
				TableCards: ImmutableArray.Create(new RuleTableCardReference(0, 100)),
				ExpectedScoreSuppressed: false,
				ScoreSuppressed: true));
		True(suppressed.Accepted);
		True(suppressed.ReadModel.TableCards[0].IsScoreSuppressed);

		MatchReadModel beforeInvalidPlace = suppressed.ReadModel;
		RuleCommandResult invalidPlace = session.Submit(new PlaceTableCardCommand(
			613,
			beforeInvalidPlace.Revision,
			beforeInvalidPlace.LifecycleGeneration,
			beforeInvalidPlace.TurnGeneration,
			ActingSeat: 0,
			Card: new LogicalCardState(20, "hearts:3", "hearts", "3"),
			TableIndex: 1,
			IsFaceUp: true));
		False(invalidPlace.Accepted);
		AssertReadModelsEquivalent(beforeInvalidPlace, invalidPlace.ReadModel);

		RuleCommandResult placed = session.Submit(new PlaceTableCardCommand(
			614,
			beforeInvalidPlace.Revision,
			beforeInvalidPlace.LifecycleGeneration,
			beforeInvalidPlace.TurnGeneration,
			ActingSeat: 0,
			Card: new LogicalCardState(40, "hearts:7", "hearts", "7"),
			TableIndex: 1,
			IsFaceUp: true,
			CardOwnerSeat: 1));
		True(placed.Accepted);
		Equal(3, placed.ReadModel.TableCards.Length);
		Equal(40L, placed.ReadModel.TableCards[1].Card!.Value.RuntimeInstanceId);
		long placedTableRuntimeId = placed.Mutation.LogicalEntityId;

		MatchReadModel beforeInvalidTransfer = placed.ReadModel;
		RuleCommandResult invalidTransfer = session.Submit(new TransferTopDeckCardCommand(
			615,
			beforeInvalidTransfer.Revision,
			beforeInvalidTransfer.LifecycleGeneration,
			beforeInvalidTransfer.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			ExpectedRuntimeInstanceId: 999,
			PreserveCardEffectsWhenEnteringDeck: false));
		False(invalidTransfer.Accepted);
		AssertReadModelsEquivalent(beforeInvalidTransfer, invalidTransfer.ReadModel);

		RuleCommandResult transferred = session.Submit(new TransferTopDeckCardCommand(
			616,
			beforeInvalidTransfer.Revision,
			beforeInvalidTransfer.LifecycleGeneration,
			beforeInvalidTransfer.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			ExpectedRuntimeInstanceId: 20,
			PreserveCardEffectsWhenEnteringDeck: false));
		True(transferred.Accepted);
		Equal(2, transferred.ReadModel.Players[0].Deck.Length);
		Equal(1, transferred.ReadModel.Players[1].Deck.Length);
		Equal(0, transferred.ReadModel.Players[0].Deck[1].NormalizedAttachedEffectIds.Length);

		MatchReadModel beforeInvalidMove = transferred.ReadModel;
		RuleCommandResult invalidMove = session.Submit(new MoveTableCardToDeckCommand(
			617,
			beforeInvalidMove.Revision,
			beforeInvalidMove.LifecycleGeneration,
			beforeInvalidMove.TurnGeneration,
			ActingSeat: 0,
			TableIndex: 1,
			ExpectedTableCardRuntimeInstanceId: placedTableRuntimeId + 1,
			DeckSeat: 0,
			PreserveCardEffectsWhenEnteringDeck: true));
		False(invalidMove.Accepted);
		AssertReadModelsEquivalent(beforeInvalidMove, invalidMove.ReadModel);

		RuleCommandResult moved = session.Submit(new MoveTableCardToDeckCommand(
			618,
			beforeInvalidMove.Revision,
			beforeInvalidMove.LifecycleGeneration,
			beforeInvalidMove.TurnGeneration,
			ActingSeat: 0,
			TableIndex: 1,
			ExpectedTableCardRuntimeInstanceId: placedTableRuntimeId,
			DeckSeat: 0,
			PreserveCardEffectsWhenEnteringDeck: true));
		True(moved.Accepted);
		Equal(2, moved.ReadModel.TableCards.Length);
		Equal(40L, moved.ReadModel.Players[0].Deck[^1].RuntimeInstanceId);
		Equal(PresentationIntentKind.TableCardsRemoved, moved.PresentationIntents.Single().Kind);
	}

	private static void AuthorityMandatoryDiscardSelectionIsGatedAndAtomic()
	{
		AuthorityRuleSession session = CreateItemCommandSession(itemRulesEnabled: true);
		MatchReadModel initial = session.ReadModel;
		RuleCommandResult began = session.Submit(new BeginMandatorySelectionCommand(
			310,
			initial.Revision,
			initial.LifecycleGeneration,
			initial.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.HandLimitDiscard,
			SourceHandIndex: -1,
			ItemId: string.Empty,
			LegalSelectionIndexes: ImmutableArray.Create(0, 2)));
		True(began.Accepted);
		Equal(MandatorySelectionKind.HandLimitDiscard, began.ReadModel.NormalizedMandatorySelection.Kind);
		Equal(PresentationIntentKind.MandatorySelectionBegan, began.PresentationIntents.Single().Kind);

		MatchReadModel pending = began.ReadModel;
		RuleCommandResult blocked = session.Submit(new DiscardHandCardCommand(
			311,
			pending.Revision,
			pending.LifecycleGeneration,
			pending.TurnGeneration,
			Seat: 0,
			DisplayIndex: 0,
			ExpectedCardRuntimeInstanceId: 1));
		False(blocked.Accepted);
		Equal(RuleRejectionCode.MandatorySelectionPending, blocked.Rejection.Code);
		AssertReadModelsEquivalent(pending, blocked.ReadModel);

		RuleCommandResult wrongKind = session.Submit(new ResolveMandatorySelectionCommand(
			312,
			pending.Revision,
			pending.LifecycleGeneration,
			pending.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.AttachmentHandCard,
			SelectionIndex: 0,
			ExpectedCardRuntimeInstanceId: 1));
		False(wrongKind.Accepted);
		Equal(RuleRejectionCode.MandatorySelectionMismatch, wrongKind.Rejection.Code);
		AssertReadModelsEquivalent(pending, wrongKind.ReadModel);

		RuleCommandResult wrongSeat = session.Submit(new ResolveMandatorySelectionCommand(
			313,
			pending.Revision,
			pending.LifecycleGeneration,
			pending.TurnGeneration,
			ActingSeat: 1,
			MandatorySelectionKind.HandLimitDiscard,
			SelectionIndex: 0,
			ExpectedCardRuntimeInstanceId: 1));
		False(wrongSeat.Accepted);
		Equal(RuleRejectionCode.MandatorySelectionMismatch, wrongSeat.Rejection.Code);
		AssertReadModelsEquivalent(pending, wrongSeat.ReadModel);

		RuleCommandResult illegalIndex = session.Submit(new ResolveMandatorySelectionCommand(
			314,
			pending.Revision,
			pending.LifecycleGeneration,
			pending.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.HandLimitDiscard,
			SelectionIndex: 1,
			ExpectedCardRuntimeInstanceId: 2));
		False(illegalIndex.Accepted);
		Equal(RuleRejectionCode.InvalidIndex, illegalIndex.Rejection.Code);
		AssertReadModelsEquivalent(pending, illegalIndex.ReadModel);

		RuleCommandResult wrongIdentity = session.Submit(new ResolveMandatorySelectionCommand(
			315,
			pending.Revision,
			pending.LifecycleGeneration,
			pending.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.HandLimitDiscard,
			SelectionIndex: 0,
			ExpectedCardRuntimeInstanceId: 999));
		False(wrongIdentity.Accepted);
		Equal(RuleRejectionCode.InvalidCommand, wrongIdentity.Rejection.Code);
		AssertReadModelsEquivalent(pending, wrongIdentity.ReadModel);

		RuleCommandResult resolved = session.Submit(new ResolveMandatorySelectionCommand(
			316,
			pending.Revision,
			pending.LifecycleGeneration,
			pending.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.HandLimitDiscard,
			SelectionIndex: 2,
			ExpectedItemId: "bomb"));
		True(resolved.Accepted);
		Equal(MandatorySelectionKind.None, resolved.ReadModel.NormalizedMandatorySelection.Kind);
		Equal(1, resolved.ReadModel.Players[0].ItemHand.Length);
		Equal("lens", resolved.ReadModel.Players[0].ItemHand[0].Id);
		Equal(2, resolved.PresentationIntents.Length);
		Equal(PresentationIntentKind.MandatorySelectionResolved, resolved.PresentationIntents[1].Kind);

		RuleCommandResult duplicate = session.Submit(new ResolveMandatorySelectionCommand(
			316,
			resolved.Revision,
			resolved.ReadModel.LifecycleGeneration,
			resolved.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.HandLimitDiscard,
			SelectionIndex: 2,
			ExpectedItemId: "bomb"));
		False(duplicate.Accepted);
		Equal(RuleRejectionCode.DuplicateAction, duplicate.Rejection.Code);

		RuleCommandResult repeated = session.Submit(new ResolveMandatorySelectionCommand(
			317,
			resolved.Revision,
			resolved.ReadModel.LifecycleGeneration,
			resolved.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.HandLimitDiscard,
			SelectionIndex: 0,
			ExpectedCardRuntimeInstanceId: 1));
		False(repeated.Accepted);
		Equal(RuleRejectionCode.MandatorySelectionMissing, repeated.Rejection.Code);

		RuleCommandResult staleGeneration = session.Submit(new ResolveMandatorySelectionCommand(
			318,
			resolved.Revision,
			resolved.ReadModel.LifecycleGeneration,
			resolved.TurnGeneration - 1,
			ActingSeat: 0,
			MandatorySelectionKind.HandLimitDiscard,
			SelectionIndex: 0,
			ExpectedCardRuntimeInstanceId: 1));
		False(staleGeneration.Accepted);
		Equal(RuleRejectionCode.StaleTurnGeneration, staleGeneration.Rejection.Code);
	}

	private static void AuthorityMandatoryAttachmentAndClearAreAtomic()
	{
		AuthorityRuleSession session = CreateItemCommandSession(itemRulesEnabled: true);
		MatchReadModel initial = session.ReadModel;
		RuleCommandResult empty = session.Submit(new BeginMandatorySelectionCommand(
			320,
			initial.Revision,
			initial.LifecycleGeneration,
			initial.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.AttachmentHandCard,
			SourceHandIndex: -1,
			ItemId: "magnet",
			LegalSelectionIndexes: ImmutableArray<int>.Empty));
		False(empty.Accepted);
		Equal(RuleRejectionCode.InvalidCommand, empty.Rejection.Code);
		AssertReadModelsEquivalent(initial, empty.ReadModel);

		RuleCommandResult began = session.Submit(new BeginMandatorySelectionCommand(
			321,
			initial.Revision,
			initial.LifecycleGeneration,
			initial.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.AttachmentHandCard,
			SourceHandIndex: -1,
			ItemId: "magnet",
			LegalSelectionIndexes: ImmutableArray.Create(0, 1)));
		True(began.Accepted);

		RuleCommandResult attached = session.Submit(new ResolveMandatorySelectionCommand(
			322,
			began.Revision,
			began.ReadModel.LifecycleGeneration,
			began.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.AttachmentHandCard,
			SelectionIndex: 1,
			ExpectedCardRuntimeInstanceId: 2));
		True(attached.Accepted);
		Equal("magnet", attached.ReadModel.Players[0].Hand[1].NormalizedAttachedEffectIds.Single());
		Equal(MandatorySelectionKind.None, attached.ReadModel.NormalizedMandatorySelection.Kind);

		MatchReadModel beforeClear = attached.ReadModel;
		RuleCommandResult beganClear = session.Submit(new BeginMandatorySelectionCommand(
			323,
			beforeClear.Revision,
			beforeClear.LifecycleGeneration,
			beforeClear.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.AttachmentHandCard,
			SourceHandIndex: -1,
			ItemId: "anchor",
			LegalSelectionIndexes: ImmutableArray.Create(0, 1)));
		True(beganClear.Accepted);
		ImmutableArray<LogicalCardState> handBeforeClear = beganClear.ReadModel.Players[0].Hand;
		RuleCommandResult cleared = session.Submit(new ClearMandatorySelectionCommand(
			324,
			beganClear.Revision,
			beganClear.ReadModel.LifecycleGeneration,
			beganClear.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.AttachmentHandCard));
		True(cleared.Accepted);
		Equal(MandatorySelectionKind.None, cleared.ReadModel.NormalizedMandatorySelection.Kind);
		AssertCardStatesEquivalent(handBeforeClear, cleared.ReadModel.Players[0].Hand);
		Equal(PresentationIntentKind.MandatorySelectionCleared, cleared.PresentationIntents.Single().Kind);
	}

	private static void MandatorySelectionSnapshotsValidateAndRestore()
	{
		AuthorityRuleSession session = CreateItemCommandSession(itemRulesEnabled: true);
		MatchReadModel initial = session.ReadModel;
		True(session.Submit(new BeginMandatorySelectionCommand(
			330,
			initial.Revision,
			initial.LifecycleGeneration,
			initial.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.AttachmentHandCard,
			SourceHandIndex: -1,
			ItemId: "magnet",
			LegalSelectionIndexes: ImmutableArray.Create(0, 1))).Accepted);

		MatchStateSnapshot snapshot = session.CaptureSnapshot();
		MatchRuleProfile profile = CreateItemRuleProfile();
		AuthorityRuleSession restored = AuthorityRuleSession.Restore(
			snapshot,
			new TestRuleRandomSource(999),
			profile);
		MandatorySelectionState restoredSelection = restored.ReadModel.NormalizedMandatorySelection;
		Equal(MandatorySelectionKind.AttachmentHandCard, restoredSelection.Kind);
		Equal(0, restoredSelection.Seat);
		Equal(snapshot.TurnGeneration, restoredSelection.TurnGeneration);
		Equal("magnet", restoredSelection.ItemId);
		True(restoredSelection.NormalizedLegalSelectionIndexes.SequenceEqual(new[] { 0, 1 }));

		foreach (MandatorySelectionState invalidSelection in new[]
		{
			restoredSelection with { Seat = snapshot.PlayerCount },
			restoredSelection with { TurnGeneration = snapshot.TurnGeneration + 1 },
			restoredSelection with { ItemId = string.Empty },
			restoredSelection with { LegalSelectionIndexes = ImmutableArray.Create(0, 0) },
			restoredSelection with { LegalSelectionIndexes = ImmutableArray.Create(2) },
		})
		{
			var replica = new ReplicaRuleSession();
			SnapshotApplyResult rejected = replica.ApplyAuthoritySnapshot(
				snapshot with { MandatorySelection = invalidSelection });
			False(rejected.Applied);
			Equal(RuleRejectionCode.InvalidConfiguration, rejected.Rejection.Code);
		}

		MatchReadModel pending = session.ReadModel;
		True(session.Submit(new ClearMandatorySelectionCommand(
			331,
			pending.Revision,
			pending.LifecycleGeneration,
			pending.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.AttachmentHandCard)).Accepted);
		MatchStateSnapshot noSelectionSnapshot = session.CaptureSnapshot() with
		{
			MandatorySelection = MandatorySelectionState.None with
			{
				LegalSelectionIndexes = default,
			},
		};
		AuthorityRuleSession noSelectionRestored = AuthorityRuleSession.Restore(
			noSelectionSnapshot,
			new TestRuleRandomSource(1000),
			profile);
		Equal(MandatorySelectionKind.None, noSelectionRestored.ReadModel.NormalizedMandatorySelection.Kind);
		Equal(0, noSelectionRestored.ReadModel.NormalizedMandatorySelection.NormalizedLegalSelectionIndexes.Length);
	}

	private static void PendingAttachmentRewardQueueIsOrderedAtomicAndRestorable()
	{
		AuthorityRuleSession session = CreateItemCommandSession(itemRulesEnabled: true);
		RuleCommandResult Enqueue(long actionId, int seat, string itemId)
		{
			MatchReadModel current = session.ReadModel;
			return session.Submit(new EnqueuePendingAttachmentRewardCommand(
				actionId,
				current.Revision,
				current.LifecycleGeneration,
				current.TurnGeneration,
				seat,
				itemId));
		}

		True(Enqueue(340, 0, "magnet").Accepted);
		True(Enqueue(341, 1, "anchor").Accepted);
		True(Enqueue(342, 0, "magnet").Accepted);
		True(session.ReadModel.NormalizedPendingAttachmentRewards.SequenceEqual(new[]
		{
			new PendingAttachmentRewardState(0, "magnet"),
			new PendingAttachmentRewardState(1, "anchor"),
			new PendingAttachmentRewardState(0, "magnet"),
		}));

		MatchReadModel beforeWrongTake = session.ReadModel;
		RuleCommandResult wrongTake = session.Submit(new TakePendingAttachmentRewardCommand(
			343,
			beforeWrongTake.Revision,
			beforeWrongTake.LifecycleGeneration,
			beforeWrongTake.TurnGeneration,
			ExpectedSeat: 1,
			ExpectedItemId: "anchor"));
		False(wrongTake.Accepted);
		Equal(RuleRejectionCode.NoLegalAction, wrongTake.Rejection.Code);
		AssertReadModelsEquivalent(beforeWrongTake, wrongTake.ReadModel);

		MatchReadModel beforeReplace = session.ReadModel;
		RuleCommandResult replaced = session.Submit(new ReplacePendingAttachmentRewardCommand(
			344,
			beforeReplace.Revision,
			beforeReplace.LifecycleGeneration,
			beforeReplace.TurnGeneration,
			Seat: 0,
			ExpectedItemId: "magnet",
			ReplacementItemId: "lens"));
		True(replaced.Accepted);
		True(replaced.ReadModel.NormalizedPendingAttachmentRewards.SequenceEqual(new[]
		{
			new PendingAttachmentRewardState(0, "magnet"),
			new PendingAttachmentRewardState(1, "anchor"),
			new PendingAttachmentRewardState(0, "lens"),
		}));

		RuleCommandResult removed = session.Submit(new RemovePendingAttachmentRewardCommand(
			345,
			replaced.Revision,
			replaced.ReadModel.LifecycleGeneration,
			replaced.TurnGeneration,
			Seat: 0,
			ItemId: "magnet"));
		True(removed.Accepted);
		True(removed.ReadModel.NormalizedPendingAttachmentRewards.SequenceEqual(new[]
		{
			new PendingAttachmentRewardState(1, "anchor"),
			new PendingAttachmentRewardState(0, "lens"),
		}));

		RuleCommandResult took = session.Submit(new TakePendingAttachmentRewardCommand(
			346,
			removed.Revision,
			removed.ReadModel.LifecycleGeneration,
			removed.TurnGeneration,
			ExpectedSeat: 1,
			ExpectedItemId: "anchor"));
		True(took.Accepted);
		Equal(new PendingAttachmentRewardState(0, "lens"),
			took.ReadModel.NormalizedPendingAttachmentRewards.Single());

		MatchReadModel beforeInvalidSet = session.ReadModel;
		RuleCommandResult invalidSet = session.Submit(new SetPendingAttachmentRewardsCommand(
			347,
			beforeInvalidSet.Revision,
			beforeInvalidSet.LifecycleGeneration,
			beforeInvalidSet.TurnGeneration,
			ImmutableArray.Create(new PendingAttachmentRewardState(2, "magnet"))));
		False(invalidSet.Accepted);
		Equal(RuleRejectionCode.InvalidSeat, invalidSet.Rejection.Code);
		AssertReadModelsEquivalent(beforeInvalidSet, invalidSet.ReadModel);

		RuleCommandResult set = session.Submit(new SetPendingAttachmentRewardsCommand(
			348,
			beforeInvalidSet.Revision,
			beforeInvalidSet.LifecycleGeneration,
			beforeInvalidSet.TurnGeneration,
			ImmutableArray.Create(
				new PendingAttachmentRewardState(1, "magnet"),
				new PendingAttachmentRewardState(0, "lens"))));
		True(set.Accepted);

		MatchStateSnapshot snapshot = session.CaptureSnapshot();
		AuthorityRuleSession restored = AuthorityRuleSession.Restore(
			snapshot,
			new TestRuleRandomSource(1001),
			CreateItemRuleProfile());
		True(restored.ReadModel.NormalizedPendingAttachmentRewards.SequenceEqual(
			set.ReadModel.NormalizedPendingAttachmentRewards));
		var replica = new ReplicaRuleSession();
		True(replica.ApplyAuthoritySnapshot(snapshot).Applied);
		True(replica.ReadModel.NormalizedPendingAttachmentRewards.SequenceEqual(
			set.ReadModel.NormalizedPendingAttachmentRewards));

		foreach (PendingAttachmentRewardState invalidReward in new[]
		{
			new PendingAttachmentRewardState(snapshot.PlayerCount, "magnet"),
			new PendingAttachmentRewardState(0, string.Empty),
		})
		{
			var invalidReplica = new ReplicaRuleSession();
			SnapshotApplyResult rejected = invalidReplica.ApplyAuthoritySnapshot(
				snapshot with
				{
					PendingAttachmentRewards = ImmutableArray.Create(invalidReward),
				});
			False(rejected.Applied);
			Equal(RuleRejectionCode.InvalidConfiguration, rejected.Rejection.Code);
		}

		MatchReadModel beforeSelection = session.ReadModel;
		RuleCommandResult began = session.Submit(new BeginMandatorySelectionCommand(
			349,
			beforeSelection.Revision,
			beforeSelection.LifecycleGeneration,
			beforeSelection.TurnGeneration,
			ActingSeat: 0,
			MandatorySelectionKind.AttachmentHandCard,
			SourceHandIndex: -1,
			ItemId: "magnet",
			LegalSelectionIndexes: ImmutableArray.Create(0, 1)));
		True(began.Accepted);
		RuleCommandResult blocked = session.Submit(new EnqueuePendingAttachmentRewardCommand(
			350,
			began.Revision,
			began.ReadModel.LifecycleGeneration,
			began.TurnGeneration,
			Seat: 0,
			ItemId: "anchor"));
		False(blocked.Accepted);
		Equal(RuleRejectionCode.MandatorySelectionPending, blocked.Rejection.Code);

		RuleCommandResult ended = session.Submit(new EndMatchSessionCommand(
			351,
			began.Revision,
			began.ReadModel.LifecycleGeneration,
			began.TurnGeneration));
		True(ended.Accepted);
		Equal(0, ended.ReadModel.NormalizedPendingAttachmentRewards.Length);
	}

	private static void ExtraActionCommandsAreOrderedAtomicAndRestorable()
	{
		var profile = new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule(
				"extra-actions",
				0,
				capabilities: new MatchCapabilitySet(MatchCapability.UpgradeSelection)),
		});
		var session = new AuthorityRuleSession(new TestRuleRandomSource(109), profile);
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(new LogicalCardState(1, "clubs:2", "clubs", "2")),
				ImmutableArray<LogicalItemState>.Empty,
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(new LogicalCardState(2, "hearts:3", "hearts", "3")),
				ImmutableArray<LogicalItemState>.Empty,
				0));
		True(session.Submit(new InitializeScriptedMatchCommand(
			620,
			0,
			1,
			0,
			2,
			100,
			players,
			ImmutableArray<TableCardState>.Empty)).Accepted);

		MatchReadModel current = session.ReadModel;
		RuleCommandResult began = session.Submit(new BeginRealTurnCommand(
			621,
			current.Revision,
			current.LifecycleGeneration,
			current.TurnGeneration,
			ActingSeat: 0));
		True(began.Accepted);

		const string markerId = "adventure.miss_bandage";
		RuleCommandResult marker = session.Submit(new RegisterRealTurnEndMarkerCommand(
			622,
			began.Revision,
			began.ReadModel.LifecycleGeneration,
			began.TurnGeneration,
			ActingSeat: 0,
			MarkerId: markerId));
		True(marker.Accepted);
		True(marker.ReadModel.NormalizedExtraActions.HasPendingRealTurnEndMarker(markerId));

		MatchReadModel beforeWrongSeat = marker.ReadModel;
		RuleCommandResult wrongSeat = session.Submit(new GrantExtraActionCommand(
			623,
			beforeWrongSeat.Revision,
			beforeWrongSeat.LifecycleGeneration,
			beforeWrongSeat.TurnGeneration,
			ActingSeat: 1,
			SourceId: "wrong-seat",
			ResumeNormalTurnAfterChain: false,
			ExtraActionReentryPolicy.DisallowSameSource,
			OncePerRealTurn: false));
		False(wrongSeat.Accepted);
		Equal(RuleRejectionCode.WrongTurn, wrongSeat.Rejection.Code);
		AssertReadModelsEquivalent(beforeWrongSeat, wrongSeat.ReadModel);

		RuleCommandResult grantedRoot = session.Submit(new GrantExtraActionCommand(
			624,
			beforeWrongSeat.Revision,
			beforeWrongSeat.LifecycleGeneration,
			beforeWrongSeat.TurnGeneration,
			ActingSeat: 0,
			SourceId: "root",
			ResumeNormalTurnAfterChain: true,
			ExtraActionReentryPolicy.DisallowSameSource,
			OncePerRealTurn: false));
		True(grantedRoot.Accepted);
		Equal(ExtraActionGrantStatus.Granted, grantedRoot.ExtraActionOutcome!.GrantStatus);
		ExtraActionGrantState rootGrant = grantedRoot.ExtraActionOutcome.Grant!.Value;

		ExtraActionQueueSnapshot beforeMismatch = grantedRoot.ReadModel.NormalizedExtraActions;
		RuleCommandResult mismatched = session.Submit(new AcceptExtraActionCommand(
			625,
			grantedRoot.Revision,
			grantedRoot.ReadModel.LifecycleGeneration,
			grantedRoot.TurnGeneration,
			ActingSeat: 0,
			ExpectedGrant: rootGrant with { SourceId = "other" }));
		True(mismatched.Accepted);
		Equal(ExtraActionAcceptStatus.QueueMismatch, mismatched.ExtraActionOutcome!.AcceptStatus);
		AssertExtraActionsEquivalent(beforeMismatch, mismatched.ReadModel.NormalizedExtraActions);

		RuleCommandResult acceptedRoot = session.Submit(new AcceptExtraActionCommand(
			626,
			mismatched.Revision,
			mismatched.ReadModel.LifecycleGeneration,
			mismatched.TurnGeneration,
			ActingSeat: 0,
			ExpectedGrant: rootGrant));
		True(acceptedRoot.Accepted);
		Equal(ExtraActionAcceptStatus.Accepted, acceptedRoot.ExtraActionOutcome!.AcceptStatus);
		Equal(rootGrant, acceptedRoot.ReadModel.NormalizedExtraActions.CurrentGrant!.Value);

		RuleCommandResult grantedDerived = session.Submit(new GrantExtraActionCommand(
			627,
			acceptedRoot.Revision,
			acceptedRoot.ReadModel.LifecycleGeneration,
			acceptedRoot.TurnGeneration,
			ActingSeat: 0,
			SourceId: "derived",
			ResumeNormalTurnAfterChain: false,
			ExtraActionReentryPolicy.DisallowSameSource,
			OncePerRealTurn: false));
		True(grantedDerived.Accepted);
		ExtraActionGrantState derivedGrant = grantedDerived.ExtraActionOutcome!.Grant!.Value;
		True(derivedGrant.ResumeNormalTurnAfterChain);

		MatchStateSnapshot snapshot = session.CaptureSnapshot();
		AuthorityRuleSession restored = AuthorityRuleSession.Restore(
			snapshot,
			new TestRuleRandomSource(1009),
			profile);
		AssertExtraActionsEquivalent(
			grantedDerived.ReadModel.NormalizedExtraActions,
			restored.ReadModel.NormalizedExtraActions);
		var replica = new ReplicaRuleSession();
		True(replica.ApplyAuthoritySnapshot(snapshot).Applied);
		AssertExtraActionsEquivalent(
			grantedDerived.ReadModel.NormalizedExtraActions,
			replica.ReadModel.NormalizedExtraActions);

		RuleCommandResult finishedRoot = restored.Submit(new FinishExtraActionExecutionCommand(
			628,
			restored.ReadModel.Revision,
			restored.ReadModel.LifecycleGeneration,
			restored.ReadModel.TurnGeneration,
			ActingSeat: 0,
			AcceptedGrant: rootGrant));
		True(finishedRoot.Accepted);
		False(finishedRoot.ReadModel.NormalizedExtraActions.CurrentGrant.HasValue);

		RuleCommandResult completedRoot = restored.Submit(new CompleteExtraActionCommand(
			629,
			finishedRoot.Revision,
			finishedRoot.ReadModel.LifecycleGeneration,
			finishedRoot.TurnGeneration,
			ActingSeat: 0,
			ConsumedGrant: rootGrant));
		True(completedRoot.Accepted);
		Equal(ExtraActionCompletionKind.ContinueChain, completedRoot.ExtraActionOutcome!.CompletionKind);

		RuleCommandResult acceptedDerived = restored.Submit(new AcceptExtraActionCommand(
			630,
			completedRoot.Revision,
			completedRoot.ReadModel.LifecycleGeneration,
			completedRoot.TurnGeneration,
			ActingSeat: 0,
			ExpectedGrant: derivedGrant));
		True(acceptedDerived.Accepted);
		RuleCommandResult finishedDerived = restored.Submit(new FinishExtraActionExecutionCommand(
			631,
			acceptedDerived.Revision,
			acceptedDerived.ReadModel.LifecycleGeneration,
			acceptedDerived.TurnGeneration,
			ActingSeat: 0,
			AcceptedGrant: derivedGrant));
		True(finishedDerived.Accepted);
		RuleCommandResult completedDerived = restored.Submit(new CompleteExtraActionCommand(
			632,
			finishedDerived.Revision,
			finishedDerived.ReadModel.LifecycleGeneration,
			finishedDerived.TurnGeneration,
			ActingSeat: 0,
			ConsumedGrant: derivedGrant));
		True(completedDerived.Accepted);
		Equal(ExtraActionCompletionKind.ResumeNormalTurn, completedDerived.ExtraActionOutcome!.CompletionKind);
		Equal(0L, completedDerived.ReadModel.NormalizedExtraActions.ActiveRootChainId);

		RuleCommandResult ended = restored.Submit(new EndRealTurnCommand(
			633,
			completedDerived.Revision,
			completedDerived.ReadModel.LifecycleGeneration,
			completedDerived.TurnGeneration,
			ActingSeat: 0));
		True(ended.Accepted);
		Equal(markerId, ended.ExtraActionOutcome!.NormalizedRealTurnEndMarkerIds.Single());
		Equal(0, ended.ReadModel.NormalizedExtraActions.ExtraActionsExecutedThisRealTurn);

		RuleCommandResult abortRoot = restored.Submit(new GrantExtraActionCommand(
			634,
			ended.Revision,
			ended.ReadModel.LifecycleGeneration,
			ended.TurnGeneration,
			ActingSeat: 0,
			SourceId: "abort-root",
			ResumeNormalTurnAfterChain: true,
			ExtraActionReentryPolicy.DisallowSameSource,
			OncePerRealTurn: false));
		True(abortRoot.Accepted);
		RuleCommandResult abortDerived = restored.Submit(new GrantExtraActionCommand(
			635,
			abortRoot.Revision,
			abortRoot.ReadModel.LifecycleGeneration,
			abortRoot.TurnGeneration,
			ActingSeat: 0,
			SourceId: "abort-derived",
			ResumeNormalTurnAfterChain: false,
			ExtraActionReentryPolicy.DisallowSameSource,
			OncePerRealTurn: false));
		True(abortDerived.Accepted);
		RuleCommandResult aborted = restored.Submit(new AbortExtraActionChainCommand(
			636,
			abortDerived.Revision,
			abortDerived.ReadModel.LifecycleGeneration,
			abortDerived.TurnGeneration,
			ActingSeat: 0));
		True(aborted.Accepted);
		True(aborted.ExtraActionOutcome!.HadActiveRootChain);
		True(aborted.ExtraActionOutcome.ResumeNormalTurn);
		Equal(2, aborted.ExtraActionOutcome.ClearedGrantCount);
		Equal(0L, aborted.ReadModel.NormalizedExtraActions.ActiveRootChainId);
	}

}
