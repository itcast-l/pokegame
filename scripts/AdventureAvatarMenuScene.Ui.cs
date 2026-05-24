using System;
using Godot;

public partial class AdventureAvatarMenuScene
{
	private void BuildUi()
	{
		if (GetEmbeddedMainMenuParent() == null)
		{
			AddStandaloneBackground();
		}

		var margin = new MarginContainer
		{
			Name = "SafeArea",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_top", 32);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_bottom", 32);
		AddChild(margin);

		var center = new CenterContainer
		{
			Name = "MenuCenter",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddChild(center);

		var menuPanel = new PanelContainer
		{
			Name = "AdventureMenuPanel",
			CustomMinimumSize = GetCompactMenuPanelSize(),
			MouseFilter = MouseFilterEnum.Stop,
		};
		menuPanel.AddThemeStyleboxOverride("panel", CreateMenuPanelStyle());
		center.AddChild(menuPanel);

		var panelMargin = new MarginContainer
		{
			Name = "AdventureMenuMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panelMargin.AddThemeConstantOverride("margin_left", 24);
		panelMargin.AddThemeConstantOverride("margin_top", 22);
		panelMargin.AddThemeConstantOverride("margin_right", 24);
		panelMargin.AddThemeConstantOverride("margin_bottom", 22);
		menuPanel.AddChild(panelMargin);

		var layout = new VBoxContainer
		{
			Name = "Layout",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 18);
		panelMargin.AddChild(layout);

		var header = new HBoxContainer
		{
			Name = "Header",
			CustomMinimumSize = new Vector2(0.0f, 58.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		header.AddThemeConstantOverride("separation", 12);
		layout.AddChild(header);

		_backButton = CreateActionButton("ADVENTURE_BACK", new Color(0.72f, 0.88f, 1.0f), ReturnToMainMenu, false, 126.0f);
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
			CustomMinimumSize = new Vector2(126.0f, 1.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		});

		var body = new HBoxContainer
		{
			Name = "Body",
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		body.AddThemeConstantOverride("separation", 18);
		layout.AddChild(body);

		body.AddChild(CreateLevelListPanel());
		body.AddChild(CreateEnemyPreviewPanel());
	}

	private void AddStandaloneBackground()
	{
		var background = new ColorRect
		{
			Name = "StandaloneBackground",
			Color = new Color(0.004f, 0.055f, 0.078f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		background.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(background);
	}

	private Vector2 GetCompactMenuPanelSize()
	{
		Vector2 viewportSize = GetViewportRect().Size;
		float width = Mathf.Clamp(viewportSize.X * 0.82f, 880.0f, 1120.0f);
		float height = Mathf.Clamp(viewportSize.Y * 0.78f, 560.0f, 690.0f);
		width = Mathf.Min(width, Mathf.Max(360.0f, viewportSize.X - 64.0f));
		height = Mathf.Min(height, Mathf.Max(420.0f, viewportSize.Y - 64.0f));
		return new Vector2(width, height);
	}

	private Button CreateActionButton(string key, Color accentColor, Action pressedAction, bool flipOrnament, float width)
	{
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{key}Frame",
			CustomMinimumSize = new Vector2(width, 56.0f),
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
		button.AddThemeFontSizeOverride("font_size", 19);
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
			CustomMinimumSize = new Vector2(360.0f, 54.0f),
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
			CustomMinimumSize = new Vector2(184.0f, 2.0f),
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
			CustomMinimumSize = new Vector2(360.0f, 44.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", 34);
		label.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		_localizedLabels.Add((label, "ADVENTURE_AVATAR_TITLE"));
		return label;
	}
}
