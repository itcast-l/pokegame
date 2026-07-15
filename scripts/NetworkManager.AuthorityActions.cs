using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class NetworkManager
{
	private readonly Dictionary<int, long> _lastAcceptedClientActionIds = new();
	private long _nextClientActionId;
	private byte[] _latestAuthorityCheckpoint = Array.Empty<byte>();
	private string _latestAuthorityCheckpointHash = string.Empty;
	private long _latestAuthorityCheckpointId;

	internal long NextClientActionId()
	{
		_nextClientActionId++;
		return _nextClientActionId;
	}

	internal NetworkRpcContext GetCurrentRpcContext()
	{
		return new NetworkRpcContext(
			_config.SessionId,
			_config.MatchId,
			GetCurrentAuthorityEpoch());
	}

	internal bool MatchesCurrentRpcContext(
		string sessionId,
		string matchId,
		long authorityEpoch,
		bool allowHigherEpoch = false)
	{
		NetworkRpcContext incoming = new(sessionId ?? string.Empty, matchId ?? string.Empty, authorityEpoch);
		return NetworkProtocolRules.Matches(
			incoming,
			_config.SessionId,
			_config.MatchId,
			GetCurrentAuthorityEpoch(),
			allowHigherEpoch);
	}

	internal static bool TryReadRpcContext(
		Godot.Collections.Dictionary payload,
		out NetworkRpcContext context)
	{
		context = default;
		if (payload is null
			|| !payload.TryGetValue("session_id", out Variant rawSessionId)
			|| !payload.TryGetValue("match_id", out Variant rawMatchId)
			|| !payload.TryGetValue("authority_epoch", out Variant rawAuthorityEpoch))
		{
			return false;
		}

		context = new NetworkRpcContext(
			rawSessionId.AsString(),
			rawMatchId.AsString(),
			rawAuthorityEpoch.AsInt64());
		return context.IsValid;
	}

	internal bool IsDuplicateOrOlderClientAction(int seatIndex, long clientActionId)
	{
		if (clientActionId <= 0)
		{
			return false;
		}

		return _lastAcceptedClientActionIds.TryGetValue(seatIndex, out long lastAccepted)
			&& AuthorityActionStateRules.ClassifyClientAction(clientActionId, lastAccepted)
			== ClientActionAcceptance.DuplicateOrOlder;
	}

	internal bool TryAcceptClientAction(int seatIndex, long clientActionId)
	{
		if (clientActionId == 0)
		{
			return true;
		}

		_lastAcceptedClientActionIds.TryGetValue(seatIndex, out long lastAccepted);
		if (!AuthorityActionStateRules.TryAcceptClientAction(
			clientActionId,
			lastAccepted,
			out long nextLastAccepted))
		{
			return false;
		}

		_lastAcceptedClientActionIds[seatIndex] = nextLastAccepted;
		return true;
	}

	public Godot.Collections.Dictionary GetLastAcceptedClientActionIdsVariant()
	{
		var result = new Godot.Collections.Dictionary();
		foreach (KeyValuePair<int, long> pair in _lastAcceptedClientActionIds)
		{
			result[pair.Key] = pair.Value;
		}

		return result;
	}

	internal List<NetworkMigrationRosterEntry> GetCheckpointRoster()
	{
		var result = new List<NetworkMigrationRosterEntry>(_players.Count);
		foreach (NetworkPlayerPublicState player in _players)
		{
			result.Add(new NetworkMigrationRosterEntry
			{
				StableUserId = player.StableUserId,
				DisplayName = player.DisplayName,
				AvatarId = player.AvatarId,
				SeatIndex = player.SeatIndex,
				PeerId = player.PeerId,
				IsHost = player.IsHost,
				IsReady = player.IsReady,
				IsConnected = player.IsConnected,
			});
		}

		return result;
	}

	internal List<NetworkAuthoritySeatControl> GetCheckpointSeatControls()
	{
		var result = new List<NetworkAuthoritySeatControl>(_seatControlStates.Count);
		foreach (SeatControlState state in _seatControlStates)
		{
			result.Add(new NetworkAuthoritySeatControl
			{
				SeatIndex = state.SeatIndex,
				Mode = state.Mode,
				PeerId = state.PeerId,
				StableUserId = state.StableUserId,
				ReconnectToken = state.ReconnectToken,
				SessionMigrationToken = _sessionMigrationTokensBySeat.TryGetValue(state.SeatIndex, out string? migrationToken)
					? migrationToken
					: string.Empty,
			});
		}

		return result;
	}

	internal Dictionary<int, string> GetSessionMigrationTokensForCheckpoint()
	{
		return new Dictionary<int, string>(_sessionMigrationTokensBySeat);
	}

	internal List<NetworkAuthorityReconnectClaim> GetCheckpointReconnectClaims()
	{
		var result = new List<NetworkAuthorityReconnectClaim>(_reconnectClaimsBySeat.Count);
		bool claimsFollowSeatControlOrder = _seatControlStates.Count == _reconnectClaimsBySeat.Count;
		foreach (SeatControlState state in _seatControlStates)
		{
			if (!_reconnectClaimsBySeat.TryGetValue(state.SeatIndex, out ReconnectClaimRecord claim))
			{
				claimsFollowSeatControlOrder = false;
				break;
			}

			result.Add(new NetworkAuthorityReconnectClaim
			{
				MatchId = claim.MatchId,
				SeatIndex = claim.SeatIndex,
				ReconnectToken = claim.ReconnectToken,
				StableUserId = claim.StableUserId,
				ClaimedPeerId = claim.ClaimedPeerId,
				ClaimState = claim.ClaimState.ToString(),
			});
		}

		if (claimsFollowSeatControlOrder)
		{
			return result;
		}

		// Keep a defensive path for an invalid/stale in-memory pairing. Normal
		// checkpoints follow the already ordered seat-control list above.
		result.Clear();
		foreach (KeyValuePair<int, ReconnectClaimRecord> pair in _reconnectClaimsBySeat.OrderBy(pair => pair.Key))
		{
			ReconnectClaimRecord claim = pair.Value;
			result.Add(new NetworkAuthorityReconnectClaim
			{
				MatchId = claim.MatchId,
				SeatIndex = claim.SeatIndex,
				ReconnectToken = claim.ReconnectToken,
				StableUserId = claim.StableUserId,
				ClaimedPeerId = claim.ClaimedPeerId,
				ClaimState = claim.ClaimState.ToString(),
			});
		}

		return result;
	}

	internal void ResetClientActionIds()
	{
		_lastAcceptedClientActionIds.Clear();
		_nextClientActionId = 0;
	}

	internal void RestoreLastAcceptedClientActionIds(IReadOnlyDictionary<int, long> values)
	{
		_lastAcceptedClientActionIds.Clear();
		_nextClientActionId = 0;
		foreach (KeyValuePair<int, long> pair in values)
		{
			if (pair.Key < 0 || pair.Key >= NetworkSessionDefaults.MaxPlayers || pair.Value < 0)
			{
				continue;
			}

			_lastAcceptedClientActionIds[pair.Key] = pair.Value;
			_nextClientActionId = Math.Max(_nextClientActionId, pair.Value);
		}
	}

	internal byte[] GetLatestAuthorityCheckpoint() => _latestAuthorityCheckpoint;
	internal string GetLatestAuthorityCheckpointHash() => _latestAuthorityCheckpointHash;
	internal long GetLatestAuthorityCheckpointId() => _latestAuthorityCheckpointId;

	internal void CommitAuthorityCheckpointFromBoard(GameBoardScene gameBoard)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (!gameBoard.TryExportAuthorityCheckpoint(
			_lastAcceptedClientActionIds,
			out byte[] payload,
			out string hash,
			out long checkpointId))
		{
			return;
		}

		_latestAuthorityCheckpoint = payload;
		_latestAuthorityCheckpointHash = hash;
		_latestAuthorityCheckpointId = checkpointId;
		SendCheckpointToHotStandbyIfNeeded(Time.GetTicksMsec() / 1000.0);
	}

	internal void CommitAuthorityCheckpointFromLobby()
	{
		if (!IsHost || _state != NetworkSessionState.Lobby || _players.Count == 0)
		{
			return;
		}

		EnsureLobbySeatControlStates();
		_latestAuthorityCheckpointId++;
		int playerCount = _config.PlayerCount;
		var checkpoint = new NetworkAuthorityCheckpoint
		{
			Phase = NetworkAuthorityPhase.Lobby,
			SessionId = _config.SessionId,
			MatchId = string.Empty,
			RoomName = _config.RoomName,
			RoomPassword = _config.RoomPassword,
			MatchSeed = _config.MatchSeed,
			PlayerCount = playerCount,
			ItemMode = _config.ItemMode,
			WinningScore = _config.WinningScore,
			Transport = _config.Transport,
			ManifestHash = GetLobbyGameplayManifestHash(),
			Roster = GetCheckpointRoster(),
			SeatControls = GetCheckpointSeatControls(),
			LastAcceptedClientActionIds = new Dictionary<int, long>(_lastAcceptedClientActionIds),
			Decks = Enumerable.Range(0, playerCount).Select(_ => new List<NetworkCheckpointCard>()).ToList(),
			Hands = Enumerable.Range(0, playerCount).Select(_ => new List<NetworkCheckpointCard>()).ToList(),
			ItemHands = Enumerable.Range(0, playerCount).Select(_ => new List<string>()).ToList(),
			Scores = new int[playerCount],
			CurrentTurnPlayerIndex = 0,
			WinnerPlayerIndex = -1,
			TurnSequenceGeneration = 0,
			AuthorityAction = AuthorityActionState.Empty,
			RngSeed = _config.MatchSeed,
			RngState = "1",
			SnapshotId = 0,
			AuthorityEpoch = _authorityEpoch,
			CheckpointId = _latestAuthorityCheckpointId,
			SessionMigrationTokens = GetSessionMigrationTokensForCheckpoint(),
			ReconnectClaims = GetCheckpointReconnectClaims(),
		};

		if (NetworkAuthorityCheckpointCodec.TryEncode(checkpoint, out byte[] payload, out string hash, out _))
		{
			_latestAuthorityCheckpoint = payload;
			_latestAuthorityCheckpointHash = hash;
			SendCheckpointToHotStandbyIfNeeded(Time.GetTicksMsec() / 1000.0);
		}
	}

	private string GetLobbyGameplayManifestHash()
	{
		try
		{
			return ItemCardCatalog.Load(this).GameplayManifestHash;
		}
		catch (Exception exception)
		{
			GD.PushWarning($"Unable to load lobby gameplay manifest: {exception.Message}");
			return new string('0', 64);
		}
	}
}
