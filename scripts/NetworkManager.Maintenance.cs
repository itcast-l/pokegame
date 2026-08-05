using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

public enum NetworkMaintenanceTaskKind
{
	LanResponder,
	LanBrowser,
	LoadHandshake,
	Reconnect,
	SeatCredential,
	LocalReadyIntent,
	ActiveHostMigration,
	LobbyMigrationRecovery,
	MigrationReadiness,
	MigrationCandidateLatency,
	HotStandby,
	HostMigration,
}

public sealed record NetworkMaintenanceTaskSnapshot(
	long ExecutionCount,
	long SkipCount,
	double MaximumDurationMilliseconds);

public partial class NetworkManager
{
	private const double LanMaintenanceIntervalSeconds = 0.05;
	private NetworkMaintenanceScheduler? _maintenanceScheduler;
	private readonly long[] _maintenanceExecutionCounts = new long[Enum.GetValues<NetworkMaintenanceTaskKind>().Length];
	private readonly long[] _maintenanceSkipCounts = new long[Enum.GetValues<NetworkMaintenanceTaskKind>().Length];
	private readonly double[] _maintenanceMaximumDurationMilliseconds = new double[Enum.GetValues<NetworkMaintenanceTaskKind>().Length];
	private readonly long[] _maintenanceManagedAllocationBytes = new long[Enum.GetValues<NetworkMaintenanceTaskKind>().Length];

	public long DebugGetNetworkMaintenanceWakeCount()
	{
		return _maintenanceScheduler?.GetSnapshot().WakeCount ?? 0;
	}

	public long DebugGetCurrentThreadAllocatedBytes()
	{
		return GC.GetAllocatedBytesForCurrentThread();
	}

	public bool DebugIsNetworkMaintenanceSchedulerArmed()
	{
		return _maintenanceScheduler?.GetSnapshot().IsArmed == true;
	}

	public double DebugGetNetworkMaintenanceMaximumDriftMilliseconds()
	{
		return _maintenanceScheduler?.GetSnapshot().MaximumDriftMilliseconds ?? 0.0;
	}

	public double DebugGetNetworkMaintenanceMaximumTickMilliseconds()
	{
		return _maintenanceScheduler?.GetSnapshot().MaximumTickMilliseconds ?? 0.0;
	}

	public long DebugGetNetworkMaintenanceTickAllocatedBytes()
	{
		return _maintenanceScheduler?.GetTickManagedAllocationBytes() ?? 0;
	}

	public long DebugGetNetworkMaintenanceRearmAllocatedBytes()
	{
		return _maintenanceScheduler?.GetRearmManagedAllocationBytes() ?? 0;
	}

	public long DebugGetNetworkMaintenanceTaskExecutionCount(int taskKind)
	{
		return taskKind >= 0 && taskKind < _maintenanceExecutionCounts.Length
			? _maintenanceExecutionCounts[taskKind]
			: 0;
	}

	public long DebugGetNetworkMaintenanceTaskAllocatedBytes(int taskKind)
	{
		return taskKind >= 0 && taskKind < _maintenanceManagedAllocationBytes.Length
			? _maintenanceManagedAllocationBytes[taskKind]
			: 0;
	}

	public NetworkMaintenanceSchedulerSnapshot DebugGetNetworkMaintenanceSchedulerSnapshot()
	{
		return _maintenanceScheduler?.GetSnapshot()
			?? new NetworkMaintenanceSchedulerSnapshot(0, 0, 0, 0.0, 0.0, false, 0);
	}

	public NetworkMaintenanceTaskSnapshot DebugGetNetworkMaintenanceTaskSnapshot(int taskKind)
	{
		if (taskKind < 0 || taskKind >= _maintenanceExecutionCounts.Length)
		{
			return new NetworkMaintenanceTaskSnapshot(0, 0, 0.0);
		}

		return new NetworkMaintenanceTaskSnapshot(
			_maintenanceExecutionCounts[taskKind],
			_maintenanceSkipCounts[taskKind],
			_maintenanceMaximumDurationMilliseconds[taskKind]);
	}

	private void InitializeNetworkMaintenanceScheduler()
	{
		_maintenanceScheduler = new NetworkMaintenanceScheduler
		{
			Name = "NetworkMaintenanceScheduler",
		};
		AddChild(_maintenanceScheduler);
		_maintenanceScheduler.Configure(RunNetworkMaintenance);
		RescheduleNetworkMaintenance(immediate: true);
	}

	private void StopNetworkMaintenanceScheduler()
	{
		_maintenanceScheduler?.Invalidate();
	}

	private void RescheduleNetworkMaintenance(bool immediate = false)
	{
		if (_maintenanceScheduler == null)
		{
			return;
		}

		double now = Time.GetTicksMsec() / 1000.0;
		double? deadline = immediate && HasNetworkMaintenanceWork()
			? now
			: GetNextNetworkMaintenanceDeadline(now);
		_maintenanceScheduler.Reschedule(deadline);
	}

