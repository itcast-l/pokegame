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

		Texture2D? previewTexture = GetAttachmentSelectionPreviewTexture();
		if (previewTexture != null)
		{
			var preview = new TextureRect
			{
				Name = "MagnetAttachmentCountdownPreview",
				Texture = previewTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
				Position = new Vector2(111.0f, -12.0f),
				Size = new Vector2(58.0f, 58.0f),
				PivotOffset = new Vector2(29.0f, 29.0f),
				RotationDegrees = 7.0f,
				ZIndex = 2,
			};
			root.AddChild(preview);
		}

		var countdownLabel = new Label
		{
			Name = "MagnetAttachmentCountdownLabel",
			Text = Mathf.CeilToInt((float)MagnetAttachmentSelectionSeconds).ToString(),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(24.0f, 10.0f),
			Size = new Vector2(128.0f, 128.0f),
			ZIndex = 1,
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

	private Texture2D? GetAttachmentSelectionPreviewTexture()
	{
		string previewItemCardId = AttachmentSelectionPreviewRules.GetPreviewItemCardId(_pendingItemCardId);
		return _itemCardTextures.TryGetValue(previewItemCardId, out Texture2D? texture)
			? texture
			: null;
	}
}
