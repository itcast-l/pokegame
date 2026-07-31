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
	private ColorRect _scrim = default!;
	private PanelContainer _promptPanel = default!;

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
		_scrim = GetNode<ColorRect>("%Scrim");
		_promptPanel = GetNode<PanelContainer>("%PromptPanel");

		_scrim.GuiInput += OnScrimGuiInput;
		_cancelButton.FocusMode = Control.FocusModeEnum.All;
		_confirmButton.FocusMode = Control.FocusModeEnum.All;
		MainMenuInteractiveStyle.ApplyActionButton(_cancelButton, 20, MainMenuInteractiveStyle.PauseDerivedPalette);
		MainMenuInteractiveStyle.ApplyActionButton(_confirmButton, 20, MainMenuInteractiveStyle.PauseDerivedPalette);
		_cancelButton.Pressed += () => EmitSignal(SignalName.CloseRequested);
		_confirmButton.Pressed += () => EmitSignal(SignalName.ConfirmRequested);
		SecondaryMenuGlassStyle.AttachPauseDerived(
			this,
			DialogPanel,
			_scrim,
			SecondaryMenuGlassStyle.PauseDerivedExitDesignSize,
			_promptPanel);
		ConfigurePauseDerivedVisualStyle();
	}

	private void ConfigurePauseDerivedVisualStyle()
	{
		ConfigureFrame("CancelFrame");
		ConfigureFrame("ConfirmFrame");
		_titleLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		_hintLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		if (FindChild("TitleUnderline", true, false) is Control underline)
		{
			SecondaryMenuGlassStyle.InstallPauseDerivedDivider(underline);
		}
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
		MainMenuInteractiveStyle.SetActionButtonFocusHighlight(_cancelButton, enabled, MainMenuInteractiveStyle.PauseDerivedPalette);
		MainMenuInteractiveStyle.SetActionButtonFocusHighlight(_confirmButton, enabled, MainMenuInteractiveStyle.PauseDerivedPalette);
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
