using System;
using Godot;

public partial class AdventureTutorialOverlay : Control
{
	private const float CalloutGap = 14.0f;
	private const float ViewportPadding = 18.0f;
	private const float CalloutMaxTextWidth = 246.0f;
	private const float PlayCalloutMaxTextWidth = 306.0f;
	private const float CalloutMinTextWidth = 116.0f;
	private const float PlayCalloutMinTextWidth = 286.0f;
	private const float CalloutHorizontalPadding = 12.0f;
	private const float CalloutVerticalPadding = 8.0f;
	private const float CalloutButtonGap = 4.0f;
	private const float CalloutTextHeightSafety = 2.0f;
	private const float CalloutButtonHeight = 28.0f;
	private static readonly Vector2 TargetPadding = new(10.0f, 10.0f);

	[Signal]
	public delegate void ContinueRequestedEventHandler();

	public enum CalloutPlacement
	{
		Above,
		Below,
		Left,
		Right,
	}

	private static readonly CalloutPlacement[] DefaultPlacementPreference =
	{
		CalloutPlacement.Above,
		CalloutPlacement.Below,
		CalloutPlacement.Right,
		CalloutPlacement.Left,
	};

	private ColorRect _inputBlocker = default!;
	private Line2D _pointerLine = default!;
	private Panel _targetOutline = default!;
	private Panel _calloutPanel = default!;
	private Label _calloutLabel = default!;
	private Button _continueButton = default!;
	private Func<Node, Tween>? _tweenFactory;
	private Tween? _pulseTween;
	private Rect2 _targetRect;
	private Vector2? _fixedCalloutCenter;
	private CalloutPlacement[] _placementPreference = DefaultPlacementPreference;
	private bool _hasTarget;
	private bool _continueMode;

	public bool IsOverlayVisible => Visible;
	public bool IsContinueMode => _continueMode;

	public override void _Ready()
	{
		_inputBlocker = GetNode<ColorRect>("AdventureTutorialInputBlocker");
		_pointerLine = GetNode<Line2D>("AdventureTutorialPointer");
		_targetOutline = GetNode<Panel>("AdventureTutorialTargetOutline");
		_calloutPanel = GetNode<Panel>("AdventureTutorialCallout");
		_calloutLabel = GetNode<Label>("AdventureTutorialCallout/AdventureTutorialText");
		_continueButton = GetNode<Button>("AdventureTutorialCallout/AdventureTutorialContinueButton");
		_continueButton.Pressed += OnContinuePressed;
		Resized += RefreshLayout;
	}

	public override void _ExitTree()
	{
		_pulseTween?.Kill();
		_pulseTween = null;
	}

	public void SetTweenFactory(Func<Node, Tween> tweenFactory)
	{
		_tweenFactory = tweenFactory ?? throw new ArgumentNullException(nameof(tweenFactory));
	}

	public void SetTargetRect(Rect2 targetRect)
	{
		SetTargetLayout(targetRect, null, null);
	}

	public void SetTargetLayout(
		Rect2 targetRect,
		Vector2? fixedCalloutCenter,
		CalloutPlacement[]? placementPreference)
	{
		_targetRect = targetRect;
		_fixedCalloutCenter = fixedCalloutCenter;
		_placementPreference = placementPreference is { Length: > 0 }
			? (CalloutPlacement[])placementPreference.Clone()
			: DefaultPlacementPreference;
		_hasTarget = true;
		RefreshLayout();
	}

	public void SetContent(string content, string continueText, bool continueMode)
	{
		_continueMode = continueMode;
		_calloutLabel.Text = string.Empty;
		_calloutLabel.CustomMinimumSize = Vector2.Zero;
		_calloutLabel.Size = new Vector2(
			continueMode ? CalloutMaxTextWidth : PlayCalloutMaxTextWidth,
			1.0f);
		_calloutLabel.Text = content ?? string.Empty;
		_continueButton.Text = continueText ?? string.Empty;
		_continueButton.Visible = continueMode;
		_inputBlocker.Visible = continueMode;
		_inputBlocker.MouseFilter = continueMode
			? MouseFilterEnum.Stop
			: MouseFilterEnum.Ignore;
		RefreshLayout();
	}

	public void ShowOverlay()
	{
		Visible = _hasTarget;
		if (Visible)
		{
			RefreshLayout();
		}
	}

	public void HideOverlay()
	{
		_pulseTween?.Kill();
		_pulseTween = null;
		_calloutPanel.Modulate = Colors.White;
		Visible = false;
		_hasTarget = false;
		_inputBlocker.MouseFilter = MouseFilterEnum.Ignore;
	}

