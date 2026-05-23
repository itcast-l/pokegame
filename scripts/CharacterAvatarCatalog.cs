using System;
using System.Collections.Generic;
using Godot;

public sealed class CharacterAvatarDefinition
{
	public CharacterAvatarDefinition(string id, string displayName, string rootPath, int idleFrameCount, int actionFrameCount = 4)
	{
		Id = id;
		DisplayName = displayName;
		RootPath = rootPath;
		IdleFrameCount = idleFrameCount;
		ActionFrameCount = actionFrameCount;
	}

	public string Id { get; }
	public string DisplayName { get; }
	public string RootPath { get; }
	public int IdleFrameCount { get; }
	public int ActionFrameCount { get; }
}

public static class CharacterAvatarCatalog
{
	public const string SettingsConfigPath = "user://settings.cfg";
	public const string DefaultAvatarId = "cat";
	private const string AdventureProgressSection = "adventure";
	private const string HighestCompletedStageKey = "highest_completed_stage";

	private static readonly CharacterAvatarDefinition[] Avatars =
	{
		new("cat", "莫莫", "res://art/characters/player_cat_avatar/run-002", 1),
		new("fish", "阿布", "res://art/characters/player_fish_avatar/run-001", 1),
		new("lanternfish", "米露", "res://art/characters/player_lanternfish_avatar/run-001", 1),
		new("shrimp", "皮皮", "res://art/characters/player_shrimp_avatar/run-001", 1),
		new("crab", "嘟嘟", "res://art/characters/player_crab_avatar/run-001", 1),
		new("jellyfish", "软糖", "res://art/characters/player_jellyfish_avatar/run-001", 1),
		new("manta_ray", "悠悠", "res://art/characters/player_manta_ray_avatar/run-001", 1),
		new("garden_eel", "阿豆", "res://art/characters/player_garden_eel_avatar/run-001", 1),
		new("sea_slug", "糯米", "res://art/characters/player_sea_slug_avatar/run-001", 1),
		new("nautilus", "罗米", "res://art/characters/player_nautilus_avatar/run-001", 1),
		new("narwhal", "诺亚", "res://art/characters/player_narwhal_avatar/run-001", 1),
		new("leafy_seadragon", "艾莉", "res://art/characters/player_leafy_seadragon_avatar/run-001", 1),
		new("dragon_prince_j", "杰伊 J", "res://art/characters/player_dragon_prince_avatar/run-001", 12, 12),
		new("dragon_queen_q", "奎娜 Q", "res://art/characters/player_dragon_queen_avatar/run-001", 12, 12),
		new("boss_k", "凯撒 K", "res://art/characters/boss_k_avatar/run-001", 12, 12),
	};

	public static IReadOnlyList<CharacterAvatarDefinition> All => Avatars;
	public static CharacterAvatarDefinition Default => Avatars[0];

	public static CharacterAvatarDefinition GetById(string avatarId)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.Id, avatarId, StringComparison.Ordinal))
			{
				return avatar;
			}
		}

		return Default;
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
		for (int avatarIndex = 1; avatarIndex < Avatars.Length; avatarIndex++)
		{
			if (avatarIndex <= highestCompletedStage)
			{
				unlockedAvatars.Add(Avatars[avatarIndex]);
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

	public static int GetAdventureStageNumber(string avatarId)
	{
		string normalized = NormalizeAvatarId(avatarId);
		for (int avatarIndex = 1; avatarIndex < Avatars.Length; avatarIndex++)
		{
			if (string.Equals(Avatars[avatarIndex].Id, normalized, StringComparison.Ordinal))
			{
				return avatarIndex;
			}
		}

		return 0;
	}

	public static int LoadHighestCompletedAdventureStage()
	{
		var config = new ConfigFile();
		Error loadResult = config.Load(SettingsConfigPath);
		if (loadResult != Error.Ok)
		{
			return 0;
		}

		int highestCompletedStage = config.GetValue(AdventureProgressSection, HighestCompletedStageKey, 0).AsInt32();
		return Mathf.Clamp(highestCompletedStage, 0, Avatars.Length - 1);
	}

	public static void RecordAdventureVictory(string enemyAvatarId)
	{
		int stageNumber = GetAdventureStageNumber(enemyAvatarId);
		if (stageNumber <= 0)
		{
			return;
		}

		var config = new ConfigFile();
		Error loadResult = config.Load(SettingsConfigPath);
		if (loadResult != Error.Ok && loadResult != Error.FileNotFound)
		{
			GD.PushWarning($"Failed to load settings for adventure progress: {loadResult}");
			return;
		}

		int previousHighestStage = config.GetValue(AdventureProgressSection, HighestCompletedStageKey, 0).AsInt32();
		if (stageNumber <= previousHighestStage)
		{
			return;
		}

		config.SetValue(AdventureProgressSection, HighestCompletedStageKey, stageNumber);
		Error saveResult = config.Save(SettingsConfigPath);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to save adventure progress: {saveResult}");
		}
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

	public static CharacterAvatarDefinition LoadSelectedAvatar()
	{
		var config = new ConfigFile();
		Error loadResult = config.Load(SettingsConfigPath);
		if (loadResult != Error.Ok)
		{
			return Default;
		}

		string avatarId = config.GetValue("general", "avatar", DefaultAvatarId).AsString();
		return GetById(NormalizeUnlockedAvatarId(avatarId));
	}

}
