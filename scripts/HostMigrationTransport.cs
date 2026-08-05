using System;
using Godot;

public enum HostMigrationTransportResult
{
	Ready,
	Waiting,
	Started,
	Connected,
	Unsupported,
	Failed,
}

public interface IHostMigrationTransport
{
	NetworkTransport Transport { get; }
	HostMigrationTransportResult ProbeCandidate(NetworkMigrationTransportDescriptor descriptor, out string error);
	HostMigrationTransportResult PrepareTakeover(NetworkMigrationPlan plan, double now, double deadline, out string error);
	HostMigrationTransportResult StartServer(NetworkMigrationPlan plan, out MultiplayerPeer? peer, out string error);
	HostMigrationTransportResult Connect(NetworkMigrationPlan plan, out MultiplayerPeer? peer, out string error);
	HostMigrationTransportResult UpdateMetadata(NetworkMigrationPlan plan, out string error);
	void Close(MultiplayerPeer? peer);
}

public sealed class LanHostMigrationTransport : IHostMigrationTransport
{
	public NetworkTransport Transport => NetworkTransport.Lan;

	public HostMigrationTransportResult ProbeCandidate(
		NetworkMigrationTransportDescriptor descriptor,
		out string error)
	{
		if (descriptor?.Lan is not { } lan
			|| !NetworkMigrationRules.IsLanMigrationDescriptorValid(lan))
		{
			error = "lan_migration_descriptor_invalid";
			return HostMigrationTransportResult.Failed;
		}
		if (OS.GetEnvironment("CODEX_LAN_SKIP_MIGRATION_PORT_PROBE") == "1")
		{
			error = string.Empty;
			return HostMigrationTransportResult.Ready;
		}

		var probe = new ENetMultiplayerPeer();
		Error result = probe.CreateServer(lan.Port, lan.MaxClients);
		probe.Close();
		if (result != Error.Ok)
		{
			error = result.ToString();
			return HostMigrationTransportResult.Failed;
		}

		error = string.Empty;
		return HostMigrationTransportResult.Ready;
	}

	public HostMigrationTransportResult PrepareTakeover(
		NetworkMigrationPlan plan,
		double now,
		double deadline,
		out string error)
	{
		if (plan.Transport != NetworkTransport.Lan
			|| !NetworkMigrationRules.IsLanMigrationDescriptorValid(plan.TransportDescriptor.Lan))
		{
			error = "lan_migration_plan_invalid";
			return HostMigrationTransportResult.Failed;
		}

		error = string.Empty;
		return HostMigrationTransportResult.Ready;
	}

	public HostMigrationTransportResult StartServer(
		NetworkMigrationPlan plan,
		out MultiplayerPeer? peer,
		out string error)
	{
		peer = null;
		if (plan.Transport != NetworkTransport.Lan
			|| !NetworkMigrationRules.IsLanMigrationDescriptorValid(plan.TransportDescriptor.Lan))
		{
			error = "lan_migration_plan_invalid";
			return HostMigrationTransportResult.Failed;
		}

		NetworkLanMigrationDescriptor lan = plan.TransportDescriptor.Lan!;
		var enetPeer = new ENetMultiplayerPeer();
		Error result = enetPeer.CreateServer(lan.Port, lan.MaxClients);
		if (result != Error.Ok)
		{
			error = result.ToString();
			enetPeer.Close();
			return HostMigrationTransportResult.Failed;
		}

		peer = enetPeer;
		error = string.Empty;
		return HostMigrationTransportResult.Started;
	}

	public HostMigrationTransportResult Connect(
		NetworkMigrationPlan plan,
		out MultiplayerPeer? peer,
		out string error)
	{
		peer = null;
		if (plan.Transport != NetworkTransport.Lan
			|| !NetworkMigrationRules.IsLanMigrationDescriptorValid(plan.TransportDescriptor.Lan))
		{
			error = "lan_migration_plan_invalid";
			return HostMigrationTransportResult.Failed;
		}

		NetworkLanMigrationDescriptor lan = plan.TransportDescriptor.Lan!;
		var enetPeer = new ENetMultiplayerPeer();
		Error result = enetPeer.CreateClient(lan.CandidateHostAddress, lan.Port);
		if (result != Error.Ok)
		{
			error = result.ToString();
			enetPeer.Close();
			return HostMigrationTransportResult.Failed;
		}

		peer = enetPeer;
		error = string.Empty;
		return HostMigrationTransportResult.Connected;
	}

	public HostMigrationTransportResult UpdateMetadata(NetworkMigrationPlan plan, out string error)
	{
		error = string.Empty;
		return plan.Transport == NetworkTransport.Lan
			? HostMigrationTransportResult.Ready
			: HostMigrationTransportResult.Failed;
	}

