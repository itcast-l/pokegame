using System;
using Godot;

public static class NetworkVariantReader
{
	public static bool TryReadString(
		Godot.Collections.Dictionary source,
		string key,
		int maximumLength,
		out string value,
		bool allowEmpty = true)
	{
		value = string.Empty;
		if (source is null
			|| maximumLength < 0
			|| !source.TryGetValue(key, out Variant raw)
			|| raw.VariantType != Variant.Type.String)
		{
			return false;
		}

		value = raw.AsString();
		return value.Length <= maximumLength && (allowEmpty || !string.IsNullOrWhiteSpace(value));
	}

	public static bool TryReadOptionalString(
		Godot.Collections.Dictionary source,
		string key,
		int maximumLength,
		string fallback,
		out string value)
	{
		value = fallback;
		return source is not null
			&& (!source.ContainsKey(key) || TryReadString(source, key, maximumLength, out value));
	}

	public static bool TryReadInt64(
		Godot.Collections.Dictionary source,
		string key,
		long minimum,
		long maximum,
		out long value)
	{
		value = 0;
		if (source is null
			|| minimum > maximum
			|| !source.TryGetValue(key, out Variant raw)
			|| raw.VariantType != Variant.Type.Int)
		{
			return false;
		}

		value = raw.AsInt64();
		return value >= minimum && value <= maximum;
	}

	public static bool TryReadInt32(
		Godot.Collections.Dictionary source,
		string key,
		int minimum,
		int maximum,
		out int value)
	{
		value = 0;
		if (!TryReadInt64(source, key, minimum, maximum, out long decoded))
		{
			return false;
		}

		value = (int)decoded;
		return true;
	}

	public static bool TryReadOptionalInt32(
		Godot.Collections.Dictionary source,
		string key,
		int fallback,
		int minimum,
		int maximum,
		out int value)
	{
		value = fallback;
		return source is not null
			&& (!source.ContainsKey(key) || TryReadInt32(source, key, minimum, maximum, out value));
	}

	public static bool TryReadBool(Godot.Collections.Dictionary source, string key, out bool value)
	{
		value = false;
		if (source is null
			|| !source.TryGetValue(key, out Variant raw)
			|| raw.VariantType != Variant.Type.Bool)
		{
			return false;
		}

		value = raw.AsBool();
		return true;
	}

	public static bool TryReadFiniteFloat(
		Godot.Collections.Dictionary source,
		string key,
		float minimum,
		float maximum,
		out float value)
	{
		value = 0;
		if (source is null
			|| minimum > maximum
			|| !source.TryGetValue(key, out Variant raw)
			|| raw.VariantType != Variant.Type.Float)
		{
			return false;
		}

		double decoded = raw.AsDouble();
		if (!double.IsFinite(decoded) || decoded < minimum || decoded > maximum)
		{
			return false;
		}

		value = (float)decoded;
		return true;
	}

	public static bool TryReadEnum<TEnum>(
		Godot.Collections.Dictionary source,
		string key,
		out TEnum value)
		where TEnum : struct, Enum
	{
		value = default;
		return TryReadString(source, key, 64, out string raw, allowEmpty: false)
			&& Enum.TryParse(raw, ignoreCase: true, out value)
			&& Enum.IsDefined(value);
	}

	public static bool TryReadArray(
		Godot.Collections.Dictionary source,
		string key,
		int minimumCount,
		int maximumCount,
		out Godot.Collections.Array values)
	{
		values = default!;
		if (source is null
			|| minimumCount < 0
			|| minimumCount > maximumCount
			|| !source.TryGetValue(key, out Variant raw)
			|| raw.VariantType != Variant.Type.Array)
		{
			return false;
		}

		values = raw.AsGodotArray();
		return values.Count >= minimumCount && values.Count <= maximumCount;
	}

	public static bool TryReadDictionary(
		Godot.Collections.Dictionary source,
		string key,
		out Godot.Collections.Dictionary value)
	{
		value = default!;
		return source is not null
			&& source.TryGetValue(key, out Variant raw)
			&& TryConvertDictionary(raw, out value);
	}

	public static bool TryConvertDictionary(Variant raw, out Godot.Collections.Dictionary value)
	{
		value = default!;
		if (raw.VariantType != Variant.Type.Dictionary)
		{
			return false;
		}

		value = raw.AsGodotDictionary();
		return true;
	}
}
