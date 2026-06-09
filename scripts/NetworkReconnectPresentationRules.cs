internal static class NetworkReconnectPresentationRules
{
	internal static string GetSeatControlStatusText(SeatControlMode mode, bool usesChineseLocale)
	{
		return mode switch
		{
			SeatControlMode.AiProxyReclaimable => usesChineseLocale
				? "断线中 · AI 接管"
				: "Disconnected · AI takeover",
			SeatControlMode.HumanReclaimPending => usesChineseLocale
				? "已重连 · 下回合恢复"
				: "Reconnected · next turn",
			_ => string.Empty,
		};
	}

	internal static string GetReconnectOverlayText(bool usesChineseLocale)
	{
		return usesChineseLocale
			? "正在同步最新战局…"
			: "Syncing latest match...";
	}

	internal static string GetReconnectNoticeText(bool usesChineseLocale)
	{
		return usesChineseLocale
			? "你已重连，当前回合结束后将在你的下一个回合恢复控制。"
			: "Reconnected. Control returns on your next turn.";
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
			|| lastAppliedSnapshotId <= 0;
	}
}
