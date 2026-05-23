using Godot;

public partial class AdventureAvatarMenuScene
{
	private Control CreateEnemyPreviewPanel()
	{
		var panel = new PanelContainer
		{
			Name = "EnemyPreviewPanel",
			CustomMinimumSize = new Vector2(320.0f, 0.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(0.68f));

		var margin = new MarginContainer
		{
			Name = "EnemyPreviewMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.AddThemeConstantOverride("margin_left", 22);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_right", 22);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		panel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "EnemyPreviewLayout",
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 10);
		margin.AddChild(layout);

		var stageBadge = new PanelContainer
		{
			Name = "SelectedStageBadge",
			CustomMinimumSize = new Vector2(104.0f, 46.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		stageBadge.AddThemeStyleboxOverride("panel", CreateBadgeStyle(new Color(0.9f, 0.94f, 0.56f)));
		_previewStageLabel = new Label
		{
			Name = "SelectedStageLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_previewStageLabel.AddThemeFontSizeOverride("font_size", 26);
		_previewStageLabel.AddThemeColorOverride("font_color", new Color(0.98f, 1.0f, 0.86f));
		stageBadge.AddChild(_previewStageLabel);

		var badgeWrap = new CenterContainer
		{
			Name = "SelectedStageWrap",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		badgeWrap.AddChild(stageBadge);
		layout.AddChild(badgeWrap);

		var previewFrame = new PanelContainer
		{
			Name = "PreviewFrame",
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 200.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		previewFrame.AddThemeStyleboxOverride("panel", CreatePreviewFrameStyle());

		var previewAnchor = new CenterContainer
		{
			Name = "PreviewAvatarAnchor",
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_previewAvatar = CreateAvatarView(CharacterAvatarCatalog.Default, 190.0f, 0.84f);
		if (_previewAvatar != null)
		{
			_previewAvatar.Name = "PreviewAvatar";
			previewAnchor.AddChild(_previewAvatar);
		}
		previewFrame.AddChild(previewAnchor);
		layout.AddChild(previewFrame);

		_previewNameLabel = new Label
		{
			Name = "PreviewNameLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_previewNameLabel.AddThemeFontSizeOverride("font_size", 30);
		_previewNameLabel.AddThemeColorOverride("font_color", new Color(0.98f, 1.0f, 0.93f));
		_previewNameLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.92f));
		_previewNameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_previewNameLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		layout.AddChild(_previewNameLabel);

		_startButton = CreateActionButton("ADVENTURE_START", new Color(0.98f, 0.92f, 0.46f), StartAdventure, true, 224.0f);
		var startWrap = new CenterContainer
		{
			Name = "StartWrap",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		startWrap.AddChild(_startButton.GetParent());
		layout.AddChild(startWrap);

		return panel;
	}

	private CharacterAvatar? CreateAvatarView(CharacterAvatarDefinition avatarDefinition, float size, float portraitScale)
	{
		if (_characterAvatarScene?.Instantiate() is not CharacterAvatar avatar)
		{
			return null;
		}

		avatar.LoadActionFrames = false;
		avatar.CustomMinimumSize = new Vector2(size, size);
		avatar.AvatarRoot = avatarDefinition.RootPath;
		avatar.AccentColor = GetAvatarAccentColor(avatarDefinition);
		avatar.PortraitScale = portraitScale;
		avatar.MouseFilter = MouseFilterEnum.Ignore;
		return avatar;
	}
}
