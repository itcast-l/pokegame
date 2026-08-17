public static class CharacterAvatarCatalog
{
	public const string DefaultAvatarId = "cat";

	public static string NormalizeAvatarId(string avatarId)
	{
		return string.IsNullOrWhiteSpace(avatarId) ? DefaultAvatarId : avatarId.Trim();
	}

	internal static GameSaveStore.GameProgressData CreateMainAdventureVictoryProfile(
		GameSaveStore.GameProgressData profile,
		int completedStageIndex,
		string enemyAvatarId,
		string nextRunJson)
	{
		var pendingUnlocks = string.IsNullOrWhiteSpace(profile.PendingAvatarUnlockQueue)
			? new List<string>()
			: new List<string>(profile.PendingAvatarUnlockQueue.Split('|'));
		if (!pendingUnlocks.Contains(enemyAvatarId))
		{
			pendingUnlocks.Add(enemyAvatarId);
		}

		return profile with
		{
			HighestCompletedMainAdventureStage = completedStageIndex,
			PendingAvatarUnlockQueue = string.Join('|', pendingUnlocks),
			PendingAdventureRunJson = nextRunJson,
		};
	}
}

public static class PlayerNameRules
{
	public const string DefaultPlayerName = "玩家";

	public static string Normalize(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? DefaultPlayerName : value;
	}
}
