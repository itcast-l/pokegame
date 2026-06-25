using System;
using Godot;

[Tool]
public partial class ScoreAvatarMeter : Control
{
	public static readonly Vector2 DefaultSize = new(92.0f, 92.0f);

	private const float FullCircleRadians = 6.2831855f;
	private const float FillAnimationSpeed = 150.0f;
	private const float PulseFadeSpeed = 2.8f;
	private const string BaseTexturePath = "res://art/ui/score_meter/run-001/processed/base.png";
	private const string FillTexturePath = "res://art/ui/score_meter/run-001/processed/fill.png";
	private const string OverTexturePath = "res://art/ui/score_meter/run-001/processed/over.png";
	private const string PulseTexturePath = "res://art/ui/score_meter/run-001/processed/pulse.png";

	private readonly Color _backgroundColor = new(0.0f, 0.08f, 0.12f, 0.08f);
	private readonly Color _rimColor = new(0.92f, 0.98f, 1.0f, 0.24f);
	private Color _fillColor = new(0.58f, 0.93f, 1.0f, 0.46f);
	private Color _glowColor = new(0.86f, 0.99f, 1.0f, 0.36f);
	private float _circleDiameter = 76.0f;
	private float _displayScore;
	private float _targetScore;
	private float _maxScore = 100.0f;
	private float _pulseAmount;
	private bool _texturedAssetsLoaded;
	private bool _texturedAssetsMissing;
	private Texture2D? _baseTexture;
	private Texture2D? _fillTexture;
	private Texture2D? _overTexture;
	private Texture2D? _pulseTexture;
	private TextureRect? _baseTextureRect;
	private Control? _fillClipRoot;
	private TextureRect? _fillTextureRect;
	private TextureRect? _overTextureRect;
	private TextureRect? _pulseTextureRect;
	private Texture2D? _baseTextureOverride;
	private Texture2D? _fillTextureOverride;
	private Texture2D? _overTextureOverride;
	private Texture2D? _pulseTextureOverride;

	[Export(PropertyHint.Range, "40,120,1")]
	public float circle_diameter
	{
		get => _circleDiameter;
		set
		{
			_circleDiameter = Mathf.Clamp(value, 24.0f, 200.0f);
			if (IsInsideTree())
			{
				ApplyLayout();
				QueueRedraw();
			}
		}
	}

	private bool _useTexturedMeter = true;

	[Export]
	public bool UseTexturedMeter
	{
		get => _useTexturedMeter;
		set
		{
			_useTexturedMeter = value;
			if (IsNodeReady())
			{
				ApplyVisualMode();
				QueueRedraw();
			}
		}
	}

	[Export]
	public Texture2D? BaseTextureOverride
	{
		get => _baseTextureOverride;
		set
		{
			if (_baseTextureOverride == value)
			{
				return;
			}

			_baseTextureOverride = value;
			ResetTexturedAssets();
		}
	}

	[Export]
	public Texture2D? FillTextureOverride
	{
		get => _fillTextureOverride;
		set
		{
			if (_fillTextureOverride == value)
			{
				return;
			}

			_fillTextureOverride = value;
			ResetTexturedAssets();
		}
	}

	[Export]
	public Texture2D? OverTextureOverride
	{
		get => _overTextureOverride;
		set
		{
			if (_overTextureOverride == value)
			{
				return;
			}

			_overTextureOverride = value;
			ResetTexturedAssets();
		}
	}

