using System;
using System.Collections.Generic;
using Godot;

public partial class NetworkManager
{
	private static Godot.Collections.Dictionary ToDictionary(SeatControlState state)
	{
		return new Godot.Collections.Dictionary
		{
			["seat_index"] = state.SeatIndex,
			["mode"] = state.Mode.ToString(),
			["peer_id"] = state.PeerId,
			["stable_user_id"] = state.StableUserId,
			["reconnect_token"] = state.ReconnectToken,
		};
	}

	private static Godot.Collections.Dictionary ToDictionary(NetworkSessionConfig config)
	{
		return new Godot.Collections.Dictionary
		{
			["session_id"] = config.SessionId,
			["room_name"] = config.RoomName,
			["room_password"] = string.Empty,
			["match_id"] = config.MatchId,
			["match_seed"] = config.MatchSeed,
			["transport"] = config.Transport.ToString(),
			["player_count"] = config.PlayerCount,
			["item_mode"] = config.ItemMode,
			["winning_score"] = config.WinningScore,
			["lobby_visibility"] = config.LobbyVisibility.ToString(),
		};
	}

	private static Godot.Collections.Dictionary ToDictionary(NetworkPlayerPublicState player)
	{
		return new Godot.Collections.Dictionary
		{
			["peer_id"] = player.PeerId,
			["stable_user_id"] = player.StableUserId,
			["display_name"] = player.DisplayName,
			["avatar_id"] = player.AvatarId,
			["seat_index"] = player.SeatIndex,
			["is_host"] = player.IsHost,
			["is_local"] = player.IsLocal,
			["is_ready"] = player.IsReady,
			["is_connected"] = player.IsConnected,
		};
	}

	private static Godot.Collections.Dictionary ToDictionary(NetworkRoomInfo room)
	{
		return new Godot.Collections.Dictionary
		{
			["session_id"] = room.SessionId,
			["room_name"] = room.RoomName,
			["host_name"] = room.HostName,
			["address"] = room.Address,
			["transport"] = room.Transport.ToString(),
			["player_count"] = room.PlayerCount,
			["item_mode"] = room.ItemMode,
			["winning_score"] = room.WinningScore,
			["has_password"] = room.HasPassword,
			["status"] = room.Status,
			["steam_lobby_id"] = room.SteamLobbyId,
		};
	}

