using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	public void SetLocalReady(bool isReady)
	{
		if (_state != NetworkSessionState.Lobby)
		{
			return;
		}

		_pendingLocalReadyIntent = isReady;
		TrySendLocalReadyIntent(Time.GetTicksMsec() / 1000.0);
	}

	private void PollLocalReadyIntent(double now)
	{
		if (_state != NetworkSessionState.Lobby || !_pendingLocalReadyIntent.HasValue)
		{
			return;
		}
		if (_lastLocalReadyIntentSentAt >= 0.0
			&& now - _lastLocalReadyIntentSentAt < LocalReadyIntentRetrySeconds)
		{
			return;
		}

		TrySendLocalReadyIntent(now);
	}

	private void TrySendLocalReadyIntent(double now)
	{
		if (_state != NetworkSessionState.Lobby
			|| _pendingLocalReadyIntent is not bool isReady)
		{
			return;
		}
		if (IsLocalReadyIntentSatisfied(isReady))
		{
			ClearPendingLocalReadyIntent();
			return;
		}

		if (!IsHost)
		{
			NetworkRpcContext context = GetCurrentRpcContext();
			RpcId(
				1,
				MethodName.RpcSetReady,
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				isReady);
			_lastLocalReadyIntentSentAt = now;
			return;
		}

		int localPeerId = GetLocalPeerId();
		if (!TryProcessPlayerReadyIntent(localPeerId == 0 ? 1 : localPeerId, isReady, out bool changed))
		{
			return;
		}
		if (changed)
		{
			BroadcastPlayerList();
			CommitAuthorityCheckpointFromLobby();
		}
		if (IsLocalReadyIntentSatisfied(isReady))
		{
			ClearPendingLocalReadyIntent();
		}
	}

	private bool IsLocalReadyIntentSatisfied(bool isReady)
	{
		return TryGetCachedLocalPlayer(out NetworkPlayerPublicState localPlayer)
			&& localPlayer.IsReady == isReady;
	}

	private void ClearPendingLocalReadyIntent()
	{
		_pendingLocalReadyIntent = null;
		_lastLocalReadyIntentSentAt = -1.0;
	}

	public Godot.Collections.Array GetRoomsVariant()
	{
		var result = new Godot.Collections.Array();
		foreach (NetworkRoomInfo room in _rooms)
		{
			result.Add(ToDictionary(room));
		}

		return result;
	}

	public Godot.Collections.Array GetPlayersVariant()
	{
		var result = new Godot.Collections.Array();
		foreach (NetworkPlayerPublicState player in _players)
		{
			result.Add(ToDictionary(player));
		}

		return result;
	}

	public Godot.Collections.Array GetSeatControlStatesVariant(bool includeCredentialVerifiers = true)
	{
		var result = new Godot.Collections.Array();
		foreach (SeatControlState state in _seatControlStates)
		{
			result.Add(ToDictionary(includeCredentialVerifiers ? state : SeatControlRules.RedactCredentialVerifiersForSharedSnapshot(state)));
		}

		return result;
	}

	public Godot.Collections.Dictionary GetCurrentConfigVariant()
	{
		return ToDictionary(_config);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestClientInfo()
	{
		RefreshLocalIdentity();
		ContinueClientAuthenticationAfterSecurityHandshake();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRegisterPlayer(string displayName, string avatarId, string identityHint)
	{
		if (!IsHost)
		{
			return;
		}
		if (!AllowPeerRequest(NetworkRequestCategory.LobbyMutation, requireRegisteredPeer: false))
		{
			return;
		}

		int senderId = Multiplayer.GetRemoteSenderId();
		if (!IsPeerSecurityAuthenticated(senderId))
		{
			RejectLobbyJoin(senderId, "房间认证失败");
			return;
		}
		if (!MultiplayerLobbyRules.CanAcceptJoinRequest(_state))
		{
			RejectLobbyJoin(senderId, "当前房间不可加入");
			return;
		}

		if (TryGetPlayerIndexByPeerId(senderId, out _))
		{
			return;
		}
		if (displayName is null
			|| !PlayerNameRules.IsValid(displayName)
			|| !string.Equals(displayName, displayName.Trim(), StringComparison.Ordinal)
			|| avatarId is null
			|| avatarId.Length > 128
			|| !string.Equals(CharacterAvatarCatalog.NormalizeAvatarId(avatarId), avatarId, StringComparison.Ordinal)
			|| identityHint is null
			|| identityHint.Length > NetworkProtocol.MaxPlayerIdLength)
		{
			RejectLobbyJoin(senderId, "Invalid player registration.");
			return;
		}

		int seatIndex = MultiplayerLobbyRules.GetNextSeatIndex(_players, _config.PlayerCount);
		if (seatIndex < 0)
		{
			RejectLobbyJoin(senderId, "Lobby is full.");
			return;
		}

		string playerSessionId = SeatCredentialRules.IssuePlayerSessionId();
		InsertPlayerInSeatOrder(new NetworkPlayerPublicState(
			senderId,
			playerSessionId,
			displayName,
			avatarId,
			seatIndex,
			false,
			false,
			false,
			true));
		EnsureLobbySeatControlStates();
		SendSeatCredentialsToUnacknowledgedPeers();
		BroadcastPlayerList();
		CommitAuthorityCheckpointFromLobby();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcLobbyRejected(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string message)
	{
		message = (message ?? string.Empty)[..Math.Min(message?.Length ?? 0, NetworkProtocol.MaxRemoteErrorLength)];
		NetworkRpcContext context = new(sessionId ?? string.Empty, matchId ?? string.Empty, authorityEpoch);
		if (!context.IsValid
			|| _state is not (NetworkSessionState.Joining or NetworkSessionState.MigratingHost)
			|| (_state == NetworkSessionState.MigratingHost
				&& !MatchesCurrentRpcContext(context.SessionId, context.MatchId, context.AuthorityEpoch, allowHigherEpoch: true)))
		{
			return;
		}

		if (_state == NetworkSessionState.MigratingHost)
		{
			SetAuthorityEpoch(context.AuthorityEpoch);
			FailMigration(ParseMigrationFailureCode(message));
			return;
		}

		_lobbyRejectPending = true;
		_lobbyRejectMessage = string.IsNullOrWhiteSpace(message) ? "Join rejected." : message;
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		_enetPeer = null;
		_steamPeer = null;
		CallDeferred(nameof(FinalizeLobbyRejectedJoinIfNeeded));
	}

	private static NetworkMigrationFailureCode ParseMigrationFailureCode(string message)
	{
		return Enum.TryParse(message, ignoreCase: true, out NetworkMigrationFailureCode code)
			? code
			: NetworkMigrationFailureCode.ExpiredCredential;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSetReady(
		string sessionId,
		string matchId,
		long authorityEpoch,
		bool isReady)
	{
		if (!IsHost
			|| _state != NetworkSessionState.Lobby
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch)
			|| !AllowPeerRequest(NetworkRequestCategory.LobbyMutation))
		{
			return;
		}

		if (!TryProcessPlayerReadyIntent(Multiplayer.GetRemoteSenderId(), isReady, out bool changed)
			|| !changed)
		{
			return;
		}
		BroadcastPlayerList();
		CommitAuthorityCheckpointFromLobby();
	}

	private bool TryProcessPlayerReadyIntent(int peerId, bool isReady, out bool changed)
	{
		changed = false;
		int playerIndex = _players.FindIndex(player => player.PeerId == peerId);
		if (playerIndex < 0)
		{
			return false;
		}

		LobbyReadyIntentDisposition disposition = MultiplayerLobbyRules.EvaluateReadyIntent(
			_players[playerIndex],
			isReady);
		if (disposition == LobbyReadyIntentDisposition.Reject)
		{
			return false;
		}
		if (disposition == LobbyReadyIntentDisposition.DeferUntilCredentialsAcknowledged)
		{
			_pendingLobbyReadyByPeerId[peerId] = isReady;
			return true;
		}

		_pendingLobbyReadyByPeerId.Remove(peerId);
		if (disposition == LobbyReadyIntentDisposition.NoChange)
		{
			return true;
		}

		_players[playerIndex] = _players[playerIndex] with { IsReady = isReady };
		EnsureLobbySeatControlStates();
		EmitSignal(SignalName.ReadyChanged, _players[playerIndex].SeatIndex, isReady);
		EmitPlayerListUpdated();
		changed = true;
		return true;
	}

	private bool TryApplyPendingPlayerReadyIntent(int peerId)
	{
		if (!_pendingLobbyReadyByPeerId.Remove(peerId, out bool isReady))
		{
			return false;
		}

		return TryProcessPlayerReadyIntent(peerId, isReady, out bool changed)
			&& changed;
	}

	private void BroadcastPlayerList()
	{
		if (!IsHost || _state != NetworkSessionState.Lobby)
		{
			return;
		}

		Godot.Collections.Dictionary config = ToDictionary(_config);
		Godot.Collections.Array players = GetPlayersVariant();
		NetworkRpcContext context = GetCurrentRpcContext();
		Rpc(MethodName.RpcSyncLobby, context.SessionId, context.MatchId, context.AuthorityEpoch, config, players);
		RpcSyncLobby(context.SessionId, context.MatchId, context.AuthorityEpoch, config, players);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSyncLobby(
		string sessionId,
		string matchId,
		long authorityEpoch,
		Godot.Collections.Dictionary config,
		Godot.Collections.Array players)
	{
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: true))
		{
			return;
		}

		int localPeerId = GetLocalPeerId();
		if (!TryDecodeLobbyState(config, players, localPeerId, out NetworkSessionConfig decodedConfig, out List<NetworkPlayerPublicState> decodedPlayers)
			|| !string.Equals(decodedConfig.SessionId, sessionId, StringComparison.Ordinal)
			|| !string.Equals(decodedConfig.MatchId, matchId, StringComparison.Ordinal))
		{
			return;
		}

		SetAuthorityEpoch(authorityEpoch);
		_config = decodedConfig;
		_players.Clear();
		_players.AddRange(decodedPlayers);

		if (_state == NetworkSessionState.MigratingHost)
		{
			bool isLobbyMigration = _networkMigrationPlan?.Phase == NetworkAuthorityPhase.Lobby;
			bool localSeatClaimed = _players.Any(player =>
				player.IsLocal
				&& player.IsConnected
				&& string.Equals(player.PlayerSessionId, GetPlayerSessionId(), StringComparison.Ordinal));
			if (isLobbyMigration && !localSeatClaimed)
			{
				// The new host broadcasts its roster before every follower has
				// claimed a seat. Keep the follower in migration state so a
				// rejection, such as an old-host token, is still actionable.
				EmitPlayerListUpdated();
				return;
			}

			_networkMigrationPlan = null;
			_migrationStartedAt = -1.0;
			_migrationPeerAttemptInFlight = false;
			_migrationPeerAttemptStartedAt = -1.0;
			TransitionMigrationCoordinatorState(NetworkMigrationCoordinatorState.Completed);
		}
		SetState(NetworkSessionState.Lobby);
		EmitPlayerListUpdated();
	}


}
