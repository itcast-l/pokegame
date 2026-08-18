using System;
using Godot;

public partial class GameBoardResultBanner : Control
{
	[Signal]
	public delegate void PrimaryPressedEventHandler();

	[Signal]
	public delegate void ReturnPressedEventHandler();

	public Control BannerPanel { get; private set; } = default!;
	public TextureRect BannerArt { get; private set; } = default!;
	public Label WinnerLabel { get; private set; } = default!;
	public CharacterAvatar ResultAvatar { get; private set; } = default!;
	public Control ActionRow { get; private set; } = default!;
	public Control PrimaryFrame { get; private set; } = default!;
	public Button PrimaryButton { get; private set; } = default!;
	public Button ReturnButton { get; private set; } = default!;

	public override void _Ready()
	{
		BannerPanel = GetNode<Control>("%WinnerBannerPanel");
		BannerArt = GetNode<TextureRect>("%WinnerBannerArt");
		WinnerLabel = GetNode<Label>("%WinnerLabel");
		ResultAvatar = GetNode<CharacterAvatar>("%ResultAvatar");
		ActionRow = GetNode<Control>("WinnerCenter/WinnerBannerPanel/WinnerButtonRow");
		PrimaryFrame = GetNode<Control>("%PrimaryFrame");
		PrimaryButton = GetNode<Button>("%PrimaryButton");
		ReturnButton = GetNode<Button>("%ReturnButton");

		PrimaryButton.Pressed += () => EmitSignal(SignalName.PrimaryPressed);
		ReturnButton.Pressed += () => EmitSignal(SignalName.ReturnPressed);
	}

	public void SetPrimaryActionVisible(bool visible)
	{
		PrimaryFrame.Visible = visible;
		PrimaryButton.Disabled = !visible;
	}

	public void SetActionsVisible(bool visible)
	{
		ActionRow.Visible = visible;
		if (!visible)
		{
			PrimaryButton.Disabled = true;
			ReturnButton.Disabled = true;
		}
	}
}
