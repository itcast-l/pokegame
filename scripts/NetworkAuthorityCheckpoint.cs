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

[JsonConverter(typeof(NetworkCheckpointCardJsonConverter))]
public readonly record struct NetworkCheckpointCard
{
	public NetworkCheckpointCard()
	{
		Code = string.Empty;
		AttachedItemCardIds = ImmutableArray<string>.Empty;
	}

	[JsonPropertyName("code")]
	[JsonPropertyOrder(0)]
	public string Code { get; init; }

	[JsonPropertyName("attached_item_card_ids")]
	[JsonPropertyOrder(1)]
	public ImmutableArray<string> AttachedItemCardIds { get; init; }
}

public sealed class NetworkCheckpointCardJsonConverter : JsonConverter<NetworkCheckpointCard>
{
	private static readonly JsonEncodedText CodeProperty = JsonEncodedText.Encode("code");
	private static readonly JsonEncodedText AttachedItemsProperty =
		JsonEncodedText.Encode("attached_item_card_ids");

	public override NetworkCheckpointCard Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException();
		}

		string? code = null;
		ImmutableArray<string> attachedItemCardIds = default;
		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
			{
				return new NetworkCheckpointCard
				{
					Code = code!,
					AttachedItemCardIds = attachedItemCardIds,
				};
			}
			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				throw new JsonException();
			}

			if (reader.ValueTextEquals("code"))
			{
				reader.Read();
				code = reader.TokenType == JsonTokenType.Null
					? null
					: reader.GetString();
				continue;
			}
			if (reader.ValueTextEquals("attached_item_card_ids"))
			{
				reader.Read();
				attachedItemCardIds = ReadAttachedItemCardIds(ref reader);
				continue;
			}

			reader.Read();
			reader.Skip();
		}

		throw new JsonException();
	}

	public override void Write(
		Utf8JsonWriter writer,
		NetworkCheckpointCard value,
		JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteString(CodeProperty, value.Code);
		writer.WritePropertyName(AttachedItemsProperty);
		writer.WriteStartArray();
		foreach (string itemCardId in value.AttachedItemCardIds)
		{
			writer.WriteStringValue(itemCardId);
		}
		writer.WriteEndArray();
		writer.WriteEndObject();
	}

	private static ImmutableArray<string> ReadAttachedItemCardIds(
		ref Utf8JsonReader reader)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return default;
		}
		if (reader.TokenType != JsonTokenType.StartArray)
		{
			throw new JsonException();
		}

		var builder = ImmutableArray.CreateBuilder<string>();
		while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
		{
			if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.Null))
			{
				throw new JsonException();
			}
			builder.Add(reader.GetString()!);
		}
		if (reader.TokenType != JsonTokenType.EndArray)
		{
			throw new JsonException();
		}
		return builder.ToImmutable();
	}
}

[JsonConverter(typeof(NetworkCheckpointTableCardJsonConverter))]
public readonly record struct NetworkCheckpointTableCard
{
	public NetworkCheckpointTableCard()
	{
		Card = null;
		IsFaceUp = false;
		IsBuriedInSand = false;
		IsSlimeCoated = false;
		CardOwnerPlayerIndex = -1;
		RemoveCardOnCapture = false;
		ItemOverlayId = string.Empty;
		ItemOwnerPlayerIndex = -1;
	}

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
	public int CardOwnerPlayerIndex { get; init; }

	[JsonPropertyName("remove_card_on_capture")]
	[JsonPropertyOrder(5)]
	public bool RemoveCardOnCapture { get; init; }

	[JsonPropertyName("item_overlay_id")]
	[JsonPropertyOrder(6)]
	public string ItemOverlayId { get; init; }

	[JsonPropertyName("item_owner_player_index")]
	[JsonPropertyOrder(7)]
	public int ItemOwnerPlayerIndex { get; init; }
}

public sealed class NetworkCheckpointTableCardJsonConverter : JsonConverter<NetworkCheckpointTableCard>
{
	private static readonly JsonEncodedText CardProperty = JsonEncodedText.Encode("card");
	private static readonly JsonEncodedText FaceUpProperty = JsonEncodedText.Encode("is_face_up");
	private static readonly JsonEncodedText BuriedProperty = JsonEncodedText.Encode("is_buried_in_sand");
	private static readonly JsonEncodedText SlimeProperty = JsonEncodedText.Encode("is_slime_coated");
	private static readonly JsonEncodedText CardOwnerProperty = JsonEncodedText.Encode("card_owner_player_index");
	private static readonly JsonEncodedText RemoveOnCaptureProperty = JsonEncodedText.Encode("remove_card_on_capture");
	private static readonly JsonEncodedText ItemOverlayProperty = JsonEncodedText.Encode("item_overlay_id");
	private static readonly JsonEncodedText ItemOwnerProperty = JsonEncodedText.Encode("item_owner_player_index");

