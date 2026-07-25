using System.Reflection;
using Godot;

public partial class AdventureStageUnlockHarness : Node
{
	private const string AdventureMenuScenePath = "res://scenes/adventure_avatar_menu_scene.tscn";

	public int GetStageCount()
	{
		return CharacterAvatarCatalog.GetAdventureStageCount();
	}

	public string[] GetCampaignEnemyAvatarIds(string campaignId)
	{
		foreach (AdventureCampaignDefinition campaign in AdventureCampaignCatalog.All)
		{
			if (!string.Equals(campaign.Id, campaignId, System.StringComparison.Ordinal))
			{
				continue;
			}

			var avatarIds = new string[campaign.Levels.Count];
			for (int index = 0; index < campaign.Levels.Count; index++)
			{
				avatarIds[index] = campaign.Levels[index].EnemyAvatarId;
			}

			return avatarIds;
		}

		return System.Array.Empty<string>();
	}

	public bool IsStageUnlocked(int stageNumber)
	{
		return CharacterAvatarCatalog.IsAdventureStageUnlocked(stageNumber);
	}

	public bool SetHighestCompletedStage(int stageNumber)
	{
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		return GameSaveStore.SaveActiveProfile(profile with
		{
			HighestCompletedMainAdventureStage = stageNumber,
			CompletedAdventureTutorialIdsCsv = string.Empty,
			PendingAvatarUnlockQueue = string.Empty,
		}) == Error.Ok;
	}

	public bool ResetProgress()
	{
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		return GameSaveStore.SaveActiveProfile(profile with
		{
			HighestCompletedMainAdventureStage = 0,
			CompletedAdventureTutorialIdsCsv = string.Empty,
			PendingAvatarUnlockQueue = string.Empty,
			PendingAdventureRunJson = string.Empty,
		}) == Error.Ok;
	}

	public int GetHighestCompletedMainStage()
	{
		return CharacterAvatarCatalog.LoadHighestCompletedAdventureStage();
	}

	public bool IsTutorialCompleted(string tutorialAvatarId)
	{
		return CharacterAvatarCatalog.IsTutorialCompleted(tutorialAvatarId);
	}

	public bool RecordTutorialVictory(string tutorialAvatarId)
	{
		if (!AdventureCampaignCatalog.IsTutorialAvatar(tutorialAvatarId))
		{
			return false;
		}

		CharacterAvatarCatalog.RecordAdventureVictory(tutorialAvatarId);
		return CharacterAvatarCatalog.IsTutorialCompleted(tutorialAvatarId)
			&& CharacterAvatarCatalog.LoadHighestCompletedAdventureStage() == 0;
	}

	public bool NetworkStandardLeavesResidualAdventureRunUntouched()
	{
		GameSaveStore.GameProgressData originalProfile = GameSaveStore.LoadActiveProfile();
		const string residualJson = "{\"SchemaVersion\":6,\"CampaignId\":\"deep_tide_throne\",\"MainStageIndex\":1,\"EnemyAvatarId\":\"shrimp\",\"ShouldGrantRunStartUpgrade\":true,\"ShouldGrantVictoryUpgrade\":true,\"AcquiredUpgradeIds\":[],\"CurrentHealth\":100,\"MaxHealth\":100,\"PermanentCaptureDamageBonus\":0,\"QuestionRewardCount\":0,\"HealingOverflowRemainder\":0}";
		if (GameSaveStore.SaveActiveProfile(originalProfile with { PendingAdventureRunJson = residualJson }) != Error.Ok)
		{
			return false;
		}

		try
		{
			var store = new AdventureRunSessionStore();
			store.ConfigureForPolicy(GameModePolicy.NetworkStandard);
			bool blocked = store.ModePolicy.Kind == GameModeKind.NetworkStandard
				&& store.ReloadFromActiveProfile() == AdventureUpgradeRestoreResult.NoSnapshot
				&& !store.HasActiveRun
				&& !store.TryGetSnapshot(out _);
			store.BeginNewMainRun(1, "shrimp");
			store.ClearRun();
			GameSaveStore.GameProgressData unchangedProfile = GameSaveStore.LoadActiveProfile();
			return blocked && unchangedProfile.PendingAdventureRunJson == residualJson;
		}
		finally
		{
			GameSaveStore.SaveActiveProfile(originalProfile);
		}
	}

	public bool RecordStageVictory(int stageNumber)
	{
		CharacterAvatarCatalog.RecordAdventureVictory(CharacterAvatarCatalog.GetAdventureAvatarIdForStage(stageNumber));
		return CharacterAvatarCatalog.LoadHighestCompletedAdventureStage() >= stageNumber;
	}

	public bool TryStartMenuStage(int stageNumber)
	{
		PackedScene? packedScene = GD.Load<PackedScene>(AdventureMenuScenePath);
		if (packedScene?.Instantiate() is not AdventureAvatarMenuScene menu)
		{
			return false;
		}

		GetTree().Root.AddChild(menu);
		FieldInfo? selectedAvatarField = typeof(AdventureAvatarMenuScene).GetField(
			"_selectedAvatarId",
			BindingFlags.Instance | BindingFlags.NonPublic);
		MethodInfo? startAdventureMethod = typeof(AdventureAvatarMenuScene).GetMethod(
			"StartAdventure",
			BindingFlags.Instance | BindingFlags.NonPublic);
		FieldInfo? isStartingField = typeof(AdventureAvatarMenuScene).GetField(
			"_isStartingAdventure",
			BindingFlags.Instance | BindingFlags.NonPublic);
		if (selectedAvatarField == null || startAdventureMethod == null || isStartingField == null)
		{
			menu.QueueFree();
			return false;
		}

		selectedAvatarField.SetValue(menu, CharacterAvatarCatalog.GetAdventureAvatarIdForStage(stageNumber));
		startAdventureMethod.Invoke(menu, null);
		bool started = isStartingField.GetValue(menu) as bool? ?? false;
		menu.QueueFree();
		return started;
	}
}
