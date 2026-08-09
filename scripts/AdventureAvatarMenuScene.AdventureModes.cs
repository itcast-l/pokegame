using System;
using System.Collections.Generic;
using Godot;

public partial class AdventureAvatarMenuScene
{
	private const string TutorialAdventureId = AdventureCampaignCatalog.TutorialCampaignId;
	private const string DeepTideThroneAdventureId = AdventureCampaignCatalog.MainCampaignId;

	private static readonly AdventureModeDefinition[] AdventureModes = BuildAdventureModes();

	private static AdventureModeDefinition[] BuildAdventureModes()
	{
		var modes = new AdventureModeDefinition[AdventureCampaignCatalog.All.Count];
		for (int modeIndex = 0; modeIndex < AdventureCampaignCatalog.All.Count; modeIndex++)
		{
			AdventureCampaignDefinition campaign = AdventureCampaignCatalog.All[modeIndex];
			var levels = new AdventureLevelDefinition[campaign.Levels.Count];
			for (int levelIndex = 0; levelIndex < campaign.Levels.Count; levelIndex++)
			{
				AdventureCampaignLevel level = campaign.Levels[levelIndex];
				levels[levelIndex] = new AdventureLevelDefinition(
					level.EnemyAvatarId,
					level.GameBoardScenePath);
			}

			Color accentColor = campaign.Id == AdventureCampaignCatalog.TutorialCampaignId
				? new Color(0.42f, 0.88f, 0.60f)
				: new Color(0.32f, 0.72f, 0.50f);
			modes[modeIndex] = new AdventureModeDefinition(
				campaign.Id,
				campaign.NameKey,
				levels,
				accentColor);
		}

		return modes;
	}

	private void BindAdventureList()
	{
		_adventureFrames.Clear();
		_adventureButtons.Clear();
		foreach (AdventureModeDefinition adventureMode in AdventureModes)
		{
			string nodeName = $"%{adventureMode.Id}AdventureEntry";
			if (GetNodeOrNull<OceanMenuButtonFrame>(nodeName) is not OceanMenuButtonFrame frame)
			{
				GD.PushWarning($"Missing adventure mode entry scene node: {nodeName}");
				continue;
			}

			frame.FrameStyle = OceanMenuButtonFrame.ButtonFrameStyle.IndividualToolRow;
			frame.AccentColor = adventureMode.AccentColor;
			frame.HighlightFocus = false;
			_adventureFrames[adventureMode.Id] = frame;
			Label nameLabel = frame.GetNode<Label>("AdventureEntryContent/AdventureName");
			_localizedLabels.Add((nameLabel, adventureMode.NameKey));
			Button button = frame.GetNode<Button>("Button");
			button.FocusMode = Control.FocusModeEnum.All;
			button.Pressed += () => SelectAdventure(adventureMode.Id);
			button.FocusEntered += () => SelectAdventure(adventureMode.Id);
			_adventureButtons[adventureMode.Id] = button;
		}

		RefreshAdventureModeSelection();
	}

	private void SelectAdventure(string adventureId)
	{
		string normalized = NormalizeAdventureId(adventureId);
		if (_selectedAdventureId == normalized)
		{
			return;
		}

		_selectedAdventureId = normalized;
		_selectedAvatarId = GetInitialAvatarId();
		RefreshAdventureModeSelection();
		RebuildLevelList();
		RefreshSelection();
		ClampScrollTarget();
		ScrollSelectedEntryIntoView();
		FocusDefaultControlDeferred();
	}

	private void RefreshAdventureModeSelection()
	{
		foreach ((string adventureId, OceanMenuButtonFrame frame) in _adventureFrames)
		{
			bool selected = adventureId == _selectedAdventureId;
			frame.Selected = selected;
			frame.Modulate = selected ? Colors.White : new Color(0.78f, 0.90f, 0.94f, 0.76f);
			frame.QueueRedraw();
		}
	}

	private List<CharacterAvatarDefinition> GetSelectedAdventureEnemyDefinitions()
	{
		var definitions = new List<CharacterAvatarDefinition>();
		AdventureModeDefinition adventureMode = GetSelectedAdventureDefinition();
		foreach (AdventureLevelDefinition level in adventureMode.Levels)
		{
			CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(level.EnemyAvatarId);
			if (avatarDefinition.Id == CharacterAvatarCatalog.DefaultAvatarId
				|| GetCampaignStageIndex(level, adventureMode.Id) <= 0)
			{
				continue;
			}

			definitions.Add(avatarDefinition);
		}

		return definitions;
	}

	private bool IsAvatarInSelectedAdventure(string avatarId)
	{
		return GetSelectedAdventureLevelDefinition(avatarId) != null;
	}

	private CharacterAvatarDefinition? GetSelectedAdventureEnemyForStage(int stageNumber)
	{
		foreach (AdventureLevelDefinition level in GetSelectedAdventureDefinition().Levels)
		{
			CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(level.EnemyAvatarId);
			if (avatarDefinition.Id == CharacterAvatarCatalog.DefaultAvatarId)
			{
				continue;
			}

			if (GetCampaignStageIndex(level, GetSelectedAdventureDefinition().Id) == stageNumber)
			{
				return avatarDefinition;
			}
		}

		return null;
	}

