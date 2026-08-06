internal enum NetworkUserErrorCategory
{
	Connection,
	Lan,
	Steam,
	RoomAuthentication,
	RoomUnavailable,
	RoomFull,
	SceneLoad,
	Reconnect,
	HostDisconnected,
	HostMigration,
	Checkpoint,
}

internal static class NetworkUserErrorRules
{
	internal static string NormalizeLocalizationKey(string localizationKey, NetworkUserErrorCategory fallback)
	{
		foreach (NetworkUserErrorCategory category in System.Enum.GetValues<NetworkUserErrorCategory>())
		{
			string knownKey = GetLocalizationKey(category);
			if (string.Equals(localizationKey, knownKey, System.StringComparison.Ordinal))
			{
				return knownKey;
			}
		}

		return GetLocalizationKey(fallback);
	}

	internal static string GetLocalizationKey(NetworkUserErrorCategory category)
	{
		return category switch
		{
			NetworkUserErrorCategory.Lan => "NETWORK_ERROR_LAN_UNAVAILABLE",
			NetworkUserErrorCategory.Steam => "NETWORK_ERROR_STEAM_UNAVAILABLE",
			NetworkUserErrorCategory.RoomAuthentication => "NETWORK_ERROR_ROOM_AUTHENTICATION_FAILED",
			NetworkUserErrorCategory.RoomUnavailable => "NETWORK_ERROR_ROOM_UNAVAILABLE",
			NetworkUserErrorCategory.RoomFull => "NETWORK_ERROR_ROOM_FULL",
			NetworkUserErrorCategory.SceneLoad => "NETWORK_ERROR_SCENE_LOAD_FAILED",
			NetworkUserErrorCategory.Reconnect => "NETWORK_ERROR_RECONNECT_FAILED",
			NetworkUserErrorCategory.HostDisconnected => "NETWORK_ERROR_HOST_DISCONNECTED",
			NetworkUserErrorCategory.HostMigration => "NETWORK_ERROR_HOST_MIGRATION_FAILED",
			NetworkUserErrorCategory.Checkpoint => "NETWORK_ERROR_CHECKPOINT_FAILED",
			_ => "NETWORK_ERROR_CONNECTION_FAILED",
		};
	}
}
