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
		_scrim = GetNode<ColorRect>("%Scrim");

		_scrim.GuiInput += OnScrimGuiInput;
		_resetButton.Pressed += () => EmitSignal(SignalName.ResetDefaultsRequested);
		_doneButton.Pressed += () => EmitSignal(SignalName.CloseRequested);

		foreach (GameInputActions.BindingAction action in GameInputActions.All)
		{
			string rowPath = $"%{ToRowNodeName(action.Id)}";
			string rowLayoutPath = $"{rowPath}/RowMargin/RowLayout";
			Label label = GetNode<Label>($"{rowLayoutPath}/ActionLabel");
			Button keyboardButton = GetNode<Button>($"{rowLayoutPath}/KeyboardButton");
			Button gamepadButton = GetNode<Button>($"{rowLayoutPath}/GamepadButton");
			MainMenuInteractiveStyle.ApplyOptionLikeButton(keyboardButton);
			MainMenuInteractiveStyle.ApplyOptionLikeButton(gamepadButton);
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
		}

		EnableKeyboardFocus(_resetButton, _doneButton);
		MainMenuInteractiveStyle.ApplyActionButton(_resetButton, 20);
		MainMenuInteractiveStyle.ApplyActionButton(_doneButton, 20);
		StyleScrollBar(_rowsScroll.GetVScrollBar());
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
			_rows[0].KeyboardButton.GrabFocus();
			return;
		}

		_doneButton.GrabFocus();
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

	private static void StyleScrollBar(ScrollBar scrollBar)
	{
		scrollBar.CustomMinimumSize = new Vector2(10.0f, 0.0f);
		scrollBar.AddThemeConstantOverride("padding_left", 2);
		scrollBar.AddThemeConstantOverride("padding_right", 2);

		var track = new StyleBoxFlat
		{
			BgColor = new Color(0.03f, 0.14f, 0.18f, 0.82f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginTop = 4.0f,
			ContentMarginBottom = 4.0f,
		};
		var grabber = new StyleBoxFlat
		{
			BgColor = new Color(0.78f, 1.0f, 0.96f, 0.68f),
			BorderColor = new Color(0.18f, 0.64f, 0.68f, 0.92f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};
		var grabberHighlight = new StyleBoxFlat
		{
			BgColor = new Color(0.94f, 1.0f, 0.98f, 0.86f),
			BorderColor = new Color(0.30f, 0.88f, 0.86f, 1.0f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};

		scrollBar.AddThemeStyleboxOverride("scroll", track);
		scrollBar.AddThemeStyleboxOverride("scroll_focus", track);
		scrollBar.AddThemeStyleboxOverride("grabber", grabber);
		scrollBar.AddThemeStyleboxOverride("grabber_highlight", grabberHighlight);
		scrollBar.AddThemeStyleboxOverride("grabber_pressed", grabberHighlight);
	}
}
