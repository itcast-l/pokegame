using System;
using System.Collections.Generic;
using Godot;

public partial class NetworkManager
{
	private const string MigrationRequiredReconnectReason = "MigrationRequired";

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReconnectPlayer(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string playerSessionId,
		long credentialGeneration,
		string credential)
	{
		if (HostMigrationCapabilityAvailable
			&& IsHost
			&& (_state == NetworkSessionState.InGame || _state == NetworkSessionState.MigratingHost))
		{
			GD.Print($"MIGRATION_RECONNECT_REQUEST sender={Multiplayer.GetRemoteSenderId()} state={_state} epoch={authorityEpoch} player_session={playerSessionId.Length} credential_generation={credentialGeneration}");
		}

		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}
		if (!AllowPeerRequest(NetworkRequestCategory.Recovery, requireRegisteredPeer: false))
		{
			return;
		}

		if (TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint migratingBoard)
			&& migratingBoard.IsMigrationInputLocked
			&& !IsMigrationReadinessFenceActive())
		{
			GD.Print($"NETWORK_RECONNECT_REJECT reason=ActionInProgress sender={Multiplayer.GetRemoteSenderId()} lock=true");
			SendReconnectResult(
				Multiplayer.GetRemoteSenderId(),
				false,
				"ActionInProgress",
				-1);
			return;
		}

		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			GD.Print($"NETWORK_RECONNECT_REJECT reason=ContextMismatch sender={Multiplayer.GetRemoteSenderId()} epoch={authorityEpoch} current={GetCurrentAuthorityEpoch()}");
			if (HostMigrationCapabilityAvailable
				&& _networkMigrationPlan is { } migrationPlan
				&& string.Equals(sessionId, migrationPlan.SessionId, StringComparison.Ordinal)
				&& string.Equals(matchId, migrationPlan.MatchId, StringComparison.Ordinal)
				&& authorityEpoch == migrationPlan.SourceEpoch
				&& GetCurrentAuthorityEpoch() == migrationPlan.TargetEpoch)
			{
				SendReconnectResult(
					Multiplayer.GetRemoteSenderId(),
					false,
					MigrationRequiredReconnectReason,
					-1);
			}
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (string.IsNullOrWhiteSpace(playerSessionId)
			|| playerSessionId.Length > NetworkProtocol.MaxPlayerIdLength
			|| !SeatCredentialRules.IsCredentialEncodingValid(credential)
			|| credentialGeneration < SeatCredentialRules.InitialGeneration)
		{
			SendReconnectResult(senderPeerId, false, "CredentialMismatch", -1);
			return;
		}

		if (!TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint gameBoard))
		{
			GD.Print($"NETWORK_RECONNECT_REJECT reason=ActionInProgress sender={senderPeerId} detail=no_board");
			SendReconnectResult(senderPeerId, false, "ActionInProgress", -1);
			return;
		}

		if (!TryFindSeatControlStateByPlayerSessionId(playerSessionId, out SeatControlState currentSeatState, out int stateIndex))
		{
			SendReconnectResult(senderPeerId, false, "CredentialMismatch", -1);
			return;
		}

		if (currentSeatState.Mode == SeatControlMode.HumanConnected)
		{
			if (currentSeatState.PeerId == senderPeerId
				&& credentialGeneration == currentSeatState.CredentialGeneration
				&& SeatCredentialRules.Verify(credential, currentSeatState.CredentialVerifier))
			{
				SendReconnectResult(senderPeerId, true, string.Empty, currentSeatState.SeatIndex);
				if (IsMigrationReadinessFenceActive()
					|| _lastCompletedMigrationReadyEpoch == authorityEpoch)
				{
					SendBoardSnapshotsFromHost(gameBoard.CreateNextSnapshotId());
				}
				return;
			}

			GD.Print($"NETWORK_RECONNECT_REJECT reason=ActionInProgress sender={senderPeerId} seat={currentSeatState.SeatIndex} detail=seat_connected");
			SendReconnectResult(senderPeerId, false, "ActionInProgress", currentSeatState.SeatIndex);
			return;
		}

		_lobbyRoster.ReconnectClaimsBySeat.TryGetValue(currentSeatState.SeatIndex, out ReconnectClaimRecord existingClaim);
		ReconnectClaimResult result = SeatControlRules.ApplyReconnectClaim(
			_config.MatchId,
			gameBoard.IsMatchClaimable,
			currentSeatState,
			existingClaim,
			matchId,
			credentialGeneration,
			credential,
			playerSessionId,
			senderPeerId);
		if (!result.Accepted)
		{
			GD.Print($"NETWORK_RECONNECT_REJECT reason={result.RejectReason} sender={senderPeerId} seat={currentSeatState.SeatIndex}");
			SendReconnectResult(senderPeerId, false, result.RejectReason.ToString(), currentSeatState.SeatIndex);
			return;
		}

		SeatControlState acceptedSeatControlState = result.SeatControlState;
		string nextCredential = string.Empty;
		if (result.StateChanged
			&& !SeatCredentialRules.TryBeginRotation(
				acceptedSeatControlState,
				senderPeerId,
				out acceptedSeatControlState,
				out nextCredential))
		{
			SendReconnectResult(senderPeerId, false, "CredentialRotationFailed", currentSeatState.SeatIndex);
			return;
		}
		if (IsMigrationReadinessFenceActive()
			&& acceptedSeatControlState.Mode == SeatControlMode.HumanReclaimPending)
		{
			// Migration input remains locked until every expected claimant has a hard
			// snapshot, so the normal turn-start promotion can happen atomically at
			// the readiness fence instead of leaving recovery waiting for a turn.
			acceptedSeatControlState = acceptedSeatControlState with
			{
				Mode = SeatControlMode.HumanConnected,
			};
		}
		_lobbyRoster.SetSeatControlAt(stateIndex, acceptedSeatControlState);
		_lobbyRoster.SetReconnectClaim(currentSeatState.SeatIndex, result.ClaimRecord);
		if (!string.IsNullOrEmpty(nextCredential))
		{
			_lobbyRoster.SetIssuedSeatCredential(currentSeatState.SeatIndex, nextCredential);
		}
		if (currentSeatState.PeerId != senderPeerId)
		{
			BindReconnectedPeerToSeat(currentSeatState.SeatIndex, senderPeerId);
		}

		SendReconnectResult(senderPeerId, true, string.Empty, currentSeatState.SeatIndex);
		GD.Print($"NETWORK_RECONNECT_ACCEPT sender={senderPeerId} seat={currentSeatState.SeatIndex} player_session={playerSessionId.Length}");
		if (result.StateChanged)
		{
			SendSeatCredentialToPeer(senderPeerId, acceptedSeatControlState, nextCredential);
		}

		if (result.ShouldSendSnapshot)
		{
			SendBoardSnapshotsFromHost(gameBoard.CreateNextSnapshotId());
		}
	}

	private void SendSeatCredentialToPeer(
		int peerId,
		SeatControlState state,
		string credential)
	{
		long generation = state.PendingCredentialGeneration > 0
			? state.PendingCredentialGeneration
			: state.CredentialGeneration;
		RpcId(
			peerId,
			MethodName.RpcReceiveSeatCredential,
			_config.SessionId,
			_config.MatchId,
			GetCurrentAuthorityEpoch(),
			state.PlayerSessionId,
			generation,
			credential);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReceiveSeatCredential(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string playerSessionId,
		long credentialGeneration,
		string credential)
	{
		bool isInitialCredential = CanAcceptInitialSeatCredential(playerSessionId);
		if ((!isInitialCredential && !MatchesCurrentRpcContext(
				sessionId,
				matchId,
				authorityEpoch,
				allowHigherEpoch: HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost))
			|| !NetworkProtocolRules.IsValidContext(new NetworkRpcContext(sessionId, matchId, authorityEpoch))
			|| string.IsNullOrWhiteSpace(playerSessionId)
			|| playerSessionId.Length > NetworkProtocol.MaxPlayerIdLength
			|| credentialGeneration < SeatCredentialRules.InitialGeneration
			|| !SeatCredentialRules.IsCredentialEncodingValid(credential)
			|| (!isInitialCredential && !string.Equals(_localPlayerSessionId, playerSessionId, StringComparison.Ordinal)))
		{
			return;
		}

		if (isInitialCredential)
		{
			_localPlayerSessionId = playerSessionId;
			_config = _config with { SessionId = sessionId, MatchId = matchId };
		}

		_pendingLocalSeatCredential = credential;
		_pendingLocalSeatCredentialGeneration = credentialGeneration;
		RpcId(
			1,
			MethodName.RpcSeatCredentialAck,
			sessionId,
			matchId,
			authorityEpoch,
			playerSessionId,
			credentialGeneration,
			credential);
	}

	private bool CanAcceptInitialSeatCredential(string playerSessionId)
	{
		if (!string.IsNullOrEmpty(_localPlayerSessionId)
			|| _state is not (NetworkSessionState.Joining or NetworkSessionState.Lobby))
		{
			return false;
		}
		if (_state == NetworkSessionState.Joining)
		{
			return true;
		}

		return TryGetCachedLocalPlayer(out NetworkPlayerPublicState localPlayer)
			&& localPlayer.IsConnected
			&& string.Equals(localPlayer.PlayerSessionId, playerSessionId, StringComparison.Ordinal);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSeatCredentialAck(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string playerSessionId,
		long credentialGeneration,
		string credential)
	{
		if (!IsHost
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch)
			|| !AllowPeerRequest(NetworkRequestCategory.Recovery)
			|| !TryFindSeatControlStateByPlayerSessionId(playerSessionId, out SeatControlState current, out int stateIndex))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (current.PeerId != senderPeerId
			|| !SeatCredentialRules.TryAcknowledge(
				current,
				senderPeerId,
				credentialGeneration,
				credential,
				out SeatControlState committed))
		{
			return;
		}

		_lobbyRoster.SetSeatControlAt(stateIndex, committed);
		if (_lobbyRoster.ReconnectClaimsBySeat.TryGetValue(committed.SeatIndex, out ReconnectClaimRecord claim))
		{
			_lobbyRoster.SetReconnectClaim(
				committed.SeatIndex,
				claim with
				{
					CredentialGeneration = committed.CredentialGeneration,
				});
		}
		_lobbyRoster.RemoveIssuedSeatCredential(committed.SeatIndex);
		if (TryGetPlayerIndexBySeatIndex(committed.SeatIndex, out int playerIndex))
		{
			_lobbyRoster.SetPlayerAt(
				playerIndex,
				_lobbyRoster.Players[playerIndex] with { CredentialsAcknowledged = true });
		}

		RpcId(
			senderPeerId,
			MethodName.RpcSeatCredentialCommitted,
			sessionId,
			matchId,
			authorityEpoch,
			playerSessionId,
			credentialGeneration);
		TryApplyPendingPlayerReadyIntent(senderPeerId);
		if (_state == NetworkSessionState.Lobby)
		{
			_lobbyMigrationRecoveryState?.TryMarkClaimed(playerSessionId);
			BroadcastPlayerList();
			CommitAuthorityCheckpointFromLobby();
			TryFinalizeLobbyMigrationRecovery(Time.GetTicksMsec() / 1000.0);
		}
		else if (_state == NetworkSessionState.InGame
			&& TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint board))
		{
			SendBoardSnapshotsFromHost(board.CreateNextSnapshotId());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSeatCredentialCommitted(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string playerSessionId,
		long credentialGeneration)
	{
		if (!MatchesCurrentRpcContext(
				sessionId,
				matchId,
				authorityEpoch,
				allowHigherEpoch: HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost)
			|| !string.Equals(playerSessionId, _localPlayerSessionId, StringComparison.Ordinal)
			|| credentialGeneration != _pendingLocalSeatCredentialGeneration
			|| !SeatCredentialRules.IsCredentialEncodingValid(_pendingLocalSeatCredential))
		{
			return;
		}

		_localSeatCredential = _pendingLocalSeatCredential;
		_localSeatCredentialGeneration = _pendingLocalSeatCredentialGeneration;
		_pendingLocalSeatCredential = string.Empty;
		_pendingLocalSeatCredentialGeneration = 0;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReconnectResult(string sessionId, string matchId, long authorityEpoch, bool accepted, string reason, int seatIndex)
	{
		if (HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost)
		{
			GD.Print($"MIGRATION_RECONNECT_RESULT accepted={accepted} reason={reason} seat={seatIndex} epoch={authorityEpoch}");
		}

		if (HostMigrationCapabilityAvailable
			&& _state == NetworkSessionState.Reconnecting
			&& !accepted
			&& string.Equals(reason, MigrationRequiredReconnectReason, StringComparison.Ordinal)
			&& _networkMigrationPlan is { } redirectPlan
			&& NetworkMigrationRules.IsReadyForMigration(_migrationCoordinatorState)
			&& authorityEpoch == redirectPlan.TargetEpoch
			&& string.Equals(sessionId, redirectPlan.SessionId, StringComparison.Ordinal)
			&& string.Equals(matchId, redirectPlan.MatchId, StringComparison.Ordinal))
		{
			GD.Print($"MIGRATION_RECONNECT_REDIRECT target_epoch={authorityEpoch}");
			StartMigrationAttemptsAfterHostDisconnect(redirectPlan);
			return;
		}

		bool isMigrationResult = HostMigrationCapabilityAvailable
			&& _state == NetworkSessionState.MigratingHost
			&& _networkMigrationPlan is { } migrationPlan
			&& authorityEpoch == migrationPlan.TargetEpoch;
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: isMigrationResult))
		{
			return;
		}
		if (isMigrationResult)
		{
			SetAuthorityEpoch(authorityEpoch);
		}

		if (_state == NetworkSessionState.Reconnecting)
		{
			NetworkReconnectTransitionResult confirmation = _reconnectCoordinator.ReceiveConfirmation(
				new NetworkReconnectContext(sessionId, matchId, authorityEpoch),
				accepted,
				Time.GetTicksMsec() / 1000.0);
			if (confirmation == NetworkReconnectTransitionResult.Expired)
			{
				HandleHostDisconnected();
				return;
			}
			if (confirmation != NetworkReconnectTransitionResult.Accepted)
			{
				return;
			}
		}
		if (!accepted)
		{
			if ((_state == NetworkSessionState.Reconnecting
					|| (HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost))
				&& string.Equals(reason, "ActionInProgress", StringComparison.Ordinal))
			{
				if (HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost)
				{
					HandleMigrationConnectionFailure();
					return;
				}

				ScheduleReconnectRetry();
				return;
			}

			string technicalDetails = string.IsNullOrWhiteSpace(reason)
				? "Reconnect rejected."
				: $"Reconnect rejected: {reason}";
			if (HostMigrationCapabilityAvailable && _state == NetworkSessionState.MigratingHost)
			{
				ReturnToMultiplayerLobbyAfterMigrationFailure(technicalDetails);
				return;
			}

			_reconnectCoordinator.Reset();
			Fail(NetworkUserErrorCategory.Reconnect, technicalDetails);
			return;
		}

		UpdateLocalPlayerAfterReconnect(seatIndex);
	}

	private void SendReconnectResult(int peerId, bool accepted, string reason, int seatIndex)
	{
		NetworkRpcContext context = GetCurrentRpcContext();
		RpcId(peerId, MethodName.RpcReconnectResult, context.SessionId, context.MatchId, context.AuthorityEpoch, accepted, reason, seatIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcHostEndedMatch(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string localizationKey)
	{
		localizationKey = (localizationKey ?? string.Empty)[..Math.Min(localizationKey?.Length ?? 0, NetworkProtocol.MaxRemoteErrorLength)];
		if (IsHost
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		string hostEndedMatchLocalizationKey = NetworkUserErrorRules.NormalizeLocalizationKey(
			localizationKey,
			NetworkUserErrorCategory.HostDisconnected);
		_hostEndedMatchPending = true;
		_hostEndedMatchLocalizationKey = hostEndedMatchLocalizationKey;
		_reconnectCoordinator.Reset();
		if (HostMigrationCapabilityAvailable
			&& _state == NetworkSessionState.MigratingHost)
		{
			ReturnToMultiplayerLobbyAfterMigrationFailure("Remote host ended the match during migration.");
			return;
		}

		_ = RequestSessionCloseAsync(NetworkSessionCloseReason.InternalFailure);
		SetState(NetworkSessionState.Failed);
		EmitSignal(SignalName.ErrorRaised, hostEndedMatchLocalizationKey);
	}

}
