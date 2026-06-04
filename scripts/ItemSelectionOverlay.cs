using System.Collections.Generic;
using Godot;

public partial class ItemSelectionOverlay : Control
{
	[Signal]
	public delegate void OptionSelectedEventHandler(int optionIndex);

	[Signal]
	public delegate void CancelRequestedEventHandler();

	private const int MaxOptionCount = 8;
	private readonly Button[] _optionButtons = new Button[MaxOptionCount];
	private Label _titleLabel = default!;
	private Button _cancelButton = default!;
	private bool _showCancel;

	public override void _Ready()
	{
		_titleLabel = GetNode<Label>("%SelectionTitle");
		_cancelButton = GetNode<Button>("%CancelButton");
		for (int index = 0; index < MaxOptionCount; index++)
		{
			int optionIndex = index;
			_optionButtons[index] = GetNode<Button>($"%Option{index + 1}Button");
			_optionButtons[index].Pressed += () => EmitSignal(SignalName.OptionSelected, optionIndex);
		}

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
			if (visible)
			{
				_optionButtons[index].Text = options[index];
			}
		}

		_cancelButton.Visible = showCancel;
		_cancelButton.Disabled = !showCancel;
		_cancelButton.Text = cancelText;
		CallDeferred(nameof(FocusDefaultButton));
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
		foreach (Button optionButton in _optionButtons)
		{
			if (optionButton.Visible && !optionButton.Disabled)
			{
				optionButton.GrabFocus();
				return;
			}
		}

		if (_cancelButton.Visible && !_cancelButton.Disabled)
		{
			_cancelButton.GrabFocus();
		}
	}
}
