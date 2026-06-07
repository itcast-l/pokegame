public static class RoomNameRules
{
	public const string DefaultRoomName = "我的房间";
	public const int MaxLength = 16;

	public static string Normalize(string value)
	{
		string trimmed = value?.Trim() ?? string.Empty;
		return IsValid(trimmed) ? trimmed : DefaultRoomName;
	}

	public static bool IsValid(string value)
	{
		string trimmed = value?.Trim() ?? string.Empty;
		if (trimmed.Length == 0 || trimmed.Length > MaxLength)
		{
			return false;
		}

		return HasOnlyAllowedCharacters(trimmed);
	}

	public static bool HasOnlyAllowedCharacters(string value)
	{
		foreach (char character in value)
		{
			if (!IsAllowedCharacter(character))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsAllowedCharacter(char character)
	{
		return (character >= 'A' && character <= 'Z')
			|| (character >= 'a' && character <= 'z')
			|| (character >= '0' && character <= '9')
			|| character == ' '
			|| character == '-'
			|| character == '_'
			|| (character >= '\u3400' && character <= '\u4DBF')
			|| (character >= '\u4E00' && character <= '\u9FFF')
			|| (character >= '\uF900' && character <= '\uFAFF');
	}
}
