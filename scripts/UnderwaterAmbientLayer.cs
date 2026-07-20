using System;
using System.Collections.Generic;
using Godot;

public partial class UnderwaterAmbientLayer : Control
{
	public enum AmbientRenderMode
	{
		Procedural,
		ImageBased,
	}

	public enum AmbientVisualProfile
	{
		MainMenu,
		GameBoard,
		AdventureMap,
	}

	private const int SeagrassFrameCount = 8;
	private const int CenterCausticFrameCount = 8;
	private const int CenterGlintFrameCount = 6;
	private const int SunbeamFrameCount = 6;
	private const int WaterCurrentFrameCount = 8;
	private const int MoteFrameCount = 6;

	private const float SeagrassFramesPerSecond = 5.0f;
	private const float CenterCausticFramesPerSecond = 5.0f;
	private const float CenterGlintFramesPerSecond = 8.0f;
	private const float SunbeamFramesPerSecond = 2.0f;
	private const float WaterCurrentFramesPerSecond = 4.0f;
	private const float MoteFramesPerSecond = 6.0f;

	private static readonly Vector2 SeagrassFrameSize = new(256.0f, 512.0f);
	private static readonly Vector2 CenterCausticFrameSize = new(512.0f, 512.0f);
	private static readonly Vector2 CenterGlintFrameSize = new(96.0f, 96.0f);
	private static readonly Vector2 SunbeamFrameSize = new(384.0f, 720.0f);
	private static readonly Vector2 WaterCurrentFrameSize = new(512.0f, 128.0f);
	private static readonly Vector2 MoteFrameSize = new(96.0f, 96.0f);

	[Export]
	public AmbientRenderMode RenderMode { get; set; } = AmbientRenderMode.Procedural;

	[Export]
	public AmbientVisualProfile VisualProfile { get; set; } = AmbientVisualProfile.GameBoard;

	[Export(PropertyHint.Range, "0,140,1")]
	public int mote_count = 64;

	[Export(PropertyHint.Range, "0,80,1")]
	public int seagrass_blade_count = 44;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float motion_scale = 1.0f;

	[Export(PropertyHint.Range, "0.0,1.0,0.05")]
	public float edge_depth_alpha_scale = 1.0f;

	[Export(PropertyHint.Range, "1,60,1")]
	public int redraw_fps = 20;

	[Export(PropertyHint.Range, "1,60,1")]
	public int image_update_fps = 30;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float sunbeam_intensity = 0.0f;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float center_caustic_intensity = 0.0f;

	[Export(PropertyHint.Range, "0,80,1")]
	public int center_glint_count = 0;

	[Export]
	public bool debug_force_missing_image_assets;

	private readonly RandomNumberGenerator _rng = new();
	private readonly List<Mote> _motes = new();
	private readonly List<SeagrassBlade> _seagrassBlades = new();
	private readonly List<CenterGlint> _centerGlints = new();
	private readonly Vector2[] _sunbeamPoints = new Vector2[4];
	private readonly Vector2[] _waterCurrentTopPoints = new Vector2[3];
	private readonly Vector2[] _waterCurrentBottomPoints = new Vector2[3];
	private readonly Vector2[] _centerCausticPoints = new Vector2[9];
	private readonly Vector2[] _seagrassStemPoints = new Vector2[5];
	private readonly Vector2[] _seagrassCenterLinePoints = new Vector2[3];

	private readonly List<ImageElement> _seagrassInstances = new();
	private readonly List<ImageElement> _centerCausticInstances = new();
	private readonly List<ImageElement> _centerGlintInstances = new();
	private readonly List<ImageElement> _sunbeamInstances = new();
	private readonly List<ImageElement> _waterCurrentInstances = new();
	private readonly List<ImageElement> _moteInstances = new();
	private readonly Dictionary<string, bool> _missingCategoryWarnings = new();

