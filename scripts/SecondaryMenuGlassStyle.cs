using System;
using Godot;

public static class SecondaryMenuGlassStyle
{
	public sealed class VisualConfig
	{
		public VisualConfig(
			string id,
			Color mainTint,
			Color contentTint,
			Color edgeLight,
			Color mainBorder,
			Color contentBorder,
			Color scrimTint,
			float scrimAlpha,
			float mainBlur,
			float contentBlur,
			float mainOpacity,
			float contentOpacity,
			Color shadowColor,
			int shadowSize,
			Color scrollTrack,
			Color scrollGrabber,
			Color scrollGrabberHighlight)
		{
			Id = id;
			MainTint = mainTint;
			ContentTint = contentTint;
			EdgeLight = edgeLight;
			MainBorder = mainBorder;
			ContentBorder = contentBorder;
			ScrimTint = scrimTint;
			ScrimAlpha = scrimAlpha;
			MainBlur = mainBlur;
			ContentBlur = contentBlur;
			MainOpacity = mainOpacity;
			ContentOpacity = contentOpacity;
			ShadowColor = shadowColor;
			ShadowSize = shadowSize;
			ScrollTrack = scrollTrack;
			ScrollGrabber = scrollGrabber;
			ScrollGrabberHighlight = scrollGrabberHighlight;
		}

		public string Id { get; }
		public Color MainTint { get; }
		public Color ContentTint { get; }
		public Color EdgeLight { get; }
		public Color MainBorder { get; }
		public Color ContentBorder { get; }
		public Color ScrimTint { get; }
		public float ScrimAlpha { get; }
		public float MainBlur { get; }
		public float ContentBlur { get; }
		public float MainOpacity { get; }
		public float ContentOpacity { get; }
		public Color ShadowColor { get; }
		public int ShadowSize { get; }
		public Color ScrollTrack { get; }
		public Color ScrollGrabber { get; }
		public Color ScrollGrabberHighlight { get; }
	}

	public static readonly Vector2 DefaultDesignSize = new(1160.0f, 620.0f);
	public static readonly Vector2 PauseDerivedCoreDesignSize = new(840.0f, 592.0f);
	public static readonly Vector2 PauseDerivedExitDesignSize = new(620.0f, 340.0f);
	public const float SafePadding = 24.0f;
	public const string GlassShaderPath = "res://shaders/adventure_menu_glass.gdshader";
	public const string VisualProfileMetadata = "secondary_menu_visual_profile";
	public const string PauseDerivedPanelArtworkName = "PauseDerivedPanelArtwork";
	public const string PauseDerivedCenterOrnamentsRemovedMetadata = "pause_derived_center_ornaments_removed";
	public const string PauseDerivedPanelTexturePath = "res://art/ui/pause_menu/pause_panel.png";
	public const string PauseDerivedButtonIdleTexturePath = "res://art/ui/pause_menu/pause_button_idle.png";
	public const string PauseDerivedButtonFocusTexturePath = "res://art/ui/pause_menu/pause_button_focus.png";
	public const string PauseDerivedButtonPressedTexturePath = "res://art/ui/pause_menu/pause_button_pressed.png";
	public const string PauseDerivedDividerTexturePath = "res://art/ui/pause_menu/pause_divider.png";

