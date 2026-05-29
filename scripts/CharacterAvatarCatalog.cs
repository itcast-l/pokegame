using System;
using System.Collections.Generic;
using Godot;

public static partial class CharacterAvatarCatalog
{
	public const string DefaultAvatarId = "cat";
	private const bool UnlockAllAdventureStagesForTesting = false;
	private const string PendingAvatarUnlockQueueSeparator = "|";
	private const string AvatarResourceDirectory = "res://resources/character_avatars";

	private static CharacterAvatarDefinition[]? _avatars;
	private static readonly CharacterAvatarDefinition EmptyDefault = new(DefaultAvatarId, DefaultAvatarId, string.Empty, 1);

	private static CharacterAvatarDefinition[] Avatars => _avatars ??= LoadAvatarDefinitions();

	public static IReadOnlyList<CharacterAvatarDefinition> All => Avatars;
	public static CharacterAvatarDefinition Default => Avatars.Length > 0 ? Avatars[0] : EmptyDefault;

	public static CharacterAvatarDefinition GetById(string avatarId)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.Id, avatarId, StringComparison.Ordinal)
				|| string.Equals(avatar.DisplayName, avatarId, StringComparison.Ordinal))
			{
				return avatar;
			}
		}

		return Default;
	}

	public static bool TryGetAvatarIdForAbility(string abilityId, out string avatarId, bool requireBossBorrowedFxEligible = false)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (!avatar.HasAbility(abilityId)
				|| (requireBossBorrowedFxEligible && !avatar.BossBorrowedAbilityFxEligible))
			{
				continue;
			}

			avatarId = avatar.Id;
			return true;
		}

		avatarId = string.Empty;
		return false;
	}

	public static string NormalizeAvatarId(string avatarId)
	{
		return GetById(avatarId).Id;
	}

	public static string NormalizeUnlockedAvatarId(string avatarId)
	{
		string normalized = NormalizeAvatarId(avatarId);
		return IsAvatarUnlocked(normalized) ? normalized : DefaultAvatarId;
	}

	public static IReadOnlyList<CharacterAvatarDefinition> GetUnlockedAvatars()
	{
		var unlockedAvatars = new List<CharacterAvatarDefinition> { Default };
		int highestCompletedStage = LoadHighestCompletedAdventureStage();
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (avatar.AdventureStageNumber > 0 && avatar.AdventureStageNumber <= highestCompletedStage)
			{
				unlockedAvatars.Add(avatar);
			}
		}

		return unlockedAvatars;
	}

	public static bool IsAvatarUnlocked(string avatarId)
	{
		string normalized = NormalizeAvatarId(avatarId);
		if (normalized == DefaultAvatarId)
		{
			return true;
		}

		int stageNumber = GetAdventureStageNumber(normalized);
		return stageNumber > 0 && stageNumber <= LoadHighestCompletedAdventureStage();
	}

	public static int GetAdventureStageCount()
	{
		int stageCount = 0;
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			stageCount = Math.Max(stageCount, avatar.AdventureStageNumber);
		}

		return stageCount;
	}

	public static bool IsAdventureStageCompleted(int stageNumber)
	{
		return stageNumber > 0 && stageNumber <= LoadHighestCompletedAdventureStage();
	}

	public static bool IsAdventureStageAvailable(int stageNumber)
	{
		return stageNumber > 0
			&& stageNumber <= GetAdventureStageCount()
			&& stageNumber == LoadHighestCompletedAdventureStage() + 1;
	}

	public static bool IsAdventureStageUnlocked(int stageNumber)
	{
		return stageNumber > 0
			&& stageNumber <= GetAdventureStageCount()
			&& (UnlockAllAdventureStagesForTesting || stageNumber <= LoadHighestCompletedAdventureStage() + 1);
	}

	public static int GetAdventureStageNumber(string avatarId)
	{
		string normalized = NormalizeAvatarId(avatarId);
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.Id, normalized, StringComparison.Ordinal))
			{
				return avatar.AdventureStageNumber;
			}
		}

		return 0;
	}

	public static string GetAdventureAvatarIdForStage(int stageNumber)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (avatar.AdventureStageNumber == stageNumber)
			{
				return avatar.Id;
			}
		}

		return DefaultAvatarId;
	}

	public static int LoadHighestCompletedAdventureStage()
	{
		int highestCompletedStage = GameSaveStore.GetInt(GameSaveStore.HighestCompletedStageKey, 0);
		return Mathf.Clamp(highestCompletedStage, 0, GetAdventureStageCount());
	}

	public static void RecordAdventureVictory(string enemyAvatarId)
	{
		string normalizedAvatarId = NormalizeAvatarId(enemyAvatarId);
		int stageNumber = GetAdventureStageNumber(normalizedAvatarId);
		if (stageNumber <= 0)
		{
			return;
		}

		int previousHighestStage = GameSaveStore.GetInt(GameSaveStore.HighestCompletedStageKey, 0);
		if (stageNumber <= previousHighestStage)
		{
			return;
		}

		var pendingUnlocks = LoadPendingAdventureAvatarUnlockQueue();
		pendingUnlocks.Add(normalizedAvatarId);
		Error saveResult = GameSaveStore.SetValues(
			new GameSaveStore.SaveValue(GameSaveStore.HighestCompletedStageKey, stageNumber),
			new GameSaveStore.SaveValue(GameSaveStore.PendingAvatarUnlockQueueKey, FormatPendingAdventureAvatarUnlockQueue(pendingUnlocks)),
			new GameSaveStore.SaveValue(GameSaveStore.PendingAvatarUnlockKey, string.Empty),
			new GameSaveStore.SaveValue(GameSaveStore.PendingAvatarUnlockStageKey, 0));
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to save adventure progress: {saveResult}");
		}
	}

	public static string ConsumePendingAdventureAvatarUnlock()
	{
		List<string> pendingUnlocks = LoadPendingAdventureAvatarUnlockQueue();
		if (pendingUnlocks.Count == 0)
		{
			return string.Empty;
		}

		while (pendingUnlocks.Count > 0)
		{
			string avatarId = pendingUnlocks[0];
			pendingUnlocks.RemoveAt(0);
			string normalizedAvatarId = NormalizeAvatarId(avatarId);
			int stageNumber = GetAdventureStageNumber(normalizedAvatarId);
			if (stageNumber > 0 && stageNumber <= LoadHighestCompletedAdventureStage())
			{
				Error consumeSaveResult = SavePendingAdventureAvatarUnlockQueue(pendingUnlocks);
				if (consumeSaveResult != Error.Ok)
				{
					GD.PushWarning($"Failed to clear pending adventure avatar unlock: {consumeSaveResult}");
				}

				return normalizedAvatarId;
			}
		}

		Error saveResult = SavePendingAdventureAvatarUnlockQueue(pendingUnlocks);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to clear pending adventure avatar unlock: {saveResult}");
		}

		return string.Empty;
	}

	public static int GetIdleFrameCount(string avatarRoot)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.RootPath, avatarRoot, StringComparison.Ordinal))
			{
				return avatar.IdleFrameCount;
			}
		}

		return Default.IdleFrameCount;
	}

	public static int GetActionFrameCount(string avatarRoot)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.RootPath, avatarRoot, StringComparison.Ordinal))
			{
				return avatar.ActionFrameCount;
			}
		}

		return Default.ActionFrameCount;
	}

	public static string GetAttackFxRoot(string avatarRoot)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.RootPath, avatarRoot, StringComparison.Ordinal))
			{
				return avatar.AttackFxRoot;
			}
		}

		return string.Empty;
	}

	public static CharacterAvatarDefinition LoadSelectedAvatar()
	{
		string avatarId = GameSaveStore.GetString(GameSaveStore.SelectedAvatarKey, DefaultAvatarId);
		return GetById(NormalizeUnlockedAvatarId(avatarId));
	}

}
