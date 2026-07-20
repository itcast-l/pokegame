using Godot;

public partial class AdventureRunResumeDialog : Control
{
	private const float PanelWidth = 744.0f;
	private const float PanelHeight = 480.0f;
	private const float ButtonHeight = 48.0f;
	private const float ButtonMinWidth = 208.0f;
	private const float ButtonHorizontalPadding = 76.0f;
	private const float ButtonRowWidth = 576.0f;
	private const int TitleFontSize = 34;
	private const int TitleMinFontSize = 26;
	private const int BodyFontSize = 18;
	private const int BodyMinFontSize = 16;
	private const int ButtonFontSize = 20;
	private const int ButtonMinFontSize = 16;
	private const float ViewportMargin = 18.0f;

	[Signal]
	public delegate void ContinueRequestedEventHandler();

	[Signal]
	public delegate void RestartRequestedEventHandler();

	[Signal]
	public delegate void CancelRequestedEventHandler();

	private Control _dialogMotion = default!;
	private Control _panel = default!;
	private Label _titleLabel = default!;
	private Label _bodyLabel = default!;
	private OceanMenuButtonFrame _continueFrame = default!;
	private Button _continueButton = default!;
	private Button _restartButton = default!;
	private Button _cancelButton = default!;
	private Tween? _motionTween;
	private bool _preferRestartFocus;

	public bool IsDialogVisible => Visible;

	public override void _Ready()
	{
		_dialogMotion = GetNode<Control>("DialogMotion");
		_panel = GetNode<Control>("DialogMotion/Panel");
		_titleLabel = GetNode<Label>("DialogMotion/Panel/ContentMargin/Layout/TitleLabel");
		_bodyLabel = GetNode<Label>("DialogMotion/Panel/ContentMargin/Layout/BodyLabel");
		_continueFrame = GetNode<OceanMenuButtonFrame>("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/ContinueFrame");
		_continueButton = GetNode<Button>("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/ContinueFrame/ContinueButton");
		_restartButton = GetNode<Button>("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/RestartFrame/RestartButton");
		_cancelButton = GetNode<Button>("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/CancelFrame/CancelButton");

		ConfigureButton(_continueButton);
		ConfigureButton(_restartButton);
		ConfigureButton(_cancelButton);
		_continueButton.Pressed += () => EmitSignal(SignalName.ContinueRequested);
		_restartButton.Pressed += () => EmitSignal(SignalName.RestartRequested);
		_cancelButton.Pressed += () => EmitSignal(SignalName.CancelRequested);
		Resized += RefreshResponsiveScale;
		RefreshResponsiveScale();
	}

	public override void _ExitTree()
	{
		_motionTween?.Kill();
		_motionTween = null;
	}

	public void Configure(
		bool recoveryMode,
		string title,
		string body,
		string continueText,
		string restartText,
		string cancelText)
	{
		_titleLabel.Text = title ?? string.Empty;
		_bodyLabel.Text = body ?? string.Empty;
		_continueButton.Text = continueText ?? string.Empty;
		_restartButton.Text = restartText ?? string.Empty;
		_cancelButton.Text = cancelText ?? string.Empty;
		_continueFrame.Visible = !recoveryMode;
		_preferRestartFocus = recoveryMode;
		RefreshLayout();
	}

	public void ShowDialog(bool focusDefault)
	{
		_motionTween?.Kill();
		Visible = true;
		MouseFilter = MouseFilterEnum.Stop;
		_dialogMotion.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_motionTween = CreateTween();
		_motionTween.SetTrans(Tween.TransitionType.Sine);
		_motionTween.SetEase(Tween.EaseType.Out);
		_motionTween.TweenProperty(_dialogMotion, "modulate:a", 1.0f, 0.18f);
		RefreshResponsiveScale();
		if (focusDefault)
		{
			CallDeferred(nameof(FocusDefaultButton));
		}
	}

