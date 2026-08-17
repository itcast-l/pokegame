using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	private MigrationCheckpointDispatchStatus BroadcastMigrationPlanIfReady(
		int candidateSeat,
		string candidateHostAddress,
		long acknowledgedCheckpointId,
		string acknowledgedCheckpointHash,
		EncryptedAuthorityCheckpointEnvelope acknowledgedEncryptedEnvelope)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return MigrationCheckpointDispatchStatus.NotRequested;
		}
		if (!IsHost
			|| IsMigrationReadinessFenceActive()
			|| acknowledgedCheckpointId < 0
			|| string.IsNullOrWhiteSpace(acknowledgedCheckpointHash)
			|| !TryGetPlayerBySeat(candidateSeat, out NetworkPlayerPublicState candidate)
			|| !TryGetPeerMigrationSecurity(candidate.PeerId, out PeerMigrationSecurityState security)
			|| _authorityMigrationSigner is null)
		{
			return _authorityMigrationSigner is null
				? MigrationCheckpointDispatchStatus.SigningFailed
				: MigrationCheckpointDispatchStatus.CandidateUnavailable;
		}

		string advertisedCheckpointHash = acknowledgedCheckpointHash;
		if (OS.GetEnvironment("CODEX_LAN_MIGRATION_CORRUPT_HASH") == "1")
		{
			advertisedCheckpointHash = new string('0', 64);
		}
		if (!TryBuildMigrationTransportDescriptor(
				candidate.PeerId,
				candidateHostAddress,
				out NetworkMigrationTransportDescriptor transportDescriptor))
		{
			return MigrationCheckpointDispatchStatus.CandidateUnavailable;
		}

		NetworkMigrationPlan plan = new()
		{
			ProtocolVersion = NetworkProtocol.CurrentVersion,
			PlanRevision = acknowledgedEncryptedEnvelope.Context.PlanRevision,
			SessionId = _config.SessionId,
			MatchId = _config.MatchId,
			SourceEpoch = GetCurrentAuthorityEpoch(),
			TargetEpoch = GetCurrentAuthorityEpoch() + 1,
			CheckpointId = acknowledgedCheckpointId,
			CheckpointHash = advertisedCheckpointHash,
			CheckpointCiphertextHash = acknowledgedEncryptedEnvelope.CiphertextHash,
			PreviousCheckpointHash = acknowledgedEncryptedEnvelope.PreviousCheckpointHash,
			CandidatePlayerSessionId = candidate.PlayerSessionId,
			CandidateSeat = candidate.SeatIndex,
			CandidateSuccessorPublicKey = HostMigrationSecurity.EncodeBytes(security.SuccessorPublicKey),
			CandidateSuccessorKeyId = security.SuccessorKeyId,
			SourcePlayerSessionId = GetPlayerSessionIdForSeat(LocalSeatIndex),
			SourceAuthorityKeyId = acknowledgedEncryptedEnvelope.SourceAuthorityKeyId,
			Transport = _config.Transport,
			TransportDescriptor = transportDescriptor,
			Phase = _state == NetworkSessionState.Lobby
				? NetworkAuthorityPhase.Lobby
				: NetworkAuthorityPhase.InGame,
		};
		if (!HostMigrationSecurity.TrySignMigrationPlan(plan, _authorityMigrationSigner, out plan)
			|| !NetworkMigrationRules.IsPlanVisibleToClients(plan))
		{
			return MigrationCheckpointDispatchStatus.SigningFailed;
		}

		_pendingNetworkMigrationPlan = plan;
		_migrationPlanAcknowledgedSeatIndices.Clear();
		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement);
		Godot.Collections.Dictionary payload = ToMigrationPlanVariant(plan);
		try
		{
			foreach (NetworkPlayerPublicState player in _lobbyRoster.Players)
			{
				if (player.IsConnected && !player.IsHost)
				{
					RpcId(player.PeerId, MethodName.RpcMigrationPlan, payload);
				}
			}
		}
		catch (Exception exception)
		{
			GD.PushWarning($"Migration plan send failed: {exception.GetType().Name}");
			return MigrationCheckpointDispatchStatus.SendFailed;
		}

		return MigrationCheckpointDispatchStatus.AwaitingAcknowledgement;
	}

	private void PollMigrationCandidateLatency(double now)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		_expiredMigrationLatencyProbePeerIds.Clear();
		foreach (KeyValuePair<int, (long Sequence, double SentAt)> entry in _pendingMigrationLatencyProbesByPeerId)
		{
			if (now - entry.Value.SentAt >= NetworkMigrationRules.MigrationLatencyProbeTimeoutSeconds)
			{
				_expiredMigrationLatencyProbePeerIds.Add(entry.Key);
			}
		}

		foreach (int peerId in _expiredMigrationLatencyProbePeerIds)
		{
			_pendingMigrationLatencyProbesByPeerId.Remove(peerId);
			_migrationLatencyMillisecondsByPeerId.Remove(peerId);
		}
		_expiredMigrationLatencyProbePeerIds.Clear();

		if (!IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| (_lastMigrationLatencyProbeAt >= 0.0
				&& now - _lastMigrationLatencyProbeAt < NetworkMigrationRules.MigrationLatencyProbeIntervalSeconds))
		{
			return;
		}

		_lastMigrationLatencyProbeAt = now;
		NetworkRpcContext context = GetCurrentRpcContext();
		foreach (NetworkPlayerPublicState player in _lobbyRoster.Players)
		{
			if (!player.IsConnected || player.IsHost || !IsPeerSecurityAuthenticated(player.PeerId))
			{
				continue;
			}

			long sequence = ++_nextMigrationLatencyProbeSequence;
			_pendingMigrationLatencyProbesByPeerId[player.PeerId] = (sequence, now);
			RpcId(
				player.PeerId,
				MethodName.RpcMigrationLatencyProbe,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				sequence);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcMigrationLatencyProbe(
		string sessionId,
		string matchId,
		long authorityEpoch,
		long sequence)
	{
		if (!HostMigrationCapabilityAvailable
			|| IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| sequence <= 0
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		RpcId(
			1,
			MethodName.RpcMigrationLatencyProbeAck,
			sessionId,
			matchId,
			authorityEpoch,
			sequence);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcMigrationLatencyProbeAck(
		string sessionId,
		string matchId,
		long authorityEpoch,
		long sequence)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch)
			|| !_pendingMigrationLatencyProbesByPeerId.TryGetValue(senderPeerId, out var pending)
			|| pending.Sequence != sequence
			|| !AllowPeerRequest(NetworkRequestCategory.ProtocolAck))
		{
			return;
		}

		double now = Time.GetTicksMsec() / 1000.0;
		_pendingMigrationLatencyProbesByPeerId.Remove(senderPeerId);
		_migrationLatencyMillisecondsByPeerId[senderPeerId] = Math.Max(0.0, (now - pending.SentAt) * 1000.0);
	}

	private void ClearMigrationCandidateLatency(int peerId)
	{
		_pendingMigrationLatencyProbesByPeerId.Remove(peerId);
		_migrationLatencyMillisecondsByPeerId.Remove(peerId);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcMigrationPlan(Godot.Collections.Dictionary rawPlan)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		if (!TryParseMigrationPlan(rawPlan, out NetworkMigrationPlan plan))
		{
			GD.Print("MIGRATION_PLAN_REJECT reason=Decode");
			return;
		}
		if (!NetworkMigrationRules.IsPlanVisibleToClients(plan))
		{
			GD.Print($"MIGRATION_PLAN_REJECT reason=Visibility source_epoch={plan.SourceEpoch} target_epoch={plan.TargetEpoch} candidate={plan.CandidatePlayerSessionId}");
			return;
		}
		if (!HostMigrationSecurity.VerifyMigrationPlan(plan, _boundAuthorityMigrationPublicKey))
		{
			GD.Print("MIGRATION_PLAN_REJECT reason=Signature");
			return;
		}
		if (!MatchesCurrentRpcContext(plan.SessionId, plan.MatchId, plan.SourceEpoch, allowHigherEpoch: false))
		{
			GD.Print($"MIGRATION_PLAN_REJECT reason=Context current_epoch={GetCurrentAuthorityEpoch()} plan_epoch={plan.SourceEpoch} session_match={plan.SessionId.Length}:{plan.MatchId.Length}");
			return;
		}
		if (!string.IsNullOrWhiteSpace(_config.MatchId)
			&& !string.Equals(_config.MatchId, plan.MatchId, StringComparison.Ordinal))
		{
			GD.Print($"MIGRATION_PLAN_REJECT reason=MatchMismatch current_len={_config.MatchId.Length} plan_len={plan.MatchId.Length}");
			return;
		}

		if (IsMigrationReadinessFenceActive())
		{
			GD.Print($"MIGRATION_PLAN_REJECT reason=ReadinessFence target_epoch={plan.TargetEpoch} checkpoint={plan.CheckpointId}");
			return;
		}

		if (!NetworkMigrationRules.CanAcceptPlanRevisionAfterRevocation(
				_lastObservedMigrationPlanRevision,
				_pendingMigrationPlanRevocationRevision,
				plan))
		{
			GD.Print($"MIGRATION_PLAN_REJECT reason=Revision incoming={plan.PlanRevision} observed={_lastObservedMigrationPlanRevision}");
			return;
		}

		if (NetworkMigrationRules.ShouldAcceptPlanUpdate(_networkMigrationPlan, plan))
		{
			if (_networkMigrationPlan is { } previousPlan
				&& IsLocalMigrationCandidate(previousPlan)
				&& !IsLocalMigrationCandidate(plan))
			{
				ClearLocalStandbyCheckpointCache();
				ClearLocalMigrationSessionKey();
			}
			_networkMigrationPlan = plan;
			if (IsLocalMigrationCandidate(plan))
			{
				_standbyConfirmedCheckpointCiphertextHash = plan.CheckpointCiphertextHash;
			}
			_lastObservedMigrationPlanRevision = plan.PlanRevision;
			_pendingMigrationPlanRevocationRevision = -1;
			TransitionMigrationCoordinatorToReady();
			GD.Print($"MIGRATION_PLAN_ACCEPTED candidate={plan.CandidatePlayerSessionId} seat={plan.CandidateSeat} target_epoch={plan.TargetEpoch}");
			try
			{
				RpcId(
					1,
					MethodName.RpcMigrationPlanAck,
					plan.SessionId,
					plan.MatchId,
					plan.SourceEpoch,
					plan.TargetEpoch,
					plan.PlanRevision,
					plan.CheckpointId,
					plan.CheckpointCiphertextHash,
					plan.CandidatePlayerSessionId,
					plan.CandidateSuccessorKeyId);
			}
			catch (Exception exception)
			{
				GD.PushWarning($"Migration plan acknowledgement send failed: {exception.GetType().Name}");
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcMigrationPlanAck(
		string sessionId,
		string matchId,
		long sourceEpoch,
		long targetEpoch,
		long planRevision,
		long checkpointId,
		string checkpointCiphertextHash,
		string candidatePlayerSessionId,
		string candidateSuccessorKeyId)
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| _migrationCoordinatorState != NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement
			|| _pendingNetworkMigrationPlan is not { } plan
			|| !string.Equals(sessionId, plan.SessionId, StringComparison.Ordinal)
			|| !string.Equals(matchId, plan.MatchId, StringComparison.Ordinal)
			|| sourceEpoch != plan.SourceEpoch
			|| targetEpoch != plan.TargetEpoch
			|| planRevision != plan.PlanRevision
			|| checkpointId != plan.CheckpointId
			|| !string.Equals(checkpointCiphertextHash, plan.CheckpointCiphertextHash, StringComparison.Ordinal)
			|| !string.Equals(candidatePlayerSessionId, plan.CandidatePlayerSessionId, StringComparison.Ordinal)
			|| !string.Equals(candidateSuccessorKeyId, plan.CandidateSuccessorKeyId, StringComparison.Ordinal))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!TryResolveSeatFromPeer(senderPeerId, out int senderSeat)
			|| !TryGetPlayerBySeat(senderSeat, out NetworkPlayerPublicState sender)
			|| !sender.IsConnected
			|| sender.IsHost
			|| !AllowPeerRequest(NetworkRequestCategory.ProtocolAck))
		{
			return;
		}

		_migrationPlanAcknowledgedSeatIndices.Add(senderSeat);
		if (!NetworkMigrationRules.ArePlanAcknowledgementsComplete(
			_lobbyRoster.Players
				.Where(player => player.IsConnected && !player.IsHost)
				.Select(player => player.SeatIndex),
			_migrationPlanAcknowledgedSeatIndices))
		{
			return;
		}

		_networkMigrationPlan = plan;
		ClearPendingMigrationPlan();
		_lastObservedMigrationPlanRevision = plan.PlanRevision;
		_pendingMigrationPlanRevocationRevision = -1;
		PruneStandbyCheckpoints();
		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Ready);
		GD.Print($"MIGRATION_PLAN_READY phase={plan.Phase} candidate_seat={plan.CandidateSeat} checkpoint={plan.CheckpointId} hash={plan.CheckpointHash}");
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcMigrationPlanRevoked(Godot.Collections.Dictionary rawRevocation)
	{
		if (!HostMigrationCapabilityAvailable
			|| !TryParseMigrationPlanRevocation(rawRevocation, out NetworkMigrationPlanRevocation revocation)
			|| !HostMigrationSecurity.VerifyMigrationPlanRevocation(revocation, _boundAuthorityMigrationPublicKey)
			|| !MatchesCurrentRpcContext(
				revocation.SessionId,
				revocation.MatchId,
				revocation.SourceEpoch,
				allowHigherEpoch: false)
			|| revocation.PlanRevision <= _lastObservedMigrationPlanRevision)
		{
			return;
		}
		if (IsMigrationRecoveryHandoffInProgress())
		{
			return;
		}

		if (_networkMigrationPlan is { } currentPlan
			&& (currentPlan.PlanRevision != revocation.RevokedPlanRevision
				|| currentPlan.CheckpointId != revocation.RevokedCheckpointId))
		{
			return;
		}

		_networkMigrationPlan = null;
		_lastObservedMigrationPlanRevision = revocation.PlanRevision;
		_pendingMigrationPlanRevocationRevision = revocation.PlanRevision;
		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.SelectingCandidate);
		GD.Print($"MIGRATION_PLAN_REVOKED revision={revocation.PlanRevision} checkpoint={revocation.RevokedCheckpointId}");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcDebugStaleMigrationPlan(Godot.Collections.Dictionary rawPlan)
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| OS.GetEnvironment("CODEX_LAN_INJECT_OLD_EPOCH") != "1")
		{
			return;
		}

		// Keep the production migration-plan RPC authority-only. The test-only
		// relay feeds the same validator on the new host after a follower binds.
		RpcMigrationPlan(rawPlan);
	}


	private void ClearPendingMigrationPlan()
	{
		_pendingNetworkMigrationPlan = null;
		_migrationPlanAcknowledgedSeatIndices.Clear();
	}

	private bool TryInvalidateMigrationAvailability(out MigrationCheckpointDispatchStatus dispatchStatus)
	{
		dispatchStatus = MigrationCheckpointDispatchStatus.NotRequested;
		if (!HostMigrationCapabilityAvailable)
		{
			_networkMigrationPlan = null;
			ClearPendingMigrationPlan();
			_standbyAckedCheckpointId = -1;
			_migrationEligibleCheckpointId = -1;
			ClearPendingStandbyCheckpoint();
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Disabled);
			return true;
		}
		if (IsActiveMigrationHandoffInProgress())
		{
			return true;
		}

		NetworkMigrationPlan? revokedPlan = _networkMigrationPlan ?? _pendingNetworkMigrationPlan;
		bool isRecoveringSourcePlan = revokedPlan is not null
			&& revokedPlan.SourceEpoch != GetCurrentAuthorityEpoch()
			&& (_migrationCoordinatorState is NetworkMigrationCoordinatorState.CandidateStarting
				or NetworkMigrationCoordinatorState.FollowersRecovering);
		if (isRecoveringSourcePlan)
		{
			// This plan was signed by the source authority and is still needed by
			// recovery RPCs. The new authority cannot revoke it with a different key.
			return true;
		}
		try
		{
			_migrationPlanRevision = checked(_migrationPlanRevision + 1);
		}
		catch (OverflowException)
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Failed);
			return false;
		}

		_networkMigrationPlan = null;
		ClearPendingMigrationPlan();
		_standbyAckedCheckpointId = -1;
		_migrationEligibleCheckpointId = -1;
		ClearPendingStandbyCheckpoint();
		_lastStandbyCheckpointSentAt = -1.0;
		_standbyCandidateHostAddress = string.Empty;
		if (revokedPlan is not null)
		{
			dispatchStatus = TryBroadcastMigrationPlanRevocation(revokedPlan);
			if (dispatchStatus != MigrationCheckpointDispatchStatus.Ready)
			{
				TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Failed);
				return false;
			}
		}

		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.SelectingCandidate);
		return true;
	}

	private MigrationCheckpointDispatchStatus TryBroadcastMigrationPlanRevocation(NetworkMigrationPlan revokedPlan)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return MigrationCheckpointDispatchStatus.NotRequested;
		}

		if (_authorityMigrationSigner is null)
		{
			GD.PrintErr("Migration plan revocation signing failed: signer_unavailable");
			return MigrationCheckpointDispatchStatus.SigningFailed;
		}

		NetworkMigrationPlanRevocation unsignedRevocation = new()
		{
			ProtocolVersion = NetworkProtocol.CurrentVersion,
			PlanRevision = _migrationPlanRevision,
			RevokedPlanRevision = revokedPlan.PlanRevision,
			RevokedCheckpointId = revokedPlan.CheckpointId,
			SessionId = revokedPlan.SessionId,
			MatchId = revokedPlan.MatchId,
			SourceEpoch = revokedPlan.SourceEpoch,
			SourceAuthorityKeyId = revokedPlan.SourceAuthorityKeyId,
		};
		if (!HostMigrationSecurity.TrySignMigrationPlanRevocation(
					unsignedRevocation,
					_authorityMigrationSigner,
					out NetworkMigrationPlanRevocation signedRevocation))
		{
			string signerKeyId = HostMigrationSecurity.ComputeKeyId(
				HostMigrationSecurity.ExportPublicKey(_authorityMigrationSigner));
			GD.PrintErr(
				$"Migration plan revocation signing failed: valid={NetworkMigrationRules.IsUnsignedPlanRevocationValid(unsignedRevocation)} revision={unsignedRevocation.PlanRevision} revoked_revision={unsignedRevocation.RevokedPlanRevision} key_match={string.Equals(unsignedRevocation.SourceAuthorityKeyId, signerKeyId, StringComparison.Ordinal)}");
			return MigrationCheckpointDispatchStatus.SigningFailed;
		}

		Godot.Collections.Dictionary payload = ToMigrationPlanRevocationVariant(signedRevocation);
		try
		{
			foreach (NetworkPlayerPublicState player in _lobbyRoster.Players)
			{
				if (player.IsConnected && !player.IsHost)
				{
					RpcId(player.PeerId, MethodName.RpcMigrationPlanRevoked, payload);
				}
			}
		}
		catch (Exception exception)
		{
			GD.PushWarning($"Migration plan revocation send failed: {exception.GetType().Name}");
			return MigrationCheckpointDispatchStatus.SendFailed;
		}

		return MigrationCheckpointDispatchStatus.Ready;
	}

}
