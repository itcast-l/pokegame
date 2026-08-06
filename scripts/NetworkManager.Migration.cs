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

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcMigrationReady(string sessionId, string matchId, long authorityEpoch)
	{
		if (!HostMigrationCapabilityAvailable
			|| IsHost
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
		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Completed);
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
		SetState(NetworkSessionState.InGame);
		if (TryGetPresentationEndpoint(out INetworkPresentationEndpoint gameBoard))
		{
			gameBoard.CompleteHostMigration();
		}
		GD.Print($"MIGRATION_READY_APPLIED epoch={authorityEpoch}");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcMigrationFollowerSnapshotApplied(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int seatIndex,
		string playerSessionId)
	{
		if (!HostMigrationCapabilityAvailable || !IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}
		if (!AllowPeerRequest(NetworkRequestCategory.ProtocolAck))
		{
			return;
		}
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!TryResolveSeatFromPeer(senderPeerId, out int senderSeat)
			|| senderSeat != seatIndex
			|| !TryGetSeatControlState(seatIndex, out SeatControlState seatState, out _)
			|| seatState.PeerId != senderPeerId
			|| seatState.Mode is not (SeatControlMode.HumanConnected or SeatControlMode.HumanReclaimPending)
			|| !string.Equals(seatState.PlayerSessionId, playerSessionId, StringComparison.Ordinal))
		{
			return;
		}

		if (_gameMigrationRecoveryState is { } recovery
			&& recovery.TargetEpoch == authorityEpoch)
		{
			if (recovery.TryMarkSnapshotApplied(seatIndex))
			{
				GD.Print($"MIGRATION_SNAPSHOT_APPLIED_ACK epoch={authorityEpoch} seat={seatIndex} pending={recovery.PendingSeatCount}");
				TryCompleteMigrationReadiness();
			}
			return;
		}

		if (NetworkMigrationRules.IsCompletedGameRecoveryEpoch(
				authorityEpoch,
				_lastCompletedMigrationReadyEpoch,
				GetCurrentAuthorityEpoch()))
		{
			SendMigrationReadyToPeer(
				senderPeerId,
				new NetworkRpcContext(sessionId, matchId, authorityEpoch));
			GD.Print($"MIGRATION_LATE_FOLLOWER_READY epoch={authorityEpoch} seat={seatIndex}");
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcClaimMigratedLobbySeat(
		string sessionId,
		long authorityEpoch,
		string playerSessionId,
		long credentialGeneration,
		string credential)
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| _state != NetworkSessionState.Lobby
			|| _networkMigrationPlan is not { Phase: NetworkAuthorityPhase.Lobby } migrationPlan
			|| !MatchesCurrentRpcContext(sessionId, string.Empty, authorityEpoch)
			|| authorityEpoch != migrationPlan.TargetEpoch)
		{
			return;
		}
		if (!AllowPeerRequest(NetworkRequestCategory.Recovery, requireRegisteredPeer: false))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (string.Equals(playerSessionId, migrationPlan.SourcePlayerSessionId, StringComparison.Ordinal))
		{
			NetworkRpcContext context = GetCurrentRpcContext();
			RpcId(
				senderPeerId,
				MethodName.RpcLobbyRejected,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				NetworkMigrationFailureCode.ExpiredCredential.ToString());
			return;
		}

		int playerIndex = _players.FindIndex(player =>
			string.Equals(player.PlayerSessionId, playerSessionId, StringComparison.Ordinal));
		if (playerIndex < 0
			|| !TryGetSeatControlState(_players[playerIndex].SeatIndex, out SeatControlState control, out int controlIndex)
			|| credentialGeneration != control.CredentialGeneration
			|| !SeatCredentialRules.Verify(credential, control.CredentialVerifier)
			|| (control.PendingCredentialGeneration > 0 && control.PendingCredentialPeerId != senderPeerId))
		{
			NetworkRpcContext context = GetCurrentRpcContext();
			RpcId(
				senderPeerId,
				MethodName.RpcLobbyRejected,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				NetworkMigrationFailureCode.ExpiredCredential.ToString());
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
		SeatControlState boundControl = control with
		{
			Mode = SeatControlMode.HumanConnected,
			PeerId = senderPeerId,
		};
		string credentialToSend = string.Empty;
		if (control.PendingCredentialGeneration == 0)
		{
			if (!SeatCredentialRules.TryBeginRotation(
				boundControl,
				senderPeerId,
				out boundControl,
				out string nextCredential))
			{
				return;
			}
			_issuedSeatCredentialsBySeat[player.SeatIndex] = nextCredential;
			credentialToSend = nextCredential;
		}
		else if (_issuedSeatCredentialsBySeat.TryGetValue(player.SeatIndex, out string? pendingCredential))
		{
			credentialToSend = pendingCredential;
		}
		_seatControlStates[controlIndex] = boundControl;
		if (!string.IsNullOrEmpty(credentialToSend))
		{
			SendSeatCredentialToPeer(senderPeerId, boundControl, credentialToSend);
		}

		BroadcastPlayerList();
		CommitAuthorityCheckpointFromLobby();
	}

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
			foreach (NetworkPlayerPublicState player in _players)
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
		foreach (NetworkPlayerPublicState player in _players)
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
			_players
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

	private void MarkCandidateAsNewLobbyHost(int candidateSeat, string sourcePlayerSessionId)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		var oldControlsByPlayerSessionId = _seatControlStates
			.ToDictionary(state => state.PlayerSessionId, StringComparer.Ordinal);
		List<NetworkPlayerPublicState> remainingPlayers = _players
			.Where(player => !string.Equals(player.PlayerSessionId, sourcePlayerSessionId, StringComparison.Ordinal))
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
		_issuedSeatCredentialsBySeat.Clear();

		for (int newSeat = 0; newSeat < remainingPlayers.Count; newSeat++)
		{
			NetworkPlayerPublicState oldPlayer = remainingPlayers[newSeat];
			bool isLocalCandidate = string.Equals(oldPlayer.PlayerSessionId, GetPlayerSessionId(), StringComparison.Ordinal);
			NetworkPlayerPublicState player = oldPlayer with
			{
				PeerId = isLocalCandidate ? 1 : 0,
				SeatIndex = newSeat,
				IsHost = isLocalCandidate,
				IsLocal = isLocalCandidate,
				IsConnected = isLocalCandidate,
				CredentialsAcknowledged = isLocalCandidate || oldPlayer.CredentialsAcknowledged,
			};
			_players.Add(player);

			if (!oldControlsByPlayerSessionId.TryGetValue(oldPlayer.PlayerSessionId, out SeatControlState oldControl))
			{
				string replacementCredential = SeatCredentialRules.IssueCredential();
				oldControl = SeatControlRules.CreateInitialState(
					oldPlayer,
					SeatCredentialRules.ComputeVerifier(replacementCredential));
				_issuedSeatCredentialsBySeat[newSeat] = replacementCredential;
			}

			SeatControlState control = oldControl with
			{
				SeatIndex = newSeat,
				Mode = isLocalCandidate ? SeatControlMode.HumanConnected : SeatControlMode.HumanReclaimPending,
				PeerId = isLocalCandidate ? 1 : 0,
			};
			_seatControlStates.Add(control);
			_reconnectClaimsBySeat[newSeat] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, control);
		}

		EmitPlayerListUpdated();
		EmitSeatControlStatesUpdated();
	}

	private void BeginLobbyMigrationRecovery(long targetEpoch, double now)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		_lobbyMigrationRecoveryState = new LobbyMigrationRecoveryState(
			targetEpoch,
			now,
			NetworkMigrationRules.LobbyRecoveryTimeoutSeconds,
			_players
				.Where(player => !player.IsLocal)
				.Select(player => player.PlayerSessionId));
	}

	private void PollLobbyMigrationRecovery(double now)
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| _state != NetworkSessionState.Lobby
			|| _lobbyMigrationRecoveryState is not { } recovery)
		{
			return;
		}

		if (recovery.IsComplete)
		{
			TryFinalizeLobbyMigrationRecovery(now);
			return;
		}
		if (!recovery.HasExpired(now))
		{
			return;
		}

		CompactLobbyAfterMigrationTimeout(recovery);
		BroadcastPlayerList();
		EmitSeatControlStatesUpdated();
		CommitAuthorityCheckpointFromLobby();
		TryFinalizeLobbyMigrationRecovery(now, force: true);
	}

	private void TryFinalizeLobbyMigrationRecovery(double now, bool force = false)
	{
		if (!HostMigrationCapabilityAvailable
			|| _lobbyMigrationRecoveryState is not { } recovery
			|| (!force && !recovery.IsComplete))
		{
			return;
		}

		_lobbyMigrationRecoveryState = null;
		ClearLocalStandbyCheckpointCache();
		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Completed);
		ReleaseHotStandbyCandidate(rekeyFormerCandidate: false, invalidateAvailability: false);
		_failedHotStandbySeats.Clear();
		CommitAuthorityCheckpointFromLobby();
		SendCheckpointToHotStandbyIfNeeded(now);
	}

	private void ClearPendingStandbyCheckpoint()
	{
		_standbyPendingCheckpointId = -1;
		_standbyPendingCheckpointHash = string.Empty;
		_standbyPendingEncryptedCheckpoint = null;
		_standbyCheckpointSentAt = -1.0;
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
			foreach (NetworkPlayerPublicState player in _players)
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

	private void CompactLobbyAfterMigrationTimeout(LobbyMigrationRecoveryState recovery)
	{
		List<NetworkPlayerPublicState> keptPlayers = _players
			.Where(player => player.IsLocal
				|| !recovery.ExpectedPlayerSessionIds.Contains(player.PlayerSessionId)
				|| recovery.IsClaimed(player.PlayerSessionId))
			.OrderBy(player => player.SeatIndex)
			.ToList();
		CompactLobbySeatMappings(keptPlayers);
	}

	private void CompactLobbySeatMappings(IReadOnlyList<NetworkPlayerPublicState> keptPlayers)
	{
		var controlsByPlayerSessionId = _seatControlStates.ToDictionary(
			state => state.PlayerSessionId,
			StringComparer.Ordinal);
		var claimsByPlayerSessionId = _reconnectClaimsBySeat.Values.ToDictionary(
			claim => claim.PlayerSessionId,
			StringComparer.Ordinal);
		var oldSeatByPlayerSessionId = _players.ToDictionary(
			player => player.PlayerSessionId,
			player => player.SeatIndex,
			StringComparer.Ordinal);
		var compactPlayers = new List<NetworkPlayerPublicState>(keptPlayers.Count);
		var compactControls = new List<SeatControlState>(keptPlayers.Count);
		var compactClaims = new Dictionary<int, ReconnectClaimRecord>();
		var compactIssuedCredentials = new Dictionary<int, string>();
		for (int newSeat = 0; newSeat < keptPlayers.Count; newSeat++)
		{
			NetworkPlayerPublicState player = keptPlayers[newSeat] with { SeatIndex = newSeat };
			SeatControlState control = controlsByPlayerSessionId[player.PlayerSessionId] with { SeatIndex = newSeat };
			ReconnectClaimRecord claim = claimsByPlayerSessionId.TryGetValue(player.PlayerSessionId, out ReconnectClaimRecord existingClaim)
				? existingClaim with { SeatIndex = newSeat }
				: SeatControlRules.CreateInitialClaimRecord(string.Empty, control);
			compactPlayers.Add(player);
			compactControls.Add(control);
			compactClaims[newSeat] = claim;
			if (oldSeatByPlayerSessionId.TryGetValue(player.PlayerSessionId, out int oldSeat)
				&& _issuedSeatCredentialsBySeat.TryGetValue(oldSeat, out string? credential))
			{
				compactIssuedCredentials[newSeat] = credential;
			}
		}

		_players.Clear();
		_players.AddRange(compactPlayers);
		_seatControlStates.Clear();
		_seatControlStates.AddRange(compactControls);
		_reconnectClaimsBySeat.Clear();
		foreach (KeyValuePair<int, ReconnectClaimRecord> pair in compactClaims)
		{
			_reconnectClaimsBySeat[pair.Key] = pair.Value;
		}
		_issuedSeatCredentialsBySeat.Clear();
		foreach (KeyValuePair<int, string> pair in compactIssuedCredentials)
		{
			_issuedSeatCredentialsBySeat[pair.Key] = pair.Value;
		}
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

	private bool CanLocalTransportTakeOver()
	{
		if (!HostMigrationCapabilityAvailable
			|| !TryBuildLocalMigrationProbeDescriptor(out NetworkMigrationTransportDescriptor descriptor)
			|| CreateMigrationTransport(_config.Transport) is not { } transport)
		{
			return false;
		}

		HostMigrationTransportResult result = transport.ProbeCandidate(descriptor, out string error);
		if (result != HostMigrationTransportResult.Ready)
		{
			GD.Print($"MIGRATION_CANDIDATE_PROBE_FAILED transport={_config.Transport} result={result} error={error}");
			return false;
		}

		return true;
	}

	private bool TryBuildLocalMigrationProbeDescriptor(out NetworkMigrationTransportDescriptor descriptor)
	{
		descriptor = new NetworkMigrationTransportDescriptor();
		if (!HostMigrationCapabilityAvailable)
		{
			return false;
		}

		if (_config.Transport == NetworkTransport.Lan)
		{
			string address = GetObservedLanAddress();
			descriptor = new NetworkMigrationTransportDescriptor
			{
				Lan = new NetworkLanMigrationDescriptor
				{
					ObservedHostAddress = address,
					CandidateHostAddress = address,
				},
			};
			return true;
		}
		if (_config.Transport != NetworkTransport.Steam
			|| _steamLobbyId == 0
			|| _steamLobbyId > (ulong)long.MaxValue
			|| !TryGetLocalSteamId(out long localSteamId))
		{
			return false;
		}

		ulong owner = GetSteamLobbyOwner(_steamLobbyId);
		if (owner == 0 || owner > (ulong)long.MaxValue || (long)owner == localSteamId)
		{
			return false;
		}

		descriptor = new NetworkMigrationTransportDescriptor
		{
			Steam = new NetworkSteamMigrationDescriptor
			{
				LobbyId = (long)_steamLobbyId,
				SourceLobbyOwnerSteamId = (long)owner,
				CandidateSteamId = localSteamId,
				ExpectedLobbyOwnerSteamId = localSteamId,
			},
		};
		return true;
	}

	private bool TryBuildMigrationTransportDescriptor(
		int candidatePeerId,
		string candidateHostAddress,
		out NetworkMigrationTransportDescriptor descriptor)
	{
		descriptor = new NetworkMigrationTransportDescriptor();
		if (!HostMigrationCapabilityAvailable)
		{
			return false;
		}

		if (_config.Transport == NetworkTransport.Lan)
		{
			var lan = new NetworkLanMigrationDescriptor
			{
				ObservedHostAddress = GetObservedLanAddress(),
				CandidateHostAddress = candidateHostAddress.Trim(),
			};
			descriptor = new NetworkMigrationTransportDescriptor { Lan = lan };
			return NetworkMigrationRules.IsLanMigrationDescriptorValid(lan);
		}
		if (_config.Transport != NetworkTransport.Steam
			|| _steamLobbyId == 0
			|| _steamLobbyId > (ulong)long.MaxValue
			|| !TryGetLocalSteamId(out long localSteamId)
			|| !TryGetSteamIdForPeer(candidatePeerId, out long candidateSteamId))
		{
			return false;
		}

		ulong owner = GetSteamLobbyOwner(_steamLobbyId);
		if (owner == 0
			|| owner > (ulong)long.MaxValue
			|| (long)owner != localSteamId
			|| candidateSteamId == localSteamId)
		{
			return false;
		}

		var steam = new NetworkSteamMigrationDescriptor
		{
			LobbyId = (long)_steamLobbyId,
			SourceLobbyOwnerSteamId = localSteamId,
			CandidateSteamId = candidateSteamId,
			ExpectedLobbyOwnerSteamId = candidateSteamId,
		};
		descriptor = new NetworkMigrationTransportDescriptor { Steam = steam };
		return NetworkMigrationRules.IsSteamMigrationDescriptorValid(steam);
	}

	private IHostMigrationTransport? CreateMigrationTransport(NetworkTransport transport)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return null;
		}

		return transport switch
		{
			NetworkTransport.Lan => new LanHostMigrationTransport(),
			NetworkTransport.Steam => new SteamHostMigrationTransport(
				_steamPlatform,
				GetLastObservedSteamLobbyOwnerId,
				TryRefreshSteamLobbyAdvertisedState),
			_ => null,
		};
	}

	private void InitializeMigrationTransportState(NetworkMigrationPlan plan, double now)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			ResetMigrationTransportState();
			return;
		}

		if (_migrationTransportAdapter is not null
			&& _migrationTransportAdapter.Transport == plan.Transport
			&& _migrationTransportPlanRevision == plan.PlanRevision)
		{
			return;
		}

		_migrationTransportAdapter = CreateMigrationTransport(plan.Transport);
		_migrationTransportPlanRevision = plan.PlanRevision;
		_migrationTransportPreparedAt = -1.0;
		_migrationOwnerConvergenceDeadline = now + NetworkMigrationRules.SteamLobbyOwnerConvergenceTimeoutSeconds;
	}

	private HostMigrationTransportResult PrepareMigrationTransport(
		NetworkMigrationPlan plan,
		double now,
		out string error)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			error = "migration_capability_unavailable";
			return HostMigrationTransportResult.Failed;
		}

		InitializeMigrationTransportState(plan, now);
		if (_migrationTransportAdapter is not { } transport)
		{
			error = "migration_transport_unavailable";
			return HostMigrationTransportResult.Failed;
		}
		if (plan.Transport == NetworkTransport.Lan && _migrationTransportPreparedAt >= 0.0)
		{
			error = string.Empty;
			return HostMigrationTransportResult.Ready;
		}

		HostMigrationTransportResult result = transport.PrepareTakeover(
			plan,
			now,
			_migrationOwnerConvergenceDeadline,
			out error);
		if (result == HostMigrationTransportResult.Ready)
		{
			_migrationTransportPreparedAt = now;
		}
		return result;
	}

	private void ArmMigrationAttemptsAfterSourceDisconnect(NetworkMigrationPlan plan, double now)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		_migrationAwaitingSourceDisconnect = false;
		_migrationStartedAt = now;
		_nextMigrationAttemptAt = now + (IsLocalMigrationCandidate(plan)
			? NetworkMigrationRules.CandidateStartDelaySeconds
			: NetworkMigrationRules.FollowerStartDelaySeconds);
		_migrationServerStarted = false;
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
	}

	private void ResetMigrationTransportState()
	{
		_migrationTransportAdapter = null;
		_migrationTransportPlanRevision = -1;
		_migrationTransportPreparedAt = -1.0;
		_migrationOwnerConvergenceDeadline = -1.0;
		_migrationAwaitingSourceDisconnect = false;
	}

	private static NetworkMigrationFailureCode GetMigrationTransportFailureCode(
		NetworkMigrationPlan plan,
		string error)
	{
		return plan.Transport == NetworkTransport.Steam
			&& error.Contains("owner", StringComparison.Ordinal)
			? NetworkMigrationFailureCode.SteamOwnerTimeout
			: NetworkMigrationFailureCode.TransportUnavailable;
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

	private bool UpdateMigratedHostMetadata(NetworkMigrationPlan plan)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return false;
		}

		string error = "migration_transport_unavailable";
		if (_migrationTransportAdapter is { } transport
			&& transport.UpdateMetadata(plan, out error) == HostMigrationTransportResult.Ready)
		{
			return true;
		}

		GD.Print($"MIGRATION_METADATA_UPDATE_FAILED endpoint={GetMigrationEndpointLog(plan)} error={error}");
		FailMigration(plan.Transport == NetworkTransport.Steam
			? NetworkMigrationFailureCode.SteamMetadataUpdateFailed
			: NetworkMigrationFailureCode.TransportUnavailable);
		return false;
	}

	private string GetLocalMigrationEndpointAcknowledgement()
	{
		return _config.Transport == NetworkTransport.Lan
			? GetObservedLanAddress()
			: string.Empty;
	}

	private static bool IsMigrationEndpointAcknowledgementValid(
		NetworkTransport transport,
		string endpoint)
	{
		return transport switch
		{
			NetworkTransport.Lan => !string.IsNullOrWhiteSpace(endpoint)
				&& endpoint.Length <= NetworkProtocol.MaxAddressLength
				&& !endpoint.Any(char.IsWhiteSpace),
			NetworkTransport.Steam => string.IsNullOrEmpty(endpoint),
			_ => false,
		};
	}

	private static string GetMigrationEndpointLog(NetworkMigrationPlan? plan)
	{
		if (plan is null)
		{
			return "none";
		}

		return plan.Transport switch
		{
			NetworkTransport.Lan when plan.TransportDescriptor.Lan is { } lan => $"lan:{lan.CandidateHostAddress}:{lan.Port}",
			NetworkTransport.Steam when plan.TransportDescriptor.Steam is { } steam => $"steam:lobby={steam.LobbyId}:owner={steam.ExpectedLobbyOwnerSteamId}",
			_ => "invalid",
		};
	}

	private static Godot.Collections.Dictionary ToMigrationPlanVariant(NetworkMigrationPlan plan)
	{
		return new Godot.Collections.Dictionary
		{
			["protocol_version"] = plan.ProtocolVersion,
			["plan_revision"] = plan.PlanRevision,
			["session_id"] = plan.SessionId,
			["match_id"] = plan.MatchId,
			["source_epoch"] = plan.SourceEpoch,
			["target_epoch"] = plan.TargetEpoch,
			["checkpoint_id"] = plan.CheckpointId,
			["checkpoint_hash"] = plan.CheckpointHash,
			["checkpoint_ciphertext_hash"] = plan.CheckpointCiphertextHash,
			["previous_checkpoint_hash"] = plan.PreviousCheckpointHash,
			["candidate_player_session_id"] = plan.CandidatePlayerSessionId,
			["candidate_seat"] = plan.CandidateSeat,
			["candidate_successor_public_key"] = plan.CandidateSuccessorPublicKey,
			["candidate_successor_key_id"] = plan.CandidateSuccessorKeyId,
			["source_player_session_id"] = plan.SourcePlayerSessionId,
			["source_authority_key_id"] = plan.SourceAuthorityKeyId,
			["transport"] = plan.Transport.ToString(),
			["transport_descriptor"] = ToMigrationTransportDescriptorVariant(plan),
			["authority_phase"] = plan.Phase.ToString(),
			["source_signature"] = plan.SourceSignature,
		};
	}

	private static Godot.Collections.Dictionary ToMigrationTransportDescriptorVariant(NetworkMigrationPlan plan)
	{
		return plan.Transport switch
		{
			NetworkTransport.Lan when plan.TransportDescriptor.Lan is { } lan => new Godot.Collections.Dictionary
			{
				["observed_host_address"] = lan.ObservedHostAddress,
				["candidate_host_address"] = lan.CandidateHostAddress,
				["port"] = lan.Port,
				["max_clients"] = lan.MaxClients,
				["peer_rebuild_mode"] = lan.PeerRebuildMode.ToString(),
			},
			NetworkTransport.Steam when plan.TransportDescriptor.Steam is { } steam => new Godot.Collections.Dictionary
			{
				["lobby_id"] = steam.LobbyId,
				["source_lobby_owner_steam_id"] = steam.SourceLobbyOwnerSteamId,
				["candidate_steam_id"] = steam.CandidateSteamId,
				["expected_lobby_owner_steam_id"] = steam.ExpectedLobbyOwnerSteamId,
				["peer_rebuild_mode"] = steam.PeerRebuildMode.ToString(),
			},
			_ => new Godot.Collections.Dictionary(),
		};
	}

	private static Godot.Collections.Dictionary ToMigrationPlanRevocationVariant(
		NetworkMigrationPlanRevocation revocation)
	{
		return new Godot.Collections.Dictionary
		{
			["protocol_version"] = revocation.ProtocolVersion,
			["plan_revision"] = revocation.PlanRevision,
			["revoked_plan_revision"] = revocation.RevokedPlanRevision,
			["revoked_checkpoint_id"] = revocation.RevokedCheckpointId,
			["session_id"] = revocation.SessionId,
			["match_id"] = revocation.MatchId,
			["source_epoch"] = revocation.SourceEpoch,
			["source_authority_key_id"] = revocation.SourceAuthorityKeyId,
			["source_signature"] = revocation.SourceSignature,
		};
	}

	private static bool TryParseMigrationPlan(
		Godot.Collections.Dictionary raw,
		out NetworkMigrationPlan plan)
	{
		plan = new NetworkMigrationPlan();
		if (!NetworkVariantReader.TryReadInt32(raw, "protocol_version", NetworkProtocol.CurrentVersion, NetworkProtocol.CurrentVersion, out int protocolVersion)
			|| !NetworkVariantReader.TryReadInt64(raw, "plan_revision", 1, long.MaxValue, out long planRevision)
			|| !NetworkVariantReader.TryReadString(raw, "session_id", NetworkProtocol.MaxSessionIdLength, out string sessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "match_id", NetworkProtocol.MaxMatchIdLength, out string matchId)
			|| !NetworkVariantReader.TryReadInt64(raw, "source_epoch", 0, long.MaxValue, out long sourceEpoch)
			|| !NetworkVariantReader.TryReadInt64(raw, "target_epoch", 0, long.MaxValue, out long targetEpoch)
			|| !NetworkVariantReader.TryReadInt64(raw, "checkpoint_id", 0, long.MaxValue, out long checkpointId)
			|| !NetworkVariantReader.TryReadString(raw, "checkpoint_hash", 64, out string checkpointHash, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "checkpoint_ciphertext_hash", 64, out string checkpointCiphertextHash, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "previous_checkpoint_hash", 64, out string previousCheckpointHash)
			|| !NetworkVariantReader.TryReadString(raw, "candidate_player_session_id", NetworkProtocol.MaxPlayerIdLength, out string candidatePlayerSessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadInt32(raw, "candidate_seat", 0, NetworkSessionDefaults.MaxPlayers - 1, out int candidateSeat)
			|| !NetworkVariantReader.TryReadString(raw, "candidate_successor_public_key", 512, out string candidateSuccessorPublicKey, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "candidate_successor_key_id", 64, out string candidateSuccessorKeyId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "source_player_session_id", NetworkProtocol.MaxPlayerIdLength, out string sourcePlayerSessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "source_authority_key_id", 64, out string sourceAuthorityKeyId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadEnum(raw, "transport", out NetworkTransport transport)
			|| !NetworkVariantReader.TryReadDictionary(raw, "transport_descriptor", out Godot.Collections.Dictionary rawTransportDescriptor)
			|| !TryParseMigrationTransportDescriptor(transport, rawTransportDescriptor, out NetworkMigrationTransportDescriptor transportDescriptor)
			|| !NetworkVariantReader.TryReadEnum(raw, "authority_phase", out NetworkAuthorityPhase phase)
			|| !NetworkVariantReader.TryReadString(raw, "source_signature", 512, out string sourceSignature, allowEmpty: false))
		{
			return false;
		}

		plan = new NetworkMigrationPlan
		{
			ProtocolVersion = protocolVersion,
			PlanRevision = planRevision,
			SessionId = sessionId,
			MatchId = matchId,
			SourceEpoch = sourceEpoch,
			TargetEpoch = targetEpoch,
			CheckpointId = checkpointId,
			CheckpointHash = checkpointHash,
			CheckpointCiphertextHash = checkpointCiphertextHash,
			PreviousCheckpointHash = previousCheckpointHash,
			CandidatePlayerSessionId = candidatePlayerSessionId,
			CandidateSeat = candidateSeat,
			CandidateSuccessorPublicKey = candidateSuccessorPublicKey,
			CandidateSuccessorKeyId = candidateSuccessorKeyId,
			SourcePlayerSessionId = sourcePlayerSessionId,
			SourceAuthorityKeyId = sourceAuthorityKeyId,
			Transport = transport,
			TransportDescriptor = transportDescriptor,
			Phase = phase,
			SourceSignature = sourceSignature,
		};
		return NetworkMigrationRules.IsPlanVisibleToClients(plan);
	}

	private static bool TryParseMigrationTransportDescriptor(
		NetworkTransport transport,
		Godot.Collections.Dictionary raw,
		out NetworkMigrationTransportDescriptor descriptor)
	{
		descriptor = new NetworkMigrationTransportDescriptor();
		if (transport == NetworkTransport.Lan)
		{
			if (!NetworkVariantReader.TryReadString(raw, "observed_host_address", NetworkProtocol.MaxAddressLength, out string observedHostAddress, allowEmpty: false)
				|| !NetworkVariantReader.TryReadString(raw, "candidate_host_address", NetworkProtocol.MaxAddressLength, out string candidateHostAddress, allowEmpty: false)
				|| !NetworkVariantReader.TryReadInt32(raw, "port", 1, ushort.MaxValue, out int port)
				|| !NetworkVariantReader.TryReadInt32(raw, "max_clients", 1, NetworkSessionDefaults.MaxPlayers - 1, out int maxClients)
				|| !NetworkVariantReader.TryReadEnum(raw, "peer_rebuild_mode", out NetworkMigrationPeerRebuildMode peerRebuildMode))
			{
				return false;
			}

			var lan = new NetworkLanMigrationDescriptor
			{
				ObservedHostAddress = observedHostAddress,
				CandidateHostAddress = candidateHostAddress,
				Port = port,
				MaxClients = maxClients,
				PeerRebuildMode = peerRebuildMode,
			};
			descriptor = new NetworkMigrationTransportDescriptor { Lan = lan };
			return NetworkMigrationRules.IsLanMigrationDescriptorValid(lan);
		}
		if (transport != NetworkTransport.Steam
			|| !NetworkVariantReader.TryReadInt64(raw, "lobby_id", 1, long.MaxValue, out long lobbyId)
			|| !NetworkVariantReader.TryReadInt64(raw, "source_lobby_owner_steam_id", 1, long.MaxValue, out long sourceOwnerSteamId)
			|| !NetworkVariantReader.TryReadInt64(raw, "candidate_steam_id", 1, long.MaxValue, out long candidateSteamId)
			|| !NetworkVariantReader.TryReadInt64(raw, "expected_lobby_owner_steam_id", 1, long.MaxValue, out long expectedOwnerSteamId)
			|| !NetworkVariantReader.TryReadEnum(raw, "peer_rebuild_mode", out NetworkMigrationPeerRebuildMode steamPeerRebuildMode))
		{
			return false;
		}

		var steam = new NetworkSteamMigrationDescriptor
		{
			LobbyId = lobbyId,
			SourceLobbyOwnerSteamId = sourceOwnerSteamId,
			CandidateSteamId = candidateSteamId,
			ExpectedLobbyOwnerSteamId = expectedOwnerSteamId,
			PeerRebuildMode = steamPeerRebuildMode,
		};
		descriptor = new NetworkMigrationTransportDescriptor { Steam = steam };
		return NetworkMigrationRules.IsSteamMigrationDescriptorValid(steam);
	}

	private static bool TryParseMigrationPlanRevocation(
		Godot.Collections.Dictionary raw,
		out NetworkMigrationPlanRevocation revocation)
	{
		revocation = new NetworkMigrationPlanRevocation();
		if (!NetworkVariantReader.TryReadInt32(raw, "protocol_version", NetworkProtocol.CurrentVersion, NetworkProtocol.CurrentVersion, out int protocolVersion)
			|| !NetworkVariantReader.TryReadInt64(raw, "plan_revision", 1, long.MaxValue, out long planRevision)
			|| !NetworkVariantReader.TryReadInt64(raw, "revoked_plan_revision", 1, long.MaxValue, out long revokedPlanRevision)
			|| !NetworkVariantReader.TryReadInt64(raw, "revoked_checkpoint_id", 0, long.MaxValue, out long revokedCheckpointId)
			|| !NetworkVariantReader.TryReadString(raw, "session_id", NetworkProtocol.MaxSessionIdLength, out string sessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "match_id", NetworkProtocol.MaxMatchIdLength, out string matchId)
			|| !NetworkVariantReader.TryReadInt64(raw, "source_epoch", 0, long.MaxValue, out long sourceEpoch)
			|| !NetworkVariantReader.TryReadString(raw, "source_authority_key_id", 64, out string sourceAuthorityKeyId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "source_signature", 512, out string sourceSignature, allowEmpty: false))
		{
			return false;
		}

		revocation = new NetworkMigrationPlanRevocation
		{
			ProtocolVersion = protocolVersion,
			PlanRevision = planRevision,
			RevokedPlanRevision = revokedPlanRevision,
			RevokedCheckpointId = revokedCheckpointId,
			SessionId = sessionId,
			MatchId = matchId,
			SourceEpoch = sourceEpoch,
			SourceAuthorityKeyId = sourceAuthorityKeyId,
			SourceSignature = sourceSignature,
		};
		return NetworkMigrationRules.IsPlanRevocationVisibleToClients(revocation);
	}

	private bool IsMigrationReadinessFenceActive()
	{
		return HostMigrationCapabilityAvailable
			&& _migrationReadyEpoch >= 0
			&& _networkMigrationPlan is { } plan
			&& plan.TargetEpoch == _migrationReadyEpoch;
	}

	private void BeginMigrationReadinessFence(NetworkMigrationPlan plan)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		_migrationReadyEpoch = plan.TargetEpoch;
		_lastCompletedMigrationReadyEpoch = -1;
		_migrationRecoveryReadyForPeers = false;
		_gameMigrationRecoveryState = null;
	}

	private void BeginMigrationReadinessFence(
		NetworkMigrationPlan plan,
		IReadOnlyCollection<NetworkAuthoritySeatControl> checkpointControls,
		double startedAt)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		_migrationReadyEpoch = plan.TargetEpoch;
		_lastCompletedMigrationReadyEpoch = -1;
		_migrationRecoveryReadyForPeers = false;
		_gameMigrationRecoveryState = new GameMigrationRecoveryState(
			plan.TargetEpoch,
			startedAt,
			checkpointControls
				.Where(control => control.Mode == SeatControlMode.HumanConnected
					&& control.SeatIndex != plan.CandidateSeat
					&& !string.Equals(control.PlayerSessionId, plan.SourcePlayerSessionId, StringComparison.Ordinal))
				.Select(control => control.SeatIndex));
		GD.Print($"MIGRATION_RECOVERY_FENCE_STARTED epoch={plan.TargetEpoch} expected={_gameMigrationRecoveryState.ExpectedSeatCount} deadline={_gameMigrationRecoveryState.Deadline:0.000}");
	}

	private void TryCompleteMigrationReadiness()
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| !IsMigrationReadinessFenceActive()
			|| !_migrationRecoveryReadyForPeers
			|| _networkMigrationPlan is not { } plan
			|| _gameMigrationRecoveryState is not { IsComplete: true } recovery
			|| recovery.TargetEpoch != plan.TargetEpoch)
		{
			return;
		}

		CompleteMigrationReadiness(
			plan,
			recovery,
			timedOut: false,
			missingSeatIndices: Array.Empty<int>());
	}

	private void PollMigrationReadiness(double now)
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| _gameMigrationRecoveryState is not { } recovery
			|| recovery.TargetEpoch != _migrationReadyEpoch
			|| _networkMigrationPlan is not { } plan
			|| plan.TargetEpoch != recovery.TargetEpoch
			|| !recovery.HasExpired(now))
		{
			return;
		}

		int[] missingSeatIndices = recovery.PendingSeatIndices.OrderBy(seatIndex => seatIndex).ToArray();
		int recoveredSeatCount = recovery.RecoveredSeatCount;
		if (!recovery.TryExpire(now))
		{
			return;
		}

		MarkMigrationRecoverySeatsUnavailable(missingSeatIndices);
		GD.Print($"MIGRATION_RECOVERY_TIMEOUT epoch={recovery.TargetEpoch} recovered={recoveredSeatCount} missing={missingSeatIndices.Length} deadline={recovery.Deadline:0.000} now={now:0.000}");
		CompleteMigrationReadiness(
			plan,
			recovery,
			timedOut: true,
			missingSeatIndices: missingSeatIndices);
	}

	private void CompleteMigrationReadiness(
		NetworkMigrationPlan plan,
		GameMigrationRecoveryState recovery,
		bool timedOut,
		IReadOnlyCollection<int> missingSeatIndices)
	{
		if (!HostMigrationCapabilityAvailable
			|| !IsHost
			|| !IsMigrationReadinessFenceActive()
			|| recovery.TargetEpoch != plan.TargetEpoch
			|| (!timedOut && (!_migrationRecoveryReadyForPeers || !recovery.IsComplete)))
		{
			return;
		}

		var missingSeats = new HashSet<int>(missingSeatIndices);
		int readinessPeerCount = 0;
		NetworkRpcContext context = new(plan.SessionId, plan.MatchId, plan.TargetEpoch);
		foreach (NetworkPlayerPublicState player in _players)
		{
			if (player.IsConnected && !player.IsHost && !missingSeats.Contains(player.SeatIndex))
			{
				SendMigrationReadyToPeer(player.PeerId, context);
				readinessPeerCount++;
			}
		}

		_lastCompletedMigrationReadyEpoch = context.AuthorityEpoch;
		ClearLocalStandbyCheckpointCache();
		_networkMigrationPlan = null;
		ClearMigrationReadinessFence();
		TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Completed);
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
		if (TryGetPresentationEndpoint(out INetworkPresentationEndpoint gameBoard))
		{
			gameBoard.CompleteHostMigration();
		}
		if (TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint authorityEndpoint))
		{
			authorityEndpoint.BeginAiProxyTurnIfNeeded();
		}
		GD.Print($"MIGRATION_READY epoch={context.AuthorityEpoch} peers={readinessPeerCount} recovered={recovery.RecoveredSeatCount} disconnected={recovery.DisconnectedSeatCount} missing={missingSeats.Count} timed_out={timedOut.ToString().ToLowerInvariant()}");
	}

	private void SendMigrationReadyToPeer(int peerId, NetworkRpcContext context)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		RpcId(
			peerId,
			MethodName.RpcMigrationReady,
			context.SessionId,
			context.MatchId,
			context.AuthorityEpoch);
	}

	private void RemoveMigrationRecoverySeatAfterDisconnect(int seatIndex)
	{
		if (!HostMigrationCapabilityAvailable
			|| _gameMigrationRecoveryState is not { } recovery
			|| recovery.TargetEpoch != _migrationReadyEpoch
			|| !recovery.TryRemoveDisconnectedSeat(seatIndex))
		{
			return;
		}

		GD.Print($"MIGRATION_RECOVERY_DISCONNECTED epoch={recovery.TargetEpoch} seat={seatIndex} pending={recovery.PendingSeatCount}");
		TryCompleteMigrationReadiness();
	}

	private void MarkMigrationRecoverySeatsUnavailable(IReadOnlyCollection<int> missingSeatIndices)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		foreach (int seatIndex in missingSeatIndices)
		{
			if (!TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
			{
				continue;
			}

			NetworkPlayerPublicState player = _players[playerIndex];
			if (!player.IsConnected || player.IsHost || player.PeerId <= 1)
			{
				continue;
			}

			TryMarkSeatDisconnected(player.PeerId);
			_players[playerIndex] = player with { IsConnected = false };
			Multiplayer.MultiplayerPeer?.DisconnectPeer(player.PeerId);
		}
	}

	private void ClearMigrationReadinessFence()
	{
		_migrationReadyEpoch = -1;
		_migrationRecoveryReadyForPeers = false;
		_gameMigrationRecoveryState = null;
		_networkMigrationPlan = null;
	}
}
