using System.Collections.Generic;
using Godot;

public partial class CharacterAvatarEditScene : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	[Signal]
	public delegate void AvatarSelectedEventHandler(string avatarId);

	public string CurrentAvatarId { get; set; } = CharacterAvatarCatalog.DefaultAvatarId;

	private static readonly Color SelectedOptionModulate = new(1.0f, 1.0f, 0.78f, 1.0f);

	private Button _backButton = default!;
	private Button _confirmButton = default!;
	private CharacterAvatar _previewAvatar = default!;
	private Label _previewName = default!;
	private GridContainer _avatarGrid = default!;
	private readonly Dictionary<string, PanelContainer> _optionPanels = new();

	public override void _Ready()
	{
		CurrentAvatarId = CharacterAvatarCatalog.NormalizeAvatarId(CurrentAvatarId);

		_backButton = GetNode<Button>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/Header/HeaderLayout/BackButton");
		_backButton.Pressed += RequestClose;

		_previewAvatar = GetNode<CharacterAvatar>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewAvatarAnchor/PreviewAvatar");
		_previewName = GetNode<Label>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PreviewName");
		_confirmButton = GetNode<Button>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/ConfirmButton");
		_confirmButton.Pressed += ConfirmSelection;

		BindActionButton("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/ActionButtons/Idle", CharacterAvatar.AvatarAction.Idle);
		BindActionButton("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/ActionButtons/Score", CharacterAvatar.AvatarAction.Score);
		BindActionButton("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/ActionButtons/Attack", CharacterAvatar.AvatarAction.Attack);
		BindActionButton("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/ActionButtons/Hit", CharacterAvatar.AvatarAction.Hit);
		BindActionButton("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/ActionButtons/Victory", CharacterAvatar.AvatarAction.Victory);
		BindActionButton("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/ActionButtons/Defeat", CharacterAvatar.AvatarAction.Defeat);

		_avatarGrid = GetNode<GridContainer>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/GridScroll/AvatarGrid");
		BuildAvatarOptions();
		ApplyPreviewAvatar();
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed("ui_cancel"))
		{
			RequestClose();
			GetViewport().SetInputAsHandled();
		}
	}

	private void RequestClose()
	{
		EmitSignal(SignalName.CloseRequested);
	}

	private void BuildAvatarOptions()
	{
		Control template = GetNode<Control>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/GridScroll/AvatarGrid/DefaultAvatarOption");
		_avatarGrid.RemoveChild(template);
		_optionPanels.Clear();

		foreach (CharacterAvatarDefinition avatarDefinition in CharacterAvatarCatalog.All)
		{
			Control option = (Control)template.Duplicate();
			option.Name = $"{avatarDefinition.Id}AvatarOption";

			if (option.GetNodeOrNull<CharacterAvatar>("OptionPanel/OptionMargin/OptionLayout/AvatarSlot/CharacterAvatar") is CharacterAvatar avatar)
			{
				avatar.AvatarRoot = avatarDefinition.RootPath;
			}

			if (option.GetNodeOrNull<Label>("OptionPanel/OptionMargin/OptionLayout/AvatarName") is Label label)
			{
				label.Text = avatarDefinition.DisplayName;
			}

			if (option.GetNodeOrNull<PanelContainer>("OptionPanel") is PanelContainer panel)
			{
				_optionPanels[avatarDefinition.Id] = panel;
			}

			Button button = option.GetNode<Button>("SelectButton");
			button.TooltipText = $"预览{avatarDefinition.DisplayName}头像";
			button.Pressed += () => SelectAvatar(avatarDefinition.Id);

			_avatarGrid.AddChild(option);
		}

		template.QueueFree();
		RefreshOptionSelection();
	}

	private void SelectAvatar(string avatarId)
	{
		CurrentAvatarId = CharacterAvatarCatalog.NormalizeAvatarId(avatarId);
		RefreshOptionSelection();
		ApplyPreviewAvatar();
	}

	private void ConfirmSelection()
	{
		EmitSignal(SignalName.AvatarSelected, CurrentAvatarId);
		RequestClose();
	}

	private void BindActionButton(string buttonPath, CharacterAvatar.AvatarAction action)
	{
		Button button = GetNode<Button>(buttonPath);
		button.Pressed += () => _previewAvatar.PlayAction(action);
	}

	private void ApplyPreviewAvatar()
	{
		CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(CurrentAvatarId);
		_previewAvatar.AvatarRoot = avatarDefinition.RootPath;
		_previewName.Text = avatarDefinition.DisplayName;
		_previewAvatar.PlayIdle();
	}

	private void RefreshOptionSelection()
	{
		foreach ((string avatarId, PanelContainer panel) in _optionPanels)
		{
			panel.Modulate = avatarId == CurrentAvatarId ? SelectedOptionModulate : Colors.White;
		}
	}
}
