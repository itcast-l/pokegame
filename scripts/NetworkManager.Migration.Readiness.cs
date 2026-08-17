using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
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
		foreach (NetworkPlayerPublicState player in _lobbyRoster.Players)
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

			NetworkPlayerPublicState player = _lobbyRoster.Players[playerIndex];
			if (!player.IsConnected || player.IsHost || player.PeerId <= 1)
			{
				continue;
			}

			TryMarkSeatDisconnected(player.PeerId);
			_lobbyRoster.SetPlayerAt(playerIndex, player with { IsConnected = false });
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
