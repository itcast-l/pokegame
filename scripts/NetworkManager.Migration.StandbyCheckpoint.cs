using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	private MigrationCheckpointDispatchStatus SendCheckpointToHotStandbyIfNeeded(double now = -1.0)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Disabled);
			return MigrationCheckpointDispatchStatus.NotRequested;
		}

		AuthorityCheckpointEnvelope latestEnvelope = _latestAuthorityCheckpointEnvelope;
		if (!IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| IsMigrationReadinessFenceActive()
			|| !latestEnvelope.HasPayload
			|| !NetworkMigrationRules.CanDispatchMigrationCheckpoint(
				latestEnvelope.CheckpointId,
				_migrationEligibleCheckpointId))
		{
			return MigrationCheckpointDispatchStatus.NotRequested;
		}
		int hostSeat = LocalSeatIndex;
		if (now < 0.0)
		{
			now = Time.GetTicksMsec() / 1000.0;
		}

		var excludedSeats = new HashSet<int>(_failedHotStandbySeats);
		foreach (NetworkPlayerPublicState player in _players)
		{
			if (!player.IsHost && !CanPeerActAsHotStandby(player.PeerId))
			{
				excludedSeats.Add(player.SeatIndex);
			}
		}

		var latencyMillisecondsBySeat = _players
			.Where(player => _migrationLatencyMillisecondsByPeerId.ContainsKey(player.PeerId))
			.ToDictionary(
				player => player.SeatIndex,
				player => _migrationLatencyMillisecondsByPeerId[player.PeerId]);
		int stableAcknowledgedSeat = _standbyAckedCheckpointId >= 0
			? _hotStandbySeat
			: -1;
		int nextStandbySeat = NetworkMigrationRules.SelectHotStandbySeat(
			GetCheckpointRoster(),
			GetCheckpointSeatControls(),
			hostSeat,
			excludedSeats,
			stableAcknowledgedSeat,
			latencyMillisecondsBySeat);
		if (nextStandbySeat < 0 || !TryGetConnectedRemotePlayerBySeatIndex(nextStandbySeat, out NetworkPlayerPublicState standby))
		{
			if (_hotStandbySeat >= 0 || _networkMigrationPlan is not null)
			{
				ReleaseHotStandbyCandidate(rekeyFormerCandidate: _hotStandbySeat >= 0);
			}
			else
			{
				TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.SelectingCandidate);
			}
			return MigrationCheckpointDispatchStatus.CandidateUnavailable;
		}

		if (_hotStandbySeat != nextStandbySeat)
		{
			ReleaseHotStandbyCandidate(rekeyFormerCandidate: _hotStandbySeat >= 0);
			_hotStandbySeat = nextStandbySeat;
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.SelectingCandidate);
		}

		if (NetworkMigrationRules.IsReadyForMigration(_migrationCoordinatorState)
			&& _standbyAckedCheckpointId == latestEnvelope.CheckpointId)
		{
			return MigrationCheckpointDispatchStatus.Ready;
		}

		if (!NetworkMigrationRules.CanSendStandbyCheckpoint(
			now,
			_lastStandbyCheckpointSentAt,
			_standbyPendingEncryptedCheckpoint is not null))
		{
			return _standbyPendingEncryptedCheckpoint is null
				? MigrationCheckpointDispatchStatus.CandidateUnavailable
				: MigrationCheckpointDispatchStatus.AwaitingAcknowledgement;
		}
		if (!TryGetPeerMigrationSecurity(standby.PeerId, out PeerMigrationSecurityState security)
			|| _authorityMigrationSigner is null)
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.KeyAgreement);
			return MigrationCheckpointDispatchStatus.CandidateUnavailable;
		}

		long planRevision = _migrationPlanRevision;
		if (planRevision <= 0)
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Failed);
			return MigrationCheckpointDispatchStatus.SigningFailed;
		}

		var securityContext = new MigrationCheckpointSecurityContext(
			_config.SessionId,
			_config.MatchId,
			GetCurrentAuthorityEpoch(),
			latestEnvelope.CheckpointId,
			standby.PlayerSessionId,
			planRevision,
			NetworkProtocol.CurrentVersion);
		EncryptedAuthorityCheckpointEnvelope? encryptedEnvelope = null;
		string encryptionError = string.Empty;
		for (int attempt = 0; attempt < 3; attempt++)
		{
			if (!HostMigrationSecurity.TryEncryptCheckpoint(
				latestEnvelope.PayloadMemory.Span,
				latestEnvelope.Hash,
				security.SessionKey,
				securityContext,
				_lastConfirmedCheckpointCiphertextHash,
				_authorityMigrationSigner,
				out EncryptedAuthorityCheckpointEnvelope candidateEnvelope,
				out encryptionError))
			{
				break;
			}

			if (_issuedStandbyCheckpointNonces.TryRegister(
				securityContext.AuthorityEpoch,
				candidateEnvelope.NonceMemory.Span))
			{
				encryptedEnvelope = candidateEnvelope;
				break;
			}
		}
		if (encryptedEnvelope is null)
		{
			GD.PushWarning($"Standby checkpoint encryption failed: {encryptionError}");
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.SelectingCandidate);
			return MigrationCheckpointDispatchStatus.EncryptionFailed;
		}

		_standbyPendingCheckpointId = latestEnvelope.CheckpointId;
		_standbyPendingCheckpointHash = latestEnvelope.Hash;
		_standbyPendingEncryptedCheckpoint = encryptedEnvelope;
		_standbyCheckpointSentAt = now;
		_lastStandbyCheckpointSentAt = now;
		try
		{
			RpcId(
				standby.PeerId,
				MethodName.RpcStandbyCheckpoint,
				_config.SessionId,
				_config.MatchId,
				GetCurrentAuthorityEpoch(),
				latestEnvelope.CheckpointId,
				planRevision,
				standby.PlayerSessionId,
				encryptedEnvelope.GetNonceCopy(),
				encryptedEnvelope.GetCiphertextCopy(),
				encryptedEnvelope.GetTagCopy(),
				encryptedEnvelope.PlaintextHash,
				encryptedEnvelope.CiphertextHash,
				encryptedEnvelope.PreviousCheckpointHash,
				encryptedEnvelope.SourceAuthorityKeyId,
				encryptedEnvelope.GetSignatureCopy()
				);
		}
		catch (Exception exception)
		{
			ClearPendingStandbyCheckpoint();
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.SelectingCandidate);
			GD.PushWarning($"Standby checkpoint send failed: {exception.GetType().Name}");
			return MigrationCheckpointDispatchStatus.SendFailed;
		}

		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement);
		return MigrationCheckpointDispatchStatus.AwaitingAcknowledgement;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcStandbyCheckpoint(
		string sessionId,
		string matchId,
		long authorityEpoch,
		long checkpointId,
		long planRevision,
		string candidatePlayerSessionId,
		byte[] nonce,
		byte[] ciphertext,
		byte[] tag,
		string plaintextHash,
		string ciphertextHash,
		string previousCheckpointHash,
		string sourceAuthorityKeyId,
		byte[] signature)
	{
		if (!HostMigrationCapabilityAvailable
			|| IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame))
		{
			return;
		}

		var context = new MigrationCheckpointSecurityContext(
			sessionId,
			matchId,
			authorityEpoch,
			checkpointId,
			candidatePlayerSessionId,
			planRevision,
			NetworkProtocol.CurrentVersion);
		var encryptedEnvelope = new EncryptedAuthorityCheckpointEnvelope(
			context,
			nonce,
			ciphertext,
			tag,
			plaintextHash,
			ciphertextHash,
			previousCheckpointHash,
			sourceAuthorityKeyId,
			signature);
		byte[] bytes = Array.Empty<byte>();
		string error = string.Empty;
		NetworkAuthorityCheckpoint checkpoint = new();
		bool matchesContext = MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch);
		if (matchesContext
			&& _standbyCheckpoint is not null
			&& !NetworkMigrationRules.IsSameCheckpointChain(
				_standbyCheckpoint,
				sessionId,
				matchId,
				authorityEpoch))
		{
			ClearLocalStandbyCheckpointCache();
			_networkMigrationPlan = null;
		}
		long previousPlanRevision = -1;
		bool hasPreviousCheckpoint = _standbyCheckpoint is not null;
		if (hasPreviousCheckpoint
			&& _standbyCheckpointsById.TryGetValue(_standbyCheckpoint!.CheckpointId, out var previousEnvelope))
		{
			previousPlanRevision = previousEnvelope.PlanRevision;
		}

		bool accepted = false;
		if (!matchesContext)
		{
			error = "migration_checkpoint_context_invalid";
		}
		else if (!string.Equals(candidatePlayerSessionId, GetPlayerSessionId(), StringComparison.Ordinal))
		{
			error = "migration_checkpoint_candidate_invalid";
		}
		else if (hasPreviousCheckpoint && previousPlanRevision < 0)
		{
			error = "migration_checkpoint_cache_invalid";
		}
		else if (!NetworkMigrationRules.CanAcceptCheckpointChainAdvance(
			previousCheckpointHash,
			_standbyConfirmedCheckpointCiphertextHash,
			checkpointId,
			hasPreviousCheckpoint ? _standbyCheckpoint!.CheckpointId : -1,
			planRevision,
			previousPlanRevision))
		{
			error = "migration_checkpoint_chain_invalid";
		}
		else if (_localMigrationSessionKey.Length != HostMigrationSecurity.SessionKeyBytes)
		{
			error = "migration_checkpoint_session_key_unavailable";
		}
		else if (_boundAuthorityMigrationPublicKey.Length == 0)
		{
			error = "migration_checkpoint_authority_key_unavailable";
		}
		else if (_acceptedStandbyCheckpointNonces.HasSeen(authorityEpoch, nonce))
		{
			error = "migration_checkpoint_nonce_replayed";
		}
		else if (HostMigrationSecurity.TryDecryptCheckpoint(
				encryptedEnvelope,
				_localMigrationSessionKey,
				_boundAuthorityMigrationPublicKey,
				out bytes,
				out error)
			&& NetworkAuthorityCheckpointCodec.TryDecode(bytes, plaintextHash, out checkpoint, out error))
		{
			if (checkpoint.CheckpointId != checkpointId)
			{
				error = "migration_checkpoint_id_invalid";
			}
			else if (checkpoint.AuthorityEpoch != authorityEpoch)
			{
				error = "migration_checkpoint_epoch_invalid";
			}
			else if (!checkpoint.Roster.Any(entry => entry.PlayerSessionId == GetPlayerSessionId() && entry.IsConnected))
			{
				error = "migration_checkpoint_roster_invalid";
			}
			else if (!_acceptedStandbyCheckpointNonces.TryRegister(authorityEpoch, nonce))
			{
				error = "migration_checkpoint_nonce_replayed";
			}
			else
			{
				accepted = true;
			}
		}
		if (!accepted)
		{
			if (string.IsNullOrEmpty(error))
			{
				error = "migration_checkpoint_validation_failed";
			}
			if (bytes.Length > 0)
			{
				CryptographicOperations.ZeroMemory(bytes);
			}
			RpcId(
				1,
				MethodName.RpcStandbyCheckpointAck,
				sessionId,
				matchId,
				authorityEpoch,
				false,
				checkpointId,
				planRevision,
				ciphertextHash,
				error,
				string.Empty);
			return;
		}

		_standbyCheckpoint = checkpoint;
		_standbyCheckpointPayload = bytes;
		_standbyCheckpointHash = plaintextHash;
		_standbyCheckpointCiphertextHash = ciphertextHash;
		_standbyCheckpointsById[checkpointId] = (
			checkpoint,
			bytes,
			plaintextHash,
			ciphertextHash,
			planRevision,
			previousCheckpointHash);
		PruneStandbyCheckpoints();
		RpcId(
			1,
			MethodName.RpcStandbyCheckpointAck,
			sessionId,
			matchId,
			authorityEpoch,
			true,
			checkpointId,
			planRevision,
			ciphertextHash,
			string.Empty,
				GetLocalMigrationEndpointAcknowledgement());
		GD.Print($"STANDBY_ACKED checkpoint={checkpointId} hash={ciphertextHash}");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcStandbyCheckpointAck(
		string sessionId,
		string matchId,
		long authorityEpoch,
		bool accepted,
		long checkpointId,
		long planRevision,
		string ciphertextHash,
		string error,
		string candidateHostAddress)
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame))
		{
			return;
		}
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!TryResolveSeatFromPeer(senderPeerId, out int senderSeat)
			|| senderSeat != _hotStandbySeat
			|| _standbyPendingCheckpointId < 0
			|| _standbyPendingEncryptedCheckpoint is not { } pendingEncrypted
			|| checkpointId != _standbyPendingCheckpointId
			|| planRevision != pendingEncrypted.Context.PlanRevision
			|| !string.Equals(ciphertextHash, pendingEncrypted.CiphertextHash, StringComparison.Ordinal))
		{
			return;
		}
		if (!AllowPeerRequest(NetworkRequestCategory.ProtocolAck))
		{
			return;
		}
		if (!accepted)
		{
			string boundedError = (error ?? string.Empty)[..Math.Min(error?.Length ?? 0, NetworkProtocol.MaxRemoteErrorLength)];
			GD.PushWarning($"Standby checkpoint rejected: {boundedError}");
			TryInvalidateMigrationAvailability(out _);
			return;
		}
		if (!IsMigrationEndpointAcknowledgementValid(_config.Transport, candidateHostAddress))
		{
			TryInvalidateMigrationAvailability(out _);
			return;
		}

		long acknowledgedCheckpointId = _standbyPendingCheckpointId;
		string acknowledgedCheckpointHash = _standbyPendingCheckpointHash;
		EncryptedAuthorityCheckpointEnvelope acknowledgedEncryptedEnvelope = pendingEncrypted;
		_standbyAckedCheckpointId = checkpointId;
		_lastStandbyCheckpointAckedAt = Time.GetTicksMsec() / 1000.0;
		_lastConfirmedCheckpointCiphertextHash = acknowledgedEncryptedEnvelope.CiphertextHash;
		ClearPendingStandbyCheckpoint();
		_standbyCandidateHostAddress = _config.Transport == NetworkTransport.Lan
			? candidateHostAddress.Trim()
			: string.Empty;
		_consecutiveUnackedCheckpoints = 0;
		MigrationCheckpointDispatchStatus dispatchStatus = BroadcastMigrationPlanIfReady(
			senderSeat,
			candidateHostAddress.Trim(),
			acknowledgedCheckpointId,
			acknowledgedCheckpointHash,
			acknowledgedEncryptedEnvelope);
		if (dispatchStatus is not (MigrationCheckpointDispatchStatus.AwaitingAcknowledgement
			or MigrationCheckpointDispatchStatus.Ready))
		{
			TryInvalidateMigrationAvailability(out _);
		}
	}

	private void HandleHotStandbyDisconnected(int seatIndex)
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| seatIndex != _hotStandbySeat)
		{
			return;
		}

		_failedHotStandbySeats.Add(seatIndex);
		ReleaseHotStandbyCandidate(rekeyFormerCandidate: false);
	}


	private void ClearPendingStandbyCheckpoint()
	{
		_standbyPendingCheckpointId = -1;
		_standbyPendingCheckpointHash = string.Empty;
		_standbyPendingEncryptedCheckpoint = null;
		_standbyCheckpointSentAt = -1.0;
	}


	private void ResetMigrationArtifactsForNewMatch()
	{
		ClearPendingLocalReadyIntent();
		_migrationReadyEpoch = -1;
		_lastCompletedMigrationReadyEpoch = -1;
		_migrationRecoveryReadyForPeers = false;
		_gameMigrationRecoveryState = null;
		_networkMigrationPlan = null;
		ClearPendingMigrationPlan();
		ClearLocalStandbyCheckpointCache();
		_standbyAckedCheckpointId = -1;
		_migrationEligibleCheckpointId = -1;
		ClearPendingStandbyCheckpoint();
		_lastStandbyCheckpointSentAt = -1.0;
		_lastStandbyCheckpointAckedAt = -1.0;
		_consecutiveUnackedCheckpoints = 0;
		_standbyCandidateHostAddress = string.Empty;
		_lastConfirmedCheckpointCiphertextHash = string.Empty;
		_migrationCoordinatorState = NetworkMigrationCoordinatorState.Disabled;
		_lastObservedMigrationPlanRevision = 0;
		_pendingMigrationPlanRevocationRevision = -1;
		ResetMigrationTransportState();
		_migrationLatencyMillisecondsByPeerId.Clear();
		_pendingMigrationLatencyProbesByPeerId.Clear();
		_lastMigrationLatencyProbeAt = -1.0;
		_nextMigrationLatencyProbeSequence = 0;
	}

	private void ReleaseHotStandbyCandidate(bool rekeyFormerCandidate, bool invalidateAvailability = true)
	{
		if (invalidateAvailability
			&& (_networkMigrationPlan is not null
				|| _pendingNetworkMigrationPlan is not null
				|| _standbyAckedCheckpointId >= 0
				|| _standbyPendingCheckpointId >= 0
				|| NetworkMigrationRules.IsReadyForMigration(_migrationCoordinatorState)))
		{
			TryInvalidateMigrationAvailability(out _);
		}

		int formerCandidateSeat = _hotStandbySeat;
		if (rekeyFormerCandidate && formerCandidateSeat >= 0)
		{
			RekeyFormerHotStandby(formerCandidateSeat);
		}

		_hotStandbySeat = -1;
		_standbyAckedCheckpointId = -1;
		_migrationEligibleCheckpointId = -1;
		ClearPendingStandbyCheckpoint();
		_lastStandbyCheckpointSentAt = -1.0;
		_consecutiveUnackedCheckpoints = 0;
		_standbyCandidateHostAddress = string.Empty;
		_lastConfirmedCheckpointCiphertextHash = string.Empty;
		ClearLocalStandbyCheckpointCache();
		_networkMigrationPlan = null;
		ClearPendingMigrationPlan();
	}

	private void ClearLocalStandbyCheckpointCache()
	{
		if (_standbyCheckpointPayload.Length > 0)
		{
			CryptographicOperations.ZeroMemory(_standbyCheckpointPayload);
		}
		foreach (var checkpointEnvelope in _standbyCheckpointsById.Values)
		{
			if (checkpointEnvelope.Payload.Length > 0)
			{
				CryptographicOperations.ZeroMemory(checkpointEnvelope.Payload);
			}
		}

		_standbyCheckpoint = null;
		_standbyCheckpointPayload = Array.Empty<byte>();
		_standbyCheckpointHash = string.Empty;
		_standbyCheckpointCiphertextHash = string.Empty;
		_standbyConfirmedCheckpointCiphertextHash = string.Empty;
		_standbyCheckpointsById.Clear();
		_acceptedStandbyCheckpointNonces.Reset();
	}

	private void TransferStandbyCheckpointPayloadOwnership(long checkpointId, byte[] payload)
	{
		if (ReferenceEquals(_standbyCheckpointPayload, payload))
		{
			_standbyCheckpointPayload = Array.Empty<byte>();
		}
		_standbyCheckpointsById.Remove(checkpointId);
		foreach (var cached in _standbyCheckpointsById.Values)
		{
			if (!ReferenceEquals(cached.Payload, payload) && cached.Payload.Length > 0)
			{
				CryptographicOperations.ZeroMemory(cached.Payload);
			}
		}
		_standbyCheckpointsById.Clear();
		_standbyCheckpoint = null;
		_standbyCheckpointHash = string.Empty;
		_standbyCheckpointCiphertextHash = string.Empty;
		_standbyConfirmedCheckpointCiphertextHash = string.Empty;
		_acceptedStandbyCheckpointNonces.Reset();
	}

	private void PruneStandbyCheckpoints()
	{
		long protectedCheckpointId = _networkMigrationPlan?.CheckpointId ?? -1;
		while (_standbyCheckpointsById.Count > 2)
		{
			long removableCheckpointId = _standbyCheckpointsById.Keys
				.Where(checkpointId => checkpointId != protectedCheckpointId)
				.DefaultIfEmpty(-1)
				.Min();
			if (removableCheckpointId < 0)
			{
				break;
			}
			if (_standbyCheckpointsById.Remove(removableCheckpointId, out var removed)
				&& removed.Payload.Length > 0)
			{
				CryptographicOperations.ZeroMemory(removed.Payload);
			}
		}
	}

}
