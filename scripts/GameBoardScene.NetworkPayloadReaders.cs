using Godot;

public partial class GameBoardScene
{
	private const int MaxNetworkCardCodeLength = 64;
	private const int MaxNetworkItemCardIdLength = 128;
	private const int MaxNetworkAttachmentCsvLength = 128;
	private const int MaxNetworkSessionIdLength = 128;
	private const int MaxNetworkOpaqueIdentityLength = 256;

	private static bool TryConvertNetworkInt32(Variant rawValue, out int value)
	{
		value = 0;
		if (rawValue.VariantType != Variant.Type.Int)
		{
			return false;
		}

		long raw = rawValue.AsInt64();
		if (raw < int.MinValue || raw > int.MaxValue)
		{
			return false;
		}

		value = (int)raw;
		return true;
	}

	private static bool TryReadNetworkInt32(Godot.Collections.Dictionary payload, string key, out int value)
	{
		value = 0;
		return payload.TryGetValue(key, out Variant rawValue) && TryConvertNetworkInt32(rawValue, out value);
	}

	private static bool TryReadNetworkInt32(Godot.Collections.Dictionary payload, string key, int minimum, int maximum, out int value)
	{
		return TryReadNetworkInt32(payload, key, out value) && value >= minimum && value <= maximum;
	}

	private static bool TryReadOptionalNetworkInt32(
		Godot.Collections.Dictionary payload,
		string key,
		int fallback,
		int minimum,
		int maximum,
		out int value)
	{
		if (!payload.ContainsKey(key))
		{
			value = fallback;
			return true;
		}

		return TryReadNetworkInt32(payload, key, minimum, maximum, out value);
	}

	private static bool TryReadNetworkString(Godot.Collections.Dictionary payload, string key, out string value)
	{
		value = string.Empty;
		if (!payload.TryGetValue(key, out Variant rawValue) || rawValue.VariantType != Variant.Type.String)
		{
			return false;
		}
		value = rawValue.AsString();
		return true;
	}

	private static bool TryReadNetworkString(Godot.Collections.Dictionary payload, string key, int maximumLength, out string value)
	{
		return TryReadNetworkString(payload, key, out value) && value.Length <= maximumLength;
	}

	private static bool TryReadOptionalNetworkString(Godot.Collections.Dictionary payload, string key, out string value)
	{
		value = string.Empty;
		return !payload.ContainsKey(key) || TryReadNetworkString(payload, key, out value);
	}

	private static bool TryReadNetworkBool(Godot.Collections.Dictionary payload, string key, out bool value)
	{
		value = false;
		if (!payload.TryGetValue(key, out Variant rawValue) || rawValue.VariantType != Variant.Type.Bool) return false;
		value = rawValue.AsBool();
		return true;
	}

	private static bool TryReadNetworkFiniteFloat(Godot.Collections.Dictionary payload, string key, out float value)
	{
		value = 0.0f;
		if (!payload.TryGetValue(key, out Variant rawValue) || rawValue.VariantType != Variant.Type.Float) return false;
		double raw = rawValue.AsDouble();
		if (!double.IsFinite(raw) || raw < -float.MaxValue || raw > float.MaxValue) return false;
		value = (float)raw;
		return true;
	}

	private static string ReadNetworkString(Godot.Collections.Dictionary payload, string key, string fallback) => TryReadNetworkString(payload, key, out string value) ? value : fallback;
	private static int ReadNetworkInt(Godot.Collections.Dictionary payload, string key, int fallback) => TryReadNetworkInt32(payload, key, out int value) ? value : fallback;
	private static long ReadNetworkLong(Godot.Collections.Dictionary payload, string key, long fallback) => payload.TryGetValue(key, out Variant value) && value.VariantType == Variant.Type.Int ? value.AsInt64() : fallback;
	private static bool ReadNetworkBool(Godot.Collections.Dictionary payload, string key, bool fallback) => TryReadNetworkBool(payload, key, out bool value) ? value : fallback;
	private static float ReadNetworkFloat(Godot.Collections.Dictionary payload, string key, float fallback) => TryReadNetworkFiniteFloat(payload, key, out float value) ? value : fallback;
}