	public override NetworkCheckpointTableCard Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException();
		}

		NetworkCheckpointCard? card = null;
		bool isFaceUp = false;
		bool isBuried = false;
		bool isSlimeCoated = false;
		int cardOwner = -1;
		bool removeOnCapture = false;
		string? itemOverlayId = string.Empty;
		int itemOwner = -1;
		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
			{
				return new NetworkCheckpointTableCard
				{
					Card = card,
					IsFaceUp = isFaceUp,
					IsBuriedInSand = isBuried,
					IsSlimeCoated = isSlimeCoated,
					CardOwnerPlayerIndex = cardOwner,
					RemoveCardOnCapture = removeOnCapture,
					ItemOverlayId = itemOverlayId!,
					ItemOwnerPlayerIndex = itemOwner,
				};
			}
			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				throw new JsonException();
			}

			if (reader.ValueTextEquals("card"))
			{
				reader.Read();
				card = reader.TokenType == JsonTokenType.Null
					? null
					: JsonSerializer.Deserialize<NetworkCheckpointCard>(ref reader, options);
			}
			else if (reader.ValueTextEquals("is_face_up"))
			{
				reader.Read();
				isFaceUp = reader.GetBoolean();
			}
			else if (reader.ValueTextEquals("is_buried_in_sand"))
			{
				reader.Read();
				isBuried = reader.GetBoolean();
			}
			else if (reader.ValueTextEquals("is_slime_coated"))
			{
				reader.Read();
				isSlimeCoated = reader.GetBoolean();
			}
			else if (reader.ValueTextEquals("card_owner_player_index"))
			{
				reader.Read();
				cardOwner = reader.GetInt32();
			}
			else if (reader.ValueTextEquals("remove_card_on_capture"))
			{
				reader.Read();
				removeOnCapture = reader.GetBoolean();
			}
			else if (reader.ValueTextEquals("item_overlay_id"))
			{
				reader.Read();
				itemOverlayId = reader.TokenType == JsonTokenType.Null
					? null
					: reader.GetString();
			}
			else if (reader.ValueTextEquals("item_owner_player_index"))
			{
				reader.Read();
				itemOwner = reader.GetInt32();
			}
			else
			{
				reader.Read();
				reader.Skip();
			}
		}

		throw new JsonException();
	}

	public override void Write(
		Utf8JsonWriter writer,
		NetworkCheckpointTableCard value,
		JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WritePropertyName(CardProperty);
		if (value.Card.HasValue)
		{
			JsonSerializer.Serialize(writer, value.Card.Value, options);
		}
		else
		{
			writer.WriteNullValue();
		}
		writer.WriteBoolean(FaceUpProperty, value.IsFaceUp);
		writer.WriteBoolean(BuriedProperty, value.IsBuriedInSand);
		writer.WriteBoolean(SlimeProperty, value.IsSlimeCoated);
		writer.WriteNumber(CardOwnerProperty, value.CardOwnerPlayerIndex);
		writer.WriteBoolean(RemoveOnCaptureProperty, value.RemoveCardOnCapture);
		writer.WriteString(ItemOverlayProperty, value.ItemOverlayId);
		writer.WriteNumber(ItemOwnerProperty, value.ItemOwnerPlayerIndex);
		writer.WriteEndObject();
	}
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

	[JsonPropertyName("winner_player_index")]
	[JsonPropertyOrder(24)]
	public int WinnerPlayerIndex { get; init; } = -1;

	[JsonPropertyName("match_outcome_kind")]
	[JsonPropertyOrder(25)]
	public MatchOutcomeKind MatchOutcomeKind { get; init; } = MatchOutcomeKind.InProgress;

	[JsonPropertyName("opening_deal_completed")]
	[JsonPropertyOrder(26)]
	public bool OpeningDealCompleted { get; init; } = true;

	[JsonPropertyName("current_turn_player_index")]
	[JsonPropertyOrder(27)]
	public int CurrentTurnPlayerIndex { get; init; }

	[JsonPropertyName("turn_sequence_generation")]
	[JsonPropertyOrder(28)]
	public int TurnSequenceGeneration { get; init; }

	[JsonPropertyName("completed_turns_since_question_refresh")]
	[JsonPropertyOrder(29)]
	public int CompletedTurnsSinceQuestionRefresh { get; init; }

	[JsonPropertyName("question_refresh_anchor_player_index")]
	[JsonPropertyOrder(30)]
	public int QuestionRefreshAnchorPlayerIndex { get; init; } = -1;

	[JsonPropertyName("last_authority_action_id")]
	[JsonPropertyOrder(31)]
	public long LastAuthorityActionId { get; init; }

	[JsonPropertyName("authority_action")]
	[JsonPropertyOrder(32)]
	public AuthorityActionState AuthorityAction { get; init; } = AuthorityActionState.Empty;

	[JsonPropertyName("rng_seed")]
	[JsonPropertyOrder(33)]
	public long RngSeed { get; init; }

	[JsonPropertyName("rng_state")]
	[JsonPropertyOrder(34)]
	public string RngState { get; init; } = string.Empty;

	[JsonPropertyName("snapshot_id")]
	[JsonPropertyOrder(35)]
	public int SnapshotId { get; init; }

	[JsonPropertyName("authority_epoch")]
	[JsonPropertyOrder(36)]
	public long AuthorityEpoch { get; init; }

	[JsonPropertyName("checkpoint_id")]
	[JsonPropertyOrder(37)]
	public long CheckpointId { get; init; }

	[JsonPropertyName("reconnect_claims")]
	[JsonPropertyOrder(38)]
	public List<NetworkAuthorityReconnectClaim> ReconnectClaims { get; init; } = new();

	[JsonIgnore]
	public string PayloadHash { get; init; } = string.Empty;
}

