using System.Collections.Generic;
using Godot;

public partial class AdventureMapDynamicLayer : Control
{
	public enum DynamicRenderMode
	{
		Procedural,
		ImageBased,
	}

	private const int CurrentRibbonFrameCount = 8;
	private const int SparkleFrameCount = 6;
	private const float CurrentRibbonFramesPerSecond = 4.0f;
	private const float SparkleFramesPerSecond = 8.0f;
	private static readonly Vector2 CurrentRibbonFrameSize = new(512.0f, 128.0f);
	private static readonly Vector2 SparkleFrameSize = new(96.0f, 96.0f);

	[Export]
	public DynamicRenderMode RenderMode { get; set; } = DynamicRenderMode.Procedural;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float MotionScale { get; set; } = 1.0f;

	[Export(PropertyHint.Range, "1,60,1")]
	public int redraw_fps = 20;

	[Export(PropertyHint.Range, "1,60,1")]
	public int image_update_fps = 30;

	private readonly RandomNumberGenerator _rng = new();
	private readonly List<CurrentRibbon> _ribbons = new();
	private readonly List<Sparkle> _sparkles = new();
	private readonly List<ImageElement> _currentRibbonInstances = new();
	private readonly List<ImageElement> _sparkleInstances = new();
	private readonly Vector2[] _currentRibbonPoints = new Vector2[36];
	private double _time;
	private double _redrawAccumulator;
	private VisualUpdateThrottle _imageUpdateThrottle;
	private bool _imageAssetsLoaded;
	private bool _pendingImageRebuild;
	private DynamicRenderMode _activeRenderMode = (DynamicRenderMode)(-1);
	private Texture2D[] _currentRibbonFrames = System.Array.Empty<Texture2D>();
	private Texture2D[] _sparkleFrames = System.Array.Empty<Texture2D>();
	private Control? _currentRibbonLayer;
	private Control? _sparkleLayer;

	public override void _Ready()
	{
		VisibilityChanged += HandleVisibilityChanged;
		MouseFilter = MouseFilterEnum.Ignore;
		_rng.Seed = 23581;
		ApplyConfiguration(force: true);
		Rebuild();
		SetProcess(IsVisibleInTree());
	}

	public override void _ExitTree()
	{
		Callable callback = Callable.From(HandleVisibilityChanged);
		if (IsConnected(CanvasItem.SignalName.VisibilityChanged, callback))
		{
			VisibilityChanged -= HandleVisibilityChanged;
		}
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
			ApplyConfiguration(force: true);
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

		_time += delta * MotionScale;
			if (_activeRenderMode == DynamicRenderMode.ImageBased)
			{
				if (HasNoImageInstances())
				{
					EnsureImageAssetsLoaded(retryIfAllMissing: true);
					RebuildImageInstances();
					UpdateImageInstances();
					_imageUpdateThrottle.Reset();
				}

				if (_imageUpdateThrottle.Advance(delta, image_update_fps))
				{
					UpdateImageInstances();
				}
			if (ShouldQueueRedrawForImageFallback())
			{
				_redrawAccumulator += delta;
				double redrawInterval = 1.0 / Mathf.Max(redraw_fps, 1);
				if (_redrawAccumulator < redrawInterval)
				{
					return;
				}

				_redrawAccumulator %= redrawInterval;
				QueueRedraw();
			}
			return;
		}

		_redrawAccumulator += delta;
		double interval = 1.0 / Mathf.Max(redraw_fps, 1);
		if (_redrawAccumulator < interval)
		{
			return;
		}

		_redrawAccumulator %= interval;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		if (ShouldDrawProceduralCurrentRibbons())
		{
			DrawCurrentRibbons();
		}

		if (ShouldDrawProceduralSparkles())
		{
			DrawSparkles();
		}
	}

