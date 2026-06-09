using System;
using System.Collections.Generic;

public static class SteamLobbyMetadata
{
	public const string ProtocolKey = "deckland_protocol";
	public const string SessionIdKey = "session_id";
	public const string RoomNameKey = "room_name";
	public const string HostNameKey = "host_name";
	public const string PlayerCountKey = "player_count";
	public const string ItemModeKey = "item_mode";
	public const string WinningScoreKey = "winning_score";
	public const string HasPasswordKey = "has_password";
	public const string StatusKey = "status";

	public static Dictionary<string, string> Build(NetworkSessionConfig config, string hostName, string status)
	{
		return new Dictionary<string, string>
		{
			[ProtocolKey] = NetworkSessionDefaults.ProtocolVersion.ToString(),
			[SessionIdKey] = config.SessionId,
			[RoomNameKey] = config.RoomName,
			[HostNameKey] = hostName,
			[PlayerCountKey] = config.PlayerCount.ToString(),
			[ItemModeKey] = config.ItemMode ? "true" : "false",
			[WinningScoreKey] = config.WinningScore.ToString(),
			[HasPasswordKey] = RoomPasswordRules.HasPassword(config.RoomPassword) ? "true" : "false",
			[StatusKey] = status,
		};
	}

	public static bool ShouldSteamLobbyStayJoinable(NetworkSessionState state)
	{
		return state is NetworkSessionState.Lobby
			or NetworkSessionState.Starting
			or NetworkSessionState.LoadingScene
			or NetworkSessionState.InGame;
	}

	public static bool TryReadRoom(
		IReadOnlyDictionary<string, string> metadata,
		ulong lobbyId,
		out NetworkRoomInfo room)
	{
		room = default;
		if (!TryReadInt(metadata, ProtocolKey, 1, int.MaxValue, out int protocolVersion)
			|| protocolVersion != NetworkSessionDefaults.ProtocolVersion
			|| !TryReadInt(metadata, PlayerCountKey, NetworkSessionDefaults.MinPlayers, NetworkSessionDefaults.MaxPlayers, out int playerCount)
			|| !TryReadBool(metadata, ItemModeKey, out bool itemMode)
			|| !TryReadWinningScore(metadata, out int winningScore))
		{
			return false;
		}

		string sessionId = TryReadString(metadata, SessionIdKey, out string rawSessionId)
			? rawSessionId
			: string.Empty;
		string hostName = TryReadString(metadata, HostNameKey, out string rawHostName)
			? rawHostName
			: "Steam Host";
		string roomName = TryReadString(metadata, RoomNameKey, out string rawRoomName)
			? rawRoomName
			: hostName;
		if (!RoomNameRules.IsValid(roomName))
		{
			return false;
		}

		string status = TryReadString(metadata, StatusKey, out string rawStatus)
			? rawStatus
			: "Lobby";
		bool hasPassword = TryReadBool(metadata, HasPasswordKey, out bool parsedHasPassword) && parsedHasPassword;

		room = new NetworkRoomInfo(
			sessionId,
			RoomNameRules.Normalize(roomName),
			hostName,
			string.Empty,
			NetworkTransport.Steam,
			playerCount,
			itemMode,
			NetworkSessionDefaults.NormalizeWinningScore(winningScore),
			hasPassword,
			status,
			lobbyId);
		return true;
	}

	public static bool TryReadInt(
		IReadOnlyDictionary<string, string> metadata,
		string key,
		int minValue,
		int maxValue,
		out int value)
	{
		value = default;
		if (!metadata.TryGetValue(key, out string? rawValue)
			|| !int.TryParse(rawValue, out int parsed)
			|| parsed < minValue
			|| parsed > maxValue)
		{
			return false;
		}

		value = parsed;
		return true;
	}

	public static bool TryReadBool(IReadOnlyDictionary<string, string> metadata, string key, out bool value)
	{
		value = default;
		if (!metadata.TryGetValue(key, out string? rawValue))
		{
			return false;
		}

		if (string.Equals(rawValue, "1", StringComparison.Ordinal)
			|| string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase))
		{
			value = true;
			return true;
		}

		if (string.Equals(rawValue, "0", StringComparison.Ordinal)
			|| string.Equals(rawValue, "false", StringComparison.OrdinalIgnoreCase))
		{
			value = false;
			return true;
		}

		return false;
	}

	private static bool TryReadWinningScore(IReadOnlyDictionary<string, string> metadata, out int value)
	{
		value = default;
		if (!TryReadInt(metadata, WinningScoreKey, 50, 200, out int parsed)
			|| NetworkSessionDefaults.NormalizeWinningScore(parsed) != parsed)
		{
			return false;
		}

		value = parsed;
		return true;
	}

	public static bool TryReadEnum<TEnum>(
		IReadOnlyDictionary<string, string> metadata,
		string key,
		out TEnum value)
		where TEnum : struct, Enum
	{
		value = default;
		return metadata.TryGetValue(key, out string? rawValue)
			&& Enum.TryParse(rawValue, ignoreCase: true, out value);
	}

	public static bool TryReadString(IReadOnlyDictionary<string, string> metadata, string key, out string value)
	{
		value = string.Empty;
		if (!metadata.TryGetValue(key, out string? rawValue) || string.IsNullOrWhiteSpace(rawValue))
		{
			return false;
		}

		value = rawValue.Trim();
		return true;
	}
}
