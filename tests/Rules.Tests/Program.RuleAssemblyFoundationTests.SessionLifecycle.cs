using System.Collections.Immutable;
using System.Reflection;
using Deckland.Rules;

internal static partial class Program
{
	private static void RuleProfileValidatesDeterministicModuleOrdering()
	{
		var first = new TestRuleModule("first", 10);
		var second = new TestRuleModule("second", 20, ImmutableArray.Create("first"));
		var profile = new MatchRuleProfile(new IRuleModule[] { second, first });
		Equal("first", profile.Modules[0].Id);
		Equal("second", profile.Modules[1].Id);

		True(ThrowsArgumentException(() => new MatchRuleProfile(new IRuleModule[]
		{
			first,
			new TestRuleModule("first", 30),
		})));
		True(ThrowsArgumentException(() => new MatchRuleProfile(new IRuleModule[]
		{
			first,
			new TestRuleModule("conflict", 10),
		})));
		True(ThrowsArgumentException(() => new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule("dependent", 10, ImmutableArray.Create("missing")),
		})));
		True(ThrowsArgumentException(() => new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule("dependent", 10, ImmutableArray.Create("later")),
			new TestRuleModule("later", 20),
		})));
	}

	private static void AuthoritySessionRejectsStaleDuplicateAndBlockedCommands()
	{
		var random = new TestRuleRandomSource(1234);
		var session = new AuthorityRuleSession(random, MatchRuleProfile.Empty);
		RuleCommandResult initialized = session.Submit(CreateInitializeCommand(
			actionId: 10,
			createOpeningBarrier: true));
		True(initialized.Accepted);
		Equal(1L, initialized.Revision);
		Equal(1L, initialized.ReadModel.PendingPresentationBarrierId);

		RuleCommandResult duplicate = session.Submit(CreateInitializeCommand(actionId: 10));
		False(duplicate.Accepted);
		Equal(RuleRejectionCode.DuplicateAction, duplicate.Rejection.Code);
		Equal(1L, duplicate.Revision);

		RuleCommandResult blocked = session.Submit(new EndMatchSessionCommand(11, 1, 1, 1));
		False(blocked.Accepted);
		Equal(RuleRejectionCode.PresentationBarrierPending, blocked.Rejection.Code);

		RuleCommandResult wrongBarrier = session.Submit(new CompletePresentationBarrierCommand(12, 1, 1, 1, 99));
		False(wrongBarrier.Accepted);
		Equal(RuleRejectionCode.PresentationBarrierMismatch, wrongBarrier.Rejection.Code);
		Equal(1L, wrongBarrier.Revision);

		RuleCommandResult completed = session.Submit(new CompletePresentationBarrierCommand(13, 1, 1, 1, 1));
		True(completed.Accepted);
		Equal(2L, completed.Revision);

		RuleCommandResult stale = session.Submit(new EndMatchSessionCommand(14, 1, 1, 1));
		False(stale.Accepted);
		Equal(RuleRejectionCode.StaleRevision, stale.Rejection.Code);

		RuleCommandResult ended = session.Submit(new EndMatchSessionCommand(15, 2, 1, 1));
		True(ended.Accepted);
		Equal(Deckland.Rules.MatchOutcome.Ended, ended.ReadModel.Outcome);
	}

	private static void AuthoritySnapshotRestoresActionAndRandomContinuity()
	{
		var originalRandom = new TestRuleRandomSource(98765);
		var original = new AuthorityRuleSession(originalRandom, MatchRuleProfile.Empty);
		True(original.Submit(CreateInitializeCommand(actionId: 21)).Accepted);
		MatchStateSnapshot snapshot = original.CaptureSnapshot();

		int[] expectedContinuation =
		{
			originalRandom.NextIntInclusive(0, 1000),
			originalRandom.NextIntInclusive(0, 1000),
			originalRandom.NextIntInclusive(0, 1000),
		};

		var restoredRandom = new TestRuleRandomSource(1);
		AuthorityRuleSession restored = AuthorityRuleSession.Restore(
			snapshot,
			restoredRandom,
			MatchRuleProfile.Empty);
		foreach (int expected in expectedContinuation)
		{
			Equal(expected, restoredRandom.NextIntInclusive(0, 1000));
		}

		RuleCommandResult duplicate = restored.Submit(CreateInitializeCommand(actionId: 21));
		False(duplicate.Accepted);
		Equal(RuleRejectionCode.DuplicateAction, duplicate.Rejection.Code);
		Equal(snapshot.Revision, restored.ReadModel.Revision);
	}

	private static void AuthoritySnapshotRestoresQuestionRefreshStateAtomically()
	{
		var sourceRandom = new TestRuleRandomSource(20260804);
		var source = new AuthorityRuleSession(sourceRandom, MatchRuleProfile.Empty);
		True(source.Submit(CreateInitializeCommand(actionId: 22)).Accepted);
		MatchStateSnapshot snapshot = source.CaptureSnapshot() with
		{
			CompletedTurnsSinceQuestionRefresh = 3,
			QuestionRefreshAnchorSeat = 1,
		};

		var restoredRandom = new TestRuleRandomSource(1);
		AuthorityRuleSession restored = AuthorityRuleSession.Restore(
			snapshot,
			restoredRandom,
			MatchRuleProfile.Empty);
		Equal(MatchStateSnapshot.CurrentSchemaVersion, snapshot.SchemaVersion);
		Equal(3, restored.ReadModel.CompletedTurnsSinceQuestionRefresh);
		Equal(1, restored.ReadModel.QuestionRefreshAnchorSeat);
		MatchStateSnapshot recaptured = restored.CaptureSnapshot();
		Equal(3, recaptured.CompletedTurnsSinceQuestionRefresh);
		Equal(1, recaptured.QuestionRefreshAnchorSeat);

		MatchReadModel beforeRejectedRestore = restored.ReadModel;
		RuleRandomState randomBeforeRejectedRestore = restoredRandom.CaptureState();
		True(ThrowsArgumentException(() => AuthorityRuleSession.Restore(
			snapshot with { QuestionRefreshAnchorSeat = snapshot.PlayerCount },
			restoredRandom,
			MatchRuleProfile.Empty)));
		Equal(randomBeforeRejectedRestore, restoredRandom.CaptureState());
		AssertReadModelsEquivalent(beforeRejectedRestore, restored.ReadModel);

		True(ThrowsArgumentException(() => AuthorityRuleSession.Restore(
			snapshot with { RandomState = new RuleRandomState(snapshot.RandomState.Seed, 0) },
			restoredRandom,
			MatchRuleProfile.Empty)));
		Equal(randomBeforeRejectedRestore, restoredRandom.CaptureState());
		AssertReadModelsEquivalent(beforeRejectedRestore, restored.ReadModel);
	}

	private static void MatchFlowCommandValidatesAndReplacesStateAtomically()
	{
		AuthorityRuleSession session = CreateGameplayCommandSession();
		MatchReadModel before = session.ReadModel;
		MatchFlowState replacement = before.NormalizedMatchFlow with
		{
			SuccessfulCapturesThisTurn = 2,
			MainPlayerCapturesThisBattle = 4,
			MainPlayerRealTurnActive = false,
			MainPlayerRealTurnStartPending = true,
			PendingBaseCardFacesUp = true,
			PendingCardPlacement = PendingCardPlacementMode.Leftmost,
			PendingCaptureDamageDelta = -3,
			CaptureMarkRuntimeId = 19,
			DisabledMainPlayerHandDisplayIndex = 1,
			DoubleEffectReady = true,
			ScriptedSequenceStep = 6,
			PendingProgressionChoices = ImmutableArray.Create(
				PendingProgressionChoiceKind.Milestone),
			PendingWinnerSeat = 0,
		};

		RuleCommandResult replaced = session.Submit(new ReplaceMatchFlowStateCommand(
			12001,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			replacement));
		True(replaced.Accepted);
		Equal(before.Revision + 1, replaced.Revision);
		Equal(replacement, replaced.ReadModel.NormalizedMatchFlow);

		MatchReadModel beforeInvalid = replaced.ReadModel;
		RuleCommandResult invalid = session.Submit(new ReplaceMatchFlowStateCommand(
			12002,
			beforeInvalid.Revision,
			beforeInvalid.LifecycleGeneration,
			beforeInvalid.TurnGeneration,
			replacement with { SuccessfulCapturesThisTurn = -1 }));
		False(invalid.Accepted);
		Equal(RuleRejectionCode.InvalidConfiguration, invalid.Rejection.Code);
		AssertReadModelsEquivalent(beforeInvalid, invalid.ReadModel);

		RuleCommandResult contradictory = session.Submit(new ReplaceMatchFlowStateCommand(
			12003,
			beforeInvalid.Revision,
			beforeInvalid.LifecycleGeneration,
			beforeInvalid.TurnGeneration,
			replacement with
			{
				MainPlayerRealTurnActive = true,
				MainPlayerRealTurnStartPending = true,
			}));
		False(contradictory.Accepted);
		Equal(RuleRejectionCode.InvalidConfiguration, contradictory.Rejection.Code);
		AssertReadModelsEquivalent(beforeInvalid, contradictory.ReadModel);

		foreach (MatchFlowState invalidFlow in new[]
		{
			replacement with
			{
				PendingProgressionChoices = ImmutableArray.Create(
					PendingProgressionChoiceKind.None),
			},
			replacement with
			{
				PendingProgressionChoices = ImmutableArray.Create(
					PendingProgressionChoiceKind.Victory,
					PendingProgressionChoiceKind.Victory),
			},
			replacement with { PendingWinnerSeat = 2 },
			replacement with
			{
				BlockedAutomatedItemSeat = 1,
				BlockedAutomatedItemId = string.Empty,
			},
			replacement with
			{
				BlockedAutomatedItemSeat = -1,
				BlockedAutomatedItemId = "lens",
			},
			replacement with
			{
				BlockedAutomatedItemSeat = 2,
				BlockedAutomatedItemId = "lens",
			},
		})
		{
			RuleCommandResult rejected = session.Submit(new ReplaceMatchFlowStateCommand(
				12004,
				beforeInvalid.Revision,
				beforeInvalid.LifecycleGeneration,
				beforeInvalid.TurnGeneration,
				invalidFlow));
			False(rejected.Accepted);
			Equal(RuleRejectionCode.InvalidConfiguration, rejected.Rejection.Code);
			AssertReadModelsEquivalent(beforeInvalid, rejected.ReadModel);
		}
	}

	private static void MatchFlowStateSurvivesAuthorityAndReplicaSnapshots()
	{
		AuthorityRuleSession source = CreateGameplayCommandSession();
		MatchReadModel initialized = source.ReadModel;
		MatchFlowState flow = initialized.NormalizedMatchFlow with
		{
			SuccessfulCapturesThisTurn = 3,
			MainPlayerCapturesThisBattle = 8,
			PendingCardPlacement = PendingCardPlacementMode.BeforeTrackedAnchor,
			PendingPlacementAnchorRuntimeId = 101,
			CaptureMarkRuntimeId = 102,
			TurnCaptureMarkRuntimeId = 103,
			SequentialRevealAnchorRuntimeId = 104,
			CaptureThresholdProgress = 2,
			CaptureThresholdReady = true,
			PeriodicItemActivationsThisMatch = 1,
			LimitedStealActivationsThisMatch = 2,
			PendingProgressionChoices = ImmutableArray.Create(
				PendingProgressionChoiceKind.Milestone,
				PendingProgressionChoiceKind.Victory),
			PendingWinnerSeat = 0,
			BlockedAutomatedItemSeat = 1,
			BlockedAutomatedItemId = "bomb",
		};
		RuleCommandResult changed = source.Submit(new ReplaceMatchFlowStateCommand(
			12011,
			initialized.Revision,
			initialized.LifecycleGeneration,
			initialized.TurnGeneration,
			flow));
		True(changed.Accepted);

		MatchStateSnapshot snapshot = source.CaptureSnapshot();
		Equal(flow, snapshot.NormalizedMatchFlow);
		AuthorityRuleSession restored = AuthorityRuleSession.Restore(
			snapshot,
			new TestRuleRandomSource(12012),
			MatchRuleProfile.Empty);
		Equal(flow, restored.ReadModel.NormalizedMatchFlow);
		Equal(flow, restored.CaptureSnapshot().NormalizedMatchFlow);

		var replica = new ReplicaRuleSession();
		SnapshotApplyResult applied = replica.ApplyAuthoritySnapshot(snapshot);
		True(applied.Applied);
		Equal(flow, replica.ReadModel.NormalizedMatchFlow);

		SnapshotApplyResult invalid = new ReplicaRuleSession().ApplyAuthoritySnapshot(
			snapshot with
			{
				MatchFlow = flow with { PendingPlacementAnchorRuntimeId = -1 },
			});
		False(invalid.Applied);
		Equal(RuleRejectionCode.InvalidConfiguration, invalid.Rejection.Code);
	}

	private static void AutomatedItemBlockIsRuleOwnedAndClearsWithTurnLifecycle()
	{
		AuthorityRuleSession turnSession = CreateGameplayCommandSession();
		MatchReadModel initial = turnSession.ReadModel;
		RuleCommandResult blocked = turnSession.Submit(new ReplaceMatchFlowStateCommand(
			12021,
			initial.Revision,
			initial.LifecycleGeneration,
			initial.TurnGeneration,
			initial.NormalizedMatchFlow with
			{
				BlockedAutomatedItemSeat = 0,
				BlockedAutomatedItemId = "bomb",
			}));
		True(blocked.Accepted);
		RuleCommandResult advancedTurn = turnSession.Submit(new AdvanceTurnCommand(
			12022,
			blocked.Revision,
			blocked.ReadModel.LifecycleGeneration,
			blocked.TurnGeneration,
			ActingSeat: 0,
			Captured: false,
			CandidateNextSeat: 1));
		True(advancedTurn.Accepted);
		Equal(-1, advancedTurn.ReadModel.NormalizedMatchFlow.BlockedAutomatedItemSeat);
		Equal(string.Empty, advancedTurn.ReadModel.NormalizedMatchFlow.BlockedAutomatedItemId);

		MatchReadModel beforeGeneration = advancedTurn.ReadModel;
		RuleCommandResult blockedAgain = turnSession.Submit(new ReplaceMatchFlowStateCommand(
			12023,
			beforeGeneration.Revision,
			beforeGeneration.LifecycleGeneration,
			beforeGeneration.TurnGeneration,
			beforeGeneration.NormalizedMatchFlow with
			{
				BlockedAutomatedItemSeat = 1,
				BlockedAutomatedItemId = "lens",
			}));
		True(blockedAgain.Accepted);
		RuleCommandResult advancedGeneration = turnSession.Submit(
			new AdvanceTurnGenerationCommand(
				12024,
				blockedAgain.Revision,
				blockedAgain.ReadModel.LifecycleGeneration,
				blockedAgain.TurnGeneration));
		True(advancedGeneration.Accepted);
		Equal(-1, advancedGeneration.ReadModel.NormalizedMatchFlow.BlockedAutomatedItemSeat);
		Equal(string.Empty, advancedGeneration.ReadModel.NormalizedMatchFlow.BlockedAutomatedItemId);

		string root = FindRepositoryRoot();
		string boardSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.cs"));
		string aiSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.NetworkAiProxy.cs"));
		False(boardSource.Contains("_networkAiBlockedItem", StringComparison.Ordinal));
		False(aiSource.Contains("_networkAiBlockedItem", StringComparison.Ordinal));
		True(aiSource.Contains("BlockedAutomatedItemId = _pendingItemCardId", StringComparison.Ordinal));
	}

	private static void ReplicaSessionOnlyAppliesNewerAuthoritySnapshots()
	{
		var authority = new AuthorityRuleSession(
			new TestRuleRandomSource(456),
			MatchRuleProfile.Empty);
		True(authority.Submit(CreateInitializeCommand(actionId: 31)).Accepted);
		MatchStateSnapshot firstSnapshot = authority.CaptureSnapshot();

		var replica = new ReplicaRuleSession();
		SnapshotApplyResult first = replica.ApplyAuthoritySnapshot(firstSnapshot);
		True(first.Applied);
		Equal(firstSnapshot.Revision, replica.ReadModel.Revision);

		SnapshotApplyResult repeated = replica.ApplyAuthoritySnapshot(firstSnapshot);
		False(repeated.Applied);
		Equal(RuleRejectionCode.StaleRevision, repeated.Rejection.Code);
		Equal(firstSnapshot.Revision, replica.ReadModel.Revision);

		False(typeof(ReplicaRuleSession).GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.Any(method => method.Name == nameof(AuthorityRuleSession.Submit)));
	}

}
