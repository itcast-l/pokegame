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

}
