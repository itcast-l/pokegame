using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public partial class MainMenuOceanDynamics : Control
{
	public enum OceanRenderMode
	{
		Procedural,
		ImageBased,
	}

	private const float WaterlineRatio = 0.34f;
	private const float BubbleSurfaceFadeDistanceRatio = 0.075f;
	private const float BubbleTextureTopOffsetFactor = 1.55f;

	private const int SunbeamInstanceCount = 5;
	private const int SurfaceWakeInstanceCount = 6;
	private const int CurrentRibbonInstanceCount = 8;
	private const int CausticRibbonInstanceCount = 7;
	private const int BubbleInstanceCount = 30;

	private const string SunbeamFramePattern = "res://art/fx/environment/main_menu_sunbeam/run-001/processed/fx-{0}.png";
	private const string SurfaceWakeFramePattern = "res://art/fx/environment/main_menu_surface_wake/run-001/processed/fx-{0}.png";
	private const string CurrentRibbonFramePattern = "res://art/fx/environment/main_menu_current_ribbon/run-001/processed/fx-{0}.png";
	private const string CausticRibbonFramePattern = "res://art/fx/environment/main_menu_caustic_ribbon/run-001/processed/fx-{0}.png";
	private const string GlintFramePattern = "res://art/fx/environment/main_menu_glint/run-001/processed/fx-{0}.png";
	private const string BubbleTexturePath = "res://art/fx/environment/main_menu_bubble/run-001/processed/fx-1.png";

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

	[Export(PropertyHint.Range, "0,60,1")]
	public int bubble_count = BubbleInstanceCount;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float motion_scale = 1.0f;

	[Export(PropertyHint.Range, "1,60,1")]
	public int redraw_fps = 20;

	[Export(PropertyHint.Range, "1,60,1")]
	public int image_update_fps = 30;

#if DECKLAND_TEST_HOOKS
	[Export]
	public bool debug_force_missing_image_assets;
#endif

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
	private readonly List<BubbleImageElement> _bubbleImageInstances = new();
	private readonly List<Bubble> _bubbles = new();

	private double _time;
	private double _redrawAccumulator;
	private VisualUpdateThrottle _imageUpdateThrottle;
	private DynamicImageAssetState _imageAssetState;
	private bool _pendingImageRebuild;
	private int _resourceLoadCount;
	private int _rebuildCount;
	private OceanRenderMode _activeRenderMode = (OceanRenderMode)(-1);

	private Texture2D[] _sunbeamFrames = Array.Empty<Texture2D>();
	private Texture2D[] _surfaceWakeFrames = Array.Empty<Texture2D>();
	private Texture2D[] _currentRibbonFrames = Array.Empty<Texture2D>();
	private Texture2D[] _causticRibbonFrames = Array.Empty<Texture2D>();
	private Texture2D[] _glintFrames = Array.Empty<Texture2D>();
	private Texture2D? _bubbleTexture;
	private bool _bubbleTextureLoadAttempted;

	private Control? _sunbeamLayer;
	private Control? _surfaceWakeLayer;
	private Control? _currentRibbonLayer;
	private Control? _causticRibbonLayer;
	private Control? _glintLayer;
	private Control? _bubbleLayer;

	public override void _Ready()
	{
		VisibilityChanged += HandleVisibilityChanged;
		MouseFilter = MouseFilterEnum.Ignore;
		_rng.Seed = 80110;
		ApplyRenderMode(force: true);
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
			if (_imageAssetState != DynamicImageAssetState.PermanentFallback && HasNoImageInstances())
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
			if (!ShouldQueueRedrawInImageMode())
			{
				_redrawAccumulator = 0.0;
				return;
			}

			AdvanceRedrawAccumulator(delta);
			return;
		}

		AdvanceRedrawAccumulator(delta);
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
			ApplyRenderMode(force: true);
		}

		if (Size.X > 0.0f
			&& Size.Y > 0.0f
			&& (_pendingImageRebuild
				|| (_activeRenderMode == OceanRenderMode.ImageBased
					&& _imageAssetState != DynamicImageAssetState.PermanentFallback
					&& HasNoImageInstances())))
		{
			Rebuild();
		}
	}

	private void AdvanceRedrawAccumulator(double delta)
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

	public override void _Draw()
	{
		if (Size.X <= 0.0f
			|| Size.Y <= 0.0f)
		{
			return;
		}

		if (ShouldDrawProceduralSunbeams())
		{
			DrawSunbeams();
		}
		if (ShouldDrawProceduralSurfaceWake())
		{
			DrawSurfaceWake();
		}
		if (ShouldDrawProceduralCurrentRibbons())
		{
			DrawCurrentRibbons();
		}
		if (ShouldDrawProceduralCausticRibbons())
		{
			DrawCausticRibbons();
		}
		if (ShouldDrawProceduralGlints())
		{
			DrawGlints();
		}

		if (ShouldDrawBubbles())
		{
			DrawBubbles();
		}
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
		_rebuildCount++;
		RebuildParticleData();
		if (_activeRenderMode == OceanRenderMode.ImageBased)
		{
			EnsureImageAssetsLoaded(retryIfAllMissing: true);
			EnsureImageLayers();
				RebuildImageInstances();
				UpdateImageInstances();
				_imageUpdateThrottle.Reset();
				QueueRedraw();
			return;
		}

		QueueRedraw();
	}

	private void RebuildParticleData()
	{
		_glints.Clear();
		_bubbles.Clear();

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

		BuildBubbleInstances();
	}

	private void BuildBubbleInstances()
	{
		if (bubble_count <= 0)
		{
			return;
		}

		for (int index = 0; index < bubble_count; index++)
		{
			int lane = index % 4;
			float xRatio = lane switch
			{
				0 => _rng.RandfRange(0.36f, 0.49f),
				1 => _rng.RandfRange(0.62f, 0.76f),
				2 => _rng.RandfRange(0.84f, 1.02f),
				_ => _rng.RandfRange(0.90f, 1.04f),
			};

			float yRatio = lane switch
			{
				0 => _rng.RandfRange(0.48f, 0.92f),
				1 => _rng.RandfRange(0.42f, 0.86f),
				2 => _rng.RandfRange(WaterlineRatio + 0.08f, 0.82f),
				_ => _rng.RandfRange(WaterlineRatio + 0.08f, 0.72f),
			};

			_bubbles.Add(new Bubble(
				xRatio,
				yRatio,
				_rng.RandfRange(6.0f, 20.0f),
				_rng.RandfRange(110.0f, 260.0f),
				_rng.RandfRange(4.6f, 8.8f),
				_rng.RandfRange(4.0f, 14.0f),
				_rng.RandfRange(0.18f, 0.48f),
				_rng.Randf(),
				_rng.RandfRange(0.20f, 0.46f)));
		}
	}

	private bool TryGetBubbleLayout(Bubble bubble, out Vector2 position, out float radius, out float alpha)
	{
		float t = (float)_time;
		float viewportScale = Mathf.Clamp(Mathf.Min(Size.X / 1672.0f, Size.Y / 941.0f), 0.78f, 1.28f);
		MainMenuBubbleMotionState motion = MainMenuBubbleMotionRules.Evaluate(
			t,
			bubble.Phase,
			bubble.Duration,
			bubble.RiseDistance * viewportScale);

		alpha = bubble.Alpha * motion.AlphaMultiplier;
		radius = bubble.Radius * motion.SizeMultiplier * viewportScale;
		Vector2 basePosition = new(Size.X * bubble.XRatio, Size.Y * bubble.YRatio);
		position = basePosition + new Vector2(
			Mathf.Sin(t * bubble.SwaySpeed + bubble.Phase * Mathf.Tau) * bubble.Sway * viewportScale,
			motion.RiseOffsetY);
		float surfaceAlpha = MainMenuBubbleMotionRules.GetSurfaceAlphaMultiplier(
			position.Y,
			radius * BubbleTextureTopOffsetFactor,
			Size.Y * WaterlineRatio,
			Size.Y * BubbleSurfaceFadeDistanceRatio);
		alpha *= surfaceAlpha;
		return alpha > 0.01f;
	}

	private readonly record struct Bubble(
		float XRatio,
		float YRatio,
		float Radius,
		float RiseDistance,
		float Duration,
		float Sway,
		float SwaySpeed,
		float Phase,
		float Alpha);

	private readonly record struct Glint(float XRatio, float YRatio, float Radius, float Speed, float Phase, float Alpha);
}
