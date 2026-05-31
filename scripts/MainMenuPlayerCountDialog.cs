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

		EmitSignal(SignalName.CloseRequested);
		GetViewport().SetInputAsHandled();
	}

	private static void StyleOptionPopup(OptionButton option)
	{
		PopupMenu popup = option.GetPopup();
		popup.PreferNativeMenu = false;
		popup.ShrinkWidth = false;
		popup.ShrinkHeight = true;
		popup.MinSize = new Vector2I(Mathf.RoundToInt(option.CustomMinimumSize.X), 0);
	}

	private static void EnableKeyboardFocus(params Control[] controls)
	{
		foreach (Control control in controls)
		{
			control.FocusMode = Control.FocusModeEnum.All;
		}
	}
}
