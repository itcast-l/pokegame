using System;
using System.Collections.Generic;
using Godot;

public partial class CharacterAvatar : Control
{
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

	[Export]
	public Vector2 AttackDirection { get; set; } = Vector2.Right;

	[Export(PropertyHint.Range, "0.55,0.95,0.01")]
	public float PortraitScale { get; set; } = 0.84f;

	private const string GeneratedAvatarRoot = "res://art/characters/player_cat_avatar/run-002";

	private Control _portraitPivot = default!;
	private TextureRect _portrait = default!;
	private Tween? _actionTween;
	private readonly Dictionary<AvatarAction, Texture2D[]> _actionFrames = new();
	private AvatarAction _currentAction = AvatarAction.Idle;
	private AvatarAction _frameAction = AvatarAction.Idle;
	private Vector2 _basePivotPosition;
	private float _frameElapsed;
	private int _frameIndex;
	private bool _frameLoops;
	private float _scorePulse;
	private float _attackPulse;
	private float _hitPulse;
	private float _victoryPulse;
	private float _defeatShade;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_portraitPivot = GetNode<Control>("PortraitPivot");
		_portrait = GetNode<TextureRect>("PortraitPivot/Portrait");
		_portrait.MouseFilter = MouseFilterEnum.Ignore;
		_portraitPivot.MouseFilter = MouseFilterEnum.Ignore;

		LoadGeneratedActionFrames();

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

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		AdvanceActionFrame(dt);

		_scorePulse = Mathf.MoveToward(_scorePulse, 0.0f, dt * 2.8f);
		_attackPulse = Mathf.MoveToward(_attackPulse, 0.0f, dt * 3.8f);
		_hitPulse = Mathf.MoveToward(_hitPulse, 0.0f, dt * 4.5f);
		_victoryPulse = Mathf.MoveToward(_victoryPulse, 0.0f, dt * 1.7f);
		_defeatShade = Mathf.MoveToward(_defeatShade, _currentAction == AvatarAction.Defeat ? 1.0f : 0.0f, dt * 2.0f);
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 center = Size / 2.0f;
		float radius = Mathf.Min(Size.X, Size.Y) * 0.45f;
		if (radius <= 0.0f)
		{
			return;
		}

		DrawCircle(center + new Vector2(0.0f, radius * 0.08f), radius, new Color(0.0f, 0.07f, 0.09f, 0.38f));
		DrawCircle(center, radius * 0.92f, new Color(0.04f, 0.17f, 0.18f, 0.56f));
		DrawArc(center, radius, 0.0f, Mathf.Tau, 96, new Color(AccentColor.R, AccentColor.G, AccentColor.B, 0.48f), 3.0f, true);

		if (_scorePulse > 0.0f)
		{
			DrawArc(center, radius + 16.0f * (1.0f - _scorePulse), 0.0f, Mathf.Tau, 96, new Color(0.7f, 1.0f, 0.8f, 0.34f * _scorePulse), 5.0f, true);
		}

		if (_attackPulse > 0.0f)
		{
			Vector2 direction = GetAttackDirection();
			Vector2 start = center + direction * radius * 0.58f;
			Vector2 end = center + direction * radius * (1.18f + 0.12f * (1.0f - _attackPulse));
			DrawLine(start, end, new Color(1.0f, 0.84f, 0.34f, 0.58f * _attackPulse), 5.0f, true);
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
		_scorePulse = 1.0f;

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
		_attackPulse = 1.0f;
		Vector2 lunge = GetAttackDirection() * 18.0f;

		_actionTween = CreateTween();
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition - lunge * 0.28f, 0.06f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition + lunge, 0.09f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_actionTween.Parallel().TweenProperty(_portraitPivot, "rotation", -0.08f * Mathf.Sign(GetAttackDirection().X == 0.0f ? 1.0f : GetAttackDirection().X), 0.09f);
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition, 0.14f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		_actionTween.Parallel().TweenProperty(_portraitPivot, "rotation", 0.0f, 0.14f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		_actionTween.TweenCallback(Callable.From(() => FinishAndReturnToIdle(AvatarAction.Attack)));
	}

	public void PlayHit()
	{
		StartAction(AvatarAction.Hit);
		_hitPulse = 1.0f;

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
		_victoryPulse = 1.0f;

		_actionTween = CreateTween();
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(0.0f, -14.0f), 0.16f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_actionTween.Parallel().TweenProperty(_portraitPivot, "scale", Vector2.One * 1.12f, 0.16f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(0.0f, -6.0f), 0.18f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_actionTween.TweenCallback(Callable.From(() => EmitFinished(AvatarAction.Victory)));
	}

	public void PlayDefeat()
	{
		StartAction(AvatarAction.Defeat);

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
		_currentAction = action;
		_scorePulse = 0.0f;
		_attackPulse = 0.0f;
		_hitPulse = 0.0f;
		_victoryPulse = 0.0f;
		_defeatShade = 0.0f;
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
		foreach (AvatarAction action in Enum.GetValues<AvatarAction>())
		{
			string slug = GetActionSlug(action);
			Texture2D[] frames = new Texture2D[GetActionFrameCount(action)];
			bool loadedAllFrames = true;

			for (int index = 0; index < frames.Length; index++)
			{
				string path = $"{GeneratedAvatarRoot}/{slug}/processed/{slug}-{index + 1}.png";
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
				_actionFrames[action] = frames;
			}
		}
	}

	private void BeginActionFrames(AvatarAction action)
	{
		_frameAction = action;
		_frameIndex = 0;
		_frameElapsed = 0.0f;
		_frameLoops = action == AvatarAction.Idle;
		ApplyActionFrame();
	}

	private void AdvanceActionFrame(float dt)
	{
		if (!_actionFrames.TryGetValue(_frameAction, out Texture2D[]? frames) || frames.Length <= 1)
		{
			return;
		}

		_frameElapsed += dt;
		float duration = GetActionFrameDuration(_frameAction);
		while (_frameElapsed >= duration)
		{
			_frameElapsed -= duration;

			if (_frameIndex < frames.Length - 1)
			{
				_frameIndex++;
			}
			else if (_frameLoops)
			{
				_frameIndex = 0;
			}
			else
			{
				_frameElapsed = 0.0f;
				break;
			}

			ApplyActionFrame();
		}
	}

	private void ApplyActionFrame()
	{
		if (_actionFrames.TryGetValue(_frameAction, out Texture2D[]? frames) && _frameIndex >= 0 && _frameIndex < frames.Length)
		{
			_portrait.Texture = frames[_frameIndex];
			return;
		}

		if (PortraitTexture != null)
		{
			_portrait.Texture = PortraitTexture;
		}
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
	}

	private Vector2 GetAttackDirection()
	{
		if (AttackDirection.LengthSquared() <= 0.0001f)
		{
			return Vector2.Right;
		}

		return AttackDirection.Normalized();
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
			AvatarAction.Idle => 0.08f,
			AvatarAction.Defeat => 0.06f,
			_ => 0.08f,
		};
	}

	private static int GetActionFrameCount(AvatarAction action)
	{
		return action == AvatarAction.Idle ? 12 : 4;
	}
}
