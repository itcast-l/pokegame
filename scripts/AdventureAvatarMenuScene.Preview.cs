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
		layout.AddThemeConstantOverride("separation", 9);
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

		_previewStatusLabel = new Label
		{
			Name = "PreviewStatusLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_previewStatusLabel.AddThemeFontSizeOverride("font_size", 18);
		_previewStatusLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.88f));
		_previewStatusLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_previewStatusLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		layout.AddChild(_previewStatusLabel);

		layout.AddChild(CreateAbilityPreviewCard());

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

	private Control CreateAbilityPreviewCard()
	{
		var panel = new PanelContainer
		{
			Name = "AbilityPreviewCard",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panel.AddThemeStyleboxOverride("panel", CreateAbilityCardStyle());

		var margin = new MarginContainer
		{
			Name = "AbilityPreviewMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		panel.AddChild(margin);

		var stack = new VBoxContainer
		{
			Name = "AbilityPreviewStack",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		stack.AddThemeConstantOverride("separation", 5);
		margin.AddChild(stack);

		_previewAbilityTitleLabel = new Label
		{
			Name = "AbilityTitleLabel",
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_previewAbilityTitleLabel.AddThemeFontSizeOverride("font_size", 14);
		_previewAbilityTitleLabel.AddThemeColorOverride("font_color", new Color(0.58f, 0.96f, 0.90f, 0.82f));
		stack.AddChild(_previewAbilityTitleLabel);

		_previewAbilityNameLabel = new Label
		{
			Name = "AbilityNameLabel",
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_previewAbilityNameLabel.AddThemeFontSizeOverride("font_size", 21);
		_previewAbilityNameLabel.AddThemeColorOverride("font_color", new Color(0.98f, 1.0f, 0.83f));
		_previewAbilityNameLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.82f));
		_previewAbilityNameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_previewAbilityNameLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		stack.AddChild(_previewAbilityNameLabel);

		_previewAbilityDescriptionLabel = new Label
		{
			Name = "AbilityDescriptionLabel",
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_previewAbilityDescriptionLabel.AddThemeFontSizeOverride("font_size", 16);
		_previewAbilityDescriptionLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.98f, 0.96f, 0.92f));
		_previewAbilityDescriptionLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.68f));
		_previewAbilityDescriptionLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_previewAbilityDescriptionLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		stack.AddChild(_previewAbilityDescriptionLabel);

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
