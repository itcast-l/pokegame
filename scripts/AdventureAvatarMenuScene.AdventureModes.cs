using System.Collections.Generic;
using Godot;

public partial class AdventureAvatarMenuScene
{
	private const string DeepTideThroneAdventureId = "deep_tide_throne";

	private static readonly string[] DeepTideThroneEnemyAvatarIds =
	{
		"fish",
		"lanternfish",
		"shrimp",
		"crab",
		"jellyfish",
		"manta_ray",
		"garden_eel",
		"sea_slug",
		"nautilus",
		"narwhal",
		"leafy_seadragon",
		"dragon_prince_j",
		"dragon_queen_q",
		"boss_k",
	};

	private static readonly AdventureModeDefinition[] AdventureModes =
	{
		new(
			DeepTideThroneAdventureId,
			"ADVENTURE_DEEP_TIDE_THRONE",
			DeepTideThroneEnemyAvatarIds,
			new Color(0.58f, 0.98f, 1.0f)),
	};

	private Control CreateAdventureListPanel()
	{
		var panel = new PanelContainer
		{
			Name = "AdventureListPanel",
			CustomMinimumSize = new Vector2(190.0f, 0.0f),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(0.50f));

		var margin = new MarginContainer
		{
			Name = "AdventureListMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "AdventureList",
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 10);
		margin.AddChild(layout);

		layout.AddChild(CreateColumnTitleLabel("ADVENTURE_LIST_TITLE"));

		_adventureFrames.Clear();
		_adventureButtons.Clear();
		foreach (AdventureModeDefinition adventureMode in AdventureModes)
		{
			layout.AddChild(CreateAdventureEntry(adventureMode));
		}

		layout.AddChild(new Control
		{
			Name = "AdventureListFill",
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		});

		RefreshAdventureModeSelection();
		return panel;
	}

	private Control CreateAdventureEntry(AdventureModeDefinition adventureMode)
	{
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{adventureMode.Id}AdventureEntry",
			CustomMinimumSize = new Vector2(0.0f, 64.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			AccentColor = adventureMode.AccentColor,
		};
		_adventureFrames[adventureMode.Id] = frame;

		var margin = new MarginContainer
		{
			Name = "AdventureEntryContent",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.OffsetLeft = 32.0f;
		margin.OffsetTop = 8.0f;
		margin.OffsetRight = -16.0f;
		margin.OffsetBottom = -8.0f;
		frame.AddChild(margin);

		var nameLabel = new Label
		{
			Name = "AdventureName",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 22);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.94f, 1.0f, 0.98f));
		nameLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.90f));
		nameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		nameLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		_localizedLabels.Add((nameLabel, adventureMode.NameKey));
		margin.AddChild(nameLabel);

		var button = new Button
		{
			Name = "Button",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		button.SetAnchorsPreset(LayoutPreset.FullRect);
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.Pressed += () => SelectAdventure(adventureMode.Id);
		frame.AddChild(button);
		_adventureButtons[adventureMode.Id] = button;

		return frame;
	}

	private Label CreateColumnTitleLabel(string key)
	{
		var label = new Label
		{
			Name = $"{key}Label",
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", new Color(0.60f, 0.96f, 0.92f, 0.86f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.78f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		_localizedLabels.Add((label, key));
		return label;
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
		foreach (string avatarId in adventureMode.EnemyAvatarIds)
		{
			CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(avatarId);
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
		string normalized = CharacterAvatarCatalog.NormalizeAvatarId(avatarId);
		foreach (CharacterAvatarDefinition avatarDefinition in GetSelectedAdventureEnemyDefinitions())
		{
			if (avatarDefinition.Id == normalized)
			{
				return true;
			}
		}

		return false;
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

	private sealed class AdventureModeDefinition
	{
		public AdventureModeDefinition(
			string id,
			string nameKey,
			string[] enemyAvatarIds,
			Color accentColor)
		{
			Id = id;
			NameKey = nameKey;
			EnemyAvatarIds = enemyAvatarIds;
			AccentColor = accentColor;
		}

		public string Id { get; }
		public string NameKey { get; }
		public string[] EnemyAvatarIds { get; }
		public Color AccentColor { get; }
	}
}
