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
	private static void SceneLoadCoordinatorCompletesOnceAndRejectsDuplicates()
	{
		var coordinator = new NetworkSceneLoadCoordinator();
		coordinator.Start("match", new[] { 0, 1 }, timeoutSeconds: 30, startedAtSeconds: 10);

		Equal(NetworkSceneLoadCoordinatorState.AwaitingAcks, coordinator.State);
		Equal(
			NetworkSceneLoadAckResult.Accepted,
			coordinator.Acknowledge("match", 0, NetworkSceneLoadSeatAvailability.Connected));
		Equal(
			NetworkSceneLoadAckResult.Duplicate,
			coordinator.Acknowledge("match", 0, NetworkSceneLoadSeatAvailability.Connected));
		Equal(
			NetworkSceneLoadAckResult.Completed,
			coordinator.Acknowledge("match", 1, NetworkSceneLoadSeatAvailability.Connected));
		Equal(NetworkSceneLoadCoordinatorState.Completed, coordinator.State);
		Equal(
			NetworkSceneLoadAckResult.Duplicate,
			coordinator.Acknowledge("match", 1, NetworkSceneLoadSeatAvailability.Connected));
		Equal(NetworkSceneLoadPollResult.Inactive, coordinator.Poll(50));
	}

	private static void SceneLoadCoordinatorRejectsInvalidAcks()
	{
		var coordinator = new NetworkSceneLoadCoordinator();
		coordinator.Start("match", new[] { 0, 1 }, timeoutSeconds: 30, startedAtSeconds: 10);

		Equal(
			NetworkSceneLoadAckResult.UnknownSeat,
			coordinator.Acknowledge("match", 0, NetworkSceneLoadSeatAvailability.Unknown));
		Equal(
			NetworkSceneLoadAckResult.OfflineSeat,
			coordinator.Acknowledge("match", 0, NetworkSceneLoadSeatAvailability.Offline));
		Equal(
			NetworkSceneLoadAckResult.StaleMatch,
			coordinator.Acknowledge("stale", 0, NetworkSceneLoadSeatAvailability.Connected));
		Equal(
			NetworkSceneLoadAckResult.UnexpectedSeat,
			coordinator.Acknowledge("match", 2, NetworkSceneLoadSeatAvailability.Connected));
		Equal(0, coordinator.LoadedSeats.Count);
	}

	private static void SceneLoadCoordinatorTimesOutAndResets()
	{
		var coordinator = new NetworkSceneLoadCoordinator();
		coordinator.Start("match", new[] { 0, 1 }, timeoutSeconds: 30, startedAtSeconds: 10);

		Equal(40.0, coordinator.DeadlineSeconds);
		Equal(NetworkSceneLoadPollResult.Waiting, coordinator.Poll(39.999));
		Equal(NetworkSceneLoadPollResult.TimedOut, coordinator.Poll(40));
		Equal(NetworkSceneLoadCoordinatorState.TimedOut, coordinator.State);
		Equal(NetworkSceneLoadPollResult.Inactive, coordinator.Poll(41));

		coordinator.Reset();
		Equal(NetworkSceneLoadCoordinatorState.Inactive, coordinator.State);
		Equal(null, coordinator.DeadlineSeconds);
		Equal(0, coordinator.ExpectedSeats.Count);
		Equal(0, coordinator.LoadedSeats.Count);
	}

	private static void NetworkLobbyRosterExposesReadOnlyViewsAndOrderedCommands()
	{
		var roster = new NetworkLobbyRoster();
		NetworkPlayerPublicState seatOne = CreateRosterPlayer(22, "player-b", 1);
		NetworkPlayerPublicState seatZero = CreateRosterPlayer(11, "player-a", 0);

		roster.InsertPlayerInSeatOrder(seatOne);
		roster.InsertPlayerInSeatOrder(seatZero);
		Equal(2, roster.Players.Count);
		Equal("player-a", roster.Players[0].PlayerSessionId);
		Equal("player-b", roster.Players[1].PlayerSessionId);
		ExpectThrows<NotSupportedException>(() =>
			((IList<NetworkPlayerPublicState>)roster.Players).Add(CreateRosterPlayer(33, "player-c", 2)));

		roster.SetPendingReady(22, true);
		True(roster.TryTakePendingReady(22, out bool isReady));
		True(isReady);
		False(roster.TryTakePendingReady(22, out _));
	}

	private static void NetworkLobbyRosterCompactsRelatedSeatStateAtomically()
	{
		var roster = new NetworkLobbyRoster();
		NetworkPlayerPublicState host = CreateRosterPlayer(1, "host", 0);
		NetworkPlayerPublicState removed = CreateRosterPlayer(22, "removed", 1);
		NetworkPlayerPublicState kept = CreateRosterPlayer(33, "kept", 2);
		NetworkPlayerPublicState[] players = { host, removed, kept };
		SeatControlState[] controls = players
			.Select(player => SeatControlRules.CreateInitialState(player, $"verifier-{player.SeatIndex}"))
			.ToArray();
		var claims = controls.ToDictionary(
			state => state.SeatIndex,
			state => SeatControlRules.CreateInitialClaimRecord("match", state));
		var credentials = new Dictionary<int, string>
		{
			[1] = "removed-credential",
			[2] = "kept-credential",
		};
		roster.ReplaceRosterState(players, controls, claims, credentials);

		roster.CompactSeatMappings(new[] { host, kept });

		Equal(2, roster.Players.Count);
		Equal("host", roster.Players[0].PlayerSessionId);
		Equal(0, roster.Players[0].SeatIndex);
		Equal("kept", roster.Players[1].PlayerSessionId);
		Equal(1, roster.Players[1].SeatIndex);
		Equal(1, roster.SeatControls[1].SeatIndex);
		Equal("kept", roster.SeatControls[1].PlayerSessionId);
		True(roster.ReconnectClaimsBySeat.TryGetValue(1, out ReconnectClaimRecord compactClaim));
		Equal("kept", compactClaim.PlayerSessionId);
		Equal(1, compactClaim.SeatIndex);
		Equal("kept-credential", roster.IssuedSeatCredentialsBySeat[1]);
		False(roster.IssuedSeatCredentialsBySeat.ContainsKey(2));
	}

	private static void NetworkReconnectCoordinatorRejectsDuplicateStartsAndSchedulesRetries()
	{
		var coordinator = new NetworkReconnectCoordinator();
		var context = new NetworkReconnectContext("session", "match", 3);

		True(coordinator.Begin(context, nowSeconds: 10));
		False(coordinator.Begin(context, nowSeconds: 11));
		Equal(20.0, coordinator.WindowDeadlineSeconds);
		Equal(10.0, coordinator.RetryDeadlineSeconds);
		Equal(
			NetworkReconnectPollAction.StartAttempt,
			coordinator.Poll(10, peerBlocksAttempt: false, enableAttemptTimeout: false, attemptTimeoutSeconds: 2));
		Equal(NetworkReconnectCoordinatorState.AttemptInFlight, coordinator.State);
		Equal(
			NetworkReconnectTransitionResult.Accepted,
			coordinator.ScheduleRetry(context, nowSeconds: 11));
		Equal(13.0, coordinator.RetryDeadlineSeconds);
		Equal(
			NetworkReconnectPollAction.Waiting,
			coordinator.Poll(12.999, peerBlocksAttempt: false, enableAttemptTimeout: false, attemptTimeoutSeconds: 2));
		Equal(
			NetworkReconnectPollAction.StartAttempt,
			coordinator.Poll(13, peerBlocksAttempt: false, enableAttemptTimeout: false, attemptTimeoutSeconds: 2));
	}

	private static void NetworkReconnectCoordinatorFencesConfirmationAndSnapshotContext()
	{
		var coordinator = new NetworkReconnectCoordinator();
		var context = new NetworkReconnectContext("session", "match", 3);
		var staleContext = context with { MatchId = "stale" };
		coordinator.Begin(context, nowSeconds: 10);
		coordinator.Poll(10, peerBlocksAttempt: false, enableAttemptTimeout: false, attemptTimeoutSeconds: 2);

		Equal(
			NetworkReconnectTransitionResult.StaleContext,
			coordinator.MarkAwaitingConfirmation(staleContext, nowSeconds: 11));
		Equal(
			NetworkReconnectTransitionResult.Accepted,
			coordinator.MarkAwaitingConfirmation(context, nowSeconds: 11));
		Equal(
			NetworkReconnectTransitionResult.InvalidState,
			coordinator.MarkAwaitingConfirmation(context, nowSeconds: 11));
		Equal(
			NetworkReconnectTransitionResult.StaleContext,
			coordinator.ReceiveConfirmation(staleContext, accepted: true, nowSeconds: 12));
		Equal(
			NetworkReconnectTransitionResult.Accepted,
			coordinator.ReceiveConfirmation(context, accepted: true, nowSeconds: 12));
		Equal(NetworkReconnectCoordinatorState.AwaitingSnapshot, coordinator.State);
		Equal(
			NetworkReconnectTransitionResult.InvalidState,
			coordinator.ReceiveConfirmation(context, accepted: true, nowSeconds: 12));
		Equal(
			NetworkReconnectTransitionResult.StaleContext,
			coordinator.CompleteSnapshot(staleContext, nowSeconds: 13));
		Equal(
			NetworkReconnectTransitionResult.Accepted,
			coordinator.CompleteSnapshot(context, nowSeconds: 13));
		Equal(NetworkReconnectCoordinatorState.Completed, coordinator.State);
	}

	private static void NetworkReconnectCoordinatorExpiresAttemptAndWindowOnce()
	{
		var coordinator = new NetworkReconnectCoordinator();
		var context = new NetworkReconnectContext("session", "match", 3);
		coordinator.Begin(context, nowSeconds: 10);
		coordinator.Poll(10, peerBlocksAttempt: false, enableAttemptTimeout: true, attemptTimeoutSeconds: 2);

		Equal(
			NetworkReconnectPollAction.Waiting,
			coordinator.Poll(11.999, peerBlocksAttempt: false, enableAttemptTimeout: true, attemptTimeoutSeconds: 2));
		Equal(
			NetworkReconnectPollAction.AttemptTimedOut,
			coordinator.Poll(12, peerBlocksAttempt: false, enableAttemptTimeout: true, attemptTimeoutSeconds: 2));
		Equal(NetworkReconnectCoordinatorState.AttemptTimedOut, coordinator.State);
		Equal(
			NetworkReconnectPollAction.Inactive,
			coordinator.Poll(13, peerBlocksAttempt: false, enableAttemptTimeout: true, attemptTimeoutSeconds: 2));

		coordinator.Reset();
		True(coordinator.Begin(context, nowSeconds: 20));
		Equal(
			NetworkReconnectPollAction.WindowExpired,
			coordinator.Poll(30, peerBlocksAttempt: false, enableAttemptTimeout: false, attemptTimeoutSeconds: 2));
		Equal(NetworkReconnectCoordinatorState.WindowExpired, coordinator.State);
		Equal(
			NetworkReconnectTransitionResult.Inactive,
			coordinator.MarkAwaitingConfirmation(context, nowSeconds: 30));
	}

	private static NetworkPlayerPublicState CreateRosterPlayer(
		int peerId,
		string playerSessionId,
		int seatIndex)
	{
		return new NetworkPlayerPublicState(
			peerId,
			playerSessionId,
			playerSessionId,
			"default",
			seatIndex,
			seatIndex == 0,
			seatIndex == 0,
			true,
			true)
		{
			CredentialsAcknowledged = true,
		};
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
			True(snapshotSource.Contains("networkManager.NotifyReconnectSnapshotApplied(network_match_id);", StringComparison.Ordinal));
			int disconnectStart = managerSource.IndexOf("private void OnServerDisconnected()", StringComparison.Ordinal);
			int disconnectEnd = managerSource.IndexOf("private void InsertPlayerInSeatOrder", disconnectStart, StringComparison.Ordinal);
			True(disconnectStart >= 0 && disconnectEnd > disconnectStart);
			False(managerSource[disconnectStart..disconnectEnd].Contains("BeginMigrationAfterHostDisconnect", StringComparison.Ordinal));
		}

}
