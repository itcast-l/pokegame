using Godot;

public partial class AdventureAvatarMenuScene
{
	private void BindUi()
	{
		if (GetNodeOrNull<CanvasItem>("%StandaloneBackground") is CanvasItem background)
		{
			background.Visible = GetEmbeddedMainMenuParent() == null;
		}

		_backButton = GetNode<Button>("%BackButton");
		_startButton = GetNode<Button>("%StartButton");
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
