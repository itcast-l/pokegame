using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

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
		True(beginMethod.Contains("steam?.Call(\"joinLobby\", lobbyId);", StringComparison.Ordinal));
		Equal(
			2,
			Regex.Matches(
				completionMethod,
				@"ClearAcceptedSteamLobbyJoinIfNeeded\(lobbyId\)",
				RegexOptions.CultureInvariant).Count);
		string reconnectSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "NetworkManager.Reconnect.cs"));
		True(reconnectSource.Contains("StartSteamLobbyJoin(_steamLobbyId);", StringComparison.Ordinal));
		False(reconnectSource.Contains("Call(\"joinLobby\", _steamLobbyId)", StringComparison.Ordinal));
		string lobbySource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "NetworkManager.Lobby.cs"));
		True(lobbySource.Contains("_pendingSteamLobbyJoinId = 0;", StringComparison.Ordinal));
		True(lobbySource.Contains("_acceptedSteamLobbyJoinId = 0;", StringComparison.Ordinal));
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
		True(partialText.Contains("private void RpcRequestPlayHandCard(", StringComparison.Ordinal));
		True(partialText.Contains("private void RpcReconnectPlayer(", StringComparison.Ordinal));
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
		string controllerPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkVisualPlaybackController.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);
		True(File.Exists(controllerPath));
		string controllerText = File.ReadAllText(controllerPath);

		True(partialText.Contains("public partial class GameBoardScene", StringComparison.Ordinal));
		True(partialText.Contains("internal void QueueNetworkCardPlayedEvent(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		True(partialText.Contains("private sealed record NetworkVisualEvent", StringComparison.Ordinal));
		True(partialText.Contains("private bool TryDecodeNetworkVisualEvent", StringComparison.Ordinal));
		True(partialText.Contains("private async Task PlayNetworkCardPlayedEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		True(partialText.Contains("private async Task PlayNetworkItemCardVisualEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		True(partialText.Contains("private async Task PlayNetworkCaptureRevealVisualEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
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


	private static void CardEventBusScopesTriggerLimitsToOneDispatch()
	{
		var bus = new CardEventBus();
		var gameEvent = new CardGameEvent(
			CardGameEvents.OnPlay,
			sourceTableCardIndex: 3);

		long firstDispatchId = bus.BeginDispatch();
		bus.Publish(
			firstDispatchId,
			gameEvent,
			new FixedCardTriggerProvider(5));
		int firstDispatchCount = 0;
		while (bus.TryDequeue(firstDispatchId, out _))
		{
			firstDispatchCount++;
		}
		Equal(4, firstDispatchCount);
		True(bus.EndDispatch(firstDispatchId));

		for (int dispatchIndex = 0; dispatchIndex < 5; dispatchIndex++)
		{
			long dispatchId = bus.BeginDispatch();
			bus.Publish(
				dispatchId,
				gameEvent,
				new FixedCardTriggerProvider(1));
			True(bus.TryDequeue(
				dispatchId,
				out CardTriggerRequest request));
			Equal(0, request.Sequence);
			False(bus.TryDequeue(dispatchId, out _));
			True(bus.EndDispatch(dispatchId));
		}

		long drainedDispatchId = bus.BeginDispatch();
		bus.Publish(
			drainedDispatchId,
			gameEvent,
			new FixedCardTriggerProvider(1));
		True(bus.TryDequeue(drainedDispatchId, out _));
		Equal(0, bus.PendingCount);
		bool beginThrew = false;
		try
		{
			bus.BeginDispatch();
		}
		catch (InvalidOperationException)
		{
			beginThrew = true;
		}

		True(beginThrew);
		True(bus.EndDispatch(drainedDispatchId));
	}

	private static void CardEventBusEndsAbortedAndFailedDispatches()
	{
		var bus = new CardEventBus();
		var gameEvent = new CardGameEvent(CardGameEvents.OnPlay);

		long abortedDispatchId = bus.BeginDispatch();
		try
		{
			bus.Publish(
				abortedDispatchId,
				gameEvent,
				new FixedCardTriggerProvider(3));
			True(bus.TryDequeue(abortedDispatchId, out _));
		}
		finally
		{
			True(bus.EndDispatch(abortedDispatchId));
		}
		Equal(0, bus.PendingCount);

		bool resolverThrew = false;
		long failedDispatchId = bus.BeginDispatch();
		try
		{
			try
			{
				bus.Publish(
					failedDispatchId,
					gameEvent,
					new FixedCardTriggerProvider(3));
				True(bus.TryDequeue(failedDispatchId, out _));
				throw new InvalidOperationException(
					"simulated resolver failure");
			}
			finally
			{
				True(bus.EndDispatch(failedDispatchId));
			}
		}
		catch (InvalidOperationException exception)
			when (exception.Message == "simulated resolver failure")
		{
			resolverThrew = true;
		}
		True(resolverThrew);
		Equal(0, bus.PendingCount);

		long replacementDispatchId = bus.BeginDispatch();
		bus.Publish(
			replacementDispatchId,
			gameEvent,
			new FixedCardTriggerProvider(1));
		True(bus.TryDequeue(
			replacementDispatchId,
			out CardTriggerRequest replacementRequest));
		Equal(0, replacementRequest.Sequence);
		True(bus.EndDispatch(replacementDispatchId));
	}

	private static void CardEventBusRejectsStaleDispatchOwnership()
	{
		var bus = new CardEventBus();
		var gameEvent = new CardGameEvent(CardGameEvents.OnPlay);
		long staleDispatchId = bus.BeginDispatch();
		bus.Publish(
			staleDispatchId,
			gameEvent,
			new FixedCardTriggerProvider(2));
		bus.Clear();
		Equal(0, bus.PendingCount);

		long currentDispatchId = bus.BeginDispatch();
		bus.Publish(
			currentDispatchId,
			gameEvent,
			new FixedCardTriggerProvider(1));
		bool stalePublishThrew = false;
		try
		{
			bus.Publish(
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
			bus.TryDequeue(staleDispatchId, out _);
		}
		catch (InvalidOperationException)
		{
			staleDequeueThrew = true;
		}
		True(staleDequeueThrew);
		False(bus.EndDispatch(staleDispatchId));
		True(bus.TryDequeue(
			currentDispatchId,
			out CardTriggerRequest currentRequest));
		Equal(0, currentRequest.Sequence);
		True(bus.EndDispatch(currentDispatchId));
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
			@"try\s*\{[\s\S]*?_cardEventBus\.Publish\(dispatchId,[\s\S]*?\}\s*finally\s*\{\s*_cardEventBus\.EndDispatch\(dispatchId\);\s*\}",
			RegexOptions.CultureInvariant));
	}

}
