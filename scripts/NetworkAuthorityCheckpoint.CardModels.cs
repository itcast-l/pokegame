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
