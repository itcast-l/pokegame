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

public sealed record NetworkAuthorityCheckpoint
{
	[JsonPropertyName("schema_version")]
	[JsonPropertyOrder(0)]
	public int SchemaVersion { get; init; } = NetworkProtocol.AuthorityCheckpointSchemaVersion;

	[JsonPropertyName("protocol_version")]
	[JsonPropertyOrder(1)]
	public int ProtocolVersion { get; init; } = NetworkProtocol.CurrentVersion;

	[JsonPropertyName("session_id")]
	[JsonPropertyOrder(2)]
	public string SessionId { get; init; } = string.Empty;

	[JsonPropertyName("match_id")]
	[JsonPropertyOrder(3)]
	public string MatchId { get; init; } = string.Empty;

	[JsonPropertyName("room_name")]
	[JsonPropertyOrder(4)]
	public string RoomName { get; init; } = string.Empty;

	[JsonPropertyName("room_password_verifier_version")]
	[JsonPropertyOrder(5)]
	public int RoomPasswordVerifierVersion { get; init; }

	[JsonPropertyName("room_password_verifier_iterations")]
	[JsonPropertyOrder(6)]
	public int RoomPasswordVerifierIterations { get; init; }

	[JsonPropertyName("room_password_verifier_salt")]
	[JsonPropertyOrder(7)]
	public string RoomPasswordVerifierSalt { get; init; } = string.Empty;

	[JsonPropertyName("room_password_verifier")]
	[JsonPropertyOrder(8)]
	public string RoomPasswordVerifier { get; init; } = string.Empty;

	[JsonPropertyName("match_seed")]
	[JsonPropertyOrder(9)]
	public long MatchSeed { get; init; }

	[JsonPropertyName("player_count")]
	[JsonPropertyOrder(10)]
	public int PlayerCount { get; init; }

	[JsonPropertyName("item_mode")]
	[JsonPropertyOrder(11)]
	public bool ItemMode { get; init; }

	[JsonPropertyName("winning_score")]
	[JsonPropertyOrder(12)]
	public int WinningScore { get; init; }

	[JsonPropertyName("transport")]
	[JsonPropertyOrder(13)]
	public NetworkTransport Transport { get; init; }

	[JsonPropertyName("authority_phase")]
	[JsonPropertyOrder(14)]
	public NetworkAuthorityPhase Phase { get; init; } = NetworkAuthorityPhase.InGame;

	[JsonPropertyName("manifest_hash")]
	[JsonPropertyOrder(15)]
	public string ManifestHash { get; init; } = string.Empty;

	[JsonPropertyName("roster")]
	[JsonPropertyOrder(16)]
	public List<NetworkMigrationRosterEntry> Roster { get; init; } = new();

	[JsonPropertyName("seat_controls")]
	[JsonPropertyOrder(17)]
	public List<NetworkAuthoritySeatControl> SeatControls { get; init; } = new();

	[JsonPropertyName("last_accepted_client_action_ids")]
	[JsonPropertyOrder(18)]
	public Dictionary<int, long> LastAcceptedClientActionIds { get; init; } = new();

	[JsonPropertyName("decks")]
	[JsonPropertyOrder(19)]
	public List<List<NetworkCheckpointCard>> Decks { get; init; } = new();

	[JsonPropertyName("hands")]
	[JsonPropertyOrder(20)]
	public List<List<NetworkCheckpointCard>> Hands { get; init; } = new();

	[JsonPropertyName("item_hands")]
	[JsonPropertyOrder(21)]
	public List<List<string>> ItemHands { get; init; } = new();

	[JsonPropertyName("table_cards")]
	[JsonPropertyOrder(22)]
	public List<NetworkCheckpointTableCard> TableCards { get; init; } = new();

	[JsonPropertyName("scores")]
	[JsonPropertyOrder(23)]
	public int[] Scores { get; init; } = Array.Empty<int>();

	[JsonPropertyName("base_play_capture_threshold")]
	[JsonPropertyOrder(24)]
	[JsonRequired]
	public int BasePlayCaptureThreshold { get; init; }

	[JsonPropertyName("winner_player_index")]
	[JsonPropertyOrder(25)]
	public int WinnerPlayerIndex { get; init; } = -1;

	[JsonPropertyName("match_outcome_kind")]
	[JsonPropertyOrder(26)]
	public MatchOutcomeKind MatchOutcomeKind { get; init; } = MatchOutcomeKind.InProgress;

	[JsonPropertyName("opening_deal_completed")]
	[JsonPropertyOrder(27)]
	public bool OpeningDealCompleted { get; init; } = true;

	[JsonPropertyName("current_turn_player_index")]
	[JsonPropertyOrder(28)]
	public int CurrentTurnPlayerIndex { get; init; }

	[JsonPropertyName("turn_sequence_generation")]
	[JsonPropertyOrder(29)]
	public int TurnSequenceGeneration { get; init; }

	[JsonPropertyName("completed_turns_since_question_refresh")]
	[JsonPropertyOrder(30)]
	public int CompletedTurnsSinceQuestionRefresh { get; init; }

	[JsonPropertyName("question_refresh_anchor_player_index")]
	[JsonPropertyOrder(31)]
	public int QuestionRefreshAnchorPlayerIndex { get; init; } = -1;

	[JsonPropertyName("last_authority_action_id")]
	[JsonPropertyOrder(32)]
	public long LastAuthorityActionId { get; init; }

	[JsonPropertyName("authority_action")]
	[JsonPropertyOrder(33)]
	public AuthorityActionState AuthorityAction { get; init; } = AuthorityActionState.Empty;

	[JsonPropertyName("rng_seed")]
	[JsonPropertyOrder(34)]
	public long RngSeed { get; init; }

	[JsonPropertyName("rng_state")]
	[JsonPropertyOrder(35)]
	public string RngState { get; init; } = string.Empty;

	[JsonPropertyName("snapshot_id")]
	[JsonPropertyOrder(36)]
	public int SnapshotId { get; init; }

	[JsonPropertyName("authority_epoch")]
	[JsonPropertyOrder(37)]
	public long AuthorityEpoch { get; init; }

	[JsonPropertyName("checkpoint_id")]
	[JsonPropertyOrder(38)]
	public long CheckpointId { get; init; }

	[JsonPropertyName("reconnect_claims")]
	[JsonPropertyOrder(39)]
	public List<NetworkAuthorityReconnectClaim> ReconnectClaims { get; init; } = new();

	[JsonPropertyName("opening_dice_roll_result")]
	[JsonPropertyOrder(40)]
	public int OpeningDiceRollResult { get; init; }

	[JsonIgnore]
	public string PayloadHash { get; init; } = string.Empty;
}
