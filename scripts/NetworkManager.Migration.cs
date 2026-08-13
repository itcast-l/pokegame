using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	private NetworkMigrationPlan? _networkMigrationPlan;
	private NetworkMigrationPlan? _pendingNetworkMigrationPlan;
	private readonly HashSet<int> _migrationPlanAcknowledgedSeatIndices = new();
	private NetworkAuthorityCheckpoint? _standbyCheckpoint;
	private byte[] _standbyCheckpointPayload = Array.Empty<byte>();
	private string _standbyCheckpointHash = string.Empty;
	private string _standbyCheckpointCiphertextHash = string.Empty;
	private string _standbyConfirmedCheckpointCiphertextHash = string.Empty;
	private readonly Dictionary<long, (NetworkAuthorityCheckpoint Checkpoint, byte[] Payload, string Hash, string CiphertextHash, long PlanRevision, string PreviousCheckpointHash)> _standbyCheckpointsById = new();
	private int _hotStandbySeat = -1;
	private long _standbyAckedCheckpointId = -1;
	private long _migrationEligibleCheckpointId = -1;
	private long _standbyPendingCheckpointId = -1;
	private string _standbyPendingCheckpointHash = string.Empty;
	private EncryptedAuthorityCheckpointEnvelope? _standbyPendingEncryptedCheckpoint;
	private double _standbyCheckpointSentAt = -1.0;
	private double _lastStandbyCheckpointSentAt = -1.0;
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
	private long _lastCompletedMigrationReadyEpoch = -1;

	internal bool IsStandbyCheckpointAcknowledged(long checkpointId)
	{
		return !HostMigrationCapabilityAvailable
			|| (checkpointId >= 0 && _standbyAckedCheckpointId == checkpointId);
	}
	private bool _migrationRecoveryReadyForPeers;
	private GameMigrationRecoveryState? _gameMigrationRecoveryState;
	private LobbyMigrationRecoveryState? _lobbyMigrationRecoveryState;
	private readonly MigrationNonceReplayGuard _issuedStandbyCheckpointNonces = new();
	private readonly MigrationNonceReplayGuard _acceptedStandbyCheckpointNonces = new();
	private NetworkMigrationCoordinatorState _migrationCoordinatorState = NetworkMigrationCoordinatorState.Disabled;
	private long _lastObservedMigrationPlanRevision;
	private long _pendingMigrationPlanRevocationRevision = -1;
	private IHostMigrationTransport? _migrationTransportAdapter;
	private long _migrationTransportPlanRevision = -1;
	private double _migrationTransportPreparedAt = -1.0;
	private double _migrationOwnerConvergenceDeadline = -1.0;
	private bool _migrationAwaitingSourceDisconnect;
	private double _lastStandbyCheckpointAckedAt = -1.0;
	private readonly Dictionary<int, double> _migrationLatencyMillisecondsByPeerId = new();
	private readonly Dictionary<int, (long Sequence, double SentAt)> _pendingMigrationLatencyProbesByPeerId = new();
	private readonly List<int> _expiredMigrationLatencyProbePeerIds = new();
	private double _lastMigrationLatencyProbeAt = -1.0;
	private long _nextMigrationLatencyProbeSequence;

	public NetworkMigrationPlan? CurrentMigrationPlan => HostMigrationCapabilityAvailable
		? _networkMigrationPlan
		: null;
	public bool IsMigratingHost => HostMigrationCapabilityAvailable
		&& _state == NetworkSessionState.MigratingHost;

	public long DebugGetAuthorityEpoch()
	{
		return GetCurrentAuthorityEpoch();
	}

	public bool DebugHasMigrationPlan()
	{
		return HostMigrationCapabilityAvailable && _networkMigrationPlan != null;
	}

	public bool DebugCanRequestActiveHostMigration()
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame))
		{
			return false;
		}

		NetworkAuthorityPhase phase = _state == NetworkSessionState.Lobby
			? NetworkAuthorityPhase.Lobby
			: NetworkAuthorityPhase.InGame;
		return HasCurrentActiveMigrationPlan(phase);
	}

	public bool DebugIsLobbyMigrationRecoveryActive()
	{
		return HostMigrationCapabilityAvailable && _lobbyMigrationRecoveryState is not null;
	}

	public bool DebugHasMigrationArtifacts()
	{
		return HostMigrationCapabilityAvailable
			&& (_networkMigrationPlan != null
			|| _latestAuthorityCheckpointEnvelope.HasPayload
			|| _standbyCheckpointPayload.Length > 0);
	}

	public int DebugGetLocalStandbyCheckpointCacheCount()
	{
		return HostMigrationCapabilityAvailable ? _standbyCheckpointsById.Count : 0;
	}

	public long DebugGetLocalStandbyCheckpointId()
	{
		return HostMigrationCapabilityAvailable ? _standbyCheckpoint?.CheckpointId ?? -1 : -1;
	}

	public long DebugGetLocalStandbyCheckpointFingerprint()
	{
		return !HostMigrationCapabilityAvailable || _standbyCheckpointHash.Length < 16
			? 0
			: unchecked((long)Convert.ToUInt64(_standbyCheckpointHash[..16], 16));
	}

	public long DebugGetLocalMigrationFingerprint()
	{
		if (!HostMigrationCapabilityAvailable || _localMigrationSessionKey.Length == 0)
		{
			return 0;
		}

		Span<byte> fingerprint = stackalloc byte[SHA256.HashSizeInBytes];
		SHA256.HashData(_localMigrationSessionKey, fingerprint);
		return BitConverter.ToInt64(fingerprint);
	}

	public int DebugGetHotStandbySeat()
	{
		return HostMigrationCapabilityAvailable ? _hotStandbySeat : -1;
	}

	public bool DebugRotateHotStandbyCandidate()
	{
		if (!HostMigrationCapabilityAvailable
			|| OS.GetEnvironment("CODEX_LAN_MIGRATION_FIXTURE") != "1"
			|| !IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame)
			|| _hotStandbySeat < 0)
		{
			return false;
		}

		int formerCandidateSeat = _hotStandbySeat;
		_failedHotStandbySeats.Add(formerCandidateSeat);
		ReleaseHotStandbyCandidate(rekeyFormerCandidate: true);
		_migrationEligibleCheckpointId = _latestAuthorityCheckpointEnvelope.CheckpointId;
		SendCheckpointToHotStandbyIfNeeded();
		return true;
	}

	public bool DebugSendStaleMigrationPlan()
	{
		if (!HostMigrationCapabilityAvailable
			|| _state != NetworkSessionState.MigratingHost
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
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame))
		{
			return false;
		}

		NetworkAuthorityPhase phase = _state == NetworkSessionState.Lobby
			? NetworkAuthorityPhase.Lobby
			: NetworkAuthorityPhase.InGame;
		if (!HasCurrentActiveMigrationPlan(phase)
			|| _networkMigrationPlan is not { } plan)
		{
			GD.Print($"MIGRATION_ACTIVE_REJECT reason=CandidateUnavailable coordinator={_migrationCoordinatorState} acked_id={_standbyAckedCheckpointId} plan_id={_networkMigrationPlan?.CheckpointId ?? -1} latest_id={_latestAuthorityCheckpointEnvelope.CheckpointId} address={_standbyCandidateHostAddress}");
			EmitSignal(
				SignalName.ErrorRaised,
				NetworkUserErrorRules.GetLocalizationKey(NetworkUserErrorCategory.HostMigration));
			return false;
		}

		double now = Time.GetTicksMsec() / 1000.0;
		ResetMigrationTransportState();
		HostMigrationTransportResult prepareResult = PrepareMigrationTransport(plan, now, out string prepareError);
		if (prepareResult == HostMigrationTransportResult.Failed)
		{
			NetworkMigrationFailureCode failureCode = GetMigrationTransportFailureCode(plan, prepareError);
			GD.Print($"MIGRATION_ACTIVE_REJECT reason={failureCode} error={prepareError}");
			EmitSignal(
				SignalName.ErrorRaised,
				NetworkUserErrorRules.GetLocalizationKey(NetworkUserErrorCategory.HostMigration));
			ResetMigrationTransportState();
			return false;
		}

		_activeMigrationPending = true;
		_activeMigrationAwaitingSourceClose = false;
		_activeMigrationDeadline = now + NetworkMigrationRules.ActiveMigrationConfirmationTimeoutSeconds;
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

		if (TryGetPresentationEndpoint(out INetworkPresentationEndpoint gameBoard))
		{
			gameBoard.BeginHostMigration();
		}
		GD.Print($"MIGRATION_ACTIVE_BEGIN checkpoint={plan.CheckpointId} target_epoch={plan.TargetEpoch}");
		RescheduleNetworkMaintenance(immediate: true);
		return true;
	}

	private bool HasCurrentActiveMigrationPlan(NetworkAuthorityPhase phase)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return false;
		}

		AuthorityCheckpointEnvelope latestEnvelope = _latestAuthorityCheckpointEnvelope;
		return NetworkMigrationRules.IsReadyForMigration(_migrationCoordinatorState)
			&& latestEnvelope.HasPayload
			&& _networkMigrationPlan is { } plan
			&& NetworkMigrationRules.IsPlanVisibleToClients(plan)
			&& plan.Phase == phase
			&& plan.CheckpointId == _standbyAckedCheckpointId
			&& plan.CheckpointId == latestEnvelope.CheckpointId
			&& string.Equals(plan.CheckpointHash, latestEnvelope.Hash, StringComparison.Ordinal);
	}

	private bool IsActiveMigrationHandoffInProgress()
	{
		return _activeMigrationPending || _activeMigrationAwaitingSourceClose;
	}

	private bool IsMigrationRecoveryHandoffInProgress()
	{
		return _migrationCoordinatorState is NetworkMigrationCoordinatorState.SourceHostLost
			or NetworkMigrationCoordinatorState.CandidateStarting
			or NetworkMigrationCoordinatorState.FollowersRecovering;
	}

	private void PollActiveHostMigration(double now)
	{
		if (!HostMigrationCapabilityAvailable || !IsHost || !_activeMigrationPending)
		{
			return;
		}
		if (_networkMigrationPlan is not { } plan)
		{
			FailActiveHostMigration(NetworkMigrationFailureCode.NoPlan, "migration_plan_missing");
			return;
		}

		HostMigrationTransportResult prepareResult = PrepareMigrationTransport(plan, now, out string prepareError);
		if (prepareResult == HostMigrationTransportResult.Failed)
		{
			FailActiveHostMigration(GetMigrationTransportFailureCode(plan, prepareError), prepareError);
			return;
		}
		if (prepareResult != HostMigrationTransportResult.Ready)
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
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| IsActiveMigrationHandoffInProgress()
			|| _state is not (NetworkSessionState.Lobby or NetworkSessionState.InGame))
		{
			return;
		}
		if (_lobbyMigrationRecoveryState is not null)
		{
			return;
		}

		if (IsMigrationReadinessFenceActive())
		{
			return;
		}
		if (!HasEligibleHotStandbyPeer())
		{
			if (_hotStandbySeat >= 0 || _networkMigrationPlan is not null)
			{
				ReleaseHotStandbyCandidate(rekeyFormerCandidate: _hotStandbySeat >= 0);
			}
			else
			{
				TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.SelectingCandidate);
			}
			return;
		}

		SendCheckpointToHotStandbyIfNeeded(now);
		if (_hotStandbySeat < 0)
		{
			return;
		}

		if (_standbyPendingCheckpointId < 0 || _standbyCheckpointSentAt < 0.0)
		{
			return;
		}

		if (now - _standbyCheckpointSentAt < NetworkMigrationRules.StandbyCheckpointAckTimeoutSeconds)
		{
			return;
		}

		_consecutiveUnackedCheckpoints++;
		ClearPendingStandbyCheckpoint();
		if (NetworkMigrationRules.ShouldSwitchToNextCandidate(true, _consecutiveUnackedCheckpoints))
		{
			_failedHotStandbySeats.Add(_hotStandbySeat);
			ReleaseHotStandbyCandidate(rekeyFormerCandidate: true);
		}

		SendCheckpointToHotStandbyIfNeeded(now);
	}

	private bool HasEligibleHotStandbyPeer()
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return false;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (!player.IsHost
				&& player.IsConnected
				&& CanPeerActAsHotStandby(player.PeerId))
			{
				return true;
			}
		}
		return false;
	}

	private void PollHostMigration(double now)
	{
		if (!HostMigrationCapabilityAvailable || _state != NetworkSessionState.MigratingHost)
		{
			return;
		}

		if (_networkMigrationPlan is not { } plan || !NetworkMigrationRules.IsPlanVisibleToClients(plan))
		{
			FailMigration(NetworkMigrationFailureCode.NoPlan);
			return;
		}

		HostMigrationTransportResult prepareResult = PrepareMigrationTransport(plan, now, out string prepareError);
		if (prepareResult == HostMigrationTransportResult.Failed)
		{
			FailMigration(GetMigrationTransportFailureCode(plan, prepareError));
			return;
		}
		if (!NetworkMigrationRules.CanRebuildMigrationPeer(
				prepareResult == HostMigrationTransportResult.Ready,
				_migrationAwaitingSourceDisconnect))
		{
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
		return string.Equals(plan.CandidatePlayerSessionId, GetPlayerSessionId(), StringComparison.Ordinal);
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


	private void TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState nextState)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			_migrationCoordinatorState = NetworkMigrationCoordinatorState.Disabled;
			return;
		}

		if (_migrationCoordinatorState == nextState)
		{
			return;
		}
		if (!NetworkMigrationRules.CanTransitionCoordinatorState(_migrationCoordinatorState, nextState))
		{
			GD.PushError($"Invalid migration coordinator transition: {_migrationCoordinatorState} -> {nextState}");
			_migrationCoordinatorState = NetworkMigrationCoordinatorState.Failed;
			return;
		}

		_migrationCoordinatorState = nextState;
	}

	private void TransitionMigrationCoordinatorToReady()
	{
		if (!HostMigrationCapabilityAvailable)
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Disabled);
			return;
		}

		if (_migrationCoordinatorState is NetworkMigrationCoordinatorState.Disabled
			or NetworkMigrationCoordinatorState.Completed
			or NetworkMigrationCoordinatorState.Failed)
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.SelectingCandidate);
		}
		if (_migrationCoordinatorState is NetworkMigrationCoordinatorState.SelectingCandidate
			or NetworkMigrationCoordinatorState.KeyAgreement)
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement);
		}
		if (_migrationCoordinatorState == NetworkMigrationCoordinatorState.AwaitingCheckpointAcknowledgement)
		{
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.AwaitingPlanAcknowledgement);
		}
		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Ready);
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
		return player.PeerId != 0 || player.PlayerSessionId.Length > 0;
	}

	private string GetPlayerSessionIdForSeat(int seatIndex)
	{
		return _players.FirstOrDefault(player => player.SeatIndex == seatIndex).PlayerSessionId;
	}

	private long GetCurrentAuthorityEpoch()
	{
		if (TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoard))
		{
			_authorityEpoch = Math.Max(_authorityEpoch, gameBoard.AuthorityEpoch);
			return gameBoard.AuthorityEpoch;
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
		if (TryGetReplicaEndpoint(out INetworkReplicaEndpoint gameBoard))
		{
			gameBoard.AdvanceAuthorityEpoch(authorityEpoch);
		}
	}


}