	public void SetContinueEnabled(bool enabled)
	{
		_continueButton.Disabled = !enabled;
		_continueButton.FocusMode = FocusModeEnum.None;
	}

	public void Pulse()
	{
		if (!Visible || !GodotObject.IsInstanceValid(_calloutPanel))
		{
			return;
		}

		_pulseTween?.Kill();
		_calloutPanel.Modulate = Colors.White;
		_pulseTween = _tweenFactory?.Invoke(_calloutPanel) ?? _calloutPanel.CreateTween();
		_pulseTween.SetTrans(Tween.TransitionType.Sine);
		_pulseTween.SetEase(Tween.EaseType.InOut);
		_pulseTween.TweenProperty(_calloutPanel, "modulate", new Color(1.0f, 0.92f, 0.52f, 1.0f), 0.10f);
		_pulseTween.TweenProperty(_calloutPanel, "modulate", Colors.White, 0.16f);
	}

	public void RefreshLayout()
	{
		if (!Visible || !_hasTarget || !IsNodeReady())
		{
			return;
		}

		Rect2 paddedTarget = new(
			_targetRect.Position - TargetPadding,
			_targetRect.Size + TargetPadding * 2.0f);
		_targetOutline.Position = paddedTarget.Position;
		_targetOutline.Size = paddedTarget.Size;

		Vector2 viewportSize = GetViewportRect().Size;
		if (Size.X > 0.0f && Size.Y > 0.0f)
		{
			viewportSize = new Vector2(
				Mathf.Min(Size.X, viewportSize.X),
				Mathf.Min(Size.Y, viewportSize.Y));
		}
		Vector2 calloutSize = GetCalloutSize(viewportSize);
		_calloutPanel.Size = calloutSize;
		_calloutPanel.CustomMinimumSize = Vector2.Zero;
		LayoutCalloutContent(calloutSize);

		Vector2 targetCenter = _targetRect.GetCenter();
		Vector2 calloutPosition = GetCalloutPosition(calloutSize, viewportSize);
		_calloutPanel.Position = calloutPosition;

		Rect2 calloutRect = new(calloutPosition, calloutSize);
		Vector2 lineStart = GetClosestPointOnRect(calloutRect, targetCenter);
		_pointerLine.Points = new[] { lineStart, targetCenter };
	}

	private Vector2 GetCalloutSize(Vector2 viewportSize)
	{
		string text = _calloutLabel.Text ?? string.Empty;
		float maxTextWidth = _continueMode ? CalloutMaxTextWidth : PlayCalloutMaxTextWidth;
		float viewportTextWidth = Mathf.Max(
			CalloutMinTextWidth,
			viewportSize.X - ViewportPadding * 2.0f - CalloutHorizontalPadding * 2.0f);
		maxTextWidth = Mathf.Min(maxTextWidth, viewportTextWidth);
		float minTextWidth = _continueMode
			? CalloutMinTextWidth
			: Mathf.Min(PlayCalloutMinTextWidth, maxTextWidth);
		float naturalTextWidth = Mathf.Max(minTextWidth, GetTextWidth(_calloutLabel, text));
		float textWidth = Mathf.Min(naturalTextWidth, maxTextWidth);
		float textHeight = GetMultilineTextHeight(_calloutLabel, text, textWidth)
			+ CalloutTextHeightSafety;
		float width = textWidth + CalloutHorizontalPadding * 2.0f;
		float height = textHeight + CalloutVerticalPadding * 2.0f;

		if (_continueMode)
		{
			float buttonWidth = GetContinueButtonWidth();
			width = Mathf.Max(width, buttonWidth + CalloutHorizontalPadding * 2.0f);
			height += CalloutButtonGap + CalloutButtonHeight;
		}

		return new Vector2((float)Math.Ceiling(width), (float)Math.Ceiling(height));
	}

	private void LayoutCalloutContent(Vector2 calloutSize)
	{
		float textWidth = Mathf.Max(1.0f, calloutSize.X - CalloutHorizontalPadding * 2.0f);
		float textHeight = calloutSize.Y - CalloutVerticalPadding * 2.0f;
		if (_continueMode)
		{
			textHeight -= CalloutButtonGap + CalloutButtonHeight;
		}

		_calloutLabel.Position = new Vector2(CalloutHorizontalPadding, CalloutVerticalPadding);
		_calloutLabel.Size = new Vector2(textWidth, Mathf.Max(1.0f, textHeight));

		float buttonWidth = GetContinueButtonWidth();
		_continueButton.Size = new Vector2(buttonWidth, CalloutButtonHeight);
		_continueButton.Position = new Vector2(
			calloutSize.X - CalloutHorizontalPadding - buttonWidth,
			calloutSize.Y - CalloutVerticalPadding - CalloutButtonHeight);
	}

