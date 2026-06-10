using System;
using Godot;

public partial class MainMenuPlayerCountDialog : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	[Signal]
	public delegate void StartGameRequestedEventHandler(int playerCount, bool itemMode, int winningScore);

	private Label _titleLabel = default!;
	private Label _messageLabel = default!;
	private Label _playersLabel = default!;
	private Label _itemModeLabel = default!;
	private Label _winningScoreLabel = default!;
	private Button _startButton = default!;
	private ColorRect _scrim = default!;
	private static Texture2D? _popupDiamondSelectedIcon;
	private static Texture2D? _popupDiamondIdleIcon;

	public Control MotionRoot { get; private set; } = default!;
	public Control DialogPanel { get; private set; } = default!;
	public OptionButton PlayerCountOption { get; private set; } = default!;
	public OptionButton ItemModeOption { get; private set; } = default!;
	public OptionButton WinningScoreOption { get; private set; } = default!;

	public override void _Ready()
	{
		MotionRoot = GetNode<Control>("%DialogMotion");
		DialogPanel = GetNode<Control>("%PlayerCountDialogPanel");
		PlayerCountOption = GetNode<OptionButton>("%PlayerCountOption");
		ItemModeOption = GetNode<OptionButton>("%ItemModeOption");
		WinningScoreOption = GetNode<OptionButton>("%WinningScoreOption");
		_titleLabel = GetNode<Label>("%PlayerCountTitle");
		_messageLabel = GetNode<Label>("%PlayerCountMessage");
		_playersLabel = GetNode<Label>("%PlayersLabel");
		_itemModeLabel = GetNode<Label>("%ItemModeLabel");
		_winningScoreLabel = GetNode<Label>("%WinningScoreLabel");
		_startButton = GetNode<Button>("%StartGameButton");
		_scrim = GetNode<ColorRect>("%Scrim");

		_scrim.GuiInput += OnScrimGuiInput;
		_startButton.Pressed += EmitStartGameRequested;
		EnableKeyboardFocus(PlayerCountOption, ItemModeOption, WinningScoreOption, _startButton);
		StyleOptionPopup(PlayerCountOption);
		StyleOptionPopup(ItemModeOption);
		StyleOptionPopup(WinningScoreOption);
	}

	public void RefreshLocalizedText(
		Func<string, string> localize,
		Func<int, string> getPlayerCountText,
		Func<int, string> getWinningScoreText)
	{
		_titleLabel.Text = localize("PLAYER_COUNT_TITLE");
		_messageLabel.Text = localize("PLAYER_COUNT_MESSAGE");
		_playersLabel.Text = localize("PLAYER_COUNT_PLAYERS");
		_itemModeLabel.Text = localize("PLAYER_COUNT_ITEM_MODE");
		_winningScoreLabel.Text = localize("PLAYER_COUNT_WINNING_SCORE");
		_startButton.Text = localize("PLAYER_COUNT_START_GAME");

		for (int index = 0; index < PlayerCountOption.ItemCount; index++)
		{
			PlayerCountOption.SetItemText(index, getPlayerCountText(PlayerCountOption.GetItemId(index)));
		}

		if (ItemModeOption.ItemCount >= 2)
		{
			ItemModeOption.SetItemText(0, localize("PLAYER_COUNT_ITEM_MODE_ON"));
			ItemModeOption.SetItemText(1, localize("PLAYER_COUNT_ITEM_MODE_OFF"));
		}

		for (int index = 0; index < WinningScoreOption.ItemCount; index++)
		{
			WinningScoreOption.SetItemText(index, getWinningScoreText(WinningScoreOption.GetItemId(index)));
		}
	}

	private void EmitStartGameRequested()
	{
		EmitSignal(
			SignalName.StartGameRequested,
			PlayerCountOption.GetSelectedId(),
			ItemModeOption.GetSelectedId() == 1,
			WinningScoreOption.GetSelectedId());
	}

	private void OnScrimGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		if (DialogPanel.GetGlobalRect().HasPoint(GetGlobalMousePosition()))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		EmitSignal(SignalName.CloseRequested);
		GetViewport().SetInputAsHandled();
	}

	private void StyleOptionPopup(OptionButton option)
	{
		PopupMenu popup = option.GetPopup();
		popup.PreferNativeMenu = false;
		popup.ShrinkWidth = false;
		popup.ShrinkHeight = true;
		popup.MinSize = new Vector2I(Mathf.RoundToInt(option.CustomMinimumSize.X), 0);
		popup.Theme = BuildPopupTheme();
	}

	private static Theme BuildPopupTheme()
	{
		_popupDiamondSelectedIcon ??= CreateDiamondIndicatorTexture(
			new Color(0.24f, 0.98f, 0.94f, 0.42f),
			new Color(0.92f, 1.0f, 0.98f, 0.92f),
			new Color(1.0f, 0.93f, 0.68f, 0.96f),
			false);
		_popupDiamondIdleIcon ??= CreateDiamondIndicatorTexture(
			new Color(0.24f, 0.98f, 0.94f, 0.12f),
			new Color(0.68f, 0.98f, 0.94f, 0.58f),
			Colors.Transparent,
			true);

		Theme theme = new();
		var popupStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.12f, 0.17f, 0.97f),
			BorderColor = new Color(0.50f, 0.98f, 0.94f, 0.36f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 14,
			CornerRadiusTopRight = 14,
			CornerRadiusBottomLeft = 14,
			CornerRadiusBottomRight = 14,
			ContentMarginLeft = 12.0f,
			ContentMarginTop = 12.0f,
			ContentMarginRight = 12.0f,
			ContentMarginBottom = 12.0f,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.42f),
			ShadowSize = 14,
			ShadowOffset = new Vector2(0.0f, 5.0f),
		};
		var hoverStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.28f, 0.33f, 0.92f),
			BorderColor = new Color(0.86f, 1.0f, 0.96f, 0.24f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10,
			ContentMarginLeft = 10.0f,
			ContentMarginTop = 7.0f,
			ContentMarginRight = 10.0f,
			ContentMarginBottom = 7.0f,
		};
		var separatorStyle = new StyleBoxLine
		{
			Color = new Color(0.55f, 0.98f, 0.94f, 0.16f),
			Thickness = 1,
			GrowBegin = 10.0f,
			GrowEnd = 10.0f,
		};
		theme.SetStylebox("panel", "PopupMenu", popupStyle);
		theme.SetStylebox("hover", "PopupMenu", hoverStyle);
		theme.SetStylebox("focus", "PopupMenu", hoverStyle);
		theme.SetStylebox("separator", "PopupMenu", separatorStyle);
		theme.SetColor("font_color", "PopupMenu", new Color(0.90f, 1.0f, 0.97f, 1.0f));
		theme.SetColor("font_hover_color", "PopupMenu", new Color(1.0f, 0.98f, 0.84f, 1.0f));
		theme.SetColor("font_focus_color", "PopupMenu", new Color(1.0f, 0.98f, 0.84f, 1.0f));
		theme.SetColor("font_disabled_color", "PopupMenu", new Color(0.56f, 0.78f, 0.77f, 0.64f));
		theme.SetColor("font_outline_color", "PopupMenu", new Color(0.0f, 0.04f, 0.07f, 0.92f));
		theme.SetColor("font_separator_color", "PopupMenu", new Color(0.55f, 0.98f, 0.94f, 0.18f));
		theme.SetFontSize("font_size", "PopupMenu", 19);
		theme.SetConstant("h_separation", "PopupMenu", 10);
		theme.SetConstant("item_start_padding", "PopupMenu", 12);
		theme.SetConstant("item_end_padding", "PopupMenu", 12);
		theme.SetConstant("icon_max_width", "PopupMenu", 16);
		theme.SetConstant("outline_size", "PopupMenu", 1);
		theme.SetConstant("v_separation", "PopupMenu", 5);
		theme.SetIcon("checked", "PopupMenu", _popupDiamondSelectedIcon);
		theme.SetIcon("radio_checked", "PopupMenu", _popupDiamondSelectedIcon);
		theme.SetIcon("unchecked", "PopupMenu", _popupDiamondIdleIcon);
		theme.SetIcon("radio_unchecked", "PopupMenu", _popupDiamondIdleIcon);
		return theme;
	}

	private static Texture2D CreateDiamondIndicatorTexture(Color glowColor, Color rimColor, Color fillColor, bool hollow)
	{
		const int size = 18;
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		Vector2 center = new(size * 0.5f - 0.5f, size * 0.5f - 0.5f);

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = Mathf.Abs(x - center.X);
				float dy = Mathf.Abs(y - center.Y);
				float diamondDistance = dx + dy;
				Color pixel = Colors.Transparent;

				if (diamondDistance <= 8.0f)
				{
					float glowFactor = 1.0f - diamondDistance / 8.0f;
					pixel = pixel.Blend(new Color(glowColor.R, glowColor.G, glowColor.B, glowColor.A * glowFactor * glowFactor));
				}

				float rimDistance = Mathf.Abs(diamondDistance - 4.3f);
				if (rimDistance <= 1.15f)
				{
					float rimFactor = 1.0f - rimDistance / 1.15f;
					pixel = pixel.Blend(new Color(rimColor.R, rimColor.G, rimColor.B, rimColor.A * rimFactor));
				}

				if (!hollow && diamondDistance <= 3.0f)
				{
					float fillFactor = 1.0f - diamondDistance / 3.0f;
					pixel = pixel.Blend(new Color(fillColor.R, fillColor.G, fillColor.B, fillColor.A * (0.70f + fillFactor * 0.30f)));
				}

				if (pixel.A > 0.0f)
				{
					image.SetPixel(x, y, pixel);
				}
			}
		}

		return ImageTexture.CreateFromImage(image);
	}

	private static void EnableKeyboardFocus(params Control[] controls)
	{
		foreach (Control control in controls)
		{
			control.FocusMode = Control.FocusModeEnum.All;
		}
	}
}
