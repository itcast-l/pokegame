using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class NetworkManager
{
	private void PollReconnect(double now)
	{
		if (_state != NetworkSessionState.Reconnecting)
		{
			return;
		}

		if (Multiplayer.MultiplayerPeer != null)
		{
			return;
		}

		if (_reconnectAttemptInFlight || now < _nextReconnectAttemptAt)
		{
			return;
		}

		TryStartReconnectAttempt(now);
	}

	private void InitializeMatchSeatControlStates()
	{
		_seatControlStates.Clear();
		_reconnectClaimsBySeat.Clear();
		ResetClientActionIds();
		foreach (NetworkPlayerPublicState player in _players)
		{
			string reconnectToken = Guid.NewGuid().ToString("N");
			SeatControlState state = SeatControlRules.CreateInitialState(player, reconnectToken);
			_seatControlStates.Add(state);
			_reconnectClaimsBySeat[player.SeatIndex] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, state);
			if (player.IsLocal)
			{
				_localReconnectToken = reconnectToken;
			}
		}

		EmitSeatControlStatesUpdated();
	}

	private void EnsureLobbySeatControlStates()
	{
		if (_state is not (NetworkSessionState.Hosting or NetworkSessionState.Lobby)
			&& !IsHost)
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (_seatControlStates.Any(state => state.SeatIndex == player.SeatIndex))
			{
				continue;
			}

			string reconnectToken = Guid.NewGuid().ToString("N");
			SeatControlState state = SeatControlRules.CreateInitialState(player, reconnectToken);
			_seatControlStates.Add(state);
			_reconnectClaimsBySeat[player.SeatIndex] = SeatControlRules.CreateInitialClaimRecord(_config.MatchId, state);
			if (player.IsLocal)
			{
				_localReconnectToken = reconnectToken;
			}
		}

		_seatControlStates.Sort((left, right) => left.SeatIndex.CompareTo(right.SeatIndex));
	}

	private void EnsureSessionMigrationTokensForRoster()
	{
		if (!IsHost)
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			EnsureSessionMigrationToken(player.SeatIndex);
		}

		SendSessionMigrationTokensToConnectedPeers();
	}

	private void EnsureSessionMigrationToken(int seatIndex)
	{
		if (seatIndex < 0 || _sessionMigrationTokensBySeat.ContainsKey(seatIndex))
		{
			return;
		}

		string token = Guid.NewGuid().ToString("N");
		_sessionMigrationTokensBySeat[seatIndex] = token;
		if (seatIndex == LocalSeatIndex || (IsHost && seatIndex == 0 && _players.Count == 0))
		{
			_localSessionMigrationToken = token;
		}
	}

	private void SendSessionMigrationTokensToConnectedPeers()
	{
		if (!IsHost || _state is not (NetworkSessionState.Lobby or NetworkSessionState.Starting or NetworkSessionState.LoadingScene or NetworkSessionState.InGame))
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (player.IsHost || !player.IsConnected || !_sessionMigrationTokensBySeat.TryGetValue(player.SeatIndex, out string? token))
			{
				continue;
			}

			RpcId(
				player.PeerId,
				MethodName.RpcReceiveSessionMigrationToken,
				_config.SessionId,
				_config.MatchId,
				GetCurrentAuthorityEpoch(),
				player.SeatIndex,
				token,
				player.StableUserId);
		}
	}

	private void ApplySessionMigrationToken(int seatIndex, string token)
	{
		if (seatIndex < 0 || string.IsNullOrWhiteSpace(token) || token.Length > 128)
		{
			return;
		}

		_sessionMigrationTokensBySeat[seatIndex] = token.Trim();
		if (seatIndex == LocalSeatIndex)
		{
			_localSessionMigrationToken = token.Trim();
		}
	}

	internal void ApplySeatControlStatesFromAuthoritySnapshot(IReadOnlyList<SeatControlState> states)
	{
		if (IsHost || states.Count == 0)
		{
			return;
		}

		var previousTokensBySeat = _seatControlStates
			.ToDictionary(state => state.SeatIndex, state => state.ReconnectToken);
		_seatControlStates.Clear();
		foreach (SeatControlState state in states.OrderBy(state => state.SeatIndex))
		{
			string reconnectToken = string.Empty;
			if (string.Equals(state.StableUserId, GetStableUserId(), StringComparison.Ordinal))
			{
				reconnectToken = _localReconnectToken;
			}
			else if (previousTokensBySeat.TryGetValue(state.SeatIndex, out string? previousToken))
			{
				reconnectToken = previousToken;
			}

			_seatControlStates.Add(state with { ReconnectToken = reconnectToken });
		}
		EmitSeatControlStatesUpdated();
	}

	private bool TryGetSeatControlState(int seatIndex, out SeatControlState state, out int stateIndex)
	{
		if (seatIndex >= 0
			&& seatIndex < _seatControlStates.Count
			&& _seatControlStates[seatIndex].SeatIndex == seatIndex)
		{
			stateIndex = seatIndex;
			state = _seatControlStates[stateIndex];
			return true;
		}

		stateIndex = _seatControlStates.FindIndex(candidate => candidate.SeatIndex == seatIndex);
		if (stateIndex < 0)
		{
			state = default;
			return false;
		}

		state = _seatControlStates[stateIndex];
		return true;
	}

	private bool TryFindSeatControlStateByToken(string reconnectToken, out SeatControlState state, out int stateIndex)
	{
		stateIndex = _seatControlStates.FindIndex(candidate => string.Equals(candidate.ReconnectToken, reconnectToken, StringComparison.Ordinal));
		if (stateIndex < 0)
		{
			state = default;
			return false;
		}

		state = _seatControlStates[stateIndex];
		return true;
	}

	private void TryMarkSeatDisconnected(int disconnectedPeerId)
	{
		int playerIndex = _players.FindIndex(player => player.PeerId == disconnectedPeerId);
		if (playerIndex < 0)
		{
			return;
		}

		int seatIndex = _players[playerIndex].SeatIndex;
		if (!TryGetSeatControlState(seatIndex, out SeatControlState current, out int stateIndex))
		{
			return;
		}

		_reconnectClaimsBySeat.TryGetValue(seatIndex, out ReconnectClaimRecord claim);
		if (!SeatControlRules.TryMarkDisconnected(current, disconnectedPeerId, out SeatControlState next, out ReconnectClaimRecord? updatedClaim, claim))
		{
			return;
		}

		_seatControlStates[stateIndex] = next;
		if (updatedClaim.HasValue)
		{
			_reconnectClaimsBySeat[seatIndex] = updatedClaim.Value;
		}

		EmitSeatControlStatesUpdated();
	}

	private bool CanAcceptRemoteBoardActionFromSeat(int seatIndex)
	{
		return TryGetSeatControlState(seatIndex, out SeatControlState state, out _)
			&& NetworkSeatControlRules.CanAcceptRemotePlayerAction(state.Mode);
	}

	private void SendReconnectCredentialsToConnectedPeers()
	{
		if (!NetworkReconnectRules.ShouldSendReconnectCredentials(IsHost, _state))
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (player.IsHost || !player.IsConnected)
			{
				continue;
			}

			if (!TryGetSeatControlState(player.SeatIndex, out SeatControlState seatControlState, out _))
			{
				continue;
			}

			RpcId(
				player.PeerId,
				MethodName.RpcReceiveReconnectCredentials,
				_config.SessionId,
				_config.MatchId,
				GetCurrentAuthorityEpoch(),
				seatControlState.SeatIndex,
				seatControlState.ReconnectToken,
				seatControlState.StableUserId);
		}
	}

	private bool CanAttemptReconnectClaim()
	{
		return !string.IsNullOrWhiteSpace(_config.MatchId)
			&& !string.IsNullOrWhiteSpace(_localReconnectToken)
			&& (!string.IsNullOrWhiteSpace(_lastLanJoinAddress) || _steamLobbyId != 0);
	}

	private void EnterReconnectState()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		_enetPeer = null;
		_steamPeer = null;
		_reconnectAttemptInFlight = false;
		_nextReconnectAttemptAt = 0.0;
		MarkLocalPlayerConnection(isConnected: false);
		SetState(NetworkSessionState.Reconnecting);
	}

	private void ScheduleReconnectRetry()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		_enetPeer = null;
		_steamPeer = null;
		_reconnectAttemptInFlight = false;
		_nextReconnectAttemptAt = Time.GetTicksMsec() / 1000.0 + ReconnectRetrySeconds;
	}

	private void TryStartReconnectAttempt(double now)
	{
		if (_config.Transport == NetworkTransport.Lan)
		{
			if (string.IsNullOrWhiteSpace(_lastLanJoinAddress))
			{
				Fail("Reconnect failed: LAN host address is missing.");
				return;
			}

			_enetPeer = new ENetMultiplayerPeer();
			Error error = _enetPeer.CreateClient(_lastLanJoinAddress, NetworkSessionDefaults.LanGamePort);
			if (error != Error.Ok)
			{
				_enetPeer = null;
				_nextReconnectAttemptAt = now + ReconnectRetrySeconds;
				return;
			}

			_reconnectAttemptInFlight = true;
			Multiplayer.MultiplayerPeer = _enetPeer;
			return;
		}

		if (_config.Transport == NetworkTransport.Steam)
		{
			if (!SteamAvailable || _steamLobbyId == 0)
			{
				Fail("Reconnect failed: Steam lobby is not available.");
				return;
			}

			_reconnectAttemptInFlight = true;
			StartSteamLobbyJoin(_steamLobbyId);
			return;
		}

		Fail("Reconnect failed: unsupported transport.");
	}

	private void BindReconnectedPeerToSeat(int seatIndex, int senderPeerId)
	{
		if (!TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
		{
			return;
		}

		_players[playerIndex] = _players[playerIndex] with
		{
			PeerId = senderPeerId,
			IsConnected = true,
		};
	}

	private void MarkLocalPlayerConnection(bool isConnected)
	{
		int localSeatIndex = LocalSeatIndex;
		if (!TryGetPlayerIndexBySeatIndex(localSeatIndex, out int playerIndex))
		{
			return;
		}

		_players[playerIndex] = _players[playerIndex] with
		{
			PeerId = isConnected ? GetLocalPeerId() : _players[playerIndex].PeerId,
			IsConnected = isConnected,
			IsLocal = true,
		};
	}

	private void UpdateLocalPlayerAfterReconnect(int seatIndex)
	{
		if (!TryGetPlayerIndexBySeatIndex(seatIndex, out int playerIndex))
		{
			return;
		}

		int peerId = GetLocalPeerId();
		_players[playerIndex] = _players[playerIndex] with
		{
			PeerId = peerId == 0 ? _players[playerIndex].PeerId : peerId,
			IsConnected = true,
			IsLocal = true,
		};
	}

	internal bool TryGetSeatControlStateForUi(int seatIndex, out SeatControlState state)
	{
		return TryGetSeatControlState(seatIndex, out state, out _);
	}

	public bool TryPromoteReclaimedSeatAtTurnStart(int seatIndex)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return false;
		}

		if (!TryGetSeatControlState(seatIndex, out SeatControlState current, out int stateIndex)
			|| !SeatControlRules.TryPromoteAtTurnStart(current, seatIndex, out SeatControlState next))
		{
			return false;
		}

		_seatControlStates[stateIndex] = next;
		EmitSeatControlStatesUpdated();
		return true;
	}

	public bool IsSeatAiControlled(int seatIndex)
	{
		if (!TryGetSeatControlState(seatIndex, out SeatControlState state, out _))
		{
			return false;
		}

		return state.Mode != SeatControlMode.HumanConnected;
	}

	public void NotifyReconnectSnapshotApplied(string matchId)
	{
		if (_state is not (NetworkSessionState.Reconnecting or NetworkSessionState.MigratingHost)
			|| !string.Equals(_config.MatchId, matchId, StringComparison.Ordinal))
		{
			return;
		}

		_reconnectAttemptInFlight = false;
		_nextReconnectAttemptAt = 0.0;
		if (_state == NetworkSessionState.MigratingHost && IsMigrationReadinessFenceActive())
		{
			// The hard snapshot is applied before the new Host has proved that every
			// expected non-source seat has reclaimed. Keep the client in the migration
			// state until the epoch's readiness broadcast arrives.
			_migrationPeerAttemptInFlight = false;
			_migrationPeerAttemptStartedAt = -1.0;
			GD.Print($"MIGRATION_SNAPSHOT_WAITING_READINESS epoch={_migrationReadyEpoch}");
			return;
		}
		if (_networkMigrationPlan != null)
		{
			_networkMigrationPlan = null;
			_migrationPeerAttemptInFlight = false;
		}
		SetState(NetworkSessionState.InGame);
	}
}
