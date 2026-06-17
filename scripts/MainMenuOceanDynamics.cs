using System;
using System.Collections.Generic;
using Godot;

public partial class MainMenuOceanDynamics : Control
{
	public enum OceanRenderMode
	{
		Procedural,
		ImageBased,
	}

	private const float WaterlineRatio = 0.34f;

	private const int SunbeamInstanceCount = 5;
	private const int SurfaceWakeInstanceCount = 6;
	private const int CurrentRibbonInstanceCount = 8;
	private const int CausticRibbonInstanceCount = 7;

	private const string SunbeamFramePattern = "res://art/fx/environment/main_menu_sunbeam/run-001/processed/fx-{0}.png";
	private const string SurfaceWakeFramePattern = "res://art/fx/environment/main_menu_surface_wake/run-001/processed/fx-{0}.png";
	private const string CurrentRibbonFramePattern = "res://art/fx/environment/main_menu_current_ribbon/run-001/processed/fx-{0}.png";
	private const string CausticRibbonFramePattern = "res://art/fx/environment/main_menu_caustic_ribbon/run-001/processed/fx-{0}.png";
	private const string GlintFramePattern = "res://art/fx/environment/main_menu_glint/run-001/processed/fx-{0}.png";

	private const int SunbeamFrameCount = 6;
	private const int SurfaceWakeFrameCount = 8;
	private const int CurrentRibbonFrameCount = 8;
	private const int CausticRibbonFrameCount = 8;
	private const int GlintFrameCount = 6;

	private const float SunbeamFramesPerSecond = 2.0f;
	private const float SurfaceWakeFramesPerSecond = 4.0f;
	private const float CurrentRibbonFramesPerSecond = 4.0f;
	private const float CausticRibbonFramesPerSecond = 5.0f;
	private const float GlintFramesPerSecond = 8.0f;

	private static readonly Vector2 SunbeamFrameSize = new(384.0f, 720.0f);
	private static readonly Vector2 SurfaceWakeFrameSize = new(640.0f, 72.0f);
	private static readonly Vector2 CurrentRibbonFrameSize = new(512.0f, 128.0f);
	private static readonly Vector2 CausticRibbonFrameSize = new(512.0f, 128.0f);
	private static readonly Vector2 GlintFrameSize = new(96.0f, 96.0f);

	[Export]
	public OceanRenderMode RenderMode { get; set; } = OceanRenderMode.Procedural;

	[Export(PropertyHint.Range, "0,60,1")]
	public int glint_count = 26;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float motion_scale = 1.0f;

	[Export(PropertyHint.Range, "1,60,1")]
	public int redraw_fps = 20;

	private readonly RandomNumberGenerator _rng = new();
	private readonly List<Glint> _glints = new();
	private readonly Vector2[] _sunbeamPoints = new Vector2[4];
	private readonly Vector2[] _surfaceWakePoints = new Vector2[56];
	private readonly Vector2[] _currentRibbonPoints = new Vector2[34];
	private readonly Vector2[] _causticRibbonPoints = new Vector2[46];

	private readonly List<ImageElement> _sunbeamInstances = new();
	private readonly List<ImageElement> _surfaceWakeInstances = new();
	private readonly List<ImageElement> _currentRibbonInstances = new();
	private readonly List<ImageElement> _causticRibbonInstances = new();
	private readonly List<ImageElement> _glintInstances = new();

	private double _time;
	private double _redrawAccumulator;
	private bool _imageAssetsLoaded;
	private bool _pendingImageRebuild;
	private OceanRenderMode _activeRenderMode = (OceanRenderMode)(-1);

	private Texture2D[] _sunbeamFrames = Array.Empty<Texture2D>();
	private Texture2D[] _surfaceWakeFrames = Array.Empty<Texture2D>();
	private Texture2D[] _currentRibbonFrames = Array.Empty<Texture2D>();
	private Texture2D[] _causticRibbonFrames = Array.Empty<Texture2D>();
	private Texture2D[] _glintFrames = Array.Empty<Texture2D>();

