using System;
using Godot;

public partial class AdventureAvatarMenuScene
{
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
}
