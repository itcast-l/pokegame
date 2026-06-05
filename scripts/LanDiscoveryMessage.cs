using System;
using System.Collections.Generic;
using System.Text.Json;

public static class LanDiscoveryMessage
{
	public static string Build(NetworkSessionConfig config, string hostName, string status)
	{
		var values = new Dictionary<string, string>
		{
			[SteamLobbyMetadata.ProtocolKey] = NetworkSessionDefaults.ProtocolVersion.ToString(),
			[SteamLobbyMetadata.SessionIdKey] = config.SessionId,
			[SteamLobbyMetadata.HostNameKey] = hostName,
			[SteamLobbyMetadata.PlayerCountKey] = config.PlayerCount.ToString(),
			[SteamLobbyMetadata.ItemModeKey] = config.ItemMode ? "true" : "false",
			[SteamLobbyMetadata.WinningScoreKey] = config.WinningScore.ToString(),
			[SteamLobbyMetadata.StatusKey] = status,
		};

		return JsonSerializer.Serialize(values);
	}

	public static bool TryParse(string payload, string senderAddress, out NetworkRoomInfo room)
	{
		room = default;
		if (string.IsNullOrWhiteSpace(payload))
		{
			return false;
		}

		try
		{
			Dictionary<string, string>? values = JsonSerializer.Deserialize<Dictionary<string, string>>(payload);
			if (values == null
				|| !SteamLobbyMetadata.TryReadRoom(values, 0, out NetworkRoomInfo parsedRoom))
			{
				return false;
			}

			room = parsedRoom with
			{
				Address = string.IsNullOrWhiteSpace(senderAddress) ? "127.0.0.1" : senderAddress,
				Transport = NetworkTransport.Lan,
			};
			return true;
		}
		catch (JsonException)
		{
			return false;
		}
		catch (NotSupportedException)
		{
			return false;
		}
	}
}
