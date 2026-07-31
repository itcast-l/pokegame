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

	private static readonly Color ValidNameHintColor = new(
		MainMenuInteractiveStyle.PauseDerivedSecondaryText.R,
		MainMenuInteractiveStyle.PauseDerivedSecondaryText.G,
		MainMenuInteractiveStyle.PauseDerivedSecondaryText.B,
		0.88f);
	private static readonly Color EmptyNameHintColor = MainMenuInteractiveStyle.PauseDerivedGold;

	private ColorRect _scrim = default!;
	private Button _backButton = default!;
	private Button _confirmButton = default!;
	private Label _titleLabel = default!;
	private CharacterAvatar _previewAvatar = default!;
	private Label _previewKickerLabel = default!;
	private Label _avatarListKickerLabel = default!;
	private Label _playerNameKickerLabel = default!;
	private LineEdit _playerNameInput = default!;
	private Label _playerNameHint = default!;
	private GridContainer _avatarGrid = default!;
	private ScrollContainer _avatarGridScroll = default!;
	private readonly Dictionary<string, PanelContainer> _optionPanels = new();
	private readonly Dictionary<string, PanelContainer> _optionBackdrops = new();
	private readonly Dictionary<string, Button> _optionButtons = new();
	private readonly Dictionary<string, OceanMenuButtonFrame> _optionFrames = new();
	private readonly HashSet<string> _hoveredAvatarIds = new();
	private readonly List<AvatarOptionSlot> _avatarOptionSlots = new();

	public override void _Ready()
	{
		GameInputActions.EnsureRegistered();
		Localization.EnsureLoaded();
		CurrentAvatarId = CharacterAvatarCatalog.NormalizeUnlockedAvatarId(CurrentAvatarId);
		CurrentPlayerName = PlayerNameRules.Normalize(CurrentPlayerName);

		_scrim = GetNode<ColorRect>("Scrim");
		_scrim.GuiInput += OnScrimGuiInput;
		DialogPanel = GetNode<PanelContainer>("DialogCenter/DialogPanel");
		_backButton = GetNode<Button>("%BackButton");
		_backButton.FocusMode = Control.FocusModeEnum.All;
		_backButton.Pressed += RequestClose;

		_titleLabel = GetNode<Label>("%Title");
		_previewAvatar = GetNode<CharacterAvatar>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/Body/PreviewPanel/PreviewMargin/PreviewLayout/PreviewAvatarAnchor/PreviewAvatar");
		_previewKickerLabel = GetNode<Label>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/Body/PreviewPanel/PreviewMargin/PreviewLayout/PreviewKicker");
		_avatarListKickerLabel = GetNode<Label>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/Body/AvatarListPanel/AvatarListMargin/AvatarListLayout/AvatarListHeader/AvatarListKicker");
		_playerNameKickerLabel = GetNode<Label>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/Body/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PlayerNameKicker");
		_playerNameInput = GetNode<LineEdit>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/Body/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PlayerNameInput");
		_playerNameInput.FocusMode = Control.FocusModeEnum.All;
		_playerNameHint = GetNode<Label>("DialogCenter/DialogPanel/DialogMargin/DialogLayout/Body/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PlayerNameHint");
		_playerNameInput.GuiInput += OnPlayerNameInputGuiInput;
		_playerNameInput.TextChanged += OnPlayerNameTextChanged;
		_playerNameInput.TextSubmitted += OnPlayerNameTextSubmitted;
		_confirmButton = GetNode<Button>("%ConfirmButton");
		_confirmButton.FocusMode = Control.FocusModeEnum.All;
		_confirmButton.Pressed += ConfirmSelection;

		_avatarGrid = GetNode<GridContainer>("%AvatarGrid");
		_avatarGridScroll = GetNode<ScrollContainer>("%GridScroll");
		_avatarGridScroll.FollowFocus = true;
		MainMenuInteractiveStyle.ApplyActionButton(
			_backButton,
			18,
			MainMenuInteractiveStyle.PauseDerivedPalette);
		MainMenuInteractiveStyle.ApplyActionButton(
			_confirmButton,
			17,
			MainMenuInteractiveStyle.PauseDerivedPalette);
		MainMenuInteractiveStyle.ApplyThemedLineEdit(
			_playerNameInput,
			MainMenuInteractiveStyle.PauseDerivedPalette,
			18);
		SecondaryMenuGlassStyle.StyleScrollBar(
			_avatarGridScroll.GetVScrollBar(),
			SecondaryMenuGlassStyle.PauseDerivedVisual);
		SecondaryMenuGlassStyle.AttachPauseDerived(
			this,
			DialogPanel,
			_scrim,
			SecondaryMenuGlassStyle.PauseDerivedCoreDesignSize,
			true,
			GetNode<PanelContainer>("%PreviewPanel"),
			GetNode<PanelContainer>("%AvatarListPanel"));
		ConfigurePauseDerivedVisualStyle();
		BindAvatarOptions();
		ConfigureAvatarOptions();
		RefreshLocalizedText();
		ApplyPreviewAvatar();
		ApplyPlayerNameInput();
		FocusDefaultControlDeferred();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && IsNodeReady())
		{
			RefreshLocalizedText();
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (ShouldIgnoreCancelForNameEditing(inputEvent))
		{
			return;
		}

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
		for (int optionIndex = 1; optionIndex <= 15; optionIndex++)
		{
			if (_avatarGrid.GetNodeOrNull<OceanMenuButtonFrame>($"AvatarOption{optionIndex}") is not OceanMenuButtonFrame root)
			{
				continue;
			}

			_avatarOptionSlots.Add(new AvatarOptionSlot(
				root,
				root.GetNode<PanelContainer>("OptionPanel"),
				root.GetNode<PanelContainer>("OptionPanel/OptionMargin/OptionLayout/AvatarSlot/SlotBackdrop"),
				root.GetNode<CharacterAvatar>("OptionPanel/OptionMargin/OptionLayout/AvatarSlot/CharacterAvatar"),
				root.GetNode<Button>("SelectButton")));
		}
	}

	private void ConfigureAvatarOptions()
	{
		_optionPanels.Clear();
		_optionBackdrops.Clear();
		_optionButtons.Clear();
		_optionFrames.Clear();
		_hoveredAvatarIds.Clear();
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
			optionSlot.Avatar.PlayIdle();
			optionSlot.Panel.Modulate = Colors.White;
			ConfigureAvatarOptionFrame(optionSlot.Root);
			optionSlot.Button.TooltipText = Localize("AVATAR_EDIT_OPTION_TOOLTIP");
			optionSlot.Button.FocusMode = Control.FocusModeEnum.All;
			optionSlot.Button.FocusEntered += () => SelectAvatar(avatarDefinition.Id);
			optionSlot.Button.Pressed += () => SelectAvatar(avatarDefinition.Id);
			optionSlot.Button.MouseEntered += () => SetAvatarHovered(avatarDefinition.Id, true);
			optionSlot.Button.MouseExited += () => SetAvatarHovered(avatarDefinition.Id, false);
			_optionPanels[avatarDefinition.Id] = optionSlot.Panel;
			_optionBackdrops[avatarDefinition.Id] = optionSlot.Backdrop;
			_optionButtons[avatarDefinition.Id] = optionSlot.Button;
			_optionFrames[avatarDefinition.Id] = optionSlot.Root;
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
			panel.Modulate = Colors.White;
			if (_optionBackdrops.TryGetValue(avatarId, out PanelContainer? backdrop))
			{
				backdrop.AddThemeStyleboxOverride(
					"panel",
					CreateAvatarSlotStyle(
						avatarId == CurrentAvatarId,
						_hoveredAvatarIds.Contains(avatarId)));
			}
			if (_optionFrames.TryGetValue(avatarId, out OceanMenuButtonFrame? frame))
			{
				frame.Selected = avatarId == CurrentAvatarId;
				frame.QueueRedraw();
			}
		}
	}

	private void SetAvatarHovered(string avatarId, bool hovered)
	{
		if (hovered)
		{
			_hoveredAvatarIds.Add(avatarId);
		}
		else
		{
			_hoveredAvatarIds.Remove(avatarId);
		}

		RefreshOptionSelection();
	}

	private void ConfigurePauseDerivedVisualStyle()
	{
		ConfigureFrame("BackFrame");
		ConfigureFrame("ConfirmFrame");
		ConfigureSectionPanels();
		_titleLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		_previewKickerLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedGold);
		_avatarListKickerLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		_playerNameKickerLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedGold);
		if (FindChild("TitleUnderline", true, false) is Control titleDivider)
		{
			SecondaryMenuGlassStyle.InstallPauseDerivedDivider(titleDivider);
		}
		if (FindChild("PreviewGlow", true, false) is PanelContainer previewGlow)
		{
			previewGlow.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		}
	}

	private void ConfigureAvatarOptionFrame(OceanMenuButtonFrame frame)
	{
		frame.SuppressVisuals = true;
		frame.Selected = false;
		frame.HighlightFocus = false;
		frame.UsePlaqueGlowBackdrop = false;
		frame.ShowIdleTexture = false;
		frame.UseNinePatchTexture = false;
		frame.QueueRedraw();
	}

	private static StyleBoxFlat CreateAvatarSlotStyle(bool selected, bool hovered)
	{
		Color border = selected
			? MainMenuInteractiveStyle.PauseDerivedGold
			: MainMenuInteractiveStyle.PauseDerivedCyan;
		float borderAlpha = selected ? 0.96f : hovered ? 0.66f : 0.28f;
		int borderWidth = selected ? 2 : 1;
		return new StyleBoxFlat
		{
			BgColor = selected
				? new Color(0.035f, 0.22f, 0.22f, 0.80f)
				: hovered
					? new Color(0.025f, 0.18f, 0.20f, 0.76f)
					: new Color(0.018f, 0.115f, 0.14f, 0.68f),
			BorderColor = new Color(
				border.R,
				border.G,
				border.B,
				borderAlpha),
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10,
			ShadowColor = new Color(0.0f, 0.012f, 0.024f, selected ? 0.56f : 0.0f),
			ShadowSize = selected ? 8 : 0,
			ShadowOffset = new Vector2(0.0f, 3.0f),
		};
	}

	private void ConfigureSectionPanels()
	{
		PanelContainer previewPanel = GetNode<PanelContainer>("%PreviewPanel");
		PanelContainer avatarListPanel = GetNode<PanelContainer>("%AvatarListPanel");
		previewPanel.AddThemeStyleboxOverride("panel", CreateSectionStyle(
			new Color(0.027f, 0.122f, 0.176f, 0.78f),
			new Color(MainMenuInteractiveStyle.PauseDerivedGold.R,
				MainMenuInteractiveStyle.PauseDerivedGold.G,
				MainMenuInteractiveStyle.PauseDerivedGold.B,
				0.36f)));
		avatarListPanel.AddThemeStyleboxOverride("panel", CreateSectionStyle(
			new Color(0.008f, 0.050f, 0.074f, 0.74f),
			new Color(MainMenuInteractiveStyle.PauseDerivedCyan.R,
				MainMenuInteractiveStyle.PauseDerivedCyan.G,
				MainMenuInteractiveStyle.PauseDerivedCyan.B,
				0.34f)));
	}

	private static StyleBoxFlat CreateSectionStyle(Color background, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = Colors.Transparent,
			ShadowSize = 0,
		};
	}

	private void ConfigureFrame(string frameName)
	{
		if (FindChild(frameName, true, false) is OceanMenuButtonFrame frame)
		{
			SecondaryMenuGlassStyle.ConfigurePauseDerivedMinimalFrame(
				frame,
				OceanMenuButtonFrame.ButtonFrameStyle.IndividualAction,
				MainMenuInteractiveStyle.PauseDerivedCyan);
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

	private void OnPlayerNameInputGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventKey
			{
				Pressed: true,
				Echo: false,
			} keyEvent)
		{
			return;
		}

		if (keyEvent.Keycode == Key.Backspace)
		{
			DeletePlayerNameCharacter(backspace: true);
			_playerNameInput.AcceptEvent();
			return;
		}

		if (keyEvent.Keycode == Key.Delete)
		{
			DeletePlayerNameCharacter(backspace: false);
			_playerNameInput.AcceptEvent();
		}
	}

	private bool ShouldIgnoreCancelForNameEditing(InputEvent inputEvent)
	{
		if (_playerNameInput == null
			|| !GodotObject.IsInstanceValid(_playerNameInput)
			|| !_playerNameInput.HasFocus())
		{
			return false;
		}

		return inputEvent is InputEventKey
		{
			Pressed: true,
			Echo: false,
		} keyEvent
		&& (keyEvent.Keycode == Key.Backspace || keyEvent.Keycode == Key.Delete);
	}

	private void DeletePlayerNameCharacter(bool backspace)
	{
		if (_playerNameInput == null
			|| !GodotObject.IsInstanceValid(_playerNameInput)
			|| !_playerNameInput.Editable)
		{
			return;
		}

		string text = _playerNameInput.Text ?? string.Empty;
		if (text.Length == 0)
		{
			return;
		}

		int caret = Mathf.Clamp(_playerNameInput.CaretColumn, 0, text.Length);
		if (backspace)
		{
			if (caret <= 0)
			{
				return;
			}

			_playerNameInput.Text = text.Remove(caret - 1, 1);
			_playerNameInput.CaretColumn = caret - 1;
		}
		else
		{
			if (caret >= text.Length)
			{
				return;
			}

			_playerNameInput.Text = text.Remove(caret, 1);
			_playerNameInput.CaretColumn = caret;
		}

		OnPlayerNameTextChanged(_playerNameInput.Text);
	}

	private void UpdatePlayerNameHint()
	{
		string playerName = _playerNameInput.Text;
		bool isValidName = PlayerNameRules.IsValid(playerName);
		if (isValidName)
		{
			_playerNameHint.Text = LocalizeFormat("AVATAR_EDIT_PLAYER_NAME_HINT_VALID", PlayerNameRules.MaxLength);
		}
		else if (string.IsNullOrEmpty(playerName))
		{
			_playerNameHint.Text = Localize("AVATAR_EDIT_PLAYER_NAME_HINT_EMPTY");
		}
		else if (playerName.Length > PlayerNameRules.MaxLength)
		{
			_playerNameHint.Text = LocalizeFormat("AVATAR_EDIT_PLAYER_NAME_HINT_TOO_LONG", PlayerNameRules.MaxLength);
		}
		else
		{
			_playerNameHint.Text = Localize("AVATAR_EDIT_PLAYER_NAME_HINT_INVALID");
		}

		_playerNameHint.Modulate = isValidName ? ValidNameHintColor : EmptyNameHintColor;
		_confirmButton.Disabled = !isValidName;
	}

	private void RefreshLocalizedText()
	{
		_backButton.Text = Localize("ADVENTURE_BACK");
		_titleLabel.Text = Localize("AVATAR_EDIT_CURRENT_AVATAR");
		_previewKickerLabel.Text = Localize("AVATAR_EDIT_CURRENT_AVATAR");
		_avatarListKickerLabel.Text = Localize("AVATAR_EDIT_OPTION_TOOLTIP");
		_playerNameKickerLabel.Text = Localize("AVATAR_EDIT_PLAYER_NAME_LABEL");
		_playerNameInput.PlaceholderText = Localize("AVATAR_EDIT_PLAYER_NAME_PLACEHOLDER");
		_confirmButton.Text = Localize("AVATAR_EDIT_SAVE");

		foreach (Button button in _optionButtons.Values)
		{
			if (GodotObject.IsInstanceValid(button))
			{
				button.TooltipText = Localize("AVATAR_EDIT_OPTION_TOOLTIP");
			}
		}

		UpdatePlayerNameHint();
	}

	private string Localize(string key)
	{
		return Tr(key).ToString();
	}

	private string LocalizeFormat(string key, params object[] args)
	{
		return string.Format(Localize(key), args);
	}

	private sealed record AvatarOptionSlot(
		OceanMenuButtonFrame Root,
		PanelContainer Panel,
		PanelContainer Backdrop,
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
