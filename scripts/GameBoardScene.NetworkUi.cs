using System;
using Godot;

public partial class GameBoardScene
{
	private const double NetworkReconnectNoticeSeconds = 4.0;
	private static readonly Vector2 NetworkReconnectNoticeSize = new(420.0f, 72.0f);

	private void CreateNetworkReconnectOverlay()
	{
		_networkReconnectOverlayPanel = new Panel
		{
			Name = "NetworkReconnectOverlay",
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 3100,
			Modulate = Colors.White,
		};
		_networkReconnectOverlayPanel.SetAnchorsPreset(LayoutPreset.FullRect);
		_networkReconnectOverlayPanel.AddThemeStyleboxOverride("panel", CreateNetworkReconnectOverlayStyle());
		AddChild(_networkReconnectOverlayPanel);

		_networkReconnectOverlayLabel = new Label
		{
			Name = "NetworkReconnectOverlayLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_networkReconnectOverlayLabel.SetAnchorsPreset(LayoutPreset.Center);
		_networkReconnectOverlayLabel.OffsetLeft = -220.0f;
		_networkReconnectOverlayLabel.OffsetTop = -32.0f;
		_networkReconnectOverlayLabel.OffsetRight = 220.0f;
		_networkReconnectOverlayLabel.OffsetBottom = 32.0f;
		_networkReconnectOverlayLabel.AddThemeFontSizeOverride("font_size", 22);
		_networkReconnectOverlayLabel.AddThemeColorOverride("font_color", new Color(0.95f, 1.0f, 0.99f, 0.96f));
		_networkReconnectOverlayLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.03f, 0.05f, 0.92f));
		_networkReconnectOverlayLabel.AddThemeConstantOverride("shadow_offset_x", 2);
		_networkReconnectOverlayLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		_networkReconnectOverlayPanel.AddChild(_networkReconnectOverlayLabel);

		_networkReconnectNoticePanel = new Panel
		{
			Name = "NetworkReconnectNotice",
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 3050,
			CustomMinimumSize = NetworkReconnectNoticeSize,
			Size = NetworkReconnectNoticeSize,
		};
		_networkReconnectNoticePanel.SetAnchorsPreset(LayoutPreset.TopWide);
		_networkReconnectNoticePanel.OffsetTop = 18.0f;
		_networkReconnectNoticePanel.OffsetBottom = 18.0f + NetworkReconnectNoticeSize.Y;
		_networkReconnectNoticePanel.OffsetLeft = 0.0f;
		_networkReconnectNoticePanel.OffsetRight = 0.0f;
		_networkReconnectNoticePanel.AddThemeStyleboxOverride("panel", CreateNetworkReconnectNoticeStyle());
		AddChild(_networkReconnectNoticePanel);

		_networkReconnectNoticeLabel = new Label
		{
			Name = "NetworkReconnectNoticeLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_networkReconnectNoticeLabel.SetAnchorsPreset(LayoutPreset.FullRect);
		_networkReconnectNoticeLabel.OffsetLeft = 18.0f;
		_networkReconnectNoticeLabel.OffsetTop = 12.0f;
		_networkReconnectNoticeLabel.OffsetRight = -18.0f;
		_networkReconnectNoticeLabel.OffsetBottom = -12.0f;
		_networkReconnectNoticeLabel.AddThemeFontSizeOverride("font_size", 15);
		_networkReconnectNoticeLabel.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.98f, 0.96f));
		_networkReconnectNoticeLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.06f, 0.08f, 0.88f));
		_networkReconnectNoticeLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_networkReconnectNoticeLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		_networkReconnectNoticePanel.AddChild(_networkReconnectNoticeLabel);
	}

	private void UpdateReconnectOverlay()
	{
		bool shouldShowOverlay = ShouldShowNetworkReconnectOverlay();
		if (_networkReconnectOverlayPanel != null)
		{
			_networkReconnectOverlayPanel.Visible = shouldShowOverlay;
		}

		if (shouldShowOverlay && _networkReconnectOverlayLabel != null)
		{
			_networkReconnectOverlayLabel.Text = GetNetworkReconnectOverlayText();
		}

		if (_networkReconnectNoticePanel != null
			&& _networkReconnectNoticePanel.Visible
			&& Time.GetTicksMsec() / 1000.0 >= _networkReconnectNoticeHideAt)
		{
			_networkReconnectNoticePanel.Visible = false;
		}
	}

	private bool ShouldShowNetworkReconnectOverlay()
	{
		NetworkManager? networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		return NetworkReconnectPresentationRules.ShouldShowOverlay(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			_networkWaitForInitialize,
			networkManager?.State ?? NetworkSessionState.Idle,
			_networkLastAppliedSnapshotId);
	}

	private void ShowReconnectRecoveryNoticeIfNeeded()
	{
		if (!TryGetSeatControlStateForUi(GetLocalHandPlayerIndex(), out SeatControlState state)
			|| state.Mode != SeatControlMode.HumanReclaimPending
			|| string.IsNullOrWhiteSpace(GetNetworkReconnectNoticeText()))
		{
			return;
		}

		ShowNetworkNotice(GetNetworkReconnectNoticeText());
	}

	private void ShowNetworkNotice(string message)
	{
		if (string.IsNullOrWhiteSpace(message)
			|| _networkReconnectNoticePanel == null
			|| _networkReconnectNoticeLabel == null)
		{
			return;
		}

		_networkReconnectNoticeLabel.Text = message;
		_networkReconnectNoticeHideAt = Time.GetTicksMsec() / 1000.0 + NetworkReconnectNoticeSeconds;
		_networkReconnectNoticePanel.Visible = true;
	}

	private bool TryGetSeatControlStateForUi(int seatIndex, out SeatControlState state)
	{
		state = default;
		if (!IsNetworkMultiplayerMode() || seatIndex < 0)
		{
			return false;
		}

		if (IsNetworkHost())
		{
			return GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.TryGetSeatControlStateForUi(seatIndex, out state) == true;
		}

		foreach (SeatControlState candidate in _networkSeatControlStates)
		{
			if (candidate.SeatIndex == seatIndex)
			{
				state = candidate;
				return true;
			}
		}

		return false;
	}

	private string GetPlayerSeatControlStatusText(int playerIndex)
	{
		if (!TryGetSeatControlStateForUi(playerIndex, out SeatControlState state))
		{
			return string.Empty;
		}

		return NetworkReconnectPresentationRules.GetSeatControlStatusText(state.Mode, UsesChineseLocale());
	}

	private string GetNetworkReconnectOverlayText()
	{
		return NetworkReconnectPresentationRules.GetReconnectOverlayText(UsesChineseLocale());
	}

	private string GetNetworkReconnectNoticeText()
	{
		return NetworkReconnectPresentationRules.GetReconnectNoticeText(UsesChineseLocale());
	}

	private static StyleBoxFlat CreateNetworkReconnectOverlayStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.01f, 0.04f, 0.06f, 0.82f),
		};
	}

	private static StyleBoxFlat CreateNetworkReconnectNoticeStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.14f, 0.18f, 0.92f),
			BorderColor = new Color(0.58f, 0.96f, 1.0f, 0.58f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10,
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.30f),
			ShadowSize = 10,
		};
	}

	private bool UsesChineseLocale()
	{
		return TranslationServer.GetLocale().StartsWith("zh", StringComparison.OrdinalIgnoreCase);
	}
}
