using System;
using Godot;

public partial class GameBoardScene
{
	private const float PlayerStatusTooltipDelaySeconds = 0.18f;
	private const float PlayerStatusTooltipFadeSeconds = 0.12f;
	private const float PlayerStatusTooltipGap = 10.0f;
	private const float PlayerStatusTooltipViewportPadding = 14.0f;
	private static readonly Vector2 PlayerStatusTooltipSize = new(172.0f, 90.0f);
	private static readonly Vector2 PlayerStatusTooltipBarSize = new(148.0f, 6.0f);

	private readonly int[] _playerStatusTooltipHoverDepths = new int[MaxPlayers];
	private Panel? _playerStatusTooltipPanel;
	private Label? _playerStatusTooltipTitleLabel;
	private Label? _playerStatusTooltipValueLabel;
	private Label? _playerStatusTooltipAdventureStatusLabel;
	private Panel? _playerStatusTooltipBarTrack;
	private ColorRect? _playerStatusTooltipBarFill;
	private Godot.Timer? _playerStatusTooltipDelayTimer;
	private Tween? _playerStatusTooltipTween;
	private int _pendingStatusTooltipPlayerIndex = -1;
	private int _activeStatusTooltipPlayerIndex = -1;

	private void CreatePlayerStatusTooltip()
	{
		_playerStatusTooltipPanel = new Panel
		{
			Name = "PlayerStatusTooltip",
			CustomMinimumSize = PlayerStatusTooltipSize,
			Size = PlayerStatusTooltipSize,
			Visible = false,
			ZIndex = 2600,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		_playerStatusTooltipPanel.AddThemeStyleboxOverride("panel", CreatePlayerStatusTooltipPanelStyle());
		AddChild(_playerStatusTooltipPanel);

		var content = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		content.SetAnchorsPreset(LayoutPreset.FullRect);
		content.OffsetLeft = 12.0f;
		content.OffsetTop = 8.0f;
		content.OffsetRight = -12.0f;
		content.OffsetBottom = -8.0f;
		content.AddThemeConstantOverride("separation", 4);
		_playerStatusTooltipPanel.AddChild(content);

		_playerStatusTooltipTitleLabel = new Label
		{
			MouseFilter = MouseFilterEnum.Ignore,
			ClipText = true,
		};
		_playerStatusTooltipTitleLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.98f, 1.0f, 0.95f));
		_playerStatusTooltipTitleLabel.AddThemeFontSizeOverride("font_size", 15);
		content.AddChild(_playerStatusTooltipTitleLabel);

