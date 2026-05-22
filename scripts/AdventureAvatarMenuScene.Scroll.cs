using Godot;

public partial class AdventureAvatarMenuScene
{
	private void SmoothScrollBy(int delta)
	{
		if (_levelScroll == null || !GodotObject.IsInstanceValid(_levelScroll))
		{
			return;
		}

		_levelScrollTarget = _levelScroll.ScrollVertical;
		_levelScrollTarget = Mathf.Clamp(
			_levelScrollTarget + delta,
			0,
			GetMaxScrollTarget());
		AnimateScrollToTarget();
	}

	private void OnLevelScrollGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
		{
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			SmoothScrollBy(-SmoothScrollWheelStep);
			_levelScroll.AcceptEvent();
		}
		else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			SmoothScrollBy(SmoothScrollWheelStep);
			_levelScroll.AcceptEvent();
		}
	}

	private void AnimateScrollToTarget()
	{
		if (_levelScroll == null || !GodotObject.IsInstanceValid(_levelScroll))
		{
			return;
		}

		_levelScrollTween?.Kill();
		_levelScrollTween = CreateTween();
		_levelScrollTween.SetTrans(Tween.TransitionType.Sine);
		_levelScrollTween.SetEase(Tween.EaseType.Out);
		_levelScrollTween.TweenProperty(_levelScroll, "scroll_vertical", _levelScrollTarget, SmoothScrollTweenSeconds);
	}

	private void ClampScrollTarget()
	{
		if (_levelScroll == null || !GodotObject.IsInstanceValid(_levelScroll))
		{
			return;
		}

		_levelScrollTween?.Kill();
		_levelScrollTarget = Mathf.Clamp(_levelScroll.ScrollVertical, 0, GetMaxScrollTarget());
		_levelScroll.ScrollVertical = _levelScrollTarget;
	}

	private int GetMaxScrollTarget()
	{
		if (_levelScroll == null || !GodotObject.IsInstanceValid(_levelScroll))
		{
			return 0;
		}

		ScrollBar verticalScrollBar = _levelScroll.GetVScrollBar();
		double maxScroll = verticalScrollBar.MaxValue - verticalScrollBar.Page;
		return Mathf.Max(0, (int)Mathf.Round(maxScroll));
	}
}
