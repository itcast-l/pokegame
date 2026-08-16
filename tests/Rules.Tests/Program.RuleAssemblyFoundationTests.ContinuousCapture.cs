using System.Collections.Immutable;
using Deckland.Rules;

internal static partial class Program
{
	private static void ContinuousCaptureRulesEnforceStrictGrowthAndBigTideReset()
	{
		int threshold = 0;
		for (int tableCaptureCount = 1; tableCaptureCount <= 4; tableCaptureCount++)
		{
			True(ContinuousCaptureRules.MeetsThreshold(tableCaptureCount, threshold));
			ContinuousCaptureResult result = ContinuousCaptureRules.RecordCapture(tableCaptureCount);
			False(result.BigTideTriggered);
			Equal(tableCaptureCount, result.NextBasePlayCaptureThreshold);
			threshold = result.NextBasePlayCaptureThreshold;
			False(ContinuousCaptureRules.MeetsThreshold(tableCaptureCount, threshold));
		}

		True(ContinuousCaptureRules.MeetsThreshold(5, threshold));
		ContinuousCaptureResult bigTide = ContinuousCaptureRules.RecordCapture(5);
		True(bigTide.BigTideTriggered);
		Equal(0, bigTide.NextBasePlayCaptureThreshold);
		Equal(1, bigTide.NextMinimumTableCaptureCount);
		True(ContinuousCaptureRules.MeetsThreshold(1, bigTide.NextBasePlayCaptureThreshold));

		ContinuousCaptureResult directBigTide = ContinuousCaptureRules.RecordCapture(8);
		True(directBigTide.BigTideTriggered);
		Equal(0, directBigTide.NextBasePlayCaptureThreshold);
	}

	private static void CardPlayResolverBlocksThresholdBeforeExtraCaptureEffects()
	{
		var oneNormalCard = new[]
		{
			new CardPlayTableCard("A", false, false, "clubs"),
		};
		CardPlayPlan blocked = CardPlayResolver.Resolve(
			oneNormalCard,
			new CardPlayCard("9", false),
			new[] { "A" },
			insertAtTableStart: false,
			basePlayCaptureThreshold: 1);
		True(blocked.CaptureThresholdBlocked);
		Equal(1, blocked.NormalTableCaptureCount);
		Equal(-1, blocked.MatchingTableCardIndex);
		Equal(-1, blocked.CaptureStartIndex);
		Equal("9", blocked.EffectiveRank);
		False(blocked.MatchedTableCardFlipped);

		var mixedCards = new[]
		{
			new CardPlayTableCard("A", true, false),
			new CardPlayTableCard("2", true, true),
			new CardPlayTableCard(null, true, false),
			new CardPlayTableCard("3", true, false, CountsTowardBasePlayCaptureThreshold: false),
		};
		CardPlayPlan allowed = CardPlayResolver.Resolve(
			mixedCards,
			new CardPlayCard("A", false),
			basePlayCaptureThreshold: 0,
			alternateRanks: null,
			insertAtTableStart: false);
		False(allowed.CaptureThresholdBlocked);
		Equal(1, allowed.NormalTableCaptureCount);

		var suitCards = new[]
		{
			new CardPlayTableCard("2", true, false, "hearts"),
			new CardPlayTableCard("K", true, false, "clubs"),
		};
		CardPlayPlan suitAllowed = CardPlayResolver.Resolve(
			suitCards,
			new CardPlayCard("9", false, "hearts", CollectBySuit: true),
			alternateRanks: null,
			insertAtTableStart: false,
			basePlayCaptureThreshold: 1);
		False(suitAllowed.CaptureThresholdBlocked);
		Equal(0, suitAllowed.MatchingTableCardIndex);
		Equal(2, suitAllowed.NormalTableCaptureCount);
		CardPlayPlan suitBlocked = CardPlayResolver.Resolve(
			suitCards,
			new CardPlayCard("9", false, "hearts", CollectBySuit: true),
			alternateRanks: null,
			insertAtTableStart: false,
			basePlayCaptureThreshold: 2);
		True(suitBlocked.CaptureThresholdBlocked);
		Equal(-1, suitBlocked.MatchingTableCardIndex);

		CardPlayPlan collectAllBlocked = CardPlayResolver.Resolve(
			suitCards,
			new CardPlayCard("J", CollectsTable: true),
			alternateRanks: null,
			insertAtTableStart: false,
			basePlayCaptureThreshold: 2);
		True(collectAllBlocked.CaptureThresholdBlocked);
		False(collectAllBlocked.PlayedCardCollectsTable);
		Equal(2, collectAllBlocked.NormalTableCaptureCount);

		PotentialCaptureCard thresholdedAiCard = new(
			"A",
			CollectsTable: false,
			HasDoubleEffect: false,
			BasePlayCaptureThreshold: 2);
		var aiTable = new[]
		{
			new CaptureScoringTableCard(
				new CaptureScoringCard("A", 1, false),
				false,
				false,
				false,
				false,
				-1),
			new CaptureScoringTableCard(
				new CaptureScoringCard("2", 3, false),
				false,
				false,
				false,
				false,
				-1),
		};
		Equal(0, MatchReadModel.GetPotentialNormalCaptureCount(aiTable, thresholdedAiCard));
		Equal(0, MatchReadModel.GetPotentialNormalCapturePoints(
			aiTable,
			thresholdedAiCard,
			DefaultScoringContext));
		False(PotentialCaptureRules.TryPickBestCaptureCardIndex(
			aiTable,
			new[] { thresholdedAiCard },
			DefaultScoringContext,
			royalCaptureThreshold: int.MaxValue,
			out int blockedAiHandIndex));
		Equal(-1, blockedAiHandIndex);
	}

