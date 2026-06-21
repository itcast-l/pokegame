using Godot;

public partial class GameBoardScene
{
	private static readonly Vector2 PlayerSeatControlIndicatorSize = new(148.0f, 26.0f);
	private const float PlayerSeatControlIndicatorGap = 8.0f;

	private void CreatePlayerSeatControlIndicators()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control playerArea = GetPlayerArea(playerIndex);
			var panel = new Panel
			{
				Name = $"Player{playerIndex + 1}SeatControlIndicator",
				Visible = false,
				MouseFilter = MouseFilterEnum.Ignore,
				ZIndex = 4,
				CustomMinimumSize = PlayerSeatControlIndicatorSize,
				Size = PlayerSeatControlIndicatorSize,
			};

			var label = new Label
			{
				Name = "Label",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.Off,
				MouseFilter = MouseFilterEnum.Ignore,
				ClipText = true,
			};
			label.SetAnchorsPreset(LayoutPreset.FullRect);
			label.OffsetLeft = 10.0f;
			label.OffsetTop = 3.0f;
			label.OffsetRight = -10.0f;
			label.OffsetBottom = -3.0f;
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", new Color(0.96f, 1.0f, 0.99f, 0.98f));
			label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.04f, 0.08f, 0.88f));
			label.AddThemeConstantOverride("shadow_offset_x", 1);
			label.AddThemeConstantOverride("shadow_offset_y", 1);

			panel.AddChild(label);
			playerArea.AddChild(panel);
			_playerSeatControlIndicatorPanels[playerIndex] = panel;
			_playerSeatControlIndicatorLabels[playerIndex] = label;
			_playerSeatControlIndicatorModes[playerIndex] = SeatControlMode.HumanConnected;
		}

		UpdateAllPlayerSeatControlIndicatorLayouts();
		MarkSeatControlIndicatorsDirty();
	}

	private void RefreshAllPlayerSeatControlIndicators()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			RefreshPlayerSeatControlIndicator(playerIndex);
		}
	}

	private void RefreshPlayerSeatControlIndicator(int playerIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= MaxPlayers
			|| _playerSeatControlIndicatorPanels[playerIndex] == null
			|| _playerSeatControlIndicatorLabels[playerIndex] == null)
		{
			return;
		}

		Panel panel = _playerSeatControlIndicatorPanels[playerIndex]!;
		Label label = _playerSeatControlIndicatorLabels[playerIndex]!;
		if (!GodotObject.IsInstanceValid(panel) || !GodotObject.IsInstanceValid(label))
		{
			return;
		}

		bool playerVisible = playerIndex < player_count;
		string statusText = playerVisible ? GetPlayerSeatControlStatusText(playerIndex) : string.Empty;
		bool visible = playerVisible && !string.IsNullOrWhiteSpace(statusText);
		SeatControlMode nextMode = SeatControlMode.HumanConnected;
		bool needsContentRefresh = panel.Visible != visible;
		if (visible)
		{
			TryGetSeatControlStateForUi(playerIndex, out SeatControlState state);
			nextMode = state.Mode;
			needsContentRefresh |= !string.Equals(label.Text, statusText)
				|| _playerSeatControlIndicatorModes[playerIndex] != nextMode;
			if (needsContentRefresh)
			{
				label.Text = statusText;
				panel.AddThemeStyleboxOverride("panel", CreatePlayerSeatControlIndicatorStyle(nextMode));
			}
		}

		panel.Visible = visible;
		if (!visible)
		{
			_playerSeatControlIndicatorModes[playerIndex] = SeatControlMode.HumanConnected;
			return;
		}

		_playerSeatControlIndicatorModes[playerIndex] = nextMode;
		if (needsContentRefresh)
		{
			ApplyPlayerSeatControlIndicatorLayout(playerIndex);
		}
	}

	private void UpdateAllPlayerSeatControlIndicatorLayouts()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			ApplyPlayerSeatControlIndicatorLayout(playerIndex);
		}
	}

	private void ApplyPlayerSeatControlIndicatorLayout(int playerIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= MaxPlayers
			|| _playerSeatControlIndicatorPanels[playerIndex] == null)
		{
			return;
		}

		Panel panel = _playerSeatControlIndicatorPanels[playerIndex]!;
		if (!GodotObject.IsInstanceValid(panel))
		{
			return;
		}

		panel.Size = PlayerSeatControlIndicatorSize;
		Vector2 position = GetPlayerSeatControlIndicatorPosition(playerIndex, PlayerSeatControlIndicatorSize);
		panel.Position = position;
	}

	private static Rect2 GetSeatControlIndicatorAnchorRect(Control? control)
	{
		if (control == null || !GodotObject.IsInstanceValid(control))
		{
			return new Rect2(Vector2.Zero, Vector2.Zero);
		}

		return new Rect2(control.Position, control.Size);
	}

	private static StyleBoxFlat CreatePlayerSeatControlIndicatorStyle(SeatControlMode mode)
	{
		Color backgroundColor = mode == SeatControlMode.HumanReclaimPending
			? new Color(0.08f, 0.34f, 0.20f, 0.92f)
			: new Color(0.20f, 0.11f, 0.04f, 0.92f);
		Color borderColor = mode == SeatControlMode.HumanReclaimPending
			? new Color(0.62f, 1.0f, 0.78f, 0.70f)
			: new Color(1.0f, 0.82f, 0.56f, 0.68f);

		return new StyleBoxFlat
		{
			BgColor = backgroundColor,
			BorderColor = borderColor,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomLeft = 12,
			CornerRadiusBottomRight = 12,
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.22f),
			ShadowSize = 6,
			ContentMarginLeft = 0,
			ContentMarginTop = 0,
			ContentMarginRight = 0,
			ContentMarginBottom = 0,
		};
	}

	private Vector2 GetPlayerSeatControlIndicatorPosition(int playerIndex, Vector2 indicatorSize)
	{
		Rect2 avatarRect = GetSeatControlIndicatorAnchorRect(_playerAvatarAreas[playerIndex]);
		Rect2 scoreRect = GetSeatControlIndicatorAnchorRect(_playerScoreMeters[playerIndex]);

		return GetPlayerViewSlotIndex(playerIndex) switch
		{
			0 => new Vector2(
				scoreRect.End.X + PlayerSeatControlIndicatorGap,
				scoreRect.Position.Y + scoreRect.Size.Y - indicatorSize.Y + 4.0f),
			1 => new Vector2(
				scoreRect.End.X + PlayerSeatControlIndicatorGap,
				Mathf.Max(0.0f, scoreRect.Position.Y + 12.0f)),
			2 or 3 => new Vector2(
				avatarRect.Position.X + PlayerSeatControlIndicatorGap,
				scoreRect.End.Y + 4.0f),
			_ => Vector2.Zero,
		};
	}
}
