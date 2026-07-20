using System;
using System.Collections.Generic;

public sealed class LobbyMigrationRecoveryState
{
	private readonly HashSet<string> _expectedPlayerSessionIds;
	private readonly HashSet<string> _claimedPlayerSessionIds = new(StringComparer.Ordinal);

	public LobbyMigrationRecoveryState(
		long targetEpoch,
		double startedAt,
		double timeoutSeconds,
		IEnumerable<string> expectedPlayerSessionIds)
	{
		TargetEpoch = targetEpoch;
		StartedAt = startedAt;
		Deadline = startedAt + timeoutSeconds;
		_expectedPlayerSessionIds = new HashSet<string>(
			expectedPlayerSessionIds,
			StringComparer.Ordinal);
		_expectedPlayerSessionIds.RemoveWhere(string.IsNullOrWhiteSpace);
	}

	public long TargetEpoch { get; }
	public double StartedAt { get; }
	public double Deadline { get; }
	public bool IsComplete => _claimedPlayerSessionIds.Count == _expectedPlayerSessionIds.Count;
	public IReadOnlySet<string> ExpectedPlayerSessionIds => _expectedPlayerSessionIds;

	public bool TryMarkClaimed(string playerSessionId)
	{
		return _expectedPlayerSessionIds.Contains(playerSessionId)
			&& _claimedPlayerSessionIds.Add(playerSessionId);
	}

	public bool IsClaimed(string playerSessionId)
	{
		return _claimedPlayerSessionIds.Contains(playerSessionId);
	}

	public bool HasExpired(double now)
	{
		return now >= Deadline;
	}
}
