using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Godot;

public static class NetworkBoardViewFingerprint
{
	private static readonly string[] TopLevelKeyOrder =
	{
		"protocol_version",
		"session_id",
		"match_id",
		"authority_epoch",
		"checkpoint_id",
		"snapshot_id",
		"turn_sequence_generation",
		"current_turn_player_index",
		"opening_dice_roll_result",
		"match_outcome_kind",
		"winner_player_index",
		"scores",
		"deck_counts",
		"hand_counts",
		"item_hand_counts",
		"own_hand_cards",
		"own_item_hand_cards",
		"table_cards",
		"pending_selection_mode",
		"pending_selection_owner",
		"pending_item_card_id",
		"pending_turn_sequence_generation",
		"opening_deal_animation_playing",
		"opening_deal_completed",
		"seat_control_states",
		"authority_action",
	};

	private static readonly string[] NestedKeyOrder =
	{
		"code",
		"attached_item_card_ids",
		"id",
		"is_face_up",
		"is_buried_in_sand",
		"is_slime_coated",
		"card_owner_player_index",
		"remove_card_on_capture",
		"item_overlay_id",
		"item_owner_player_index",
		"seat_index",
		"mode",
		"peer_id",
		"player_session_id",
	};

	public static string Compute(Godot.Collections.Dictionary snapshot)
	{
		string canonical = Canonicalize(snapshot);
		byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
		return Convert.ToHexString(digest).ToLowerInvariant();
	}

	private static string Canonicalize(Godot.Collections.Dictionary snapshot)
	{
		var canonical = new StringBuilder(4096);
		canonical.Append("network-board-view-v1\n");
		foreach (string key in TopLevelKeyOrder)
		{
			canonical.Append(key).Append('=');
			if (snapshot.TryGetValue(key, out Variant value))
			{
				AppendVariant(canonical, value, NestedKeyOrder);
			}
			else
			{
				canonical.Append("<missing>");
			}

			canonical.Append('\n');
		}

		return canonical.ToString();
	}

	private static void AppendVariant(StringBuilder builder, Variant value, IReadOnlyList<string> nestedKeyOrder)
	{
		switch (value.VariantType)
		{
			case Variant.Type.Nil:
				builder.Append("nil");
				return;
			case Variant.Type.Bool:
				builder.Append(value.AsBool() ? "true" : "false");
				return;
			case Variant.Type.Int:
				builder.Append(value.AsInt64());
				return;
			case Variant.Type.Float:
				builder.Append(value.AsDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture));
				return;
			case Variant.Type.String:
				AppendString(builder, value.AsString());
				return;
			case Variant.Type.Array:
				Godot.Collections.Array array = value.AsGodotArray();
				builder.Append('[');
				for (int index = 0; index < array.Count; index++)
				{
					if (index > 0)
					{
						builder.Append(',');
					}

					AppendVariant(builder, array[index], nestedKeyOrder);
				}

				builder.Append(']');
				return;
			case Variant.Type.Dictionary:
				AppendDictionary(builder, value.AsGodotDictionary(), nestedKeyOrder);
				return;
			default:
				AppendString(builder, value.ToString());
				return;
		}
	}

	private static void AppendDictionary(
		StringBuilder builder,
		Godot.Collections.Dictionary dictionary,
		IReadOnlyList<string> preferredKeys)
	{
		builder.Append('{');
		var emitted = new HashSet<string>(StringComparer.Ordinal);
		bool hasPrevious = false;
		foreach (string key in preferredKeys)
		{
			if (!dictionary.TryGetValue(key, out Variant value))
			{
				continue;
			}

			AppendDictionaryEntry(builder, key, value, preferredKeys, ref hasPrevious);
			emitted.Add(key);
		}

		var remainingKeys = new List<string>();
		foreach (Variant rawKey in dictionary.Keys)
		{
			string key = rawKey.AsString();
			if (!emitted.Contains(key))
			{
				remainingKeys.Add(key);
			}
		}

		remainingKeys.Sort(StringComparer.Ordinal);
		foreach (string key in remainingKeys)
		{
			if (dictionary.TryGetValue(key, out Variant value))
			{
				AppendDictionaryEntry(builder, key, value, preferredKeys, ref hasPrevious);
			}
		}

		builder.Append('}');
	}

	private static void AppendDictionaryEntry(
		StringBuilder builder,
		string key,
		Variant value,
		IReadOnlyList<string> nestedKeyOrder,
		ref bool hasPrevious)
	{
		if (hasPrevious)
		{
			builder.Append(',');
		}

		hasPrevious = true;
		AppendString(builder, key);
		builder.Append(':');
		AppendVariant(builder, value, nestedKeyOrder);
	}

	private static void AppendString(StringBuilder builder, string value)
	{
		builder.Append('"');
		foreach (char character in value ?? string.Empty)
		{
			switch (character)
			{
				case '\\': builder.Append("\\\\"); break;
				case '"': builder.Append("\\\""); break;
				case '\n': builder.Append("\\n"); break;
				case '\r': builder.Append("\\r"); break;
				case '\t': builder.Append("\\t"); break;
				default: builder.Append(character); break;
			}
		}

		builder.Append('"');
	}
}
