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

	[Export]
	public Color AccentColor { get; set; } = new(0.18f, 0.86f, 0.82f, 1.0f);

	[Export(PropertyHint.Range, "0.55,0.95,0.01")]
	public float PortraitScale { get; set; } = 0.84f;

	[Export]
	public bool ShowFrame { get; set; } = true;

	[Export]
	public bool LoadActionFrames { get; set; } = true;

	[Export]
	public CharacterAudioConfig? AudioConfig { get; set; }

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
	private TextureRect _attackFx = default!;
	private TextureRect _feedbackFx = default!;
	private AnimationPlayer _frameAnimationPlayer = default!;
	private Texture2D? _idleFallbackTexture;
	private Tween? _actionTween;
	private Tween? _attackFxTween;
	private Tween? _feedbackFxTween;
	private static readonly Dictionary<string, AvatarFrameCache> ActionFrameCache = new();
	private static readonly Dictionary<string, Texture2D[]> AttackFxFrameCache = new();
	private static readonly Dictionary<string, Texture2D[]> FeedbackFxFrameCache = new();
	private readonly Dictionary<AvatarAction, Texture2D[]> _actionFrames = new();
	private Texture2D[] _attackFxFrames = Array.Empty<Texture2D>();
	private Texture2D[] _scoreFeedbackFxFrames = Array.Empty<Texture2D>();
	private Texture2D[] _hitFeedbackFxFrames = Array.Empty<Texture2D>();
	private Texture2D[] _victoryFeedbackFxFrames = Array.Empty<Texture2D>();
	private Texture2D[] _defeatFeedbackFxFrames = Array.Empty<Texture2D>();
	private AvatarAction _currentAction = AvatarAction.Idle;
	private AvatarAction _frameAction = AvatarAction.Idle;
	private Vector2 _basePivotPosition;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_portraitPivot = GetNode<Control>("PortraitPivot");
		_portrait = GetNode<TextureRect>("PortraitPivot/Portrait");
		_attackFx = GetNodeOrNull<TextureRect>("AttackFx") ?? CreateAttackFxRect();
		_feedbackFx = GetNodeOrNull<TextureRect>("FeedbackFx") ?? CreateFeedbackFxRect();
		_frameAnimationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer") ?? CreateFrameAnimationPlayer();
		_portrait.MouseFilter = MouseFilterEnum.Ignore;
		_portraitPivot.MouseFilter = MouseFilterEnum.Ignore;
		_attackFx.MouseFilter = MouseFilterEnum.Ignore;
		_feedbackFx.MouseFilter = MouseFilterEnum.Ignore;

		UpdateAttackFxRootForAvatarRoot(AvatarRoot);
		LoadGeneratedActionFrames();
		LoadAttackFxFrames();
		LoadFeedbackFxFrames();

		if (PortraitTexture != null)
		{
			_portrait.Texture = PortraitTexture;
		}

		LayoutPortrait();
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

}
