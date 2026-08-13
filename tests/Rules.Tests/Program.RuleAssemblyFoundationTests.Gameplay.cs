using System.Collections.Immutable;
using System.Reflection;
using Deckland.Rules;

internal static partial class Program
{
	private static void FixedRandomSourceProducesStableInitialDeal()
	{
		var session = new AuthorityRuleSession(
			new TestRuleRandomSource(20260802),
			MatchRuleProfile.Empty);
		RuleCommandResult result = session.Submit(CreateInitializeCommand(actionId: 41));
		True(result.Accepted);

		string deal = string.Join(
			"|",
			result.ReadModel.Players.Select(player => string.Join(",", player.Hand.Select(card => card.Code))));
		Equal("hearts:3,spades:2,hearts:1|diamonds:3,spades:3,diamonds:1", deal);
	}

	private static void LogicalCardCodeSurvivesAttachmentAndSnapshotRestore()
	{
		const string code = "special:mirror_7";
		var session = new AuthorityRuleSession(new TestRuleRandomSource(9), MatchRuleProfile.Empty);
		var deck = ImmutableArray.Create(
			new LogicalCardState(1, code, "mirror", "7"),
			new LogicalCardState(2, "hearts:1", "hearts", "1"),
			new LogicalCardState(3, "clubs:1", "clubs", "1"),
			new LogicalCardState(4, "diamonds:1", "diamonds", "1"));
		RuleCommandResult initialized = session.Submit(new InitializeMatchCommand(
			91, 1, 2, 100, 1, false, deck));
		True(initialized.Accepted);
		LogicalCardState card = initialized.ReadModel.Players
			.SelectMany(player => player.Hand.Concat(player.Deck))
			.Single(cardState => cardState.RuntimeInstanceId == 1);
		Equal(code, card.Code);

		MatchStateSnapshot snapshot = session.CaptureSnapshot();
		Equal(code, snapshot.Players.SelectMany(player => player.Hand.Concat(player.Deck))
				.Single(cardState => cardState.RuntimeInstanceId == 1).Code);
	}

	private static void RestoredRuntimeIdsAdvanceFutureAllocators()
	{
		var initialCard = new PlayingCard("seed", "1");
		long restoredCardId = checked(initialCard.InstanceId + 32);
		PlayingCard.Restore(
			restoredCardId,
			"special:restored",
			"special",
			"R",
			Array.Empty<string>());
		var nextCard = new PlayingCard("seed", "2");
		Equal(restoredCardId + 1, nextCard.InstanceId);

		var initialTableCard = new global::TableCard(initialCard, isFaceUp: true);
		long restoredTableCardId = checked(initialTableCard.InstanceId + 32);
		global::TableCard.Restore(
			restoredTableCardId,
			initialCard,
			isFaceUp: false,
			itemOverlay: null,
			itemOwnerPlayerIndex: -1,
			cardOwnerPlayerIndex: 0,
			removeCardOnCapture: false);
		var nextTableCard = new global::TableCard(nextCard, isFaceUp: false);
		Equal(restoredTableCardId + 1, nextTableCard.InstanceId);
	}

	private static void RestartCommandAdvancesLifecycleWithoutAcceptingStaleInitialization()
	{
		var session = new AuthorityRuleSession(new TestRuleRandomSource(19), MatchRuleProfile.Empty);
		InitializeMatchCommand initialize = CreateInitializeCommand(101) with
		{
			CreateOpeningPresentationBarrier = true,
		};
		RuleCommandResult initialized = session.Submit(initialize);
		True(initialized.Accepted);
		RuleCommandResult staleInitialize = session.Submit(CreateInitializeCommand(102));
		False(staleInitialize.Accepted);
		Equal(RuleRejectionCode.SessionAlreadyInitialized, staleInitialize.Rejection.Code);

		RuleCommandResult restarted = session.Submit(new RestartMatchCommand(
			102,
			ExpectedRevision: session.ReadModel.Revision,
			LifecycleGeneration: 2,
			TurnGeneration: session.ReadModel.TurnGeneration,
			PlayerCount: 2,
			WinningScore: 100,
			StartingHandSize: 3,
			DealEqualRanks: true,
			Deck: initialize.Deck));
		True(restarted.Accepted);
		Equal(2, restarted.ReadModel.LifecycleGeneration);
		Equal(2, restarted.ReadModel.TurnGeneration);
		Equal(1L, restarted.ReadModel.Revision);
		Equal(MatchFlowState.NewMatch, restarted.ReadModel.NormalizedMatchFlow);

		RuleCommandResult staleBarrierCompletion = session.Submit(
			new CompletePresentationBarrierCommand(
				103,
				initialized.Revision,
				initialized.ReadModel.LifecycleGeneration,
				initialized.ReadModel.TurnGeneration,
				initialized.ReadModel.PendingPresentationBarrierId));
		False(staleBarrierCompletion.Accepted);
		Equal(RuleRejectionCode.StaleLifecycleGeneration, staleBarrierCompletion.Rejection.Code);
	}