	private static readonly Color MainTint = new(0.004f, 0.12f, 0.15f, 0.68f);
	private static readonly Color ContentTint = new(0.004f, 0.105f, 0.125f, 0.30f);
	private static readonly Color EdgeLight = new(0.52f, 1.0f, 0.94f, 0.12f);
	public static readonly VisualConfig DefaultVisual = new(
		"shared-default",
		MainTint,
		ContentTint,
		EdgeLight,
		new Color(0.56f, 1.0f, 0.94f, 0.62f),
		new Color(0.62f, 1.0f, 0.94f, 0.36f),
		new Color(0.0f, 0.035f, 0.052f, 1.0f),
		0.42f,
		2.8f,
		2.2f,
		0.92f,
		0.54f,
		new Color(0.0f, 0.02f, 0.04f, 0.70f),
		24,
		new Color(0.01f, 0.09f, 0.12f, 0.42f),
		new Color(0.50f, 0.94f, 0.90f, 0.68f),
		new Color(0.88f, 1.0f, 0.96f, 0.92f));
	public static readonly VisualConfig UnifiedVisual = new(
		"secondary-menu-unified",
		new Color(0.02f, 0.14f, 0.17f, 0.68f),
		new Color(0.02f, 0.12f, 0.15f, 0.32f),
		new Color(0.52f, 1.0f, 0.94f, 0.14f),
		new Color(0.56f, 1.0f, 0.94f, 0.60f),
		new Color(0.62f, 1.0f, 0.94f, 0.34f),
		new Color(0.0f, 0.035f, 0.052f, 1.0f),
		0.38f,
		2.6f,
		2.0f,
		0.88f,
		0.46f,
		new Color(0.0f, 0.02f, 0.04f, 0.62f),
		20,
		new Color(0.01f, 0.09f, 0.12f, 0.44f),
		new Color(0.48f, 0.90f, 0.86f, 0.70f),
		new Color(0.84f, 1.0f, 0.96f, 0.94f));
	public static readonly VisualConfig MultiplayerLobbyVisual = new(
		"secondary-menu-unified-multiplayer-readable",
		new Color(0.008f, 0.095f, 0.12f, 0.78f),
		new Color(0.008f, 0.08f, 0.105f, 0.62f),
		new Color(0.52f, 1.0f, 0.94f, 0.16f),
		new Color(0.60f, 1.0f, 0.94f, 0.68f),
		new Color(0.62f, 1.0f, 0.94f, 0.48f),
		new Color(0.0f, 0.028f, 0.045f, 1.0f),
		0.50f,
		3.0f,
		2.4f,
		0.94f,
		0.76f,
		new Color(0.0f, 0.015f, 0.03f, 0.74f),
		22,
		new Color(0.008f, 0.07f, 0.095f, 0.64f),
		new Color(0.52f, 0.94f, 0.90f, 0.78f),
		new Color(0.88f, 1.0f, 0.96f, 0.96f));
	public static readonly VisualConfig PauseDerivedVisual = new(
		"pause-derived-core-menu",
		new Color(0.027f, 0.122f, 0.176f, 1.0f),
		new Color(0.027f, 0.122f, 0.176f, 0.34f),
		new Color(0.133f, 0.725f, 0.753f, 0.22f),
		new Color(0.133f, 0.725f, 0.753f, 0.82f),
		new Color(0.831f, 0.702f, 0.310f, 0.42f),
		new Color(0.0f, 0.024f, 0.040f, 1.0f),
		0.50f,
		2.8f,
		0.0f,
		1.0f,
		0.0f,
		new Color(0.0f, 0.012f, 0.024f, 0.72f),
		24,
		new Color(0.027f, 0.122f, 0.176f, 0.78f),
		new Color(0.133f, 0.725f, 0.753f, 0.76f),
		new Color(0.831f, 0.702f, 0.310f, 0.96f));
	private static Shader? _glassShader;
	private static Shader? _scrimShader;
	private static Texture2D? _pauseDerivedPanelTexture;
	private static Texture2D? _pauseDerivedPanelLeftTexture;
	private static Texture2D? _pauseDerivedPanelRightTexture;
	private static Texture2D? _pauseDerivedButtonIdleTexture;
	private static Texture2D? _pauseDerivedButtonFocusTexture;
	private static Texture2D? _pauseDerivedButtonPressedTexture;
	private static Texture2D? _pauseDerivedDividerTexture;

	public static void Attach(
		Control root,
		Control mainPanel,
		ColorRect? scrim = null,
		params PanelContainer[] contentPanels)
	{
		Attach(root, mainPanel, scrim, DefaultDesignSize, DefaultVisual, contentPanels);
	}

	public static void Attach(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize,
		params PanelContainer[] contentPanels)
	{
		Attach(root, mainPanel, scrim, designSize, DefaultVisual, contentPanels);
	}

	public static void Attach(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize,
		VisualConfig visualConfig,
		params PanelContainer[] contentPanels)
	{
		root.SetMeta(VisualProfileMetadata, visualConfig.Id);
		ApplyReadabilityVeil(scrim, visualConfig);
		InstallGlassLayers(mainPanel, "GlassSurface", "GlassFrame", contentPane: false, visualConfig);
		foreach (PanelContainer contentPanel in contentPanels)
		{
			InstallGlassLayers(
				contentPanel,
				$"{contentPanel.Name}GlassSurface",
				$"{contentPanel.Name}GlassFrame",
				contentPane: true,
				visualConfig);
		}
		InstallResponsiveSizing(root, mainPanel, designSize);
	}

