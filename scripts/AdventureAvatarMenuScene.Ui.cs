using Godot;

public partial class AdventureAvatarMenuScene
{
	private static readonly Color AdventureRouteAccent = MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette.ActiveBorder;
	private const float AdventureMenuSafeAreaInset = 32.0f;
	private const float ExpandedAdventureMenuMinimumHeight = 720.0f;

	private void BindUi()
	{
		_backButton = GetNode<Button>("%BackButton");
		_startButton = GetNode<Button>("%StartButton");
		SuppressDefaultPressedSfx(_backButton);
		_levelScroll = GetNode<ScrollContainer>("%LevelScroll");
		_levelList = GetNode<VBoxContainer>("%LevelList");
		_previewStageLabel = GetNode<Label>("%SelectedStageLabel");
		_previewNameLabel = GetNode<Label>("%PreviewNameLabel");
		_previewStatusLabel = GetNode<Label>("%PreviewStatusLabel");
		_previewAbilityTitleLabel = GetNode<Label>("%AbilityTitleLabel");
		_previewAbilityNameLabel = GetNode<Label>("%AbilityNameLabel");
		_previewAbilityDescriptionScroll = GetNode<ScrollContainer>("%AbilityDescriptionScroll");
		_previewAbilityDescriptionLabel = GetNode<Label>("%AbilityDescriptionLabel");
		_previewAvatar = GetNode<CharacterAvatar>("%PreviewAvatar");
		_previewAvatar.LoadActionFrames = true;

		_backButton.Pressed += ReturnToMainMenu;
		_startButton.Pressed += StartAdventure;
		ConfigureUnifiedVisualStyle();
		ConfigureAdventureMenuFocus();
		_levelScroll.GuiInput += OnLevelScrollGuiInput;
		_levelScroll.GetVScrollBar().ValueChanged += value => _levelScrollTarget = (int)Mathf.Round(value);

		_localizedLabels.Add((GetNode<Label>("%TitleLabel"), "ADVENTURE_MENU_TITLE"));
		_localizedLabels.Add((GetNode<Label>("%AdventureListTitle"), "ADVENTURE_LIST_TITLE"));
		_localizedLabels.Add((GetNode<Label>("%EnemyListTitle"), "ADVENTURE_ENEMY_LIST_TITLE"));
		_localizedButtons.Add((_backButton, "ADVENTURE_BACK"));
		_localizedButtons.Add((_startButton, "ADVENTURE_START"));

		BindAdventureList();
		BindLevelList();
		EnsureAvatarUnlockPrompt();
		CallDeferred(nameof(ApplyResponsiveAdventureLayout));
	}

