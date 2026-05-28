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

	public void PlayAction(AvatarAction action)
	{
		switch (action)
		{
			case AvatarAction.Idle:
				PlayIdle();
				break;
			case AvatarAction.Score:
				PlayScore();
				break;
			case AvatarAction.Attack:
				PlayAttack();
				break;
			case AvatarAction.Hit:
				PlayHit();
				break;
			case AvatarAction.Victory:
				PlayVictory();
				break;
			case AvatarAction.Defeat:
				PlayDefeat();
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(action), action, null);
		}
	}

	public void PlayActionByName(string actionName)
	{
		if (!TryParseAction(actionName, out AvatarAction action))
		{
			GD.PushWarning($"Unknown avatar action: {actionName}");
			return;
		}

		PlayAction(action);
	}

	public void PlayIdle()
	{
		StartAction(AvatarAction.Idle);
		ResetPortraitPose();
	}

	public void PlayScore()
	{
		StartAction(AvatarAction.Score);
		AnimateDrawPulse(value => _scorePulse = value, 1.0f, 0.0f, 0.36f);

		_actionTween = CreateTween();
		_actionTween.TweenProperty(_portraitPivot, "scale", Vector2.One * 1.18f, 0.10f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_actionTween.Parallel().TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(0.0f, -10.0f), 0.10f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		_actionTween.TweenProperty(_portraitPivot, "scale", Vector2.One * 0.98f, 0.10f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_actionTween.TweenProperty(_portraitPivot, "scale", Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		_actionTween.Parallel().TweenProperty(_portraitPivot, "position", _basePivotPosition, 0.12f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		_actionTween.TweenCallback(Callable.From(() => FinishAndReturnToIdle(AvatarAction.Score)));
	}

	public void PlayAttack()
	{
		StartAction(AvatarAction.Attack);
		PlayAttackFx();

		_actionTween = CreateTween();
		_actionTween.TweenInterval(GetActionFrameDuration(AvatarAction.Attack) * GetActionFrameCount(AvatarAction.Attack));
		_actionTween.TweenCallback(Callable.From(() => FinishAndReturnToIdle(AvatarAction.Attack)));
	}

	public void PlayHit()
	{
		StartAction(AvatarAction.Hit);
		AnimateDrawPulse(value => _hitPulse = value, 1.0f, 0.0f, 0.24f);

		_actionTween = CreateTween();
		_actionTween.TweenProperty(_portrait, "modulate", new Color(1.0f, 0.42f, 0.36f, 1.0f), 0.04f);
		_actionTween.Parallel().TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(-9.0f, 0.0f), 0.04f);
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(8.0f, 0.0f), 0.04f);
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(-5.0f, 0.0f), 0.04f);
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition, 0.07f);
		_actionTween.Parallel().TweenProperty(_portrait, "modulate", Colors.White, 0.12f);
		_actionTween.TweenCallback(Callable.From(() => FinishAndReturnToIdle(AvatarAction.Hit)));
	}

	public void PlayVictory()
	{
		StartAction(AvatarAction.Victory);
		AnimateDrawPulse(value => _victoryPulse = value, 1.0f, 0.0f, 0.58f);

		_actionTween = CreateTween();
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(0.0f, -14.0f), 0.16f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_actionTween.Parallel().TweenProperty(_portraitPivot, "scale", Vector2.One * 1.12f, 0.16f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(0.0f, -6.0f), 0.18f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_actionTween.TweenCallback(Callable.From(() => EmitFinished(AvatarAction.Victory)));
	}

	public void PlayDefeat()
	{
		StartAction(AvatarAction.Defeat);
		AnimateDrawPulse(value => _defeatShade = value, 0.0f, 1.0f, 0.50f);

		_actionTween = CreateTween();
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(0.0f, 12.0f), 0.18f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		_actionTween.Parallel().TweenProperty(_portraitPivot, "rotation", 0.09f, 0.18f);
		_actionTween.Parallel().TweenProperty(_portraitPivot, "scale", Vector2.One * 0.92f, 0.18f);
		_actionTween.Parallel().TweenProperty(_portrait, "modulate", new Color(0.62f, 0.70f, 0.72f, 0.88f), 0.18f);
		_actionTween.TweenCallback(Callable.From(() => EmitFinished(AvatarAction.Defeat)));
	}

	private void StartAction(AvatarAction action)
	{
		_actionTween?.Kill();
		_actionTween = null;
		_attackFxTween?.Kill();
		_attackFxTween = null;
		HideAttackFx();
		_currentAction = action;
		_scorePulse = 0.0f;
		_hitPulse = 0.0f;
		_victoryPulse = 0.0f;
		if (action != AvatarAction.Defeat)
		{
			_defeatShade = 0.0f;
		}

		_pulseTween?.Kill();
		_pulseTween = null;
		ResetPortraitPose();
		BeginActionFrames(action);
		QueueRedraw();
	}

	private void FinishAndReturnToIdle(AvatarAction finishedAction)
	{
		EmitFinished(finishedAction);
		PlayIdle();
	}

	private void EmitFinished(AvatarAction action)
	{
		EmitSignal(SignalName.ActionFinished, action.ToString());
	}

	private void ResetPortraitPose()
	{
		_portraitPivot.Position = _basePivotPosition;
		_portraitPivot.Rotation = 0.0f;
		_portraitPivot.Scale = Vector2.One;
		_portrait.Modulate = Colors.White;
	}

	private void LoadGeneratedActionFrames()
	{
		_actionFrames.Clear();
		AvatarFrameCache cache = LoadAvatarFrames(GetResolvedAvatarRoot());
		_idleFallbackTexture = cache.IdleFallbackTexture;
		if (!LoadActionFrames)
		{
			return;
		}

		foreach ((AvatarAction action, Texture2D[] frames) in cache.ActionFrames)
		{
			_actionFrames[action] = frames;
		}
	}

	private void LoadAttackFxFrames()
	{
		_attackFxFrames = LoadFxFrames(AttackFxRoot);
		HideAttackFx();
	}

	private void PlayAttackFx()
	{
		if (_attackFxFrames.Length == 0)
		{
			return;
		}

		LayoutAttackFx();
		_attackFx.Visible = true;
		_attackFx.Texture = _attackFxFrames[0];
		_attackFx.Modulate = Colors.White;
		_attackFx.Scale = Vector2.One * AttackFxStartScale;

		_attackFxTween = CreateTween();
		_attackFxTween.SetParallel(true);
		_attackFxTween.SetTrans(Tween.TransitionType.Sine);
		_attackFxTween.SetEase(Tween.EaseType.Out);
		_attackFxTween.TweenProperty(_attackFx, "scale", Vector2.One * AttackFxEndScale, AttackFxFrameSeconds * AttackFxFrameCount);
		_attackFxTween.TweenProperty(_attackFx, "modulate:a", 0.0f, AttackFxFrameSeconds * AttackFxFrameCount);
		FrameAnimationHelpers.TweenTextureFrames(_attackFxTween, _attackFx, _attackFxFrames, AttackFxFrameSeconds);
		_attackFxTween.Finished += HideAttackFx;
	}

	private void HideAttackFx()
	{
		if (_attackFx == null)
		{
			return;
		}

		_attackFx.Visible = false;
		_attackFx.Texture = null;
		_attackFx.Modulate = Colors.White;
		_attackFx.Scale = Vector2.One;
	}

	private void BeginActionFrames(AvatarAction action)
	{
		_frameAction = action;
		if (_actionFrames.TryGetValue(_frameAction, out Texture2D[]? frames) && frames.Length > 0)
		{
			StringName animationName = GetActionAnimationName(action);
			_portrait.Texture = frames[0];
			FrameAnimationHelpers.ConfigureTextureAnimation(
				_frameAnimationPlayer,
				animationName,
			new NodePath("PortraitPivot/Portrait:texture"),
			frames,
			GetActionFrameDuration(action),
			action == AvatarAction.Idle);
			_frameAnimationPlayer.Play(animationName);
			return;
		}

		_frameAnimationPlayer.Stop();
		if (_idleFallbackTexture != null)
		{
			_portrait.Texture = _idleFallbackTexture;
			return;
		}

		if (PortraitTexture != null)
		{
			_portrait.Texture = PortraitTexture;
		}
	}

	private AnimationPlayer CreateFrameAnimationPlayer()
	{
		var animationPlayer = new AnimationPlayer
		{
			Name = "AnimationPlayer",
			RootNode = new NodePath(".."),
		};
		AddChild(animationPlayer);
		return animationPlayer;
	}

	private TextureRect CreateAttackFxRect()
	{
		var attackFx = new TextureRect
		{
			Name = "AttackFx",
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ZIndex = AttackFxZIndex,
			Visible = false,
		};
		AddChild(attackFx);
		return attackFx;
	}

	private void AnimateDrawPulse(Action<float> applyPulse, float from, float to, float seconds)
	{
		applyPulse(from);
		QueueRedraw();
		_pulseTween?.Kill();
		_pulseTween = CreateTween();
		_pulseTween.SetTrans(Tween.TransitionType.Cubic);
		_pulseTween.SetEase(Tween.EaseType.Out);
		_pulseTween.TweenMethod(Callable.From<float>(value =>
		{
			applyPulse(value);
			QueueRedraw();
		}), from, to, seconds);
	}

	private static AvatarFrameCache LoadAvatarFrames(string avatarRoot)
	{
		if (ActionFrameCache.TryGetValue(avatarRoot, out AvatarFrameCache? cachedFrames))
		{
			return cachedFrames;
		}

		var actionFrames = new Dictionary<AvatarAction, Texture2D[]>();
		Texture2D? idleFallbackTexture = ResourceLoader.Load<Texture2D>($"{avatarRoot}/idle/processed/idle-1.png");
		foreach (AvatarAction action in Enum.GetValues<AvatarAction>())
		{
			string slug = GetActionSlug(action);
			Texture2D[] frames = new Texture2D[GetActionFrameCount(avatarRoot, action)];
			bool loadedAllFrames = true;

			for (int index = 0; index < frames.Length; index++)
			{
				string path = $"{avatarRoot}/{slug}/processed/{slug}-{index + 1}.png";
				Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
				if (texture == null)
				{
					loadedAllFrames = false;
					break;
				}

				frames[index] = texture;
			}

			if (loadedAllFrames)
			{
				actionFrames[action] = frames;
			}
		}

		cachedFrames = new AvatarFrameCache(idleFallbackTexture, actionFrames);
		ActionFrameCache[avatarRoot] = cachedFrames;
		return cachedFrames;
	}

	private static Texture2D[] LoadFxFrames(string fxRoot)
	{
		if (string.IsNullOrWhiteSpace(fxRoot))
		{
			return Array.Empty<Texture2D>();
		}

		if (AttackFxFrameCache.TryGetValue(fxRoot, out Texture2D[]? cachedFrames))
		{
			return cachedFrames;
		}

		Texture2D[] frames = FrameAnimationHelpers.LoadTextureFrames(
			$"{fxRoot}/processed/fx-{{0}}.png",
			AttackFxFrameCount,
			"avatar attack effect",
			preferImageFile: true);
		AttackFxFrameCache[fxRoot] = frames;
		return frames;
	}

	private static StringName GetActionAnimationName(AvatarAction action)
	{
		return new StringName($"avatar_{GetActionSlug(action)}");
	}

	private void LayoutPortrait()
	{
		if (_portraitPivot == null)
		{
			return;
		}

		float side = Mathf.Max(1.0f, Mathf.Min(Size.X, Size.Y) * Mathf.Clamp(PortraitScale, 0.55f, 0.95f));
		_portraitPivot.Size = new Vector2(side, side);
		_portraitPivot.PivotOffset = _portraitPivot.Size / 2.0f;
		_basePivotPosition = (Size - _portraitPivot.Size) / 2.0f;

		if (_currentAction == AvatarAction.Idle)
		{
			_portraitPivot.Position = _basePivotPosition;
		}

		LayoutAttackFx();
	}

	private void LayoutAttackFx()
	{
		if (_attackFx == null)
		{
			return;
		}

		float side = Mathf.Max(1.0f, Mathf.Min(Size.X, Size.Y) * AttackFxVisualScale);
		_attackFx.Size = new Vector2(side, side);
		_attackFx.PivotOffset = _attackFx.Size / 2.0f;
		_attackFx.Position = (Size - _attackFx.Size) / 2.0f;
	}

	private void UpdateAttackFxRootForAvatarRoot(string previousRoot)
	{
		string previousDefaultFxRoot = GetDefaultAttackFxRoot(previousRoot);
		if (!string.IsNullOrWhiteSpace(_attackFxRoot)
			&& !string.Equals(_attackFxRoot, previousDefaultFxRoot, StringComparison.Ordinal))
		{
			return;
		}

		_attackFxRoot = GetDefaultAttackFxRoot(GetResolvedAvatarRoot());
	}

	private static string GetDefaultAttackFxRoot(string avatarRoot)
	{
		if (string.IsNullOrWhiteSpace(avatarRoot))
		{
			return string.Empty;
		}

		return CharacterAvatarCatalog.GetAttackFxRoot(avatarRoot);
	}

	private string GetResolvedAvatarRoot()
	{
		return string.IsNullOrWhiteSpace(_avatarRoot)
			? CharacterAvatarCatalog.Default.RootPath
			: _avatarRoot;
	}

	private static bool TryParseAction(string actionName, out AvatarAction action)
	{
		string normalized = actionName.Trim().ToLowerInvariant();
		switch (normalized)
		{
			case "idle":
			case "待机":
				action = AvatarAction.Idle;
				return true;
			case "score":
			case "scored":
			case "得分":
				action = AvatarAction.Score;
				return true;
			case "attack":
			case "攻击":
				action = AvatarAction.Attack;
				return true;
			case "hit":
			case "hurt":
			case "damaged":
			case "受到攻击":
			case "受击":
				action = AvatarAction.Hit;
				return true;
			case "victory":
			case "win":
			case "胜利":
				action = AvatarAction.Victory;
				return true;
			case "defeat":
			case "lose":
			case "loss":
			case "失败":
				action = AvatarAction.Defeat;
				return true;
			default:
				action = AvatarAction.Idle;
				return false;
		}
	}

	private static string GetActionSlug(AvatarAction action)
	{
		return action switch
		{
			AvatarAction.Idle => "idle",
			AvatarAction.Score => "score",
			AvatarAction.Attack => "attack",
			AvatarAction.Hit => "hit",
			AvatarAction.Victory => "victory",
			AvatarAction.Defeat => "defeat",
			_ => "idle",
		};
	}

	private static float GetActionFrameDuration(AvatarAction action)
	{
		return action switch
		{
			AvatarAction.Idle => 0.16f,
			AvatarAction.Defeat => 0.06f,
			_ => 0.08f,
		};
	}

	private int GetActionFrameCount(AvatarAction action)
	{
		return GetActionFrameCount(GetResolvedAvatarRoot(), action);
	}

	private static int GetActionFrameCount(string avatarRoot, AvatarAction action)
	{
		return action == AvatarAction.Idle
			? CharacterAvatarCatalog.GetIdleFrameCount(avatarRoot)
			: CharacterAvatarCatalog.GetActionFrameCount(avatarRoot);
	}

	private sealed record AvatarFrameCache(Texture2D? IdleFallbackTexture, Dictionary<AvatarAction, Texture2D[]> ActionFrames);
}
