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