internal static class NetworkAuthorityCheckpointRuleAdapter
{
	internal static NetworkAuthorityCheckpoint ApplyRuleSnapshot(
		NetworkAuthorityCheckpoint checkpoint,
		MatchStateSnapshot snapshot,
		bool recordTelemetry = true)
	{
		ArgumentNullException.ThrowIfNull(checkpoint);
		ArgumentNullException.ThrowIfNull(snapshot);
		if (!recordTelemetry)
		{
			return ApplyRuleSnapshotCore(checkpoint, snapshot, out _);
		}

		long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
		long startedAt = Stopwatch.GetTimestamp();
		int projectedEntryCount = 0;
		try
		{
			return ApplyRuleSnapshotCore(
				checkpoint,
				snapshot,
				out projectedEntryCount);
		}
		finally
		{
			long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
			long allocatedBytes =
				GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
			AuthorityCheckpointTelemetry.RecordProjection(
				projectedEntryCount,
				allocatedBytes,
				elapsedTicks);
		}
	}

	private static NetworkAuthorityCheckpoint ApplyRuleSnapshotCore(
		NetworkAuthorityCheckpoint checkpoint,
		MatchStateSnapshot snapshot,
		out int projectedEntryCount)
	{
		List<List<NetworkCheckpointCard>> decks =
			ExportCardLists(snapshot.Players, exportDecks: true);
		List<List<NetworkCheckpointCard>> hands =
			ExportCardLists(snapshot.Players, exportDecks: false);
		List<List<string>> itemHands = ExportItemHands(snapshot.Players);
		List<NetworkCheckpointTableCard> tableCards =
			ExportTableCards(snapshot.TableCards);
		projectedEntryCount = CountProjectedEntries(
			decks,
			hands,
			itemHands,
			tableCards.Count);
		return checkpoint with
		{
			PlayerCount = snapshot.PlayerCount,
			WinningScore = snapshot.WinningScore,
			Decks = decks,
			Hands = hands,
			ItemHands = itemHands,
			TableCards = tableCards,
			Scores = ExportScores(snapshot.Players),
			MatchOutcomeKind = ToNetworkMatchOutcome(snapshot.Outcome),
			WinnerPlayerIndex = snapshot.WinnerSeat,
			CurrentTurnPlayerIndex = snapshot.CurrentTurnSeat,
			TurnSequenceGeneration = snapshot.TurnGeneration,
			CompletedTurnsSinceQuestionRefresh =
				snapshot.CompletedTurnsSinceQuestionRefresh,
			QuestionRefreshAnchorPlayerIndex = snapshot.QuestionRefreshAnchorSeat,
			RngSeed = unchecked((long)snapshot.RandomState.Seed),
			RngState = snapshot.RandomState.State.ToString(CultureInfo.InvariantCulture),
		};
	}

	internal static int CountProjectedEntries(NetworkAuthorityCheckpoint checkpoint)
	{
		ArgumentNullException.ThrowIfNull(checkpoint);
		return CountProjectedEntries(
			checkpoint.Decks,
			checkpoint.Hands,
			checkpoint.ItemHands,
			checkpoint.TableCards.Count);
	}

