using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Godot;

public partial class NetworkManager
{
	private sealed class PeerMigrationSecurityState
	{
		public byte[] SessionKey { get; init; } = Array.Empty<byte>();
		public byte[] SuccessorPublicKey { get; init; } = Array.Empty<byte>();
		public string SuccessorKeyId { get; init; } = string.Empty;
		public bool TrustedHotStandbyAccepted { get; init; }
		public bool TransportTakeoverCapable { get; init; }
	}

	private sealed record PeerSecurityChallenge(byte[] Challenge, bool RekeyExistingPeer);

	private readonly Dictionary<int, PeerSecurityChallenge> _pendingPeerSecurityChallenges = new();
	private readonly Dictionary<int, PeerMigrationSecurityState> _migrationSecurityByPeerId = new();
	private ECDiffieHellman? _localMigrationKeyAgreement;
	private ECDsa? _localMigrationSuccessorSigner;
	private ECDsa? _authorityMigrationSigner;
	private byte[] _localMigrationSessionKey = Array.Empty<byte>();
	private byte[] _boundAuthorityMigrationPublicKey = Array.Empty<byte>();
	private bool _localSecurityHandshakeAccepted;
	private bool _localSecurityHandshakeIsRekey;
	private bool _hostMigrationEnabled = true;
	private bool _trustedHotStandbyAccepted = true;
	private long _migrationPlanRevision;
	private string _lastConfirmedCheckpointCiphertextHash = string.Empty;

	public bool HostMigrationEnabled => _hostMigrationEnabled;
	public bool TrustedHotStandbyAccepted => _trustedHotStandbyAccepted;

	public void ConfigureHostMigration(bool enabled, bool trustedHotStandbyAccepted)
	{
		if (_state is not (NetworkSessionState.Idle or NetworkSessionState.Searching or NetworkSessionState.Hosting or NetworkSessionState.Joining))
		{
			return;
		}

		_hostMigrationEnabled = enabled;
		_trustedHotStandbyAccepted = trustedHotStandbyAccepted;
	}

	private void InitializeHostMigrationSecurity()
	{
		DisposeMigrationSecurity();
		_localMigrationKeyAgreement = HostMigrationSecurity.CreateKeyAgreement();
		_authorityMigrationSigner = HostMigrationSecurity.CreateSigner();
		_localMigrationSuccessorSigner = HostMigrationSecurity.CreateSigner();
		_localSecurityHandshakeAccepted = true;
		_migrationPlanRevision = 0;
		_lastConfirmedCheckpointCiphertextHash = string.Empty;
	}

	private void PrepareLocalMigrationSecurity()
	{
		_localMigrationKeyAgreement ??= HostMigrationSecurity.CreateKeyAgreement();
		_localMigrationSuccessorSigner ??= HostMigrationSecurity.CreateSigner();
		_localSecurityHandshakeAccepted = false;
	}

