using System.Collections.Generic;
using Godot;

public partial class ItemSelectionOverlay : Control
{
	[Signal]
	public delegate void OptionSelectedEventHandler(int optionIndex);

	[Signal]
	public delegate void CancelRequestedEventHandler();

	private const int MaxOptionCount = 8;
	private static readonly Color ButtonFocusBorderColor = new(1.0f, 0.9f, 0.45f, 0.95f);
	private static readonly Color ButtonFocusShadowColor = new(0.62f, 1.0f, 0.94f, 0.34f);
	private static readonly StyleBoxFlat HiddenFocusStyle = new() { BgColor = Colors.Transparent };
	private readonly Button[] _optionButtons = new Button[MaxOptionCount];
	private Label _titleLabel = default!;
	private Button _cancelButton = default!;
	private bool _showCancel;
	private bool _inputFocusVisible;

	public override void _Ready()
	{
		_titleLabel = GetNode<Label>("%SelectionTitle");
		_cancelButton = GetNode<Button>("%CancelButton");
		for (int index = 0; index < MaxOptionCount; index++)
		{
			int optionIndex = index;
			_optionButtons[index] = GetNode<Button>($"%Option{index + 1}Button");
			ConfigureFocusableButton(_optionButtons[index]);
			_optionButtons[index].Pressed += () => EmitSignal(SignalName.OptionSelected, optionIndex);
		}

		ConfigureFocusableButton(_cancelButton);
		_cancelButton.Pressed += () => EmitSignal(SignalName.CancelRequested);
	}

	public void Configure(string title, IReadOnlyList<string> options, string cancelText, bool showCancel)
	{
		_titleLabel.Text = title;
		_showCancel = showCancel;
		for (int index = 0; index < _optionButtons.Length; index++)
		{
			bool visible = index < options.Count;
			_optionButtons[index].Visible = visible;
			_optionButtons[index].Disabled = !visible;
			_optionButtons[index].FocusMode = visible
				? FocusModeEnum.All
				: FocusModeEnum.None;
			if (visible)
			{
				_optionButtons[index].Text = options[index];
			}
		}

		_cancelButton.Visible = showCancel;
		_cancelButton.Disabled = !showCancel;
		_cancelButton.FocusMode = showCancel
			? FocusModeEnum.All
			: FocusModeEnum.None;
		_cancelButton.Text = cancelText;
		SetInputFocusVisible(IsKeyboardOrJoypadActionPressedNow());
		CallDeferred(nameof(FocusDefaultButton));
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (!Visible)
		{
			return;
		}

		if (IsMouseInput(inputEvent))
		{
			SetInputFocusVisible(false);
			return;
		}

		if (IsKeyboardOrJoypadNavigationInput(inputEvent))
		{
			SetInputFocusVisible(true);
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!Visible || !GameInputActions.IsCancelPressed(inputEvent))
		{
			return;
		}

		if (_showCancel)
		{
			EmitSignal(SignalName.CancelRequested);
		}

		GetViewport().SetInputAsHandled();
	}

	private void FocusDefaultButton()
	{
		if (!IsInsideTree())
		{
			return;
		}

		foreach (Button optionButton in _optionButtons)
		{
			if (optionButton.Visible && !optionButton.Disabled && IsInstanceValid(optionButton))
			{
				optionButton.GrabFocus();
				return;
			}
		}

		if (_cancelButton.Visible && !_cancelButton.Disabled && IsInstanceValid(_cancelButton))
		{
			_cancelButton.GrabFocus();
		}
	}

	private static void ConfigureFocusableButton(Button button)
	{
		button.FocusMode = FocusModeEnum.All;
		button.AddThemeStyleboxOverride("focus", HiddenFocusStyle);
	}

	private void SetInputFocusVisible(bool visible)
	{
		if (_inputFocusVisible == visible)
		{
			return;
		}

		_inputFocusVisible = visible;
		StyleBoxFlat focusStyle = visible ? CreateButtonFocusStyle() : HiddenFocusStyle;
		foreach (Button optionButton in _optionButtons)
		{
			if (optionButton != null && IsInstanceValid(optionButton))
			{
				optionButton.AddThemeStyleboxOverride("focus", focusStyle);
			}
		}

		if (IsInstanceValid(_cancelButton))
		{
			_cancelButton.AddThemeStyleboxOverride("focus", focusStyle);
		}
	}

	private static bool IsMouseInput(InputEvent inputEvent)
	{
		return inputEvent is InputEventMouseButton or InputEventMouseMotion;
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
			|| inputEvent is InputEventJoypadMotion joypadMotion && Mathf.Abs(joypadMotion.AxisValue) > 0.45f;
	}

	private static bool IsKeyboardOrJoypadActionPressedNow()
	{
		return Input.IsActionPressed(GameInputActions.GameConfirm)
			|| Input.IsActionPressed(GameInputActions.GameCancel)
			|| Input.IsActionPressed(GameInputActions.HandPrevious)
			|| Input.IsActionPressed(GameInputActions.HandNext)
			|| Input.IsActionPressed("ui_accept")
			|| Input.IsActionPressed("ui_cancel")
			|| Input.IsActionPressed("ui_up")
			|| Input.IsActionPressed("ui_down")
			|| Input.IsActionPressed("ui_left")
			|| Input.IsActionPressed("ui_right");
	}

	private static StyleBoxFlat CreateButtonFocusStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			BorderColor = ButtonFocusBorderColor,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ShadowColor = ButtonFocusShadowColor,
			ShadowSize = 8,
		};
	}
}