	private double _time;
	private double _redrawAccumulator;
	private VisualUpdateThrottle _imageUpdateThrottle;
	private DynamicImageAssetState _imageAssetState;
	private bool _pendingImageRebuild;
	private int _resourceLoadCount;
	private int _rebuildCount;
	private AmbientRenderMode _activeRenderMode = (AmbientRenderMode)(-1);
	private AmbientVisualProfile _activeVisualProfile = (AmbientVisualProfile)(-1);

	private Texture2D[] _seagrassFrames = Array.Empty<Texture2D>();
	private Texture2D[] _centerCausticFrames = Array.Empty<Texture2D>();
	private Texture2D[] _centerGlintFrames = Array.Empty<Texture2D>();
	private Texture2D[] _sunbeamFrames = Array.Empty<Texture2D>();
	private Texture2D[] _waterCurrentFrames = Array.Empty<Texture2D>();
	private Texture2D[] _moteFrames = Array.Empty<Texture2D>();
	private Control? _seagrassLayer;
	private Control? _centerCausticLayer;
	private Control? _centerGlintLayer;
	private Control? _sunbeamLayer;
	private Control? _waterCurrentLayer;
	private Control? _moteLayer;
	public override void _Ready()
	{
		VisibilityChanged += HandleVisibilityChanged;
		MouseFilter = MouseFilterEnum.Ignore;
		_rng.Seed = 4107;
		ApplyConfiguration(force: true);
		RebuildAmbient();
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
			RebuildAmbient();
		}
	}

	public override void _Process(double delta)
	{
		if (RenderMode != _activeRenderMode || VisualProfile != _activeVisualProfile)
		{
			ApplyConfiguration(force: true);
			RebuildAmbient();
		}

		if (_pendingImageRebuild && Size.X > 0.0f && Size.Y > 0.0f)
		{
			RebuildAmbient();
		}

		if (!IsVisibleInTree())
		{
			return;
		}

		_time += delta * motion_scale;
		if (_activeRenderMode == AmbientRenderMode.ImageBased)
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
				return;
			}

			_redrawAccumulator = 0.0;
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

		DrawEdgeDepth();
		if (ShouldDrawProceduralSunbeams())
		{
			DrawSunbeams();
		}
		if (ShouldDrawProceduralSeagrass())
		{
			DrawSeagrass();
		}

		if (ShouldDrawProceduralWaterCurrents())
		{
			DrawWaterCurrents();
		}
		if (ShouldDrawProceduralCenterCaustics())
		{
			DrawCenterCaustics();
		}

		if (ShouldDrawProceduralMotes())
		{
			DrawMotes();
		}
		if (ShouldDrawProceduralCenterGlints())
		{
			DrawCenterGlints();
		}

	}

	private void ApplyConfiguration(bool force = false)
	{
		if (!force
			&& RenderMode == _activeRenderMode
			&& VisualProfile == _activeVisualProfile)
		{
			return;
		}
		if (VisualProfile != _activeVisualProfile)
		{
			_imageAssetState = DynamicImageAssetState.Uninitialized;
			_seagrassFrames = Array.Empty<Texture2D>();
			_centerCausticFrames = Array.Empty<Texture2D>();
			_centerGlintFrames = Array.Empty<Texture2D>();
			_sunbeamFrames = Array.Empty<Texture2D>();
			_waterCurrentFrames = Array.Empty<Texture2D>();
			_moteFrames = Array.Empty<Texture2D>();
			ClearImageInstances();
		}

		_activeRenderMode = RenderMode;
		_activeVisualProfile = VisualProfile;
		_redrawAccumulator = 0.0;
		if (_activeRenderMode == AmbientRenderMode.ImageBased)
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
		if (RenderMode != _activeRenderMode || VisualProfile != _activeVisualProfile)
		{
			ApplyConfiguration(force: true);
		}

		if (Size.X > 0.0f
			&& Size.Y > 0.0f
			&& (_pendingImageRebuild
				|| (_activeRenderMode == AmbientRenderMode.ImageBased
					&& _imageAssetState != DynamicImageAssetState.PermanentFallback
					&& HasNoImageInstances())))
			{
				RebuildAmbient();
			}
		}

	private void RebuildAmbient()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			_pendingImageRebuild = true;
			return;
		}

		_pendingImageRebuild = false;
		_rebuildCount++;
		_motes.Clear();
		_seagrassBlades.Clear();
		_centerGlints.Clear();

		for (int i = 0; i < mote_count; i++)
		{
			int side = _rng.RandiRange(0, 3);
			_motes.Add(new Mote(
				EdgePosition(side, 0.28f),
				_rng.RandfRange(0.8f, 2.4f),
				_rng.RandfRange(0.08f, 0.28f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(10.0f, 38.0f),
				_rng.RandfRange(0.1f, 0.28f)));
		}

		for (int i = 0; i < seagrass_blade_count; i++)
		{
			int side = i % 2 == 0 ? 2 : 3;
			Vector2 basePosition = EdgePosition(side, 0.12f);
			bool fromBottom = _rng.Randf() < 0.64f;
			basePosition.Y = fromBottom
				? _rng.RandfRange(Size.Y * 0.58f, Size.Y * 0.98f)
				: _rng.RandfRange(Size.Y * 0.04f, Size.Y * 0.36f);

			_seagrassBlades.Add(new SeagrassBlade(
				basePosition,
				_rng.RandfRange(Size.Y * 0.09f, Size.Y * 0.23f),
				_rng.RandfRange(3.0f, 8.0f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(0.45f, 0.95f),
				_rng.RandfRange(10.0f, 28.0f),
				new Color(
					_rng.RandfRange(0.10f, 0.20f),
					_rng.RandfRange(0.58f, 0.88f),
					_rng.RandfRange(0.58f, 0.82f),
					_rng.RandfRange(0.15f, 0.31f)),
				side == 3 ? -1.0f : 1.0f,
				!fromBottom));
		}

		for (int i = 0; i < center_glint_count; i++)
		{
			_centerGlints.Add(new CenterGlint(
				_rng.RandfRange(0.30f, 0.72f),
				_rng.RandfRange(0.25f, 0.66f),
				_rng.RandfRange(1.8f, 4.6f),
				_rng.RandfRange(0.55f, 1.45f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(0.10f, 0.28f)));
		}

		if (_activeRenderMode == AmbientRenderMode.ImageBased)
			{
				EnsureImageAssetsLoaded(retryIfAllMissing: true);
				EnsureImageLayers();
				RebuildImageInstances();
				UpdateImageInstances();
				_imageUpdateThrottle.Reset();
			}

		QueueRedraw();
	}

	private Vector2 EdgePosition(int side, float bandRatio)
	{
		float bandX = Size.X * bandRatio;
		float bandY = Size.Y * bandRatio;

		return side switch
		{
			0 => new Vector2(_rng.RandfRange(0.0f, Size.X), _rng.RandfRange(0.0f, bandY)),
			1 => new Vector2(_rng.RandfRange(0.0f, Size.X), _rng.RandfRange(Size.Y - bandY, Size.Y)),
			2 => new Vector2(_rng.RandfRange(0.0f, bandX), _rng.RandfRange(0.0f, Size.Y)),
			_ => new Vector2(_rng.RandfRange(Size.X - bandX, Size.X), _rng.RandfRange(0.0f, Size.Y)),
		};
	}

	private readonly record struct Mote(Vector2 Base, float Radius, float Speed, float Phase, float Drift, float Alpha);
	private readonly record struct SeagrassBlade(Vector2 Base, float Height, float Width, float Phase, float Speed, float Sway, Color Color, float Flip, bool GrowsDown);
	private readonly record struct CenterGlint(float XRatio, float YRatio, float Radius, float Speed, float Phase, float Alpha);
}
