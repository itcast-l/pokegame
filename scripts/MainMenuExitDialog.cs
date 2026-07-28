using Godot;

public partial class MainMenuExitDialog : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	[Signal]
	public delegate void ConfirmRequestedEventHandler();

	private Label _titleLabel = default!;
	private Label _hintLabel = default!;
	private Button _cancelButton = default!;
	private Button _confirmButton = default!;
	private Button _backButton = default!;
	private ColorRect _scrim = default!;

	public PanelContainer DialogPanel { get; private set; } = default!;
	public Button CancelButton => _cancelButton;
	public Button ConfirmButton => _confirmButton;

	public override void _Ready()
	{
		DialogPanel = GetNode<PanelContainer>("%ExitDialogPanel");
		_titleLabel = GetNode<Label>("%ExitTitle");
		_hintLabel = GetNode<Label>("%ExitHint");
		_cancelButton = GetNode<Button>("%CancelButton");
		_confirmButton = GetNode<Button>("%ConfirmButton");
		_backButton = GetNode<Button>("%BackButton");
		_scrim = GetNode<ColorRect>("%Scrim");

		_scrim.GuiInput += OnScrimGuiInput;
		_cancelButton.FocusMode = Control.FocusModeEnum.All;
		_confirmButton.FocusMode = Control.FocusModeEnum.All;
		_backButton.FocusMode = Control.FocusModeEnum.All;
		MainMenuInteractiveStyle.ApplyActionButton(_backButton, 18);
		MainMenuInteractiveStyle.ApplyActionButton(_cancelButton, 20);
		MainMenuInteractiveStyle.ApplyActionButton(_confirmButton, 20);
		_backButton.Pressed += () => EmitSignal(SignalName.CloseRequested);
		_cancelButton.Pressed += () => EmitSignal(SignalName.CloseRequested);
		_confirmButton.Pressed += () => EmitSignal(SignalName.ConfirmRequested);
		SecondaryMenuGlassStyle.Attach(
			this,
			DialogPanel,
			_scrim,
			new Vector2(560.0f, 360.0f),
			GetNode<PanelContainer>("%PromptPanel"));
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (!Visible)
		{
			return;
		}

		if (IsPointerInput(inputEvent))
		{
			SetActionFocusHighlightEnabled(false);
			return;
		}

		if (IsNavigationInput(inputEvent))
		{
			SetActionFocusHighlightEnabled(true);
		}
	}

	public void RefreshLocalizedText(Func<string, string> localize)
	{
		_titleLabel.Text = localize("EXIT_TITLE");
		_backButton.Text = localize("ADVENTURE_BACK");
		_hintLabel.Text = localize("EXIT_HINT");
		_cancelButton.Text = localize("EXIT_CANCEL");
		_confirmButton.Text = localize("EXIT_CONFIRM");
	}

	public void ResetActionFocusHighlight()
	{
		SetActionFocusHighlightEnabled(false);
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

	private void SetActionFocusHighlightEnabled(bool enabled)
	{
		MainMenuInteractiveStyle.SetActionButtonFocusHighlight(_cancelButton, enabled);
		MainMenuInteractiveStyle.SetActionButtonFocusHighlight(_confirmButton, enabled);
	}

	private static bool IsPointerInput(InputEvent inputEvent)
	{
		return inputEvent is InputEventMouseButton or InputEventMouseMotion;
	}

	private static bool IsNavigationInput(InputEvent inputEvent)
	{
		return inputEvent is InputEventKey { Pressed: true, Echo: false }
			|| inputEvent is InputEventJoypadButton { Pressed: true }
			|| inputEvent is InputEventJoypadMotion joypadMotion && Mathf.Abs(joypadMotion.AxisValue) > 0.45f;
	}
}
