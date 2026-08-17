using System.Collections.Immutable;
using System.Reflection;
using Deckland.Rules;

internal static partial class Program
{
	private static void AdventureProductionStateStaysBehindAuthoritySession()
	{
		string root = FindRepositoryRoot();
		string storeSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"AdventureRunSessionStore.cs"));
		string storeImplementationSource = string.Concat(
			Directory.GetFiles(
				Path.Combine(root, "scripts"),
				"AdventureRunSessionStore*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));
		string authorityOperationsSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"AdventureRunSessionStore.AuthorityOperations.cs"));
		string boardSource = string.Join(
			'\n',
			Directory.GetFiles(
				Path.Combine(root, "scripts"),
				"GameBoardScene*.cs",
				SearchOption.TopDirectoryOnly)
				.Select(File.ReadAllText));
		string assemblyInfoSource = File.ReadAllText(Path.Combine(
			root,
			"src",
			"Deckland.Rules",
			"AssemblyInfo.cs"));

		False(storeImplementationSource.Contains("_runState", StringComparison.Ordinal));
		False(storeImplementationSource.Contains("new AdventureUpgradeRunState", StringComparison.Ordinal));
		True(authorityOperationsSource.Contains(".SubmitAdventure(", StringComparison.Ordinal));
		False(storeSource.Contains("internal AdventureRunWriteResult BeginNewRun(", StringComparison.Ordinal));
		foreach (string implementationType in new[]
		{
			"AdventureUpgradeEffectService",
			"AdventureUpgradeChoiceRules",
			"AdventureUpgradeTriggerRules",
			"AdventureUpgradeRuntimeValues",
		})
		{
			False(storeImplementationSource.Contains(implementationType, StringComparison.Ordinal));
			False(boardSource.Contains(implementationType, StringComparison.Ordinal));
		}

		False(assemblyInfoSource.Contains(
			"InternalsVisibleTo(\"pokegame\")",
			StringComparison.Ordinal));
		False(typeof(AdventureUpgradeRunState).IsPublic);
		False(typeof(AdventureUpgradeEffectState).IsPublic);
		False(typeof(AdventureUpgradeEffectService).IsPublic);
		False(typeof(AdventureUpgradeChoiceRules).IsPublic);
		False(typeof(AdventureUpgradeTriggerRules).IsPublic);
		False(typeof(AdventureUpgradeRuntimeValues).IsPublic);
	}

	private static void AdventureAuthorityHandoffPreservesTransientState()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var sourceRandom = new TestRuleRandomSource(7101);
		var source = new AuthorityRuleSession(sourceRandom, MatchRuleProfile.Empty);
		True(source.ConfigureAdventureManifest(manifest));
		True(source.SubmitAdventure(
			new BeginAdventureRunCommand(
				1,
				0,
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1)),
			_ => true).Accepted);
		AdventureEffectStateSnapshot transientEffects = source.AdventureReadModel.Effects with
		{
			NextCaptureDamageBonus = 4,
			NextTurnHealAmount = 3,
			NextTurnDrawCount = 2,
			ClubShortBranchReturnUsedThisTurn = true,
			ClubMissClimbGrowthStacks = 3,
			ClubBorrowSuitSetArmed = true,
			ClubGapRootingUsedThisTurn = true,
		};
		True(source.SubmitAdventure(
			new ReplaceAdventureEffectStateCommand(2, 1, transientEffects),
			_ => true).Accepted);
		MatchFlowState transientFlow = MatchFlowState.NewMatch with
		{
			SuccessfulCapturesThisTurn = 2,
			MainPlayerCapturesThisBattle = 5,
			PendingBaseCardFacesUp = true,
			PendingCardPlacement = PendingCardPlacementMode.BeforeTrackedAnchor,
			PendingPlacementAnchorRuntimeId = 41,
			CaptureMarkRuntimeId = 42,
			ActiveControlUsedThisTurn = true,
			PendingProgressionChoices = ImmutableArray.Create(
				PendingProgressionChoiceKind.SessionStart,
				PendingProgressionChoiceKind.Victory),
			PendingWinnerSeat = 0,
			BlockedAutomatedItemSeat = 1,
			BlockedAutomatedItemId = "lens",
		};
		AdventureAuthoritySnapshot sourceSnapshot = source.CaptureAdventureAuthority();
		Equal(
			AdventureUpgradeRestoreResult.Restored,
			source.RestoreAdventureAuthority(sourceSnapshot with { MatchFlow = transientFlow }));

		AdventureAuthoritySnapshot snapshot = source.CaptureAdventureAuthority();
		Equal(2L, snapshot.Revision);
		True(snapshot.Effects.HasValue);
		Equal(transientFlow, snapshot.NormalizedMatchFlow);
		var restored = new AuthorityRuleSession(
			new TestRuleRandomSource(7102),
			MatchRuleProfile.Empty);
		True(restored.ConfigureAdventureManifest(manifest));
		Equal(
			AdventureUpgradeRestoreResult.Restored,
			restored.RestoreAdventureAuthority(snapshot));
		Equal(snapshot.Effects!.Value, restored.AdventureReadModel.Effects);
		Equal(snapshot.Revision, restored.AdventureReadModel.Revision);
		Equal(transientFlow, restored.ReadModel.NormalizedMatchFlow);

		AdventureRuleCommandResult duplicate = restored.SubmitAdventure(
			new SetAdventureHealthCommand(2, snapshot.Revision, 80, 100),
			_ => true);
		False(duplicate.Accepted);
		Equal(RuleRejectionCode.DuplicateAction, duplicate.Rejection.Code);
		AdventureRuleCommandResult continued = restored.SubmitAdventure(
			new SetAdventureHealthCommand(3, snapshot.Revision, 80, 100),
			_ => true);
		True(continued.Accepted);
		Equal(3L, continued.Revision);
		Equal(80, continued.ReadModel.CurrentHealth);
	}

	private static void AdventurePersistedRestoreDropsTransientState()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var session = new AuthorityRuleSession(
			new TestRuleRandomSource(7201),
			MatchRuleProfile.Empty);
		True(session.ConfigureAdventureManifest(manifest));
		True(session.SubmitAdventure(
			new BeginAdventureRunCommand(
				1,
				0,
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1)),
			_ => true).Accepted);
		True(session.SubmitAdventure(
			new ReplaceAdventureEffectStateCommand(
				2,
				1,
				session.AdventureReadModel.Effects with
				{
					NextCaptureDamageBonus = 5,
					NextTurnHealAmount = 4,
					NextTurnDrawCount = 3,
					ClubBorrowSuitSetArmed = true,
				}),
			_ => true).Accepted);
		AdventureAuthoritySnapshot authority = session.CaptureAdventureAuthority();
		Equal(
			AdventureUpgradeRestoreResult.Restored,
			session.RestoreAdventureAuthority(authority with
			{
				MatchFlow = MatchFlowState.NewMatch with
				{
					SuccessfulCapturesThisTurn = 3,
					QuestionRejudgeUsedThisBattle = true,
					SequentialRevealAnchorRuntimeId = 77,
				},
			}));
		AdventureUpgradeRunSnapshot persisted =
			session.CaptureAdventureAuthority().RunSnapshot!.Value;

		var restored = new AuthorityRuleSession(
			new TestRuleRandomSource(7202),
			MatchRuleProfile.Empty);
		True(restored.ConfigureAdventureManifest(manifest));
		Equal(
			AdventureUpgradeRestoreResult.Restored,
			restored.RestoreAdventurePersistedRun(persisted));
		Equal(0L, restored.AdventureReadModel.Revision);
		Equal(0, restored.AdventureReadModel.Effects.NextCaptureDamageBonus);
		Equal(0, restored.AdventureReadModel.Effects.NextTurnHealAmount);
		Equal(0, restored.AdventureReadModel.Effects.NextTurnDrawCount);
		False(restored.AdventureReadModel.Effects.ClubBorrowSuitSetArmed);
		Equal(MatchFlowState.Empty, restored.ReadModel.NormalizedMatchFlow);
	}

	private static void AdventurePersistenceFailurePreservesAuthorityAtomically()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var random = new TestRuleRandomSource(7301);
		var session = new AuthorityRuleSession(random, MatchRuleProfile.Empty);
		True(session.ConfigureAdventureManifest(manifest));
		True(session.SubmitAdventure(
			new BeginAdventureRunCommand(
				1,
				0,
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1)),
			_ => true).Accepted);

		AdventureRunReadModel readModelBefore = session.AdventureReadModel;
		AdventureAuthoritySnapshot authorityBefore = session.CaptureAdventureAuthority();
		RuleRandomState randomBefore = random.CaptureState();
		AdventureRuleCommandResult failed = session.SubmitAdventure(
			new SetAdventureHealthCommand(2, readModelBefore.Revision, 45, 100),
			_ => false);

		False(failed.Accepted);
		Equal(AdventureRunWriteResult.SaveFailed, failed.WriteResult);
		True(ReferenceEquals(readModelBefore, session.AdventureReadModel));
		AdventureAuthoritySnapshot authorityAfter = session.CaptureAdventureAuthority();
		Equal(authorityBefore.Revision, authorityAfter.Revision);
		True(authorityBefore.NormalizedAppliedActionIds.SequenceEqual(
			authorityAfter.NormalizedAppliedActionIds));
		Equal(authorityBefore.Effects, authorityAfter.Effects);
		Equal(randomBefore, random.CaptureState());

		AdventureRuleCommandResult retried = session.SubmitAdventure(
			new SetAdventureHealthCommand(2, readModelBefore.Revision, 45, 100),
			_ => true);
		True(retried.Accepted);
		Equal(readModelBefore.Revision + 1, retried.Revision);
		Equal(45, retried.ReadModel.CurrentHealth);
	}

	private static void AdventurePrepareKeepsAuthorityUnchangedUntilOneCommit()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var random = new TestRuleRandomSource(7401);
		var session = new AuthorityRuleSession(random, MatchRuleProfile.Empty);
		True(session.ConfigureAdventureManifest(manifest));
		True(session.SubmitAdventure(
			new BeginAdventureRunCommand(
				1,
				0,
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1)),
			_ => true).Accepted);

		AdventureRunReadModel readModelBefore = session.AdventureReadModel;
		AdventureAuthoritySnapshot snapshotBefore = session.CaptureAdventureAuthority();
		RuleRandomState randomBefore = random.CaptureState();
		PreparedAdventureResult prepared = session.PrepareAdventure(
			new SetAdventureHealthCommand(2, readModelBefore.Revision, 45, 100));

		True(prepared.Accepted);
		True(prepared.Handle != null);
		True(prepared.PersistenceRequest != null);
		True(prepared.StateChanged);
		Equal(45, prepared.PreviewReadModel.CurrentHealth);
		Equal(readModelBefore.Revision + 1, prepared.PreviewReadModel.Revision);
		Equal(45, prepared.PersistenceRequest!.CandidateSnapshot!.Value.CurrentHealth);
		True(ReferenceEquals(readModelBefore, session.AdventureReadModel));
		AdventureAuthoritySnapshot snapshotAfterPrepare = session.CaptureAdventureAuthority();
		Equal(snapshotBefore.Revision, snapshotAfterPrepare.Revision);
		Equal(
			snapshotBefore.RunSnapshot!.Value.CurrentHealth,
			snapshotAfterPrepare.RunSnapshot!.Value.CurrentHealth);
		True(snapshotBefore.NormalizedAppliedActionIds.SequenceEqual(
			snapshotAfterPrepare.NormalizedAppliedActionIds));
		Equal(snapshotBefore.Effects, snapshotAfterPrepare.Effects);
		Equal(snapshotBefore.NormalizedMatchFlow, snapshotAfterPrepare.NormalizedMatchFlow);
		Equal(randomBefore, random.CaptureState());

		AdventureRuleCommandResult committed = session.CommitPreparedAdventure(prepared.Handle);
		True(committed.Accepted);
		Equal(45, committed.ReadModel.CurrentHealth);
		Equal(readModelBefore.Revision + 1, committed.Revision);
		False(session.HasPreparedAdventure);
		AdventureRuleCommandResult duplicateCommit = session.CommitPreparedAdventure(prepared.Handle);
		False(duplicateCommit.Accepted);
		Equal(RuleRejectionCode.AdventureTransactionMissing, duplicateCommit.Rejection.Code);
		Equal(committed.Revision, session.AdventureReadModel.Revision);
	}

	private static void AdventurePreparedTransactionAbortAndHandleChecksAreAtomic()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var random = new TestRuleRandomSource(7402);
		var session = new AuthorityRuleSession(random, MatchRuleProfile.Empty);
		True(session.ConfigureAdventureManifest(manifest));
		True(session.SubmitAdventure(
			new BeginAdventureRunCommand(
				1,
				0,
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1)),
			_ => true).Accepted);
		AdventureAuthoritySnapshot before = session.CaptureAdventureAuthority();
		RuleRandomState randomBefore = random.CaptureState();
		PreparedAdventureResult prepared = session.PrepareAdventure(
			new SetAdventureHealthCommand(2, before.Revision, 20, 100));
		True(prepared.Accepted);

		var wrongHandle = new AdventurePreparedHandle();
		AdventureRuleCommandResult wrongCommit = session.CommitPreparedAdventure(wrongHandle);
		False(wrongCommit.Accepted);
		Equal(RuleRejectionCode.AdventureTransactionMismatch, wrongCommit.Rejection.Code);
		True(session.HasPreparedAdventure);
		AdventureRuleCommandResult wrongAbort = session.AbortPreparedAdventure(wrongHandle);
		False(wrongAbort.Accepted);
		Equal(RuleRejectionCode.AdventureTransactionMismatch, wrongAbort.Rejection.Code);
		True(session.AbortPreparedAdventure(prepared.Handle).Accepted);
		False(session.HasPreparedAdventure);
		AdventureAuthoritySnapshot afterAbort = session.CaptureAdventureAuthority();
		Equal(before.Revision, afterAbort.Revision);
		Equal(
			before.RunSnapshot!.Value.CurrentHealth,
			afterAbort.RunSnapshot!.Value.CurrentHealth);
		True(before.NormalizedAppliedActionIds.SequenceEqual(
			afterAbort.NormalizedAppliedActionIds));
		Equal(before.Effects, afterAbort.Effects);
		Equal(before.NormalizedMatchFlow, afterAbort.NormalizedMatchFlow);
		Equal(randomBefore, random.CaptureState());

		AdventureRuleCommandResult retried = session.SubmitAdventure(
			new SetAdventureHealthCommand(2, before.Revision, 20, 100),
			_ => true);
		True(retried.Accepted);
		Equal(20, retried.ReadModel.CurrentHealth);
	}

	private static void AdventurePreparedTransactionRejectsConcurrentAndStaleCommit()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var session = new AuthorityRuleSession(
			new TestRuleRandomSource(7403),
			MatchRuleProfile.Empty);
		True(session.ConfigureAdventureManifest(manifest));
		True(session.SubmitAdventure(
			new BeginAdventureRunCommand(
				1,
				0,
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1)),
			_ => true).Accepted);

		AdventureAuthoritySnapshot before = session.CaptureAdventureAuthority();
		PreparedAdventureResult prepared = session.PrepareAdventure(
			new SetAdventureHealthCommand(2, before.Revision, 60, 100));
		True(prepared.Accepted);
		PreparedAdventureResult concurrent = session.PrepareAdventure(
			new SetAdventureHealthCommand(3, before.Revision, 50, 100));
		False(concurrent.Accepted);
		Equal(RuleRejectionCode.AdventureTransactionPending, concurrent.Rejection.Code);

		Equal(
			AdventureUpgradeRestoreResult.Restored,
			session.RestoreAdventureAuthority(before with
			{
				Revision = before.Revision + 1,
			}));
		AdventureRuleCommandResult stale = session.CommitPreparedAdventure(prepared.Handle);
		False(stale.Accepted);
		Equal(RuleRejectionCode.StaleRevision, stale.Rejection.Code);
		Equal(100, session.AdventureReadModel.CurrentHealth);
		True(session.AbortPreparedAdventure(prepared.Handle).Accepted);
	}

	private static void AdventureUnchangedPreparedCommandCommitsActionWithoutRevision()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var session = new AuthorityRuleSession(
			new TestRuleRandomSource(7404),
			MatchRuleProfile.Empty);
		True(session.ConfigureAdventureManifest(manifest));
		True(session.SubmitAdventure(
			new BeginAdventureRunCommand(
				1,
				0,
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1)),
			_ => true).Accepted);
		const string upgradeId = "clubs_gap_rooting";
		True(session.SubmitAdventure(
			new GrantAdventureUpgradeCommand(
				2,
				1,
				upgradeId,
				AdventureUpgradeTriggerSource.None),
			_ => true).Accepted);
		long revisionBefore = session.AdventureReadModel.Revision;

		PreparedAdventureResult prepared = session.PrepareAdventure(
			new GrantAdventureUpgradeCommand(
				3,
				revisionBefore,
				upgradeId,
				AdventureUpgradeTriggerSource.None));
		True(prepared.Accepted);
		False(prepared.StateChanged);
		Equal(revisionBefore, prepared.PreviewReadModel.Revision);
		AdventureUpgradeRunSnapshot previous =
			prepared.PersistenceRequest!.PreviousSnapshot!.Value;
		AdventureUpgradeRunSnapshot candidate =
			prepared.PersistenceRequest.CandidateSnapshot!.Value;
		Equal(previous.CurrentHealth, candidate.CurrentHealth);
		Equal(previous.MainStageIndex, candidate.MainStageIndex);
		True(previous.AcquiredUpgradeIds.SequenceEqual(candidate.AcquiredUpgradeIds));
		AdventureRuleCommandResult committed = session.CommitPreparedAdventure(prepared.Handle);
		True(committed.Accepted);
		Equal(revisionBefore, committed.Revision);

		AdventureRuleCommandResult duplicate = session.SubmitAdventure(
			new SetAdventureHealthCommand(3, revisionBefore, 90, 100),
			_ => true);
		False(duplicate.Accepted);
		Equal(RuleRejectionCode.DuplicateAction, duplicate.Rejection.Code);
	}

	private static void AdventurePreparedTransactionSurvivesMatchRollbackAndCommit()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var random = new TestRuleRandomSource(7405);
		var session = new AuthorityRuleSession(random, MatchRuleProfile.Empty);
		True(session.ConfigureAdventureManifest(manifest));
		True(session.SubmitAdventure(
			new BeginAdventureRunCommand(
				1,
				0,
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1)),
			_ => true).Accepted);
		True(session.Submit(CreateInitializeCommand(7410)).Accepted);
		MatchStateSnapshot rollbackSnapshot = session.CaptureSnapshot();
		MatchReadModel beforeScore = session.ReadModel;
		True(session.Submit(new ApplyScoreMutationsCommand(
			7411,
			beforeScore.Revision,
			beforeScore.LifecycleGeneration,
			beforeScore.TurnGeneration,
			ImmutableArray.Create(
				new RuleScoreMutation(0, RuleScoreMutationKind.Add, 7)))).Accepted);
		MatchStateSnapshot committedMatchSnapshot = session.CaptureSnapshot();
		_ = random.NextIntInclusive(0, 100);
		RuleRandomState preparedRandomState = random.CaptureState();
		AdventureRunReadModel adventureBefore = session.AdventureReadModel;
		PreparedAdventureResult prepared = session.PrepareAdventure(
			new SetAdventureHealthCommand(2, adventureBefore.Revision, 45, 100));
		True(prepared.Accepted);
		AdventurePreparedHandle handle = prepared.Handle!;
		AdventureUpgradeRunSnapshot candidate =
			prepared.PersistenceRequest!.CandidateSnapshot!.Value;

		session.RestoreMatchSnapshotPreservingAdventureTransaction(rollbackSnapshot);
		True(session.HasPreparedAdventure);
		True(ReferenceEquals(adventureBefore, session.AdventureReadModel));
		Equal(rollbackSnapshot.Revision, session.ReadModel.Revision);
		Equal(0, session.ReadModel.Players[0].Score);
		Equal(preparedRandomState, random.CaptureState());
		Equal(45, candidate.CurrentHealth);

		AdventureRuleCommandResult committed = session.CommitPreparedAdventure(handle);
		True(committed.Accepted);
		False(session.HasPreparedAdventure);
		Equal(prepared.PreviewReadModel.Revision, committed.Revision);
		Equal(candidate.CurrentHealth, committed.ReadModel.CurrentHealth);
		True(session.CaptureAdventureAuthority().NormalizedAppliedActionIds.Contains(2));
		session.RestoreMatchSnapshotPreservingAdventureTransaction(committedMatchSnapshot);
		Equal(7, session.ReadModel.Players[0].Score);
		Equal(committed.ReadModel, session.ReadModel.Adventure);
		Equal(preparedRandomState, random.CaptureState());
	}

	private static void AdventurePreparedTransactionSurvivesMatchRollbackAndAbort()
	{
		AdventureUpgradeManifest manifest = CreateFullAdventureUpgradeManifest();
		var random = new TestRuleRandomSource(7406);
		var session = new AuthorityRuleSession(random, MatchRuleProfile.Empty);
		True(session.ConfigureAdventureManifest(manifest));
		True(session.SubmitAdventure(
			new BeginAdventureRunCommand(
				1,
				0,
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1)),
			_ => true).Accepted);
		True(session.Submit(CreateInitializeCommand(7420)).Accepted);
		MatchStateSnapshot rollbackSnapshot = session.CaptureSnapshot();
		_ = random.NextIntInclusive(0, 100);
		RuleRandomState preparedRandomState = random.CaptureState();
		AdventureAuthoritySnapshot adventureBefore = session.CaptureAdventureAuthority();
		PreparedAdventureResult prepared = session.PrepareAdventure(
			new SetAdventureHealthCommand(2, adventureBefore.Revision, 20, 100));
		True(prepared.Accepted);

		session.RestoreMatchSnapshotPreservingAdventureTransaction(rollbackSnapshot);
		True(session.HasPreparedAdventure);
		True(session.AbortPreparedAdventure(prepared.Handle).Accepted);
		False(session.HasPreparedAdventure);
		AdventureAuthoritySnapshot afterAbort = session.CaptureAdventureAuthority();
		Equal(adventureBefore.Revision, afterAbort.Revision);
		Equal(
			adventureBefore.RunSnapshot!.Value.CurrentHealth,
			afterAbort.RunSnapshot!.Value.CurrentHealth);
		True(adventureBefore.NormalizedAppliedActionIds.SequenceEqual(
			afterAbort.NormalizedAppliedActionIds));
		Equal(preparedRandomState, random.CaptureState());
	}

}
