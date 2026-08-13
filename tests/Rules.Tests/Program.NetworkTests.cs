using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Deckland.Rules;

internal static partial class Program
{
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

	private static void TurnRulesSkipOutOfRangeSeats()
	{
		int[] turnOrder = { 0, 2, 1, 3 };

		Equal(1, GameTurnRules.GetNextPlayerIndex(
			turnOrder,
			currentPlayerIndex: 2,
			playerCount: 2,
			fallbackPlayerIndex: 0));
	}

	private static void TurnRulesSkipPlayersWithoutCards()
	{
		int[] turnOrder = { 0, 2, 1, 3 };
		var threePlayerStates = new[]
		{
			new GameTurnPlayerCardState(1, 0, 0, false),
			new GameTurnPlayerCardState(1, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 0, false),
		};
		var fourPlayerStates = new[]
		{
			new GameTurnPlayerCardState(1, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 0, false),
			new GameTurnPlayerCardState(1, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 0, false),
		};

		True(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			threePlayerStates,
			currentPlayerIndex: 0,
			playerCount: 3,
			out int nextPlayerIndex));
		Equal(1, nextPlayerIndex);
		True(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			threePlayerStates,
			currentPlayerIndex: 1,
			playerCount: 3,
			out nextPlayerIndex));
		Equal(0, nextPlayerIndex);
		True(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			fourPlayerStates,
			currentPlayerIndex: 2,
			playerCount: 4,
			out nextPlayerIndex));
		Equal(0, nextPlayerIndex);
	}

	private static void TurnRulesSeparateResourcesFromImmediateActions()
	{
		int[] turnOrder = { 0, 2, 1, 3 };
		var playerStates = new[]
		{
			new GameTurnPlayerCardState(1, 0, 0, false),
			new GameTurnPlayerCardState(0, 1, 0, false),
			new GameTurnPlayerCardState(0, 0, 1, true),
		};

		True(playerStates[1].HasResources);
		False(playerStates[1].CanActNow);
		True(playerStates[2].HasResources);
		True(playerStates[2].CanActNow);
		True(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			playerStates,
			currentPlayerIndex: 0,
			playerCount: 3,
			out int nextPlayerIndex));
		Equal(2, nextPlayerIndex);
	}

	private static void TurnRulesReportNoEligibleNextPlayer()
	{
		int[] turnOrder = { 0, 2, 1, 3 };
		var playerStates = new[]
		{
			new GameTurnPlayerCardState(0, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 1, false),
			new GameTurnPlayerCardState(0, 1, 0, false),
		};

		False(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			playerStates,
			currentPlayerIndex: 0,
			playerCount: 3,
			out int nextPlayerIndex));
		Equal(-1, nextPlayerIndex);
		False(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			playerStates,
			currentPlayerIndex: 7,
			playerCount: 3,
			out nextPlayerIndex));
	}

	private static void MatchOutcomeRulesCoverResourcesBlocksAndDraws()
	{
		var zeroResources = new[]
		{
			new GameTurnPlayerCardState(0, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 0, false),
		};
		MatchOutcome tiedEmpty = MatchOutcomeRules.Evaluate(zeroResources, new[] { 12, 12 }, 2);
		Equal(MatchOutcomeKind.Draw, tiedEmpty.Kind);
		Equal(-1, tiedEmpty.WinnerPlayerIndex);

		var singleResourceOwner = new[]
		{
			new GameTurnPlayerCardState(0, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 1, false),
		};
		MatchOutcome resourceWinner = MatchOutcomeRules.Evaluate(singleResourceOwner, new[] { 99, 1 }, 2);
		Equal(MatchOutcomeKind.Winner, resourceWinner.Kind);
		Equal(1, resourceWinner.WinnerPlayerIndex);

		var blockedWithTemporaryItem = new[]
		{
			new GameTurnPlayerCardState(0, 0, 1, false),
			new GameTurnPlayerCardState(0, 1, 0, false),
		};
		MatchOutcome blockedWinner = MatchOutcomeRules.Evaluate(blockedWithTemporaryItem, new[] { 5, 8 }, 2);
		Equal(MatchOutcomeKind.Winner, blockedWinner.Kind);
		Equal(1, blockedWinner.WinnerPlayerIndex);

		MatchOutcome scorePriority = MatchOutcomeRules.Evaluate(singleResourceOwner, new[] { 1, 99 }, 2, 0);
		Equal(0, scorePriority.WinnerPlayerIndex);
		True(MatchOutcomeRules.IsSerializedOutcomeValid(MatchOutcomeKind.Draw, -1, 2));
		False(MatchOutcomeRules.IsSerializedOutcomeValid(MatchOutcomeKind.Draw, 0, 2));
		True(MatchOutcomeRules.IsSerializedOutcomeValid(MatchOutcomeKind.Winner, 1, 2));

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
			hasQuestionCard: false,
			new QuestionCardRefreshState(1, 2),
			nextTurnPlayerIndex: 1,
			playerCount: 4,
			refreshCyclesAfterCapture: 2,
			questionCardLifetimeTurns: 0);

		False(result.ShouldRefreshQuestionCard);
		Equal(2, result.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(2, result.State.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void TurnProgressRefreshesQuestionCardAfterConfiguredCycle()
	{
		QuestionCardRefreshAdvanceResult result = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: false,
			hasQuestionCard: false,
			new QuestionCardRefreshState(3, 2),
			nextTurnPlayerIndex: 2,
			playerCount: 2,
			refreshCyclesAfterCapture: 2,
			questionCardLifetimeTurns: 0);

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
			hasQuestionCard: false,
			currentState,
			nextTurnPlayerIndex: 2,
			playerCount: 2,
			refreshCyclesAfterCapture: 2,
			questionCardLifetimeTurns: 0);

		False(result.ShouldRefreshQuestionCard);
		Equal(currentState.CompletedTurnsSinceQuestionCardRefresh, result.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(currentState.QuestionCardRefreshAnchorPlayerIndex, result.State.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void TurnProgressExpiresUncollectedQuestionCardBeforeRestartingRefresh()
	{
		QuestionCardRefreshAdvanceResult firstTurn = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: true,
			hasQuestionCard: true,
			new QuestionCardRefreshState(0, 0),
			nextTurnPlayerIndex: 1,
			playerCount: 2,
			refreshCyclesAfterCapture: 2,
			questionCardLifetimeTurns: 2);

		False(firstTurn.ShouldExpireQuestionCard);
		Equal(1, firstTurn.State.CompletedTurnsSinceQuestionCardRefresh);

		QuestionCardRefreshAdvanceResult expired = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: true,
			hasQuestionCard: true,
			firstTurn.State,
			nextTurnPlayerIndex: 0,
			playerCount: 2,
			refreshCyclesAfterCapture: 2,
			questionCardLifetimeTurns: 2);

		True(expired.ShouldExpireQuestionCard);
		False(expired.ShouldRefreshQuestionCard);
		Equal(0, expired.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(0, expired.State.QuestionCardRefreshAnchorPlayerIndex);

		QuestionCardRefreshAdvanceResult refresh = expired;
		for (int completedTurn = 1; completedTurn <= 4; completedTurn++)
		{
			refresh = TurnProgressRules.AdvanceQuestionCardRefresh(
				itemMode: true,
				hasTableSpecialCard: false,
				hasQuestionCard: false,
				refresh.State,
				nextTurnPlayerIndex: completedTurn % 2,
				playerCount: 2,
				refreshCyclesAfterCapture: 2,
				questionCardLifetimeTurns: 0);
			Equal(completedTurn == 4, refresh.ShouldRefreshQuestionCard);
		}
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

	private static void SteamMetadataPublishesMigrationAuthority()
	{
		NetworkSessionConfig config = new(
			"session",
			"Steam Room",
			string.Empty,
			"match",
			1234,
			NetworkTransport.Steam,
			3,
			true,
			150,
			LobbyVisibility.FriendsOnly);
		Dictionary<string, string> metadata = SteamLobbyMetadata.Build(
			config,
			"host",
			NetworkSessionState.InGame.ToString(),
			authorityEpoch: 7,
			authorityHostSteamId: 76561198000000002);

		Equal("7", metadata[SteamLobbyMetadata.AuthorityEpochKey]);
		Equal("76561198000000002", metadata[SteamLobbyMetadata.AuthorityHostSteamIdKey]);
		Equal("0", SteamLobbyMetadata.Build(
			config,
			"host",
			NetworkSessionState.Lobby.ToString(),
			authorityEpoch: -1,
			authorityHostSteamId: -1)[SteamLobbyMetadata.AuthorityEpochKey]);
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

	private static void MultiplayerLobbyReadyIntentWaitsForCredentialAcknowledgement()
	{
		NetworkPlayerPublicState pendingCredential = Player(peerId: 2, seatIndex: 1, isHost: false, isReady: false) with
		{
			CredentialsAcknowledged = false,
		};
		Equal(
			LobbyReadyIntentDisposition.DeferUntilCredentialsAcknowledged,
			MultiplayerLobbyRules.EvaluateReadyIntent(pendingCredential, isReady: true));

		NetworkPlayerPublicState acknowledged = pendingCredential with { CredentialsAcknowledged = true };
		Equal(
			LobbyReadyIntentDisposition.Apply,
			MultiplayerLobbyRules.EvaluateReadyIntent(acknowledged, isReady: true));
		Equal(
			LobbyReadyIntentDisposition.NoChange,
			MultiplayerLobbyRules.EvaluateReadyIntent(acknowledged, isReady: false));
		Equal(
			LobbyReadyIntentDisposition.Reject,
			MultiplayerLobbyRules.EvaluateReadyIntent(acknowledged with { IsConnected = false }, isReady: true));
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
			currentCredentialGeneration: 1,
			requestCredentialGeneration: 1,
			currentCredentialVerifier: SeatCredentialRules.ComputeVerifier("token-1"),
			requestCredential: "token-1",
			currentPlayerSessionId: "stable-1",
			requestPlayerSessionId: "stable-1"));

		True(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanConnected,
			currentPeerId: 22,
			requestPeerId: 22,
			currentCredentialGeneration: 1,
			requestCredentialGeneration: 1,
			currentCredentialVerifier: SeatCredentialRules.ComputeVerifier("token-1"),
			requestCredential: "token-1",
			currentPlayerSessionId: "stable-1",
			requestPlayerSessionId: "stable-1"));

		False(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.AiProxyReclaimable,
			currentPeerId: 0,
			requestPeerId: 33,
			currentCredentialGeneration: 1,
			requestCredentialGeneration: 1,
			currentCredentialVerifier: SeatCredentialRules.ComputeVerifier("token-1"),
			requestCredential: "token-1",
			currentPlayerSessionId: "stable-1",
			requestPlayerSessionId: "stable-1"));

		False(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanConnected,
			currentPeerId: 22,
			requestPeerId: 33,
			currentCredentialGeneration: 1,
			requestCredentialGeneration: 1,
			currentCredentialVerifier: SeatCredentialRules.ComputeVerifier("token-1"),
			requestCredential: "token-2",
			currentPlayerSessionId: "stable-1",
			requestPlayerSessionId: "stable-1"));

		False(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanConnected,
			currentPeerId: 22,
			requestPeerId: 33,
			currentCredentialGeneration: 1,
			requestCredentialGeneration: 1,
			currentCredentialVerifier: SeatCredentialRules.ComputeVerifier("token-1"),
			requestCredential: "token-1",
			currentPlayerSessionId: "stable-1",
			requestPlayerSessionId: "stable-2"));

		True(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanReclaimPending,
			currentPeerId: 44,
			requestPeerId: 55,
			currentCredentialGeneration: 1,
			requestCredentialGeneration: 1,
			currentCredentialVerifier: SeatCredentialRules.ComputeVerifier("token-1"),
			requestCredential: "token-1",
			currentPlayerSessionId: "stable-1",
			requestPlayerSessionId: "stable-1"));

		False(NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			SeatControlMode.HumanReclaimPending,
			currentPeerId: 44,
			requestPeerId: 44,
			currentCredentialGeneration: 1,
			requestCredentialGeneration: 1,
			currentCredentialVerifier: SeatCredentialRules.ComputeVerifier("token-1"),
			requestCredential: "token-1",
			currentPlayerSessionId: "stable-1",
			requestPlayerSessionId: "stable-1"));
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

	private static void NetworkReconnectRulesDispatchSteamLobbyJoinCallbacks()
	{
		int leaveCount = 0;
		int retryCount = 0;
		int failCount = 0;
		int acceptCount = 0;
		ulong leftLobbyId = 0;

		SteamLobbyJoinCallbackAction Dispatch(
			NetworkSessionState state,
			bool pendingHost,
			ulong expected,
			ulong callback,
			ulong accepted,
			int response)
		{
			leaveCount = 0;
			retryCount = 0;
			failCount = 0;
			acceptCount = 0;
			leftLobbyId = 0;
			return NetworkReconnectRules.DispatchSteamLobbyJoinCallback(
				state,
				pendingHost,
				expected,
				callback,
				accepted,
				response,
				id =>
				{
					leaveCount++;
					leftLobbyId = id;
				},
				() => retryCount++,
				() => failCount++,
				() => acceptCount++);
		}

		Equal(
			SteamLobbyJoinCallbackAction.LeaveUnexpectedLobby,
			Dispatch(NetworkSessionState.Joining, false, 22, 11, 0, 1));
		Equal(1, leaveCount);
		Equal(11UL, leftLobbyId);
		Equal(0, acceptCount);
		Equal(1, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.LeaveUnexpectedLobby,
			Dispatch(NetworkSessionState.Idle, false, 0, 11, 0, 1));
		Equal(1, leaveCount);
		Equal(0, acceptCount);
		Equal(1, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.Ignore,
			Dispatch(NetworkSessionState.Hosting, true, 0, 33, 0, 1));
		Equal(0, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.Ignore,
			Dispatch(NetworkSessionState.Lobby, false, 0, 33, 33, 1));
		Equal(0, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.LeaveUnexpectedLobby,
			Dispatch(NetworkSessionState.Lobby, false, 33, 33, 0, 1));
		Equal(1, leaveCount);
		Equal(33UL, leftLobbyId);
		Equal(1, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.AcceptExpectedJoin,
			Dispatch(NetworkSessionState.Reconnecting, false, 22, 22, 0, 1));
		Equal(1, acceptCount);
		Equal(0, leaveCount);
		Equal(1, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.RetryExpectedJoin,
			Dispatch(NetworkSessionState.Reconnecting, false, 22, 22, 0, 5));
		Equal(1, retryCount);
		Equal(0, acceptCount);
		Equal(1, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.FailExpectedJoin,
			Dispatch(NetworkSessionState.Joining, false, 22, 22, 0, 5));
		Equal(1, failCount);
		Equal(0, acceptCount);
		Equal(1, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.LeaveUnexpectedLobby,
			Dispatch(NetworkSessionState.Joining, false, 0, 22, 0, 1));
		Equal(1, leaveCount);
		Equal(0, acceptCount);
		Equal(1, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.Ignore,
			Dispatch(NetworkSessionState.Reconnecting, false, 0, 22, 0, 5));
		Equal(0, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.Ignore,
			Dispatch(NetworkSessionState.Joining, false, 0, 22, 22, 1));
		Equal(0, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.Ignore,
			Dispatch(NetworkSessionState.Lobby, false, 0, 22, 22, 1));
		Equal(0, leaveCount + retryCount + failCount + acceptCount);

		Equal(
			SteamLobbyJoinCallbackAction.LeaveUnexpectedLobby,
			Dispatch(NetworkSessionState.Joining, false, 0, 11, 22, 1));
		Equal(1, leaveCount);
		Equal(11UL, leftLobbyId);
	}

	private static void SteamLobbyJoinHandlerGatesClientPeerCreation()
	{
		string path = Path.Combine(FindRepositoryRoot(), "scripts", "NetworkManager.Steam.cs");
		string source = File.ReadAllText(path);
		int callbackStart = source.IndexOf("private void OnSteamLobbyJoined(", StringComparison.Ordinal);
		int beginStart = source.IndexOf("private void StartSteamLobbyJoin(", StringComparison.Ordinal);
		int completionStart = source.IndexOf("private void CompleteExpectedSteamLobbyJoin(", StringComparison.Ordinal);
		int leaveStart = source.IndexOf("private void LeaveSteamLobby(", StringComparison.Ordinal);
		True(callbackStart >= 0);
		True(beginStart > callbackStart);
		True(completionStart > callbackStart);
		True(leaveStart > completionStart);

		string callbackMethod = source[callbackStart..completionStart];
		int hostCreatedStart = source.IndexOf("private void OnSteamLobbyCreated(", StringComparison.Ordinal);
		int lobbyJoinedStart = source.IndexOf("private void OnSteamLobbyJoined(", StringComparison.Ordinal);
		True(hostCreatedStart >= 0);
		True(lobbyJoinedStart > hostCreatedStart);
		string hostCreatedMethod = source[hostCreatedStart..lobbyJoinedStart];
		True(hostCreatedMethod.Contains("_acceptedSteamLobbyJoinId = lobbyId;", StringComparison.Ordinal));
		string beginMethod = source[beginStart..completionStart];
		string completionMethod = source[completionStart..leaveStart];
		True(callbackMethod.Contains("NetworkReconnectRules.DispatchSteamLobbyJoinCallback(", StringComparison.Ordinal));
		True(callbackMethod.Contains("_pendingSteamLobbyJoinId = 0;", StringComparison.Ordinal));
		True(callbackMethod.Contains("_acceptedSteamLobbyJoinId = lobbyId;", StringComparison.Ordinal));
		False(callbackMethod.Contains("TryCreateSteamPeer(", StringComparison.Ordinal));
		True(beginMethod.Contains("_pendingSteamLobbyJoinId = lobbyId;", StringComparison.Ordinal));
			True(beginMethod.Contains("_steamPlatform?.JoinLobby(lobbyId)", StringComparison.Ordinal));
		Equal(
			2,
			Regex.Matches(
				completionMethod,
				@"ClearAcceptedSteamLobbyJoinIfNeeded\(lobbyId\)",
				RegexOptions.CultureInvariant).Count);
		string reconnectSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "NetworkManager.Reconnect.cs"));
		True(reconnectSource.Contains("StartSteamLobbyJoin(_steamLobbyId);", StringComparison.Ordinal));
		False(reconnectSource.Contains("Call(\"joinLobby\", _steamLobbyId)", StringComparison.Ordinal));
		string lifecycleSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "NetworkManager.MatchLifecycle.cs"));
		True(lifecycleSource.Contains("_pendingSteamLobbyJoinId = 0;", StringComparison.Ordinal));
		True(lifecycleSource.Contains("_acceptedSteamLobbyJoinId = 0;", StringComparison.Ordinal));
		Equal(
			1,
			Regex.Matches(
				completionMethod,
				@"TryCreateSteamPeer\s*\(\s*server:\s*false",
				RegexOptions.CultureInvariant).Count);
		Equal(
			1,
			Regex.Matches(
				source,
				@"TryCreateSteamPeer\s*\(\s*server:\s*false",
				RegexOptions.CultureInvariant).Count);
	}

	private static void NetworkReconnectRulesRejectSteamOwnerMigrationDuringReconnect()
	{
		True(NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			NetworkSessionState.Reconnecting,
			originalLobbyOwnerSteamId: 123,
			currentLobbyOwnerSteamId: 456));

		False(NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			NetworkSessionState.Joining,
			originalLobbyOwnerSteamId: 123,
			currentLobbyOwnerSteamId: 456));

		False(NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			NetworkSessionState.Reconnecting,
			originalLobbyOwnerSteamId: 123,
			currentLobbyOwnerSteamId: 123));

		False(NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			NetworkSessionState.Reconnecting,
			originalLobbyOwnerSteamId: 0,
			currentLobbyOwnerSteamId: 456));

		False(NetworkReconnectRules.ShouldAbortSteamReconnectForLobbyOwnerChange(
			NetworkSessionState.Reconnecting,
			originalLobbyOwnerSteamId: 123,
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

	private static void NetworkReconnectPresentationReturnsLocalizationKeys()
	{
		Equal("NETWORK_STATE_AI_TAKEOVER", NetworkReconnectPresentationRules.GetSeatControlStatusLocalizationKey(
			SeatControlMode.AiProxyReclaimable));
		Equal("NETWORK_STATE_CONTROL_PENDING", NetworkReconnectPresentationRules.GetSeatControlStatusLocalizationKey(
			SeatControlMode.HumanReclaimPending));
		Equal(string.Empty, NetworkReconnectPresentationRules.GetSeatControlStatusLocalizationKey(
			SeatControlMode.HumanConnected));
		Equal("NETWORK_STATE_SYNCING_MATCH", NetworkReconnectPresentationRules.GetReconnectOverlayLocalizationKey());
		Equal("NETWORK_STATE_RECONNECTED_NOTICE", NetworkReconnectPresentationRules.GetReconnectNoticeLocalizationKey());
		Equal("NETWORK_STATE_MIGRATING_HOST", NetworkReconnectPresentationRules.GetMigrationOverlayLocalizationKey());
		Equal("NETWORK_STATE_MIGRATION_FAILED_RETURNING", NetworkReconnectPresentationRules.GetMigrationFailureLocalizationKey());
	}

	private static void NetworkUserErrorCategoriesMapToStableLocalizationKeys()
	{
		var expected = new Dictionary<NetworkUserErrorCategory, string>
		{
			[NetworkUserErrorCategory.Connection] = "NETWORK_ERROR_CONNECTION_FAILED",
			[NetworkUserErrorCategory.Lan] = "NETWORK_ERROR_LAN_UNAVAILABLE",
			[NetworkUserErrorCategory.Steam] = "NETWORK_ERROR_STEAM_UNAVAILABLE",
			[NetworkUserErrorCategory.RoomAuthentication] = "NETWORK_ERROR_ROOM_AUTHENTICATION_FAILED",
			[NetworkUserErrorCategory.RoomUnavailable] = "NETWORK_ERROR_ROOM_UNAVAILABLE",
			[NetworkUserErrorCategory.RoomFull] = "NETWORK_ERROR_ROOM_FULL",
			[NetworkUserErrorCategory.SceneLoad] = "NETWORK_ERROR_SCENE_LOAD_FAILED",
			[NetworkUserErrorCategory.Reconnect] = "NETWORK_ERROR_RECONNECT_FAILED",
			[NetworkUserErrorCategory.HostDisconnected] = "NETWORK_ERROR_HOST_DISCONNECTED",
			[NetworkUserErrorCategory.HostMigration] = "NETWORK_ERROR_HOST_MIGRATION_FAILED",
			[NetworkUserErrorCategory.Checkpoint] = "NETWORK_ERROR_CHECKPOINT_FAILED",
		};
		Equal(Enum.GetValues<NetworkUserErrorCategory>().Length, expected.Count);
		foreach ((NetworkUserErrorCategory category, string localizationKey) in expected)
		{
			Equal(localizationKey, NetworkUserErrorRules.GetLocalizationKey(category));
			Equal(localizationKey, NetworkUserErrorRules.NormalizeLocalizationKey(localizationKey, NetworkUserErrorCategory.Connection));
		}
		Equal(
			"NETWORK_ERROR_RECONNECT_FAILED",
			NetworkUserErrorRules.NormalizeLocalizationKey("raw_remote_error", NetworkUserErrorCategory.Reconnect));
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
			True(partialText.Contains("private void InitializeSteamPlatformAdapter()", StringComparison.Ordinal));
			True(File.ReadAllText(Path.Combine(repoRoot, "scripts", "SteamPlatformAdapter.cs"))
				.Contains("public sealed class SteamPlatformAdapter : ISteamPlatformAdapter", StringComparison.Ordinal));
			True(partialText.Contains("private void RefreshSteamLobbyAdvertisedState()", StringComparison.Ordinal));
		False(mainText.Contains("public void HostSteam(", StringComparison.Ordinal));
		False(mainText.Contains("private void TryConnectSteamSignals()", StringComparison.Ordinal));
		False(mainText.Contains("private void OnSteamLobbyJoined(", StringComparison.Ordinal));
		False(mainText.Contains("private bool TryReadSteamRoom(", StringComparison.Ordinal));
			False(mainText.Contains("GetSteamSingleton()", StringComparison.Ordinal));
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

	private static void NetworkManagerLobbyResponsibilitiesMoveIntoPartialFiles()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "NetworkManager.cs");
		string oldPartialPath = Path.Combine(repoRoot, "scripts", "NetworkManager.Lobby.cs");
		string membershipPath = Path.Combine(repoRoot, "scripts", "NetworkManager.LobbyMembership.cs");
		string lifecyclePath = Path.Combine(repoRoot, "scripts", "NetworkManager.MatchLifecycle.cs");
		string handshakePath = Path.Combine(repoRoot, "scripts", "NetworkManager.SceneLoadHandshake.cs");
		string mainText = File.ReadAllText(mainPath);

		False(File.Exists(oldPartialPath));
		True(File.Exists(membershipPath));
		True(File.Exists(lifecyclePath));
		True(File.Exists(handshakePath));
		string membershipText = File.ReadAllText(membershipPath);
		string lifecycleText = File.ReadAllText(lifecyclePath);
		string handshakeText = File.ReadAllText(handshakePath);

		True(membershipText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(membershipText.Contains("public void SetLocalReady(bool isReady)", StringComparison.Ordinal));
		True(membershipText.Contains("public Godot.Collections.Array GetRoomsVariant()", StringComparison.Ordinal));
		True(membershipText.Contains("private void RpcRegisterPlayer(", StringComparison.Ordinal));
		True(membershipText.Contains("private void BroadcastPlayerList()", StringComparison.Ordinal));
		True(membershipText.Contains("private void RpcSyncLobby(", StringComparison.Ordinal));
		True(membershipText.Contains(
			"authorityEpoch != migrationPlan.TargetEpoch",
			StringComparison.Ordinal));

		True(lifecycleText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(lifecycleText.Contains("public void StartMatch()", StringComparison.Ordinal));
		True(lifecycleText.Contains("public bool RequestReturnToLobbyAfterMatch()", StringComparison.Ordinal));
		True(lifecycleText.Contains("public void LeaveSession()", StringComparison.Ordinal));
			True(lifecycleText.Contains("public Task RequestSessionCloseAsync(NetworkSessionCloseReason reason)", StringComparison.Ordinal));

		True(handshakeText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(handshakeText.Contains("public void NotifyLocalBoardReady()", StringComparison.Ordinal));
		True(handshakeText.Contains("private void MarkClientSceneLoaded(", StringComparison.Ordinal));
		True(handshakeText.Contains("private Godot.Collections.Dictionary BuildBoardInitializePayload()", StringComparison.Ordinal));
		True(handshakeText.Contains("private void PollLoadHandshake(double now)", StringComparison.Ordinal));
		True(handshakeText.Contains("private void ReturnToMultiplayerLobbyScene(", StringComparison.Ordinal));
		False(mainText.Contains("public void SetLocalReady(bool isReady)", StringComparison.Ordinal));
		False(mainText.Contains("public void StartMatch()", StringComparison.Ordinal));
		False(mainText.Contains("public void LeaveSession()", StringComparison.Ordinal));
		False(mainText.Contains("private void RpcRegisterPlayer(", StringComparison.Ordinal));
		False(mainText.Contains("private void BroadcastPlayerList()", StringComparison.Ordinal));
		False(mainText.Contains("private void MarkClientSceneLoaded(", StringComparison.Ordinal));
		False(mainText.Contains("private void PollLoadHandshake(double now)", StringComparison.Ordinal));
	}

	private static void NetworkManagerP1BOrderingStaysOnHotPaths()
	{
		string repoRoot = FindRepositoryRoot();
		string reconnectText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "NetworkManager.Reconnect.cs"));
		string authorityText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "NetworkManager.AuthorityActions.cs"));

		int ensureStart = reconnectText.IndexOf("private void EnsureLobbySeatControlStates()", StringComparison.Ordinal);
		int ensureEnd = reconnectText.IndexOf("private void InsertSeatControlStateInSeatOrder", ensureStart, StringComparison.Ordinal);
		True(ensureStart >= 0);
		True(ensureEnd > ensureStart);
		if (ensureStart < 0 || ensureEnd <= ensureStart)
		{
			return;
		}

		string ensureMethod = reconnectText[ensureStart..ensureEnd];
		False(ensureMethod.Contains("_seatControlStates.Sort(", StringComparison.Ordinal));
		True(ensureMethod.Contains("InsertSeatControlStateInSeatOrder(state)", StringComparison.Ordinal));

		int snapshotStart = reconnectText.IndexOf("internal void ApplySeatControlStatesFromAuthoritySnapshot(", StringComparison.Ordinal);
		int snapshotEnd = reconnectText.IndexOf("private bool TryGetSeatControlState(", snapshotStart, StringComparison.Ordinal);
		True(snapshotStart >= 0);
		True(snapshotEnd > snapshotStart);
		if (snapshotStart < 0 || snapshotEnd <= snapshotStart)
		{
			return;
		}

		string snapshotMethod = reconnectText[snapshotStart..snapshotEnd];
		False(snapshotMethod.Contains("OrderBy(", StringComparison.Ordinal));
		True(snapshotMethod.Contains("foreach (SeatControlState state in states)", StringComparison.Ordinal));

		int claimsStart = authorityText.IndexOf("internal List<NetworkAuthorityReconnectClaim> GetCheckpointReconnectClaims()", StringComparison.Ordinal);
		int claimsEnd = authorityText.IndexOf("internal void ResetClientActionIds()", claimsStart, StringComparison.Ordinal);
		True(claimsStart >= 0);
		True(claimsEnd > claimsStart);
		if (claimsStart < 0 || claimsEnd <= claimsStart)
		{
			return;
		}

		string claimsMethod = authorityText[claimsStart..claimsEnd];
		True(claimsMethod.Contains("foreach (SeatControlState state in _seatControlStates)", StringComparison.Ordinal));
		True(claimsMethod.Contains("TryGetValue(state.SeatIndex", StringComparison.Ordinal));
		True(claimsMethod.Contains("OrderBy(pair => pair.Key)", StringComparison.Ordinal));
	}

	private static void NetworkManagerBoardProtocolHelpersMoveIntoResponsibilityPartials()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "NetworkManager.cs");
		string oldPartialPath = Path.Combine(repoRoot, "scripts", "NetworkManager.BoardSync.cs");
		string replicationPath = Path.Combine(repoRoot, "scripts", "NetworkManager.BoardReplication.cs");
		string requestsPath = Path.Combine(repoRoot, "scripts", "NetworkManager.BoardRequests.cs");
		string recoveryPath = Path.Combine(repoRoot, "scripts", "NetworkManager.SessionRecoveryRpc.cs");
		string mainText = File.ReadAllText(mainPath);

		False(File.Exists(oldPartialPath));
		True(File.Exists(replicationPath));
		True(File.Exists(requestsPath));
		True(File.Exists(recoveryPath));
		string replicationText = File.ReadAllText(replicationPath);
		string requestsText = File.ReadAllText(requestsPath);
		string recoveryText = File.ReadAllText(recoveryPath);

		True(replicationText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(replicationText.Contains("internal void BroadcastCardPlayedEventFromHost(", StringComparison.Ordinal));
			True(replicationText.Contains("public void SendBoardSnapshotsFromHost(int snapshotId)", StringComparison.Ordinal));
			True(replicationText.Contains("INetworkAuthorityEndpoint", StringComparison.Ordinal));
			True(replicationText.Contains("INetworkReplicaEndpoint", StringComparison.Ordinal));
		True(replicationText.Contains("private void RpcCardPlayedEvent(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		True(replicationText.Contains("private void RpcBoardSnapshot(Godot.Collections.Dictionary snapshot)", StringComparison.Ordinal));

		True(requestsText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(requestsText.Contains("private void RpcRequestPlayHandCard(", StringComparison.Ordinal));
		True(requestsText.Contains("private void RpcRequestPlayItemCard(", StringComparison.Ordinal));
		True(requestsText.Contains("private void RpcRequestPendingItemSelection(", StringComparison.Ordinal));
		True(requestsText.Contains("private void RpcRejectBoardAction(", StringComparison.Ordinal));

		True(recoveryText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(recoveryText.Contains("private void RpcReconnectPlayer(", StringComparison.Ordinal));
		True(recoveryText.Contains("private void RpcReceiveSeatCredential(", StringComparison.Ordinal));
		True(recoveryText.Contains("private void RpcSeatCredentialAck(", StringComparison.Ordinal));
		True(recoveryText.Contains("private void RpcReconnectResult(", StringComparison.Ordinal));
		True(recoveryText.Contains("private void RpcHostEndedMatch(", StringComparison.Ordinal));
		False(mainText.Contains("internal void BroadcastCardPlayedEventFromHost(", StringComparison.Ordinal));
			False(mainText.Contains("public void SendBoardSnapshotsFromHost(", StringComparison.Ordinal));
		False(mainText.Contains("private void RpcRequestPlayHandCard(int handIndex, int turnSequenceGeneration)", StringComparison.Ordinal));
		False(mainText.Contains("private void RpcReconnectPlayer(string matchId, string reconnectToken, string playerSessionId)", StringComparison.Ordinal));
		False(mainText.Contains("private void RpcBoardSnapshot(Godot.Collections.Dictionary snapshot)", StringComparison.Ordinal));
	}

	private static void GameBoardNetworkSnapshotHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.Network.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkSnapshot.cs");
		string adapterPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.RuleSessionAdapter.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);
		string adapterText = File.ReadAllText(adapterPath);

		True(partialText.Contains("public partial class GameBoardScene", StringComparison.Ordinal));
		True(partialText.Contains("internal Godot.Collections.Dictionary BuildNetworkBoardSnapshotForSeat", StringComparison.Ordinal));
		True(partialText.Contains("internal void ApplyNetworkBoardSnapshot(Godot.Collections.Dictionary snapshot)", StringComparison.Ordinal));
		True(partialText.Contains("private void ApplyNetworkBoardSnapshotNow(", StringComparison.Ordinal));
		False(partialText.Contains("private void ApplyHandsFromSnapshot(", StringComparison.Ordinal));
		False(partialText.Contains("private void ApplyItemHandsFromSnapshot(", StringComparison.Ordinal));
		False(partialText.Contains("private void ApplyTableCardsFromSnapshot(", StringComparison.Ordinal));
		False(partialText.Contains("private void ApplyPendingSelectionFromSnapshot(", StringComparison.Ordinal));
		False(partialText.Contains("_pendingItemSelectionMode =", StringComparison.Ordinal));
		False(partialText.Contains("_pendingItemOwnerIndex =", StringComparison.Ordinal));
		True(adapterText.Contains("private static MandatorySelectionState CreateReplicaMandatorySelection(", StringComparison.Ordinal));
		True(adapterText.Contains("MandatorySelection: mandatorySelection", StringComparison.Ordinal));
		True(adapterText.Contains("_replicaRuleSession.ApplyAuthoritySnapshot(ruleSnapshot)", StringComparison.Ordinal));
		True(partialText.Contains("private void ApplySeatControlStatesFromSnapshot(", StringComparison.Ordinal));
		False(mainText.Contains("internal Godot.Collections.Dictionary BuildNetworkBoardSnapshotForSeat", StringComparison.Ordinal));
		False(mainText.Contains("private void ApplyNetworkBoardSnapshotNow(", StringComparison.Ordinal));
		False(mainText.Contains("private void ApplyHandsFromSnapshot(", StringComparison.Ordinal));
		False(mainText.Contains("private void ApplySeatControlStatesFromSnapshot(", StringComparison.Ordinal));
	}

	private static void NetworkReplicaVisualsDoNotMutateRuleCollections()
	{
		string repoRoot = FindRepositoryRoot();
		string boardText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.cs"));
		string cardsText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkCards.cs"));
		string predictionText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkPrediction.cs"));
		string visualQueueText = string.Concat(
			Directory.GetFiles(
				Path.Combine(repoRoot, "scripts"),
				"GameBoardScene.NetworkVisualQueue*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));
		string snapshotText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkSnapshot.cs"));
		string controllerText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkVisualPlaybackController.cs"));
		string debugText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.Debug.Network.cs"));
		string authorityStateText = string.Concat(
			Directory.GetFiles(
				Path.Combine(repoRoot, "scripts"),
				"GameBoardScene.NetworkAuthority*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));

		True(boardText.Contains(
			"private IReadOnlyList<IReadOnlyList<PlayingCard>> _playerHands =>",
			StringComparison.Ordinal));
		True(boardText.Contains(
			"? presentation.PlayerHands",
			StringComparison.Ordinal));
		True(boardText.Contains(
			": CurrentRuleProjection.PlayerHands;",
			StringComparison.Ordinal));
		True(boardText.Contains(
			"private IReadOnlyList<TableCard> _tableCards =>",
			StringComparison.Ordinal));
		True(boardText.Contains(
			": CurrentRuleProjection.TableCards;",
			StringComparison.Ordinal));
		False(boardText.Contains("GameBoardState _state", StringComparison.Ordinal));
		True(cardsText.Contains(
			"private sealed class NetworkReplicaPresentationState",
			StringComparison.Ordinal));
		True(cardsText.Contains(
			"IReadOnlyList<IReadOnlyList<PlayingCard>> playerDeckSource",
			StringComparison.Ordinal));
		True(cardsText.Contains(
			"TableCard.Restore(",
			StringComparison.Ordinal));
		True(controllerText.Contains(
			"if (!EnsureNetworkReplicaPresentationState())",
			StringComparison.Ordinal));
		True(snapshotText.Contains(
			"if (!TryApplyReplicaRuleSnapshot(snapshot))",
			StringComparison.Ordinal));
		True(debugText.Contains(
			"DebugUseReplicaRuleSession(preserveCurrentStateForPresentation: true);",
			StringComparison.Ordinal));
		True(debugText.Contains(
			"TryApplyReplicaRuleSnapshot(validatedSnapshot)",
			StringComparison.Ordinal));
		True(authorityStateText.Contains(
			"TrySubmitGrantItemToHandRuleCommand(",
			StringComparison.Ordinal));
		False(Regex.IsMatch(
			authorityStateText,
			@"_playerItemHands\s*\[[^\r\n;]+\]\s*\.(?:Add|AddRange|Insert|Remove|RemoveAt|RemoveRange|Clear)\s*\("));

		string replicaMutationSources = string.Concat(
			predictionText,
			visualQueueText,
			snapshotText);
		foreach (string forbiddenMutation in new[]
		{
			"_tableCards.Add(",
			"_tableCards.AddRange(",
			"_tableCards.Insert(",
			"_tableCards.RemoveAt(",
			"_tableCards.RemoveRange(",
			"_tableCards.Clear(",
			"_playerDecks[",
			"_playerHands[",
			"_playerItemHands[",
		})
		{
			if (forbiddenMutation is "_playerDecks[" or "_playerHands[" or "_playerItemHands[")
			{
				continue;
			}
			False(replicaMutationSources.Contains(forbiddenMutation, StringComparison.Ordinal));
		}

		False(Regex.IsMatch(
			predictionText + visualQueueText,
			@"_(?:playerDecks|playerHands|playerItemHands)\s*\[[^\r\n;]+\]\s*\.(?:Add|AddRange|Insert|Remove|RemoveAt|RemoveRange|Clear)\s*\("));
		False(Regex.IsMatch(
			predictionText + visualQueueText,
			@"_(?:playerHands|playerItemHands|tableCards)\s*\[[^\r\n;]+\]\s*="));
		False(snapshotText.Contains("ApplyValidatedCountArray(", StringComparison.Ordinal));
		False(snapshotText.Contains("ApplyHandsFromSnapshot(", StringComparison.Ordinal));
		False(snapshotText.Contains("ApplyItemHandsFromSnapshot(", StringComparison.Ordinal));
		False(snapshotText.Contains("ApplyTableCardsFromSnapshot(", StringComparison.Ordinal));
		False(predictionText.Contains("AttachMagnetToHandCard(", StringComparison.Ordinal));
		False(visualQueueText.Contains("Shuffle(_player", StringComparison.Ordinal));
	}

	private static void NetworkRpcSignaturesRemainProtocolV11Compatible()
	{
		string root = FindRepositoryRoot();
		string networkSources = string.Join(
			'\n',
			Directory.GetFiles(Path.Combine(root, "scripts"), "NetworkManager*.cs")
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(File.ReadAllText));
		string[] actual = Regex.Matches(
				networkSources,
				@"\bprivate\s+void\s+(Rpc[A-Za-z0-9_]+)\s*\((.*?)\)\s*\{",
				RegexOptions.Singleline | RegexOptions.CultureInvariant)
			.Select(match =>
			{
				string parameters = Regex.Replace(match.Groups[2].Value, @"\s+", " ").Trim();
				parameters = Regex.Replace(parameters, @"\s*,\s*", ",");
				return $"{match.Groups[1].Value}({parameters})";
			})
			.OrderBy(signature => signature, StringComparer.Ordinal)
			.ToArray();
		string[] expected =
		{
			"RpcBeginHostMigration(string sessionId,string matchId,long sourceEpoch,long targetEpoch,long checkpointId,string checkpointHash)",
			"RpcBeginSceneLoad(string sessionId,string matchId,long authorityEpoch,Godot.Collections.Dictionary bootstrap)",
			"RpcBeginSecurityHandshake(string sessionId,int protocolVersion,byte[] hostKeyAgreementPublicKey,byte[] sourceAuthorityPublicKey,int passwordVerifierVersion,int passwordVerifierIterations,string passwordVerifierSalt,byte[] passwordChallenge,bool hostMigrationEnabled,bool rekeyExistingPeer)",
			"RpcBoardActionCommitted(string sessionId,string matchId,long authorityEpoch,long actionId,long clientActionId,long checkpointId,int snapshotId,int turnSequenceGeneration)",
			"RpcBoardSnapshot(Godot.Collections.Dictionary snapshot)",
			"RpcCancelMatchStart(string sessionId,string matchId,long authorityEpoch,string localizationKey)",
			"RpcCardPlayedEvent(Godot.Collections.Dictionary payload)",
			"RpcClaimMigratedLobbySeat(string sessionId,long authorityEpoch,string playerSessionId,long credentialGeneration,string credential)",
			"RpcClientSceneLoaded(string sessionId,string matchId,long authorityEpoch,int seatIndex)",
			"RpcCompleteSecurityHandshake(string sessionId,int protocolVersion,byte[] clientKeyAgreementPublicKey,byte[] successorPublicKey,byte[] passwordChallenge,byte[] passwordProof,bool trustedHotStandbyAccepted,bool transportTakeoverCapable)",
			"RpcConfirmHostMigration(string sessionId,string matchId,long sourceEpoch,long targetEpoch,long checkpointId,string playerSessionId)",
			"RpcDebugStaleMigrationPlan(Godot.Collections.Dictionary rawPlan)",
			"RpcHostEndedMatch(string sessionId,string matchId,long authorityEpoch,string localizationKey)",
			"RpcInitializeBoard(string sessionId,string matchId,long authorityEpoch,Godot.Collections.Dictionary payload)",
			"RpcLobbyRejected(string sessionId,string matchId,long authorityEpoch,string localizationKey)",
			"RpcMigrationFollowerSnapshotApplied(string sessionId,string matchId,long authorityEpoch,int seatIndex,string playerSessionId)",
			"RpcMigrationLatencyProbe(string sessionId,string matchId,long authorityEpoch,long sequence)",
			"RpcMigrationLatencyProbeAck(string sessionId,string matchId,long authorityEpoch,long sequence)",
			"RpcMigrationPlan(Godot.Collections.Dictionary rawPlan)",
			"RpcMigrationPlanAck(string sessionId,string matchId,long sourceEpoch,long targetEpoch,long planRevision,long checkpointId,string checkpointCiphertextHash,string candidatePlayerSessionId,string candidateSuccessorKeyId)",
			"RpcMigrationPlanRevoked(Godot.Collections.Dictionary rawRevocation)",
			"RpcMigrationReady(string sessionId,string matchId,long authorityEpoch)",
			"RpcReceiveSeatCredential(string sessionId,string matchId,long authorityEpoch,string playerSessionId,long credentialGeneration,string credential)",
			"RpcReconnectPlayer(string sessionId,string matchId,long authorityEpoch,string playerSessionId,long credentialGeneration,string credential)",
			"RpcReconnectResult(string sessionId,string matchId,long authorityEpoch,bool accepted,string reason,int seatIndex)",
			"RpcRegisterPlayer(string displayName,string avatarId,string identityHint)",
			"RpcRejectBoardAction(string sessionId,string matchId,long authorityEpoch,string reason,int turnSequenceGeneration,int rejectedHandIndex)",
			"RpcRequestClientInfo()",
			"RpcRequestPendingItemSelection(string sessionId,string matchId,long authorityEpoch,int selectionIndex,int turnSequenceGeneration,long clientActionId)",
			"RpcRequestPlayHandCard(int handIndex,int turnSequenceGeneration)",
			"RpcRequestPlayHandCard(string sessionId,string matchId,long authorityEpoch,int handIndex,int turnSequenceGeneration,long clientActionId)",
			"RpcRequestPlayItemCard(string sessionId,string matchId,long authorityEpoch,int itemHandIndex,int turnSequenceGeneration,long clientActionId)",
			"RpcRequestReturnToLobbyAfterMatch(string sessionId,string matchId,long authorityEpoch)",
			"RpcReturnToLobbyAfterMatch(string completedSessionId,string completedMatchId,long completedAuthorityEpoch,Godot.Collections.Dictionary config,Godot.Collections.Array players)",
			"RpcRevokeHotStandby(string sessionId,string matchId,long authorityEpoch,string candidatePlayerSessionId)",
			"RpcSeatCredentialAck(string sessionId,string matchId,long authorityEpoch,string playerSessionId,long credentialGeneration,string credential)",
			"RpcSeatCredentialCommitted(string sessionId,string matchId,long authorityEpoch,string playerSessionId,long credentialGeneration)",
			"RpcSecurityHandshakeAccepted(string sessionId,int protocolVersion,bool hostMigrationEnabled,bool rekeyExistingPeer)",
			"RpcSetReady(string sessionId,string matchId,long authorityEpoch,bool isReady)",
			"RpcStandbyCheckpoint(string sessionId,string matchId,long authorityEpoch,long checkpointId,long planRevision,string candidatePlayerSessionId,byte[] nonce,byte[] ciphertext,byte[] tag,string plaintextHash,string ciphertextHash,string previousCheckpointHash,string sourceAuthorityKeyId,byte[] signature)",
			"RpcStandbyCheckpointAck(string sessionId,string matchId,long authorityEpoch,bool accepted,long checkpointId,long planRevision,string ciphertextHash,string error,string candidateHostAddress)",
			"RpcSyncLobby(string sessionId,string matchId,long authorityEpoch,Godot.Collections.Dictionary config,Godot.Collections.Array players)",
		};

		Equal(string.Join('\n', expected), string.Join('\n', actual));
	}

	private static void NetworkSnapshotRuleProjectionPreservesCompatibilityAndPrivacy()
	{
		string root = FindRepositoryRoot();
		string snapshotSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.NetworkSnapshot.cs"));
		string adapterSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.RuleSessionAdapter.cs"));
		int authorityBuildStart = snapshotSource.IndexOf(
			"internal Godot.Collections.Dictionary BuildNetworkBoardSnapshotForSeat(",
			StringComparison.Ordinal);
		int buildStart = snapshotSource.IndexOf(
			"private Godot.Collections.Dictionary BuildNetworkBoardSnapshotFromReadModelForSeat(",
			authorityBuildStart,
			StringComparison.Ordinal);
		int buildEnd = snapshotSource.IndexOf(
			"private enum RulePlayerZone",
			buildStart,
			StringComparison.Ordinal);
		True(authorityBuildStart >= 0 && buildStart > authorityBuildStart && buildEnd > buildStart);
		string authorityBuildBody = snapshotSource[authorityBuildStart..buildStart];
		string buildBody = snapshotSource[buildStart..buildEnd];
		string[] actualKeys = Regex.Matches(buildBody, "\\[\"([^\"]+)\"\\]\\s*=")
			.Select(match => match.Groups[1].Value)
			.ToArray();
		string[] expectedKeys =
		{
			"protocol_version",
			"session_id",
			"match_id",
			"authority_epoch",
			"checkpoint_id",
			"snapshot_id",
			"turn_sequence_generation",
			"current_turn_player_index",
			"match_outcome_kind",
			"winner_player_index",
			"scores",
			"deck_counts",
			"hand_counts",
			"item_hand_counts",
			"own_item_hand_cards",
			"table_cards",
			"own_hand_cards",
			"pending_selection_mode",
			"pending_selection_owner",
			"pending_item_card_id",
			"pending_turn_sequence_generation",
			"opening_deal_animation_playing",
			"opening_deal_completed",
			"seat_control_states",
			"authority_action",
		};
		True(expectedKeys.SequenceEqual(actualKeys, StringComparer.Ordinal));

		True(authorityBuildBody.Contains(
			"CurrentRuleReadModel is not { IsInitialized: true } readModel",
			StringComparison.Ordinal));
		True(buildBody.Contains(
			"BuildOwnHandCardsArray(readModel, recipientSeatIndex)",
			StringComparison.Ordinal));
		True(buildBody.Contains(
			"BuildOwnItemHandCardsArray(readModel, recipientSeatIndex)",
			StringComparison.Ordinal));
		True(buildBody.Contains(
			"BuildTableCardsArray(readModel.TableCards, recipientSeatIndex)",
			StringComparison.Ordinal));
		False(buildBody.Contains("_playerDecks", StringComparison.Ordinal));
		False(buildBody.Contains("_playerHands", StringComparison.Ordinal));
		False(buildBody.Contains("_playerItemHands", StringComparison.Ordinal));
		False(buildBody.Contains("_tableCards", StringComparison.Ordinal));
		False(buildBody.Contains("RandomState", StringComparison.Ordinal));

		True(snapshotSource.Contains(
			"tableCard.IsFaceUp || tableCard.CardOwnerSeat == recipientSeatIndex",
			StringComparison.Ordinal));
		True(snapshotSource.Contains(
			"canSeeCard ? tableCard.Card!.Value.Code : string.Empty",
			StringComparison.Ordinal));
		True(snapshotSource.Contains(
			"GetCompatiblePendingItemCardId(",
			StringComparison.Ordinal));
		True(snapshotSource.Contains(
			"MatchesCompatiblePendingDiscardItemId(",
			StringComparison.Ordinal));
		True(snapshotSource.Contains(
			"NetworkAuthorityActionVariantCodec.ToVariant(_networkAuthorityActionState)",
			StringComparison.Ordinal));
		True(adapterSource.Contains(
			"kind == MandatorySelectionKind.HandLimitDiscard",
			StringComparison.Ordinal));
		True(adapterSource.Contains(
			"? string.Empty",
			StringComparison.Ordinal));
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

	private static void TargetItemSelectionCapabilityIsRemoved()
	{
		string repoRoot = FindRepositoryRoot();
		string path = Path.Combine(repoRoot, "scripts", "GameBoardScene.TargetItemSelection.cs");
		string text = File.ReadAllText(path);

		False(text.Contains("CompletePendingItemSelection", StringComparison.Ordinal));
		False(text.Contains("ShowTargetPlayerSelection", StringComparison.Ordinal));
		False(text.Contains("ShowTargetHandSelection", StringComparison.Ordinal));
		True(text.Contains("PendingItemSelectionMode.HandLimitDiscard", StringComparison.Ordinal));
		True(text.Contains("PendingItemSelectionMode.AttachmentRewardHandCard", StringComparison.Ordinal));
	}

	private static void NetworkManagerSceneLoadAckValidatesSenderBeforeMarkingLoaded()
	{
		string repoRoot = FindRepositoryRoot();
		string path = Path.Combine(repoRoot, "scripts", "NetworkManager.SceneLoadHandshake.cs");
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
		string playbackPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkVisualQueue.Playback.cs");
		string controllerPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkVisualPlaybackController.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);
		True(File.Exists(playbackPath));
		string playbackText = File.ReadAllText(playbackPath);
		True(File.Exists(controllerPath));
		string controllerText = File.ReadAllText(controllerPath);

		True(partialText.Contains("public partial class GameBoardScene", StringComparison.Ordinal));
		True(partialText.Contains("internal void QueueNetworkCardPlayedEvent(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		True(partialText.Contains("private sealed record NetworkVisualEvent", StringComparison.Ordinal));
		True(partialText.Contains("private bool TryDecodeNetworkVisualEvent", StringComparison.Ordinal));
		True(playbackText.Contains("private async Task PlayNetworkCardPlayedEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		True(playbackText.Contains("private async Task PlayNetworkItemCardVisualEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		True(playbackText.Contains("private async Task PlayNetworkCaptureRevealVisualEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		True(partialText.Contains("NetworkCaptureRevealEntry[] CaptureRevealEntries", StringComparison.Ordinal));
		True(controllerText.Contains("private sealed class NetworkVisualPlaybackController", StringComparison.Ordinal));
		True(controllerText.Contains("private readonly Queue<NetworkVisualEvent> _pendingEvents", StringComparison.Ordinal));
		True(controllerText.Contains("private async Task ProcessAsync()", StringComparison.Ordinal));
		True(controllerText.Contains("IsFastForwarding = true", StringComparison.Ordinal));
		True(controllerText.Contains("_pendingEvents.Clear()", StringComparison.Ordinal));
		False(partialText.Contains("IsUsableNetworkVisualEvent(payload", StringComparison.Ordinal));
		False(partialText.Contains("= ReadNetworkInt(payload", StringComparison.Ordinal));
		False(partialText.Contains("= ReadNetworkString(payload", StringComparison.Ordinal));
		False(partialText.Contains("= ReadArray(payload", StringComparison.Ordinal));
		False(partialText.Contains("= ReadIntArray(payload", StringComparison.Ordinal));
		False(partialText.Contains("Split(',', StringSplitOptions", StringComparison.Ordinal));
		False(mainText.Contains("internal void QueueNetworkCardPlayedEvent(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		False(mainText.Contains("private async Task PlayNetworkCardPlayedEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		False(mainText.Contains("private async Task PlayNetworkCaptureRevealVisualEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		False(partialText.Contains("private async Task PlayNetworkCardPlayedEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		False(partialText.Contains("private async Task PlayNetworkCaptureRevealVisualEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		False(partialText.Contains("private async Task ProcessNetworkVisualQueueAsync()", StringComparison.Ordinal));
	}

	private static void NetworkVisualQueueAdmitsOnlyBoundedPendingEvents()
	{
		for (int pendingEventCount = 0; pendingEventCount < NetworkVisualQueuePolicy.MaxPendingEventCount; pendingEventCount++)
		{
			Equal(
				NetworkVisualQueueAdmission.Enqueue,
				NetworkVisualQueuePolicy.ClassifyAdmission(pendingEventCount, fastForwarding: false));
		}

		Equal(
			NetworkVisualQueueAdmission.EnterFastForward,
			NetworkVisualQueuePolicy.ClassifyAdmission(
				NetworkVisualQueuePolicy.MaxPendingEventCount,
				fastForwarding: false));
		Equal(
			NetworkVisualQueueAdmission.EnterFastForward,
			NetworkVisualQueuePolicy.ClassifyAdmission(int.MaxValue, fastForwarding: false));
	}

	private static void NetworkVisualQueueEntersAndRemainsInFastForward()
	{
		Equal(
			NetworkVisualQueueAdmission.DropWhileFastForwarding,
			NetworkVisualQueuePolicy.ClassifyAdmission(0, fastForwarding: true));
		Equal(
			NetworkVisualQueueAdmission.DropWhileFastForwarding,
			NetworkVisualQueuePolicy.ClassifyAdmission(
				NetworkVisualQueuePolicy.MaxPendingEventCount,
				fastForwarding: true));

		True(NetworkVisualQueuePolicy.ClassifyAdmission(0, false) == NetworkVisualQueueAdmission.Enqueue);
		True(NetworkVisualQueuePolicy.ClassifyAdmission(0, false) != NetworkVisualQueueAdmission.DropWhileFastForwarding);
	}

	private static void NetworkVisualQueueSaturatingCountersStayNonnegative()
	{
		Equal(64, NetworkVisualQueuePolicy.SaturatingAdd(0, 64));
		Equal(12, NetworkVisualQueuePolicy.SaturatingAdd(12, 0));
		Equal(12, NetworkVisualQueuePolicy.SaturatingAdd(12, -4));
		Equal(int.MaxValue, NetworkVisualQueuePolicy.SaturatingAdd(int.MaxValue - 1, 64));
		True(NetworkVisualQueuePolicy.SaturatingAdd(0, int.MaxValue) >= 0);
	}

	private static void NetworkVisualPlaybackFailuresStayIsolatedFromAuthority()
	{
		string repoRoot = FindRepositoryRoot();
		string controllerText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkVisualPlaybackController.cs"));
		string queueText = string.Concat(
			Directory.GetFiles(
				Path.Combine(repoRoot, "scripts"),
				"GameBoardScene.NetworkVisualQueue*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));
		string transactionText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkAuthorityTransaction.cs"));
		string replicationText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"NetworkManager.BoardReplication.cs"));
		string lifetimeText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"BoardAsyncLifetime.cs"));
		string tableCaptureFlowText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.TableCaptureFlow.cs"));
		string tableCardTriggersText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.TableCardTriggers.cs"));
		string baseCardEnchantsText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.BaseCardEnchants.cs"));
		string turnEndText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.TurnEndState.cs"));
		string resultsText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.Results.cs"));
		string smokePath = Path.Combine(
			repoRoot,
			"tests",
			"NetworkVisualFailureIsolationSmoke.gd");
		string boundsSmokePath = Path.Combine(
			repoRoot,
			"tests",
			"NetworkSnapshotBoundsSmoke.gd");

		True(controllerText.Contains("PlaybackFailureCount", StringComparison.Ordinal));
		True(controllerText.Contains("catch (Exception error)", StringComparison.Ordinal));
		True(controllerText.Contains("_recoverPlaybackFailure(visualEvent)", StringComparison.Ordinal));
		True(controllerText.Contains("IsFastForwarding = true", StringComparison.Ordinal));
		True(queueText.Contains("ActionIdKey", StringComparison.Ordinal));
		True(queueText.Contains("IsAuthorityHostVisualOnly", StringComparison.Ordinal));
		True(queueText.Contains("PlayNetworkAuthorityHostVisualEventAsync", StringComparison.Ordinal));
		True(transactionText.Contains("_networkAuthorityStagedHostVisualEvents", StringComparison.Ordinal));
		True(transactionText.Contains("QueueCommittedNetworkAuthorityHostVisualEvents", StringComparison.Ordinal));
		True(transactionText.Contains("_networkAuthorityRuleCenterArenaGlobalPosition = _centerArena.GlobalPosition", StringComparison.Ordinal));
		True(transactionText.Contains("_networkAuthorityRuleTableLayoutRect = GetTableCardLayoutRect()", StringComparison.Ordinal));
		True(transactionText.Contains("PresentCommittedNetworkAuthorityMatchOutcome();", StringComparison.Ordinal));
		True(replicationText.Contains("StageBoardVisualEventForLocalHost", StringComparison.Ordinal));
		True(replicationText.Contains("if (LocalSeatIndex != actorSeatIndex)", StringComparison.Ordinal));
		True(lifetimeText.Contains("!tween.IsValid()", StringComparison.Ordinal));
		True(tableCaptureFlowText.Contains("GetAuthorityRuleSafeTableLayoutRect()", StringComparison.Ordinal));
		False(tableCaptureFlowText.Contains("_centerArena.GlobalPosition", StringComparison.Ordinal));
		True(tableCardTriggersText.Contains("centerArenaGlobalPosition + GetTableCardPosition", StringComparison.Ordinal));
		False(baseCardEnchantsText.Contains("_centerArena.GlobalPosition + GetTableCardPosition", StringComparison.Ordinal));
		True(turnEndText.Contains("PresentCommittedNetworkAuthorityMatchOutcome()", StringComparison.Ordinal));
		True(turnEndText.Contains("if (!_networkAuthorityRuleTransactionActive", StringComparison.Ordinal));
		True(resultsText.Contains("if (_networkAuthorityRuleTransactionActive)", StringComparison.Ordinal));
		True(File.Exists(smokePath));
		string smokeText = File.ReadAllText(smokePath);
		True(smokeText.Contains("DebugInjectNetworkVisualPlaybackFailures", StringComparison.Ordinal));
		True(smokeText.Contains("DebugKillTrackedBoardTweens", StringComparison.Ordinal));
		True(smokeText.Contains("queue_free()", StringComparison.Ordinal));
		True(File.Exists(boundsSmokePath));
		string boundsSmokeText = File.ReadAllText(boundsSmokePath);
		True(boundsSmokeText.Contains("Accepted visual event without action ID", StringComparison.Ordinal));
		True(boundsSmokeText.Contains("Accepted visual event with negative action ID", StringComparison.Ordinal));
		True(boundsSmokeText.Contains("Accepted visual event with non-integer action ID", StringComparison.Ordinal));
		True(File.Exists(Path.Combine(
			repoRoot,
			"tests",
			"run_network_visual_failure_isolation_smoke.sh")));
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
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("K", CollectsTable: false, HasDoubleEffect: false),
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
			PlayerSessionId: "stable-20",
			Credential: "token-1");
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
			PlayerSessionId: "stable-20",
			Credential: "token-1");
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
			PlayerSessionId: "stable-20",
			Credential: "token-1");
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
			PlayerSessionId: "stable-20",
			Credential: "token-1");
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
			PlayerSessionId: "stable-55",
			Credential: "token-2");

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
			PlayerSessionId: "stable-55",
			Credential: "token-2");

		False(SeatControlRules.TryPromoteAtTurnStart(state, 1, out SeatControlState next));
		Equal(state, next);
	}

	private static void SeatControlAllowsReclaimAgainAfterReclaimPendingDisconnect()
	{
		SeatControlState reclaimPending = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 33,
			PlayerSessionId: "stable-20",
			Credential: "token-1");
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
			PlayerSessionId: "stable-20",
			Credential: "token-1");
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
		Equal(ReconnectClaimRejectReason.CredentialMismatch, wrongToken.RejectReason);

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
		Equal(ReconnectClaimRejectReason.PlayerSessionIdMismatch, wrongUser.RejectReason);
	}

	private static void SeatControlRejectsReconnectClaimAfterMatchEnd()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.AiProxyReclaimable,
			PeerId: 0,
			PlayerSessionId: "stable-20",
			Credential: "token-1");
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

	private static void SeatControlSharedSnapshotRedactsCredentialVerifier()
	{
		SeatControlState state = new(
			SeatIndex: 1,
			Mode: SeatControlMode.HumanReclaimPending,
			PeerId: 33,
			PlayerSessionId: "stable-20",
			Credential: "token-1");

		SeatControlState shared = SeatControlRules.RedactCredentialVerifiersForSharedSnapshot(state);

		Equal(state.SeatIndex, shared.SeatIndex);
		Equal(state.Mode, shared.Mode);
		Equal(state.PeerId, shared.PeerId);
		Equal(state.PlayerSessionId, shared.PlayerSessionId);
		Equal(string.Empty, shared.CredentialVerifier);
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


	private static void CardEventBusScopesTriggerLimitsToOneDispatch()
	{
		var bus = CreateCardTriggerDispatchSession();
		var gameEvent = new CardGameEvent(
			CardGameEvents.OnPlay,
			sourceTableCardIndex: 3);

		long firstDispatchId = bus.BeginCardTriggerDispatch();
		bus.PublishCardTriggerEvent(
			firstDispatchId,
			gameEvent,
			new FixedCardTriggerProvider(5));
		int firstDispatchCount = 0;
		while (bus.TryTakeCardTrigger(firstDispatchId, out _))
		{
			firstDispatchCount++;
		}
		Equal(4, firstDispatchCount);
		True(bus.EndCardTriggerDispatch(firstDispatchId));

		for (int dispatchIndex = 0; dispatchIndex < 5; dispatchIndex++)
		{
			long dispatchId = bus.BeginCardTriggerDispatch();
			bus.PublishCardTriggerEvent(
				dispatchId,
				gameEvent,
				new FixedCardTriggerProvider(1));
			True(bus.TryTakeCardTrigger(
				dispatchId,
				out CardTriggerRequest request));
			Equal(0, request.Sequence);
			False(bus.TryTakeCardTrigger(dispatchId, out _));
			True(bus.EndCardTriggerDispatch(dispatchId));
		}

		long drainedDispatchId = bus.BeginCardTriggerDispatch();
		bus.PublishCardTriggerEvent(
			drainedDispatchId,
			gameEvent,
			new FixedCardTriggerProvider(1));
		True(bus.TryTakeCardTrigger(drainedDispatchId, out _));
		Equal(0, bus.PendingCardTriggerCount);
		bool beginThrew = false;
		try
		{
			bus.BeginCardTriggerDispatch();
		}
		catch (InvalidOperationException)
		{
			beginThrew = true;
		}

		True(beginThrew);
		True(bus.EndCardTriggerDispatch(drainedDispatchId));
	}

	private static void CardEventBusEndsAbortedAndFailedDispatches()
	{
		var bus = CreateCardTriggerDispatchSession();
		var gameEvent = new CardGameEvent(CardGameEvents.OnPlay);

		long abortedDispatchId = bus.BeginCardTriggerDispatch();
		try
		{
			bus.PublishCardTriggerEvent(
				abortedDispatchId,
				gameEvent,
				new FixedCardTriggerProvider(3));
			True(bus.TryTakeCardTrigger(abortedDispatchId, out _));
		}
		finally
		{
			True(bus.EndCardTriggerDispatch(abortedDispatchId));
		}
		Equal(0, bus.PendingCardTriggerCount);

		bool resolverThrew = false;
		long failedDispatchId = bus.BeginCardTriggerDispatch();
		try
		{
			try
			{
				bus.PublishCardTriggerEvent(
					failedDispatchId,
					gameEvent,
					new FixedCardTriggerProvider(3));
				True(bus.TryTakeCardTrigger(failedDispatchId, out _));
				throw new InvalidOperationException(
					"simulated resolver failure");
			}
			finally
			{
				True(bus.EndCardTriggerDispatch(failedDispatchId));
			}
		}
		catch (InvalidOperationException exception)
			when (exception.Message == "simulated resolver failure")
		{
			resolverThrew = true;
		}
		True(resolverThrew);
		Equal(0, bus.PendingCardTriggerCount);

		long replacementDispatchId = bus.BeginCardTriggerDispatch();
		bus.PublishCardTriggerEvent(
			replacementDispatchId,
			gameEvent,
			new FixedCardTriggerProvider(1));
		True(bus.TryTakeCardTrigger(
			replacementDispatchId,
			out CardTriggerRequest replacementRequest));
		Equal(0, replacementRequest.Sequence);
		True(bus.EndCardTriggerDispatch(replacementDispatchId));
	}

	private static void CardEventBusRejectsStaleDispatchOwnership()
	{
		var bus = CreateCardTriggerDispatchSession();
		var gameEvent = new CardGameEvent(CardGameEvents.OnPlay);
		long staleDispatchId = bus.BeginCardTriggerDispatch();
		bus.PublishCardTriggerEvent(
			staleDispatchId,
			gameEvent,
			new FixedCardTriggerProvider(2));
		bus.ClearCardTriggerDispatch();
		Equal(0, bus.PendingCardTriggerCount);

		long currentDispatchId = bus.BeginCardTriggerDispatch();
		bus.PublishCardTriggerEvent(
			currentDispatchId,
			gameEvent,
			new FixedCardTriggerProvider(1));
		bool stalePublishThrew = false;
		try
		{
			bus.PublishCardTriggerEvent(
				staleDispatchId,
				gameEvent,
				new FixedCardTriggerProvider(1));
		}
		catch (InvalidOperationException)
		{
			stalePublishThrew = true;
		}
		True(stalePublishThrew);

		bool staleDequeueThrew = false;
		try
		{
			bus.TryTakeCardTrigger(staleDispatchId, out _);
		}
		catch (InvalidOperationException)
		{
			staleDequeueThrew = true;
		}
		True(staleDequeueThrew);
		False(bus.EndCardTriggerDispatch(staleDispatchId));
		True(bus.TryTakeCardTrigger(
			currentDispatchId,
			out CardTriggerRequest currentRequest));
		Equal(0, currentRequest.Sequence);
		True(bus.EndCardTriggerDispatch(currentDispatchId));
	}

	private static void CardEventResolverEndsItsOwnedDispatchInFinally()
	{
		string path = Path.Combine(
			FindRepositoryRoot(),
			"scripts",
			"GameBoardScene.SpecialCards.cs");
		string source = File.ReadAllText(path);
		int start = source.IndexOf(
			"private async Task<PlayCardResult> PublishAndResolveCardEventAsync(",
			StringComparison.Ordinal);
		True(start >= 0);
		int end = source.IndexOf(
			"private async Task<PlayCardResult> ResolveItemCardOnPlayAsync(",
			start,
			StringComparison.Ordinal);
		True(end > start);

		string method = source[start..end];
		True(Regex.IsMatch(
			method,
			@"try\s*\{[\s\S]*?ruleSession\.PublishCardTriggerEvent\(dispatchId,[\s\S]*?\}\s*finally\s*\{\s*ruleSession\.EndCardTriggerDispatch\(dispatchId\);\s*\}",
			RegexOptions.CultureInvariant));
	}

	private static void NetworkMigrationCoreUsesTransportAdapters()
	{
		string migrationSource = string.Concat(
			Directory.GetFiles(
				Path.Combine(FindRepositoryRoot(), "scripts"),
				"NetworkManager.Migration*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));
		string transportSource = File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"scripts",
			"HostMigrationTransport.cs"));

		Equal(1, Regex.Matches(
			migrationSource,
			@"new LanHostMigrationTransport\(\)",
			RegexOptions.CultureInvariant).Count);
		False(migrationSource.Contains("migrationPlan.CandidateHostAddress", StringComparison.Ordinal));
		False(migrationSource.Contains("plan.CandidateHostAddress", StringComparison.Ordinal));
		True(migrationSource.Contains("PrepareMigrationTransport(plan, now", StringComparison.Ordinal));
		True(migrationSource.Contains("CanRebuildMigrationPeer(", StringComparison.Ordinal));
		True(migrationSource.Contains("ArmMigrationAttemptsAfterSourceDisconnect(", StringComparison.Ordinal));
		True(migrationSource.Contains("FinishFailedActiveHostMigrationAsync(", StringComparison.Ordinal));
		True(migrationSource.Contains("MethodName.RpcHostEndedMatch", StringComparison.Ordinal));
		True(migrationSource.Contains("[\"transport_descriptor\"]", StringComparison.Ordinal));
		True(migrationSource.Contains("\"expected_lobby_owner_steam_id\"", StringComparison.Ordinal));
			True(transportSource.Contains("SetLobbyOwner", StringComparison.Ordinal));
		True(transportSource.Contains("_getObservedLobbyOwnerSteamId()", StringComparison.Ordinal));
		True(transportSource.Contains("host_with_lobby", StringComparison.Ordinal));
		True(transportSource.Contains("connect_to_lobby", StringComparison.Ordinal));
	}

		private static void GameMigrationRecoveryRuntimeIsFencedByDisabledCapability()
		{
			string root = FindRepositoryRoot();
			string managerSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.cs"));
			string securitySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Security.cs"));
			string maintenanceSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Maintenance.cs"));
			string migrationPath = Path.Combine(root, "scripts", "NetworkManager.Migration.Readiness.cs");
			string migrationSource = File.ReadAllText(migrationPath);
			string migrationEntrySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Migration.cs"));
			string reconnectSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Reconnect.cs"));
			string snapshotSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.NetworkSnapshot.cs"));

			True(securitySource.Contains("#if DEBUG", StringComparison.Ordinal));
			True(securitySource.Contains("OS.GetEnvironment(\"CODEX_LAN_MIGRATION_FIXTURE\") == \"1\"", StringComparison.Ordinal));
			True(securitySource.Contains("#else\n\t\t\treturn false;\n#endif", StringComparison.Ordinal));
			True(maintenanceSource.Contains(
				"ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.MigrationReadiness, HostMigrationCapabilityAvailable",
				StringComparison.Ordinal));
			True(maintenanceSource.Contains(
				"HostMigrationCapabilityAvailable && IsHost && _gameMigrationRecoveryState",
				StringComparison.Ordinal));
			int rpcStart = migrationSource.IndexOf("private void RpcMigrationFollowerSnapshotApplied(", StringComparison.Ordinal);
			int rpcEnd = migrationSource.IndexOf("private bool IsMigrationReadinessFenceActive()", rpcStart, StringComparison.Ordinal);
			True(rpcStart >= 0 && rpcEnd > rpcStart);
			True(migrationSource[rpcStart..rpcEnd].Contains("if (!HostMigrationCapabilityAvailable", StringComparison.Ordinal));
			False(migrationEntrySource.Contains("private void RpcMigrationFollowerSnapshotApplied(", StringComparison.Ordinal));
			True(reconnectSource.Contains("HostMigrationCapabilityAvailable", StringComparison.Ordinal));
			True(reconnectSource.Contains("MethodName.RpcMigrationFollowerSnapshotApplied", StringComparison.Ordinal));
			True(snapshotSource.Contains("networkManager.NotifyReconnectSnapshotApplied(_networkMatchId);", StringComparison.Ordinal));
			int disconnectStart = managerSource.IndexOf("private void OnServerDisconnected()", StringComparison.Ordinal);
			int disconnectEnd = managerSource.IndexOf("private void InsertPlayerInSeatOrder", disconnectStart, StringComparison.Ordinal);
			True(disconnectStart >= 0 && disconnectEnd > disconnectStart);
			False(managerSource[disconnectStart..disconnectEnd].Contains("BeginMigrationAfterHostDisconnect", StringComparison.Ordinal));
		}

}
