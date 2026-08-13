using System.Collections.Immutable;
using System.Reflection;
using Deckland.Rules;

internal static partial class Program
{
	private static void RunRuleAssemblyFoundationTests()
	{
		Run("rules assembly has no Godot reference", RulesAssemblyHasNoGodotReference);
		Run("rules public signatures have no Godot types", RulesPublicSignaturesHaveNoGodotTypes);
		Run("rules behavior is exposed only through sessions and read model", RulesBehaviorIsExposedOnlyThroughSessionsAndReadModel);
		Run("rules projects enforce the compile boundary", RulesProjectsEnforceCompileBoundary);
		Run("core state models and services come from rules assembly", CoreStateModelsAndServicesComeFromRulesAssembly);
		Run("slice five AI rules come from rules assembly", SliceFiveAiRulesComeFromRulesAssembly);
		Run("game board selects authority or replica state by role", GameBoardSelectsAuthorityOrReplicaStateByRole);
		Run("card trigger dispatch state stays inside authority session", CardTriggerDispatchStateStaysInsideAuthoritySession);
		Run("adventure production state stays behind authority session", AdventureProductionStateStaysBehindAuthoritySession);
		Run("adventure authority handoff preserves transient state", AdventureAuthorityHandoffPreservesTransientState);
		Run("adventure persisted restore drops transient state", AdventurePersistedRestoreDropsTransientState);
		Run("adventure persistence failure preserves authority atomically", AdventurePersistenceFailurePreservesAuthorityAtomically);
		Run("rule profile validates deterministic module ordering", RuleProfileValidatesDeterministicModuleOrdering);
		Run("authority session rejects stale duplicate and blocked commands", AuthoritySessionRejectsStaleDuplicateAndBlockedCommands);
		Run("authority snapshot restores action and random continuity", AuthoritySnapshotRestoresActionAndRandomContinuity);
		Run("authority snapshot restores question refresh state atomically", AuthoritySnapshotRestoresQuestionRefreshStateAtomically);
		Run("match flow command validates and replaces state atomically", MatchFlowCommandValidatesAndReplacesStateAtomically);
		Run("match flow state survives authority and replica snapshots", MatchFlowStateSurvivesAuthorityAndReplicaSnapshots);
		Run("automated item block is rule owned and clears with turn lifecycle", AutomatedItemBlockIsRuleOwnedAndClearsWithTurnLifecycle);
		Run("replica session only applies newer authority snapshots", ReplicaSessionOnlyAppliesNewerAuthoritySnapshots);
			Run("fixed random source produces stable initial deal", FixedRandomSourceProducesStableInitialDeal);
			Run("logical card code survives attachment and snapshot restore", LogicalCardCodeSurvivesAttachmentAndSnapshotRestore);
			Run("restored runtime IDs advance future allocators", RestoredRuntimeIdsAdvanceFutureAllocators);
			Run("restart command advances lifecycle without accepting stale initialization", RestartCommandAdvancesLifecycleWithoutAcceptingStaleInitialization);
		Run("match initialization preserves configured starting scores", MatchInitializationPreservesConfiguredStartingScores);
		Run("invalid starting scores are rejected atomically", InvalidStartingScoresAreRejectedAtomically);
		Run("match initialization applies starting draw and supply limits", MatchInitializationAppliesStartingDrawAndSupplyLimits);
		Run("invalid starting draw options are rejected atomically", InvalidStartingDrawOptionsAreRejectedAtomically);
		Run("scripted initialization validates and applies one atomic state", ScriptedInitializationValidatesAndAppliesOneAtomicState);
		Run("question refresh state changes with capture and turn commands", QuestionRefreshStateChangesWithCaptureAndTurnCommands);
		Run("question card expires after two completed turn commands", QuestionCardExpiresAfterTwoCompletedTurnCommands);
		Run("question card age survives a capture that leaves it on the table", QuestionCardAgeSurvivesUnrelatedCapture);
		Run("turn generation advances only through its rule command", TurnGenerationAdvancesOnlyThroughItsRuleCommand);
		Run("turn generation advance cancels an obsolete presentation barrier", TurnGenerationAdvanceCancelsObsoletePresentationBarrier);
		Run("authority card play capture score and outcome commands are ordered", AuthorityCardPlayCaptureScoreAndOutcomeCommandsAreOrdered);
		Run("authority gameplay commands reject illegal stale and duplicate actions atomically", AuthorityGameplayCommandsRejectIllegalStaleAndDuplicateActionsAtomically);
		Run("authority draw and turn commands preserve zone and turn invariants", AuthorityDrawAndTurnCommandsPreserveZoneAndTurnInvariants);
		Run("authority return-to-hand command is ordered and atomic", AuthorityReturnToHandCommandIsOrderedAndAtomic);
		Run("authority table placement accepts a suitless special card", AuthorityTablePlacementAcceptsSuitlessSpecialCard);
		Run("base table face-state command does not require item rules", BaseTableFaceStateCommandDoesNotRequireItemRules);
			Run("authority item play grant discard and attachment commands are atomic", AuthorityItemCommandsAreAtomic);
			Run("authority item random selection advances only through commands", AuthorityItemRandomSelectionAdvancesOnlyThroughCommands);
			Run("authority gameplay random commands preserve sequence and atomicity", AuthorityGameplayRandomCommandsPreserveSequenceAndAtomicity);
			Run("slice three gameplay randomness stays behind rule commands", SliceThreeGameplayRandomnessStaysBehindRuleCommands);
			Run("authority item transfers are ordered atomic and deterministic", AuthorityItemTransfersAreOrderedAtomicAndDeterministic);
				Run("authority table item commands are ordered atomic and deterministic", AuthorityTableItemCommandsAreOrderedAtomicAndDeterministic);
				Run("authority table shuffle plan survives barriers and snapshots", AuthorityTableShufflePlanSurvivesBarriersAndSnapshots);
				Run("slice four random commands preserve exact sequence and atomicity", SliceFourRandomCommandsPreserveExactSequenceAndAtomicity);
				Run("slice five AI random command sequence is deterministic and atomic", SliceFiveAiRandomCommandSequenceIsDeterministicAndAtomic);
				Run("slice four table commands validate atomically", SliceFourTableCommandsValidateAtomically);
				Run("authority mandatory discard selection is gated and atomic", AuthorityMandatoryDiscardSelectionIsGatedAndAtomic);
				Run("authority mandatory attachment and clear are atomic", AuthorityMandatoryAttachmentAndClearAreAtomic);
				Run("mandatory selection snapshots validate and restore", MandatorySelectionSnapshotsValidateAndRestore);
				Run("pending attachment reward queue is ordered atomic and restorable", PendingAttachmentRewardQueueIsOrderedAtomicAndRestorable);
				Run("extra action commands are ordered atomic and restorable", ExtraActionCommandsAreOrderedAtomicAndRestorable);
				Run("slice four gameplay boundaries stay behind neutral rule commands", SliceFourGameplayBoundariesStayBehindNeutralRuleCommands);
				Run("slice five AI randomness stays behind rule commands", SliceFiveAiRandomnessStaysBehindRuleCommands);
				Run("authority score mutations validate the whole batch before applying", AuthorityScoreMutationsValidateTheWholeBatchBeforeApplying);
		Run("authority outcome command resolves equal score exhaustion as draw", AuthorityOutcomeCommandResolvesEqualScoreExhaustionAsDraw);
	}

	private static void RulesAssemblyHasNoGodotReference()
	{
		Assembly rulesAssembly = typeof(AuthorityRuleSession).Assembly;
		Equal("Deckland.Rules", rulesAssembly.GetName().Name);
		foreach (AssemblyName reference in rulesAssembly.GetReferencedAssemblies())
		{
			False(reference.Name?.Contains("Godot", StringComparison.OrdinalIgnoreCase) == true);
		}
	}

