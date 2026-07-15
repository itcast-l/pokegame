using Godot;

public enum HostMigrationTransportResult
{
	Started,
	Connected,
	Unsupported,
	Failed,
}

public interface IHostMigrationTransport
{
	string ObservedAddress { get; }
	HostMigrationTransportResult StartServer(int port, int maxClients, out MultiplayerPeer? peer, out string error);
	HostMigrationTransportResult Connect(string address, int port, out MultiplayerPeer? peer, out string error);
	void Close(MultiplayerPeer? peer);
}

public sealed class LanHostMigrationTransport : IHostMigrationTransport
{
	public string ObservedAddress { get; }

	public LanHostMigrationTransport(string observedAddress)
	{
		ObservedAddress = observedAddress?.Trim() ?? string.Empty;
	}

	public HostMigrationTransportResult StartServer(
		int port,
		int maxClients,
		out MultiplayerPeer? peer,
		out string error)
	{
		peer = new ENetMultiplayerPeer();
		Error result = ((ENetMultiplayerPeer)peer).CreateServer(port, maxClients);
		if (result != Error.Ok)
		{
			error = result.ToString();
			peer = null;
			return HostMigrationTransportResult.Failed;
		}

		error = string.Empty;
		return HostMigrationTransportResult.Started;
	}

	public HostMigrationTransportResult Connect(
		string address,
		int port,
		out MultiplayerPeer? peer,
		out string error)
	{
		peer = new ENetMultiplayerPeer();
		Error result = ((ENetMultiplayerPeer)peer).CreateClient(address.Trim(), port);
		if (result != Error.Ok)
		{
			error = result.ToString();
			peer = null;
			return HostMigrationTransportResult.Failed;
		}

		error = string.Empty;
		return HostMigrationTransportResult.Connected;
	}

	public void Close(MultiplayerPeer? peer)
	{
		peer?.Close();
	}
}

public sealed class SteamHostMigrationTransport : IHostMigrationTransport
{
	public string ObservedAddress => string.Empty;

	public HostMigrationTransportResult StartServer(
		int port,
		int maxClients,
		out MultiplayerPeer? peer,
		out string error)
	{
		peer = null;
		error = "Steam host migration is deferred to P1.";
		return HostMigrationTransportResult.Unsupported;
	}

	public HostMigrationTransportResult Connect(
		string address,
		int port,
		out MultiplayerPeer? peer,
		out string error)
	{
		peer = null;
		error = "Steam host migration is deferred to P1.";
		return HostMigrationTransportResult.Unsupported;
	}

	public void Close(MultiplayerPeer? peer)
	{
		peer?.Close();
	}
}
