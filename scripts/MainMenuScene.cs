using System;
using Godot;

public partial class MainMenuScene : Control
{
	private const string BackgroundMusicPath = "BackgroundMusic";
	private const string OceanDynamicsLayerPath = "OceanDynamicsLayer";
	private const string UnderwaterAmbientLayerPath = "UnderwaterAmbientLayer";
	private const string WaterCausticOverlayPath = "WaterCausticOverlay";
	private const string SinglePlayerButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SinglePlayerFrame/Button";
	private const string SettingsButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SettingsFrame/Button";
	private const string ExitButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/ExitFrame/Button";
	private const string AvatarEntryPath = "AvatarEntryLayer/MainMenuAvatarEntry";
	private const string GameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const string AvatarEditScenePath = "res://scenes/character_avatar_edit_scene.tscn";
	private const string SettingsPanelTexturePath = "res://art/ui/settings_menu/run-002/processed/clean.png";
	private const string SettingsConfigPath = "user://settings.cfg";
	private const float DialogFadeSeconds = 0.14f;
	private const int AvatarEditDialogLayer = 42;
	private const float DefaultMasterVolume = 0.82f;
	private const float DefaultMusicVolume = 0.76f;
	private const float DefaultMotionScale = 1.0f;
	private const bool DefaultMuteAudio = false;
	private const bool DefaultFullscreen = false;
	private const bool DefaultOceanMotion = true;

	private AudioStreamPlayer _backgroundMusic = default!;
	private MainMenuOceanDynamics? _oceanDynamicsLayer;
	private UnderwaterAmbientLayer? _underwaterAmbientLayer;
	private CanvasItem? _waterCausticOverlay;
	private CanvasLayer _playerCountDialogLayer = default!;
	private Control _playerCountDialogRoot = default!;
	private PanelContainer _playerCountDialogPanel = default!;
	private Tween? _playerCountDialogTween;
	private CanvasLayer _settingsDialogLayer = default!;
	private Control _settingsDialogRoot = default!;
	private Control _settingsDialogPanel = default!;
	private Tween? _settingsDialogTween;
	private HSlider _masterVolumeSlider = default!;
	private HSlider _musicVolumeSlider = default!;
	private HSlider _motionScaleSlider = default!;
	private Label _masterVolumeValue = default!;
	private Label _musicVolumeValue = default!;
	private Label _motionScaleValue = default!;
	private CheckButton _muteAudioToggle = default!;
	private CheckButton _oceanMotionToggle = default!;
	private OptionButton _windowModeOption = default!;
	private CanvasLayer _exitDialogLayer = default!;
	private Control _exitDialogRoot = default!;
	private PanelContainer _exitDialogPanel = default!;
	private Tween? _exitDialogTween;
	private CanvasLayer? _avatarEditDialogLayer;
	private float _masterVolume = DefaultMasterVolume;
	private float _musicVolume = DefaultMusicVolume;
	private float _motionScale = DefaultMotionScale;
	private bool _muteAudio = DefaultMuteAudio;
	private bool _fullscreen = DefaultFullscreen;
	private bool _oceanMotion = DefaultOceanMotion;
	private bool _isSyncingSettingsControls;

	public override void _Ready()
	{
		PrepareBackgroundMusic();
		_oceanDynamicsLayer = GetNodeOrNull<MainMenuOceanDynamics>(OceanDynamicsLayerPath);
		_underwaterAmbientLayer = GetNodeOrNull<UnderwaterAmbientLayer>(UnderwaterAmbientLayerPath);
		_waterCausticOverlay = GetNodeOrNull<CanvasItem>(WaterCausticOverlayPath);

		Button singlePlayerButton = GetNode<Button>(SinglePlayerButtonPath);
		singlePlayerButton.Pressed += ShowPlayerCountDialog;

		Button settingsButton = GetNode<Button>(SettingsButtonPath);
		settingsButton.Pressed += ShowSettingsDialog;

		Button exitButton = GetNode<Button>(ExitButtonPath);
		exitButton.Pressed += ShowExitDialog;

		MainMenuAvatarEntry avatarEntry = GetNode<MainMenuAvatarEntry>(AvatarEntryPath);
		avatarEntry.AvatarPressed += OpenAvatarEditor;

		BuildPlayerCountDialog();
		BuildSettingsDialog();
		BuildExitDialog();
		LoadSettings();
		SyncSettingsControls();
		ApplySettings();
	}