	private Control? _sunbeamLayer;
	private Control? _surfaceWakeLayer;
	private Control? _currentRibbonLayer;
	private Control? _causticRibbonLayer;
	private Control? _glintLayer;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_rng.Seed = 80110;
		ApplyRenderMode(force: true);
		Rebuild();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			Rebuild();
		}
	}

	public override void _Process(double delta)
	{
		if (RenderMode != _activeRenderMode)
		{
			ApplyRenderMode(force: true);
			Rebuild();
		}

		if (_pendingImageRebuild && Size.X > 0.0f && Size.Y > 0.0f)
		{
			Rebuild();
		}

		if (!IsVisibleInTree())
		{
			return;
		}

		_time += delta * motion_scale;
		if (_activeRenderMode == OceanRenderMode.ImageBased)
		{
			if (HasNoImageInstances())
			{
				EnsureImageAssetsLoaded(retryIfAllMissing: true);
				RebuildImageInstances();
			}
			UpdateImageInstances();
			return;
		}

		_redrawAccumulator += delta;
		double redrawInterval = 1.0 / Mathf.Max(redraw_fps, 1);
		if (_redrawAccumulator < redrawInterval)
		{
			return;
		}

		_redrawAccumulator %= redrawInterval;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_activeRenderMode != OceanRenderMode.Procedural
			|| Size.X <= 0.0f
			|| Size.Y <= 0.0f)
		{
			return;
		}

		DrawSunbeams();
		DrawSurfaceWake();
		DrawCurrentRibbons();
		DrawCausticRibbons();
		DrawGlints();
	}

	private void ApplyRenderMode(bool force = false)
	{
		if (!force && RenderMode == _activeRenderMode)
		{
			return;
		}

		_activeRenderMode = RenderMode;
		_redrawAccumulator = 0.0;
		if (_activeRenderMode == OceanRenderMode.ImageBased)
		{
			EnsureImageAssetsLoaded();
			EnsureImageLayers();
			if (Size.X > 0.0f && Size.Y > 0.0f)
			{
				RebuildImageInstances();
			}
			else
			{
				_pendingImageRebuild = true;
			}
			SetImageLayersVisible(true);
		}
		else
		{
			SetImageLayersVisible(false);
			QueueRedraw();
		}
	}

	private void Rebuild()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			_pendingImageRebuild = true;
			return;
		}

		_pendingImageRebuild = false;
		RebuildParticleData();
		if (_activeRenderMode == OceanRenderMode.ImageBased)
		{
			EnsureImageAssetsLoaded(retryIfAllMissing: true);
			EnsureImageLayers();
			RebuildImageInstances();
			UpdateImageInstances();
			return;
		}

		QueueRedraw();
	}

	private void RebuildParticleData()
	{
		_glints.Clear();

		for (int i = 0; i < glint_count; i++)
		{
			_glints.Add(new Glint(
				_rng.RandfRange(0.38f, 0.98f),
				_rng.RandfRange(WaterlineRatio + 0.06f, 0.9f),
				_rng.RandfRange(1.4f, 4.2f),
				_rng.RandfRange(0.55f, 1.35f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(0.08f, 0.26f)));
		}
	}

	private void EnsureImageAssetsLoaded(bool retryIfAllMissing = false)
	{
		if (_imageAssetsLoaded && !(retryIfAllMissing && AllImageFramesMissing()))
		{
			return;
		}

		_sunbeamFrames = FrameAnimationHelpers.LoadTextureFrames(SunbeamFramePattern, SunbeamFrameCount, "main menu sunbeam", preferImageFile: true);
		_surfaceWakeFrames = FrameAnimationHelpers.LoadTextureFrames(SurfaceWakeFramePattern, SurfaceWakeFrameCount, "main menu surface wake", preferImageFile: true);
		_currentRibbonFrames = FrameAnimationHelpers.LoadTextureFrames(CurrentRibbonFramePattern, CurrentRibbonFrameCount, "main menu current ribbon", preferImageFile: true);
		_causticRibbonFrames = FrameAnimationHelpers.LoadTextureFrames(CausticRibbonFramePattern, CausticRibbonFrameCount, "main menu caustic ribbon", preferImageFile: true);
		_glintFrames = FrameAnimationHelpers.LoadTextureFrames(GlintFramePattern, GlintFrameCount, "main menu glint", preferImageFile: true);
		_imageAssetsLoaded = true;
	}

	private bool AllImageFramesMissing()
	{
		return _sunbeamFrames.Length == 0
			&& _surfaceWakeFrames.Length == 0
			&& _currentRibbonFrames.Length == 0
			&& _causticRibbonFrames.Length == 0
			&& _glintFrames.Length == 0;
	}

	private bool HasNoImageInstances()
	{
		return _sunbeamInstances.Count == 0
			&& _surfaceWakeInstances.Count == 0
			&& _currentRibbonInstances.Count == 0
			&& _causticRibbonInstances.Count == 0
			&& _glintInstances.Count == 0;
	}

	private void EnsureImageLayers()
	{
		GetOrCreateLayer(ref _sunbeamLayer, "SunbeamLayer");
		GetOrCreateLayer(ref _surfaceWakeLayer, "SurfaceWakeLayer");
		GetOrCreateLayer(ref _currentRibbonLayer, "CurrentRibbonLayer");
		GetOrCreateLayer(ref _causticRibbonLayer, "CausticRibbonLayer");
		GetOrCreateLayer(ref _glintLayer, "GlintLayer");
	}

	private void SetImageLayersVisible(bool visible)
	{
		SetLayerVisible(_sunbeamLayer, visible);
		SetLayerVisible(_surfaceWakeLayer, visible);
		SetLayerVisible(_currentRibbonLayer, visible);
		SetLayerVisible(_causticRibbonLayer, visible);
		SetLayerVisible(_glintLayer, visible);
	}

	private Control GetOrCreateLayer(ref Control? layer, string name)
	{
		if (layer != null && GodotObject.IsInstanceValid(layer))
		{
			layer.Visible = true;
			return layer;
		}

		layer = new Control
		{
			Name = name,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
		};
		layer.SetAnchorsPreset(LayoutPreset.FullRect);
		layer.OffsetRight = 0.0f;
		layer.OffsetBottom = 0.0f;
		AddChild(layer);
		return layer;
	}

	private static void SetLayerVisible(Control? layer, bool visible)
	{
		if (layer != null && GodotObject.IsInstanceValid(layer))
		{
			layer.Visible = visible;
		}
	}

	private void RebuildImageInstances()
	{
		ClearImageInstances();
		BuildSunbeamInstances();
		BuildSurfaceWakeInstances();
		BuildCurrentRibbonInstances();
		BuildCausticRibbonInstances();
		BuildGlintInstances();
	}

	private void ClearImageInstances()
	{
		ClearImageLayer(_sunbeamLayer, _sunbeamInstances);
		ClearImageLayer(_surfaceWakeLayer, _surfaceWakeInstances);
		ClearImageLayer(_currentRibbonLayer, _currentRibbonInstances);
		ClearImageLayer(_causticRibbonLayer, _causticRibbonInstances);
		ClearImageLayer(_glintLayer, _glintInstances);
	}

	private static void ClearImageLayer(Control? layer, List<ImageElement> instances)
	{
		instances.Clear();
		if (layer == null || !GodotObject.IsInstanceValid(layer))
		{
			return;
		}

		while (layer.GetChildCount() > 0)
		{
			layer.GetChild(0).Free();
		}
	}

	private void BuildSunbeamInstances()
	{
		if (_sunbeamLayer == null || _sunbeamFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < SunbeamInstanceCount; index++)
		{
			var instance = CreateImageElement(
				_sunbeamLayer,
				$"Sunbeam{index + 1}",
				_sunbeamFrames,
				SunbeamFrameSize,
				SunbeamFramesPerSecond);
			instance.XRatio = 0.56f + index * 0.08f + _rng.RandfRange(-0.008f, 0.008f);
			instance.Scale = 0.44f + index * 0.035f;
			instance.Alpha = 0.028f + index * 0.004f;
			instance.Phase = _rng.RandfRange(0.0f, Mathf.Tau);
			instance.Speed = _rng.RandfRange(0.01f, 0.03f);
			instance.Drift = _rng.RandfRange(0.0f, 0.003f);
			instance.Secondary = _rng.RandfRange(0.01f, 0.02f);
			_sunbeamInstances.Add(instance);
		}
	}

	private void BuildSurfaceWakeInstances()
	{
		if (_surfaceWakeLayer == null || _surfaceWakeFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < SurfaceWakeInstanceCount; index++)
		{
			var instance = CreateImageElement(
				_surfaceWakeLayer,
				$"SurfaceWake{index + 1}",
				_surfaceWakeFrames,
				SurfaceWakeFrameSize,
				SurfaceWakeFramesPerSecond);
			instance.XRatio = 0.56f + _rng.RandfRange(-0.035f, 0.035f);
			instance.YRatio = 0.304f + index * 0.010f + _rng.RandfRange(-0.0025f, 0.0025f);
			instance.Scale = 0.30f + index * 0.025f;
			instance.Alpha = 0.050f - index * 0.005f;
			instance.Phase = _rng.RandfRange(0.0f, Mathf.Tau);
			instance.Speed = _rng.RandfRange(0.02f, 0.05f);
			instance.Drift = _rng.RandfRange(1.0f, 3.0f);
			_surfaceWakeInstances.Add(instance);
		}
	}

	private void BuildCurrentRibbonInstances()
	{
		if (_currentRibbonLayer == null || _currentRibbonFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < CurrentRibbonInstanceCount; index++)
		{
			var instance = CreateImageElement(
				_currentRibbonLayer,
				$"CurrentRibbon{index + 1}",
				_currentRibbonFrames,
				CurrentRibbonFrameSize,
				CurrentRibbonFramesPerSecond);
			instance.XRatio = -0.18f + index * 0.11f;
			instance.YRatio = WaterlineRatio + 0.08f + index * 0.045f + _rng.RandfRange(-0.006f, 0.006f);
			instance.Scale = 0.14f + _rng.RandfRange(0.025f, 0.06f);
			instance.Alpha = 0.042f + _rng.RandfRange(0.004f, 0.014f);
			instance.Phase = _rng.RandfRange(0.0f, Mathf.Tau);
			instance.Speed = 0.025f + index * 0.004f;
			instance.Drift = _rng.RandfRange(2.0f, 5.0f);
			_currentRibbonInstances.Add(instance);
		}
	}

	private void BuildCausticRibbonInstances()
	{
		if (_causticRibbonLayer == null || _causticRibbonFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < CausticRibbonInstanceCount; index++)
		{
			var instance = CreateImageElement(
				_causticRibbonLayer,
				$"CausticRibbon{index + 1}",
				_causticRibbonFrames,
				CausticRibbonFrameSize,
				CausticRibbonFramesPerSecond);
			instance.XRatio = 0.58f + _rng.RandfRange(-0.08f, 0.08f);
			instance.YRatio = 0.58f + index * 0.045f + _rng.RandfRange(-0.008f, 0.008f);
			instance.Scale = 0.28f + _rng.RandfRange(0.04f, 0.12f);
			instance.Alpha = 0.07f + _rng.RandfRange(0.0f, 0.02f);
			instance.Phase = _rng.RandfRange(0.0f, Mathf.Tau);
			instance.Speed = _rng.RandfRange(0.18f, 0.32f);
			instance.Drift = _rng.RandfRange(2.0f, 6.0f);
			_causticRibbonInstances.Add(instance);
		}
	}

	private void BuildGlintInstances()
	{
		if (_glintLayer == null || _glintFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < _glints.Count; index++)
		{
			Glint glint = _glints[index];
			var instance = CreateImageElement(
				_glintLayer,
				$"Glint{index + 1}",
				_glintFrames,
				GlintFrameSize,
				GlintFramesPerSecond);
			instance.XRatio = glint.XRatio;
			instance.YRatio = glint.YRatio;
			instance.Scale = glint.Radius / 10.0f;
			instance.Alpha = glint.Alpha;
			instance.Phase = glint.Phase;
			instance.Speed = glint.Speed;
			_glintInstances.Add(instance);
		}
	}

	private ImageElement CreateImageElement(
		Control layer,
		string name,
		Texture2D[] frames,
		Vector2 baseSize,
		float framesPerSecond)
	{
		var node = new TextureRect
		{
			Name = name,
			Texture = frames.Length > 0 ? frames[0] : null,
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Size = baseSize,
			PivotOffset = baseSize / 2.0f,
		};
		layer.AddChild(node);
		return new ImageElement
		{
			Node = node,
			Frames = frames,
			BaseSize = baseSize,
			FramesPerSecond = framesPerSecond,
		};
	}

	private void UpdateImageInstances()
	{
		UpdateSunbeamInstances();
		UpdateSurfaceWakeInstances();
		UpdateCurrentRibbonInstances();
		UpdateCausticRibbonInstances();
		UpdateGlintInstances();
	}

	private void UpdateSunbeamInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _sunbeamInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float widthPulse = 0.72f + Mathf.Sin(time * 0.18f + instance.Phase) * instance.Secondary;
			float scale = (Size.Y / instance.BaseSize.Y) * instance.Scale;
			Vector2 size = new(instance.BaseSize.X * scale * widthPulse, instance.BaseSize.Y * scale);
			float centerX = Size.X * instance.XRatio
				+ Mathf.Sin(time * (0.02f + instance.Speed) + instance.Phase) * Size.X * instance.Drift;
			float topY = -Size.Y * 0.015f + Mathf.Sin(time * 0.06f + instance.Phase) * 1.0f;
			float alpha = instance.Alpha * (0.58f + 0.08f * Mathf.Sin(time * 0.14f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, topY + size.Y * 0.5f), size, alpha);
		}
	}

	private void UpdateSurfaceWakeInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _surfaceWakeInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float width = Size.X * instance.Scale;
			float scale = width / instance.BaseSize.X;
			Vector2 size = instance.BaseSize * scale;
			float centerX = Size.X * instance.XRatio + Mathf.Sin(time * (0.20f + instance.Speed) + instance.Phase) * 8.0f;
			float centerY = Size.Y * instance.YRatio + Mathf.Sin(time * (0.24f + instance.Speed) + instance.Phase) * instance.Drift;
			float alpha = instance.Alpha * (0.68f + 0.14f * Mathf.Sin(time * 0.28f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateCurrentRibbonInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _currentRibbonInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float width = Size.X * instance.Scale;
			float scale = width / instance.BaseSize.X;
			Vector2 size = instance.BaseSize * scale;
			float xRatio = Mathf.PosMod(instance.XRatio + time * instance.Speed, 1.28f) - 0.18f;
			float centerX = Size.X * xRatio;
			float centerY = Size.Y * instance.YRatio + Mathf.Sin(time * 0.50f + instance.Phase) * instance.Drift;
			float alpha = instance.Alpha * (0.64f + 0.14f * Mathf.Sin(time * 0.38f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateCausticRibbonInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _causticRibbonInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float width = Size.X * instance.Scale;
			float scale = width / instance.BaseSize.X;
			Vector2 size = instance.BaseSize * scale;
			float centerX = Size.X * (instance.XRatio + Mathf.Sin(time * instance.Speed + instance.Phase) * 0.08f);
			float centerY = Size.Y * instance.YRatio + Mathf.Sin(time * 0.42f + instance.Phase) * instance.Drift;
			float alpha = instance.Alpha * (0.78f + 0.22f * Mathf.Sin(time * 0.28f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateGlintInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _glintInstances)
		{
			instance.ElapsedTime = time;
			float pulse = Mathf.Max(0.0f, Mathf.Sin(time * instance.Speed + instance.Phase));
			if (pulse <= 0.04f)
			{
				instance.Node.Visible = false;
				continue;
			}

			ApplyAnimatedFrame(instance);
			Vector2 size = instance.BaseSize * instance.Scale * (0.8f + pulse * 0.7f);
			float centerX = Size.X * instance.XRatio + Mathf.Sin(time * 0.18f + instance.Phase) * 12.0f;
			float centerY = Size.Y * instance.YRatio + Mathf.Cos(time * 0.22f + instance.Phase) * 8.0f;
			float alpha = instance.Alpha * pulse;
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private static void ApplyAnimatedFrame(ImageElement instance)
	{
		if (instance.Frames.Length == 0)
		{
			instance.Node.Texture = null;
			return;
		}

		float frameTime = instance.ElapsedTime * instance.FramesPerSecond
			+ instance.Phase * instance.Frames.Length / Mathf.Tau;
		int frameIndex = WrapFrameIndex(Mathf.FloorToInt(frameTime), instance.Frames.Length);
		Texture2D texture = instance.Frames[frameIndex];
		if (instance.Node.Texture != texture)
		{
			instance.Node.Texture = texture;
		}
	}

	private static int WrapFrameIndex(int frameIndex, int frameCount)
	{
		if (frameCount <= 0)
		{
			return 0;
		}

		int wrapped = frameIndex % frameCount;
		return wrapped < 0 ? wrapped + frameCount : wrapped;
	}

	private void ApplyImageLayout(ImageElement instance, Vector2 center, Vector2 size, float alpha)
	{
		instance.ElapsedTime = (float)_time;
		if (size.X <= 0.0f || size.Y <= 0.0f || alpha <= 0.001f)
		{
			instance.Node.Visible = false;
			return;
		}

		instance.Node.Visible = true;
		instance.Node.Size = size;
		instance.Node.PivotOffset = size / 2.0f;
		instance.Node.Position = center - size / 2.0f;
		instance.Node.Modulate = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp(alpha, 0.0f, 1.0f));
	}

	private void DrawSunbeams()
	{
		float t = (float)_time;
		for (int i = 0; i < 5; i++)
		{
			float x = Size.X * (0.47f + i * 0.12f + Mathf.Sin(t * 0.08f + i) * 0.015f);
			float width = Size.X * (0.07f + i * 0.014f);
			float alpha = 0.062f + Mathf.Sin(t * 0.42f + i * 1.7f) * 0.024f;
			_sunbeamPoints[0] = new Vector2(x - width * 0.3f, 0.0f);
			_sunbeamPoints[1] = new Vector2(x + width * 0.8f, 0.0f);
			_sunbeamPoints[2] = new Vector2(x + width * 1.65f, Size.Y);
			_sunbeamPoints[3] = new Vector2(x - width * 1.1f, Size.Y);
			DrawColoredPolygon(_sunbeamPoints, new Color(0.74f, 1.0f, 0.96f, Mathf.Max(alpha, 0.0f)));
		}
	}

	private void DrawSurfaceWake()
	{
		float t = (float)_time;
		float baseY = Size.Y * 0.315f;
		for (int line = 0; line < 6; line++)
		{
			float y = baseY + line * Size.Y * 0.018f;
			for (int i = 0; i < _surfaceWakePoints.Length; i++)
			{
				float ratio = i / (float)(_surfaceWakePoints.Length - 1);
				float x = ratio * Size.X;
				float wave = Mathf.Sin(ratio * Mathf.Tau * (2.0f + line * 0.22f) + t * (0.46f + line * 0.06f)) * (5.0f + line);
				float ripple = Mathf.Sin(ratio * Mathf.Tau * 8.0f - t * 0.7f + line) * 1.8f;
				_surfaceWakePoints[i] = new Vector2(x, y + wave + ripple);
			}

			DrawPolyline(_surfaceWakePoints, new Color(0.78f, 1.0f, 0.96f, 0.16f - line * 0.012f), 1.65f, true);
		}
	}

	private void DrawCurrentRibbons()
	{
		float t = (float)_time;
		for (int ribbon = 0; ribbon < 8; ribbon++)
		{
			float yBase = Size.Y * (WaterlineRatio + 0.04f + ribbon * 0.07f);
			float drift = Mathf.PosMod(t * (42.0f + ribbon * 8.0f) + ribbon * Size.X * 0.18f, Size.X * 1.18f) - Size.X * 0.14f;
			for (int i = 0; i < _currentRibbonPoints.Length; i++)
			{
				float ratio = i / (float)(_currentRibbonPoints.Length - 1);
				float x = drift + ratio * Size.X * 0.46f;
				float y = yBase + ratio * Size.Y * 0.16f + Mathf.Sin(ratio * Mathf.Tau * 1.6f + t * 0.9f + ribbon) * 9.0f;
				_currentRibbonPoints[i] = new Vector2(x, y);
			}

			float alpha = 0.07f + Mathf.Sin(t * 0.55f + ribbon) * 0.024f;
			DrawPolyline(_currentRibbonPoints, new Color(0.62f, 1.0f, 0.94f, Mathf.Max(alpha, 0.025f)), 2.2f, true);
		}
	}

	private void DrawCausticRibbons()
	{
		float t = (float)_time;
		for (int ribbon = 0; ribbon < 7; ribbon++)
		{
			float y = Size.Y * (0.58f + ribbon * 0.045f);
			for (int i = 0; i < _causticRibbonPoints.Length; i++)
			{
				float ratio = i / (float)(_causticRibbonPoints.Length - 1);
				float x = Size.X * (0.35f + ratio * 0.63f);
				float wave = Mathf.Sin(ratio * Mathf.Tau * 2.4f + t * 0.55f + ribbon) * (8.0f + ribbon * 0.9f);
				_causticRibbonPoints[i] = new Vector2(x, y + wave);
			}
			DrawPolyline(_causticRibbonPoints, new Color(0.66f, 1.0f, 0.9f, 0.08f), 1.6f, true);
		}
	}

	private void DrawGlints()
	{
		float t = (float)_time;
		foreach (Glint glint in _glints)
		{
			float pulse = Mathf.Max(0.0f, Mathf.Sin(t * glint.Speed + glint.Phase));
			if (pulse <= 0.04f)
			{
				continue;
			}

			Vector2 position = new(
				Size.X * glint.XRatio + Mathf.Sin(t * 0.18f + glint.Phase) * 12.0f,
				Size.Y * glint.YRatio + Mathf.Cos(t * 0.22f + glint.Phase) * 8.0f);
			float radius = glint.Radius * (0.8f + pulse * 0.7f);
			Color color = new(0.82f, 1.0f, 0.9f, glint.Alpha * pulse);
			DrawLine(position - new Vector2(radius, 0.0f), position + new Vector2(radius, 0.0f), color, 1.0f, true);
			DrawLine(position - new Vector2(0.0f, radius), position + new Vector2(0.0f, radius), color, 1.0f, true);
			DrawCircle(position, radius * 0.22f, color);
		}
	}

	private sealed class ImageElement
	{
		public TextureRect Node { get; set; } = default!;
		public Texture2D[] Frames { get; set; } = Array.Empty<Texture2D>();
		public Vector2 BaseSize { get; set; }
		public float FramesPerSecond { get; set; }
		public float XRatio { get; set; }
		public float YRatio { get; set; }
		public float Scale { get; set; }
		public float Alpha { get; set; }
		public float Phase { get; set; }
		public float Speed { get; set; }
		public float Drift { get; set; }
		public float Secondary { get; set; }
		public float ElapsedTime { get; set; }
	}

	private readonly record struct Glint(float XRatio, float YRatio, float Radius, float Speed, float Phase, float Alpha);
}
