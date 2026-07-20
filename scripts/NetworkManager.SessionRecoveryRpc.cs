using System;
using System.Collections.Generic;
using Godot;

public partial class NetworkManager
{
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReconnectPlayer(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string reconnectToken,
		string stableUserId)
	{
		if (IsHost && (_state == NetworkSessionState.InGame || _state == NetworkSessionState.MigratingHost))
		{
			GD.Print($"MIGRATION_RECONNECT_REQUEST sender={Multiplayer.GetRemoteSenderId()} state={_state} epoch={authorityEpoch} stable={stableUserId} token_len={reconnectToken.Length}");
		}

		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (GetTree().CurrentScene is GameBoardScene migratingBoard
			&& migratingBoard.IsNetworkMigrationInputLocked
			&& !IsMigrationReadinessFenceActive())
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=ActionInProgress sender={Multiplayer.GetRemoteSenderId()} lock=true");
			SendReconnectResult(
				Multiplayer.GetRemoteSenderId(),
				false,
				"ActionInProgress",
				-1);
			return;
		}

		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=ContextMismatch sender={Multiplayer.GetRemoteSenderId()} epoch={authorityEpoch} current={GetCurrentAuthorityEpoch()}");
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (string.IsNullOrWhiteSpace(reconnectToken))
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=TokenMismatch sender={senderPeerId} detail=empty");
			SendReconnectResult(senderPeerId, false, "TokenMismatch", -1);
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=ActionInProgress sender={senderPeerId} detail=no_board");
			SendReconnectResult(senderPeerId, false, "ActionInProgress", -1);
			return;
		}

		if (!TryFindSeatControlStateByToken(reconnectToken, out SeatControlState currentSeatState, out int stateIndex))
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=TokenMismatch sender={senderPeerId} detail=unknown_token");
			SendReconnectResult(senderPeerId, false, "TokenMismatch", -1);
			return;
		}

		if (NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			currentSeatState.Mode,
			currentSeatState.PeerId,
			senderPeerId,
			currentSeatState.ReconnectToken,
			reconnectToken,
			currentSeatState.StableUserId,
			stableUserId))
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=ActionInProgress sender={senderPeerId} seat={currentSeatState.SeatIndex} detail=seat_connected");
			SendReconnectResult(senderPeerId, false, "ActionInProgress", currentSeatState.SeatIndex);
			return;
		}

		_reconnectClaimsBySeat.TryGetValue(currentSeatState.SeatIndex, out ReconnectClaimRecord existingClaim);
		ReconnectClaimResult result = SeatControlRules.ApplyReconnectClaim(
			_config.MatchId,
			gameBoard.IsNetworkMatchClaimable(),
			currentSeatState,
			existingClaim,
			matchId,
			reconnectToken,
			stableUserId,
			senderPeerId);
		if (!result.Accepted)
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason={result.RejectReason} sender={senderPeerId} seat={currentSeatState.SeatIndex}");
			SendReconnectResult(senderPeerId, false, result.RejectReason.ToString(), currentSeatState.SeatIndex);
			return;
		}

		SeatControlState acceptedSeatControlState = result.SeatControlState;
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
		_seatControlStates[stateIndex] = acceptedSeatControlState;
		_reconnectClaimsBySeat[currentSeatState.SeatIndex] = result.ClaimRecord;
		if (currentSeatState.PeerId != senderPeerId)
		{
			BindReconnectedPeerToSeat(currentSeatState.SeatIndex, senderPeerId);
		}

		SendReconnectResult(senderPeerId, true, string.Empty, currentSeatState.SeatIndex);
		GD.Print($"MIGRATION_RECONNECT_ACCEPT sender={senderPeerId} seat={currentSeatState.SeatIndex} stable={stableUserId}");
		if (result.StateChanged)
		{
			SendReconnectCredentialsToConnectedPeers();
		}

			if (result.ShouldSendSnapshot)
			{
				SendBoardSnapshotsFromHost(gameBoard, gameBoard.CreateNextNetworkSnapshotId());
				TryCompleteMigrationReadiness();
			}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReceiveReconnectCredentials(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int seatIndex,
		string reconnectToken,
		string stableUserId)
	{
		bool isInitialMatchCredential = string.IsNullOrWhiteSpace(_config.MatchId)
			&& !string.IsNullOrWhiteSpace(matchId)
			&& string.Equals(_config.SessionId, sessionId, StringComparison.Ordinal)
			&& _state is NetworkSessionState.Lobby or NetworkSessionState.Starting or NetworkSessionState.LoadingScene;
		if ((!isInitialMatchCredential
				&& !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
			|| !string.Equals(GetStableUserId(), stableUserId, StringComparison.Ordinal))
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(matchId))
		{
			_config = _config with { MatchId = matchId };
		}

		_localReconnectToken = reconnectToken?.Trim() ?? string.Empty;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReceiveSessionMigrationToken(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int seatIndex,
		string sessionMigrationToken,
		string stableUserId)
	{
		bool isInitialLobbyCredential = _state == NetworkSessionState.Joining
			&& string.IsNullOrWhiteSpace(_config.MatchId)
			&& string.IsNullOrWhiteSpace(matchId)
			&& !string.IsNullOrWhiteSpace(sessionId);
		if ((!isInitialLobbyCredential
				&& !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
			|| !string.Equals(GetStableUserId(), stableUserId, StringComparison.Ordinal)
			|| seatIndex < 0
			|| seatIndex >= NetworkSessionDefaults.MaxPlayers)
		{
			return;
		}

		if (isInitialLobbyCredential)
		{
			_config = _config with { SessionId = sessionId };
		}
		ApplySessionMigrationToken(seatIndex, sessionMigrationToken);
		// Lobby credentials can arrive before RpcSyncLobby establishes the local
		// seat. Stable identity keeps the local token usable during early
		// active-lobby migration.
		if (string.Equals(GetStableUserId(), stableUserId, StringComparison.Ordinal))
		{
			_localSessionMigrationToken = sessionMigrationToken.Trim();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void RpcReconnectResult(string sessionId, string matchId, long authorityEpoch, bool accepted, string reason, int seatIndex)
		{
		if (_state == NetworkSessionState.MigratingHost)
		{
			GD.Print($"MIGRATION_RECONNECT_RESULT accepted={accepted} reason={reason} seat={seatIndex} epoch={authorityEpoch}");
		}

			bool isMigrationResult = _state == NetworkSessionState.MigratingHost
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

		_reconnectAttemptInFlight = false;
		if (!accepted)
		{
			if ((_state is NetworkSessionState.Reconnecting or NetworkSessionState.MigratingHost)
				&& string.Equals(reason, "ActionInProgress", StringComparison.Ordinal))
			{
				if (_state == NetworkSessionState.MigratingHost)
				{
					HandleMigrationConnectionFailure();
					return;
				}

				ScheduleReconnectRetry();
				return;
			}

			Fail(string.IsNullOrWhiteSpace(reason) ? "Reconnect rejected." : $"Reconnect rejected: {reason}");
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
		string message)
	{
		if (IsHost
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		_hostEndedMatchPending = true;
		_hostEndedMatchMessage = string.IsNullOrWhiteSpace(message) ? "Host disconnected." : message;
		_reconnectAttemptInFlight = false;
		_nextReconnectAttemptAt = 0.0;
		Fail(_hostEndedMatchMessage);
	}

}
