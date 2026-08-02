using System.Collections.Generic;

public sealed class GameMigrationRecoveryState
{
	private readonly HashSet<int> _pendingSeatIndices;
	private readonly HashSet<int> _snapshotAppliedSeatIndices = new();
	private readonly HashSet<int> _disconnectedSeatIndices = new();

	public GameMigrationRecoveryState(
		long targetEpoch,
		double startedAt,
		IEnumerable<int> expectedSeatIndices)
	{
		TargetEpoch = targetEpoch;
		StartedAt = startedAt;
		Deadline = startedAt + NetworkMigrationRules.GameRecoveryTimeoutSeconds;
		_pendingSeatIndices = new HashSet<int>(expectedSeatIndices);
		_pendingSeatIndices.RemoveWhere(seatIndex => seatIndex < 0);
		ExpectedSeatCount = _pendingSeatIndices.Count;
	}

	public long TargetEpoch { get; }
	public double StartedAt { get; }
	public double Deadline { get; }
	public int ExpectedSeatCount { get; }
	public int PendingSeatCount => _pendingSeatIndices.Count;
	public int RecoveredSeatCount => _snapshotAppliedSeatIndices.Count;
	public int DisconnectedSeatCount => _disconnectedSeatIndices.Count;
	public bool IsComplete => PendingSeatCount == 0;
	public IReadOnlySet<int> PendingSeatIndices => _pendingSeatIndices;

	public bool TryMarkSnapshotApplied(int seatIndex)
	{
		return _pendingSeatIndices.Remove(seatIndex)
			&& _snapshotAppliedSeatIndices.Add(seatIndex);
	}

	public bool TryRemoveDisconnectedSeat(int seatIndex)
	{
		return _pendingSeatIndices.Remove(seatIndex)
			&& _disconnectedSeatIndices.Add(seatIndex);
	}

	public bool HasExpired(double now)
	{
		return now >= Deadline;
	}

	public bool TryExpire(double now)
	{
		if (!HasExpired(now))
		{
			return false;
		}

		_pendingSeatIndices.Clear();
		return true;
	}
}