	private static void MatchInitializationPreservesConfiguredStartingScores()
	{
		var session = new AuthorityRuleSession(new TestRuleRandomSource(23), MatchRuleProfile.Empty);
		InitializeMatchCommand command = CreateInitializeCommand(103) with
		{
			StartingScores = ImmutableArray.Create(75, 40),
		};

		RuleCommandResult result = session.Submit(command);
		True(result.Accepted);
		Equal(75, result.ReadModel.Players[0].Score);
		Equal(40, result.ReadModel.Players[1].Score);
	}

	private static void InvalidStartingScoresAreRejectedAtomically()
	{
		var session = new AuthorityRuleSession(new TestRuleRandomSource(27), MatchRuleProfile.Empty);
		InitializeMatchCommand initialize = CreateInitializeCommand(104) with
		{
			StartingScores = ImmutableArray.Create(12, 34),
		};
		RuleCommandResult initialized = session.Submit(initialize);
		True(initialized.Accepted);
		MatchReadModel before = initialized.ReadModel;

		RuleCommandResult wrongLength = session.Submit(new RestartMatchCommand(
			105,
			before.Revision,
			2,
			before.TurnGeneration,
			2,
			100,
			3,
			true,
			initialize.Deck,
			StartingScores: ImmutableArray.Create(99)));
		False(wrongLength.Accepted);
		Equal(RuleRejectionCode.InvalidConfiguration, wrongLength.Rejection.Code);
		AssertReadModelsEquivalent(before, wrongLength.ReadModel);

		RuleCommandResult negative = session.Submit(new RestartMatchCommand(
			106,
			before.Revision,
			2,
			before.TurnGeneration,
			2,
			100,
			3,
			true,
			initialize.Deck,
			StartingScores: ImmutableArray.Create(99, -1)));
		False(negative.Accepted);
		Equal(RuleRejectionCode.InvalidConfiguration, negative.Rejection.Code);
		AssertReadModelsEquivalent(before, negative.ReadModel);
	}

	private static void MatchInitializationAppliesStartingDrawAndSupplyLimits()
	{
		var session = new AuthorityRuleSession(new TestRuleRandomSource(28), MatchRuleProfile.Empty);
		InitializeMatchCommand command = CreateInitializeCommand(107) with
		{
			MaximumHandSize = 5,
			AdditionalStartingDraws = ImmutableArray.Create(2, 1),
			SeatCardSupplyLimits = ImmutableArray.Create(1, -1),
		};

		RuleCommandResult result = session.Submit(command);
		True(result.Accepted);
		Equal(1, result.ReadModel.Players[0].Hand.Length);
		Equal(0, result.ReadModel.Players[0].Deck.Length);
		Equal(4, result.ReadModel.Players[1].Hand.Length);
		Equal(2, result.ReadModel.Players[1].Deck.Length);
	}

	private static void InvalidStartingDrawOptionsAreRejectedAtomically()
	{
		var session = new AuthorityRuleSession(new TestRuleRandomSource(30), MatchRuleProfile.Empty);
		InitializeMatchCommand initialize = CreateInitializeCommand(108);
		RuleCommandResult initialized = session.Submit(initialize);
		True(initialized.Accepted);
		MatchReadModel before = initialized.ReadModel;

		RuleCommandResult rejected = session.Submit(new RestartMatchCommand(
			109,
			before.Revision,
			2,
			before.TurnGeneration,
			2,
			100,
			3,
			true,
			initialize.Deck,
			MaximumHandSize: 4,
			AdditionalStartingDraws: ImmutableArray.Create(2, 0),
			SeatCardSupplyLimits: ImmutableArray.Create(-1, -1)));
		False(rejected.Accepted);
		Equal(RuleRejectionCode.InvalidConfiguration, rejected.Rejection.Code);
		AssertReadModelsEquivalent(before, rejected.ReadModel);
	}

