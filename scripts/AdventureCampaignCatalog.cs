using System;
using System.Collections.Generic;

public sealed record AdventureCampaignLevel(
	string EnemyAvatarId,
	string GameBoardScenePath);

public sealed record AdventureCampaignDefinition(
	string Id,
	string NameKey,
	IReadOnlyList<AdventureCampaignLevel> Levels);

public static class AdventureCampaignCatalog
{
	public const string TutorialCampaignId = "tutorial";
	public const string MainCampaignId = "deep_tide_throne";
	public const string TutorialGameBoardScenePath = "res://scenes/adventure_tutorial_game_board_scene.tscn";
	public const string ItemTutorialGameBoardScenePath = "res://scenes/adventure_item_tutorial_game_board_scene.tscn";
	public const string MainGameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const int LegacyMainStageCount = 12;
	private const int LegacyFirstMainAvatarStageNumber = 3;
	private const int LegacyLastMainAvatarStageNumber = LegacyFirstMainAvatarStageNumber + LegacyMainStageCount - 1;

	private static readonly IReadOnlyList<AdventureCampaignLevel> TutorialLevels =
		Array.AsReadOnly(new[]
		{
			new AdventureCampaignLevel("fish", TutorialGameBoardScenePath),
			new AdventureCampaignLevel("lanternfish", ItemTutorialGameBoardScenePath),
		});

	private static readonly IReadOnlyList<AdventureCampaignLevel> MainLevels =
		Array.AsReadOnly(new[]
		{
			new AdventureCampaignLevel("shrimp", MainGameBoardScenePath),
			new AdventureCampaignLevel("crab", MainGameBoardScenePath),
			new AdventureCampaignLevel("jellyfish", MainGameBoardScenePath),
			new AdventureCampaignLevel("manta_ray", MainGameBoardScenePath),
			new AdventureCampaignLevel("garden_eel", MainGameBoardScenePath),
			new AdventureCampaignLevel("sea_slug", MainGameBoardScenePath),
			new AdventureCampaignLevel("nautilus", MainGameBoardScenePath),
			new AdventureCampaignLevel("narwhal", MainGameBoardScenePath),
			new AdventureCampaignLevel("leafy_seadragon", MainGameBoardScenePath),
			new AdventureCampaignLevel("dragon_prince_j", MainGameBoardScenePath),
			new AdventureCampaignLevel("dragon_queen_q", MainGameBoardScenePath),
			new AdventureCampaignLevel("boss_k", MainGameBoardScenePath),
		});

	private static readonly IReadOnlyList<AdventureCampaignDefinition> Campaigns =
		Array.AsReadOnly(new[]
		{
			new AdventureCampaignDefinition(TutorialCampaignId, "ADVENTURE_TUTORIAL", TutorialLevels),
			new AdventureCampaignDefinition(MainCampaignId, "ADVENTURE_DEEP_TIDE_THRONE", MainLevels),
		});

	public static IReadOnlyList<AdventureCampaignDefinition> All => Campaigns;
	public static IReadOnlyList<AdventureCampaignLevel> Tutorial => TutorialLevels;
	public static IReadOnlyList<AdventureCampaignLevel> Main => MainLevels;
	public static int MainStageCount => MainLevels.Count;

	public static bool IsTutorialAvatar(string avatarId)
	{
		return TryGetTutorialLevel(avatarId, out _);
	}

	public static bool IsMainAvatar(string avatarId)
	{
		return TryGetMainStageIndex(avatarId, out _);
	}

	public static bool TryGetTutorialLevel(string avatarId, out AdventureCampaignLevel level)
	{
		return TryFindLevel(TutorialLevels, avatarId, out level);
	}

	public static bool TryGetMainStageIndex(string avatarId, out int mainStageIndex)
	{
		string normalized = avatarId?.Trim() ?? string.Empty;
		for (int index = 0; index < MainLevels.Count; index++)
		{
			if (string.Equals(MainLevels[index].EnemyAvatarId, normalized, StringComparison.Ordinal))
			{
				mainStageIndex = index + 1;
				return true;
			}
		}

		mainStageIndex = 0;
		return false;
	}

	public static string GetMainAvatarId(int mainStageIndex)
	{
		if (mainStageIndex < 1 || mainStageIndex > MainStageCount)
		{
			return string.Empty;
		}

		return MainLevels[mainStageIndex - 1].EnemyAvatarId;
	}

