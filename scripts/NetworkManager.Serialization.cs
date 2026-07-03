using System;
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

	private static NetworkSessionConfig ParseConfig(Godot.Collections.Dictionary dictionary)
	{
		return new NetworkSessionConfig(
			ReadString(dictionary, "session_id"),
			RoomNameRules.Normalize(ReadString(dictionary, "room_name")),
			RoomPasswordRules.Normalize(ReadString(dictionary, "room_password")),
			ReadString(dictionary, "match_id"),
			ReadLong(dictionary, "match_seed"),
			ReadEnum(dictionary, "transport", NetworkTransport.Lan),
			NetworkSessionDefaults.ClampPlayerCount(ReadInt(dictionary, "player_count", NetworkSessionDefaults.DefaultPlayerCount)),
			ReadBool(dictionary, "item_mode", NetworkSessionDefaults.DefaultItemMode),
			NetworkSessionDefaults.NormalizeWinningScore(ReadInt(dictionary, "winning_score", NetworkSessionDefaults.DefaultWinningScore)),
			ReadEnum(dictionary, "lobby_visibility", LobbyVisibility.FriendsOnly));
	}

	private static NetworkPlayerPublicState ParsePlayer(Godot.Collections.Dictionary dictionary)
	{
		return new NetworkPlayerPublicState(
			ReadInt(dictionary, "peer_id", 0),
			ReadString(dictionary, "stable_user_id"),
			PlayerNameRules.Normalize(ReadString(dictionary, "display_name")),
			CharacterAvatarCatalog.NormalizeAvatarId(ReadString(dictionary, "avatar_id")),
			ReadInt(dictionary, "seat_index", 0),
			ReadBool(dictionary, "is_host", false),
			ReadBool(dictionary, "is_local", false),
			ReadBool(dictionary, "is_ready", false),
			ReadBool(dictionary, "is_connected", true));
	}

	private static string ReadString(Godot.Collections.Dictionary dictionary, string key)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsString() : string.Empty;
	}

	private static int ReadInt(Godot.Collections.Dictionary dictionary, string key, int fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) ? (int)value.AsInt64() : fallback;
	}

	private static long ReadLong(Godot.Collections.Dictionary dictionary, string key)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsInt64() : 0;
	}

	private static bool ReadBool(Godot.Collections.Dictionary dictionary, string key, bool fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsBool() : fallback;
	}

	private static TEnum ReadEnum<TEnum>(Godot.Collections.Dictionary dictionary, string key, TEnum fallback)
		where TEnum : struct, Enum
	{
		if (!dictionary.TryGetValue(key, out Variant value))
		{
			return fallback;
		}

		return Enum.TryParse(value.AsString(), ignoreCase: true, out TEnum parsed) ? parsed : fallback;
	}
}
