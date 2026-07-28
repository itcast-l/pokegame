using System;
using Godot;

public static class SecondaryMenuGlassStyle
{
	public static readonly Vector2 DefaultDesignSize = new(1160.0f, 620.0f);
	public const float SafePadding = 24.0f;
	public const string GlassShaderPath = "res://shaders/adventure_menu_glass.gdshader";

	private static readonly Color MainTint = new(0.004f, 0.12f, 0.15f, 0.68f);
	private static readonly Color ContentTint = new(0.004f, 0.105f, 0.125f, 0.30f);
	private static readonly Color EdgeLight = new(0.52f, 1.0f, 0.94f, 0.12f);
	private static Shader? _glassShader;
	private static Shader? _scrimShader;

	public static void Attach(
		Control root,
		Control mainPanel,
		ColorRect? scrim = null,
		params PanelContainer[] contentPanels)
	{
		Attach(root, mainPanel, scrim, DefaultDesignSize, contentPanels);
	}

	public static void Attach(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize,
		params PanelContainer[] contentPanels)
	{
		ApplyReadabilityVeil(scrim);
		InstallGlassLayers(mainPanel, "GlassSurface", "GlassFrame", contentPane: false);
		foreach (PanelContainer contentPanel in contentPanels)
		{
			InstallGlassLayers(
				contentPanel,
				$"{contentPanel.Name}GlassSurface",
				$"{contentPanel.Name}GlassFrame",
				contentPane: true);
		}
		InstallResponsiveSizing(root, mainPanel, designSize);
	}

	public static void AttachImagePanel(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize)
	{
		ApplyReadabilityVeil(scrim);
		InstallResponsiveSizing(root, mainPanel, designSize);
	}

	public static void AttachNeutralSmoke(
		Control root,
		Control mainPanel,
		ColorRect? scrim,
		Vector2 designSize)
	{
		ApplyReadabilityVeil(scrim, 0.18f, new Color(0.10f, 0.12f, 0.13f, 1.0f));
		InstallGlassLayers(mainPanel, "GlassSurface", "GlassFrame", contentPane: false);
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
		scrollBar.CustomMinimumSize = new Vector2(12.0f, 0.0f);
		scrollBar.AddThemeConstantOverride("padding_left", 2);
		scrollBar.AddThemeConstantOverride("padding_right", 2);

		StyleBoxFlat track = new()
		{
			BgColor = new Color(0.01f, 0.09f, 0.12f, 0.42f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginTop = 4.0f,
			ContentMarginBottom = 4.0f,
		};
		StyleBoxFlat grabber = CreateScrollGrabber(new Color(0.50f, 0.94f, 0.90f, 0.68f));
		StyleBoxFlat grabberHighlight = CreateScrollGrabber(new Color(0.88f, 1.0f, 0.96f, 0.92f));

		scrollBar.AddThemeStyleboxOverride("scroll", track);
		scrollBar.AddThemeStyleboxOverride("scroll_focus", track);
		scrollBar.AddThemeStyleboxOverride("grabber", grabber);
		scrollBar.AddThemeStyleboxOverride("grabber_highlight", grabberHighlight);
		scrollBar.AddThemeStyleboxOverride("grabber_pressed", grabberHighlight);
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

	private static void InstallGlassLayers(
		Control target,
		string surfaceName,
		string frameName,
		bool contentPane)
	{
		if (target.GetNodeOrNull<Control>(surfaceName) != null)
		{
			return;
		}

		ColorRect surface = new()
		{
			Name = surfaceName,
			Material = CreateGlassMaterial(contentPane),
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
		frame.AddThemeStyleboxOverride("panel", CreateFrameStyle(contentPane));
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
		_glassShader ??= GD.Load<Shader>(GlassShaderPath);
		ShaderMaterial material = new() { Shader = _glassShader };
		material.SetShaderParameter("corner_radius", 8.0f);
		material.SetShaderParameter("blur_lod", contentPane ? 2.2f : 2.8f);
		material.SetShaderParameter("tint_color", contentPane ? ContentTint : MainTint);
		material.SetShaderParameter("edge_light_color", contentPane
			? new Color(EdgeLight.R, EdgeLight.G, EdgeLight.B, 0.16f)
			: EdgeLight);
		material.SetShaderParameter("surface_opacity", contentPane ? 0.54f : 0.92f);
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
		return new StyleBoxFlat
		{
			BgColor = contentPane
				? new Color(0.005f, 0.08f, 0.10f, 0.07f)
				: Colors.Transparent,
			BorderColor = contentPane
				? new Color(0.62f, 1.0f, 0.94f, 0.36f)
				: new Color(0.56f, 1.0f, 0.94f, 0.62f),
			BorderWidthLeft = 1,
			BorderWidthTop = contentPane ? 1 : 2,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = contentPane
				? new Color(0.0f, 0.02f, 0.04f, 0.24f)
				: new Color(0.0f, 0.02f, 0.04f, 0.70f),
			ShadowSize = contentPane ? 4 : 24,
			ShadowOffset = contentPane ? Vector2.Zero : new Vector2(0.0f, 9.0f),
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

	private static StyleBoxFlat CreateScrollGrabber(Color background)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = new Color(0.20f, 0.72f, 0.72f, 0.88f),
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
