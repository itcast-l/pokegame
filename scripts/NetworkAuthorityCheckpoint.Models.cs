using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Deckland.Rules;

public sealed record NetworkMigrationRosterEntry
{
	[JsonPropertyName("player_session_id")]
	[JsonPropertyOrder(0)]
	public string PlayerSessionId { get; init; } = string.Empty;

	[JsonPropertyName("display_name")]
	[JsonPropertyOrder(1)]
	public string DisplayName { get; init; } = string.Empty;

	[JsonPropertyName("avatar_id")]
	[JsonPropertyOrder(2)]
	public string AvatarId { get; init; } = string.Empty;

	[JsonPropertyName("seat_index")]
	[JsonPropertyOrder(3)]
	public int SeatIndex { get; init; }

	[JsonPropertyName("peer_id")]
	[JsonPropertyOrder(4)]
	public int PeerId { get; init; }

	[JsonPropertyName("is_host")]
	[JsonPropertyOrder(5)]
	public bool IsHost { get; init; }

	[JsonPropertyName("is_ready")]
	[JsonPropertyOrder(6)]
	public bool IsReady { get; init; }

	[JsonPropertyName("is_connected")]
	[JsonPropertyOrder(7)]
	public bool IsConnected { get; init; }

	[JsonPropertyName("credentials_acknowledged")]
	[JsonPropertyOrder(8)]
	public bool CredentialsAcknowledged { get; init; }
}

public sealed record NetworkAuthoritySeatControl
{
	[JsonPropertyName("seat_index")]
	[JsonPropertyOrder(0)]
	public int SeatIndex { get; init; }

	[JsonPropertyName("mode")]
	[JsonPropertyOrder(1)]
	public SeatControlMode Mode { get; init; }

	[JsonPropertyName("peer_id")]
	[JsonPropertyOrder(2)]
	public int PeerId { get; init; }

	[JsonPropertyName("player_session_id")]
	[JsonPropertyOrder(3)]
	public string PlayerSessionId { get; init; } = string.Empty;

	[JsonPropertyName("credential_verifier")]
	[JsonPropertyOrder(4)]
	public string CredentialVerifier { get; init; } = string.Empty;

	[JsonPropertyName("credential_generation")]
	[JsonPropertyOrder(5)]
	public long CredentialGeneration { get; init; }

	[JsonPropertyName("pending_credential_verifier")]
	[JsonPropertyOrder(6)]
	public string PendingCredentialVerifier { get; init; } = string.Empty;

	[JsonPropertyName("pending_credential_generation")]
	[JsonPropertyOrder(7)]
	public long PendingCredentialGeneration { get; init; }

	[JsonPropertyName("pending_credential_peer_id")]
	[JsonPropertyOrder(8)]
	public int PendingCredentialPeerId { get; init; }
}

public sealed record NetworkAuthorityReconnectClaim
{
	[JsonPropertyName("match_id")]
	[JsonPropertyOrder(0)]
	public string MatchId { get; init; } = string.Empty;

	[JsonPropertyName("seat_index")]
	[JsonPropertyOrder(1)]
	public int SeatIndex { get; init; }

	[JsonPropertyName("credential_generation")]
	[JsonPropertyOrder(2)]
	public long CredentialGeneration { get; init; }

	[JsonPropertyName("player_session_id")]
	[JsonPropertyOrder(3)]
	public string PlayerSessionId { get; init; } = string.Empty;

	[JsonPropertyName("claimed_peer_id")]
	[JsonPropertyOrder(4)]
	public int ClaimedPeerId { get; init; }

	[JsonPropertyName("claim_state")]
	[JsonPropertyOrder(5)]
	public string ClaimState { get; init; } = nameof(ReconnectClaimState.Unclaimed);
}

public enum NetworkMigrationPeerRebuildMode
{
	EnetServer,
	SteamLobby,
}

public sealed record NetworkLanMigrationDescriptor
{
	[JsonPropertyName("observed_host_address")]
	[JsonPropertyOrder(0)]
	public string ObservedHostAddress { get; init; } = string.Empty;

	[JsonPropertyName("candidate_host_address")]
	[JsonPropertyOrder(1)]
	public string CandidateHostAddress { get; init; } = string.Empty;

	[JsonPropertyName("port")]
	[JsonPropertyOrder(2)]
	public int Port { get; init; } = NetworkProtocol.LanHostMigrationPort;

	[JsonPropertyName("max_clients")]
	[JsonPropertyOrder(3)]
	public int MaxClients { get; init; } = NetworkSessionDefaults.MaxPlayers - 1;

	[JsonPropertyName("peer_rebuild_mode")]
	[JsonPropertyOrder(4)]
	public NetworkMigrationPeerRebuildMode PeerRebuildMode { get; init; } = NetworkMigrationPeerRebuildMode.EnetServer;
}

public sealed record NetworkSteamMigrationDescriptor
{
	[JsonPropertyName("lobby_id")]
	[JsonPropertyOrder(0)]
	public long LobbyId { get; init; }

	[JsonPropertyName("source_lobby_owner_steam_id")]
	[JsonPropertyOrder(1)]
	public long SourceLobbyOwnerSteamId { get; init; }

	[JsonPropertyName("candidate_steam_id")]
	[JsonPropertyOrder(2)]
	public long CandidateSteamId { get; init; }

