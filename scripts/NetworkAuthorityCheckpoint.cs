using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record NetworkMigrationRosterEntry
{
	[JsonPropertyName("stable_user_id")]
	[JsonPropertyOrder(0)]
	public string StableUserId { get; init; } = string.Empty;

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

	[JsonPropertyName("stable_user_id")]
	[JsonPropertyOrder(3)]
	public string StableUserId { get; init; } = string.Empty;

	[JsonPropertyName("reconnect_token")]
	[JsonPropertyOrder(4)]
	public string ReconnectToken { get; init; } = string.Empty;

	[JsonPropertyName("session_migration_token")]
	[JsonPropertyOrder(5)]
	public string SessionMigrationToken { get; init; } = string.Empty;
}

public sealed record NetworkAuthorityReconnectClaim
{
	[JsonPropertyName("match_id")]
	[JsonPropertyOrder(0)]
	public string MatchId { get; init; } = string.Empty;

	[JsonPropertyName("seat_index")]
	[JsonPropertyOrder(1)]
	public int SeatIndex { get; init; }

	[JsonPropertyName("reconnect_token")]
	[JsonPropertyOrder(2)]
	public string ReconnectToken { get; init; } = string.Empty;

	[JsonPropertyName("stable_user_id")]
	[JsonPropertyOrder(3)]
	public string StableUserId { get; init; } = string.Empty;

	[JsonPropertyName("claimed_peer_id")]
	[JsonPropertyOrder(4)]
	public int ClaimedPeerId { get; init; }

	[JsonPropertyName("claim_state")]
	[JsonPropertyOrder(5)]
	public string ClaimState { get; init; } = nameof(ReconnectClaimState.Unclaimed);
}

public sealed record NetworkCheckpointCard
{
	[JsonPropertyName("code")]
	[JsonPropertyOrder(0)]
	public string Code { get; init; } = string.Empty;

	[JsonPropertyName("attached_item_card_ids")]
	[JsonPropertyOrder(1)]
	public string[] AttachedItemCardIds { get; init; } = Array.Empty<string>();
}

public sealed record NetworkCheckpointTableCard
{
	[JsonPropertyName("card")]
	[JsonPropertyOrder(0)]
	public NetworkCheckpointCard? Card { get; init; }

	[JsonPropertyName("is_face_up")]
	[JsonPropertyOrder(1)]
	public bool IsFaceUp { get; init; }

	[JsonPropertyName("is_buried_in_sand")]
	[JsonPropertyOrder(2)]
	public bool IsBuriedInSand { get; init; }

	[JsonPropertyName("is_slime_coated")]
	[JsonPropertyOrder(3)]
	public bool IsSlimeCoated { get; init; }

	[JsonPropertyName("card_owner_player_index")]
	[JsonPropertyOrder(4)]
	public int CardOwnerPlayerIndex { get; init; } = -1;

	[JsonPropertyName("remove_card_on_capture")]
	[JsonPropertyOrder(5)]
	public bool RemoveCardOnCapture { get; init; }

	[JsonPropertyName("item_overlay_id")]
	[JsonPropertyOrder(6)]
	public string ItemOverlayId { get; init; } = string.Empty;

	[JsonPropertyName("item_owner_player_index")]
	[JsonPropertyOrder(7)]
	public int ItemOwnerPlayerIndex { get; init; } = -1;
}

public sealed record NetworkMigrationPlan
{
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

	[JsonPropertyName("candidate_stable_user_id")]
	[JsonPropertyOrder(6)]
	public string CandidateStableUserId { get; init; } = string.Empty;

	[JsonPropertyName("candidate_seat")]
	[JsonPropertyOrder(7)]
	public int CandidateSeat { get; init; } = -1;

	[JsonPropertyName("observed_host_address")]
	[JsonPropertyOrder(8)]
	public string ObservedHostAddress { get; init; } = string.Empty;

	[JsonPropertyName("candidate_host_address")]
	[JsonPropertyOrder(9)]
	public string CandidateHostAddress { get; init; } = string.Empty;

	[JsonPropertyName("port")]
	[JsonPropertyOrder(10)]
	public int Port { get; init; } = NetworkProtocol.LanHostMigrationPort;

	[JsonPropertyName("source_stable_user_id")]
	[JsonPropertyOrder(11)]
	public string SourceStableUserId { get; init; } = string.Empty;