	public static void AttachUniformlyScaled(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize,
		VisualConfig visualConfig,
		params PanelContainer[] contentPanels)
	{
		root.SetMeta(VisualProfileMetadata, visualConfig.Id);
		ApplyReadabilityVeil(scrim, visualConfig);
		InstallGlassLayers(mainPanel, "GlassSurface", "GlassFrame", contentPane: false, visualConfig);
		foreach (PanelContainer contentPanel in contentPanels)
		{
			InstallGlassLayers(
				contentPanel,
				$"{contentPanel.Name}GlassSurface",
				$"{contentPanel.Name}GlassFrame",
				contentPane: true,
				visualConfig);
		}
		InstallPauseDerivedResponsiveSizing(root, mainPanel, designSize);
	}

	public static void AttachImagePanel(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize)
	{
		AttachImagePanel(root, mainPanel, scrim, designSize, DefaultVisual);
	}

	public static void AttachImagePanel(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize,
		VisualConfig visualConfig)
	{
		root.SetMeta(VisualProfileMetadata, visualConfig.Id);
		ApplyReadabilityVeil(scrim, visualConfig);
		InstallResponsiveSizing(root, mainPanel, designSize);
	}

	public static void AttachPauseDerived(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize,
		params PanelContainer[] contentSections)
	{
		AttachPauseDerived(
			root,
			mainPanel,
			scrim,
			designSize,
			removeCenterOrnaments: false,
			contentSections);
	}

	public static void AttachPauseDerived(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize,
		bool removeCenterOrnaments,
		params PanelContainer[] contentSections)
	{
		root.SetMeta(VisualProfileMetadata, PauseDerivedVisual.Id);
		mainPanel.SetMeta("pause_derived_design_size", designSize);
		mainPanel.SetMeta(PauseDerivedCenterOrnamentsRemovedMetadata, removeCenterOrnaments);
		ApplyReadabilityVeil(scrim, PauseDerivedVisual);
		InstallPauseDerivedPanelArtwork(mainPanel, removeCenterOrnaments);
		if (mainPanel is PanelContainer panelContainer)
		{
			panelContainer.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		}

		foreach (PanelContainer contentSection in contentSections)
		{
			contentSection.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		}

		InstallPauseDerivedResponsiveSizing(root, mainPanel, designSize);
	}

	public static void ConfigurePauseDerivedFrame(
		OceanMenuButtonFrame frame,
		bool selected = false,
		bool showIdleTexture = false,
		bool installDivider = true)
	{
		frame.FrameStyle = OceanMenuButtonFrame.ButtonFrameStyle.CardSlabs;
		frame.AccentColor = new Color(0.6f, 0.94f, 0.96f, 1.0f);
		frame.Selected = selected;
		frame.UsePlaqueGlowBackdrop = false;
		frame.ShowIdleTexture = showIdleTexture;
		frame.UseNinePatchTexture = false;
		frame.OceanPlaqueTexture = null;
		frame.OceanPlaqueFlippedTexture = null;
		frame.CardSlabTexture = null;
		frame.SelectionGlowTexture = null;
		frame.FocusTexture = null;
		frame.PressedTexture = null;
		frame.CustomMinimumSize = new Vector2(
			frame.CustomMinimumSize.X,
			Mathf.Max(54.0f, frame.CustomMinimumSize.Y));
		for (int childIndex = 0; childIndex < frame.GetChildCount(); childIndex++)
		{
			if (frame.GetChild(childIndex) is Button button && button is not OptionButton)
			{
				button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			}
		}
		if (installDivider)
		{
			InstallPauseDerivedRowDivider(frame);
		}
		frame.QueueRedraw();
	}

	public static void ConfigurePauseDerivedMinimalFrame(
		OceanMenuButtonFrame frame,
		OceanMenuButtonFrame.ButtonFrameStyle frameStyle,
		Color? accent = null)
	{
		frame.FrameStyle = frameStyle;
		frame.AccentColor = accent ?? MainMenuInteractiveStyle.PauseDerivedPalette.ActiveBorder;
		frame.Selected = false;
		frame.UsePlaqueGlowBackdrop = false;
		frame.UseToolRowIdleBackdrop = false;
		frame.ShowIdleTexture = false;
		frame.SuppressVisuals = frameStyle == OceanMenuButtonFrame.ButtonFrameStyle.IndividualToolRow;
		frame.UseNinePatchTexture = false;
		frame.OceanPlaqueTexture = null;
		frame.FocusTexture = null;
		frame.PressedTexture = null;
		frame.CustomMinimumSize = new Vector2(
			frame.CustomMinimumSize.X,
			Mathf.Max(54.0f, frame.CustomMinimumSize.Y));
		frame.QueueRedraw();
	}

