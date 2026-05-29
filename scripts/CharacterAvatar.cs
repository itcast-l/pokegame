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
	private AnimationPlayer _frameAnimationPlayer = default!;
	private Texture2D? _idleFallbackTexture;
	private Tween? _actionTween;
	private Tween? _pulseTween;
	private Tween? _attackFxTween;
	private static readonly Dictionary<string, AvatarFrameCache> ActionFrameCache = new();
	private static readonly Dictionary<string, Texture2D[]> AttackFxFrameCache = new();
	private readonly Dictionary<AvatarAction, Texture2D[]> _actionFrames = new();
	private Texture2D[] _attackFxFrames = Array.Empty<Texture2D>();
	private AvatarAction _currentAction = AvatarAction.Idle;
	private AvatarAction _frameAction = AvatarAction.Idle;
	private Vector2 _basePivotPosition;
	private float _scorePulse;
	private float _hitPulse;
	private float _victoryPulse;
	private float _defeatShade;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_portraitPivot = GetNode<Control>("PortraitPivot");
		_portrait = GetNode<TextureRect>("PortraitPivot/Portrait");
		_attackFx = GetNodeOrNull<TextureRect>("AttackFx") ?? CreateAttackFxRect();
		_frameAnimationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer") ?? CreateFrameAnimationPlayer();
		_portrait.MouseFilter = MouseFilterEnum.Ignore;
		_portraitPivot.MouseFilter = MouseFilterEnum.Ignore;
		_attackFx.MouseFilter = MouseFilterEnum.Ignore;

		UpdateAttackFxRootForAvatarRoot(AvatarRoot);
		LoadGeneratedActionFrames();
		LoadAttackFxFrames();

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

		if (_scorePulse > 0.0f)
		{
			DrawArc(center, radius + 16.0f * (1.0f - _scorePulse), 0.0f, Mathf.Tau, 96, new Color(0.7f, 1.0f, 0.8f, 0.34f * _scorePulse), 5.0f, true);
		}

		if (_hitPulse > 0.0f)
		{
			DrawCircle(center, radius * 1.02f, new Color(1.0f, 0.12f, 0.08f, 0.18f * _hitPulse));
			DrawArc(center, radius * 1.03f, 0.0f, Mathf.Tau, 96, new Color(1.0f, 0.28f, 0.18f, 0.48f * _hitPulse), 4.0f, true);
		}

		if (_victoryPulse > 0.0f)
		{
			DrawArc(center, radius + 10.0f, 0.0f, Mathf.Tau, 96, new Color(1.0f, 0.88f, 0.34f, 0.5f * _victoryPulse), 4.0f, true);
			DrawCircle(center + new Vector2(-radius * 0.28f, -radius * 0.32f), radius * 0.12f, new Color(1.0f, 0.96f, 0.62f, 0.42f * _victoryPulse));
			DrawCircle(center + new Vector2(radius * 0.34f, -radius * 0.24f), radius * 0.08f, new Color(1.0f, 0.96f, 0.62f, 0.34f * _victoryPulse));
		}

		if (_defeatShade > 0.0f)
		{
			DrawCircle(center, radius * 0.96f, new Color(0.0f, 0.0f, 0.0f, 0.20f * _defeatShade));
		}
	}

}
