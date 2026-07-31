using System;
using System.Collections.Generic;
using Godot;

public partial class MainMenuControlsDialog : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	[Signal]
	public delegate void BindingEditRequestedEventHandler(string actionId, long slot);

	[Signal]
	public delegate void ResetDefaultsRequestedEventHandler();

	private Label _titleLabel = default!;
	private Label _messageLabel = default!;
	private Label _keyboardHeader = default!;
	private Label _gamepadHeader = default!;
	private Label _captureHintLabel = default!;
	private Button _resetButton = default!;
	private Button _doneButton = default!;
	private Button _backButton = default!;
	private ColorRect _scrim = default!;
	private ScrollContainer _rowsScroll = default!;
	private readonly List<RowWidgets> _rows = new();
	private Func<string, string>? _localize;
	private string? _captureActionId;
	private GameInputActions.BindingSlot? _captureSlot;
	private int _captureEarliestFrame;

	public Control DialogPanel { get; private set; } = default!;
	public Button DoneButton => _doneButton;
	public Button ResetDefaultsButton => _resetButton;
	public string? CaptureActionId => _captureActionId;
	public GameInputActions.BindingSlot? CaptureSlot => _captureSlot;
	public int CaptureEarliestFrame => _captureEarliestFrame;

	private sealed class RowWidgets
	{
		public RowWidgets(string actionId, Label label, Button keyboardButton, Button gamepadButton)
		{
			ActionId = actionId;
			Label = label;
			KeyboardButton = keyboardButton;
			GamepadButton = gamepadButton;
		}

		public string ActionId { get; }
		public Label Label { get; }
		public Button KeyboardButton { get; }
		public Button GamepadButton { get; }
	}

	public override void _Ready()
	{
		DialogPanel = GetNode<Control>("%ControlsDialogPanel");
		_titleLabel = GetNode<Label>("%ControlsTitle");
		_messageLabel = GetNode<Label>("%ControlsMessage");
		_keyboardHeader = GetNode<Label>("%KeyboardHeader");
		_gamepadHeader = GetNode<Label>("%GamepadHeader");
		_captureHintLabel = GetNode<Label>("%CaptureHint");
		_rowsScroll = GetNode<ScrollContainer>("%RowsScroll");
		_resetButton = GetNode<Button>("%ResetDefaultsButton");
		_doneButton = GetNode<Button>("%DoneButton");
		_backButton = GetNode<Button>("%BackButton");
		_scrim = GetNode<ColorRect>("%Scrim");

		_scrim.GuiInput += OnScrimGuiInput;
		_backButton.Pressed += () => EmitSignal(SignalName.CloseRequested);
		_resetButton.Pressed += () => EmitSignal(SignalName.ResetDefaultsRequested);
		_doneButton.Pressed += () => EmitSignal(SignalName.CloseRequested);

		foreach (GameInputActions.BindingAction action in GameInputActions.All)
		{
			string rowPath = $"%{ToRowNodeName(action.Id)}";
			string rowLayoutPath = $"{rowPath}/RowMargin/RowLayout";
			Control row = GetNode<Control>(rowPath);
			Label label = GetNode<Label>($"{rowLayoutPath}/ActionLabel");
			Button keyboardButton = GetNode<Button>($"{rowLayoutPath}/KeyboardButton");
			Button gamepadButton = GetNode<Button>($"{rowLayoutPath}/GamepadButton");
			MainMenuInteractiveStyle.ApplyThemedControl(keyboardButton, MainMenuInteractiveStyle.PauseDerivedPalette, 16);
			MainMenuInteractiveStyle.ApplyThemedControl(gamepadButton, MainMenuInteractiveStyle.PauseDerivedPalette, 16);
			ConfigureBindingCell(keyboardButton);
			ConfigureBindingCell(gamepadButton);
			ConfigureBindingRow(row);
			keyboardButton.FocusMode = FocusModeEnum.All;
			gamepadButton.FocusMode = FocusModeEnum.All;
			string actionId = action.Id;
			keyboardButton.GuiInput += inputEvent => OnBindingButtonGuiInput(inputEvent, actionId, GameInputActions.BindingSlot.Keyboard);
			gamepadButton.GuiInput += inputEvent => OnBindingButtonGuiInput(inputEvent, actionId, GameInputActions.BindingSlot.Gamepad);
			keyboardButton.Pressed += () =>
			{
				BeginBindingCapture(actionId, GameInputActions.BindingSlot.Keyboard);
			};
			gamepadButton.Pressed += () =>
			{
				BeginBindingCapture(actionId, GameInputActions.BindingSlot.Gamepad);
			};
			_rows.Add(new RowWidgets(actionId, label, keyboardButton, gamepadButton));
			if (GetNodeOrNull<OceanMenuButtonFrame>(rowPath) is { } rowFrame)
			{
				SecondaryMenuGlassStyle.ConfigurePauseDerivedMinimalFrame(
					rowFrame,
					OceanMenuButtonFrame.ButtonFrameStyle.IndividualToolRow);
			}
		}

		EnableKeyboardFocus(_backButton, _resetButton, _doneButton);
		MainMenuInteractiveStyle.ApplyActionButton(_backButton, 18, MainMenuInteractiveStyle.PauseDerivedPalette);
		MainMenuInteractiveStyle.ApplyActionButton(_resetButton, 20, MainMenuInteractiveStyle.PauseDerivedPalette);
		MainMenuInteractiveStyle.ApplyActionButton(_doneButton, 20, MainMenuInteractiveStyle.PauseDerivedPalette);
		ConfigurePauseDerivedVisualStyle();
		SecondaryMenuGlassStyle.StyleScrollBar(_rowsScroll.GetVScrollBar(), SecondaryMenuGlassStyle.PauseDerivedVisual);
		SecondaryMenuGlassStyle.AttachPauseDerived(
			this,
			DialogPanel,
			_scrim,
			SecondaryMenuGlassStyle.PauseDerivedCoreDesignSize,
			removeCenterOrnaments: true,
			GetNode<PanelContainer>("%ControlsContentPanel"));
	}

	private void ConfigurePauseDerivedVisualStyle()
	{
		ConfigureFrame("BackFrame");
		ConfigureFrame("ResetDefaultsFrame");
		ConfigureFrame("DoneFrame");
		_titleLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		_messageLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedSecondaryText);
		_keyboardHeader.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedGold);
		_gamepadHeader.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedGold);
		_captureHintLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedSecondaryText);
		foreach (RowWidgets row in _rows)
		{
			row.Label.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		}
		if (FindChild("TitleUnderline", true, false) is Control underline)
		{
			SecondaryMenuGlassStyle.InstallPauseDerivedDivider(underline);
		}
		if (FindChild("Rows", true, false) is Container rows)
		{
			rows.AddThemeConstantOverride("v_separation", 0);
		}
	}

	private static void ConfigureBindingRow(Control row)
	{
		row.CustomMinimumSize = new Vector2(row.CustomMinimumSize.X, 72.0f);
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.01f, 0.08f, 0.12f, 0.30f),
			BorderColor = new Color(0.20f, 0.66f, 0.70f, 0.14f),
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
		});
	}

	private static void ConfigureBindingCell(Button button)
	{
		button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, 48.0f);
		MainMenuInteractiveStyle.ApplyUnifiedButtonText(button);
		button.AddThemeStyleboxOverride("normal", CreateBindingCellStyle(active: false));
		button.AddThemeStyleboxOverride("pressed", CreateBindingCellStyle(active: true));
		button.AddThemeStyleboxOverride("hover", CreateBindingCellStyle(active: true));
		button.AddThemeStyleboxOverride("focus", CreateBindingCellStyle(active: true));
	}

	private static StyleBoxFlat CreateBindingCellStyle(bool active)
	{
		return new StyleBoxFlat
		{
			BgColor = active
				? new Color(0.08f, 0.24f, 0.27f, 0.98f)
				: new Color(0.015f, 0.12f, 0.17f, 0.86f),
			BorderColor = active
				? MainMenuInteractiveStyle.PauseDerivedGold
				: new Color(0.13f, 0.72f, 0.76f, 0.50f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 12.0f,
			ContentMarginTop = 5.0f,
			ContentMarginRight = 12.0f,
			ContentMarginBottom = 5.0f,
		};
	}

	private void ConfigureFrame(string frameName)
	{
		if (FindChild(frameName, true, false) is not OceanMenuButtonFrame frame)
		{
			return;
		}

		SecondaryMenuGlassStyle.ConfigurePauseDerivedMinimalFrame(
			frame,
			OceanMenuButtonFrame.ButtonFrameStyle.IndividualAction,
			MainMenuInteractiveStyle.PauseDerivedCyan);
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (!Visible || _captureActionId != null || _captureSlot != null)
		{
			return;
		}

		if (!inputEvent.IsActionPressed(GameInputActions.GameConfirm))
		{
			return;
		}

		if (!BeginBindingCaptureFromFocusedControl())
		{
			return;
		}

		GetViewport().SetInputAsHandled();
	}

	public void RefreshLocalizedText(Func<string, string> localize)
	{
		_localize = localize;
		_titleLabel.Text = localize("SETTINGS_CONTROLS_TITLE");
		_backButton.Text = localize("ADVENTURE_BACK");
		_messageLabel.Text = localize("SETTINGS_CONTROLS_MESSAGE");
		_keyboardHeader.Text = localize("SETTINGS_CONTROLS_KEYBOARD_HEADER");
		_gamepadHeader.Text = localize("SETTINGS_CONTROLS_GAMEPAD_HEADER");
		_resetButton.Text = localize("SETTINGS_RESET_DEFAULTS");
		_doneButton.Text = localize("SETTINGS_DONE");

		foreach (RowWidgets row in _rows)
		{
			GameInputActions.BindingAction action = FindAction(row.ActionId);
			row.Label.Text = localize(action.LabelKey);
			row.KeyboardButton.Text = BuildBindingButtonText(localize, action.Id, GameInputActions.BindingSlot.Keyboard);
			row.GamepadButton.Text = BuildBindingButtonText(localize, action.Id, GameInputActions.BindingSlot.Gamepad);
		}

		_captureHintLabel.Text = _captureActionId == null || _captureSlot == null
			? localize("SETTINGS_CONTROLS_HINT_IDLE")
			: string.Format(localize("SETTINGS_CONTROLS_HINT_CAPTURE"), localize(FindAction(_captureActionId).LabelKey));
	}

	public void ClearBindingCapture()
	{
		_captureActionId = null;
		_captureSlot = null;
		_captureEarliestFrame = 0;
		if (_localize != null)
		{
			RefreshLocalizedText(_localize);
		}
	}

	public void FocusDefaultButton()
	{
		if (_rows.Count > 0)
		{
			_rowsScroll.ScrollVertical = 0;
			_rows[0].KeyboardButton.GrabFocus();
			CallDeferred(nameof(ResetRowsScrollToTop));
			return;
		}

		_doneButton.GrabFocus();
	}

	private void ResetRowsScrollToTop()
	{
		_rowsScroll.ScrollVertical = 0;
	}

	public bool BeginBindingCaptureFromFocusedControl()
	{
		foreach (RowWidgets row in _rows)
		{
			if (row.KeyboardButton.HasFocus())
			{
				BeginBindingCapture(row.ActionId, GameInputActions.BindingSlot.Keyboard);
				return true;
			}

			if (row.GamepadButton.HasFocus())
			{
				BeginBindingCapture(row.ActionId, GameInputActions.BindingSlot.Gamepad);
				return true;
			}
		}

		return false;
	}

	private string BuildBindingButtonText(
		Func<string, string> localize,
		string actionId,
		GameInputActions.BindingSlot slot)
	{
		bool capturing = _captureActionId == actionId && _captureSlot == slot;
		if (capturing)
		{
			return localize("SETTINGS_CONTROLS_LISTENING");
		}

		return GameInputActions.GetBindingText(actionId, slot, localize);
	}

	private static GameInputActions.BindingAction FindAction(string actionId)
	{
		foreach (GameInputActions.BindingAction action in GameInputActions.All)
		{
			if (action.Id == actionId)
			{
				return action;
			}
		}

		throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "Unknown input action.");
	}

	private void BeginBindingCapture(string actionId, GameInputActions.BindingSlot slot)
	{
		_captureActionId = actionId;
		_captureSlot = slot;
		_captureEarliestFrame = Engine.GetFramesDrawn() + 1;
		if (_localize != null)
		{
			RefreshLocalizedText(_localize);
		}

		EmitSignal(SignalName.BindingEditRequested, actionId, (long)slot);
	}

	private void OnBindingButtonGuiInput(InputEvent inputEvent, string actionId, GameInputActions.BindingSlot slot)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		BeginBindingCapture(actionId, slot);
		GetViewport().SetInputAsHandled();
	}

	private static string ToRowNodeName(string actionId)
	{
		string[] parts = actionId.Split('_', StringSplitOptions.RemoveEmptyEntries);
		for (int index = 0; index < parts.Length; index++)
		{
			if (parts[index].Length == 0)
			{
				continue;
			}

			parts[index] = char.ToUpperInvariant(parts[index][0]) + parts[index][1..];
		}

		return string.Concat(parts) + "Row";
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

	private static void EnableKeyboardFocus(params Control[] controls)
	{
		foreach (Control control in controls)
		{
			control.FocusMode = FocusModeEnum.All;
		}
	}

}
