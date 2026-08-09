using System;
using System.Collections.Generic;
using Godot;

public static partial class CharacterAvatarCatalog
{
	public const string DefaultAvatarId = "cat";
	private static readonly HashSet<string> StarterAvatarIds = new(StringComparer.Ordinal)
	{
		DefaultAvatarId,
		"bear",
		"fox",
		"hamster",
		"lop_rabbit",
		"shiba",
		"red_panda",
	};
	private const bool UnlockAllAdventureStagesForTesting = true;
	private const string PendingAvatarUnlockQueueSeparator = "|";
	private const string AvatarResourceDirectory = "res://resources/character_avatars";

	private static CharacterAvatarDefinition[]? _avatars;
	private static readonly CharacterAvatarDefinition EmptyDefault = new(DefaultAvatarId, DefaultAvatarId, string.Empty, 1);

	private static CharacterAvatarDefinition[] Avatars => _avatars ??= LoadAvatarDefinitions();

	public static IReadOnlyList<CharacterAvatarDefinition> All => Avatars;
	public static CharacterAvatarDefinition Default
	{
		get
		{
			foreach (CharacterAvatarDefinition avatar in Avatars)
			{
				if (string.Equals(avatar.Id, DefaultAvatarId, StringComparison.Ordinal))
				{
					return avatar;
				}
			}

			return Avatars.Length > 0 ? Avatars[0] : EmptyDefault;
		}
	}

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
		var unlockedAvatars = new List<CharacterAvatarDefinition>();
		int highestCompletedStage = LoadHighestCompletedAdventureStage();
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			bool isMainAvatar = AdventureCampaignCatalog.TryGetMainStageIndex(
				avatar.Id,
				out int mainStageIndex);
			if (IsStarterAvatar(avatar.Id)
				|| (isMainAvatar && mainStageIndex <= highestCompletedStage))
			{
				unlockedAvatars.Add(avatar);
			}
		}

		return unlockedAvatars;
	}

	public static bool IsAvatarUnlocked(string avatarId)
	{
		string normalized = NormalizeAvatarId(avatarId);
		if (IsStarterAvatar(normalized))
		{
			return true;
		}

		return AdventureCampaignCatalog.TryGetMainStageIndex(normalized, out int mainStageIndex)
			&& IsPlayerObtainableAdventureStage(mainStageIndex)
			&& mainStageIndex <= LoadHighestCompletedAdventureStage();
	}

	public static int GetAdventureStageCount()
	{
		return AdventureCampaignCatalog.MainStageCount;
	}

	public static bool IsAdventureStageCompleted(int stageNumber)
	{
		return stageNumber >= 1 && stageNumber <= AdventureCampaignCatalog.MainStageCount
			&& stageNumber <= LoadHighestCompletedAdventureStage();
	}

	public static bool IsAdventureStageAvailable(int stageNumber)
	{
		return stageNumber >= 1 && stageNumber <= AdventureCampaignCatalog.MainStageCount
			&& stageNumber == LoadHighestCompletedAdventureStage() + 1;
	}

	public static bool IsAdventureStageUnlocked(int stageNumber)
	{
		return stageNumber >= 1 && stageNumber <= AdventureCampaignCatalog.MainStageCount
			&& (UnlockAllAdventureStagesForTesting || stageNumber <= LoadHighestCompletedAdventureStage() + 1);
	}

	public static int GetAdventureStageNumber(string avatarId)
	{
		string normalized = NormalizeAvatarId(avatarId);
		if (AdventureCampaignCatalog.TryGetMainStageIndex(normalized, out int mainStageIndex))
		{
			return AdventureCampaignCatalog.LegacyAvatarStageForMainStage(mainStageIndex);
		}

		for (int tutorialIndex = 0; tutorialIndex < AdventureCampaignCatalog.Tutorial.Count; tutorialIndex++)
		{
			if (string.Equals(
				AdventureCampaignCatalog.Tutorial[tutorialIndex].EnemyAvatarId,
				normalized,
				StringComparison.Ordinal))
			{
				return tutorialIndex + 1;
			}
		}

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
		return AdventureCampaignCatalog.GetMainAvatarId(stageNumber) is string avatarId
			&& !string.IsNullOrEmpty(avatarId)
			? avatarId
			: DefaultAvatarId;
	}

	public static string GetLegacyAdventureAvatarIdForStage(int legacyStageNumber)
	{
		if (AdventureCampaignCatalog.TryMapLegacyRunStage(legacyStageNumber, out int mainStageIndex))
		{
			return AdventureCampaignCatalog.GetMainAvatarId(mainStageIndex);
		}

		if (legacyStageNumber is 1 or 2)
		{
			return AdventureCampaignCatalog.Tutorial[legacyStageNumber - 1].EnemyAvatarId;
		}

		return DefaultAvatarId;
	}

	public static int LoadHighestCompletedAdventureStage()
	{
		int highestCompletedStage = GameSaveStore.LoadActiveProfile().HighestCompletedMainAdventureStage;
		return Mathf.Clamp(highestCompletedStage, 0, AdventureCampaignCatalog.MainStageCount);
	}

	public static bool IsTutorialCompleted(string completedTutorialIdsCsv, string tutorialAvatarId)
	{
		return AdventureCampaignCatalog.IsTutorialCompleted(completedTutorialIdsCsv, tutorialAvatarId);
	}

	public static bool IsTutorialCompleted(string tutorialAvatarId)
	{
		return IsTutorialCompleted(
			GameSaveStore.LoadActiveProfile().CompletedAdventureTutorialIdsCsv,
			tutorialAvatarId);
	}

	public static void RecordAdventureVictory(string enemyAvatarId)
	{
		string normalizedAvatarId = NormalizeAvatarId(enemyAvatarId);
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		if (AdventureCampaignCatalog.IsTutorialAvatar(normalizedAvatarId))
		{
			string completedTutorialIds = AdventureCampaignCatalog.AddCompletedTutorialId(
				profile.CompletedAdventureTutorialIdsCsv,
				normalizedAvatarId);
			if (!string.Equals(completedTutorialIds, profile.CompletedAdventureTutorialIdsCsv, StringComparison.Ordinal))
			{
				Error tutorialSaveResult = GameSaveStore.SaveActiveProfile(profile with
				{
					CompletedAdventureTutorialIdsCsv = completedTutorialIds,
				});
				if (tutorialSaveResult != Error.Ok)
				{
					GD.PushWarning($"Failed to save adventure tutorial progress: {tutorialSaveResult}");
				}
			}

			return;
		}

	if (!AdventureCampaignCatalog.TryGetMainStageIndex(normalizedAvatarId, out int mainStageIndex))
		{
			return;
		}

		int previousHighestStage = Mathf.Clamp(
			profile.HighestCompletedMainAdventureStage,
			0,
			AdventureCampaignCatalog.MainStageCount);
		if (mainStageIndex <= previousHighestStage)
		{
			return;
		}
		if (mainStageIndex != previousHighestStage + 1)
		{
			return;
		}

		var pendingUnlocks = LoadPendingAdventureAvatarUnlockQueue();
		if (IsPlayerObtainableAdventureStage(mainStageIndex))
		{
			if (!pendingUnlocks.Contains(normalizedAvatarId))
			{
				pendingUnlocks.Add(normalizedAvatarId);
			}
		}

		Error saveResult = GameSaveStore.SaveActiveProfile(profile with
		{
			HighestCompletedMainAdventureStage = mainStageIndex,
			PendingAvatarUnlockQueue = FormatPendingAdventureAvatarUnlockQueue(pendingUnlocks),
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to save adventure progress: {saveResult}");
		}
	}

	internal static GameSaveStore.GameProgressData CreateMainAdventureVictoryProfile(
		GameSaveStore.GameProgressData profile,
		int completedStageIndex,
		string enemyAvatarId,
		string nextRunJson)
	{
		List<string> pendingUnlocks = ParsePendingAdventureAvatarUnlockQueue(profile.PendingAvatarUnlockQueue);
		if (IsPlayerObtainableAdventureStage(completedStageIndex)
			&& !pendingUnlocks.Contains(enemyAvatarId))
		{
			pendingUnlocks.Add(enemyAvatarId);
		}

		return profile with
		{
			HighestCompletedMainAdventureStage = completedStageIndex,
			PendingAvatarUnlockQueue = FormatPendingAdventureAvatarUnlockQueue(pendingUnlocks),
			PendingAdventureRunJson = nextRunJson,
		};
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
			if (AdventureCampaignCatalog.TryGetMainStageIndex(normalizedAvatarId, out int mainStageIndex)
				&& IsPlayerObtainableAdventureStage(mainStageIndex)
				&& mainStageIndex <= LoadHighestCompletedAdventureStage())
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

	private static bool IsPlayerObtainableAdventureAvatar(CharacterAvatarDefinition avatar)
	{
		return IsPlayerObtainableAdventureStage(avatar.AdventureStageNumber);
	}

	private static bool IsStarterAvatar(string avatarId)
	{
		return StarterAvatarIds.Contains(avatarId);
	}

	private static bool IsPlayerObtainableAdventureStage(int stageNumber)
	{
		return AdventureCampaignCatalog.IsMainAvatar(
			AdventureCampaignCatalog.GetMainAvatarId(stageNumber));
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

	public static float GetIdleFrameSeconds(string avatarRoot)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.RootPath, avatarRoot, StringComparison.Ordinal))
			{
				return avatar.IdleFrameSeconds;
			}
		}

		return 0.0f;
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
		string avatarId = GameSaveStore.LoadActiveProfile().SelectedAvatarId;
		return GetById(NormalizeUnlockedAvatarId(avatarId));
	}

}
