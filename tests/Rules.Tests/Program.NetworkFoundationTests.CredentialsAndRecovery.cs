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
	private static void LobbyReadyIntentIsConsumedAfterCredentialAcknowledgement()
	{
		string root = FindRepositoryRoot();
		string managerSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.cs"));
		string lobbySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.LobbyMembership.cs"));
		string recoverySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.SessionRecoveryRpc.cs"));
		string lifecycleSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.MatchLifecycle.cs"));

		True(managerSource.Contains("Dictionary<int, bool> _pendingLobbyReadyByPeerId", StringComparison.Ordinal));
		True(lobbySource.Contains("LobbyReadyIntentDisposition.DeferUntilCredentialsAcknowledged", StringComparison.Ordinal));
		True(lobbySource.Contains("_pendingLobbyReadyByPeerId[peerId] = isReady;", StringComparison.Ordinal));
		int credentialsCommitted = recoverySource.IndexOf("CredentialsAcknowledged = true", StringComparison.Ordinal);
		int credentialCommitRpc = recoverySource.IndexOf("MethodName.RpcSeatCredentialCommitted", StringComparison.Ordinal);
		int pendingReadyApplied = recoverySource.IndexOf("TryApplyPendingPlayerReadyIntent(senderPeerId);", StringComparison.Ordinal);
		True(credentialsCommitted >= 0
			&& credentialCommitRpc > credentialsCommitted
			&& pendingReadyApplied > credentialCommitRpc);
		True(managerSource.Contains("_pendingLobbyReadyByPeerId.Remove((int)peerId);", StringComparison.Ordinal));
		True(lifecycleSource.Contains("_pendingLobbyReadyByPeerId.Clear();", StringComparison.Ordinal));
	}

	private static void LocalLobbyReadyIntentRetriesUntilAuthorityEcho()
	{
		string root = FindRepositoryRoot();
		string managerSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.cs"));
		string maintenanceSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Maintenance.cs"));
		string lobbySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.LobbyMembership.cs"));
		string lifecycleSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.MatchLifecycle.cs"));

		True(managerSource.Contains("LocalReadyIntentRetrySeconds = 1.0", StringComparison.Ordinal));
		True(maintenanceSource.Contains("PollLocalReadyIntent(now);", StringComparison.Ordinal));
		True(lobbySource.Contains("private void PollLocalReadyIntent(double now)", StringComparison.Ordinal));
		True(lobbySource.Contains("RpcId(\n\t\t\t\t1,\n\t\t\t\tMethodName.RpcSetReady", StringComparison.Ordinal));
		True(lobbySource.Contains("IsLocalReadyIntentSatisfied(isReady)", StringComparison.Ordinal));
		True(lifecycleSource.Contains("ClearPendingLocalReadyIntent();", StringComparison.Ordinal));
	}

	private static void LobbyInitialSeatCredentialDeliveryRetriesAcrossSyncOrdering()
	{
		string root = FindRepositoryRoot();
		string managerSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.cs"));
		string maintenanceSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Maintenance.cs"));
		string reconnectSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Reconnect.cs"));
		string recoverySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.SessionRecoveryRpc.cs"));

		True(maintenanceSource.Contains("PollSeatCredentialDelivery(now);", StringComparison.Ordinal));
		True(managerSource.Contains("SeatCredentialRetrySeconds = 1.0", StringComparison.Ordinal));
		True(reconnectSource.Contains("private void PollSeatCredentialDelivery(double now)", StringComparison.Ordinal));
		True(reconnectSource.Contains("SendSeatCredentialsToUnacknowledgedPeers();", StringComparison.Ordinal));
		True(recoverySource.Contains("NetworkSessionState.Joining or NetworkSessionState.Lobby", StringComparison.Ordinal));
		True(recoverySource.Contains("TryGetCachedLocalPlayer(out NetworkPlayerPublicState localPlayer)", StringComparison.Ordinal));
		True(recoverySource.Contains("string.Equals(localPlayer.PlayerSessionId, playerSessionId, StringComparison.Ordinal)", StringComparison.Ordinal));
	}

	private static void ClientBoardFingerprintPreservesRedactedSeatShape()
	{
		string source = File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"scripts",
			"GameBoardScene.NetworkSnapshot.cs"));
		int methodStart = source.IndexOf("private Godot.Collections.Array BuildSeatControlStatesArray()", StringComparison.Ordinal);
		int methodEnd = source.IndexOf(
			"private static Godot.Collections.Array BuildRuleScoresArray",
			methodStart,
			StringComparison.Ordinal);
		True(methodStart >= 0 && methodEnd > methodStart);
		string method = source[methodStart..methodEnd];
		True(method.Contains("[\"credential_verifier\"] = string.Empty", StringComparison.Ordinal));
		False(method.Contains("state.CredentialVerifier", StringComparison.Ordinal));
	}

	private static void SeatCredentialsIssueVerifyRotateAndRejectReplay()
	{
		string playerSessionId = SeatCredentialRules.IssuePlayerSessionId();
		string credential = SeatCredentialRules.IssueCredential();
		string verifier = SeatCredentialRules.ComputeVerifier(credential);
		Equal(SeatCredentialRules.PlayerSessionIdBytes * 2, playerSessionId.Length);
		True(SeatCredentialRules.IsCredentialEncodingValid(credential));
		True(SeatCredentialRules.IsVerifierValid(verifier));
		True(SeatCredentialRules.Verify(credential, verifier));
		False(SeatCredentialRules.Verify(SeatCredentialRules.IssueCredential(), verifier));

		SeatControlState current = new(
			SeatIndex: 1,
			Mode: SeatControlMode.AiProxyReclaimable,
			PeerId: 0,
			PlayerSessionId: playerSessionId,
			CredentialVerifier: verifier,
			CredentialGeneration: SeatCredentialRules.InitialGeneration,
			PendingCredentialVerifier: string.Empty,
			PendingCredentialGeneration: 0,
			PendingCredentialPeerId: 0);
		True(SeatCredentialRules.TryBeginRotation(current with { PeerId = 42 }, 42, out SeatControlState pending, out string nextCredential));
		True(SeatCredentialRules.Verify(credential, pending.CredentialVerifier));
		False(SeatCredentialRules.TryAcknowledge(pending, 43, 2, nextCredential, out _));
		True(SeatCredentialRules.TryAcknowledge(pending, 42, 2, nextCredential, out SeatControlState committed));
		Equal(2L, committed.CredentialGeneration);
		True(SeatCredentialRules.Verify(nextCredential, committed.CredentialVerifier));
		False(SeatCredentialRules.Verify(credential, committed.CredentialVerifier));
	}

	private static void SeatCredentialPendingRotationCancelsBackToCurrentGeneration()
	{
		string credential = SeatCredentialRules.IssueCredential();
		SeatControlState current = new(
			0,
			SeatControlMode.HumanConnected,
			10,
			SeatCredentialRules.IssuePlayerSessionId(),
			SeatCredentialRules.ComputeVerifier(credential),
			1,
			string.Empty,
			0,
			0);
		True(SeatCredentialRules.TryBeginRotation(current, 10, out SeatControlState pending, out _));
		SeatControlState cancelled = SeatCredentialRules.CancelPendingRotation(pending);
		Equal(1L, cancelled.CredentialGeneration);
		Equal(0L, cancelled.PendingCredentialGeneration);
		True(SeatCredentialRules.Verify(credential, cancelled.CredentialVerifier));
	}

	private static void PeerRateLimiterEnforcesCategoryIsolationRefillCleanupAndDisconnectThreshold()
	{
		var limiter = new NetworkPeerRateLimiter();
		for (int index = 0; index < 4; index++)
		{
			True(limiter.TryConsume(2, NetworkRequestCategory.LobbyMutation, 0, out _));
		}
		False(limiter.TryConsume(2, NetworkRequestCategory.LobbyMutation, 0, out bool disconnect));
		False(disconnect);
		True(limiter.TryConsume(2, NetworkRequestCategory.BoardAction, 0, out _));
		True(limiter.TryConsume(3, NetworkRequestCategory.LobbyMutation, 0, out _));
		True(limiter.TryConsume(2, NetworkRequestCategory.LobbyMutation, 1, out _));

		for (int index = 0; index < 64; index++)
		{
			False(limiter.TryConsume(2, NetworkRequestCategory.LobbyMutation, 1, out disconnect));
		}
		True(disconnect);
		limiter.ClearPeer(2);
		True(limiter.TryConsume(2, NetworkRequestCategory.LobbyMutation, 2, out disconnect));
		False(disconnect);
		True(limiter.BucketCount <= 2);
	}

	private static void LobbyMigrationRecoveryTracksClaimsAndTenSecondExpiry()
	{
		var recovery = new LobbyMigrationRecoveryState(
			targetEpoch: 7,
			startedAt: 100,
			timeoutSeconds: NetworkMigrationRules.LobbyRecoveryTimeoutSeconds,
			expectedPlayerSessionIds: new[] { "p2", "p3" });
		False(recovery.IsComplete);
		True(recovery.TryMarkClaimed("p2"));
		False(recovery.TryMarkClaimed("p2"));
		False(recovery.HasExpired(109.999));
		True(recovery.HasExpired(110));
		True(recovery.TryMarkClaimed("p3"));
		True(recovery.IsComplete);
	}

	private static void GameMigrationRecoveryWaitsForSnapshotAcknowledgements()
	{
		var recovery = new GameMigrationRecoveryState(
			targetEpoch: 8,
			startedAt: 100,
			expectedSeatIndices: new[] { 1, 2, 2, -1 });
		Equal(8L, recovery.TargetEpoch);
		Equal(2, recovery.ExpectedSeatCount);
		Equal(2, recovery.PendingSeatCount);
		False(recovery.IsComplete);
		True(recovery.TryMarkSnapshotApplied(1));
		False(recovery.TryMarkSnapshotApplied(1));
		False(recovery.TryMarkSnapshotApplied(3));
		Equal(1, recovery.RecoveredSeatCount);
		True(recovery.TryMarkSnapshotApplied(2));
		True(recovery.IsComplete);
	}

	private static void GameMigrationRecoveryRemovesDisconnectedSeatsWithoutExtendingDeadline()
	{
		var recovery = new GameMigrationRecoveryState(
			targetEpoch: 3,
			startedAt: 25,
			expectedSeatIndices: new[] { 1, 2 });
		double deadline = recovery.Deadline;
		True(recovery.TryMarkSnapshotApplied(1));
		True(recovery.TryRemoveDisconnectedSeat(2));
		False(recovery.TryRemoveDisconnectedSeat(2));
		Equal(deadline, recovery.Deadline);
		Equal(1, recovery.RecoveredSeatCount);
		Equal(1, recovery.DisconnectedSeatCount);
		True(recovery.IsComplete);
	}

	private static void GameMigrationRecoveryExpiresAfterFixedTenSeconds()
	{
		var recovery = new GameMigrationRecoveryState(
			targetEpoch: 5,
			startedAt: 40,
			expectedSeatIndices: new[] { 1, 2 });
		Equal(50.0, recovery.Deadline);
		False(recovery.HasExpired(49.999));
		False(recovery.TryExpire(49.999));
		True(recovery.TryExpire(50.0));
		True(recovery.IsComplete);
	}

	private static void LateGameMigrationReadinessMatchesOnlyTheCompletedCurrentEpoch()
	{
		True(NetworkMigrationRules.IsCompletedGameRecoveryEpoch(7, 7, 7));
		False(NetworkMigrationRules.IsCompletedGameRecoveryEpoch(6, 7, 7));
		False(NetworkMigrationRules.IsCompletedGameRecoveryEpoch(7, 6, 7));
		False(NetworkMigrationRules.IsCompletedGameRecoveryEpoch(7, 7, 8));
		False(NetworkMigrationRules.IsCompletedGameRecoveryEpoch(-1, -1, -1));
	}

}
