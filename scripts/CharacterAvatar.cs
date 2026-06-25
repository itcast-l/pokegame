using System;
using System.Collections.Generic;
using Godot;

public partial class CharacterAvatar : Control
{
	private const int AttackFxFrameCount = 4;
	private const float AttackFxFrameSeconds = 0.12f;
	private const float AttackFxVisualScale = 1.28f;
	private const float AttackFxStartScale = 0.78f;
	private const float AttackFxEndScale = 1.06f;
	private const int AttackFxZIndex = 8;
	private const float FeedbackFxVisualScale = 1.42f;
	private const string FrameBaseTexturePath = "res://art/ui/avatar_frame/run-001/processed/base.png";
	private const string FrameAccentTexturePath = "res://art/ui/avatar_frame/run-001/processed/accent.png";

	public enum AvatarAction
	{
		Idle,
		Score,
		Attack,
		Hit,
		Victory,
		Defeat,
	}

	[Signal]
	public delegate void ActionFinishedEventHandler(string actionName);

	[Export]
	public Texture2D? PortraitTexture { get; set; }

	private Color _accentColor = new(0.18f, 0.86f, 0.82f, 1.0f);

	[Export]
	public Color AccentColor
	{
		get => _accentColor;
		set
		{
			_accentColor = value;
			if (IsNodeReady())
			{
				ApplyFrameTextures();
				QueueRedraw();
			}
		}
	}

	[Export(PropertyHint.Range, "0.55,0.95,0.01")]
	public float PortraitScale { get; set; } = 0.84f;

	[Export]
	public bool ShowFrame { get; set; } = true;

	private bool _useTexturedFrame = true;

	[Export]
	public bool UseTexturedFrame
	{
		get => _useTexturedFrame;
		set
		{
			_useTexturedFrame = value;
			if (IsNodeReady())
			{
				ApplyFrameTextures();
				QueueRedraw();
			}
		}
	}

	[Export]
	public bool LoadActionFrames { get; set; } = true;

	[Export]
	public CharacterAudioConfig? AudioConfig { get; set; }

	private Texture2D? _frameBaseTextureOverride;

	[Export]
	public Texture2D? FrameBaseTextureOverride
	{
		get => _frameBaseTextureOverride;
		set
		{
			if (_frameBaseTextureOverride == value)
			{
				return;
			}

			_frameBaseTextureOverride = value;
			ResetFrameTextures();
		}
	}

	private Texture2D? _frameAccentTextureOverride;

	[Export]
	public Texture2D? FrameAccentTextureOverride
	{
		get => _frameAccentTextureOverride;
		set
		{
			if (_frameAccentTextureOverride == value)
			{
				return;
			}

			_frameAccentTextureOverride = value;
			ResetFrameTextures();
		}
	}

	private string _avatarRoot = string.Empty;
	private string _attackFxRoot = "";

	[Export]
	public string AvatarRoot
	{
		get => _avatarRoot;
		set
		{
			string nextRoot = value?.Trim() ?? string.Empty;
			if (_avatarRoot == nextRoot)
			{
				return;
			}

			string previousRoot = GetResolvedAvatarRoot();
			_avatarRoot = nextRoot;
			if (IsNodeReady())
			{
				UpdateAttackFxRootForAvatarRoot(previousRoot);
				LoadGeneratedActionFrames();
				LoadAttackFxFrames();
				BeginActionFrames(_currentAction);
			}
		}
	}

	[Export]
	public string AttackFxRoot
	{
		get => _attackFxRoot;
		set
		{
			string nextRoot = value.Trim();
			if (_attackFxRoot == nextRoot)
			{
				return;
			}

			_attackFxRoot = nextRoot;
			if (IsNodeReady())
			{
				LoadAttackFxFrames();
			}
		}
	}

