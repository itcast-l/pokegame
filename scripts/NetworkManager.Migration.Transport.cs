using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	private string GetObservedLanAddress()
	{
		string configuredAddress = OS.GetEnvironment("CODEX_LAN_MIGRATION_ADDRESS").Trim();
		if (!string.IsNullOrWhiteSpace(configuredAddress))
		{
			return configuredAddress;
		}

		return LanAddressHelper.GetLocalIpv4Addresses().FirstOrDefault(address => !address.StartsWith("127.", StringComparison.Ordinal))
			?? LanAddressHelper.GetLocalIpv4Addresses().FirstOrDefault()
				?? "127.0.0.1";
	}

	private bool CanLocalTransportTakeOver()
	{
		if (!HostMigrationCapabilityAvailable
			|| !TryBuildLocalMigrationProbeDescriptor(out NetworkMigrationTransportDescriptor descriptor)
			|| CreateMigrationTransport(_config.Transport) is not { } transport)
		{
			return false;
		}

		HostMigrationTransportResult result = transport.ProbeCandidate(descriptor, out string error);
		if (result != HostMigrationTransportResult.Ready)
		{
			GD.Print($"MIGRATION_CANDIDATE_PROBE_FAILED transport={_config.Transport} result={result} error={error}");
			return false;
		}

		return true;
	}

	private bool TryBuildLocalMigrationProbeDescriptor(out NetworkMigrationTransportDescriptor descriptor)
	{
		descriptor = new NetworkMigrationTransportDescriptor();
		if (!HostMigrationCapabilityAvailable)
		{
			return false;
		}

		if (_config.Transport == NetworkTransport.Lan)
		{
			string address = GetObservedLanAddress();
			descriptor = new NetworkMigrationTransportDescriptor
			{
				Lan = new NetworkLanMigrationDescriptor
				{
					ObservedHostAddress = address,
					CandidateHostAddress = address,
				},
			};
			return true;
		}
		if (_config.Transport != NetworkTransport.Steam
			|| _steamLobbyId == 0
			|| _steamLobbyId > (ulong)long.MaxValue
			|| !TryGetLocalSteamId(out long localSteamId))
		{
			return false;
		}

		ulong owner = GetSteamLobbyOwner(_steamLobbyId);
		if (owner == 0 || owner > (ulong)long.MaxValue || (long)owner == localSteamId)
		{
			return false;
		}

		descriptor = new NetworkMigrationTransportDescriptor
		{
			Steam = new NetworkSteamMigrationDescriptor
			{
				LobbyId = (long)_steamLobbyId,
				SourceLobbyOwnerSteamId = (long)owner,
				CandidateSteamId = localSteamId,
				ExpectedLobbyOwnerSteamId = localSteamId,
			},
		};
		return true;
	}

	private bool TryBuildMigrationTransportDescriptor(
		int candidatePeerId,
		string candidateHostAddress,
		out NetworkMigrationTransportDescriptor descriptor)
	{
		descriptor = new NetworkMigrationTransportDescriptor();
		if (!HostMigrationCapabilityAvailable)
		{
			return false;
		}

		if (_config.Transport == NetworkTransport.Lan)
		{
			var lan = new NetworkLanMigrationDescriptor
			{
				ObservedHostAddress = GetObservedLanAddress(),
				CandidateHostAddress = candidateHostAddress.Trim(),
			};
			descriptor = new NetworkMigrationTransportDescriptor { Lan = lan };
			return NetworkMigrationRules.IsLanMigrationDescriptorValid(lan);
		}
		if (_config.Transport != NetworkTransport.Steam
			|| _steamLobbyId == 0
			|| _steamLobbyId > (ulong)long.MaxValue
			|| !TryGetLocalSteamId(out long localSteamId)
			|| !TryGetSteamIdForPeer(candidatePeerId, out long candidateSteamId))
		{
			return false;
		}

		ulong owner = GetSteamLobbyOwner(_steamLobbyId);
		if (owner == 0
			|| owner > (ulong)long.MaxValue
			|| (long)owner != localSteamId
			|| candidateSteamId == localSteamId)
		{
			return false;
		}

		var steam = new NetworkSteamMigrationDescriptor
		{
			LobbyId = (long)_steamLobbyId,
			SourceLobbyOwnerSteamId = localSteamId,
			CandidateSteamId = candidateSteamId,
			ExpectedLobbyOwnerSteamId = candidateSteamId,
		};
		descriptor = new NetworkMigrationTransportDescriptor { Steam = steam };
		return NetworkMigrationRules.IsSteamMigrationDescriptorValid(steam);
	}

	private IHostMigrationTransport? CreateMigrationTransport(NetworkTransport transport)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return null;
		}

		return transport switch
		{
			NetworkTransport.Lan => new LanHostMigrationTransport(),
			NetworkTransport.Steam => new SteamHostMigrationTransport(
				_steamPlatform,
				GetLastObservedSteamLobbyOwnerId,
				TryRefreshSteamLobbyAdvertisedState),
			_ => null,
		};
	}

	private void InitializeMigrationTransportState(NetworkMigrationPlan plan, double now)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			ResetMigrationTransportState();
			return;
		}

		if (_migrationTransportAdapter is not null
			&& _migrationTransportAdapter.Transport == plan.Transport
			&& _migrationTransportPlanRevision == plan.PlanRevision)
		{
			return;
		}

		_migrationTransportAdapter = CreateMigrationTransport(plan.Transport);
		_migrationTransportPlanRevision = plan.PlanRevision;
		_migrationTransportPreparedAt = -1.0;
		_migrationOwnerConvergenceDeadline = now + NetworkMigrationRules.SteamLobbyOwnerConvergenceTimeoutSeconds;
	}

	private HostMigrationTransportResult PrepareMigrationTransport(
		NetworkMigrationPlan plan,
		double now,
		out string error)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			error = "migration_capability_unavailable";
			return HostMigrationTransportResult.Failed;
		}

		InitializeMigrationTransportState(plan, now);
		if (_migrationTransportAdapter is not { } transport)
		{
			error = "migration_transport_unavailable";
			return HostMigrationTransportResult.Failed;
		}
		if (plan.Transport == NetworkTransport.Lan && _migrationTransportPreparedAt >= 0.0)
		{
			error = string.Empty;
			return HostMigrationTransportResult.Ready;
		}

		HostMigrationTransportResult result = transport.PrepareTakeover(
			plan,
			now,
			_migrationOwnerConvergenceDeadline,
			out error);
		if (result == HostMigrationTransportResult.Ready)
		{
			_migrationTransportPreparedAt = now;
		}
		return result;
	}

	private void ArmMigrationAttemptsAfterSourceDisconnect(NetworkMigrationPlan plan, double now)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return;
		}

		_migrationAwaitingSourceDisconnect = false;
		_migrationStartedAt = now;
		_nextMigrationAttemptAt = now + (IsLocalMigrationCandidate(plan)
			? NetworkMigrationRules.CandidateStartDelaySeconds
			: NetworkMigrationRules.FollowerStartDelaySeconds);
		_migrationServerStarted = false;
		_migrationPeerAttemptInFlight = false;
		_migrationPeerAttemptStartedAt = -1.0;
	}

	private void ResetMigrationTransportState()
	{
		_migrationTransportAdapter = null;
		_migrationTransportPlanRevision = -1;
		_migrationTransportPreparedAt = -1.0;
		_migrationOwnerConvergenceDeadline = -1.0;
		_migrationAwaitingSourceDisconnect = false;
	}

	private static NetworkMigrationFailureCode GetMigrationTransportFailureCode(
		NetworkMigrationPlan plan,
		string error)
	{
		return plan.Transport == NetworkTransport.Steam
			&& error.Contains("owner", StringComparison.Ordinal)
			? NetworkMigrationFailureCode.SteamOwnerTimeout
			: NetworkMigrationFailureCode.TransportUnavailable;
	}


	private bool UpdateMigratedHostMetadata(NetworkMigrationPlan plan)
	{
		if (!HostMigrationCapabilityAvailable)
		{
			return false;
		}

		string error = "migration_transport_unavailable";
		if (_migrationTransportAdapter is { } transport
			&& transport.UpdateMetadata(plan, out error) == HostMigrationTransportResult.Ready)
		{
			return true;
		}

		GD.Print($"MIGRATION_METADATA_UPDATE_FAILED endpoint={GetMigrationEndpointLog(plan)} error={error}");
		FailMigration(plan.Transport == NetworkTransport.Steam
			? NetworkMigrationFailureCode.SteamMetadataUpdateFailed
			: NetworkMigrationFailureCode.TransportUnavailable);
		return false;
	}

	private string GetLocalMigrationEndpointAcknowledgement()
	{
		return _config.Transport == NetworkTransport.Lan
			? GetObservedLanAddress()
			: string.Empty;
	}

	private static bool IsMigrationEndpointAcknowledgementValid(
		NetworkTransport transport,
		string endpoint)
	{
		return transport switch
		{
			NetworkTransport.Lan => !string.IsNullOrWhiteSpace(endpoint)
				&& endpoint.Length <= NetworkProtocol.MaxAddressLength
				&& !endpoint.Any(char.IsWhiteSpace),
			NetworkTransport.Steam => string.IsNullOrEmpty(endpoint),
			_ => false,
		};
	}

	private static string GetMigrationEndpointLog(NetworkMigrationPlan? plan)
	{
		if (plan is null)
		{
			return "none";
		}

		return plan.Transport switch
		{
			NetworkTransport.Lan when plan.TransportDescriptor.Lan is { } lan => $"lan:{lan.CandidateHostAddress}:{lan.Port}",
			NetworkTransport.Steam when plan.TransportDescriptor.Steam is { } steam => $"steam:lobby={steam.LobbyId}:owner={steam.ExpectedLobbyOwnerSteamId}",
			_ => "invalid",
		};
	}

}