	private static void RulesPublicSignaturesHaveNoGodotTypes()
	{
		Assembly rulesAssembly = typeof(AuthorityRuleSession).Assembly;
		foreach (Type type in rulesAssembly.GetExportedTypes())
		{
			AssertNoForbiddenGodotType(type.BaseType);
			foreach (Type implementedInterface in type.GetInterfaces())
			{
				AssertNoForbiddenGodotType(implementedInterface);
			}

			foreach (ConstructorInfo constructor in type.GetConstructors())
			{
				foreach (ParameterInfo parameter in constructor.GetParameters())
				{
					AssertNoForbiddenGodotType(parameter.ParameterType);
				}
			}

			foreach (PropertyInfo property in type.GetProperties())
			{
				AssertNoForbiddenGodotType(property.PropertyType);
			}

			foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public))
			{
				AssertNoForbiddenGodotType(method.ReturnType);
				foreach (ParameterInfo parameter in method.GetParameters())
				{
					AssertNoForbiddenGodotType(parameter.ParameterType);
				}
			}
		}
	}

	private static void RulesBehaviorIsExposedOnlyThroughSessionsAndReadModel()
	{
		foreach (Type implementationType in new[]
		{
			typeof(AiCardSelectionRules),
			typeof(CaptureFlowPlanner),
			typeof(CaptureResolver),
			typeof(CaptureRevealOrder),
			typeof(CaptureScoringRules),
			typeof(CardPlayResolver),
			typeof(EnemyAiRules),
			typeof(GameLoopPhaseRules),
			typeof(GameTurnRules),
			typeof(MatchOutcomeRules),
			typeof(NetworkAiFallbackRules),
			typeof(PotentialCaptureRules),
			typeof(SoloAiRules),
			typeof(TurnGenerationRules),
			typeof(CardTriggerDispatchState),
		})
		{
			False(implementationType.IsPublic);
		}
		True(typeof(AuthorityRuleSession).IsPublic);
		True(typeof(ReplicaRuleSession).IsPublic);
		True(typeof(MatchReadModel).IsPublic);
	}

	private static void CardTriggerDispatchStateStaysInsideAuthoritySession()
	{
		Assembly rulesAssembly = typeof(AuthorityRuleSession).Assembly;
		True(ReferenceEquals(rulesAssembly, typeof(CardGameEvent).Assembly));
		True(ReferenceEquals(rulesAssembly, typeof(CardTriggerRequest).Assembly));
		True(ReferenceEquals(rulesAssembly, typeof(ItemCardEffectRule).Assembly));
		False(typeof(CardTriggerDispatchState).IsPublic);

		string root = FindRepositoryRoot();
		string compatibilitySource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"SpecialCardSystem.cs"));
		string boardSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.cs"));
		string adapterSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.SpecialCards.cs"));
		False(compatibilitySource.Contains("class CardEventBus", StringComparison.Ordinal));
		False(compatibilitySource.Contains("Vector2", StringComparison.Ordinal));
		False(boardSource.Contains("_cardEventBus", StringComparison.Ordinal));
		True(adapterSource.Contains(
			"ruleSession.BeginCardTriggerDispatch()",
			StringComparison.Ordinal));
		True(adapterSource.Contains(
			"new CardEventPosition(",
			StringComparison.Ordinal));

		var extraIndexes = new List<int> { 2, 4 };
		var gameEvent = new CardGameEvent(
			"test",
			extraTableCardIndexes: extraIndexes,
			originGlobalCenter: new CardEventPosition(12.5f, 18.25f));
		extraIndexes.Clear();
		True(gameEvent.ExtraTableCardIndexes.SequenceEqual(new[] { 2, 4 }));
		Equal(new CardEventPosition(12.5f, 18.25f), gameEvent.OriginGlobalCenter!.Value);
	}

	private static void RulesProjectsEnforceCompileBoundary()
	{
		string root = FindRepositoryRoot();
		string mainProject = File.ReadAllText(Path.Combine(root, "pokegame.csproj"));
		string testProject = File.ReadAllText(Path.Combine(root, "tests", "Rules.Tests", "Rules.Tests.csproj"));
		string rulesProject = File.ReadAllText(Path.Combine(root, "src", "Deckland.Rules", "Deckland.Rules.csproj"));

		True(mainProject.Contains("<Compile Remove=\"src/Deckland.Rules/**/*.cs\" />", StringComparison.Ordinal));
		True(mainProject.Contains("<ProjectReference Include=\"src/Deckland.Rules/Deckland.Rules.csproj\" />", StringComparison.Ordinal));
		True(testProject.Contains("<ProjectReference Include=\"../../src/Deckland.Rules/Deckland.Rules.csproj\" />", StringComparison.Ordinal));
		False(rulesProject.Contains("Godot.NET.Sdk", StringComparison.Ordinal));
		False(rulesProject.Contains("GodotSharp", StringComparison.Ordinal));
	}

	private static void CoreStateModelsAndServicesComeFromRulesAssembly()
	{
		Assembly rulesAssembly = typeof(AuthorityRuleSession).Assembly;
		foreach (Type type in new[]
		{
			typeof(GameBoardState),
			typeof(PlayingCard),
			typeof(ItemCard),
			typeof(TableCard),
			typeof(CardPlayStateService),
			typeof(CaptureStateService),
			typeof(TurnAdvancementService),
			typeof(ScoreService),
		})
		{
			True(ReferenceEquals(rulesAssembly, type.Assembly));
		}
	}

	private static void SliceFiveAiRulesComeFromRulesAssembly()
	{
		Assembly rulesAssembly = typeof(AuthorityRuleSession).Assembly;
		foreach (Type type in new[]
		{
			typeof(PotentialCaptureRules),
			typeof(AiCardSelectionRules),
			typeof(EnemyAiRules),
			typeof(SoloAiRules),
			typeof(NetworkAiFallbackRules),
		})
		{
			True(ReferenceEquals(rulesAssembly, type.Assembly));
		}

		string root = FindRepositoryRoot();
		string testProject = File.ReadAllText(Path.Combine(
			root,
			"tests",
			"Rules.Tests",
			"Rules.Tests.csproj"));
		foreach (string sourceName in new[]
		{
			"PotentialCaptureRules.cs",
			"AiCardSelectionRules.cs",
			"EnemyAiRules.cs",
			"SoloAiRules.cs",
			"NetworkAiFallbackRules.cs",
		})
		{
			False(testProject.Contains(sourceName, StringComparison.Ordinal));
			True(File.Exists(Path.Combine(root, "src", "Deckland.Rules", sourceName)));
			False(File.Exists(Path.Combine(root, "scripts", sourceName)));
		}
	}

	private static void GameBoardSelectsAuthorityOrReplicaStateByRole()
	{
		string root = FindRepositoryRoot();
		string boardSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.cs"));
		string adapterSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.RuleSessionAdapter.cs"));
		string assemblyInfoSource = File.ReadAllText(Path.Combine(
			root,
			"src",
			"Deckland.Rules",
			"AssemblyInfo.cs"));

		False(boardSource.Contains("new AuthorityRuleSession", StringComparison.Ordinal));
		True(adapterSource.Contains("IsNetworkMultiplayerMode() && !IsNetworkHost()", StringComparison.Ordinal));
		True(adapterSource.Contains("_replicaRuleSession = new ReplicaRuleSession()", StringComparison.Ordinal));
		True(adapterSource.Contains("_authorityRuleSession = new AuthorityRuleSession(", StringComparison.Ordinal));
		True(adapterSource.Contains("_replicaRuleSession.ApplyAuthoritySnapshot(ruleSnapshot)", StringComparison.Ordinal));
		True(boardSource.Contains(
			"private IReadOnlyList<IReadOnlyList<PlayingCard>> _playerHands =>",
			StringComparison.Ordinal));
		True(boardSource.Contains(
			": CurrentRuleProjection.PlayerHands;",
			StringComparison.Ordinal));
		False(boardSource.Contains("GameBoardState _state", StringComparison.Ordinal));
		False(adapterSource.Contains("_state", StringComparison.Ordinal));
		False(typeof(AuthorityRuleSession).GetProperties(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Any(property => property.Name == "State"));
		False(typeof(ReplicaRuleSession).GetProperties(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Any(property => property.Name == "State"));
		False(assemblyInfoSource.Contains(
			"InternalsVisibleTo(\"pokegame\")",
			StringComparison.Ordinal));
		True(assemblyInfoSource.Contains(
			"InternalsVisibleTo(\"Rules.Tests\")",
			StringComparison.Ordinal));
		False(typeof(GameBoardState).IsPublic);
		False(typeof(CardPlayStateService).IsPublic);
		False(typeof(CaptureStateService).IsPublic);
		False(typeof(TurnAdvancementService).IsPublic);
		False(typeof(ScoreService).IsPublic);
		False(typeof(TurnStateMachine).IsPublic);
		foreach (string legacySceneFlag in new[]
		{
			"IsLeavingGame",
			"IsProcessingCardPlay",
			"IsResolvingTurnSequence",
			"IsOpeningDealAnimationPlaying",
			"IsResolvingMagnetAttachmentSelection",
			"IsAnimatingMagnetAttachment",
		})
		{
			False(typeof(GameBoardState).GetProperty(legacySceneFlag) != null);
		}

		foreach (string sourcePath in Directory.GetFiles(
			Path.Combine(root, "scripts"),
			"GameBoardScene*.cs",
			SearchOption.TopDirectoryOnly))
		{
			if (Path.GetFileName(sourcePath).Contains(".Debug.", StringComparison.Ordinal))
			{
				continue;
			}

			string source = File.ReadAllText(sourcePath);
			False(source.Contains("GameBoardState", StringComparison.Ordinal));
			False(System.Text.RegularExpressions.Regex.IsMatch(
				source,
				@"_(?:playerDecks|playerHands|playerItemHands|tableCards|playerScores)(?:\s*\[[^\]]+\])?\s*\.\s*(?:Add|AddRange|Insert|Remove|RemoveAt|RemoveRange|Clear|Sort|Reverse)\s*\("));
			False(System.Text.RegularExpressions.Regex.IsMatch(
				source,
				@"_(?:playerDecks|playerHands|playerItemHands|tableCards|playerScores)\s*\[[^\r\n;]+\]\s*="));
		}
	}

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

	private static void AuthorityItemCommandsAreAtomic()
	{
		AuthorityRuleSession disabledSession = CreateItemCommandSession(itemRulesEnabled: false);
		MatchReadModel disabledBefore = disabledSession.ReadModel;
		RuleCommandResult disabled = disabledSession.Submit(new PlayItemFromHandCommand(
			251,
			disabledBefore.Revision,
			disabledBefore.LifecycleGeneration,
			disabledBefore.TurnGeneration,
			Seat: 0,
			ItemHandIndex: 0,
			ExpectedItemId: "bomb"));
		False(disabled.Accepted);
		Equal(RuleRejectionCode.ModuleRejected, disabled.Rejection.Code);
		AssertReadModelsEquivalent(disabledBefore, disabled.ReadModel);

		AuthorityRuleSession session = CreateItemCommandSession(itemRulesEnabled: true);
		MatchReadModel before = session.ReadModel;
		RuleCommandResult wrongItem = session.Submit(new PlayItemFromHandCommand(
			252,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			ItemHandIndex: 0,
			ExpectedItemId: "lens"));
		False(wrongItem.Accepted);
		AssertReadModelsEquivalent(before, wrongItem.ReadModel);

		RuleCommandResult played = session.Submit(new PlayItemFromHandCommand(
			253,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			ItemHandIndex: 0,
			ExpectedItemId: "bomb",
			Destination: RuleItemPlayDestination.Table,
			TableIndex: 0,
			TableItemFacesUp: false));
		True(played.Accepted);
		Equal(1, played.ReadModel.Players[0].ItemHand.Length);
		Equal("bomb", played.ReadModel.TableCards[0].ItemOverlay!.Value.Id);
		Equal(PresentationIntentKind.ItemPlayed, played.PresentationIntents[0].Kind);

		RuleCommandResult invalidGrant = session.Submit(new GrantItemToHandCommand(
			254,
			played.Revision,
			played.ReadModel.LifecycleGeneration,
			played.TurnGeneration,
			Seat: 0,
			ItemId: "reward",
			MaximumHandSize: 3,
			DiscardDisplayIndex: 0,
			ExpectedDiscardCardRuntimeInstanceId: 999));
		False(invalidGrant.Accepted);
		AssertReadModelsEquivalent(played.ReadModel, invalidGrant.ReadModel);

		RuleCommandResult granted = session.Submit(new GrantItemToHandCommand(
			255,
			played.Revision,
			played.ReadModel.LifecycleGeneration,
			played.TurnGeneration,
			Seat: 0,
			ItemId: "reward",
			MaximumHandSize: 3,
			DiscardDisplayIndex: 0,
			ExpectedDiscardCardRuntimeInstanceId: 1));
		True(granted.Accepted);
		Equal(1, granted.ReadModel.Players[0].Hand.Length);
		Equal(2, granted.ReadModel.Players[0].ItemHand.Length);
		Equal("reward", granted.ReadModel.Players[0].ItemHand[1].Id);
		Equal(PresentationIntentKind.HandCardDiscarded, granted.PresentationIntents[0].Kind);
		Equal(PresentationIntentKind.ItemGranted, granted.PresentationIntents[1].Kind);

		RuleCommandResult attached = session.Submit(new AttachItemToHandCardCommand(
			256,
			granted.Revision,
			granted.ReadModel.LifecycleGeneration,
			granted.TurnGeneration,
			Seat: 0,
			HandIndex: 0,
			ItemId: "magnet"));
		True(attached.Accepted);
		Equal(2L, attached.ReadModel.Players[0].Hand[0].RuntimeInstanceId);
		Equal("magnet", attached.ReadModel.Players[0].Hand[0].NormalizedAttachedEffectIds.Single());

		RuleCommandResult replaced = session.Submit(new ReplaceItemInHandCommand(
			257,
			attached.Revision,
			attached.ReadModel.LifecycleGeneration,
			attached.TurnGeneration,
			Seat: 0,
			ItemHandIndex: 0,
			ExpectedItemId: "lens",
			ReplacementItemId: "rerolled"));
		True(replaced.Accepted);
		Equal("rerolled", replaced.ReadModel.Players[0].ItemHand[0].Id);

		int rewardDisplayIndex = replaced.ReadModel.Players[0].Hand.Length + 1;
		RuleCommandResult discarded = session.Submit(new DiscardHandCardCommand(
			258,
			replaced.Revision,
			replaced.ReadModel.LifecycleGeneration,
			replaced.TurnGeneration,
			Seat: 0,
			DisplayIndex: rewardDisplayIndex,
			ExpectedItemId: "reward"));
		True(discarded.Accepted);
		Equal(1, discarded.ReadModel.Players[0].ItemHand.Length);
		Equal("reward", discarded.Mutation.LogicalItemId);
	}

	private static void AuthorityItemRandomSelectionAdvancesOnlyThroughCommands()
	{
		var random = new TestRuleRandomSource(77);
		AuthorityRuleSession session = CreateItemCommandSession(
			itemRulesEnabled: true,
			random);
		MatchReadModel before = session.ReadModel;
		RuleRandomState randomBefore = random.CaptureState();
		RuleCommandResult invalid = session.Submit(new ChooseRandomHandCardIndexCommand(
			261,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			CandidateHandIndexes: ImmutableArray.Create(0, 99)));
		False(invalid.Accepted);
		Equal(randomBefore, random.CaptureState());
		AssertReadModelsEquivalent(before, invalid.ReadModel);

		RuleCommandResult selected = session.Submit(new ChooseRandomHandCardIndexCommand(
			262,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			CandidateHandIndexes: ImmutableArray.Create(0, 1)));
		True(selected.Accepted);
		True(selected.Mutation.SourceIndex is 0 or 1);
		False(randomBefore.Equals(random.CaptureState()));
		Equal(2, selected.ReadModel.Players[0].Hand.Length);
	}

	private static void AuthorityGameplayRandomCommandsPreserveSequenceAndAtomicity()
	{
		var random = new TestRuleRandomSource(79);
		var expectedRandom = new TestRuleRandomSource(79);
		var session = new AuthorityRuleSession(random, CreateItemRuleProfile());
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray.Create(
					new LogicalCardState(10, "clubs:2", "clubs", "2"),
					new LogicalCardState(11, "clubs:3", "clubs", "3"),
					new LogicalCardState(12, "clubs:4", "clubs", "4"),
					new LogicalCardState(13, "clubs:5", "clubs", "5")),
				ImmutableArray.Create(new LogicalCardState(1, "hearts:4", "hearts", "4")),
				ImmutableArray<LogicalItemState>.Empty,
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(new LogicalCardState(3, "spades:9", "spades", "9")),
				ImmutableArray<LogicalItemState>.Empty,
				0));
		var table = ImmutableArray.Create(
			CreateRuleTableCardState(100, 20, "diamonds:2"),
			CreateRuleTableCardState(101, 21, "diamonds:3"),
			CreateRuleTableCardState(102, 22, "diamonds:4"));
		True(session.Submit(new InitializeScriptedMatchCommand(
			263,
			0,
			1,
			0,
			2,
			100,
			players,
			table)).Accepted);

		MatchReadModel beforeInvalid = session.ReadModel;
		RuleRandomState randomBeforeInvalid = random.CaptureState();
		RuleCommandResult invalidWeighted = session.Submit(new ChooseWeightedOptionCommand(
			264,
			beforeInvalid.Revision,
			beforeInvalid.LifecycleGeneration,
			beforeInvalid.TurnGeneration,
			ActingSeat: 0,
			Options: ImmutableArray.Create(
				new RuleWeightedOption("bomb", 1),
				new RuleWeightedOption("bomb", 2))));
		False(invalidWeighted.Accepted);
		Equal(randomBeforeInvalid, random.CaptureState());
		AssertReadModelsEquivalent(beforeInvalid, invalidWeighted.ReadModel);

		RuleCommandResult targetSeat = session.Submit(new ChooseRandomTargetSeatCommand(
			265,
			beforeInvalid.Revision,
			beforeInvalid.LifecycleGeneration,
			beforeInvalid.TurnGeneration,
			ActingSeat: 0,
			CandidateSeats: ImmutableArray.Create(1)));
		True(targetSeat.Accepted);
		Equal(1, targetSeat.Mutation.SourceIndex);
		expectedRandom.NextIntInclusive(0, 0);
		Equal(expectedRandom.CaptureState(), random.CaptureState());

		RuleCommandResult targetHand = session.Submit(new ChooseRandomHandDisplayIndexCommand(
			266,
			targetSeat.Revision,
			targetSeat.ReadModel.LifecycleGeneration,
			targetSeat.TurnGeneration,
			ActingSeat: 0,
			HandSeat: 1));
		True(targetHand.Accepted);
		Equal(0, targetHand.Mutation.SourceIndex);
		expectedRandom.NextIntInclusive(0, 0);
		Equal(expectedRandom.CaptureState(), random.CaptureState());

		string expectedWeightedId = string.Empty;
		if (expectedRandom.NextIntInclusive(1, 2) <= 2)
		{
			expectedWeightedId = "bomb";
		}
		if (expectedRandom.NextIntInclusive(1, 5) <= 3)
		{
			expectedWeightedId = "lens";
		}
		RuleCommandResult weighted = session.Submit(new ChooseWeightedOptionCommand(
			267,
			targetHand.Revision,
			targetHand.ReadModel.LifecycleGeneration,
			targetHand.TurnGeneration,
			ActingSeat: 0,
			Options: ImmutableArray.Create(
				new RuleWeightedOption("bomb", 2),
				new RuleWeightedOption("lens", 3),
				new RuleWeightedOption("magnet", 0))));
		True(weighted.Accepted);
		Equal(expectedWeightedId, weighted.Mutation.LogicalItemId);
		Equal(expectedRandom.CaptureState(), random.CaptureState());

		int expectedTableIndex = 0;
		if (expectedRandom.NextIntInclusive(1, 1) == 1)
		{
			expectedTableIndex = 0;
		}
		if (expectedRandom.NextIntInclusive(1, 2) == 1)
		{
			expectedTableIndex = 2;
		}
		RuleCommandResult tableCard = session.Submit(new ChooseRandomTableCardIndexCommand(
			268,
			weighted.Revision,
			weighted.ReadModel.LifecycleGeneration,
			weighted.TurnGeneration,
			ActingSeat: 0,
			Candidates: ImmutableArray.Create(
				new RuleTableCardReference(0, 100),
				new RuleTableCardReference(2, 102))));
		True(tableCard.Accepted);
		Equal(expectedTableIndex, tableCard.Mutation.SourceIndex);
		Equal(expectedRandom.CaptureState(), random.CaptureState());

		var expectedDeck = tableCard.ReadModel.Players[0].Deck
			.Select(card => card.RuntimeInstanceId)
			.ToList();
		for (int index = expectedDeck.Count - 1; index > 0; index--)
		{
			int swapIndex = expectedRandom.NextIntInclusive(0, index);
			(expectedDeck[index], expectedDeck[swapIndex]) = (expectedDeck[swapIndex], expectedDeck[index]);
		}
		int cutIndex = expectedRandom.NextIntInclusive(1, expectedDeck.Count - 1);
		expectedDeck.Reverse(0, cutIndex);
		expectedDeck.Reverse(cutIndex, expectedDeck.Count - cutIndex);
		expectedDeck.Reverse();

		RuleCommandResult shuffled = session.Submit(new ShuffleDeckCommand(
			269,
			tableCard.Revision,
			tableCard.ReadModel.LifecycleGeneration,
			tableCard.TurnGeneration,
			ActingSeat: 0,
			DeckSeat: 0,
			CutAfterShuffle: true));
		True(shuffled.Accepted);
		True(expectedDeck.SequenceEqual(
			shuffled.ReadModel.Players[0].Deck.Select(card => card.RuntimeInstanceId)));
		Equal(expectedRandom.CaptureState(), random.CaptureState());
	}

	private static void SliceThreeGameplayRandomnessStaysBehindRuleCommands()
	{
		string root = FindRepositoryRoot();
		string questionCards = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.QuestionCards.cs"));
		string targetItems = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.TargetItemTargeting.cs"));
		string tableTriggers = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.TableCardTriggers.cs"));
		string capture = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.CaptureResolutionCoordinator.cs"));
		string tableCapture = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.TableCapture.cs"));
		string effectResolver = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.EffectResolver.cs"));
		string deck = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.Deck.cs"));

		foreach (string source in new[]
		{
			questionCards,
			targetItems,
			tableTriggers,
			capture,
			effectResolver,
		})
		{
			False(source.Contains("_rng.", StringComparison.Ordinal));
		}

		True(questionCards.Contains(
			"TrySubmitChooseWeightedOptionRuleCommand(",
			StringComparison.Ordinal));
		True(targetItems.Contains(
			"TrySubmitChooseRandomTargetSeatRuleCommand(",
			StringComparison.Ordinal));
		True(targetItems.Contains(
			"TrySubmitChooseRandomHandDisplayIndexRuleCommand(",
			StringComparison.Ordinal));
		True(tableTriggers.Contains(
			"TrySubmitChooseRandomTableCardIndexRuleCommand(",
			StringComparison.Ordinal));
			True(tableTriggers.Contains(
				"TrySubmitSetTableCardsFaceUpRuleCommand(",
				StringComparison.Ordinal));
			False(tableTriggers.Contains("tableCard.IsFaceUp = true", StringComparison.Ordinal));
			True(tableTriggers.Contains(
				"TrySubmitRemoveTableCardAttachmentRuleCommand(",
				StringComparison.Ordinal));
			False(tableTriggers.Contains("ClearAttachedItemEffect(", StringComparison.Ordinal));
			False(tableTriggers.Contains(".IsHiddenDuringCapture =", StringComparison.Ordinal));
			True(tableCapture.Contains(
				"TrySubmitSetTableCardsFaceUpRuleCommand(",
				StringComparison.Ordinal));
			False(tableCapture.Contains(".IsFaceUp = true", StringComparison.Ordinal));
		True(capture.Contains(
			"TrySubmitShuffleDeckRuleCommand(",
			StringComparison.Ordinal));
		True(effectResolver.Contains(
			"TrySubmitShuffleDeckRuleCommand(",
			StringComparison.Ordinal));
		False(capture.Contains("Shuffle(_playerDecks", StringComparison.Ordinal));
		False(effectResolver.Contains("Shuffle(_playerDecks", StringComparison.Ordinal));
		False(deck.Contains("private void Shuffle(", StringComparison.Ordinal));
		False(deck.Contains("private void Cut(", StringComparison.Ordinal));
	}

	private static void AuthorityItemTransfersAreOrderedAtomicAndDeterministic()
	{
		var random = new TestRuleRandomSource(81);
		var profile = new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule(
				"items",
				0,
				capabilities: new MatchCapabilitySet(MatchCapability.ItemRules)),
		});
		var session = new AuthorityRuleSession(random, profile);
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray.Create(new LogicalCardState(10, "clubs:2", "clubs", "2")),
				ImmutableArray.Create(new LogicalCardState(1, "clubs:4", "clubs", "4")),
				ImmutableArray<LogicalItemState>.Empty,
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray.Create(
					new LogicalCardState(20, "hearts:5", "hearts", "5", ImmutableArray.Create("magnet")),
					new LogicalCardState(21, "spades:6", "spades", "6"),
					new LogicalCardState(22, "diamonds:7", "diamonds", "7")),
				ImmutableArray.Create(new LogicalCardState(3, "spades:9", "spades", "9")),
				ImmutableArray.Create(new LogicalItemState("lens")),
				0));
		True(session.Submit(new InitializeScriptedMatchCommand(
			270,
			0,
			1,
			0,
			2,
			100,
			players,
			ImmutableArray<TableCardState>.Empty)).Accepted);

		MatchReadModel beforeInvalidHand = session.ReadModel;
		RuleRandomState randomBeforeInvalidHand = random.CaptureState();
		RuleCommandResult invalidHand = session.Submit(new TransferHandCardCommand(
			271,
			beforeInvalidHand.Revision,
			beforeInvalidHand.LifecycleGeneration,
			beforeInvalidHand.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			SourceDisplayIndex: 0,
			MaximumHandSize: 4,
			ExpectedCardRuntimeInstanceId: 999));
		False(invalidHand.Accepted);
		AssertReadModelsEquivalent(beforeInvalidHand, invalidHand.ReadModel);
		Equal(randomBeforeInvalidHand, random.CaptureState());

		RuleCommandResult transferredItem = session.Submit(new TransferHandCardCommand(
			272,
			beforeInvalidHand.Revision,
			beforeInvalidHand.LifecycleGeneration,
			beforeInvalidHand.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			SourceDisplayIndex: 1,
			MaximumHandSize: 4,
			ExpectedItemId: "lens"));
		True(transferredItem.Accepted);
		Equal("lens", transferredItem.ReadModel.Players[0].ItemHand.Single().Id);
		Equal(0, transferredItem.ReadModel.Players[1].ItemHand.Length);
		Equal(PresentationIntentKind.HandCardTransferred, transferredItem.PresentationIntents.Single().Kind);

		RuleCommandResult transferredCard = session.Submit(new TransferHandCardCommand(
			273,
			transferredItem.Revision,
			transferredItem.ReadModel.LifecycleGeneration,
			transferredItem.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			SourceDisplayIndex: 0,
			MaximumHandSize: 4,
			ExpectedCardRuntimeInstanceId: 3));
		True(transferredCard.Accepted);
		Equal(3L, transferredCard.ReadModel.Players[0].Hand[1].RuntimeInstanceId);
		Equal(0, transferredCard.ReadModel.Players[1].Hand.Length);

		MatchReadModel beforeInvalidDeck = transferredCard.ReadModel;
		RuleRandomState randomBeforeInvalidDeck = random.CaptureState();
		RuleCommandResult invalidDeck = session.Submit(new TransferDeckCardsCommand(
			274,
			beforeInvalidDeck.Revision,
			beforeInvalidDeck.LifecycleGeneration,
			beforeInvalidDeck.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			MaximumCount: 2,
			PreserveCardEffectsWhenEnteringDeck: false,
			ExpectedSourceTopCardRuntimeInstanceIds: ImmutableArray.Create(999L, 21L)));
		False(invalidDeck.Accepted);
		AssertReadModelsEquivalent(beforeInvalidDeck, invalidDeck.ReadModel);
		Equal(randomBeforeInvalidDeck, random.CaptureState());

		RuleCommandResult transferredDeck = session.Submit(new TransferDeckCardsCommand(
			275,
			beforeInvalidDeck.Revision,
			beforeInvalidDeck.LifecycleGeneration,
			beforeInvalidDeck.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			MaximumCount: 2,
			PreserveCardEffectsWhenEnteringDeck: false,
			ExpectedSourceTopCardRuntimeInstanceIds: ImmutableArray.Create(20L, 21L)));
		True(transferredDeck.Accepted);
		Equal(3, transferredDeck.ReadModel.Players[0].Deck.Length);
		Equal(1, transferredDeck.ReadModel.Players[1].Deck.Length);
		Equal(2, transferredDeck.Mutation.CollectedCardCount);
		Equal(PresentationIntentKind.DeckCardsTransferred, transferredDeck.PresentationIntents.Single().Kind);
		LogicalCardState transferredEnchantedCard = transferredDeck.ReadModel.Players[0].Deck.Single(
			card => card.RuntimeInstanceId == 20);
		Equal(0, transferredEnchantedCard.NormalizedAttachedEffectIds.Length);
		False(randomBeforeInvalidDeck.Equals(random.CaptureState()));
	}

	private static void AuthorityTableItemCommandsAreOrderedAtomicAndDeterministic()
	{
		var random = new TestRuleRandomSource(91);
		var profile = new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule(
				"items",
				0,
				capabilities: new MatchCapabilitySet(MatchCapability.ItemRules)),
		});
		var session = new AuthorityRuleSession(random, profile);
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				0));
		var tableCards = ImmutableArray.Create(
			new TableCardState(
				100,
				new LogicalCardState(
					10,
					"clubs:2",
					"clubs",
					"2",
					ImmutableArray.Create("overtime")),
				false,
				false,
				false,
				false,
				null,
				-1,
				1,
				false),
			new TableCardState(
				101,
				new LogicalCardState(11, "hearts:3", "hearts", "3"),
				false,
				false,
				false,
				false,
				null,
				-1,
				1,
				false),
			new TableCardState(
				102,
				null,
				false,
				false,
				false,
				false,
				new LogicalItemState("bomb"),
				1,
				-1,
				false));
		True(session.Submit(new InitializeScriptedMatchCommand(
			280,
			0,
			1,
			0,
			2,
			100,
			players,
			tableCards)).Accepted);

		MatchReadModel beforePlace = session.ReadModel;
		RuleCommandResult placed = session.Submit(new PlaceTableItemCommand(
			281,
			beforePlace.Revision,
			beforePlace.LifecycleGeneration,
			beforePlace.TurnGeneration,
			ActingSeat: 0,
			ItemId: "question_mark",
			TableIndex: 3,
			IsFaceUp: true));
		True(placed.Accepted);
		Equal(4, placed.ReadModel.TableCards.Length);
		Equal("question_mark", placed.ReadModel.TableCards[3].ItemOverlay!.Value.Id);
		Equal(PresentationIntentKind.TableItemPlaced, placed.PresentationIntents.Single().Kind);

		RuleRandomState randomBeforeInvalidChoice = random.CaptureState();
		RuleCommandResult invalidChoice = session.Submit(new ChooseTableCardsCommand(
			282,
			placed.Revision,
			placed.ReadModel.LifecycleGeneration,
			placed.TurnGeneration,
			ActingSeat: 0,
			Candidates: ImmutableArray.Create(new RuleTableCardReference(0, 999)),
			MaximumCount: 1));
		False(invalidChoice.Accepted);
		AssertReadModelsEquivalent(placed.ReadModel, invalidChoice.ReadModel);
		Equal(randomBeforeInvalidChoice, random.CaptureState());

		RuleCommandResult chosen = session.Submit(new ChooseTableCardsCommand(
			283,
			placed.Revision,
			placed.ReadModel.LifecycleGeneration,
			placed.TurnGeneration,
			ActingSeat: 0,
			Candidates: ImmutableArray.Create(
				new RuleTableCardReference(0, 100),
				new RuleTableCardReference(1, 101)),
			MaximumCount: 1));
		True(chosen.Accepted);
		Equal(1, chosen.PresentationIntents.Length);
		Equal(PresentationIntentKind.TableCardsSelected, chosen.PresentationIntents[0].Kind);
		False(randomBeforeInvalidChoice.Equals(random.CaptureState()));

		int chosenIndex = chosen.PresentationIntents[0].TargetIndex;
		long chosenRuntimeId = chosen.PresentationIntents[0].LogicalEntityId;
		RuleCommandResult revealed = session.Submit(new SetTableCardsFaceUpCommand(
			284,
			chosen.Revision,
			chosen.ReadModel.LifecycleGeneration,
			chosen.TurnGeneration,
			ActingSeat: 0,
			TableCards: ImmutableArray.Create(new RuleTableCardReference(chosenIndex, chosenRuntimeId)),
			ExpectedFaceUp: false,
			FaceUp: true));
		True(revealed.Accepted);
		True(revealed.ReadModel.TableCards[chosenIndex].IsFaceUp);

		int otherIndex = chosenIndex == 0 ? 1 : 0;
		long otherRuntimeId = revealed.ReadModel.TableCards[otherIndex].RuntimeInstanceId;
		RuleCommandResult invalidBatchReveal = session.Submit(new SetTableCardsFaceUpCommand(
			285,
			revealed.Revision,
			revealed.ReadModel.LifecycleGeneration,
			revealed.TurnGeneration,
			ActingSeat: 0,
			TableCards: ImmutableArray.Create(
				new RuleTableCardReference(chosenIndex, chosenRuntimeId),
				new RuleTableCardReference(otherIndex, otherRuntimeId)),
			ExpectedFaceUp: false,
			FaceUp: true));
		False(invalidBatchReveal.Accepted);
		AssertReadModelsEquivalent(revealed.ReadModel, invalidBatchReveal.ReadModel);
		False(invalidBatchReveal.ReadModel.TableCards[otherIndex].IsFaceUp);

		RuleCommandResult hidden = session.Submit(new SetTableCardsHiddenCommand(
			286,
			revealed.Revision,
			revealed.ReadModel.LifecycleGeneration,
			revealed.TurnGeneration,
			ActingSeat: 0,
			TableCards: ImmutableArray.Create(new RuleTableCardReference(otherIndex, otherRuntimeId)),
			ExpectedHidden: false,
			Hidden: true));
		True(hidden.Accepted);
		True(hidden.ReadModel.TableCards[otherIndex].IsHiddenDuringCapture);

		RuleCommandResult invalidDetach = session.Submit(new RemoveTableCardAttachmentCommand(
			296,
			hidden.Revision,
			hidden.ReadModel.LifecycleGeneration,
			hidden.TurnGeneration,
			ActingSeat: 0,
			TableIndex: 0,
			ExpectedTableCardRuntimeInstanceId: 100,
			ItemId: "double"));
		False(invalidDetach.Accepted);
		AssertReadModelsEquivalent(hidden.ReadModel, invalidDetach.ReadModel);

		RuleCommandResult detached = session.Submit(new RemoveTableCardAttachmentCommand(
			297,
			hidden.Revision,
			hidden.ReadModel.LifecycleGeneration,
			hidden.TurnGeneration,
			ActingSeat: 0,
			TableIndex: 0,
			ExpectedTableCardRuntimeInstanceId: 100,
			ItemId: "overtime"));
		True(detached.Accepted);
		Equal(0, detached.ReadModel.TableCards[0].Card!.Value.NormalizedAttachedEffectIds.Length);
		Equal("overtime", detached.Mutation.LogicalItemId);

		long placedRuntimeId = placed.Mutation.LogicalEntityId;
		RuleCommandResult moved = session.Submit(new MoveTableCardCommand(
			287,
			detached.Revision,
			detached.ReadModel.LifecycleGeneration,
			detached.TurnGeneration,
			ActingSeat: 0,
			SourceIndex: 3,
			TargetIndex: 0,
			ExpectedRuntimeInstanceId: placedRuntimeId));
		True(moved.Accepted);
		Equal(placedRuntimeId, moved.ReadModel.TableCards[0].RuntimeInstanceId);

		ImmutableArray<long> removalIds = moved.ReadModel.TableCards
			.Take(2)
			.Select(card => card.RuntimeInstanceId)
			.ToImmutableArray();
		RuleCommandResult invalidRemoval = session.Submit(new RemoveTableCardRangeCommand(
			288,
			moved.Revision,
			moved.ReadModel.LifecycleGeneration,
			moved.TurnGeneration,
			ActingSeat: 0,
			StartIndex: 0,
			ExpectedRuntimeInstanceIds: removalIds.SetItem(1, 999)));
		False(invalidRemoval.Accepted);
		AssertReadModelsEquivalent(moved.ReadModel, invalidRemoval.ReadModel);

		RuleCommandResult removed = session.Submit(new RemoveTableCardRangeCommand(
			289,
			moved.Revision,
			moved.ReadModel.LifecycleGeneration,
			moved.TurnGeneration,
			ActingSeat: 0,
			StartIndex: 0,
			ExpectedRuntimeInstanceIds: removalIds));
		True(removed.Accepted);
		Equal(2, removed.ReadModel.TableCards.Length);
		Equal(2, removed.Mutation.RemovedTableCardCount);
		Equal(PresentationIntentKind.TableCardsRemoved, removed.PresentationIntents.Single().Kind);
	}

	private static void AuthorityTableShufflePlanSurvivesBarriersAndSnapshots()
	{
		var random = new TestRuleRandomSource(101);
		var profile = new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule(
				"items",
				0,
				capabilities: new MatchCapabilitySet(MatchCapability.ItemRules)),
		});
		var session = new AuthorityRuleSession(random, profile);
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				0));
		var tableCards = ImmutableArray.Create(
			CreateRuleTableCardState(300, 30, "clubs:2"),
			CreateRuleTableCardState(301, 31, "hearts:3"),
			CreateRuleTableCardState(302, 32, "spades:4"),
			CreateRuleTableCardState(303, 33, "diamonds:5"));
		True(session.Submit(new InitializeScriptedMatchCommand(
			290,
			0,
			1,
			0,
			2,
			100,
			players,
			tableCards)).Accepted);

		ImmutableArray<RuleTableCardReference> references = session.ReadModel.TableCards
			.Select((card, index) => new RuleTableCardReference(index, card.RuntimeInstanceId))
			.ToImmutableArray();
		MatchReadModel beforeInvalid = session.ReadModel;
		RuleRandomState randomBeforeInvalid = random.CaptureState();
		RuleCommandResult invalid = session.Submit(new PlanTableShuffleCommand(
			291,
			beforeInvalid.Revision,
			beforeInvalid.LifecycleGeneration,
			beforeInvalid.TurnGeneration,
			ActingSeat: 0,
			TableCards: references,
			FixedTableIndexes: ImmutableArray.Create(1, 1)));
		False(invalid.Accepted);
		AssertReadModelsEquivalent(beforeInvalid, invalid.ReadModel);
		Equal(randomBeforeInvalid, random.CaptureState());

		RuleCommandResult planned = session.Submit(new PlanTableShuffleCommand(
			292,
			beforeInvalid.Revision,
			beforeInvalid.LifecycleGeneration,
			beforeInvalid.TurnGeneration,
			ActingSeat: 0,
			TableCards: references,
			FixedTableIndexes: ImmutableArray.Create(1)));
		True(planned.Accepted);
		True(planned.ReadModel.HasPendingPresentationBarrier);
		Equal(4, planned.PresentationIntents.Length);
		True(planned.PresentationIntents.All(intent =>
			intent.Kind == PresentationIntentKind.TableShufflePlanned
			&& intent.BarrierId == planned.ReadModel.PendingPresentationBarrierId));
		ImmutableArray<long> plannedOrder = planned.ReadModel.NormalizedPendingTableOrderRuntimeInstanceIds;
		Equal(4, plannedOrder.Length);
		Equal(301L, plannedOrder[1]);
		False(plannedOrder.SequenceEqual(references.Select(reference => reference.RuntimeInstanceId)));
		False(randomBeforeInvalid.Equals(random.CaptureState()));

		MatchStateSnapshot snapshot = session.CaptureSnapshot();
		var restoredRandom = new TestRuleRandomSource(999);
		AuthorityRuleSession restored = AuthorityRuleSession.Restore(snapshot, restoredRandom, profile);
		True(restored.ReadModel.NormalizedPendingTableOrderRuntimeInstanceIds.SequenceEqual(plannedOrder));
		Equal(snapshot.RandomState, restoredRandom.CaptureState());

		RuleCommandResult blocked = restored.Submit(new MoveTableCardCommand(
			293,
			restored.ReadModel.Revision,
			restored.ReadModel.LifecycleGeneration,
			restored.ReadModel.TurnGeneration,
			ActingSeat: 0,
			SourceIndex: 0,
			TargetIndex: 3,
			ExpectedRuntimeInstanceId: 300));
		False(blocked.Accepted);
		Equal(RuleRejectionCode.PresentationBarrierPending, blocked.Rejection.Code);

		MatchReadModel beforeWrongCompletion = restored.ReadModel;
		RuleCommandResult wrongCompletion = restored.Submit(new CompletePresentationBarrierCommand(
			294,
			beforeWrongCompletion.Revision,
			beforeWrongCompletion.LifecycleGeneration,
			beforeWrongCompletion.TurnGeneration,
			beforeWrongCompletion.PendingPresentationBarrierId + 1));
		False(wrongCompletion.Accepted);
		AssertReadModelsEquivalent(beforeWrongCompletion, wrongCompletion.ReadModel);

		RuleCommandResult completed = restored.Submit(new CompletePresentationBarrierCommand(
			295,
			beforeWrongCompletion.Revision,
			beforeWrongCompletion.LifecycleGeneration,
			beforeWrongCompletion.TurnGeneration,
			beforeWrongCompletion.PendingPresentationBarrierId));
		True(completed.Accepted);
		False(completed.ReadModel.HasPendingPresentationBarrier);
		Equal(0, completed.ReadModel.NormalizedPendingTableOrderRuntimeInstanceIds.Length);
		True(completed.ReadModel.TableCards
			.Select(card => card.RuntimeInstanceId)
			.SequenceEqual(plannedOrder));
	}

	private static void SliceFourRandomCommandsPreserveExactSequenceAndAtomicity()
	{
		var random = new TestRuleRandomSource(103);
		var expectedRandom = new TestRuleRandomSource(103);
		AuthorityRuleSession session = CreateItemCommandSession(
			itemRulesEnabled: true,
			random);

		MatchReadModel beforeInvalidRange = session.ReadModel;
		RuleRandomState randomBeforeInvalidRange = random.CaptureState();
		RuleCommandResult invalidRange = session.Submit(new ChooseRandomIndexCommand(
			600,
			beforeInvalidRange.Revision,
			beforeInvalidRange.LifecycleGeneration,
			beforeInvalidRange.TurnGeneration,
			ActingSeat: 0,
			MinimumInclusive: 8,
			MaximumInclusive: 7));
		False(invalidRange.Accepted);
		Equal(randomBeforeInvalidRange, random.CaptureState());
		AssertReadModelsEquivalent(beforeInvalidRange, invalidRange.ReadModel);

		int expectedIndex = expectedRandom.NextIntInclusive(4, 8);
		RuleCommandResult chosenIndex = session.Submit(new ChooseRandomIndexCommand(
			601,
			beforeInvalidRange.Revision,
			beforeInvalidRange.LifecycleGeneration,
			beforeInvalidRange.TurnGeneration,
			ActingSeat: 0,
			MinimumInclusive: 4,
			MaximumInclusive: 8));
		True(chosenIndex.Accepted);
		Equal(expectedIndex, chosenIndex.Mutation.SourceIndex);
		Equal(expectedRandom.CaptureState(), random.CaptureState());

		MatchReadModel beforeInvalidReservoir = chosenIndex.ReadModel;
		RuleRandomState randomBeforeInvalidReservoir = random.CaptureState();
		RuleCommandResult invalidReservoir = session.Submit(
			new ChooseReservoirHandDisplayIndexCommand(
				602,
				beforeInvalidReservoir.Revision,
				beforeInvalidReservoir.LifecycleGeneration,
				beforeInvalidReservoir.TurnGeneration,
				ActingSeat: 0,
				HandSeat: 0,
				CandidateDisplayIndexes: ImmutableArray.Create(0, 4)));
		False(invalidReservoir.Accepted);
		Equal(randomBeforeInvalidReservoir, random.CaptureState());
		AssertReadModelsEquivalent(beforeInvalidReservoir, invalidReservoir.ReadModel);

		ImmutableArray<int> candidates = ImmutableArray.Create(0, 3, 1);
		int expectedDisplayIndex = -1;
		for (int ordinal = 0; ordinal < candidates.Length; ordinal++)
		{
			if (expectedRandom.NextIntInclusive(1, ordinal + 1) == 1)
			{
				expectedDisplayIndex = candidates[ordinal];
			}
		}
		RuleCommandResult chosenDisplayIndex = session.Submit(
			new ChooseReservoirHandDisplayIndexCommand(
				603,
				beforeInvalidReservoir.Revision,
				beforeInvalidReservoir.LifecycleGeneration,
				beforeInvalidReservoir.TurnGeneration,
				ActingSeat: 0,
				HandSeat: 0,
				CandidateDisplayIndexes: candidates));
		True(chosenDisplayIndex.Accepted);
		Equal(expectedDisplayIndex, chosenDisplayIndex.Mutation.SourceIndex);
		Equal(candidates.Length, chosenDisplayIndex.Mutation.CollectedCardCount);
		Equal(expectedRandom.CaptureState(), random.CaptureState());
	}

	private static void SliceFiveAiRandomCommandSequenceIsDeterministicAndAtomic()
	{
		var random = new TestRuleRandomSource(109);
		var expectedRandom = new TestRuleRandomSource(109);
		AuthorityRuleSession session = CreateItemCommandSession(
			itemRulesEnabled: true,
			random);

		MatchReadModel beforeRejected = session.ReadModel;
		RuleRandomState randomBeforeRejected = random.CaptureState();
		RuleCommandResult rejected = session.Submit(new ChooseRandomIndexCommand(
			700,
			beforeRejected.Revision,
			beforeRejected.LifecycleGeneration,
			beforeRejected.TurnGeneration,
			ActingSeat: 1,
			MinimumInclusive: 0,
			MaximumInclusive: 99));
		False(rejected.Accepted);
		Equal(RuleRejectionCode.WrongTurn, rejected.Rejection.Code);
		Equal(randomBeforeRejected, random.CaptureState());
		AssertReadModelsEquivalent(beforeRejected, rejected.ReadModel);

		(int MinimumInclusive, int MaximumInclusive)[] aiRanges =
		{
			(0, 99),
			(0, int.MaxValue),
			(0, int.MaxValue),
			(0, 99),
			(0, int.MaxValue),
			(0, 6),
			(0, int.MaxValue),
			(0, int.MaxValue),
			(0, int.MaxValue),
			(0, 99),
			(0, 9999),
			(0, 4),
			(0, int.MaxValue),
		};
		for (int index = 0; index < aiRanges.Length; index++)
		{
			(int minimumInclusive, int maximumInclusive) = aiRanges[index];
			int expectedIndex = expectedRandom.NextIntInclusive(
				minimumInclusive,
				maximumInclusive);
			MatchReadModel beforeCommand = session.ReadModel;
			RuleCommandResult result = session.Submit(new ChooseRandomIndexCommand(
				701 + index,
				beforeCommand.Revision,
				beforeCommand.LifecycleGeneration,
				beforeCommand.TurnGeneration,
				ActingSeat: 0,
				minimumInclusive,
				maximumInclusive));

			True(result.Accepted);
			Equal(expectedIndex, result.Mutation.SourceIndex);
			Equal(beforeCommand.Revision + 1, result.ReadModel.Revision);
			Equal(expectedRandom.CaptureState(), random.CaptureState());
		}
	}

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

	private static void SliceFourGameplayBoundariesStayBehindNeutralRuleCommands()
	{
		string root = FindRepositoryRoot();
		string[] paths =
		{
			"scripts/GameBoardScene.AdventureUpgrades.cs",
			"scripts/GameBoardScene.AdventureUpgradeOffers.cs",
			"scripts/GameBoardScene.BossTableAbilities.cs",
			"scripts/GameBoardScene.DragonPrinceTableAbility.cs",
			"scripts/GameBoardScene.TableAbilityTriggers.cs",
			"scripts/GameBoardScene.TableTriggerHelpers.cs",
			"scripts/GameBoardScene.TurnAdvancement.cs",
			"scripts/AdventureExtraActionChain.cs",
		};
		var sources = paths.ToDictionary(
			path => path,
			path => File.ReadAllText(Path.Combine(root, path)),
			StringComparer.Ordinal);
		foreach (string source in sources.Values)
		{
			False(source.Contains("_rng.", StringComparison.Ordinal));
		}

		string upgrades = sources["scripts/GameBoardScene.AdventureUpgrades.cs"];
		True(upgrades.Contains("TrySubmitMoveTableCardRuleCommand(", StringComparison.Ordinal));
		False(upgrades.Contains("_tableCards.Insert(", StringComparison.Ordinal));
		string tableAbilities = sources["scripts/GameBoardScene.TableAbilityTriggers.cs"];
		True(tableAbilities.Contains(
			"TrySubmitSetTableCardsScoreSuppressedRuleCommand(",
			StringComparison.Ordinal));
		False(tableAbilities.Contains(".IsSlimeCoated =", StringComparison.Ordinal));

		string boss = sources["scripts/GameBoardScene.BossTableAbilities.cs"];
		True(boss.Contains("TrySubmitMoveTableCardToDeckRuleCommand(", StringComparison.Ordinal));
		True(boss.Contains("TrySubmitTransferTopDeckCardRuleCommand(", StringComparison.Ordinal));
		string dragon = sources["scripts/GameBoardScene.DragonPrinceTableAbility.cs"];
		True(dragon.Contains("TrySubmitPlaceTableCardRuleCommand(", StringComparison.Ordinal));

		string turn = sources["scripts/GameBoardScene.TurnAdvancement.cs"];
		True(turn.Contains("TrySubmitEndRealTurnRuleCommand(", StringComparison.Ordinal));
		False(turn.Contains(".IsHiddenDuringCapture =", StringComparison.Ordinal));
		string extraActionAdapter = sources["scripts/AdventureExtraActionChain.cs"];
		False(extraActionAdapter.Contains("ExtraActionQueueState", StringComparison.Ordinal));
		False(extraActionAdapter.Contains("_state.", StringComparison.Ordinal));
		string upgradeOffers = sources["scripts/GameBoardScene.AdventureUpgradeOffers.cs"];
		False(upgradeOffers.Contains(
			"_pendingAdventureUpgradeTriggers",
			StringComparison.Ordinal));
		False(upgradeOffers.Contains(
			"_pendingAdventureWinnerPlayerIndex",
			StringComparison.Ordinal));
		True(upgradeOffers.Contains(
			"CurrentMatchFlowState.NormalizedPendingProgressionChoices",
			StringComparison.Ordinal));
		True(upgradeOffers.Contains("next.ToImmutable()", StringComparison.Ordinal));
		False(upgradeOffers.Contains("next.MoveToImmutable()", StringComparison.Ordinal));
		string itemCards = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.ItemCards.cs"));
		string questionCards = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.QuestionCards.cs"));
		False(itemCards.Contains(
			"_plannedQuestionRewardDiscardExclusions",
			StringComparison.Ordinal));
		False(questionCards.Contains(
			"_plannedQuestionRewardDiscardExclusions",
			StringComparison.Ordinal));
		True(itemCards.Contains(
			"pending.NormalizedLegalSelectionIndexes.Contains(displayIndex)",
			StringComparison.Ordinal));

		string extraActionCore = File.ReadAllText(Path.Combine(
			root,
			"src",
			"Deckland.Rules",
			"ExtraActionQueue.cs"));
		False(extraActionCore.Contains("Adventure", StringComparison.Ordinal));
		string legacyState = File.ReadAllText(Path.Combine(
			root,
			"src",
			"Deckland.Rules",
			"LegacyCardStateModels.cs"));
		False(legacyState.Contains("AdventureExtraAction", StringComparison.Ordinal));
		string rulesProject = File.ReadAllText(Path.Combine(
			root,
			"src",
			"Deckland.Rules",
			"Deckland.Rules.csproj"));
		False(rulesProject.Contains("../../scripts/AdventureExtraActionChain.cs", StringComparison.Ordinal));
	}

	private static void SliceFiveAiRandomnessStaysBehindRuleCommands()
	{
		string root = FindRepositoryRoot();
		string turnAi = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.TurnAi.cs"));
		string soloAi = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.SoloAi.cs"));
		string networkAi = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.NetworkAiProxy.cs"));

		foreach (string source in new[] { turnAi, soloAi, networkAi })
		{
			False(source.Contains("_rng.", StringComparison.Ordinal));
			False(source.Contains("_playerHands", StringComparison.Ordinal));
			False(source.Contains("_playerItemHands", StringComparison.Ordinal));
			True(source.Contains(
				"TrySubmitChooseRandomIndexRuleCommand(",
				StringComparison.Ordinal));
			True(source.Contains(
				"TryGetAuthorityRuleReadModel(",
				StringComparison.Ordinal));
		}
		False(soloAi.Contains("_tableCards", StringComparison.Ordinal));
		False(networkAi.Contains("_tableCards", StringComparison.Ordinal));
		False(turnAi.Contains("ToCaptureScoringTableCards()", StringComparison.Ordinal));
		True(turnAi.Contains(
			"ToCaptureScoringTableCards(readModel.TableCards)",
			StringComparison.Ordinal));
		True(soloAi.Contains(
			"ToPotentialCaptureCards(playerIndex, player.Hand)",
			StringComparison.Ordinal));
		True(networkAi.Contains(
			"ToPotentialCaptureCards(\n\t\t\t\t\tplayerIndex,\n\t\t\t\t\tplayer.Hand)",
			StringComparison.Ordinal));

		Equal(
			5,
			turnAi.Split(
				"TrySubmitChooseRandomIndexRuleCommand(",
				StringSplitOptions.None).Length - 1);
		Equal(
			4,
			soloAi.Split(
				"TrySubmitChooseRandomIndexRuleCommand(",
				StringSplitOptions.None).Length - 1);
		Equal(
			3,
			networkAi.Split(
				"TrySubmitChooseRandomIndexRuleCommand(",
				StringSplitOptions.None).Length - 1);
		True(turnAi.Contains("out int decisionRollPercent", StringComparison.Ordinal));
		True(turnAi.Contains("out int randomCandidateOrdinal", StringComparison.Ordinal));
		True(soloAi.Contains("out int randomCaptureOrdinal", StringComparison.Ordinal));
		True(networkAi.Contains("9999", StringComparison.Ordinal));
	}

	private static TableCardState CreateRuleTableCardState(
		long tableRuntimeInstanceId,
		long cardRuntimeInstanceId,
		string code)
	{
		string[] parts = code.Split(':');
		return new TableCardState(
			tableRuntimeInstanceId,
			new LogicalCardState(cardRuntimeInstanceId, code, parts[0], parts[1]),
			false,
			false,
			false,
			false,
			null,
			-1,
			1,
			false);
	}

	private static AuthorityRuleSession CreateItemCommandSession(
		bool itemRulesEnabled,
		TestRuleRandomSource? random = null)
	{
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(
					new LogicalCardState(1, "clubs:4", "clubs", "4"),
					new LogicalCardState(2, "hearts:8", "hearts", "8")),
				ImmutableArray.Create(
					new LogicalItemState("bomb"),
					new LogicalItemState("lens")),
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(new LogicalCardState(3, "spades:9", "spades", "9")),
				ImmutableArray<LogicalItemState>.Empty,
				0));
		MatchRuleProfile profile = itemRulesEnabled
			? CreateItemRuleProfile()
			: MatchRuleProfile.Empty;
		var session = new AuthorityRuleSession(
			random ?? new TestRuleRandomSource(55),
			profile);
		True(session.Submit(new InitializeScriptedMatchCommand(
			250,
			0,
			1,
			0,
			2,
			100,
			players,
			ImmutableArray<TableCardState>.Empty)).Accepted);
		return session;
	}

	private static MatchRuleProfile CreateItemRuleProfile()
	{
		return new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule(
				"items",
				0,
				capabilities: new MatchCapabilitySet(MatchCapability.ItemRules)),
		});
	}

	private static void AuthorityOutcomeCommandResolvesEqualScoreExhaustionAsDraw()
	{
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				12),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				12));
		var session = new AuthorityRuleSession(new TestRuleRandomSource(44), MatchRuleProfile.Empty);
		True(session.Submit(new InitializeScriptedMatchCommand(
			241,
			0,
			1,
			0,
			2,
			100,
			players,
			ImmutableArray<TableCardState>.Empty)).Accepted);

		MatchReadModel before = session.ReadModel;
		RuleCommandResult outcome = session.Submit(new EvaluateMatchOutcomeCommand(
			242,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration));
		True(outcome.Accepted);
		Equal(Deckland.Rules.MatchOutcome.Draw, outcome.ReadModel.Outcome);
		Equal(-1, outcome.ReadModel.WinnerSeat);
	}

	private static AuthorityRuleSession CreateGameplayCommandSession(
		int playerZeroScore = 0,
		ImmutableArray<LogicalCardState> playerZeroDeck = default,
		MatchRuleProfile? profile = null)
	{
		if (playerZeroDeck.IsDefault)
		{
			playerZeroDeck = ImmutableArray<LogicalCardState>.Empty;
		}

		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				playerZeroDeck,
				ImmutableArray.Create(new LogicalCardState(1, "spades:5", "spades", "5")),
				ImmutableArray<LogicalItemState>.Empty,
				playerZeroScore),
			new PlayerStateSnapshot(
				1,
				ImmutableArray.Create(new LogicalCardState(4, "clubs:9", "clubs", "9")),
				ImmutableArray.Create(new LogicalCardState(5, "diamonds:8", "diamonds", "8")),
				ImmutableArray<LogicalItemState>.Empty,
				0));
		var tableCards = ImmutableArray.Create(
			new TableCardState(
				11,
				new LogicalCardState(2, "hearts:5", "hearts", "5"),
				true,
				false,
				false,
				false,
				null,
				-1,
				-1,
				false),
			new TableCardState(
				12,
				new LogicalCardState(3, "clubs:2", "clubs", "2"),
				true,
				false,
				false,
				false,
				null,
				-1,
				-1,
				false));
		var session = new AuthorityRuleSession(
			new TestRuleRandomSource(43),
			profile ?? MatchRuleProfile.Empty);
		RuleCommandResult initialized = session.Submit(new InitializeScriptedMatchCommand(
			200,
			0,
			1,
			0,
			2,
			100,
			players,
			tableCards));
		True(initialized.Accepted);
		return session;
	}

	private static void AssertExtraActionsEquivalent(
		ExtraActionQueueSnapshot expected,
		ExtraActionQueueSnapshot actual)
	{
		Equal(expected.NextRootChainId, actual.NextRootChainId);
		Equal(expected.ActiveRootChainId, actual.ActiveRootChainId);
		Equal(expected.RootResumesNormalTurn, actual.RootResumesNormalTurn);
		Equal(expected.RootDerivationStopped, actual.RootDerivationStopped);
		Equal(expected.RootEventCount, actual.RootEventCount);
		Equal(expected.RootMaxDepth, actual.RootMaxDepth);
		Equal(
			expected.ExtraActionsExecutedThisRealTurn,
			actual.ExtraActionsExecutedThisRealTurn);
		Equal(
			expected.ContinueBranchUsedThisRealTurn,
			actual.ContinueBranchUsedThisRealTurn);
		Equal(expected.CurrentGrant, actual.CurrentGrant);
		True(expected.QueuedGrants.SequenceEqual(actual.QueuedGrants));
		True(expected.SourcesInRootChain.SequenceEqual(actual.SourcesInRootChain));
		True(expected.PendingRealTurnEndMarkerIds.SequenceEqual(
			actual.PendingRealTurnEndMarkerIds));
	}

	private static void AssertReadModelsEquivalent(MatchReadModel expected, MatchReadModel actual)
	{
		Equal(expected.Revision, actual.Revision);
		Equal(expected.LifecycleGeneration, actual.LifecycleGeneration);
		Equal(expected.TurnGeneration, actual.TurnGeneration);
		Equal(expected.PlayerCount, actual.PlayerCount);
		Equal(expected.WinningScore, actual.WinningScore);
		Equal(expected.CurrentTurnSeat, actual.CurrentTurnSeat);
		Equal(expected.Outcome, actual.Outcome);
		Equal(expected.WinnerSeat, actual.WinnerSeat);
		Equal(
			expected.CompletedTurnsSinceQuestionRefresh,
			actual.CompletedTurnsSinceQuestionRefresh);
		Equal(expected.QuestionRefreshAnchorSeat, actual.QuestionRefreshAnchorSeat);
		Equal(expected.PendingPresentationBarrierId, actual.PendingPresentationBarrierId);
		True(expected.NormalizedPendingTableOrderRuntimeInstanceIds.SequenceEqual(
			actual.NormalizedPendingTableOrderRuntimeInstanceIds));
		MandatorySelectionState expectedSelection = expected.NormalizedMandatorySelection;
		MandatorySelectionState actualSelection = actual.NormalizedMandatorySelection;
		Equal(expectedSelection.Kind, actualSelection.Kind);
		Equal(expectedSelection.Seat, actualSelection.Seat);
		Equal(expectedSelection.SourceHandIndex, actualSelection.SourceHandIndex);
		Equal(expectedSelection.TurnGeneration, actualSelection.TurnGeneration);
		Equal(expectedSelection.ItemId, actualSelection.ItemId);
		True(expectedSelection.NormalizedLegalSelectionIndexes.SequenceEqual(
			actualSelection.NormalizedLegalSelectionIndexes));
		True(expected.NormalizedPendingAttachmentRewards.SequenceEqual(
			actual.NormalizedPendingAttachmentRewards));
		AssertExtraActionsEquivalent(
			expected.NormalizedExtraActions,
			actual.NormalizedExtraActions);
		Equal(expected.NormalizedMatchFlow, actual.NormalizedMatchFlow);
		Equal(expected.Players.Length, actual.Players.Length);
		for (int seat = 0; seat < expected.Players.Length; seat++)
		{
			PlayerStateSnapshot expectedPlayer = expected.Players[seat];
			PlayerStateSnapshot actualPlayer = actual.Players[seat];
			Equal(expectedPlayer.Seat, actualPlayer.Seat);
			Equal(expectedPlayer.Score, actualPlayer.Score);
			AssertCardStatesEquivalent(expectedPlayer.Deck, actualPlayer.Deck);
			AssertCardStatesEquivalent(expectedPlayer.Hand, actualPlayer.Hand);
			True(expectedPlayer.ItemHand.SequenceEqual(actualPlayer.ItemHand));
		}

		Equal(expected.TableCards.Length, actual.TableCards.Length);
		for (int index = 0; index < expected.TableCards.Length; index++)
		{
			TableCardState expectedCard = expected.TableCards[index];
			TableCardState actualCard = actual.TableCards[index];
			Equal(expectedCard.RuntimeInstanceId, actualCard.RuntimeInstanceId);
			Equal(expectedCard.IsFaceUp, actualCard.IsFaceUp);
			Equal(expectedCard.IsHiddenDuringCapture, actualCard.IsHiddenDuringCapture);
			Equal(expectedCard.IsBuried, actualCard.IsBuried);
			Equal(expectedCard.IsScoreSuppressed, actualCard.IsScoreSuppressed);
			Equal(expectedCard.ItemOverlay, actualCard.ItemOverlay);
			Equal(expectedCard.ItemOwnerSeat, actualCard.ItemOwnerSeat);
			Equal(expectedCard.CardOwnerSeat, actualCard.CardOwnerSeat);
			Equal(expectedCard.RemoveOnCapture, actualCard.RemoveOnCapture);
			Equal(expectedCard.Card.HasValue, actualCard.Card.HasValue);
			if (expectedCard.Card.HasValue)
			{
				AssertCardStatesEquivalent(
					ImmutableArray.Create(expectedCard.Card.Value),
					ImmutableArray.Create(actualCard.Card!.Value));
			}
		}
	}

	private static void AssertCardStatesEquivalent(
		ImmutableArray<LogicalCardState> expected,
		ImmutableArray<LogicalCardState> actual)
	{
		Equal(expected.Length, actual.Length);
		for (int index = 0; index < expected.Length; index++)
		{
			Equal(expected[index].RuntimeInstanceId, actual[index].RuntimeInstanceId);
			Equal(expected[index].Code, actual[index].Code);
			Equal(expected[index].Suit, actual[index].Suit);
			Equal(expected[index].Rank, actual[index].Rank);
			True(expected[index].NormalizedAttachedEffectIds.SequenceEqual(
				actual[index].NormalizedAttachedEffectIds));
		}
	}

	private static InitializeMatchCommand CreateInitializeCommand(
		long actionId,
		bool createOpeningBarrier = false)
	{
		var cards = ImmutableArray.CreateBuilder<LogicalCardState>();
		long instanceId = 1;
		foreach (string rank in new[] { "1", "2", "3" })
		{
			foreach (string suit in new[] { "hearts", "clubs", "diamonds", "spades" })
			{
				cards.Add(new LogicalCardState(
					instanceId++,
					$"{suit}:{rank}",
					suit,
					rank));
			}
		}

		return new InitializeMatchCommand(
			actionId,
			LifecycleGeneration: 1,
			PlayerCount: 2,
			WinningScore: 100,
			StartingHandSize: 3,
			DealEqualRanks: true,
			Deck: cards.ToImmutable(),
			CreateOpeningPresentationBarrier: createOpeningBarrier);
	}

	private static void AssertNoForbiddenGodotType(Type? type)
	{
		if (type == null)
		{
			return;
		}

		string fullName = type.FullName ?? type.Name;
		False(fullName == "Godot.Node"
			|| fullName == "Godot.GodotObject"
			|| fullName == "Godot.Texture2D"
			|| fullName == "Godot.Vector2"
			|| fullName == "Godot.Variant"
			|| fullName.StartsWith("Godot.Collections.", StringComparison.Ordinal));

		if (type.HasElementType)
		{
			AssertNoForbiddenGodotType(type.GetElementType());
		}

		if (type.IsGenericType)
		{
			foreach (Type genericArgument in type.GetGenericArguments())
			{
				AssertNoForbiddenGodotType(genericArgument);
			}
		}
	}

	private static bool ThrowsArgumentException(Action action)
	{
		try
		{
			action();
			return false;
		}
		catch (ArgumentException)
		{
			return true;
		}
	}

	private sealed class TestRuleModule : IRuleModule
	{
		internal TestRuleModule(
			string id,
			int order,
			ImmutableArray<string> dependencies = default,
			MatchCapabilitySet capabilities = default)
		{
			Id = id;
			Order = order;
			Dependencies = dependencies.IsDefault ? ImmutableArray<string>.Empty : dependencies;
			Capabilities = capabilities;
		}

		public string Id { get; }
		public int Order { get; }
		public ImmutableArray<string> Dependencies { get; }
		public MatchCapabilitySet Capabilities { get; }

		public RuleModuleDecision Evaluate(MatchCommand command, MatchReadModel readModel)
		{
			return RuleModuleDecision.Allow;
		}
	}

	private sealed class TestRuleRandomSource : IRuleRandomSource
	{
		private ulong _seed;
		private ulong _state;

		internal TestRuleRandomSource(ulong seed)
		{
			_seed = seed;
			_state = seed;
		}

		public RuleRandomState CaptureState()
		{
			return new RuleRandomState(_seed, _state);
		}

		public void RestoreState(RuleRandomState state)
		{
			_seed = state.Seed;
			_state = state.State;
		}

		public int NextIntInclusive(int minimum, int maximum)
		{
			if (minimum > maximum)
			{
				throw new ArgumentOutOfRangeException(nameof(minimum));
			}

			_state = unchecked(_state * 6364136223846793005UL + 1442695040888963407UL);
			ulong range = (ulong)((long)maximum - minimum + 1);
			return minimum + (int)(_state % range);
		}
	}
}