		_playerStatusTooltipValueLabel = new Label
		{
			MouseFilter = MouseFilterEnum.Ignore,
			ClipText = true,
		};
		_playerStatusTooltipValueLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.93f, 1.0f, 0.92f));
		_playerStatusTooltipValueLabel.AddThemeFontSizeOverride("font_size", 13);
		content.AddChild(_playerStatusTooltipValueLabel);

		_playerStatusTooltipAdventureStatusLabel = new Label
		{
			MouseFilter = MouseFilterEnum.Ignore,
			ClipText = true,
			Visible = false,
		};
		_playerStatusTooltipAdventureStatusLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.96f, 0.72f, 0.94f));
		_playerStatusTooltipAdventureStatusLabel.AddThemeFontSizeOverride("font_size", 13);
		content.AddChild(_playerStatusTooltipAdventureStatusLabel);

		_playerStatusTooltipBarTrack = new Panel
		{
			CustomMinimumSize = PlayerStatusTooltipBarSize,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_playerStatusTooltipBarTrack.AddThemeStyleboxOverride("panel", CreatePlayerStatusTooltipBarTrackStyle());
		content.AddChild(_playerStatusTooltipBarTrack);

		_playerStatusTooltipBarFill = new ColorRect
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Position = Vector2.Zero,
			Size = PlayerStatusTooltipBarSize,
			Color = AvatarScoreGlowColors[0],
		};
		_playerStatusTooltipBarTrack.AddChild(_playerStatusTooltipBarFill);

		_playerStatusTooltipDelayTimer = new Godot.Timer
		{
			Name = "PlayerStatusTooltipDelayTimer",
			OneShot = true,
			WaitTime = PlayerStatusTooltipDelaySeconds,
		};
		_playerStatusTooltipDelayTimer.Timeout += ShowPendingPlayerStatusTooltip;
		AddChild(_playerStatusTooltipDelayTimer);
	}

	private void BindPlayerStatusTooltipHover(Control control, int playerIndex)
	{
		control.MouseFilter = MouseFilterEnum.Pass;
		control.TooltipText = string.Empty;
		control.MouseEntered += () => QueuePlayerStatusTooltip(playerIndex);
		control.MouseExited += () => ClearPlayerStatusTooltip(playerIndex);
	}

	private void QueuePlayerStatusTooltip(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= MaxPlayers)
		{
			return;
		}

		_playerStatusTooltipHoverDepths[playerIndex]++;
		_pendingStatusTooltipPlayerIndex = playerIndex;
		_playerStatusTooltipDelayTimer?.Start();
	}

	private void ClearPlayerStatusTooltip(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= MaxPlayers)
		{
			return;
		}

		_playerStatusTooltipHoverDepths[playerIndex] = Math.Max(0, _playerStatusTooltipHoverDepths[playerIndex] - 1);
		if (_playerStatusTooltipHoverDepths[playerIndex] > 0)
		{
			return;
		}

		if (_pendingStatusTooltipPlayerIndex == playerIndex)
		{
			_pendingStatusTooltipPlayerIndex = -1;
			_playerStatusTooltipDelayTimer?.Stop();
		}

		if (_activeStatusTooltipPlayerIndex == playerIndex)
		{
			HidePlayerStatusTooltip();
		}
	}

	private void ShowPendingPlayerStatusTooltip()
	{
		int playerIndex = _pendingStatusTooltipPlayerIndex;
		if (playerIndex < 0
			|| playerIndex >= MaxPlayers
			|| _playerStatusTooltipHoverDepths[playerIndex] <= 0
			|| playerIndex >= player_count)
		{
			return;
		}

		ShowPlayerStatusTooltip(playerIndex);
	}

	private void ShowPlayerStatusTooltip(int playerIndex)
	{
		if (_playerStatusTooltipPanel == null)
		{
			return;
		}

		_activeStatusTooltipPlayerIndex = playerIndex;
		MarkActivePlayerStatusTooltipContentDirty();
		MarkActivePlayerStatusTooltipLayoutDirty();
		FlushUiDirtyFlagsIfNeeded();
		_playerStatusTooltipPanel.Visible = true;

		_playerStatusTooltipTween?.Kill();
		_playerStatusTooltipTween = CreateTween();
		_playerStatusTooltipTween.SetTrans(Tween.TransitionType.Cubic);
		_playerStatusTooltipTween.SetEase(Tween.EaseType.Out);
		_playerStatusTooltipTween.TweenProperty(_playerStatusTooltipPanel, "modulate:a", 1.0f, PlayerStatusTooltipFadeSeconds);
	}

	private void HidePlayerStatusTooltip()
	{
		if (_playerStatusTooltipPanel == null)
		{
			return;
		}

		_activeStatusTooltipPlayerIndex = -1;
		_playerStatusTooltipTween?.Kill();
		_playerStatusTooltipTween = CreateTween();
		_playerStatusTooltipTween.SetTrans(Tween.TransitionType.Cubic);
		_playerStatusTooltipTween.SetEase(Tween.EaseType.Out);
		_playerStatusTooltipTween.TweenProperty(_playerStatusTooltipPanel, "modulate:a", 0.0f, PlayerStatusTooltipFadeSeconds);
		_playerStatusTooltipTween.TweenCallback(Callable.From(() =>
		{
			if (_playerStatusTooltipPanel != null)
			{
				_playerStatusTooltipPanel.Visible = false;
			}
		}));
	}

	private void RefreshPlayerStatusTooltip(int playerIndex)
	{
		if (_activeStatusTooltipPlayerIndex != playerIndex
			|| _playerStatusTooltipTitleLabel == null
			|| _playerStatusTooltipValueLabel == null
			|| _playerStatusTooltipAdventureStatusLabel == null)
		{
			return;
		}

		int scoreLimit = GetScoreLimit(playerIndex);
		int score = _playerScores[playerIndex];
		float progress = Mathf.Clamp((float)score / Math.Max(1, scoreLimit), 0.0f, 1.0f);

		_playerStatusTooltipTitleLabel.Text = GetPlayerDisplayName(playerIndex);
		string baseValueText = $"{GetPlayerStatusMetricLabel()}: {score}/{scoreLimit}";
		string seatControlStatusText = GetPlayerSeatControlStatusText(playerIndex);
		_playerStatusTooltipValueLabel.Text = string.IsNullOrWhiteSpace(seatControlStatusText)
			? baseValueText
			: $"{baseValueText}  {seatControlStatusText}";
		bool hasFlourish = TryGetAdventureClubFlourishStacks(
			playerIndex,
			out int flourishStacks);
		_playerStatusTooltipAdventureStatusLabel.Visible = hasFlourish;
		_playerStatusTooltipAdventureStatusLabel.Text = hasFlourish
			? LocalizeFormat("ADVENTURE_STATUS_FLOURISH", flourishStacks)
			: string.Empty;
		if (_playerStatusTooltipBarFill != null)
		{
			_playerStatusTooltipBarFill.Color = AvatarScoreGlowColors[playerIndex];
			_playerStatusTooltipBarFill.Size = new Vector2(PlayerStatusTooltipBarSize.X * progress, PlayerStatusTooltipBarSize.Y);
		}
	}

	private void UpdatePlayerStatusTooltipLayout()
	{
		if (_playerStatusTooltipPanel == null || _activeStatusTooltipPlayerIndex < 0)
		{
			return;
		}

		Rect2 targetRect = GetPlayerStatusTooltipTargetRect(_activeStatusTooltipPlayerIndex);
		Vector2 viewportSize = Size == Vector2.Zero ? GetViewportRect().Size : Size;
		Vector2 position = new(
			targetRect.Position.X + targetRect.Size.X + PlayerStatusTooltipGap,
			targetRect.Position.Y + (targetRect.Size.Y - PlayerStatusTooltipSize.Y) / 2.0f);

		if (position.X + PlayerStatusTooltipSize.X > viewportSize.X - PlayerStatusTooltipViewportPadding)
		{
			position.X = targetRect.Position.X - PlayerStatusTooltipSize.X - PlayerStatusTooltipGap;
		}

		position.X = Mathf.Clamp(
			position.X,
			PlayerStatusTooltipViewportPadding,
			Mathf.Max(PlayerStatusTooltipViewportPadding, viewportSize.X - PlayerStatusTooltipSize.X - PlayerStatusTooltipViewportPadding));
		position.Y = Mathf.Clamp(
			position.Y,
			PlayerStatusTooltipViewportPadding,
			Mathf.Max(PlayerStatusTooltipViewportPadding, viewportSize.Y - PlayerStatusTooltipSize.Y - PlayerStatusTooltipViewportPadding));

		_playerStatusTooltipPanel.GlobalPosition = position;
	}

	private void MarkActivePlayerStatusTooltipForRefresh()
	{
		if (_activeStatusTooltipPlayerIndex < 0)
		{
			return;
		}

		MarkActivePlayerStatusTooltipContentDirty();
		MarkActivePlayerStatusTooltipLayoutDirty();
	}

	private Rect2 GetPlayerStatusTooltipTargetRect(int playerIndex)
	{
		Rect2 targetRect = new();
		bool hasRect = false;
		if (playerIndex >= 0
			&& playerIndex < _playerAvatarAreas.Length
			&& _playerAvatarAreas[playerIndex] is DraggableAvatarArea avatarArea
			&& GodotObject.IsInstanceValid(avatarArea))
		{
			targetRect = avatarArea.GetGlobalRect();
			hasRect = true;
		}

		if (playerIndex >= 0
			&& playerIndex < _playerScoreMeters.Length
			&& _playerScoreMeters[playerIndex] is ScoreAvatarMeter meter
			&& GodotObject.IsInstanceValid(meter))
		{
			Rect2 meterRect = meter.GetGlobalRect();
			targetRect = hasRect ? targetRect.Merge(meterRect) : meterRect;
			hasRect = true;
		}

		return hasRect ? targetRect : new Rect2(Vector2.Zero, PlayerStatusTooltipSize);
	}

	private string GetPlayerStatusMetricLabel()
	{
		string key = adventure_mode ? "GAME_STATUS_HEALTH" : "GAME_STATUS_SCORE";
		string fallback = GetStatusMetricFallback(adventure_mode);
		string text = Localize(key);
		return text == key ? fallback : text;
	}

	private static string GetStatusMetricFallback(bool isHealth)
	{
		string locale = TranslationServer.GetLocale();
		if (locale.StartsWith("zh_TW", StringComparison.OrdinalIgnoreCase)
			|| locale.StartsWith("zh_HK", StringComparison.OrdinalIgnoreCase))
		{
			return isHealth ? "生命值" : "分數";
		}

		if (locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
		{
			return isHealth ? "生命值" : "分数";
		}

		return isHealth ? "Health" : "Score";
	}

	private static StyleBoxFlat CreatePlayerStatusTooltipPanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.14f, 0.18f, 0.90f),
			BorderColor = new Color(0.58f, 0.96f, 1.0f, 0.62f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.28f),
			ShadowSize = 12,
			ContentMarginLeft = 0.0f,
			ContentMarginTop = 0.0f,
			ContentMarginRight = 0.0f,
			ContentMarginBottom = 0.0f,
		};
	}

	private static StyleBoxFlat CreatePlayerStatusTooltipBarTrackStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.0f, 0.06f, 0.10f, 0.68f),
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		};
	}
}
