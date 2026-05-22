using System;
using System.Collections.Generic;
using Godot;

public partial class AdventureAvatarMenuScene : Control
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";
	private const string GameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const string CharacterAvatarScenePath = "res://scenes/character_avatar.tscn";
	private const double SmoothScrollTweenSeconds = 0.22;
	private const int SmoothScrollWheelStep = 96;

	private readonly Dictionary<string, OceanMenuButtonFrame> _avatarFrames = new();
	private readonly Dictionary<string, int> _stageNumbers = new();
	private readonly List<(Label Label, string Key)> _localizedLabels = new();
	private readonly List<(Button Button, string Key)> _localizedButtons = new();
	private Button _startButton = default!;
	private Button _backButton = default!;
	private ScrollContainer _levelScroll = default!;
	private Label _previewStageLabel = default!;
	private Label _previewNameLabel = default!;
	private CharacterAvatar? _previewAvatar;
	private Tween? _levelScrollTween;
	private int _levelScrollTarget;
	private string _selectedAvatarId = string.Empty;
	private PackedScene? _characterAvatarScene;
	private bool _isReturningToMainMenu;

	[Export]
	public string selected_avatar_id { get; set; } = string.Empty;

	public override void _Ready()
	{
		Localization.EnsureLoaded();
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		_characterAvatarScene = GD.Load<PackedScene>(CharacterAvatarScenePath);

		BuildUi();
		_levelScrollTarget = 0;
		_selectedAvatarId = string.IsNullOrWhiteSpace(selected_avatar_id)
			? GetInitialAvatarId()
			: NormalizeAdventureAvatarId(selected_avatar_id);
		RefreshSelection();
		ClampScrollTarget();
		RefreshLocalizedText();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && IsNodeReady())
		{
			RefreshLocalizedText();
		}
		else if (what == NotificationResized && IsNodeReady())
		{
			ClampScrollTarget();
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed("ui_cancel"))
		{
			ReturnToMainMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildUi()
	{
		var background = new ColorRect
		{
			Name = "Background",
			Color = new Color(0.0f, 0.04f, 0.07f, 1.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		background.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(background);

		var topShade = new ColorRect
		{
			Name = "TopShade",
			Color = new Color(0.0f, 0.02f, 0.035f, 0.34f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		topShade.AnchorRight = 1.0f;
		topShade.OffsetBottom = 118.0f;
		AddChild(topShade);

		var ambient = new UnderwaterAmbientLayer
		{
			Name = "AmbientLayer",
			bubble_count = 42,
			mote_count = 92,
			seagrass_blade_count = 28,
			motion_scale = 0.64f,
		};
		ambient.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(ambient);

		var margin = new MarginContainer
		{
			Name = "SafeArea",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 56);
		margin.AddThemeConstantOverride("margin_top", 40);
		margin.AddThemeConstantOverride("margin_right", 56);
		margin.AddThemeConstantOverride("margin_bottom", 44);
		AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "Layout",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 24);
		margin.AddChild(layout);

		var header = new HBoxContainer
		{
			Name = "Header",
			CustomMinimumSize = new Vector2(0.0f, 68.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		header.AddThemeConstantOverride("separation", 18);
		layout.AddChild(header);

		_backButton = CreateActionButton("ADVENTURE_BACK", new Color(0.72f, 0.88f, 1.0f), ReturnToMainMenu, false, 142.0f);
		header.AddChild(_backButton.GetParent());

		var titleWrap = new CenterContainer
		{
			Name = "TitleWrap",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		titleWrap.AddChild(CreateTitleBlock());
		header.AddChild(titleWrap);

		header.AddChild(new Control
		{
			Name = "HeaderBalance",
			CustomMinimumSize = new Vector2(142.0f, 1.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		});

		var body = new HBoxContainer
		{
			Name = "Body",
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		body.AddThemeConstantOverride("separation", 26);
		layout.AddChild(body);

		body.AddChild(CreateLevelListPanel());
		body.AddChild(CreateEnemyPreviewPanel());
	}

	private Control CreateLevelListPanel()
	{
		var panel = new PanelContainer
		{
			Name = "LevelListPanel",
			CustomMinimumSize = new Vector2(480.0f, 0.0f),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(0.52f));

		var margin = new MarginContainer
		{
			Name = "LevelListMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		panel.AddChild(margin);

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
		margin.AddChild(scroll);

		var levelList = new VBoxContainer
		{
			Name = "LevelList",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		levelList.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(levelList);

		_avatarFrames.Clear();
		_stageNumbers.Clear();
		int stageNumber = 1;
		foreach (CharacterAvatarDefinition avatarDefinition in CharacterAvatarCatalog.All)
		{
			if (avatarDefinition.Id == CharacterAvatarCatalog.DefaultAvatarId)
			{
				continue;
			}

			levelList.AddChild(CreateLevelEntry(avatarDefinition, stageNumber));
			stageNumber++;
		}

		return panel;
	}

	private Control CreateEnemyPreviewPanel()
	{
		var panel = new PanelContainer
		{
			Name = "EnemyPreviewPanel",
			CustomMinimumSize = new Vector2(360.0f, 0.0f),
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
		margin.AddThemeConstantOverride("margin_left", 30);
		margin.AddThemeConstantOverride("margin_top", 22);
		margin.AddThemeConstantOverride("margin_right", 30);
		margin.AddThemeConstantOverride("margin_bottom", 22);
		panel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "EnemyPreviewLayout",
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 14);
		margin.AddChild(layout);

		var stageBadge = new PanelContainer
		{
			Name = "SelectedStageBadge",
			CustomMinimumSize = new Vector2(118.0f, 54.0f),
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
		_previewStageLabel.AddThemeFontSizeOverride("font_size", 30);
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
			CustomMinimumSize = new Vector2(0.0f, 228.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		previewFrame.AddThemeStyleboxOverride("panel", CreatePreviewFrameStyle());

		var previewAnchor = new CenterContainer
		{
			Name = "PreviewAvatarAnchor",
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_previewAvatar = CreateAvatarView(CharacterAvatarCatalog.Default, 216.0f, 0.86f);
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
		_previewNameLabel.AddThemeFontSizeOverride("font_size", 34);
		_previewNameLabel.AddThemeColorOverride("font_color", new Color(0.98f, 1.0f, 0.93f));
		_previewNameLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.92f));
		_previewNameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_previewNameLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		layout.AddChild(_previewNameLabel);

		_startButton = CreateActionButton("ADVENTURE_START", new Color(0.98f, 0.92f, 0.46f), StartAdventure, true, 244.0f);
		var startWrap = new CenterContainer
		{
			Name = "StartWrap",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		startWrap.AddChild(_startButton.GetParent());
		layout.AddChild(startWrap);

		return panel;
	}

	private Button CreateActionButton(string key, Color accentColor, Action pressedAction, bool flipOrnament, float width)
	{
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{key}Frame",
			CustomMinimumSize = new Vector2(width, 62.0f),
			AccentColor = accentColor,
			FlipOrnament = flipOrnament,
		};

		var button = new Button
		{
			Name = "Button",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		button.SetAnchorsPreset(LayoutPreset.FullRect);
		button.OffsetLeft = flipOrnament ? 22.0f : 4.0f;
		button.OffsetTop = 5.0f;
		button.OffsetRight = flipOrnament ? -4.0f : -22.0f;
		button.OffsetBottom = -5.0f;
		button.AddThemeFontSizeOverride("font_size", 20);
		button.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.78f));
		button.AddThemeColorOverride("font_pressed_color", new Color(0.78f, 1.0f, 0.96f));
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.Pressed += pressedAction;
		frame.AddChild(button);

		_localizedButtons.Add((button, key));
		return button;
	}

	private Label CreateLocalizedLabel(string key, int fontSize, Color color, HorizontalAlignment alignment)
	{
		var label = new Label
		{
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		_localizedLabels.Add((label, key));
		return label;
	}

	private Control CreateTitleBlock()
	{
		var stack = new VBoxContainer
		{
			Name = "TitleBlock",
			CustomMinimumSize = new Vector2(420.0f, 66.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		stack.AddThemeConstantOverride("separation", 3);
		stack.AddChild(CreateHeaderTitleLabel());

		var underlineWrap = new CenterContainer
		{
			Name = "TitleUnderlineWrap",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		var underline = new ColorRect
		{
			Name = "TitleUnderline",
			CustomMinimumSize = new Vector2(210.0f, 2.0f),
			Color = new Color(0.64f, 1.0f, 0.93f, 0.62f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		underlineWrap.AddChild(underline);
		stack.AddChild(underlineWrap);
		return stack;
	}

	private Label CreateHeaderTitleLabel()
	{
		var label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ClipText = true,
			CustomMinimumSize = new Vector2(420.0f, 52.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", 38);
		label.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		_localizedLabels.Add((label, "ADVENTURE_AVATAR_TITLE"));
		return label;
	}

	private Control CreateLevelEntry(CharacterAvatarDefinition avatarDefinition, int stageNumber)
	{
		Color accentColor = GetAvatarAccentColor(avatarDefinition);
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{avatarDefinition.Id}LevelEntry",
			CustomMinimumSize = new Vector2(0.0f, 76.0f),
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
		contentMargin.OffsetLeft = 22.0f;
		contentMargin.OffsetTop = 8.0f;
		contentMargin.OffsetRight = -22.0f;
		contentMargin.OffsetBottom = -8.0f;
		frame.AddChild(contentMargin);

		var row = new HBoxContainer
		{
			Name = "EntryRow",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 14);
		contentMargin.AddChild(row);

		var badge = new PanelContainer
		{
			Name = "StageBadge",
			CustomMinimumSize = new Vector2(54.0f, 54.0f),
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
		badgeLabel.AddThemeFontSizeOverride("font_size", 23);
		badgeLabel.AddThemeColorOverride("font_color", new Color(0.98f, 1.0f, 0.9f));
		badge.AddChild(badgeLabel);
		row.AddChild(badge);

		var avatarAnchor = new CenterContainer
		{
			Name = "EntryAvatarAnchor",
			CustomMinimumSize = new Vector2(58.0f, 58.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		CharacterAvatar? avatar = CreateAvatarView(avatarDefinition, 58.0f, 0.76f);
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
		nameLabel.AddThemeFontSizeOverride("font_size", 26);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		nameLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.92f));
		nameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		nameLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		row.AddChild(nameLabel);

		var arrowLabel = new Label
		{
			Name = "EntryArrow",
			Text = ">>",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(34.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		arrowLabel.AddThemeFontSizeOverride("font_size", 23);
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

		return frame;
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

	private void SelectAvatar(string avatarId)
	{
		_selectedAvatarId = NormalizeAdventureAvatarId(avatarId);
		RefreshSelection();
	}

	private void RefreshSelection()
	{
		foreach ((string avatarId, OceanMenuButtonFrame frame) in _avatarFrames)
		{
			frame.Selected = avatarId == _selectedAvatarId;
			frame.QueueRedraw();
		}

		UpdatePreview();
	}

	private void SmoothScrollBy(int delta)
	{
		if (_levelScroll == null || !GodotObject.IsInstanceValid(_levelScroll))
		{
			return;
		}

		_levelScrollTarget = _levelScroll.ScrollVertical;
		_levelScrollTarget = Mathf.Clamp(
			_levelScrollTarget + delta,
			0,
			GetMaxScrollTarget());
		AnimateScrollToTarget();
	}

	private void OnLevelScrollGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
		{
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			SmoothScrollBy(-SmoothScrollWheelStep);
			_levelScroll.AcceptEvent();
		}
		else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			SmoothScrollBy(SmoothScrollWheelStep);
			_levelScroll.AcceptEvent();
		}
	}

	private void AnimateScrollToTarget()
	{
		if (_levelScroll == null || !GodotObject.IsInstanceValid(_levelScroll))
		{
			return;
		}

		_levelScrollTween?.Kill();
		_levelScrollTween = CreateTween();
		_levelScrollTween.SetTrans(Tween.TransitionType.Sine);
		_levelScrollTween.SetEase(Tween.EaseType.Out);
		_levelScrollTween.TweenProperty(_levelScroll, "scroll_vertical", _levelScrollTarget, SmoothScrollTweenSeconds);
	}

	private void ClampScrollTarget()
	{
		if (_levelScroll == null || !GodotObject.IsInstanceValid(_levelScroll))
		{
			return;
		}

		_levelScrollTween?.Kill();
		_levelScrollTarget = Mathf.Clamp(_levelScroll.ScrollVertical, 0, GetMaxScrollTarget());
		_levelScroll.ScrollVertical = _levelScrollTarget;
	}

	private int GetMaxScrollTarget()
	{
		if (_levelScroll == null || !GodotObject.IsInstanceValid(_levelScroll))
		{
			return 0;
		}

		ScrollBar verticalScrollBar = _levelScroll.GetVScrollBar();
		double maxScroll = verticalScrollBar.MaxValue - verticalScrollBar.Page;
		return Mathf.Max(0, (int)Mathf.Round(maxScroll));
	}

	private void UpdatePreview()
	{
		CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(_selectedAvatarId);
		int stageNumber = _stageNumbers.TryGetValue(avatarDefinition.Id, out int number) ? number : 1;

		_previewStageLabel.Text = FormatStageNumber(stageNumber);
		_previewNameLabel.Text = avatarDefinition.DisplayName;

		if (_previewAvatar != null && GodotObject.IsInstanceValid(_previewAvatar))
		{
			_previewAvatar.AvatarRoot = avatarDefinition.RootPath;
			_previewAvatar.AccentColor = GetAvatarAccentColor(avatarDefinition);
			_previewAvatar.PlayIdle();
		}
	}

	private void StartAdventure()
	{
		PackedScene? gameBoardScene = GD.Load<PackedScene>(GameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {GameBoardScenePath}");
			return;
		}

		Node gameBoard = gameBoardScene.Instantiate();
		gameBoard.Set("player_count", 2);
		gameBoard.Set("item_mode", true);
		gameBoard.Set("adventure_mode", true);
		gameBoard.Set("adventure_enemy_avatar_id", _selectedAvatarId);
		GetTree().Root.AddChild(gameBoard);
		GetTree().CurrentScene = gameBoard;
		QueueFree();
	}

	private void ReturnToMainMenu()
	{
		if (_isReturningToMainMenu)
		{
			return;
		}

		PackedScene? mainMenuScene = GD.Load<PackedScene>(MainMenuScenePath);
		if (mainMenuScene == null)
		{
			GD.PushError($"Missing main menu scene: {MainMenuScenePath}");
			return;
		}

		_isReturningToMainMenu = true;
		_backButton.Disabled = true;
		_startButton.Disabled = true;

		Node mainMenuNode = mainMenuScene.Instantiate();
		if (mainMenuNode is not MainMenuScene mainMenu)
		{
			GD.PushError($"{MainMenuScenePath} root must use MainMenuScene.");
			mainMenuNode.QueueFree();
			_isReturningToMainMenu = false;
			_backButton.Disabled = false;
			_startButton.Disabled = false;
			return;
		}

		SceneTree tree = GetTree();
		mainMenu.PrepareAdventureReturnTransition();
		tree.Root.AddChild(mainMenu);
		tree.CurrentScene = mainMenu;
		QueueFree();
	}

	private string GetInitialAvatarId()
	{
		return NormalizeAdventureAvatarId(CharacterAvatarCatalog.LoadSelectedAvatar().Id);
	}

	private static string NormalizeAdventureAvatarId(string avatarId)
	{
		string normalized = CharacterAvatarCatalog.NormalizeAvatarId(avatarId);
		if (normalized != CharacterAvatarCatalog.DefaultAvatarId)
		{
			return normalized;
		}

		foreach (CharacterAvatarDefinition avatarDefinition in CharacterAvatarCatalog.All)
		{
			if (avatarDefinition.Id != CharacterAvatarCatalog.DefaultAvatarId)
			{
				return avatarDefinition.Id;
			}
		}

		return CharacterAvatarCatalog.DefaultAvatarId;
	}

	private void RefreshLocalizedText()
	{
		for (int index = _localizedLabels.Count - 1; index >= 0; index--)
		{
			(Label label, string key) = _localizedLabels[index];
			if (!GodotObject.IsInstanceValid(label))
			{
				_localizedLabels.RemoveAt(index);
				continue;
			}

			label.Text = Tr(key).ToString();
		}

		for (int index = _localizedButtons.Count - 1; index >= 0; index--)
		{
			(Button button, string key) = _localizedButtons[index];
			if (!GodotObject.IsInstanceValid(button))
			{
				_localizedButtons.RemoveAt(index);
				continue;
			}

			button.Text = Tr(key).ToString();
		}
	}

	private static string FormatStageNumber(int stageNumber)
	{
		return stageNumber.ToString("00");
	}

	private static Color GetAvatarAccentColor(CharacterAvatarDefinition avatarDefinition)
	{
		return avatarDefinition.Id is "dragon_prince_j" or "dragon_queen_q"
			? new Color(1.0f, 0.86f, 0.46f)
			: avatarDefinition.Id == "boss_k"
				? new Color(0.24f, 0.88f, 1.0f)
				: new Color(0.62f, 1.0f, 0.92f);
	}

	private static StyleBoxFlat CreatePanelStyle(float alpha)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.005f, 0.105f, 0.13f, alpha),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.30f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.28f),
			ShadowSize = 6,
		};
	}

	private static StyleBoxFlat CreateBadgeStyle(Color accentColor)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(accentColor.R * 0.12f, accentColor.G * 0.15f, accentColor.B * 0.16f, 0.88f),
			BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.58f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
		};
	}

	private static StyleBoxFlat CreatePreviewFrameStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.0f, 0.06f, 0.075f, 0.32f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.24f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
		};
	}
}
