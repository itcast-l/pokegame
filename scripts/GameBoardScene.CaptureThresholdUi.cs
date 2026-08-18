using System;
using System.Globalization;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private const float CaptureThresholdFeedbackSeconds = 1.2f;
	private const float CaptureThresholdViewportPadding = 8.0f;
	private const float CaptureRequirementWatermarkChangeAlpha = 0.72f;
	private const float CaptureRequirementWatermarkFadeSeconds = 0.18f;
	private static readonly Vector2 CaptureThresholdFeedbackSize = new(196.0f, 44.0f);

	private Label _captureRequirementWatermark = default!;
	private Tween? _captureRequirementWatermarkTween;
	private int _captureRequirementWatermarkValue = -1;
	private readonly Panel?[] _captureThresholdFeedbackPanels = new Panel?[MaxPlayers];
	private readonly Label?[] _captureThresholdFeedbackLabels = new Label?[MaxPlayers];
	private readonly Tween?[] _captureThresholdFeedbackTweens = new Tween?[MaxPlayers];

	private void InitializeCaptureRequirementWatermark()
	{
		_captureRequirementWatermarkTween?.Kill();
		_captureRequirementWatermarkTween = null;
		_captureRequirementWatermarkValue = -1;
		_captureRequirementWatermark.Visible = false;
		_captureRequirementWatermark.Modulate = Colors.White;
	}

	private void RefreshCaptureRequirementWatermark(bool animateChange = true)
	{
		if (_captureRequirementWatermark == null
			|| !GodotObject.IsInstanceValid(_captureRequirementWatermark))
		{
			return;
		}

		if (CurrentRuleReadModel is not MatchReadModel readModel
			|| !readModel.IsInitialized
			|| IsGameOver()
			|| _isLeavingGame)
		{
			_captureRequirementWatermarkTween?.Kill();
			_captureRequirementWatermarkTween = null;
			_captureRequirementWatermarkValue = -1;
			_captureRequirementWatermark.Visible = false;
			return;
		}

		int requiredCaptureCount = ContinuousCaptureRules.GetMinimumMiddleTableCaptureCount(
			readModel.BasePlayCaptureThreshold);
		bool valueChanged = _captureRequirementWatermarkValue >= 0
			&& _captureRequirementWatermarkValue != requiredCaptureCount;
		_captureRequirementWatermarkValue = requiredCaptureCount;
		_captureRequirementWatermark.Text = requiredCaptureCount.ToString(CultureInfo.InvariantCulture);
		_captureRequirementWatermark.Visible = true;

		_captureRequirementWatermarkTween?.Kill();
		_captureRequirementWatermarkTween = null;
		_captureRequirementWatermark.Modulate = Colors.White;
		if (!animateChange || !valueChanged)
		{
			return;
		}

		_captureRequirementWatermark.Modulate = new Color(
			1.0f,
			1.0f,
			1.0f,
			CaptureRequirementWatermarkChangeAlpha);
		Tween tween = CreateBoardTween();
		_captureRequirementWatermarkTween = tween;
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(
			_captureRequirementWatermark,
			"modulate:a",
			1.0f,
			CaptureRequirementWatermarkFadeSeconds);
	}

	private void CreateCaptureThresholdFeedbackPanels()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Panel feedback = CreateCaptureThresholdPanel(
				$"Player{playerIndex + 1}CaptureThresholdFeedback",
				CaptureThresholdFeedbackSize,
				CreateCaptureThresholdFeedbackStyle(bigTide: false));
			feedback.ZIndex = 2060;
			feedback.Visible = false;
			Label feedbackLabel = CreateCaptureThresholdLabel(fontSize: 12);
			feedbackLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			feedbackLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			feedback.AddChild(feedbackLabel);
			AddChild(feedback);
			_captureThresholdFeedbackPanels[playerIndex] = feedback;
			_captureThresholdFeedbackLabels[playerIndex] = feedbackLabel;
		}

		UpdateCaptureThresholdFeedbackLayout();
	}

	private static Panel CreateCaptureThresholdPanel(
		string name,
		Vector2 size,
		StyleBoxFlat style)
	{
		var panel = new Panel
		{
			Name = name,
			CustomMinimumSize = size,
			Size = size,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	private static Label CreateCaptureThresholdLabel(int fontSize)
	{
		var label = new Label
		{
			Name = "CaptureThresholdLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.SetAnchorsPreset(LayoutPreset.FullRect);
		label.AddThemeColorOverride("font_color", new Color(0.94f, 1.0f, 1.0f, 1.0f));
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private void UpdateCaptureThresholdFeedbackLayout()
	{
		Vector2 viewportSize = Size == Vector2.Zero ? GetViewportRect().Size : Size;
		Vector2 feedbackPosition = GlobalPosition
			+ (viewportSize - CaptureThresholdFeedbackSize) * 0.5f;
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			if (_captureThresholdFeedbackPanels[playerIndex] is Panel feedback)
			{
				feedback.GlobalPosition = ClampCaptureThresholdPanelPosition(
					feedbackPosition,
					CaptureThresholdFeedbackSize,
					viewportSize);
			}
		}
	}

	private static Vector2 ClampCaptureThresholdPanelPosition(
		Vector2 position,
		Vector2 panelSize,
		Vector2 viewportSize)
	{
		return new Vector2(
			Mathf.Clamp(
				position.X,
				CaptureThresholdViewportPadding,
				Mathf.Max(CaptureThresholdViewportPadding, viewportSize.X - panelSize.X - CaptureThresholdViewportPadding)),
			Mathf.Clamp(
				position.Y,
				CaptureThresholdViewportPadding,
				Mathf.Max(CaptureThresholdViewportPadding, viewportSize.Y - panelSize.Y - CaptureThresholdViewportPadding)));
	}

	private void ShowCaptureThresholdFeedback(
		int playerIndex,
		bool bigTide,
		int count)
	{
		if (playerIndex < 0
			|| playerIndex >= player_count
			|| _captureThresholdFeedbackPanels[playerIndex] is not Panel panel
			|| _captureThresholdFeedbackLabels[playerIndex] is not Label label)
		{
			return;
		}
		if (playerIndex != GetLocalHandPlayerIndex())
		{
			_captureThresholdFeedbackTweens[playerIndex]?.Kill();
			panel.Visible = false;
			return;
		}

		_captureThresholdFeedbackTweens[playerIndex]?.Kill();
		panel.AddThemeStyleboxOverride("panel", CreateCaptureThresholdFeedbackStyle(bigTide));
		label.Text = bigTide
			? Localize("GAME_CAPTURE_THRESHOLD_BIG_TIDE")
			: LocalizeFormat("GAME_CAPTURE_THRESHOLD_BLOCKED", count);
		panel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		panel.Visible = true;
		UpdateCaptureThresholdFeedbackLayout();

		Tween tween = CreateBoardTween();
		_captureThresholdFeedbackTweens[playerIndex] = tween;
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(panel, "modulate:a", 1.0f, 0.12f);
		if (bigTide)
		{
			tween.TweenProperty(panel, "modulate", new Color(0.72f, 1.0f, 1.0f, 1.0f), 0.16f);
			tween.TweenProperty(panel, "modulate", Colors.White, 0.16f);
		}
		tween.TweenInterval(Math.Max(0.0f, CaptureThresholdFeedbackSeconds - 0.52f));
		tween.TweenProperty(panel, "modulate:a", 0.0f, 0.12f);
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(panel))
			{
				panel.Visible = false;
			}
		}));
	}

	private void PublishCaptureThresholdFeedback(
		int playerIndex,
		bool bigTide,
		int count,
		int turnSequenceGeneration)
	{
		MarkActivePlayerStatusTooltipForRefresh();
		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
		{
			GetNetworkManager()?.BroadcastCaptureThresholdFeedbackFromHost(
				playerIndex,
				bigTide,
				count,
				turnSequenceGeneration,
				snapshotId);
			return;
		}

		ShowCaptureThresholdFeedback(playerIndex, bigTide, count);
	}

	private static StyleBoxFlat CreateCaptureThresholdStyle(Color background, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6,
		};
	}

	private static StyleBoxFlat CreateCaptureThresholdFeedbackStyle(bool bigTide) =>
		bigTide
			? CreateCaptureThresholdStyle(new Color(0.00f, 0.24f, 0.27f, 0.96f), new Color(0.32f, 1.0f, 0.95f, 0.95f))
			: CreateCaptureThresholdStyle(new Color(0.28f, 0.15f, 0.01f, 0.96f), new Color(1.0f, 0.68f, 0.20f, 0.96f));
}