	private static int CountProjectedEntries(
		IReadOnlyList<List<NetworkCheckpointCard>> decks,
		IReadOnlyList<List<NetworkCheckpointCard>> hands,
		IReadOnlyList<List<string>> itemHands,
		int tableCardCount)
	{
		int count = tableCardCount;
		for (int seat = 0; seat < decks.Count; seat++)
		{
			count = checked(
				count
				+ decks[seat].Count
				+ hands[seat].Count
				+ itemHands[seat].Count);
		}
		return count;
	}

	internal static bool TryCreateRuleSnapshot(
		NetworkAuthorityCheckpoint checkpoint,
		int lifecycleGeneration,
		int turnGeneration,
		ImmutableArray<PlayerStateSnapshot> players,
		ImmutableArray<TableCardState> tableCards,
		RuleRandomState fallbackRandomState,
		ImmutableArray<PendingAttachmentRewardState> pendingAttachmentRewards,
		out MatchStateSnapshot snapshot,
		out string error)
	{
		snapshot = null!;
		error = string.Empty;
		if (checkpoint is null
			|| lifecycleGeneration <= 0
			|| turnGeneration <= 0
			|| players.IsDefault
			|| players.Length != checkpoint.PlayerCount
			|| tableCards.IsDefault
			|| pendingAttachmentRewards.IsDefault)
		{
			error = "checkpoint rule state is invalid";
			return false;
		}

		RuleRandomState randomState = fallbackRandomState;
		if (checkpoint.RngSeed != 0)
		{
			if (!ulong.TryParse(
					checkpoint.RngState,
					NumberStyles.None,
					CultureInfo.InvariantCulture,
					out ulong rngState)
				|| rngState == 0)
			{
				error = "checkpoint RNG state is invalid";
				return false;
			}

			randomState = new RuleRandomState(
				unchecked((ulong)checkpoint.RngSeed),
				rngState);
		}

		if (randomState.State == 0)
		{
			error = "checkpoint RNG state is invalid";
			return false;
		}

		snapshot = new MatchStateSnapshot(
			MatchStateSnapshot.CurrentSchemaVersion,
			checkpoint.CheckpointId,
			lifecycleGeneration,
			turnGeneration,
			checkpoint.PlayerCount,
			checkpoint.WinningScore,
			checkpoint.CurrentTurnPlayerIndex,
			ToRuleMatchOutcome(checkpoint.MatchOutcomeKind),
			checkpoint.WinnerPlayerIndex,
			players,
			tableCards,
			randomState,
			PendingPresentationBarrierId: 0,
			AppliedActionIds: ImmutableArray<long>.Empty,
			PendingAttachmentRewards: pendingAttachmentRewards,
			CompletedTurnsSinceQuestionRefresh:
				checkpoint.CompletedTurnsSinceQuestionRefresh,
			QuestionRefreshAnchorSeat:
				checkpoint.QuestionRefreshAnchorPlayerIndex);
		return true;
	}

	internal static MatchOutcomeKind ToNetworkMatchOutcome(
		Deckland.Rules.MatchOutcome outcome)
	{
		return outcome switch
		{
			Deckland.Rules.MatchOutcome.Won => MatchOutcomeKind.Winner,
			Deckland.Rules.MatchOutcome.Draw => MatchOutcomeKind.Draw,
			_ => MatchOutcomeKind.InProgress,
		};
	}

	private static Deckland.Rules.MatchOutcome ToRuleMatchOutcome(
		MatchOutcomeKind outcome)
	{
		return outcome switch
		{
			MatchOutcomeKind.Winner => Deckland.Rules.MatchOutcome.Won,
			MatchOutcomeKind.Draw => Deckland.Rules.MatchOutcome.Draw,
			_ => Deckland.Rules.MatchOutcome.InProgress,
		};
	}

	private static List<List<NetworkCheckpointCard>> ExportCardLists(
		ImmutableArray<PlayerStateSnapshot> players,
		bool exportDecks)
	{
		var result = new List<List<NetworkCheckpointCard>>(players.Length);
		foreach (PlayerStateSnapshot player in players)
		{
			ImmutableArray<LogicalCardState> source = exportDecks
				? player.Deck
				: player.Hand;
			var cards = new List<NetworkCheckpointCard>(source.Length);
			foreach (LogicalCardState card in source)
			{
				cards.Add(ToCheckpointCard(card));
			}
			result.Add(cards);
		}

		return result;
	}

	private static List<List<string>> ExportItemHands(
		ImmutableArray<PlayerStateSnapshot> players)
	{
		var result = new List<List<string>>(players.Length);
		foreach (PlayerStateSnapshot player in players)
		{
			var items = new List<string>(player.ItemHand.Length);
			foreach (LogicalItemState item in player.ItemHand)
			{
				items.Add(item.Id);
			}
			result.Add(items);
		}

		return result;
	}