	[Export]
	public Texture2D? PulseTextureOverride
	{
		get => _pulseTextureOverride;
		set
		{
			if (_pulseTextureOverride == value)
			{
				return;
			}

			_pulseTextureOverride = value;
			ResetTexturedAssets();
		}
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		if (Size == Vector2.Zero)
		{
			Size = DefaultSize;
		}

		PivotOffset = Size / 2.0f;
		EnsureTexturedNodes();
		EnsureTexturedAssetsLoaded();
		ApplyVisualMode();
		SetProcess(false);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			PivotOffset = Size / 2.0f;
			ApplyLayout();
			QueueRedraw();
		}
	}

	public override void _Process(double delta)
	{
		float previousScore = _displayScore;
		float previousPulse = _pulseAmount;
		_displayScore = Mathf.MoveToward(_displayScore, _targetScore, (float)delta * FillAnimationSpeed);
		_pulseAmount = Mathf.MoveToward(_pulseAmount, 0.0f, (float)delta * PulseFadeSpeed);

		if (!Mathf.IsEqualApprox(previousScore, _displayScore) || !Mathf.IsEqualApprox(previousPulse, _pulseAmount))
		{
			UpdateTexturedMeter();
			QueueRedraw();
		}

		if (Mathf.IsEqualApprox(_displayScore, _targetScore) && Mathf.IsZeroApprox(_pulseAmount))
		{
			SetProcess(false);
		}
	}

	public override void _Draw()
	{
		if (ShouldUseProceduralMeter())
		{
			DrawProceduralMeter();
		}
	}

	public void Configure(Color fillColor, Color glowColor)
	{
		_fillColor = fillColor;
		_glowColor = glowColor;
		UpdateTexturedMeter();
		QueueRedraw();
	}

	public void SetScore(int score, int maxScore, bool animate)
	{
		_maxScore = Math.Max(1, maxScore);
		float nextTarget = Mathf.Clamp((float)score, 0.0f, _maxScore);
		bool gainedScore = nextTarget > _targetScore;
		_targetScore = nextTarget;

		if (!animate)
		{
			_displayScore = _targetScore;
			_pulseAmount = 0.0f;
			SetProcess(false);
			UpdateTexturedMeter();
			QueueRedraw();
			return;
		}

		if (gainedScore)
		{
			_pulseAmount = 1.0f;
		}

		UpdateTexturedMeter();
		SetProcess(true);
	}

	private bool ShouldUseProceduralMeter()
	{
		return !UseTexturedMeter || _texturedAssetsMissing;
	}

	private void EnsureTexturedNodes()
	{
		_baseTextureRect ??= CreateTextureRect("BaseTexture");
		_fillClipRoot ??= CreateClipRoot("FillClip");
		_fillTextureRect ??= CreateTextureRect("FillTexture");
		_overTextureRect ??= CreateTextureRect("OverTexture");
		_pulseTextureRect ??= CreateTextureRect("PulseTexture");

		EnsureChild(_baseTextureRect);
		EnsureChild(_fillClipRoot);
		if (_fillClipRoot != null && _fillTextureRect != null && _fillTextureRect.GetParent() != _fillClipRoot)
		{
			_fillClipRoot.AddChild(_fillTextureRect);
		}
		EnsureChild(_overTextureRect);
		EnsureChild(_pulseTextureRect);
	}

	private void EnsureTexturedAssetsLoaded()
	{
		if (_texturedAssetsLoaded)
		{
			return;
		}

		_baseTexture = BaseTextureOverride
			?? FrameAnimationHelpers.LoadTextureResource(BaseTexturePath, allowEditorImageFallback: true);
		_fillTexture = FillTextureOverride
			?? FrameAnimationHelpers.LoadTextureResource(FillTexturePath, allowEditorImageFallback: true);
		_overTexture = OverTextureOverride
			?? FrameAnimationHelpers.LoadTextureResource(OverTexturePath, allowEditorImageFallback: true);
		_pulseTexture = PulseTextureOverride
			?? FrameAnimationHelpers.LoadTextureResource(PulseTexturePath, allowEditorImageFallback: true);
		_texturedAssetsMissing = _baseTexture == null || _fillTexture == null || _overTexture == null || _pulseTexture == null;
		_texturedAssetsLoaded = true;
	}

	private void ResetTexturedAssets()
	{
		_texturedAssetsLoaded = false;
		_texturedAssetsMissing = false;
		_baseTexture = null;
		_fillTexture = null;
		_overTexture = null;
		_pulseTexture = null;
		if (!IsNodeReady())
		{
			return;
		}

		EnsureTexturedAssetsLoaded();
		ApplyVisualMode();
		QueueRedraw();
	}

	private void ApplyVisualMode()
	{
		ApplyLayout();
		UpdateTexturedMeter();

		bool showTextured = !ShouldUseProceduralMeter();
		SetTextureNodeVisible(_baseTextureRect, showTextured);
		SetTextureNodeVisible(_fillClipRoot, showTextured && _displayScore > 0.0f);
		SetTextureNodeVisible(_overTextureRect, showTextured);
		SetTextureNodeVisible(_pulseTextureRect, showTextured && _pulseAmount > 0.001f);
	}

	private void ApplyLayout()
	{
		Vector2 center = Size / 2.0f;
		float diameter = Mathf.Min(_circleDiameter, Mathf.Min(Size.X, Size.Y));
		Vector2 textureSize = new(diameter, diameter);
		Vector2 topLeft = center - textureSize / 2.0f;

		ApplyTextureRectLayout(_baseTextureRect, topLeft, textureSize);
		ApplyTextureRectLayout(_overTextureRect, topLeft, textureSize);
		ApplyTextureRectLayout(_pulseTextureRect, topLeft, textureSize);
		ApplyFillTextureProgress(Mathf.Clamp(_displayScore / _maxScore, 0.0f, 1.0f));
	}

	private void UpdateTexturedMeter()
	{
		if (_baseTextureRect == null
			|| _fillClipRoot == null
			|| _fillTextureRect == null
			|| _overTextureRect == null
			|| _pulseTextureRect == null)
		{
			return;
		}

		EnsureTexturedAssetsLoaded();
		ApplyVisualModeWithoutRecursion();
		if (ShouldUseProceduralMeter())
		{
			return;
		}

		float progress = Mathf.Clamp(_displayScore / _maxScore, 0.0f, 1.0f);
		_baseTextureRect.Texture = _baseTexture;
		_fillTextureRect.Texture = _fillTexture;
		_fillTextureRect.Modulate = _fillColor;
		_overTextureRect.Texture = _overTexture;
		_pulseTextureRect.Texture = _pulseTexture;
		ApplyFillTextureProgress(progress);
		_overTextureRect.Modulate = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp(_glowColor.A * 2.0f, 0.0f, 1.0f));

		float pulseScale = 1.0f + 0.12f * _pulseAmount;
		_pulseTextureRect.Scale = new Vector2(pulseScale, pulseScale);
		_pulseTextureRect.Modulate = new Color(_fillColor.R, _fillColor.G, _fillColor.B, 0.85f * _pulseAmount);
		SetTextureNodeVisible(_pulseTextureRect, _pulseAmount > 0.001f);
	}

	private void ApplyVisualModeWithoutRecursion()
	{
		bool showTextured = !ShouldUseProceduralMeter();
		SetTextureNodeVisible(_baseTextureRect, showTextured);
		SetTextureNodeVisible(_fillClipRoot, showTextured && _displayScore > 0.0f);
		SetTextureNodeVisible(_overTextureRect, showTextured);
		if (!showTextured)
		{
			SetTextureNodeVisible(_pulseTextureRect, false);
		}
	}

	private void ApplyFillTextureProgress(float progress)
	{
		if (_fillClipRoot == null || _fillTextureRect == null)
		{
			return;
		}

		Vector2 center = Size / 2.0f;
		float diameter = Mathf.Min(_circleDiameter, Mathf.Min(Size.X, Size.Y));
		Vector2 textureSize = new(diameter, diameter);
		Vector2 topLeft = center - textureSize / 2.0f;
		float visibleHeight = textureSize.Y * progress;
		if (visibleHeight <= 0.001f)
		{
			_fillClipRoot.Visible = false;
			return;
		}

		_fillClipRoot.Position = new Vector2(topLeft.X, topLeft.Y + textureSize.Y - visibleHeight);
		_fillClipRoot.Size = new Vector2(textureSize.X, visibleHeight);
		_fillClipRoot.PivotOffset = Vector2.Zero;
		_fillTextureRect.Position = new Vector2(0.0f, visibleHeight - textureSize.Y);
		_fillTextureRect.Size = textureSize;
		_fillTextureRect.PivotOffset = textureSize / 2.0f;
		_fillClipRoot.Visible = !ShouldUseProceduralMeter();
	}

	private static Control CreateClipRoot(string name)
	{
		return new Control
		{
			Name = name,
			MouseFilter = MouseFilterEnum.Ignore,
			ClipContents = true,
			ZIndex = 0,
		};
	}

	private static TextureRect CreateTextureRect(string name)
	{
		return new TextureRect
		{
			Name = name,
			MouseFilter = MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = TextureFilterEnum.Linear,
			ZIndex = 0,
		};
	}

	private void EnsureChild(Node child)
	{
		if (child.GetParent() != this)
		{
			AddChild(child);
		}
	}

	private static void SetTextureNodeVisible(CanvasItem? item, bool visible)
	{
		if (item != null)
		{
			item.Visible = visible;
		}
	}

	private static void ApplyTextureRectLayout(TextureRect? textureRect, Vector2 topLeft, Vector2 size)
	{
		if (textureRect == null)
		{
			return;
		}

		textureRect.Position = topLeft;
		textureRect.Size = size;
		textureRect.PivotOffset = size / 2.0f;
	}

	private void DrawProceduralMeter()
	{
		Vector2 center = Size / 2.0f;
		float radius = Mathf.Min(_circleDiameter, Mathf.Min(Size.X, Size.Y)) / 2.0f;
		float progress = Mathf.Clamp(_displayScore / _maxScore, 0.0f, 1.0f);

		if (_pulseAmount > 0.0f)
		{
			var pulseColor = new Color(_fillColor.R, _fillColor.G, _fillColor.B, 0.18f * _pulseAmount);
			DrawArc(center, radius + 2.0f + 4.0f * _pulseAmount, 0.0f, FullCircleRadians, 96, pulseColor, 4.0f + 5.0f * _pulseAmount, true);
		}

		DrawCircle(center, radius, _backgroundColor);
		DrawBottomFill(center, radius - 2.0f, progress);
		DrawFillSurface(center, radius - 2.0f, progress);

		DrawArc(center, radius - 1.0f, 0.0f, FullCircleRadians, 96, _rimColor, 2.0f, true);
		DrawArc(center, radius - 4.0f, 0.0f, FullCircleRadians, 96, _glowColor, 1.5f, true);
		DrawCircle(center + new Vector2(-radius * 0.18f, -radius * 0.20f), radius * 0.28f, new Color(1.0f, 1.0f, 1.0f, 0.055f));
	}

	private void DrawBottomFill(Vector2 center, float radius, float progress)
	{
		if (progress <= 0.001f)
		{
			return;
		}

		if (progress >= 0.999f)
		{
			DrawCircle(center, radius, _fillColor);
			return;
		}

		float topY = center.Y + radius - radius * 2.0f * progress;
		int firstY = Mathf.CeilToInt(topY);
		int lastY = Mathf.FloorToInt(center.Y + radius);
		for (int y = firstY; y <= lastY; y++)
		{
			float verticalDistance = y - center.Y;
			float halfWidth = Mathf.Sqrt(Mathf.Max(0.0f, radius * radius - verticalDistance * verticalDistance));
			DrawLine(
				new Vector2(center.X - halfWidth, y),
				new Vector2(center.X + halfWidth, y),
				_fillColor,
				1.5f,
				true);
		}
	}

	private void DrawFillSurface(Vector2 center, float radius, float progress)
	{
		if (progress <= 0.001f || progress >= 0.999f)
		{
			return;
		}

		float topY = center.Y + radius - radius * 2.0f * progress;
		float verticalDistance = topY - center.Y;
		float halfWidth = Mathf.Sqrt(Mathf.Max(0.0f, radius * radius - verticalDistance * verticalDistance));
		DrawLine(
			new Vector2(center.X - halfWidth, topY),
			new Vector2(center.X + halfWidth, topY),
			_glowColor,
			2.0f,
			true);
	}
}