	public static string GetGameBoardScenePathForAdventureAvatar(string avatarId)
	{
		string adventureId = GetAdventureIdForAvatar(avatarId);
		return GetGameBoardScenePathForAvatar(adventureId, avatarId);
	}

	private string GetGameBoardScenePathForAvatar(string avatarId)
	{
		return GetGameBoardScenePathForAvatar(_selectedAdventureId, avatarId);
	}

	private static string GetGameBoardScenePathForAvatar(string adventureId, string avatarId)
	{
		string normalized = CharacterAvatarCatalog.NormalizeAvatarId(avatarId);
		foreach (AdventureLevelDefinition level in GetAdventureModeDefinition(adventureId).Levels)
		{
			if (CharacterAvatarCatalog.NormalizeAvatarId(level.EnemyAvatarId) == normalized)
			{
				return level.GameBoardScenePath;
			}
		}

		return GameBoardScenePath;
	}

	private AdventureLevelDefinition? GetSelectedAdventureLevelDefinition(string avatarId)
	{
		string normalized = CharacterAvatarCatalog.NormalizeAvatarId(avatarId);
		foreach (AdventureLevelDefinition level in GetSelectedAdventureDefinition().Levels)
		{
			if (CharacterAvatarCatalog.NormalizeAvatarId(level.EnemyAvatarId) == normalized)
			{
				return level;
			}
		}

		return null;
	}

	private AdventureModeDefinition GetSelectedAdventureDefinition()
	{
		return GetAdventureModeDefinition(_selectedAdventureId);
	}

	private string GetInitialAdventureId()
	{
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		if (profile.HighestCompletedMainAdventureStage > 0
			|| !string.IsNullOrWhiteSpace(profile.PendingAdventureRunJson))
		{
			return DeepTideThroneAdventureId;
		}

		if (!CharacterAvatarCatalog.IsTutorialCompleted(
			profile.CompletedAdventureTutorialIdsCsv,
			AdventureCampaignCatalog.Tutorial[0].EnemyAvatarId)
			|| !CharacterAvatarCatalog.IsTutorialCompleted(
				profile.CompletedAdventureTutorialIdsCsv,
				AdventureCampaignCatalog.Tutorial[1].EnemyAvatarId))
		{
			return TutorialAdventureId;
		}

		return DeepTideThroneAdventureId;
	}

	private static string GetAdventureIdForAvatar(string avatarId)
	{
		string normalized = CharacterAvatarCatalog.NormalizeAvatarId(avatarId);
		foreach (AdventureModeDefinition adventureMode in AdventureModes)
		{
			foreach (AdventureLevelDefinition level in adventureMode.Levels)
			{
				if (CharacterAvatarCatalog.NormalizeAvatarId(level.EnemyAvatarId) == normalized)
				{
					return adventureMode.Id;
				}
			}
		}

		return AdventureModes[0].Id;
	}

	private static AdventureModeDefinition GetAdventureModeDefinition(string adventureId)
	{
		string normalized = NormalizeAdventureId(adventureId);
		foreach (AdventureModeDefinition adventureMode in AdventureModes)
		{
			if (adventureMode.Id == normalized)
			{
				return adventureMode;
			}
		}

		return AdventureModes[0];
	}

	private static string NormalizeAdventureId(string adventureId)
	{
		foreach (AdventureModeDefinition adventureMode in AdventureModes)
		{
			if (adventureMode.Id == adventureId)
			{
				return adventureMode.Id;
			}
		}

		return AdventureModes[0].Id;
	}

	private static int GetCampaignStageIndex(AdventureLevelDefinition level, string adventureId)
	{
		if (adventureId == AdventureCampaignCatalog.MainCampaignId
			&& AdventureCampaignCatalog.TryGetMainStageIndex(level.EnemyAvatarId, out int mainStageIndex))
		{
			return mainStageIndex;
		}

		for (int index = 0; index < AdventureCampaignCatalog.Tutorial.Count; index++)
		{
			if (string.Equals(
				AdventureCampaignCatalog.Tutorial[index].EnemyAvatarId,
				level.EnemyAvatarId,
				StringComparison.Ordinal))
			{
				return index + 1;
			}
		}

		return 0;
	}

	private sealed class AdventureLevelDefinition
	{
		public AdventureLevelDefinition(
			string enemyAvatarId,
			string? gameBoardScenePath = null)
		{
			EnemyAvatarId = enemyAvatarId;
			GameBoardScenePath = string.IsNullOrWhiteSpace(gameBoardScenePath)
				? AdventureAvatarMenuScene.GameBoardScenePath
				: gameBoardScenePath;
		}

		public string EnemyAvatarId { get; }
		public string GameBoardScenePath { get; }
	}

	private sealed class AdventureModeDefinition
	{
		public AdventureModeDefinition(
			string id,
			string nameKey,
			AdventureLevelDefinition[] levels,
			Color accentColor)
		{
			Id = id;
			NameKey = nameKey;
			Levels = levels;
			AccentColor = accentColor;
		}

		public string Id { get; }
		public string NameKey { get; }
		public AdventureLevelDefinition[] Levels { get; }
		public Color AccentColor { get; }
	}
}
