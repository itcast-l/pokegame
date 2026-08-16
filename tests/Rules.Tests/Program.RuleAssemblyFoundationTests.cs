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
		Run("starting player dice follows clockwise seat order", StartingPlayerDiceFollowsClockwiseSeatOrder);
		Run("match initialization validates and applies starting turn seat", MatchInitializationValidatesAndAppliesStartingTurnSeat);
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
		Run("continuous capture rules enforce strict growth and big tide reset", ContinuousCaptureRulesEnforceStrictGrowthAndBigTideReset);
		Run("card play resolver blocks thresholds before extra capture effects", CardPlayResolverBlocksThresholdBeforeExtraCaptureEffects);
		Run("card play resolver classifies hidden threshold matches", CardPlayResolverClassifiesHiddenThresholdMatches);
		Run("authority continuous capture transaction records final table count", AuthorityContinuousCaptureTransactionRecordsFinalTableCount);
		Run("authority continuous capture threshold is shared across seats", AuthorityContinuousCaptureThresholdIsSharedAcrossSeats);
		Run("authority continuous capture sequence crosses seats and resets globally", AuthorityContinuousCaptureSequenceCrossesSeatsAndResetsGlobally);
		Run("authority continuous capture binds the current base-play identity", AuthorityContinuousCaptureBindsCurrentBasePlayIdentity);
		Run("continuous capture threshold survives authority and replica snapshots", ContinuousCaptureThresholdSurvivesSnapshots);
		Run("pending base-play capture survives snapshots and rejects tampering", PendingBasePlayCaptureSurvivesSnapshotsAndRejectsTampering);
		Run("pending base-play capture clears at action boundaries", PendingBasePlayCaptureClearsAtActionBoundaries);
		Run("continuous capture threshold persists across misses and turns then resets", ContinuousCaptureThresholdPersistsAcrossMissesAndTurnsThenResets);
		Run("direct capture bypasses continuous capture threshold", DirectCaptureBypassesContinuousCaptureThreshold);
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

}
