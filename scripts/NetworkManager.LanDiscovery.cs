using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Text;
using Godot;

public partial class NetworkManager
{
	private const int MaxLanDiscoveryPacketsPerMaintenanceTick = 32;
	private const int MaxLanDiscoveryPacketBytes = 1024;
	private const int MaxLanDiscoverySources = 256;
	private readonly Dictionary<string, (double Tokens, double UpdatedAt)> _lanDiscoveryReplyBuckets = new(StringComparer.Ordinal);
	private readonly List<string> _staleLanDiscoverySources = new();
#if DECKLAND_TEST_HOOKS
	private long _lanDiscoveryDroppedPacketCount;
	private long _lanDiscoveryRateLimitedSourceCount;
	private long _lanDiscoveryParseFailureCount;
	private int _lanDiscoveryLastResponderProcessedPacketCount;
	private int _lanDiscoveryMaximumResponderProcessedPacketCount;
	private long _lanDiscoveryResponderBudgetExhaustionCount;
	private long _lanRoomAdvertisementBuildCount;

	public long DebugGetLanDiscoveryDroppedPacketCount() => _lanDiscoveryDroppedPacketCount;
	public long DebugGetLanDiscoveryRateLimitedSourceCount() => _lanDiscoveryRateLimitedSourceCount;
	public long DebugGetLanDiscoveryParseFailureCount() => _lanDiscoveryParseFailureCount;
	public int DebugGetLanDiscoveryLastResponderProcessedPacketCount() => _lanDiscoveryLastResponderProcessedPacketCount;
	public int DebugGetLanDiscoveryMaximumResponderProcessedPacketCount() => _lanDiscoveryMaximumResponderProcessedPacketCount;
	public long DebugGetLanDiscoveryResponderBudgetExhaustionCount() => _lanDiscoveryResponderBudgetExhaustionCount;
	public long DebugGetLanRoomAdvertisementBuildCount() => _lanRoomAdvertisementBuildCount;
#endif
	private void StartLanDiscoveryResponder()
	{
		StopLanDiscoveryResponder();
#if DECKLAND_TEST_HOOKS
		_lanDiscoveryLastResponderProcessedPacketCount = 0;
		_lanDiscoveryMaximumResponderProcessedPacketCount = 0;
		_lanDiscoveryResponderBudgetExhaustionCount = 0;
#endif
		if (!LanDiscoverySocket.TryBind(
			NetworkSessionDefaults.LanDiscoveryPort,
			MaxLanDiscoveryPacketBytes,
			out _lanDiscoveryResponder,
			out string bindError))
		{
			GD.PushWarning($"LAN discovery responder failed: {bindError}");
			_lanDiscoveryResponder?.Dispose();
			_lanDiscoveryResponder = null;
			return;
		}

		RefreshLanDiscoveryBroadcastAddresses();
		_lanRoomAdvertisementPayload = Encoding.UTF8.GetBytes(LanDiscoveryMessage.Build(
			_config,
			_localDisplayName,
			NetworkSessionState.Lobby.ToString()));
#if DECKLAND_TEST_HOOKS
		_lanRoomAdvertisementBuildCount++;
#endif
	}

	private void StopLanDiscoveryResponder()
	{
		_lanDiscoveryResponder?.Dispose();
		_lanDiscoveryResponder = null;
		_lanDiscoveryReplyBuckets.Clear();
		_lanRoomAdvertisementPayload = Array.Empty<byte>();
	}

	private void StopLanDiscoveryBrowser()
	{
		if (_lanDiscoveryBrowser == null)
		{
			return;
		}

		_lanDiscoveryBrowser.Dispose();
		_lanDiscoveryBrowser = null;
	}

