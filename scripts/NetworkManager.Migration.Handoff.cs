using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBeginHostMigration(
		string sessionId,
		string matchId,
		long sourceEpoch,
		long targetEpoch,
		long checkpointId,
		string checkpointHash)
	{
		if (!HostMigrationCapabilityAvailable
			|| IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| !MatchesCurrentRpcContext(sessionId, matchId, sourceEpoch)
			|| !NetworkMigrationRules.IsReadyForMigration(_migrationCoordinatorState)
			|| _networkMigrationPlan is not { } plan
			|| plan.TargetEpoch != targetEpoch
			|| plan.CheckpointId != checkpointId
			|| !string.Equals(plan.CheckpointHash, checkpointHash, StringComparison.Ordinal))
		{
			return;
		}

		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.SourceHostLost);
		TransitionMigrationCoordinatorState(IsLocalMigrationCandidate(plan)
			? NetworkMigrationCoordinatorState.CandidateStarting
			: NetworkMigrationCoordinatorState.FollowersRecovering);
		BeginMigrationReadinessFence(plan);
		double now = Time.GetTicksMsec() / 1000.0;
		ResetMigrationTransportState();
		InitializeMigrationTransportState(plan, now);
		_migrationAwaitingSourceDisconnect = true;
		_migrationStartedAt = -1.0;
		_nextMigrationAttemptAt = 0.0;
		_migrationServerStarted = false;
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
		SetState(NetworkSessionState.MigratingHost);
		if (TryGetPresentationEndpoint(out INetworkPresentationEndpoint gameBoard))
		{
			gameBoard.BeginHostMigration();
		}

		RpcId(
			1,
			MethodName.RpcConfirmHostMigration,
			sessionId,
			matchId,
			sourceEpoch,
			plan.TargetEpoch,
			plan.CheckpointId,
			GetPlayerSessionId());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcConfirmHostMigration(
		string sessionId,
		string matchId,
		long sourceEpoch,
		long targetEpoch,
		long checkpointId,
		string playerSessionId)
	{
		if (!HostMigrationCapabilityAvailable
			|| !AllowPeerRequest(NetworkRequestCategory.ProtocolAck))
		{
			return;
		}
		if (!IsHost
			|| !_activeMigrationPending
			|| _networkMigrationPlan is not { } plan
			|| !MatchesCurrentRpcContext(sessionId, matchId, sourceEpoch)
			|| plan.TargetEpoch != targetEpoch
			|| plan.CheckpointId != checkpointId)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!TryResolveSeatFromPeer(senderPeerId, out int senderSeat)
			|| !TryGetPlayerBySeat(senderSeat, out NetworkPlayerPublicState sender)
			|| !string.Equals(sender.PlayerSessionId, playerSessionId, StringComparison.Ordinal)
			|| sender.IsHost)
		{
			return;
		}

		_activeMigrationConfirmedSeats.Add(senderSeat);
	}


	private void BeginMigrationAfterHostDisconnect()
	{
		if (!HostMigrationCapabilityAvailable)
		{
			HandleHostDisconnected();
			return;
		}

		if (!NetworkMigrationRules.IsReadyForMigration(_migrationCoordinatorState)
			|| _networkMigrationPlan is not { } plan
			|| !NetworkMigrationRules.IsPlanVisibleToClients(plan))
		{
			FailMigration(NetworkMigrationFailureCode.NoPlan);
			return;
		}

		StartMigrationAttemptsAfterHostDisconnect(plan);
	}

	private bool TryAdvancePlannedMigrationAfterSourceDisconnect()
	{
		if (!HostMigrationCapabilityAvailable
			|| _state != NetworkSessionState.MigratingHost
			|| !_migrationAwaitingSourceDisconnect)
		{
			return false;
		}
		if (_networkMigrationPlan is not { } plan
			|| !NetworkMigrationRules.IsPlanVisibleToClients(plan))
		{
			FailMigration(NetworkMigrationFailureCode.NoPlan);
			return true;
		}

		CloseActivePeerForMigration();
		ArmMigrationAttemptsAfterSourceDisconnect(
			plan,
			Time.GetTicksMsec() / 1000.0);
		RescheduleNetworkMaintenance(immediate: true);
		return true;
	}

	private void StartMigrationAttemptsAfterHostDisconnect(NetworkMigrationPlan plan)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			HandleHostDisconnected();
			return;
		}

		if (_migrationCoordinatorState is not (NetworkMigrationCoordinatorState.Ready or NetworkMigrationCoordinatorState.SourceHostLost))
		{
			FailMigration(NetworkMigrationFailureCode.NoPlan);
			return;
		}

		if (NetworkMigrationRules.IsReadyForMigration(_migrationCoordinatorState))
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.SourceHostLost);
		}
		TransitionMigrationCoordinatorState(IsLocalMigrationCandidate(plan)
			? NetworkMigrationCoordinatorState.CandidateStarting
			: NetworkMigrationCoordinatorState.FollowersRecovering);
		BeginMigrationReadinessFence(plan);
		_activeMigrationAwaitingSourceClose = false;
		ResetMigrationTransportState();
		double now = Time.GetTicksMsec() / 1000.0;
		InitializeMigrationTransportState(plan, now);
		_migrationAwaitingSourceDisconnect = false;
		CloseActivePeerForMigration();
		ArmMigrationAttemptsAfterSourceDisconnect(plan, now);
		GD.Print($"MIGRATION_BEGIN match={_config.MatchId.Length} credential_generation={_localSeatCredentialGeneration} address={_lastLanJoinAddress}");
		SetState(NetworkSessionState.MigratingHost);
		if (TryGetPresentationEndpoint(out INetworkPresentationEndpoint gameBoard))
		{
			gameBoard.BeginHostMigration();
		}
	}

	private void FinishActiveMigrationSource()
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		ReturnToMultiplayerLobbyAfterMigrationFailure(
			"Active host migration handed off.",
			NetworkSessionCloseReason.MigrationHandoff);
	}

	private void CloseActivePeerForMigration()
	{
		MultiplayerPeer? peer = Multiplayer.MultiplayerPeer;
		if (_migrationTransportAdapter is { } transport)
		{
			transport.Close(peer);
		}
		else
		{
			peer?.Close();
		}
		Multiplayer.MultiplayerPeer = null;
		_enetPeer = null;
		_steamPeer = null;
		_reconnectAttemptInFlight = false;
		_reconnectAttemptStartedAt = -1.0;
	}

	private void HandleMigrationConnectionFailure()
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
		MultiplayerPeer? peer = Multiplayer.MultiplayerPeer;
		if (_migrationTransportAdapter is { } transport)
		{
			transport.Close(peer);
		}
		else
		{
			peer?.Close();
		}
		Multiplayer.MultiplayerPeer = null;

		_enetPeer = null;
		_steamPeer = null;
		_nextMigrationAttemptAt = Time.GetTicksMsec() / 1000.0
			+ NetworkMigrationRules.FollowerRetryIntervalSeconds;
	}

	private void TryStartMigratedHost(double now)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.CandidateStarting);
		if (_networkMigrationPlan is not { } migrationPlan)
		{
			FailMigration(NetworkMigrationFailureCode.NoPlan);
			return;
		}

		if (!_standbyCheckpointsById.TryGetValue(migrationPlan.CheckpointId, out var checkpointEnvelope))
		{
			FailMigration(NetworkMigrationFailureCode.CheckpointMissing);
			return;
		}

		NetworkAuthorityCheckpoint standbyCheckpoint = checkpointEnvelope.Checkpoint;
		byte[] standbyCheckpointPayload = checkpointEnvelope.Payload;
		string standbyCheckpointHash = checkpointEnvelope.Hash;
		if (!string.Equals(migrationPlan.CheckpointHash, standbyCheckpointHash, StringComparison.Ordinal)
			|| !string.Equals(migrationPlan.CheckpointCiphertextHash, checkpointEnvelope.CiphertextHash, StringComparison.Ordinal)
			|| !string.Equals(migrationPlan.PreviousCheckpointHash, checkpointEnvelope.PreviousCheckpointHash, StringComparison.Ordinal)
			|| migrationPlan.PlanRevision != checkpointEnvelope.PlanRevision
			|| migrationPlan.CheckpointId != standbyCheckpoint.CheckpointId)
		{
			GD.Print($"MIGRATION_CHECKPOINT_HASH_MISMATCH plan_hash={migrationPlan.CheckpointHash} checkpoint_hash={standbyCheckpointHash} plan_id={migrationPlan.CheckpointId} checkpoint_id={standbyCheckpoint.CheckpointId}");
			FailMigration(NetworkMigrationFailureCode.CheckpointHashMismatch);
			return;
		}

		if (!NetworkAuthorityCheckpointCodec.TryDecode(
			standbyCheckpointPayload,
			standbyCheckpointHash,
			out NetworkAuthorityCheckpoint checkpoint,
			out string checkpointDecodeError))
		{
			GD.Print($"MIGRATION_CHECKPOINT_INVALID detail={checkpointDecodeError}");
			FailMigration(NetworkMigrationFailureCode.CheckpointInvalid);
			return;
		}

		if (checkpoint.Phase != migrationPlan.Phase
			|| !string.Equals(checkpoint.SessionId, migrationPlan.SessionId, StringComparison.Ordinal)
			|| !string.Equals(checkpoint.MatchId, migrationPlan.MatchId, StringComparison.Ordinal))
		{
			GD.Print($"MIGRATION_CHECKPOINT_CONTEXT_MISMATCH checkpoint_phase={checkpoint.Phase} plan_phase={migrationPlan.Phase} checkpoint_session={checkpoint.SessionId.Length} plan_session={migrationPlan.SessionId.Length} checkpoint_match={checkpoint.MatchId.Length} plan_match={migrationPlan.MatchId.Length}");
			FailMigration(NetworkMigrationFailureCode.CheckpointInvalid);
			return;
		}
		if (!TryPromoteLocalMigrationAuthority(migrationPlan))
		{
			FailMigration(NetworkMigrationFailureCode.CheckpointInvalid);
			return;
		}

		if (OS.GetEnvironment("CODEX_LAN_FORCE_BIND_FAILURE") == "1")
		{
			GD.Print("MIGRATION_HOST_BIND_FAILED result=ForcedFailure error=Test hook");
			_nextMigrationAttemptAt = now + NetworkMigrationRules.CandidateRetryIntervalSeconds;
			return;
		}

		if (_migrationAwaitingSourceDisconnect
			|| _migrationTransportAdapter is not { } transport
			|| transport.Transport != migrationPlan.Transport)
		{
			return;
		}
		_migrationPeerAttemptInFlight = false;
		HostMigrationTransportResult result = transport.StartServer(migrationPlan, out MultiplayerPeer? peer, out string bindError);
		if (result != HostMigrationTransportResult.Started || peer == null)
		{
			GD.Print($"MIGRATION_HOST_BIND_FAILED result={result} error={bindError}");
			_nextMigrationAttemptAt = now + NetworkMigrationRules.CandidateRetryIntervalSeconds;
			return;
		}

		ApplyCheckpointMetadataForMigration(checkpoint);
		_enetPeer = peer as ENetMultiplayerPeer;
		_steamPeer = migrationPlan.Transport == NetworkTransport.Steam ? peer : null;
		Multiplayer.MultiplayerPeer = peer;
		_migrationServerStarted = true;
		GD.Print($"MIGRATION_HOST_BOUND seat={migrationPlan.CandidateSeat} endpoint={GetMigrationEndpointLog(migrationPlan)} at={now:0.000}");

		if (migrationPlan.Phase == NetworkAuthorityPhase.Lobby)
		{
			SetAuthorityEpoch(migrationPlan.TargetEpoch);
			MarkCandidateAsNewLobbyHost(migrationPlan.CandidateSeat, migrationPlan.SourcePlayerSessionId);
			SetState(NetworkSessionState.Lobby);
			if (!UpdateMigratedHostMetadata(migrationPlan))
			{
				return;
			}
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.FollowersRecovering);
			BeginLobbyMigrationRecovery(migrationPlan.TargetEpoch, now);
			CommitAuthorityCheckpointFromLobby();
			BroadcastPlayerList();
			TryFinalizeLobbyMigrationRecovery(now);
			GD.Print($"MIGRATION_LOBBY_HOST_READY seat={migrationPlan.CandidateSeat} epoch={migrationPlan.TargetEpoch}");
			return;
		}

		string restoreError = string.Empty;
		if (TryGetReplicaEndpoint(out INetworkReplicaEndpoint replica)
			&& TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoard)
			&& replica.TryRestoreCheckpoint(standbyCheckpointPayload, standbyCheckpointHash, out restoreError))
		{
			GD.Print($"MIGRATION_CHECKPOINT_RESTORED checkpoint={migrationPlan.CheckpointId} at={Time.GetTicksMsec() / 1000.0:0.000}");
			replica.AdvanceAuthorityEpoch(migrationPlan.TargetEpoch);
			_authorityEpoch = migrationPlan.TargetEpoch;
			TransferStandbyCheckpointPayloadOwnership(migrationPlan.CheckpointId, standbyCheckpointPayload);
			ReplaceLatestAuthorityCheckpointEnvelope(new AuthorityCheckpointEnvelope(
				checkpoint.AuthorityEpoch,
				checkpoint.CheckpointId,
				standbyCheckpointPayload,
				standbyCheckpointHash));
			BeginMigrationReadinessFence(migrationPlan, checkpoint.SeatControls, now);
			MarkCandidateAsNewHost(migrationPlan.CandidateSeat, migrationPlan.SourcePlayerSessionId);
			SetState(NetworkSessionState.InGame);
			if (!UpdateMigratedHostMetadata(migrationPlan))
			{
				return;
			}
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.FollowersRecovering);
			// The restored board stays input-locked until the first hard snapshot has
			// reached followers and the serialized authority action has resumed.
			SendBoardSnapshotsFromHost(gameBoard.CreateNextSnapshotId());
			_ = ResumeMigratedHostAfterSnapshotAsync(gameBoard, migrationPlan);
			GD.Print($"MIGRATION_HOST_SNAPSHOT_READY seat={migrationPlan.CandidateSeat} epoch={migrationPlan.TargetEpoch}");
		}
		else
		{
			GD.Print($"MIGRATION_CHECKPOINT_RESTORE_FAILED detail={restoreError}");
			FailMigration(NetworkMigrationFailureCode.CheckpointInvalid);
		}
	}

	private async Task ResumeMigratedHostAfterSnapshotAsync(
		INetworkAuthorityEndpoint gameBoard,
		NetworkMigrationPlan migrationPlan)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		try
		{
			await gameBoard.ResumeAuthorityActionAsync();
			if (!gameBoard.IsAlive
				|| _state != NetworkSessionState.InGame
				|| _networkMigrationPlan is not { } activePlan
				|| activePlan.TargetEpoch != migrationPlan.TargetEpoch)
			{
				return;
			}

			SendBoardSnapshotsFromHost(gameBoard.CreateNextSnapshotId());
			_migrationRecoveryReadyForPeers = true;
			TryCompleteMigrationReadiness();
			GD.Print($"MIGRATION_RECOVERY_READY epoch={migrationPlan.TargetEpoch}");
		}
		catch (Exception exception)
		{
			GD.PushError($"Migrated host action resume failed: {exception}");
			FailMigration(NetworkMigrationFailureCode.CheckpointInvalid);
		}
	}

	private void TryConnectToMigratedHost(double now)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		if (_networkMigrationPlan is not { } plan)
		{
			return;
		}

		if (_migrationAwaitingSourceDisconnect
			|| _migrationTransportAdapter is not { } transport
			|| transport.Transport != plan.Transport)
		{
			return;
		}

		HostMigrationTransportResult result = transport.Connect(plan, out MultiplayerPeer? peer, out string error);
		if (result != HostMigrationTransportResult.Connected || peer == null)
		{
			if (plan.Transport == NetworkTransport.Steam)
			{
				_migrationTransportPreparedAt = -1.0;
			}
			GD.Print($"MIGRATION_CONNECT_ATTEMPT_FAILED endpoint={GetMigrationEndpointLog(plan)} result={result} error={error}");
			_nextMigrationAttemptAt = now + NetworkMigrationRules.FollowerRetryIntervalSeconds;
			return;
		}

		_enetPeer = peer as ENetMultiplayerPeer;
		_steamPeer = plan.Transport == NetworkTransport.Steam ? peer : null;
		Multiplayer.MultiplayerPeer = peer;
		_migrationPeerAttemptInFlight = true;
		_migrationPeerAttemptStartedAt = now;
		GD.Print($"MIGRATION_CONNECT_ATTEMPT_STARTED endpoint={GetMigrationEndpointLog(plan)} at={now:0.000}");
	}

	private void FailMigration(NetworkMigrationFailureCode code)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			HandleHostDisconnected();
			return;
		}

		if (_state != NetworkSessionState.MigratingHost
			&& _migrationCoordinatorState is not (NetworkMigrationCoordinatorState.CandidateStarting
				or NetworkMigrationCoordinatorState.FollowersRecovering))
		{
			return;
		}

		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Failed);
		GD.Print($"MIGRATION_FAILED code={code}");
		ReturnToMultiplayerLobbyAfterMigrationFailure($"Host migration failed: {code}");
	}

	private void ReturnToMultiplayerLobbyAfterMigrationFailure(
		string technicalDetails,
		NetworkSessionCloseReason closeReason = NetworkSessionCloseReason.InternalFailure)
	{
		if (_migrationFailureReturnPending)
		{
			return;
		}

		if (TryGetPresentationEndpoint(out INetworkPresentationEndpoint gameBoard))
		{
			gameBoard.CompleteHostMigration();
		}

		_ = RequestSessionCloseAsync(closeReason);
		GD.PushWarning($"Host migration ended: {technicalDetails}");
		string localizationKey = NetworkUserErrorRules.GetLocalizationKey(NetworkUserErrorCategory.HostMigration);
		_pendingLobbyErrorLocalizationKey = localizationKey;
		_migrationFailureReturnPending = true;
		SetState(NetworkSessionState.Failed);
		EmitSignal(SignalName.ErrorRaised, localizationKey);
		CallDeferred(
			nameof(ReturnToMultiplayerLobbyScene),
			(int)NetworkSessionState.Failed,
			_stateGeneration);
	}

	private void ApplyCheckpointMetadataForMigration(NetworkAuthorityCheckpoint checkpoint)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		_config = new NetworkSessionConfig(
			checkpoint.SessionId,
			checkpoint.RoomName,
			string.Empty,
			checkpoint.MatchId,
			checkpoint.MatchSeed,
			checkpoint.Transport,
			checkpoint.PlayerCount,
			checkpoint.ItemMode,
			checkpoint.WinningScore,
			LobbyVisibility.FriendsOnly)
		{
			RoomPasswordVerifier = new RoomPasswordVerifier
			{
				Version = checkpoint.RoomPasswordVerifierVersion,
				Iterations = checkpoint.RoomPasswordVerifierIterations,
				Salt = checkpoint.RoomPasswordVerifierSalt,
				Verifier = checkpoint.RoomPasswordVerifier,
			},
			RoomPasswordRequired = checkpoint.RoomPasswordVerifierVersion > 0,
		};
		_players.Clear();
		foreach (NetworkMigrationRosterEntry entry in checkpoint.Roster.OrderBy(entry => entry.SeatIndex))
		{
			_players.Add(new NetworkPlayerPublicState(
				entry.PeerId,
				entry.PlayerSessionId,
				entry.DisplayName,
				entry.AvatarId,
				entry.SeatIndex,
				entry.IsHost,
				string.Equals(entry.PlayerSessionId, GetPlayerSessionId(), StringComparison.Ordinal),
				entry.IsReady,
				entry.IsConnected)
			{
				CredentialsAcknowledged = entry.CredentialsAcknowledged,
			});
		}
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		_issuedSeatCredentialsBySeat.Clear();
		foreach (NetworkAuthoritySeatControl control in checkpoint.SeatControls.OrderBy(control => control.SeatIndex))
		{
			var state = new SeatControlState(
				control.SeatIndex,
				control.Mode,
				control.PeerId,
				control.PlayerSessionId,
				control.CredentialVerifier,
				control.CredentialGeneration,
				control.PendingCredentialVerifier,
				control.PendingCredentialGeneration,
				control.PendingCredentialPeerId);
			_seatControlStates.Add(state);
			NetworkAuthorityReconnectClaim? serializedClaim = checkpoint.ReconnectClaims
				.FirstOrDefault(claim => claim.SeatIndex == control.SeatIndex);
			if (serializedClaim != null
				&& Enum.TryParse(serializedClaim.ClaimState, true, out ReconnectClaimState claimState))
			{
				_reconnectClaimsBySeat[control.SeatIndex] = new ReconnectClaimRecord(
					serializedClaim.MatchId,
					serializedClaim.SeatIndex,
					serializedClaim.CredentialGeneration,
					serializedClaim.PlayerSessionId,
					serializedClaim.ClaimedPeerId,
					claimState);
			}
			else
			{
				_reconnectClaimsBySeat[control.SeatIndex] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, state);
			}
		}
		_lastAcceptedClientActionIds.Clear();
		foreach (KeyValuePair<int, long> pair in checkpoint.LastAcceptedClientActionIds)
		{
			_lastAcceptedClientActionIds[pair.Key] = pair.Value;
		}
	}

	private void MarkCandidateAsNewHost(int candidateSeat, string sourcePlayerSessionId)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		int localPeerId = 1;
		for (int index = 0; index < _players.Count; index++)
		{
			NetworkPlayerPublicState player = _players[index];
			bool local = player.SeatIndex == candidateSeat;
			_players[index] = player with
			{
				PeerId = local ? localPeerId : 0,
				IsHost = local,
				IsLocal = local,
				IsConnected = local,
			};
		}

		for (int index = 0; index < _seatControlStates.Count; index++)
		{
			SeatControlState state = _seatControlStates[index];
			if (state.SeatIndex == candidateSeat)
			{
				_seatControlStates[index] = state with { Mode = SeatControlMode.HumanConnected, PeerId = localPeerId };
			}
			else
			{
				// The old peer ids are invalid on the new ENet server. Treat every
				// remote seat as reclaimable until its session credential is presented.
				_seatControlStates[index] = state with
				{
					Mode = SeatControlMode.AiProxyReclaimable,
					PeerId = 0,
				};
			}
		}
		EmitSeatControlStatesUpdated();
		GD.Print($"MIGRATION_HOST_ROSTER candidate_seat={candidateSeat} controls={GetSeatControlStatesVariant(false)}");
	}


	private void FailActiveHostMigration(NetworkMigrationFailureCode code, string error)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		_activeMigrationPending = false;
		_activeMigrationAwaitingSourceClose = true;
		_activeMigrationDeadline = -1.0;
		_activeMigrationConfirmedSeats.Clear();
		_hostEndedMatchBroadcasted = true;
		string technicalDetails = $"Host migration failed: {code}";
		string localizationKey = NetworkUserErrorRules.GetLocalizationKey(NetworkUserErrorCategory.HostMigration);
		NetworkRpcContext context = GetCurrentRpcContext();
		try
		{
			Rpc(
				MethodName.RpcHostEndedMatch,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				localizationKey);
		}
		catch (Exception exception)
		{
			GD.PushWarning($"Migration failure broadcast failed: {exception.GetType().Name}");
		}
		GD.Print($"MIGRATION_ACTIVE_FAILED reason={code} error={error}");
		_ = FinishFailedActiveHostMigrationAsync(technicalDetails);
	}

	private async Task FinishFailedActiveHostMigrationAsync(string technicalDetails)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!IsInsideTree())
		{
			return;
		}

		await ToSignal(GetTree().CreateTimer(HostEndedMatchBroadcastGraceSeconds), SceneTreeTimer.SignalName.Timeout);
		if (!IsInsideTree() || !_activeMigrationAwaitingSourceClose)
		{
			return;
		}

		ReturnToMultiplayerLobbyAfterMigrationFailure(technicalDetails);
	}

}
