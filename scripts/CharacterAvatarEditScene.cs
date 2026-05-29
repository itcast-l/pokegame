using System.Collections.Generic;
using Godot;

public partial class CharacterAvatarEditScene : Control
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	[Signal]
	public delegate void ProfileSavedEventHandler(string avatarId, string playerName);

	public string CurrentAvatarId { get; set; } = CharacterAvatarCatalog.DefaultAvatarId;
	public string CurrentPlayerName { get; set; } = PlayerNameRules.DefaultPlayerName;

	private static readonly Color SelectedOptionModulate = new(1.0f, 1.0f, 0.78f, 1.0f);
	private static readonly Color ValidNameHintColor = new(0.62f, 1.0f, 0.92f, 0.76f);
	private static readonly Color EmptyNameHintColor = new(1.0f, 0.72f, 0.58f, 0.92f);

	private Button _backButton = default!;
	private Button _confirmButton = default!;
	private CharacterAvatar _previewAvatar = default!;
	private Label _previewName = default!;
	private LineEdit _playerNameInput = default!;
	private Label _playerNameHint = default!;
	private GridContainer _avatarGrid = default!;
	private readonly Dictionary<string, PanelContainer> _optionPanels = new();
	private readonly List<AvatarOptionSlot> _avatarOptionSlots = new();

	public override void _Ready()
	{
		CurrentAvatarId = CharacterAvatarCatalog.NormalizeUnlockedAvatarId(CurrentAvatarId);
		CurrentPlayerName = PlayerNameRules.Normalize(CurrentPlayerName);

		_backButton = GetNode<Button>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/Header/HeaderLayout/BackButton");
		_backButton.Pressed += RequestClose;

		_previewAvatar = GetNode<CharacterAvatar>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewAvatarAnchor/PreviewAvatar");
		_previewName = GetNode<Label>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PreviewName");
		_playerNameInput = GetNode<LineEdit>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PlayerNameInput");
		_playerNameHint = GetNode<Label>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PlayerNameHint");
		_playerNameInput.TextChanged += OnPlayerNameTextChanged;
		_confirmButton = GetNode<Button>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/ConfirmButton");
		_confirmButton.Pressed += ConfirmSelection;

		_avatarGrid = GetNode<GridContainer>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/GridScroll/AvatarGrid");
		BindAvatarOptions();
		ConfigureAvatarOptions();
		ApplyPreviewAvatar();
		ApplyPlayerNameInput();
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

	private void BindAvatarOptions()
	{
		_avatarOptionSlots.Clear();
		for (int optionIndex = 1; optionIndex <= 15; optionIndex++)
		{
			if (_avatarGrid.GetNodeOrNull<Control>($"AvatarOption{optionIndex}") is not Control root)
			{
				continue;
			}

			_avatarOptionSlots.Add(new AvatarOptionSlot(
				root,
				root.GetNode<PanelContainer>("OptionPanel"),
				root.GetNode<CharacterAvatar>("OptionPanel/OptionMargin/OptionLayout/AvatarSlot/CharacterAvatar"),
				root.GetNode<Label>("OptionPanel/OptionMargin/OptionLayout/AvatarName"),
				root.GetNode<Button>("SelectButton")));
		}
	}

	private void ConfigureAvatarOptions()
	{
		_optionPanels.Clear();
		foreach (AvatarOptionSlot optionSlot in _avatarOptionSlots)
		{
			optionSlot.Root.Visible = false;
		}

		IReadOnlyList<CharacterAvatarDefinition> unlockedAvatars = CharacterAvatarCatalog.GetUnlockedAvatars();
		for (int avatarIndex = 0; avatarIndex < unlockedAvatars.Count; avatarIndex++)
		{
			if (avatarIndex >= _avatarOptionSlots.Count)
			{
				GD.PushWarning("Character avatar edit scene does not have enough option slots for every unlocked avatar.");
				break;
			}

			CharacterAvatarDefinition avatarDefinition = unlockedAvatars[avatarIndex];
			AvatarOptionSlot optionSlot = _avatarOptionSlots[avatarIndex];

			optionSlot.Root.Visible = true;
			optionSlot.Avatar.AvatarRoot = avatarDefinition.RootPath;
			optionSlot.Avatar.PlayIdle();
			optionSlot.Label.Text = avatarDefinition.DisplayName;
			optionSlot.Panel.Modulate = Colors.White;
			optionSlot.Button.TooltipText = $"预览{avatarDefinition.DisplayName}头像";
			optionSlot.Button.Pressed += () => SelectAvatar(avatarDefinition.Id);
			_optionPanels[avatarDefinition.Id] = optionSlot.Panel;
		}

		RefreshOptionSelection();
	}

	private void SelectAvatar(string avatarId)
	{
		CurrentAvatarId = CharacterAvatarCatalog.NormalizeUnlockedAvatarId(avatarId);
		RefreshOptionSelection();
		ApplyPreviewAvatar();
	}

	private void ConfirmSelection()
	{
		if (!PlayerNameRules.IsValid(_playerNameInput.Text))
		{
			UpdatePlayerNameHint();
			return;
		}

		CurrentPlayerName = _playerNameInput.Text;
		EmitSignal(SignalName.ProfileSaved, CurrentAvatarId, CurrentPlayerName);
		RequestClose();
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

	private void ApplyPlayerNameInput()
	{
		_playerNameInput.Text = CurrentPlayerName;
		_playerNameInput.CaretColumn = _playerNameInput.Text.Length;
		UpdatePlayerNameHint();
	}

	private void OnPlayerNameTextChanged(string text)
	{
		CurrentPlayerName = text;
		UpdatePlayerNameHint();
	}

	private void UpdatePlayerNameHint()
	{
		string playerName = _playerNameInput.Text;
		bool isValidName = PlayerNameRules.IsValid(playerName);
		if (isValidName)
		{
			_playerNameHint.Text = $"仅支持中英文，最多 {PlayerNameRules.MaxLength} 个字";
		}
		else if (string.IsNullOrEmpty(playerName))
		{
			_playerNameHint.Text = "请输入中英文名字";
		}
		else if (playerName.Length > PlayerNameRules.MaxLength)
		{
			_playerNameHint.Text = $"名字最多 {PlayerNameRules.MaxLength} 个字";
		}
		else
		{
			_playerNameHint.Text = "名字只能使用中文或英文字母";
		}

		_playerNameHint.Modulate = isValidName ? ValidNameHintColor : EmptyNameHintColor;
		_confirmButton.Disabled = !isValidName;
	}

	private sealed record AvatarOptionSlot(
		Control Root,
		PanelContainer Panel,
		CharacterAvatar Avatar,
		Label Label,
		Button Button);
}
