using Godot;
using System;
using System.Collections.Generic;

public partial class MainMenuSkillCodexOverlay : CanvasLayer
{
	private const string CardScenePath = "res://scenes/adventure_upgrade_catalog_card.tscn";
	private const float CatalogCardWidth = 196.0f;
	private const float CatalogCardHeight = 322.0f;
	private const float CatalogCardGap = 16.0f;
	private static readonly string[] SuitOrder = { "hearts", "diamonds", "spades", "clubs" };
	private static readonly StyleBoxFlat HiddenFocusStyle = new() { BgColor = Colors.Transparent };

	public event Action? CloseRequested;

	[ExportGroup("Testing")]
	[Export]
	public bool UnlockAllForTesting { get; set; }

	private Button _backButton = default!;
	private Control _root = default!;
	private Label _titleLabel = default!;
	private readonly Dictionary<string, Button> _suitButtons = new(StringComparer.Ordinal);
	private readonly Dictionary<Button, StyleBox> _buttonFocusStyles = new();
	private ScrollContainer _scrollContainer = default!;
	private GridContainer _grid = default!;
	private Label _emptyLabel = default!;
	private Control _previewLayer = default!;
	private Control _previewFocusAnchor = default!;
	private AdventureUpgradeCatalogCard _previewCard = default!;
	private PackedScene? _cardScene;
	private AdventureSuitUpgradeCatalog? _catalog;
	private AdventureUpgradeManifest? _manifest;
	private readonly HashSet<string> _unlockedUpgradeIds = new(StringComparer.Ordinal);
	private readonly List<AdventureUpgradeCatalogCard> _visibleCards = new();
	private readonly List<AdventureSuitUpgradeDefinition> _visibleDefinitions = new();
	private string _currentSuit = "hearts";
	private string _previewUpgradeId = string.Empty;
	private Control? _focusBeforePreview;
	private bool _acceptInput;
	private bool _inputFocusVisible;
	private bool _lastInputWasKeyboardOrJoypad;

	public bool IsOpen => Visible;
	public bool IsPreviewOpen => _previewLayer.Visible;
	public Control TransitionRoot => _root;

	public override void _Ready()
	{
		_root = GetNode<Control>("Root");
		_backButton = GetNode<Button>("Root/SafeMargin/Layout/TopBar/TopBarLayout/BackButton");
		_titleLabel = GetNode<Label>("Root/SafeMargin/Layout/TopBar/TopBarLayout/Title");
		_scrollContainer = GetNode<ScrollContainer>("Root/SafeMargin/Layout/CardScroll");
		_grid = GetNode<GridContainer>("Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid");
		_emptyLabel = GetNode<Label>("Root/SafeMargin/Layout/EmptyLabel");
		_previewLayer = GetNode<Control>("Root/PreviewLayer");
		_previewFocusAnchor = GetNode<Control>("Root/PreviewLayer/PreviewFocusAnchor");
		_previewCard = GetNode<AdventureUpgradeCatalogCard>("Root/PreviewLayer/PreviewCenter/PreviewCard");

		foreach (string suit in SuitOrder)
		{
			Button button = GetNode<Button>($"Root/SafeMargin/Layout/SuitTabs/{ToPascalCase(suit)}Button");
			_suitButtons[suit] = button;
			string capturedSuit = suit;
			button.Pressed += () => SelectSuit(capturedSuit, focusFirstCard: true);
		}
		RememberButtonFocusStyle(_backButton);
		foreach (Button button in _suitButtons.Values)
		{
			RememberButtonFocusStyle(button);
		}

		_backButton.Pressed += RequestClose;
		_root.Resized += () => CallDeferred(nameof(RefreshGridColumns));
		_scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		_cardScene = GD.Load<PackedScene>(CardScenePath);
		LoadCatalog();
		RefreshLocalizedText();
		Visible = false;
		SetProcessUnhandledInput(false);
		SetInputFocusVisible(false);
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
		if (!Visible || !GameInputActions.IsCancelPressed(inputEvent))
		{
			return;
		}

		HandleCancel();
		GetViewport().SetInputAsHandled();
	}

	public override void _Input(InputEvent inputEvent)
	{
		TrackInputMode(inputEvent);
	}

	public void TrackInputMode(InputEvent inputEvent)
	{
		if (IsMouseInput(inputEvent))
		{
			_lastInputWasKeyboardOrJoypad = false;
			if (Visible && _acceptInput)
			{
				SetInputFocusVisible(false);
			}
			return;
		}

		if (IsKeyboardOrJoypadNavigationInput(inputEvent))
		{
			_lastInputWasKeyboardOrJoypad = true;
			if (Visible && _acceptInput)
			{
				SetInputFocusVisible(true);
			}
		}
	}