	public void HideDialog()
	{
		_motionTween?.Kill();
		_motionTween = null;
		_dialogMotion.Modulate = Colors.White;
		_continueButton.ReleaseFocus();
		_restartButton.ReleaseFocus();
		_cancelButton.ReleaseFocus();
		Visible = false;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void FocusDefaultButton()
	{
		Button target = _preferRestartFocus ? _restartButton : _continueButton;
		if (GodotObject.IsInstanceValid(target) && target.IsVisibleInTree() && !target.Disabled)
		{
			target.GrabFocus();
		}
	}

	public void FocusContinueButton()
	{
		if (GodotObject.IsInstanceValid(_continueButton)
			&& _continueButton.IsVisibleInTree()
			&& !_continueButton.Disabled)
		{
			_continueButton.GrabFocus();
		}
	}

	public void RefreshLayout()
	{
		ApplyLabelSizing(_titleLabel, TitleFontSize, TitleMinFontSize, 2.4f);
		ApplyLabelSizing(_bodyLabel, BodyFontSize, BodyMinFontSize, 3.8f);
		ApplyButtonSizing();
		RefreshResponsiveScale();
	}

	private static void ConfigureButton(Button button)
	{
		MainMenuInteractiveStyle.ApplyActionButton(button, ButtonFontSize);
		button.SetMeta(GameAudioManager.UiSuppressPressedSfxMeta, true);
	}

	private void RefreshResponsiveScale()
	{
		if (_panel == null || !GodotObject.IsInstanceValid(_panel))
		{
			return;
		}

		Vector2 viewportSize = Size;
		if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
		{
			viewportSize = GetViewportRect().Size;
		}
		float availableWidth = Mathf.Max(1.0f, viewportSize.X - ViewportMargin * 2.0f);
		float availableHeight = Mathf.Max(1.0f, viewportSize.Y - ViewportMargin * 2.0f);
		float scale = Mathf.Min(
			1.0f,
			Mathf.Min(availableWidth / PanelWidth, availableHeight / PanelHeight));
		_panel.PivotOffset = new Vector2(PanelWidth * 0.5f, PanelHeight * 0.5f);
		_panel.Scale = Vector2.One * scale;
	}

	private static void ApplyLabelSizing(Label label, int maxFontSize, int minFontSize, float estimatedLineLimit)
	{
		string text = label.Text?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(text))
		{
			label.AddThemeFontSizeOverride("font_size", maxFontSize);
			return;
		}

		Font font = label.GetThemeFont("font");
		int resolvedSize = minFontSize;
		for (int fontSize = maxFontSize; fontSize >= minFontSize; fontSize--)
		{
			float singleLineWidth = font.GetStringSize(text, label.HorizontalAlignment, -1.0f, fontSize).X;
			if (singleLineWidth <= ButtonRowWidth * estimatedLineLimit)
			{
				resolvedSize = fontSize;
				break;
			}
		}

		label.AddThemeFontSizeOverride("font_size", resolvedSize);
	}

	private void ApplyButtonSizing()
	{
		Button[] buttons = { _continueButton, _restartButton, _cancelButton };
		Color[] baseColors =
		{
			new(0.96f, 1.0f, 0.98f, 1.0f),
			new(0.78f, 0.96f, 0.96f, 0.98f),
			new(0.84f, 0.90f, 0.92f, 0.92f),
		};
		Color[] hoverColors =
		{
			new(1.0f, 0.98f, 0.84f, 1.0f),
			new(0.96f, 1.0f, 0.90f, 1.0f),
			new(0.96f, 1.0f, 0.94f, 1.0f),
		};

		int widestTextWidth = 0;
		int resolvedFontSize = ButtonFontSize;
		foreach (Button candidate in buttons)
		{
			resolvedFontSize = Mathf.Min(
				resolvedFontSize,
				ResolveButtonFontSize(candidate.Text ?? string.Empty, candidate));
		}
		foreach (Button candidate in buttons)
		{
			widestTextWidth = Mathf.Max(
				widestTextWidth,
				Mathf.CeilToInt(MeasureButtonTextWidth(candidate, candidate.Text ?? string.Empty, resolvedFontSize)));
		}

		float preferredWidth = Mathf.Clamp(
			widestTextWidth + ButtonHorizontalPadding,
			ButtonMinWidth,
			ButtonRowWidth);
		Vector2 minimumSize = new(preferredWidth, ButtonHeight);
		for (int index = 0; index < buttons.Length; index++)
		{
			Button button = buttons[index];
			Color baseFontColor = baseColors[index];
			Color hoverFontColor = hoverColors[index];
			button.AddThemeFontSizeOverride("font_size", resolvedFontSize);
			button.AddThemeColorOverride("font_color", baseFontColor);
			button.AddThemeColorOverride(
				"font_pressed_color",
				baseFontColor.Lerp(Colors.White, index == 0 ? 0.10f : 0.05f));
			button.AddThemeColorOverride("font_hover_color", hoverFontColor);
			button.AddThemeColorOverride("font_focus_color", hoverFontColor);
			button.CustomMinimumSize = minimumSize;
			if (button.GetParent() is Control frame)
			{
				frame.CustomMinimumSize = minimumSize;
			}
		}
	}

	private static int ResolveButtonFontSize(string text, Button button)
	{
		for (int fontSize = ButtonFontSize; fontSize >= ButtonMinFontSize; fontSize--)
		{
			if (MeasureButtonTextWidth(button, text, fontSize) + ButtonHorizontalPadding <= ButtonRowWidth)
			{
				return fontSize;
			}
		}
		return ButtonMinFontSize;
	}

	private static float MeasureButtonTextWidth(Button button, string text, int fontSize)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0.0f;
		}
		return button.GetThemeFont("font").GetStringSize(
			text,
			HorizontalAlignment.Center,
			-1.0f,
			fontSize).X;
	}
}
