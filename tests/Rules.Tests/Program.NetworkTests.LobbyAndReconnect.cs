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

}