	private void ConfigureUnifiedVisualStyle()
	{
		SetMeta(SecondaryMenuGlassStyle.VisualProfileMetadata, SecondaryMenuGlassStyle.UnifiedVisual.Id);
		SecondaryMenuGlassStyle.ApplyReadabilityVeil(
			GetNodeOrNull<ColorRect>("%ReadabilityVeil"),
			SecondaryMenuGlassStyle.UnifiedVisual);

		PanelContainer menuPanel = FindRequired<PanelContainer>("AdventureMenuPanel");
		GetNode<ColorRect>("%GlassSurface").Material = SecondaryMenuGlassStyle.CreateGlassMaterial(
			contentPane: false,
			SecondaryMenuGlassStyle.UnifiedVisual);
		GetNode<Panel>("%GlassFrame").AddThemeStyleboxOverride(
			"panel",
			SecondaryMenuGlassStyle.CreateFrameStyle(contentPane: false, SecondaryMenuGlassStyle.UnifiedVisual));
		menuPanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

		ShaderMaterial contentMaterial = SecondaryMenuGlassStyle.CreateGlassMaterial(
			contentPane: true,
			SecondaryMenuGlassStyle.UnifiedVisual);
		StyleBoxFlat contentStyle = SecondaryMenuGlassStyle.CreateFrameStyle(
			contentPane: true,
			SecondaryMenuGlassStyle.UnifiedVisual);
		foreach ((string panelName, string surfaceName) in new[]
		{
			("AdventureListPanel", "AdventureListGlassSurface"),
			("LevelListPanel", "LevelListGlassSurface"),
			("EnemyPreviewPanel", "EnemyPreviewGlassSurface"),
		})
		{
			FindRequired<PanelContainer>(panelName).AddThemeStyleboxOverride("panel", contentStyle);
			GetNode<ColorRect>($"%{surfaceName}").Material = contentMaterial;
		}

		ConfigureFrame("BackFrame", MainMenuInteractiveStyle.SecondaryMenuPalette.ActiveBorder);
		ConfigureFrame("StartFrame", AdventureRouteAccent);
		MainMenuInteractiveStyle.ApplyActionButton(_backButton, 18, MainMenuInteractiveStyle.SecondaryMenuPalette);
		MainMenuInteractiveStyle.ApplyActionButton(_startButton, 19, MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
		_startButton.OffsetLeft = 0.0f;
		_startButton.OffsetTop = 0.0f;
		_startButton.OffsetRight = 0.0f;
		_startButton.OffsetBottom = 0.0f;
		SecondaryMenuGlassStyle.StyleScrollBar(_levelScroll.GetVScrollBar(), SecondaryMenuGlassStyle.UnifiedVisual);

		FindRequired<ColorRect>("TitleUnderline").Color = new Color(0.62f, 1.0f, 0.94f, 0.58f);
		GetNode<Label>("%AdventureListTitle").AddThemeColorOverride("font_color", new Color(0.72f, 0.98f, 0.93f));
		GetNode<Label>("%EnemyListTitle").AddThemeColorOverride("font_color", new Color(0.72f, 0.98f, 0.93f));
		_previewAbilityTitleLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.82f, 0.80f));
		_previewAbilityNameLabel.AddThemeColorOverride("font_color", new Color(0.91f, 0.94f, 0.94f));
		FindRequired<PanelContainer>("AbilityPreviewCard").AddThemeStyleboxOverride("panel", CreateAbilityCardStyle());
		FindRequired<PanelContainer>("SelectedStageBadge").AddThemeStyleboxOverride(
			"panel",
			CreateBadgeStyle(AdventureRouteAccent));
	}

	private void ConfigureFrame(string frameName, Color accent)
	{
		if (FindChild(frameName, true, false) is not OceanMenuButtonFrame frame)
		{
			return;
		}

		frame.FrameStyle = OceanMenuButtonFrame.ButtonFrameStyle.IndividualAction;
		frame.AccentColor = accent;
		frame.QueueRedraw();
	}

	private void ApplyResponsiveAdventureLayout()
	{
		if (!IsNodeReady())
		{
			return;
		}

		Vector2 viewportSize = Size;
		if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
		{
			viewportSize = GetViewportRect().Size;
		}
		Vector2 available = new(
			Mathf.Max(1.0f, viewportSize.X - AdventureMenuSafeAreaInset * 2.0f),
			Mathf.Max(1.0f, viewportSize.Y - AdventureMenuSafeAreaInset * 2.0f));
		bool compactWidth = viewportSize.X < 1100.0f;
		bool compactHeight = available.Y < ExpandedAdventureMenuMinimumHeight;
		PanelContainer menuPanel = FindRequired<PanelContainer>("AdventureMenuPanel");
		menuPanel.CustomMinimumSize = new Vector2(
			Mathf.Min(1160.0f, available.X),
			Mathf.Min(620.0f, available.Y));

		FindRequired<PanelContainer>("AdventureListPanel").CustomMinimumSize = new Vector2(compactWidth ? 210.0f : 340.0f, 0.0f);
		FindRequired<PanelContainer>("LevelListPanel").CustomMinimumSize = new Vector2(compactWidth ? 310.0f : 420.0f, 0.0f);
		FindRequired<PanelContainer>("EnemyPreviewPanel").CustomMinimumSize = new Vector2(compactWidth ? 240.0f : 320.0f, 0.0f);
		FindRequired<HBoxContainer>("Body").AddThemeConstantOverride("separation", compactWidth ? 10 : 14);

		MarginContainer menuMargin = FindRequired<MarginContainer>("AdventureMenuMargin");
		SetMargins(menuMargin, compactWidth ? 16 : 24, compactHeight ? 12 : 22, compactWidth ? 16 : 24, compactHeight ? 12 : 22);
		MarginContainer previewMargin = FindRequired<MarginContainer>("EnemyPreviewMargin");
		SetMargins(previewMargin, compactWidth ? 10 : 22, compactHeight ? 8 : 18, compactWidth ? 10 : 22, compactHeight ? 8 : 18);

		FindRequired<PanelContainer>("PreviewFrame").CustomMinimumSize = new Vector2(0.0f, compactHeight ? 78.0f : 140.0f);
		CharacterAvatar previewAvatar = FindRequired<CharacterAvatar>("PreviewAvatar");
		previewAvatar.CustomMinimumSize = new Vector2(compactHeight ? 76.0f : 138.0f, compactHeight ? 76.0f : 138.0f);
		previewAvatar.PortraitScale = compactHeight ? 0.64f : 0.8f;
		FindRequired<PanelContainer>("AbilityPreviewCard").CustomMinimumSize = new Vector2(0.0f, compactHeight ? 82.0f : 148.0f);
		FindRequired<PanelContainer>("SelectedStageBadge").CustomMinimumSize = new Vector2(compactWidth ? 76.0f : 96.0f, compactHeight ? 34.0f : 42.0f);
		FindRequired<OceanMenuButtonFrame>("StartFrame").CustomMinimumSize = new Vector2(compactWidth ? 180.0f : 224.0f, compactHeight ? 44.0f : 56.0f);
		GetNode<Label>("%TitleLabel").AddThemeFontSizeOverride("font_size", compactHeight ? 29 : 36);
		_previewNameLabel.AddThemeFontSizeOverride("font_size", compactHeight ? 24 : 30);
		_previewAbilityNameLabel.AddThemeFontSizeOverride("font_size", compactHeight ? 17 : 21);
		_previewAbilityDescriptionLabel.AddThemeFontSizeOverride("font_size", compactHeight ? 14 : 16);

		foreach (OceanMenuButtonFrame frame in _adventureFrames.Values)
		{
			frame.CustomMinimumSize = new Vector2(compactWidth ? 0.0f : 316.0f, compactHeight ? 52.0f : 64.0f);
		}
		foreach (OceanMenuButtonFrame frame in _avatarFrames.Values)
		{
			frame.CustomMinimumSize = new Vector2(compactWidth ? 0.0f : 392.0f, compactHeight ? 56.0f : 68.0f);
		}
	}

	private static void SetMargins(MarginContainer margin, int left, int top, int right, int bottom)
	{
		margin.AddThemeConstantOverride("margin_left", left);
		margin.AddThemeConstantOverride("margin_top", top);
		margin.AddThemeConstantOverride("margin_right", right);
		margin.AddThemeConstantOverride("margin_bottom", bottom);
	}

	private T FindRequired<T>(string nodeName) where T : Node
	{
		return FindChild(nodeName, recursive: true, owned: false) as T
			?? throw new System.InvalidOperationException($"Missing required adventure menu node: {nodeName}");
	}

	private void ConfigureAvatarView(CharacterAvatar avatar, CharacterAvatarDefinition avatarDefinition, float portraitScale)
	{
		avatar.LoadActionFrames = false;
		avatar.AvatarRoot = avatarDefinition.RootPath;
		avatar.AudioConfig = avatarDefinition.AudioConfig;
		avatar.AccentColor = GetAvatarAccentColor(avatarDefinition);
		avatar.PortraitScale = portraitScale;
		avatar.MouseFilter = MouseFilterEnum.Ignore;
	}
}
