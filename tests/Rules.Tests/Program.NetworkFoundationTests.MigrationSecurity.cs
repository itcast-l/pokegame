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
	private static void MigrationPlanRevocationIsSignedAndTamperEvident()
	{
		using ECDsa sourceSigner = HostMigrationSecurity.CreateSigner();
		byte[] sourcePublicKey = HostMigrationSecurity.ExportPublicKey(sourceSigner);
		NetworkMigrationPlanRevocation unsigned = new()
		{
			ProtocolVersion = NetworkProtocol.CurrentVersion,
			PlanRevision = 8,
			RevokedPlanRevision = 7,
			RevokedCheckpointId = 21,
			SessionId = "session",
			MatchId = "match",
			SourceEpoch = 4,
			SourceAuthorityKeyId = HostMigrationSecurity.ComputeKeyId(sourcePublicKey),
		};

		True(HostMigrationSecurity.TrySignMigrationPlanRevocation(unsigned, sourceSigner, out NetworkMigrationPlanRevocation signed));
		True(NetworkMigrationRules.IsPlanRevocationVisibleToClients(signed));
		True(HostMigrationSecurity.VerifyMigrationPlanRevocation(signed, sourcePublicKey));
		False(HostMigrationSecurity.VerifyMigrationPlanRevocation(
			signed with { RevokedCheckpointId = 22 },
			sourcePublicKey));
		False(NetworkMigrationRules.IsPlanRevocationVisibleToClients(signed with { PlanRevision = signed.RevokedPlanRevision }));
	}

	private static void RevocationBlocksPlanReplayAndStaleCheckpointEligibility()
	{
		NetworkMigrationPlan stalePlan = new() { PlanRevision = 7 };
		NetworkMigrationPlan replacementPlan = new() { PlanRevision = 8 };
		NetworkMigrationPlan newerPlan = new() { PlanRevision = 9 };
		False(NetworkMigrationRules.CanAcceptPlanRevisionAfterRevocation(8, 8, stalePlan));
		True(NetworkMigrationRules.CanAcceptPlanRevisionAfterRevocation(8, 8, replacementPlan));
		True(NetworkMigrationRules.CanAcceptPlanRevisionAfterRevocation(8, 8, newerPlan));
		False(NetworkMigrationRules.CanDispatchMigrationCheckpoint(21, -1));
		False(NetworkMigrationRules.CanDispatchMigrationCheckpoint(21, 20));
		True(NetworkMigrationRules.CanDispatchMigrationCheckpoint(22, 22));
		True(NetworkMigrationRules.CanAcceptCheckpointChainAdvance(
			"confirmed",
			"confirmed",
			22,
			21,
			9,
			8));
		False(NetworkMigrationRules.CanAcceptCheckpointChainAdvance(
			"speculative",
			"confirmed",
			22,
			21,
			9,
			8));
		False(NetworkMigrationRules.CanAcceptCheckpointChainAdvance(
			"confirmed",
			"confirmed",
			21,
			21,
			9,
			8));
		False(NetworkMigrationRules.CanAcceptCheckpointChainAdvance(
			"confirmed",
			"confirmed",
			22,
			21,
			8,
			8));
	}

	private static void MigrationCheckpointChainIsScopedToSessionMatchAndEpoch()
	{
		NetworkAuthorityCheckpoint checkpoint = CreateCheckpoint() with
		{
			SessionId = "session-a",
			MatchId = "match-a",
			AuthorityEpoch = 4,
		};
		True(NetworkMigrationRules.IsSameCheckpointChain(checkpoint, "session-a", "match-a", 4));
		False(NetworkMigrationRules.IsSameCheckpointChain(checkpoint, "session-b", "match-a", 4));
		False(NetworkMigrationRules.IsSameCheckpointChain(checkpoint, "session-a", "match-b", 4));
		False(NetworkMigrationRules.IsSameCheckpointChain(checkpoint, "session-a", "match-a", 5));
		False(NetworkMigrationRules.IsSameCheckpointChain(null, "session-a", "match-a", 4));
	}

	private static void MigrationRekeyHandshakeContinuesIntoSeatAuthentication()
	{
		True(NetworkMigrationRules.ShouldContinueAuthenticationAfterSecurityHandshake(
			NetworkSessionState.Reconnecting,
			localHandshakeIsRekey: true,
			authorityHandshakeIsRekey: true));
		True(NetworkMigrationRules.ShouldContinueAuthenticationAfterSecurityHandshake(
			NetworkSessionState.MigratingHost,
			localHandshakeIsRekey: true,
			authorityHandshakeIsRekey: true));
		False(NetworkMigrationRules.ShouldContinueAuthenticationAfterSecurityHandshake(
			NetworkSessionState.Lobby,
			localHandshakeIsRekey: true,
			authorityHandshakeIsRekey: true));
		True(NetworkMigrationRules.ShouldContinueAuthenticationAfterSecurityHandshake(
			NetworkSessionState.Joining,
			localHandshakeIsRekey: false,
			authorityHandshakeIsRekey: false));
	}

	private static void RoomPasswordChallengeRejectsPlaintextReplaySubstitutes()
	{
		RoomPasswordVerifier verifier = RoomPasswordRules.CreateVerifier(" trusted-room ");
		byte[] challenge = RoomPasswordRules.CreateChallenge();
		True(verifier.HasPassword);
		True(RoomPasswordRules.IsVerifierValid(verifier));
		True(RoomPasswordRules.TryComputeChallengeProof(
			"trusted-room",
			verifier,
			challenge,
			"session-a",
			out byte[] proof));
		True(RoomPasswordRules.TryComputeChallengeProof(
			"trusted-room",
			verifier.Version,
			verifier.Iterations,
			verifier.Salt,
			challenge,
			"session-a",
			out byte[] parameterProof));
		True(proof.SequenceEqual(parameterProof));
		False(RoomPasswordRules.TryComputeChallengeProof(
			"trusted-room",
			verifier.Version + 1,
			verifier.Iterations,
			verifier.Salt,
			challenge,
			"session-a",
			out _));
		True(RoomPasswordRules.VerifyChallengeProof(verifier, challenge, "session-a", proof));
		False(RoomPasswordRules.VerifyChallengeProof(verifier, RoomPasswordRules.CreateChallenge(), "session-a", proof));
		False(RoomPasswordRules.VerifyChallengeProof(verifier, challenge, "session-b", proof));
		True(RoomPasswordRules.TryComputeChallengeProof(
			"wrong-password",
			verifier,
			challenge,
			"session-a",
			out byte[] wrongProof));
		False(RoomPasswordRules.VerifyChallengeProof(verifier, challenge, "session-a", wrongProof));
		False(Encoding.UTF8.GetString(proof).Contains("trusted-room", StringComparison.Ordinal));
	}

	private static void MigrationKeyAgreementDerivesPeerUniqueSessionKeys()
	{
		using ECDiffieHellman host = HostMigrationSecurity.CreateKeyAgreement();
		using ECDiffieHellman candidate = HostMigrationSecurity.CreateKeyAgreement();
		using ECDiffieHellman follower = HostMigrationSecurity.CreateKeyAgreement();
		True(HostMigrationSecurity.TryDeriveSessionKey(
			host,
			HostMigrationSecurity.ExportPublicKey(candidate),
			"session-a",
			out byte[] hostCandidateKey));
		True(HostMigrationSecurity.TryDeriveSessionKey(
			candidate,
			HostMigrationSecurity.ExportPublicKey(host),
			"session-a",
			out byte[] candidateKey));
		True(HostMigrationSecurity.TryDeriveSessionKey(
			host,
			HostMigrationSecurity.ExportPublicKey(follower),
			"session-a",
			out byte[] hostFollowerKey));
		True(hostCandidateKey.SequenceEqual(candidateKey));
		False(hostCandidateKey.SequenceEqual(hostFollowerKey));
	}

	private static void EncryptedMigrationCheckpointRejectsTamperingAndWrongIdentities()
	{
		using ECDiffieHellman hostAgreement = HostMigrationSecurity.CreateKeyAgreement();
		using ECDiffieHellman candidateAgreement = HostMigrationSecurity.CreateKeyAgreement();
		using ECDiffieHellman otherAgreement = HostMigrationSecurity.CreateKeyAgreement();
		using ECDsa sourceSigner = HostMigrationSecurity.CreateSigner();
		using ECDsa wrongSigner = HostMigrationSecurity.CreateSigner();
		True(HostMigrationSecurity.TryDeriveSessionKey(
			hostAgreement,
			HostMigrationSecurity.ExportPublicKey(candidateAgreement),
			"session-a",
			out byte[] hostSessionKey));
		True(HostMigrationSecurity.TryDeriveSessionKey(
			candidateAgreement,
			HostMigrationSecurity.ExportPublicKey(hostAgreement),
			"session-a",
			out byte[] candidateSessionKey));
		True(HostMigrationSecurity.TryDeriveSessionKey(
			otherAgreement,
			HostMigrationSecurity.ExportPublicKey(hostAgreement),
			"session-a",
			out byte[] wrongSessionKey));

		byte[] payload = Encoding.UTF8.GetBytes("private checkpoint payload");
		string payloadHash = NetworkAuthorityCheckpointCodec.ComputeHash(payload);
		var context = new MigrationCheckpointSecurityContext(
			"session-a",
			"match-a",
			4,
			9,
			"candidate-a",
			3,
			NetworkProtocol.CurrentVersion);
		True(HostMigrationSecurity.TryEncryptCheckpoint(
			payload,
			payloadHash,
			hostSessionKey,
			context,
			new string('a', 64),
			sourceSigner,
			out EncryptedAuthorityCheckpointEnvelope envelope,
			out _));
		True(HostMigrationSecurity.TryDecryptCheckpoint(
			envelope,
			candidateSessionKey,
			HostMigrationSecurity.ExportPublicKey(sourceSigner),
			out byte[] decrypted,
			out _));
		True(payload.SequenceEqual(decrypted));
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			envelope,
			wrongSessionKey,
			HostMigrationSecurity.ExportPublicKey(sourceSigner),
			out _,
			out _));
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			envelope,
			candidateSessionKey,
			HostMigrationSecurity.ExportPublicKey(wrongSigner),
			out _,
			out _));

		byte[] tamperedCiphertext = envelope.GetCiphertextCopy();
		tamperedCiphertext[0] ^= 0x40;
		var tampered = new EncryptedAuthorityCheckpointEnvelope(
			envelope.Context,
			envelope.GetNonceCopy(),
			tamperedCiphertext,
			envelope.GetTagCopy(),
			envelope.PlaintextHash,
			envelope.CiphertextHash,
			envelope.PreviousCheckpointHash,
			envelope.SourceAuthorityKeyId,
			envelope.GetSignatureCopy());
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			tampered,
			candidateSessionKey,
			HostMigrationSecurity.ExportPublicKey(sourceSigner),
			out _,
			out _));

		byte[] tamperedTag = envelope.GetTagCopy();
		tamperedTag[^1] ^= 0x01;
		var tagTampered = new EncryptedAuthorityCheckpointEnvelope(
			envelope.Context,
			envelope.GetNonceCopy(),
			envelope.GetCiphertextCopy(),
			tamperedTag,
			envelope.PlaintextHash,
			envelope.CiphertextHash,
			envelope.PreviousCheckpointHash,
			envelope.SourceAuthorityKeyId,
			envelope.GetSignatureCopy());
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			tagTampered,
			candidateSessionKey,
			HostMigrationSecurity.ExportPublicKey(sourceSigner),
			out _,
			out _));

		var wrongCandidate = new EncryptedAuthorityCheckpointEnvelope(
			envelope.Context with { CandidatePlayerSessionId = "candidate-b" },
			envelope.GetNonceCopy(),
			envelope.GetCiphertextCopy(),
			envelope.GetTagCopy(),
			envelope.PlaintextHash,
			envelope.CiphertextHash,
			envelope.PreviousCheckpointHash,
			envelope.SourceAuthorityKeyId,
			envelope.GetSignatureCopy());
		False(HostMigrationSecurity.TryDecryptCheckpoint(
			wrongCandidate,
			candidateSessionKey,
			HostMigrationSecurity.ExportPublicKey(sourceSigner),
			out _,
			out _));
	}

	private static void MigrationNonceReplayGuardRejectsDuplicatesAndResetsPerEpoch()
	{
		var guard = new MigrationNonceReplayGuard();
		byte[] firstNonce = Enumerable.Range(0, HostMigrationSecurity.AesNonceBytes)
			.Select(value => (byte)value)
			.ToArray();
		byte[] secondNonce = Enumerable.Range(1, HostMigrationSecurity.AesNonceBytes)
			.Select(value => (byte)value)
			.ToArray();

		False(guard.HasSeen(4, firstNonce));
		True(guard.TryRegister(4, firstNonce));
		True(guard.HasSeen(4, firstNonce));
		False(guard.TryRegister(4, firstNonce));
		True(guard.TryRegister(4, secondNonce));
		Equal(2, guard.Count);
		True(guard.TryRegister(5, firstNonce));
		Equal(1, guard.Count);
		False(guard.HasSeen(4, firstNonce));
		True(guard.HasSeen(5, firstNonce));
		False(guard.TryRegister(5, firstNonce[..^1]));
		guard.Reset();
		Equal(0, guard.Count);
		False(guard.HasSeen(5, firstNonce));
	}

	private static void MigrationSerializationExcludesRawSecrets()
	{
		const string rawPassword = "raw-password-never-serialize";
		const string rawCredential = "raw-seat-credential-never-serialize";
		byte[] sessionKey = Enumerable.Repeat((byte)0x5a, HostMigrationSecurity.SessionKeyBytes).ToArray();
		string encodedSessionKey = HostMigrationSecurity.EncodeBytes(sessionKey);
		RoomPasswordVerifier passwordVerifier = RoomPasswordRules.CreateVerifier(rawPassword);
		NetworkAuthorityCheckpoint baseline = CreateCheckpoint();
		NetworkAuthorityCheckpoint checkpoint = baseline with
		{
			RoomPasswordVerifierVersion = passwordVerifier.Version,
			RoomPasswordVerifierIterations = passwordVerifier.Iterations,
			RoomPasswordVerifierSalt = passwordVerifier.Salt,
			RoomPasswordVerifier = passwordVerifier.Verifier,
			SeatControls = baseline.SeatControls
				.Select((control, index) => control with
				{
					CredentialVerifier = index == 0
						? SeatCredentialRules.ComputeVerifier(rawCredential)
						: control.CredentialVerifier,
				})
				.ToList(),
		};

		True(NetworkAuthorityCheckpointCodec.TryEncode(checkpoint, out byte[] payload, out _, out _));
		string checkpointJson = Encoding.UTF8.GetString(payload);
		string planJson = JsonSerializer.Serialize(new NetworkMigrationPlan());
		string serialized = checkpointJson + planJson;
		False(serialized.Contains(rawPassword, StringComparison.Ordinal));
		False(serialized.Contains(rawCredential, StringComparison.Ordinal));
		False(serialized.Contains(encodedSessionKey, StringComparison.Ordinal));
		False(serialized.Contains("\"room_password\"", StringComparison.Ordinal));
		False(serialized.Contains("\"credential\"", StringComparison.Ordinal));
		False(serialized.Contains("\"session_key\"", StringComparison.Ordinal));
		True(checkpointJson.Contains("\"room_password_verifier\"", StringComparison.Ordinal));
		True(checkpointJson.Contains("\"credential_verifier\"", StringComparison.Ordinal));
		False(typeof(NetworkMigrationPlan).GetProperties().Any(property =>
			property.Name is "RoomPassword" or "Credential" or "SessionKey"));
	}

	private static void ReleaseDebugApiSurfaceCannotExportMigrationSecrets()
	{
		string root = FindRepositoryRoot();
		string migrationSource = string.Concat(
			Directory.GetFiles(Path.Combine(root, "scripts"), "NetworkManager.Migration*.cs")
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(File.ReadAllText));
		string debugSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Debug.cs"));
		string source = migrationSource + debugSource;
		False(Regex.IsMatch(
			source,
			@"public\s+(?:byte\[\]|string|NetworkAuthorityCheckpoint\??|EncryptedAuthorityCheckpointEnvelope\??|AuthorityCheckpointEnvelope)\s+Debug[A-Za-z0-9_]*\s*\(",
			RegexOptions.CultureInvariant));
		False(Regex.IsMatch(
			source,
			@"public\s+[^\r\n{]+\s+Debug[A-Za-z0-9_]*(?:CheckpointPayload|SessionKey|Credential|Password)\s*\(",
			RegexOptions.CultureInvariant));

		string serializationSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Serialization.cs"));
		int sharedConfigStart = serializationSource.IndexOf("ToDictionary(NetworkSessionConfig config)", StringComparison.Ordinal);
		int sharedConfigEnd = serializationSource.IndexOf("ToDictionary(NetworkPlayerPublicState player)", sharedConfigStart, StringComparison.Ordinal);
		True(sharedConfigStart >= 0 && sharedConfigEnd > sharedConfigStart);
		string sharedConfigSerializer = serializationSource[sharedConfigStart..sharedConfigEnd];
		False(sharedConfigSerializer.Contains("config.RoomPassword", StringComparison.Ordinal));
		False(sharedConfigSerializer.Contains("RoomPasswordVerifier", StringComparison.Ordinal));
		False(sharedConfigSerializer.Contains("Credential", StringComparison.Ordinal));
		False(sharedConfigSerializer.Contains("SessionKey", StringComparison.Ordinal));
	}

	private static void HostMigrationCapabilityIsReleaseDisabledWithDebugFixture()
	{
		string root = FindRepositoryRoot();
		string scene = File.ReadAllText(Path.Combine(root, "scenes", "multiplayer_lobby_scene.tscn"));
		string lobbySource = File.ReadAllText(Path.Combine(root, "scripts", "MultiplayerLobbyScene.cs"));
		string securitySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Security.cs"));
		string migrationSource = string.Concat(
			Directory.GetFiles(Path.Combine(root, "scripts"), "NetworkManager.Migration*.cs")
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(File.ReadAllText));
		string maintenanceSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Maintenance.cs"));
		string authoritySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.AuthorityActions.cs"));
		string managerSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.cs"));
		string reconnectCoordinatorSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkReconnectCoordinator.cs"));
		string reconnectSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Reconnect.cs"));
		string recoverySource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.SessionRecoveryRpc.cs"));
		string steamSource = File.ReadAllText(Path.Combine(root, "scripts", "NetworkManager.Steam.cs"));
		string boardSessionSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.NetworkSession.cs"));

		int capabilityStart = securitySource.IndexOf("private static bool HostMigrationCapabilityAvailable", StringComparison.Ordinal);
		int capabilityEnd = securitySource.IndexOf("private readonly HashSet<int> _authenticatedPeerIds", capabilityStart, StringComparison.Ordinal);
		True(capabilityStart >= 0 && capabilityEnd > capabilityStart);
		string capabilityBlock = securitySource[capabilityStart..capabilityEnd];
		True(capabilityBlock.Contains("#if DEBUG", StringComparison.Ordinal));
		True(capabilityBlock.Contains("OS.GetEnvironment(\"CODEX_LAN_MIGRATION_FIXTURE\") == \"1\"", StringComparison.Ordinal));
		True(capabilityBlock.Contains("#else", StringComparison.Ordinal));
		True(capabilityBlock.Contains("return false;", StringComparison.Ordinal));
		True(capabilityBlock.Contains("#endif", StringComparison.Ordinal));
		True(securitySource.Contains("public bool HostMigrationEnabled => HostMigrationCapabilityAvailable;", StringComparison.Ordinal));
		True(securitySource.Contains("public bool TrustedHotStandbyAccepted => HostMigrationCapabilityAvailable;", StringComparison.Ordinal));
		int configureStart = securitySource.IndexOf("public void ConfigureHostMigration", StringComparison.Ordinal);
		int configureEnd = securitySource.IndexOf("private void InitializeHostMigrationSecurity", configureStart, StringComparison.Ordinal);
		True(configureStart >= 0 && configureEnd > configureStart);
		string configureBlock = securitySource[configureStart..configureEnd];
		True(configureBlock.Contains("_ = enabled;", StringComparison.Ordinal));
		True(configureBlock.Contains("_ = trustedHotStandbyAccepted;", StringComparison.Ordinal));
		False(configureBlock.Contains("HostMigrationCapabilityAvailable =", StringComparison.Ordinal));

		foreach (string removedControl in new[]
		{
			"HostMigrationToggle",
			"TrustedHotStandbyToggle",
			"MigrationTrustNoticeLabel",
		})
		{
			False(scene.Contains(removedControl, StringComparison.Ordinal));
			False(lobbySource.Contains(removedControl, StringComparison.Ordinal));
		}
		False(lobbySource.Contains("ConfigureHostMigration(", StringComparison.Ordinal));

		Dictionary<string, List<string>> rows = ReadLocalizationRowsByKey(
			Path.Combine(root, "localization", "ui.csv"),
			out _);
		foreach (string key in new[]
		{
			"MULTIPLAYER_LOBBY_HOST_MIGRATION_TOGGLE",
			"MULTIPLAYER_LOBBY_TRUSTED_STANDBY_TOGGLE",
			"MULTIPLAYER_LOBBY_MIGRATION_TRUST_NOTICE",
		})
		{
			False(rows.ContainsKey(key));
		}

		True(maintenanceSource.Contains("HostMigrationCapabilityAvailable && IsHost && _activeMigrationPending", StringComparison.Ordinal));
		True(maintenanceSource.Contains("return HostMigrationCapabilityAvailable", StringComparison.Ordinal));
		True(authoritySource.Contains("if (!HostMigrationCapabilityAvailable)", StringComparison.Ordinal));
		True(authoritySource.Contains("return CommitAuthoritySequence(authority);", StringComparison.Ordinal));
		True(authoritySource.Contains("return CommitAuthoritySequence();", StringComparison.Ordinal));
		True(migrationSource.Contains("private IHostMigrationTransport? CreateMigrationTransport", StringComparison.Ordinal));
		True(migrationSource.Contains("return null;", StringComparison.Ordinal));
		True(migrationSource.Contains("TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Disabled);", StringComparison.Ordinal));
		int artifactStart = migrationSource.IndexOf("public bool DebugHasMigrationArtifacts", StringComparison.Ordinal);
		int artifactEnd = migrationSource.IndexOf("public int DebugGetLocalStandbyCheckpointCacheCount", artifactStart, StringComparison.Ordinal);
		True(artifactStart >= 0 && artifactEnd > artifactStart);
		string artifactBlock = migrationSource[artifactStart..artifactEnd];
		False(artifactBlock.Contains("_issuedSeatCredentialsBySeat", StringComparison.Ordinal));
		False(artifactBlock.Contains("_localSeatCredential", StringComparison.Ordinal));

		True(reconnectCoordinatorSource.Contains("internal const double WindowDurationSeconds = 10.0;", StringComparison.Ordinal));
		int disconnectStart = managerSource.IndexOf("private void OnServerDisconnected()", StringComparison.Ordinal);
		int disconnectEnd = managerSource.IndexOf("private void InsertPlayerInSeatOrder", disconnectStart, StringComparison.Ordinal);
		True(disconnectStart >= 0 && disconnectEnd > disconnectStart);
		string disconnectBlock = managerSource[disconnectStart..disconnectEnd];
		True(disconnectBlock.Contains("EnterReconnectState();", StringComparison.Ordinal));
		True(disconnectBlock.Contains("HandleHostDisconnected();", StringComparison.Ordinal));
		True(disconnectBlock.Contains("TryAdvancePlannedMigrationAfterSourceDisconnect()", StringComparison.Ordinal));
		False(disconnectBlock.Contains("BeginMigrationAfterHostDisconnect", StringComparison.Ordinal));
		False(disconnectBlock.Contains("StartMigrationAttemptsAfterHostDisconnect", StringComparison.Ordinal));
		True(managerSource.Contains(
			"bool resumesAuthenticatedSeat = _state == NetworkSessionState.InGame",
			StringComparison.Ordinal));
		True(reconnectSource.Contains("_reconnectCoordinator.Poll(", StringComparison.Ordinal));
		True(maintenanceSource.Contains("_reconnectCoordinator.WindowDeadlineSeconds", StringComparison.Ordinal));
		int handlerStart = reconnectSource.IndexOf("private void HandleHostDisconnected()", StringComparison.Ordinal);
		int handlerEnd = reconnectSource.IndexOf("private void TryStartReconnectAttempt", handlerStart, StringComparison.Ordinal);
		True(handlerStart >= 0 && handlerEnd > handlerStart);
		string handlerBlock = reconnectSource[handlerStart..handlerEnd];
		True(handlerBlock.Contains("_hostDisconnectReturnPending", StringComparison.Ordinal));
		True(handlerBlock.Contains("_hostDisconnectErrorPublished", StringComparison.Ordinal));
		True(handlerBlock.Contains("RequestSessionCloseAsync(NetworkSessionCloseReason.InternalFailure)", StringComparison.Ordinal));
		True(handlerBlock.Contains("_pendingLobbyErrorLocalizationKey = needsLobbySceneChange ? localizationKey : string.Empty;", StringComparison.Ordinal));
		True(handlerBlock.Contains("nameof(ReturnToMultiplayerLobbyScene)", StringComparison.Ordinal));
		False(handlerBlock.Contains("Host disconnected.", StringComparison.Ordinal));
		True(handlerBlock.Contains("NetworkUserErrorCategory.HostDisconnected", StringComparison.Ordinal));
		True(boardSessionSource.Contains("if (networkManager.IsHostDisconnectReturnPending)", StringComparison.Ordinal));
		True(recoverySource.Contains("NetworkUserErrorRules.NormalizeLocalizationKey(", StringComparison.Ordinal));
		True(recoverySource.Contains("_hostEndedMatchLocalizationKey = hostEndedMatchLocalizationKey;", StringComparison.Ordinal));
		True(recoverySource.Contains(
			"RequestSessionCloseAsync(NetworkSessionCloseReason.InternalFailure)",
			StringComparison.Ordinal));
		True(recoverySource.Contains("EmitSignal(SignalName.ErrorRaised, hostEndedMatchLocalizationKey);", StringComparison.Ordinal));
		True(steamSource.Contains("_lastObservedSteamLobbyOwnerId != _originalSteamLobbyOwnerId", StringComparison.Ordinal));
	}

}
