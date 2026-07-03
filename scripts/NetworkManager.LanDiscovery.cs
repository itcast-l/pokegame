using System;
using System.Text;
using Godot;

public partial class NetworkManager
{
	private void StartLanDiscoveryResponder()
	{
		StopLanDiscoveryResponder();
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
		if (_lanDiscoveryResponder == null || _state != NetworkSessionState.Lobby || _config.Transport != NetworkTransport.Lan)
		{
			return;
		}

		while (_lanDiscoveryResponder.GetAvailablePacketCount() > 0)
		{
			byte[] packet = _lanDiscoveryResponder.GetPacket();
			string message = Encoding.UTF8.GetString(packet);
			if (!string.Equals(message, "deckland_discover", StringComparison.Ordinal))
			{
				continue;
			}

			string senderIp = _lanDiscoveryResponder.GetPacketIP();
			int senderPort = _lanDiscoveryResponder.GetPacketPort();
			string payload = LanDiscoveryMessage.Build(_config, _localDisplayName, _state.ToString());
			_lanDiscoveryResponder.SetDestAddress(senderIp, senderPort);
			_lanDiscoveryResponder.PutPacket(Encoding.UTF8.GetBytes(payload));
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
		while (_lanDiscoveryBrowser.GetAvailablePacketCount() > 0)
		{
			byte[] packet = _lanDiscoveryBrowser.GetPacket();
			string senderIp = _lanDiscoveryBrowser.GetPacketIP();
			string payload = Encoding.UTF8.GetString(packet);
			if (!LanDiscoveryMessage.TryParse(payload, senderIp, out NetworkRoomInfo room))
			{
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
