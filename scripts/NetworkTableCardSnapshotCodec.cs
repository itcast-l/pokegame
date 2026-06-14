using System;
using System.Collections.Generic;

internal readonly record struct NetworkTableCardSnapshot(
	string CardCode,
	string[] AttachedItemCardIds,
	bool IsFaceUp,
	bool IsBuriedInSand,
	bool IsSlimeCoated,
	int CardOwnerPlayerIndex,
	bool RemoveCardOnCapture,
	string ItemOverlayId,
	int ItemOwnerPlayerIndex);

internal static class NetworkTableCardSnapshotCodec
{
	public const string CodeKey = "code";
	public const string AttachedItemCardIdsKey = "attached_item_card_ids";
	public const string IsFaceUpKey = "is_face_up";
	public const string IsBuriedInSandKey = "is_buried_in_sand";
	public const string IsSlimeCoatedKey = "is_slime_coated";
	public const string CardOwnerPlayerIndexKey = "card_owner_player_index";
	public const string RemoveCardOnCaptureKey = "remove_card_on_capture";
	public const string ItemOverlayIdKey = "item_overlay_id";
	public const string ItemOwnerPlayerIndexKey = "item_owner_player_index";

	public static Dictionary<string, object?> ToDictionary(NetworkTableCardSnapshot snapshot)
	{
		return new Dictionary<string, object?>
		{
			[CodeKey] = snapshot.CardCode,
			[AttachedItemCardIdsKey] = snapshot.AttachedItemCardIds,
			[IsFaceUpKey] = snapshot.IsFaceUp,
			[IsBuriedInSandKey] = snapshot.IsBuriedInSand,
			[IsSlimeCoatedKey] = snapshot.IsSlimeCoated,
			[CardOwnerPlayerIndexKey] = snapshot.CardOwnerPlayerIndex,
			[RemoveCardOnCaptureKey] = snapshot.RemoveCardOnCapture,
			[ItemOverlayIdKey] = snapshot.ItemOverlayId,
			[ItemOwnerPlayerIndexKey] = snapshot.ItemOwnerPlayerIndex,
		};
	}

	public static NetworkTableCardSnapshot FromDictionary(IReadOnlyDictionary<string, object?> dictionary)
	{
		return new NetworkTableCardSnapshot(
			ReadString(dictionary, CodeKey),
			ReadStringArray(dictionary, AttachedItemCardIdsKey),
			ReadBool(dictionary, IsFaceUpKey),
			ReadBool(dictionary, IsBuriedInSandKey),
			ReadBool(dictionary, IsSlimeCoatedKey),
			ReadInt(dictionary, CardOwnerPlayerIndexKey, -1),
			ReadBool(dictionary, RemoveCardOnCaptureKey),
			ReadString(dictionary, ItemOverlayIdKey),
			ReadInt(dictionary, ItemOwnerPlayerIndexKey, -1));
	}

	private static string ReadString(IReadOnlyDictionary<string, object?> dictionary, string key)
	{
		if (!dictionary.TryGetValue(key, out object? value) || value == null)
		{
			return string.Empty;
		}

		return value as string ?? string.Empty;
	}

	private static int ReadInt(IReadOnlyDictionary<string, object?> dictionary, string key, int fallback = 0)
	{
		if (!dictionary.TryGetValue(key, out object? value) || value == null)
		{
			return fallback;
		}

		return value switch
		{
			int intValue => intValue,
			long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
			_ => fallback,
		};
	}

	private static bool ReadBool(IReadOnlyDictionary<string, object?> dictionary, string key)
	{
		return dictionary.TryGetValue(key, out object? value)
			&& value is bool boolValue
			&& boolValue;
	}

	private static string[] ReadStringArray(IReadOnlyDictionary<string, object?> dictionary, string key)
	{
		if (!dictionary.TryGetValue(key, out object? value) || value == null)
		{
			return Array.Empty<string>();
		}

		switch (value)
		{
			case string[] array:
				return array;
			case List<string> list:
				return list.ToArray();
			case IReadOnlyList<string> readOnlyList:
			{
				var result = new string[readOnlyList.Count];
				for (int index = 0; index < readOnlyList.Count; index++)
				{
					result[index] = readOnlyList[index];
				}

				return result;
			}
			case object[] objectArray:
			{
				var result = new List<string>(objectArray.Length);
				foreach (object? item in objectArray)
				{
					if (item is string stringItem && !string.IsNullOrWhiteSpace(stringItem))
					{
						result.Add(stringItem);
					}
				}

				return result.ToArray();
			}
			default:
				return Array.Empty<string>();
		}
	}
}
