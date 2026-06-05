using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SceneLoadHandshakeTracker
{
	private readonly HashSet<int> _expectedSeats;
	private readonly HashSet<int> _loadedSeats = new();

	public SceneLoadHandshakeTracker(string matchId, IEnumerable<int> expectedSeats, double timeoutSeconds, double startedAtSeconds = 0.0)
	{
		MatchId = matchId;
		_expectedSeats = expectedSeats.ToHashSet();
		TimeoutSeconds = timeoutSeconds;
		StartedAtSeconds = startedAtSeconds;
	}

	public string MatchId { get; }
	public double TimeoutSeconds { get; }
	public double StartedAtSeconds { get; }
	public IReadOnlyCollection<int> LoadedSeats => _loadedSeats;
	public IReadOnlyCollection<int> ExpectedSeats => _expectedSeats;
	public bool IsComplete => _expectedSeats.Count > 0 && _loadedSeats.Count == _expectedSeats.Count;

	public bool MarkLoaded(string matchId, int seatIndex)
	{
		if (!string.Equals(MatchId, matchId, StringComparison.Ordinal) || !_expectedSeats.Contains(seatIndex))
		{
			return false;
		}

		_loadedSeats.Add(seatIndex);
		return true;
	}

	public bool HasTimedOut(double nowSeconds)
	{
		return !IsComplete && nowSeconds - StartedAtSeconds >= TimeoutSeconds;
	}
}