	public void Open()
	{
		LoadCatalog();
		LoadUnlockedUpgradeIds();
		Visible = true;
		_acceptInput = true;
		_root.MouseFilter = Control.MouseFilterEnum.Stop;
		SetProcessUnhandledInput(true);
		SetInputFocusVisible(
			_lastInputWasKeyboardOrJoypad || IsKeyboardOrJoypadActionPressedNow());
		ClosePreview();
		SelectSuit("hearts", focusFirstCard: false);
		CallDeferred(nameof(RefreshGridColumns));
		CallDeferred(nameof(FocusFirstAvailableControl));
	}

	public void Close()
	{
		BeginClose();
		Visible = false;
	}

	public void BeginClose()
	{
		if (!Visible || !_acceptInput)
		{
			return;
		}

		ClosePreview();
		_acceptInput = false;
		_root.MouseFilter = Control.MouseFilterEnum.Ignore;
		SetProcessUnhandledInput(false);
		GetViewport().GuiReleaseFocus();
	}

	public bool HandleCancel()
	{
		if (!Visible || !_acceptInput)
		{
			return false;
		}

		if (_previewLayer.Visible)
		{
			ClosePreview();
			return true;
		}

		RequestClose();
		return true;
	}

	public int DebugGetVisibleCardCount() => _visibleCards.Count;

	public int DebugGetTotalEnabledCardCount()
	{
		int count = 0;
		if (_catalog == null)
		{
			return count;
		}

		foreach (AdventureSuitUpgradeDefinition definition in _catalog.All)
		{
			if (definition.Enabled)
			{
				count++;
			}
		}
		return count;
	}

	public string DebugGetCurrentSuit() => _currentSuit;
	public int DebugGetGridColumns() => _grid.Columns;
	public bool DebugIsPreviewVisible() => _previewLayer.Visible;
	public bool DebugIsInputFocusVisible() => _inputFocusVisible;

	public bool DebugSelectSuit(string suit)
	{
		return SelectSuit(suit, focusFirstCard: true);
	}

	public bool DebugActivateVisibleCard(int index)
	{
		if (index < 0 || index >= _visibleCards.Count || _visibleCards[index].IsLocked)
		{
			return false;
		}

		OpenPreview(_visibleDefinitions[index], _visibleCards[index]);
		return true;
	}

	public int DebugGetCurrentSuitUnlockedCardCount()
	{
		int count = 0;
		foreach (AdventureUpgradeCatalogCard card in _visibleCards)
		{
			if (!card.IsLocked)
			{
				count++;
			}
		}
		return count;
	}

	public int DebugGetTotalUnlockedCardCount()
	{
		int count = 0;
		if (_catalog == null)
		{
			return count;
		}

		foreach (AdventureSuitUpgradeDefinition definition in _catalog.All)
		{
			if (definition.Enabled && _unlockedUpgradeIds.Contains(definition.Id))
			{
				count++;
			}
		}
		return count;
	}

	public Godot.Collections.Array DebugGetVisibleCardStates()
	{
		var states = new Godot.Collections.Array();
		for (int index = 0; index < _visibleCards.Count; index++)
		{
			Godot.Collections.Dictionary state = _visibleCards[index].DebugGetCardState();
			state["id"] = _visibleDefinitions[index].Id;
			state["sort_order"] = _visibleDefinitions[index].SortOrder;
			state["scene_file_path"] = _visibleCards[index].SceneFilePath;
			states.Add(state);
		}
		return states;
	}

	public Godot.Collections.Dictionary DebugGetPreviewCardState()
	{
		Godot.Collections.Dictionary state = _previewCard.DebugGetCardState();
		state["id"] = _previewUpgradeId;
		return state;
	}

	private void LoadCatalog()
	{
		_catalog = AdventureSuitUpgradeCatalog.Load(this);
		_manifest = _catalog.TryCreateManifest(out AdventureUpgradeManifest manifest)
			? manifest
			: null;
	}

