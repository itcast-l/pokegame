using System;
using System.Collections.Generic;
using System.Text.Json;

public sealed class CardEffectValues
{
	public static readonly CardEffectValues Empty = new(new Dictionary<string, string>(StringComparer.Ordinal));

	private readonly Dictionary<string, string> _values;

	private CardEffectValues(Dictionary<string, string> values)
	{
		_values = values;
	}

	public bool IsEmpty => _values.Count == 0;

	public IReadOnlyDictionary<string, string> All => _values;

	public static CardEffectValues Parse(string rawValue)
	{
		return TryParse(rawValue, out CardEffectValues values)
			? values
			: Empty;
	}

	public static bool TryParse(string rawValue, out CardEffectValues values)
	{
		if (string.IsNullOrWhiteSpace(rawValue))
		{
			values = Empty;
			return true;
		}

		string raw = rawValue.Trim();
		if (raw.StartsWith("{", StringComparison.Ordinal))
		{
			return TryParseJsonObject(raw, out values);
		}

		var parsedValues = new Dictionary<string, string>(StringComparer.Ordinal);
		string[] pairs = raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (string pair in pairs)
		{
			int separatorIndex = pair.IndexOf('=');
			if (separatorIndex <= 0 || separatorIndex >= pair.Length - 1)
			{
				values = Empty;
				return false;
			}

			string key = pair[..separatorIndex].Trim();
			string value = pair[(separatorIndex + 1)..].Trim();
			if (key.Length == 0 || value.Length == 0)
			{
				values = Empty;
				return false;
			}

			parsedValues[key] = value;
		}

		values = parsedValues.Count == 0 ? Empty : new CardEffectValues(parsedValues);
		return true;
	}

	public string GetString(string key, string fallback = "")
	{
		return _values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
			? value
			: fallback;
	}

	public int GetInt(string key, int fallback = 0)
	{
		return _values.TryGetValue(key, out string? value) && int.TryParse(value, out int parsed)
			? parsed
			: fallback;
	}

	public bool GetBool(string key, bool fallback = false)
	{
		return _values.TryGetValue(key, out string? value) && bool.TryParse(value, out bool parsed)
			? parsed
			: fallback;
	}

	public CardEffectValues WithOverrides(CardEffectValues overrides)
	{
		ArgumentNullException.ThrowIfNull(overrides);
		if (overrides.IsEmpty)
		{
			return this;
		}

		var merged = new Dictionary<string, string>(_values, StringComparer.Ordinal);
		foreach (KeyValuePair<string, string> pair in overrides._values)
		{
			merged[pair.Key] = pair.Value;
		}

		return new CardEffectValues(merged);
	}

	private static bool TryParseJsonObject(string raw, out CardEffectValues values)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(raw);
			if (document.RootElement.ValueKind != JsonValueKind.Object)
			{
				values = Empty;
				return false;
			}

			var parsedValues = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (JsonProperty property in document.RootElement.EnumerateObject())
			{
				if (property.Value.ValueKind == JsonValueKind.Array || property.Value.ValueKind == JsonValueKind.Object)
				{
					values = Empty;
					return false;
				}

				parsedValues[property.Name] = property.Value.ValueKind == JsonValueKind.String
					? property.Value.GetString() ?? string.Empty
					: property.Value.ToString();
				if (parsedValues[property.Name].Length == 0)
				{
					values = Empty;
					return false;
				}
			}

			values = parsedValues.Count == 0 ? Empty : new CardEffectValues(parsedValues);
			return true;
		}
		catch (JsonException)
		{
			values = Empty;
			return false;
		}
	}
}