	private static int[] ExportScores(ImmutableArray<PlayerStateSnapshot> players)
	{
		var scores = new int[players.Length];
		for (int seat = 0; seat < players.Length; seat++)
		{
			scores[seat] = players[seat].Score;
		}
		return scores;
	}

	private static NetworkCheckpointCard ToCheckpointCard(LogicalCardState card)
	{
		return new NetworkCheckpointCard
		{
			Code = card.Code,
			AttachedItemCardIds = card.NormalizedAttachedEffectIds,
		};
	}

	private static List<NetworkCheckpointTableCard> ExportTableCards(
		ImmutableArray<TableCardState> tableCards)
	{
		var result = new List<NetworkCheckpointTableCard>(tableCards.Length);
		foreach (TableCardState tableCard in tableCards)
		{
			result.Add(new NetworkCheckpointTableCard
			{
				Card = tableCard.Card.HasValue
					? ToCheckpointCard(tableCard.Card.Value)
					: null,
				IsFaceUp = tableCard.IsFaceUp,
				IsBuriedInSand = tableCard.IsBuried,
				IsSlimeCoated = tableCard.IsScoreSuppressed,
				CardOwnerPlayerIndex = tableCard.CardOwnerSeat,
				RemoveCardOnCapture = tableCard.RemoveOnCapture,
				ItemOverlayId = tableCard.ItemOverlay?.Id ?? string.Empty,
				ItemOwnerPlayerIndex = tableCard.ItemOwnerSeat,
			});
		}

		return result;
	}
}

public static class NetworkAuthorityCheckpointCodec
{
	public const int SchemaVersion = NetworkProtocol.AuthorityCheckpointSchemaVersion;
	public const int MaxPayloadBytes = NetworkProtocol.MaxAuthorityCheckpointBytes;
	public const string EncodeInvalidError = "checkpoint_encode_invalid";
	public const string EncodeFailedError = "checkpoint_encode_failed";
	public const string PayloadSizeError = "checkpoint_payload_size";
	public const string HashMismatchError = "checkpoint_hash_mismatch";
	public const string DecodeInvalidError = "checkpoint_decode_invalid";
	private const int MaxJsonDepth = 64;
	private const int MaxCardPileCount = 208;
	private const int MaxHandCount = 8;
	private const int MaxTableCardCount = 256;
	private const int MaxScore = 1_000_000;
	private const int MaxRefreshTurnCount = 1_000_000;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
		PropertyNamingPolicy = null,
		WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.Never,
		MaxDepth = MaxJsonDepth,
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
		long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
		long startedAt = Stopwatch.GetTimestamp();
		int encodedBytes = 0;
		try
		{
			if (!Validate(checkpoint, out _))
			{
				error = EncodeInvalidError;
				return false;
			}

			payload = JsonSerializer.SerializeToUtf8Bytes(checkpoint, JsonOptions);
			if (payload.Length > MaxPayloadBytes)
			{
				error = PayloadSizeError;
				payload = Array.Empty<byte>();
				return false;
			}

			hash = ComputeHash(payload);
			encodedBytes = payload.Length;
			return true;
		}
		catch (Exception)
		{
			payload = Array.Empty<byte>();
			hash = string.Empty;
			error = EncodeFailedError;
			return false;
		}
		finally
		{
			long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
			long allocatedBytes =
				GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
			AuthorityCheckpointTelemetry.RecordEncoding(
				encodedBytes,
				allocatedBytes,
				elapsedTicks);
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
			error = PayloadSizeError;
			return false;
		}

		try
		{
			string actualHash = ComputeHash(payload);
			if (!IsSha256Hash(expectedHash)
				|| !string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
			{
				error = HashMismatchError;
				return false;
			}

			NetworkAuthorityCheckpoint? decoded = JsonSerializer.Deserialize<NetworkAuthorityCheckpoint>(payload, JsonOptions);
			if (decoded is null || !Validate(decoded, out _))
			{
				error = DecodeInvalidError;
				return false;
			}

			checkpoint = decoded with { PayloadHash = actualHash };
			return true;
		}
		catch (Exception)
		{
			checkpoint = new NetworkAuthorityCheckpoint();
			error = DecodeInvalidError;
			return false;
		}
	}

	public static string ComputeHash(ReadOnlySpan<byte> payload)
	{
		return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
	}

	public static bool Validate(NetworkAuthorityCheckpoint checkpoint, out string error)
	{
		try
		{
			if (!ValidateCore(checkpoint, out error))
			{
				return false;
			}

			error = string.Empty;
			return true;
		}
		catch (Exception)
		{
			error = DecodeInvalidError;
			return false;
		}
	}

