using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;

public partial class NetworkManager
{
	private static Godot.Collections.Dictionary ToMigrationPlanVariant(NetworkMigrationPlan plan)
	{
		return new Godot.Collections.Dictionary
		{
			["protocol_version"] = plan.ProtocolVersion,
			["plan_revision"] = plan.PlanRevision,
			["session_id"] = plan.SessionId,
			["match_id"] = plan.MatchId,
			["source_epoch"] = plan.SourceEpoch,
			["target_epoch"] = plan.TargetEpoch,
			["checkpoint_id"] = plan.CheckpointId,
			["checkpoint_hash"] = plan.CheckpointHash,
			["checkpoint_ciphertext_hash"] = plan.CheckpointCiphertextHash,
			["previous_checkpoint_hash"] = plan.PreviousCheckpointHash,
			["candidate_player_session_id"] = plan.CandidatePlayerSessionId,
			["candidate_seat"] = plan.CandidateSeat,
			["candidate_successor_public_key"] = plan.CandidateSuccessorPublicKey,
			["candidate_successor_key_id"] = plan.CandidateSuccessorKeyId,
			["source_player_session_id"] = plan.SourcePlayerSessionId,
			["source_authority_key_id"] = plan.SourceAuthorityKeyId,
			["transport"] = plan.Transport.ToString(),
			["transport_descriptor"] = ToMigrationTransportDescriptorVariant(plan),
			["authority_phase"] = plan.Phase.ToString(),
			["source_signature"] = plan.SourceSignature,
		};
	}

	private static Godot.Collections.Dictionary ToMigrationTransportDescriptorVariant(NetworkMigrationPlan plan)
	{
		return plan.Transport switch
		{
			NetworkTransport.Lan when plan.TransportDescriptor.Lan is { } lan => new Godot.Collections.Dictionary
			{
				["observed_host_address"] = lan.ObservedHostAddress,
				["candidate_host_address"] = lan.CandidateHostAddress,
				["port"] = lan.Port,
				["max_clients"] = lan.MaxClients,
				["peer_rebuild_mode"] = lan.PeerRebuildMode.ToString(),
			},
			NetworkTransport.Steam when plan.TransportDescriptor.Steam is { } steam => new Godot.Collections.Dictionary
			{
				["lobby_id"] = steam.LobbyId,
				["source_lobby_owner_steam_id"] = steam.SourceLobbyOwnerSteamId,
				["candidate_steam_id"] = steam.CandidateSteamId,
				["expected_lobby_owner_steam_id"] = steam.ExpectedLobbyOwnerSteamId,
				["peer_rebuild_mode"] = steam.PeerRebuildMode.ToString(),
			},
			_ => new Godot.Collections.Dictionary(),
		};
	}

	private static Godot.Collections.Dictionary ToMigrationPlanRevocationVariant(
		NetworkMigrationPlanRevocation revocation)
	{
		return new Godot.Collections.Dictionary
		{
			["protocol_version"] = revocation.ProtocolVersion,
			["plan_revision"] = revocation.PlanRevision,
			["revoked_plan_revision"] = revocation.RevokedPlanRevision,
			["revoked_checkpoint_id"] = revocation.RevokedCheckpointId,
			["session_id"] = revocation.SessionId,
			["match_id"] = revocation.MatchId,
			["source_epoch"] = revocation.SourceEpoch,
			["source_authority_key_id"] = revocation.SourceAuthorityKeyId,
			["source_signature"] = revocation.SourceSignature,
		};
	}

