using System;

public enum NetworkSessionState
{
	Idle,
	Searching,
	Hosting,
	Joining,
	Lobby,
	Starting,
	LoadingScene,
	InGame,
	Reconnecting,
	Failed,
}

public enum NetworkTransport
{
	Lan,
	Steam,
}

public enum LobbyVisibility
{
	Private,
	FriendsOnly,
	Public,
}

public readonly record struct NetworkSessionConfig(
	string SessionId,
	string RoomName,
	string RoomPassword,
	string MatchId,
	long MatchSeed,
	NetworkTransport Transport,
	int PlayerCount,
	bool ItemMode,
	int WinningScore,
	LobbyVisibility LobbyVisibility);

public readonly record struct NetworkPlayerPublicState(
	int PeerId,
	string StableUserId,
	string DisplayName,
	string AvatarId,
	int SeatIndex,
	bool IsHost,
	bool IsLocal,
	bool IsReady,
	bool IsConnected);

public enum SeatControlMode
{
	HumanConnected,
	AiProxyReclaimable,
	HumanReclaimPending,
}

public readonly record struct SeatControlState(
	int SeatIndex,
	SeatControlMode Mode,
	int PeerId,
	string StableUserId,
	string ReconnectToken);

public readonly record struct NetworkRoomInfo(
	string SessionId,
	string RoomName,
	string HostName,
	string Address,
	NetworkTransport Transport,
	int PlayerCount,
	bool ItemMode,
	int WinningScore,
	bool HasPassword,
	string Status,
	ulong SteamLobbyId = 0);

public static class NetworkSessionDefaults
{
	public const int MinPlayers = 2;
	public const int MaxPlayers = 4;
	public const int DefaultPlayerCount = 2;
	public const int DefaultWinningScore = 100;
	public const bool DefaultItemMode = true;
	public const int LanDiscoveryPort = 37145;
	public const int LanGamePort = 37146;
	public const int ProtocolVersion = 3;
	public const double SceneLoadTimeoutSeconds = 30.0;

	public static int ClampPlayerCount(int playerCount)
	{
		return Math.Clamp(playerCount, MinPlayers, MaxPlayers);
	}

	public static int NormalizeWinningScore(int winningScore)
	{
		return winningScore switch
		{
			50 or 100 or 200 => winningScore,
			_ => DefaultWinningScore,
		};
	}
}