	private void LoadUnlockedUpgradeIds()
	{
		_unlockedUpgradeIds.Clear();
		if (UnlockAllForTesting && OS.IsDebugBuild() && _catalog != null)
		{
			foreach (AdventureSuitUpgradeDefinition definition in _catalog.All)
			{
				if (definition.Enabled)
				{
					_unlockedUpgradeIds.Add(definition.Id);
				}
			}
			return;
		}

		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		foreach (string upgradeId in SkillCodexUnlockRules.ParseUpgradeIds(
			profile.UnlockedSkillCodexUpgradeIdsCsv))
		{
			_unlockedUpgradeIds.Add(upgradeId);
		}
	}

	private bool SelectSuit(string suit, bool focusFirstCard)
	{
		if (!_acceptInput)
		{
			return false;
		}

		string normalizedSuit = suit?.Trim().ToLowerInvariant() ?? string.Empty;
		if (Array.IndexOf(SuitOrder, normalizedSuit) < 0)
		{
			return false;
		}

		_currentSuit = normalizedSuit;
		ClosePreview();
		RebuildCards();
		RefreshSuitTabs();
		CallDeferred(nameof(RefreshGridColumns));
		if (focusFirstCard)
		{
			CallDeferred(nameof(FocusFirstAvailableControl));
		}
		return true;
	}

	private void RebuildCards()
	{
		foreach (Node child in _grid.GetChildren())
		{
			_grid.RemoveChild(child);
			child.QueueFree();
		}
		_visibleCards.Clear();
		_visibleDefinitions.Clear();

		if (_catalog == null || _cardScene == null)
		{
			_emptyLabel.Visible = true;
			return;
		}

		foreach (AdventureSuitUpgradeDefinition definition in _catalog.All)
		{
			if (!definition.Enabled
				|| !string.Equals(definition.Suit, _currentSuit, StringComparison.Ordinal))
			{
				continue;
			}

			if (_cardScene.Instantiate() is not AdventureUpgradeCatalogCard card)
			{
				continue;
			}

			card.Name = $"UpgradeCard_{definition.Id}";
			card.CustomMinimumSize = new Vector2(CatalogCardWidth, CatalogCardHeight);
			_grid.AddChild(card);
			bool unlocked = _unlockedUpgradeIds.Contains(definition.Id);
			if (unlocked)
			{
				card.ConfigureCard(
					definition.Suit,
					definition.Rarity,
					FormatDescription(definition),
					Localize(GetRarityKey(definition.Rarity)),
					interactive: true);
				AdventureSuitUpgradeDefinition capturedDefinition = definition;
				card.Pressed += () => OpenPreview(capturedDefinition, card);
				card.FocusEntered += () => _scrollContainer.EnsureControlVisible(card);
			}
			else
			{
				card.ConfigureLockedCard(Localize("SKILL_CODEX_LOCKED"));
			}
			card.SetFocusHighlightEnabled(_inputFocusVisible);
			_visibleCards.Add(card);
			_visibleDefinitions.Add(definition);
		}

		_emptyLabel.Visible = _visibleCards.Count == 0;
	}

	private string FormatDescription(AdventureSuitUpgradeDefinition definition)
	{
		string localized = LocalizeDescription(definition);
		AdventureUpgradeManifestEntry? entry = null;
		if (_manifest?.TryGetEnabledEntry(definition.Id, out AdventureUpgradeManifestEntry resolvedEntry) == true)
		{
			entry = resolvedEntry;
		}

		return AdventureUpgradeDefinitionDescriptionFormatter.Format(
			definition,
			entry,
			localized,
			currentItemCount: 0);
	}

	private string LocalizeDescription(AdventureSuitUpgradeDefinition definition)
	{
		string localized = Tr(definition.DescriptionKey).ToString();
		return string.IsNullOrWhiteSpace(localized)
			|| string.Equals(localized, definition.DescriptionKey, StringComparison.Ordinal)
			? definition.EffectText
			: localized;
	}

	private void OpenPreview(
		AdventureSuitUpgradeDefinition definition,
		AdventureUpgradeCatalogCard sourceCard)
	{
		if (!_acceptInput
			|| sourceCard.IsLocked
			|| !_unlockedUpgradeIds.Contains(definition.Id))
		{
			return;
		}

		_focusBeforePreview = sourceCard;
		_previewUpgradeId = definition.Id;
		_previewCard.ConfigureCard(
			definition.Suit,
			definition.Rarity,
			FormatDescription(definition),
			Localize(GetRarityKey(definition.Rarity)),
			interactive: false);
		_previewLayer.Visible = true;
		_previewFocusAnchor.GrabFocus();
	}