	private static bool ValidateCore(NetworkAuthorityCheckpoint checkpoint, out string error)
	{
		error = string.Empty;
		if (checkpoint is null
			|| checkpoint.SchemaVersion != SchemaVersion
			|| checkpoint.ProtocolVersion != NetworkProtocol.CurrentVersion
			|| checkpoint.Phase is not (NetworkAuthorityPhase.Lobby or NetworkAuthorityPhase.InGame)
			|| checkpoint.PlayerCount < NetworkSessionDefaults.MinPlayers
			|| checkpoint.PlayerCount > NetworkProtocol.MaxAuthorityCheckpointPlayers
			|| !IsBoundedString(checkpoint.SessionId, NetworkProtocol.MaxSessionIdLength, allowEmpty: false)
			|| !IsBoundedString(checkpoint.MatchId, NetworkProtocol.MaxMatchIdLength)
			|| !IsBoundedString(checkpoint.RoomName, 128)
			|| !RoomPasswordRules.IsVerifierValid(new RoomPasswordVerifier
			{
				Version = checkpoint.RoomPasswordVerifierVersion,
				Iterations = checkpoint.RoomPasswordVerifierIterations,
				Salt = checkpoint.RoomPasswordVerifierSalt,
				Verifier = checkpoint.RoomPasswordVerifier,
			})
			|| !Enum.IsDefined(checkpoint.Transport)
			|| checkpoint.WinningScore is not (50 or 100 or 200)
			|| checkpoint.Scores is null
			|| checkpoint.Roster is null
			|| checkpoint.SeatControls is null
			|| checkpoint.LastAcceptedClientActionIds is null
			|| checkpoint.Decks is null
			|| checkpoint.Hands is null
			|| checkpoint.ItemHands is null
			|| checkpoint.TableCards is null
			|| checkpoint.ReconnectClaims is null
			|| checkpoint.AuthorityAction is null
			|| !IsSha256Hash(checkpoint.ManifestHash)
			|| !IsBoundedString(checkpoint.RngState, 32, allowEmpty: false)
			|| !ulong.TryParse(checkpoint.RngState, out ulong rngState)
			|| rngState == 0
			|| checkpoint.AuthorityEpoch < 0
			|| checkpoint.LastAuthorityActionId < 0
			|| checkpoint.LastAuthorityActionId < checkpoint.AuthorityAction.ActionId
			|| checkpoint.SnapshotId < 0
			|| checkpoint.CheckpointId < 0
			|| checkpoint.CurrentTurnPlayerIndex < 0
			|| checkpoint.CurrentTurnPlayerIndex >= checkpoint.PlayerCount
			|| checkpoint.TurnSequenceGeneration < 0
			|| checkpoint.TurnSequenceGeneration == int.MaxValue
			|| checkpoint.CompletedTurnsSinceQuestionRefresh < 0
			|| checkpoint.CompletedTurnsSinceQuestionRefresh > MaxRefreshTurnCount
			|| checkpoint.QuestionRefreshAnchorPlayerIndex < -1
			|| checkpoint.QuestionRefreshAnchorPlayerIndex >= checkpoint.PlayerCount
			|| checkpoint.WinnerPlayerIndex < -1
			|| checkpoint.WinnerPlayerIndex >= checkpoint.PlayerCount
			|| !MatchReadModel.IsSerializedOutcomeValid(checkpoint.MatchOutcomeKind, checkpoint.WinnerPlayerIndex, checkpoint.PlayerCount)
			|| !AuthorityActionStateRules.IsValid(checkpoint.AuthorityAction, checkpoint.PlayerCount)
			|| !AuthorityActionStateRules.IsStableCheckpointState(checkpoint.AuthorityAction))
		{
			error = "checkpoint_top_level";
			return false;
		}

		int rosterCount = checkpoint.Roster.Count;
		if (rosterCount < 1
			|| rosterCount > checkpoint.PlayerCount
			|| checkpoint.SeatControls.Count != rosterCount
			|| checkpoint.ReconnectClaims.Count != rosterCount
			|| checkpoint.Scores.Length != checkpoint.PlayerCount
			|| checkpoint.Decks.Count != checkpoint.PlayerCount
			|| checkpoint.Hands.Count != checkpoint.PlayerCount
			|| checkpoint.ItemHands.Count != checkpoint.PlayerCount
			|| checkpoint.TableCards.Count > MaxTableCardCount
			|| checkpoint.LastAcceptedClientActionIds.Count > checkpoint.PlayerCount)
		{
			error = "checkpoint_collection_shape";
			return false;
		}

		if (checkpoint.Phase == NetworkAuthorityPhase.InGame)
		{
			if (string.IsNullOrWhiteSpace(checkpoint.MatchId)
				|| rosterCount != checkpoint.PlayerCount
				|| !checkpoint.OpeningDealCompleted
				|| checkpoint.Roster.Any(entry => entry is null || !entry.CredentialsAcknowledged))
				{
					error = "checkpoint_ingame_phase";
					return false;
			}
		}
		else if (!string.IsNullOrEmpty(checkpoint.MatchId)
			|| checkpoint.TableCards.Count != 0
			|| checkpoint.LastAuthorityActionId != 0
			|| !IsEmptyAuthorityAction(checkpoint.AuthorityAction))
		{
			error = "checkpoint_lobby_phase";
			return false;
		}

		var rosterBySeat = new NetworkMigrationRosterEntry?[checkpoint.PlayerCount];
		var playerIds = new HashSet<string>(StringComparer.Ordinal);
		int hostCount = 0;
		for (int index = 0; index < rosterCount; index++)
		{
			NetworkMigrationRosterEntry? entry = checkpoint.Roster[index];
			if (entry is null
				|| entry.SeatIndex < 0
				|| entry.SeatIndex >= checkpoint.PlayerCount
				|| rosterBySeat[entry.SeatIndex] is not null
				|| !IsBoundedString(entry.PlayerSessionId, NetworkProtocol.MaxPlayerIdLength, allowEmpty: false)
				|| !IsBoundedString(entry.DisplayName, 128)
				|| !IsBoundedString(entry.AvatarId, 128)
				|| entry.PeerId < 0
				|| !playerIds.Add(entry.PlayerSessionId))
				{
					error = $"checkpoint_roster_{index}";
					return false;
			}

			rosterBySeat[entry.SeatIndex] = entry;
			hostCount += entry.IsHost ? 1 : 0;
		}

		if (hostCount != 1)
		{
			error = "checkpoint_roster_host_count";
			return false;
		}
		for (int seat = 0; seat < rosterCount; seat++)
		{
			if (rosterBySeat[seat] is null)
				{
					error = $"checkpoint_roster_missing_seat_{seat}";
					return false;
			}
		}

		var controlsBySeat = new NetworkAuthoritySeatControl?[checkpoint.PlayerCount];
		foreach (NetworkAuthoritySeatControl? control in checkpoint.SeatControls)
		{
			if (control is null
				|| control.SeatIndex < 0
				|| control.SeatIndex >= rosterCount
				|| controlsBySeat[control.SeatIndex] is not null
				|| !Enum.IsDefined(control.Mode)
				|| control.PeerId < 0
				|| !IsBoundedString(control.PlayerSessionId, NetworkProtocol.MaxPlayerIdLength, allowEmpty: false)
				|| !SeatCredentialRules.IsVerifierValid(control.CredentialVerifier)
				|| control.CredentialGeneration < SeatCredentialRules.InitialGeneration
				|| control.PendingCredentialGeneration < 0
				|| control.PendingCredentialPeerId < 0
				|| !string.Equals(control.PlayerSessionId, rosterBySeat[control.SeatIndex]!.PlayerSessionId, StringComparison.Ordinal)
				|| !HasValidPendingCredentialState(control))
				{
					error = $"checkpoint_control_{control?.SeatIndex ?? -1}";
					return false;
			}

			controlsBySeat[control.SeatIndex] = control;
		}

		var claimSeen = new bool[rosterCount];
		foreach (NetworkAuthorityReconnectClaim? claim in checkpoint.ReconnectClaims)
		{
			if (claim is null
				|| claim.SeatIndex < 0
				|| claim.SeatIndex >= rosterCount
				|| claimSeen[claim.SeatIndex]
				|| !IsBoundedString(claim.MatchId, NetworkProtocol.MaxMatchIdLength)
				|| !string.Equals(claim.MatchId, checkpoint.MatchId, StringComparison.Ordinal)
				|| claim.CredentialGeneration < SeatCredentialRules.InitialGeneration
				|| !IsBoundedString(claim.PlayerSessionId, NetworkProtocol.MaxPlayerIdLength, allowEmpty: false)
				|| claim.CredentialGeneration != controlsBySeat[claim.SeatIndex]!.CredentialGeneration
				|| !string.Equals(claim.PlayerSessionId, controlsBySeat[claim.SeatIndex]!.PlayerSessionId, StringComparison.Ordinal)
				|| claim.ClaimedPeerId < 0
				|| claim.ClaimState is not (nameof(ReconnectClaimState.Unclaimed) or nameof(ReconnectClaimState.Claimed))
				|| (claim.ClaimState == nameof(ReconnectClaimState.Unclaimed) ? claim.ClaimedPeerId != 0 : claim.ClaimedPeerId <= 0))
				{
					error = $"checkpoint_claim_{claim?.SeatIndex ?? -1}";
					return false;
			}

			claimSeen[claim.SeatIndex] = true;
		}

		foreach (KeyValuePair<int, long> actionId in checkpoint.LastAcceptedClientActionIds)
		{
			if (actionId.Key < 0 || actionId.Key >= checkpoint.PlayerCount || actionId.Value < 0)
				{
					error = $"checkpoint_action_id_{actionId.Key}";
					return false;
			}
		}

		for (int seat = 0; seat < checkpoint.PlayerCount; seat++)
		{
			List<NetworkCheckpointCard>? deck = checkpoint.Decks[seat];
			List<NetworkCheckpointCard>? hand = checkpoint.Hands[seat];
			List<string>? itemHand = checkpoint.ItemHands[seat];
			if (deck is null
				|| hand is null
				|| itemHand is null
				|| checkpoint.Scores[seat] < 0
				|| checkpoint.Scores[seat] > MaxScore
				|| deck.Count > MaxCardPileCount
				|| hand.Count > MaxHandCount
				|| itemHand.Count > MaxHandCount
				|| hand.Count + itemHand.Count > MaxHandCount
				|| !HasValidCards(deck)
				|| !HasValidCards(hand)
				|| !HasValidItemIds(itemHand))
				{
					error = $"checkpoint_pile_{seat}";
					return false;
			}
		}

		foreach (NetworkCheckpointTableCard tableCard in checkpoint.TableCards)
		{
			if (tableCard.CardOwnerPlayerIndex < -1
				|| tableCard.CardOwnerPlayerIndex >= checkpoint.PlayerCount
				|| tableCard.ItemOwnerPlayerIndex < -1
				|| tableCard.ItemOwnerPlayerIndex >= checkpoint.PlayerCount
				|| !IsBoundedString(tableCard.ItemOverlayId, 128)
				|| (tableCard.Card is not null && !IsValidCard(tableCard.Card))
					|| (tableCard.Card is null && (string.IsNullOrEmpty(tableCard.ItemOverlayId)
						|| tableCard.IsBuriedInSand
						|| tableCard.IsSlimeCoated
						|| tableCard.CardOwnerPlayerIndex >= 0
						|| tableCard.RemoveCardOnCapture))
					|| (string.IsNullOrEmpty(tableCard.ItemOverlayId)
						? tableCard.ItemOwnerPlayerIndex != -1
						: tableCard.ItemOwnerPlayerIndex < -1))
				{
					error = "checkpoint_table_card";
					return false;
			}
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

	private static bool IsBoundedString(string? value, int maximumLength, bool allowEmpty = true)
	{
		return value is not null
			&& value.Length <= maximumLength
			&& (allowEmpty || !string.IsNullOrWhiteSpace(value));
	}

	private static bool HasValidCards(List<NetworkCheckpointCard> cards)
	{
		foreach (NetworkCheckpointCard? card in cards)
		{
			if (!IsValidCard(card))
			{
				return false;
			}
		}

		return true;
	}

	private static bool HasValidPendingCredentialState(NetworkAuthoritySeatControl control)
	{
		bool hasPending = control.PendingCredentialGeneration > 0
			|| !string.IsNullOrEmpty(control.PendingCredentialVerifier)
			|| control.PendingCredentialPeerId > 0;
		if (!hasPending)
		{
			return control.PendingCredentialGeneration == 0
				&& string.IsNullOrEmpty(control.PendingCredentialVerifier)
				&& control.PendingCredentialPeerId == 0;
		}

		return control.PendingCredentialGeneration == control.CredentialGeneration + 1
			&& control.CredentialGeneration < long.MaxValue
			&& SeatCredentialRules.IsVerifierValid(control.PendingCredentialVerifier)
			&& control.PendingCredentialPeerId > 0;
	}

	private static bool HasValidItemIds(List<string> itemIds)
	{
		foreach (string? itemId in itemIds)
		{
			if (!IsBoundedString(itemId, 128, allowEmpty: false))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsValidCard(NetworkCheckpointCard? card)
	{
		if (!card.HasValue)
		{
			return false;
		}

		NetworkCheckpointCard value = card.Value;
		return IsBoundedString(value.Code, 64, allowEmpty: false)
			&& !value.AttachedItemCardIds.IsDefault
			&& value.AttachedItemCardIds.Length <= 1
			&& HasValidAttachmentIds(value.AttachedItemCardIds);
	}

	private static bool HasValidAttachmentIds(ImmutableArray<string> attachmentIds)
	{
		foreach (string? attachmentId in attachmentIds)
		{
			if (!IsBoundedString(attachmentId, 128, allowEmpty: false))
			{
				return false;
			}
		}

		return true;
	}
}
