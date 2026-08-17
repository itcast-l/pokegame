using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

internal sealed class NetworkLobbyRoster
{
	private readonly List<NetworkPlayerPublicState> _players = new();
	private readonly List<SeatControlState> _seatControls = new();
	private readonly Dictionary<int, ReconnectClaimRecord> _reconnectClaimsBySeat = new();
	private readonly Dictionary<int, string> _issuedSeatCredentialsBySeat = new();
	private readonly Dictionary<int, bool> _pendingReadyByPeerId = new();
	private readonly ReadOnlyCollection<NetworkPlayerPublicState> _playersView;
	private readonly ReadOnlyCollection<SeatControlState> _seatControlsView;
	private readonly ReadOnlyDictionary<int, ReconnectClaimRecord> _reconnectClaimsView;
	private readonly ReadOnlyDictionary<int, string> _issuedSeatCredentialsView;
	private readonly ReadOnlyDictionary<int, bool> _pendingReadyView;

	internal NetworkLobbyRoster()
	{
		_playersView = _players.AsReadOnly();
		_seatControlsView = _seatControls.AsReadOnly();
		_reconnectClaimsView = new ReadOnlyDictionary<int, ReconnectClaimRecord>(_reconnectClaimsBySeat);
		_issuedSeatCredentialsView = new ReadOnlyDictionary<int, string>(_issuedSeatCredentialsBySeat);
		_pendingReadyView = new ReadOnlyDictionary<int, bool>(_pendingReadyByPeerId);
	}

	internal IReadOnlyList<NetworkPlayerPublicState> Players => _playersView;
	internal IReadOnlyList<SeatControlState> SeatControls => _seatControlsView;
	internal IReadOnlyDictionary<int, ReconnectClaimRecord> ReconnectClaimsBySeat => _reconnectClaimsView;
	internal IReadOnlyDictionary<int, string> IssuedSeatCredentialsBySeat => _issuedSeatCredentialsView;
	internal IReadOnlyDictionary<int, bool> PendingReadyByPeerId => _pendingReadyView;

	internal int FindPlayerIndex(Predicate<NetworkPlayerPublicState> match)
	{
		return _players.FindIndex(match);
	}

	internal int FindSeatControlIndex(Predicate<SeatControlState> match)
	{
		return _seatControls.FindIndex(match);
	}

	internal void ClearPlayers()
	{
		_players.Clear();
	}

	internal void ReplacePlayers(IEnumerable<NetworkPlayerPublicState> players)
	{
		NetworkPlayerPublicState[] snapshot = players.ToArray();
		_players.Clear();
		_players.AddRange(snapshot);
	}

	internal void AddPlayer(NetworkPlayerPublicState player)
	{
		_players.Add(player);
	}

	internal void InsertPlayerInSeatOrder(NetworkPlayerPublicState player)
	{
		int insertIndex = _players.FindIndex(candidate => candidate.SeatIndex > player.SeatIndex);
		if (insertIndex < 0)
		{
			_players.Add(player);
			return;
		}

		_players.Insert(insertIndex, player);
	}

	internal void SetPlayerAt(int playerIndex, NetworkPlayerPublicState player)
	{
		_players[playerIndex] = player;
	}

	internal void RemovePlayerAt(int playerIndex)
	{
		_players.RemoveAt(playerIndex);
	}

	internal void ClearSeatControls()
	{
		_seatControls.Clear();
	}

	internal void ReplaceSeatControls(IEnumerable<SeatControlState> states)
	{
		SeatControlState[] snapshot = states.ToArray();
		_seatControls.Clear();
		_seatControls.AddRange(snapshot);
	}

	internal void AddSeatControl(SeatControlState state)
	{
		_seatControls.Add(state);
	}

	internal void InsertSeatControlInSeatOrder(SeatControlState state)
	{
		int insertIndex = _seatControls.FindIndex(candidate => candidate.SeatIndex > state.SeatIndex);
		if (insertIndex < 0)
		{
			_seatControls.Add(state);
			return;
		}

		_seatControls.Insert(insertIndex, state);
	}

