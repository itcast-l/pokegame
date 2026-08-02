using System;
using System.Collections.Generic;

public enum NetworkRequestCategory
{
	LobbyMutation,
	BoardAction,
	Recovery,
	ProtocolAck,
}

public sealed class NetworkPeerRateLimiter
{
	private readonly record struct BucketConfig(double Capacity, double RefillPerSecond);
	private readonly record struct BucketState(double Tokens, double UpdatedAt);

	private static readonly IReadOnlyDictionary<NetworkRequestCategory, BucketConfig> Configs =
		new Dictionary<NetworkRequestCategory, BucketConfig>
		{
			[NetworkRequestCategory.LobbyMutation] = new(4, 1),
			[NetworkRequestCategory.BoardAction] = new(12, 6),
			[NetworkRequestCategory.Recovery] = new(4, 1),
			[NetworkRequestCategory.ProtocolAck] = new(16, 4),
		};

	private readonly Dictionary<(int PeerId, NetworkRequestCategory Category), BucketState> _buckets = new();
	private readonly Dictionary<int, Queue<double>> _violationsByPeer = new();
	private readonly Dictionary<(int PeerId, NetworkRequestCategory Category), double> _lastWarningAt = new();

	public int BucketCount => _buckets.Count;

	public bool TryConsume(
		int peerId,
		NetworkRequestCategory category,
		double now,
		out bool shouldDisconnect)
	{
		shouldDisconnect = false;
		if (peerId <= 0 || !double.IsFinite(now) || now < 0 || !Configs.TryGetValue(category, out BucketConfig config))
		{
			return false;
		}

		var key = (peerId, category);
		BucketState previous = _buckets.TryGetValue(key, out BucketState existing)
			? existing
			: new BucketState(config.Capacity, now);
		double elapsed = Math.Max(0, now - previous.UpdatedAt);
		double available = Math.Min(config.Capacity, previous.Tokens + elapsed * config.RefillPerSecond);
		if (available >= 1)
		{
			_buckets[key] = new BucketState(available - 1, now);
			return true;
		}

		_buckets[key] = new BucketState(available, now);
		Queue<double> violations = GetViolations(peerId);
		while (violations.Count > 0 && now - violations.Peek() > 10.0)
		{
			violations.Dequeue();
		}
		violations.Enqueue(now);
		shouldDisconnect = violations.Count >= 64;
		return false;
	}

	public bool ShouldLogWarning(int peerId, NetworkRequestCategory category, double now)
	{
		var key = (peerId, category);
		if (_lastWarningAt.TryGetValue(key, out double lastWarningAt)
			&& now - lastWarningAt < 5.0)
		{
			return false;
		}

		_lastWarningAt[key] = now;
		return true;
	}

	public void ClearPeer(int peerId)
	{
		foreach ((int PeerId, NetworkRequestCategory Category) key in new List<(int, NetworkRequestCategory)>(_buckets.Keys))
		{
			if (key.PeerId == peerId)
			{
				_buckets.Remove(key);
				_lastWarningAt.Remove(key);
			}
		}
		_violationsByPeer.Remove(peerId);
	}

	public void Clear()
	{
		_buckets.Clear();
		_violationsByPeer.Clear();
		_lastWarningAt.Clear();
	}

	private Queue<double> GetViolations(int peerId)
	{
		if (!_violationsByPeer.TryGetValue(peerId, out Queue<double>? violations))
		{
			violations = new Queue<double>(64);
			_violationsByPeer[peerId] = violations;
		}
		return violations;
	}
}