	private void BeginPeerSecurityHandshake(int peerId, bool rekeyExistingPeer = false)
	{
		if (!IsHost
			|| peerId <= 1
			|| _state is NetworkSessionState.Idle or NetworkSessionState.Searching or NetworkSessionState.Failed)
		{
			return;
		}

		_localMigrationKeyAgreement ??= HostMigrationSecurity.CreateKeyAgreement();
		_authorityMigrationSigner ??= HostMigrationSecurity.CreateSigner();
		byte[] challenge = RoomPasswordRules.CreateChallenge();
		_pendingPeerSecurityChallenges[peerId] = new PeerSecurityChallenge(challenge, rekeyExistingPeer);
		RoomPasswordVerifier verifier = rekeyExistingPeer
			? RoomPasswordVerifier.Empty
			: _config.RoomPasswordVerifier;
		RpcId(
			peerId,
			MethodName.RpcBeginSecurityHandshake,
			_config.SessionId,
			NetworkProtocol.CurrentVersion,
			HostMigrationSecurity.ExportPublicKey(_localMigrationKeyAgreement),
			HostMigrationSecurity.ExportPublicKey(_authorityMigrationSigner),
			verifier.Version,
			verifier.Iterations,
			verifier.Salt,
			challenge,
			_hostMigrationEnabled,
			rekeyExistingPeer);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBeginSecurityHandshake(
		string sessionId,
		int protocolVersion,
		byte[] hostKeyAgreementPublicKey,
		byte[] sourceAuthorityPublicKey,
		int passwordVerifierVersion,
		int passwordVerifierIterations,
		string passwordVerifierSalt,
		byte[] passwordChallenge,
		bool hostMigrationEnabled,
		bool rekeyExistingPeer)
	{
		if (IsHost
			|| _state is not (NetworkSessionState.Joining
				or NetworkSessionState.Lobby
				or NetworkSessionState.InGame
				or NetworkSessionState.Reconnecting
				or NetworkSessionState.MigratingHost)
			|| protocolVersion != NetworkProtocol.CurrentVersion
			|| string.IsNullOrWhiteSpace(sessionId)
			|| sessionId.Length > NetworkProtocol.MaxSessionIdLength
			|| passwordChallenge is not { Length: RoomPasswordRules.ChallengeBytes })
		{
			return;
		}

		if (_state == NetworkSessionState.MigratingHost
			&& _networkMigrationPlan is { } migrationPlan
			&& (!HostMigrationSecurity.TryDecodeBytes(
					migrationPlan.CandidateSuccessorPublicKey,
					512,
					out byte[] authorizedAuthorityPublicKey)
				|| !authorizedAuthorityPublicKey.AsSpan().SequenceEqual(sourceAuthorityPublicKey)
				|| !string.Equals(
					migrationPlan.CandidateSuccessorKeyId,
					HostMigrationSecurity.ComputeKeyId(sourceAuthorityPublicKey),
					StringComparison.Ordinal)))
		{
			return;
		}

		PrepareLocalMigrationSecurity();
		if (_localMigrationKeyAgreement is null
			|| _localMigrationSuccessorSigner is null
			|| !HostMigrationSecurity.TryDeriveSessionKey(
				_localMigrationKeyAgreement,
				hostKeyAgreementPublicKey,
				sessionId,
				out byte[] sessionKey))
		{
			return;
		}

		byte[] passwordProof = Array.Empty<byte>();
		if (passwordVerifierVersion > 0)
		{
			if (!RoomPasswordRules.TryComputeChallengeProof(
				_pendingJoinRoomPassword,
				passwordVerifierVersion,
				passwordVerifierIterations,
				passwordVerifierSalt,
				passwordChallenge,
				sessionId,
				out passwordProof))
			{
				return;
			}
		}

		ClearLocalMigrationSessionKey();
		_localMigrationSessionKey = sessionKey;
		_boundAuthorityMigrationPublicKey = sourceAuthorityPublicKey?.Clone() as byte[] ?? Array.Empty<byte>();
		_hostMigrationEnabled = hostMigrationEnabled;
		_localSecurityHandshakeIsRekey = rekeyExistingPeer;
		_config = _config with { SessionId = sessionId };
		RpcId(
			1,
			MethodName.RpcCompleteSecurityHandshake,
			sessionId,
			protocolVersion,
			HostMigrationSecurity.ExportPublicKey(_localMigrationKeyAgreement),
			HostMigrationSecurity.ExportPublicKey(_localMigrationSuccessorSigner),
			passwordChallenge,
				passwordProof,
				_trustedHotStandbyAccepted,
				CanLocalTransportTakeOver());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcCompleteSecurityHandshake(
		string sessionId,
		int protocolVersion,
		byte[] clientKeyAgreementPublicKey,
		byte[] successorPublicKey,
		byte[] passwordChallenge,
		byte[] passwordProof,
		bool trustedHotStandbyAccepted,
		bool transportTakeoverCapable)
	{
		if (!IsHost
			|| protocolVersion != NetworkProtocol.CurrentVersion
			|| !string.Equals(sessionId, _config.SessionId, StringComparison.Ordinal)
			|| _localMigrationKeyAgreement is null)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!_pendingPeerSecurityChallenges.TryGetValue(senderPeerId, out PeerSecurityChallenge? pendingChallenge)
			|| passwordChallenge is null
			|| !CryptographicOperations.FixedTimeEquals(pendingChallenge.Challenge, passwordChallenge))
		{
			return;
		}

		_pendingPeerSecurityChallenges.Remove(senderPeerId);
		bool passwordAccepted = pendingChallenge.RekeyExistingPeer
			? passwordProof is { Length: 0 }
			: !_config.RoomPasswordVerifier.HasPassword
			? passwordProof is { Length: 0 }
			: RoomPasswordRules.VerifyChallengeProof(
				_config.RoomPasswordVerifier,
				pendingChallenge.Challenge,
				_config.SessionId,
				passwordProof);
		if (!passwordAccepted
			|| !HostMigrationSecurity.TryDeriveSessionKey(
				_localMigrationKeyAgreement,
				clientKeyAgreementPublicKey,
				_config.SessionId,
				out byte[] sessionKey)
			|| successorPublicKey is not { Length: > 0 })
		{
			RejectLobbyJoin(senderPeerId, "房间认证失败");
			return;
		}

		ClearPeerMigrationSecurity(senderPeerId);
		_migrationSecurityByPeerId[senderPeerId] = new PeerMigrationSecurityState
		{
			SessionKey = sessionKey,
			SuccessorPublicKey = successorPublicKey.ToArray(),
			SuccessorKeyId = HostMigrationSecurity.ComputeKeyId(successorPublicKey),
			TrustedHotStandbyAccepted = trustedHotStandbyAccepted,
			TransportTakeoverCapable = transportTakeoverCapable,
		};
		RpcId(
			senderPeerId,
			MethodName.RpcSecurityHandshakeAccepted,
			_config.SessionId,
			NetworkProtocol.CurrentVersion,
			_hostMigrationEnabled,
			pendingChallenge.RekeyExistingPeer);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSecurityHandshakeAccepted(
		string sessionId,
		int protocolVersion,
		bool hostMigrationEnabled,
		bool rekeyExistingPeer)
	{
		if (IsHost
			|| protocolVersion != NetworkProtocol.CurrentVersion
			|| !string.Equals(sessionId, _config.SessionId, StringComparison.Ordinal)
			|| _localMigrationSessionKey.Length != HostMigrationSecurity.SessionKeyBytes
			|| _boundAuthorityMigrationPublicKey.Length == 0)
		{
			return;
		}

		_localSecurityHandshakeAccepted = true;
		_hostMigrationEnabled = hostMigrationEnabled;
		_pendingJoinRoomPassword = string.Empty;
		bool shouldContinueAuthentication = NetworkMigrationRules.ShouldContinueAuthenticationAfterSecurityHandshake(
			_state,
			_localSecurityHandshakeIsRekey,
			rekeyExistingPeer);
		_localSecurityHandshakeIsRekey = false;
		if (!shouldContinueAuthentication)
		{
			return;
		}
		ContinueClientAuthenticationAfterSecurityHandshake();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRevokeHotStandby(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string candidatePlayerSessionId)
	{
		if (IsHost
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch)
			|| !string.Equals(candidatePlayerSessionId, GetPlayerSessionId(), StringComparison.Ordinal))
		{
			return;
		}

		ClearLocalStandbyCheckpointCache();
		ClearLocalMigrationSessionKey();
		_localMigrationKeyAgreement?.Dispose();
		_localMigrationKeyAgreement = null;
		_localMigrationSuccessorSigner?.Dispose();
		_localMigrationSuccessorSigner = null;
		_localSecurityHandshakeAccepted = false;
		_localSecurityHandshakeIsRekey = false;
	}

	private void RekeyFormerHotStandby(int seatIndex)
	{
		if (!IsHost
			|| seatIndex < 0
			|| !TryGetConnectedRemotePlayerBySeatIndex(seatIndex, out NetworkPlayerPublicState formerCandidate))
		{
			return;
		}

		NetworkRpcContext context = GetCurrentRpcContext();
		RpcId(
			formerCandidate.PeerId,
			MethodName.RpcRevokeHotStandby,
			context.SessionId,
			context.MatchId,
			context.AuthorityEpoch,
			formerCandidate.PlayerSessionId);
		ClearPeerMigrationSecurity(formerCandidate.PeerId);
		BeginPeerSecurityHandshake(formerCandidate.PeerId, rekeyExistingPeer: true);
	}

	private void ResetMigrationSecurityAfterCompletedMatch()
	{
		DisposeMigrationSecurity();
		if (!IsHost)
		{
			PrepareLocalMigrationSecurity();
			return;
		}

		InitializeHostMigrationSecurity();
		foreach (NetworkPlayerPublicState player in _players)
		{
			if (player.IsConnected && !player.IsHost)
			{
				BeginPeerSecurityHandshake(player.PeerId, rekeyExistingPeer: true);
			}
		}
	}

	private bool IsPeerSecurityAuthenticated(int peerId)
	{
		return peerId > 1 && _migrationSecurityByPeerId.ContainsKey(peerId);
	}

	private bool CanPeerActAsHotStandby(int peerId)
	{
		return _hostMigrationEnabled
			&& _migrationSecurityByPeerId.TryGetValue(peerId, out PeerMigrationSecurityState? security)
			&& security.TrustedHotStandbyAccepted
			&& security.TransportTakeoverCapable
			&& security.SessionKey.Length == HostMigrationSecurity.SessionKeyBytes
			&& security.SuccessorPublicKey.Length > 0;
	}

	private bool TryGetPeerMigrationSecurity(int peerId, out PeerMigrationSecurityState security)
	{
		return _migrationSecurityByPeerId.TryGetValue(peerId, out security!);
	}

	private bool TryPromoteLocalMigrationAuthority(NetworkMigrationPlan plan)
	{
		if (!IsLocalMigrationCandidate(plan)
			|| !HostMigrationSecurity.TryDecodeBytes(
				plan.CandidateSuccessorPublicKey,
				512,
				out byte[] authorizedPublicKey))
		{
			return false;
		}

		if (_authorityMigrationSigner is not null)
		{
			byte[] currentPublicKey = HostMigrationSecurity.ExportPublicKey(_authorityMigrationSigner);
			if (currentPublicKey.AsSpan().SequenceEqual(authorizedPublicKey)
				&& string.Equals(
					HostMigrationSecurity.ComputeKeyId(currentPublicKey),
					plan.CandidateSuccessorKeyId,
					StringComparison.Ordinal))
			{
				return true;
			}
		}

		if (_localMigrationSuccessorSigner is null)
		{
			return false;
		}

		byte[] localSuccessorPublicKey = HostMigrationSecurity.ExportPublicKey(_localMigrationSuccessorSigner);
		if (!localSuccessorPublicKey.AsSpan().SequenceEqual(authorizedPublicKey)
			|| !string.Equals(
				HostMigrationSecurity.ComputeKeyId(localSuccessorPublicKey),
				plan.CandidateSuccessorKeyId,
				StringComparison.Ordinal))
		{
			return false;
		}

		_authorityMigrationSigner?.Dispose();
		_authorityMigrationSigner = _localMigrationSuccessorSigner;
		_localMigrationSuccessorSigner = HostMigrationSecurity.CreateSigner();
		_localMigrationKeyAgreement?.Dispose();
		_localMigrationKeyAgreement = HostMigrationSecurity.CreateKeyAgreement();
		ClearLocalMigrationSessionKey();
		_localSecurityHandshakeAccepted = true;
		return true;
	}

	private void ClearPeerMigrationSecurity(int peerId)
	{
		_pendingPeerSecurityChallenges.Remove(peerId);
		if (_migrationSecurityByPeerId.Remove(peerId, out PeerMigrationSecurityState? security)
			&& security.SessionKey.Length > 0)
		{
			CryptographicOperations.ZeroMemory(security.SessionKey);
		}
	}

	private void ClearLocalMigrationSessionKey()
	{
		if (_localMigrationSessionKey.Length > 0)
		{
			CryptographicOperations.ZeroMemory(_localMigrationSessionKey);
		}
		_localMigrationSessionKey = Array.Empty<byte>();
	}

	private void DisposeMigrationSecurity()
	{
		foreach (int peerId in new List<int>(_migrationSecurityByPeerId.Keys))
		{
			ClearPeerMigrationSecurity(peerId);
		}
		_pendingPeerSecurityChallenges.Clear();
		ClearLocalMigrationSessionKey();
		if (_boundAuthorityMigrationPublicKey.Length > 0)
		{
			CryptographicOperations.ZeroMemory(_boundAuthorityMigrationPublicKey);
		}
		_boundAuthorityMigrationPublicKey = Array.Empty<byte>();
		_localMigrationKeyAgreement?.Dispose();
		_localMigrationKeyAgreement = null;
		_localMigrationSuccessorSigner?.Dispose();
		_localMigrationSuccessorSigner = null;
		_authorityMigrationSigner?.Dispose();
		_authorityMigrationSigner = null;
		_localSecurityHandshakeAccepted = false;
		_localSecurityHandshakeIsRekey = false;
		_migrationPlanRevision = 0;
		_lastConfirmedCheckpointCiphertextHash = string.Empty;
	}
}
