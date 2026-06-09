public static class RoomPasswordRules
{
	public const int MaxLength = 24;

	public static string Normalize(string value)
	{
		string normalized = value?.Trim() ?? string.Empty;
		return normalized.Length <= MaxLength ? normalized : normalized[..MaxLength];
	}

	public static bool HasPassword(string value)
	{
		return !string.IsNullOrEmpty(Normalize(value));
	}
}
