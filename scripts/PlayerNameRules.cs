public static class PlayerNameRules
{
	public const string DefaultPlayerName = "玩家";
	public const int MaxLength = 12;

	public static string Normalize(string value)
	{
		return IsValid(value) ? value : DefaultPlayerName;
	}

	public static bool IsValid(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return false;
		}

		if (value.Length > MaxLength)
		{
			return false;
		}

		return HasOnlyAllowedCharacters(value);
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
			|| (character >= '\u3400' && character <= '\u4DBF')
			|| (character >= '\u4E00' && character <= '\u9FFF')
			|| (character >= '\uF900' && character <= '\uFAFF');
	}
}
