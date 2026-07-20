using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public partial class NetworkManager
{
	private const int MaxLanDiscoveryPacketsPerFrame = 32;
	private const int MaxLanDiscoveryPacketBytes = 1024;
	private const int MaxLanDiscoverySources = 256;
	private readonly Dictionary<string, (double Tokens, double UpdatedAt)> _lanDiscoveryReplyBuckets = new(StringComparer.Ordinal);
	private long _lanDiscoveryDroppedPacketCount;
	private long _lanDiscoveryRateLimitedSourceCount;
	private long _lanDiscoveryParseFailureCount;
	private int _lanDiscoveryLastResponderProcessedPacketCount;
	private int _lanDiscoveryMaximumResponderProcessedPacketCount;
	private long _lanDiscoveryResponderBudgetExhaustionCount;

	public long DebugGetLanDiscoveryDroppedPacketCount() => _lanDiscoveryDroppedPacketCount;
	public long DebugGetLanDiscoveryRateLimitedSourceCount() => _lanDiscoveryRateLimitedSourceCount;
	public long DebugGetLanDiscoveryParseFailureCount() => _lanDiscoveryParseFailureCount;
	public int DebugGetLanDiscoveryLastResponderProcessedPacketCount() => _lanDiscoveryLastResponderProcessedPacketCount;
	public int DebugGetLanDiscoveryMaximumResponderProcessedPacketCount() => _lanDiscoveryMaximumResponderProcessedPacketCount;
	public long DebugGetLanDiscoveryResponderBudgetExhaustionCount() => _lanDiscoveryResponderBudgetExhaustionCount;
	private void StartLanDiscoveryResponder()
	{
		StopLanDiscoveryResponder();
		_lanDiscoveryLastResponderProcessedPacketCount = 0;
		_lanDiscoveryMaximumResponderProcessedPacketCount = 0;
		_lanDiscoveryResponderBudgetExhaustionCount = 0;
		_lanDiscoveryResponder = new PacketPeerUdp();
		Error bindError = _lanDiscoveryResponder.Bind(NetworkSessionDefaults.LanDiscoveryPort);
		if (bindError != Error.Ok)
		{
			GD.PushWarning($"LAN discovery responder failed: {bindError}");
			_lanDiscoveryResponder.Close();
			_lanDiscoveryResponder = null;
		}
	}

	private void StopLanDiscoveryResponder()
	{
		if (_lanDiscoveryResponder == null)
		{
			return;
		}

		_lanDiscoveryResponder.Close();
		_lanDiscoveryResponder = null;
		_lanDiscoveryReplyBuckets.Clear();
	}

	private void StopLanDiscoveryBrowser()
	{
		if (_lanDiscoveryBrowser == null)
		{
			return;
		}

		_lanDiscoveryBrowser.Close();
		_lanDiscoveryBrowser = null;
	}

	private void PollLanDiscoveryResponder(double now)
	{
		_lanDiscoveryLastResponderProcessedPacketCount = 0;
		if (_lanDiscoveryResponder == null || _state != NetworkSessionState.Lobby || _config.Transport != NetworkTransport.Lan)
		{
			return;
		}

		EvictStaleLanDiscoverySources(now);
		int processedPacketCount = 0;
		while (processedPacketCount < MaxLanDiscoveryPacketsPerFrame
			&& _lanDiscoveryResponder.GetAvailablePacketCount() > 0)
		{
			byte[] packet = _lanDiscoveryResponder.GetPacket();
			processedPacketCount++;
			string senderIp = _lanDiscoveryResponder.GetPacketIP();
			int senderPort = _lanDiscoveryResponder.GetPacketPort();
			if (packet.Length > MaxLanDiscoveryPacketBytes)
			{
				_lanDiscoveryDroppedPacketCount++;
				continue;
			}
			string message = Encoding.UTF8.GetString(packet);
			if (!string.Equals(message, "deckland_discover", StringComparison.Ordinal))
			{
				_lanDiscoveryParseFailureCount++;
				continue;
			}
			if (!TryConsumeLanDiscoveryReply(senderIp, now))
			{
				_lanDiscoveryRateLimitedSourceCount++;
				continue;
			}
			string payload = LanDiscoveryMessage.Build(_config, _localDisplayName, _state.ToString());
			_lanDiscoveryResponder.SetDestAddress(senderIp, senderPort);
			_lanDiscoveryResponder.PutPacket(Encoding.UTF8.GetBytes(payload));
		}
		_lanDiscoveryLastResponderProcessedPacketCount = processedPacketCount;
		_lanDiscoveryMaximumResponderProcessedPacketCount = Math.Max(
			_lanDiscoveryMaximumResponderProcessedPacketCount,
			processedPacketCount);
		if (processedPacketCount == MaxLanDiscoveryPacketsPerFrame
			&& _lanDiscoveryResponder.GetAvailablePacketCount() > 0)
		{
			_lanDiscoveryResponderBudgetExhaustionCount++;
		}

		if (now - _lastDiscoveryBroadcastAt >= DiscoveryBroadcastSeconds)
		{
			_lastDiscoveryBroadcastAt = now;
			BroadcastLanRoomAdvertisement();
		}
	}

	private void SendLanDiscoveryPing()
	{
		if (_lanDiscoveryBrowser == null)
		{
			return;
		}

		_lanDiscoveryBrowser.SetBroadcastEnabled(true);
		byte[] payload = Encoding.UTF8.GetBytes("deckland_discover");
		foreach (string broadcastAddress in LanAddressHelper.GetBroadcastAddresses())
		{
			_lanDiscoveryBrowser.SetDestAddress(broadcastAddress, NetworkSessionDefaults.LanDiscoveryPort);
			_lanDiscoveryBrowser.PutPacket(payload);
		}
	}

	private void BroadcastLanRoomAdvertisement()
	{
		if (_lanDiscoveryResponder == null || _state != NetworkSessionState.Lobby || _config.Transport != NetworkTransport.Lan)
		{
			return;
		}

		_lanDiscoveryResponder.SetBroadcastEnabled(true);
		byte[] payload = Encoding.UTF8.GetBytes(LanDiscoveryMessage.Build(_config, _localDisplayName, _state.ToString()));
		foreach (string broadcastAddress in LanAddressHelper.GetBroadcastAddresses())
		{
			_lanDiscoveryResponder.SetDestAddress(broadcastAddress, NetworkSessionDefaults.LanDiscoveryPort);
			_lanDiscoveryResponder.PutPacket(payload);
		}
	}

	private void PollLanDiscoveryBrowser(double now)
	{
		if (_lanDiscoveryBrowser == null || _state != NetworkSessionState.Searching)
		{
			return;
		}

		if (now - _lastDiscoveryBroadcastAt >= DiscoveryBroadcastSeconds)
		{
			_lastDiscoveryBroadcastAt = now;
			SendLanDiscoveryPing();
		}

		bool changed = false;
		int processedPacketCount = 0;
		while (processedPacketCount < MaxLanDiscoveryPacketsPerFrame
			&& _lanDiscoveryBrowser.GetAvailablePacketCount() > 0)
		{
			byte[] packet = _lanDiscoveryBrowser.GetPacket();
			processedPacketCount++;
			string senderIp = _lanDiscoveryBrowser.GetPacketIP();
			if (packet.Length > MaxLanDiscoveryPacketBytes)
			{
				_lanDiscoveryDroppedPacketCount++;
				continue;
			}
			string payload = Encoding.UTF8.GetString(packet);
			if (!LanDiscoveryMessage.TryParse(payload, senderIp, out NetworkRoomInfo room))
			{
				_lanDiscoveryParseFailureCount++;
				continue;
			}

			UpsertRoom(room);
			_roomLastSeenAt[room.SessionId] = now;
			changed = true;
		}

		for (int index = _rooms.Count - 1; index >= 0; index--)
		{
			NetworkRoomInfo room = _rooms[index];
			if (room.Transport == NetworkTransport.Lan
				&& _roomLastSeenAt.TryGetValue(room.SessionId, out double lastSeenAt)
				&& now - lastSeenAt > RoomStaleSeconds)
			{
				_rooms.RemoveAt(index);
				_roomLastSeenAt.Remove(room.SessionId);
				changed = true;
			}
		}

		if (changed)
		{
			EmitRoomListUpdated();
		}
	}

	private bool TryConsumeLanDiscoveryReply(string sourceIp, double now)
	{
		if (string.IsNullOrWhiteSpace(sourceIp)
			|| sourceIp.Length > NetworkProtocol.MaxAddressLength)
		{
			return false;
		}

		if (!_lanDiscoveryReplyBuckets.TryGetValue(sourceIp, out (double Tokens, double UpdatedAt) state))
		{
			if (_lanDiscoveryReplyBuckets.Count >= MaxLanDiscoverySources)
			{
				return false;
			}
			state = (4, now);
		}

		double available = Math.Min(4, state.Tokens + Math.Max(0, now - state.UpdatedAt));
		if (available < 1)
		{
			_lanDiscoveryReplyBuckets[sourceIp] = (available, now);
			return false;
		}

		_lanDiscoveryReplyBuckets[sourceIp] = (available - 1, now);
		return true;
	}

	private void EvictStaleLanDiscoverySources(double now)
	{
		foreach (string sourceIp in _lanDiscoveryReplyBuckets
			.Where(pair => now - pair.Value.UpdatedAt > 10.0)
			.Select(pair => pair.Key)
			.ToArray())
		{
			_lanDiscoveryReplyBuckets.Remove(sourceIp);
		}
	}

	private void UpsertRoom(NetworkRoomInfo room)
	{
		int existingIndex = _rooms.FindIndex(candidate =>
			candidate.Transport == room.Transport
			&& (candidate.Transport == NetworkTransport.Steam
				? candidate.SteamLobbyId == room.SteamLobbyId
				: candidate.SessionId == room.SessionId));
		if (existingIndex >= 0)
		{
			_rooms[existingIndex] = room;
			return;
		}

		_rooms.Add(room);
	}
}