	public static TextureRect InstallPauseDerivedDivider(Control host, string name = "PauseDerivedDivider")
	{
		EnsurePauseDerivedTextures();
		if (host is ColorRect colorRect)
		{
			colorRect.Color = Colors.Transparent;
		}

		TextureRect divider = host.GetNodeOrNull<TextureRect>(name) ?? new TextureRect
		{
			Name = name,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
		};
		if (divider.GetParent() == null)
		{
			host.AddChild(divider);
		}
		divider.Texture = _pauseDerivedDividerTexture;
		divider.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		return divider;
	}

	public static TextureRect InstallPauseDerivedRowDivider(
		Control host,
		string name = "PauseDerivedRowDivider",
		float horizontalInset = 18.0f)
	{
		EnsurePauseDerivedTextures();
		Control dividerHost = host;
		if (host is Container || host.IsClass("Container"))
		{
			string layerName = $"{name}Layer";
			dividerHost = host.GetNodeOrNull<Control>(layerName) ?? new Control
			{
				Name = layerName,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			if (dividerHost.GetParent() == null)
			{
				host.AddChild(dividerHost);
			}
		}

		TextureRect divider = dividerHost.GetNodeOrNull<TextureRect>(name) ?? new TextureRect
		{
			Name = name,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
		};
		if (divider.GetParent() == null)
		{
			dividerHost.AddChild(divider);
		}
		divider.Texture = _pauseDerivedDividerTexture;
		divider.AnchorLeft = 0.0f;
		divider.AnchorTop = 1.0f;
		divider.AnchorRight = 1.0f;
		divider.AnchorBottom = 1.0f;
		divider.OffsetLeft = horizontalInset;
		divider.OffsetTop = -3.0f;
		divider.OffsetRight = -horizontalInset;
		divider.OffsetBottom = 0.0f;
		return divider;
	}

	private static void InstallPauseDerivedPanelArtwork(Control mainPanel, bool removeCenterOrnaments)
	{
		EnsurePauseDerivedTextures();
		TextureRect artwork = mainPanel.GetNodeOrNull<TextureRect>(PauseDerivedPanelArtworkName) ?? new TextureRect
		{
			Name = PauseDerivedPanelArtworkName,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
		};
		if (artwork.GetParent() == null)
		{
			mainPanel.AddChild(artwork);
		}
		artwork.Texture = _pauseDerivedPanelTexture;
		artwork.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		artwork.SelfModulate = removeCenterOrnaments
			? new Color(1.0f, 1.0f, 1.0f, 0.0f)
			: Colors.White;
		if (removeCenterOrnaments)
		{
			InstallPauseDerivedPanelSlice(
				artwork,
				"PauseDerivedPanelLeft",
				_pauseDerivedPanelLeftTexture,
				0.0f,
				0.5f);
			InstallPauseDerivedPanelSlice(
				artwork,
				"PauseDerivedPanelRight",
				_pauseDerivedPanelRightTexture,
				0.5f,
				1.0f);
		}
		mainPanel.MoveChild(artwork, 0);
	}

	private static void InstallPauseDerivedPanelSlice(
		TextureRect artwork,
		string name,
		Texture2D? texture,
		float anchorLeft,
		float anchorRight)
	{
		TextureRect slice = artwork.GetNodeOrNull<TextureRect>(name) ?? new TextureRect
		{
			Name = name,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
		};
		if (slice.GetParent() == null)
		{
			artwork.AddChild(slice);
		}
		slice.Texture = texture;
		slice.AnchorLeft = anchorLeft;
		slice.AnchorTop = 0.0f;
		slice.AnchorRight = anchorRight;
		slice.AnchorBottom = 1.0f;
		slice.OffsetLeft = 0.0f;
		slice.OffsetTop = 0.0f;
		slice.OffsetRight = 0.0f;
		slice.OffsetBottom = 0.0f;
	}

	private static void InstallPauseDerivedResponsiveSizing(
		Control root,
		Control mainPanel,
		Vector2 designSize)
	{
		Viewport viewport = root.GetViewport();
		Control transformRoot = mainPanel.GetParentOrNull<Control>() ?? mainPanel;
		float desiredLocalScale = 1.0f;

		void ApplyPanelTransform()
		{
			if (!GodotObject.IsInstanceValid(mainPanel)
				|| !GodotObject.IsInstanceValid(transformRoot))
			{
				return;
			}

			mainPanel.Scale = Vector2.One;
			transformRoot.PivotOffset = transformRoot.Size * 0.5f;
			transformRoot.Scale = Vector2.One * desiredLocalScale;
		}

		void ResizeWindow()
		{
			if (!GodotObject.IsInstanceValid(root)
				|| !GodotObject.IsInstanceValid(mainPanel)
				|| !GodotObject.IsInstanceValid(transformRoot)
				|| !GodotObject.IsInstanceValid(viewport))
			{
				return;
			}

			Vector2 viewportSize = viewport.GetVisibleRect().Size;
			if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
			{
				viewportSize = root.Size;
			}

			Vector2 available = new(
				Mathf.Max(1.0f, viewportSize.X - SafePadding * 2.0f),
				Mathf.Max(1.0f, viewportSize.Y - SafePadding * 2.0f));
			float screenScale = Mathf.Min(
				1.0f,
				Mathf.Min(available.X / designSize.X, available.Y / designSize.Y));
			Vector2 canvasScale = root.GetGlobalTransformWithCanvas().Scale.Abs();
			float canvasUniformScale = Mathf.Max(0.001f, Mathf.Min(canvasScale.X, canvasScale.Y));
			desiredLocalScale = screenScale / canvasUniformScale;
			mainPanel.CustomMinimumSize = designSize;
			mainPanel.SetMeta("pause_derived_scale", screenScale);
			ApplyPanelTransform();
			Callable.From(ApplyPanelTransform).CallDeferred();
		}

		root.Resized += ResizeWindow;
		viewport.SizeChanged += ResizeWindow;
		root.TreeExiting += () =>
		{
			Callable resizeWindow = Callable.From(ResizeWindow);
			if (GodotObject.IsInstanceValid(viewport)
				&& viewport.IsConnected(Viewport.SignalName.SizeChanged, resizeWindow))
			{
				viewport.Disconnect(Viewport.SignalName.SizeChanged, resizeWindow);
			}
		};
		transformRoot.Resized += () => Callable.From(ApplyPanelTransform).CallDeferred();
		ResizeWindow();
		Callable.From(ResizeWindow).CallDeferred();
	}

	private static void EnsurePauseDerivedTextures()
	{
		_pauseDerivedPanelTexture ??= CreateAtlasTexture(
			PauseDerivedPanelTexturePath,
			new Rect2(41.0f, 180.0f, 942.0f, 664.0f));
		_pauseDerivedPanelLeftTexture ??= CreateAtlasTexture(
			PauseDerivedPanelTexturePath,
			new Rect2(41.0f, 180.0f, 445.0f, 664.0f));
		_pauseDerivedPanelRightTexture ??= CreateAtlasTexture(
			PauseDerivedPanelTexturePath,
			new Rect2(538.0f, 180.0f, 445.0f, 664.0f));
		_pauseDerivedButtonIdleTexture ??= CreateAtlasTexture(
			PauseDerivedButtonIdleTexturePath,
			new Rect2(41.0f, 438.0f, 942.0f, 148.0f));
		_pauseDerivedButtonFocusTexture ??= CreateAtlasTexture(
			PauseDerivedButtonFocusTexturePath,
			new Rect2(41.0f, 428.0f, 942.0f, 167.0f));
		_pauseDerivedButtonPressedTexture ??= CreateAtlasTexture(
			PauseDerivedButtonPressedTexturePath,
			new Rect2(41.0f, 426.0f, 942.0f, 172.0f));
		_pauseDerivedDividerTexture ??= CreateAtlasTexture(
			PauseDerivedDividerTexturePath,
			new Rect2(20.0f, 249.0f, 471.0f, 14.0f));
	}

	private static AtlasTexture CreateAtlasTexture(string path, Rect2 region)
	{
		return new AtlasTexture
		{
			Atlas = GD.Load<Texture2D>(path),
			Region = region,
			FilterClip = true,
		};
	}

	public static void AttachNeutralSmoke(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize)
	{
		ApplyReadabilityVeil(scrim, 0.18f, new Color(0.10f, 0.12f, 0.13f, 1.0f));
		InstallGlassLayers(mainPanel, "GlassSurface", "GlassFrame", contentPane: false, DefaultVisual);
		if (mainPanel.GetNodeOrNull<ColorRect>("GlassSurface") is { } surface)
		{
			surface.Material = CreateNeutralSmokeGlassMaterial();
		}
		if (mainPanel.GetNodeOrNull<Panel>("GlassFrame") is { } frame)
		{
			frame.AddThemeStyleboxOverride("panel", CreateNeutralSmokeFrameStyle());
		}
		InstallResponsiveSizing(root, mainPanel, designSize);
	}

	private static void InstallResponsiveSizing(Control root, Control mainPanel, Vector2 designSize)
	{
		void ResizeWindow()
		{
			Vector2 viewportSize = root.Size;
			if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
			{
				viewportSize = root.GetViewportRect().Size;
			}

			Vector2 available = new(
				Mathf.Max(1.0f, viewportSize.X - SafePadding * 2.0f),
				Mathf.Max(1.0f, viewportSize.Y - SafePadding * 2.0f));
			mainPanel.CustomMinimumSize = new Vector2(
				Mathf.Min(designSize.X, available.X),
				Mathf.Min(designSize.Y, available.Y));
			mainPanel.PivotOffset = mainPanel.Size * 0.5f;
		}

		root.Resized += ResizeWindow;
		mainPanel.Resized += () => mainPanel.PivotOffset = mainPanel.Size * 0.5f;
		ResizeWindow();
	}

	public static void StyleScrollBar(ScrollBar scrollBar)
	{
		StyleScrollBar(scrollBar, DefaultVisual);
	}

	public static void StyleScrollBar(ScrollBar scrollBar, VisualConfig visualConfig)
	{
		scrollBar.CustomMinimumSize = new Vector2(12.0f, 0.0f);
		scrollBar.AddThemeConstantOverride("padding_left", 2);
		scrollBar.AddThemeConstantOverride("padding_right", 2);

		StyleBoxFlat track = new()
		{
			BgColor = visualConfig.ScrollTrack,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginTop = 4.0f,
			ContentMarginBottom = 4.0f,
		};
		StyleBoxFlat grabber = CreateScrollGrabber(visualConfig.ScrollGrabber, visualConfig.ContentBorder);
		StyleBoxFlat grabberHighlight = CreateScrollGrabber(visualConfig.ScrollGrabberHighlight, visualConfig.MainBorder);

		scrollBar.AddThemeStyleboxOverride("scroll", track);
		scrollBar.AddThemeStyleboxOverride("scroll_focus", track);
		scrollBar.AddThemeStyleboxOverride("grabber", grabber);
		scrollBar.AddThemeStyleboxOverride("grabber_highlight", grabberHighlight);
		scrollBar.AddThemeStyleboxOverride("grabber_pressed", grabberHighlight);
	}

	public static void StyleGlassPanel(
		Control panel,
		VisualConfig visualConfig,
		bool contentPane = true)
	{
		InstallGlassLayers(panel, "GlassSurface", "GlassFrame", contentPane, visualConfig);
	}

	private static void ApplyReadabilityVeil(
		ColorRect? scrim,
		float tintAlpha = 0.42f,
		Color? tint = null)
	{
		if (scrim == null)
		{
			return;
		}

		Color veilTint = tint ?? new Color(0.0f, 0.035f, 0.052f, 1.0f);
		if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
		{
			scrim.Material = null;
			scrim.Color = new Color(veilTint.R, veilTint.G, veilTint.B, tintAlpha);
			return;
		}

		_scrimShader ??= GD.Load<Shader>("res://shaders/menu_blur_scrim.gdshader");
		ShaderMaterial material = new() { Shader = _scrimShader };
		material.SetShaderParameter("blur_lod", 2.8f);
		material.SetShaderParameter("tint_color", new Color(veilTint.R, veilTint.G, veilTint.B, tintAlpha));
		scrim.Material = material;
		scrim.Color = Colors.White;
	}

	public static void ApplyReadabilityVeil(ColorRect? scrim, VisualConfig visualConfig)
	{
		ApplyReadabilityVeil(scrim, visualConfig.ScrimAlpha, visualConfig.ScrimTint);
	}

	private static void InstallGlassLayers(
		Control target,
		string surfaceName,
		string frameName,
		bool contentPane,
		VisualConfig visualConfig)
	{
		if (target.GetNodeOrNull<ColorRect>(surfaceName) is { } existingSurface
			&& target.GetNodeOrNull<Panel>(frameName) is { } existingFrame)
		{
			existingSurface.Material = CreateGlassMaterial(contentPane, visualConfig);
			existingFrame.AddThemeStyleboxOverride("panel", CreateFrameStyle(contentPane, visualConfig));
			return;
		}

		ColorRect surface = new()
		{
			Name = surfaceName,
			Material = CreateGlassMaterial(contentPane, visualConfig),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		target.AddChild(surface);
		surface.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		target.MoveChild(surface, 0);

		Panel frame = new()
		{
			Name = frameName,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		frame.AddThemeStyleboxOverride("panel", CreateFrameStyle(contentPane, visualConfig));
		target.AddChild(frame);
		frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		target.MoveChild(frame, 1);

		if (target is PanelContainer panelContainer)
		{
			panelContainer.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		}
	}

	private static ShaderMaterial CreateGlassMaterial(bool contentPane)
	{
		return CreateGlassMaterial(contentPane, DefaultVisual);
	}

	public static ShaderMaterial CreateGlassMaterial(bool contentPane, VisualConfig visualConfig)
	{
		_glassShader ??= GD.Load<Shader>(GlassShaderPath);
		ShaderMaterial material = new() { Shader = _glassShader };
		material.SetShaderParameter("corner_radius", 8.0f);
		material.SetShaderParameter("blur_lod", contentPane ? visualConfig.ContentBlur : visualConfig.MainBlur);
		material.SetShaderParameter("tint_color", contentPane ? visualConfig.ContentTint : visualConfig.MainTint);
		material.SetShaderParameter("edge_light_color", contentPane
			? new Color(visualConfig.EdgeLight.R, visualConfig.EdgeLight.G, visualConfig.EdgeLight.B, visualConfig.EdgeLight.A + 0.04f)
			: visualConfig.EdgeLight);
		material.SetShaderParameter("surface_opacity", contentPane ? visualConfig.ContentOpacity : visualConfig.MainOpacity);
		return material;
	}

	private static ShaderMaterial CreateNeutralSmokeGlassMaterial()
	{
		_glassShader ??= GD.Load<Shader>(GlassShaderPath);
		ShaderMaterial material = new() { Shader = _glassShader };
		material.SetShaderParameter("corner_radius", 5.0f);
		material.SetShaderParameter("blur_lod", 2.4f);
		material.SetShaderParameter("tint_color", new Color(0.46f, 0.50f, 0.51f, 0.70f));
		material.SetShaderParameter("edge_light_color", new Color(0.76f, 0.84f, 0.83f, 0.18f));
		material.SetShaderParameter("surface_opacity", 0.88f);
		return material;
	}

	private static StyleBoxFlat CreateFrameStyle(bool contentPane)
	{
		return CreateFrameStyle(contentPane, DefaultVisual);
	}

	public static StyleBoxFlat CreateFrameStyle(bool contentPane, VisualConfig visualConfig)
	{
		return new StyleBoxFlat
		{
			BgColor = contentPane ? new Color(visualConfig.ContentTint.R, visualConfig.ContentTint.G, visualConfig.ContentTint.B, 0.06f) : Colors.Transparent,
			BorderColor = contentPane ? visualConfig.ContentBorder : visualConfig.MainBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = contentPane ? 1 : 2,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = contentPane
				? new Color(visualConfig.ShadowColor.R, visualConfig.ShadowColor.G, visualConfig.ShadowColor.B, visualConfig.ShadowColor.A * 0.46f)
				: visualConfig.ShadowColor,
			ShadowSize = contentPane ? 4 : visualConfig.ShadowSize,
			ShadowOffset = contentPane ? Vector2.Zero : new Vector2(0.0f, Mathf.Min(8.0f, visualConfig.ShadowSize * 0.4f)),
		};
	}

	private static StyleBoxFlat CreateNeutralSmokeFrameStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			BorderColor = new Color(0.70f, 0.80f, 0.79f, 0.54f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5,
			ShadowColor = new Color(0.02f, 0.03f, 0.035f, 0.46f),
			ShadowSize = 18,
			ShadowOffset = new Vector2(0.0f, 8.0f),
		};
	}

	private static StyleBoxFlat CreateScrollGrabber(Color background, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = new Color(border.R, border.G, border.B, Mathf.Max(0.54f, border.A)),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};
	}
}