	private static void AuthorityContinuousCaptureTransactionRecordsFinalTableCount()
	{
		AuthorityRuleSession session = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[] { ("A", "clubs") },
			extraCardsBeforeMatch: new[] { ("7", "hearts") });
		RuleCommandResult play = SubmitContinuousCapturePlay(session, actionId: 2);
		True(play.Accepted);
		Equal(1, play.Mutation.CaptureStartIndex);

		RuleCommandResult capture = session.Submit(new CaptureCardsCommand(
			3,
			play.Revision,
			play.ReadModel.LifecycleGeneration,
			play.TurnGeneration,
			0,
			play.Mutation.CaptureStartIndex,
			ExtraTableCardIndexes: ImmutableArray.Create(0),
			PreservedTableCardIndexes: ImmutableArray.Create(play.Mutation.TargetIndex),
			BasePlayCardRuntimeInstanceId: play.Mutation.LogicalEntityId));
		True(capture.Accepted);
		Equal(2, capture.Mutation.CollectedCardCount);
		Equal(2, capture.Mutation.FinalTableCaptureCount);
		Equal(3, capture.Mutation.NextBasePlayMinimumTableCaptureCount);
		False(capture.Mutation.BigTideTriggered);
		Equal(2, capture.ReadModel.BasePlayCaptureThreshold);
		Equal(1, capture.ReadModel.TableCards.Length);

