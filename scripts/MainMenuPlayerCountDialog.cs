using System;
using System.Collections.Generic;
using Godot;

public partial class MainMenuPlayerCountDialog : Control
{
	private static readonly StyleBoxFlat HiddenOptionFocusStyle = new()
	{
		BgColor = Colors.Transparent,
		BorderColor = Colors.Transparent,
	};

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
	private PanelContainer _configurationPanel = default!;
	private ScrollContainer _configurationScroll = default!;
	private GridContainer _configurationRows = default!;
	private HBoxContainer _startGameWrap = default!;
	private Control _startGameFrame = default!;
	private Control[] _configurationRowPanels = Array.Empty<Control>();
	private readonly Dictionary<OptionButton, StyleBox> _optionFocusStyles = new();
	private bool _suppressConfigurationFocusScroll;
	private bool _inputFocusVisible = true;
	private bool _lastInputWasKeyboardOrJoypad = true;

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
		SetInputFocusVisible(_lastInputWasKeyboardOrJoypad);
		if (_lastInputWasKeyboardOrJoypad)
		{
			PlayerCountOption.GrabFocus();
		}
		CallDeferred(nameof(FinishDefaultFocus));
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton or InputEventMouseMotion)
		{
			_lastInputWasKeyboardOrJoypad = false;
			if (Visible)
			{
				SetInputFocusVisible(false);
			}
			return;
		}

		if (!IsKeyboardOrJoypadNavigationInput(inputEvent))
		{
			return;
		}

		_lastInputWasKeyboardOrJoypad = true;
		if (Visible)
		{
			SetInputFocusVisible(true);
			if (!HasDialogFocus() && !HasOpenConfigurationPopup())
			{
				PlayerCountOption.GrabFocus();
			}
		}
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
		_configurationPanel = GetNode<PanelContainer>("%ConfigurationPanel");
		_configurationScroll = GetNode<ScrollContainer>("%ConfigurationScroll");
		_configurationRows = GetNode<GridContainer>("%ConfigurationRows");
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
		MainMenuInteractiveStyle.ApplyActionButton(_backButton, 20, MainMenuInteractiveStyle.SecondaryMenuPalette);
		ApplyUnifiedConfigurationOption(PlayerCountOption);
		ApplyUnifiedConfigurationOption(DifficultyOption);
		ApplyUnifiedConfigurationOption(ItemModeOption);
		ApplyUnifiedConfigurationOption(WinningScoreOption);
		MainMenuInteractiveStyle.ApplyActionButton(_startButton, 24, MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
		ConfigureUnifiedVisualStyle();
		ConfigureConfigurationFocusScrolling(
			PlayerCountOption,
			DifficultyOption,
			ItemModeOption,
			WinningScoreOption);
		SecondaryMenuGlassStyle.Attach(
			this,
			DialogPanel,
			_scrim,
			SecondaryMenuGlassStyle.PauseDerivedCoreDesignSize,
			SecondaryMenuGlassStyle.UnifiedVisual,
			_configurationPanel);
		SecondaryMenuGlassStyle.StyleScrollBar(
			_configurationScroll.GetVScrollBar(),
			SecondaryMenuGlassStyle.UnifiedVisual);
		Resized += ApplyResponsiveDensity;
		ApplyResponsiveDensity();
	}

	private void ApplyUnifiedConfigurationOption(OptionButton option)
	{
		MainMenuInteractiveStyle.ApplySecondaryMenuGameConfigurationOption(option);
		_optionFocusStyles[option] = option.GetThemeStylebox("focus");
		option.Alignment = HorizontalAlignment.Left;
		if (option.GetThemeStylebox("normal") is StyleBoxFlat normalStyle)
		{
			Color surface = normalStyle.BgColor;
			normalStyle.BgColor = new Color(surface.R, surface.G, surface.B, 0.82f);
		}
	}

	private void ConfigureUnifiedVisualStyle()
	{
		foreach (Control row in _configurationRowPanels)
		{
			if (row is OceanMenuButtonFrame frame)
			{
				ConfigureUnifiedFrame(
					frame,
					OceanMenuButtonFrame.ButtonFrameStyle.IndividualToolRow,
					MainMenuInteractiveStyle.SecondaryMenuPalette.ActiveBorder);
			}
		}

		ConfigureFrame(
			"BackFrame",
			MainMenuInteractiveStyle.SecondaryMenuPalette.ActiveBorder);
		ConfigureFrame(
			"StartGameFrame",
			MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette.ActiveBorder);
		_titleLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.SecondaryMenuPalette.FocusText);
		_messageLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.SecondaryMenuPalette.DisabledText);
		foreach (Label label in new[] { _playersLabel, _difficultyLabel, _itemModeLabel, _winningScoreLabel })
		{
			label.AddThemeColorOverride("font_color", new Color(0.72f, 0.98f, 0.93f, 0.86f));
		}
		foreach (Node chevron in FindChildren("Chevron", "Label", true, false))
		{
			if (chevron is Label label)
			{
				label.AddThemeColorOverride("font_color", new Color(0.72f, 0.98f, 0.93f, 0.90f));
			}
		}
		if (FindChild("TitleUnderline", true, false) is ColorRect underline)
		{
			underline.Color = new Color(0.62f, 1.0f, 0.94f, 0.58f);
		}
	}

	private void ConfigureFrame(string frameName, Color accent)
	{
		if (FindChild(frameName, true, false) is not OceanMenuButtonFrame frame)
		{
			return;
		}

		ConfigureUnifiedFrame(
			frame,
			OceanMenuButtonFrame.ButtonFrameStyle.IndividualAction,
			accent);
	}

	private static void ConfigureUnifiedFrame(
		OceanMenuButtonFrame frame,
		OceanMenuButtonFrame.ButtonFrameStyle frameStyle,
		Color accent,
		bool suppressVisuals = false)
	{
		frame.FrameStyle = frameStyle;
		frame.AccentColor = accent;
		frame.Selected = false;
		frame.HighlightFocus = true;
		frame.UseToolRowIdleBackdrop = frameStyle == OceanMenuButtonFrame.ButtonFrameStyle.IndividualToolRow;
		frame.SuppressVisuals = suppressVisuals;
		frame.QueueRedraw();
	}

	private void ApplyResponsiveDensity()
	{
		float viewportHeight = Size.Y > 0.0f ? Size.Y : GetViewportRect().Size.Y;
		bool compact = viewportHeight <= 600.0f;
		float margin = 22.0f;
		float rowHeight = compact ? 64.0f : 76.0f;
		float optionHeight = compact ? 46.0f : 54.0f;
		float optionHalfHeight = optionHeight * 0.5f;
		float optionLeft = compact ? 158.0f : 180.0f;
		float labelRight = compact ? 142.0f : 164.0f;

		_dialogMargin.OffsetTop = margin;
		_dialogMargin.OffsetBottom = -margin;
		_dialogLayout.AddThemeConstantOverride("separation", compact ? 10 : 12);
		_header.CustomMinimumSize = new Vector2(0.0f, compact ? 72.0f : 76.0f);
		_configurationRows.AddThemeConstantOverride("v_separation", 6);

		for (int index = 0; index < _configurationRowPanels.Length; index++)
		{
			Control row = _configurationRowPanels[index];
			row.CustomMinimumSize = new Vector2(0.0f, rowHeight);
			OptionButton option = row.GetChild<OptionButton>(1);
			Label label = row.GetChild<Label>(2);
			Label chevron = row.GetChild<Label>(3);
			option.CustomMinimumSize = new Vector2(0.0f, optionHeight);
			option.OffsetLeft = optionLeft;
			option.OffsetRight = -12.0f;
			option.OffsetTop = -optionHalfHeight;
			option.OffsetBottom = optionHalfHeight;
			label.AnchorRight = 0.0f;
			label.OffsetLeft = 24.0f;
			label.OffsetRight = labelRight;
			label.OffsetTop = -20.0f;
			label.OffsetBottom = 20.0f;
			label.HorizontalAlignment = HorizontalAlignment.Left;
			label.AddThemeFontSizeOverride("font_size", compact ? 16 : 18);
			chevron.OffsetTop = -optionHalfHeight;
			chevron.OffsetBottom = optionHalfHeight;
		}
		PlayerCountOption.CustomMinimumSize = new Vector2(0.0f, optionHeight);
		DifficultyOption.CustomMinimumSize = new Vector2(0.0f, optionHeight);
		ItemModeOption.CustomMinimumSize = new Vector2(0.0f, optionHeight);
		WinningScoreOption.CustomMinimumSize = new Vector2(0.0f, optionHeight);
		_startGameWrap.CustomMinimumSize = new Vector2(0.0f, compact ? 68.0f : 72.0f);
		_startGameFrame.CustomMinimumSize = new Vector2(330.0f, compact ? 60.0f : 64.0f);
	}

	public void RefreshLocalizedText(
		Func<string, string> localize,
		Func<int, string> getPlayerCountText,
		Func<int, string> getDifficultyText,
		Func<int, string> getWinningScoreText)
	{
		_titleLabel.Text = localize("MENU_SINGLE_PLAYER");
		_backButton.Text = localize("ADVENTURE_BACK");
		_messageLabel.Text = localize("PLAYER_COUNT_MESSAGE");
		_playersLabel.Text = localize("PLAYER_COUNT_PLAYERS");
		_difficultyLabel.Text = localize("PLAYER_COUNT_DIFFICULTY");
		_itemModeLabel.Text = localize("PLAYER_COUNT_ITEM_MODE");
		_winningScoreLabel.Text = localize("PLAYER_COUNT_WINNING_SCORE");
		_startButton.Text = localize("PLAYER_COUNT_START_GAME");

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

	private void SetInputFocusVisible(bool visible)
	{
		if (_inputFocusVisible == visible)
		{
			return;
		}

		_inputFocusVisible = visible;
		foreach (KeyValuePair<OptionButton, StyleBox> pair in _optionFocusStyles)
		{
			if (GodotObject.IsInstanceValid(pair.Key))
			{
				pair.Key.AddThemeStyleboxOverride(
					"focus",
					visible ? pair.Value : HiddenOptionFocusStyle);
			}
		}

		foreach (Control row in _configurationRowPanels)
		{
			if (row is OceanMenuButtonFrame frame)
			{
				frame.HighlightFocus = visible;
				frame.QueueRedraw();
			}
		}

		SetFrameFocusVisible(_backButton.GetParent(), visible);
		SetFrameFocusVisible(_startGameFrame, visible);
	}

	private static void SetFrameFocusVisible(Node? node, bool visible)
	{
		if (node is OceanMenuButtonFrame frame)
		{
			frame.HighlightFocus = visible;
			frame.QueueRedraw();
		}
	}

	private bool HasDialogFocus()
	{
		Control? focusOwner = GetViewport().GuiGetFocusOwner();
		return focusOwner != null && (focusOwner == this || IsAncestorOf(focusOwner));
	}

	private bool HasOpenConfigurationPopup()
	{
		return PlayerCountOption.GetPopup().Visible
			|| DifficultyOption.GetPopup().Visible
			|| ItemModeOption.GetPopup().Visible
			|| WinningScoreOption.GetPopup().Visible;
	}

	private static bool IsKeyboardOrJoypadNavigationInput(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed(GameInputActions.GameConfirm)
			|| inputEvent.IsActionPressed(GameInputActions.GameCancel)
			|| inputEvent.IsActionPressed(GameInputActions.HandPrevious)
			|| inputEvent.IsActionPressed(GameInputActions.HandNext)
			|| inputEvent.IsActionPressed("ui_accept")
			|| inputEvent.IsActionPressed("ui_cancel")
			|| inputEvent.IsActionPressed("ui_up")
			|| inputEvent.IsActionPressed("ui_down")
			|| inputEvent.IsActionPressed("ui_left")
			|| inputEvent.IsActionPressed("ui_right"))
		{
			return true;
		}

		return inputEvent is InputEventJoypadButton { Pressed: true }
			|| inputEvent is InputEventJoypadMotion joypadMotion
				&& Mathf.Abs(joypadMotion.AxisValue) > 0.45f;
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
