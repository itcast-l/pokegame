using System;
using System.Collections.Generic;

public sealed record AdventureCampaignLevel(
	string EnemyAvatarId,
	string GameBoardScenePath,
	int MainStageIndex = 0);

public sealed record AdventureCampaignDefinition(
	string Id,
	string NameKey,
	IReadOnlyList<AdventureCampaignLevel> Levels);

public static class AdventureCampaignCatalog
{
	public const string TutorialCampaignId = "tutorial";
	public const string MainCampaignId = "deep_tide_throne";
	public const int MainStageCount = 12;
	public const string TutorialGameBoardScenePath = "res://scenes/adventure_tutorial_game_board_scene.tscn";
	public const string ItemTutorialGameBoardScenePath = "res://scenes/adventure_item_tutorial_game_board_scene.tscn";
	public const string MainGameBoardScenePath = "res://scenes/game_board_scene.tscn";

	private static readonly IReadOnlyList<AdventureCampaignLevel> TutorialLevels =
		Array.AsReadOnly(new[]
		{
			new AdventureCampaignLevel("fish", TutorialGameBoardScenePath),
			new AdventureCampaignLevel("lanternfish", ItemTutorialGameBoardScenePath),
		});

	private static readonly IReadOnlyList<AdventureCampaignLevel> MainLevels =
		Array.AsReadOnly(new[]
		{
			new AdventureCampaignLevel("shrimp", MainGameBoardScenePath, 1),
			new AdventureCampaignLevel("crab", MainGameBoardScenePath, 2),
			new AdventureCampaignLevel("jellyfish", MainGameBoardScenePath, 3),
			new AdventureCampaignLevel("manta_ray", MainGameBoardScenePath, 4),
			new AdventureCampaignLevel("garden_eel", MainGameBoardScenePath, 5),
			new AdventureCampaignLevel("sea_slug", MainGameBoardScenePath, 6),
			new AdventureCampaignLevel("nautilus", MainGameBoardScenePath, 7),
			new AdventureCampaignLevel("narwhal", MainGameBoardScenePath, 8),
			new AdventureCampaignLevel("leafy_seadragon", MainGameBoardScenePath, 9),
			new AdventureCampaignLevel("dragon_prince_j", MainGameBoardScenePath, 10),
			new AdventureCampaignLevel("dragon_queen_q", MainGameBoardScenePath, 11),
			new AdventureCampaignLevel("boss_k", MainGameBoardScenePath, 12),
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
		if (TryFindLevel(MainLevels, avatarId, out AdventureCampaignLevel level))
		{
			mainStageIndex = level.MainStageIndex;
			return true;
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

		return Math.Clamp(legacyHighestCompletedStage - 2, 0, MainStageCount);
	}

	public static bool TryMapLegacyRunStage(int legacyAvatarStage, out int mainStageIndex)
	{
		if (legacyAvatarStage is >= 3 and <= 14)
		{
			mainStageIndex = legacyAvatarStage - 2;
			return true;
		}

		mainStageIndex = 0;
		return false;
	}

	public static int LegacyAvatarStageForMainStage(int mainStageIndex)
	{
		return mainStageIndex is >= 1 and <= MainStageCount ? mainStageIndex + 2 : 0;
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