	public static string GetCampaignIdForAvatar(string avatarId)
	{
		if (IsTutorialAvatar(avatarId))
		{
			return TutorialCampaignId;
		}

		return IsMainAvatar(avatarId) ? MainCampaignId : string.Empty;
	}

	public static int MapLegacyCompletedMainStage(int legacyHighestCompletedStage)
	{
		if (legacyHighestCompletedStage <= 2)
		{
			return 0;
		}

		return Math.Clamp(legacyHighestCompletedStage - 2, 0, LegacyMainStageCount);
	}

	public static bool TryMapLegacyRunStage(int legacyAvatarStage, out int mainStageIndex)
	{
		if (legacyAvatarStage is >= LegacyFirstMainAvatarStageNumber and <= LegacyLastMainAvatarStageNumber)
		{
			mainStageIndex = legacyAvatarStage - LegacyFirstMainAvatarStageNumber + 1;
			return true;
		}

		mainStageIndex = 0;
		return false;
	}

	public static int LegacyAvatarStageForMainStage(int mainStageIndex)
	{
		return mainStageIndex >= 1 && mainStageIndex <= MainStageCount
			? mainStageIndex + LegacyFirstMainAvatarStageNumber - 1
			: 0;
	}

	public static bool IsTutorialCompleted(string completedTutorialIdsCsv, string avatarId)
	{
		string normalizedAvatarId = avatarId?.Trim() ?? string.Empty;
		if (!IsTutorialAvatar(normalizedAvatarId))
		{
			return false;
		}

		foreach (string completedId in ParseCompletedTutorialIds(completedTutorialIdsCsv))
		{
			if (string.Equals(completedId, normalizedAvatarId, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	public static string AddCompletedTutorialId(string completedTutorialIdsCsv, string avatarId)
	{
		List<string> completedIds = ParseCompletedTutorialIds(completedTutorialIdsCsv);
		string normalizedAvatarId = avatarId?.Trim() ?? string.Empty;
		if (IsTutorialAvatar(normalizedAvatarId)
			&& !completedIds.Exists(existing => string.Equals(existing, normalizedAvatarId, StringComparison.Ordinal)))
		{
			completedIds.Add(normalizedAvatarId);
		}

		return string.Join("|", completedIds);
	}

	public static string NormalizeCompletedTutorialIdsCsv(string completedTutorialIdsCsv)
	{
		return string.Join("|", ParseCompletedTutorialIds(completedTutorialIdsCsv));
	}

	public static string NormalizePendingAvatarUnlockQueueCsv(string rawQueue)
	{
		var normalizedIds = new List<string>();
		if (string.IsNullOrWhiteSpace(rawQueue))
		{
			return string.Empty;
		}

		foreach (string rawId in rawQueue.Split(
			new[] { '|', ',', ';' },
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (string.IsNullOrWhiteSpace(rawId)
				|| IsTutorialAvatar(rawId)
				|| normalizedIds.Exists(existing => string.Equals(existing, rawId, StringComparison.Ordinal)))
			{
				continue;
			}

			normalizedIds.Add(rawId);
		}

		return string.Join("|", normalizedIds);
	}

	private static List<string> ParseCompletedTutorialIds(string completedTutorialIdsCsv)
	{
		var result = new List<string>();
		if (string.IsNullOrWhiteSpace(completedTutorialIdsCsv))
		{
			return result;
		}

		foreach (string rawId in completedTutorialIdsCsv.Split(
			new[] { '|', ',', ';' },
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (IsTutorialAvatar(rawId)
				&& !result.Exists(existing => string.Equals(existing, rawId, StringComparison.Ordinal)))
			{
				result.Add(rawId);
			}
		}

		return result;
	}

	private static bool TryFindLevel(
		IReadOnlyList<AdventureCampaignLevel> levels,
		string avatarId,
		out AdventureCampaignLevel level)
	{
		string normalized = avatarId?.Trim() ?? string.Empty;
		foreach (AdventureCampaignLevel candidate in levels)
		{
			if (string.Equals(candidate.EnemyAvatarId, normalized, StringComparison.Ordinal))
			{
				level = candidate;
				return true;
			}
		}

		level = default!;
		return false;
	}
}