	internal void SetSeatControlAt(int stateIndex, SeatControlState state)
	{
		_seatControls[stateIndex] = state;
	}

	internal void RemoveSeatControlAt(int stateIndex)
	{
		_seatControls.RemoveAt(stateIndex);
	}

	internal void ClearReconnectClaims()
	{
		_reconnectClaimsBySeat.Clear();
	}

	internal void SetReconnectClaim(int seatIndex, ReconnectClaimRecord claim)
	{
		_reconnectClaimsBySeat[seatIndex] = claim;
	}

	internal bool RemoveReconnectClaim(int seatIndex)
	{
		return _reconnectClaimsBySeat.Remove(seatIndex);
	}

	internal void ClearIssuedSeatCredentials()
	{
		_issuedSeatCredentialsBySeat.Clear();
	}

	internal void SetIssuedSeatCredential(int seatIndex, string credential)
	{
		_issuedSeatCredentialsBySeat[seatIndex] = credential;
	}

	internal bool RemoveIssuedSeatCredential(int seatIndex)
	{
		return _issuedSeatCredentialsBySeat.Remove(seatIndex);
	}

	internal void SetPendingReady(int peerId, bool isReady)
	{
		_pendingReadyByPeerId[peerId] = isReady;
	}

	internal bool RemovePendingReady(int peerId)
	{
		return _pendingReadyByPeerId.Remove(peerId);
	}

	internal bool TryTakePendingReady(int peerId, out bool isReady)
	{
		return _pendingReadyByPeerId.Remove(peerId, out isReady);
	}

	internal void ClearPendingReady()
	{
		_pendingReadyByPeerId.Clear();
	}

	internal void ClearAll()
	{
		_players.Clear();
		_seatControls.Clear();
		_reconnectClaimsBySeat.Clear();
		_issuedSeatCredentialsBySeat.Clear();
		_pendingReadyByPeerId.Clear();
	}

	internal void ReplaceRosterState(
		IEnumerable<NetworkPlayerPublicState> players,
		IEnumerable<SeatControlState> seatControls,
		IReadOnlyDictionary<int, ReconnectClaimRecord> reconnectClaimsBySeat,
		IReadOnlyDictionary<int, string> issuedSeatCredentialsBySeat)
	{
		NetworkPlayerPublicState[] playerSnapshot = players.ToArray();
		SeatControlState[] seatControlSnapshot = seatControls.ToArray();
		KeyValuePair<int, ReconnectClaimRecord>[] claimSnapshot = reconnectClaimsBySeat.ToArray();
		KeyValuePair<int, string>[] credentialSnapshot = issuedSeatCredentialsBySeat.ToArray();
		_players.Clear();
		_players.AddRange(playerSnapshot);
		_seatControls.Clear();
		_seatControls.AddRange(seatControlSnapshot);
		_reconnectClaimsBySeat.Clear();
		foreach (KeyValuePair<int, ReconnectClaimRecord> pair in claimSnapshot)
		{
			_reconnectClaimsBySeat[pair.Key] = pair.Value;
		}
		_issuedSeatCredentialsBySeat.Clear();
		foreach (KeyValuePair<int, string> pair in credentialSnapshot)
		{
			_issuedSeatCredentialsBySeat[pair.Key] = pair.Value;
		}
	}

	internal void CompactSeatMappings(IReadOnlyList<NetworkPlayerPublicState> keptPlayers)
	{
		var controlsByPlayerSessionId = _seatControls.ToDictionary(
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
			ReconnectClaimRecord claim = claimsByPlayerSessionId.TryGetValue(
				player.PlayerSessionId,
				out ReconnectClaimRecord existingClaim)
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

		ReplaceRosterState(
			compactPlayers,
			compactControls,
			compactClaims,
			compactIssuedCredentials);
	}
}
