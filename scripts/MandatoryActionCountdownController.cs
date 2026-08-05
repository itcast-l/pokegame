using System;
using Godot;

internal sealed class MandatoryActionCountdownController : IDisposable
{
	private static readonly Color NormalCountdownColor = Colors.White;
	private static readonly Color UrgentCountdownColor = new(1.0f, 0.86f, 0.42f);
	private const float PulseMinimumAlpha = 0.78f;
	private const double PulseHalfCycleSeconds = 0.16;

	private Control? _root;
	private Label? _countdownLabel;
	private Label? _hintLabel;
	private Func<bool>? _isActionActive;
	private Tween? _introTween;
	private Tween? _urgentPulseTween;
	private readonly double _durationSeconds;
	private readonly double _urgentProgressThreshold;
	private double _remainingSeconds;
	private int _lastDisplaySeconds;
	private bool _lastUrgentState;
	private bool _disposed;

	internal MandatoryActionCountdownController(
		Control root,
		Label countdownLabel,
		Label hintLabel,
		double durationSeconds,
		double urgentProgressThreshold,
		Func<bool> isActionActive)
	{
		_root = root ?? throw new ArgumentNullException(nameof(root));
		_countdownLabel = countdownLabel ?? throw new ArgumentNullException(nameof(countdownLabel));
		_hintLabel = hintLabel ?? throw new ArgumentNullException(nameof(hintLabel));
		_isActionActive = isActionActive ?? throw new ArgumentNullException(nameof(isActionActive));
		_durationSeconds = Math.Max(0.001, durationSeconds);
		_urgentProgressThreshold = Math.Clamp(urgentProgressThreshold, 0.0, 1.0);
		_remainingSeconds = _durationSeconds;
		_lastDisplaySeconds = Math.Max(0, (int)Math.Ceiling(_remainingSeconds));
		_lastUrgentState = false;
	}

	internal bool IsActive => !_disposed
		&& _root != null
		&& GodotObject.IsInstanceValid(_root);

	internal int TextWriteCount { get; private set; }
	internal int UrgentStateWriteCount { get; private set; }
	internal long LastTickManagedAllocationBytes { get; private set; }
	internal long MaxStableTickManagedAllocationBytes { get; private set; }
	internal int DisplaySeconds => _lastDisplaySeconds;
	internal string RenderedText => _countdownLabel?.Text ?? string.Empty;
	internal bool IsUrgent => _lastUrgentState;

	internal void StartIntro()
	{
		if (!IsActive || _root == null)
		{
			return;
		}

		KillTween(ref _introTween);
		_introTween = _root.CreateTween();
		_introTween.SetTrans(Tween.TransitionType.Cubic);
		_introTween.SetEase(Tween.EaseType.Out);
		_introTween.TweenProperty(_root, "modulate:a", 1.0f, 0.16f);
	}

	internal bool Update(double delta)
	{
		long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
		int textWritesBefore = TextWriteCount;
		int urgentWritesBefore = UrgentStateWriteCount;

		if (!IsActive || _isActionActive?.Invoke() != true)
		{
			Dispose();
			LastTickManagedAllocationBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
			return false;
		}

		_remainingSeconds = Math.Max(0.0, _remainingSeconds - Math.Max(0.0, delta));
		double progress = Math.Clamp(_remainingSeconds / _durationSeconds, 0.0, 1.0);
		int displaySeconds = progress <= 0.0
			? 0
			: Math.Max(0, (int)Math.Ceiling(_remainingSeconds));
		UpdateDisplaySeconds(displaySeconds);
		UpdateUrgentState(progress <= _urgentProgressThreshold);

		LastTickManagedAllocationBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
		if (textWritesBefore == TextWriteCount && urgentWritesBefore == UrgentStateWriteCount)
		{
			MaxStableTickManagedAllocationBytes = Math.Max(
				MaxStableTickManagedAllocationBytes,
				LastTickManagedAllocationBytes);
		}
		return true;
	}

	internal void RefreshHintText(string text)
	{
		if (_hintLabel != null && GodotObject.IsInstanceValid(_hintLabel))
		{
			_hintLabel.Text = text ?? string.Empty;
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		KillTween(ref _introTween);
		KillTween(ref _urgentPulseTween);

		Control? root = _root;
		_root = null;
		_countdownLabel = null;
		_hintLabel = null;
		_isActionActive = null;
		_remainingSeconds = 0.0;

		if (root != null && GodotObject.IsInstanceValid(root) && !root.IsQueuedForDeletion())
		{
			root.GetParent()?.RemoveChild(root);
			root.QueueFree();
		}
	}

	private void UpdateDisplaySeconds(int displaySeconds)
	{
		if (_lastDisplaySeconds == displaySeconds)
		{
			return;
		}

		_lastDisplaySeconds = displaySeconds;
		if (_countdownLabel != null && GodotObject.IsInstanceValid(_countdownLabel))
		{
			_countdownLabel.Text = displaySeconds.ToString();
			TextWriteCount++;
		}
	}

	private void UpdateUrgentState(bool isUrgent)
	{
		if (_lastUrgentState == isUrgent)
		{
			return;
		}

		_lastUrgentState = isUrgent;
		KillTween(ref _urgentPulseTween);
		if (_countdownLabel != null && GodotObject.IsInstanceValid(_countdownLabel))
		{
			_countdownLabel.AddThemeColorOverride(
				"font_color",
				isUrgent ? UrgentCountdownColor : NormalCountdownColor);
			UrgentStateWriteCount++;
		}

		if (_hintLabel == null || !GodotObject.IsInstanceValid(_hintLabel))
		{
			return;
		}

		_hintLabel.Modulate = Colors.White;
		if (!isUrgent)
		{
			return;
		}

		_urgentPulseTween = _hintLabel.CreateTween();
		_urgentPulseTween.SetLoops();
		_urgentPulseTween.SetTrans(Tween.TransitionType.Cubic);
		_urgentPulseTween.SetEase(Tween.EaseType.InOut);
		_urgentPulseTween.TweenProperty(_hintLabel, "modulate:a", PulseMinimumAlpha, PulseHalfCycleSeconds);
		_urgentPulseTween.TweenProperty(_hintLabel, "modulate:a", 1.0f, PulseHalfCycleSeconds);
	}

	private static void KillTween(ref Tween? tween)
	{
		if (tween != null && GodotObject.IsInstanceValid(tween) && tween.IsValid())
		{
			tween.Kill();
		}
		tween = null;
	}
}
