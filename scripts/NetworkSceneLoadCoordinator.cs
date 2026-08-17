using System;
using System.Collections.Generic;

internal enum NetworkSceneLoadCoordinatorState
{
	Inactive,
	AwaitingAcks,
	Completed,
	TimedOut,
}

internal enum NetworkSceneLoadSeatAvailability
{
	Unknown,
	Offline,
	Connected,
}

internal enum NetworkSceneLoadAckResult
{
	Inactive,
	UnknownSeat,
	OfflineSeat,
	StaleMatch,
	UnexpectedSeat,
	Duplicate,
	Accepted,
	Completed,
}

internal enum NetworkSceneLoadPollResult
{
	Inactive,
	Waiting,
	TimedOut,
}

internal sealed class NetworkSceneLoadCoordinator
{
	private readonly HashSet<int> _expectedSeats = new();
	private readonly HashSet<int> _loadedSeats = new();
	private string _matchId = string.Empty;
	private double _deadlineSeconds;

	internal NetworkSceneLoadCoordinatorState State { get; private set; }
	internal string MatchId => _matchId;
	internal bool IsAwaitingAcks => State == NetworkSceneLoadCoordinatorState.AwaitingAcks;
	internal double? DeadlineSeconds => IsAwaitingAcks ? _deadlineSeconds : null;
	internal IReadOnlyCollection<int> ExpectedSeats => _expectedSeats;
	internal IReadOnlyCollection<int> LoadedSeats => _loadedSeats;

	internal void Start(
		string matchId,
		IEnumerable<int> expectedSeats,
		double timeoutSeconds,
		double startedAtSeconds)
	{
		ArgumentNullException.ThrowIfNull(expectedSeats);

		_matchId = matchId ?? string.Empty;
		_expectedSeats.Clear();
		_expectedSeats.UnionWith(expectedSeats);
		_loadedSeats.Clear();
		_deadlineSeconds = startedAtSeconds + timeoutSeconds;
		State = NetworkSceneLoadCoordinatorState.AwaitingAcks;
	}

	internal NetworkSceneLoadAckResult Acknowledge(
		string matchId,
		int seatIndex,
		NetworkSceneLoadSeatAvailability seatAvailability)
	{
		if (!IsAwaitingAcks)
		{
			return State == NetworkSceneLoadCoordinatorState.Completed
				&& string.Equals(_matchId, matchId, StringComparison.Ordinal)
				&& _loadedSeats.Contains(seatIndex)
					? NetworkSceneLoadAckResult.Duplicate
					: NetworkSceneLoadAckResult.Inactive;
		}

		if (seatAvailability == NetworkSceneLoadSeatAvailability.Unknown)
		{
			return NetworkSceneLoadAckResult.UnknownSeat;
		}
		if (seatAvailability == NetworkSceneLoadSeatAvailability.Offline)
		{
			return NetworkSceneLoadAckResult.OfflineSeat;
		}
		if (!string.Equals(_matchId, matchId, StringComparison.Ordinal))
		{
			return NetworkSceneLoadAckResult.StaleMatch;
		}
		if (!_expectedSeats.Contains(seatIndex))
		{
			return NetworkSceneLoadAckResult.UnexpectedSeat;
		}
		if (!_loadedSeats.Add(seatIndex))
		{
			return NetworkSceneLoadAckResult.Duplicate;
		}

		if (_expectedSeats.Count > 0 && _loadedSeats.Count == _expectedSeats.Count)
		{
			State = NetworkSceneLoadCoordinatorState.Completed;
			return NetworkSceneLoadAckResult.Completed;
		}

		return NetworkSceneLoadAckResult.Accepted;
	}

	internal NetworkSceneLoadPollResult Poll(double nowSeconds)
	{
		if (!IsAwaitingAcks)
		{
			return NetworkSceneLoadPollResult.Inactive;
		}
		if (nowSeconds < _deadlineSeconds)
		{
			return NetworkSceneLoadPollResult.Waiting;
		}

		State = NetworkSceneLoadCoordinatorState.TimedOut;
		return NetworkSceneLoadPollResult.TimedOut;
	}

	internal void Reset()
	{
		_matchId = string.Empty;
		_expectedSeats.Clear();
		_loadedSeats.Clear();
		_deadlineSeconds = 0.0;
		State = NetworkSceneLoadCoordinatorState.Inactive;
	}
}