	[JsonPropertyName("expected_lobby_owner_steam_id")]
	[JsonPropertyOrder(3)]
	public long ExpectedLobbyOwnerSteamId { get; init; }

	[JsonPropertyName("peer_rebuild_mode")]
	[JsonPropertyOrder(4)]
	public NetworkMigrationPeerRebuildMode PeerRebuildMode { get; init; } = NetworkMigrationPeerRebuildMode.SteamLobby;
}

public sealed record NetworkMigrationTransportDescriptor
{
	[JsonPropertyName("lan")]
	[JsonPropertyOrder(0)]
	public NetworkLanMigrationDescriptor? Lan { get; init; }

	[JsonPropertyName("steam")]
	[JsonPropertyOrder(1)]
	public NetworkSteamMigrationDescriptor? Steam { get; init; }
}

public sealed record NetworkMigrationPlan
{
	[JsonPropertyName("protocol_version")]
	[JsonPropertyOrder(-2)]
	public int ProtocolVersion { get; init; } = NetworkProtocol.CurrentVersion;

	[JsonPropertyName("plan_revision")]
	[JsonPropertyOrder(-1)]
	public long PlanRevision { get; init; }

	[JsonPropertyName("session_id")]
	[JsonPropertyOrder(0)]
	public string SessionId { get; init; } = string.Empty;

	[JsonPropertyName("match_id")]
	[JsonPropertyOrder(1)]
	public string MatchId { get; init; } = string.Empty;

	[JsonPropertyName("source_epoch")]
	[JsonPropertyOrder(2)]
	public long SourceEpoch { get; init; }

	[JsonPropertyName("target_epoch")]
	[JsonPropertyOrder(3)]
	public long TargetEpoch { get; init; }

	[JsonPropertyName("checkpoint_id")]
	[JsonPropertyOrder(4)]
	public long CheckpointId { get; init; }

	[JsonPropertyName("checkpoint_hash")]
	[JsonPropertyOrder(5)]
	public string CheckpointHash { get; init; } = string.Empty;

	[JsonPropertyName("checkpoint_ciphertext_hash")]
	[JsonPropertyOrder(6)]
	public string CheckpointCiphertextHash { get; init; } = string.Empty;

	[JsonPropertyName("previous_checkpoint_hash")]
	[JsonPropertyOrder(7)]
	public string PreviousCheckpointHash { get; init; } = string.Empty;

	[JsonPropertyName("candidate_player_session_id")]
	[JsonPropertyOrder(8)]
	public string CandidatePlayerSessionId { get; init; } = string.Empty;

	[JsonPropertyName("candidate_seat")]
	[JsonPropertyOrder(9)]
	public int CandidateSeat { get; init; } = -1;

	[JsonPropertyName("candidate_successor_public_key")]
	[JsonPropertyOrder(10)]
	public string CandidateSuccessorPublicKey { get; init; } = string.Empty;

	[JsonPropertyName("candidate_successor_key_id")]
	[JsonPropertyOrder(11)]
	public string CandidateSuccessorKeyId { get; init; } = string.Empty;

	[JsonPropertyName("transport")]
	[JsonPropertyOrder(12)]
	public NetworkTransport Transport { get; init; } = NetworkTransport.Lan;

	[JsonPropertyName("transport_descriptor")]
	[JsonPropertyOrder(13)]
	public NetworkMigrationTransportDescriptor TransportDescriptor { get; init; } = new();

	[JsonPropertyName("source_player_session_id")]
	[JsonPropertyOrder(14)]
	public string SourcePlayerSessionId { get; init; } = string.Empty;

	[JsonPropertyName("source_authority_key_id")]
	[JsonPropertyOrder(15)]
	public string SourceAuthorityKeyId { get; init; } = string.Empty;

	[JsonPropertyName("authority_phase")]
	[JsonPropertyOrder(16)]
	public NetworkAuthorityPhase Phase { get; init; } = NetworkAuthorityPhase.InGame;

	[JsonPropertyName("source_signature")]
	[JsonPropertyOrder(17)]
	public string SourceSignature { get; init; } = string.Empty;
}

public sealed record NetworkMigrationPlanRevocation
{
	[JsonPropertyName("protocol_version")]
	[JsonPropertyOrder(0)]
	public int ProtocolVersion { get; init; } = NetworkProtocol.CurrentVersion;

	[JsonPropertyName("plan_revision")]
	[JsonPropertyOrder(1)]
	public long PlanRevision { get; init; }

	[JsonPropertyName("revoked_plan_revision")]
	[JsonPropertyOrder(2)]
	public long RevokedPlanRevision { get; init; }

	[JsonPropertyName("revoked_checkpoint_id")]
	[JsonPropertyOrder(3)]
	public long RevokedCheckpointId { get; init; }

	[JsonPropertyName("session_id")]
	[JsonPropertyOrder(4)]
	public string SessionId { get; init; } = string.Empty;

	[JsonPropertyName("match_id")]
	[JsonPropertyOrder(5)]
	public string MatchId { get; init; } = string.Empty;

	[JsonPropertyName("source_epoch")]
	[JsonPropertyOrder(6)]
	public long SourceEpoch { get; init; }

	[JsonPropertyName("source_authority_key_id")]
	[JsonPropertyOrder(7)]
	public string SourceAuthorityKeyId { get; init; } = string.Empty;

	[JsonPropertyName("source_signature")]
	[JsonPropertyOrder(8)]
	public string SourceSignature { get; init; } = string.Empty;
}
