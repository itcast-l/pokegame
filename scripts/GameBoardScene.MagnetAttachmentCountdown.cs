using System;
using Godot;

public partial class GameBoardScene
{
	private void ShowMagnetAttachmentCountdown()
	{
		ShowMandatoryActionCountdown(
			MagnetAttachmentSelectionSeconds,
			IsMainPlayerMagnetSelectionActive);
	}

	private void ShowMandatoryActionCountdown(double durationSeconds, Func<bool> isActionActive)
	{
		EnsureItemModeOptionalFxResourcesLoaded();
		ClearMagnetAttachmentCountdown();

		const float countdownWidth = 176.0f;
		const float countdownHeight = 172.0f;
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
			Text = Math.Max(0, (int)Math.Ceiling(durationSeconds)).ToString(),
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
		_mandatoryActionCountdownController = new MandatoryActionCountdownController(
			root,
			countdownLabel,
			hintLabel,
			durationSeconds,
			urgentProgressThreshold: 0.35,
			isActionActive);
		_mandatoryActionCountdownFrameRegistration =
			_frameScheduler.Register(UpdateMandatoryActionCountdown);
		_mandatoryActionCountdownController.StartIntro();
		RequestBoardFrameUpdate();
	}

	private void UpdateMandatoryActionCountdown(double delta)
	{
		MandatoryActionCountdownController? controller = _mandatoryActionCountdownController;
		if (controller == null)
		{
			return;
		}

		if (!controller.Update(delta) && ReferenceEquals(_mandatoryActionCountdownController, controller))
		{
			_mandatoryActionCountdownController = null;
			_mandatoryActionCountdownFrameRegistration?.Dispose();
			_mandatoryActionCountdownFrameRegistration = null;
		}
	}

	private void ClearMagnetAttachmentCountdown()
	{
		_mandatoryActionCountdownFrameRegistration?.Dispose();
		_mandatoryActionCountdownFrameRegistration = null;
		_mandatoryActionCountdownController?.Dispose();
		_mandatoryActionCountdownController = null;
		_debugMandatoryActionCountdownProbeActive = false;
		RefreshBoardProcessState();
	}

	public bool DebugStartMandatoryActionCountdownProbe(double durationSeconds = 1.0)
	{
		if (durationSeconds < 0.25 || durationSeconds > 5.0)
		{
			return false;
		}

		ShowMandatoryActionCountdown(durationSeconds, IsDebugMandatoryActionCountdownProbeActive);
		_debugMandatoryActionCountdownProbeActive = true;
		RequestBoardFrameUpdate();
		return _mandatoryActionCountdownController?.IsActive == true;
	}

	public void DebugStopMandatoryActionCountdownProbe()
	{
		ClearMagnetAttachmentCountdown();
	}

	private bool IsDebugMandatoryActionCountdownProbeActive()
	{
		return _debugMandatoryActionCountdownProbeActive;
	}

	public Godot.Collections.Dictionary DebugGetMandatoryActionCountdownState()
	{
		MandatoryActionCountdownController? controller = _mandatoryActionCountdownController;
		return new Godot.Collections.Dictionary
		{
			["active"] = controller?.IsActive == true,
			["display_seconds"] = controller?.DisplaySeconds ?? -1,
			["rendered_text"] = controller?.RenderedText ?? string.Empty,
			["urgent"] = controller?.IsUrgent == true,
			["text_write_count"] = controller?.TextWriteCount ?? 0,
			["urgent_state_write_count"] = controller?.UrgentStateWriteCount ?? 0,
			["last_tick_allocated_bytes"] = controller?.LastTickManagedAllocationBytes ?? 0L,
			["max_stable_tick_allocated_bytes"] = controller?.MaxStableTickManagedAllocationBytes ?? 0L,
		};
	}

	private Texture2D? GetAttachmentSelectionPreviewTexture()
	{
		string previewItemCardId = AttachmentSelectionPreviewRules.GetPreviewItemCardId(_pendingItemCardId);
		return _itemCardTextures.TryGetValue(previewItemCardId, out Texture2D? texture)
			? texture
			: null;
	}
}