	private float GetContinueButtonWidth()
	{
		return Mathf.Max(56.0f, GetTextWidth(_continueButton, _continueButton.Text ?? string.Empty) + 26.0f);
	}

	private static float GetTextWidth(Control control, string text)
	{
		Font font = control.GetThemeFont("font");
		int fontSize = control.GetThemeFontSize("font_size");
		return font.GetStringSize(text, HorizontalAlignment.Left, -1.0f, fontSize).X;
	}

	private static float GetMultilineTextHeight(Label label, string text, float width)
	{
		Font font = label.GetThemeFont("font");
		int fontSize = label.GetThemeFontSize("font_size");
		float measuredHeight = font.GetMultilineStringSize(
			text,
			label.HorizontalAlignment,
			width,
			fontSize).Y;

		label.Size = new Vector2(width, 1.0f);
		return Mathf.Max(measuredHeight, label.GetCombinedMinimumSize().Y);
	}

	private Vector2 GetCalloutPosition(Vector2 calloutSize, Vector2 viewportSize)
	{
		if (_fixedCalloutCenter.HasValue)
		{
			return ClampCalloutPosition(
				_fixedCalloutCenter.Value - calloutSize * 0.5f,
				calloutSize,
				viewportSize);
		}

		Vector2 bestPosition = ClampCalloutPosition(
			GetCalloutCandidatePosition(_targetRect, calloutSize, _placementPreference[0]),
			calloutSize,
			viewportSize);
		float bestScore = float.MaxValue;
		Vector2 targetCenter = _targetRect.GetCenter();

		for (int index = 0; index < _placementPreference.Length; index++)
		{
			Vector2 position = ClampCalloutPosition(
				GetCalloutCandidatePosition(_targetRect, calloutSize, _placementPreference[index]),
				calloutSize,
				viewportSize);
			Rect2 calloutRect = new(position, calloutSize);
			float overlapArea = GetRectOverlapArea(calloutRect, _targetRect);
			float distanceScore = calloutRect.GetCenter().DistanceTo(targetCenter) * 0.01f;
			float score = overlapArea * 100.0f + index * 10.0f + distanceScore;
			if (score < bestScore)
			{
				bestScore = score;
				bestPosition = position;
			}
		}

		return bestPosition;
	}

	private static Vector2 GetCalloutCandidatePosition(
		Rect2 targetRect,
		Vector2 calloutSize,
		CalloutPlacement placement)
	{
		Vector2 targetCenter = targetRect.GetCenter();
		return placement switch
		{
			CalloutPlacement.Below => new Vector2(
				targetCenter.X - calloutSize.X * 0.5f,
				targetRect.End.Y + CalloutGap),
			CalloutPlacement.Left => new Vector2(
				targetRect.Position.X - calloutSize.X - CalloutGap,
				targetCenter.Y - calloutSize.Y * 0.5f),
			CalloutPlacement.Right => new Vector2(
				targetRect.End.X + CalloutGap,
				targetCenter.Y - calloutSize.Y * 0.5f),
			_ => new Vector2(
				targetCenter.X - calloutSize.X * 0.5f,
				targetRect.Position.Y - calloutSize.Y - CalloutGap),
		};
	}

	private static Vector2 ClampCalloutPosition(Vector2 position, Vector2 calloutSize, Vector2 viewportSize)
	{
		float maxX = Mathf.Max(ViewportPadding, viewportSize.X - calloutSize.X - ViewportPadding);
		float maxY = Mathf.Max(ViewportPadding, viewportSize.Y - calloutSize.Y - ViewportPadding);
		return new Vector2(
			Mathf.Clamp(position.X, ViewportPadding, maxX),
			Mathf.Clamp(position.Y, ViewportPadding, maxY));
	}

	private static float GetRectOverlapArea(Rect2 first, Rect2 second)
	{
		float left = Mathf.Max(first.Position.X, second.Position.X);
		float top = Mathf.Max(first.Position.Y, second.Position.Y);
		float right = Mathf.Min(first.End.X, second.End.X);
		float bottom = Mathf.Min(first.End.Y, second.End.Y);
		return right <= left || bottom <= top ? 0.0f : (right - left) * (bottom - top);
	}

	private static Vector2 GetClosestPointOnRect(Rect2 rect, Vector2 point)
	{
		return new Vector2(
			Mathf.Clamp(point.X, rect.Position.X, rect.End.X),
			Mathf.Clamp(point.Y, rect.Position.Y, rect.End.Y));
	}

	private void OnContinuePressed()
	{
		EmitSignal(SignalName.ContinueRequested);
	}
}