	public void Close(MultiplayerPeer? peer)
	{
		peer?.Close();
	}
}

public sealed class SteamHostMigrationTransport : IHostMigrationTransport
{
	private const double OwnerTransferRetrySeconds = 0.5;

	private readonly ISteamPlatformAdapter? _steam;
	private readonly Func<long> _getObservedLobbyOwnerSteamId;
	private readonly Func<bool> _updateMetadata;
	private double _nextOwnerTransferAttemptAt;

	public NetworkTransport Transport => NetworkTransport.Steam;

	public SteamHostMigrationTransport(
		ISteamPlatformAdapter? steam,
		Func<long> getObservedLobbyOwnerSteamId,
		Func<bool> updateMetadata)
	{
		_steam = steam;
		_getObservedLobbyOwnerSteamId = getObservedLobbyOwnerSteamId
			?? throw new ArgumentNullException(nameof(getObservedLobbyOwnerSteamId));
		_updateMetadata = updateMetadata ?? throw new ArgumentNullException(nameof(updateMetadata));
	}

	public HostMigrationTransportResult ProbeCandidate(
		NetworkMigrationTransportDescriptor descriptor,
		out string error)
	{
		if (_steam is null
			|| descriptor?.Steam is not { } steam
			|| !NetworkMigrationRules.IsSteamMigrationDescriptorValid(steam)
				|| !_steam.TryGetLocalSteamId(out long localSteamId)
			|| localSteamId != steam.CandidateSteamId
			|| !TryInstantiatePeer(out MultiplayerPeer? peer)
			|| peer is null)
		{
			error = "steam_migration_candidate_unavailable";
			return HostMigrationTransportResult.Unsupported;
		}
		bool supported = peer.HasMethod("host_with_lobby")
			&& peer.HasMethod("connect_to_lobby")
			&& peer.HasMethod("get_steam_id_for_peer_id");
		peer.Close();
		if (!supported)
		{
			error = "steam_migration_candidate_unavailable";
			return HostMigrationTransportResult.Unsupported;
		}

		error = string.Empty;
		return HostMigrationTransportResult.Ready;
	}

	public HostMigrationTransportResult PrepareTakeover(
		NetworkMigrationPlan plan,
		double now,
		double deadline,
		out string error)
	{
		if (_steam is null
			|| plan.Transport != NetworkTransport.Steam
			|| plan.TransportDescriptor.Steam is not { } steam
			|| !NetworkMigrationRules.IsSteamMigrationDescriptorValid(steam)
			|| !_steam.TryGetLocalSteamId(out long localSteamId))
		{
			error = "steam_migration_transport_unavailable";
			return HostMigrationTransportResult.Failed;
		}
		if (!TryGetLobbyOwner(steam.LobbyId, out long currentOwnerSteamId))
		{
			error = "steam_migration_owner_unavailable";
			return now >= deadline
				? HostMigrationTransportResult.Failed
				: HostMigrationTransportResult.Waiting;
		}

		SteamLobbyOwnerConvergenceAction action = NetworkMigrationRules.GetSteamLobbyOwnerConvergenceAction(
			steam,
			localSteamId,
			currentOwnerSteamId,
			now >= deadline);
		switch (action)
		{
			case SteamLobbyOwnerConvergenceAction.Ready:
				if (_getObservedLobbyOwnerSteamId() != steam.ExpectedLobbyOwnerSteamId)
				{
					error = "steam_migration_owner_update_pending";
					return now >= deadline
						? HostMigrationTransportResult.Failed
						: HostMigrationTransportResult.Waiting;
				}
				error = string.Empty;
				return HostMigrationTransportResult.Ready;
			case SteamLobbyOwnerConvergenceAction.RequestTransfer:
				if (now < _nextOwnerTransferAttemptAt)
				{
					error = "steam_migration_owner_transfer_pending";
					return HostMigrationTransportResult.Waiting;
				}
				_nextOwnerTransferAttemptAt = now + OwnerTransferRetrySeconds;
					bool transferRequested = _steam.SetLobbyOwner(
						steam.LobbyId,
						steam.ExpectedLobbyOwnerSteamId);
					error = transferRequested
					? "steam_migration_owner_transfer_requested"
					: "steam_migration_owner_transfer_rejected";
				return HostMigrationTransportResult.Waiting;
			case SteamLobbyOwnerConvergenceAction.Wait:
				error = "steam_migration_owner_transfer_pending";
				return HostMigrationTransportResult.Waiting;
			default:
				error = "steam_migration_owner_timeout";
				return HostMigrationTransportResult.Failed;
		}
	}

