using System;
using System.Collections.Generic;
using Godot;

public partial class CharacterAvatar
{
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

	public void ShowIdleStill()
	{
		_actionTween?.Kill();
		_actionTween = null;
		_attackFxTween?.Kill();
		_attackFxTween = null;
		HideAttackFx();
		_feedbackFxTween?.Kill();
		_feedbackFxTween = null;
		HideFeedbackFx();
		_currentAction = AvatarAction.Idle;
		ResetPortraitPose();
		ShowActionStillFrame(AvatarAction.Idle);
	}

	public void PlayScore()
	{
		StartAction(AvatarAction.Score);

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
		PlayFeedbackFx(_hitFeedbackFxFrames, 0.24f);

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
		PlayFeedbackFx(_victoryFeedbackFxFrames, 0.58f);

		_actionTween = CreateTween();
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(0.0f, -14.0f), 0.16f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_actionTween.Parallel().TweenProperty(_portraitPivot, "scale", Vector2.One * 1.12f, 0.16f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_actionTween.TweenProperty(_portraitPivot, "position", _basePivotPosition + new Vector2(0.0f, -6.0f), 0.18f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_actionTween.TweenCallback(Callable.From(() => EmitFinished(AvatarAction.Victory)));
	}

	public void PlayDefeat()
	{
		StartAction(AvatarAction.Defeat);
		PlayFeedbackFx(_defeatFeedbackFxFrames, 0.50f);

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
		_feedbackFxTween?.Kill();
		_feedbackFxTween = null;
		HideAttackFx();
		HideFeedbackFx();
		_currentAction = action;
		ResetPortraitPose();
		BeginActionFrames(action);
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

}
