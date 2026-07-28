using System;
using Godot;

public partial class MainMenuPlayerCountDialog : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	[Signal]
	public delegate void StartGameRequestedEventHandler(int playerCount, int soloAiDifficulty, bool itemMode, int winningScore);

	private Label _titleLabel = default!;
	private Label _messageLabel = default!;
	private Label _playersLabel = default!;
	private Label _difficultyLabel = default!;
	private Label _itemModeLabel = default!;
	private Label _winningScoreLabel = default!;
	private Button _startButton = default!;
	private Button _backButton = default!;
	private ColorRect _scrim = default!;
	private MarginContainer _dialogMargin = default!;
	private VBoxContainer _dialogLayout = default!;
	private HBoxContainer _header = default!;
	private ScrollContainer _configurationScroll = default!;
	private GridContainer _configurationRows = default!;
	private HBoxContainer _startGameWrap = default!;
	private Control _startGameFrame = default!;
	private Control[] _configurationRowPanels = Array.Empty<Control>();
	private bool _suppressConfigurationFocusScroll;

	public Control MotionRoot { get; private set; } = default!;
	public Control DialogPanel { get; private set; } = default!;
	public OptionButton PlayerCountOption { get; private set; } = default!;
	public OptionButton DifficultyOption { get; private set; } = default!;
	public OptionButton ItemModeOption { get; private set; } = default!;
	public OptionButton WinningScoreOption { get; private set; } = default!;

	public void FocusDefaultControl()
	{
		_suppressConfigurationFocusScroll = true;
		_configurationScroll.ScrollVertical = 0;
		PlayerCountOption.GrabFocus();
		CallDeferred(nameof(FinishDefaultFocus));
	}

	public override void _Ready()
	{
		MotionRoot = GetNode<Control>("%DialogMotion");
		DialogPanel = GetNode<Control>("%PlayerCountDialogPanel");
		PlayerCountOption = GetNode<OptionButton>("%PlayerCountOption");
		DifficultyOption = GetNode<OptionButton>("%DifficultyOption");
		ItemModeOption = GetNode<OptionButton>("%ItemModeOption");
		WinningScoreOption = GetNode<OptionButton>("%WinningScoreOption");
		_titleLabel = GetNode<Label>("%PlayerCountTitle");
		_messageLabel = GetNode<Label>("%PlayerCountMessage");
		_playersLabel = GetNode<Label>("%PlayersLabel");
		_difficultyLabel = GetNode<Label>("%DifficultyLabel");
		_itemModeLabel = GetNode<Label>("%ItemModeLabel");
		_winningScoreLabel = GetNode<Label>("%WinningScoreLabel");
		_startButton = GetNode<Button>("%StartGameButton");
		_backButton = GetNode<Button>("%BackButton");
		_scrim = GetNode<ColorRect>("%Scrim");
		_dialogMargin = DialogPanel.GetNode<MarginContainer>("DialogMargin");
		_dialogLayout = _dialogMargin.GetNode<VBoxContainer>("DialogLayout");
		_header = _dialogLayout.GetNode<HBoxContainer>("Header");
		_configurationScroll = GetNode<ScrollContainer>("%ConfigurationScroll");
		_configurationRows = _configurationScroll.GetNode<GridContainer>("ConfigurationRows");
		_startGameWrap = _dialogLayout.GetNode<HBoxContainer>("StartGameWrap");
		_startGameFrame = _startGameWrap.GetNode<Control>("StartGameFrame");
		_configurationRowPanels = new Control[]
		{
			_configurationRows.GetNode<Control>("PlayersRowPanel"),
			_configurationRows.GetNode<Control>("DifficultyRowPanel"),
			_configurationRows.GetNode<Control>("ItemModeRowPanel"),
			_configurationRows.GetNode<Control>("WinningScoreRowPanel"),
		};

		_scrim.GuiInput += OnScrimGuiInput;
		_backButton.Pressed += () => EmitSignal(SignalName.CloseRequested);
		_startButton.Pressed += EmitStartGameRequested;
		EnableKeyboardFocus(_backButton, PlayerCountOption, DifficultyOption, ItemModeOption, WinningScoreOption, _startButton);
		MainMenuInteractiveStyle.ApplyActionButton(_backButton, 20);
		ApplyBackActionText(_backButton);
		MainMenuInteractiveStyle.ApplyNeutralSmokeGameConfigurationOption(PlayerCountOption);
		MainMenuInteractiveStyle.ApplyNeutralSmokeGameConfigurationOption(DifficultyOption);
		MainMenuInteractiveStyle.ApplyNeutralSmokeGameConfigurationOption(ItemModeOption);
		MainMenuInteractiveStyle.ApplyNeutralSmokeGameConfigurationOption(WinningScoreOption);
		MainMenuInteractiveStyle.ApplyActionButton(_startButton, 24);
		ApplyPrimaryActionText(_startButton);
		ConfigureConfigurationFocusScrolling(
			PlayerCountOption,
			DifficultyOption,
			ItemModeOption,
			WinningScoreOption);
		SecondaryMenuGlassStyle.AttachNeutralSmoke(
			this,
			DialogPanel,
			_scrim,
			new Vector2(860.0f, 560.0f));
		SecondaryMenuGlassStyle.StyleScrollBar(_configurationScroll.GetVScrollBar());
		Resized += ApplyResponsiveDensity;
		ApplyResponsiveDensity();
	}

	private void ApplyResponsiveDensity()
	{
		float viewportHeight = Size.Y > 0.0f ? Size.Y : GetViewportRect().Size.Y;
		bool compact = viewportHeight <= 600.0f;
		float margin = compact ? 24.0f : 28.0f;
		float rowHeight = compact ? 60.0f : 62.0f;
		float optionHeight = rowHeight - 16.0f;

		_dialogMargin.OffsetTop = margin;
		_dialogMargin.OffsetBottom = -margin;
		_dialogLayout.AddThemeConstantOverride("separation", compact ? 10 : 12);
		_header.CustomMinimumSize = new Vector2(0.0f, compact ? 72.0f : 76.0f);
		_configurationRows.AddThemeConstantOverride("v_separation", compact ? 10 : 12);

		for (int index = 0; index < _configurationRowPanels.Length; index++)
		{
			_configurationRowPanels[index].CustomMinimumSize = new Vector2(0.0f, rowHeight);
		}
		PlayerCountOption.CustomMinimumSize = new Vector2(0.0f, optionHeight);
		DifficultyOption.CustomMinimumSize = new Vector2(0.0f, optionHeight);
		ItemModeOption.CustomMinimumSize = new Vector2(0.0f, optionHeight);
		WinningScoreOption.CustomMinimumSize = new Vector2(0.0f, optionHeight);
		_startGameWrap.CustomMinimumSize = new Vector2(0.0f, compact ? 68.0f : 72.0f);
		_startGameFrame.CustomMinimumSize = new Vector2(330.0f, compact ? 60.0f : 64.0f);
	}

	private static void ApplyBackActionText(Button button)
	{
		Color idle = new(0.94f, 0.96f, 0.96f, 1.0f);
		Color active = new(0.91f, 1.0f, 0.96f, 1.0f);
		button.AddThemeColorOverride("font_color", idle);
		button.AddThemeColorOverride("font_pressed_color", active);
		button.AddThemeColorOverride("font_hover_color", active);
		button.AddThemeColorOverride("font_focus_color", active);
	}

	private static void ApplyPrimaryActionText(Button button)
	{
		Color idle = new(0.96f, 1.0f, 0.99f, 1.0f);
		Color active = Colors.White;
		button.AddThemeColorOverride("font_color", idle);
		button.AddThemeColorOverride("font_pressed_color", active);
		button.AddThemeColorOverride("font_hover_color", active);
		button.AddThemeColorOverride("font_focus_color", active);
	}

	public void RefreshLocalizedText(
		Func<string, string> localize,
		Func<int, string> getPlayerCountText,
		Func<int, string> getDifficultyText,
		Func<int, string> getWinningScoreText)
	{
		_titleLabel.Text = localize("MENU_SINGLE_PLAYER");
		_backButton.Text = localize("ADVENTURE_BACK");
		_backButton.TooltipText = localize("ADVENTURE_BACK");
		_messageLabel.Text = localize("PLAYER_COUNT_MESSAGE");
		_playersLabel.Text = localize("PLAYER_COUNT_PLAYERS");
		_difficultyLabel.Text = localize("PLAYER_COUNT_DIFFICULTY");
		_itemModeLabel.Text = localize("PLAYER_COUNT_ITEM_MODE");
		_winningScoreLabel.Text = localize("PLAYER_COUNT_WINNING_SCORE");
		_startButton.Text = localize("PLAYER_COUNT_START_GAME");
		_startButton.TooltipText = localize("PLAYER_COUNT_START_GAME");

		for (int index = 0; index < PlayerCountOption.ItemCount; index++)
		{
			PlayerCountOption.SetItemText(index, getPlayerCountText(PlayerCountOption.GetItemId(index)));
		}

		for (int index = 0; index < DifficultyOption.ItemCount; index++)
		{
			DifficultyOption.SetItemText(index, getDifficultyText(DifficultyOption.GetItemId(index)));
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
			DifficultyOption.GetSelectedId(),
			ItemModeOption.GetSelectedId() == 1,
			WinningScoreOption.GetSelectedId());
	}

	private void FinishDefaultFocus()
	{
		_configurationScroll.ScrollVertical = 0;
		_suppressConfigurationFocusScroll = false;
	}

	private void ConfigureConfigurationFocusScrolling(params Control[] controls)
	{
		_configurationScroll.FollowFocus = false;
		foreach (Control control in controls)
		{
			control.FocusEntered += () =>
			{
				if (!_suppressConfigurationFocusScroll)
				{
					CallDeferred(nameof(EnsureConfigurationControlVisible), control);
				}
			};
		}
	}

	private void EnsureConfigurationControlVisible(Control control)
	{
		if (!GodotObject.IsInstanceValid(control) || !control.IsVisibleInTree())
		{
			return;
		}

		const float padding = 8.0f;
		Rect2 viewportRect = _configurationScroll.GetGlobalRect();
		Rect2 controlRect = control.GetGlobalRect();
		float delta = 0.0f;
		if (controlRect.Position.Y < viewportRect.Position.Y + padding)
		{
			delta = controlRect.Position.Y - viewportRect.Position.Y - padding;
		}
		else if (controlRect.End.Y > viewportRect.End.Y - padding)
		{
			delta = controlRect.End.Y - viewportRect.End.Y + padding;
		}

		if (!Mathf.IsZeroApprox(delta))
		{
			_configurationScroll.ScrollVertical = Mathf.Max(
				0,
				_configurationScroll.ScrollVertical + Mathf.RoundToInt(delta));
		}
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

	private static void EnableKeyboardFocus(params Control[] controls)
	{
		foreach (Control control in controls)
		{
			control.FocusMode = Control.FocusModeEnum.All;
		}
	}
}
