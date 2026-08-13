using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Deckland.Rules;

internal static partial class Program
{
	private static void NetworkProtocolContextsFenceEpochsAndSnapshots()
	{
		Equal(11, NetworkProtocol.CurrentVersion);
		Equal(5, NetworkProtocol.AuthorityCheckpointSchemaVersion);
		NetworkRpcContext current = new("session", "match", 3);
		True(current.IsValid);
		True(NetworkProtocolRules.Matches(current, "session", "match", 3, allowHigherEpoch: false));
		False(NetworkProtocolRules.Matches(new NetworkRpcContext("session", "match", 2), "session", "match", 3, allowHigherEpoch: false));
		True(NetworkProtocolRules.Matches(new NetworkRpcContext("session", "match", 4), "session", "match", 3, allowHigherEpoch: true));
		False(NetworkProtocolRules.Matches(new NetworkRpcContext("other", "match", 4), "session", "match", 3, allowHigherEpoch: true));

		True(NetworkProtocolRules.IsNewerFence(4, 0, 3, 99));
		True(NetworkProtocolRules.IsNewerFence(3, 100, 3, 99));
		False(NetworkProtocolRules.IsNewerFence(3, 99, 3, 99));
		False(NetworkProtocolRules.IsNewerFence(2, 100, 3, 99));
	}

	private static void MigrationRestoreGenerationIsStrictlyMonotonic()
	{
		True(TurnGenerationRules.TryGetNextAfterRestore(8, 3, out int fromNewerLocal));
		Equal(9, fromNewerLocal);
		True(TurnGenerationRules.TryGetNextAfterRestore(3, 8, out int fromNewerCheckpoint));
		Equal(9, fromNewerCheckpoint);
		False(TurnGenerationRules.TryGetNextAfterRestore(int.MaxValue, 8, out _));
	}

