using Godot;

public partial class GameBoardScene
{
	private void ShowMagnetAttachmentCountdown()
	{
		ClearMagnetAttachmentCountdown();

		const float countdownWidth = 176.0f;
		const float countdownHeight = 172.0f;
		_magnetAttachmentCountdownRemainingSeconds = MagnetAttachmentSelectionSeconds;
		_magnetAttachmentCountdownHintIsUrgent = false;

		var root = new Control
		{
			Name = "MagnetAttachmentCountdown",
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -countdownWidth / 2.0f,
			OffsetTop = -countdownHeight / 2.0f,
			OffsetRight = countdownWidth / 2.0f,
			OffsetBottom = countdownHeight / 2.0f,
			PivotOffset = new Vector2(countdownWidth, countdownHeight) / 2.0f,
			ZIndex = 1900,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};

		if (_magnetCountdownFrameTextures.Count > 0)
		{
			var countdownFrame = new TextureRect
			{
				Name = "MagnetAttachmentCountdownFrame",
				Texture = _magnetCountdownFrameTextures[0],
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
				Position = new Vector2(10.0f, -2.0f),
				Size = new Vector2(156.0f, 156.0f),
				PivotOffset = new Vector2(78.0f, 78.0f),
				ZIndex = 0,
			};
			root.AddChild(countdownFrame);
		}

		var ring = new MagnetAttachmentCountdownRing
		{
			Name = "MagnetAttachmentCountdownRing",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(24.0f, 10.0f),
			Size = new Vector2(128.0f, 128.0f),
			ZIndex = 1,
			countdown_progress = 1.0f,
		};
		root.AddChild(ring);

		var countdownLabel = new Label
		{
			Name = "MagnetAttachmentCountdownLabel",
			Text = Mathf.CeilToInt((float)MagnetAttachmentSelectionSeconds).ToString(),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = ring.Position,
			Size = ring.Size,
			ZIndex = 2,
		};
		countdownLabel.AddThemeFontSizeOverride("font_size", 46);
		countdownLabel.AddThemeColorOverride("font_color", Colors.White);
		countdownLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.08f, 0.12f, 0.72f));
		countdownLabel.AddThemeConstantOverride("shadow_offset_x", 0);
		countdownLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		root.AddChild(countdownLabel);

		var hintLabel = new Label
		{
			Name = "MagnetAttachmentCountdownHint",
			Text = GetMagnetAttachmentCountdownHint(),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(0.0f, 146.0f),
			Size = new Vector2(countdownWidth, 22.0f),
			ZIndex = 2,
		};
		hintLabel.AddThemeFontSizeOverride("font_size", 16);
		hintLabel.AddThemeColorOverride("font_color", new Color(0.82f, 1.0f, 0.98f, 0.95f));
		hintLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.08f, 0.12f, 0.72f));
		hintLabel.AddThemeConstantOverride("shadow_offset_x", 0);
		hintLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		root.AddChild(hintLabel);

		AddChild(root);
		_magnetAttachmentCountdownRoot = root;
		_magnetAttachmentCountdownRing = ring;
		_magnetAttachmentCountdownLabel = countdownLabel;
		_magnetAttachmentCountdownHintLabel = hintLabel;

		Tween introTween = CreateTween();
		introTween.SetTrans(Tween.TransitionType.Cubic);
		introTween.SetEase(Tween.EaseType.Out);
		introTween.TweenProperty(root, "modulate:a", 1.0f, 0.16f);
	}

	private void UpdateMagnetAttachmentCountdown(double delta)
	{
		if (_magnetAttachmentCountdownRoot == null || !GodotObject.IsInstanceValid(_magnetAttachmentCountdownRoot))
		{
			return;
		}

		if (!IsMainPlayerMagnetSelectionActive())
		{
			ClearMagnetAttachmentCountdown();
			return;
		}

		_magnetAttachmentCountdownRemainingSeconds = Mathf.Max(
			0.0f,
			(float)(_magnetAttachmentCountdownRemainingSeconds - delta));
		float progress = Mathf.Clamp(
			(float)(_magnetAttachmentCountdownRemainingSeconds / MagnetAttachmentSelectionSeconds),
			0.0f,
			1.0f);
		int displaySeconds = progress <= 0.0f
			? 0
			: Mathf.CeilToInt((float)_magnetAttachmentCountdownRemainingSeconds);

		if (_magnetAttachmentCountdownLabel != null && GodotObject.IsInstanceValid(_magnetAttachmentCountdownLabel))
		{
			_magnetAttachmentCountdownLabel.Text = displaySeconds.ToString();
			_magnetAttachmentCountdownLabel.AddThemeColorOverride(
				"font_color",
				progress <= 0.35f ? new Color(1.0f, 0.86f, 0.42f) : Colors.White);
		}

		UpdateMagnetAttachmentCountdownHintPulse(progress <= 0.35f);

		if (_magnetAttachmentCountdownRing != null && GodotObject.IsInstanceValid(_magnetAttachmentCountdownRing))
		{
			_magnetAttachmentCountdownRing.countdown_progress = progress;
		}
	}

	private void ClearMagnetAttachmentCountdown()
	{
		KillMagnetAttachmentCountdownHintTween();

		if (_magnetAttachmentCountdownRoot != null && GodotObject.IsInstanceValid(_magnetAttachmentCountdownRoot))
		{
			RemoveChild(_magnetAttachmentCountdownRoot);
			_magnetAttachmentCountdownRoot.QueueFree();
		}

		_magnetAttachmentCountdownRoot = null;
		_magnetAttachmentCountdownRing = null;
		_magnetAttachmentCountdownLabel = null;
		_magnetAttachmentCountdownHintLabel = null;
		_magnetAttachmentCountdownRemainingSeconds = 0.0;
		_magnetAttachmentCountdownHintIsUrgent = false;
	}

	private void UpdateMagnetAttachmentCountdownHintPulse(bool isUrgent)
	{
		if (_magnetAttachmentCountdownHintLabel == null || !GodotObject.IsInstanceValid(_magnetAttachmentCountdownHintLabel))
		{
			return;
		}

		if (_magnetAttachmentCountdownHintIsUrgent == isUrgent)
		{
			return;
		}

		KillMagnetAttachmentCountdownHintTween();
		_magnetAttachmentCountdownHintIsUrgent = isUrgent;
		if (!isUrgent)
		{
			_magnetAttachmentCountdownHintLabel.Modulate = Colors.White;
			return;
		}

		_magnetAttachmentCountdownHintLabel.Modulate = Colors.White;
		Tween pulseTween = CreateTween();
		pulseTween.SetLoops();
		pulseTween.SetTrans(Tween.TransitionType.Cubic);
		pulseTween.SetEase(Tween.EaseType.InOut);
		pulseTween.TweenProperty(_magnetAttachmentCountdownHintLabel, "modulate:a", 0.78f, 0.16f);
		pulseTween.TweenProperty(_magnetAttachmentCountdownHintLabel, "modulate:a", 1.0f, 0.16f);
		_magnetAttachmentCountdownHintTween = pulseTween;
	}

	private void KillMagnetAttachmentCountdownHintTween()
	{
		if (_magnetAttachmentCountdownHintTween != null && GodotObject.IsInstanceValid(_magnetAttachmentCountdownHintTween))
		{
			_magnetAttachmentCountdownHintTween.Kill();
		}

		_magnetAttachmentCountdownHintTween = null;
	}

	private partial class MagnetAttachmentCountdownRing : Control
	{
		private float _countdownProgress = 1.0f;
		public float countdown_progress
		{
			get => _countdownProgress;
			set
			{
				_countdownProgress = Mathf.Clamp(value, 0.0f, 1.0f);
				QueueRedraw();
			}
		}

		public override void _Draw()
		{
			Vector2 center = Size / 2.0f;
			float radius = Mathf.Min(Size.X, Size.Y) * 0.36f;
			bool urgent = _countdownProgress <= 0.35f;
			Color accentColor = urgent
				? new Color(1.0f, 0.76f, 0.24f, 0.95f)
				: new Color(0.58f, 1.0f, 0.96f, 0.95f);

			DrawArc(center, radius + 7.0f, 0.0f, FullCircleRadians, 96, new Color(0.76f, 1.0f, 0.96f, 0.24f), 5.0f, true);

			if (_countdownProgress > 0.0f)
			{
				float startAngle = -Mathf.Pi / 2.0f;
				float endAngle = startAngle + FullCircleRadians * _countdownProgress;
				int pointCount = Mathf.Max(12, Mathf.CeilToInt(96.0f * _countdownProgress));
				DrawArc(center, radius + 7.0f, startAngle, endAngle, pointCount, accentColor, urgent ? 7.0f : 6.0f, true);
			}

			DrawArc(center, radius - 8.0f, 0.0f, FullCircleRadians, 96, new Color(1.0f, 1.0f, 1.0f, 0.30f), 2.0f, true);
		}
	}
}