	private static void ScriptedInitializationValidatesAndAppliesOneAtomicState()
	{
		var session = new AuthorityRuleSession(new TestRuleRandomSource(29), MatchRuleProfile.Empty);
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray.Create(new LogicalCardState(1, "special:deck", "special", "D")),
				ImmutableArray.Create(new LogicalCardState(2, "special:hand", "special", "H")),
				ImmutableArray<LogicalItemState>.Empty,
				7),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(new LogicalCardState(3, "clubs:K", "clubs", "K")),
				ImmutableArray.Create(new LogicalItemState("bomb")),
				4));
		var table = ImmutableArray.Create(new TableCardState(
			1,
			new LogicalCardState(4, "hearts:5", "hearts", "5"),
			true,
			false,
			false,
			false,
			null,
			-1,
			-1,
			false));

		RuleCommandResult result = session.Submit(new InitializeScriptedMatchCommand(
			111, 0, 1, 0, 2, 100, players, table, CurrentTurnSeat: 1));
		True(result.Accepted);
		Equal(1, result.ReadModel.CurrentTurnSeat);
		Equal("special:hand", result.ReadModel.Players[0].Hand[0].Code);
		Equal("bomb", result.ReadModel.Players[1].ItemHand[0].Id);
		Equal("hearts:5", result.ReadModel.TableCards[0].Card!.Value.Code);

		RuleCommandResult invalidCurrentSeat = session.Submit(
			new InitializeScriptedMatchCommand(
				113,
				result.Revision,
				2,
				result.TurnGeneration,
				2,
				100,
				players,
				table,
				CurrentTurnSeat: 2));
		False(invalidCurrentSeat.Accepted);
		Equal(RuleRejectionCode.InvalidConfiguration, invalidCurrentSeat.Rejection.Code);
		AssertReadModelsEquivalent(result.ReadModel, invalidCurrentSeat.ReadModel);

		var duplicatePlayers = players.SetItem(
			1,
			players[1] with
			{
				Hand = ImmutableArray.Create(new LogicalCardState(2, "clubs:K", "clubs", "K")),
			});
		RuleCommandResult rejected = session.Submit(new InitializeScriptedMatchCommand(
			112,
			result.Revision,
			2,
			result.TurnGeneration,
			2,
			100,
			duplicatePlayers,
			table));
		False(rejected.Accepted);
		Equal(RuleRejectionCode.InvalidConfiguration, rejected.Rejection.Code);
		AssertReadModelsEquivalent(result.ReadModel, rejected.ReadModel);
	}

	private static void QuestionRefreshStateChangesWithCaptureAndTurnCommands()
	{
		MatchRuleProfile profile = CreateItemRuleProfile();
		AuthorityRuleSession source = CreateGameplayCommandSession(profile: profile);
		MatchStateSnapshot seededSnapshot = source.CaptureSnapshot() with
		{
			CompletedTurnsSinceQuestionRefresh = 3,
			QuestionRefreshAnchorSeat = 1,
		};
		var session = AuthorityRuleSession.Restore(
			seededSnapshot,
			new TestRuleRandomSource(1),
			profile);
		MatchReadModel beforeCapture = session.ReadModel;

		RuleCommandResult capture = session.Submit(new CaptureCardsCommand(
			121,
			beforeCapture.Revision,
			beforeCapture.LifecycleGeneration,
			beforeCapture.TurnGeneration,
			Seat: 0,
			CaptureStartIndex: 0,
			RestartQuestionRefreshTracking: true));
		True(capture.Accepted);
		Equal(beforeCapture.Revision + 1, capture.Revision);
		Equal(0, capture.ReadModel.CompletedTurnsSinceQuestionRefresh);
		Equal(0, capture.ReadModel.QuestionRefreshAnchorSeat);

		RuleCommandResult firstTurn = session.Submit(new AdvanceTurnCommand(
			122,
			capture.Revision,
			capture.ReadModel.LifecycleGeneration,
			capture.TurnGeneration,
			ActingSeat: 0,
			Captured: false,
			CandidateNextSeat: 1,
			AdvanceQuestionRefresh: true,
			QuestionRefreshCyclesAfterCapture: 1));
		True(firstTurn.Accepted);
		False(firstTurn.Mutation.ShouldRefreshQuestionCard);
		Equal(1, firstTurn.ReadModel.CompletedTurnsSinceQuestionRefresh);
		Equal(0, firstTurn.ReadModel.QuestionRefreshAnchorSeat);

		RuleCommandResult refreshTurn = session.Submit(new AdvanceTurnCommand(
			123,
			firstTurn.Revision,
			firstTurn.ReadModel.LifecycleGeneration,
			firstTurn.TurnGeneration,
			ActingSeat: 1,
			Captured: false,
			CandidateNextSeat: 0,
			AdvanceQuestionRefresh: true,
			QuestionRefreshCyclesAfterCapture: 1));
		True(refreshTurn.Accepted);
		True(refreshTurn.Mutation.ShouldRefreshQuestionCard);
		Equal(0, refreshTurn.ReadModel.CompletedTurnsSinceQuestionRefresh);
		Equal(-1, refreshTurn.ReadModel.QuestionRefreshAnchorSeat);

		RuleCommandResult invalid = session.Submit(new AdvanceTurnCommand(
			124,
			refreshTurn.Revision,
			refreshTurn.ReadModel.LifecycleGeneration,
			refreshTurn.TurnGeneration,
			ActingSeat: 0,
			Captured: false,
			CandidateNextSeat: 1,
			AdvanceQuestionRefresh: true,
			QuestionRefreshCyclesAfterCapture: 0));
		False(invalid.Accepted);
		Equal(RuleRejectionCode.InvalidCommand, invalid.Rejection.Code);
		AssertReadModelsEquivalent(refreshTurn.ReadModel, invalid.ReadModel);
	}

	private static void QuestionCardExpiresAfterTwoCompletedTurnCommands()
	{
		AuthorityRuleSession session = CreateGameplayCommandSession(profile: CreateItemRuleProfile());
		MatchReadModel initial = session.ReadModel;
		RuleCommandResult firstTurn = session.Submit(new AdvanceTurnCommand(
			125,
			initial.Revision,
			initial.LifecycleGeneration,
			initial.TurnGeneration,
			ActingSeat: 0,
			Captured: false,
			CandidateNextSeat: 1,
			AdvanceQuestionRefresh: true,
			HasTableSpecialCard: true,
			QuestionRefreshCyclesAfterCapture: 2,
			HasQuestionCard: true,
			QuestionCardLifetimeTurns: 2));
		True(firstTurn.Accepted);
		False(firstTurn.Mutation.ShouldExpireQuestionCard);
		Equal(1, firstTurn.ReadModel.CompletedTurnsSinceQuestionRefresh);

		RuleCommandResult expired = session.Submit(new AdvanceTurnCommand(
			126,
			firstTurn.Revision,
			firstTurn.ReadModel.LifecycleGeneration,
			firstTurn.TurnGeneration,
			ActingSeat: 1,
			Captured: false,
			CandidateNextSeat: 0,
			AdvanceQuestionRefresh: true,
			HasTableSpecialCard: true,
			QuestionRefreshCyclesAfterCapture: 2,
			HasQuestionCard: true,
			QuestionCardLifetimeTurns: 2));
		True(expired.Accepted);
		True(expired.Mutation.ShouldExpireQuestionCard);
		False(expired.Mutation.ShouldRefreshQuestionCard);
		Equal(0, expired.ReadModel.CompletedTurnsSinceQuestionRefresh);
		Equal(0, expired.ReadModel.QuestionRefreshAnchorSeat);

		RuleCommandResult current = expired;
		for (int completedTurn = 1; completedTurn <= 4; completedTurn++)
		{
			int actingSeat = current.ReadModel.CurrentTurnSeat;
			int nextSeat = actingSeat == 0 ? 1 : 0;
			current = session.Submit(new AdvanceTurnCommand(
				126 + completedTurn,
				current.Revision,
				current.ReadModel.LifecycleGeneration,
				current.TurnGeneration,
				ActingSeat: actingSeat,
				Captured: false,
				CandidateNextSeat: nextSeat,
				AdvanceQuestionRefresh: true,
				QuestionRefreshCyclesAfterCapture: 2));
			True(current.Accepted);
			Equal(completedTurn == 4, current.Mutation.ShouldRefreshQuestionCard);
		}
	}

	private static void QuestionCardAgeSurvivesUnrelatedCapture()
	{
		MatchRuleProfile profile = CreateItemRuleProfile();
		AuthorityRuleSession source = CreateGameplayCommandSession(profile: profile);
		MatchStateSnapshot sourceSnapshot = source.CaptureSnapshot();
		var questionCard = new TableCardState(
			900_001,
			null,
			true,
			false,
			false,
			false,
			new LogicalItemState("question_mark"),
			-1,
			-1,
			false);
		AuthorityRuleSession session = AuthorityRuleSession.Restore(
			sourceSnapshot with
			{
				TableCards = sourceSnapshot.TableCards.Insert(0, questionCard),
				CompletedTurnsSinceQuestionRefresh = 1,
				QuestionRefreshAnchorSeat = 0,
			},
			new TestRuleRandomSource(1),
			profile);
		MatchReadModel beforeCapture = session.ReadModel;

		RuleCommandResult capture = session.Submit(new CaptureCardsCommand(
			135,
			beforeCapture.Revision,
			beforeCapture.LifecycleGeneration,
			beforeCapture.TurnGeneration,
			Seat: 0,
			CaptureStartIndex: 1,
			RestartQuestionRefreshTracking: false));
		True(capture.Accepted);
		Equal(1, capture.ReadModel.TableCards.Length);
		Equal("question_mark", capture.ReadModel.TableCards[0].ItemOverlay!.Value.Id);
		Equal(1, capture.ReadModel.CompletedTurnsSinceQuestionRefresh);
		Equal(0, capture.ReadModel.QuestionRefreshAnchorSeat);

		RuleCommandResult expired = session.Submit(new AdvanceTurnCommand(
			136,
			capture.Revision,
			capture.ReadModel.LifecycleGeneration,
			capture.TurnGeneration,
			ActingSeat: 0,
			Captured: false,
			CandidateNextSeat: 1,
			AdvanceQuestionRefresh: true,
			HasTableSpecialCard: true,
			QuestionRefreshCyclesAfterCapture: 2,
			HasQuestionCard: true,
			QuestionCardLifetimeTurns: 2));
		True(expired.Accepted);
		True(expired.Mutation.ShouldExpireQuestionCard);
	}

	private static void TurnGenerationAdvancesOnlyThroughItsRuleCommand()
	{
		AuthorityRuleSession session = CreateGameplayCommandSession();
		MatchReadModel before = session.ReadModel;
		RuleCommandResult advanced = session.Submit(new AdvanceTurnGenerationCommand(
			131,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration));
		True(advanced.Accepted);
		Equal(before.Revision + 1, advanced.Revision);
		Equal(before.TurnGeneration + 1, advanced.TurnGeneration);

		RuleCommandResult stale = session.Submit(new AdvanceTurnGenerationCommand(
			132,
			advanced.Revision,
			advanced.ReadModel.LifecycleGeneration,
			before.TurnGeneration));
		False(stale.Accepted);
		Equal(RuleRejectionCode.StaleTurnGeneration, stale.Rejection.Code);
		AssertReadModelsEquivalent(advanced.ReadModel, stale.ReadModel);

		MatchRuleProfile profile = MatchRuleProfile.Empty;
		MatchStateSnapshot exhaustedSnapshot = session.CaptureSnapshot() with
		{
			TurnGeneration = int.MaxValue,
		};
		var exhausted = AuthorityRuleSession.Restore(
			exhaustedSnapshot,
			new TestRuleRandomSource(1),
			profile);
		MatchReadModel beforeExhausted = exhausted.ReadModel;
		RuleCommandResult rejected = exhausted.Submit(new AdvanceTurnGenerationCommand(
			133,
			beforeExhausted.Revision,
			beforeExhausted.LifecycleGeneration,
			beforeExhausted.TurnGeneration));
		False(rejected.Accepted);
		Equal(RuleRejectionCode.InvalidConfiguration, rejected.Rejection.Code);
		AssertReadModelsEquivalent(beforeExhausted, rejected.ReadModel);
	}

	private static void TurnGenerationAdvanceCancelsObsoletePresentationBarrier()
	{
		AuthorityRuleSession session = CreateGameplayCommandSession(
			profile: CreateItemRuleProfile());
		MatchReadModel before = session.ReadModel;
		ImmutableArray<RuleTableCardReference> tableCards = before.TableCards
			.Select((card, index) => new RuleTableCardReference(
				index,
				card.RuntimeInstanceId))
			.ToImmutableArray();
		RuleCommandResult planned = session.Submit(new PlanTableShuffleCommand(
			134,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			ActingSeat: 0,
			TableCards: tableCards,
			FixedTableIndexes: ImmutableArray<int>.Empty));
		True(planned.Accepted);
		True(planned.ReadModel.HasPendingPresentationBarrier);
		True(planned.ReadModel.NormalizedPendingTableOrderRuntimeInstanceIds.Length > 0);

		RuleCommandResult advanced = session.Submit(new AdvanceTurnGenerationCommand(
			135,
			planned.Revision,
			planned.ReadModel.LifecycleGeneration,
			planned.TurnGeneration));
		True(advanced.Accepted);
		Equal(planned.TurnGeneration + 1, advanced.TurnGeneration);
		False(advanced.ReadModel.HasPendingPresentationBarrier);
		Equal(0, advanced.ReadModel.NormalizedPendingTableOrderRuntimeInstanceIds.Length);

		RuleCommandResult staleCompletion = session.Submit(
			new CompletePresentationBarrierCommand(
				136,
				advanced.Revision,
				advanced.ReadModel.LifecycleGeneration,
				planned.TurnGeneration,
				planned.ReadModel.PendingPresentationBarrierId));
		False(staleCompletion.Accepted);
		Equal(RuleRejectionCode.StaleTurnGeneration, staleCompletion.Rejection.Code);
		AssertReadModelsEquivalent(advanced.ReadModel, staleCompletion.ReadModel);
	}

	private static void AuthorityCardPlayCaptureScoreAndOutcomeCommandsAreOrdered()
	{
		AuthorityRuleSession session = CreateGameplayCommandSession(
			playerZeroScore: 98,
			playerZeroDeck: ImmutableArray<LogicalCardState>.Empty);
		MatchReadModel initialized = session.ReadModel;

		RuleCommandResult play = session.Submit(new PlayCardFromHandCommand(
			201,
			initialized.Revision,
			initialized.LifecycleGeneration,
			initialized.TurnGeneration,
			Seat: 0,
			HandIndex: 0,
			PrimaryMatchRank: "5"));
		True(play.Accepted);
		Equal(2L, play.Revision);
		Equal(0, play.ReadModel.Players[0].Hand.Length);
		Equal(3, play.ReadModel.TableCards.Length);
		Equal(0, play.Mutation.CaptureStartIndex);
		Equal(PresentationIntentKind.CardPlayed, play.PresentationIntents[0].Kind);
		Equal("card.played", play.PresentationIntents[0].SemanticEffectId);

		RuleCommandResult capture = session.Submit(new CaptureCardsCommand(
			202,
			play.Revision,
			play.ReadModel.LifecycleGeneration,
			play.TurnGeneration,
			Seat: 0,
			CaptureStartIndex: play.Mutation.CaptureStartIndex,
			ScoreMutations: ImmutableArray.Create(
				new RuleScoreMutation(0, RuleScoreMutationKind.Add, 2))));
		True(capture.Accepted);
		Equal(0, capture.ReadModel.TableCards.Length);
		Equal(3, capture.ReadModel.Players[0].Deck.Length);
		Equal(100, capture.ReadModel.Players[0].Score);
		Equal(3, capture.Mutation.CollectedCardCount);
		Equal(3, capture.Mutation.RemovedTableCardCount);
		Equal(PresentationIntentKind.CardsCaptured, capture.PresentationIntents[0].Kind);
		Equal(PresentationIntentKind.ScoreChanged, capture.PresentationIntents[1].Kind);
		Equal(2, capture.PresentationIntents[1].Value);

		RuleCommandResult outcome = session.Submit(new EvaluateMatchOutcomeCommand(
			203,
			capture.Revision,
			capture.ReadModel.LifecycleGeneration,
			capture.TurnGeneration));
		True(outcome.Accepted);
		Equal(Deckland.Rules.MatchOutcome.Won, outcome.ReadModel.Outcome);
		Equal(0, outcome.ReadModel.WinnerSeat);
		Equal(2, outcome.TurnGeneration);
		True(outcome.Mutation.MatchOutcomeChanged);
		Equal(PresentationIntentKind.MatchEnded, outcome.PresentationIntents[0].Kind);
	}

	private static void AuthorityGameplayCommandsRejectIllegalStaleAndDuplicateActionsAtomically()
	{
		AuthorityRuleSession session = CreateGameplayCommandSession();
		MatchReadModel before = session.ReadModel;
		PlayCardFromHandCommand valid = new(
			211,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			HandIndex: 0,
			PrimaryMatchRank: "5");

		RuleCommandResult wrongTurn = session.Submit(valid with { ActionId = 212, Seat = 1 });
		False(wrongTurn.Accepted);
		Equal(RuleRejectionCode.WrongTurn, wrongTurn.Rejection.Code);
		AssertReadModelsEquivalent(before, wrongTurn.ReadModel);

		RuleCommandResult invalidIndex = session.Submit(valid with { ActionId = 213, HandIndex = 99 });
		False(invalidIndex.Accepted);
		Equal(RuleRejectionCode.InvalidIndex, invalidIndex.Rejection.Code);
		AssertReadModelsEquivalent(before, invalidIndex.ReadModel);

		RuleCommandResult staleGeneration = session.Submit(valid with
		{
			ActionId = 214,
			TurnGeneration = before.TurnGeneration + 1,
		});
		False(staleGeneration.Accepted);
		Equal(RuleRejectionCode.StaleTurnGeneration, staleGeneration.Rejection.Code);
		AssertReadModelsEquivalent(before, staleGeneration.ReadModel);

		RuleCommandResult played = session.Submit(valid);
		True(played.Accepted);
		RuleCommandResult duplicate = session.Submit(valid);
		False(duplicate.Accepted);
		Equal(RuleRejectionCode.DuplicateAction, duplicate.Rejection.Code);
		AssertReadModelsEquivalent(played.ReadModel, duplicate.ReadModel);

		RuleCommandResult invalidCapture = session.Submit(new CaptureCardsCommand(
			215,
			played.Revision,
			played.ReadModel.LifecycleGeneration,
			played.TurnGeneration,
			Seat: 0,
			CaptureStartIndex: played.Mutation.CaptureStartIndex,
			ExtraTableCardIndexes: ImmutableArray.Create(played.Mutation.CaptureStartIndex)));
		False(invalidCapture.Accepted);
		Equal(RuleRejectionCode.InvalidIndex, invalidCapture.Rejection.Code);
		AssertReadModelsEquivalent(played.ReadModel, invalidCapture.ReadModel);

		RuleCommandResult staleRevision = session.Submit(new CaptureCardsCommand(
			216,
			before.Revision,
			played.ReadModel.LifecycleGeneration,
			played.TurnGeneration,
			Seat: 0,
			CaptureStartIndex: played.Mutation.CaptureStartIndex));
		False(staleRevision.Accepted);
		Equal(RuleRejectionCode.StaleRevision, staleRevision.Rejection.Code);
		AssertReadModelsEquivalent(played.ReadModel, staleRevision.ReadModel);
	}

	private static void AuthorityDrawAndTurnCommandsPreserveZoneAndTurnInvariants()
	{
		var drawCard = new LogicalCardState(10, "clubs:7", "clubs", "7");
		AuthorityRuleSession session = CreateGameplayCommandSession(
			playerZeroDeck: ImmutableArray.Create(drawCard));
		MatchReadModel before = session.ReadModel;

		RuleCommandResult draw = session.Submit(new DrawCardsCommand(
			221,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			Count: 1,
			MaximumHandSize: 8));
		True(draw.Accepted);
		Equal(0, draw.ReadModel.Players[0].Deck.Length);
		Equal(2, draw.ReadModel.Players[0].Hand.Length);
		Equal(drawCard.RuntimeInstanceId, draw.ReadModel.Players[0].Hand[1].RuntimeInstanceId);
		Equal(PresentationIntentKind.CardDrawn, draw.PresentationIntents[0].Kind);

		RuleCommandResult wrongActor = session.Submit(new AdvanceTurnCommand(
			222,
			draw.Revision,
			draw.ReadModel.LifecycleGeneration,
			draw.TurnGeneration,
			ActingSeat: 1,
			Captured: false,
			CandidateNextSeat: 0));
		False(wrongActor.Accepted);
		Equal(RuleRejectionCode.WrongTurn, wrongActor.Rejection.Code);
		AssertReadModelsEquivalent(draw.ReadModel, wrongActor.ReadModel);

		RuleCommandResult advance = session.Submit(new AdvanceTurnCommand(
			223,
			draw.Revision,
			draw.ReadModel.LifecycleGeneration,
			draw.TurnGeneration,
			ActingSeat: 0,
			Captured: false,
			CandidateNextSeat: 1));
		True(advance.Accepted);
		Equal(1, advance.ReadModel.CurrentTurnSeat);
		Equal(1, advance.TurnGeneration);
		Equal(PresentationIntentKind.TurnChanged, advance.PresentationIntents[0].Kind);
	}

	private static void AuthorityScoreMutationsValidateTheWholeBatchBeforeApplying()
	{
		AuthorityRuleSession session = CreateGameplayCommandSession(playerZeroScore: 10);
		MatchReadModel before = session.ReadModel;
		RuleCommandResult rejected = session.Submit(new ApplyScoreMutationsCommand(
			231,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			ImmutableArray.Create(
				new RuleScoreMutation(0, RuleScoreMutationKind.Add, 5),
				new RuleScoreMutation(9, RuleScoreMutationKind.Set, 1))));
		False(rejected.Accepted);
		Equal(RuleRejectionCode.InvalidCommand, rejected.Rejection.Code);
		AssertReadModelsEquivalent(before, rejected.ReadModel);

		RuleCommandResult applied = session.Submit(new ApplyScoreMutationsCommand(
			232,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			ImmutableArray.Create(
				new RuleScoreMutation(0, RuleScoreMutationKind.Add, 5),
				new RuleScoreMutation(0, RuleScoreMutationKind.Subtract, 3))));
		True(applied.Accepted);
		Equal(12, applied.ReadModel.Players[0].Score);
		Equal(2, applied.Mutation.NormalizedScoreChanges.Length);
	}

	private static void AuthorityReturnToHandCommandIsOrderedAndAtomic()
	{
		var first = new LogicalCardState(10, "clubs:7", "clubs", "7");
		var second = new LogicalCardState(11, "hearts:9", "hearts", "9");
		AuthorityRuleSession session = CreateGameplayCommandSession(
			playerZeroDeck: ImmutableArray.Create(first, second));
		MatchReadModel before = session.ReadModel;

		RuleCommandResult missing = session.Submit(new ReturnCardsToHandCommand(
			225,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			CardRuntimeInstanceIds: ImmutableArray.Create(first.RuntimeInstanceId, 999L),
			MaximumHandSize: 8));
		False(missing.Accepted);
		Equal(RuleRejectionCode.InvalidIndex, missing.Rejection.Code);
		AssertReadModelsEquivalent(before, missing.ReadModel);

		RuleCommandResult returned = session.Submit(new ReturnCardsToHandCommand(
			226,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			CardRuntimeInstanceIds: ImmutableArray.Create(second.RuntimeInstanceId, first.RuntimeInstanceId),
			MaximumHandSize: 8));
		True(returned.Accepted);
		Equal(0, returned.ReadModel.Players[0].Deck.Length);
		Equal(second.RuntimeInstanceId, returned.ReadModel.Players[0].Hand[1].RuntimeInstanceId);
		Equal(first.RuntimeInstanceId, returned.ReadModel.Players[0].Hand[2].RuntimeInstanceId);
		Equal(2, returned.Mutation.CollectedCardCount);
		Equal(1, returned.Mutation.TargetIndex);
		Equal(PresentationIntentKind.CardReturnedToHand, returned.PresentationIntents[0].Kind);
		Equal("card.returned_to_hand", returned.PresentationIntents[0].SemanticEffectId);

		MatchReadModel after = returned.ReadModel;
		RuleCommandResult overCapacity = session.Submit(new ReturnCardsToHandCommand(
			227,
			after.Revision,
			after.LifecycleGeneration,
			after.TurnGeneration,
			Seat: 0,
			CardRuntimeInstanceIds: ImmutableArray.Create(first.RuntimeInstanceId),
			MaximumHandSize: after.Players[0].Hand.Length));
		False(overCapacity.Accepted);
		Equal(RuleRejectionCode.NoLegalAction, overCapacity.Rejection.Code);
		AssertReadModelsEquivalent(after, overCapacity.ReadModel);
	}

	private static void AuthorityTablePlacementAcceptsSuitlessSpecialCard()
	{
		AuthorityRuleSession session = CreateGameplayCommandSession();
		MatchReadModel before = session.ReadModel;
		var specialCard = new LogicalCardState(40, ":J", string.Empty, "J");

		RuleCommandResult placed = session.Submit(new PlaceTableCardCommand(
			201,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			ActingSeat: 0,
			Card: specialCard,
			TableIndex: before.TableCards.Length,
			IsFaceUp: true,
			CardOwnerSeat: 0,
			RemoveOnCapture: true));

		True(placed.Accepted);
		TableCardState tableCard = placed.ReadModel.TableCards[^1];
		Equal(specialCard, tableCard.Card!.Value);
		True(tableCard.RemoveOnCapture);
		Equal(specialCard, session.CaptureSnapshot().TableCards[^1].Card!.Value);
	}

	private static void BaseTableFaceStateCommandDoesNotRequireItemRules()
	{
		AuthorityRuleSession session = CreateGameplayCommandSession();
		MatchReadModel before = session.ReadModel;
		TableCardState target = before.TableCards[0];

		RuleCommandResult result = session.Submit(new SetTableCardsFaceUpCommand(
			203,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			ActingSeat: 0,
			TableCards: ImmutableArray.Create(new RuleTableCardReference(
				0,
				target.RuntimeInstanceId)),
			ExpectedFaceUp: true,
			FaceUp: false));

		True(result.Accepted);
		False(result.ReadModel.TableCards[0].IsFaceUp);
		Equal(1, result.PresentationIntents.Length);
		Equal(PresentationIntentKind.TableCardStateChanged, result.PresentationIntents[0].Kind);
		Equal("table.card_concealed", result.PresentationIntents[0].SemanticEffectId);
	}

}
