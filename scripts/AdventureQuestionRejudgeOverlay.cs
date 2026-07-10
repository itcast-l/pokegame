using Godot;

public partial class AdventureQuestionRejudgeOverlay : Control
{
	[Signal]
	public delegate void OptionSelectedEventHandler(int optionIndex);

	[Signal]
	public delegate void CancelRequestedEventHandler();

	private static readonly Color ButtonFocusBorderColor = new(0.46f, 1.0f, 0.96f, 0.95f);
	private static readonly Color ButtonFocusShadowColor = new(0.46f, 1.0f, 0.96f, 0.26f);
	private static readonly StyleBoxFlat HiddenFocusStyle = new() { BgColor = Colors.Transparent };
	private TextureRect _rewardCard = default!;
	private Control _rewardEffectPanel = default!;
	private Label _rewardEffectLabel = default!;
	private Control _promptPanel = default!;
	private Label _promptLabel = default!;
	private Button _keepButton = default!;
	private Button _rerollButton = default!;
	private bool _inputFocusVisible;

	public override void _Ready()
	{
		_rewardCard = GetNode<TextureRect>("RewardPreview/RewardMargin/RewardCard");
		_rewardEffectPanel = GetNode<Control>("RewardPreview/RewardMargin/RewardOverlay/RewardEffectPanel");
		_rewardEffectLabel = GetNode<Label>("RewardPreview/RewardMargin/RewardOverlay/RewardEffectPanel/RewardEffectMargin/RewardEffectLabel");
		_promptPanel = GetNode<Control>("PromptPanel");
		_promptLabel = GetNode<Label>("PromptPanel/PromptMargin/PromptLayout/PromptLabel");
		_keepButton = GetNode<Button>("PromptPanel/PromptMargin/PromptLayout/ButtonsRow/KeepButton");
		_rerollButton = GetNode<Button>("PromptPanel/PromptMargin/PromptLayout/ButtonsRow/RerollButton");

		ConfigureFocusableButton(_keepButton);
		ConfigureFocusableButton(_rerollButton);
		_keepButton.Pressed += () => EmitSignal(SignalName.OptionSelected, 0);
		_rerollButton.Pressed += () => EmitSignal(SignalName.OptionSelected, 1);
	}

	public void Configure(Texture2D rewardTexture, string rewardEffectText, string promptText, string keepText, string rerollText)
	{
		_promptPanel.Visible = true;
		_promptPanel.MouseFilter = MouseFilterEnum.Stop;
		_rewardCard.Texture = rewardTexture;
		_rewardEffectLabel.Text = rewardEffectText;
		_rewardEffectPanel.Visible = !string.IsNullOrWhiteSpace(rewardEffectText);
		_promptLabel.Text = promptText;
		_keepButton.Text = keepText;
		_rerollButton.Text = rerollText;
		SetInputFocusVisible(IsKeyboardOrJoypadActionPressedNow());
		CallDeferred(nameof(FocusDefaultButton));
	}

	public void HideDecisionPanelForResolution()
	{
		_promptPanel.Visible = false;
		_promptPanel.MouseFilter = MouseFilterEnum.Ignore;
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

		EmitSignal(SignalName.CancelRequested);
		GetViewport().SetInputAsHandled();
	}

	private void FocusDefaultButton()
	{
		if (!IsInsideTree())
		{
			return;
		}

		if (IsInstanceValid(_keepButton))
		{
			_keepButton.GrabFocus();
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
		if (IsInstanceValid(_keepButton))
		{
			_keepButton.AddThemeStyleboxOverride("focus", focusStyle);
		}

		if (IsInstanceValid(_rerollButton))
		{
			_rerollButton.AddThemeStyleboxOverride("focus", focusStyle);
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
			CornerRadiusTopLeft = 18,
			CornerRadiusTopRight = 18,
			CornerRadiusBottomRight = 18,
			CornerRadiusBottomLeft = 18,
			ShadowColor = ButtonFocusShadowColor,
			ShadowSize = 10,
		};
	}
}
