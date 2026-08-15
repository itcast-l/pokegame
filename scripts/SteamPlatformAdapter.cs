using System;
using Godot;

public interface ISteamPlatformAdapter
{
	bool IsAvailable { get; }
	bool IsInitialized { get; }
	bool ConnectSignal(StringName signalName, Callable callable);
	bool DisconnectSignal(StringName signalName, Callable callable);
	bool RequestLobbyList();
	bool InviteUserToLobby(ulong lobbyId, ulong steamId);
	bool ActivateLobbyInviteDialog(ulong lobbyId);
	bool CreateLobby(int lobbyType, int playerCount);
	bool JoinLobby(ulong lobbyId);
	bool TryGetLobbyData(ulong lobbyId, string key, out string value);
	bool TryGetLobbyOwner(long lobbyId, out long ownerSteamId);
	bool TryGetLocalSteamId(out long steamId);
	bool TryGetSteamIdForPeer(MultiplayerPeer? peer, int peerId, out long steamId);
	bool SetLobbyJoinable(ulong lobbyId, bool joinable);
	bool SetLobbyData(ulong lobbyId, string key, string value);
	bool SetLobbyOwner(long lobbyId, long ownerSteamId);
	bool LeaveLobby(ulong lobbyId);
}

public sealed class SteamPlatformAdapter : ISteamPlatformAdapter
{
	private const int MaximumLobbyDataLength = 4096;
	private readonly Node? _steamManager;

	public SteamPlatformAdapter(Node? steamManager)
	{
		_steamManager = steamManager;
	}

	public bool IsAvailable => TryReadBoolProperty("is_available", out bool value) && value;
	public bool IsInitialized => TryReadBoolProperty("is_initialized", out bool value) && value;

	public bool ConnectSignal(StringName signalName, Callable callable)
	{
		if (!IsValid || !_steamManager!.HasSignal(signalName))
		{
			return false;
		}
		if (!_steamManager.IsConnected(signalName, callable))
		{
			_steamManager.Connect(signalName, callable);
		}
		return true;
	}

	public bool DisconnectSignal(StringName signalName, Callable callable)
	{
		if (!IsValid
			|| !_steamManager!.HasSignal(signalName)
			|| !_steamManager.IsConnected(signalName, callable))
		{
			return false;
		}

		_steamManager.Disconnect(signalName, callable);
		return true;
	}

	public bool RequestLobbyList()
	{
		return TryCallBool("request_lobby_list");
	}

	public bool InviteUserToLobby(ulong lobbyId, ulong steamId)
	{
		return lobbyId > 0 && steamId > 0 && TryCallBool("invite_user_to_lobby", lobbyId, steamId);
	}

	public bool ActivateLobbyInviteDialog(ulong lobbyId)
	{
		return lobbyId > 0 && TryCallBool("activate_lobby_invite_dialog", lobbyId);
	}

	public bool CreateLobby(int lobbyType, int playerCount)
	{
		return lobbyType >= 0 && playerCount > 0 && TryCallBool("create_lobby", lobbyType, playerCount);
	}

	public bool JoinLobby(ulong lobbyId)
	{
		return lobbyId > 0 && TryCallBool("join_lobby", lobbyId);
	}

	public bool TryGetLobbyData(ulong lobbyId, string key, out string value)
	{
		value = string.Empty;
		if (!IsValid || lobbyId == 0 || string.IsNullOrEmpty(key))
		{
			return false;
		}

		Variant raw = _steamManager!.Call("get_lobby_data", lobbyId, key);
		if (raw.VariantType != Variant.Type.String)
		{
			return false;
		}
		value = raw.AsString();
		return value.Length <= MaximumLobbyDataLength;
	}

	public bool TryGetLobbyOwner(long lobbyId, out long ownerSteamId)
	{
		ownerSteamId = 0;
		if (!TryCallPositiveInt64("get_lobby_owner", out long value, lobbyId))
		{
			return false;
		}
		ownerSteamId = value;
		return true;
	}

	public bool TryGetLocalSteamId(out long steamId)
	{
		steamId = 0;
		if (!IsValid)
		{
			return false;
		}
		Variant raw = _steamManager!.Get("steam_id");
		if (raw.VariantType != Variant.Type.Int || raw.AsInt64() <= 0)
		{
			return false;
		}
		steamId = raw.AsInt64();
		return true;
	}

	public bool TryGetSteamIdForPeer(MultiplayerPeer? peer, int peerId, out long steamId)
	{
		steamId = 0;
		if (peer == null || peerId <= 0
			|| !TryCallPositiveInt64("get_steam_id_for_peer", out long value, peer, peerId))
		{
			return false;
		}
		steamId = value;
		return true;
	}

	public bool SetLobbyJoinable(ulong lobbyId, bool joinable)
	{
		return lobbyId > 0 && TryCallBool("set_lobby_joinable", lobbyId, joinable);
	}

	public bool SetLobbyData(ulong lobbyId, string key, string value)
	{
		return lobbyId > 0
			&& !string.IsNullOrEmpty(key)
			&& value.Length <= MaximumLobbyDataLength
			&& TryCallBool("set_lobby_data", lobbyId, key, value);
	}

	public bool SetLobbyOwner(long lobbyId, long ownerSteamId)
	{
		return lobbyId > 0 && ownerSteamId > 0 && TryCallBool("set_lobby_owner", lobbyId, ownerSteamId);
	}

	public bool LeaveLobby(ulong lobbyId)
	{
		return lobbyId > 0 && TryCallBool("leave_lobby", lobbyId);
	}

	private bool IsValid => _steamManager != null && GodotObject.IsInstanceValid(_steamManager);

	private bool TryReadBoolProperty(StringName propertyName, out bool value)
	{
		value = false;
		if (!IsValid)
		{
			return false;
		}
		Variant raw = _steamManager!.Get(propertyName);
		if (raw.VariantType != Variant.Type.Bool)
		{
			return false;
		}
		value = raw.AsBool();
		return true;
	}

	private bool TryCallBool(StringName method, params Variant[] arguments)
	{
		if (!IsValid)
		{
			return false;
		}
		Variant raw = _steamManager!.Call(method, arguments);
		return raw.VariantType == Variant.Type.Bool && raw.AsBool();
	}

	private bool TryCallPositiveInt64(StringName method, out long value, params Variant[] arguments)
	{
		value = 0;
		if (!IsValid)
		{
			return false;
		}
		Variant raw = _steamManager!.Call(method, arguments);
		if (raw.VariantType != Variant.Type.Int || raw.AsInt64() <= 0)
		{
			return false;
		}
		value = raw.AsInt64();
		return true;
	}
}
