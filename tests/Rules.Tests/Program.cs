using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class Program
{
	private static int _passed;
	private static int _failed;

	public static int Main()
	{
		Run("capture scoring applies double effect and skips buried cards", CaptureScoringAppliesDoubleEffectAndSkipsBuriedCards);
		Run("capture scoring suppresses slime coated cards", CaptureScoringSuppressesSlimeCoatedCards);
		Run("capture scoring redirects pair destroy owner", CaptureScoringRedirectsPairDestroyOwner);
		Run("card play resolver ignores buried matches", CardPlayResolverIgnoresBuriedMatches);
		Run("card play resolver captures table for collect card", CardPlayResolverCapturesTableForCollectCard);
		Run("card play resolver uses alternate rank for borrow tide match", CardPlayResolverUsesAlternateRankForBorrowTideMatch);
		Run("borrow tide only grants alternate ranks to the enchanted card", BorrowTideOnlyGrantsAlternateRanksToTheEnchantedCard);
		Run("borrow tide only counts as used when alternate rank matched", BorrowTideOnlyCountsAsUsedWhenAlternateRankMatched);
		Run("card play resolver inserts miss at table start for reverse flow anchor", CardPlayResolverInsertsMissAtTableStartForReverseFlowAnchor);
		Run("card play resolver uses first matching suit for suit collect attachment", CardPlayResolverUsesFirstMatchingSuitForSuitCollectAttachment);
		Run("card play resolver falls back to rank when suit collect has no suit match", CardPlayResolverFallsBackToRankWhenSuitCollectHasNoSuitMatch);
		Run("card play resolver prefers suit start over later rank match for suit collect", CardPlayResolverPrefersSuitStartOverLaterRankMatchForSuitCollect);
		Run("card play resolver supports pending insert override and face up", CardPlayResolverSupportsPendingInsertOverrideAndFaceUp);
		Run("capture resolver identifies middle captures", CaptureResolverIdentifiesMiddleCaptures);
		Run("score service returns score breakdown", ScoreServiceReturnsScoreBreakdown);
		Run("capture flow planner builds capture summary", CaptureFlowPlannerBuildsCaptureSummary);
		Run("capture flow planner skips score when award disabled", CaptureFlowPlannerSkipsScoreWhenAwardDisabled);
		Run("adventure upgrade rules map main stages and weights", AdventureUpgradeRulesMapMainStagesAndWeights);
		Run("adventure upgrade choice skips acquired ids and deduplicates offer", AdventureUpgradeChoiceSkipsAcquiredIdsAndDeduplicatesOffer);
		Run("adventure upgrade choice falls back when rolled rarity unavailable", AdventureUpgradeChoiceFallsBackWhenRolledRarityUnavailable);
		Run("adventure upgrade choice returns remaining pool when below offer size", AdventureUpgradeChoiceReturnsRemainingPoolWhenBelowOfferSize);
		Run("adventure upgrade trigger ordering prefers big capture before victory", AdventureUpgradeTriggerOrderingPrefersBigCaptureBeforeVictory);
		Run("adventure upgrade trigger ordering includes run start before combat triggers", AdventureUpgradeTriggerOrderingIncludesRunStartFirst);
		Run("adventure upgrade run state tracks stage progression and acquired ids", AdventureUpgradeRunStateTracksStageProgressionAndAcquiredIds);
		Run("adventure upgrade ids expose fifty six active upgrades across four suits", AdventureUpgradeIdsExposeFiftySixActiveUpgradesAcrossFourSuits);
		Run("adventure upgrade run snapshot v2 restores legacy v1 saves", AdventureUpgradeRunSnapshotV2RestoresLegacyV1Saves);
		Run("adventure upgrade run state persists effect counters and health", AdventureUpgradeRunStatePersistsEffectCountersAndHealth);
		Run("adventure question reward side effects track count permanent bonus and final double", AdventureQuestionRewardSideEffectsTrackCountPermanentBonusAndFinalDouble);
		Run("adventure question reward support upgrades add heal draw and one time rejudge access", AdventureQuestionRewardSupportUpgradesAddHealDrawAndOneTimeRejudgeAccess);
		Run("question reward table render only waits for local hand player animation", QuestionRewardTableRenderOnlyWaitsForLocalHandPlayerAnimation);
		Run("adventure question reward direct damage reuses player attack animation", AdventureQuestionRewardDirectDamageReusesPlayerAttackAnimation);
		Run("adventure upgrade acquire applies immediate max health heal and draw bonuses", AdventureUpgradeAcquireAppliesImmediateMaxHealthHealAndDrawBonuses);
		Run("adventure battle start draw bonuses stack across acquired heart upgrades", AdventureBattleStartDrawBonusesStackAcrossAcquiredHeartUpgrades);
		Run("adventure healing overflow increases max health every four overflow", AdventureHealingOverflowIncreasesMaxHealthEveryFourOverflow);
		Run("adventure miss effects queue turn start recovery and next capture bonus", AdventureMissEffectsQueueTurnStartRecoveryAndNextCaptureBonus);
		Run("adventure miss effects include club table rewrites", AdventureMissEffectsIncludeClubTableRewrites);
		Run("adventure turn start effects are consumed once", AdventureTurnStartEffectsAreConsumedOnce);
		Run("adventure capture damage applies minimum and final double in order", AdventureCaptureDamageAppliesMinimumAndFinalDoubleInOrder);
		Run("adventure capture damage consumes carry bonus and re-arms high return bonus", AdventureCaptureDamageConsumesCarryBonusAndReArmsHighReturnBonus);
		Run("adventure capture damage applies first capture pressure once per battle", AdventureCaptureDamageAppliesFirstCapturePressureOncePerBattle);
		Run("adventure capture damage applies multi card premium and top prize repeat", AdventureCaptureDamageAppliesMultiCardPremiumAndTopPrizeRepeat);
		Run("adventure capture damage does not apply full payout below six scoring cards", AdventureCaptureDamageDoesNotApplyFullPayoutBelowSixScoringCards);
		Run("adventure capture damage doubles full payout at six scoring cards after final additions", AdventureCaptureDamageDoublesFullPayoutAtSixScoringCardsAfterFinalAdditions);
		Run("adventure capture damage stacks full payout with final double before high return bonus", AdventureCaptureDamageStacksFullPayoutWithFinalDoubleBeforeHighReturnBonus);
		Run("adventure capture damage adds bounty amplify when reward question is captured", AdventureCaptureDamageAddsBountyAmplifyWhenRewardQuestionIsCaptured);
		Run("adventure capture damage adds question echo point bonus per scoring card", AdventureCaptureDamageAddsQuestionEchoPointBonusPerScoringCard);
		Run("adventure post capture heal triggers after six damage", AdventurePostCaptureHealTriggersAfterSixDamage);
		Run("adventure post capture bonuses stack by capture ordinal", AdventurePostCaptureBonusesStackByCaptureOrdinal);
		Run("adventure post capture includes enchanted recovery and hollow burst", AdventurePostCaptureIncludesEnchantedRecoveryAndHollowBurst);
		Run("adventure low health rescue triggers at half and quarter thresholds once each", AdventureLowHealthRescueTriggersAtHalfAndQuarterThresholdsOnceEach);
		Run("adventure enemy miss effects queue reveal insert penalty and bleed damage", AdventureEnemyMissEffectsQueueRevealInsertPenaltyAndBleedDamage);
		Run("adventure enemy miss misorder only arms tracked anchor when old cards existed", AdventureEnemyMissMisorderOnlyArmsTrackedAnchorWhenOldCardsExisted);
		Run("adventure enemy miss grants extra base action from intercept beat", AdventureEnemyMissGrantsExtraBaseActionFromInterceptBeat);
		Run("adventure main player miss grants continue branch extra action only at four cards", AdventureMainPlayerMissGrantsContinueBranchExtraActionOnlyAtFourCards);
		Run("adventure black ledger changes old table score contribution by capturer", AdventureBlackLedgerChangesOldTableScoreContributionByCapturer);
		Run("adventure enemy miss hook dredge moves oldest old card and deals half damage", AdventureEnemyMissHookDredgeMovesOldestOldCardAndDealsHalfDamage);
		Run("adventure enemy tail pressure preserves played tail card on large capture", AdventureEnemyTailPressurePreservesPlayedTailCardOnLargeCapture);
		Run("adventure old table damage delta applies player mark bonuses", AdventureOldTableDamageDeltaAppliesPlayerMarkBonuses);
		Run("adventure old table damage delta applies enemy tax and exaction", AdventureOldTableDamageDeltaAppliesEnemyTaxAndExaction);
		Run("adventure clubs primary root return gives played base card back", AdventureClubsPrimaryRootReturnGivesPlayedBaseCardBack);
		Run("adventure clubs short branch return only applies to two-card capture once", AdventureClubsShortBranchReturnOnlyAppliesToTwoCardCaptureOnce);
		Run("adventure clubs climb canopy returns played base card after attached effect trigger", AdventureClubsClimbCanopyReturnsPlayedBaseCardAfterAttachedEffectTrigger);
		Run("adventure clubs crown canopy includes capture endpoints in scoring", AdventureClubsCrownCanopyIncludesCaptureEndpointsInScoring);
		Run("adventure clubs gap rooting captures left adjacent base once per turn", AdventureClubsGapRootingCapturesLeftAdjacentBaseOncePerTurn);
		Run("adventure clubs linked stems grants question reward on second capture", AdventureClubsLinkedStemsGrantsQuestionRewardOnSecondCapture);
		Run("adventure clubs miss climb growth and cross graft adjust match ranks", AdventureClubsMissClimbGrowthAndCrossGraftAdjustMatchRanks);
		Run("adventure clubs borrow suit set arms and is consumed by next numeric base card", AdventureClubsBorrowSuitSetArmsAndIsConsumedByNextNumericBaseCard);
		Run("adventure disabled undertow upgrade stays out of enabled pool", AdventureDisabledUndertowUpgradeStaysOutOfEnabledPool);
		Run("game save progress roundtrips pending adventure run json", GameSaveProgressRoundTripsPendingAdventureRunJson);
		Run("delete active profile switches replacement before removing original slot", DeleteActiveProfileSwitchesReplacementBeforeRemovingOriginalSlot);
		Run("delete active profile keeps original slot when replacement creation fails", DeleteActiveProfileKeepsOriginalSlotWhenReplacementCreationFails);
		Run("delete active profile keeps original slot when active switch fails", DeleteActiveProfileKeepsOriginalSlotWhenActiveSwitchFails);
		Run("damaged pending adventure run json clears from active profile", DamagedPendingAdventureRunJsonClearsFromActiveProfile);
		Run("valid pending adventure run json reloads active run", ValidPendingAdventureRunJsonReloadsActiveRun);
		Run("capture reveal order opens edges before middle", CaptureRevealOrderOpensEdgesBeforeMiddle);
		Run("reward resolver uses weighted reservoir selection", RewardResolverUsesWeightedReservoirSelection);
		Run("item card effect support blocks reserved empty effects", ItemCardEffectSupportBlocksReservedEmptyEffects);
		Run("playing card attachment replaces existing effect", PlayingCardAttachmentReplacesExistingEffect);
		Run("capture state apply capture clears effects when preserve disabled", CaptureStateApplyCaptureClearsEffectsWhenPreserveDisabled);
		Run("capture state apply capture preserves effects when preserve enabled", CaptureStateApplyCapturePreservesEffectsWhenPreserveEnabled);
		Run("captured card effect persists through deck and can be replaced", CapturedCardEffectPersistsThroughDeckAndCanBeReplaced);
		Run("capture refill shuffles before replacement draw animation", CaptureRefillShufflesBeforeReplacementDrawAnimation);
		Run("potential capture ignores buried matches", PotentialCaptureIgnoresBuriedMatches);
		Run("potential capture counts suit collect from first matching suit", PotentialCaptureCountsSuitCollectFromFirstMatchingSuit);
		Run("potential capture uses alternate ranks for club graft matching", PotentialCaptureUsesAlternateRanksForClubGraftMatching);
		Run("potential capture prefers royal threshold", PotentialCapturePrefersRoyalThreshold);
		Run("potential capture identifies pair only captures", PotentialCaptureIdentifiesPairOnlyCaptures);
		Run("adventure ai early profile can skip obvious capture", AdventureAiEarlyProfileCanSkipObviousCapture);
		Run("adventure ai early profile skips pair only capture", AdventureAiEarlyProfileSkipsPairOnlyCapture);
		Run("adventure ai point scoring starts after early stages", AdventureAiPointScoringStartsAfterEarlyStages);
		Run("adventure ai random capture can pick non-best capture", AdventureAiRandomCaptureCanPickNonBestCapture);
		Run("adventure ai late best capture skips pair only captures", AdventureAiLateBestCaptureSkipsPairOnlyCaptures);
		Run("adventure ai late random capture skips pair only captures", AdventureAiLateRandomCaptureSkipsPairOnlyCaptures);
		Run("adventure ai final profile can skip best capture", AdventureAiFinalProfileCanSkipBestCapture);
		Run("ai early table new card priority picks non matching card before capture", AiEarlyTableNewCardPriorityPicksNonMatchingCardBeforeCapture);
		Run("ai early table new card priority yields after four table cards", AiEarlyTableNewCardPriorityYieldsAfterFourTableCards);
		Run("ai early table new card priority ignores buried matches", AiEarlyTableNewCardPriorityIgnoresBuriedMatches);
		Run("solo ai difficulty profiles expose expected values", SoloAiDifficultyProfilesExposeExpectedValues);
		Run("solo ai medium capture prefers more cards while high prefers points", SoloAiMediumCapturePrefersMoreCardsWhileHighPrefersPoints);
		Run("solo ai medium and high skip pair only captures", SoloAiMediumAndHighSkipPairOnlyCaptures);
		Run("solo ai target player strategy picks largest relevant opponent", SoloAiTargetPlayerStrategyPicksLargestRelevantOpponent);
		Run("solo ai discard and magnet heuristics keep immediate captures when possible", SoloAiDiscardAndMagnetHeuristicsKeepImmediateCapturesWhenPossible);
		Run("lan address helper computes ipv4 broadcast address", LanAddressHelperComputesIpv4BroadcastAddress);
		Run("character audio config resources point to existing files", CharacterAudioConfigResourcesPointToExistingFiles);
		Run("character avatar visual resources point to existing files", CharacterAvatarVisualResourcesPointToExistingFiles);
		Run("character avatar display names are localized for every locale", CharacterAvatarDisplayNamesAreLocalizedForEveryLocale);
		Run("player count localization difficulty rows exist for every locale", PlayerCountLocalizationDifficultyRowsExistForEveryLocale);
		Run("runtime localization keys exist for every reference", RuntimeLocalizationKeysExistForEveryReference);
		Run("runtime localization rows cover every supported language", RuntimeLocalizationRowsCoverEverySupportedLanguage);
		Run("runtime scenes do not expose hardcoded natural language text", RuntimeScenesDoNotExposeHardcodedNaturalLanguageText);
		Run("avatar feedback effect resources stay contiguous", AvatarFeedbackEffectResourcesStayContiguous);
		Run("adventure upgrade popup scene has no embedded image payloads", AdventureUpgradePopupSceneHasNoEmbeddedImagePayloads);
		Run("adventure upgrade visual catalog exposes 12 processed card visuals", AdventureUpgradeVisualCatalogExposesTwelveProcessedCards);
		Run("adventure upgrade visual catalog includes palette fields and rarity tier mappings", AdventureUpgradeVisualCatalogIncludesPaletteFieldsAndRarityTierMappings);
		Run("adventure upgrade visual resolver returns atlas texture from catalog", AdventureUpgradeVisualResolverReturnsAtlasTextureFromCatalog);
		Run("adventure upgrade visual resolver uses catalog rarity tier mappings", AdventureUpgradeVisualResolverUsesCatalogRarityTierMappings);
		Run("adventure upgrade runtime no longer hardcodes popup art path switch tables", AdventureUpgradeRuntimeNoLongerHardcodesPopupArtSwitchTables);
		Run("adventure upgrade preview script reads popup art metadata from catalog", AdventureUpgradePreviewScriptReadsPopupArtMetadataFromCatalog);
		Run("adventure upgrade overlay sample art uses catalog keys instead of hardcoded atlas metadata", AdventureUpgradeOverlaySampleArtUsesCatalogKeysInsteadOfHardcodedAtlasMetadata);
		Run("item card visual resources point to existing files", ItemCardVisualResourcesPointToExistingFiles);
		Run("jack hand visual scale is normalized", JackHandVisualScaleIsNormalized);
		Run("attachment selection preview follows pending item card", AttachmentSelectionPreviewFollowsPendingItemCard);
		Run("main menu bubbles rise and fade through lifecycle", MainMenuBubblesRiseAndFadeThroughLifecycle);
		Run("main menu bubbles hide before crossing waterline", MainMenuBubblesHideBeforeCrossingWaterline);
		Run("main menu bubble visual resource points to existing file", MainMenuBubbleVisualResourcePointsToExistingFile);
		Run("board effect resources point to existing files", BoardEffectResourcesPointToExistingFiles);
		Run("game loop phase precedence is stable", GameLoopPhasePrecedenceIsStable);
		Run("result presentation uses local seat for network defeat", ResultPresentationUsesLocalSeatForNetworkDefeat);
		Run("result presentation preserves local win and offline rules", ResultPresentationPreservesLocalWinAndOfflineRules);
		Run("result presentation disables network replay primary action", ResultPresentationDisablesNetworkReplayPrimaryAction);
		Run("turn state machine advances context generation", TurnStateMachineAdvancesContextGeneration);
		Run("turn state machine gates action starts", TurnStateMachineGatesActionStarts);
		Run("turn rules skip inactive players", TurnRulesSkipInactivePlayers);
		Run("turn rules find single remaining player", TurnRulesFindSingleRemainingPlayer);
		Run("turn progress starts and resets question card tracking", TurnProgressStartsAndResetsQuestionCardTracking);
		Run("turn progress waits for anchor turn before refreshing question card", TurnProgressWaitsForAnchorTurnBeforeRefreshingQuestionCard);
		Run("turn progress refreshes question card after configured cycle", TurnProgressRefreshesQuestionCardAfterConfiguredCycle);
		Run("turn progress stays blocked when table special cards are present", TurnProgressStaysBlockedWhenTableSpecialCardsArePresent);
		Run("room name rules restrict special symbols", RoomNameRulesRestrictSpecialSymbols);
		Run("steam metadata rejects dirty values", SteamMetadataRejectsDirtyValues);
		Run("steam metadata parses valid values defensively", SteamMetadataParsesValidValuesDefensively);
		Run("steam metadata keeps active match lobby joinable for reconnect", SteamMetadataKeepsActiveMatchLobbyJoinableForReconnect);
		Run("multiplayer lobby start requires filled ready seats", MultiplayerLobbyStartRequiresFilledReadySeats);
		Run("multiplayer lobby only accepts join requests in lobby state", MultiplayerLobbyOnlyAcceptsJoinRequestsInLobbyState);
		Run("multiplayer lobby room list only allows joining lobby rooms", MultiplayerLobbyRoomListOnlyAllowsJoiningLobbyRooms);
		Run("multiplayer lobby only auto accepts steam invites from safe states", MultiplayerLobbyOnlyAutoAcceptsSteamInvitesFromSafeStates);
		Run("multiplayer board sync resolves sender seat", MultiplayerBoardSyncResolvesSenderSeat);
		Run("scene load ack accepts only sender own seat", SceneLoadAckAcceptsOnlySenderOwnSeat);
		Run("scene load ack rejects unregistered sender", SceneLoadAckRejectsUnregisteredSender);
		Run("scene load ack rejects mismatched reported seat", SceneLoadAckRejectsMismatchedReportedSeat);
		Run("multiplayer board sync rejects invalid play requests", MultiplayerBoardSyncRejectsInvalidPlayRequests);
		Run("multiplayer board sync limits private hand visibility", MultiplayerBoardSyncLimitsPrivateHandVisibility);
		Run("network table card snapshot roundtrips overlay fields", NetworkTableCardSnapshotRoundtripsOverlayFields);
		Run("local seat input enables only for in-game human connected state", LocalSeatInputEnablesOnlyForInGameHumanConnectedState);
		Run("network seat control accepts remote actions only for connected humans", NetworkSeatControlAcceptsRemoteActionsOnlyForConnectedHumans);
		Run("network seat control reveals pending target hand only for connected humans", NetworkSeatControlRevealsPendingTargetHandOnlyForConnectedHumans);
		Run("network seat control sends pending selection requests only when client is ready", NetworkSeatControlSendsPendingSelectionRequestsOnlyWhenClientIsReady);
		Run("game board exported setters short circuit redundant work", GameBoardExportedSettersShortCircuitRedundantWork);
		Run("decorative ui art avoids unconditional per frame redraws", DecorativeUiArtAvoidsUnconditionalPerFrameRedraws);
		Run("dynamic background layers gate image mode redraw to fallback needs", DynamicBackgroundLayersGateImageModeRedrawToFallbackNeeds);
		Run("main menu ocean dynamics keeps procedural fallback paths in image mode", MainMenuOceanDynamicsKeepsProceduralFallbackPathsInImageMode);
		Run("game board defers mode specific resource loads until needed", GameBoardDefersModeSpecificResourceLoadsUntilNeeded);
		Run("game board optional fx resources are no longer statically preloaded", GameBoardOptionalFxResourcesAreNoLongerStaticallyPreloaded);
		Run("game board warmup includes optional fx resource category", GameBoardWarmupIncludesOptionalFxResources);
		Run("game board idle performance trace smoke exists", GameBoardIdlePerformanceTraceSmokeExists);
		Run("resource export audit script checks tracked assets and export config", ResourceExportAuditScriptChecksTrackedAssetsAndExportConfig);
		Run("resource export audit doc describes read only command", ResourceExportAuditDocDescribesReadOnlyCommand);
		Run("export preset excludes upgrade popup source only assets", ExportPresetExcludesUpgradePopupSourceOnlyAssets);
		Run("gitignore covers upgrade popup source only assets", GitignoreCoversUpgradePopupSourceOnlyAssets);
		Run("tracked generated asset inventory doc summarizes current categories", TrackedGeneratedAssetInventoryDocSummarizesCurrentCategories);
		Run("tracked generated asset inventory doc includes suggested manual cleanup batches", TrackedGeneratedAssetInventoryDocIncludesSuggestedManualCleanupBatches);
		Run("network manager serialization helpers move into partial file", NetworkManagerSerializationHelpersMoveIntoPartialFile);
		Run("network manager lan discovery helpers move into partial file", NetworkManagerLanDiscoveryHelpersMoveIntoPartialFile);
		Run("network manager steam helpers move into partial file", NetworkManagerSteamHelpersMoveIntoPartialFile);
		Run("network manager reconnect helpers move into partial file", NetworkManagerReconnectHelpersMoveIntoPartialFile);
		Run("network manager lobby helpers move into partial file", NetworkManagerLobbyHelpersMoveIntoPartialFile);
		Run("network manager board sync helpers move into partial file", NetworkManagerBoardSyncHelpersMoveIntoPartialFile);
		Run("game board network snapshot helpers move into partial file", GameBoardNetworkSnapshotHelpersMoveIntoPartialFile);
		Run("game board network request entry points avoid async void", GameBoardNetworkRequestEntryPointsAvoidAsyncVoid);
		Run("target item selection completion avoids async void", TargetItemSelectionCompletionAvoidsAsyncVoid);
		Run("network manager scene load ack validates sender before mark loaded", NetworkManagerSceneLoadAckValidatesSenderBeforeMarkingLoaded);
		Run("game board network visual queue helpers move into partial file", GameBoardNetworkVisualQueueHelpersMoveIntoPartialFile);
		Run("game board network prediction helpers move into partial file", GameBoardNetworkPredictionHelpersMoveIntoPartialFile);
		Run("game board network authority helpers move into partial file", GameBoardNetworkAuthorityHelpersMoveIntoPartialFile);
		Run("game board network ai proxy helpers move into partial file", GameBoardNetworkAiProxyHelpersMoveIntoPartialFile);
		Run("game board network card helpers move into partial file", GameBoardNetworkCardHelpersMoveIntoPartialFile);
		Run("network ai playable turn action ignores blocked item-only turns", NetworkAiPlayableTurnActionIgnoresBlockedItemOnlyTurns);
		Run("network ai proxy capture profile matches weakest adventure ai", NetworkAiProxyCaptureProfileMatchesWeakestAdventureAi);
		Run("network ai resumes proxy turn only after authority action settles", NetworkAiResumesProxyTurnOnlyAfterAuthorityActionSettles);
		Run("network ai starts proxy turn for ai-owned pending selection", NetworkAiStartsProxyTurnForAiOwnedPendingSelection);
		Run("network ai recovers only for host side unhandled failures", NetworkAiRecoversOnlyForHostSideUnhandledFailures);
		Run("network ai blocked item skip is scoped to exact turn and item", NetworkAiBlockedItemSkipIsScopedToExactTurnAndItem);
		Run("network ai pending choice fallback picks only legal candidates", NetworkAiPendingChoiceFallbackPicksOnlyLegalCandidates);
		Run("network ai pending choice fallback fails without legal candidates", NetworkAiPendingChoiceFallbackFailsWithoutLegalCandidates);
		Run("network reconnect credentials are sent before first board snapshot", NetworkReconnectCredentialsAreSentBeforeFirstBoardSnapshot);
		Run("network reconnect rules defer claim while seat still connected", NetworkReconnectRulesDeferClaimWhileSeatStillConnected);
		Run("network reconnect rules end match before reconnect after host termination", NetworkReconnectRulesEndMatchBeforeReconnectAfterHostTermination);
		Run("network reconnect rules ignore stale disconnect after reconnect", NetworkReconnectRulesIgnoreStaleDisconnectAfterReconnect);
		Run("network reconnect rules only hand off non host disconnects to ai", NetworkReconnectRulesOnlyHandOffNonHostDisconnectsToAi);
		Run("network reconnect rules scope realtime board events to active in-game clients", NetworkReconnectRulesScopeRealtimeBoardEventsToActiveInGameClients);
		Run("network reconnect rules classify steam lobby join retries", NetworkReconnectRulesClassifySteamLobbyJoinRetries);
		Run("network reconnect rules reject steam owner migration during reconnect", NetworkReconnectRulesRejectSteamOwnerMigrationDuringReconnect);
		Run("network reconnect rules apply reconnect snapshot immediately on client", NetworkReconnectRulesApplyReconnectSnapshotImmediatelyOnClient);
		Run("network reconnect rules complete catch up only after visual queue drains", NetworkReconnectRulesCompleteCatchUpOnlyAfterVisualQueueDrains);
		Run("network reconnect overlay shows only during client catch up", NetworkReconnectOverlayShowsOnlyDuringClientCatchUp);
		Run("network reconnect presentation matches reconnect plan copy", NetworkReconnectPresentationMatchesReconnectPlanCopy);
		Run("seat control disconnect transitions to ai reclaimable", SeatControlDisconnectTransitionsToAiReclaimable);
		Run("seat control reclaim pending disconnect falls back to ai reclaimable", SeatControlReclaimPendingDisconnectFallsBackToAiReclaimable);
		Run("seat control reconnect claim is idempotent for same peer", SeatControlReconnectClaimIsIdempotentForSamePeer);
		Run("seat control reconnect claim rejects conflicting peer", SeatControlReconnectClaimRejectsConflictingPeer);
		Run("seat control stays reclaim pending until own turn start", SeatControlStaysReclaimPendingUntilOwnTurnStart);
		Run("seat control promotes reclaimed seat at turn start", SeatControlPromotesReclaimedSeatAtTurnStart);
		Run("seat control allows reclaim again after reclaim pending disconnect", SeatControlAllowsReclaimAgainAfterReclaimPendingDisconnect);
		Run("seat control rejects invalid reconnect metadata", SeatControlRejectsInvalidReconnectMetadata);
		Run("seat control rejects reconnect claim after match end", SeatControlRejectsReconnectClaimAfterMatchEnd);
		Run("seat control shared snapshot redacts reconnect token", SeatControlSharedSnapshotRedactsReconnectToken);
		Run("scene load handshake completes and times out", SceneLoadHandshakeCompletesAndTimesOut);
		Run("lan discovery parses valid messages and drops dirty data", LanDiscoveryParsesValidMessagesAndDropsDirtyData);
		Run("local seat board controls block until human control is restored", LocalSeatBoardControlsBlockUntilHumanControlIsRestored);

		Console.WriteLine($"{_passed} passed, {_failed} failed");
		return _failed == 0 ? 0 : 1;
	}

	private static void CaptureScoringAppliesDoubleEffectAndSkipsBuriedCards()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 5),
			TableCard("3", 7, isBuriedInSand: true),
			TableCard("4", 3, hasDoubleCollectItemEffect: true),
			TableCard("5", 9),
		};

		int points = CaptureScoringRules.GetCapturedTablePoints(tableCards, 0, DefaultScoringContext);

		Equal(16, points);
	}

	private static void CaptureScoringSuppressesSlimeCoatedCards()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 5, isSlimeCoated: true),
			TableCard("3", 4),
			TableCard("4", 9),
		};
		var context = new CaptureScoringRules.Context(
			SuppressSlimeCoatedScore: true,
			ApplyScoreReductionPerCard: false,
			ScoreReductionPerCard: 0);

		int points = CaptureScoringRules.GetCapturedTablePoints(tableCards, 0, context);

		Equal(4, points);
	}

	private static void CaptureScoringRedirectsPairDestroyOwner()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 5, hasPairDestroyItemEffect: true, itemOwnerPlayerIndex: 1),
			TableCard("3", 4),
			TableCard("4", 9, hasPairDestroyItemEffect: true, itemOwnerPlayerIndex: 2),
		};

		int playerIndex = CaptureScoringRules.GetCapturedTableScorePlayerIndex(
			tableCards,
			captureStartIndex: 0,
			fallbackPlayerIndex: 0,
			playerCount: 4);

		Equal(2, playerIndex);
	}

	private static void CardPlayResolverIgnoresBuriedMatches()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: true),
			new CardPlayTableCard("K", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("A", IsFaceUp: false, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(tableCards, new CardPlayCard("A", CollectsTable: false));

		Equal(2, plan.MatchingTableCardIndex);
		Equal(2, plan.CaptureStartIndex);
		True(plan.MatchedTableCardFlipped);
	}

	private static void CardPlayResolverCapturesTableForCollectCard()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("3", IsFaceUp: true, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(tableCards, new CardPlayCard("Q", CollectsTable: true));

		Equal(-1, plan.MatchingTableCardIndex);
		True(plan.PlayedCardCollectsTable);
		Equal(0, plan.CaptureStartIndex);
	}

	private static void JackHandVisualScaleIsNormalized()
	{
		Equal(1.0f, CardVisualTuningRules.GetPlayingCardHandVisualScale("A"));
		Equal(1.08f, CardVisualTuningRules.GetPlayingCardHandVisualScale("J"));
		Equal(1.08f, CardVisualTuningRules.GetItemCardHandVisualScale("J"));
	}

	private static void CardPlayResolverUsesAlternateRankForBorrowTideMatch()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("6", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("7", IsFaceUp: false, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("8", CollectsTable: false),
			new[] { "7", "9" },
			insertAtTableStart: false);

		Equal(1, plan.MatchingTableCardIndex);
		Equal(1, plan.CaptureStartIndex);
		Equal("7", plan.EffectiveRank);
		True(plan.MatchedTableCardFlipped);
		Equal(2, plan.TableInsertIndex);
	}

	private static void BorrowTideOnlyGrantsAlternateRanksToTheEnchantedCard()
	{
		IReadOnlyList<string> noEffectRanks = BorrowTideRules.GetAlternateRanks(
			cardHasBorrowTideEffect: false,
			playedRank: "8");
		Equal(0, noEffectRanks.Count);

		IReadOnlyList<string> enchantedRanks = BorrowTideRules.GetAlternateRanks(
			cardHasBorrowTideEffect: true,
			playedRank: "8");
		Equal(2, enchantedRanks.Count);
		Equal("7", enchantedRanks[0]);
		Equal("9", enchantedRanks[1]);

		IReadOnlyList<string> nonNumericRanks = BorrowTideRules.GetAlternateRanks(
			cardHasBorrowTideEffect: true,
			playedRank: "Q");
		Equal(0, nonNumericRanks.Count);
	}

	private static void BorrowTideOnlyCountsAsUsedWhenAlternateRankMatched()
	{
		False(BorrowTideRules.DidUseBorrowTide(
			cardHasBorrowTideEffect: false,
			playedRank: "8",
			effectiveRank: "7"));
		False(BorrowTideRules.DidUseBorrowTide(
			cardHasBorrowTideEffect: true,
			playedRank: "8",
			effectiveRank: "8"));
		True(BorrowTideRules.DidUseBorrowTide(
			cardHasBorrowTideEffect: true,
			playedRank: "8",
			effectiveRank: "7"));
	}

	private static void CardPlayResolverInsertsMissAtTableStartForReverseFlowAnchor()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("4", IsFaceUp: true, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("9", CollectsTable: false),
			alternateRanks: null,
			insertAtTableStart: true);

		Equal(-1, plan.MatchingTableCardIndex);
		Equal(-1, plan.CaptureStartIndex);
		Equal("9", plan.EffectiveRank);
		Equal(0, plan.TableInsertIndex);
		False(plan.MatchedTableCardFlipped);
	}

	private static void CardPlayResolverUsesFirstMatchingSuitForSuitCollectAttachment()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false, Suit: "clubs"),
			new CardPlayTableCard("7", IsFaceUp: false, IsBuriedInSand: false, Suit: "hearts"),
			new CardPlayTableCard("9", IsFaceUp: true, IsBuriedInSand: false, Suit: "spades"),
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: false, Suit: "hearts"),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("5", CollectsTable: false, Suit: "hearts", CollectBySuit: true));

		Equal(1, plan.MatchingTableCardIndex);
		Equal(1, plan.CaptureStartIndex);
		Equal("5", plan.EffectiveRank);
		True(plan.MatchedTableCardFlipped);
	}

	private static void CardPlayResolverFallsBackToRankWhenSuitCollectHasNoSuitMatch()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("5", IsFaceUp: true, IsBuriedInSand: false, Suit: "clubs"),
			new CardPlayTableCard("8", IsFaceUp: true, IsBuriedInSand: false, Suit: "diamonds"),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("5", CollectsTable: false, Suit: "hearts", CollectBySuit: true));

		Equal(0, plan.MatchingTableCardIndex);
		Equal(0, plan.CaptureStartIndex);
		Equal("5", plan.EffectiveRank);
		False(plan.MatchedTableCardFlipped);
	}

	private static void CardPlayResolverPrefersSuitStartOverLaterRankMatchForSuitCollect()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false, Suit: "hearts"),
			new CardPlayTableCard("5", IsFaceUp: true, IsBuriedInSand: false, Suit: "clubs"),
			new CardPlayTableCard("8", IsFaceUp: true, IsBuriedInSand: false, Suit: "spades"),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("5", CollectsTable: false, Suit: "hearts", CollectBySuit: true));

		Equal(0, plan.MatchingTableCardIndex);
		Equal(0, plan.CaptureStartIndex);
		Equal("5", plan.EffectiveRank);
	}

	private static void CardPlayResolverSupportsPendingInsertOverrideAndFaceUp()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("7", IsFaceUp: true, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("5", CollectsTable: false),
			alternateRanks: null,
			insertAtTableStart: false,
			tableInsertIndexOverride: 1,
			playedCardFacesUp: true);

		Equal(1, plan.TableInsertIndex);
		True(plan.PlayedCardFacesUp);
	}

	private static void CaptureResolverIdentifiesMiddleCaptures()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2, isBuriedInSand: true),
			TableCard("3", 3),
			TableCard("4", 4),
		};

		CapturePlan plan = CaptureResolver.Resolve(tableCards, 0);

		True(plan.Captured);
		True(plan.CapturedMiddleCards);
	}

	private static void ScoreServiceReturnsScoreBreakdown()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 5),
			TableCard("3", 7, hasDoubleCollectItemEffect: true),
			TableCard("4", 9),
		};

		ScoreBreakdown score = ScoreService.GetCapturedScore(
			tableCards,
			captureStartIndex: 0,
			scorePlayerIndex: 1,
			DefaultScoringContext);

		Equal(1, score.ScorePlayerIndex);
		True(score.HasDoubleEffect);
		Equal(24, score.Points);
	}

	private static void CaptureFlowPlannerBuildsCaptureSummary()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("3", 3, hasDoubleCollectItemEffect: true),
			TableCard("4", 4),
		};
		var extras = new HashSet<int> { 1 };

		CaptureFlowPlan plan = CaptureFlowPlanner.Build(
			tableCards,
			captureStartIndex: 0,
			scorePlayerIndex: 2,
			DefaultScoringContext,
			awardScore: true,
			extras);

		True(plan.Capture.Captured);
		True(plan.Capture.CapturedMiddleCards);
		Equal(2, plan.Score.ScorePlayerIndex);
		True(plan.Score.HasDoubleEffect);
		True(plan.Score.Points > 0);
		True(plan.CapturedScoringCards);
		True(plan.ShouldTrackQuestionCardRefresh);
		Equal(4, plan.CaptureCount);
		Equal(1, plan.ExtraCaptureCount);
		Equal(5, plan.TotalCapturedCardCount);
	}

	private static void CaptureFlowPlannerSkipsScoreWhenAwardDisabled()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("3", 3),
		};

		CaptureFlowPlan plan = CaptureFlowPlanner.Build(
			tableCards,
			captureStartIndex: 0,
			scorePlayerIndex: 1,
			DefaultScoringContext,
			awardScore: false);

		True(plan.Capture.Captured);
		True(plan.Capture.CapturedMiddleCards);
		Equal(1, plan.Score.ScorePlayerIndex);
		Equal(0, plan.Score.Points);
		False(plan.Score.HasDoubleEffect);
		True(plan.ShouldTrackQuestionCardRefresh);
	}

	private static void AdventureUpgradeRulesMapMainStagesAndWeights()
	{
		False(AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(2));
		True(AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(3));
		True(AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(14));
		False(AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(15));

		Equal(1, AdventureUpgradeChoiceRules.ToRunStageIndex(3));
		Equal(4, AdventureUpgradeChoiceRules.ToRunStageIndex(6));
		Equal(12, AdventureUpgradeChoiceRules.ToRunStageIndex(14));

		AdventureUpgradeRarityWeights early = AdventureUpgradeChoiceRules.GetRarityWeights(1);
		AdventureUpgradeRarityWeights mid = AdventureUpgradeChoiceRules.GetRarityWeights(5);
		AdventureUpgradeRarityWeights late = AdventureUpgradeChoiceRules.GetRarityWeights(9);

		Equal(70, early.CommonWeight);
		Equal(25, early.RareWeight);
		Equal(5, early.LegendaryWeight);
		Equal(50, mid.CommonWeight);
		Equal(35, mid.RareWeight);
		Equal(15, mid.LegendaryWeight);
		Equal(30, late.CommonWeight);
		Equal(45, late.RareWeight);
		Equal(25, late.LegendaryWeight);

		False(AdventureUpgradeChoiceRules.ShouldGrantBigCapture(6));
		True(AdventureUpgradeChoiceRules.ShouldGrantBigCapture(7));
	}

	private static void AdventureUpgradeChoiceSkipsAcquiredIdsAndDeduplicatesOffer()
	{
		var definitions = new[]
		{
			Upgrade("common_a", "common"),
			Upgrade("common_b", "common"),
			Upgrade("common_c", "common"),
			Upgrade("rare_a", "rare"),
		};
		var acquiredIds = new HashSet<string>(StringComparer.Ordinal)
		{
			"common_a",
		};
		int[] rolls = { 0, 0, 0, 0, 0, 0 };
		int rollIndex = 0;

		IReadOnlyList<AdventureUpgradeDefinitionModel> offer = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			acquiredIds,
			runStageIndex: 1,
			maxExclusive => rolls[rollIndex++] % maxExclusive);

		Equal(3, offer.Count);
		False(ContainsUpgradeId(offer, "common_a"));
		True(ContainsUpgradeId(offer, "common_b"));
		True(ContainsUpgradeId(offer, "common_c"));
		True(ContainsUpgradeId(offer, "rare_a"));
		True(HasUniqueUpgradeIds(offer));
	}

	private static void AdventureUpgradeChoiceFallsBackWhenRolledRarityUnavailable()
	{
		var definitions = new[]
		{
			Upgrade("common_a", "common"),
			Upgrade("common_b", "common"),
			Upgrade("rare_a", "rare"),
			Upgrade("rare_b", "rare"),
		};
		int[] rolls = { 99, 1, 0, 0, 99, 0 };
		int rollIndex = 0;

		IReadOnlyList<AdventureUpgradeDefinitionModel> offer = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			acquiredIds: Array.Empty<string>(),
			runStageIndex: 1,
			maxExclusive => rolls[rollIndex++] % maxExclusive);

		Equal(3, offer.Count);
		Equal("rare_b", offer[0].Id);
		Equal("common_a", offer[1].Id);
		Equal("rare_a", offer[2].Id);
	}

	private static void AdventureUpgradeChoiceReturnsRemainingPoolWhenBelowOfferSize()
	{
		var definitions = new[]
		{
			Upgrade("legendary_a", "legendary"),
			Upgrade("rare_a", "rare"),
		};

		IReadOnlyList<AdventureUpgradeDefinitionModel> offer = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			acquiredIds: Array.Empty<string>(),
			runStageIndex: 12,
			maxExclusive => throw new InvalidOperationException("random should not be used when fewer than three upgrades remain"));

		Equal(2, offer.Count);
		Equal("legendary_a", offer[0].Id);
		Equal("rare_a", offer[1].Id);
	}

	private static void AdventureUpgradeTriggerOrderingPrefersBigCaptureBeforeVictory()
	{
		IReadOnlyList<AdventureUpgradeTriggerSource> triggerOrder = AdventureUpgradeTriggerRules.BuildTriggerOrder(
			runStartTriggered: false,
			bigCaptureTriggered: true,
			victoryTriggered: true);

		Equal(2, triggerOrder.Count);
		Equal(AdventureUpgradeTriggerSource.BigCapture, triggerOrder[0]);
		Equal(AdventureUpgradeTriggerSource.Victory, triggerOrder[1]);
	}

	private static void AdventureUpgradeTriggerOrderingIncludesRunStartFirst()
	{
		IReadOnlyList<AdventureUpgradeTriggerSource> triggerOrder = AdventureUpgradeTriggerRules.BuildTriggerOrder(
			runStartTriggered: true,
			bigCaptureTriggered: true,
			victoryTriggered: true);

		Equal(3, triggerOrder.Count);
		Equal(AdventureUpgradeTriggerSource.RunStart, triggerOrder[0]);
		Equal(AdventureUpgradeTriggerSource.BigCapture, triggerOrder[1]);
		Equal(AdventureUpgradeTriggerSource.Victory, triggerOrder[2]);
	}

	private static void AdventureUpgradeRunStateTracksStageProgressionAndAcquiredIds()
	{
		var runState = new AdventureUpgradeRunState();
		runState.BeginNewRun(avatarStageNumber: 3);

		True(runState.IsActive);
		Equal(1, runState.CurrentRunStageIndex);
		True(runState.ShouldGrantRunStartUpgrade);
		True(runState.ShouldGrantVictoryUpgrade);

		runState.MarkRunStartUpgradeGranted();
		runState.AcquireUpgrade("common_a");
		False(runState.ShouldGrantRunStartUpgrade);
		True(runState.HasAcquiredUpgrade("common_a"));

		runState.MarkVictoryUpgradeGranted();
		False(runState.ShouldGrantVictoryUpgrade);

		runState.AdvanceToStage(avatarStageNumber: 4);
		Equal(2, runState.CurrentRunStageIndex);
		False(runState.ShouldGrantRunStartUpgrade);
		True(runState.ShouldGrantVictoryUpgrade);
		True(runState.HasAcquiredUpgrade("common_a"));

		runState.Clear();
		False(runState.IsActive);
		Equal(0, runState.CurrentRunStageIndex);
		False(runState.HasAcquiredUpgrade("common_a"));
	}

	private static void AdventureUpgradeIdsExposeFiftySixActiveUpgradesAcrossFourSuits()
	{
		string repoRoot = FindRepositoryRoot();
		string idsPath = Path.Combine(repoRoot, "pandora", "adventure_suit_upgrade_ids.gd");
		True(File.Exists(idsPath));
		string text = File.ReadAllText(idsPath);

		MatchCollection constants = Regex.Matches(text, @"(?m)^const ([A-Z0-9_]+) = ""([a-z0-9_]+)""$");
		Equal(57, constants.Count);

		var activePerSuit = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["hearts"] = 0,
			["diamonds"] = 0,
			["spades"] = 0,
			["clubs"] = 0,
		};

		foreach (Match constant in constants)
		{
			string id = constant.Groups[2].Value;
			if (id == "spades_undertow_asphyxia")
			{
				continue;
			}

			string suit = id.Split('_')[0];
			activePerSuit[suit] = activePerSuit[suit] + 1;
		}

		Equal(14, activePerSuit["hearts"]);
		Equal(14, activePerSuit["diamonds"]);
		Equal(14, activePerSuit["spades"]);
		Equal(14, activePerSuit["clubs"]);
	}

	private static void AdventureUpgradeRunSnapshotV2RestoresLegacyV1Saves()
	{
		AdventureUpgradeRunSnapshot legacy = new(
			1,
			3,
			1,
			true,
			true,
			new List<string> { "hearts_thick_blood" });

		var state = new AdventureUpgradeRunState();
		state.RestoreFromSnapshot(legacy);

		True(state.IsActive);
		Equal(100, state.CurrentHealth);
		Equal(100, state.MaxHealth);
		True(state.HasAcquiredUpgrade("hearts_thick_blood"));
		Equal(0, state.EffectState.PermanentCaptureDamageBonus);
		Equal(0, state.EffectState.QuestionRewardCount);
		False(state.EffectState.ThirdQuestionDamageDoubleActive);
	}

	private static void AdventureUpgradeRunStatePersistsEffectCountersAndHealth()
	{
		var state = new AdventureUpgradeRunState();
		state.BeginNewRun(3);
		state.SetHealth(92, 108);
		state.ReplaceEffectState(new AdventureUpgradeEffectState
		{
			PermanentCaptureDamageBonus = 2,
			QuestionRewardCount = 3,
			ThirdQuestionDamageDoubleActive = true,
			HealingOverflowRemainder = 1,
			NextCaptureDamageBonus = 4,
			NextTurnHealAmount = 3,
			NextTurnDrawCount = 1,
		});

		AdventureUpgradeRunSnapshot snapshot = state.ToSnapshot();
		var restored = new AdventureUpgradeRunState();
		restored.RestoreFromSnapshot(snapshot);

		Equal(92, restored.CurrentHealth);
		Equal(108, restored.MaxHealth);
		Equal(2, restored.EffectState.PermanentCaptureDamageBonus);
		Equal(3, restored.EffectState.QuestionRewardCount);
		True(restored.EffectState.ThirdQuestionDamageDoubleActive);
		Equal(1, restored.EffectState.HealingOverflowRemainder);
		Equal(4, restored.EffectState.NextCaptureDamageBonus);
		Equal(3, restored.EffectState.NextTurnHealAmount);
		Equal(1, restored.EffectState.NextTurnDrawCount);
	}

	private static void AdventureQuestionRewardSideEffectsTrackCountPermanentBonusAndFinalDouble()
	{
		var state = new AdventureUpgradeEffectState();
		string[] upgrades =
		{
			"diamonds_question_mark_windfall",
			"diamonds_question_mark_interest",
			"diamonds_final_double",
		};

		for (int rewardCount = 1; rewardCount <= 9; rewardCount++)
		{
			AdventureQuestionRewardEffectResult result = AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);
			Equal(rewardCount, result.BonusDirectDamage);
			Equal(rewardCount, state.QuestionRewardCount);
			Equal(rewardCount, state.PermanentCaptureDamageBonus);
			False(state.ThirdQuestionDamageDoubleActive);
		}

		AdventureQuestionRewardEffectResult tenth = AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);

		Equal(10, tenth.BonusDirectDamage);
		Equal(10, state.QuestionRewardCount);
		Equal(10, state.PermanentCaptureDamageBonus);
		True(state.ThirdQuestionDamageDoubleActive);
	}

	private static void AdventureQuestionRewardSupportUpgradesAddHealDrawAndOneTimeRejudgeAccess()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureQuestionRewardEffectResult result = AdventureUpgradeEffectService.OnQuestionRewardGranted(
			state,
			new[]
			{
				"diamonds_question_mark_raise",
				"hearts_question_mark_first_aid",
				"hearts_supply_cache_return",
			});

		Equal(7, result.HealAmount);
		Equal(2, result.DrawCount);
		True(AdventureUpgradeEffectService.ShouldOfferQuestionRewardRejudge(
			new[] { "diamonds_rejudge_once" },
			questionRejudgeUsedThisBattle: false));
		False(AdventureUpgradeEffectService.ShouldOfferQuestionRewardRejudge(
			new[] { "diamonds_rejudge_once" },
			questionRejudgeUsedThisBattle: true));
	}

	private static void QuestionRewardTableRenderOnlyWaitsForLocalHandPlayerAnimation()
	{
		False(QuestionRewardPresentationRules.ShouldDelayTableRenderForQuestionReward(
			hasQuestionRewardAnimation: false,
			isLocalHandPlayer: true));
		False(QuestionRewardPresentationRules.ShouldDelayTableRenderForQuestionReward(
			hasQuestionRewardAnimation: true,
			isLocalHandPlayer: false));
		True(QuestionRewardPresentationRules.ShouldDelayTableRenderForQuestionReward(
			hasQuestionRewardAnimation: true,
			isLocalHandPlayer: true));
	}

	private static void AdventureQuestionRewardDirectDamageReusesPlayerAttackAnimation()
	{
		string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "GameBoardScene.AdventureUpgrades.cs"));
		int methodStart = source.IndexOf("private async Task ApplyAdventureQuestionRewardEffects(", StringComparison.Ordinal);
		int methodEnd = source.IndexOf("private void ApplyAdventureUpgradeAcquisitionEffects(", methodStart, StringComparison.Ordinal);

		True(methodStart >= 0);
		True(methodEnd > methodStart);

		string method = source[methodStart..methodEnd];
		int attackAnimationIndex = method.IndexOf("await PlayAdventureAvatarAttackAsync(", StringComparison.Ordinal);
		int damageCallbackIndex = method.IndexOf("() => AddPlayerScore(playerIndex, resolved.BonusDirectDamage)", StringComparison.Ordinal);

		True(attackAnimationIndex >= 0);
		True(damageCallbackIndex > attackAnimationIndex);
	}

	private static void AdventureUpgradeAcquireAppliesImmediateMaxHealthHealAndDrawBonuses()
	{
		AdventureUpgradeAcquisitionEffectResult thickBlood = AdventureUpgradeEffectService.OnUpgradeAcquired(
			new AdventureUpgradeEffectState(),
			"hearts_thick_blood",
			triggerBattleStartBonusImmediately: false);
		AdventureUpgradeAcquisitionEffectResult deepTideLung = AdventureUpgradeEffectService.OnUpgradeAcquired(
			new AdventureUpgradeEffectState(),
			"hearts_deep_tide_lung",
			triggerBattleStartBonusImmediately: true);

		Equal(4, thickBlood.MaxHealthDelta);
		Equal(4, thickBlood.HealAmount);
		Equal(0, thickBlood.ImmediateDrawCount);
		Equal(8, deepTideLung.MaxHealthDelta);
		Equal(0, deepTideLung.HealAmount);
		Equal(1, deepTideLung.ImmediateDrawCount);
	}

	private static void AdventureBattleStartDrawBonusesStackAcrossAcquiredHeartUpgrades()
	{
		int drawCount = AdventureUpgradeEffectService.GetBattleStartDrawCount(new[]
		{
			"hearts_shoal_preparation",
			"hearts_deep_tide_lung",
		});

		Equal(2, drawCount);
	}

	private static void AdventureHealingOverflowIncreasesMaxHealthEveryFourOverflow()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureHealingResult result = AdventureUpgradeEffectService.ApplyHealing(
			currentHealth: 100,
			maxHealth: 100,
			state,
			new[] { "hearts_overflow_growth" },
			healAmount: 9);

		Equal(102, result.CurrentHealth);
		Equal(102, result.MaxHealth);
		Equal(2, result.MaxHealthIncrease);
		Equal(1, result.OverflowRemainder);
		Equal(1, state.HealingOverflowRemainder);
	}

	private static void AdventureMissEffectsQueueTurnStartRecoveryAndNextCaptureBonus()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureMainPlayerMissEffectResult result = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			state,
			new[] { "hearts_miss_bandage", "diamonds_miss_discount" },
			tableCardCountAfterMiss: 1);

		Equal(3, state.NextTurnHealAmount);
		Equal(1, state.NextTurnDrawCount);
		Equal(1, state.NextCaptureDamageBonus);
		False(result.GrantExtraBaseCardAction);
	}

	private static void AdventureMissEffectsIncludeClubTableRewrites()
	{
		AdventureMainPlayerMissEffectResult result = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			new AdventureUpgradeEffectState(),
			new[] { "clubs_hanging_reverse", "clubs_wild_branch", "clubs_canopy_reveal" },
			tableCardCountAfterMiss: 4);

		True(result.InsertPlayedCardAtLeftmost);
		True(result.ShuffleTableAfterMiss);
		True(result.SetCanopyRevealAnchor);
	}

	private static void AdventureTurnStartEffectsAreConsumedOnce()
	{
		var state = new AdventureUpgradeEffectState
		{
			NextTurnHealAmount = 3,
			NextTurnDrawCount = 1,
			ClubShortBranchReturnUsedThisTurn = true,
		};

		AdventureTurnStartEffectResult first = AdventureUpgradeEffectService.ConsumeMainPlayerTurnStartEffects(state);
		AdventureTurnStartEffectResult second = AdventureUpgradeEffectService.ConsumeMainPlayerTurnStartEffects(state);

		Equal(3, first.HealAmount);
		Equal(1, first.DrawCount);
		False(state.ClubShortBranchReturnUsedThisTurn);
		Equal(0, second.HealAmount);
		Equal(0, second.DrawCount);
	}

	private static void AdventureCaptureDamageAppliesMinimumAndFinalDoubleInOrder()
	{
		var state = new AdventureUpgradeEffectState
		{
			ThirdQuestionDamageDoubleActive = true,
		};

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_small_insurance" },
			new AdventureCaptureDamageInput(BaseDamage: 4));

		Equal(10, result.FinalDamage);
		Equal(0, state.NextCaptureDamageBonus);
	}

	private static void AdventureCaptureDamageConsumesCarryBonusAndReArmsHighReturnBonus()
	{
		var state = new AdventureUpgradeEffectState
		{
			PermanentCaptureDamageBonus = 1,
			NextCaptureDamageBonus = 2,
		};

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_high_return_bonus" },
			new AdventureCaptureDamageInput(BaseDamage: 4));

		Equal(7, result.FinalDamage);
		Equal(2, state.NextCaptureDamageBonus);
		True(result.ReArmedNextCaptureDamageBonus);
	}

	private static void AdventureCaptureDamageAppliesFirstCapturePressureOncePerBattle()
	{
		var state = new AdventureUpgradeEffectState();

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_first_capture_pressure" },
			new AdventureCaptureDamageInput(BaseDamage: 4, IsFirstCaptureThisBattle: true));

		Equal(6, result.FinalDamage);
	}

	private static void AdventureCaptureDamageAppliesMultiCardPremiumAndTopPrizeRepeat()
	{
		var state = new AdventureUpgradeEffectState();

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_multi_card_premium", "diamonds_top_prize_repeat" },
			new AdventureCaptureDamageInput(
				BaseDamage: 12,
				ScoringCardRawPoints: new[] { 3, 4, 5 },
				ScoringCardResolvedPoints: new[] { 3, 4, 5 }));

		Equal(21, result.FinalDamage);
	}

	private static void AdventureCaptureDamageDoesNotApplyFullPayoutBelowSixScoringCards()
	{
		var state = new AdventureUpgradeEffectState();

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_full_payout" },
			new AdventureCaptureDamageInput(
				BaseDamage: 12,
				ScoringCardRawPoints: new[] { 1, 1, 1, 1, 1 },
				ScoringCardResolvedPoints: new[] { 1, 1, 1, 1, 1 }));

		Equal(12, result.FinalDamage);
	}

	private static void AdventureCaptureDamageDoublesFullPayoutAtSixScoringCardsAfterFinalAdditions()
	{
		var state = new AdventureUpgradeEffectState
		{
			PermanentCaptureDamageBonus = 1,
			NextCaptureDamageBonus = 2,
		};

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_full_payout" },
			new AdventureCaptureDamageInput(
				BaseDamage: 12,
				FlatDamageDelta: 3,
				ScoringCardRawPoints: new[] { 1, 1, 1, 1, 1, 1 },
				ScoringCardResolvedPoints: new[] { 1, 1, 1, 1, 1, 1 }));

		Equal(36, result.FinalDamage);
		Equal(0, state.NextCaptureDamageBonus);
	}

	private static void AdventureCaptureDamageStacksFullPayoutWithFinalDoubleBeforeHighReturnBonus()
	{
		var state = new AdventureUpgradeEffectState
		{
			ThirdQuestionDamageDoubleActive = true,
		};

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_full_payout", "diamonds_high_return_bonus" },
			new AdventureCaptureDamageInput(
				BaseDamage: 2,
				ScoringCardRawPoints: new[] { 1, 1, 1, 1, 1, 1 },
				ScoringCardResolvedPoints: new[] { 1, 1, 1, 1, 1, 1 }));

		Equal(8, result.FinalDamage);
		Equal(2, state.NextCaptureDamageBonus);
		True(result.ReArmedNextCaptureDamageBonus);
	}

	private static void AdventureCaptureDamageAddsBountyAmplifyWhenRewardQuestionIsCaptured()
	{
		var state = new AdventureUpgradeEffectState();

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_bounty_amplify" },
			new AdventureCaptureDamageInput(
				BaseDamage: 6,
				IncludesQuestionReward: true,
				ScoringCardRawPoints: new[] { 2, 4 },
				ScoringCardResolvedPoints: new[] { 2, 4 }));

		Equal(9, result.FinalDamage);
	}

	private static void AdventureCaptureDamageAddsQuestionEchoPointBonusPerScoringCard()
	{
		var state = new AdventureUpgradeEffectState();

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			state,
			new[] { "diamonds_question_mark_echo" },
			new AdventureCaptureDamageInput(
				BaseDamage: 9,
				QuestionRewardEchoActive: true,
				ScoringCardRawPoints: new[] { 2, 3, 4 },
				ScoringCardResolvedPoints: new[] { 2, 3, 4 }));

		Equal(12, result.FinalDamage);
	}

	private static void AdventurePostCaptureHealTriggersAfterSixDamage()
	{
		AdventurePostCaptureEffectResult result = AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
			new AdventureUpgradeEffectState(),
			new[] { "hearts_long_capture_recovery" },
			finalDamage: 6);

		Equal(2, result.HealAmount);
	}

	private static void AdventurePostCaptureBonusesStackByCaptureOrdinal()
	{
		AdventurePostCaptureEffectResult first = AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
			new AdventureUpgradeEffectState(),
			new[] { "hearts_warm_current_supply", "hearts_return_tide_heart" },
			finalDamage: 4,
			successfulCapturesThisTurn: 1);
		AdventurePostCaptureEffectResult second = AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
			new AdventureUpgradeEffectState(),
			new[] { "hearts_chain_tide_breath", "hearts_return_tide_heart" },
			finalDamage: 4,
			successfulCapturesThisTurn: 2);

		Equal(2, first.DrawCount);
		Equal(2, first.HealAmount);
		Equal(2, second.DrawCount);
		Equal(5, second.HealAmount);
	}

	private static void AdventurePostCaptureIncludesEnchantedRecoveryAndHollowBurst()
	{
		AdventurePostCaptureEffectResult result = AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
			new AdventureUpgradeEffectState(),
			new[] { "hearts_enchanted_recovery", "clubs_hollow_burst" },
			finalDamage: 4,
			successfulCapturesThisTurn: 1,
			attachedEffectTriggered: true,
			capturedOnlyOuterVisibleCards: true);

		Equal(3, result.HealAmount);
		Equal(1, result.DrawCount);
		True(result.RevealRemainingHiddenCards);
	}

	private static void AdventureLowHealthRescueTriggersAtHalfAndQuarterThresholdsOnceEach()
	{
		AdventureLowHealthRescueResult half = AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
			previousHealth: 60,
			currentHealth: 45,
			maxHealth: 100,
			new[] { "hearts_red_tide_return", "hearts_revival_draw" },
			halfThresholdUsed: false,
			quarterThresholdUsed: false);
		AdventureLowHealthRescueResult quarter = AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
			previousHealth: 30,
			currentHealth: 20,
			maxHealth: 100,
			new[] { "hearts_red_tide_return", "hearts_revival_draw" },
			halfThresholdUsed: true,
			quarterThresholdUsed: false);
		AdventureLowHealthRescueResult none = AdventureUpgradeEffectService.ResolveMainPlayerLowHealthRescue(
			previousHealth: 20,
			currentHealth: 10,
			maxHealth: 100,
			new[] { "hearts_red_tide_return", "hearts_revival_draw" },
			halfThresholdUsed: true,
			quarterThresholdUsed: true);

		Equal(5, half.HealAmount);
		Equal(2, half.DrawCount);
		True(half.ConsumedHalfThreshold);
		Equal(8, quarter.HealAmount);
		Equal(2, quarter.DrawCount);
		True(quarter.ConsumedQuarterThreshold);
		Equal(0, none.HealAmount);
		Equal(0, none.DrawCount);
	}

	private static void AdventureEnemyMissEffectsQueueRevealInsertPenaltyAndBleedDamage()
	{
		AdventureEnemyMissEffectResult result = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			new[]
			{
				"spades_dark_harbor_reveal",
				"spades_wrong_shore_lineup",
				"spades_missed_beat_blunt_hurt",
				"spades_miss_bleed",
				"spades_missed_beat_calamity",
			},
			tableCardCountAfterMiss: 4,
			hadOldTableCardsBeforeMiss: true);

		True(result.NextCardFacesUp);
		Equal(AdventureEnemyNextCardInsertMode.Leftmost, result.InsertMode);
		Equal(-6, result.NextCaptureDamageDelta);
		Equal(2, result.DirectDamageToEnemy);
	}

	private static void AdventureEnemyMissMisorderOnlyArmsTrackedAnchorWhenOldCardsExisted()
	{
		AdventureEnemyMissEffectResult armed = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			new[] { "spades_return_tide_misorder" },
			tableCardCountAfterMiss: 2,
			hadOldTableCardsBeforeMiss: true);
		AdventureEnemyMissEffectResult notArmed = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			new[] { "spades_return_tide_misorder" },
			tableCardCountAfterMiss: 1,
			hadOldTableCardsBeforeMiss: false);

		Equal(AdventureEnemyNextCardInsertMode.BeforeTrackedOldest, armed.InsertMode);
		Equal(AdventureEnemyNextCardInsertMode.None, notArmed.InsertMode);
	}

	private static void AdventureEnemyMissGrantsExtraBaseActionFromInterceptBeat()
	{
		AdventureEnemyMissEffectResult result = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			new[] { "spades_intercept_beat" },
			tableCardCountAfterMiss: 2,
			hadOldTableCardsBeforeMiss: false);

		True(result.GrantMainPlayerExtraBaseAction);
	}

	private static void AdventureMainPlayerMissGrantsContinueBranchExtraActionOnlyAtFourCards()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureMainPlayerMissEffectResult granted = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			state,
			new[] { "clubs_continue_branch" },
			tableCardCountAfterMiss: 4);
		AdventureMainPlayerMissEffectResult notGranted = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			new AdventureUpgradeEffectState(),
			new[] { "clubs_continue_branch" },
			tableCardCountAfterMiss: 3);

		True(granted.GrantExtraBaseCardAction);
		False(notGranted.GrantExtraBaseCardAction);
	}

	private static void AdventureBlackLedgerChangesOldTableScoreContributionByCapturer()
	{
		int mainDelta = AdventureUpgradeEffectService.ResolveBlackLedgerDamageDelta(
			blackLedgerActive: true,
			capturingPlayerIsMain: true,
			totalResolvedOldTableCardPoints: 10,
			currentResolvedOldTableScoringPoints: 4);
		int enemyDelta = AdventureUpgradeEffectService.ResolveBlackLedgerDamageDelta(
			blackLedgerActive: true,
			capturingPlayerIsMain: false,
			totalResolvedOldTableCardPoints: 10,
			currentResolvedOldTableScoringPoints: 4);
		int inactiveDelta = AdventureUpgradeEffectService.ResolveBlackLedgerDamageDelta(
			blackLedgerActive: false,
			capturingPlayerIsMain: true,
			totalResolvedOldTableCardPoints: 10,
			currentResolvedOldTableScoringPoints: 4);

		Equal(6, mainDelta);
		Equal(-4, enemyDelta);
		Equal(0, inactiveDelta);
	}

	private static void AdventureEnemyMissHookDredgeMovesOldestOldCardAndDealsHalfDamage()
	{
		AdventureEnemyMissHookDredgeResult result = AdventureUpgradeEffectService.ResolveEnemyMissHookDredge(
			new[] { "spades_hook_dredge" },
			tableCardCountAfterMiss: 5,
			oldestOldTableCardRawScore: 5);

		True(result.MoveOldestOldTableCardToPlayerDeckTop);
		Equal(3, result.DirectDamageToEnemy);
	}

	private static void AdventureEnemyTailPressurePreservesPlayedTailCardOnLargeCapture()
	{
		True(AdventureUpgradeEffectService.ShouldPreserveEnemyCaptureTailCard(
			new[] { "spades_tail_pressure" },
			totalCapturedCardCount: 3));
		False(AdventureUpgradeEffectService.ShouldPreserveEnemyCaptureTailCard(
			new[] { "spades_tail_pressure" },
			totalCapturedCardCount: 2));
	}

	private static void AdventureOldTableDamageDeltaAppliesPlayerMarkBonuses()
	{
		int delta = AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
			new[] { "spades_trace_hunt", "spades_hunt_order" },
			capturingPlayerIsMain: true,
			capturedOldScoringCardCount: 1,
			capturedTraceMark: true,
			capturedHuntMark: true);

		Equal(10, delta);
	}

	private static void AdventureOldTableDamageDeltaAppliesEnemyTaxAndExaction()
	{
		int taxDelta = AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
			new[] { "spades_reverse_capture_tax" },
			capturingPlayerIsMain: false,
			capturedOldScoringCardCount: 4,
			capturedTraceMark: false,
			capturedHuntMark: false);
		int exactionDelta = AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
			new[] { "spades_exaction" },
			capturingPlayerIsMain: false,
			capturedOldScoringCardCount: 1,
			capturedTraceMark: false,
			capturedHuntMark: false);

		Equal(-3, taxDelta);
		Equal(-4, exactionDelta);
	}

	private static void AdventureClubsPrimaryRootReturnGivesPlayedBaseCardBack()
	{
		AdventureClubCaptureEffectResult result = AdventureUpgradeEffectService.ResolveClubCaptureEffects(
			new AdventureUpgradeEffectState(),
			new[] { "clubs_primary_root_return" },
			baseCaptureCardCount: 3,
			handCountBeforeReturn: 2,
			maxTotalHandCards: 8,
			playUsedAttachedEffect: false,
			playedCardWasBaseCard: true);

		True(result.ReturnPlayedCardToHand);
		False(result.ReturnTwoCapturedCardsToHand);
	}

	private static void AdventureClubsShortBranchReturnOnlyAppliesToTwoCardCaptureOnce()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureClubCaptureEffectResult first = AdventureUpgradeEffectService.ResolveClubCaptureEffects(
			state,
			new[] { "clubs_short_branch_return" },
			baseCaptureCardCount: 2,
			handCountBeforeReturn: 2,
			maxTotalHandCards: 8,
			playUsedAttachedEffect: false,
			playedCardWasBaseCard: true);
		AdventureClubCaptureEffectResult second = AdventureUpgradeEffectService.ResolveClubCaptureEffects(
			state,
			new[] { "clubs_short_branch_return" },
			baseCaptureCardCount: 2,
			handCountBeforeReturn: 2,
			maxTotalHandCards: 8,
			playUsedAttachedEffect: false,
			playedCardWasBaseCard: true);

		True(first.ReturnTwoCapturedCardsToHand);
		False(second.ReturnTwoCapturedCardsToHand);
	}

	private static void AdventureClubsClimbCanopyReturnsPlayedBaseCardAfterAttachedEffectTrigger()
	{
		AdventureClubCaptureEffectResult result = AdventureUpgradeEffectService.ResolveClubCaptureEffects(
			new AdventureUpgradeEffectState(),
			new[] { "clubs_climb_canopy" },
			baseCaptureCardCount: 3,
			handCountBeforeReturn: 2,
			maxTotalHandCards: 8,
			playUsedAttachedEffect: true,
			playedCardWasBaseCard: true);

		True(result.ReturnPlayedCardToHand);
	}

	private static void AdventureClubsCrownCanopyIncludesCaptureEndpointsInScoring()
	{
		AdventureClubCaptureScoringResult result = AdventureUpgradeEffectService.ResolveClubCaptureScoring(
			new[] { "clubs_crown_canopy" });

		True(result.IncludeCaptureEndpointsInScoring);
	}

	private static void AdventureClubsGapRootingCapturesLeftAdjacentBaseOncePerTurn()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureClubCaptureExtensionResult first = AdventureUpgradeEffectService.ResolveClubCaptureExtensions(
			state,
			new[] { "clubs_gap_rooting" },
			hasMiddleScoringCards: false,
			leftAdjacentBaseCardAvailable: true);
		AdventureClubCaptureExtensionResult second = AdventureUpgradeEffectService.ResolveClubCaptureExtensions(
			state,
			new[] { "clubs_gap_rooting" },
			hasMiddleScoringCards: false,
			leftAdjacentBaseCardAvailable: true);

		True(first.CaptureLeftAdjacentBaseCard);
		False(second.CaptureLeftAdjacentBaseCard);
	}

	private static void AdventureClubsLinkedStemsGrantsQuestionRewardOnSecondCapture()
	{
		True(AdventureUpgradeEffectService.ShouldGrantLinkedStemsQuestionReward(
			new[] { "clubs_linked_stems" },
			successfulCapturesThisTurn: 2));
		False(AdventureUpgradeEffectService.ShouldGrantLinkedStemsQuestionReward(
			new[] { "clubs_linked_stems" },
			successfulCapturesThisTurn: 1));
	}

	private static void AdventureClubsMissClimbGrowthAndCrossGraftAdjustMatchRanks()
	{
		var state = new AdventureUpgradeEffectState
		{
			ClubMissClimbGrowthStacks = 2,
		};

		AdventureClubPlayAdjustmentResult result = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_miss_climb_growth", "clubs_cross_graft" },
			playedCardRank: "4");

		Equal("6", result.PrimaryRank);
		Equal(2, result.AlternateRanks.Count);
		Equal("5", result.AlternateRanks[0]);
		Equal("7", result.AlternateRanks[1]);
	}

	private static void AdventureClubsBorrowSuitSetArmsAndIsConsumedByNextNumericBaseCard()
	{
		var state = new AdventureUpgradeEffectState();
		AdventureQuestionRewardEffectResult _ = AdventureUpgradeEffectService.OnQuestionRewardGranted(
			state,
			new[] { "clubs_borrow_suit_set" });

		AdventureClubPlayAdjustmentResult armed = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_borrow_suit_set" },
			playedCardRank: "4");
		AdventureUpgradeEffectService.ConsumeBorrowSuitSetAfterNumericBaseCardPlay(state, "4");
		AdventureClubPlayAdjustmentResult consumed = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			state,
			new[] { "clubs_borrow_suit_set" },
			playedCardRank: "4");

		True(armed.CollectBySuitOnce);
		False(consumed.CollectBySuitOnce);
	}

	private static void AdventureDisabledUndertowUpgradeStaysOutOfEnabledPool()
	{
		var definitions = new[]
		{
			new AdventureUpgradeDefinitionModel(
				"spades_undertow_asphyxia",
				"Undertow",
				"spades",
				"rare",
				"",
				"",
				Enabled: false),
			new AdventureUpgradeDefinitionModel(
				"spades_dark_harbor_reveal",
				"Reveal",
				"spades",
				"common",
				"",
				"",
				Enabled: true),
		};

		IReadOnlyList<AdventureUpgradeDefinitionModel> offer = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			Array.Empty<string>(),
			runStageIndex: 1,
			nextIntExclusive: _ => 0);

		Equal(1, offer.Count);
		Equal("spades_dark_harbor_reveal", offer[0].Id);
	}

	private static void GameSaveProgressRoundTripsPendingAdventureRunJson()
	{
		const string pendingRunJson = "{\"CurrentAvatarStageNumber\":3}";
		GameSaveStore.GameProgressData progress = GameSaveStore.GameProgressData.CreateDefault() with
		{
			PendingAdventureRunJson = pendingRunJson,
		};

		GameSaveStore.GameProgressData roundTripped = GameSaveStore.GameProgressData.FromDictionary(
			progress.ToDictionary(),
			GameSaveStore.GameProgressData.CreateDefault());

		Equal(pendingRunJson, roundTripped.PendingAdventureRunJson);
	}

	private static void DeleteActiveProfileSwitchesReplacementBeforeRemovingOriginalSlot()
	{
		ResetGameSaveStoreTestState();
		string originalSlotId = GameSaveStore.DefaultProfileSlotId;
		string replacementSlotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(originalSlotId);
		SeedProfile(saveManager, originalSlotId, CreateProgressData(playerName: "Active"));

		string activeSlotDuringDelete = string.Empty;
		Godot.DirAccess.BeforeRemoveAbsolute = path =>
		{
			if (path.EndsWith($"{originalSlotId}.json", StringComparison.Ordinal))
			{
				activeSlotDuringDelete = saveManager.GetStoredString(GameSaveStore.ActiveProfileSlotKey);
			}
		};

		Equal(Godot.Error.Ok, GameSaveStore.DeleteProfile(originalSlotId));
		Equal(replacementSlotId, activeSlotDuringDelete);
		Equal(replacementSlotId, saveManager.GetStoredString(GameSaveStore.ActiveProfileSlotKey));
		False(saveManager.HasSlotFile(originalSlotId));
		True(saveManager.HasSlotFile(replacementSlotId));
		Equal(1, saveManager.UnregisteredSlots.Count);
		Equal(originalSlotId, saveManager.UnregisteredSlots[0]);
	}

	private static void DeleteActiveProfileKeepsOriginalSlotWhenReplacementCreationFails()
	{
		ResetGameSaveStoreTestState();
		string originalSlotId = GameSaveStore.DefaultProfileSlotId;
		string replacementSlotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(originalSlotId);
		SeedProfile(saveManager, originalSlotId, CreateProgressData(playerName: "Active"));
		saveManager.SaveSlotResults[replacementSlotId] = Godot.Error.Failed;

		Equal(Godot.Error.Failed, GameSaveStore.DeleteProfile(originalSlotId));
		Equal(originalSlotId, saveManager.GetStoredString(GameSaveStore.ActiveProfileSlotKey));
		True(saveManager.HasSlotFile(originalSlotId));
		False(saveManager.HasSlotFile(replacementSlotId));
		Equal(0, saveManager.UnregisteredSlots.Count);
	}

	private static void DeleteActiveProfileKeepsOriginalSlotWhenActiveSwitchFails()
	{
		ResetGameSaveStoreTestState();
		string originalSlotId = GameSaveStore.DefaultProfileSlotId;
		string replacementSlotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(originalSlotId);
		SeedProfile(saveManager, originalSlotId, CreateProgressData(playerName: "Active"));
		SeedProfile(saveManager, replacementSlotId, CreateProgressData(playerName: "Replacement"));
		saveManager.PersistResult = Godot.Error.Failed;

		Equal(Godot.Error.Failed, GameSaveStore.DeleteProfile(originalSlotId));
		Equal(originalSlotId, saveManager.GetStoredString(GameSaveStore.ActiveProfileSlotKey));
		True(saveManager.HasSlotFile(originalSlotId));
		True(saveManager.HasSlotFile(replacementSlotId));
		Equal(0, saveManager.UnregisteredSlots.Count);
	}

	private static void DamagedPendingAdventureRunJsonClearsFromActiveProfile()
	{
		ResetGameSaveStoreTestState();
		string activeSlotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(activeSlotId);
		SeedProfile(saveManager, activeSlotId, CreateProgressData(
			playerName: "Runner",
			pendingAdventureRunJson: "{bad json"));

		var store = new AdventureRunSessionStore();
		store.ReloadFromActiveProfile();

		False(store.HasActiveRun);
		Equal(string.Empty, LoadStoredProfile(saveManager, activeSlotId).PendingAdventureRunJson);
		Equal(1, Godot.GD.WarningMessages.Count);

		store.ReloadFromActiveProfile();
		Equal(1, Godot.GD.WarningMessages.Count);
		Equal(string.Empty, LoadStoredProfile(saveManager, activeSlotId).PendingAdventureRunJson);
	}

	private static void ValidPendingAdventureRunJsonReloadsActiveRun()
	{
		ResetGameSaveStoreTestState();
		string activeSlotId = GameSaveStore.GetProfileSlotId(2);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(activeSlotId);
		var runState = new AdventureUpgradeRunState();
		runState.BeginNewRun(3);
		runState.AcquireUpgrade("test_upgrade");
		string pendingRunJson = JsonSerializer.Serialize(runState.ToSnapshot());
		SeedProfile(saveManager, activeSlotId, CreateProgressData(
			playerName: "Runner",
			pendingAdventureRunJson: pendingRunJson));

		var store = new AdventureRunSessionStore();
		store.ReloadFromActiveProfile();

		True(store.HasActiveRun);
		True(store.TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot));
		Equal(3, snapshot.CurrentAvatarStageNumber);
		Equal(1, snapshot.CurrentRunStageIndex);
		Equal(pendingRunJson, LoadStoredProfile(saveManager, activeSlotId).PendingAdventureRunJson);
		Equal(0, Godot.GD.WarningMessages.Count);
	}

	private static void ResultPresentationUsesLocalSeatForNetworkDefeat()
	{
		True(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 0,
			localPlayerIndex: 2,
			isNetworkMultiplayer: true,
			adventureMode: false,
			soloMode: false));

		False(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 2,
			localPlayerIndex: 2,
			isNetworkMultiplayer: true,
			adventureMode: false,
			soloMode: false));
	}

	private static void ResultPresentationPreservesLocalWinAndOfflineRules()
	{
		False(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 0,
			localPlayerIndex: 0,
			isNetworkMultiplayer: false,
			adventureMode: false,
			soloMode: false));

		False(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 1,
			localPlayerIndex: 0,
			isNetworkMultiplayer: false,
			adventureMode: false,
			soloMode: false));

		True(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 1,
			localPlayerIndex: 0,
			isNetworkMultiplayer: false,
			adventureMode: true,
			soloMode: false));

		True(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 1,
			localPlayerIndex: 0,
			isNetworkMultiplayer: false,
			adventureMode: false,
			soloMode: true));
	}

	private static void ResultPresentationDisablesNetworkReplayPrimaryAction()
	{
		False(ResultPresentationRules.ShouldEnableWinnerPrimaryAction(
			isNetworkMultiplayer: true,
			shouldAdvanceAdventure: false));

		True(ResultPresentationRules.ShouldEnableWinnerPrimaryAction(
			isNetworkMultiplayer: false,
			shouldAdvanceAdventure: false));

		True(ResultPresentationRules.ShouldEnableWinnerPrimaryAction(
			isNetworkMultiplayer: true,
			shouldAdvanceAdventure: true));
	}

	private static void RewardResolverUsesWeightedReservoirSelection()
	{
		var candidates = new[]
		{
			new RewardCandidate("bomb", 2),
			new RewardCandidate("magnet", 3),
			new RewardCandidate("lens", 5),
		};
		int[] rolls = { 2, 1, 6 };
		int rollIndex = 0;

		RewardPlan reward = RewardResolver.PickWeightedReward(candidates, maxInclusive => rolls[rollIndex++]);

		True(reward.HasReward);
		Equal("magnet", reward.ItemCardId);
	}

	private static void ItemCardEffectSupportBlocksReservedEmptyEffects()
	{
		True(ItemCardEffectSupport.IsSupportedEffectType("grant_reward"));
		False(ItemCardEffectSupport.IsSupportedEffectType("block_match"));
		False(ItemCardEffectSupport.IsSupportedEffectType("reverse_collect"));
		False(ItemCardEffectSupport.IsSupportedEffectType(string.Empty));
	}

	private static void PlayingCardAttachmentReplacesExistingEffect()
	{
		PlayingCard card = CreatePlayingCard()
			.WithAttachedItemEffect("magnet")
			.WithAttachedItemEffect("double");

		True(card.HasItemEffect);
		False(card.HasAttachedItemEffect("magnet"));
		True(card.HasAttachedItemEffect("double"));
		Equal("double", card.AttachedItemCardIdsCsv);
		Equal(1, card.GetAttachedItemCardIds().Count);
		Equal("double", card.GetAttachedItemCardIds()[0]);
	}

	private static void CaptureStateApplyCaptureClearsEffectsWhenPreserveDisabled()
	{
		GameBoardState state = CreateCaptureState(CreatePlayingCard().WithAttachedItemEffect("magnet"));
		var service = new CaptureStateService();

		CaptureStatePlan plan = service.CreatePlan(state, 0, null);
		CaptureStateResult result = service.ApplyCapture(state, plan, playerIndex: 0, preserveCardEffectsWhenEnteringDeck: false);

		Equal(1, result.CollectedCardCount);
		Equal(1, state.PlayerDecks[0].Count);
		False(state.PlayerDecks[0][0].HasItemEffect);
		Equal(0, state.TableCards.Count);
	}

	private static void CaptureStateApplyCapturePreservesEffectsWhenPreserveEnabled()
	{
		GameBoardState state = CreateCaptureState(CreatePlayingCard().WithAttachedItemEffect("magnet"));
		var service = new CaptureStateService();

		CaptureStatePlan plan = service.CreatePlan(state, 0, null);
		CaptureStateResult result = service.ApplyCapture(state, plan, playerIndex: 0, preserveCardEffectsWhenEnteringDeck: true);

		Equal(1, result.CollectedCardCount);
		Equal(1, state.PlayerDecks[0].Count);
		True(state.PlayerDecks[0][0].HasAttachedItemEffect("magnet"));
		Equal("magnet", state.PlayerDecks[0][0].AttachedItemCardIdsCsv);
		Equal(0, state.TableCards.Count);
	}

	private static void CapturedCardEffectPersistsThroughDeckAndCanBeReplaced()
	{
		GameBoardState state = CreateCaptureState(CreatePlayingCard().WithAttachedItemEffect("magnet"));
		state.PlayerHands.Add(new List<PlayingCard>());
		var service = new CaptureStateService();

		CaptureStatePlan plan = service.CreatePlan(state, 0, null);
		service.ApplyCapture(state, plan, playerIndex: 0, preserveCardEffectsWhenEnteringDeck: true);

		PlayingCard drawnCard = state.PlayerDecks[0][0];
		state.PlayerHands[0].Add(drawnCard);
		state.PlayerDecks[0].RemoveAt(0);

		state.PlayerHands[0][0] = state.PlayerHands[0][0].WithAttachedItemEffect("double");

		False(state.PlayerHands[0][0].HasAttachedItemEffect("magnet"));
		True(state.PlayerHands[0][0].HasAttachedItemEffect("double"));
		Equal("double", state.PlayerHands[0][0].AttachedItemCardIdsCsv);
	}

	private static void CaptureRefillShufflesBeforeReplacementDrawAnimation()
	{
		string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "GameBoardScene.CardPlay.cs"));
		int methodStart = source.IndexOf("private async Task<CardPlayBranchResult> ResolvePlayedCardCaptureAsync(", StringComparison.Ordinal);
		int methodEnd = source.IndexOf("private async Task<CardPlayBranchResult> ResolveMissedPlayedCardAsync(", methodStart, StringComparison.Ordinal);
		True(methodStart >= 0);
		True(methodEnd > methodStart);

		string method = source[methodStart..methodEnd];
		int captureApplyIndex = method.IndexOf("CaptureTableCardsResult captureResult = await CaptureTableCardsAsync", StringComparison.Ordinal);
		int shuffleIndex = method.IndexOf("Shuffle(_playerDecks[playerIndex]);", captureApplyIndex, StringComparison.Ordinal);
		int cutIndex = method.IndexOf("Cut(_playerDecks[playerIndex]);", shuffleIndex, StringComparison.Ordinal);
		int shuffleAnimationIndex = method.IndexOf("await PlayPhysicalDeckShuffleAsync(playerIndex);", cutIndex, StringComparison.Ordinal);
		int drawReplacementIndex = method.IndexOf("DrawnHandCard? replacementCard = DrawReplacementCard(playerIndex);", cutIndex, StringComparison.Ordinal);
		int drawAnimationIndex = method.IndexOf("await PlayReplacementDrawAnimationAsync(playerIndex, replacementCard, turnSequenceGeneration);", drawReplacementIndex, StringComparison.Ordinal);

		True(captureApplyIndex >= 0);
		True(shuffleIndex > captureApplyIndex);
		True(cutIndex > shuffleIndex);
		True(shuffleAnimationIndex > cutIndex);
		True(drawReplacementIndex > shuffleAnimationIndex);
		True(drawAnimationIndex > drawReplacementIndex);
	}

	private static void CaptureRevealOrderOpensEdgesBeforeMiddle()
	{
		int[] order = CaptureRevealOrder.BuildBaseRevealIndexes(captureStartIndex: 2, tableCardCount: 6);

		Equal("2,5,3,4", string.Join(",", order));
	}

	private static void PotentialCaptureIgnoresBuriedMatches()
	{
		var tableCards = new[]
		{
			TableCard("A", 2, isBuriedInSand: true),
			TableCard("A", 3),
			TableCard("Q", 4),
		};
		var card = new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false);

		int captureCount = PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card);
		int points = PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, DefaultScoringContext);

		Equal(3, captureCount);
		Equal(4, points);
	}

	private static void PotentialCaptureCountsSuitCollectFromFirstMatchingSuit()
	{
		var tableCards = new[]
		{
			TableCard("2", 1, suit: "clubs"),
			TableCard("4", 4, suit: "hearts"),
			TableCard("6", 5, suit: "spades"),
			TableCard("8", 6, suit: "hearts"),
		};
		var card = new PotentialCaptureRules.Card("8", CollectsTable: false, HasDoubleEffect: false, Suit: "hearts", CollectBySuit: true);

		int captureCount = PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card);
		int points = PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, DefaultScoringContext);

		Equal(4, captureCount);
		Equal(11, points);
	}

	private static void PotentialCaptureUsesAlternateRanksForClubGraftMatching()
	{
		var tableCards = new[]
		{
			TableCard("5", 2),
			TableCard("7", 4),
			TableCard("9", 6),
		};
		var card = new PotentialCaptureRules.Card(
			"6",
			CollectsTable: false,
			HasDoubleEffect: false,
			Suit: "",
			CollectBySuit: false,
			AlternateRanks: new[] { "5", "7" });

		int captureCount = PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card);
		int points = PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, DefaultScoringContext);

		Equal(4, captureCount);
		Equal(10, points);
	}

	private static void PotentialCapturePrefersRoyalThreshold()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 1),
			TableCard("3", 1),
			TableCard("K", 12),
			TableCard("Q", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = PotentialCaptureRules.TryPickBestCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			royalCaptureThreshold: 5,
			out int handIndex);

		True(picked);
		Equal(0, handIndex);
	}

	private static void PotentialCaptureIdentifiesPairOnlyCaptures()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
		};
		var card = new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false);

		True(PotentialCaptureRules.IsPairOnlyCapture(tableCards, card));
	}

	private static void AdventureAiEarlyProfileCanSkipObviousCapture()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(1),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 90,
			randomCaptureOrdinal: 0,
			out _);

		False(picked);
	}

	private static void AdventureAiEarlyProfileSkipsPairOnlyCapture()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(1),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out _);

		False(picked);
	}

	private static void AdventureAiPointScoringStartsAfterEarlyStages()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("5", 5),
			TableCard("6", 6),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: true),
		};

		bool earlyPicked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(6),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out int earlyHandIndex);
		bool laterPicked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(7),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out int laterHandIndex);

		True(earlyPicked);
		True(laterPicked);
		Equal(0, earlyHandIndex);
		Equal(1, laterHandIndex);
	}

	private static void AdventureAiRandomCaptureCanPickNonBestCapture()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("K", 12),
			TableCard("Q", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(1),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 30,
			randomCaptureOrdinal: 1,
			out int handIndex);

		True(picked);
		Equal(1, handIndex);
	}

	private static void AdventureAiLateBestCaptureSkipsPairOnlyCaptures()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(10),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out _);

		False(picked);
	}

	private static void AdventureAiLateRandomCaptureSkipsPairOnlyCaptures()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("K", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(10),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 92,
			randomCaptureOrdinal: 7,
			out int handIndex);

		True(picked);
		Equal(1, handIndex);
	}

	private static void AdventureAiFinalProfileCanSkipBestCapture()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 1),
			TableCard("3", 1),
			TableCard("K", 12),
			TableCard("Q", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
		};

		bool lowRollPicked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(14),
			royalCaptureThreshold: 5,
			decisionRollPercent: 64,
			randomCaptureOrdinal: 0,
			out int lowRollHandIndex);
		bool highRollPicked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(14),
			royalCaptureThreshold: 5,
			decisionRollPercent: 99,
			randomCaptureOrdinal: 0,
			out _);

		True(lowRollPicked);
		Equal(0, lowRollHandIndex);
		False(highRollPicked);
	}

	private static void AiEarlyTableNewCardPriorityPicksNonMatchingCardBeforeCapture()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
		};
		var handCards = new[]
		{
			new CardPlayCard("A", CollectsTable: false),
			new CardPlayCard("K", CollectsTable: false),
		};

		bool picked = AiCardSelectionRules.TryPickEarlyTableNewCardIndex(
			tableCards,
			handCards,
			randomOrdinal: 0,
			out int handIndex);

		True(picked);
		Equal(1, handIndex);
	}

	private static void AiEarlyTableNewCardPriorityYieldsAfterFourTableCards()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("3", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("4", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("5", IsFaceUp: true, IsBuriedInSand: false),
		};
		var handCards = new[]
		{
			new CardPlayCard("K", CollectsTable: false),
		};

		bool picked = AiCardSelectionRules.TryPickEarlyTableNewCardIndex(
			tableCards,
			handCards,
			randomOrdinal: 0,
			out _);

		False(picked);
	}

	private static void AiEarlyTableNewCardPriorityIgnoresBuriedMatches()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("K", IsFaceUp: true, IsBuriedInSand: true),
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: false),
		};
		var handCards = new[]
		{
			new CardPlayCard("A", CollectsTable: false),
			new CardPlayCard("K", CollectsTable: false),
		};

		bool picked = AiCardSelectionRules.TryPickEarlyTableNewCardIndex(
			tableCards,
			handCards,
			randomOrdinal: 0,
			out int handIndex);

		True(picked);
		Equal(1, handIndex);
	}

	private static void SoloAiDifficultyProfilesExposeExpectedValues()
	{
		SoloAiRules.Profile low = SoloAiRules.GetProfile(SoloAiRules.Difficulty.Low);
		SoloAiRules.Profile medium = SoloAiRules.GetProfile(SoloAiRules.Difficulty.Medium);
		SoloAiRules.Profile high = SoloAiRules.GetProfile(SoloAiRules.Difficulty.High);

		Equal(25, low.BestCaptureChancePercent);
		Equal(25, low.AnyCaptureChancePercent);
		False(low.UsePointScoring);
		False(low.AvoidPairOnlyCaptures);

		Equal(55, medium.BestCaptureChancePercent);
		Equal(20, medium.AnyCaptureChancePercent);
		False(medium.UsePointScoring);
		True(medium.AvoidPairOnlyCaptures);

		Equal(78, high.BestCaptureChancePercent);
		Equal(12, high.AnyCaptureChancePercent);
		True(high.UsePointScoring);
		True(high.AvoidPairOnlyCaptures);
	}

	private static void SoloAiMediumCapturePrefersMoreCardsWhileHighPrefersPoints()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("3", 3),
			TableCard("K", 0),
			TableCard("10", 10),
			TableCard("Q", 0),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: true),
		};

		bool mediumPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiRules.Difficulty.Medium,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out int mediumHandIndex);
		bool highPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiRules.Difficulty.High,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out int highHandIndex);

		True(mediumPicked);
		True(highPicked);
		Equal(0, mediumHandIndex);
		Equal(1, highHandIndex);
	}

	private static void SoloAiMediumAndHighSkipPairOnlyCaptures()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("K", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		bool mediumPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiRules.Difficulty.Medium,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 60,
			randomCaptureOrdinal: 7,
			out int mediumHandIndex);
		bool highPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiRules.Difficulty.High,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 80,
			randomCaptureOrdinal: 7,
			out int highHandIndex);
		bool lowPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiRules.Difficulty.Low,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 30,
			randomCaptureOrdinal: 0,
			out int lowHandIndex);

		True(mediumPicked);
		True(highPicked);
		True(lowPicked);
		Equal(1, mediumHandIndex);
		Equal(1, highHandIndex);
		Equal(0, lowHandIndex);
	}

	private static void SoloAiTargetPlayerStrategyPicksLargestRelevantOpponent()
	{
		var candidates = new[]
		{
			new SoloAiRules.PlayerTargetCandidate(PlayerIndex: 1, TotalHandCardCount: 5, DeckCount: 2, Score: 14),
			new SoloAiRules.PlayerTargetCandidate(PlayerIndex: 2, TotalHandCardCount: 7, DeckCount: 8, Score: 12),
			new SoloAiRules.PlayerTargetCandidate(PlayerIndex: 3, TotalHandCardCount: 6, DeckCount: 8, Score: 18),
		};

		Equal(2, SoloAiRules.PickItemTargetPlayer(
			SoloAiRules.Difficulty.High,
			SoloAiRules.TargetPreferenceKind.PlayerHand,
			candidates,
			strategyRollPercent: 0,
			tieBreakOrdinal: 0));
		Equal(3, SoloAiRules.PickItemTargetPlayer(
			SoloAiRules.Difficulty.High,
			SoloAiRules.TargetPreferenceKind.PlayerScore,
			candidates,
			strategyRollPercent: 0,
			tieBreakOrdinal: 0));
		Equal(2, SoloAiRules.PickItemTargetPlayer(
			SoloAiRules.Difficulty.High,
			SoloAiRules.TargetPreferenceKind.PlayerDeck,
			candidates,
			strategyRollPercent: 0,
			tieBreakOrdinal: 0));
		Equal(1, SoloAiRules.PickItemTargetPlayer(
			SoloAiRules.Difficulty.Low,
			SoloAiRules.TargetPreferenceKind.PlayerScore,
			candidates,
			strategyRollPercent: 0,
			tieBreakOrdinal: 0));
		Equal(1, SoloAiRules.PickItemTargetPlayer(
			SoloAiRules.Difficulty.Medium,
			SoloAiRules.TargetPreferenceKind.PlayerScore,
			candidates,
			strategyRollPercent: 95,
			tieBreakOrdinal: 0));
	}

	private static void SoloAiDiscardAndMagnetHeuristicsKeepImmediateCapturesWhenPossible()
	{
		var discardCandidates = new[]
		{
			new SoloAiRules.HandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true),
			new SoloAiRules.HandCardDecisionCandidate(Index: 1, ScoreValue: 3, CanCaptureNow: false),
			new SoloAiRules.HandCardDecisionCandidate(Index: 2, ScoreValue: 2, CanCaptureNow: false),
		};

		Equal(0, SoloAiRules.PickDiscardDisplayIndex(
			SoloAiRules.Difficulty.Low,
			discardCandidates,
			totalDisplayCount: 4,
			tieBreakOrdinal: 0));
		Equal(0, SoloAiRules.PickDiscardDisplayIndex(
			SoloAiRules.Difficulty.Medium,
			new[] { new SoloAiRules.HandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true) },
			totalDisplayCount: 3,
			tieBreakOrdinal: 0));
		Equal(2, SoloAiRules.PickDiscardDisplayIndex(
			SoloAiRules.Difficulty.High,
			discardCandidates,
			totalDisplayCount: 4,
			tieBreakOrdinal: 0));

		var magnetCandidates = new[]
		{
			new SoloAiRules.HandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true),
			new SoloAiRules.HandCardDecisionCandidate(Index: 1, ScoreValue: 3, CanCaptureNow: false),
			new SoloAiRules.HandCardDecisionCandidate(Index: 2, ScoreValue: 2, CanCaptureNow: false),
		};

		Equal(0, SoloAiRules.PickMagnetHandIndex(
			SoloAiRules.Difficulty.Low,
			magnetCandidates,
			tieBreakOrdinal: 0));
		Equal(0, SoloAiRules.PickMagnetHandIndex(
			SoloAiRules.Difficulty.Medium,
			new[] { new SoloAiRules.HandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true) },
			tieBreakOrdinal: 0));
		Equal(2, SoloAiRules.PickMagnetHandIndex(
			SoloAiRules.Difficulty.High,
			magnetCandidates,
			tieBreakOrdinal: 0));
	}

	private static void CharacterAudioConfigResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		string audioConfigDirectory = Path.Combine(repoRoot, "resources", "character_audio");
		True(Directory.Exists(audioConfigDirectory));

		string[] configPaths = Directory.GetFiles(audioConfigDirectory, "*.tres");
		True(configPaths.Length > 0);

		int audioReferenceCount = 0;
		foreach (string configPath in configPaths)
		{
			string configText = File.ReadAllText(configPath);
			foreach (Match match in Regex.Matches(configText, "path=\"res://([^\"]+\\.(?:mp3|wav))\""))
			{
				audioReferenceCount++;
				string path = Path.Combine(repoRoot, match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));
				True(File.Exists(path));
				True(new FileInfo(path).Length > 1024);
			}
		}

		True(audioReferenceCount > 0);
	}

	private static void LanAddressHelperComputesIpv4BroadcastAddress()
	{
		IPAddress? broadcast = LanAddressHelper.TryComputeBroadcastAddress(
			IPAddress.Parse("192.168.12.34"),
			IPAddress.Parse("255.255.255.0"));

		Equal("192.168.12.255", broadcast?.ToString());
	}

	private static void CharacterAvatarVisualResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		string avatarDirectory = Path.Combine(repoRoot, "resources", "character_avatars");
		True(Directory.Exists(avatarDirectory));

		string[] avatarPaths = Directory.GetFiles(avatarDirectory, "*.tres");
		True(avatarPaths.Length > 0);

		foreach (string avatarPath in avatarPaths)
		{
			string avatarText = File.ReadAllText(avatarPath);
			string rootPath = ReadTresString(avatarText, "RootPath");
			True(!string.IsNullOrWhiteSpace(rootPath));

			int idleFrameCount = Math.Max(1, ReadTresInt(avatarText, "IdleFrameCount", 1));
			int actionFrameCount = Math.Max(1, ReadTresInt(avatarText, "ActionFrameCount", 1));
			AssertFramePatternExists(repoRoot, $"{rootPath}/idle/processed/idle-{{0}}.png", idleFrameCount);
			foreach (string action in new[] { "score", "attack", "hit", "victory", "defeat" })
			{
				AssertFramePatternExists(repoRoot, $"{rootPath}/{action}/processed/{action}-{{0}}.png", actionFrameCount);
			}

			string attackFxRoot = ReadTresString(avatarText, "AttackFxRoot");
			if (!string.IsNullOrWhiteSpace(attackFxRoot))
			{
				AssertFramePatternExists(repoRoot, $"{attackFxRoot}/processed/fx-{{0}}.png", 1);
			}

			string damageEffectFramePattern = ReadTresString(avatarText, "DamageEffectFramePattern");
			int damageEffectFrameCount = ReadTresInt(avatarText, "DamageEffectFrameCount", 0);
			if (!string.IsNullOrWhiteSpace(damageEffectFramePattern) && damageEffectFrameCount > 0)
			{
				AssertFramePatternExists(repoRoot, damageEffectFramePattern, damageEffectFrameCount);
			}
		}
	}

	private static void CharacterAvatarDisplayNamesAreLocalizedForEveryLocale()
	{
		string repoRoot = FindRepositoryRoot();
		string avatarDirectory = Path.Combine(repoRoot, "resources", "character_avatars");
		string localizationPath = Path.Combine(repoRoot, "localization", "ui.csv");
		True(Directory.Exists(avatarDirectory));
		True(File.Exists(localizationPath));

		string[] localizationLines = File.ReadAllLines(localizationPath);
		True(localizationLines.Length > 1);
		List<string> header = ParseCsvLine(localizationLines[0]);
		True(header.Count > 1);

		var rowsByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (string line in localizationLines[1..])
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			List<string> row = ParseCsvLine(line);
			if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
			{
				continue;
			}

			rowsByKey[row[0].Trim()] = row;
		}

		string[] avatarPaths = Directory.GetFiles(avatarDirectory, "*.tres");
		True(avatarPaths.Length > 0);

		foreach (string avatarPath in avatarPaths)
		{
			string avatarText = File.ReadAllText(avatarPath);
			string avatarId = ReadTresString(avatarText, "Id");
			string fallbackName = ReadTresString(avatarText, "DisplayName");
			string displayNameKey = ReadTresString(avatarText, "DisplayNameKey");
			True(!string.IsNullOrWhiteSpace(avatarId));
			True(!string.IsNullOrWhiteSpace(fallbackName));
			True(!string.IsNullOrWhiteSpace(displayNameKey));
			True(displayNameKey.StartsWith("AVATAR_", StringComparison.Ordinal));
			True(displayNameKey.EndsWith("_NAME", StringComparison.Ordinal));
			if (!rowsByKey.TryGetValue(displayNameKey, out List<string>? row))
			{
				throw new InvalidOperationException($"missing localization row for {displayNameKey}");
			}

			Equal(header.Count, row.Count);

			for (int index = 1; index < header.Count; index++)
			{
				string locale = header[index];
				string localizedName = row[index].Trim();
				True(!string.IsNullOrWhiteSpace(localizedName));
				if (locale != "zh_CN" && locale != "zh_TW")
				{
					False(ContainsHanCharacter(localizedName));
				}
			}
		}
	}

	private static void PlayerCountLocalizationDifficultyRowsExistForEveryLocale()
	{
		string localizationPath = Path.Combine(FindRepositoryRoot(), "localization", "ui.csv");
		True(File.Exists(localizationPath));

		string[] localizationLines = File.ReadAllLines(localizationPath);
		True(localizationLines.Length > 1);
		List<string> header = ParseCsvLine(localizationLines[0]);
		True(header.Count > 1);

		var rowsByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (string line in localizationLines[1..])
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			List<string> row = ParseCsvLine(line);
			if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
			{
				continue;
			}

			rowsByKey[row[0].Trim()] = row;
		}

		foreach (string key in new[]
		{
			"PLAYER_COUNT_DIFFICULTY",
			"PLAYER_COUNT_DIFFICULTY_LOW",
			"PLAYER_COUNT_DIFFICULTY_MEDIUM",
			"PLAYER_COUNT_DIFFICULTY_HIGH",
		})
			{
				True(rowsByKey.TryGetValue(key, out List<string>? row));
				row ??= new List<string>();
				Equal(header.Count, row.Count);

			for (int index = 1; index < row.Count; index++)
			{
				True(!string.IsNullOrWhiteSpace(row[index]));
			}
		}
	}

	private static void RuntimeLocalizationKeysExistForEveryReference()
	{
		string repoRoot = FindRepositoryRoot();
		var rowsByKey = ReadLocalizationRowsByKey(Path.Combine(repoRoot, "localization", "ui.csv"), out _);
		var missing = new List<string>();

		foreach (string relativePath in EnumerateRuntimeLocalizationReferenceFiles(repoRoot))
		{
			string absolutePath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
			foreach (LocalizationReference reference in FindLocalizationReferences(relativePath, File.ReadAllLines(absolutePath)))
			{
				if (!rowsByKey.ContainsKey(reference.Key))
				{
					missing.Add($"{reference.Key} ({reference.RelativePath}:{reference.LineNumber})");
				}
			}
		}

		if (missing.Count > 0)
		{
			throw new InvalidOperationException("missing runtime localization keys: " + string.Join(", ", missing));
		}
	}

	private static void RuntimeLocalizationRowsCoverEverySupportedLanguage()
	{
		string repoRoot = FindRepositoryRoot();
		string localizationPath = Path.Combine(repoRoot, "localization", "ui.csv");
		var rowsByKey = ReadLocalizationRowsByKey(localizationPath, out List<string> header);
		List<string> expectedLocales = ReadSupportedLocalePreferences(repoRoot);
		List<string> actualLocales = header.Skip(1).Select(locale => locale.Trim()).ToList();

		if (expectedLocales.Count != actualLocales.Count)
		{
			throw new InvalidOperationException($"expected {expectedLocales.Count} localization locales, got {actualLocales.Count}");
		}

		for (int index = 0; index < expectedLocales.Count; index++)
		{
			if (!string.Equals(expectedLocales[index], actualLocales[index], StringComparison.Ordinal))
			{
				throw new InvalidOperationException($"localization header locale mismatch at column {index + 1}: expected {expectedLocales[index]}, got {actualLocales[index]}");
			}
		}

		foreach (KeyValuePair<string, List<string>> entry in rowsByKey)
		{
			List<string> row = entry.Value;
			if (row.Count != header.Count)
			{
				throw new InvalidOperationException($"localization row {entry.Key} has {row.Count} columns; expected {header.Count}");
			}

			for (int index = 1; index < row.Count; index++)
			{
				if (string.IsNullOrWhiteSpace(row[index]))
				{
					throw new InvalidOperationException($"localization row {entry.Key} is missing a value for locale {header[index]}");
				}
			}
		}
	}

	private static void RuntimeScenesDoNotExposeHardcodedNaturalLanguageText()
	{
		string repoRoot = FindRepositoryRoot();
		foreach (string relativePath in RuntimeHardcodedSceneRelativePaths)
		{
			string absolutePath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
			True(File.Exists(absolutePath));

			string[] lines = File.ReadAllLines(absolutePath);
			for (int index = 0; index < lines.Length; index++)
			{
				Match match = Regex.Match(lines[index], "^\\s*(?<property>(?:text|placeholder_text|tooltip_text|popup/item_\\d+/text))\\s*=\\s*\"(?<value>[^\"]*)\"$");
				if (!match.Success)
				{
					continue;
				}

				string value = match.Groups["value"].Value.Trim();
				if (IsAllowedRuntimeSceneText(value))
				{
					continue;
				}

				throw new InvalidOperationException($"hardcoded runtime scene text in {relativePath}:{index + 1} -> {value}");
			}
		}
	}

	private static void AvatarFeedbackEffectResourcesStayContiguous()
	{
		string repoRoot = FindRepositoryRoot();
		foreach ((string root, int minFrameCount) in new[]
		{
			("res://art/fx/avatar_feedback_score/run-001/processed", 4),
			("res://art/fx/avatar_feedback_hit/run-001/processed", 4),
			("res://art/fx/avatar_feedback_victory/run-001/processed", 4),
			("res://art/fx/avatar_feedback_defeat/run-001/processed", 4),
		})
		{
			int frameCount = CountContiguousFrames(repoRoot, root, "fx-{0}.png");
			True(frameCount >= minFrameCount);
		}
	}

	private static void AdventureUpgradePopupSceneHasNoEmbeddedImagePayloads()
	{
		string scenePath = Path.Combine(FindRepositoryRoot(), "scenes", "upgrade_choice_popup_asset_scene.tscn");
		string text = File.ReadAllText(scenePath);

		False(text.Contains("[sub_resource type=\"Image\""));
		False(text.Contains("[sub_resource type=\"ImageTexture\""));
		False(text.Contains("PackedByteArray("));
	}

	private static void AdventureUpgradeVisualCatalogExposesTwelveProcessedCards()
	{
		string repoRoot = FindRepositoryRoot();
		string catalogPath = Path.Combine(
			repoRoot,
			"resources",
			"adventure_upgrades",
			"visuals",
			"adventure_upgrade_card_visual_catalog.tres");
		True(File.Exists(catalogPath));
		string text = File.ReadAllText(catalogPath);

		Equal(12, Regex.Matches(text, @"(?m)^\[sub_resource type=""Resource"" id=""Entry_").Count);

		MatchCollection entryBlocks = Regex.Matches(
			text,
			@"(?ms)^\[sub_resource type=""Resource"" id=""Entry_[^""]+""\]\s+script = ExtResource\(""[^""]+""\)\s+Suit = ""([^""]+)""\s+Tier = ""([^""]+)""\s+TexturePath = ""([^""]+)""\s+Region = Rect2\(([^)]+)\)");
		Equal(12, entryBlocks.Count);

		var seenKeys = new HashSet<string>(StringComparer.Ordinal);
		var seenSuits = new HashSet<string>(StringComparer.Ordinal);
		var seenTiers = new HashSet<string>(StringComparer.Ordinal);

		for (int index = 0; index < entryBlocks.Count; index++)
		{
			string suit = entryBlocks[index].Groups[1].Value.Trim().ToLowerInvariant();
			string tier = entryBlocks[index].Groups[2].Value.Trim().ToLowerInvariant();
			string texturePath = entryBlocks[index].Groups[3].Value;
			True(seenKeys.Add($"{suit}::{tier}"));
			seenSuits.Add(suit);
			seenTiers.Add(tier);

			True(texturePath.StartsWith("res://generated-images/upgrade-popup-assets/", StringComparison.Ordinal));
			True(texturePath.Contains("/processed/", StringComparison.Ordinal));

			string[] regionParts = entryBlocks[index].Groups[4].Value.Split(',', StringSplitOptions.TrimEntries);
			Equal(4, regionParts.Length);
			True(float.TryParse(regionParts[2], out float width));
			True(float.TryParse(regionParts[3], out float height));
			True(width > 0.0f);
			True(height > 0.0f);
			AssertResPathExists(repoRoot, texturePath);
		}

		Equal(4, seenSuits.Count);
		True(seenSuits.Contains("hearts"));
		True(seenSuits.Contains("spades"));
		True(seenSuits.Contains("diamonds"));
		True(seenSuits.Contains("clubs"));

		Equal(3, seenTiers.Count);
		True(seenTiers.Contains("common"));
		True(seenTiers.Contains("uncommon"));
		True(seenTiers.Contains("rare"));
	}

	private static void AdventureUpgradeVisualCatalogIncludesPaletteFieldsAndRarityTierMappings()
	{
		string repoRoot = FindRepositoryRoot();
		string catalogPath = Path.Combine(
			repoRoot,
			"resources",
			"adventure_upgrades",
			"visuals",
			"adventure_upgrade_card_visual_catalog.tres");
		True(File.Exists(catalogPath));
		string text = File.ReadAllText(catalogPath);

		Equal(12, Regex.Matches(text, @"(?m)^TitleColor = Color\(([^)]+)\)$").Count);
		Equal(12, Regex.Matches(text, @"(?m)^GlowColor = Color\(([^)]+)\)$").Count);
		Equal(12, Regex.Matches(text, @"(?m)^RuleColor = Color\(([^)]+)\)$").Count);

		Equal(3, Regex.Matches(text, @"(?m)^\[sub_resource type=""Resource"" id=""RarityTier_").Count);

		MatchCollection mappingBlocks = Regex.Matches(
			text,
			@"(?ms)^\[sub_resource type=""Resource"" id=""RarityTier_[^""]+""\]\s+script = ExtResource\(""[^""]+""\)\s+Rarity = ""([^""]+)""\s+Tier = ""([^""]+)""$");
		Equal(3, mappingBlocks.Count);

		var tierMappings = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (Match mappingBlock in mappingBlocks)
		{
			string rarity = mappingBlock.Groups[1].Value.Trim().ToLowerInvariant();
			string mappedTier = mappingBlock.Groups[2].Value.Trim().ToLowerInvariant();
			tierMappings[rarity] = mappedTier;
		}

		Equal("common", tierMappings["common"]);
		Equal("uncommon", tierMappings["rare"]);
		Equal("rare", tierMappings["legendary"]);
	}

	private static void AdventureUpgradeVisualResolverReturnsAtlasTextureFromCatalog()
	{
		string resolverPath = Path.Combine(FindRepositoryRoot(), "scripts", "AdventureUpgradeCardVisualResolver.cs");
		True(File.Exists(resolverPath));

		string text = File.ReadAllText(resolverPath);
		True(text.Contains("adventure_upgrade_card_visual_catalog.tres", StringComparison.Ordinal));
		True(text.Contains("new AtlasTexture", StringComparison.Ordinal));
		True(text.Contains("entry.Region", StringComparison.Ordinal));
		True(text.Contains("entry.TexturePath", StringComparison.Ordinal));
	}

	private static void AdventureUpgradeVisualResolverUsesCatalogRarityTierMappings()
	{
		string resolverPath = Path.Combine(FindRepositoryRoot(), "scripts", "AdventureUpgradeCardVisualResolver.cs");
		True(File.Exists(resolverPath));

		string text = File.ReadAllText(resolverPath);
		True(text.Contains("ResolveTier(", StringComparison.Ordinal));
		True(text.Contains("LoadCatalog()", StringComparison.Ordinal));
		True(text.Contains("catalog.ResolveTier", StringComparison.Ordinal));
	}

	private static void AdventureUpgradeRuntimeNoLongerHardcodesPopupArtSwitchTables()
	{
		string runtimePath = Path.Combine(FindRepositoryRoot(), "scripts", "GameBoardScene.AdventureUpgrades.cs");
		string text = File.ReadAllText(runtimePath);

		False(text.Contains("GetAdventureUpgradeCardTextureRegion(", StringComparison.Ordinal));
		False(text.Contains("GetAdventureUpgradeCardTexturePath(", StringComparison.Ordinal));
		False(text.Contains("res://generated-images/upgrade-popup-assets/hearts/processed/hearts-common.png", StringComparison.Ordinal));
		False(text.Contains("res://generated-images/upgrade-popup-assets/spades/processed/spades-common.png", StringComparison.Ordinal));
		False(text.Contains("res://generated-images/upgrade-popup-assets/diamonds/processed/diamonds-common.png", StringComparison.Ordinal));
		False(text.Contains("res://generated-images/upgrade-popup-assets/clubs/processed/clubs-common.png", StringComparison.Ordinal));
		False(text.Contains("MapAdventureUpgradeRarityToTextureTier(", StringComparison.Ordinal));
		False(text.Contains("ApplyAdventureUpgradeChoicePalette(", StringComparison.Ordinal));
		False(text.Contains("normalizedSuit == ", StringComparison.Ordinal));
		False(text.Contains("normalizedRarity == ", StringComparison.Ordinal));
	}

	private static void AdventureUpgradePreviewScriptReadsPopupArtMetadataFromCatalog()
	{
		string previewScriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "UpgradeChoicePopupPreview.gd");
		string text = File.ReadAllText(previewScriptPath);

		True(text.Contains("resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres", StringComparison.Ordinal));
		False(text.Contains("hearts-common.png", StringComparison.Ordinal));
		False(text.Contains("spades-common.png", StringComparison.Ordinal));
		False(text.Contains("diamonds-common.png", StringComparison.Ordinal));
		False(text.Contains("clubs-common.png", StringComparison.Ordinal));
		False(text.Contains("Image.new()", StringComparison.Ordinal));
		False(text.Contains("ImageTexture.create_from_image", StringComparison.OrdinalIgnoreCase));
	}

	private static void AdventureUpgradeOverlaySampleArtUsesCatalogKeysInsteadOfHardcodedAtlasMetadata()
	{
		string overlayScenePath = Path.Combine(FindRepositoryRoot(), "scenes", "adventure_upgrade_selection_overlay.tscn");
		string text = File.ReadAllText(overlayScenePath);

		False(text.Contains("AtlasTexture_sample_", StringComparison.Ordinal));
		False(text.Contains("generated-images/upgrade-popup-assets/", StringComparison.Ordinal));
		True(text.Contains("PreviewSuit = \"diamonds\"", StringComparison.Ordinal));
		True(text.Contains("PreviewSuit = \"hearts\"", StringComparison.Ordinal));
		True(text.Contains("PreviewSuit = \"spades\"", StringComparison.Ordinal));
		True(text.Contains("PreviewTier = \"uncommon\"", StringComparison.Ordinal));
		True(text.Contains("PreviewTier = \"common\"", StringComparison.Ordinal));
		True(text.Contains("PreviewTier = \"rare\"", StringComparison.Ordinal));
	}

	private static void ItemCardVisualResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		string itemCardDirectory = Path.Combine(repoRoot, "resources", "item_cards");
		True(Directory.Exists(itemCardDirectory));

		string[] itemCardPaths = Directory.GetFiles(itemCardDirectory, "*.tres");
		True(itemCardPaths.Length > 0);

		foreach (string itemCardPath in itemCardPaths)
		{
			string itemCardText = File.ReadAllText(itemCardPath);
			string itemCardId = ReadTresString(itemCardText, "Id");
			True(!string.IsNullOrWhiteSpace(itemCardId));

			int frameCount = Math.Max(1, ReadTresInt(itemCardText, "FrameCount", 1));
			string framePathPattern = ReadTresString(itemCardText, "FramePathPattern");
			if (string.IsNullOrWhiteSpace(framePathPattern))
			{
				string artRunName = ReadTresString(itemCardText, "ArtRunName");
				True(!string.IsNullOrWhiteSpace(artRunName));
				framePathPattern = $"res://art/cards/item_cards/{itemCardId}/{artRunName}/processed/frames/frame-{{0:000}}.png";
			}

			AssertFramePatternExists(repoRoot, framePathPattern, frameCount);

			string attachmentOverlayTexturePath = ReadTresString(itemCardText, "AttachmentOverlayTexturePath");
			if (!string.IsNullOrWhiteSpace(attachmentOverlayTexturePath))
			{
				AssertResPathExists(repoRoot, attachmentOverlayTexturePath);
			}
		}
	}

	private static void AttachmentSelectionPreviewFollowsPendingItemCard()
	{
		Equal("double", AttachmentSelectionPreviewRules.GetPreviewItemCardId("double"));
		Equal("magnet", AttachmentSelectionPreviewRules.GetPreviewItemCardId("magnet"));
		Equal("magnet", AttachmentSelectionPreviewRules.GetPreviewItemCardId(string.Empty));
	}

	private static void MainMenuBubblesRiseAndFadeThroughLifecycle()
	{
		MainMenuBubbleMotionState early = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 0.4f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);
		MainMenuBubbleMotionState middle = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 2.0f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);
		MainMenuBubbleMotionState late = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 3.8f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);
		MainMenuBubbleMotionState wrapped = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 4.2f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);

		True(middle.RiseOffsetY < early.RiseOffsetY - 35.0f);
		True(late.RiseOffsetY < middle.RiseOffsetY - 25.0f);
		True(late.AlphaMultiplier < middle.AlphaMultiplier);
		True(wrapped.RiseOffsetY > late.RiseOffsetY + 80.0f);
		True(wrapped.AlphaMultiplier < middle.AlphaMultiplier);
	}

	private static void MainMenuBubblesHideBeforeCrossingWaterline()
	{
		float visible = MainMenuBubbleMotionRules.GetSurfaceAlphaMultiplier(
			centerY: 430.0f,
			topOffset: 32.0f,
			waterlineY: 340.0f,
			fadeDistance: 48.0f);
		float hidden = MainMenuBubbleMotionRules.GetSurfaceAlphaMultiplier(
			centerY: 365.0f,
			topOffset: 32.0f,
			waterlineY: 340.0f,
			fadeDistance: 48.0f);

		True(visible > 0.9f);
		Equal(0.0f, hidden);
	}

	private static void MainMenuBubbleVisualResourcePointsToExistingFile()
	{
		AssertResPathExists(FindRepositoryRoot(), "res://art/fx/environment/main_menu_bubble/run-001/processed/fx-1.png");
	}

	private static void BoardEffectResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		foreach ((string pattern, int frameCount) in new[]
		{
			("res://art/cards/ocean_animated_deck/back/processed/frames/frame-{0:000}.png", 8),
			("res://art/cards/deck_shuffle/overhand_drop/game_frames/frame-{0:000}.png", 6),
			("res://art/fx/question_card_transform/run-001/processed/fx-{0}.png", 6),
			("res://art/fx/special_card_trigger/ocean_chrysanthemum_preview5/sea_anemone_rays/processed/fx-{0}.png", 4),
			("res://art/fx/bomb_pair_destroy/card_rip_burst/run-001/processed/fx-{0}.png", 4),
			("res://art/fx/lens_reveal_sprites/run-001/processed/fx-{0}.png", 4),
		})
		{
			AssertFramePatternExists(repoRoot, pattern, frameCount);
		}

		foreach (string resPath in new[]
		{
			"res://art/fx/slime_card_overlay/run-001/processed/card-aspect.png",
			"res://art/ui/result_text_prompt/run-001/processed/victory-text-prompt-effect.png",
			"res://art/ui/result_text_prompt/run-001/processed/defeat-text-prompt-effect.png",
		})
		{
			AssertResPathExists(repoRoot, resPath);
		}
	}

	private static void GameLoopPhasePrecedenceIsStable()
	{
		Equal(GameLoopPhase.LeavingGame, GameLoopPhaseRules.GetPhase(new GameLoopPhaseState(
			IsLeavingGame: true,
			IsGameOver: true,
			IsOpeningDeal: true,
			HasPendingItemSelection: true,
			IsProcessingCardPlay: true,
			IsResolvingTurnSequence: true,
			IsMainPlayerTurn: true)));
		Equal(GameLoopPhase.PendingMainPlayerSelection, GameLoopPhaseRules.GetPhase(new GameLoopPhaseState(
			IsLeavingGame: false,
			IsGameOver: false,
			IsOpeningDeal: false,
			HasPendingItemSelection: true,
			IsProcessingCardPlay: true,
			IsResolvingTurnSequence: false,
			IsMainPlayerTurn: true)));
		Equal(GameLoopPhase.AwaitingMainPlayerAction, GameLoopPhaseRules.GetPhase(new GameLoopPhaseState(
			IsLeavingGame: false,
			IsGameOver: false,
			IsOpeningDeal: false,
			HasPendingItemSelection: false,
			IsProcessingCardPlay: false,
			IsResolvingTurnSequence: false,
			IsMainPlayerTurn: true)));
	}

	private static void TurnStateMachineAdvancesContextGeneration()
	{
		var stateMachine = new TurnStateMachine();
		TurnContext initialContext = stateMachine.CurrentContext;

		int generation = stateMachine.AdvanceGeneration();
		TurnContext nextContext = stateMachine.CurrentContext;

		Equal(1, generation);
		False(initialContext.IsCurrent(stateMachine.Generation));
		True(nextContext.IsCurrent(stateMachine.Generation));
	}

	private static void TurnStateMachineGatesActionStarts()
	{
		var stateMachine = new TurnStateMachine();
		var awaitingMainPlayer = new GameLoopPhaseState(
			IsLeavingGame: false,
			IsGameOver: false,
			IsOpeningDeal: false,
			HasPendingItemSelection: false,
			IsProcessingCardPlay: false,
			IsResolvingTurnSequence: false,
			IsMainPlayerTurn: true);
		var pendingSelection = awaitingMainPlayer with { HasPendingItemSelection = true };
		var opponentTurn = awaitingMainPlayer with { IsMainPlayerTurn = false };

		True(stateMachine.CanBeginMainPlayerAction(awaitingMainPlayer));
		False(stateMachine.CanBeginMainPlayerAction(pendingSelection));
		False(stateMachine.CanBeginOpponentSequence(awaitingMainPlayer));
		True(stateMachine.CanBeginOpponentSequence(opponentTurn));
		False(stateMachine.CanBeginOpponentSequence(opponentTurn with { IsResolvingTurnSequence = true }));
	}

	private static void TurnRulesSkipInactivePlayers()
	{
		int[] turnOrder = { 0, 2, 1, 3 };

		Equal(1, GameTurnRules.GetNextTurnPlayerIndex(turnOrder, currentPlayerIndex: 2, playerCount: 2, fallbackPlayerIndex: 0));
		Equal(0, GameTurnRules.GetNextTurnPlayerIndex(turnOrder, currentPlayerIndex: 7, playerCount: 2, fallbackPlayerIndex: 0));
	}

	private static void TurnRulesFindSingleRemainingPlayer()
	{
		var singleRemaining = new[]
		{
			new GameTurnRules.PlayerCardState(0, 0, false),
			new GameTurnRules.PlayerCardState(0, 0, true),
			new GameTurnRules.PlayerCardState(0, 0, false),
		};
		var multipleRemaining = new[]
		{
			new GameTurnRules.PlayerCardState(1, 0, false),
			new GameTurnRules.PlayerCardState(0, 1, false),
		};

		True(GameTurnRules.TryGetSingleRemainingPlayer(singleRemaining, playerCount: 3, out int remainingPlayerIndex));
		Equal(1, remainingPlayerIndex);
		False(GameTurnRules.TryGetSingleRemainingPlayer(multipleRemaining, playerCount: 2, out _));
	}

	private static void TurnProgressStartsAndResetsQuestionCardTracking()
	{
		QuestionCardRefreshState tracking = TurnProgressRules.StartQuestionCardRefreshTracking(2);
		QuestionCardRefreshState reset = TurnProgressRules.ResetQuestionCardRefreshTracking();

		Equal(0, tracking.CompletedTurnsSinceQuestionCardRefresh);
		Equal(2, tracking.QuestionCardRefreshAnchorPlayerIndex);
		Equal(0, reset.CompletedTurnsSinceQuestionCardRefresh);
		Equal(-1, reset.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void TurnProgressWaitsForAnchorTurnBeforeRefreshingQuestionCard()
	{
		QuestionCardRefreshAdvanceResult result = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: false,
			new QuestionCardRefreshState(1, 2),
			nextTurnPlayerIndex: 1,
			playerCount: 4,
			refreshCyclesAfterCapture: 2);

		False(result.ShouldRefreshQuestionCard);
		Equal(2, result.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(2, result.State.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void TurnProgressRefreshesQuestionCardAfterConfiguredCycle()
	{
		QuestionCardRefreshAdvanceResult result = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: false,
			new QuestionCardRefreshState(3, 2),
			nextTurnPlayerIndex: 2,
			playerCount: 2,
			refreshCyclesAfterCapture: 2);

		True(result.ShouldRefreshQuestionCard);
		Equal(0, result.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(-1, result.State.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void TurnProgressStaysBlockedWhenTableSpecialCardsArePresent()
	{
		QuestionCardRefreshState currentState = new(3, 2);
		QuestionCardRefreshAdvanceResult result = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: true,
			currentState,
			nextTurnPlayerIndex: 2,
			playerCount: 2,
			refreshCyclesAfterCapture: 2);

		False(result.ShouldRefreshQuestionCard);
		Equal(currentState.CompletedTurnsSinceQuestionCardRefresh, result.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(currentState.QuestionCardRefreshAnchorPlayerIndex, result.State.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void SteamMetadataRejectsDirtyValues()
	{
		var dirty = new Dictionary<string, string>
		{
			[SteamLobbyMetadata.ProtocolKey] = NetworkSessionDefaults.ProtocolVersion.ToString(),
			[SteamLobbyMetadata.SessionIdKey] = "session",
			[SteamLobbyMetadata.RoomNameKey] = "bad@room",
			[SteamLobbyMetadata.HostNameKey] = "host",
			[SteamLobbyMetadata.PlayerCountKey] = "four",
			[SteamLobbyMetadata.ItemModeKey] = "sometimes",
			[SteamLobbyMetadata.WinningScoreKey] = "75",
			[SteamLobbyMetadata.StatusKey] = "Lobby",
		};

		False(SteamLobbyMetadata.TryReadRoom(dirty, 10, out _));

		var dirtyRoomName = new Dictionary<string, string>
		{
			[SteamLobbyMetadata.ProtocolKey] = NetworkSessionDefaults.ProtocolVersion.ToString(),
			[SteamLobbyMetadata.SessionIdKey] = "session",
			[SteamLobbyMetadata.RoomNameKey] = "bad@room",
			[SteamLobbyMetadata.HostNameKey] = "host",
			[SteamLobbyMetadata.PlayerCountKey] = "4",
			[SteamLobbyMetadata.ItemModeKey] = "true",
			[SteamLobbyMetadata.WinningScoreKey] = "100",
			[SteamLobbyMetadata.StatusKey] = "Lobby",
		};

		False(SteamLobbyMetadata.TryReadRoom(dirtyRoomName, 10, out _));
	}

	private static void RoomNameRulesRestrictSpecialSymbols()
	{
		True(RoomNameRules.IsValid("我的房间 01"));
		True(RoomNameRules.IsValid("Room-01_A"));
		False(RoomNameRules.IsValid(""));
		False(RoomNameRules.IsValid("bad@room"));
		False(RoomNameRules.IsValid("bad/room"));
		False(RoomNameRules.IsValid("bad.room"));
		False(RoomNameRules.IsValid(new string('A', RoomNameRules.MaxLength + 1)));
		Equal(RoomNameRules.DefaultRoomName, RoomNameRules.Normalize("bad@room"));
	}

	private static void SteamMetadataParsesValidValuesDefensively()
	{
		var metadata = new Dictionary<string, string>
		{
			[SteamLobbyMetadata.ProtocolKey] = NetworkSessionDefaults.ProtocolVersion.ToString(),
			[SteamLobbyMetadata.SessionIdKey] = "session",
			[SteamLobbyMetadata.RoomNameKey] = "房间 01",
			[SteamLobbyMetadata.HostNameKey] = "host",
			[SteamLobbyMetadata.PlayerCountKey] = "4",
			[SteamLobbyMetadata.ItemModeKey] = "1",
			[SteamLobbyMetadata.WinningScoreKey] = "200",
			[SteamLobbyMetadata.StatusKey] = "Lobby",
		};

		True(SteamLobbyMetadata.TryReadRoom(metadata, 10, out NetworkRoomInfo room));
		Equal(NetworkTransport.Steam, room.Transport);
		Equal("房间 01", room.RoomName);
		Equal(4, room.PlayerCount);
		True(room.ItemMode);
		Equal(200, room.WinningScore);
		Equal(10UL, room.SteamLobbyId);
	}

	private static void SteamMetadataKeepsActiveMatchLobbyJoinableForReconnect()
	{
		False(SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(NetworkSessionState.Idle));
		False(SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(NetworkSessionState.Searching));
		False(SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(NetworkSessionState.Hosting));
		False(SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(NetworkSessionState.Joining));
		True(SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(NetworkSessionState.Lobby));
		True(SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(NetworkSessionState.Starting));
		True(SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(NetworkSessionState.LoadingScene));
		True(SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(NetworkSessionState.InGame));
		False(SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(NetworkSessionState.Reconnecting));
		False(SteamLobbyMetadata.ShouldSteamLobbyStayJoinable(NetworkSessionState.Failed));
	}

	private static void MultiplayerLobbyStartRequiresFilledReadySeats()
	{
		var readyPlayers = new[]
		{
			Player(peerId: 1, seatIndex: 0, isHost: true, isReady: true),
			Player(peerId: 2, seatIndex: 1, isHost: false, isReady: true),
		};
		var missingReady = new[]
		{
			Player(peerId: 1, seatIndex: 0, isHost: true, isReady: true),
			Player(peerId: 2, seatIndex: 1, isHost: false, isReady: false),
		};
		var missingSeat = new[]
		{
			Player(peerId: 1, seatIndex: 0, isHost: true, isReady: true),
			Player(peerId: 2, seatIndex: 2, isHost: false, isReady: true),
		};

		True(MultiplayerLobbyRules.CanStart(readyPlayers, 2));
		False(MultiplayerLobbyRules.CanStart(missingReady, 2));
		False(MultiplayerLobbyRules.CanStart(missingSeat, 2));
		Equal(1, MultiplayerLobbyRules.GetNextSeatIndex(new[] { readyPlayers[0] }, 2));
		Equal(-1, MultiplayerLobbyRules.GetNextSeatIndex(readyPlayers, 2));
	}

	private static void MultiplayerLobbyOnlyAcceptsJoinRequestsInLobbyState()
	{
		True(MultiplayerLobbyRules.CanAcceptJoinRequest(NetworkSessionState.Lobby));
		False(MultiplayerLobbyRules.CanAcceptJoinRequest(NetworkSessionState.Idle));
		False(MultiplayerLobbyRules.CanAcceptJoinRequest(NetworkSessionState.Searching));
		False(MultiplayerLobbyRules.CanAcceptJoinRequest(NetworkSessionState.Hosting));
		False(MultiplayerLobbyRules.CanAcceptJoinRequest(NetworkSessionState.Joining));
		False(MultiplayerLobbyRules.CanAcceptJoinRequest(NetworkSessionState.Starting));
		False(MultiplayerLobbyRules.CanAcceptJoinRequest(NetworkSessionState.LoadingScene));
		False(MultiplayerLobbyRules.CanAcceptJoinRequest(NetworkSessionState.InGame));
		False(MultiplayerLobbyRules.CanAcceptJoinRequest(NetworkSessionState.Reconnecting));
		False(MultiplayerLobbyRules.CanAcceptJoinRequest(NetworkSessionState.Failed));
	}

	private static void MultiplayerLobbyRoomListOnlyAllowsJoiningLobbyRooms()
	{
		True(MultiplayerLobbyRules.CanJoinListedRoom(NetworkSessionState.Lobby.ToString()));
		False(MultiplayerLobbyRules.CanJoinListedRoom(NetworkSessionState.Starting.ToString()));
		False(MultiplayerLobbyRules.CanJoinListedRoom(NetworkSessionState.LoadingScene.ToString()));
		False(MultiplayerLobbyRules.CanJoinListedRoom(NetworkSessionState.InGame.ToString()));
		False(MultiplayerLobbyRules.CanJoinListedRoom("not-a-state"));
	}

	private static void MultiplayerLobbyOnlyAutoAcceptsSteamInvitesFromSafeStates()
	{
		True(MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(NetworkSessionState.Idle));
		True(MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(NetworkSessionState.Searching));
		True(MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(NetworkSessionState.Failed));

		False(MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(NetworkSessionState.Hosting));
		False(MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(NetworkSessionState.Joining));
		False(MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(NetworkSessionState.Lobby));
		False(MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(NetworkSessionState.Starting));
		False(MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(NetworkSessionState.LoadingScene));
		False(MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(NetworkSessionState.InGame));
		False(MultiplayerLobbyRules.CanAutoAcceptSteamJoinRequest(NetworkSessionState.Reconnecting));
	}

	private static void MultiplayerBoardSyncResolvesSenderSeat()
	{
		var players = new[]
		{
			Player(peerId: 1, seatIndex: 0, isHost: true, isReady: true),
			Player(peerId: 23, seatIndex: 2, isHost: false, isReady: true),
		};

		True(MultiplayerBoardSyncRules.TryResolveSeatFromPeer(players, 23, out int seatIndex));
		Equal(2, seatIndex);
		False(MultiplayerBoardSyncRules.TryResolveSeatFromPeer(players, 99, out _));
	}

	private static void SceneLoadAckAcceptsOnlySenderOwnSeat()
	{
		var players = new[]
		{
			Player(peerId: 1, seatIndex: 0, isHost: true, isReady: true),
			Player(peerId: 23, seatIndex: 2, isHost: false, isReady: true),
		};

		bool accepted = InvokeCanAcceptSceneLoadAck(players, senderPeerId: 23, reportedSeatIndex: 2, out int senderSeatIndex);
		True(accepted);
		Equal(2, senderSeatIndex);
	}

	private static void SceneLoadAckRejectsUnregisteredSender()
	{
		var players = new[]
		{
			Player(peerId: 1, seatIndex: 0, isHost: true, isReady: true),
			Player(peerId: 23, seatIndex: 2, isHost: false, isReady: true),
		};

		bool accepted = InvokeCanAcceptSceneLoadAck(players, senderPeerId: 99, reportedSeatIndex: 2, out int senderSeatIndex);
		False(accepted);
		Equal(-1, senderSeatIndex);
	}

	private static void SceneLoadAckRejectsMismatchedReportedSeat()
	{
		var players = new[]
		{
			Player(peerId: 1, seatIndex: 0, isHost: true, isReady: true),
			Player(peerId: 23, seatIndex: 2, isHost: false, isReady: true),
		};

		bool accepted = InvokeCanAcceptSceneLoadAck(players, senderPeerId: 23, reportedSeatIndex: 1, out int senderSeatIndex);
		False(accepted);
		Equal(2, senderSeatIndex);
	}

	private static void MultiplayerBoardSyncRejectsInvalidPlayRequests()
	{
		MultiplayerBoardActionValidation accepted = MultiplayerBoardSyncRules.ValidatePlayHandCardRequest(
			senderSeatIndex: 1,
			currentTurnPlayerIndex: 1,
			requestedHandIndex: 2,
			handCount: 5,
			requestTurnSequenceGeneration: 7,
			currentTurnSequenceGeneration: 7,
			isGameOver: false,
			isActionInProgress: false);
		True(accepted.Accepted);
		Equal(MultiplayerBoardActionRejectReason.None, accepted.RejectReason);

		AssertRejected(MultiplayerBoardActionRejectReason.UnknownSender, senderSeatIndex: -1, currentTurnPlayerIndex: 1, handIndex: 0, handCount: 5, requestGeneration: 7, currentGeneration: 7);
		AssertRejected(MultiplayerBoardActionRejectReason.WrongTurn, senderSeatIndex: 0, currentTurnPlayerIndex: 1, handIndex: 0, handCount: 5, requestGeneration: 7, currentGeneration: 7);
		AssertRejected(MultiplayerBoardActionRejectReason.InvalidHandIndex, senderSeatIndex: 1, currentTurnPlayerIndex: 1, handIndex: 5, handCount: 5, requestGeneration: 7, currentGeneration: 7);
		AssertRejected(MultiplayerBoardActionRejectReason.StaleTurnGeneration, senderSeatIndex: 1, currentTurnPlayerIndex: 1, handIndex: 0, handCount: 5, requestGeneration: 6, currentGeneration: 7);
		AssertRejected(MultiplayerBoardActionRejectReason.ActionInProgress, senderSeatIndex: 1, currentTurnPlayerIndex: 1, handIndex: 0, handCount: 5, requestGeneration: 7, currentGeneration: 7, isActionInProgress: true);
		AssertRejected(MultiplayerBoardActionRejectReason.GameOver, senderSeatIndex: 1, currentTurnPlayerIndex: 1, handIndex: 0, handCount: 5, requestGeneration: 7, currentGeneration: 7, isGameOver: true);
	}

	private static void MultiplayerBoardSyncLimitsPrivateHandVisibility()
	{
		True(MultiplayerBoardSyncRules.ShouldIncludePrivateHandCards(cardOwnerSeatIndex: 2, recipientSeatIndex: 2));
		False(MultiplayerBoardSyncRules.ShouldIncludePrivateHandCards(cardOwnerSeatIndex: 2, recipientSeatIndex: 1));
	}

	private static void NetworkReconnectCredentialsAreSentBeforeFirstBoardSnapshot()
	{
		False(NetworkReconnectRules.ShouldSendReconnectCredentials(
			isHost: false,
			state: NetworkSessionState.Starting));

		False(NetworkReconnectRules.ShouldSendReconnectCredentials(
			isHost: true,
			state: NetworkSessionState.Lobby));

		True(NetworkReconnectRules.ShouldSendReconnectCredentials(
			isHost: true,
			state: NetworkSessionState.Starting));

		True(NetworkReconnectRules.ShouldSendReconnectCredentials(
			isHost: true,
			state: NetworkSessionState.LoadingScene));

		True(NetworkReconnectRules.ShouldSendReconnectCredentials(
			isHost: true,
			state: NetworkSessionState.InGame));
	}

	private static void NetworkReconnectRulesEndMatchBeforeReconnectAfterHostTermination()
	{
		True(NetworkReconnectRules.ShouldBroadcastHostEndedMatch(
			isHost: true,
			state: NetworkSessionState.InGame));

		False(NetworkReconnectRules.ShouldBroadcastHostEndedMatch(
			isHost: true,
			state: NetworkSessionState.Lobby));

		False(NetworkReconnectRules.ShouldAttemptReconnectAfterServerDisconnect(
			NetworkSessionState.InGame,
			isHost: false,
			canAttemptReconnectClaim: true,
			hostEndedMatch: true));

		True(NetworkReconnectRules.ShouldAttemptReconnectAfterServerDisconnect(
			NetworkSessionState.InGame,
			isHost: false,
			canAttemptReconnectClaim: true,
			hostEndedMatch: false));
	}

	private static void NetworkReconnectRulesIgnoreStaleDisconnectAfterReconnect()
	{
		True(NetworkReconnectRules.ShouldIgnoreLateServerDisconnectAfterReconnect(
			NetworkSessionState.InGame,
			isHost: false,
			hasActivePeer: true,
			peerIsConnected: true));

		False(NetworkReconnectRules.ShouldIgnoreLateServerDisconnectAfterReconnect(
			NetworkSessionState.Reconnecting,
			isHost: false,
			hasActivePeer: true,
			peerIsConnected: true));

		False(NetworkReconnectRules.ShouldIgnoreLateServerDisconnectAfterReconnect(
			NetworkSessionState.InGame,
			isHost: true,
			hasActivePeer: true,
			peerIsConnected: true));

		False(NetworkReconnectRules.ShouldIgnoreLateServerDisconnectAfterReconnect(
			NetworkSessionState.InGame,
			isHost: false,
			hasActivePeer: true,
			peerIsConnected: false));
	}

	private static void NetworkReconnectRulesOnlyHandOffNonHostDisconnectsToAi()
	{
		True(NetworkReconnectRules.ShouldHandOffDisconnectedSeatToAi(
			NetworkSessionState.InGame,
			disconnectedPlayerIsHost: false));

		False(NetworkReconnectRules.ShouldHandOffDisconnectedSeatToAi(
			NetworkSessionState.InGame,
			disconnectedPlayerIsHost: true));

		False(NetworkReconnectRules.ShouldHandOffDisconnectedSeatToAi(
			NetworkSessionState.Lobby,
			disconnectedPlayerIsHost: false));
	}

	private static void NetworkReconnectRulesDeferClaimWhileSeatStillConnected()
	{
		True(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanConnected,
			currentPeerId: 22,
			requestPeerId: 33,
			currentReconnectToken: "token-1",
			requestReconnectToken: "token-1",
			currentStableUserId: "stable-1",
			requestStableUserId: "stable-1"));

		True(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanConnected,
			currentPeerId: 22,
			requestPeerId: 22,
			currentReconnectToken: "token-1",
			requestReconnectToken: "token-1",
			currentStableUserId: "stable-1",
			requestStableUserId: "stable-1"));

		False(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.AiProxyReclaimable,
			currentPeerId: 0,
			requestPeerId: 33,
			currentReconnectToken: "token-1",
			requestReconnectToken: "token-1",
			currentStableUserId: "stable-1",
			requestStableUserId: "stable-1"));

		False(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanConnected,
			currentPeerId: 22,
			requestPeerId: 33,
			currentReconnectToken: "token-1",
			requestReconnectToken: "token-2",
			currentStableUserId: "stable-1",
			requestStableUserId: "stable-1"));

		False(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanConnected,
			currentPeerId: 22,
			requestPeerId: 33,
			currentReconnectToken: "token-1",
			requestReconnectToken: "token-1",
			currentStableUserId: "stable-1",
			requestStableUserId: "stable-2"));

		True(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanReclaimPending,
			currentPeerId: 44,
			requestPeerId: 55,
			currentReconnectToken: "token-1",
			requestReconnectToken: "token-1",
			currentStableUserId: "stable-1",
			requestStableUserId: "stable-1"));

		False(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanReclaimPending,
			currentPeerId: 44,
			requestPeerId: 44,
			currentReconnectToken: "token-1",
			requestReconnectToken: "token-1",
			currentStableUserId: "stable-1",
			requestStableUserId: "stable-1"));
	}

	private static void NetworkReconnectRulesScopeRealtimeBoardEventsToActiveInGameClients()
	{
		True(NetworkReconnectRules.ShouldSendRealtimeBoardEventToPeer(
			isHost: true,
			state: NetworkSessionState.InGame,
			playerIsConnected: true,
			playerIsHost: false));

		False(NetworkReconnectRules.ShouldSendRealtimeBoardEventToPeer(
			isHost: false,
			state: NetworkSessionState.InGame,
			playerIsConnected: true,
			playerIsHost: false));

		False(NetworkReconnectRules.ShouldSendRealtimeBoardEventToPeer(
			isHost: true,
			state: NetworkSessionState.Lobby,
			playerIsConnected: true,
			playerIsHost: false));

		False(NetworkReconnectRules.ShouldSendRealtimeBoardEventToPeer(
			isHost: true,
			state: NetworkSessionState.InGame,
			playerIsConnected: false,
			playerIsHost: false));

		False(NetworkReconnectRules.ShouldSendRealtimeBoardEventToPeer(
			isHost: true,
			state: NetworkSessionState.InGame,
			playerIsConnected: true,
			playerIsHost: true));

		True(NetworkReconnectRules.ShouldQueueRealtimeBoardEventForClient(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame));

		False(NetworkReconnectRules.ShouldQueueRealtimeBoardEventForClient(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.Reconnecting));

		False(NetworkReconnectRules.ShouldQueueRealtimeBoardEventForClient(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			networkState: NetworkSessionState.InGame));

		False(NetworkReconnectRules.ShouldQueueRealtimeBoardEventForClient(
			isNetworkMultiplayer: false,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame));
	}

	private static void NetworkReconnectRulesClassifySteamLobbyJoinRetries()
	{
		True(NetworkReconnectRules.IsSteamLobbyJoinSuccessful(response: 1));
		False(NetworkReconnectRules.IsSteamLobbyJoinSuccessful(response: 5));

		True(NetworkReconnectRules.ShouldRetrySteamLobbyJoinAfterFailure(
			NetworkSessionState.Reconnecting,
			response: 5));

		False(NetworkReconnectRules.ShouldRetrySteamLobbyJoinAfterFailure(
			NetworkSessionState.Joining,
			response: 5));
		False(NetworkReconnectRules.ShouldRetrySteamLobbyJoinAfterFailure(
			NetworkSessionState.Reconnecting,
			response: 2));
		False(NetworkReconnectRules.ShouldRetrySteamLobbyJoinAfterFailure(
			NetworkSessionState.Reconnecting,
			response: 3));
		False(NetworkReconnectRules.ShouldRetrySteamLobbyJoinAfterFailure(
			NetworkSessionState.Reconnecting,
			response: 4));
	}

	private static void NetworkReconnectRulesRejectSteamOwnerMigrationDuringReconnect()
	{
		True(NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			NetworkSessionState.Reconnecting,
			originalHostStableUserId: "steam:123",
			currentLobbyOwnerSteamId: 456));

		False(NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			NetworkSessionState.Joining,
			originalHostStableUserId: "steam:123",
			currentLobbyOwnerSteamId: 456));

		False(NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			NetworkSessionState.Reconnecting,
			originalHostStableUserId: "steam:123",
			currentLobbyOwnerSteamId: 123));

		False(NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			NetworkSessionState.Reconnecting,
			originalHostStableUserId: "peer:abc",
			currentLobbyOwnerSteamId: 456));

		False(NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			NetworkSessionState.Reconnecting,
			originalHostStableUserId: "steam:123",
			currentLobbyOwnerSteamId: 0));
	}

	private static void NetworkReconnectRulesApplyReconnectSnapshotImmediatelyOnClient()
	{
		True(NetworkReconnectRules.ShouldApplyBoardSnapshotImmediatelyForClient(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.Reconnecting));

		False(NetworkReconnectRules.ShouldApplyBoardSnapshotImmediatelyForClient(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame));

		False(NetworkReconnectRules.ShouldApplyBoardSnapshotImmediatelyForClient(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			networkState: NetworkSessionState.Reconnecting));

		False(NetworkReconnectRules.ShouldApplyBoardSnapshotImmediatelyForClient(
			isNetworkMultiplayer: false,
			isNetworkHost: false,
			networkState: NetworkSessionState.Reconnecting));
	}

	private static void NetworkReconnectRulesCompleteCatchUpOnlyAfterVisualQueueDrains()
	{
		True(NetworkReconnectRules.ShouldCompleteReconnectCatchupForClient(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.Reconnecting,
			isVisualQueueProcessing: false));

		False(NetworkReconnectRules.ShouldCompleteReconnectCatchupForClient(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.Reconnecting,
			isVisualQueueProcessing: true));

		False(NetworkReconnectRules.ShouldCompleteReconnectCatchupForClient(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			isVisualQueueProcessing: false));

		False(NetworkReconnectRules.ShouldCompleteReconnectCatchupForClient(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			networkState: NetworkSessionState.Reconnecting,
			isVisualQueueProcessing: false));
	}

	private static void NetworkReconnectOverlayShowsOnlyDuringClientCatchUp()
	{
		False(NetworkReconnectPresentationRules.ShouldShowOverlay(
			isNetworkMultiplayer: false,
			isNetworkHost: false,
			networkWaitForInitialize: false,
			networkState: NetworkSessionState.Idle,
			lastAppliedSnapshotId: 1));

		False(NetworkReconnectPresentationRules.ShouldShowOverlay(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			networkWaitForInitialize: true,
			networkState: NetworkSessionState.Reconnecting,
			lastAppliedSnapshotId: -1));

		True(NetworkReconnectPresentationRules.ShouldShowOverlay(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkWaitForInitialize: true,
			networkState: NetworkSessionState.InGame,
			lastAppliedSnapshotId: 5));

		True(NetworkReconnectPresentationRules.ShouldShowOverlay(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkWaitForInitialize: false,
			networkState: NetworkSessionState.Reconnecting,
			lastAppliedSnapshotId: 5));

		True(NetworkReconnectPresentationRules.ShouldShowOverlay(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkWaitForInitialize: false,
			networkState: NetworkSessionState.InGame,
			lastAppliedSnapshotId: 0));

		False(NetworkReconnectPresentationRules.ShouldShowOverlay(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkWaitForInitialize: false,
			networkState: NetworkSessionState.InGame,
			lastAppliedSnapshotId: 2));
	}

	private static void NetworkReconnectPresentationMatchesReconnectPlanCopy()
	{
		Equal("断线中 · AI 接管", NetworkReconnectPresentationRules.GetSeatControlStatusText(
			SeatControlMode.AiProxyReclaimable,
			usesChineseLocale: true));
		Equal("Disconnected · AI takeover", NetworkReconnectPresentationRules.GetSeatControlStatusText(
			SeatControlMode.AiProxyReclaimable,
			usesChineseLocale: false));
		Equal("已重连 · 下回合恢复", NetworkReconnectPresentationRules.GetSeatControlStatusText(
			SeatControlMode.HumanReclaimPending,
			usesChineseLocale: true));
		Equal("Reconnected · next turn", NetworkReconnectPresentationRules.GetSeatControlStatusText(
			SeatControlMode.HumanReclaimPending,
			usesChineseLocale: false));
		Equal(string.Empty, NetworkReconnectPresentationRules.GetSeatControlStatusText(
			SeatControlMode.HumanConnected,
			usesChineseLocale: true));

		Equal("正在同步最新战局…", NetworkReconnectPresentationRules.GetReconnectOverlayText(usesChineseLocale: true));
		Equal("Syncing latest match...", NetworkReconnectPresentationRules.GetReconnectOverlayText(usesChineseLocale: false));
		Equal("你已重连，当前回合结束后将在你的下一个回合恢复控制。", NetworkReconnectPresentationRules.GetReconnectNoticeText(usesChineseLocale: true));
		Equal("Reconnected. Control returns on your next turn.", NetworkReconnectPresentationRules.GetReconnectNoticeText(usesChineseLocale: false));
	}

	private static void LocalSeatInputEnablesOnlyForInGameHumanConnectedState()
	{
		True(LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: false,
			networkState: NetworkSessionState.Idle,
			seatStateFound: false,
			seatMode: SeatControlMode.AiProxyReclaimable));

		True(LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: true,
			networkState: NetworkSessionState.InGame,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanConnected));

		False(LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: true,
			networkState: NetworkSessionState.Reconnecting,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanConnected));

		False(LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: true,
			networkState: NetworkSessionState.InGame,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanReclaimPending));

		False(LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: true,
			networkState: NetworkSessionState.InGame,
			seatStateFound: false,
			seatMode: SeatControlMode.HumanConnected));
	}

	private static void LocalSeatBoardControlsBlockUntilHumanControlIsRestored()
	{
		False(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: false,
			isNetworkHost: false,
			networkState: NetworkSessionState.Idle,
			seatStateFound: false,
			seatMode: SeatControlMode.AiProxyReclaimable));

		False(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			networkState: NetworkSessionState.InGame,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanReclaimPending));

		False(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanConnected));

		True(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.Reconnecting,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanConnected));

		True(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			seatStateFound: true,
			seatMode: SeatControlMode.HumanReclaimPending));

		True(LocalSeatControlRules.ShouldBlockBoardControlInput(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			seatStateFound: false,
			seatMode: SeatControlMode.HumanConnected));
	}

	private static void NetworkSeatControlAcceptsRemoteActionsOnlyForConnectedHumans()
	{
		True(NetworkSeatControlRules.CanAcceptRemotePlayerAction(SeatControlMode.HumanConnected));
		False(NetworkSeatControlRules.CanAcceptRemotePlayerAction(SeatControlMode.AiProxyReclaimable));
		False(NetworkSeatControlRules.CanAcceptRemotePlayerAction(SeatControlMode.HumanReclaimPending));
	}

	private static void NetworkSeatControlRevealsPendingTargetHandOnlyForConnectedHumans()
	{
		True(NetworkSeatControlRules.CanRevealPendingTargetHand(SeatControlMode.HumanConnected));
		False(NetworkSeatControlRules.CanRevealPendingTargetHand(SeatControlMode.AiProxyReclaimable));
		False(NetworkSeatControlRules.CanRevealPendingTargetHand(SeatControlMode.HumanReclaimPending));
	}

	private static void NetworkSeatControlSendsPendingSelectionRequestsOnlyWhenClientIsReady()
	{
		True(NetworkSeatControlRules.CanSendPendingSelectionRequest(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			requestInFlight: false));

		False(NetworkSeatControlRules.CanSendPendingSelectionRequest(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			requestInFlight: true));

		False(NetworkSeatControlRules.CanSendPendingSelectionRequest(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			networkState: NetworkSessionState.Reconnecting,
			requestInFlight: false));

		False(NetworkSeatControlRules.CanSendPendingSelectionRequest(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			networkState: NetworkSessionState.InGame,
			requestInFlight: false));

		False(NetworkSeatControlRules.CanSendPendingSelectionRequest(
			isNetworkMultiplayer: false,
			isNetworkHost: false,
			networkState: NetworkSessionState.InGame,
			requestInFlight: false));
	}

	private static void GameBoardExportedSettersShortCircuitRedundantWork()
	{
		string repoRoot = FindRepositoryRoot();
		string text =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Exports.cs"));

		True(text.Contains("if (normalizedPlayerCount == _playerCount)", StringComparison.Ordinal));
		True(text.Contains("if (value == _itemMode)", StringComparison.Ordinal));
		True(text.Contains("if (normalizedWinningScore == _winningScore)", StringComparison.Ordinal));
	}

	private static void DecorativeUiArtAvoidsUnconditionalPerFrameRedraws()
	{
		string repoRoot = FindRepositoryRoot();

		string oceanMenuText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "OceanMenuButtonFrame.cs"));
		False(Regex.IsMatch(
			oceanMenuText,
			@"_glow = Mathf\.Lerp\(_glow, target, .*?\);\s*QueueRedraw\(\);",
			RegexOptions.Singleline));

		string oceanDialogText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "OceanDialogPanelArt.cs"));
		True(oceanDialogText.Contains("SparkRedrawInterval", StringComparison.Ordinal));
		True(oceanDialogText.Contains("if (!IsVisibleInTree())", StringComparison.Ordinal));
		False(Regex.IsMatch(
			oceanDialogText,
			@"public override void _Process\(double delta\)\s*\{\s*_time \+= delta;\s*QueueRedraw\(\);\s*\}",
			RegexOptions.Singleline));

		string playerCountFrameText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "PlayerCountDialogFrameArt.cs"));
		False(Regex.IsMatch(
			playerCountFrameText,
			@"_glow = Mathf\.Lerp\(_glow, targetGlow, .*?\);\s*QueueRedraw\(\);",
			RegexOptions.Singleline));

		string mainMenuPanelText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "MainMenuPlayerCountDialogPanelArt.cs"));
		True(mainMenuPanelText.Contains("IdleSparkRedrawInterval", StringComparison.Ordinal));
		True(mainMenuPanelText.Contains("ActiveGlowRedrawInterval", StringComparison.Ordinal));
		True(mainMenuPanelText.Contains("if (!IsVisibleInTree())", StringComparison.Ordinal));
		False(Regex.IsMatch(
			mainMenuPanelText,
			@"_startButtonGlow = Mathf\.Lerp\(_startButtonGlow, targetGlow, .*?\);\s*QueueRedraw\(\);",
			RegexOptions.Singleline));
	}

	private static void DynamicBackgroundLayersGateImageModeRedrawToFallbackNeeds()
	{
		string repoRoot = FindRepositoryRoot();

		string underwaterText =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "UnderwaterAmbientLayer.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "UnderwaterAmbientLayer.ProceduralDraw.cs"));
		True(underwaterText.Contains("ShouldQueueRedrawForImageFallback()", StringComparison.Ordinal));
		True(underwaterText.Contains("if (ShouldQueueRedrawForImageFallback())", StringComparison.Ordinal));

		string adventureMapText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "AdventureMapDynamicLayer.cs"));
		True(adventureMapText.Contains("ShouldQueueRedrawForImageFallback()", StringComparison.Ordinal));
		True(adventureMapText.Contains("if (ShouldQueueRedrawForImageFallback())", StringComparison.Ordinal));
	}

	private static void MainMenuOceanDynamicsKeepsProceduralFallbackPathsInImageMode()
	{
		string repoRoot = FindRepositoryRoot();
		string text =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "MainMenuOceanDynamics.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "MainMenuOceanDynamics.ProceduralDraw.cs"));

		True(text.Contains("ShouldQueueRedrawInImageMode()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralSunbeams()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralSurfaceWake()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralCurrentRibbons()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralCausticRibbons()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralGlints()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawBubbles()", StringComparison.Ordinal));
		False(text.Contains("if (_activeRenderMode == OceanRenderMode.Procedural)", StringComparison.Ordinal));
	}

	private static void GameBoardDefersModeSpecificResourceLoadsUntilNeeded()
	{
		string repoRoot = FindRepositoryRoot();
		string gameBoardText =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Exports.cs"));
		string assetsText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Assets.cs"));

		True(gameBoardText.Contains("EnsureAdventureModeResourcesLoaded();", StringComparison.Ordinal));
		True(gameBoardText.Contains("EnsureItemModeResourcesLoaded();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadMantaRayTideShiftCycloneFxTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadLeafySeadragonBladeProjectileTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadBossKRoyalSeizureLanceTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadAdventureDamageEffectTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadItemCardTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadAttachmentCardOverlayTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadQuestionCardTransformTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadBombPairDestroyEffectTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadLensRevealWispTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadMagnetCountdownFxTextures();", StringComparison.Ordinal));
		True(assetsText.Contains("private void EnsureAdventureModeResourcesLoaded()", StringComparison.Ordinal));
		True(assetsText.Contains("private void EnsureItemModeResourcesLoaded()", StringComparison.Ordinal));
	}

	private static void GameBoardOptionalFxResourcesAreNoLongerStaticallyPreloaded()
	{
		string text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "GameBoardScene.cs"));

		False(text.Contains("private static readonly Texture2D[] ScoreFillEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] EnemySpecialAbilityAvatarFxFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] MantaRayTideShiftCycloneFxFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] DefaultAdventureDamageEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] CatAdventureDamageEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] LeafySeadragonBladeFlightFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] LeafySeadragonBladeImpactFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] BossKRoyalSeizureLanceFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] QuestionCardTransformFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] SpecialCardTriggerEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] SpecialCardTriggerActivationFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] BombPairDestroyEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] LensRevealWispFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] MagnetCountdownFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] TableShuffleVortexFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] MagnetAttachmentApplyRingFrames", StringComparison.Ordinal));
	}

	private static void GameBoardWarmupIncludesOptionalFxResources()
	{
		string repoRoot = FindRepositoryRoot();
		string warmupText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.ResourceWarmup.cs"));
		string assetsText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Assets.cs"));

		True(warmupText.Contains("WarmupOptionalFxResourcesAsync", StringComparison.Ordinal));
		True(warmupText.Contains("WarmupOptionalFx:CoreGameplay", StringComparison.Ordinal));
		True(warmupText.Contains("WarmupOptionalFx:AdventureMode", StringComparison.Ordinal));
		True(warmupText.Contains("WarmupOptionalFx:ItemMode", StringComparison.Ordinal));
		True(assetsText.Contains("private void EnsureCommonGameplayFxResourcesLoaded()", StringComparison.Ordinal));
	}

	private static void GameBoardIdlePerformanceTraceSmokeExists()
	{
		string repoRoot = FindRepositoryRoot();
		string smokeScriptPath = Path.Combine(repoRoot, "tests", "GameBoardIdlePerformanceTraceSmoke.gd");
		string runnerPath = Path.Combine(repoRoot, "tests", "run_game_board_idle_performance_trace_smoke.sh");

		True(File.Exists(smokeScriptPath));
		True(File.Exists(runnerPath));

		string smokeText = File.ReadAllText(smokeScriptPath);
		string runnerText = File.ReadAllText(runnerPath);

		True(smokeText.Contains("IDLE_WINDOW_BEGIN", StringComparison.Ordinal));
		True(smokeText.Contains("IDLE_WINDOW_END", StringComparison.Ordinal));
		True(smokeText.Contains("debug_performance_trace", StringComparison.Ordinal));
		True(runnerText.Contains("RenderPlayerHands", StringComparison.Ordinal));
		True(runnerText.Contains("RenderTableCards", StringComparison.Ordinal));
		True(runnerText.Contains("IDLE_WINDOW_BEGIN", StringComparison.Ordinal));
		True(runnerText.Contains("IDLE_WINDOW_END", StringComparison.Ordinal));
	}

	private static void ResourceExportAuditScriptChecksTrackedAssetsAndExportConfig()
	{
		string scriptPath = Path.Combine(FindRepositoryRoot(), "tools", "audit_export_resources.sh");
		True(File.Exists(scriptPath));
		string text = File.ReadAllText(scriptPath);

		True(text.Contains("git ls-files", StringComparison.Ordinal));
		True(text.Contains("generated-images", StringComparison.Ordinal));
		True(text.Contains("outputs", StringComparison.Ordinal));
		True(text.Contains("debug-sse.log", StringComparison.Ordinal));
		True(text.Contains("project.godot", StringComparison.Ordinal));
		True(text.Contains("export_presets.cfg", StringComparison.Ordinal));
		True(text.Contains("autoload", StringComparison.Ordinal));
		True(text.Contains("exclude_filter", StringComparison.Ordinal));
		True(text.Contains("raw", StringComparison.Ordinal));
		True(text.Contains("transparent-processing-meta.json", StringComparison.Ordinal));
		True(text.Contains("upgrade-popup-assets", StringComparison.Ordinal));
		True(text.Contains("source", StringComparison.Ordinal));
		True(text.Contains("Category Summary", StringComparison.Ordinal));
		True(text.Contains("upgrade_popup_processed", StringComparison.Ordinal));
		True(text.Contains("board_top_preview", StringComparison.Ordinal));
		False(text.Contains("git rm", StringComparison.Ordinal));
		False(text.Contains("rm -rf", StringComparison.Ordinal));
	}

	private static void ResourceExportAuditDocDescribesReadOnlyCommand()
	{
		string docPath = Path.Combine(FindRepositoryRoot(), "docs", "RESOURCE_EXPORT_AUDIT.md");
		True(File.Exists(docPath));
		string text = File.ReadAllText(docPath);

		True(text.Contains("bash tools/audit_export_resources.sh", StringComparison.Ordinal));
		True(text.Contains("只报告", StringComparison.Ordinal));
		True(text.Contains("不自动删除", StringComparison.Ordinal));
		True(text.Contains("processed", StringComparison.Ordinal));
		True(text.Contains("raw", StringComparison.Ordinal));
	}

	private static void ExportPresetExcludesUpgradePopupSourceOnlyAssets()
	{
		string text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "export_presets.cfg"));

		True(text.Contains("generated-images/upgrade-popup-assets/raw/**", StringComparison.Ordinal));
		True(text.Contains("generated-images/upgrade-popup-assets/*.png", StringComparison.Ordinal));
		True(text.Contains("generated-images/upgrade-popup-assets/*.png.import", StringComparison.Ordinal));
		True(text.Contains("generated-images/upgrade-popup-assets/transparent-processing-meta.json", StringComparison.Ordinal));
	}

	private static void GitignoreCoversUpgradePopupSourceOnlyAssets()
	{
		string text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".gitignore"));

		True(text.Contains("/generated-images/upgrade-popup-assets/*/raw/", StringComparison.Ordinal));
		True(text.Contains("/generated-images/upgrade-popup-assets/transparent-processing-meta.json", StringComparison.Ordinal));
		True(text.Contains("/generated-images/upgrade-popup-assets/*.png", StringComparison.Ordinal));
		True(text.Contains("/generated-images/upgrade-popup-assets/*.png.import", StringComparison.Ordinal));
	}

	private static void TrackedGeneratedAssetInventoryDocSummarizesCurrentCategories()
	{
		string docPath = Path.Combine(FindRepositoryRoot(), "docs", "TRACKED_GENERATED_ASSET_INVENTORY.md");
		True(File.Exists(docPath));
		string text = File.ReadAllText(docPath);

		True(text.Contains("upgrade_popup_processed", StringComparison.Ordinal));
		True(text.Contains("upgrade_popup_raw", StringComparison.Ordinal));
		True(text.Contains("board_top_preview", StringComparison.Ordinal));
		True(text.Contains("generated_misc", StringComparison.Ordinal));
		True(text.Contains("debug_log", StringComparison.Ordinal));
		True(text.Contains("保留跟踪", StringComparison.Ordinal));
		True(text.Contains("候选取消跟踪", StringComparison.Ordinal));
	}

	private static void TrackedGeneratedAssetInventoryDocIncludesSuggestedManualCleanupBatches()
	{
		string docPath = Path.Combine(FindRepositoryRoot(), "docs", "TRACKED_GENERATED_ASSET_INVENTORY.md");
		string text = File.ReadAllText(docPath);

		True(text.Contains("Suggested Manual Cleanup Batches", StringComparison.Ordinal));
		True(text.Contains("Batch 1", StringComparison.Ordinal));
		True(text.Contains("board-top-extension-preview", StringComparison.Ordinal));
		True(text.Contains("git rm --cached --", StringComparison.Ordinal));
		True(text.Contains("review first", StringComparison.OrdinalIgnoreCase));
	}

	private static void NetworkManagerSerializationHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "NetworkManager.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "NetworkManager.Serialization.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(partialText.Contains("private static Godot.Collections.Dictionary ToDictionary(NetworkSessionConfig config)", StringComparison.Ordinal));
		True(partialText.Contains("private static NetworkSessionConfig ParseConfig(Godot.Collections.Dictionary dictionary)", StringComparison.Ordinal));
		True(partialText.Contains("private static NetworkPlayerPublicState ParsePlayer(Godot.Collections.Dictionary dictionary)", StringComparison.Ordinal));
		False(mainText.Contains("private static Godot.Collections.Dictionary ToDictionary(NetworkSessionConfig config)", StringComparison.Ordinal));
		False(mainText.Contains("private static NetworkPlayerPublicState ParsePlayer(Godot.Collections.Dictionary dictionary)", StringComparison.Ordinal));
		False(mainText.Contains("private static TEnum ReadEnum<TEnum>", StringComparison.Ordinal));
	}

	private static void NetworkManagerLanDiscoveryHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "NetworkManager.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "NetworkManager.LanDiscovery.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(partialText.Contains("private void StartLanDiscoveryResponder()", StringComparison.Ordinal));
		True(partialText.Contains("private void PollLanDiscoveryResponder(double now)", StringComparison.Ordinal));
		True(partialText.Contains("private void PollLanDiscoveryBrowser(double now)", StringComparison.Ordinal));
		True(partialText.Contains("private void UpsertRoom(NetworkRoomInfo room)", StringComparison.Ordinal));
		False(mainText.Contains("private void StartLanDiscoveryResponder()", StringComparison.Ordinal));
		False(mainText.Contains("private void PollLanDiscoveryResponder(double now)", StringComparison.Ordinal));
		False(mainText.Contains("private void PollLanDiscoveryBrowser(double now)", StringComparison.Ordinal));
		False(mainText.Contains("private void UpsertRoom(NetworkRoomInfo room)", StringComparison.Ordinal));
	}

	private static void NetworkManagerSteamHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "NetworkManager.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "NetworkManager.Steam.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(partialText.Contains("public void HostSteam(", StringComparison.Ordinal));
		True(partialText.Contains("private void TryConnectSteamSignals()", StringComparison.Ordinal));
		True(partialText.Contains("private void OnSteamLobbyJoined(", StringComparison.Ordinal));
		True(partialText.Contains("private bool TryReadSteamRoom(", StringComparison.Ordinal));
		True(partialText.Contains("private GodotObject? GetSteamSingleton()", StringComparison.Ordinal));
		True(partialText.Contains("private void RefreshSteamLobbyAdvertisedState()", StringComparison.Ordinal));
		False(mainText.Contains("public void HostSteam(", StringComparison.Ordinal));
		False(mainText.Contains("private void TryConnectSteamSignals()", StringComparison.Ordinal));
		False(mainText.Contains("private void OnSteamLobbyJoined(", StringComparison.Ordinal));
		False(mainText.Contains("private bool TryReadSteamRoom(", StringComparison.Ordinal));
		False(mainText.Contains("private GodotObject? GetSteamSingleton()", StringComparison.Ordinal));
		False(mainText.Contains("private void RefreshSteamLobbyAdvertisedState()", StringComparison.Ordinal));
	}

	private static void NetworkManagerReconnectHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "NetworkManager.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "NetworkManager.Reconnect.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(partialText.Contains("private void PollReconnect(double now)", StringComparison.Ordinal));
		True(partialText.Contains("private void TryMarkSeatDisconnected(int disconnectedPeerId)", StringComparison.Ordinal));
		True(partialText.Contains("private bool CanAttemptReconnectClaim()", StringComparison.Ordinal));
		True(partialText.Contains("private void TryStartReconnectAttempt(double now)", StringComparison.Ordinal));
		True(partialText.Contains("public void NotifyReconnectSnapshotApplied(string matchId)", StringComparison.Ordinal));
		False(mainText.Contains("private void PollReconnect(double now)", StringComparison.Ordinal));
		False(mainText.Contains("private void TryMarkSeatDisconnected(int disconnectedPeerId)", StringComparison.Ordinal));
		False(mainText.Contains("private bool CanAttemptReconnectClaim()", StringComparison.Ordinal));
		False(mainText.Contains("private void TryStartReconnectAttempt(double now)", StringComparison.Ordinal));
		False(mainText.Contains("public void NotifyReconnectSnapshotApplied(string matchId)", StringComparison.Ordinal));
	}

	private static void NetworkManagerLobbyHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "NetworkManager.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "NetworkManager.Lobby.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(partialText.Contains("public void SetLocalReady(bool isReady)", StringComparison.Ordinal));
		True(partialText.Contains("public void StartMatch()", StringComparison.Ordinal));
		True(partialText.Contains("public void LeaveSession()", StringComparison.Ordinal));
		True(partialText.Contains("private void RpcRegisterPlayer(", StringComparison.Ordinal));
		True(partialText.Contains("private void BroadcastPlayerList()", StringComparison.Ordinal));
		True(partialText.Contains("private void MarkClientSceneLoaded(", StringComparison.Ordinal));
		True(partialText.Contains("private void PollLoadHandshake(double now)", StringComparison.Ordinal));
		False(mainText.Contains("public void SetLocalReady(bool isReady)", StringComparison.Ordinal));
		False(mainText.Contains("public void StartMatch()", StringComparison.Ordinal));
		False(mainText.Contains("public void LeaveSession()", StringComparison.Ordinal));
		False(mainText.Contains("private void RpcRegisterPlayer(", StringComparison.Ordinal));
		False(mainText.Contains("private void BroadcastPlayerList()", StringComparison.Ordinal));
		False(mainText.Contains("private void MarkClientSceneLoaded(", StringComparison.Ordinal));
		False(mainText.Contains("private void PollLoadHandshake(double now)", StringComparison.Ordinal));
	}

	private static void NetworkManagerBoardSyncHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "NetworkManager.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "NetworkManager.BoardSync.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(partialText.Contains("internal void BroadcastCardPlayedEventFromHost(", StringComparison.Ordinal));
		True(partialText.Contains("public void SendBoardSnapshotsFromHost(GameBoardScene gameBoard, int snapshotId)", StringComparison.Ordinal));
		True(partialText.Contains("private void RpcRequestPlayHandCard(int handIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		True(partialText.Contains("private void RpcReconnectPlayer(string matchId, string reconnectToken, string stableUserId)", StringComparison.Ordinal));
		True(partialText.Contains("private void RpcBoardSnapshot(Godot.Collections.Dictionary snapshot)", StringComparison.Ordinal));
		False(mainText.Contains("internal void BroadcastCardPlayedEventFromHost(", StringComparison.Ordinal));
		False(mainText.Contains("public void SendBoardSnapshotsFromHost(GameBoardScene gameBoard, int snapshotId)", StringComparison.Ordinal));
		False(mainText.Contains("private void RpcRequestPlayHandCard(int handIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		False(mainText.Contains("private void RpcReconnectPlayer(string matchId, string reconnectToken, string stableUserId)", StringComparison.Ordinal));
		False(mainText.Contains("private void RpcBoardSnapshot(Godot.Collections.Dictionary snapshot)", StringComparison.Ordinal));
	}

	private static void GameBoardNetworkSnapshotHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.Network.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkSnapshot.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class GameBoardScene", StringComparison.Ordinal));
		True(partialText.Contains("internal Godot.Collections.Dictionary BuildNetworkBoardSnapshotForSeat", StringComparison.Ordinal));
		True(partialText.Contains("internal void ApplyNetworkBoardSnapshot(Godot.Collections.Dictionary snapshot)", StringComparison.Ordinal));
		True(partialText.Contains("private void ApplyNetworkBoardSnapshotNow(", StringComparison.Ordinal));
		True(partialText.Contains("private void ApplyHandsFromSnapshot(", StringComparison.Ordinal));
		True(partialText.Contains("private void ApplyItemHandsFromSnapshot(", StringComparison.Ordinal));
		True(partialText.Contains("private void ApplyTableCardsFromSnapshot(", StringComparison.Ordinal));
		True(partialText.Contains("private void ApplyPendingSelectionFromSnapshot(", StringComparison.Ordinal));
		True(partialText.Contains("private void ApplySeatControlStatesFromSnapshot(", StringComparison.Ordinal));
		False(mainText.Contains("internal Godot.Collections.Dictionary BuildNetworkBoardSnapshotForSeat", StringComparison.Ordinal));
		False(mainText.Contains("private void ApplyNetworkBoardSnapshotNow(", StringComparison.Ordinal));
		False(mainText.Contains("private void ApplyHandsFromSnapshot(", StringComparison.Ordinal));
		False(mainText.Contains("private void ApplySeatControlStatesFromSnapshot(", StringComparison.Ordinal));
	}

	private static void GameBoardNetworkRequestEntryPointsAvoidAsyncVoid()
	{
		string repoRoot = FindRepositoryRoot();
		string path = Path.Combine(repoRoot, "scripts", "GameBoardScene.Network.cs");
		string text = File.ReadAllText(path);

		False(text.Contains("internal async void RequestNetworkItemCardPlay(", StringComparison.Ordinal));
		True(text.Contains("internal void RequestNetworkItemCardPlay(", StringComparison.Ordinal));
		True(text.Contains("private async Task RequestNetworkItemCardPlayAsync(", StringComparison.Ordinal));
		False(text.Contains("internal async void RequestNetworkHandCardPlay(", StringComparison.Ordinal));
		True(text.Contains("internal void RequestNetworkHandCardPlay(", StringComparison.Ordinal));
		True(text.Contains("private async Task RequestNetworkHandCardPlayAsync(", StringComparison.Ordinal));
	}

	private static void TargetItemSelectionCompletionAvoidsAsyncVoid()
	{
		string repoRoot = FindRepositoryRoot();
		string path = Path.Combine(repoRoot, "scripts", "GameBoardScene.TargetItemSelection.cs");
		string text = File.ReadAllText(path);

		False(text.Contains("private async void CompletePendingItemSelection(", StringComparison.Ordinal));
		True(text.Contains("private void CompletePendingItemSelection(", StringComparison.Ordinal));
		True(text.Contains("private async Task CompletePendingItemSelectionAsync(", StringComparison.Ordinal));
	}

	private static void NetworkManagerSceneLoadAckValidatesSenderBeforeMarkingLoaded()
	{
		string repoRoot = FindRepositoryRoot();
		string path = Path.Combine(repoRoot, "scripts", "NetworkManager.Lobby.cs");
		string text = File.ReadAllText(path);

		True(text.Contains("int senderPeerId = Multiplayer.GetRemoteSenderId();", StringComparison.Ordinal));
		True(text.Contains("CanAcceptSceneLoadAck(_players, senderPeerId, seatIndex, out int senderSeatIndex)", StringComparison.Ordinal));
		True(text.Contains("GD.PushWarning(", StringComparison.Ordinal));
	}

	private static void GameBoardNetworkVisualQueueHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.Network.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkVisualQueue.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class GameBoardScene", StringComparison.Ordinal));
		True(partialText.Contains("internal void QueueNetworkCardPlayedEvent(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		True(partialText.Contains("private async Task ProcessNetworkVisualQueueAsync()", StringComparison.Ordinal));
		True(partialText.Contains("private async Task PlayNetworkCardPlayedEventAsync(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		True(partialText.Contains("private async Task PlayNetworkItemCardVisualEventAsync(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		True(partialText.Contains("private async Task PlayNetworkCaptureRevealVisualEventAsync(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		False(mainText.Contains("internal void QueueNetworkCardPlayedEvent(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		False(mainText.Contains("private async Task ProcessNetworkVisualQueueAsync()", StringComparison.Ordinal));
		False(mainText.Contains("private async Task PlayNetworkCardPlayedEventAsync(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		False(mainText.Contains("private async Task PlayNetworkCaptureRevealVisualEventAsync(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
	}

	private static void GameBoardNetworkPredictionHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.Network.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkPrediction.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class GameBoardScene", StringComparison.Ordinal));
		True(partialText.Contains("private void ResetNetworkPredictedHandVisualState()", StringComparison.Ordinal));
		True(partialText.Contains("private bool BeginPredictedNetworkHandCardPlay(int handIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		True(partialText.Contains("private bool BeginPredictedNetworkItemCardPlay(int itemHandIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		True(partialText.Contains("private void RollbackPredictedNetworkHandCardPlay()", StringComparison.Ordinal));
		True(partialText.Contains("private void RollbackPredictedNetworkItemCardPlay()", StringComparison.Ordinal));
		True(partialText.Contains("private async Task BeginPredictedNetworkMagnetAttachmentSelectionAsync(int handIndex)", StringComparison.Ordinal));
		False(mainText.Contains("private void ResetNetworkPredictedHandVisualState()", StringComparison.Ordinal));
		False(mainText.Contains("private bool BeginPredictedNetworkHandCardPlay(int handIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		False(mainText.Contains("private bool BeginPredictedNetworkItemCardPlay(int itemHandIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		False(mainText.Contains("private void RollbackPredictedNetworkItemCardPlay()", StringComparison.Ordinal));
	}

	private static void GameBoardNetworkAuthorityHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.Network.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkAuthority.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class GameBoardScene", StringComparison.Ordinal));
		True(partialText.Contains("internal void ConfirmNetworkHostItemCardPlay(int itemHandIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		True(partialText.Contains("internal bool TryStartNetworkHandCardPlayFromHost(", StringComparison.Ordinal));
		True(partialText.Contains("internal bool TryStartNetworkItemCardPlayFromHost(", StringComparison.Ordinal));
		True(partialText.Contains("internal bool TryStartNetworkPendingItemSelectionFromHost(", StringComparison.Ordinal));
		True(partialText.Contains("private async Task PlayNetworkHostHandCardAndBroadcastAsync(", StringComparison.Ordinal));
		True(partialText.Contains("private async Task PlayNetworkHostItemCardAndBroadcastAsync(", StringComparison.Ordinal));
		True(partialText.Contains("internal int CreateNextNetworkSnapshotId()", StringComparison.Ordinal));
		True(partialText.Contains("internal void SendBoardSnapshotIfHost()", StringComparison.Ordinal));
		False(mainText.Contains("internal void ConfirmNetworkHostItemCardPlay(int itemHandIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		False(mainText.Contains("internal bool TryStartNetworkHandCardPlayFromHost(", StringComparison.Ordinal));
		False(mainText.Contains("private async Task PlayNetworkHostItemCardAndBroadcastAsync(", StringComparison.Ordinal));
		False(mainText.Contains("internal void SendBoardSnapshotIfHost()", StringComparison.Ordinal));
	}

	private static void GameBoardNetworkAiProxyHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.Network.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkAiProxy.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class GameBoardScene", StringComparison.Ordinal));
		True(partialText.Contains("internal bool IsNetworkMatchClaimable()", StringComparison.Ordinal));
		True(partialText.Contains("private bool IsLocalSeatHumanConnected()", StringComparison.Ordinal));
		True(partialText.Contains("internal void BeginNetworkAiProxyTurnIfNeeded()", StringComparison.Ordinal));
		True(partialText.Contains("private async Task ResolveNetworkAiProxyTurnsAsync(int turnSequenceGeneration)", StringComparison.Ordinal));
		True(partialText.Contains("private async Task<bool> RecoverNetworkAiProxyTurnAfterFailureAsync(int failedSeatIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		True(partialText.Contains("private async Task<bool> TryResolvePendingNetworkAiProxySelectionAsync(int turnSequenceGeneration)", StringComparison.Ordinal));
		True(partialText.Contains("private async Task<PlayCardResult> PlayNetworkAiProxyHandEntryAsync(int playerIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		False(mainText.Contains("internal void BeginNetworkAiProxyTurnIfNeeded()", StringComparison.Ordinal));
		False(mainText.Contains("private async Task ResolveNetworkAiProxyTurnsAsync(int turnSequenceGeneration)", StringComparison.Ordinal));
		False(mainText.Contains("private async Task<PlayCardResult> PlayNetworkAiProxyHandEntryAsync(int playerIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		False(mainText.Contains("private int PickPendingItemTargetPlayerForNetworkAiProxy()", StringComparison.Ordinal));
	}

	private static void GameBoardNetworkCardHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.Network.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkCards.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class GameBoardScene", StringComparison.Ordinal));
		True(partialText.Contains("private string ToNetworkCardCode(PlayingCard card)", StringComparison.Ordinal));
		True(partialText.Contains("private PlayingCard CreateNetworkPlayingCard(string cardCode)", StringComparison.Ordinal));
		True(partialText.Contains("private ItemCard CreateNetworkVisualItemCard(string itemCardId)", StringComparison.Ordinal));
		True(partialText.Contains("private void EnsureNetworkCardCache()", StringComparison.Ordinal));
		True(partialText.Contains("private bool TryGetNetworkPlayerName(int playerIndex, out string playerName)", StringComparison.Ordinal));
		True(partialText.Contains("private static string[] SplitNetworkCsv(string csv)", StringComparison.Ordinal));
		False(mainText.Contains("private string ToNetworkCardCode(PlayingCard card)", StringComparison.Ordinal));
		False(mainText.Contains("private PlayingCard CreateNetworkPlayingCard(string cardCode)", StringComparison.Ordinal));
		False(mainText.Contains("private ItemCard CreateNetworkVisualItemCard(string itemCardId)", StringComparison.Ordinal));
		False(mainText.Contains("private void EnsureNetworkCardCache()", StringComparison.Ordinal));
		False(mainText.Contains("private bool TryGetNetworkPlayerName(int playerIndex, out string playerName)", StringComparison.Ordinal));
	}

	private static void NetworkAiPlayableTurnActionIgnoresBlockedItemOnlyTurns()
	{
		True(NetworkAiFallbackRules.HasPlayableAiTurnAction(
			handCardCount: 1,
			playableItemCount: 0));

		True(NetworkAiFallbackRules.HasPlayableAiTurnAction(
			handCardCount: 0,
			playableItemCount: 1));

		False(NetworkAiFallbackRules.HasPlayableAiTurnAction(
			handCardCount: 0,
			playableItemCount: 0));
	}

	private static void NetworkAiProxyCaptureProfileMatchesWeakestAdventureAi()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("K", 12),
			TableCard("Q", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = NetworkAiFallbackRules.TryPickCaptureHandIndexForNetworkAiProxy(
			tableCards,
			handCards,
			DefaultScoringContext,
			decisionRollPercent: 30,
			randomCaptureOrdinal: 1,
			out int handIndex);

		True(picked);
		Equal(1, handIndex);
	}

	private static void NetworkAiResumesProxyTurnOnlyAfterAuthorityActionSettles()
	{
		True(NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			isGameOver: false,
			isProcessingCardPlay: false));

		False(NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			isGameOver: false,
			isProcessingCardPlay: true));

		False(NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			isGameOver: false,
			isProcessingCardPlay: false));

		False(NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer: false,
			isNetworkHost: true,
			isGameOver: false,
			isProcessingCardPlay: false));

		False(NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			isGameOver: true,
			isProcessingCardPlay: false));
	}

	private static void NetworkAiStartsProxyTurnForAiOwnedPendingSelection()
	{
		True(NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			hasAiOwnedPendingSelection: true));

		False(NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			hasAiOwnedPendingSelection: false));

		False(NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			hasAiOwnedPendingSelection: true));

		False(NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
			isNetworkMultiplayer: false,
			isNetworkHost: true,
			hasAiOwnedPendingSelection: true));
	}

	private static void NetworkAiRecoversOnlyForHostSideUnhandledFailures()
	{
		True(NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			isGameOver: false));

		False(NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			isNetworkMultiplayer: true,
			isNetworkHost: false,
			isGameOver: false));

		False(NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			isNetworkMultiplayer: false,
			isNetworkHost: true,
			isGameOver: false));

		False(NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			isNetworkMultiplayer: true,
			isNetworkHost: true,
			isGameOver: true));
	}

	private static void NetworkAiBlockedItemSkipIsScopedToExactTurnAndItem()
	{
		NetworkAiBlockedItemState blocked = new(
			OwnerSeatIndex: 2,
			ItemCardId: "queen");

		True(NetworkAiFallbackRules.ShouldSkipBlockedItem(
			blocked,
			ownerSeatIndex: 2,
			itemCardId: "queen"));

		False(NetworkAiFallbackRules.ShouldSkipBlockedItem(
			blocked,
			ownerSeatIndex: 1,
			itemCardId: "queen"));

		False(NetworkAiFallbackRules.ShouldSkipBlockedItem(
			blocked,
			ownerSeatIndex: 2,
			itemCardId: "king"));

		True(NetworkAiFallbackRules.ShouldClearBlockedItemForTurnChange(previousTurnPlayerIndex: 2, nextTurnPlayerIndex: 3));
		False(NetworkAiFallbackRules.ShouldClearBlockedItemForTurnChange(previousTurnPlayerIndex: 2, nextTurnPlayerIndex: 2));
	}

	private static void NetworkAiPendingChoiceFallbackPicksOnlyLegalCandidates()
	{
		Equal(1, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 4,
			isLegalCandidate: candidateIndex => candidateIndex is 1 or 3,
			randomOrdinal: 0));
		Equal(3, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 4,
			isLegalCandidate: candidateIndex => candidateIndex is 1 or 3,
			randomOrdinal: 1));
		Equal(1, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 4,
			isLegalCandidate: candidateIndex => candidateIndex is 1 or 3,
			randomOrdinal: 2));
		Equal(4, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 5,
			isLegalCandidate: candidateIndex => candidateIndex is 0 or 2 or 4,
			randomOrdinal: 5));
		Equal(2, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 6,
			isLegalCandidate: candidateIndex => candidateIndex >= 2 && candidateIndex <= 4,
			randomOrdinal: 0));
	}

	private static void NetworkAiPendingChoiceFallbackFailsWithoutLegalCandidates()
	{
		Equal(-1, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 0,
			isLegalCandidate: _ => true,
			randomOrdinal: 0));
		Equal(-1, NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount: 4,
			isLegalCandidate: _ => false,
			randomOrdinal: 17));
	}

	private static void SeatControlDisconnectTransitionsToAiReclaimable()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanConnected,
			PeerId: 20,
			StableUserId: "stable-20",
			ReconnectToken: "token-1");
		ReconnectClaimRecord claim = SeatControlRules.CreateInitialClaimRecord("match-1", state);

		True(SeatControlRules.TryMarkDisconnected(state, 20, out SeatControlState next, out ReconnectClaimRecord? updatedClaim, claim));
		Equal(SeatControlMode.AiProxyReclaimable, next.Mode);
		Equal(0, next.PeerId);
		True(updatedClaim.HasValue);
		ReconnectClaimRecord disconnectClaim = updatedClaim.GetValueOrDefault();
		Equal(ReconnectClaimState.Unclaimed, disconnectClaim.ClaimState);
		Equal(0, disconnectClaim.ClaimedPeerId);
	}

	private static void SeatControlReclaimPendingDisconnectFallsBackToAiReclaimable()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 33,
			StableUserId: "stable-20",
			ReconnectToken: "token-1");
		ReconnectClaimRecord claim = new(
			"match-1",
			1,
			"token-1",
			"stable-20",
			33,
			ReconnectClaimState.Claimed);

		True(SeatControlRules.TryMarkDisconnected(state, 33, out SeatControlState next, out ReconnectClaimRecord? updatedClaim, claim));
		Equal(SeatControlMode.AiProxyReclaimable, next.Mode);
		Equal(0, next.PeerId);
		True(updatedClaim.HasValue);
		ReconnectClaimRecord disconnectClaim = updatedClaim.GetValueOrDefault();
		Equal(ReconnectClaimState.Unclaimed, disconnectClaim.ClaimState);
		Equal(0, disconnectClaim.ClaimedPeerId);
	}

	private static void SeatControlReconnectClaimIsIdempotentForSamePeer()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.AiProxyReclaimable,
			PeerId: 0,
			StableUserId: "stable-20",
			ReconnectToken: "token-1");
		ReconnectClaimRecord claim = SeatControlRules.CreateInitialClaimRecord("match-1", state);

		ReconnectClaimResult first = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			state,
			claim,
			"match-1",
			"token-1",
			"stable-20",
			senderPeerId: 33);
		True(first.Accepted);
		True(first.StateChanged);
		True(first.ShouldSendSnapshot);
		Equal(SeatControlMode.HumanReclaimPending, first.SeatControlState.Mode);
		Equal(33, first.SeatControlState.PeerId);

		ReconnectClaimResult second = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			first.SeatControlState,
			first.ClaimRecord,
			"match-1",
			"token-1",
			"stable-20",
			senderPeerId: 33);
		True(second.Accepted);
		False(second.StateChanged);
		False(second.ShouldSendSnapshot);
		Equal(SeatControlMode.HumanReclaimPending, second.SeatControlState.Mode);
	}

	private static void SeatControlReconnectClaimRejectsConflictingPeer()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 33,
			StableUserId: "stable-20",
			ReconnectToken: "token-1");
		ReconnectClaimRecord claim = new(
			"match-1",
			1,
			"token-1",
			"stable-20",
			33,
			ReconnectClaimState.Claimed);

		ReconnectClaimResult result = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			state,
			claim,
			"match-1",
			"token-1",
			"stable-20",
			senderPeerId: 44);
		False(result.Accepted);
		Equal(ReconnectClaimRejectReason.AlreadyClaimedByDifferentPeer, result.RejectReason);
	}

	private static void SeatControlPromotesReclaimedSeatAtTurnStart()
	{
		SeatControlState state = new(
			SeatIndex: 2,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 55,
			StableUserId: "stable-55",
			ReconnectToken: "token-2");

		True(SeatControlRules.TryPromoteAtTurnStart(state, 2, out SeatControlState next));
		Equal(SeatControlMode.HumanConnected, next.Mode);
		Equal(55, next.PeerId);
		False(SeatControlRules.TryPromoteAtTurnStart(next, 2, out _));
	}

	private static void SeatControlStaysReclaimPendingUntilOwnTurnStart()
	{
		SeatControlState state = new(
			SeatIndex: 2,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 55,
			StableUserId: "stable-55",
			ReconnectToken: "token-2");

		False(SeatControlRules.TryPromoteAtTurnStart(state, 1, out SeatControlState next));
		Equal(state, next);
	}

	private static void SeatControlAllowsReclaimAgainAfterReclaimPendingDisconnect()
	{
		SeatControlState reclaimPending = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 33,
			StableUserId: "stable-20",
			ReconnectToken: "token-1");
		ReconnectClaimRecord claimed = new(
			"match-1",
			1,
			"token-1",
			"stable-20",
			33,
			ReconnectClaimState.Claimed);

		True(SeatControlRules.TryMarkDisconnected(reclaimPending, 33, out SeatControlState disconnected, out ReconnectClaimRecord? updatedClaim, claimed));
		True(updatedClaim.HasValue);
		ReconnectClaimRecord resetClaim = updatedClaim.GetValueOrDefault();
		Equal(SeatControlMode.AiProxyReclaimable, disconnected.Mode);
		Equal(ReconnectClaimState.Unclaimed, resetClaim.ClaimState);
		Equal(0, resetClaim.ClaimedPeerId);

		ReconnectClaimResult reconnected = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			disconnected,
			resetClaim,
			"match-1",
			"token-1",
			"stable-20",
			senderPeerId: 44);
		True(reconnected.Accepted);
		True(reconnected.StateChanged);
		True(reconnected.ShouldSendSnapshot);
		Equal(SeatControlMode.HumanReclaimPending, reconnected.SeatControlState.Mode);
		Equal(44, reconnected.SeatControlState.PeerId);
		Equal(ReconnectClaimState.Claimed, reconnected.ClaimRecord.ClaimState);
		Equal(44, reconnected.ClaimRecord.ClaimedPeerId);
	}

	private static void SeatControlRejectsInvalidReconnectMetadata()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.AiProxyReclaimable,
			PeerId: 0,
			StableUserId: "stable-20",
			ReconnectToken: "token-1");
		ReconnectClaimRecord claim = SeatControlRules.CreateInitialClaimRecord("match-1", state);

		ReconnectClaimResult wrongMatch = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			state,
			claim,
			"match-2",
			"token-1",
			"stable-20",
			senderPeerId: 33);
		False(wrongMatch.Accepted);
		Equal(ReconnectClaimRejectReason.MatchMismatch, wrongMatch.RejectReason);

		ReconnectClaimResult wrongToken = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			state,
			claim,
			"match-1",
			"bad",
			"stable-20",
			senderPeerId: 33);
		False(wrongToken.Accepted);
		Equal(ReconnectClaimRejectReason.TokenMismatch, wrongToken.RejectReason);

		ReconnectClaimResult wrongUser = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: true,
			state,
			claim,
			"match-1",
			"token-1",
			"other",
			senderPeerId: 33);
		False(wrongUser.Accepted);
		Equal(ReconnectClaimRejectReason.StableUserIdMismatch, wrongUser.RejectReason);
	}

	private static void SeatControlRejectsReconnectClaimAfterMatchEnd()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.AiProxyReclaimable,
			PeerId: 0,
			StableUserId: "stable-20",
			ReconnectToken: "token-1");
		ReconnectClaimRecord claim = SeatControlRules.CreateInitialClaimRecord("match-1", state);

		ReconnectClaimResult result = SeatControlRules.ApplyReconnectClaim(
			"match-1",
			isMatchActive: false,
			state,
			claim,
			"match-1",
			"token-1",
			"stable-20",
			senderPeerId: 33);
		False(result.Accepted);
		Equal(ReconnectClaimRejectReason.MatchEnded, result.RejectReason);
	}

	private static void SeatControlSharedSnapshotRedactsReconnectToken()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 33,
			StableUserId: "stable-20",
			ReconnectToken: "token-1");

		SeatControlState shared = SeatControlRules.RedactReconnectTokenForSharedSnapshot(state);

		Equal(state.SeatIndex, shared.SeatIndex);
		Equal(state.Mode, shared.Mode);
		Equal(state.PeerId, shared.PeerId);
		Equal(state.StableUserId, shared.StableUserId);
		Equal(string.Empty, shared.ReconnectToken);
	}

	private static void AssertRejected(
		MultiplayerBoardActionRejectReason expectedReason,
		int senderSeatIndex,
		int currentTurnPlayerIndex,
		int handIndex,
		int handCount,
		int requestGeneration,
		int currentGeneration,
		bool isGameOver = false,
		bool isActionInProgress = false)
	{
		MultiplayerBoardActionValidation validation = MultiplayerBoardSyncRules.ValidatePlayHandCardRequest(
			senderSeatIndex,
			currentTurnPlayerIndex,
			handIndex,
			handCount,
			requestGeneration,
			currentGeneration,
			isGameOver,
			isActionInProgress);

		False(validation.Accepted);
		Equal(expectedReason, validation.RejectReason);
	}

	private static void SceneLoadHandshakeCompletesAndTimesOut()
	{
		var tracker = new SceneLoadHandshakeTracker("match", new[] { 0, 1 }, timeoutSeconds: 30, startedAtSeconds: 10);

		True(tracker.MarkLoaded("match", 0));
		False(tracker.IsComplete);
		False(tracker.MarkLoaded("wrong", 1));
		True(tracker.MarkLoaded("match", 1));
		True(tracker.IsComplete);
		False(tracker.HasTimedOut(50));

		var timedOut = new SceneLoadHandshakeTracker("match", new[] { 0, 1 }, timeoutSeconds: 30, startedAtSeconds: 10);
		True(timedOut.MarkLoaded("match", 0));
		True(timedOut.HasTimedOut(40));
	}

	private static void LanDiscoveryParsesValidMessagesAndDropsDirtyData()
	{
		var config = new NetworkSessionConfig(
			"session",
			"LAN 房间_1",
			string.Empty,
			"match",
			123,
			NetworkTransport.Lan,
			3,
			ItemMode: false,
			WinningScore: 100,
			LobbyVisibility.FriendsOnly);
		string payload = LanDiscoveryMessage.Build(config, "host", "Lobby");

		True(LanDiscoveryMessage.TryParse(payload, "192.168.1.2", out NetworkRoomInfo room));
		Equal(NetworkTransport.Lan, room.Transport);
		Equal("LAN 房间_1", room.RoomName);
		Equal("192.168.1.2", room.Address);
		Equal(3, room.PlayerCount);
		False(room.ItemMode);
		Equal(100, room.WinningScore);

		False(LanDiscoveryMessage.TryParse("{bad json", "192.168.1.2", out _));
		False(LanDiscoveryMessage.TryParse("{\"player_count\":\"nope\"}", "192.168.1.2", out _));
	}

	private static void NetworkTableCardSnapshotRoundtripsOverlayFields()
	{
		var snapshot = new NetworkTableCardSnapshot(
			CardCode: string.Empty,
			AttachedItemCardIds: Array.Empty<string>(),
			IsFaceUp: false,
			IsBuriedInSand: true,
			IsSlimeCoated: false,
			CardOwnerPlayerIndex: -1,
			RemoveCardOnCapture: true,
			ItemOverlayId: "bomb",
			ItemOwnerPlayerIndex: 2);

		Dictionary<string, object?> data = NetworkTableCardSnapshotCodec.ToDictionary(snapshot);
		NetworkTableCardSnapshot parsed = NetworkTableCardSnapshotCodec.FromDictionary(data);

		Equal(string.Empty, parsed.CardCode);
		Equal(0, parsed.AttachedItemCardIds.Length);
		False(parsed.IsFaceUp);
		True(parsed.IsBuriedInSand);
		False(parsed.IsSlimeCoated);
		Equal(-1, parsed.CardOwnerPlayerIndex);
		True(parsed.RemoveCardOnCapture);
		Equal("bomb", parsed.ItemOverlayId);
		Equal(2, parsed.ItemOwnerPlayerIndex);
	}

	private static CaptureScoringRules.Context DefaultScoringContext => new(
		SuppressSlimeCoatedScore: false,
		ApplyScoreReductionPerCard: false,
		ScoreReductionPerCard: 0);

	private static PlayingCard CreatePlayingCard()
	{
		return new PlayingCard("hearts", "A", new Godot.Texture2D());
	}

	private static GameBoardState CreateCaptureState(PlayingCard card)
	{
		var state = new GameBoardState();
		state.PlayerDecks.Add(new List<PlayingCard>());
		state.TableCards.Add(new TableCard(card, isFaceUp: true));
		return state;
	}

	private static bool InvokeCanAcceptSceneLoadAck(
		IReadOnlyCollection<NetworkPlayerPublicState> players,
		int senderPeerId,
		int reportedSeatIndex,
		out int senderSeatIndex)
	{
		MethodInfo? method = typeof(MultiplayerBoardSyncRules).GetMethod(
			"CanAcceptSceneLoadAck",
			BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		True(method != null);
		object?[] args = { players, senderPeerId, reportedSeatIndex, -1 };
		bool accepted = (bool)(method!.Invoke(null, args) ?? false);
		senderSeatIndex = (int)args[3]!;
		return accepted;
	}

	private static void ResetGameSaveStoreTestState()
	{
		Godot.Engine.MainLoopOverride = null;
		Godot.GD.ClearWarnings();
		Godot.FileAccess.ClearTestState();
		Godot.DirAccess.ClearTestState();
		SetStaticField(typeof(GameSaveStore), "_attemptedLegacyMigration", false);
		SetStaticField(typeof(GameSaveStore), "_attemptedProgressProfileMigration", false);
		SetStaticField(typeof(GameSaveStore), "_warnedMissingSaveManager", false);
	}

	private static Godot.TestSaveManagerNode InstallSaveManagerForTests(string activeSlotId)
	{
		var root = new Godot.Node();
		var saveManager = new Godot.TestSaveManagerNode();
		root.AddNamedChild("SaveManager", saveManager);
		Godot.Engine.MainLoopOverride = new Godot.SceneTree { Root = root };
		saveManager.SeedValue("migration.settings_cfg.v1", true);
		saveManager.SeedValue("migration.progress_profile.v1", true);
		saveManager.SeedValue(GameSaveStore.ActiveProfileSlotKey, activeSlotId);
		return saveManager;
	}

	private static void SeedProfile(
		Godot.TestSaveManagerNode saveManager,
		string slotId,
		GameSaveStore.GameProgressData progressData)
	{
		saveManager.SeedSlot(slotId, progressData.ToDictionary());
	}

	private static GameSaveStore.GameProgressData LoadStoredProfile(Godot.TestSaveManagerNode saveManager, string slotId)
	{
		return GameSaveStore.GameProgressData.FromDictionary(
			saveManager.GetSlotData(slotId),
			GameSaveStore.GameProgressData.CreateDefault());
	}

	private static GameSaveStore.GameProgressData CreateProgressData(
		string playerName,
		string pendingAdventureRunJson = "")
	{
		return GameSaveStore.GameProgressData.CreateDefault() with
		{
			PlayerName = playerName,
			PendingAdventureRunJson = pendingAdventureRunJson,
		};
	}

	private static void SetStaticField(Type type, string fieldName, object value)
	{
		FieldInfo? field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		True(field != null);
		field!.SetValue(null, value);
	}

	private static NetworkPlayerPublicState Player(int peerId, int seatIndex, bool isHost, bool isReady)
	{
		return new NetworkPlayerPublicState(
			peerId,
			$"stable-{peerId}",
			$"Player{peerId}",
			"cat",
			seatIndex,
			isHost,
			IsLocal: peerId == 1,
			isReady,
			IsConnected: true);
	}

	private static string ReadTresString(string tresText, string propertyName)
	{
		Match match = Regex.Match(tresText, $"^{Regex.Escape(propertyName)} = \"([^\"]*)\"$", RegexOptions.Multiline);
		return match.Success ? match.Groups[1].Value : string.Empty;
	}

	private static Dictionary<string, List<string>> ReadLocalizationRowsByKey(string localizationPath, out List<string> header)
	{
		True(File.Exists(localizationPath));
		string[] localizationLines = File.ReadAllLines(localizationPath);
		True(localizationLines.Length > 1);

		header = ParseCsvLine(localizationLines[0]);
		True(header.Count > 1);

		var rowsByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (string line in localizationLines[1..])
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			List<string> row = ParseCsvLine(line);
			if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
			{
				continue;
			}

			rowsByKey[row[0].Trim()] = row;
		}

		return rowsByKey;
	}

	private static List<string> ParseCsvLine(string line)
	{
		var values = new List<string>();
		var current = new System.Text.StringBuilder();
		bool inQuotes = false;

		for (int index = 0; index < line.Length; index++)
		{
			char value = line[index];
			if (value == '"')
			{
				if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
				{
					current.Append('"');
					index++;
				}
				else
				{
					inQuotes = !inQuotes;
				}
				continue;
			}

			if (value == ',' && !inQuotes)
			{
				values.Add(current.ToString());
				current.Clear();
				continue;
			}

			current.Append(value);
		}

		values.Add(current.ToString());
		return values;
	}

	private static bool ContainsHanCharacter(string value)
	{
		foreach (char character in value)
		{
			if (character is >= '\u3400' and <= '\u9fff')
			{
				return true;
			}
		}

		return false;
	}

	private static List<string> ReadSupportedLocalePreferences(string repoRoot)
	{
		string localizationSourcePath = Path.Combine(repoRoot, "scripts", "Localization.cs");
		True(File.Exists(localizationSourcePath));

		string source = File.ReadAllText(localizationSourcePath);
		var locales = new List<string>();
		foreach (Match match in Regex.Matches(
			source,
			"^\\s*\\(\"(?<preference>[^\"]+)\",\\s*\"[^\"]+\",\\s*\"[^\"]*\",\\s*\"[^\"]*\"\\),?\\s*$",
			RegexOptions.Multiline))
		{
			string preference = match.Groups["preference"].Value.Trim();
			if (!string.IsNullOrWhiteSpace(preference) && preference != "auto")
			{
				locales.Add(preference);
			}
		}

		True(locales.Count > 0);
		return locales;
	}

	private static IEnumerable<string> EnumerateRuntimeLocalizationReferenceFiles(string repoRoot)
	{
		foreach (string absolutePath in Directory.GetFiles(repoRoot, "*.*", SearchOption.AllDirectories))
		{
			string extension = Path.GetExtension(absolutePath);
			if (extension != ".cs" && extension != ".tscn" && extension != ".tres")
			{
				continue;
			}

			string relativePath = Path.GetRelativePath(repoRoot, absolutePath).Replace('\\', '/');
			if (IsExcludedRuntimeLocalizationPath(relativePath))
			{
				continue;
			}

			yield return relativePath;
		}
	}

	private static bool IsExcludedRuntimeLocalizationPath(string relativePath)
	{
		if (relativePath.StartsWith("tests/", StringComparison.Ordinal)
			|| relativePath.StartsWith("addons/", StringComparison.Ordinal)
			|| relativePath.StartsWith(".godot/", StringComparison.Ordinal)
			|| relativePath.StartsWith("outputs/", StringComparison.Ordinal))
		{
			return true;
		}

		if (relativePath.EndsWith(".uid", StringComparison.Ordinal)
			|| relativePath.EndsWith(".import", StringComparison.Ordinal))
		{
			return true;
		}

		if (relativePath.Contains("gallery", StringComparison.OrdinalIgnoreCase)
			|| relativePath.Contains("preview", StringComparison.OrdinalIgnoreCase)
			|| relativePath.Contains("_editor_", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (relativePath == "scenes/adventure_suit_upgrade_editor_scene.tscn"
			|| relativePath == "scenes/upgrade_choice_popup_asset_scene.tscn")
		{
			return true;
		}

		return relativePath.EndsWith(".cs", StringComparison.Ordinal)
			&& (relativePath.Contains(".Debug", StringComparison.Ordinal)
				|| relativePath.Contains("DebugRecording", StringComparison.Ordinal));
	}

	private static IEnumerable<LocalizationReference> FindLocalizationReferences(string relativePath, string[] lines)
	{
		for (int index = 0; index < lines.Length; index++)
		{
			string line = lines[index];
			if (relativePath.EndsWith(".cs", StringComparison.Ordinal))
			{
				foreach (Match match in Regex.Matches(line, "\\b(?:Localize|localize|Tr)\\(\\s*\"(?<key>[A-Z0-9_]+)\"\\s*\\)"))
				{
					string key = match.Groups["key"].Value;
					if (IsLikelyLocalizationKey(key))
					{
						yield return new LocalizationReference(relativePath, index + 1, key);
					}
				}

				continue;
			}

			if (relativePath.EndsWith(".tscn", StringComparison.Ordinal))
			{
				foreach (Match match in Regex.Matches(line, "^\\s*(?:text|placeholder_text|tooltip_text|popup/item_\\d+/text)\\s*=\\s*\"(?<key>[^\"]*)\"$"))
				{
					string key = match.Groups["key"].Value.Trim();
					if (IsLikelyLocalizationKey(key))
					{
						yield return new LocalizationReference(relativePath, index + 1, key);
					}
				}

				continue;
			}

			foreach (Match match in Regex.Matches(line, "^\\s*[A-Za-z0-9_]*Key\\s*=\\s*\"(?<key>[^\"]*)\"$"))
			{
				string key = match.Groups["key"].Value.Trim();
				if (IsLikelyLocalizationKey(key))
				{
					yield return new LocalizationReference(relativePath, index + 1, key);
				}
			}
		}
	}

	private static bool IsLikelyLocalizationKey(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || !value.Contains('_', StringComparison.Ordinal))
		{
			return false;
		}

		foreach (char character in value)
		{
			if ((character < 'A' || character > 'Z') && (character < '0' || character > '9') && character != '_')
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsAllowedRuntimeSceneText(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || IsLikelyLocalizationKey(value))
		{
			return true;
		}

		if (Regex.IsMatch(value, "^v\\d+(?:\\.\\d+)*$", RegexOptions.CultureInvariant)
			|| Regex.IsMatch(value, "^\\d+$", RegexOptions.CultureInvariant)
			|| Regex.IsMatch(value, "^\\d+%$", RegexOptions.CultureInvariant)
			|| Regex.IsMatch(value, "^\\d+\\s*x\\s*\\d+$", RegexOptions.CultureInvariant))
		{
			return true;
		}

		if (value is "A" or "B" or "J" or "Q" or "K" or "P" or "Enter" or "Escape" or "Start"
			or "Up" or "Down" or "Left" or "Right"
			or "D-Pad Up" or "D-Pad Down" or "D-Pad Left" or "D-Pad Right"
			or "Deckland" or "Steam" or "LAN")
		{
			return true;
		}

		return !ContainsHanCharacter(value) && !Regex.IsMatch(value, "[A-Za-z]", RegexOptions.CultureInvariant);
	}

	private static int ReadTresInt(string tresText, string propertyName, int fallback)
	{
		Match match = Regex.Match(tresText, $"^{Regex.Escape(propertyName)} = (-?\\d+)$", RegexOptions.Multiline);
		return match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : fallback;
	}

	private static readonly string[] RuntimeHardcodedSceneRelativePaths =
	{
		"scenes/main_menu_scene.tscn",
		"scenes/main_menu_player_count_dialog.tscn",
		"scenes/main_menu_settings_dialog.tscn",
		"scenes/main_menu_controls_dialog.tscn",
		"scenes/main_menu_exit_dialog.tscn",
		"scenes/main_menu_avatar_entry.tscn",
		"scenes/character_avatar_edit_scene.tscn",
		"scenes/avatar_unlock_prompt.tscn",
		"scenes/multiplayer_lobby_scene.tscn",
		"scenes/multiplayer_lobby_room_row.tscn",
		"scenes/multiplayer_lobby_player_row.tscn",
		"scenes/game_board_result_banner.tscn",
		"scenes/pause_menu.tscn",
		"scenes/item_selection_overlay.tscn",
		"scenes/adventure_avatar_menu_scene.tscn",
		"scenes/adventure_level_entry.tscn",
		"scenes/adventure_mode_entry.tscn",
		"scenes/adventure_upgrade_selection_overlay.tscn",
	};

	private readonly record struct LocalizationReference(string RelativePath, int LineNumber, string Key);

	private static void AssertFramePatternExists(string repoRoot, string framePattern, int frameCount)
	{
		for (int frameIndex = 1; frameIndex <= frameCount; frameIndex++)
		{
			AssertResPathExists(repoRoot, string.Format(framePattern, frameIndex));
		}
	}

	private static void AssertResPathExists(string repoRoot, string resPath)
	{
		True(resPath.StartsWith("res://", StringComparison.Ordinal));
		string absolutePath = Path.Combine(repoRoot, resPath["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
		True(File.Exists(absolutePath));
		True(new FileInfo(absolutePath).Length > 0);
	}

	private static int CountContiguousFrames(string repoRoot, string resDirectory, string filePattern)
	{
		True(resDirectory.StartsWith("res://", StringComparison.Ordinal));
		string absoluteDirectory = Path.Combine(repoRoot, resDirectory["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
		True(Directory.Exists(absoluteDirectory));

		int frameCount = 0;
		for (int frameIndex = 1; ; frameIndex++)
		{
			string absolutePath = Path.Combine(absoluteDirectory, string.Format(filePattern, frameIndex));
			if (!File.Exists(absolutePath))
			{
				break;
			}

			True(new FileInfo(absolutePath).Length > 0);
			frameCount++;
		}

		return frameCount;
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "project.godot")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate project.godot.");
	}

	private static CaptureScoringRules.TableCard TableCard(
		string rank,
		int scoreValue,
		bool isBuriedInSand = false,
		bool isSlimeCoated = false,
		bool hasDoubleCollectItemEffect = false,
		bool hasPairDestroyItemEffect = false,
		int itemOwnerPlayerIndex = -1,
		string suit = "")
	{
		return new CaptureScoringRules.TableCard(
			new CaptureScoringRules.Card(rank, scoreValue, HasDoubleCollectEffect: false, Suit: suit),
			isBuriedInSand,
			isSlimeCoated,
			hasDoubleCollectItemEffect,
			hasPairDestroyItemEffect,
			itemOwnerPlayerIndex);
	}

	private static AdventureUpgradeDefinitionModel Upgrade(string id, string rarity, string suit = "hearts")
	{
		return new AdventureUpgradeDefinitionModel(
			id,
			DisplayName: id,
			suit,
			rarity,
			TriggerText: string.Empty,
			EffectText: string.Empty,
			Enabled: true);
	}

	private static bool ContainsUpgradeId(IReadOnlyList<AdventureUpgradeDefinitionModel> offer, string upgradeId)
	{
		foreach (AdventureUpgradeDefinitionModel definition in offer)
		{
			if (definition.Id == upgradeId)
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasUniqueUpgradeIds(IReadOnlyList<AdventureUpgradeDefinitionModel> offer)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (AdventureUpgradeDefinitionModel definition in offer)
		{
			if (!seen.Add(definition.Id))
			{
				return false;
			}
		}

		return true;
	}

	private static void Run(string name, Action test)
	{
		try
		{
			test();
			_passed++;
			Console.WriteLine($"PASS {name}");
		}
		catch (Exception ex)
		{
			_failed++;
			Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
		}
	}

	private static void Equal<T>(T expected, T actual)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
		{
			throw new InvalidOperationException($"expected {expected}, got {actual}");
		}
	}

	private static void True(bool value)
	{
		if (!value)
		{
			throw new InvalidOperationException("expected true, got false");
		}
	}

	private static void False(bool value)
	{
		if (value)
		{
			throw new InvalidOperationException("expected false, got true");
		}
	}
}