		AuthorityRuleSession insertedBeforeCaptureSession = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[] { ("A", "clubs") },
			extraCardsBeforeMatch: new[] { ("7", "hearts") });
		MatchReadModel beforeInsertedPlay = insertedBeforeCaptureSession.ReadModel;
		RuleCommandResult insertedPlay = insertedBeforeCaptureSession.Submit(
			new PlayCardFromHandCommand(
				2,
				beforeInsertedPlay.Revision,
				beforeInsertedPlay.LifecycleGeneration,
				beforeInsertedPlay.TurnGeneration,
				0,
				0,
				"A",
				TableInsertIndexOverride: 0));
		True(insertedPlay.Accepted);
		Equal(0, insertedPlay.Mutation.TargetIndex);
		Equal(1, insertedPlay.Mutation.CaptureStartIndex);
		RuleCommandResult insertedCapture = insertedBeforeCaptureSession.Submit(
			new CaptureCardsCommand(
				3,
				insertedPlay.Revision,
				insertedPlay.ReadModel.LifecycleGeneration,
				insertedPlay.TurnGeneration,
				0,
				insertedPlay.Mutation.CaptureStartIndex,
				BasePlayCardRuntimeInstanceId: insertedPlay.Mutation.LogicalEntityId));
		True(insertedCapture.Accepted);
		Equal(2, insertedCapture.Mutation.FinalTableCaptureCount);
		Equal(1, insertedCapture.ReadModel.TableCards.Length);
		Equal(insertedPlay.Mutation.LogicalEntityId,
			insertedCapture.ReadModel.TableCards[0].Card?.RuntimeInstanceId);

		AuthorityRuleSession blockedSession = CreateContinuousCaptureSession(
			threshold: 1,
			baseCaptureCards: new[] { ("A", "clubs") });
		RuleCommandResult blockedPlay = SubmitContinuousCapturePlay(blockedSession, actionId: 2);
		True(blockedPlay.Accepted);
		Equal(-1, blockedPlay.Mutation.CaptureStartIndex);
		False(blockedPlay.Mutation.Captured);
		Equal(1, blockedPlay.ReadModel.BasePlayCaptureThreshold);

		AuthorityRuleSession extraCannotRescueSession = CreateContinuousCaptureSession(
			threshold: 1,
			baseCaptureCards: new[] { ("A", "clubs") },
			extraCardsBeforeMatch: new[] { ("7", "hearts") });
		RuleCommandResult extraCannotRescuePlay = SubmitContinuousCapturePlay(
			extraCannotRescueSession,
			actionId: 2);
		True(extraCannotRescuePlay.Accepted);
		Equal(-1, extraCannotRescuePlay.Mutation.CaptureStartIndex);
		False(extraCannotRescuePlay.Mutation.Captured);
		Equal(1, extraCannotRescuePlay.ReadModel.BasePlayCaptureThreshold);

		AuthorityRuleSession bigTideSession = CreateContinuousCaptureSession(
			threshold: 1,
			baseCaptureCards: new[]
			{
				("A", "clubs"),
				("2", "hearts"),
				("3", "spades"),
				("4", "diamonds"),
				("5", "clubs"),
			},
			currentTurnSeat: 1);
		RuleCommandResult bigTidePlay = SubmitContinuousCapturePlay(
			bigTideSession,
			actionId: 2,
			seat: 1);
		True(bigTidePlay.Accepted);
		RuleCommandResult bigTideCapture = bigTideSession.Submit(new CaptureCardsCommand(
			3,
			bigTidePlay.Revision,
			bigTidePlay.ReadModel.LifecycleGeneration,
			bigTidePlay.TurnGeneration,
			1,
			bigTidePlay.Mutation.CaptureStartIndex,
			BasePlayCardRuntimeInstanceId: bigTidePlay.Mutation.LogicalEntityId));
		True(bigTideCapture.Accepted);
		Equal(5, bigTideCapture.Mutation.FinalTableCaptureCount);
		True(bigTideCapture.Mutation.BigTideTriggered);
		Equal(1, bigTideCapture.Mutation.NextBasePlayMinimumTableCaptureCount);
		Equal(0, bigTideCapture.ReadModel.BasePlayCaptureThreshold);
	}

	private static void AuthorityContinuousCaptureThresholdIsSharedAcrossSeats()
	{
		AuthorityRuleSession source = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[]
			{
				("A", "clubs"),
				("2", "hearts"),
			},
			handCardCountPerSeat: 3);
		RuleCommandResult sourcePlay = SubmitContinuousCapturePlay(source, actionId: 2);
		True(sourcePlay.Accepted);
		RuleCommandResult sourceCapture = source.Submit(new CaptureCardsCommand(
			3,
			sourcePlay.Revision,
			sourcePlay.ReadModel.LifecycleGeneration,
			sourcePlay.TurnGeneration,
			0,
			sourcePlay.Mutation.CaptureStartIndex,
			BasePlayCardRuntimeInstanceId: sourcePlay.Mutation.LogicalEntityId));
		True(sourceCapture.Accepted);
		Equal(2, sourceCapture.ReadModel.BasePlayCaptureThreshold);

		MatchStateSnapshot sharedSnapshot = source.CaptureSnapshot();
		int branchOrdinal = 0;
		foreach (int seat in new[] { 0, 1 })
		{
			foreach (int captureCount in new[] { 1, 2 })
			{
				AuthorityRuleSession blocked = RestoreContinuousCaptureScenario(
					sharedSnapshot,
					seat,
					captureCount,
					900 + branchOrdinal++);
				RuleCommandResult blockedPlay = SubmitContinuousCapturePlay(
					blocked,
					actionId: 100 + branchOrdinal,
					seat: seat);
				True(blockedPlay.Accepted);
				False(blockedPlay.Mutation.Captured);
				Equal(-1, blockedPlay.Mutation.CaptureStartIndex);
				Equal(2, blockedPlay.ReadModel.BasePlayCaptureThreshold);
			}
		}

		AuthorityRuleSession seatOneSuccess = RestoreContinuousCaptureScenario(
			sharedSnapshot,
			seat: 1,
			captureCount: 3,
			randomSeed: 950);
		RuleCommandResult seatOnePlay = SubmitContinuousCapturePlay(
			seatOneSuccess,
			actionId: 200,
			seat: 1);
		True(seatOnePlay.Accepted);
		RuleCommandResult seatOneCapture = seatOneSuccess.Submit(new CaptureCardsCommand(
			201,
			seatOnePlay.Revision,
			seatOnePlay.ReadModel.LifecycleGeneration,
			seatOnePlay.TurnGeneration,
			1,
			seatOnePlay.Mutation.CaptureStartIndex,
			BasePlayCardRuntimeInstanceId: seatOnePlay.Mutation.LogicalEntityId));
		True(seatOneCapture.Accepted);
		Equal(3, seatOneCapture.Mutation.FinalTableCaptureCount);
		Equal(3, seatOneCapture.ReadModel.BasePlayCaptureThreshold);
	}

	private static void AuthorityContinuousCaptureSequenceCrossesSeatsAndResetsGlobally()
	{
		AuthorityRuleSession session = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: Array.Empty<(string Rank, string Suit)>(),
			handCardCountPerSeat: 3);
		int[] captureCounts = { 1, 2, 3, 4, 5, 1 };
		int[] expectedThresholds = { 1, 2, 3, 4, 0, 1 };
		long actionId = 2;
		for (int step = 0; step < captureCounts.Length; step++)
		{
			int seat = step % 2;
			PlaceContinuousCaptureTableCards(
				session,
				seat,
				captureCounts[step],
				step,
				ref actionId);
			RuleCommandResult play = SubmitContinuousCapturePlay(
				session,
				actionId++,
				seat: seat);
			RequireContinuousCaptureAccepted(play, $"sequence play {step}");
			Equal(0, play.Mutation.CaptureStartIndex);
			RuleCommandResult capture = session.Submit(new CaptureCardsCommand(
				actionId++,
				play.Revision,
				play.ReadModel.LifecycleGeneration,
				play.TurnGeneration,
				seat,
				play.Mutation.CaptureStartIndex,
				BasePlayCardRuntimeInstanceId: play.Mutation.LogicalEntityId));
			RequireContinuousCaptureAccepted(capture, $"sequence capture {step}");
			Equal(captureCounts[step], capture.Mutation.FinalTableCaptureCount);
			Equal(expectedThresholds[step], capture.ReadModel.BasePlayCaptureThreshold);
			Equal(captureCounts[step] >= ContinuousCaptureRules.BigTideTableCaptureCount,
				capture.Mutation.BigTideTriggered);

			if (step == captureCounts.Length - 1)
			{
				continue;
			}
			RuleCommandResult advanced = session.Submit(new AdvanceTurnCommand(
				actionId++,
				capture.Revision,
				capture.ReadModel.LifecycleGeneration,
				capture.TurnGeneration,
				ActingSeat: seat,
				Captured: false,
				CandidateNextSeat: 1 - seat));
			RequireContinuousCaptureAccepted(advanced, $"sequence advance {step}");
			Equal(expectedThresholds[step], advanced.ReadModel.BasePlayCaptureThreshold);
		}
	}

	private static void AuthorityContinuousCaptureBindsCurrentBasePlayIdentity()
	{
		AuthorityRuleSession source = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[] { ("A", "clubs") },
			extraCardsBeforeMatch: new[] { ("7", "hearts") });
		MatchStateSnapshot sourceSnapshot = source.CaptureSnapshot();
		long staleOwnedCardId = sourceSnapshot.TableCards[0].Card!.Value.RuntimeInstanceId;
		MatchStateSnapshot ownedSnapshot = sourceSnapshot with
		{
			TableCards = sourceSnapshot.TableCards.SetItem(
				0,
				sourceSnapshot.TableCards[0] with { CardOwnerSeat = 0 }),
		};
		AuthorityRuleSession session = AuthorityRuleSession.Restore(
			ownedSnapshot,
			new TestRuleRandomSource(809),
			MatchRuleProfile.Empty);

		RuleCommandResult play = SubmitContinuousCapturePlay(session, actionId: 2);
		True(play.Accepted);
		PendingBasePlayCaptureState pending =
			session.CaptureSnapshot().NormalizedPendingBasePlayCapture;
		True(pending.IsPending);
		Equal(0, pending.Seat);
		Equal(play.Mutation.LogicalEntityId, pending.CardRuntimeInstanceId);
		Equal(2L, pending.PlayActionId);
		Equal(play.TurnGeneration, pending.TurnGeneration);

		MatchStateSnapshot beforeWrongId = session.CaptureSnapshot();
		RuleCommandResult wrongId = session.Submit(new CaptureCardsCommand(
			3,
			play.Revision,
			play.ReadModel.LifecycleGeneration,
			play.TurnGeneration,
			0,
			play.Mutation.CaptureStartIndex,
			BasePlayCardRuntimeInstanceId: staleOwnedCardId));
		False(wrongId.Accepted);
		Equal(RuleRejectionCode.NoLegalAction, wrongId.Rejection.Code);
		Equal(beforeWrongId.Revision, wrongId.Revision);
		Equal(0, wrongId.ReadModel.BasePlayCaptureThreshold);
		MatchStateSnapshot afterWrongId = session.CaptureSnapshot();
		Equal(beforeWrongId.Revision, afterWrongId.Revision);
		Equal(beforeWrongId.NormalizedPendingBasePlayCapture,
			afterWrongId.NormalizedPendingBasePlayCapture);
		True(beforeWrongId.AppliedActionIds.SequenceEqual(afterWrongId.AppliedActionIds));
		True(beforeWrongId.TableCards
			.Select(card => (
				card.RuntimeInstanceId,
				card.Card?.RuntimeInstanceId,
				card.CardOwnerSeat))
			.SequenceEqual(afterWrongId.TableCards.Select(card => (
				card.RuntimeInstanceId,
				card.Card?.RuntimeInstanceId,
				card.CardOwnerSeat))));

		RuleCommandResult blockedPlay = session.Submit(new PlayCardFromHandCommand(
			4,
			play.Revision,
			play.ReadModel.LifecycleGeneration,
			play.TurnGeneration,
			0,
			0,
			"A"));
		False(blockedPlay.Accepted);
		Equal(RuleRejectionCode.NoLegalAction, blockedPlay.Rejection.Code);
		Equal(play.Revision, blockedPlay.Revision);

		RuleCommandResult retry = session.Submit(new CaptureCardsCommand(
			5,
			play.Revision,
			play.ReadModel.LifecycleGeneration,
			play.TurnGeneration,
			0,
			play.Mutation.CaptureStartIndex,
			BasePlayCardRuntimeInstanceId: play.Mutation.LogicalEntityId));
		True(retry.Accepted);
		Equal(1, retry.Mutation.FinalTableCaptureCount);
		Equal(1, retry.ReadModel.BasePlayCaptureThreshold);
		Equal(1, retry.ReadModel.TableCards.Length);
		Equal(staleOwnedCardId, retry.ReadModel.TableCards[0].Card?.RuntimeInstanceId);
		False(session.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);
	}

	private static void ContinuousCaptureThresholdSurvivesSnapshots()
	{
		AuthorityRuleSession session = CreateContinuousCaptureSession(
			threshold: 2,
			baseCaptureCards: new[]
			{
				("A", "clubs"),
				("2", "hearts"),
				("3", "spades"),
			});
		MatchStateSnapshot snapshot = session.CaptureSnapshot();
		var restoredRandom = new TestRuleRandomSource(999);
		AuthorityRuleSession restored = AuthorityRuleSession.Restore(
			snapshot,
			restoredRandom,
			MatchRuleProfile.Empty);
		Equal(2, restored.ReadModel.BasePlayCaptureThreshold);

		var replica = new ReplicaRuleSession();
		SnapshotApplyResult applied = replica.ApplyAuthoritySnapshot(snapshot);
		True(applied.Applied);
		Equal(2, replica.ReadModel.BasePlayCaptureThreshold);

		MatchStateSnapshot invalid = snapshot with
		{
			Revision = snapshot.Revision + 1,
			BasePlayCaptureThreshold = 5,
		};
		bool rejected = false;
		try
		{
			_ = AuthorityRuleSession.Restore(
				invalid,
				new TestRuleRandomSource(1000),
				MatchRuleProfile.Empty);
		}
		catch (ArgumentException)
		{
			rejected = true;
		}
		True(rejected);
		SnapshotApplyResult invalidReplica = replica.ApplyAuthoritySnapshot(
			snapshot with
			{
				Revision = snapshot.Revision + 1,
				BasePlayCaptureThreshold = -1,
			});
		False(invalidReplica.Applied);
		Equal(RuleRejectionCode.InvalidConfiguration, invalidReplica.Rejection.Code);
	}

	private static void PendingBasePlayCaptureSurvivesSnapshotsAndRejectsTampering()
	{
		AuthorityRuleSession session = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[] { ("A", "clubs") });
		RuleCommandResult play = SubmitContinuousCapturePlay(session, actionId: 2);
		True(play.Accepted);
		MatchStateSnapshot snapshot = session.CaptureSnapshot();
		PendingBasePlayCaptureState pending = snapshot.NormalizedPendingBasePlayCapture;
		True(pending.IsPending);

		AuthorityRuleSession restored = AuthorityRuleSession.Restore(
			snapshot,
			new TestRuleRandomSource(810),
			MatchRuleProfile.Empty);
		RuleCommandResult capture = restored.Submit(new CaptureCardsCommand(
			3,
			restored.ReadModel.Revision,
			restored.ReadModel.LifecycleGeneration,
			restored.ReadModel.TurnGeneration,
			0,
			play.Mutation.CaptureStartIndex,
			BasePlayCardRuntimeInstanceId: pending.CardRuntimeInstanceId));
		True(capture.Accepted);
		False(restored.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);

		True(ThrowsArgumentException(() => AuthorityRuleSession.Restore(
			snapshot with
			{
				PendingBasePlayCapture = pending with { CardRuntimeInstanceId = long.MaxValue },
			},
			new TestRuleRandomSource(811),
			MatchRuleProfile.Empty)));
		True(ThrowsArgumentException(() => AuthorityRuleSession.Restore(
			snapshot with
			{
				PendingBasePlayCapture = pending with { Seat = 1 },
			},
			new TestRuleRandomSource(812),
			MatchRuleProfile.Empty)));
		True(ThrowsArgumentException(() => AuthorityRuleSession.Restore(
			snapshot with
			{
				PendingBasePlayCapture = pending with { PlayActionId = long.MaxValue },
			},
			new TestRuleRandomSource(813),
			MatchRuleProfile.Empty)));
		True(ThrowsArgumentException(() => AuthorityRuleSession.Restore(
			snapshot with
			{
				PendingBasePlayCapture = pending with
				{
					TurnGeneration = pending.TurnGeneration + 1,
				},
			},
			new TestRuleRandomSource(814),
			MatchRuleProfile.Empty)));

		int pendingTableIndex = -1;
		for (int index = 0; index < snapshot.TableCards.Length; index++)
		{
			if (snapshot.TableCards[index].Card?.RuntimeInstanceId == pending.CardRuntimeInstanceId)
			{
				pendingTableIndex = index;
				break;
			}
		}
		True(pendingTableIndex >= 0);
		True(ThrowsArgumentException(() => AuthorityRuleSession.Restore(
			snapshot with
			{
				TableCards = snapshot.TableCards.SetItem(
					pendingTableIndex,
					snapshot.TableCards[pendingTableIndex] with { CardOwnerSeat = 1 }),
			},
			new TestRuleRandomSource(815),
			MatchRuleProfile.Empty)));
	}

	private static void PendingBasePlayCaptureClearsAtActionBoundaries()
	{
		AuthorityRuleSession turnSession = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[] { ("A", "clubs") });
		RuleCommandResult turnPlay = SubmitContinuousCapturePlay(turnSession, actionId: 2);
		True(turnSession.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);
		RuleCommandResult advancedTurn = turnSession.Submit(new AdvanceTurnCommand(
			3,
			turnPlay.Revision,
			turnPlay.ReadModel.LifecycleGeneration,
			turnPlay.TurnGeneration,
			ActingSeat: 0,
			Captured: false,
			CandidateNextSeat: 1));
		True(advancedTurn.Accepted);
		False(turnSession.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);

		AuthorityRuleSession generationSession = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[] { ("A", "clubs") });
		RuleCommandResult generationPlay = SubmitContinuousCapturePlay(generationSession, actionId: 2);
		True(generationSession.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);
		RuleCommandResult advancedGeneration = generationSession.Submit(
			new AdvanceTurnGenerationCommand(
				3,
				generationPlay.Revision,
				generationPlay.ReadModel.LifecycleGeneration,
				generationPlay.TurnGeneration));
		True(advancedGeneration.Accepted);
		False(generationSession.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);

		AuthorityRuleSession restartSession = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[] { ("A", "clubs") });
		RuleCommandResult restartPlay = SubmitContinuousCapturePlay(restartSession, actionId: 2);
		True(restartSession.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);
		InitializeMatchCommand restartTemplate = CreateInitializeCommand(816);
		RuleCommandResult restarted = restartSession.Submit(new RestartMatchCommand(
			3,
			restartPlay.Revision,
			LifecycleGeneration: 2,
			TurnGeneration: restartPlay.TurnGeneration,
			PlayerCount: 2,
			WinningScore: 100,
			StartingHandSize: 3,
			DealEqualRanks: true,
			Deck: restartTemplate.Deck));
		True(restarted.Accepted);
		False(restartSession.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);

		AuthorityRuleSession endSession = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[] { ("A", "clubs") });
		RuleCommandResult endPlay = SubmitContinuousCapturePlay(endSession, actionId: 2);
		True(endSession.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);
		RuleCommandResult ended = endSession.Submit(new EndMatchSessionCommand(
			3,
			endPlay.Revision,
			endPlay.ReadModel.LifecycleGeneration,
			endPlay.TurnGeneration));
		True(ended.Accepted);
		False(endSession.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);
	}

	private static void ContinuousCaptureThresholdPersistsAcrossMissesAndTurnsThenResets()
	{
		var normalSession = new AuthorityRuleSession(
			new TestRuleRandomSource(699),
			MatchRuleProfile.Empty);
		RuleCommandResult normalInitialization = normalSession.Submit(
			CreateInitializeCommand(699));
		RequireContinuousCaptureAccepted(normalInitialization, "normal initialization");
		Equal(0, normalInitialization.ReadModel.BasePlayCaptureThreshold);

		AuthorityRuleSession session = CreateContinuousCaptureSession(
			threshold: 3,
			baseCaptureCards: new[] { ("K", "clubs") },
			profile: new MatchRuleProfile(new IRuleModule[]
			{
				new TestRuleModule(
					"continuous-capture-extra-actions",
					0,
					capabilities: new MatchCapabilitySet(
						MatchCapability.UpgradeSelection)),
			}));
		RuleCommandResult miss = SubmitContinuousCapturePlay(session, actionId: 2);
		RequireContinuousCaptureAccepted(miss, "threshold persistence miss");
		False(miss.Mutation.Captured);
		Equal(3, miss.ReadModel.BasePlayCaptureThreshold);

		RuleCommandResult extraAction = session.Submit(new GrantExtraActionCommand(
			3,
			miss.Revision,
			miss.ReadModel.LifecycleGeneration,
			miss.TurnGeneration,
			ActingSeat: 0,
			SourceId: "continuous-capture-test",
			ResumeNormalTurnAfterChain: true,
			ExtraActionReentryPolicy.DisallowSameSource,
			OncePerRealTurn: false));
		RequireContinuousCaptureAccepted(extraAction, "threshold persistence extra action");
		Equal(3, extraAction.ReadModel.BasePlayCaptureThreshold);

		RuleCommandResult advanced = session.Submit(new AdvanceTurnCommand(
			4,
			extraAction.Revision,
			extraAction.ReadModel.LifecycleGeneration,
			extraAction.TurnGeneration,
			ActingSeat: 0,
			Captured: false,
			CandidateNextSeat: 1));
		RequireContinuousCaptureAccepted(advanced, "threshold persistence advance");
		Equal(1, advanced.ReadModel.CurrentTurnSeat);
		Equal(3, advanced.ReadModel.BasePlayCaptureThreshold);

		InitializeMatchCommand restartTemplate = CreateInitializeCommand(700);
		RuleCommandResult restarted = session.Submit(new RestartMatchCommand(
			5,
			advanced.Revision,
			LifecycleGeneration: 2,
			TurnGeneration: advanced.TurnGeneration,
			PlayerCount: 2,
			WinningScore: 100,
			StartingHandSize: 3,
			DealEqualRanks: true,
			Deck: restartTemplate.Deck));
		RequireContinuousCaptureAccepted(restarted, "threshold persistence restart");
		Equal(0, restarted.ReadModel.BasePlayCaptureThreshold);
	}

	private static void DirectCaptureBypassesContinuousCaptureThreshold()
	{
		AuthorityRuleSession session = CreateContinuousCaptureSession(
			threshold: 4,
			baseCaptureCards: new[]
			{
				("A", "clubs"),
				("2", "hearts"),
				("3", "spades"),
				("4", "diamonds"),
				("5", "clubs"),
			});
		MatchReadModel before = session.ReadModel;
		RuleCommandResult directCapture = session.Submit(new CaptureCardsCommand(
			2,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			0,
			0));
		True(directCapture.Accepted);
		Equal(5, directCapture.Mutation.CollectedCardCount);
		Equal(4, directCapture.ReadModel.BasePlayCaptureThreshold);
		Equal(0, directCapture.Mutation.FinalTableCaptureCount);
		False(directCapture.Mutation.BigTideTriggered);
		False(session.CaptureSnapshot().NormalizedPendingBasePlayCapture.IsPending);

		AuthorityRuleSession pendingSession = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[] { ("A", "clubs") },
			extraCardsBeforeMatch: new[] { ("7", "hearts") });
		RuleCommandResult pendingPlay = SubmitContinuousCapturePlay(
			pendingSession,
			actionId: 2,
			tableInsertIndexOverride: 0);
		True(pendingPlay.Accepted);
		PendingBasePlayCaptureState pending =
			pendingSession.CaptureSnapshot().NormalizedPendingBasePlayCapture;
		True(pending.IsPending);
		RuleCommandResult directDuringPending = pendingSession.Submit(new CaptureCardsCommand(
			3,
			pendingPlay.Revision,
			pendingPlay.ReadModel.LifecycleGeneration,
			pendingPlay.TurnGeneration,
			0,
			pendingPlay.Mutation.CaptureStartIndex));
		True(directDuringPending.Accepted);
		Equal(0, directDuringPending.ReadModel.BasePlayCaptureThreshold);
		Equal(pending,
			pendingSession.CaptureSnapshot().NormalizedPendingBasePlayCapture);

		AuthorityRuleSession pendingCardSession = CreateContinuousCaptureSession(
			threshold: 0,
			baseCaptureCards: new[] { ("A", "clubs") });
		RuleCommandResult pendingCardPlay = SubmitContinuousCapturePlay(
			pendingCardSession,
			actionId: 2);
		True(pendingCardPlay.Accepted);
		MatchStateSnapshot beforePendingCardCapture = pendingCardSession.CaptureSnapshot();
		PendingBasePlayCaptureState pendingCard =
			beforePendingCardCapture.NormalizedPendingBasePlayCapture;
		True(pendingCard.IsPending);

		RuleCommandResult rejectedPendingCardCapture = pendingCardSession.Submit(
			new CaptureCardsCommand(
				3,
				pendingCardPlay.Revision,
				pendingCardPlay.ReadModel.LifecycleGeneration,
				pendingCardPlay.TurnGeneration,
				0,
				pendingCardPlay.Mutation.CaptureStartIndex));
		False(rejectedPendingCardCapture.Accepted);
		Equal(RuleRejectionCode.NoLegalAction, rejectedPendingCardCapture.Rejection.Code);
		Equal(beforePendingCardCapture.Revision, rejectedPendingCardCapture.Revision);
		Equal(0, rejectedPendingCardCapture.ReadModel.BasePlayCaptureThreshold);
		MatchStateSnapshot afterPendingCardCapture = pendingCardSession.CaptureSnapshot();
		Equal(beforePendingCardCapture.Revision, afterPendingCardCapture.Revision);
		Equal(pendingCard, afterPendingCardCapture.NormalizedPendingBasePlayCapture);
		True(beforePendingCardCapture.AppliedActionIds.SequenceEqual(
			afterPendingCardCapture.AppliedActionIds));
		True(beforePendingCardCapture.TableCards
			.Select(card => (
				card.RuntimeInstanceId,
				card.Card?.RuntimeInstanceId,
				card.CardOwnerSeat))
			.SequenceEqual(afterPendingCardCapture.TableCards.Select(card => (
				card.RuntimeInstanceId,
				card.Card?.RuntimeInstanceId,
				card.CardOwnerSeat))));

		RuleCommandResult resolvedPendingCardCapture = pendingCardSession.Submit(
			new CaptureCardsCommand(
				4,
				pendingCardPlay.Revision,
				pendingCardPlay.ReadModel.LifecycleGeneration,
				pendingCardPlay.TurnGeneration,
				0,
				pendingCardPlay.Mutation.CaptureStartIndex,
				BasePlayCardRuntimeInstanceId: pendingCard.CardRuntimeInstanceId));
		True(resolvedPendingCardCapture.Accepted);
		Equal(1, resolvedPendingCardCapture.Mutation.FinalTableCaptureCount);
		Equal(1, resolvedPendingCardCapture.ReadModel.BasePlayCaptureThreshold);
		False(pendingCardSession.CaptureSnapshot()
			.NormalizedPendingBasePlayCapture.IsPending);
	}

	private static AuthorityRuleSession RestoreContinuousCaptureScenario(
		MatchStateSnapshot source,
		int seat,
		int captureCount,
		int randomSeed)
	{
		var tableCards = ImmutableArray.CreateBuilder<TableCardState>(captureCount);
		for (int index = 0; index < captureCount; index++)
		{
			long cardId = 10_000 + randomSeed * 10L + index;
			string rank = index == 0 ? "A" : (index + 1).ToString();
			tableCards.Add(new TableCardState(
				20_000 + randomSeed * 10L + index,
				new LogicalCardState(
					cardId,
					$"scenario:{randomSeed}:{index}",
					"clubs",
					rank),
				IsFaceUp: true,
				IsHiddenDuringCapture: false,
				IsBuried: false,
				IsScoreSuppressed: false,
				ItemOverlay: null,
				ItemOwnerSeat: -1,
				CardOwnerSeat: -1,
				RemoveOnCapture: false));
		}

		return AuthorityRuleSession.Restore(
			source with
			{
				CurrentTurnSeat = seat,
				TableCards = tableCards.MoveToImmutable(),
				PendingBasePlayCapture = PendingBasePlayCaptureState.None,
			},
			new TestRuleRandomSource((ulong)randomSeed),
			MatchRuleProfile.Empty);
	}

	private static void PlaceContinuousCaptureTableCards(
		AuthorityRuleSession session,
		int seat,
		int captureCount,
		int step,
		ref long actionId)
	{
		for (int index = 0; index < captureCount; index++)
		{
			MatchReadModel before = session.ReadModel;
			long cardId = 30_000 + step * 10L + index;
			string rank = index == 0 ? "A" : (index + 1).ToString();
			RuleCommandResult placed = session.Submit(new PlaceTableCardCommand(
				actionId++,
				before.Revision,
				before.LifecycleGeneration,
				before.TurnGeneration,
				ActingSeat: seat,
				new LogicalCardState(
					cardId,
					$"sequence:{step}:{index}",
					"clubs",
					rank),
				TableIndex: before.TableCards.Length,
				IsFaceUp: true));
			RequireContinuousCaptureAccepted(placed, $"sequence table card {step}:{index}");
		}
	}

	private static void RequireContinuousCaptureAccepted(
		RuleCommandResult result,
		string operation)
	{
		if (!result.Accepted)
		{
			throw new InvalidOperationException(
				$"{operation} rejected: {result.Rejection.Code} {result.Rejection.Detail}");
		}
	}

	private static AuthorityRuleSession CreateContinuousCaptureSession(
		int threshold,
		IReadOnlyList<(string Rank, string Suit)> baseCaptureCards,
		IReadOnlyList<(string Rank, string Suit)>? extraCardsBeforeMatch = null,
		int currentTurnSeat = 0,
		int handCardCountPerSeat = 1,
		MatchRuleProfile? profile = null)
	{
		extraCardsBeforeMatch ??= Array.Empty<(string Rank, string Suit)>();
		var players = ImmutableArray.CreateBuilder<PlayerStateSnapshot>(2);
		for (int seat = 0; seat < 2; seat++)
		{
			var hand = ImmutableArray.CreateBuilder<LogicalCardState>(handCardCountPerSeat);
			for (int cardIndex = 0; cardIndex < handCardCountPerSeat; cardIndex++)
			{
				long cardId = 1 + seat * 500 + cardIndex;
				hand.Add(new LogicalCardState(
					cardId,
					$"diamonds:A:{seat}:{cardIndex}",
					"diamonds",
					"A"));
			}
			players.Add(new PlayerStateSnapshot(
				seat,
				ImmutableArray<LogicalCardState>.Empty,
				hand.MoveToImmutable(),
				ImmutableArray<LogicalItemState>.Empty,
				0));
		}

		var tableCards = ImmutableArray.CreateBuilder<TableCardState>();
		int ordinal = 0;
		foreach ((string rank, string suit) in extraCardsBeforeMatch.Concat(baseCaptureCards))
		{
			long cardId = 100 + ordinal;
			tableCards.Add(new TableCardState(
				1000 + ordinal,
				new LogicalCardState(cardId, $"{suit}:{rank}", suit, rank),
				true,
				false,
				false,
				false,
				null,
				-1,
				-1,
				false));
			ordinal++;
		}

		var session = new AuthorityRuleSession(
			new TestRuleRandomSource(808),
			profile ?? MatchRuleProfile.Empty);
		RuleCommandResult initialized = session.Submit(new InitializeScriptedMatchCommand(
			1,
			0,
			1,
			0,
			2,
			100,
			players.MoveToImmutable(),
			tableCards.ToImmutable(),
			CurrentTurnSeat: currentTurnSeat,
			BasePlayCaptureThreshold: threshold));
		True(initialized.Accepted);
		return session;
	}

	private static RuleCommandResult SubmitContinuousCapturePlay(
		AuthorityRuleSession session,
		long actionId,
		int tableInsertIndexOverride = -1,
		int seat = 0)
	{
		MatchReadModel before = session.ReadModel;
		return session.Submit(new PlayCardFromHandCommand(
			actionId,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			seat,
			0,
			"A",
			TableInsertIndexOverride: tableInsertIndexOverride));
	}
}