	private double? RunNetworkMaintenance(double now)
	{
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.LanResponder, IsLanResponderMaintenanceActive(), now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.LanBrowser, IsLanBrowserMaintenanceActive(), now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.LoadHandshake, IsLoadHandshakeMaintenanceActive(), now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.Reconnect, _state == NetworkSessionState.Reconnecting, now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.SeatCredential, IsSeatCredentialMaintenanceActive(), now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.LocalReadyIntent, IsLocalReadyIntentMaintenanceActive(), now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.ActiveHostMigration, HostMigrationCapabilityAvailable && IsHost && _activeMigrationPending, now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.LobbyMigrationRecovery, HostMigrationCapabilityAvailable && IsHost && _lobbyMigrationRecoveryState != null, now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.MigrationReadiness, HostMigrationCapabilityAvailable && IsHost && _gameMigrationRecoveryState != null, now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.MigrationCandidateLatency, IsMigrationLatencyMaintenanceActive(), now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.HotStandby, IsHotStandbyMaintenanceActive(), now);
		ExecuteMaintenanceTask(NetworkMaintenanceTaskKind.HostMigration, HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost, now);
		return GetNextNetworkMaintenanceDeadline(Time.GetTicksMsec() / 1000.0);
	}

	private void ExecuteMaintenanceTask(NetworkMaintenanceTaskKind kind, bool active, double now)
	{
		int index = (int)kind;
		if (!active)
		{
			_maintenanceSkipCounts[index]++;
			return;
		}

		long startedAt = Stopwatch.GetTimestamp();
		long allocationStartedAt = GC.GetAllocatedBytesForCurrentThread();
		switch (kind)
		{
			case NetworkMaintenanceTaskKind.LanResponder:
				PollLanDiscoveryResponder(now);
				break;
			case NetworkMaintenanceTaskKind.LanBrowser:
				PollLanDiscoveryBrowser(now);
				break;
			case NetworkMaintenanceTaskKind.LoadHandshake:
				PollLoadHandshake(now);
				break;
			case NetworkMaintenanceTaskKind.Reconnect:
				PollReconnect(now);
				break;
			case NetworkMaintenanceTaskKind.SeatCredential:
				PollSeatCredentialDelivery(now);
				break;
			case NetworkMaintenanceTaskKind.LocalReadyIntent:
				PollLocalReadyIntent(now);
				break;
			case NetworkMaintenanceTaskKind.ActiveHostMigration:
				PollActiveHostMigration(now);
				break;
			case NetworkMaintenanceTaskKind.LobbyMigrationRecovery:
				PollLobbyMigrationRecovery(now);
				break;
			case NetworkMaintenanceTaskKind.MigrationReadiness:
				PollMigrationReadiness(now);
				break;
			case NetworkMaintenanceTaskKind.MigrationCandidateLatency:
				PollMigrationCandidateLatency(now);
				break;
			case NetworkMaintenanceTaskKind.HotStandby:
				PollHotStandby(now);
				break;
			case NetworkMaintenanceTaskKind.HostMigration:
				PollHostMigration(now);
				break;
		}

		_maintenanceManagedAllocationBytes[index] += GC.GetAllocatedBytesForCurrentThread() - allocationStartedAt;
		_maintenanceExecutionCounts[index]++;
		double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
		_maintenanceMaximumDurationMilliseconds[index] = Math.Max(
			_maintenanceMaximumDurationMilliseconds[index],
			elapsedMilliseconds);
	}

	private double? GetNextNetworkMaintenanceDeadline(double now)
	{
		double? deadline = null;
		if (IsLanResponderMaintenanceActive() || IsLanBrowserMaintenanceActive())
		{
			ConsiderDeadline(ref deadline, now + LanMaintenanceIntervalSeconds);
		}

		if (IsLoadHandshakeMaintenanceActive() && _loadHandshake != null)
		{
			ConsiderDeadline(ref deadline, _loadHandshake.StartedAtSeconds + _loadHandshake.TimeoutSeconds);
		}

		if (_state == NetworkSessionState.Reconnecting)
		{
			if (_reconnectWindowStartedAt >= 0.0)
			{
				ConsiderDeadline(ref deadline, _reconnectWindowStartedAt + ClientReconnectWindowSeconds);
			}
			ConsiderDeadline(
				ref deadline,
				_reconnectAttemptInFlight || Multiplayer.MultiplayerPeer != null
					? now + LanMaintenanceIntervalSeconds
					: Math.Max(now, _nextReconnectAttemptAt));
		}

		if (IsSeatCredentialMaintenanceActive())
		{
			ConsiderDeadline(ref deadline, _lastSeatCredentialRetryAt < 0.0
				? now
				: _lastSeatCredentialRetryAt + SeatCredentialRetrySeconds);
		}

		if (IsLocalReadyIntentMaintenanceActive())
		{
			ConsiderDeadline(ref deadline, _lastLocalReadyIntentSentAt < 0.0
				? now
				: _lastLocalReadyIntentSentAt + LocalReadyIntentRetrySeconds);
		}

		if (HostMigrationCapabilityAvailable && IsHost && _activeMigrationPending)
		{
			ConsiderDeadline(ref deadline, now + LanMaintenanceIntervalSeconds);
			if (_activeMigrationDeadline >= 0.0)
			{
				ConsiderDeadline(ref deadline, _activeMigrationDeadline);
			}
		}

		if (HostMigrationCapabilityAvailable && IsHost && _lobbyMigrationRecoveryState is { } lobbyRecovery)
		{
			ConsiderDeadline(ref deadline, lobbyRecovery.IsComplete ? now : lobbyRecovery.Deadline);
		}

		if (HostMigrationCapabilityAvailable && IsHost && _gameMigrationRecoveryState is { } gameRecovery)
		{
			ConsiderDeadline(ref deadline, gameRecovery.Deadline);
		}

		if (HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost)
		{
			ConsiderDeadline(ref deadline, now + LanMaintenanceIntervalSeconds);
		}

		if (IsMigrationLatencyMaintenanceActive())
		{
			double nextProbeAt = _lastMigrationLatencyProbeAt < 0.0
				? now
				: _lastMigrationLatencyProbeAt + NetworkMigrationRules.MigrationLatencyProbeIntervalSeconds;
			ConsiderDeadline(ref deadline, nextProbeAt);
			foreach ((long Sequence, double SentAt) pending in _pendingMigrationLatencyProbesByPeerId.Values)
			{
				ConsiderDeadline(ref deadline, pending.SentAt + NetworkMigrationRules.MigrationLatencyProbeTimeoutSeconds);
			}
		}

		if (IsHotStandbyMaintenanceActive())
		{
			if (_standbyPendingCheckpointId >= 0 && _standbyCheckpointSentAt >= 0.0)
			{
				ConsiderDeadline(ref deadline, _standbyCheckpointSentAt + NetworkMigrationRules.StandbyCheckpointAckTimeoutSeconds);
			}
			else if (_hotStandbySeat >= 0
				&& _latestAuthorityCheckpointEnvelope.HasPayload
				&& _latestAuthorityCheckpointEnvelope.CheckpointId > _standbyAckedCheckpointId)
			{
				ConsiderDeadline(ref deadline, _lastStandbyCheckpointSentAt < 0.0
					? now
					: _lastStandbyCheckpointSentAt + NetworkMigrationRules.StandbyCheckpointMinimumSendIntervalSeconds);
			}
		}

		return deadline;
	}

	private bool HasNetworkMaintenanceWork()
	{
		return IsLanResponderMaintenanceActive()
			|| IsLanBrowserMaintenanceActive()
			|| IsLoadHandshakeMaintenanceActive()
			|| _state == NetworkSessionState.Reconnecting
			|| IsSeatCredentialMaintenanceActive()
			|| IsLocalReadyIntentMaintenanceActive()
			|| (HostMigrationCapabilityAvailable
				&& IsHost
				&& (_activeMigrationPending || _lobbyMigrationRecoveryState != null || _gameMigrationRecoveryState != null))
			|| IsMigrationLatencyMaintenanceActive()
			|| IsHotStandbyMaintenanceActive()
			|| (HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost);
	}

	private bool IsLanResponderMaintenanceActive()
	{
		return _lanDiscoveryResponder != null
			&& _state == NetworkSessionState.Lobby
			&& _config.Transport == NetworkTransport.Lan;
	}

	private bool IsLanBrowserMaintenanceActive()
	{
		return _lanDiscoveryBrowser != null && _state == NetworkSessionState.Searching;
	}

	private bool IsLoadHandshakeMaintenanceActive()
	{
		return IsHost && _state == NetworkSessionState.LoadingScene && _loadHandshake != null;
	}

	private bool IsSeatCredentialMaintenanceActive()
	{
		if (!IsHost || _state != NetworkSessionState.Lobby)
		{
			return false;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (!player.IsHost && player.IsConnected && !player.CredentialsAcknowledged)
			{
				return true;
			}
		}
		return false;
	}

	private bool IsLocalReadyIntentMaintenanceActive()
	{
		return _state == NetworkSessionState.Lobby && _pendingLocalReadyIntent.HasValue;
	}

	private bool IsMigrationLatencyMaintenanceActive()
	{
		return HostMigrationCapabilityAvailable
			&& (_pendingMigrationLatencyProbesByPeerId.Count > 0
				|| (IsHost && _state is NetworkSessionState.Lobby or NetworkSessionState.InGame));
	}

	private bool IsHotStandbyMaintenanceActive()
	{
		return HostMigrationCapabilityAvailable
			&& IsHost
			&& _state is NetworkSessionState.Lobby or NetworkSessionState.InGame
			&& !IsActiveMigrationHandoffInProgress()
			&& _lobbyMigrationRecoveryState == null
			&& !IsMigrationReadinessFenceActive();
	}

	private static void ConsiderDeadline(ref double? current, double candidate)
	{
		if (!current.HasValue || candidate < current.Value)
		{
			current = candidate;
		}
	}
}
