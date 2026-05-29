using Godot;

public partial class AdventureAvatarMenuScene
{
	private Control CreateLevelListPanel()
	{
		var panel = new PanelContainer
		{
			Name = "LevelListPanel",
			CustomMinimumSize = new Vector2(300.0f, 0.0f),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(0.52f));

		var margin = new MarginContainer
		{
			Name = "LevelListMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "LevelListLayout",
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 10);
		margin.AddChild(layout);

		layout.AddChild(CreateColumnTitleLabel("ADVENTURE_ENEMY_LIST_TITLE"));

		var scroll = new ScrollContainer
		{
			Name = "LevelScroll",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
			MouseFilter = MouseFilterEnum.Stop,
		};
		_levelScroll = scroll;
		scroll.GuiInput += OnLevelScrollGuiInput;
		scroll.GetVScrollBar().ValueChanged += value => _levelScrollTarget = (int)Mathf.Round(value);
		layout.AddChild(scroll);

		var levelList = new VBoxContainer
		{
			Name = "LevelList",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		levelList.AddThemeConstantOverride("separation", 8);
		_levelList = levelList;
		scroll.AddChild(levelList);

		RebuildLevelList();
		return panel;
	}

	private void RebuildLevelList()
	{
		if (_levelList == null || !GodotObject.IsInstanceValid(_levelList))
		{
			return;
		}

		while (_levelList.GetChildCount() > 0)
		{
			Node child = _levelList.GetChild(0);
			_levelList.RemoveChild(child);
			child.QueueFree();
		}

		_avatarFrames.Clear();
		_stageNumbers.Clear();
		_stageButtons.Clear();
		_stageStatusLabels.Clear();
		foreach (CharacterAvatarDefinition avatarDefinition in GetSelectedAdventureEnemyDefinitions())
		{
			if (avatarDefinition.AdventureStageNumber <= 0)
			{
				continue;
			}

			_levelList.AddChild(CreateLevelEntry(avatarDefinition, avatarDefinition.AdventureStageNumber));
		}
	}

	private Control CreateLevelEntry(CharacterAvatarDefinition avatarDefinition, int stageNumber)
	{
		Color accentColor = GetAvatarAccentColor(avatarDefinition);
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{avatarDefinition.Id}LevelEntry",
			CustomMinimumSize = new Vector2(0.0f, 68.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			AccentColor = accentColor,
		};
		_avatarFrames[avatarDefinition.Id] = frame;
		_stageNumbers[avatarDefinition.Id] = stageNumber;

		var contentMargin = new MarginContainer
		{
			Name = "EntryContent",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		contentMargin.SetAnchorsPreset(LayoutPreset.FullRect);
		contentMargin.OffsetLeft = 18.0f;
		contentMargin.OffsetTop = 6.0f;
		contentMargin.OffsetRight = -18.0f;
		contentMargin.OffsetBottom = -6.0f;
		frame.AddChild(contentMargin);

		var row = new HBoxContainer
		{
			Name = "EntryRow",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 10);
		contentMargin.AddChild(row);

		var badge = new PanelContainer
		{
			Name = "StageBadge",
			CustomMinimumSize = new Vector2(48.0f, 48.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		badge.AddThemeStyleboxOverride("panel", CreateBadgeStyle(accentColor));
		var badgeLabel = new Label
		{
			Name = "StageNumber",
			Text = FormatStageNumber(stageNumber),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		badgeLabel.AddThemeFontSizeOverride("font_size", 21);
		badgeLabel.AddThemeColorOverride("font_color", new Color(0.98f, 1.0f, 0.9f));
		badge.AddChild(badgeLabel);
		row.AddChild(badge);

		var avatarAnchor = new CenterContainer
		{
			Name = "EntryAvatarAnchor",
			CustomMinimumSize = new Vector2(50.0f, 50.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		CharacterAvatar? avatar = CreateAvatarView(avatarDefinition, 50.0f, 0.74f);
		if (avatar != null)
		{
			avatar.Name = "EntryAvatar";
			avatarAnchor.AddChild(avatar);
		}
		row.AddChild(avatarAnchor);

		var nameLabel = new Label
		{
			Name = "EntryName",
			Text = avatarDefinition.DisplayName,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 24);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		nameLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.92f));
		nameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		nameLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		row.AddChild(nameLabel);

		var statusLabel = new Label
		{
			Name = "EntryStatus",
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(78.0f, 0.0f),
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		statusLabel.AddThemeFontSizeOverride("font_size", 16);
		statusLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		statusLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		statusLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		_stageStatusLabels[avatarDefinition.Id] = statusLabel;
		row.AddChild(statusLabel);

		var arrowLabel = new Label
		{
			Name = "EntryArrow",
			Text = ">",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(26.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		arrowLabel.AddThemeFontSizeOverride("font_size", 20);
		arrowLabel.AddThemeColorOverride("font_color", new Color(accentColor.R, accentColor.G, accentColor.B, 0.82f));
		row.AddChild(arrowLabel);

		var button = new Button
		{
			Name = "Button",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			TooltipText = avatarDefinition.DisplayName,
			MouseFilter = MouseFilterEnum.Stop,
		};
		button.SetAnchorsPreset(LayoutPreset.FullRect);
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.Pressed += () => SelectAvatar(avatarDefinition.Id);
		frame.AddChild(button);
		_stageButtons[avatarDefinition.Id] = button;

		return frame;
	}
}
