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
		return NetworkVariantReader.TryReadInt32(payload, key, int.MinValue, int.MaxValue, out value);
	}

	private static bool TryReadNetworkInt32(Godot.Collections.Dictionary payload, string key, int minimum, int maximum, out int value)
	{
		return NetworkVariantReader.TryReadInt32(payload, key, minimum, maximum, out value);
	}

	private static bool TryReadOptionalNetworkInt32(
		Godot.Collections.Dictionary payload,
		string key,
		int fallback,
		int minimum,
		int maximum,
		out int value)
	{
		return NetworkVariantReader.TryReadOptionalInt32(payload, key, fallback, minimum, maximum, out value);
	}

	private static bool TryReadNetworkString(Godot.Collections.Dictionary payload, string key, out string value)
	{
		return NetworkVariantReader.TryReadString(payload, key, int.MaxValue, out value);
	}

	private static bool TryReadNetworkString(Godot.Collections.Dictionary payload, string key, int maximumLength, out string value)
	{
		return NetworkVariantReader.TryReadString(payload, key, maximumLength, out value);
	}

	private static bool TryReadOptionalNetworkString(Godot.Collections.Dictionary payload, string key, out string value)
	{
		return NetworkVariantReader.TryReadOptionalString(payload, key, int.MaxValue, string.Empty, out value);
	}

	private static bool TryReadNetworkBool(Godot.Collections.Dictionary payload, string key, out bool value)
	{
		return NetworkVariantReader.TryReadBool(payload, key, out value);
	}

	private static bool TryReadNetworkFiniteFloat(Godot.Collections.Dictionary payload, string key, out float value)
	{
		return NetworkVariantReader.TryReadFiniteFloat(payload, key, -float.MaxValue, float.MaxValue, out value);
	}

}