	private bool TryParseSharedConfig(
		Godot.Collections.Dictionary dictionary,
		out NetworkSessionConfig config)
	{
		config = default;
		if (!NetworkVariantReader.TryReadString(dictionary, "session_id", NetworkProtocol.MaxSessionIdLength, out string sessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(dictionary, "room_name", RoomNameRules.MaxLength, out string roomName, allowEmpty: false)
			|| !RoomNameRules.IsValid(roomName)
			|| !string.Equals(roomName, roomName.Trim(), StringComparison.Ordinal)
			|| !NetworkVariantReader.TryReadString(dictionary, "room_password", RoomPasswordRules.MaxLength, out string roomPassword)
			|| !string.Equals(roomPassword, roomPassword.Trim(), StringComparison.Ordinal)
			|| !NetworkVariantReader.TryReadString(dictionary, "match_id", NetworkProtocol.MaxMatchIdLength, out string matchId)
			|| !NetworkVariantReader.TryReadInt64(dictionary, "match_seed", long.MinValue, long.MaxValue, out long matchSeed)
			|| !NetworkVariantReader.TryReadEnum(dictionary, "transport", out NetworkTransport transport)
			|| !NetworkVariantReader.TryReadInt32(dictionary, "player_count", NetworkSessionDefaults.MinPlayers, NetworkSessionDefaults.MaxPlayers, out int playerCount)
			|| !NetworkVariantReader.TryReadBool(dictionary, "item_mode", out bool itemMode)
			|| !NetworkVariantReader.TryReadInt32(dictionary, "winning_score", 50, 200, out int winningScore)
			|| winningScore is not (50 or 100 or 200)
			|| !NetworkVariantReader.TryReadEnum(dictionary, "lobby_visibility", out LobbyVisibility lobbyVisibility))
		{
			return false;
		}

		if (IsHost
			&& RoomPasswordRules.HasPassword(_config.RoomPassword)
			&& string.IsNullOrEmpty(roomPassword))
		{
			roomPassword = _config.RoomPassword;
		}

		config = new NetworkSessionConfig(
			sessionId,
			roomName,
			roomPassword,
			matchId,
			matchSeed,
			transport,
			playerCount,
			itemMode,
			winningScore,
			lobbyVisibility);
		return true;
	}

	private static bool TryParsePlayer(
		Godot.Collections.Dictionary dictionary,
		out NetworkPlayerPublicState player)
	{
		player = default;
		if (!NetworkVariantReader.TryReadInt32(dictionary, "peer_id", 0, int.MaxValue, out int peerId)
			|| !NetworkVariantReader.TryReadString(dictionary, "stable_user_id", NetworkProtocol.MaxPlayerIdLength, out string stableUserId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(dictionary, "display_name", PlayerNameRules.MaxLength, out string displayName, allowEmpty: false)
			|| !PlayerNameRules.IsValid(displayName)
			|| !string.Equals(displayName, displayName.Trim(), StringComparison.Ordinal)
			|| !NetworkVariantReader.TryReadString(dictionary, "avatar_id", 128, out string avatarId, allowEmpty: false)
			|| !string.Equals(CharacterAvatarCatalog.NormalizeAvatarId(avatarId), avatarId, StringComparison.Ordinal)
			|| !NetworkVariantReader.TryReadInt32(dictionary, "seat_index", 0, NetworkSessionDefaults.MaxPlayers - 1, out int seatIndex)
			|| !NetworkVariantReader.TryReadBool(dictionary, "is_host", out bool isHost)
			|| !NetworkVariantReader.TryReadBool(dictionary, "is_local", out bool isLocal)
			|| !NetworkVariantReader.TryReadBool(dictionary, "is_ready", out bool isReady)
			|| !NetworkVariantReader.TryReadBool(dictionary, "is_connected", out bool isConnected))
		{
			return false;
		}

		player = new NetworkPlayerPublicState(
			peerId,
			stableUserId,
			displayName,
			avatarId,
			seatIndex,
			isHost,
			isLocal,
			isReady,
			isConnected);
		return true;
	}

	private bool TryDecodeLobbyState(
		Godot.Collections.Dictionary rawConfig,
		Godot.Collections.Array rawPlayers,
		int localPeerId,
		out NetworkSessionConfig config,
		out List<NetworkPlayerPublicState> players)
	{
		config = default;
		players = new List<NetworkPlayerPublicState>();
		if (!TryParseSharedConfig(rawConfig, out config)
			|| rawPlayers is null
			|| rawPlayers.Count < 1
			|| rawPlayers.Count > NetworkProtocol.MaxAuthorityCheckpointPlayers
			|| rawPlayers.Count > config.PlayerCount)
		{
			return false;
		}

		var seenSeats = new bool[config.PlayerCount];
		var playerIds = new HashSet<string>(StringComparer.Ordinal);
		var positivePeerIds = new HashSet<int>();
		int hostCount = 0;
		for (int index = 0; index < rawPlayers.Count; index++)
		{
			if (!NetworkVariantReader.TryConvertDictionary(rawPlayers[index], out Godot.Collections.Dictionary rawPlayer)
				|| !TryParsePlayer(rawPlayer, out NetworkPlayerPublicState player)
				|| player.SeatIndex >= rawPlayers.Count
				|| seenSeats[player.SeatIndex]
				|| !playerIds.Add(player.StableUserId)
				|| (player.PeerId > 0 && !positivePeerIds.Add(player.PeerId)))
			{
				players.Clear();
				return false;
			}

			seenSeats[player.SeatIndex] = true;
			hostCount += player.IsHost ? 1 : 0;
			players.Add(player with
			{
				IsLocal = player.PeerId == localPeerId || (localPeerId == 0 && player.IsHost),
			});
		}

		if (hostCount != 1)
		{
			players.Clear();
			return false;
		}

		players.Sort((left, right) => left.SeatIndex.CompareTo(right.SeatIndex));
		return true;
	}
}