	private void ApplyConfiguration(bool force = false)
	{
		if (!force && RenderMode == _activeRenderMode)
		{
			return;
		}

		_activeRenderMode = RenderMode;
		_redrawAccumulator = 0.0;
		if (_activeRenderMode == DynamicRenderMode.ImageBased)
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

		private void HandleVisibilityChanged()
	{
		bool isVisible = IsVisibleInTree();
		SetProcess(isVisible);
		if (!isVisible)
		{
			return;
		}

		_imageUpdateThrottle.ForceNextUpdate();
		if (RenderMode != _activeRenderMode)
		{
			ApplyConfiguration(force: true);
		}

		if (Size.X > 0.0f
			&& Size.Y > 0.0f
			&& (_pendingImageRebuild || (_activeRenderMode == DynamicRenderMode.ImageBased && HasNoImageInstances())))
			{
				Rebuild();
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
		_ribbons.Clear();
		_sparkles.Clear();

		for (int i = 0; i < 9; i++)
		{
			_ribbons.Add(new CurrentRibbon(
				_rng.RandfRange(0.12f, 0.88f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(0.18f, 0.46f),
				_rng.RandfRange(18.0f, 54.0f),
				_rng.RandfRange(0.035f, 0.09f)));
		}

		for (int i = 0; i < 44; i++)
		{
			_sparkles.Add(new Sparkle(
				new Vector2(_rng.RandfRange(0.06f, 0.94f), _rng.RandfRange(0.08f, 0.92f)),
				_rng.RandfRange(1.2f, 3.8f),
				_rng.RandfRange(0.35f, 1.15f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(0.08f, 0.24f)));
		}

			if (_activeRenderMode == DynamicRenderMode.ImageBased)
			{
				EnsureImageAssetsLoaded(retryIfAllMissing: true);
				EnsureImageLayers();
				RebuildImageInstances();
				UpdateImageInstances();
				_imageUpdateThrottle.Reset();
			if (ShouldQueueRedrawForImageFallback())
			{
				QueueRedraw();
			}
		}
	}

	private void EnsureImageAssetsLoaded(bool retryIfAllMissing = false)
	{
		if (_imageAssetsLoaded && !(retryIfAllMissing && AllImageFramesMissing()))
		{
			return;
		}

		_currentRibbonFrames = FrameAnimationHelpers.LoadTextureFrames(
			"res://art/fx/environment/adventure_map_dynamic/current_ribbon/run-001/processed/fx-{0}.png",
			CurrentRibbonFrameCount,
			"adventure map dynamic current ribbon",
			preferImageFile: true);
		_sparkleFrames = FrameAnimationHelpers.LoadTextureFrames(
			"res://art/fx/environment/adventure_map_dynamic/sparkle/run-001/processed/fx-{0}.png",
			SparkleFrameCount,
			"adventure map dynamic sparkle",
			preferImageFile: true);
		_imageAssetsLoaded = true;
	}

	private bool AllImageFramesMissing()
	{
		return _currentRibbonFrames.Length == 0 && _sparkleFrames.Length == 0;
	}

	private bool HasNoImageInstances()
	{
		return _currentRibbonInstances.Count == 0 && _sparkleInstances.Count == 0;
	}

	private bool ShouldQueueRedrawForImageFallback()
	{
		return _currentRibbonFrames.Length == 0 || _sparkleFrames.Length == 0;
	}

	private void EnsureImageLayers()
	{
		_currentRibbonLayer = GetOrCreateLayer(ref _currentRibbonLayer, "CurrentRibbonLayer");
		_sparkleLayer = GetOrCreateLayer(ref _sparkleLayer, "SparkleLayer");
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
			GrowHorizontal = GrowDirection.Both,
			GrowVertical = GrowDirection.Both,
		};
		layer.SetAnchorsPreset(LayoutPreset.FullRect);
		layer.OffsetRight = 0.0f;
		layer.OffsetBottom = 0.0f;
		AddChild(layer);
		return layer;
	}

	private void SetImageLayersVisible(bool visible)
	{
		SetLayerVisible(_currentRibbonLayer, visible);
		SetLayerVisible(_sparkleLayer, visible);
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
		BuildCurrentRibbonInstances();
		BuildSparkleInstances();
	}

	private void ClearImageInstances()
	{
		ClearImageLayer(_currentRibbonLayer, _currentRibbonInstances);
		ClearImageLayer(_sparkleLayer, _sparkleInstances);
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

	private void BuildCurrentRibbonInstances()
	{
		if (_currentRibbonLayer == null || _currentRibbonFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < _ribbons.Count; index++)
		{
			CurrentRibbon ribbon = _ribbons[index];
			var instance = CreateImageElement(
				_currentRibbonLayer,
				$"CurrentRibbon{index + 1}",
				_currentRibbonFrames,
				CurrentRibbonFrameSize,
				CurrentRibbonFramesPerSecond);
			instance.XRatio = -0.16f + index * 0.12f;
			instance.YRatio = ribbon.YRatio;
			instance.Scale = 0.18f;
			instance.Alpha = ribbon.Alpha;
			instance.Phase = ribbon.Phase;
			instance.Speed = ribbon.Speed * 0.06f;
			instance.Drift = ribbon.Amplitude;
			_currentRibbonInstances.Add(instance);
		}
	}

	private void BuildSparkleInstances()
	{
		if (_sparkleLayer == null || _sparkleFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < _sparkles.Count; index++)
		{
			Sparkle sparkle = _sparkles[index];
			var instance = CreateImageElement(
				_sparkleLayer,
				$"Sparkle{index + 1}",
				_sparkleFrames,
				SparkleFrameSize,
				SparkleFramesPerSecond);
			instance.XRatio = sparkle.PositionRatio.X;
			instance.YRatio = sparkle.PositionRatio.Y;
			instance.Scale = sparkle.Radius / 10.0f;
			instance.Alpha = sparkle.Alpha;
			instance.Phase = sparkle.Phase;
			instance.Speed = sparkle.Speed;
			_sparkleInstances.Add(instance);
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
			TextureFilter = TextureFilterEnum.Linear,
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
		UpdateCurrentRibbonInstances();
		UpdateSparkleInstances();
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
			float xRatio = Mathf.PosMod(instance.XRatio + time * instance.Speed, 1.24f) - 0.16f;
			float centerX = Size.X * xRatio;
			float centerY = Size.Y * instance.YRatio + Mathf.Sin(time * 0.50f + instance.Phase) * instance.Drift;
			float alpha = instance.Alpha * (0.64f + 0.16f * Mathf.Sin(time * 0.36f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateSparkleInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _sparkleInstances)
		{
			instance.ElapsedTime = time;
			float pulse = Mathf.Max(0.0f, Mathf.Sin(time * instance.Speed + instance.Phase));
				if (pulse <= 0.03f)
				{
					SetImageVisible(instance, false);
				continue;
			}

			ApplyAnimatedFrame(instance);
			Vector2 size = instance.BaseSize * instance.Scale * (0.7f + pulse * 0.8f);
			float centerX = Size.X * instance.XRatio + Mathf.Sin(time * 0.26f + instance.Phase) * 10.0f;
			float centerY = Size.Y * instance.YRatio + Mathf.Cos(time * 0.22f + instance.Phase) * 7.0f;
			float alpha = instance.Alpha * pulse;
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private static void ApplyAnimatedFrame(ImageElement instance)
	{
			if (instance.Frames.Length == 0)
			{
				if (instance.Node.Texture != null)
				{
					instance.Node.Texture = null;
				}
				instance.LastFrameIndex = -1;
				return;
		}

		float frameTime = instance.ElapsedTime * instance.FramesPerSecond
			+ instance.Phase * instance.Frames.Length / Mathf.Tau;
		int frameIndex = WrapFrameIndex(Mathf.FloorToInt(frameTime), instance.Frames.Length);
		Texture2D texture = instance.Frames[frameIndex];
			if (instance.LastFrameIndex != frameIndex || instance.Node.Texture != texture)
			{
				instance.Node.Texture = texture;
				instance.LastFrameIndex = frameIndex;
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
			if (size.X <= 0.0f || size.Y <= 0.0f || alpha <= 0.001f)
			{
				SetImageVisible(instance, false);
				return;
			}

			Vector2 position = center - size / 2.0f;
			Color modulate = new(1.0f, 1.0f, 1.0f, Mathf.Clamp(alpha, 0.0f, 1.0f));
			SetImageVisible(instance, true);
			if (!instance.HasAppliedLayout || instance.LastSize.DistanceTo(size) >= 0.01f)
			{
				instance.Node.Size = size;
				instance.Node.PivotOffset = size / 2.0f;
				instance.LastSize = size;
			}
			if (!instance.HasAppliedLayout || instance.LastPosition.DistanceTo(position) >= 0.01f)
			{
				instance.Node.Position = position;
				instance.LastPosition = position;
			}
			if (!instance.HasAppliedLayout || Mathf.Abs(instance.LastModulate.A - modulate.A) >= 0.001f)
			{
				instance.Node.Modulate = modulate;
				instance.LastModulate = modulate;
			}
			instance.HasAppliedLayout = true;
		}

		private static void SetImageVisible(ImageElement instance, bool visible)
		{
			if (!instance.HasAppliedLayout || instance.LastVisible != visible)
			{
				instance.Node.Visible = visible;
				instance.LastVisible = visible;
			}
		}

	private void DrawCurrentRibbons()
	{
		float t = (float)_time;
		foreach (CurrentRibbon ribbon in _ribbons)
		{
			float drift = Mathf.PosMod(t * ribbon.Speed + ribbon.Phase * 80.0f, Size.X * 1.22f) - Size.X * 0.16f;
			for (int i = 0; i < _currentRibbonPoints.Length; i++)
			{
				float ratio = i / (float)(_currentRibbonPoints.Length - 1);
				float x = drift + ratio * Size.X * 0.48f;
				float wave = Mathf.Sin(ratio * Mathf.Tau * 1.6f + t * 0.62f + ribbon.Phase) * ribbon.Amplitude;
				float y = Size.Y * ribbon.YRatio + wave;
				_currentRibbonPoints[i] = new Vector2(x, y);
			}

			DrawPolyline(_currentRibbonPoints, new Color(0.62f, 1.0f, 0.94f, ribbon.Alpha), 2.0f, true);
		}
	}

	private void DrawSparkles()
	{
		float t = (float)_time;
		foreach (Sparkle sparkle in _sparkles)
		{
			float pulse = Mathf.Max(0.0f, Mathf.Sin(t * sparkle.Speed + sparkle.Phase));
			if (pulse <= 0.03f)
			{
				continue;
			}

			Vector2 position = new(
				Size.X * sparkle.PositionRatio.X + Mathf.Sin(t * 0.26f + sparkle.Phase) * 10.0f,
				Size.Y * sparkle.PositionRatio.Y + Mathf.Cos(t * 0.22f + sparkle.Phase) * 7.0f);
			float radius = sparkle.Radius * (0.65f + pulse * 0.75f);
			Color color = new(0.82f, 1.0f, 0.92f, sparkle.Alpha * pulse);
			DrawLine(position - new Vector2(radius, 0.0f), position + new Vector2(radius, 0.0f), color, 1.0f, true);
			DrawLine(position - new Vector2(0.0f, radius), position + new Vector2(0.0f, radius), color, 1.0f, true);
			DrawCircle(position, radius * 0.18f, color);
		}
	}

	private bool ShouldDrawProceduralCurrentRibbons()
	{
		return _activeRenderMode != DynamicRenderMode.ImageBased || _currentRibbonFrames.Length == 0;
	}

	private bool ShouldDrawProceduralSparkles()
	{
		return _activeRenderMode != DynamicRenderMode.ImageBased || _sparkleFrames.Length == 0;
	}

	private sealed class ImageElement
	{
		public TextureRect Node { get; set; } = default!;
		public Texture2D[] Frames { get; set; } = System.Array.Empty<Texture2D>();
		public Vector2 BaseSize { get; set; }
		public float FramesPerSecond { get; set; }
		public float XRatio { get; set; }
		public float YRatio { get; set; }
		public float Scale { get; set; }
		public float Alpha { get; set; }
		public float Phase { get; set; }
		public float Speed { get; set; }
			public float Drift { get; set; }
			public float ElapsedTime { get; set; }
			public int LastFrameIndex { get; set; } = -1;
			public bool LastVisible { get; set; }
			public Vector2 LastPosition { get; set; }
			public Vector2 LastSize { get; set; }
			public Color LastModulate { get; set; }
			public bool HasAppliedLayout { get; set; }
		}

	private readonly record struct CurrentRibbon(float YRatio, float Phase, float Speed, float Amplitude, float Alpha);
	private readonly record struct Sparkle(Vector2 PositionRatio, float Radius, float Speed, float Phase, float Alpha);
}
