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
		True(partialText.Contains("packet.Span.SequenceEqual(LanDiscoveryPingPayload)", StringComparison.Ordinal));
		True(partialText.Contains("TrySend(_lanRoomAdvertisementPayload, sender)", StringComparison.Ordinal));
		True(partialText.Contains("DebugGetLanRoomAdvertisementBuildCount", StringComparison.Ordinal));
		Equal(1, Regex.Matches(partialText, @"LanDiscoveryMessage\.Build\(").Count);
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
			string adapterText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "SteamPlatformAdapter.cs"));
			True(adapterText.Contains("public sealed class SteamPlatformAdapter : ISteamPlatformAdapter", StringComparison.Ordinal));
			True(adapterText.Contains("bool DisconnectSignal(StringName signalName, Callable callable);", StringComparison.Ordinal));
			True(adapterText.Contains("public bool DisconnectSignal(StringName signalName, Callable callable)", StringComparison.Ordinal));
			True(partialText.Contains("private void DisconnectSteamSignals()", StringComparison.Ordinal));
			True(partialText.Contains("_steamReadyCallable = Callable.From(OnSteamReady);", StringComparison.Ordinal));
			True(partialText.Contains("_steamPlatform?.RefreshState();", StringComparison.Ordinal));
			True(partialText.Contains("private void RefreshSteamLobbyAdvertisedState()", StringComparison.Ordinal));
		False(mainText.Contains("public void HostSteam(", StringComparison.Ordinal));
		False(mainText.Contains("private void TryConnectSteamSignals()", StringComparison.Ordinal));
		False(mainText.Contains("private void OnSteamLobbyJoined(", StringComparison.Ordinal));
		False(mainText.Contains("private bool TryReadSteamRoom(", StringComparison.Ordinal));
			False(mainText.Contains("GetSteamSingleton()", StringComparison.Ordinal));
		False(mainText.Contains("private void RefreshSteamLobbyAdvertisedState()", StringComparison.Ordinal));
		True(mainText.Contains("private void DisconnectMultiplayerSignals()", StringComparison.Ordinal));
		int exitTreeStart = mainText.IndexOf("public override void _ExitTree()", StringComparison.Ordinal);
		int stopScheduler = mainText.IndexOf("StopNetworkMaintenanceScheduler();", exitTreeStart, StringComparison.Ordinal);
		int disconnectMultiplayer = mainText.IndexOf("DisconnectMultiplayerSignals();", exitTreeStart, StringComparison.Ordinal);
		int disconnectSteam = mainText.IndexOf("DisconnectSteamSignals();", exitTreeStart, StringComparison.Ordinal);
		int cleanup = mainText.IndexOf("AbortSessionCloseAndCleanup", exitTreeStart, StringComparison.Ordinal);
		int clearAdapter = mainText.IndexOf("_steamPlatform = null;", exitTreeStart, StringComparison.Ordinal);
		True(exitTreeStart >= 0
			&& stopScheduler > exitTreeStart
			&& disconnectMultiplayer > stopScheduler
			&& disconnectSteam > disconnectMultiplayer
			&& cleanup > disconnectSteam
			&& clearAdapter > cleanup);
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
		True(partialText.Contains("_reconnectCoordinator.Poll(", StringComparison.Ordinal));
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
		False(ensureMethod.Contains(".Sort(", StringComparison.Ordinal));
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
		True(claimsMethod.Contains("foreach (SeatControlState state in _lobbyRoster.SeatControls)", StringComparison.Ordinal));
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

	private static void NetworkRpcSignaturesRemainProtocolV12Compatible()
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
			"opening_dice_roll_result",
			"match_outcome_kind",
			"winner_player_index",
			"scores",
			"base_play_capture_threshold",
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
		True(text.Contains("CanAcceptSceneLoadAck(_lobbyRoster.Players, senderPeerId, seatIndex, out int senderSeatIndex)", StringComparison.Ordinal));
		True(text.Contains("GD.PushWarning(", StringComparison.Ordinal));
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
			True(partialText.Contains("private bool TryGetConfiguredPlayerName(int playerIndex, out string playerName)", StringComparison.Ordinal));
		True(partialText.Contains("private static string[] SplitNetworkCsv(string csv)", StringComparison.Ordinal));
		False(mainText.Contains("private string ToNetworkCardCode(PlayingCard card)", StringComparison.Ordinal));
		False(mainText.Contains("private PlayingCard CreateNetworkPlayingCard(string cardCode)", StringComparison.Ordinal));
		False(mainText.Contains("private ItemCard CreateNetworkVisualItemCard(string itemCardId)", StringComparison.Ordinal));
		False(mainText.Contains("private void EnsureNetworkCardCache()", StringComparison.Ordinal));
			False(mainText.Contains("private bool TryGetConfiguredPlayerName(int playerIndex, out string playerName)", StringComparison.Ordinal));
	}

}
