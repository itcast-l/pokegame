public static class CharacterAvatarCatalog
{
	public const string DefaultAvatarId = "cat";
}

public static class PlayerNameRules
{
	public const string DefaultPlayerName = "玩家";

	public static string Normalize(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? DefaultPlayerName : value;
	}
}