	private static void MultiplayerRematchAcceptsOnlyCompletedCurrentMatchPlayers()
	{
		NetworkSessionConfig config = new(
			"session",
			"room",
			"secret",
			"match",
			123,
			NetworkTransport.Lan,
			2,
			true,
			100,
			LobbyVisibility.FriendsOnly);
		var players = new List<NetworkPlayerPublicState>
		{
			new(1, "host", "Host", "dolphin", 0, true, true, true, true),
			new(42, "guest", "Guest", "turtle", 1, false, false, true, true),
		};
		NetworkRpcContext current = new("session", "match", 3);

		True(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.InGame,
			current,
			config,
			3,
			players,
			42,
			hasWinner: true));
		False(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.InGame,
			current,
			config,
			3,
			players,
			42,
			hasWinner: false));
		False(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.Lobby,
			current,
			config,
			3,
			players,
			42,
			hasWinner: true));
		False(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.InGame,
			new NetworkRpcContext("session", "old-match", 3),
			config,
			3,
			players,
			42,
			hasWinner: true));
		False(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.InGame,
			current,
			config,
			3,
			players,
			99,
			hasWinner: true));
		players[1] = players[1] with { IsConnected = false };
		False(MultiplayerRematchRules.CanHostAcceptReturnRequest(
			NetworkSessionState.InGame,
			current,
			config,
			3,
			players,
			42,
			hasWinner: true));
	}

	private static void MultiplayerRematchReturnsConnectedReadyPlayersToPreservedLobby()
	{
		NetworkSessionConfig matchConfig = new(
			"session",
			"room",
			"secret",
			"match",
			987,
			NetworkTransport.Steam,
			4,
			false,
			200,
			LobbyVisibility.Private);
		var players = new List<NetworkPlayerPublicState>
		{
			new(1, "host", "Host", "dolphin", 0, true, true, true, true),
			new(20, "offline", "Offline", "turtle", 1, false, false, true, false),
			new(30, "guest", "Guest", "whale", 2, false, false, true, true),
		};

		NetworkSessionConfig lobbyConfig = MultiplayerRematchRules.CreateReturnedLobbyConfig(matchConfig);
		List<NetworkPlayerPublicState> retained = MultiplayerRematchRules.RetainConnectedPlayers(players);

		Equal(matchConfig.SessionId, lobbyConfig.SessionId);
		Equal(matchConfig.RoomName, lobbyConfig.RoomName);
		Equal(matchConfig.RoomPassword, lobbyConfig.RoomPassword);
		Equal(matchConfig.Transport, lobbyConfig.Transport);
		Equal(matchConfig.PlayerCount, lobbyConfig.PlayerCount);
		Equal(matchConfig.ItemMode, lobbyConfig.ItemMode);
		Equal(matchConfig.WinningScore, lobbyConfig.WinningScore);
		Equal(string.Empty, lobbyConfig.MatchId);
		Equal(0L, lobbyConfig.MatchSeed);
		Equal(2, retained.Count);
		True(retained.All(player => player.IsReady && player.IsConnected));
		Equal(0, retained[0].SeatIndex);
		Equal(1, retained[1].SeatIndex);
		Equal("guest", retained[1].PlayerSessionId);
		False(retained.Any(player => player.PlayerSessionId == "offline"));
		True(MultiplayerRematchRules.IsValidAuthorityReturn(matchConfig, lobbyConfig, retained));
		False(MultiplayerRematchRules.IsValidAuthorityReturn(
			matchConfig,
			lobbyConfig with { WinningScore = 50 },
			retained));
	}

	private static void MigrationPlanSelectsLowestEligibleStandby()
	{
		var roster = new List<NetworkMigrationRosterEntry>
		{
			new() { PlayerSessionId = "host", SeatIndex = 0, IsHost = true, IsConnected = true, CredentialsAcknowledged = true },
			new() { PlayerSessionId = "seat-2", SeatIndex = 2, IsConnected = true, CredentialsAcknowledged = true },
			new() { PlayerSessionId = "seat-1", SeatIndex = 1, IsConnected = true, CredentialsAcknowledged = true },
			new() { PlayerSessionId = "seat-3", SeatIndex = 3, IsConnected = true, CredentialsAcknowledged = true },
		};
		var controls = new List<NetworkAuthoritySeatControl>
		{
			new() { SeatIndex = 0, Mode = SeatControlMode.HumanConnected },
			new() { SeatIndex = 1, Mode = SeatControlMode.HumanConnected },
			new() { SeatIndex = 2, Mode = SeatControlMode.AiProxyReclaimable },
			new() { SeatIndex = 3, Mode = SeatControlMode.HumanConnected },
		};

		Equal(1, NetworkMigrationRules.SelectHotStandbySeat(roster, controls, hostSeat: 0));
		Equal(3, NetworkMigrationRules.SelectHotStandbySeat(roster, controls, hostSeat: 0, excludedSeats: new[] { 1 }));
		True(NetworkMigrationRules.ShouldSwitchToNextCandidate(true, NetworkMigrationRules.ConsecutiveUnackedCheckpointsBeforeSwitch));
		True(NetworkMigrationRules.ShouldSwitchToNextCandidate(false, 0));
		False(NetworkMigrationRules.ShouldSwitchToNextCandidate(true, 1));
	}

	private static void MigrationCandidateScorePrefersStableAcknowledgementsThenLatency()
	{
		var roster = new List<NetworkMigrationRosterEntry>
		{
			new() { PlayerSessionId = "host", SeatIndex = 0, IsHost = true, IsConnected = true, CredentialsAcknowledged = true },
			new() { PlayerSessionId = "seat-1", SeatIndex = 1, IsConnected = true, CredentialsAcknowledged = true },
			new() { PlayerSessionId = "seat-2", SeatIndex = 2, IsConnected = true, CredentialsAcknowledged = true },
		};
		var controls = new List<NetworkAuthoritySeatControl>
		{
			new() { SeatIndex = 0, Mode = SeatControlMode.HumanConnected },
			new() { SeatIndex = 1, Mode = SeatControlMode.HumanConnected },
			new() { SeatIndex = 2, Mode = SeatControlMode.HumanConnected },
		};
		var latencyBySeat = new Dictionary<int, double>
		{
			[1] = 80.0,
			[2] = 20.0,
		};

		Equal(2, NetworkMigrationRules.SelectHotStandbySeat(
			roster,
			controls,
			hostSeat: 0,
			excludedSeats: Array.Empty<int>(),
			stableAcknowledgedSeat: -1,
			latencyMillisecondsBySeat: latencyBySeat));
		Equal(1, NetworkMigrationRules.SelectHotStandbySeat(
			roster,
			controls,
			hostSeat: 0,
			excludedSeats: Array.Empty<int>(),
			stableAcknowledgedSeat: 1,
			latencyMillisecondsBySeat: latencyBySeat));
	}

	private static void MigrationPlanRejectsStaleOrUnsafeEndpoints()
	{
		using ECDsa sourceSigner = HostMigrationSecurity.CreateSigner();
		using ECDsa successorSigner = HostMigrationSecurity.CreateSigner();
		byte[] sourcePublicKey = HostMigrationSecurity.ExportPublicKey(sourceSigner);
		byte[] successorPublicKey = HostMigrationSecurity.ExportPublicKey(successorSigner);
		NetworkMigrationPlan unsigned = new()
		{
			ProtocolVersion = NetworkProtocol.CurrentVersion,
			PlanRevision = 1,
			SessionId = "session",
			MatchId = "match",
			SourceEpoch = 3,
			TargetEpoch = 4,
			CheckpointId = 7,
			CheckpointHash = new string('a', 64),
			CheckpointCiphertextHash = new string('b', 64),
			CandidatePlayerSessionId = "seat-1",
			CandidateSeat = 1,
			CandidateSuccessorPublicKey = HostMigrationSecurity.EncodeBytes(successorPublicKey),
			CandidateSuccessorKeyId = HostMigrationSecurity.ComputeKeyId(successorPublicKey),
			SourcePlayerSessionId = "host",
			SourceAuthorityKeyId = HostMigrationSecurity.ComputeKeyId(sourcePublicKey),
			Transport = NetworkTransport.Lan,
			TransportDescriptor = new NetworkMigrationTransportDescriptor
			{
				Lan = new NetworkLanMigrationDescriptor
				{
					ObservedHostAddress = "192.168.1.10",
					CandidateHostAddress = "192.168.1.11",
					Port = NetworkProtocol.LanHostMigrationPort,
					MaxClients = NetworkSessionDefaults.MaxPlayers - 1,
					PeerRebuildMode = NetworkMigrationPeerRebuildMode.EnetServer,
				},
			},
			Phase = NetworkAuthorityPhase.InGame,
		};
		True(HostMigrationSecurity.TrySignMigrationPlan(unsigned, sourceSigner, out NetworkMigrationPlan valid));
		True(NetworkMigrationRules.IsPlanVisibleToClients(valid));
		True(HostMigrationSecurity.VerifyMigrationPlan(valid, sourcePublicKey));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with { TargetEpoch = 3 }));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with { CheckpointHash = new string('A', 64) }));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with
		{
			TransportDescriptor = valid.TransportDescriptor with
			{
				Lan = valid.TransportDescriptor.Lan! with { CandidateHostAddress = "bad address" },
			},
		}));
		False(NetworkMigrationRules.IsPlanVisibleToClients(valid with
		{
			TransportDescriptor = valid.TransportDescriptor with
			{
				Lan = valid.TransportDescriptor.Lan! with { Port = NetworkSessionDefaults.LanGamePort + 1 },
			},
		}));
		False(HostMigrationSecurity.VerifyMigrationPlan(valid with { CandidateSeat = 2 }, sourcePublicKey));
		NetworkMigrationPlan steamUnsigned = unsigned with
		{
			Transport = NetworkTransport.Steam,
			TransportDescriptor = new NetworkMigrationTransportDescriptor
			{
				Steam = new NetworkSteamMigrationDescriptor
				{
					LobbyId = 109775241000000001,
					SourceLobbyOwnerSteamId = 76561198000000001,
					CandidateSteamId = 76561198000000002,
					ExpectedLobbyOwnerSteamId = 76561198000000002,
					PeerRebuildMode = NetworkMigrationPeerRebuildMode.SteamLobby,
				},
			},
		};
		True(HostMigrationSecurity.TrySignMigrationPlan(steamUnsigned, sourceSigner, out NetworkMigrationPlan steamPlan));
		True(NetworkMigrationRules.IsPlanVisibleToClients(steamPlan));
		True(HostMigrationSecurity.VerifyMigrationPlan(steamPlan, sourcePublicKey));
		string steamJson = JsonSerializer.Serialize(steamPlan);
		NetworkMigrationPlan? steamRoundtrip = JsonSerializer.Deserialize<NetworkMigrationPlan>(steamJson);
		True(steamRoundtrip is not null);
		True(steamRoundtrip!.TransportDescriptor.Lan is null);
		Equal(steamPlan.TransportDescriptor.Steam, steamRoundtrip.TransportDescriptor.Steam);
		True(HostMigrationSecurity.VerifyMigrationPlan(steamRoundtrip, sourcePublicKey));
		False(NetworkMigrationRules.IsPlanVisibleToClients(steamPlan with
		{
			TransportDescriptor = steamPlan.TransportDescriptor with
			{
				Steam = steamPlan.TransportDescriptor.Steam! with
				{
					ExpectedLobbyOwnerSteamId = 76561198000000003,
				},
			},
		}));
		False(HostMigrationSecurity.VerifyMigrationPlan(steamPlan with
		{
			TransportDescriptor = steamPlan.TransportDescriptor with
			{
				Steam = steamPlan.TransportDescriptor.Steam! with
				{
					CandidateSteamId = 76561198000000003,
					ExpectedLobbyOwnerSteamId = 76561198000000003,
				},
			},
			}, sourcePublicKey));
		False(HostMigrationSecurity.VerifyMigrationPlan(steamPlan with
		{
			TransportDescriptor = steamPlan.TransportDescriptor with
			{
				Steam = steamPlan.TransportDescriptor.Steam! with
				{
					LobbyId = 109775241000000002,
					SourceLobbyOwnerSteamId = 76561198000000004,
				},
			},
		}, sourcePublicKey));
		NetworkMigrationPlan mismatchedCandidateKey = unsigned with
		{
			CandidateSuccessorKeyId = new string('c', 64),
		};
		False(NetworkMigrationRules.IsUnsignedPlanValid(mismatchedCandidateKey));
		False(HostMigrationSecurity.TrySignMigrationPlan(mismatchedCandidateKey, sourceSigner, out _));
		False(HostMigrationSecurity.TrySignMigrationPlan(
			unsigned with { SourceAuthorityKeyId = new string('d', 64) },
			sourceSigner,
			out _));
		True(NetworkMigrationRules.ShouldAcceptPlanUpdate(null, valid));
		True(NetworkMigrationRules.ShouldAcceptPlanUpdate(valid, valid with { PlanRevision = 2 }));
		True(NetworkMigrationRules.ShouldAcceptPlanUpdate(valid, valid with { TargetEpoch = 5, PlanRevision = 1 }));
		False(NetworkMigrationRules.ShouldAcceptPlanUpdate(valid, valid));
		False(NetworkMigrationRules.ShouldAcceptPlanUpdate(valid, valid with { PlanRevision = 0 }));
		False(NetworkMigrationRules.ShouldAcceptPlanUpdate(valid, valid with { TargetEpoch = 3, PlanRevision = 99 }));
		True(NetworkMigrationRules.CanCandidateStart(valid, "seat-1", NetworkSessionState.MigratingHost, hasCheckpoint: true, isSourceHost: false));
		False(NetworkMigrationRules.CanCandidateStart(valid, "host", NetworkSessionState.MigratingHost, hasCheckpoint: true, isSourceHost: false));
		False(NetworkMigrationRules.ShouldFollowerConnect(valid, "seat-1", NetworkSessionState.MigratingHost, isCandidate: true, elapsedTimeout: false));
	}

	private static void SteamOwnerConvergenceFollowsSignedCandidate()
	{
		var descriptor = new NetworkSteamMigrationDescriptor
		{
			LobbyId = 109775241000000001,
			SourceLobbyOwnerSteamId = 76561198000000001,
			CandidateSteamId = 76561198000000002,
			ExpectedLobbyOwnerSteamId = 76561198000000002,
		};

		Equal(
			SteamLobbyOwnerConvergenceAction.Ready,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: 76561198000000003,
				currentLobbyOwnerSteamId: descriptor.ExpectedLobbyOwnerSteamId,
				timedOut: false));
		Equal(
			SteamLobbyOwnerConvergenceAction.RequestTransfer,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: 76561198000000003,
				currentLobbyOwnerSteamId: 76561198000000003,
				timedOut: false));
		Equal(
			SteamLobbyOwnerConvergenceAction.Wait,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: 76561198000000004,
				currentLobbyOwnerSteamId: 76561198000000003,
				timedOut: false));
		Equal(
			SteamLobbyOwnerConvergenceAction.Fail,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: 76561198000000004,
				currentLobbyOwnerSteamId: 76561198000000003,
				timedOut: true));
		Equal(
			SteamLobbyOwnerConvergenceAction.Wait,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: descriptor.CandidateSteamId,
				currentLobbyOwnerSteamId: 0,
				timedOut: false));
		Equal(
			SteamLobbyOwnerConvergenceAction.Fail,
			NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
				descriptor,
				localSteamId: descriptor.CandidateSteamId,
				currentLobbyOwnerSteamId: 0,
				timedOut: true));
	}

	private static void MigrationPeerRebuildWaitsForSourceDisconnect()
	{
		False(NetworkMigrationRules.CanRebuildMigrationPeer(
			transportPrepared: false,
			awaitingSourceDisconnect: true));
		False(NetworkMigrationRules.CanRebuildMigrationPeer(
			transportPrepared: true,
			awaitingSourceDisconnect: true));
		False(NetworkMigrationRules.CanRebuildMigrationPeer(
			transportPrepared: false,
			awaitingSourceDisconnect: false));
		True(NetworkMigrationRules.CanRebuildMigrationPeer(
			transportPrepared: true,
			awaitingSourceDisconnect: false));
	}

	private static void StandbyCheckpointSendsAreSerializedAndRateBounded()
	{
		True(NetworkMigrationRules.CanSendStandbyCheckpoint(10.0, -1.0, hasPendingCheckpoint: false));
		False(NetworkMigrationRules.CanSendStandbyCheckpoint(10.1, 10.0, hasPendingCheckpoint: false));
		True(NetworkMigrationRules.CanSendStandbyCheckpoint(10.5, 10.0, hasPendingCheckpoint: false));
		False(NetworkMigrationRules.CanSendStandbyCheckpoint(11.0, 10.0, hasPendingCheckpoint: true));
		False(NetworkMigrationRules.CanSendStandbyCheckpoint(double.NaN, 10.0, hasPendingCheckpoint: false));
	}

	private static void MigrationCoordinatorPermitsOnlyOrderedRecoveryTransitions()
	{
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.Disabled,
			NetworkMigrationCoordinatorState.SelectingCandidate));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.SelectingCandidate,
			NetworkMigrationCoordinatorState.KeyAgreement));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.KeyAgreement,
			NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement,
			NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement,
			NetworkMigrationCoordinatorState.Ready));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.Ready,
			NetworkMigrationCoordinatorState.SourceHostLost));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.SourceHostLost,
			NetworkMigrationCoordinatorState.CandidateStarting));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.CandidateStarting,
			NetworkMigrationCoordinatorState.FollowersRecovering));
		True(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.FollowersRecovering,
			NetworkMigrationCoordinatorState.Completed));
		False(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.Disabled,
			NetworkMigrationCoordinatorState.Ready));
		False(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.SelectingCandidate,
			NetworkMigrationCoordinatorState.Completed));
		False(NetworkMigrationRules.CanTransitionCoordinatorState(
			NetworkMigrationCoordinatorState.FollowersRecovering,
			NetworkMigrationCoordinatorState.Ready));
		True(NetworkMigrationRules.IsReadyForMigration(NetworkMigrationCoordinatorState.Ready));
		False(NetworkMigrationRules.IsReadyForMigration(NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement));
		False(NetworkMigrationRules.IsReadyForMigration(NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement));
		True(NetworkMigrationRules.ArePlanAcknowledgementsComplete(new[] { 1, 2 }, new[] { 1, 2 }));
		False(NetworkMigrationRules.ArePlanAcknowledgementsComplete(new[] { 1, 2 }, new[] { 1 }));
	}

	private static void ActiveHostMigrationFreezesCheckpointAndStandbyMaintenance()
	{
		string root = FindRepositoryRoot();
		string migrationSource = string.Concat(
			Directory.GetFiles(Path.Combine(root, "scripts"), "NetworkManager.Migration*.cs")
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(File.ReadAllText));
		string authoritySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.AuthorityActions.cs"));
		string maintenanceSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Maintenance.cs"));

		int handoffStart = migrationSource.IndexOf("private bool IsActiveMigrationHandoffInProgress()", StringComparison.Ordinal);
		int handoffEnd = migrationSource.IndexOf("private bool IsMigrationRecoveryHandoffInProgress()", handoffStart, StringComparison.Ordinal);
		True(handoffStart >= 0 && handoffEnd > handoffStart);
		string handoffBlock = migrationSource[handoffStart..handoffEnd];
		True(handoffBlock.Contains("_activeMigrationPending || _activeMigrationAwaitingSourceClose", StringComparison.Ordinal));

		int boardStart = authoritySource.IndexOf("CommitAuthorityCheckpointFromBoard()", StringComparison.Ordinal);
		int lobbyStart = authoritySource.IndexOf("CommitAuthorityCheckpointFromLobby()", boardStart, StringComparison.Ordinal);
		int lobbyEnd = authoritySource.IndexOf("private AuthorityCheckpointCommitResult CommitAuthoritySequence", lobbyStart, StringComparison.Ordinal);
		True(boardStart >= 0 && lobbyStart > boardStart && lobbyEnd > lobbyStart);
		string boardCommitBlock = authoritySource[boardStart..lobbyStart];
		string lobbyCommitBlock = authoritySource[lobbyStart..lobbyEnd];
		foreach (string commitBlock in new[] { boardCommitBlock, lobbyCommitBlock })
		{
			int freezeCheck = commitBlock.IndexOf("if (IsActiveMigrationHandoffInProgress())", StringComparison.Ordinal);
			int invalidation = commitBlock.IndexOf("TryInvalidateMigrationAvailability", StringComparison.Ordinal);
			True(freezeCheck >= 0 && invalidation > freezeCheck);
			True(commitBlock.Contains("active_migration_handoff_in_progress", StringComparison.Ordinal));
		}

		int pollStart = migrationSource.IndexOf("private void PollHotStandby", StringComparison.Ordinal);
		int pollEnd = migrationSource.IndexOf("private bool HasEligibleHotStandbyPeer", pollStart, StringComparison.Ordinal);
		True(pollStart >= 0 && pollEnd > pollStart);
		True(migrationSource[pollStart..pollEnd].Contains("IsActiveMigrationHandoffInProgress()", StringComparison.Ordinal));

		int invalidationStart = migrationSource.IndexOf("private bool TryInvalidateMigrationAvailability", StringComparison.Ordinal);
		int invalidationEnd = migrationSource.IndexOf("private MigrationCheckpointDispatchStatus TryBroadcastMigrationPlanRevocation", invalidationStart, StringComparison.Ordinal);
		True(invalidationStart >= 0 && invalidationEnd > invalidationStart);
		True(migrationSource[invalidationStart..invalidationEnd].Contains("if (IsActiveMigrationHandoffInProgress())", StringComparison.Ordinal));

		int revocationStart = migrationSource.IndexOf("private void RpcMigrationPlanRevoked", StringComparison.Ordinal);
		int revocationEnd = migrationSource.IndexOf("private void RpcDebugStaleMigrationPlan", revocationStart, StringComparison.Ordinal);
		True(revocationStart >= 0 && revocationEnd > revocationStart);
		True(migrationSource[revocationStart..revocationEnd].Contains("if (IsMigrationRecoveryHandoffInProgress())", StringComparison.Ordinal));

		int maintenanceStart = maintenanceSource.IndexOf("private bool IsHotStandbyMaintenanceActive", StringComparison.Ordinal);
		int maintenanceEnd = maintenanceSource.IndexOf("private static void ConsiderDeadline", maintenanceStart, StringComparison.Ordinal);
		True(maintenanceStart >= 0 && maintenanceEnd > maintenanceStart);
		True(maintenanceSource[maintenanceStart..maintenanceEnd].Contains("&& !IsActiveMigrationHandoffInProgress()", StringComparison.Ordinal));
	}

	private static void CheckpointCommitResultsClassifyLocalAndMigrationFailures()
	{
		AuthorityCheckpointCommitResult committed = AuthorityCheckpointCommitResult.Committed(
			12,
			MigrationCheckpointDispatchStatus.CandidateUnavailable);
		AuthorityCheckpointCommitResult signingFailed = AuthorityCheckpointCommitResult.Rejected(
			AuthorityCheckpointCommitStatus.SigningFailed,
			"revocation_signing_failed");
		True(committed.IsCheckpointCommitted);
		False(committed.IsMigrationReady);
		Equal(12L, committed.CheckpointId);
		Equal(MigrationCheckpointDispatchStatus.CandidateUnavailable, committed.DispatchStatus);
		False(signingFailed.IsCheckpointCommitted);
		Equal(AuthorityCheckpointCommitStatus.SigningFailed, signingFailed.CommitStatus);
		Equal(MigrationCheckpointDispatchStatus.NotRequested, signingFailed.DispatchStatus);
		Equal(
			AuthorityCheckpointCommitStatus.PayloadTooLarge,
			NetworkMigrationRules.ClassifyCheckpointBuildFailure(NetworkAuthorityCheckpointCodec.PayloadSizeError));
		Equal(
			AuthorityCheckpointCommitStatus.EncodingFailed,
			NetworkMigrationRules.ClassifyCheckpointBuildFailure(NetworkAuthorityCheckpointCodec.EncodeFailedError));
		Equal(
			AuthorityCheckpointCommitStatus.RulesStateUnavailable,
			NetworkMigrationRules.ClassifyCheckpointBuildFailure("authority_action_not_stable action=ItemCard"));
		Equal(
			AuthorityCheckpointCommitStatus.DataValidationFailed,
			NetworkMigrationRules.ClassifyCheckpointBuildFailure(NetworkAuthorityCheckpointCodec.EncodeInvalidError));
	}

}
