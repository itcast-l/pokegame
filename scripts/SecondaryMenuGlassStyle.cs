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
	public const float SafePadding = 24.0f;
	public const string GlassShaderPath = "res://shaders/adventure_menu_glass.gdshader";
	public const string VisualProfileMetadata = "secondary_menu_visual_profile";

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
	private static Shader? _glassShader;
	private static Shader? _scrimShader;

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

		_scrimShader ??= GD.Load<Shader>("res://shaders/menu_blur_scrim.gdshader");
		ShaderMaterial material = new() { Shader = _scrimShader };
		Color veilTint = tint ?? new Color(0.0f, 0.035f, 0.052f, 1.0f);
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