	public HostMigrationTransportResult StartServer(
		NetworkMigrationPlan plan,
		out MultiplayerPeer? peer,
		out string error)
	{
		peer = null;
		error = string.Empty;
		if (_steam is null
			|| !TryGetReadySteamDescriptor(plan, out NetworkSteamMigrationDescriptor descriptor, out error)
			|| !_steam.TryGetLocalSteamId(out long localSteamId)
			|| localSteamId != descriptor.CandidateSteamId)
		{
			return HostMigrationTransportResult.Failed;
		}

		return TryCreatePeer(server: true, descriptor.LobbyId, out peer, out error)
			? HostMigrationTransportResult.Started
			: HostMigrationTransportResult.Failed;
	}

	public HostMigrationTransportResult Connect(
		NetworkMigrationPlan plan,
		out MultiplayerPeer? peer,
		out string error)
	{
		peer = null;
		if (!TryGetReadySteamDescriptor(plan, out NetworkSteamMigrationDescriptor descriptor, out error))
		{
			return HostMigrationTransportResult.Failed;
		}

		return TryCreatePeer(server: false, descriptor.LobbyId, out peer, out error)
			? HostMigrationTransportResult.Connected
			: HostMigrationTransportResult.Failed;
	}

	public HostMigrationTransportResult UpdateMetadata(NetworkMigrationPlan plan, out string error)
	{
		if (!TryGetReadySteamDescriptor(plan, out _, out error) || !_updateMetadata())
		{
			error = string.IsNullOrEmpty(error) ? "steam_migration_metadata_update_failed" : error;
			return HostMigrationTransportResult.Failed;
		}

		error = string.Empty;
		return HostMigrationTransportResult.Ready;
	}

	public void Close(MultiplayerPeer? peer)
	{
		peer?.Close();
	}

	public static bool TryCreatePeer(
		bool server,
		long lobbyId,
		out MultiplayerPeer? peer,
		out string error)
	{
		peer = null;
		if (lobbyId <= 0 || !TryInstantiatePeer(out MultiplayerPeer? multiplayerPeer) || multiplayerPeer is null)
		{
			error = "steam_multiplayer_peer_unavailable";
			return false;
		}

		string method = server ? "host_with_lobby" : "connect_to_lobby";
		if (!multiplayerPeer.HasMethod(method))
		{
			error = $"steam_multiplayer_peer_{method}_unavailable";
			multiplayerPeer.Close();
			return false;
		}

		Variant result = multiplayerPeer.Call(method, unchecked((ulong)lobbyId));
		if (result.VariantType != Variant.Type.Int || (Error)result.AsInt32() != Error.Ok)
		{
			error = result.VariantType == Variant.Type.Int
				? ((Error)result.AsInt32()).ToString()
				: "steam_multiplayer_peer_result_invalid";
			multiplayerPeer.Close();
			return false;
		}

		peer = multiplayerPeer;
		error = string.Empty;
		return true;
	}

	private bool TryGetReadySteamDescriptor(
		NetworkMigrationPlan plan,
		out NetworkSteamMigrationDescriptor descriptor,
		out string error)
	{
		descriptor = plan.TransportDescriptor.Steam ?? new NetworkSteamMigrationDescriptor();
		if (_steam is null
			|| plan.Transport != NetworkTransport.Steam
			|| !NetworkMigrationRules.IsSteamMigrationDescriptorValid(descriptor)
			|| !TryGetLobbyOwner(descriptor.LobbyId, out long currentOwnerSteamId)
			|| currentOwnerSteamId != descriptor.ExpectedLobbyOwnerSteamId
			|| _getObservedLobbyOwnerSteamId() != descriptor.ExpectedLobbyOwnerSteamId)
		{
			error = "steam_migration_owner_not_converged";
			return false;
		}

		error = string.Empty;
		return true;
	}

	private bool TryGetLobbyOwner(long lobbyId, out long ownerSteamId)
	{
		ownerSteamId = 0;
		if (_steam is null || lobbyId <= 0)
		{
			return false;
		}

		return _steam.TryGetLobbyOwner(lobbyId, out ownerSteamId);
	}

	private static bool TryInstantiatePeer(out MultiplayerPeer? peer)
	{
		peer = null;
		Variant created = ClassDB.Instantiate("SteamMultiplayerPeer");
		if (created.VariantType != Variant.Type.Object
			|| created.AsGodotObject() is not MultiplayerPeer multiplayerPeer)
		{
			return false;
		}

		peer = multiplayerPeer;
		return true;
	}
}
