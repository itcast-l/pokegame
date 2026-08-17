using System;

public enum AdventureTutorialFlowKind
{
	BasicCapture,
	BombItem,
}

public static class AdventureTutorialFlowRules
{
	public static bool TryResolveForAvatar(
		string enemyAvatarId,
		out AdventureTutorialFlowKind flow,
		out string error)
	{
		flow = default;
		error = string.Empty;
		string normalizedAvatarId = CharacterAvatarCatalog.NormalizeAvatarId(enemyAvatarId);
		if (!AdventureCampaignCatalog.TryGetTutorialLevel(normalizedAvatarId, out AdventureCampaignLevel level))
		{
			error = $"Unknown adventure tutorial avatar '{enemyAvatarId}'.";
			return false;
		}

		if (string.Equals(
			level.GameBoardScenePath,
			AdventureCampaignCatalog.ItemTutorialGameBoardScenePath,
			StringComparison.Ordinal))
		{
			flow = AdventureTutorialFlowKind.BombItem;
			return true;
		}

		if (string.Equals(
			level.GameBoardScenePath,
			AdventureCampaignCatalog.TutorialGameBoardScenePath,
			StringComparison.Ordinal))
		{
			flow = AdventureTutorialFlowKind.BasicCapture;
			return true;
		}

		error = $"Tutorial metadata for '{normalizedAvatarId}' does not define a supported board scene.";
		return false;
	}

	public static string ToStableId(this AdventureTutorialFlowKind flow)
	{
		return flow switch
		{
			AdventureTutorialFlowKind.BasicCapture => "basic_capture",
			AdventureTutorialFlowKind.BombItem => "bomb_item",
			_ => throw new ArgumentOutOfRangeException(nameof(flow), flow, "Unsupported tutorial flow."),
		};
	}
}