	private static bool TryParseMigrationPlan(
		Godot.Collections.Dictionary raw,
		out NetworkMigrationPlan plan)
	{
		plan = new NetworkMigrationPlan();
		if (!NetworkVariantReader.TryReadInt32(raw, "protocol_version", NetworkProtocol.CurrentVersion, NetworkProtocol.CurrentVersion, out int protocolVersion)
			|| !NetworkVariantReader.TryReadInt64(raw, "plan_revision", 1, long.MaxValue, out long planRevision)
			|| !NetworkVariantReader.TryReadString(raw, "session_id", NetworkProtocol.MaxSessionIdLength, out string sessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "match_id", NetworkProtocol.MaxMatchIdLength, out string matchId)
			|| !NetworkVariantReader.TryReadInt64(raw, "source_epoch", 0, long.MaxValue, out long sourceEpoch)
			|| !NetworkVariantReader.TryReadInt64(raw, "target_epoch", 0, long.MaxValue, out long targetEpoch)
			|| !NetworkVariantReader.TryReadInt64(raw, "checkpoint_id", 0, long.MaxValue, out long checkpointId)
			|| !NetworkVariantReader.TryReadString(raw, "checkpoint_hash", 64, out string checkpointHash, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "checkpoint_ciphertext_hash", 64, out string checkpointCiphertextHash, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "previous_checkpoint_hash", 64, out string previousCheckpointHash)
			|| !NetworkVariantReader.TryReadString(raw, "candidate_player_session_id", NetworkProtocol.MaxPlayerIdLength, out string candidatePlayerSessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadInt32(raw, "candidate_seat", 0, NetworkSessionDefaults.MaxPlayers - 1, out int candidateSeat)
			|| !NetworkVariantReader.TryReadString(raw, "candidate_successor_public_key", 512, out string candidateSuccessorPublicKey, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "candidate_successor_key_id", 64, out string candidateSuccessorKeyId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "source_player_session_id", NetworkProtocol.MaxPlayerIdLength, out string sourcePlayerSessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "source_authority_key_id", 64, out string sourceAuthorityKeyId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadEnum(raw, "transport", out NetworkTransport transport)
			|| !NetworkVariantReader.TryReadDictionary(raw, "transport_descriptor", out Godot.Collections.Dictionary rawTransportDescriptor)
			|| !TryParseMigrationTransportDescriptor(transport, rawTransportDescriptor, out NetworkMigrationTransportDescriptor transportDescriptor)
			|| !NetworkVariantReader.TryReadEnum(raw, "authority_phase", out NetworkAuthorityPhase phase)
			|| !NetworkVariantReader.TryReadString(raw, "source_signature", 512, out string sourceSignature, allowEmpty: false))
		{
			return false;
		}

		plan = new NetworkMigrationPlan
		{
			ProtocolVersion = protocolVersion,
			PlanRevision = planRevision,
			SessionId = sessionId,
			MatchId = matchId,
			SourceEpoch = sourceEpoch,
			TargetEpoch = targetEpoch,
			CheckpointId = checkpointId,
			CheckpointHash = checkpointHash,
			CheckpointCiphertextHash = checkpointCiphertextHash,
			PreviousCheckpointHash = previousCheckpointHash,
			CandidatePlayerSessionId = candidatePlayerSessionId,
			CandidateSeat = candidateSeat,
			CandidateSuccessorPublicKey = candidateSuccessorPublicKey,
			CandidateSuccessorKeyId = candidateSuccessorKeyId,
			SourcePlayerSessionId = sourcePlayerSessionId,
			SourceAuthorityKeyId = sourceAuthorityKeyId,
			Transport = transport,
			TransportDescriptor = transportDescriptor,
			Phase = phase,
			SourceSignature = sourceSignature,
		};
		return NetworkMigrationRules.IsPlanVisibleToClients(plan);
	}

	private static bool TryParseMigrationTransportDescriptor(
		NetworkTransport transport,
		Godot.Collections.Dictionary raw,
		out NetworkMigrationTransportDescriptor descriptor)
	{
		descriptor = new NetworkMigrationTransportDescriptor();
		if (transport == NetworkTransport.Lan)
		{
			if (!NetworkVariantReader.TryReadString(raw, "observed_host_address", NetworkProtocol.MaxAddressLength, out string observedHostAddress, allowEmpty: false)
				|| !NetworkVariantReader.TryReadString(raw, "candidate_host_address", NetworkProtocol.MaxAddressLength, out string candidateHostAddress, allowEmpty: false)
				|| !NetworkVariantReader.TryReadInt32(raw, "port", 1, ushort.MaxValue, out int port)
				|| !NetworkVariantReader.TryReadInt32(raw, "max_clients", 1, NetworkSessionDefaults.MaxPlayers - 1, out int maxClients)
				|| !NetworkVariantReader.TryReadEnum(raw, "peer_rebuild_mode", out NetworkMigrationPeerRebuildMode peerRebuildMode))
			{
				return false;
			}

			var lan = new NetworkLanMigrationDescriptor
			{
				ObservedHostAddress = observedHostAddress,
				CandidateHostAddress = candidateHostAddress,
				Port = port,
				MaxClients = maxClients,
				PeerRebuildMode = peerRebuildMode,
			};
			descriptor = new NetworkMigrationTransportDescriptor { Lan = lan };
			return NetworkMigrationRules.IsLanMigrationDescriptorValid(lan);
		}
		if (transport != NetworkTransport.Steam
			|| !NetworkVariantReader.TryReadInt64(raw, "lobby_id", 1, long.MaxValue, out long lobbyId)
			|| !NetworkVariantReader.TryReadInt64(raw, "source_lobby_owner_steam_id", 1, long.MaxValue, out long sourceOwnerSteamId)
			|| !NetworkVariantReader.TryReadInt64(raw, "candidate_steam_id", 1, long.MaxValue, out long candidateSteamId)
			|| !NetworkVariantReader.TryReadInt64(raw, "expected_lobby_owner_steam_id", 1, long.MaxValue, out long expectedOwnerSteamId)
			|| !NetworkVariantReader.TryReadEnum(raw, "peer_rebuild_mode", out NetworkMigrationPeerRebuildMode steamPeerRebuildMode))
		{
			return false;
		}

		var steam = new NetworkSteamMigrationDescriptor
		{
			LobbyId = lobbyId,
			SourceLobbyOwnerSteamId = sourceOwnerSteamId,
			CandidateSteamId = candidateSteamId,
			ExpectedLobbyOwnerSteamId = expectedOwnerSteamId,
			PeerRebuildMode = steamPeerRebuildMode,
		};
		descriptor = new NetworkMigrationTransportDescriptor { Steam = steam };
		return NetworkMigrationRules.IsSteamMigrationDescriptorValid(steam);
	}

	private static bool TryParseMigrationPlanRevocation(
		Godot.Collections.Dictionary raw,
		out NetworkMigrationPlanRevocation revocation)
	{
		revocation = new NetworkMigrationPlanRevocation();
		if (!NetworkVariantReader.TryReadInt32(raw, "protocol_version", NetworkProtocol.CurrentVersion, NetworkProtocol.CurrentVersion, out int protocolVersion)
			|| !NetworkVariantReader.TryReadInt64(raw, "plan_revision", 1, long.MaxValue, out long planRevision)
			|| !NetworkVariantReader.TryReadInt64(raw, "revoked_plan_revision", 1, long.MaxValue, out long revokedPlanRevision)
			|| !NetworkVariantReader.TryReadInt64(raw, "revoked_checkpoint_id", 0, long.MaxValue, out long revokedCheckpointId)
			|| !NetworkVariantReader.TryReadString(raw, "session_id", NetworkProtocol.MaxSessionIdLength, out string sessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "match_id", NetworkProtocol.MaxMatchIdLength, out string matchId)
			|| !NetworkVariantReader.TryReadInt64(raw, "source_epoch", 0, long.MaxValue, out long sourceEpoch)
			|| !NetworkVariantReader.TryReadString(raw, "source_authority_key_id", 64, out string sourceAuthorityKeyId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(raw, "source_signature", 512, out string sourceSignature, allowEmpty: false))
		{
			return false;
		}

		revocation = new NetworkMigrationPlanRevocation
		{
			ProtocolVersion = protocolVersion,
			PlanRevision = planRevision,
			RevokedPlanRevision = revokedPlanRevision,
			RevokedCheckpointId = revokedCheckpointId,
			SessionId = sessionId,
			MatchId = matchId,
			SourceEpoch = sourceEpoch,
			SourceAuthorityKeyId = sourceAuthorityKeyId,
			SourceSignature = sourceSignature,
		};
		return NetworkMigrationRules.IsPlanRevocationVisibleToClients(revocation);
	}

}
