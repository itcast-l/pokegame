using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	private NetworkMigrationPlan? _networkMigrationPlan;
	private NetworkAuthorityCheckpoint? _standbyCheckpoint;
	private byte[] _standbyCheckpointPayload = Array.Empty<byte>();
	private string _standbyCheckpointHash = string.Empty;
	private readonly Dictionary<long, (NetworkAuthorityCheckpoint Checkpoint, byte[] Payload, string Hash)> _standbyCheckpointsById = new();
	private int _hotStandbySeat = -1;
	private bool _standbyCheckpointAcked;
	private long _standbyAckedCheckpointId = -1;
	private long _standbyPendingCheckpointId = -1;
	private double _standbyCheckpointSentAt = -1.0;
	private int _consecutiveUnackedCheckpoints;
	private readonly HashSet<int> _failedHotStandbySeats = new();
	private double _migrationStartedAt = -1.0;
	private double _nextMigrationAttemptAt;
	private bool _migrationServerStarted;
	private bool _migrationPeerAttemptInFlight;
	private double _migrationPeerAttemptStartedAt = -1.0;
	private string _standbyCandidateHostAddress = string.Empty;
	private bool _activeMigrationPending;
	private bool _activeMigrationAwaitingSourceClose;
	private double _activeMigrationDeadline = -1.0;
	private readonly HashSet<int> _activeMigrationConfirmedSeats = new();
	// A migrated host must finish the epoch handoff before it can publish a new
	// standby checkpoint/plan. Otherwise a follower can replace the recovery plan
	// while it is still waiting for the original epoch's readiness signal.
	private long _migrationReadyEpoch = -1;
	private bool _migrationRecoveryReadyForPeers;
	private readonly HashSet<int> _migrationReadinessSeatIndices = new();

	public NetworkMigrationPlan? CurrentMigrationPlan => _networkMigrationPlan;
	public bool IsMigratingHost => _state == NetworkSessionState.MigratingHost;

	public long DebugGetAuthorityEpoch()
	{
		return GetCurrentAuthorityEpoch();
	}

	public bool DebugHasMigrationPlan()
	{
		return _networkMigrationPlan != null;
	}

	public bool DebugHasMigrationArtifacts()
	{
		return _networkMigrationPlan != null
			|| _latestAuthorityCheckpointEnvelope.HasPayload
			|| _standbyCheckpointPayload.Length > 0
			|| _sessionMigrationTokensBySeat.Count > 0
			|| !string.IsNullOrEmpty(_localSessionMigrationToken);
	}

	public bool DebugSendStaleMigrationPlan()
	{
		if (_state != NetworkSessionState.MigratingHost
			|| _networkMigrationPlan is not { } activePlan
			|| activePlan.TargetEpoch <= 0)
		{
			return false;
		}

		// Keep the plan structurally visible while making its source epoch older
		// than the newly bound host's target epoch. This exercises context
		// rejection even for the first migration, whose source epoch is zero.
		NetworkMigrationPlan stalePlan = activePlan with
		{
			SourceEpoch = activePlan.TargetEpoch - 1,
			TargetEpoch = activePlan.TargetEpoch,
		};
		RpcId(1, MethodName.RpcDebugStaleMigrationPlan, ToMigrationPlanVariant(stalePlan));
		GD.Print($"MIGRATION_STALE_EPOCH_INJECTED source_epoch={stalePlan.SourceEpoch} target_epoch={stalePlan.TargetEpoch}");
		return true;
	}

	public bool RequestActiveHostMigration()
	{
		if (!IsHost || _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame))
		{
			return false;
		}

		NetworkAuthorityPhase phase = _state == NetworkSessionState.Lobby
			? NetworkAuthorityPhase.Lobby
			: NetworkAuthorityPhase.InGame;
		AuthorityCheckpointEnvelope latestEnvelope = _latestAuthorityCheckpointEnvelope;
		if (!_standbyCheckpointAcked
			|| _standbyAckedCheckpointId != latestEnvelope.CheckpointId
			|| string.IsNullOrWhiteSpace(_standbyCandidateHostAddress))
		{
			GD.Print($"MIGRATION_ACTIVE_REJECT reason=CandidateUnavailable acked={_standbyCheckpointAcked} acked_id={_standbyAckedCheckpointId} latest_id={latestEnvelope.CheckpointId} address={_standbyCandidateHostAddress}");
			EmitSignal(SignalName.ErrorRaised, NetworkMigrationFailureCode.CandidateUnavailable.ToString());
			return false;
		}

		if (_networkMigrationPlan is not { } plan
			|| plan.Phase != phase
			|| plan.CheckpointId != latestEnvelope.CheckpointId)
		{
			BroadcastMigrationPlanIfReady(_hotStandbySeat, _standbyCandidateHostAddress);
			plan = _networkMigrationPlan;
		}

		if (plan is null || !NetworkMigrationRules.IsPlanVisibleToClients(plan))
		{
			EmitSignal(SignalName.ErrorRaised, NetworkMigrationFailureCode.CandidateUnavailable.ToString());
			return false;
		}

		_activeMigrationPending = true;
		_activeMigrationAwaitingSourceClose = false;
		_activeMigrationDeadline = Time.GetTicksMsec() / 1000.0 + NetworkMigrationRules.ActiveMigrationConfirmationTimeoutSeconds;
		_activeMigrationConfirmedSeats.Clear();
		NetworkRpcContext context = GetCurrentRpcContext();
		foreach (NetworkPlayerPublicState player in _players)
		{
			if (player.IsConnected && !player.IsHost)
			{
				RpcId(
					player.PeerId,
					MethodName.RpcBeginHostMigration,
					context.SessionId,
					context.MatchId,
					context.AuthorityEpoch,
					plan.TargetEpoch,
					plan.CheckpointId,
					plan.CheckpointHash);
			}
		}

		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.BeginNetworkHostMigrationVisualState();
		}
		GD.Print($"MIGRATION_ACTIVE_BEGIN checkpoint={plan.CheckpointId} target_epoch={plan.TargetEpoch}");
		return true;
	}

	private void PollActiveHostMigration(double now)
	{
		if (!IsHost || !_activeMigrationPending)
		{
			return;
		}

		int expectedConfirmationCount = _players.Count(player => player.IsConnected && !player.IsHost);
		if (now < _activeMigrationDeadline && _activeMigrationConfirmedSeats.Count < expectedConfirmationCount)
		{
			return;
		}

		_activeMigrationPending = false;
		_activeMigrationAwaitingSourceClose = true;
		_hostEndedMatchBroadcasted = true;
		CloseActivePeerForMigration();
		FinishActiveMigrationSource();
	}

	private void PollHotStandby(double now)
	{
		if (!IsHost || _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame))
		{
			return;
		}

		if (IsMigrationReadinessFenceActive())
		{
			return;
		}

		if (_hotStandbySeat < 0)
		{
			SendCheckpointToHotStandbyIfNeeded(now);
			return;
		}

		if (_standbyCheckpointAcked || _standbyCheckpointSentAt < 0.0)
		{
			return;
		}

		if (now - _standbyCheckpointSentAt < NetworkMigrationRules.StandbyCheckpointAckTimeoutSeconds)
		{
			return;
		}

		_consecutiveUnackedCheckpoints++;
		_standbyCheckpointSentAt = -1.0;
			_standbyPendingCheckpointId = -1;
			_standbyCheckpointAcked = false;
			_standbyAckedCheckpointId = -1;
		if (NetworkMigrationRules.ShouldSwitchToNextCandidate(true, _consecutiveUnackedCheckpoints))
		{
			_failedHotStandbySeats.Add(_hotStandbySeat);
			_hotStandbySeat = -1;
			_networkMigrationPlan = null;
			_consecutiveUnackedCheckpoints = 0;
		}

		SendCheckpointToHotStandbyIfNeeded(now);
	}

	private void PollHostMigration(double now)
	{
		if (_state != NetworkSessionState.MigratingHost)
		{
			return;
		}

		if (_networkMigrationPlan is not { } plan || !NetworkMigrationRules.IsPlanVisibleToClients(plan))
		{
			FailMigration(NetworkMigrationFailureCode.NoPlan);
			return;
		}

		if (_migrationStartedAt < 0.0)
		{
			_migrationStartedAt = now;
			_nextMigrationAttemptAt = now + (IsLocalMigrationCandidate(plan)
				? NetworkMigrationRules.CandidateStartDelaySeconds
				: NetworkMigrationRules.FollowerStartDelaySeconds);
		}

		if (IsLocalMigrationCandidate(plan))
		{
			if (!_migrationServerStarted
					&& now >= _nextMigrationAttemptAt)
			{
				TryStartMigratedHost(now);
			}

			if (!_migrationServerStarted
				&& now - _migrationStartedAt >= NetworkMigrationRules.CandidateRetryWindowSeconds)
			{
				FailMigration(NetworkMigrationFailureCode.BindTimeout);
			}
			return;
		}

		if (Multiplayer.MultiplayerPeer == null
			&& !_migrationPeerAttemptInFlight
			&& now >= _nextMigrationAttemptAt)
		{
			TryConnectToMigratedHost(now);
		}

		if (_migrationPeerAttemptInFlight
			&& _migrationPeerAttemptStartedAt >= 0.0
			&& now - _migrationPeerAttemptStartedAt >= NetworkMigrationRules.FollowerConnectionAttemptTimeoutSeconds)
		{
			GD.Print($"MIGRATION_CONNECTION_ATTEMPT_TIMEOUT elapsed={now - _migrationPeerAttemptStartedAt:0.00}");
			HandleMigrationConnectionFailure();
		}

		if (now - _migrationStartedAt >= NetworkMigrationRules.FollowerTimeoutSeconds)
		{
			FailMigration(NetworkMigrationFailureCode.ConnectTimeout);
		}
	}

	private bool IsLocalMigrationCandidate(NetworkMigrationPlan plan)
	{
		return string.Equals(plan.CandidateStableUserId, GetStableUserId(), StringComparison.Ordinal);
	}

	private void ConfigureLanPeerTimeout(int peerId)
	{
		if (_enetPeer is null || peerId <= 0)
		{
			return;
		}

		ENetPacketPeer packetPeer = _enetPeer.GetPeer(peerId);
		if (packetPeer is null)
		{
			return;
		}

		packetPeer.SetTimeout(
			NetworkMigrationRules.LanPeerTimeoutFactor,
			NetworkMigrationRules.LanPeerTimeoutMinimumMilliseconds,
			NetworkMigrationRules.LanPeerTimeoutMaximumMilliseconds);
	}

	private void SendCheckpointToHotStandbyIfNeeded(double now = -1.0)
	{
		AuthorityCheckpointEnvelope latestEnvelope = _latestAuthorityCheckpointEnvelope;
		if (!IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| IsMigrationReadinessFenceActive()
			|| !latestEnvelope.HasPayload)
		{
			return;
		}

		int hostSeat = LocalSeatIndex;
		if (now < 0.0)
		{
			now = Time.GetTicksMsec() / 1000.0;
		}

		int nextStandbySeat = NetworkMigrationRules.SelectHotStandbySeat(
			GetCheckpointRoster(),
			GetCheckpointSeatControls(),
			hostSeat,
		_failedHotStandbySeats);
		if (nextStandbySeat < 0 || !TryGetConnectedRemotePlayerBySeatIndex(nextStandbySeat, out NetworkPlayerPublicState standby))
		{
			_hotStandbySeat = -1;
			_standbyCheckpointAcked = false;
			_standbyAckedCheckpointId = -1;
			_standbyPendingCheckpointId = -1;
			_standbyCheckpointSentAt = -1.0;
			return;
		}

		if (_hotStandbySeat != nextStandbySeat)
		{
			_hotStandbySeat = nextStandbySeat;
			_standbyCheckpointAcked = false;
			_standbyAckedCheckpointId = -1;
			_standbyPendingCheckpointId = -1;
			_standbyCheckpointSentAt = -1.0;
			_consecutiveUnackedCheckpoints = 0;
			_networkMigrationPlan = null;
		}

		if (_standbyCheckpointAcked
			&& _standbyAckedCheckpointId == latestEnvelope.CheckpointId)
		{
			return;
		}

		if (_standbyPendingCheckpointId == latestEnvelope.CheckpointId
			&& _standbyCheckpointSentAt >= 0.0)
		{
			return;
		}

		if (_standbyPendingCheckpointId != latestEnvelope.CheckpointId)
		{
			_networkMigrationPlan = null;
			_standbyCheckpointAcked = false;
			_standbyAckedCheckpointId = -1;
		}

		RpcId(
				standby.PeerId,
				MethodName.RpcStandbyCheckpoint,
				_config.SessionId,
				_config.MatchId,
				GetCurrentAuthorityEpoch(),
				latestEnvelope.GetPayloadCopy(),
				latestEnvelope.Hash,
				latestEnvelope.CheckpointId
				);
		_standbyPendingCheckpointId = latestEnvelope.CheckpointId;
		_standbyCheckpointSentAt = now;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcStandbyCheckpoint(
		string sessionId,
		string matchId,
		long authorityEpoch,
		byte[] payload,
		string hash,
		long checkpointId)
	{
		if (IsHost || _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame))
		{
			return;
		}

		byte[] bytes = payload;
		string error = string.Empty;
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch)
			|| !NetworkAuthorityCheckpointCodec.TryDecode(bytes, hash, out NetworkAuthorityCheckpoint checkpoint, out error)
			|| checkpoint.CheckpointId != checkpointId
			|| checkpoint.AuthorityEpoch != authorityEpoch
			|| !checkpoint.Roster.Any(entry => entry.StableUserId == GetStableUserId() && entry.IsConnected))
		{
			RpcId(
				1,
				MethodName.RpcStandbyCheckpointAck,
				sessionId,
				matchId,
				authorityEpoch,
				false,
				checkpointId,
				hash,
				error,
				string.Empty);
			return;
		}

		_standbyCheckpoint = checkpoint;
		_standbyCheckpointPayload = bytes;
		_standbyCheckpointHash = hash;
		_standbyCheckpointsById[checkpointId] = (checkpoint, bytes.ToArray(), hash);
		while (_standbyCheckpointsById.Count > 8)
		{
			_standbyCheckpointsById.Remove(_standbyCheckpointsById.Keys.Min());
		}
		RpcId(
			1,
			MethodName.RpcStandbyCheckpointAck,
			sessionId,
			matchId,
			authorityEpoch,
			true,
			checkpointId,
			hash,
			string.Empty,
			GetObservedLanAddress());
		GD.Print($"STANDBY_ACKED checkpoint={checkpointId} hash={hash}");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcStandbyCheckpointAck(
		string sessionId,
		string matchId,
		long authorityEpoch,
		bool accepted,
		long checkpointId,
		string hash,
		string error,
		string candidateHostAddress)
	{
		if (!IsHost || _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame))
		{
			return;
		}

		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		AuthorityCheckpointEnvelope latestEnvelope = _latestAuthorityCheckpointEnvelope;
		if (!TryResolveSeatFromPeer(senderPeerId, out int senderSeat)
			|| senderSeat != _hotStandbySeat
			|| !accepted
			|| checkpointId != latestEnvelope.CheckpointId
			|| !string.Equals(hash, latestEnvelope.Hash, StringComparison.Ordinal)
			|| string.IsNullOrWhiteSpace(candidateHostAddress))
		{
			if (!accepted)
			{
				GD.PushWarning($"Standby checkpoint rejected: {error}");
			}

			return;
		}

		_standbyCheckpointAcked = true;
		_standbyAckedCheckpointId = checkpointId;
		_standbyPendingCheckpointId = -1;
		_standbyCheckpointSentAt = -1.0;
		_standbyCandidateHostAddress = candidateHostAddress.Trim();
		_consecutiveUnackedCheckpoints = 0;
		BroadcastMigrationPlanIfReady(senderSeat, candidateHostAddress.Trim());
	}

	private void HandleHotStandbyDisconnected(int seatIndex)
	{
		if (!IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| seatIndex != _hotStandbySeat)
		{
			return;
		}

		_failedHotStandbySeats.Add(seatIndex);
		_hotStandbySeat = -1;
		_standbyCheckpointAcked = false;
		_standbyAckedCheckpointId = -1;
		_standbyPendingCheckpointId = -1;
		_standbyCheckpointSentAt = -1.0;
		_consecutiveUnackedCheckpoints = 0;
		_networkMigrationPlan = null;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBeginHostMigration(
		string sessionId,
		string matchId,
		long sourceEpoch,
		long targetEpoch,
		long checkpointId,
		string checkpointHash)
	{
		if (IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| !MatchesCurrentRpcContext(sessionId, matchId, sourceEpoch)
			|| _networkMigrationPlan is not { } plan
			|| plan.TargetEpoch != targetEpoch
			|| plan.CheckpointId != checkpointId
			|| !string.Equals(plan.CheckpointHash, checkpointHash, StringComparison.Ordinal))
		{
			return;
		}

		BeginMigrationReadinessFence(plan);
		SetState(NetworkSessionState.MigratingHost);
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.BeginNetworkHostMigrationVisualState();
		}

		RpcId(
			1,
			MethodName.RpcConfirmHostMigration,
			sessionId,
			matchId,
			sourceEpoch,
			plan.TargetEpoch,
			plan.CheckpointId,
			GetStableUserId());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcConfirmHostMigration(
		string sessionId,
		string matchId,
		long sourceEpoch,
		long targetEpoch,
		long checkpointId,
		string stableUserId)
	{
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
			|| !string.Equals(sender.StableUserId, stableUserId, StringComparison.Ordinal)
			|| sender.IsHost)
		{
			return;
		}

		_activeMigrationConfirmedSeats.Add(senderSeat);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcMigrationReady(string sessionId, string matchId, long authorityEpoch)
	{
		if (IsHost
			|| _state != NetworkSessionState.MigratingHost
			|| _networkMigrationPlan is not { } migrationPlan
			|| authorityEpoch != migrationPlan.TargetEpoch
			|| _migrationReadyEpoch != authorityEpoch
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: true))
		{
			return;
		}

		SetAuthorityEpoch(authorityEpoch);
		ClearMigrationReadinessFence();
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
		SetState(NetworkSessionState.InGame);
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.CompleteNetworkHostMigrationVisualState();
		}
		GD.Print($"MIGRATION_READY_APPLIED epoch={authorityEpoch}");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcClaimMigratedLobbySeat(
		string sessionId,
		long authorityEpoch,
		string sessionMigrationToken,
		string stableUserId)
	{
		if (!IsHost
			|| _state != NetworkSessionState.Lobby
			|| _networkMigrationPlan is not { Phase: NetworkAuthorityPhase.Lobby } migrationPlan
			|| !MatchesCurrentRpcContext(sessionId, string.Empty, authorityEpoch)
			|| authorityEpoch != migrationPlan.TargetEpoch)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (string.Equals(stableUserId, migrationPlan.SourceStableUserId, StringComparison.Ordinal))
		{
			NetworkRpcContext context = GetCurrentRpcContext();
			RpcId(
				senderPeerId,
				MethodName.RpcLobbyRejected,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				NetworkMigrationFailureCode.OldHostTokenRejected.ToString());
			return;
		}

		int playerIndex = _players.FindIndex(player =>
			string.Equals(player.StableUserId, stableUserId, StringComparison.Ordinal));
		string? resolvedToken = null;
		bool hasExpectedToken = playerIndex >= 0
			&& _sessionMigrationTokensBySeat.TryGetValue(_players[playerIndex].SeatIndex, out resolvedToken);
		string expectedToken = resolvedToken ?? string.Empty;
		if (playerIndex < 0
			|| !hasExpectedToken
			|| string.IsNullOrWhiteSpace(sessionMigrationToken)
			|| !string.Equals(expectedToken, sessionMigrationToken, StringComparison.Ordinal))
		{
			NetworkRpcContext context = GetCurrentRpcContext();
			RpcId(
				senderPeerId,
				MethodName.RpcLobbyRejected,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				NetworkMigrationFailureCode.OldHostTokenRejected.ToString());
			return;
		}

		NetworkPlayerPublicState player = _players[playerIndex];
		_players[playerIndex] = player with
		{
			PeerId = senderPeerId,
			IsHost = false,
			IsLocal = false,
			IsConnected = true,
		};
		if (TryGetSeatControlState(player.SeatIndex, out SeatControlState control, out int controlIndex))
		{
			_seatControlStates[controlIndex] = control with
			{
				Mode = SeatControlMode.HumanConnected,
				PeerId = senderPeerId,
			};
		}

		BroadcastPlayerList();
	}

	private void BroadcastMigrationPlanIfReady(int candidateSeat, string candidateHostAddress)
	{
		AuthorityCheckpointEnvelope latestEnvelope = _latestAuthorityCheckpointEnvelope;
		if (!IsHost
			|| IsMigrationReadinessFenceActive()
			|| !latestEnvelope.HasPayload
			|| !TryGetPlayerBySeat(candidateSeat, out NetworkPlayerPublicState candidate))
		{
			return;
		}

		string advertisedCheckpointHash = latestEnvelope.Hash;
		if (OS.GetEnvironment("CODEX_LAN_MIGRATION_CORRUPT_HASH") == "1")
		{
			advertisedCheckpointHash = new string('0', 64);
		}

		NetworkMigrationPlan plan = new()
		{
			SessionId = _config.SessionId,
			MatchId = _config.MatchId,
			SourceEpoch = GetCurrentAuthorityEpoch(),
			TargetEpoch = GetCurrentAuthorityEpoch() + 1,
			CheckpointId = latestEnvelope.CheckpointId,
			CheckpointHash = advertisedCheckpointHash,
			CandidateStableUserId = candidate.StableUserId,
			CandidateSeat = candidate.SeatIndex,
			ObservedHostAddress = GetObservedLanAddress(),
			CandidateHostAddress = candidateHostAddress,
			Port = NetworkProtocol.LanHostMigrationPort,
			SourceStableUserId = GetStableUserIdForSeat(LocalSeatIndex),
			Phase = _state == NetworkSessionState.Lobby
				? NetworkAuthorityPhase.Lobby
				: NetworkAuthorityPhase.InGame,
		};
		if (!NetworkMigrationRules.IsPlanVisibleToClients(plan))
		{
			return;
		}

		_networkMigrationPlan = plan;
		Godot.Collections.Dictionary payload = ToMigrationPlanVariant(plan);
		foreach (NetworkPlayerPublicState player in _players)
		{
			if (player.IsConnected && !player.IsHost)
			{
				RpcId(player.PeerId, MethodName.RpcMigrationPlan, payload);
			}
		}
		GD.Print($"MIGRATION_PLAN_READY candidate_seat={candidateSeat} checkpoint={plan.CheckpointId} hash={plan.CheckpointHash}");
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcMigrationPlan(Godot.Collections.Dictionary rawPlan)
	{
		if (!TryParseMigrationPlan(rawPlan, out NetworkMigrationPlan plan))
		{
			GD.Print("MIGRATION_PLAN_REJECT reason=Decode");
			return;
		}
		if (!NetworkMigrationRules.IsPlanVisibleToClients(plan))
		{
			GD.Print($"MIGRATION_PLAN_REJECT reason=Visibility source_epoch={plan.SourceEpoch} target_epoch={plan.TargetEpoch} candidate={plan.CandidateStableUserId}");
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

		if (_networkMigrationPlan == null
			|| plan.TargetEpoch > _networkMigrationPlan.TargetEpoch
			|| (plan.TargetEpoch == _networkMigrationPlan.TargetEpoch && plan.CheckpointId > _networkMigrationPlan.CheckpointId))
		{
			_networkMigrationPlan = plan;
			GD.Print($"MIGRATION_PLAN_ACCEPTED candidate={plan.CandidateStableUserId} seat={plan.CandidateSeat} target_epoch={plan.TargetEpoch}");
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcDebugStaleMigrationPlan(Godot.Collections.Dictionary rawPlan)
	{
		if (!IsHost || OS.GetEnvironment("CODEX_LAN_INJECT_OLD_EPOCH") != "1")
		{
			return;
		}

		// Keep the production migration-plan RPC authority-only. The test-only
		// relay feeds the same validator on the new host after a follower binds.
		RpcMigrationPlan(rawPlan);
	}

	private void BeginMigrationAfterHostDisconnect()
	{
		if (_networkMigrationPlan is not { } plan || !NetworkMigrationRules.IsPlanVisibleToClients(plan))
		{
			FailMigration(NetworkMigrationFailureCode.NoPlan);
			return;
		}

		StartMigrationAttemptsAfterHostDisconnect(plan);
	}

	private void StartMigrationAttemptsAfterHostDisconnect(NetworkMigrationPlan plan)
	{
		BeginMigrationReadinessFence(plan);
		_activeMigrationAwaitingSourceClose = false;
		CloseActivePeerForMigration();
		_migrationStartedAt = Time.GetTicksMsec() / 1000.0;
		_nextMigrationAttemptAt = _migrationStartedAt + (IsLocalMigrationCandidate(plan)
			? NetworkMigrationRules.CandidateStartDelaySeconds
			: NetworkMigrationRules.FollowerStartDelaySeconds);
		_migrationServerStarted = false;
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
		GD.Print($"MIGRATION_BEGIN match={_config.MatchId.Length} token={_localReconnectToken.Length} address={_lastLanJoinAddress}");
		SetState(NetworkSessionState.MigratingHost);
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.BeginNetworkHostMigrationVisualState();
		}
	}

	private void FinishActiveMigrationSource()
	{
		ReturnToMultiplayerLobbyAfterMigrationFailure("Active host migration handed off.");
	}

	private void CloseActivePeerForMigration()
	{
		Multiplayer.MultiplayerPeer?.Close();
		Multiplayer.MultiplayerPeer = null;
		_enetPeer = null;
		_steamPeer = null;
		_reconnectAttemptInFlight = false;
	}

	private void HandleMigrationConnectionFailure()
	{
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		_enetPeer = null;
		_nextMigrationAttemptAt = Time.GetTicksMsec() / 1000.0
			+ NetworkMigrationRules.FollowerRetryIntervalSeconds;
	}

	private void TryStartMigratedHost(double now)
	{
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

		if (OS.GetEnvironment("CODEX_LAN_FORCE_BIND_FAILURE") == "1")
		{
			GD.Print("MIGRATION_HOST_BIND_FAILED result=ForcedFailure error=Test hook");
			_nextMigrationAttemptAt = now + NetworkMigrationRules.CandidateRetryIntervalSeconds;
			return;
		}

		var transport = new LanHostMigrationTransport(migrationPlan.CandidateHostAddress);
		_migrationPeerAttemptInFlight = false;
		HostMigrationTransportResult result = transport.StartServer(
			NetworkProtocol.LanHostMigrationPort,
			NetworkSessionDefaults.MaxPlayers - 1,
			out MultiplayerPeer? peer,
			out string bindError);
		if (result != HostMigrationTransportResult.Started || peer == null)
		{
			GD.Print($"MIGRATION_HOST_BIND_FAILED result={result} error={bindError}");
			_nextMigrationAttemptAt = now + NetworkMigrationRules.CandidateRetryIntervalSeconds;
			return;
		}

			ApplyCheckpointMetadataForMigration(checkpoint);
			_enetPeer = peer as ENetMultiplayerPeer;
			Multiplayer.MultiplayerPeer = peer;
			_migrationServerStarted = true;
			GD.Print($"MIGRATION_HOST_BOUND seat={migrationPlan.CandidateSeat} address={migrationPlan.CandidateHostAddress} port={NetworkProtocol.LanHostMigrationPort} at={now:0.000}");

		if (migrationPlan.Phase == NetworkAuthorityPhase.Lobby)
		{
			SetAuthorityEpoch(migrationPlan.TargetEpoch);
			MarkCandidateAsNewLobbyHost(migrationPlan.CandidateSeat, migrationPlan.SourceStableUserId);
			SetState(NetworkSessionState.Lobby);
			BroadcastPlayerList();
			GD.Print($"MIGRATION_LOBBY_HOST_READY seat={migrationPlan.CandidateSeat} epoch={migrationPlan.TargetEpoch}");
			return;
		}

		string restoreError = string.Empty;
		if (GetTree().CurrentScene is GameBoardScene gameBoard
				&& gameBoard.TryRestoreAuthorityCheckpoint(standbyCheckpointPayload, standbyCheckpointHash, out restoreError))
		{
			GD.Print($"MIGRATION_CHECKPOINT_RESTORED checkpoint={migrationPlan.CheckpointId} at={Time.GetTicksMsec() / 1000.0:0.000}");
			gameBoard.AdvanceNetworkAuthorityEpoch(migrationPlan.TargetEpoch);
			_authorityEpoch = migrationPlan.TargetEpoch;
			_latestAuthorityCheckpointEnvelope = new AuthorityCheckpointEnvelope(
				checkpoint.AuthorityEpoch,
				checkpoint.CheckpointId,
				standbyCheckpointPayload,
				standbyCheckpointHash);
			BeginMigrationReadinessFence(migrationPlan, checkpoint.SeatControls);
			MarkCandidateAsNewHost(migrationPlan.CandidateSeat, migrationPlan.SourceStableUserId);
			SetState(NetworkSessionState.InGame);
			// The restored board stays input-locked until the first hard snapshot has
			// reached followers and the serialized authority action has resumed.
			SendBoardSnapshotsFromHost(gameBoard, gameBoard.CreateNextNetworkSnapshotId());
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
		GameBoardScene gameBoard,
		NetworkMigrationPlan migrationPlan)
	{
		try
		{
			await gameBoard.ResumeAuthorityActionAsync();
			if (!GodotObject.IsInstanceValid(gameBoard)
				|| _state != NetworkSessionState.InGame
				|| _networkMigrationPlan is not { } activePlan
				|| activePlan.TargetEpoch != migrationPlan.TargetEpoch)
			{
				return;
			}

			SendBoardSnapshotsFromHost(gameBoard, gameBoard.CreateNextNetworkSnapshotId());
			_migrationRecoveryReadyForPeers = true;
			TryCompleteMigrationReadiness();
			gameBoard.BeginNetworkAiProxyTurnIfNeeded();
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
		if (_networkMigrationPlan is not { } plan)
		{
			return;
		}

		var transport = new LanHostMigrationTransport(plan.CandidateHostAddress);
		HostMigrationTransportResult result = transport.Connect(
				plan.CandidateHostAddress,
			plan.Port,
			out MultiplayerPeer? peer,
			out string error);
		if (result != HostMigrationTransportResult.Connected || peer == null)
		{
			GD.Print($"MIGRATION_CONNECT_ATTEMPT_FAILED address={plan.CandidateHostAddress} port={plan.Port} result={result} error={error}");
			_nextMigrationAttemptAt = now + NetworkMigrationRules.FollowerRetryIntervalSeconds;
			return;
		}

		_enetPeer = peer as ENetMultiplayerPeer;
		Multiplayer.MultiplayerPeer = peer;
		_migrationPeerAttemptInFlight = true;
		_migrationPeerAttemptStartedAt = now;
			GD.Print($"MIGRATION_CONNECT_ATTEMPT_STARTED address={plan.CandidateHostAddress} port={plan.Port} at={now:0.000}");
	}

	private void FailMigration(NetworkMigrationFailureCode code)
	{
		if (_state != NetworkSessionState.MigratingHost)
		{
			return;
		}

		GD.Print($"MIGRATION_FAILED code={code}");
		ReturnToMultiplayerLobbyAfterMigrationFailure($"Host migration failed: {code}");
	}

	private void ReturnToMultiplayerLobbyAfterMigrationFailure(string message)
	{
		if (_migrationFailureReturnPending)
		{
			return;
		}

		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.BeginNetworkHostMigrationVisualState();
		}

		LeaveSession();
		_pendingLobbyErrorMessage = message;
		_migrationFailureReturnPending = true;
		SetState(NetworkSessionState.Failed);
		EmitSignal(SignalName.ErrorRaised, message);
		CallDeferred(
			nameof(ReturnToMultiplayerLobbyScene),
			(int)NetworkSessionState.Failed,
			_stateGeneration);
	}

	private void ApplyCheckpointMetadataForMigration(NetworkAuthorityCheckpoint checkpoint)
	{
		_config = new NetworkSessionConfig(
			checkpoint.SessionId,
			checkpoint.RoomName,
			checkpoint.RoomPassword,
			checkpoint.MatchId,
			checkpoint.MatchSeed,
			checkpoint.Transport,
			checkpoint.PlayerCount,
			checkpoint.ItemMode,
			checkpoint.WinningScore,
			LobbyVisibility.FriendsOnly);
		_players.Clear();
		foreach (NetworkMigrationRosterEntry entry in checkpoint.Roster.OrderBy(entry => entry.SeatIndex))
		{
			_players.Add(new NetworkPlayerPublicState(
				entry.PeerId,
				entry.StableUserId,
				entry.DisplayName,
				entry.AvatarId,
				entry.SeatIndex,
				entry.IsHost,
				string.Equals(entry.StableUserId, GetStableUserId(), StringComparison.Ordinal),
				entry.IsReady,
				entry.IsConnected));
		}
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		_sessionMigrationTokensBySeat.Clear();
		_localSessionMigrationToken = string.Empty;
		foreach (NetworkAuthoritySeatControl control in checkpoint.SeatControls.OrderBy(control => control.SeatIndex))
		{
			var state = new SeatControlState(
				control.SeatIndex,
				control.Mode,
				control.PeerId,
				control.StableUserId,
				control.ReconnectToken);
			_seatControlStates.Add(state);
			if (!string.IsNullOrWhiteSpace(control.SessionMigrationToken))
			{
				ApplySessionMigrationToken(control.SeatIndex, control.SessionMigrationToken);
			}
			NetworkAuthorityReconnectClaim? serializedClaim = checkpoint.ReconnectClaims
				.FirstOrDefault(claim => claim.SeatIndex == control.SeatIndex);
			if (serializedClaim != null
				&& Enum.TryParse(serializedClaim.ClaimState, true, out ReconnectClaimState claimState))
			{
				_reconnectClaimsBySeat[control.SeatIndex] = new ReconnectClaimRecord(
					serializedClaim.MatchId,
					serializedClaim.SeatIndex,
					serializedClaim.ReconnectToken,
					serializedClaim.StableUserId,
					serializedClaim.ClaimedPeerId,
					claimState);
			}
			else
			{
				_reconnectClaimsBySeat[control.SeatIndex] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, state);
			}
			if (state.StableUserId == GetStableUserId())
			{
				_localReconnectToken = state.ReconnectToken;
			}
		}
		foreach (KeyValuePair<int, string> pair in checkpoint.SessionMigrationTokens)
		{
			ApplySessionMigrationToken(pair.Key, pair.Value);
		}
		_lastAcceptedClientActionIds.Clear();
		foreach (KeyValuePair<int, long> pair in checkpoint.LastAcceptedClientActionIds)
		{
			_lastAcceptedClientActionIds[pair.Key] = pair.Value;
		}
	}

	private void MarkCandidateAsNewHost(int candidateSeat, string sourceStableUserId)
	{
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
				// remote seat as reclaimable until its stable id and reconnect token
				// are presented on the new connection.
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

	private void MarkCandidateAsNewLobbyHost(int candidateSeat, string sourceStableUserId)
	{
		var oldControlsByStableId = _seatControlStates
			.ToDictionary(state => state.StableUserId, StringComparer.Ordinal);
		var oldTokensByStableId = _players
			.ToDictionary(
				player => player.StableUserId,
				player => _sessionMigrationTokensBySeat.TryGetValue(player.SeatIndex, out string? token)
					? token
					: string.Empty,
				StringComparer.Ordinal);

		List<NetworkPlayerPublicState> remainingPlayers = _players
			.Where(player => !string.Equals(player.StableUserId, sourceStableUserId, StringComparison.Ordinal))
			.OrderBy(player => player.SeatIndex)
			.ToList();
		if (!remainingPlayers.Any(player => player.SeatIndex == candidateSeat))
		{
			FailMigration(NetworkMigrationFailureCode.NotCandidate);
			return;
		}

		_players.Clear();
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		_sessionMigrationTokensBySeat.Clear();
		_localSessionMigrationToken = string.Empty;

		for (int newSeat = 0; newSeat < remainingPlayers.Count; newSeat++)
		{
			NetworkPlayerPublicState oldPlayer = remainingPlayers[newSeat];
			bool isLocalCandidate = string.Equals(oldPlayer.StableUserId, GetStableUserId(), StringComparison.Ordinal);
			NetworkPlayerPublicState player = oldPlayer with
			{
				PeerId = isLocalCandidate ? 1 : 0,
				SeatIndex = newSeat,
				IsHost = isLocalCandidate,
				IsLocal = isLocalCandidate,
				IsConnected = isLocalCandidate,
			};
			_players.Add(player);

			if (!oldControlsByStableId.TryGetValue(oldPlayer.StableUserId, out SeatControlState oldControl))
			{
				oldControl = SeatControlRules.CreateInitialState(oldPlayer, Guid.NewGuid().ToString("N"));
			}

			SeatControlState control = oldControl with
			{
				SeatIndex = newSeat,
				Mode = isLocalCandidate ? SeatControlMode.HumanConnected : SeatControlMode.HumanReclaimPending,
				PeerId = isLocalCandidate ? 1 : 0,
			};
			_seatControlStates.Add(control);
			_reconnectClaimsBySeat[newSeat] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, control);
			string token = oldTokensByStableId.TryGetValue(oldPlayer.StableUserId, out string? oldToken)
				? oldToken
				: string.Empty;
			if (!string.IsNullOrWhiteSpace(token))
			{
				_sessionMigrationTokensBySeat[newSeat] = token;
				if (isLocalCandidate)
				{
					_localSessionMigrationToken = token;
				}
			}
		}

		EmitPlayerListUpdated();
		EmitSeatControlStatesUpdated();
	}

	private bool TryResolveSeatFromPeer(int peerId, out int seatIndex)
	{
		NetworkPlayerPublicState player = _players.FirstOrDefault(candidate => candidate.PeerId == peerId);
		seatIndex = player.PeerId == 0 ? -1 : player.SeatIndex;
		return seatIndex >= 0;
	}

	private bool TryGetPlayerBySeat(int seatIndex, out NetworkPlayerPublicState player)
	{
		player = _players.FirstOrDefault(candidate => candidate.SeatIndex == seatIndex);
		return player.PeerId != 0 || player.StableUserId.Length > 0;
	}

	private string GetStableUserIdForSeat(int seatIndex)
	{
		return _players.FirstOrDefault(player => player.SeatIndex == seatIndex).StableUserId;
	}

	private long GetCurrentAuthorityEpoch()
	{
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			_authorityEpoch = Math.Max(_authorityEpoch, gameBoard.NetworkAuthorityEpoch);
			return gameBoard.NetworkAuthorityEpoch;
		}

		return _authorityEpoch;
	}

	private void SetAuthorityEpoch(long authorityEpoch)
	{
		if (authorityEpoch < _authorityEpoch)
		{
			return;
		}

		_authorityEpoch = authorityEpoch;
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.AdvanceNetworkAuthorityEpoch(authorityEpoch);
		}
	}

	private string GetObservedLanAddress()
	{
		string configuredAddress = OS.GetEnvironment("CODEX_LAN_MIGRATION_ADDRESS").Trim();
		if (!string.IsNullOrWhiteSpace(configuredAddress))
		{
			return configuredAddress;
		}

		return LanAddressHelper.GetLocalIpv4Addresses().FirstOrDefault(address => !address.StartsWith("127.", StringComparison.Ordinal))
			?? LanAddressHelper.GetLocalIpv4Addresses().FirstOrDefault()
			?? "127.0.0.1";
	}

	private static Godot.Collections.Dictionary ToMigrationPlanVariant(NetworkMigrationPlan plan)
	{
		return new Godot.Collections.Dictionary
		{
			["session_id"] = plan.SessionId,
			["match_id"] = plan.MatchId,
			["source_epoch"] = plan.SourceEpoch,
			["target_epoch"] = plan.TargetEpoch,
			["checkpoint_id"] = plan.CheckpointId,
			["checkpoint_hash"] = plan.CheckpointHash,
			["candidate_stable_user_id"] = plan.CandidateStableUserId,
				["candidate_seat"] = plan.CandidateSeat,
				["observed_host_address"] = plan.ObservedHostAddress,
				["candidate_host_address"] = plan.CandidateHostAddress,
			["port"] = plan.Port,
			["source_stable_user_id"] = plan.SourceStableUserId,
			["authority_phase"] = plan.Phase.ToString(),
		};
	}

	private static bool TryParseMigrationPlan(
		Godot.Collections.Dictionary raw,
		out NetworkMigrationPlan plan)
	{
		plan = new NetworkMigrationPlan();
		if (!NetworkVariantReader.TryReadString(raw, "session_id", NetworkProtocol.MaxSessionIdLength, out string sessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "match_id", NetworkProtocol.MaxMatchIdLength, out string matchId)
			|| !NetworkVariantReader.TryReadInt64(raw, "source_epoch", 0, long.MaxValue, out long sourceEpoch)
			|| !NetworkVariantReader.TryReadInt64(raw, "target_epoch", 0, long.MaxValue, out long targetEpoch)
			|| !NetworkVariantReader.TryReadInt64(raw, "checkpoint_id", 0, long.MaxValue, out long checkpointId)
			|| !NetworkVariantReader.TryReadString(raw, "checkpoint_hash", 64, out string checkpointHash, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "candidate_stable_user_id", NetworkProtocol.MaxPlayerIdLength, out string candidateStableUserId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadInt32(raw, "candidate_seat", 0, NetworkSessionDefaults.MaxPlayers - 1, out int candidateSeat)
			|| !NetworkVariantReader.TryReadString(raw, "observed_host_address", NetworkProtocol.MaxAddressLength, out string observedHostAddress, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "candidate_host_address", NetworkProtocol.MaxAddressLength, out string candidateHostAddress, allowEmpty: false)
			|| !NetworkVariantReader.TryReadInt32(raw, "port", 1, ushort.MaxValue, out int port)
			|| !NetworkVariantReader.TryReadString(raw, "source_stable_user_id", NetworkProtocol.MaxPlayerIdLength, out string sourceStableUserId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadEnum(raw, "authority_phase", out NetworkAuthorityPhase phase))
		{
			return false;
		}

		plan = new NetworkMigrationPlan
		{
			SessionId = sessionId,
			MatchId = matchId,
			SourceEpoch = sourceEpoch,
			TargetEpoch = targetEpoch,
			CheckpointId = checkpointId,
			CheckpointHash = checkpointHash,
			CandidateStableUserId = candidateStableUserId,
			CandidateSeat = candidateSeat,
			ObservedHostAddress = observedHostAddress,
			CandidateHostAddress = candidateHostAddress,
			Port = port,
			SourceStableUserId = sourceStableUserId,
			Phase = phase,
		};
		return NetworkMigrationRules.IsPlanVisibleToClients(plan);
	}

	private bool IsMigrationReadinessFenceActive()
	{
		return _migrationReadyEpoch >= 0
			&& _networkMigrationPlan is { } plan
			&& plan.TargetEpoch == _migrationReadyEpoch;
	}

	private void BeginMigrationReadinessFence(NetworkMigrationPlan plan)
	{
		_migrationReadyEpoch = plan.TargetEpoch;
		_migrationRecoveryReadyForPeers = false;
		_migrationReadinessSeatIndices.Clear();
		foreach (SeatControlState state in _seatControlStates)
		{
			if (state.Mode == SeatControlMode.HumanConnected
				&& !string.Equals(state.StableUserId, plan.SourceStableUserId, StringComparison.Ordinal))
			{
				_migrationReadinessSeatIndices.Add(state.SeatIndex);
			}
		}
	}

	private void BeginMigrationReadinessFence(
		NetworkMigrationPlan plan,
		IReadOnlyCollection<NetworkAuthoritySeatControl> checkpointControls)
	{
		_migrationReadyEpoch = plan.TargetEpoch;
		_migrationRecoveryReadyForPeers = false;
		_migrationReadinessSeatIndices.Clear();
		foreach (NetworkAuthoritySeatControl control in checkpointControls)
		{
			if (control.Mode == SeatControlMode.HumanConnected
				&& !string.Equals(control.StableUserId, plan.SourceStableUserId, StringComparison.Ordinal))
			{
				_migrationReadinessSeatIndices.Add(control.SeatIndex);
			}
		}
	}

	private void TryCompleteMigrationReadiness()
	{
		if (!IsHost
			|| !IsMigrationReadinessFenceActive()
			|| !_migrationRecoveryReadyForPeers
			|| _networkMigrationPlan is not { } plan
			|| _migrationReadinessSeatIndices.Any(seatIndex =>
				!TryGetSeatControlState(seatIndex, out SeatControlState state, out _)
				|| state.Mode != SeatControlMode.HumanConnected))
		{
			return;
		}

		int readinessPeerCount = _migrationReadinessSeatIndices.Count;
		NetworkRpcContext context = new(plan.SessionId, plan.MatchId, plan.TargetEpoch);
		foreach (NetworkPlayerPublicState player in _players)
		{
			if (player.IsConnected && !player.IsHost)
			{
				RpcId(
					player.PeerId,
					MethodName.RpcMigrationReady,
					context.SessionId,
					context.MatchId,
					context.AuthorityEpoch);
			}
		}

		_networkMigrationPlan = null;
		ClearMigrationReadinessFence();
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.CompleteNetworkHostMigrationVisualState();
			gameBoard.BeginNetworkAiProxyTurnIfNeeded();
		}
		GD.Print($"MIGRATION_READY epoch={context.AuthorityEpoch} peers={readinessPeerCount}");
	}

	private void ClearMigrationReadinessFence()
	{
		_migrationReadyEpoch = -1;
		_migrationRecoveryReadyForPeers = false;
		_migrationReadinessSeatIndices.Clear();
		_networkMigrationPlan = null;
	}
}
