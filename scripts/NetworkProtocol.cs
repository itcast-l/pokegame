public static class NetworkProtocol
{
	public const int CurrentVersion = 11;
	public const int AuthorityCheckpointSchemaVersion = 5;
	public const int MaxAuthorityCheckpointBytes = 256 * 1024;
	public const int MaxAuthorityCheckpointPlayers = 4;
	public const int MaxAuthorityActionQueueCount = 64;
	public const int LanHostMigrationPort = 37146;
	public const int MaxSessionIdLength = 128;
	public const int MaxMatchIdLength = 128;
	public const int MaxPlayerIdLength = 128;
	public const int MaxAddressLength = 64;
	public const int MaxRemoteErrorLength = 160;
	public const int MaxCredentialEncodedLength = 128;
}

public readonly record struct NetworkRpcContext(
	string SessionId,
	string MatchId,
	long AuthorityEpoch)
{
	public bool IsValid => NetworkProtocolRules.IsValidContext(this);
}

public static class NetworkProtocolRules
{
	public static bool IsValidContext(NetworkRpcContext context)
	{
		return !string.IsNullOrWhiteSpace(context.SessionId)
			&& context.SessionId.Length <= NetworkProtocol.MaxSessionIdLength
			&& context.MatchId is not null
			&& context.MatchId.Length <= NetworkProtocol.MaxMatchIdLength
			&& context.AuthorityEpoch >= 0;
	}

	public static bool Matches(
		NetworkRpcContext incoming,
		string sessionId,
		string matchId,
		long currentAuthorityEpoch,
		bool allowHigherEpoch)
	{
		return incoming.IsValid
			&& string.Equals(incoming.SessionId, sessionId, StringComparison.Ordinal)
			&& string.Equals(incoming.MatchId, matchId, StringComparison.Ordinal)
			&& (allowHigherEpoch
				? incoming.AuthorityEpoch >= currentAuthorityEpoch
				: incoming.AuthorityEpoch == currentAuthorityEpoch);
	}

	public static bool IsNewerFence(
		long incomingEpoch,
		int incomingSnapshotId,
		long currentEpoch,
		int currentSnapshotId)
	{
		return incomingEpoch > currentEpoch
			|| (incomingEpoch == currentEpoch && incomingSnapshotId > currentSnapshotId);
	}
}

public enum NetworkAuthorityPhase
{
	Lobby,
	Starting,
	LoadingScene,
	InGame,
	MigratingHost,
	Failed,
}

public enum NetworkMigrationFailureCode
{
	None,
	NoPlan,
	NotCandidate,
	CheckpointMissing,
	CheckpointHashMismatch,
	CheckpointInvalid,
	BindTimeout,
	ConnectTimeout,
	CandidateUnavailable,
	ExpiredCredential,
	ConcurrentCandidateExit,
	TransportUnavailable,
	SteamOwnerTimeout,
	SteamMetadataUpdateFailed,
}
