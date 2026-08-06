internal static class NetworkReconnectPresentationRules
{
	internal static string GetSeatControlStatusLocalizationKey(SeatControlMode mode)
	{
		return mode switch
		{
			SeatControlMode.AiProxyReclaimable => "NETWORK_STATE_AI_TAKEOVER",
			SeatControlMode.HumanReclaimPending => "NETWORK_STATE_CONTROL_PENDING",
			_ => string.Empty,
		};
	}

	internal static string GetReconnectOverlayLocalizationKey()
	{
		return "NETWORK_STATE_SYNCING_MATCH";
	}

	internal static string GetReconnectNoticeLocalizationKey()
	{
		return "NETWORK_STATE_RECONNECTED_NOTICE";
	}

	internal static string GetMigrationOverlayLocalizationKey()
	{
		return "NETWORK_STATE_MIGRATING_HOST";
	}

	internal static string GetMigrationFailureLocalizationKey()
	{
		return "NETWORK_STATE_MIGRATION_FAILED_RETURNING";
	}

	internal static bool ShouldShowOverlay(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		bool networkWaitForInitialize,
		NetworkSessionState networkState,
		int lastAppliedSnapshotId)
	{
		if (!isNetworkMultiplayer || isNetworkHost)
		{
			return false;
		}

		return networkWaitForInitialize
			|| networkState == NetworkSessionState.Reconnecting
			|| networkState == NetworkSessionState.MigratingHost
			|| lastAppliedSnapshotId <= 0;
	}
}
