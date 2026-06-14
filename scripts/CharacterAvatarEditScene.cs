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
	public PanelContainer DialogPanel { get; private set; } = default!;

	private static readonly Color ValidNameHintColor = new(0.62f, 1.0f, 0.92f, 0.76f);
	private static readonly Color EmptyNameHintColor = new(1.0f, 0.72f, 0.58f, 0.92f);

	private ColorRect _scrim = default!;
	private Button _backButton = default!;
	private Button _confirmButton = default!;
	private CharacterAvatar _previewAvatar = default!;
	private LineEdit _playerNameInput = default!;
	private Label _playerNameHint = default!;
	private GridContainer _avatarGrid = default!;
	private ScrollContainer _avatarGridScroll = default!;
	private readonly Dictionary<string, PanelContainer> _optionPanels = new();
	private readonly Dictionary<string, Control> _optionSelectionRings = new();
	private readonly Dictionary<string, Button> _optionButtons = new();
	private readonly List<AvatarOptionSlot> _avatarOptionSlots = new();

	public override void _Ready()
	{
		GameInputActions.EnsureRegistered();
		CurrentAvatarId = CharacterAvatarCatalog.NormalizeUnlockedAvatarId(CurrentAvatarId);
		CurrentPlayerName = PlayerNameRules.Normalize(CurrentPlayerName);

		_scrim = GetNode<ColorRect>("Scrim");
		_scrim.GuiInput += OnScrimGuiInput;
		DialogPanel = GetNode<PanelContainer>("DialogCenter/DialogPanel");
		_backButton = GetNode<Button>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/Header/HeaderLayout/BackButton");
		_backButton.FocusMode = Control.FocusModeEnum.All;
		_backButton.Pressed += RequestClose;

		_previewAvatar = GetNode<CharacterAvatar>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewAvatarAnchor/PreviewAvatar");
		_playerNameInput = GetNode<LineEdit>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PlayerNameInput");
		_playerNameInput.FocusMode = Control.FocusModeEnum.All;
		_playerNameHint = GetNode<Label>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PlayerNameHint");
		_playerNameInput.TextChanged += OnPlayerNameTextChanged;
		_playerNameInput.TextSubmitted += OnPlayerNameTextSubmitted;
		_confirmButton = GetNode<Button>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/ConfirmButton");
		_confirmButton.FocusMode = Control.FocusModeEnum.All;
		_confirmButton.Pressed += ConfirmSelection;

		_avatarGrid = GetNode<GridContainer>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/GridScroll/AvatarGrid");
		_avatarGridScroll = GetNode<ScrollContainer>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/GridScroll");
		_avatarGridScroll.FollowFocus = true;
		BindAvatarOptions();
		ConfigureAvatarOptions();
		ApplyPreviewAvatar();
		ApplyPlayerNameInput();
		FocusDefaultControlDeferred();
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (GameInputActions.IsCancelPressed(inputEvent))
		{
			RequestClose();
			GetViewport().SetInputAsHandled();
		}
	}

	private void RequestClose()
	{
		EmitSignal(SignalName.CloseRequested);
	}

	private void OnScrimGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		RequestClose();
		GetViewport().SetInputAsHandled();
	}

	private void BindAvatarOptions()
	{
		_avatarOptionSlots.Clear();
		for (int optionIndex = 1; optionIndex <= 18; optionIndex++)
		{
			if (_avatarGrid.GetNodeOrNull<Control>($"AvatarOption{optionIndex}") is not Control root)
			{
				continue;
			}

			_avatarOptionSlots.Add(new AvatarOptionSlot(
				root,
				root.GetNode<Control>("SelectionRing"),
				root.GetNode<PanelContainer>("OptionPanel"),
				root.GetNode<CharacterAvatar>("OptionPanel/OptionMargin/OptionLayout/AvatarSlot/CharacterAvatar"),
				root.GetNode<Button>("SelectButton")));
		}
	}

	private void ConfigureAvatarOptions()
	{
		_optionPanels.Clear();
		_optionSelectionRings.Clear();
		_optionButtons.Clear();
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
			optionSlot.Avatar.AudioConfig = avatarDefinition.AudioConfig;
			optionSlot.Avatar.ShowIdleStill();
			optionSlot.Panel.Modulate = Colors.White;
			optionSlot.SelectionRing.Visible = false;
			optionSlot.Button.TooltipText = "预览头像";
			optionSlot.Button.FocusMode = Control.FocusModeEnum.All;
			optionSlot.Button.FocusEntered += () => SelectAvatar(avatarDefinition.Id);
			optionSlot.Button.Pressed += () => SelectAvatar(avatarDefinition.Id);
			_optionPanels[avatarDefinition.Id] = optionSlot.Panel;
			_optionSelectionRings[avatarDefinition.Id] = optionSlot.SelectionRing;
			_optionButtons[avatarDefinition.Id] = optionSlot.Button;
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
		_previewAvatar.AudioConfig = avatarDefinition.AudioConfig;
		_previewAvatar.PlayIdle();
	}

	private void RefreshOptionSelection()
	{
		foreach ((string avatarId, PanelContainer panel) in _optionPanels)
		{
			panel.Modulate = avatarId == CurrentAvatarId
				? new Color(1.04f, 1.04f, 1.02f, 1.0f)
				: Colors.White;
		}

		foreach ((string avatarId, Control ring) in _optionSelectionRings)
		{
			ring.Visible = avatarId == CurrentAvatarId;
		}
	}

	private void ApplyPlayerNameInput()
	{
		_playerNameInput.Text = CurrentPlayerName;
		_playerNameInput.CaretColumn = _playerNameInput.Text.Length;
		UpdatePlayerNameHint();
	}

	public void FocusDefaultControlDeferred()
	{
		CallDeferred(nameof(FocusDefaultControl));
	}

	private void FocusDefaultControl()
	{
		if (_optionButtons.TryGetValue(CurrentAvatarId, out Button? selectedButton))
		{
			GrabFocusIfAvailable(selectedButton);
			if (selectedButton.HasFocus())
			{
				return;
			}
		}

		GrabFocusIfAvailable(_backButton);
	}

	private void OnPlayerNameTextChanged(string text)
	{
		CurrentPlayerName = text;
		UpdatePlayerNameHint();
	}

	private void OnPlayerNameTextSubmitted(string _text)
	{
		ConfirmSelection();
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
		Control SelectionRing,
		PanelContainer Panel,
		CharacterAvatar Avatar,
		Button Button);

	private static void GrabFocusIfAvailable(Control? control)
	{
		if (control == null
			|| !GodotObject.IsInstanceValid(control)
			|| !control.IsVisibleInTree()
			|| control.FocusMode == Control.FocusModeEnum.None)
		{
			return;
		}

		if (control is BaseButton { Disabled: true })
		{
			return;
		}

		control.GrabFocus();
	}
}