	private void PrepareBackgroundMusic()
	{
		_backgroundMusic = GetNode<AudioStreamPlayer>(BackgroundMusicPath);
		if (_backgroundMusic.Stream is AudioStreamMP3 backgroundMp3)
		{
			backgroundMp3.Loop = true;
		}

		if (!_backgroundMusic.Playing)
		{
			_backgroundMusic.Play();
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (_settingsDialogRoot.Visible && inputEvent.IsActionPressed("ui_cancel"))
		{
			HideSettingsDialog();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_playerCountDialogRoot.Visible && inputEvent.IsActionPressed("ui_cancel"))
		{
			HidePlayerCountDialog();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_exitDialogRoot.Visible && inputEvent.IsActionPressed("ui_cancel"))
		{
			HideExitDialog();
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildPlayerCountDialog()
	{
		_playerCountDialogLayer = new CanvasLayer
		{
			Name = "PlayerCountDialogLayer",
			Layer = 35,
		};
		AddChild(_playerCountDialogLayer);

		_playerCountDialogRoot = new Control
		{
			Name = "PlayerCountDialogRoot",
			Visible = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_playerCountDialogRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerCountDialogLayer.AddChild(_playerCountDialogRoot);

		var scrim = new ColorRect
		{
			Name = "Scrim",
			Color = new Color(0.0f, 0.035f, 0.07f, 0.54f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		scrim.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				HidePlayerCountDialog();
				GetViewport().SetInputAsHandled();
			}
		};
		_playerCountDialogRoot.AddChild(scrim);

		var center = new CenterContainer
		{
			Name = "DialogCenter",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerCountDialogRoot.AddChild(center);

		_playerCountDialogPanel = new PanelContainer
		{
			Name = "PlayerCountDialogPanel",
			CustomMinimumSize = new Vector2(660.0f, 360.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_playerCountDialogPanel.AddThemeStyleboxOverride("panel", CreateDialogPanelStyle());
		center.AddChild(_playerCountDialogPanel);

		var margin = new MarginContainer
		{
			Name = "DialogMargin",
		};
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_top", 26);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_bottom", 26);
		_playerCountDialogPanel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "DialogLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		layout.AddThemeConstantOverride("separation", 12);
		margin.AddChild(layout);

		var pearl = new Control
		{
			Name = "PearlDivider",
			CustomMinimumSize = new Vector2(0.0f, 20.0f),
		};
		pearl.Draw += () => DrawPearlDivider(pearl);
		layout.AddChild(pearl);

		var title = CreateDialogLabel("选择游戏人数", 30, new Color(0.9f, 1.0f, 0.96f), HorizontalAlignment.Center);
		layout.AddChild(title);

		var message = CreateDialogLabel("选择本局参与人数", 18, new Color(0.66f, 0.96f, 0.92f, 0.9f), HorizontalAlignment.Center);
		layout.AddChild(message);

		var spacer = new Control
		{
			CustomMinimumSize = new Vector2(0.0f, 6.0f),
		};
		layout.AddChild(spacer);

		var actions = new HBoxContainer
		{
			Name = "DialogActions",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		actions.AddThemeConstantOverride("separation", 16);
		layout.AddChild(actions);

		actions.AddChild(CreatePlayerCountOption(2, "双人对局", "40张牌", new Color(0.58f, 1.0f, 0.92f)));
		actions.AddChild(CreatePlayerCountOption(3, "三人对局", "80张牌", new Color(0.95f, 0.86f, 0.52f)));
		actions.AddChild(CreatePlayerCountOption(4, "四人对局", "80张牌", new Color(0.58f, 0.82f, 1.0f)));
	}

	private void BuildSettingsDialog()
	{
		_settingsDialogLayer = new CanvasLayer
		{
			Name = "SettingsDialogLayer",
			Layer = 38,
		};
		AddChild(_settingsDialogLayer);

		_settingsDialogRoot = new Control
		{
			Name = "SettingsDialogRoot",
			Visible = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_settingsDialogRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		_settingsDialogLayer.AddChild(_settingsDialogRoot);

		var scrim = new ColorRect
		{
			Name = "Scrim",
			Color = new Color(0.0f, 0.035f, 0.07f, 0.58f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		scrim.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				HideSettingsDialog();
				GetViewport().SetInputAsHandled();
			}
		};
		_settingsDialogRoot.AddChild(scrim);

		var center = new CenterContainer
		{
			Name = "DialogCenter",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		center.OffsetTop = -28.0f;
		center.OffsetBottom = -28.0f;
		_settingsDialogRoot.AddChild(center);

		_settingsDialogPanel = new Control
		{
			Name = "SettingsDialogPanel",
			CustomMinimumSize = new Vector2(560.0f, 500.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		center.AddChild(_settingsDialogPanel);

		Texture2D? frameTexture = LoadTextureWithImageFallback(SettingsPanelTexturePath);
		if (frameTexture != null)
		{
			var frameArt = new TextureRect
			{
				Name = "GeneratedSettingsFrame",
				Texture = frameTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			frameArt.SetAnchorsPreset(LayoutPreset.FullRect);
			frameArt.OffsetLeft = -8.0f;
			frameArt.OffsetTop = -14.0f;
			frameArt.OffsetRight = 8.0f;
			frameArt.OffsetBottom = 14.0f;
			_settingsDialogPanel.AddChild(frameArt);
		}
		else
		{
			var fallbackPanel = new Panel
			{
				Name = "FallbackPanel",
				MouseFilter = MouseFilterEnum.Ignore,
			};
			fallbackPanel.SetAnchorsPreset(LayoutPreset.FullRect);
			fallbackPanel.AddThemeStyleboxOverride("panel", CreateDialogPanelStyle());
			_settingsDialogPanel.AddChild(fallbackPanel);
			GD.PushWarning($"Missing settings menu frame texture: {SettingsPanelTexturePath}");
		}

		var margin = new MarginContainer
		{
			Name = "SettingsDialogMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 74);
		margin.AddThemeConstantOverride("margin_top", 64);
		margin.AddThemeConstantOverride("margin_right", 74);
		margin.AddThemeConstantOverride("margin_bottom", 56);
		_settingsDialogPanel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "SettingsDialogLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 12);
		margin.AddChild(layout);

		var title = CreateDialogLabel("设置", 34, new Color(0.9f, 1.0f, 0.96f), HorizontalAlignment.Center);
		layout.AddChild(title);

		var message = CreateDialogLabel("基础游戏选项", 17, new Color(0.66f, 0.96f, 0.92f, 0.86f), HorizontalAlignment.Center);
		layout.AddChild(message);

		var settingsList = new VBoxContainer
		{
			Name = "SettingsList",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		settingsList.AddThemeConstantOverride("separation", 10);
		layout.AddChild(settingsList);

		(_masterVolumeSlider, _masterVolumeValue) = CreateSettingsSlider(
			settingsList,
			"主音量",
			value =>
			{
				_masterVolume = (float)value / 100.0f;
				OnSettingsChanged();
			},
			hidden: true);
		(_musicVolumeSlider, _musicVolumeValue) = CreateSettingsSlider(
			settingsList,
			"音乐音量",
			value =>
			{
				_musicVolume = (float)value / 100.0f;
				OnSettingsChanged();
			});

		_muteAudioToggle = CreateSettingsToggle(settingsList, "静音", pressed =>
		{
			_muteAudio = pressed;
			OnSettingsChanged();
		});

		_windowModeOption = CreateWindowModeOption(settingsList);

		_oceanMotionToggle = CreateSettingsToggle(settingsList, "海洋动态", pressed =>
		{
			_oceanMotion = pressed;
			_motionScaleSlider.Editable = pressed;
			OnSettingsChanged();
		}, hidden: true);

		(_motionScaleSlider, _motionScaleValue) = CreateSettingsSlider(
			settingsList,
			"动态强度",
			value =>
			{
				_motionScale = (float)value / 100.0f;
				OnSettingsChanged();
			},
			50.0,
			150.0,
			hidden: true);

		var actions = new HBoxContainer
		{
			Name = "SettingsActions",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		actions.AddThemeConstantOverride("separation", 18);
		layout.AddChild(actions);

		actions.AddChild(CreateDialogButton("恢复默认", new Color(0.78f, 0.9f, 1.0f), ResetSettingsToDefaults, false, 180.0f));
		actions.AddChild(CreateDialogButton("完成", new Color(0.58f, 1.0f, 0.92f), HideSettingsDialog, true, 160.0f));
	}

	private void BuildExitDialog()
	{
		_exitDialogLayer = new CanvasLayer
		{
			Name = "ExitDialogLayer",
			Layer = 40,
		};
		AddChild(_exitDialogLayer);

		_exitDialogRoot = new Control
		{
			Name = "ExitDialogRoot",
			Visible = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_exitDialogRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		_exitDialogLayer.AddChild(_exitDialogRoot);

		var scrim = new ColorRect
		{
			Name = "Scrim",
			Color = new Color(0.0f, 0.035f, 0.07f, 0.62f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		_exitDialogRoot.AddChild(scrim);

		var center = new CenterContainer
		{
			Name = "DialogCenter",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		_exitDialogRoot.AddChild(center);

		_exitDialogPanel = new PanelContainer
		{
			Name = "ExitDialogPanel",
			CustomMinimumSize = new Vector2(500.0f, 250.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_exitDialogPanel.AddThemeStyleboxOverride("panel", CreateDialogPanelStyle());
		center.AddChild(_exitDialogPanel);

		var margin = new MarginContainer
		{
			Name = "DialogMargin",
		};
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_top", 26);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_bottom", 26);
		_exitDialogPanel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "DialogLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		layout.AddThemeConstantOverride("separation", 14);
		margin.AddChild(layout);

		var pearl = new Control
		{
			Name = "PearlDivider",
			CustomMinimumSize = new Vector2(0.0f, 20.0f),
		};
		pearl.Draw += () => DrawPearlDivider(pearl);
		layout.AddChild(pearl);

		var title = CreateDialogLabel("退出游戏", 31, new Color(0.9f, 1.0f, 0.96f), HorizontalAlignment.Center);
		layout.AddChild(title);

		var message = CreateDialogLabel("是否要离开当前海域？", 21, new Color(0.66f, 0.96f, 0.92f, 0.9f), HorizontalAlignment.Center);
		layout.AddChild(message);

		var hint = CreateDialogLabel("未保存的对局进度可能会丢失", 16, new Color(0.95f, 0.84f, 0.58f, 0.76f), HorizontalAlignment.Center);
		layout.AddChild(hint);

		var spacer = new Control
		{
			CustomMinimumSize = new Vector2(0.0f, 8.0f),
		};
		layout.AddChild(spacer);

		var actions = new HBoxContainer
		{
			Name = "DialogActions",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		actions.AddThemeConstantOverride("separation", 18);
		layout.AddChild(actions);

		Control cancelFrame = CreateDialogButton("继续游戏", new Color(0.58f, 1.0f, 0.92f), HideExitDialog, false);
		Control confirmFrame = CreateDialogButton("确认退出", new Color(0.95f, 0.68f, 0.56f), QuitGame, true);
		actions.AddChild(cancelFrame);
		actions.AddChild(confirmFrame);
	}

	private Label CreateDialogLabel(string text, int fontSize, Color color, HorizontalAlignment alignment)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		return label;
	}

	private Control CreatePlayerCountOption(int playerCount, string title, string deckText, Color accentColor)
	{
		var card = new PanelContainer
		{
			Name = $"{playerCount}PlayerOption",
			CustomMinimumSize = new Vector2(178.0f, 104.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		card.AddThemeStyleboxOverride("panel", CreatePlayerCountOptionStyle(accentColor, false));

		var margin = new MarginContainer
		{
			Name = "OptionMargin",
		};
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		card.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "OptionLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);

		var titleLabel = CreateDialogLabel(title, 24, new Color(0.92f, 1.0f, 0.97f), HorizontalAlignment.Center);
		titleLabel.VerticalAlignment = VerticalAlignment.Center;
		layout.AddChild(titleLabel);

		var deckBadge = new PanelContainer
		{
			Name = "DeckBadge",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		deckBadge.AddThemeStyleboxOverride("panel", CreatePlayerCountBadgeStyle(accentColor));
		layout.AddChild(deckBadge);

		var deckBadgeMargin = new MarginContainer
		{
			Name = "DeckBadgeMargin",
		};
		deckBadgeMargin.AddThemeConstantOverride("margin_left", 14);
		deckBadgeMargin.AddThemeConstantOverride("margin_top", 3);
		deckBadgeMargin.AddThemeConstantOverride("margin_right", 14);
		deckBadgeMargin.AddThemeConstantOverride("margin_bottom", 3);
		deckBadge.AddChild(deckBadgeMargin);

		var deckLabel = CreateDialogLabel(deckText, 15, new Color(0.88f, 1.0f, 0.96f, 0.95f), HorizontalAlignment.Center);
		deckBadgeMargin.AddChild(deckLabel);

		var button = new Button
		{
			Name = "Button",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		button.SetAnchorsPreset(LayoutPreset.FullRect);
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		button.MouseEntered += () => card.AddThemeStyleboxOverride("panel", CreatePlayerCountOptionStyle(accentColor, true));
		button.MouseExited += () => card.AddThemeStyleboxOverride("panel", CreatePlayerCountOptionStyle(accentColor, false));
		button.Pressed += () => StartGameWithPlayerCount(playerCount);
		card.AddChild(button);

		return card;
	}

	private Control CreateDialogButton(string text, Color accentColor, Action pressedAction, bool flipOrnament, float width = 190.0f)
	{
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{text}Frame",
			CustomMinimumSize = new Vector2(width, 64.0f),
			AccentColor = accentColor,
			FlipOrnament = flipOrnament,
		};

		var button = new Button
		{
			Name = "Button",
			Text = text,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		button.SetAnchorsPreset(LayoutPreset.FullRect);
		button.OffsetLeft = flipOrnament ? 26.0f : 4.0f;
		button.OffsetTop = 5.0f;
		button.OffsetRight = flipOrnament ? -4.0f : -26.0f;
		button.OffsetBottom = -5.0f;
		button.AddThemeFontSizeOverride("font_size", 20);
		button.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.78f));
		button.AddThemeColorOverride("font_pressed_color", new Color(0.78f, 1.0f, 0.96f));
		button.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		button.AddThemeConstantOverride("shadow_offset_x", 1);
		button.AddThemeConstantOverride("shadow_offset_y", 2);
		StyleBoxEmpty emptyStyle = new();
		button.AddThemeStyleboxOverride("normal", emptyStyle);
		button.AddThemeStyleboxOverride("hover", emptyStyle);
		button.AddThemeStyleboxOverride("pressed", emptyStyle);
		button.AddThemeStyleboxOverride("focus", emptyStyle);
		button.Pressed += pressedAction;

		frame.AddChild(button);
		return frame;
	}

	private static Texture2D? LoadTextureWithImageFallback(string path)
	{
		Texture2D? texture = GD.Load<Texture2D>(path);
		if (texture != null)
		{
			return texture;
		}

		Image image = Image.LoadFromFile(path);
		return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
	}

	private (HSlider Slider, Label ValueLabel) CreateSettingsSlider(
		Container parent,
		string labelText,
		Action<double> valueChanged,
		double minValue = 0.0,
		double maxValue = 100.0,
		bool hidden = false)
	{
		var row = CreateSettingsRow(parent, labelText, hidden);

		var slider = new HSlider
		{
			Name = $"{labelText}Slider",
			MinValue = minValue,
			MaxValue = maxValue,
			Step = 1.0,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
		};
		slider.AddThemeStyleboxOverride("slider", CreateSliderTrackStyle(false));
		slider.AddThemeStyleboxOverride("grabber_area", CreateSliderTrackStyle(true));
		slider.AddThemeStyleboxOverride("grabber_area_highlight", CreateSliderTrackStyle(true));
		slider.AddThemeStyleboxOverride("grabber", CreateSliderGrabberStyle(false));
		slider.AddThemeStyleboxOverride("grabber_highlight", CreateSliderGrabberStyle(true));
		slider.ValueChanged += value =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			valueChanged(value);
		};
		row.AddChild(slider);

		var valueLabel = CreateDialogLabel("100%", 16, new Color(0.88f, 1.0f, 0.96f, 0.92f), HorizontalAlignment.Right);
		valueLabel.CustomMinimumSize = new Vector2(58.0f, 0.0f);
		row.AddChild(valueLabel);

		return (slider, valueLabel);
	}

	private CheckButton CreateSettingsToggle(Container parent, string labelText, Action<bool> toggled, bool hidden = false)
	{
		var row = CreateSettingsRow(parent, labelText, hidden);

		var spacer = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(spacer);

		var toggle = new CheckButton
		{
			Name = $"{labelText}Toggle",
			Text = "",
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		toggle.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		toggle.Toggled += pressed =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			toggled(pressed);
		};
		row.AddChild(toggle);
		return toggle;
	}

	private OptionButton CreateWindowModeOption(Container parent)
	{
		var row = CreateSettingsRow(parent, "窗口模式");
		var spacer = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(spacer);

		var option = new OptionButton
		{
			Name = "WindowModeOption",
			CustomMinimumSize = new Vector2(174.0f, 40.0f),
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		option.AddItem("窗口", 0);
		option.AddItem("全屏", 1);
		option.AddThemeFontSizeOverride("font_size", 17);
		option.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		option.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.78f));
		option.AddThemeColorOverride("font_pressed_color", new Color(0.78f, 1.0f, 0.96f));
		option.AddThemeStyleboxOverride("normal", CreateOptionButtonStyle(false));
		option.AddThemeStyleboxOverride("hover", CreateOptionButtonStyle(true));
		option.AddThemeStyleboxOverride("pressed", CreateOptionButtonStyle(true));
		option.AddThemeStyleboxOverride("focus", CreateOptionButtonStyle(true));
		option.ItemSelected += index =>
		{
			if (_isSyncingSettingsControls)
			{
				return;
			}

			_fullscreen = index == 1;
			OnSettingsChanged();
		};
		row.AddChild(option);
		return option;
	}

	private HBoxContainer CreateSettingsRow(Container parent, string labelText, bool hidden = false)
	{
		var panel = new PanelContainer
		{
			Name = $"{labelText}RowPanel",
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = !hidden,
		};
		panel.AddThemeStyleboxOverride("panel", CreateSettingsRowStyle());
		parent.AddChild(panel);

		var margin = new MarginContainer
		{
			Name = "RowMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 7);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 7);
		panel.AddChild(margin);

		var row = new HBoxContainer
		{
			Name = "RowLayout",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 12);
		margin.AddChild(row);

		var label = CreateDialogLabel(labelText, 18, new Color(0.88f, 1.0f, 0.96f, 0.96f), HorizontalAlignment.Left);
		label.CustomMinimumSize = new Vector2(104.0f, 38.0f);
		label.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(label);

		return row;
	}

	private static StyleBoxFlat CreateDialogPanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.012f, 0.14f, 0.18f, 0.9f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.62f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.68f),
			ShadowSize = 26,
			ShadowOffset = new Vector2(0.0f, 10.0f),
		};
	}

	private static StyleBoxFlat CreateSettingsRowStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.01f, 0.18f, 0.22f, 0.48f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.16f),
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

	private static StyleBoxFlat CreateSliderTrackStyle(bool filled)
	{
		return new StyleBoxFlat
		{
			BgColor = filled
				? new Color(0.56f, 1.0f, 0.92f, 0.52f)
				: new Color(0.0f, 0.08f, 0.12f, 0.72f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, filled ? 0.28f : 0.18f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginTop = 5,
			ContentMarginBottom = 5,
		};
	}

	private static StyleBoxFlat CreateSliderGrabberStyle(bool hovered)
	{
		return new StyleBoxFlat
		{
			BgColor = hovered
				? new Color(1.0f, 1.0f, 0.78f, 0.95f)
				: new Color(0.84f, 1.0f, 0.96f, 0.92f),
			BorderColor = new Color(0.08f, 0.34f, 0.38f, 0.9f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 8,
			ContentMarginBottom = 8,
		};
	}

	private static StyleBoxFlat CreateOptionButtonStyle(bool hovered)
	{
		return new StyleBoxFlat
		{
			BgColor = hovered
				? new Color(0.03f, 0.28f, 0.33f, 0.86f)
				: new Color(0.015f, 0.18f, 0.23f, 0.78f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, hovered ? 0.62f : 0.36f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 12,
			ContentMarginRight = 12,
			ContentMarginTop = 7,
			ContentMarginBottom = 7,
		};
	}

	private static StyleBoxFlat CreatePlayerCountOptionStyle(Color accentColor, bool hovered)
	{
		return new StyleBoxFlat
		{
			BgColor = hovered
				? new Color(0.035f, 0.24f, 0.29f, 0.88f)
				: new Color(0.015f, 0.17f, 0.21f, 0.78f),
			BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, hovered ? 0.86f : 0.54f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(accentColor.R, accentColor.G, accentColor.B, hovered ? 0.22f : 0.12f),
			ShadowSize = hovered ? 16 : 10,
			ShadowOffset = new Vector2(0.0f, hovered ? 5.0f : 4.0f),
		};
	}

	private static StyleBoxFlat CreatePlayerCountBadgeStyle(Color accentColor)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.13f),
			BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.36f),
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

	private static void DrawPearlDivider(Control target)
	{
		Vector2 center = target.Size * 0.5f;
		float lineWidth = Mathf.Max(target.Size.X * 0.34f, 80.0f);
		Color lineColor = new(0.58f, 1.0f, 0.92f, 0.22f);
		target.DrawLine(center - new Vector2(lineWidth, 0.0f), center - new Vector2(14.0f, 0.0f), lineColor, 1.4f, true);
		target.DrawLine(center + new Vector2(14.0f, 0.0f), center + new Vector2(lineWidth, 0.0f), lineColor, 1.4f, true);
		target.DrawCircle(center, 7.0f, new Color(0.88f, 1.0f, 0.94f, 0.82f));
		target.DrawCircle(center - new Vector2(2.5f, 2.5f), 2.3f, new Color(1.0f, 1.0f, 0.86f, 0.88f));
	}

	private void LoadSettings()
	{
		var config = new ConfigFile();
		Error loadResult = config.Load(SettingsConfigPath);
		if (loadResult != Error.Ok)
		{
			return;
		}

		_masterVolume = Mathf.Clamp(config.GetValue("audio", "master_volume", DefaultMasterVolume).AsSingle(), 0.0f, 1.0f);
		_musicVolume = Mathf.Clamp(config.GetValue("audio", "music_volume", DefaultMusicVolume).AsSingle(), 0.0f, 1.0f);
		_muteAudio = config.GetValue("audio", "mute", DefaultMuteAudio).AsBool();
		_fullscreen = config.GetValue("video", "fullscreen", DefaultFullscreen).AsBool();
		_oceanMotion = config.GetValue("video", "ocean_motion", DefaultOceanMotion).AsBool();
		_motionScale = Mathf.Clamp(config.GetValue("video", "motion_scale", DefaultMotionScale).AsSingle(), 0.5f, 1.5f);
	}

	private void SaveSettings()
	{
		var config = new ConfigFile();
		config.SetValue("audio", "master_volume", _masterVolume);
		config.SetValue("audio", "music_volume", _musicVolume);
		config.SetValue("audio", "mute", _muteAudio);
		config.SetValue("video", "fullscreen", _fullscreen);
		config.SetValue("video", "ocean_motion", _oceanMotion);
		config.SetValue("video", "motion_scale", _motionScale);

		Error saveResult = config.Save(SettingsConfigPath);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to save settings: {saveResult}");
		}
	}

	private void ApplySettings()
	{
		_masterVolume = Mathf.Clamp(_masterVolume, 0.0f, 1.0f);
		_musicVolume = Mathf.Clamp(_musicVolume, 0.0f, 1.0f);
		_motionScale = Mathf.Clamp(_motionScale, 0.5f, 1.5f);

		AudioServer.SetBusVolumeDb(0, LinearVolumeToDb(_masterVolume));
		AudioServer.SetBusMute(0, _muteAudio);
		_backgroundMusic.VolumeDb = LinearVolumeToDb(_musicVolume);

		DisplayServer.WindowSetMode(_fullscreen
			? DisplayServer.WindowMode.Fullscreen
			: DisplayServer.WindowMode.Windowed);

		if (_oceanDynamicsLayer != null)
		{
			_oceanDynamicsLayer.motion_scale = _motionScale;
			_oceanDynamicsLayer.Visible = _oceanMotion;
			_oceanDynamicsLayer.SetProcess(_oceanMotion);
		}

		if (_underwaterAmbientLayer != null)
		{
			_underwaterAmbientLayer.motion_scale = _motionScale;
			_underwaterAmbientLayer.Visible = _oceanMotion;
			_underwaterAmbientLayer.SetProcess(_oceanMotion);
		}

		if (_waterCausticOverlay != null)
		{
			_waterCausticOverlay.Visible = _oceanMotion;
		}

		if (_motionScaleSlider != null)
		{
			_motionScaleSlider.Editable = _oceanMotion;
		}

		UpdateSettingsLabels();
	}

	private static float LinearVolumeToDb(float value)
	{
		return value <= 0.001f ? -80.0f : Mathf.LinearToDb(value);
	}

	private void SyncSettingsControls()
	{
		_isSyncingSettingsControls = true;
		_masterVolumeSlider.Value = Mathf.RoundToInt(_masterVolume * 100.0f);
		_musicVolumeSlider.Value = Mathf.RoundToInt(_musicVolume * 100.0f);
		_muteAudioToggle.ButtonPressed = _muteAudio;
		_windowModeOption.Select(_fullscreen ? 1 : 0);
		_oceanMotionToggle.ButtonPressed = _oceanMotion;
		_motionScaleSlider.Value = Mathf.RoundToInt(_motionScale * 100.0f);
		_motionScaleSlider.Editable = _oceanMotion;
		_isSyncingSettingsControls = false;
		UpdateSettingsLabels();
	}

	private void UpdateSettingsLabels()
	{
		if (_masterVolumeValue != null)
		{
			_masterVolumeValue.Text = $"{Mathf.RoundToInt(_masterVolume * 100.0f)}%";
		}

		if (_musicVolumeValue != null)
		{
			_musicVolumeValue.Text = $"{Mathf.RoundToInt(_musicVolume * 100.0f)}%";
		}

		if (_motionScaleValue != null)
		{
			_motionScaleValue.Text = $"{Mathf.RoundToInt(_motionScale * 100.0f)}%";
		}
	}

	private void OnSettingsChanged()
	{
		ApplySettings();
		SaveSettings();
	}

	private void ResetSettingsToDefaults()
	{
		_masterVolume = DefaultMasterVolume;
		_musicVolume = DefaultMusicVolume;
		_motionScale = DefaultMotionScale;
		_muteAudio = DefaultMuteAudio;
		_fullscreen = DefaultFullscreen;
		_oceanMotion = DefaultOceanMotion;
		SyncSettingsControls();
		ApplySettings();
		SaveSettings();
	}

	private void ShowExitDialog()
	{
		_exitDialogTween?.Kill();
		_exitDialogRoot.Visible = true;
		_exitDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_exitDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_exitDialogPanel.Scale = new Vector2(0.94f, 0.94f);
		_exitDialogPanel.PivotOffset = _exitDialogPanel.Size * 0.5f;

		_exitDialogTween = CreateTween();
		_exitDialogTween.SetParallel(true);
		_exitDialogTween.TweenProperty(_exitDialogRoot, "modulate:a", 1.0f, DialogFadeSeconds);
		_exitDialogTween.TweenProperty(_exitDialogPanel, "scale", Vector2.One, DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}

	private void HideExitDialog()
	{
		_exitDialogTween?.Kill();
		_exitDialogRoot.MouseFilter = MouseFilterEnum.Ignore;

		_exitDialogTween = CreateTween();
		_exitDialogTween.SetParallel(true);
		_exitDialogTween.TweenProperty(_exitDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_exitDialogTween.TweenProperty(_exitDialogPanel, "scale", new Vector2(0.96f, 0.96f), DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_exitDialogTween.Finished += () => _exitDialogRoot.Visible = false;
	}

	private void ShowSettingsDialog()
	{
		_settingsDialogTween?.Kill();
		_settingsDialogRoot.Visible = true;
		_settingsDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_settingsDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_settingsDialogPanel.Scale = new Vector2(0.94f, 0.94f);
		_settingsDialogPanel.PivotOffset = _settingsDialogPanel.Size * 0.5f;
		SyncSettingsControls();

		_settingsDialogTween = CreateTween();
		_settingsDialogTween.SetParallel(true);
		_settingsDialogTween.TweenProperty(_settingsDialogRoot, "modulate:a", 1.0f, DialogFadeSeconds);
		_settingsDialogTween.TweenProperty(_settingsDialogPanel, "scale", Vector2.One, DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}

	private void HideSettingsDialog()
	{
		_settingsDialogTween?.Kill();
		_settingsDialogRoot.MouseFilter = MouseFilterEnum.Ignore;

		_settingsDialogTween = CreateTween();
		_settingsDialogTween.SetParallel(true);
		_settingsDialogTween.TweenProperty(_settingsDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_settingsDialogTween.TweenProperty(_settingsDialogPanel, "scale", new Vector2(0.96f, 0.96f), DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_settingsDialogTween.Finished += () => _settingsDialogRoot.Visible = false;
	}

	private void ShowPlayerCountDialog()
	{
		_playerCountDialogTween?.Kill();
		_playerCountDialogRoot.Visible = true;
		_playerCountDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_playerCountDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_playerCountDialogPanel.Scale = new Vector2(0.94f, 0.94f);
		_playerCountDialogPanel.PivotOffset = _playerCountDialogPanel.Size * 0.5f;

		_playerCountDialogTween = CreateTween();
		_playerCountDialogTween.SetParallel(true);
		_playerCountDialogTween.TweenProperty(_playerCountDialogRoot, "modulate:a", 1.0f, DialogFadeSeconds);
		_playerCountDialogTween.TweenProperty(_playerCountDialogPanel, "scale", Vector2.One, DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}

	private void HidePlayerCountDialog()
	{
		_playerCountDialogTween?.Kill();
		_playerCountDialogRoot.MouseFilter = MouseFilterEnum.Ignore;

		_playerCountDialogTween = CreateTween();
		_playerCountDialogTween.SetParallel(true);
		_playerCountDialogTween.TweenProperty(_playerCountDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_playerCountDialogTween.TweenProperty(_playerCountDialogPanel, "scale", new Vector2(0.96f, 0.96f), DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_playerCountDialogTween.Finished += () => _playerCountDialogRoot.Visible = false;
	}

	private void StartGameWithPlayerCount(int playerCount)
	{
		PackedScene? gameBoardScene = GD.Load<PackedScene>(GameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {GameBoardScenePath}");
			return;
		}

		Node gameBoard = gameBoardScene.Instantiate();
		gameBoard.Set("player_count", playerCount);
		GetTree().Root.AddChild(gameBoard);
		GetTree().CurrentScene = gameBoard;
		QueueFree();
	}

	private void OpenAvatarEditor()
	{
		if (_avatarEditDialogLayer != null && GodotObject.IsInstanceValid(_avatarEditDialogLayer))
		{
			return;
		}

		PackedScene? avatarEditScene = GD.Load<PackedScene>(AvatarEditScenePath);
		if (avatarEditScene == null)
		{
			GD.PushError($"Missing avatar edit scene: {AvatarEditScenePath}");
			return;
		}

		Node avatarEditNode = avatarEditScene.Instantiate();
		if (avatarEditNode is not CharacterAvatarEditScene avatarEditor)
		{
			GD.PushError($"{AvatarEditScenePath} root must use CharacterAvatarEditScene.");
			avatarEditNode.QueueFree();
			return;
		}

		_avatarEditDialogLayer = new CanvasLayer
		{
			Name = "AvatarEditDialogLayer",
			Layer = AvatarEditDialogLayer,
		};
		AddChild(_avatarEditDialogLayer);

		avatarEditor.CloseRequested += CloseAvatarEditor;
		_avatarEditDialogLayer.AddChild(avatarEditor);
	}

	private void CloseAvatarEditor()
	{
		if (_avatarEditDialogLayer == null || !GodotObject.IsInstanceValid(_avatarEditDialogLayer))
		{
			_avatarEditDialogLayer = null;
			return;
		}

		_avatarEditDialogLayer.QueueFree();
		_avatarEditDialogLayer = null;
	}

	private void QuitGame()
	{
		GetTree().Quit();
	}
}
