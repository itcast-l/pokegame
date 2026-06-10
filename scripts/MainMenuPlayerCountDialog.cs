using System;
using Godot;

public partial class MainMenuPlayerCountDialog : Control
{
	private const int DefaultPlayerCountId = 2;
	private const int DefaultItemModeId = 1;
	private const int DefaultWinningScoreId = 100;

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
	private PanelContainer _playerCountValueOverlay = default!;
	private PanelContainer _itemModeValueOverlay = default!;
	private PanelContainer _winningScoreValueOverlay = default!;
	private Label _playerCountValueLabel = default!;
	private Label _itemModeValueLabel = default!;
	private Label _winningScoreValueLabel = default!;

	public Control MotionRoot { get; private set; } = default!;
	public PanelContainer DialogPanel { get; private set; } = default!;
	public OptionButton PlayerCountOption { get; private set; } = default!;
	public OptionButton ItemModeOption { get; private set; } = default!;
	public OptionButton WinningScoreOption { get; private set; } = default!;

	public override void _Ready()
	{
		MotionRoot = GetNode<Control>("%DialogMotion");
		DialogPanel = GetNode<PanelContainer>("%PlayerCountDialogPanel");
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
		_playerCountValueOverlay = GetNode<PanelContainer>("%PlayerCountValueOverlay");
		_itemModeValueOverlay = GetNode<PanelContainer>("%ItemModeValueOverlay");
		_winningScoreValueOverlay = GetNode<PanelContainer>("%WinningScoreValueOverlay");
		_playerCountValueLabel = _playerCountValueOverlay.GetNode<Label>("Label");
		_itemModeValueLabel = _itemModeValueOverlay.GetNode<Label>("Label");
		_winningScoreValueLabel = _winningScoreValueOverlay.GetNode<Label>("Label");

		_scrim.GuiInput += OnScrimGuiInput;
		_startButton.Pressed += EmitStartGameRequested;
		PlayerCountOption.ItemSelected += OnOptionSelectionChanged;
		ItemModeOption.ItemSelected += OnOptionSelectionChanged;
		WinningScoreOption.ItemSelected += OnOptionSelectionChanged;
		Resized += OnLayoutChanged;
		DialogPanel.Resized += OnLayoutChanged;
		EnableKeyboardFocus(PlayerCountOption, ItemModeOption, WinningScoreOption, _startButton);
		StyleOptionPopup(PlayerCountOption);
		StyleOptionPopup(ItemModeOption);
		StyleOptionPopup(WinningScoreOption);
		CallDeferred(nameof(RefreshValueOverlays));
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

		CallDeferred(nameof(RefreshValueOverlays));
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

		EmitSignal(SignalName.CloseRequested);
		GetViewport().SetInputAsHandled();
	}

	private void OnOptionSelectionChanged(long _index)
	{
		RefreshValueOverlays();
	}

	private void OnLayoutChanged()
	{
		CallDeferred(nameof(RefreshValueOverlays));
	}

	private void RefreshValueOverlays()
	{
		RefreshValueOverlay(_playerCountValueOverlay, _playerCountValueLabel, PlayerCountOption, DefaultPlayerCountId);
		RefreshValueOverlay(_itemModeValueOverlay, _itemModeValueLabel, ItemModeOption, DefaultItemModeId);
		RefreshValueOverlay(_winningScoreValueOverlay, _winningScoreValueLabel, WinningScoreOption, DefaultWinningScoreId);
	}

	private void RefreshValueOverlay(PanelContainer overlay, Label label, OptionButton option, int defaultId)
	{
		if (option.GetSelectedId() == defaultId)
		{
			overlay.Visible = false;
			return;
		}

		PositionValueOverlay(overlay, option, new Vector2(4.0f, 3.0f), new Vector2(38.0f, 6.0f));
		label.Text = GetSelectedItemText(option);
	}

	private void PositionValueOverlay(Control overlay, Control option, Vector2 insetPosition, Vector2 insetSize)
	{
		Rect2 optionRect = option.GetGlobalRect();
		Rect2 panelRect = DialogPanel.GetGlobalRect();
		Vector2 localPosition = optionRect.Position - panelRect.Position + insetPosition;
		Vector2 overlaySize = optionRect.Size - insetSize;
		overlay.Position = localPosition;
		overlay.Size = new Vector2(Mathf.Max(overlaySize.X, 0.0f), Mathf.Max(overlaySize.Y, 0.0f));
		overlay.Visible = overlay.Size.X > 0.0f && overlay.Size.Y > 0.0f;
	}

	private static string GetSelectedItemText(OptionButton option)
	{
		int selected = option.Selected;
		return selected >= 0 ? option.GetItemText(selected) : string.Empty;
	}

	private static void StyleOptionPopup(OptionButton option)
	{
		PopupMenu popup = option.GetPopup();
		popup.PreferNativeMenu = false;
		popup.ShrinkWidth = false;
		popup.ShrinkHeight = true;
		popup.MinSize = new Vector2I(Mathf.RoundToInt(option.CustomMinimumSize.X), 0);
		popup.Theme = new Theme();
		var popupStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.03f, 0.16f, 0.20f, 0.96f),
			BorderColor = new Color(0.55f, 0.98f, 0.94f, 0.42f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomLeft = 12,
			CornerRadiusBottomRight = 12,
			ContentMarginLeft = 8.0f,
			ContentMarginTop = 8.0f,
			ContentMarginRight = 8.0f,
			ContentMarginBottom = 8.0f,
		};
		var hoverStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.25f, 0.31f, 0.88f),
			BorderColor = Colors.Transparent,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 8.0f,
			ContentMarginTop = 5.0f,
			ContentMarginRight = 8.0f,
			ContentMarginBottom = 5.0f,
		};
		popup.Theme.SetStylebox("panel", "PopupMenu", popupStyle);
		popup.Theme.SetStylebox("hover", "PopupMenu", hoverStyle);
		popup.Theme.SetStylebox("focus", "PopupMenu", hoverStyle);
		popup.Theme.SetColor("font_color", "PopupMenu", new Color(0.90f, 1.0f, 0.97f, 1.0f));
		popup.Theme.SetColor("font_hover_color", "PopupMenu", new Color(1.0f, 1.0f, 0.84f, 1.0f));
		popup.Theme.SetColor("font_focus_color", "PopupMenu", new Color(1.0f, 1.0f, 0.84f, 1.0f));
		popup.Theme.SetColor("font_separator_color", "PopupMenu", new Color(0.55f, 0.98f, 0.94f, 0.18f));
		popup.Theme.SetFontSize("font_size", "PopupMenu", 18);
		popup.Theme.SetConstant("v_separation", "PopupMenu", 4);
	}

	private static void EnableKeyboardFocus(params Control[] controls)
	{
		foreach (Control control in controls)
		{
			control.FocusMode = Control.FocusModeEnum.All;
		}
	}
}
