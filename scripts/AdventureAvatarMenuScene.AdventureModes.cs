using System.Collections.Generic;
using Godot;

public partial class AdventureAvatarMenuScene
{
	private const string DeepTideThroneAdventureId = "deep_tide_throne";

	private static readonly AdventureLevelDefinition[] DeepTideThroneLevels =
	{
		new("fish", TutorialGameBoardScenePath),
		new("lanternfish", ItemTutorialGameBoardScenePath),
		new("shrimp"),
		new("crab"),
		new("jellyfish"),
		new("manta_ray"),
		new("garden_eel"),
		new("sea_slug"),
		new("nautilus"),
		new("narwhal"),
		new("leafy_seadragon"),
		new("dragon_prince_j"),
		new("dragon_queen_q"),
		new("boss_k"),
	};

	private static readonly AdventureModeDefinition[] AdventureModes =
	{
		new(
			DeepTideThroneAdventureId,
			"ADVENTURE_DEEP_TIDE_THRONE",
			DeepTideThroneLevels,
			new Color(0.58f, 0.98f, 1.0f)),
	};

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

			frame.AccentColor = adventureMode.AccentColor;
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
		RefreshAdventureModeSelection();
		RebuildLevelList();
		_selectedAvatarId = GetInitialAvatarId();
		RefreshSelection();
		ClampScrollTarget();
		ScrollSelectedEntryIntoView();
	}

	private void RefreshAdventureModeSelection()
	{
		foreach ((string adventureId, OceanMenuButtonFrame frame) in _adventureFrames)
		{
			bool selected = adventureId == _selectedAdventureId;
			frame.Selected = selected;
			frame.Modulate = selected ? Colors.White : new Color(0.78f, 0.90f, 0.94f, 0.76f);
			frame.QueueRedraw();

			if (_adventureButtons.TryGetValue(adventureId, out Button? button))
			{
				AdventureModeDefinition adventureMode = GetAdventureModeDefinition(adventureId);
				button.TooltipText = Localize(adventureMode.NameKey);
			}
		}
	}

	private List<CharacterAvatarDefinition> GetSelectedAdventureEnemyDefinitions()
	{
		var definitions = new List<CharacterAvatarDefinition>();
		AdventureModeDefinition adventureMode = GetSelectedAdventureDefinition();
		foreach (AdventureLevelDefinition level in adventureMode.Levels)
		{
			CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(level.EnemyAvatarId);
			if (avatarDefinition.Id == CharacterAvatarCatalog.DefaultAvatarId || avatarDefinition.AdventureStageNumber <= 0)
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
		foreach (CharacterAvatarDefinition avatarDefinition in GetSelectedAdventureEnemyDefinitions())
		{
			if (avatarDefinition.AdventureStageNumber == stageNumber)
			{
				return avatarDefinition;
			}
		}

		return null;
	}

	public static string GetGameBoardScenePathForAdventureAvatar(string avatarId)
	{
		return GetGameBoardScenePathForAvatar(DeepTideThroneAdventureId, avatarId);
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

	private sealed class AdventureLevelDefinition
	{
		public AdventureLevelDefinition(string enemyAvatarId, string? gameBoardScenePath = null)
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