	[JsonPropertyName("authority_phase")]
	[JsonPropertyOrder(12)]
	public NetworkAuthorityPhase Phase { get; init; } = NetworkAuthorityPhase.InGame;
}

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

	[JsonPropertyName("room_password")]
	[JsonPropertyOrder(5)]
	public string RoomPassword { get; init; } = string.Empty;

	[JsonPropertyName("match_seed")]
	[JsonPropertyOrder(6)]
	public long MatchSeed { get; init; }

	[JsonPropertyName("player_count")]
	[JsonPropertyOrder(7)]
	public int PlayerCount { get; init; }

	[JsonPropertyName("item_mode")]
	[JsonPropertyOrder(8)]
	public bool ItemMode { get; init; }

	[JsonPropertyName("winning_score")]
	[JsonPropertyOrder(9)]
	public int WinningScore { get; init; }

	[JsonPropertyName("transport")]
	[JsonPropertyOrder(10)]
	public NetworkTransport Transport { get; init; }

	[JsonPropertyName("authority_phase")]
	[JsonPropertyOrder(11)]
	public NetworkAuthorityPhase Phase { get; init; } = NetworkAuthorityPhase.InGame;

	[JsonPropertyName("manifest_hash")]
	[JsonPropertyOrder(12)]
	public string ManifestHash { get; init; } = string.Empty;

	[JsonPropertyName("roster")]
	[JsonPropertyOrder(13)]
	public List<NetworkMigrationRosterEntry> Roster { get; init; } = new();

	[JsonPropertyName("seat_controls")]
	[JsonPropertyOrder(14)]
	public List<NetworkAuthoritySeatControl> SeatControls { get; init; } = new();

	[JsonPropertyName("last_accepted_client_action_ids")]
	[JsonPropertyOrder(15)]
	public Dictionary<int, long> LastAcceptedClientActionIds { get; init; } = new();

	[JsonPropertyName("decks")]
	[JsonPropertyOrder(16)]
	public List<List<NetworkCheckpointCard>> Decks { get; init; } = new();

	[JsonPropertyName("hands")]
	[JsonPropertyOrder(17)]
	public List<List<NetworkCheckpointCard>> Hands { get; init; } = new();

	[JsonPropertyName("item_hands")]
	[JsonPropertyOrder(18)]
	public List<List<string>> ItemHands { get; init; } = new();

	[JsonPropertyName("table_cards")]
	[JsonPropertyOrder(19)]
	public List<NetworkCheckpointTableCard> TableCards { get; init; } = new();

	[JsonPropertyName("scores")]
	[JsonPropertyOrder(20)]
	public int[] Scores { get; init; } = Array.Empty<int>();

	[JsonPropertyName("winner_player_index")]
	[JsonPropertyOrder(21)]
	public int WinnerPlayerIndex { get; init; } = -1;

	[JsonPropertyName("match_outcome_kind")]
	[JsonPropertyOrder(34)]
	public MatchOutcomeKind MatchOutcomeKind { get; init; } = MatchOutcomeKind.InProgress;

	[JsonPropertyName("opening_deal_completed")]
	[JsonPropertyOrder(35)]
	public bool OpeningDealCompleted { get; init; } = true;

	[JsonPropertyName("current_turn_player_index")]
	[JsonPropertyOrder(22)]
	public int CurrentTurnPlayerIndex { get; init; }

	[JsonPropertyName("turn_sequence_generation")]
	[JsonPropertyOrder(23)]
	public int TurnSequenceGeneration { get; init; }

	[JsonPropertyName("completed_turns_since_question_refresh")]
	[JsonPropertyOrder(24)]
	public int CompletedTurnsSinceQuestionRefresh { get; init; }

	[JsonPropertyName("question_refresh_anchor_player_index")]
	[JsonPropertyOrder(25)]
	public int QuestionRefreshAnchorPlayerIndex { get; init; } = -1;

	[JsonPropertyName("authority_action")]
	[JsonPropertyOrder(26)]
	public AuthorityActionState AuthorityAction { get; init; } = AuthorityActionState.Empty;

	[JsonPropertyName("rng_seed")]
	[JsonPropertyOrder(27)]
	public long RngSeed { get; init; }

	[JsonPropertyName("rng_state")]
	[JsonPropertyOrder(28)]
	public string RngState { get; init; } = string.Empty;

	[JsonPropertyName("snapshot_id")]
	[JsonPropertyOrder(29)]
	public int SnapshotId { get; init; }

	[JsonPropertyName("authority_epoch")]
	[JsonPropertyOrder(30)]
	public long AuthorityEpoch { get; init; }

	[JsonPropertyName("checkpoint_id")]
	[JsonPropertyOrder(31)]
	public long CheckpointId { get; init; }

	[JsonPropertyName("session_migration_tokens")]
	[JsonPropertyOrder(32)]
	public Dictionary<int, string> SessionMigrationTokens { get; init; } = new();

	[JsonPropertyName("reconnect_claims")]
	[JsonPropertyOrder(33)]
	public List<NetworkAuthorityReconnectClaim> ReconnectClaims { get; init; } = new();

	[JsonIgnore]
	public string PayloadHash { get; init; } = string.Empty;
}