	private Control _portraitPivot = default!;
	private TextureRect _portrait = default!;
	private TextureRect _frameBase = default!;
	private TextureRect _frameAccent = default!;
	private TextureRect _attackFx = default!;
	private TextureRect _feedbackFx = default!;
	private AnimationPlayer _frameAnimationPlayer = default!;
	private Texture2D? _frameBaseTexture;
	private Texture2D? _frameAccentTexture;
	private bool _frameAssetsLoaded;
	private bool _frameAssetsMissing;
	private Texture2D? _idleFallbackTexture;
	private Tween? _actionTween;
	private Tween? _attackFxTween;
	private Tween? _feedbackFxTween;
	private static readonly Dictionary<string, AvatarFrameCache> ActionFrameCache = new();
	private static readonly Dictionary<string, Texture2D[]> AttackFxFrameCache = new();
	private static readonly Dictionary<string, Texture2D[]> FeedbackFxFrameCache = new();
	private readonly Dictionary<AvatarAction, Texture2D[]> _actionFrames = new();
	private Texture2D[] _attackFxFrames = Array.Empty<Texture2D>();
	private Texture2D[] _hitFeedbackFxFrames = Array.Empty<Texture2D>();
	private Texture2D[] _victoryFeedbackFxFrames = Array.Empty<Texture2D>();
	private Texture2D[] _defeatFeedbackFxFrames = Array.Empty<Texture2D>();
	private AvatarAction _currentAction = AvatarAction.Idle;
	private AvatarAction _frameAction = AvatarAction.Idle;
	private Vector2 _basePivotPosition;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_frameBase = GetNodeOrNull<TextureRect>("FrameBase") ?? CreateFrameTextureRect("FrameBase", zIndex: -2);
		_frameAccent = GetNodeOrNull<TextureRect>("FrameAccent") ?? CreateFrameTextureRect("FrameAccent", zIndex: -1);
		_portraitPivot = GetNode<Control>("PortraitPivot");
		_portrait = GetNode<TextureRect>("PortraitPivot/Portrait");
		_attackFx = GetNodeOrNull<TextureRect>("AttackFx") ?? CreateAttackFxRect();
		_feedbackFx = GetNodeOrNull<TextureRect>("FeedbackFx") ?? CreateFeedbackFxRect();
		_frameAnimationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer") ?? CreateFrameAnimationPlayer();
		_frameBase.MouseFilter = MouseFilterEnum.Ignore;
		_frameAccent.MouseFilter = MouseFilterEnum.Ignore;
		_portrait.MouseFilter = MouseFilterEnum.Ignore;
		_portraitPivot.MouseFilter = MouseFilterEnum.Ignore;
		_attackFx.MouseFilter = MouseFilterEnum.Ignore;
		_feedbackFx.MouseFilter = MouseFilterEnum.Ignore;
		LoadFrameTextures();

		UpdateAttackFxRootForAvatarRoot(AvatarRoot);
		LoadGeneratedActionFrames();
		LoadAttackFxFrames();
		LoadFeedbackFxFrames();

		if (PortraitTexture != null)
		{
			_portrait.Texture = PortraitTexture;
		}

		LayoutPortrait();
		ApplyFrameTextures();
		PlayIdle();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized && IsInsideTree())
		{
			LayoutPortrait();
		}
	}

	public override void _Draw()
	{
		if (ShouldDrawProceduralFrame())
		{
			DrawProceduralFrame();
		}
	}

	private bool ShouldDrawProceduralFrame()
	{
		return ShowFrame && (!UseTexturedFrame || _frameAssetsMissing);
	}

	private void DrawProceduralFrame()
	{
		Vector2 center = Size / 2.0f;
		float radius = Mathf.Min(Size.X, Size.Y) * 0.45f;
		if (radius <= 0.0f)
		{
			return;
		}

		if (ShowFrame)
		{
			DrawCircle(center + new Vector2(0.0f, radius * 0.08f), radius, new Color(0.0f, 0.07f, 0.09f, 0.38f));
			DrawCircle(center, radius * 0.92f, new Color(0.04f, 0.17f, 0.18f, 0.56f));
			DrawArc(center, radius, 0.0f, Mathf.Tau, 96, new Color(AccentColor.R, AccentColor.G, AccentColor.B, 0.48f), 3.0f, true);
		}

	}

	private void LoadFrameTextures()
	{
		if (_frameAssetsLoaded)
		{
			return;
		}

		_frameBaseTexture = FrameBaseTextureOverride
			?? FrameAnimationHelpers.LoadTextureResource(FrameBaseTexturePath, allowEditorImageFallback: true);
		_frameAccentTexture = FrameAccentTextureOverride
			?? FrameAnimationHelpers.LoadTextureResource(FrameAccentTexturePath, allowEditorImageFallback: true);
		_frameAssetsMissing = _frameBaseTexture == null || _frameAccentTexture == null;
		_frameAssetsLoaded = true;
	}

	private void ResetFrameTextures()
	{
		_frameAssetsLoaded = false;
		_frameAssetsMissing = false;
		_frameBaseTexture = null;
		_frameAccentTexture = null;
		if (!IsNodeReady())
		{
			return;
		}

		LoadFrameTextures();
		ApplyFrameTextures();
	}

	private void ApplyFrameTextures()
	{
		LoadFrameTextures();
		bool showTextured = ShowFrame && UseTexturedFrame && !_frameAssetsMissing;
		_frameBase.Visible = showTextured;
		_frameAccent.Visible = showTextured;
		_frameBase.Texture = showTextured ? _frameBaseTexture : null;
		_frameAccent.Texture = showTextured ? _frameAccentTexture : null;
		_frameAccent.Modulate = new Color(AccentColor.R, AccentColor.G, AccentColor.B, 1.0f);
		QueueRedraw();
	}

	private TextureRect CreateFrameTextureRect(string name, int zIndex)
	{
		var textureRect = new TextureRect
		{
			Name = name,
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ZIndex = zIndex,
			Visible = false,
		};
		AddChild(textureRect);
		MoveChild(textureRect, 0);
		return textureRect;
	}

}