	private void PollLanDiscoveryResponder(double now)
	{
#if DECKLAND_TEST_HOOKS
		_lanDiscoveryLastResponderProcessedPacketCount = 0;
#endif
		if (_lanDiscoveryResponder == null || _state != NetworkSessionState.Lobby || _config.Transport != NetworkTransport.Lan)
		{
			return;
		}

		EvictStaleLanDiscoverySources(now);
		int processedPacketCount = 0;
		while (processedPacketCount < MaxLanDiscoveryPacketsPerMaintenanceTick
			&& _lanDiscoveryResponder.TryReceive(out ReadOnlyMemory<byte> packet, out IPEndPoint sender))
		{
			processedPacketCount++;
			string senderIp = sender.Address.ToString();
			if (packet.Length > MaxLanDiscoveryPacketBytes)
			{
#if DECKLAND_TEST_HOOKS
				_lanDiscoveryDroppedPacketCount++;
#endif
				continue;
			}
			if (!packet.Span.SequenceEqual(LanDiscoveryPingPayload))
			{
#if DECKLAND_TEST_HOOKS
				_lanDiscoveryParseFailureCount++;
#endif
				continue;
			}
			if (!TryConsumeLanDiscoveryReply(senderIp, now))
			{
#if DECKLAND_TEST_HOOKS
				_lanDiscoveryRateLimitedSourceCount++;
#endif
				continue;
			}
			_lanDiscoveryResponder.TrySend(_lanRoomAdvertisementPayload, sender);
		}
#if DECKLAND_TEST_HOOKS
		_lanDiscoveryLastResponderProcessedPacketCount = processedPacketCount;
		_lanDiscoveryMaximumResponderProcessedPacketCount = Math.Max(
			_lanDiscoveryMaximumResponderProcessedPacketCount,
			processedPacketCount);
		if (processedPacketCount == MaxLanDiscoveryPacketsPerMaintenanceTick
			&& _lanDiscoveryResponder.HasAvailablePacket())
		{
			_lanDiscoveryResponderBudgetExhaustionCount++;
		}
#endif

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

		foreach (string broadcastAddress in _lanDiscoveryBroadcastAddresses)
		{
			_lanDiscoveryBrowser.TrySend(
				LanDiscoveryPingPayload,
				broadcastAddress,
				NetworkSessionDefaults.LanDiscoveryPort);
		}
	}

	private void BroadcastLanRoomAdvertisement()
	{
		if (_lanDiscoveryResponder == null || _state != NetworkSessionState.Lobby || _config.Transport != NetworkTransport.Lan)
		{
			return;
		}

		foreach (string broadcastAddress in _lanDiscoveryBroadcastAddresses)
		{
			_lanDiscoveryResponder.TrySend(
				_lanRoomAdvertisementPayload,
				broadcastAddress,
				NetworkSessionDefaults.LanDiscoveryPort);
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
		while (processedPacketCount < MaxLanDiscoveryPacketsPerMaintenanceTick
			&& _lanDiscoveryBrowser.TryReceive(out ReadOnlyMemory<byte> packet, out IPEndPoint sender))
		{
			processedPacketCount++;
			string senderIp = sender.Address.ToString();
			if (packet.Length > MaxLanDiscoveryPacketBytes)
			{
#if DECKLAND_TEST_HOOKS
				_lanDiscoveryDroppedPacketCount++;
#endif
				continue;
			}
			string payload = Encoding.UTF8.GetString(packet.Span);
			if (!LanDiscoveryMessage.TryParse(payload, senderIp, out NetworkRoomInfo room))
			{
#if DECKLAND_TEST_HOOKS
				_lanDiscoveryParseFailureCount++;
#endif
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

	private void RefreshLanDiscoveryBroadcastAddresses()
	{
		_lanDiscoveryBroadcastAddresses.Clear();
		foreach (string address in LanAddressHelper.GetBroadcastAddresses())
		{
			_lanDiscoveryBroadcastAddresses.Add(address);
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
		_staleLanDiscoverySources.Clear();
		foreach (KeyValuePair<string, (double Tokens, double UpdatedAt)> pair in _lanDiscoveryReplyBuckets)
		{
			if (now - pair.Value.UpdatedAt > 10.0)
			{
				_staleLanDiscoverySources.Add(pair.Key);
			}
		}

		foreach (string sourceIp in _staleLanDiscoverySources)
		{
			_lanDiscoveryReplyBuckets.Remove(sourceIp);
		}
		_staleLanDiscoverySources.Clear();
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
