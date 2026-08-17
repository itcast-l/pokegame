using Godot;

public partial class AdventureRunResumeDialog : Control
{
	private const float PanelWidth = 704.0f;
	private const float PanelHeight = 360.0f;
	private const float ButtonHeight = 54.0f;
	private const float ButtonMinWidth = 208.0f;
	private const float ButtonHorizontalPadding = 64.0f;
	private const float ButtonRowWidth = 624.0f;
	private const int TitleFontSize = 30;
	private const int TitleMinFontSize = 24;
	private const int ButtonFontSize = 20;
	private const int ButtonMinFontSize = 16;
	private const float ViewportMargin = 24.0f;

	[Signal]
	public delegate void ContinueRequestedEventHandler();

	[Signal]
	public delegate void RestartRequestedEventHandler();

	[Signal]
	public delegate void CancelRequestedEventHandler();

	private Control _dialogMotion = default!;
	private Control _panel = default!;
	private ColorRect _scrim = default!;
	private Label _titleLabel = default!;
	private OceanMenuButtonFrame _continueFrame = default!;
	private OceanMenuButtonFrame _restartFrame = default!;
	private OceanMenuButtonFrame _cancelFrame = default!;
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
		_scrim = GetNode<ColorRect>("Scrim");
		_titleLabel = GetNode<Label>("DialogMotion/Panel/ContentMargin/Layout/TitleLabel");
		_continueFrame = GetNode<OceanMenuButtonFrame>("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/ContinueFrame");
		_restartFrame = GetNode<OceanMenuButtonFrame>("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/RestartFrame");
		_cancelFrame = GetNode<OceanMenuButtonFrame>("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/CancelFrame");
		_continueButton = GetNode<Button>("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/ContinueFrame/ContinueButton");
		_restartButton = GetNode<Button>("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/RestartFrame/RestartButton");
		_cancelButton = GetNode<Button>("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/CancelFrame/CancelButton");

		ConfigureVisualStyle();
		ConfigureButton(_continueButton, MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
		ConfigureButton(_restartButton, MainMenuInteractiveStyle.DangerPalette);
		ConfigureButton(_cancelButton, MainMenuInteractiveStyle.SecondaryMenuPalette);
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
		string continueText,
		string restartText,
		string cancelText)
	{
		_titleLabel.Text = title ?? string.Empty;
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

	public void SetActionsDisabled(bool disabled)
	{
		_continueButton.Disabled = disabled;
		_restartButton.Disabled = disabled;
		_cancelButton.Disabled = disabled;
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
		ApplyButtonSizing();
		RefreshResponsiveScale();
	}

	private void ConfigureVisualStyle()
	{
		SetMeta(SecondaryMenuGlassStyle.VisualProfileMetadata, SecondaryMenuGlassStyle.UnifiedVisual.Id);
		SecondaryMenuGlassStyle.ApplyReadabilityVeil(_scrim, SecondaryMenuGlassStyle.UnifiedVisual);
		SecondaryMenuGlassStyle.StyleGlassPanel(_panel, SecondaryMenuGlassStyle.UnifiedVisual, contentPane: false);

		ConfigureFrame(
			_continueFrame,
			MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette.ActiveBorder,
			selected: true);
		ConfigureFrame(
			_restartFrame,
			MainMenuInteractiveStyle.DangerPalette.ActiveBorder,
			selected: false);
		ConfigureFrame(
			_cancelFrame,
			MainMenuInteractiveStyle.SecondaryMenuPalette.ActiveBorder,
			selected: false);
	}

	private static void ConfigureFrame(OceanMenuButtonFrame frame, Color accent, bool selected)
	{
		frame.FrameStyle = OceanMenuButtonFrame.ButtonFrameStyle.IndividualAction;
		frame.AccentColor = accent;
		frame.Selected = selected;
		frame.HighlightFocus = true;
		frame.UsePlaqueGlowBackdrop = false;
		frame.UseToolRowIdleBackdrop = false;
		frame.ShowIdleTexture = false;
		frame.SuppressVisuals = false;
		frame.QueueRedraw();
	}

	private static void ConfigureButton(Button button, MainMenuInteractiveStyle.Palette palette)
	{
		MainMenuInteractiveStyle.ApplyActionButton(button, ButtonFontSize, palette);
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
			button.AddThemeFontSizeOverride("font_size", resolvedFontSize);
			MainMenuInteractiveStyle.ApplyUnifiedButtonText(button);
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
