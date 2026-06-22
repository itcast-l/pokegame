using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
		Run("card play resolver inserts miss at table start for reverse flow anchor", CardPlayResolverInsertsMissAtTableStartForReverseFlowAnchor);
		Run("capture resolver identifies middle captures", CaptureResolverIdentifiesMiddleCaptures);
		Run("score service returns score breakdown", ScoreServiceReturnsScoreBreakdown);
		Run("capture flow planner builds capture summary", CaptureFlowPlannerBuildsCaptureSummary);
		Run("capture flow planner skips score when award disabled", CaptureFlowPlannerSkipsScoreWhenAwardDisabled);
		Run("reward resolver uses weighted reservoir selection", RewardResolverUsesWeightedReservoirSelection);
		Run("item card effect support blocks reserved empty effects", ItemCardEffectSupportBlocksReservedEmptyEffects);
		Run("playing card attachment replaces existing effect", PlayingCardAttachmentReplacesExistingEffect);
		Run("capture state apply capture clears effects when preserve disabled", CaptureStateApplyCaptureClearsEffectsWhenPreserveDisabled);
		Run("capture state apply capture preserves effects when preserve enabled", CaptureStateApplyCapturePreservesEffectsWhenPreserveEnabled);
		Run("captured card effect persists through deck and can be replaced", CapturedCardEffectPersistsThroughDeckAndCanBeReplaced);
		Run("potential capture ignores buried matches", PotentialCaptureIgnoresBuriedMatches);
		Run("potential capture prefers royal threshold", PotentialCapturePrefersRoyalThreshold);
		Run("potential capture identifies pair only captures", PotentialCaptureIdentifiesPairOnlyCaptures);
		Run("adventure ai early profile can skip obvious capture", AdventureAiEarlyProfileCanSkipObviousCapture);
		Run("adventure ai early profile skips pair only capture", AdventureAiEarlyProfileSkipsPairOnlyCapture);
		Run("adventure ai point scoring starts after early stages", AdventureAiPointScoringStartsAfterEarlyStages);
		Run("adventure ai random capture can pick non-best capture", AdventureAiRandomCaptureCanPickNonBestCapture);
		Run("adventure ai late best capture skips pair only captures", AdventureAiLateBestCaptureSkipsPairOnlyCaptures);
		Run("adventure ai late random capture skips pair only captures", AdventureAiLateRandomCaptureSkipsPairOnlyCaptures);
		Run("adventure ai final profile can skip best capture", AdventureAiFinalProfileCanSkipBestCapture);
		Run("lan address helper computes ipv4 broadcast address", LanAddressHelperComputesIpv4BroadcastAddress);
		Run("character audio config resources point to existing files", CharacterAudioConfigResourcesPointToExistingFiles);
		Run("character avatar visual resources point to existing files", CharacterAvatarVisualResourcesPointToExistingFiles);
		Run("character avatar display names are localized for every locale", CharacterAvatarDisplayNamesAreLocalizedForEveryLocale);
		Run("avatar feedback effect resources stay contiguous", AvatarFeedbackEffectResourcesStayContiguous);
		Run("item card visual resources point to existing files", ItemCardVisualResourcesPointToExistingFiles);
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
		Run("multiplayer board sync rejects invalid play requests", MultiplayerBoardSyncRejectsInvalidPlayRequests);
		Run("multiplayer board sync limits private hand visibility", MultiplayerBoardSyncLimitsPrivateHandVisibility);
		Run("network table card snapshot roundtrips overlay fields", NetworkTableCardSnapshotRoundtripsOverlayFields);
		Run("local seat input enables only for in-game human connected state", LocalSeatInputEnablesOnlyForInGameHumanConnectedState);
		Run("network seat control accepts remote actions only for connected humans", NetworkSeatControlAcceptsRemoteActionsOnlyForConnectedHumans);
		Run("network seat control reveals pending target hand only for connected humans", NetworkSeatControlRevealsPendingTargetHandOnlyForConnectedHumans);
		Run("network seat control sends pending selection requests only when client is ready", NetworkSeatControlSendsPendingSelectionRequestsOnlyWhenClientIsReady);
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

	private static int ReadTresInt(string tresText, string propertyName, int fallback)
	{
		Match match = Regex.Match(tresText, $"^{Regex.Escape(propertyName)} = (-?\\d+)$", RegexOptions.Multiline);
		return match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : fallback;
	}

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
		int itemOwnerPlayerIndex = -1)
	{
		return new CaptureScoringRules.TableCard(
			new CaptureScoringRules.Card(rank, scoreValue, HasDoubleCollectEffect: false),
			isBuriedInSand,
			isSlimeCoated,
			hasDoubleCollectItemEffect,
			hasPairDestroyItemEffect,
			itemOwnerPlayerIndex);
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