	private void ClosePreview()
	{
		if (_previewLayer == null || !_previewLayer.Visible)
		{
			return;
		}

		_previewLayer.Visible = false;
		_previewUpgradeId = string.Empty;
		if (_focusBeforePreview != null
			&& GodotObject.IsInstanceValid(_focusBeforePreview)
			&& _focusBeforePreview.IsVisibleInTree())
		{
			_focusBeforePreview.GrabFocus();
		}
		_focusBeforePreview = null;
	}

	private void RequestClose()
	{
		if (_acceptInput)
		{
			CloseRequested?.Invoke();
		}
	}

	private void RefreshLocalizedText()
	{
		_titleLabel.Text = Localize("SKILL_CODEX_TITLE");
		_backButton.Text = Localize("SKILL_CODEX_BACK");
		_emptyLabel.Text = Localize("SKILL_CODEX_EMPTY");
		foreach (string suit in SuitOrder)
		{
			_suitButtons[suit].Text = $"{GetSuitSymbol(suit)} {Localize($"SKILL_CODEX_SUIT_{suit.ToUpperInvariant()}")}";
		}

		string previewId = _previewUpgradeId;
		RebuildCards();
		RefreshSuitTabs();
		if (!string.IsNullOrEmpty(previewId))
		{
			for (int index = 0; index < _visibleDefinitions.Count; index++)
			{
				if (string.Equals(_visibleDefinitions[index].Id, previewId, StringComparison.Ordinal))
				{
					OpenPreview(_visibleDefinitions[index], _visibleCards[index]);
					break;
				}
			}
		}
	}

	private void RefreshSuitTabs()
	{
		foreach (KeyValuePair<string, Button> pair in _suitButtons)
		{
			bool selected = string.Equals(pair.Key, _currentSuit, StringComparison.Ordinal);
			pair.Value.ButtonPressed = selected;
			pair.Value.AddThemeColorOverride(
				"font_color",
				selected
					? new Color(1.0f, 0.93f, 0.68f, 1.0f)
					: new Color(0.72f, 0.94f, 0.94f, 0.94f));
		}
	}

	private void RefreshGridColumns()
	{
		float availableWidth = Mathf.Max(0.0f, _scrollContainer.Size.X - 48.0f);
		_grid.Columns = Mathf.Clamp(
			Mathf.FloorToInt((availableWidth + CatalogCardGap) / (CatalogCardWidth + CatalogCardGap)),
			2,
			4);
	}

	private void FocusFirstAvailableControl()
	{
		if (!Visible)
		{
			return;
		}

		foreach (AdventureUpgradeCatalogCard card in _visibleCards)
		{
			if (!card.IsLocked)
			{
				card.GrabFocus();
				_scrollContainer.EnsureControlVisible(card);
				return;
			}
		}

		_backButton.GrabFocus();
	}

	private void RememberButtonFocusStyle(Button button)
	{
		_buttonFocusStyles[button] = button.GetThemeStylebox("focus");
	}

	private void SetInputFocusVisible(bool visible)
	{
		_inputFocusVisible = visible;
		foreach (KeyValuePair<Button, StyleBox> pair in _buttonFocusStyles)
		{
			if (GodotObject.IsInstanceValid(pair.Key))
			{
				pair.Key.AddThemeStyleboxOverride("focus", visible ? pair.Value : HiddenFocusStyle);
			}
		}

		foreach (AdventureUpgradeCatalogCard card in _visibleCards)
		{
			if (GodotObject.IsInstanceValid(card))
			{
				card.SetFocusHighlightEnabled(visible);
			}
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
			|| inputEvent is InputEventJoypadMotion joypadMotion
				&& Mathf.Abs(joypadMotion.AxisValue) > 0.45f;
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

	private string Localize(string key)
	{
		string text = Tr(key).ToString();
		return text == key ? key : text;
	}

	private static string GetRarityKey(string rarity)
	{
		return rarity?.Trim().ToLowerInvariant() switch
		{
			"rare" => "SKILL_CODEX_RARITY_RARE",
			"legendary" => "SKILL_CODEX_RARITY_LEGENDARY",
			_ => "SKILL_CODEX_RARITY_COMMON",
		};
	}

	private static string GetSuitSymbol(string suit)
	{
		return suit switch
		{
			"hearts" => "\u2665",
			"diamonds" => "\u2666",
			"spades" => "\u2660",
			"clubs" => "\u2663",
			_ => string.Empty,
		};
	}

	private static string ToPascalCase(string value)
	{
		return string.IsNullOrEmpty(value)
			? string.Empty
			: char.ToUpperInvariant(value[0]) + value[1..];
	}
}