public static class NetworkAuthorityCheckpointCodec
{
	public const int SchemaVersion = NetworkProtocol.AuthorityCheckpointSchemaVersion;
	public const int MaxPayloadBytes = NetworkProtocol.MaxAuthorityCheckpointBytes;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
		PropertyNamingPolicy = null,
		WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.Never,
	};

	public static bool TryEncode(
		NetworkAuthorityCheckpoint checkpoint,
		out byte[] payload,
		out string hash,
		out string error)
	{
		payload = Array.Empty<byte>();
		hash = string.Empty;
		error = string.Empty;
		if (!Validate(checkpoint, out error))
		{
			return false;
		}

		try
		{
			payload = JsonSerializer.SerializeToUtf8Bytes(checkpoint with { PayloadHash = string.Empty }, JsonOptions);
			if (payload.Length > MaxPayloadBytes)
			{
				error = "checkpoint payload exceeds 256KB";
				payload = Array.Empty<byte>();
				return false;
			}

			hash = ComputeHash(payload);
			return true;
		}
		catch (JsonException exception)
		{
			error = exception.Message;
			return false;
		}
	}

	public static bool TryDecode(
		ReadOnlySpan<byte> payload,
		string expectedHash,
		out NetworkAuthorityCheckpoint checkpoint,
		out string error)
	{
		checkpoint = new NetworkAuthorityCheckpoint();
		error = string.Empty;
		if (payload.Length == 0 || payload.Length > MaxPayloadBytes)
		{
			error = "checkpoint payload size is invalid";
			return false;
		}

		string actualHash = ComputeHash(payload);
		if (!IsSha256Hash(expectedHash)
			|| !string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
		{
			error = "checkpoint hash mismatch";
			return false;
		}

		try
		{
			NetworkAuthorityCheckpoint? decoded = JsonSerializer.Deserialize<NetworkAuthorityCheckpoint>(payload, JsonOptions);
			if (decoded is null || !Validate(decoded, out error))
			{
				return false;
			}

			checkpoint = decoded with { PayloadHash = actualHash };
			return true;
		}
		catch (JsonException exception)
		{
			error = exception.Message;
			return false;
		}
	}

	public static string ComputeHash(ReadOnlySpan<byte> payload)
	{
		return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
	}

	public static bool Validate(NetworkAuthorityCheckpoint checkpoint, out string error)
	{
		error = string.Empty;
		if (checkpoint is null
			|| checkpoint.SchemaVersion != SchemaVersion
			|| checkpoint.ProtocolVersion != NetworkProtocol.CurrentVersion
			|| checkpoint.Phase is not (NetworkAuthorityPhase.Lobby or NetworkAuthorityPhase.InGame)
			|| checkpoint.PlayerCount < 2
			|| checkpoint.PlayerCount > NetworkProtocol.MaxAuthorityCheckpointPlayers
			|| string.IsNullOrWhiteSpace(checkpoint.SessionId)
			|| (checkpoint.Phase == NetworkAuthorityPhase.InGame && string.IsNullOrWhiteSpace(checkpoint.MatchId))
			|| checkpoint.SessionId.Length > 128
			|| checkpoint.MatchId.Length > 128
			|| checkpoint.RoomName is null
			|| checkpoint.RoomPassword is null
			|| checkpoint.RoomName.Length > 128
			|| checkpoint.RoomPassword.Length > 128
			|| checkpoint.Scores is null
			|| checkpoint.Roster is null
			|| checkpoint.SeatControls is null
			|| checkpoint.LastAcceptedClientActionIds is null
			|| checkpoint.Decks is null
			|| checkpoint.Hands is null
			|| checkpoint.ItemHands is null
			|| checkpoint.TableCards is null
			|| checkpoint.SessionMigrationTokens is null
			|| checkpoint.ReconnectClaims is null
			|| string.IsNullOrWhiteSpace(checkpoint.ManifestHash)
			|| checkpoint.ManifestHash.Length != 64
			|| !checkpoint.ManifestHash.All(char.IsAsciiHexDigit)
			|| string.IsNullOrWhiteSpace(checkpoint.RngState)
			|| checkpoint.RngState.Length > 32
			|| !ulong.TryParse(checkpoint.RngState, out _)
			|| checkpoint.Scores.Length != checkpoint.PlayerCount
			|| checkpoint.Roster.Count < 1
			|| checkpoint.Roster.Count > checkpoint.PlayerCount
			|| checkpoint.SeatControls.Count != checkpoint.Roster.Count
			|| checkpoint.Decks.Count != checkpoint.PlayerCount
			|| checkpoint.Hands.Count != checkpoint.PlayerCount
			|| checkpoint.ItemHands.Count != checkpoint.PlayerCount
			|| checkpoint.ReconnectClaims.Count != checkpoint.Roster.Count
			|| checkpoint.AuthorityEpoch < 0
			|| checkpoint.SnapshotId < 0
			|| checkpoint.CheckpointId < 0
			|| checkpoint.WinnerPlayerIndex < -1
			|| checkpoint.WinnerPlayerIndex >= checkpoint.PlayerCount
			|| !MatchOutcomeRules.IsSerializedOutcomeValid(
				checkpoint.MatchOutcomeKind,
				checkpoint.WinnerPlayerIndex,
				checkpoint.PlayerCount)
			|| checkpoint.CurrentTurnPlayerIndex < 0
			|| checkpoint.CurrentTurnPlayerIndex >= checkpoint.PlayerCount
			|| checkpoint.TurnSequenceGeneration < 0
			|| checkpoint.TurnSequenceGeneration == int.MaxValue
			|| (checkpoint.Phase == NetworkAuthorityPhase.InGame && !checkpoint.OpeningDealCompleted)
			|| (checkpoint.Phase == NetworkAuthorityPhase.Lobby
				&& (!string.IsNullOrEmpty(checkpoint.MatchId)
					|| checkpoint.TableCards.Count != 0
					|| !IsEmptyAuthorityAction(checkpoint.AuthorityAction)))
			|| !AuthorityActionStateRules.IsValid(checkpoint.AuthorityAction, checkpoint.PlayerCount)
			|| !AuthorityActionStateRules.IsStableCheckpointState(checkpoint.AuthorityAction))
		{
			error = "checkpoint semantic bounds are invalid";
			return false;
		}

		if (!HasValidRoster(checkpoint)
			|| !HasValidSeatControls(checkpoint)
			|| !HasUniqueSeatIndexes(checkpoint.Roster.Select(entry => entry.SeatIndex), checkpoint.Roster.Count)
			|| !HasUniqueSeatIndexes(checkpoint.SeatControls.Select(entry => entry.SeatIndex), checkpoint.Roster.Count)
			|| checkpoint.LastAcceptedClientActionIds.Any(pair => pair.Key < 0 || pair.Key >= checkpoint.PlayerCount || pair.Value < 0)
			|| checkpoint.LastAcceptedClientActionIds.Count > checkpoint.PlayerCount
			|| checkpoint.SessionMigrationTokens.Count != checkpoint.Roster.Count
			|| checkpoint.SessionMigrationTokens.Any(pair => pair.Key < 0 || pair.Key >= checkpoint.PlayerCount || string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length > 128))
		{
			error = "checkpoint seat indexes or action ids are invalid";
			return false;
		}

		if (!HasValidReconnectClaims(checkpoint))
		{
			error = "checkpoint reconnect claims are invalid";
			return false;
		}

		for (int seat = 0; seat < checkpoint.PlayerCount; seat++)
		{
			if (checkpoint.Scores[seat] < 0
				|| checkpoint.Decks[seat].Count > 208
				|| checkpoint.Hands[seat].Count > 8
				|| checkpoint.ItemHands[seat].Count > 8
				|| checkpoint.Hands[seat].Count + checkpoint.ItemHands[seat].Count > 8
				|| checkpoint.Decks[seat].Any(card => !IsValidCard(card))
				|| checkpoint.Hands[seat].Any(card => !IsValidCard(card))
				|| checkpoint.ItemHands[seat].Any(item => item.Length == 0 || item.Length > 128))
			{
				error = "checkpoint card collections are invalid";
				return false;
			}
		}

		if (checkpoint.TableCards.Count > 256 || checkpoint.TableCards.Any(card => card is null))
		{
			error = "checkpoint table cards are invalid";
			return false;
		}

		if (checkpoint.TableCards.Any(card =>
			(card.CardOwnerPlayerIndex < -1 || card.CardOwnerPlayerIndex >= checkpoint.PlayerCount)
			|| (card.ItemOwnerPlayerIndex < -1 || card.ItemOwnerPlayerIndex >= checkpoint.PlayerCount)
			|| (!string.IsNullOrEmpty(card.ItemOverlayId) && card.ItemOverlayId.Length > 128)
			|| (card.Card is null && (card.IsFaceUp || card.IsBuriedInSand || card.IsSlimeCoated || card.CardOwnerPlayerIndex >= 0))))
		{
			error = "checkpoint table card ownership is invalid";
			return false;
		}

		return true;
	}

	private static bool IsSha256Hash(string? value)
	{
		return value is { Length: 64 }
			&& value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
	}

	private static bool IsEmptyAuthorityAction(AuthorityActionState? state)
	{
		return state is not null
			&& !state.IsActive
			&& AuthorityActionStateRules.IsValid(state);
	}

	private static bool HasValidRoster(NetworkAuthorityCheckpoint checkpoint)
	{
		return checkpoint.Roster.Count >= 1
			&& checkpoint.Roster.Count(entry => entry.IsHost) == 1
			&& checkpoint.Roster.All(entry =>
				!string.IsNullOrWhiteSpace(entry.StableUserId)
				&& entry.StableUserId.Length <= 128
				&& entry.DisplayName.Length <= 128
				&& entry.AvatarId.Length <= 128
				&& entry.PeerId >= 0)
			&& checkpoint.Roster.Select(entry => entry.StableUserId).Distinct(StringComparer.Ordinal).Count() == checkpoint.Roster.Count;
	}

	private static bool HasValidSeatControls(NetworkAuthorityCheckpoint checkpoint)
	{
		var rosterBySeat = checkpoint.Roster.ToDictionary(entry => entry.SeatIndex);
		foreach (NetworkAuthoritySeatControl control in checkpoint.SeatControls)
		{
			if (!rosterBySeat.TryGetValue(control.SeatIndex, out NetworkMigrationRosterEntry? rosterEntry)
				|| !Enum.IsDefined(control.Mode)
				|| control.PeerId < 0
				|| !string.Equals(control.StableUserId, rosterEntry.StableUserId, StringComparison.Ordinal)
				|| string.IsNullOrWhiteSpace(control.ReconnectToken)
				|| control.ReconnectToken.Length > 128
				|| string.IsNullOrWhiteSpace(control.SessionMigrationToken)
				|| control.SessionMigrationToken.Length > 128
				|| !checkpoint.SessionMigrationTokens.TryGetValue(control.SeatIndex, out string? sessionToken)
				|| !string.Equals(control.SessionMigrationToken, sessionToken, StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;
	}

	private static bool HasValidReconnectClaims(NetworkAuthorityCheckpoint checkpoint)
	{
		var controlsBySeat = checkpoint.SeatControls.ToDictionary(control => control.SeatIndex);
		if (checkpoint.ReconnectClaims.Count != checkpoint.Roster.Count
			|| checkpoint.ReconnectClaims.Select(claim => claim.SeatIndex).Distinct().Count() != checkpoint.Roster.Count)
		{
			return false;
		}

		foreach (NetworkAuthorityReconnectClaim claim in checkpoint.ReconnectClaims)
		{
			if (claim.SeatIndex < 0
				|| claim.SeatIndex >= checkpoint.Roster.Count
				|| !string.Equals(claim.MatchId, checkpoint.MatchId, StringComparison.Ordinal)
				|| !controlsBySeat.TryGetValue(claim.SeatIndex, out NetworkAuthoritySeatControl? control)
				|| !string.Equals(claim.ReconnectToken, control.ReconnectToken, StringComparison.Ordinal)
				|| !string.Equals(claim.StableUserId, control.StableUserId, StringComparison.Ordinal)
				|| claim.ClaimedPeerId < 0
				|| claim.ClaimState is not (nameof(ReconnectClaimState.Unclaimed) or nameof(ReconnectClaimState.Claimed))
				|| (claim.ClaimState == nameof(ReconnectClaimState.Unclaimed)
					? claim.ClaimedPeerId != 0
					: claim.ClaimedPeerId <= 0))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsValidCard(NetworkCheckpointCard card)
	{
		return card is not null
			&& !string.IsNullOrWhiteSpace(card.Code)
			&& card.Code.Length <= 64
			&& card.AttachedItemCardIds is not null
			&& card.AttachedItemCardIds.Length <= 1
			&& card.AttachedItemCardIds.All(item => !string.IsNullOrWhiteSpace(item) && item.Length <= 128);
	}

	private static bool HasUniqueSeatIndexes(IEnumerable<int> seatIndexes, int playerCount)
	{
		int[] indexes = seatIndexes.ToArray();
		return indexes.Length == playerCount
			&& indexes.Distinct().Count() == playerCount
			&& indexes.All(index => index >= 0 && index < playerCount);
	}
}
