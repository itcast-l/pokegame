using Godot;
using System;
using System.Collections.Generic;

public partial class MainMenuSkillCodexOverlay : CanvasLayer
{
	private const string UpgradeCardScenePath = "res://scenes/adventure_upgrade_catalog_card.tscn";
	private const string ItemCardScenePath = "res://scenes/main_menu_item_codex_card.tscn";
	private const string UpgradesCategory = "upgrades";
	private const string ItemsCategory = "items";
	private const float CatalogCardWidth = 196.0f;
	private const float CatalogCardHeight = 322.0f;
	private const float CatalogCardGap = 16.0f;
	private const float MinimumCatalogCardHeight = 208.0f;
	private const int MaximumVisibleUpgradeRows = 3;
	private const int MaximumVisibleItemRows = 3;
	private const int MaximumVisibleCatalogColumns = 8;
	private static readonly string[] SuitOrder = { "hearts", "diamonds", "spades", "clubs" };
	private static readonly StyleBoxFlat HiddenFocusStyle = new() { BgColor = Colors.Transparent };

	public event Action? CloseRequested;

#if DECKLAND_TEST_HOOKS
	[ExportGroup("Testing")]
	[Export]
	public bool UnlockAllForTesting { get; set; }
#endif

	private Button _backButton = default!;
	private Control _root = default!;
	private Label _titleLabel = default!;
	private Button _upgradesButton = default!;
	private Button _itemsButton = default!;
	private Control _suitTabs = default!;
	private readonly Dictionary<string, Button> _suitButtons = new(StringComparer.Ordinal);
	private readonly Dictionary<Button, StyleBox> _buttonFocusStyles = new();
	private ScrollContainer _scrollContainer = default!;
	private GridContainer _grid = default!;
	private Label _emptyLabel = default!;
	private Control _previewLayer = default!;
	private Control _previewFocusAnchor = default!;
	private AdventureUpgradeCatalogCard _previewCard = default!;
	private MainMenuItemCodexCard _previewItemCard = default!;
	private PackedScene? _upgradeCardScene;
	private PackedScene? _itemCardScene;
	private AdventureSuitUpgradeCatalog? _catalog;
	private AdventureUpgradeManifest? _manifest;
	private ItemCardCatalog? _itemCatalog;
	private readonly HashSet<string> _unlockedUpgradeIds = new(StringComparer.Ordinal);
	private readonly List<AdventureUpgradeCatalogCard> _visibleCards = new();
	private readonly List<AdventureSuitUpgradeDefinition> _visibleDefinitions = new();
	private readonly List<MainMenuItemCodexCard> _visibleItemCards = new();
	private readonly List<ItemCardDefinition> _visibleItemDefinitions = new();
	private string _currentCategory = UpgradesCategory;
	private string _currentSuit = "hearts";
	private string _previewUpgradeId = string.Empty;
	private string _previewItemCardId = string.Empty;
	private Control? _focusBeforePreview;
	private bool _acceptInput;
	private bool _inputFocusVisible;
	private bool _lastInputWasKeyboardOrJoypad;

	public bool IsOpen => Visible;
	public bool IsPreviewOpen => _previewLayer.Visible;
	public Control TransitionRoot => _root;

	public override void _Ready()
	{
		GameInputActions.EnsureRegistered();
		_root = GetNode<Control>("Root");
		_backButton = GetNode<Button>("Root/SafeMargin/Layout/TopBar/TopBarLayout/BackButton");
		_titleLabel = GetNode<Label>("Root/SafeMargin/Layout/TopBar/TopBarLayout/Title");
		_upgradesButton = GetNode<Button>("Root/SafeMargin/Layout/CategoryTabs/UpgradesButton");
		_itemsButton = GetNode<Button>("Root/SafeMargin/Layout/CategoryTabs/ItemsButton");
		_suitTabs = GetNode<Control>("Root/SafeMargin/Layout/SuitTabs");
		_scrollContainer = GetNode<ScrollContainer>(
			"Root/SafeMargin/Layout/CatalogFrame/CatalogMargin/CatalogContent/CardScroll");
		_grid = GetNode<GridContainer>(
			"Root/SafeMargin/Layout/CatalogFrame/CatalogMargin/CatalogContent/CardScroll/GridCenter/CardGrid");
		_emptyLabel = GetNode<Label>(
			"Root/SafeMargin/Layout/CatalogFrame/CatalogMargin/CatalogContent/EmptyLabel");
		_previewLayer = GetNode<Control>("Root/PreviewLayer");
		_previewFocusAnchor = GetNode<Control>("Root/PreviewLayer/PreviewFocusAnchor");
		_previewCard = GetNode<AdventureUpgradeCatalogCard>("Root/PreviewLayer/PreviewCenter/PreviewCard");
		_previewItemCard = GetNode<MainMenuItemCodexCard>("Root/PreviewLayer/PreviewCenter/PreviewItemCard");

		_upgradesButton.Pressed += () => SelectCategory(UpgradesCategory, focusFirstCard: true);
		_itemsButton.Pressed += () => SelectCategory(ItemsCategory, focusFirstCard: true);

		foreach (string suit in SuitOrder)
		{
			Button button = GetNode<Button>($"Root/SafeMargin/Layout/SuitTabs/{ToPascalCase(suit)}Button");
			_suitButtons[suit] = button;
			string capturedSuit = suit;
			button.Pressed += () => SelectSuit(capturedSuit, focusFirstCard: true);
		}
		ConfigureUnifiedVisualStyle();
		RememberButtonFocusStyle(_backButton);

		_backButton.Pressed += RequestClose;
		_root.Resized += () => CallDeferred(nameof(RefreshGridColumns));
		_scrollContainer.Resized += () => CallDeferred(nameof(RefreshGridColumns));
		_scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		_upgradeCardScene = GD.Load<PackedScene>(UpgradeCardScenePath);
		_itemCardScene = GD.Load<PackedScene>(ItemCardScenePath);
		LoadCatalogs();
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
		LoadCatalogs();
		LoadUnlockedUpgradeIds();
		Visible = true;
		_acceptInput = true;
		_root.MouseFilter = Control.MouseFilterEnum.Stop;
		SetProcessUnhandledInput(true);
		SetInputFocusVisible(
			_lastInputWasKeyboardOrJoypad || IsKeyboardOrJoypadActionPressedNow());
		ClosePreview();
		_currentSuit = "hearts";
		SelectCategory(UpgradesCategory, focusFirstCard: false);
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

#if DECKLAND_TEST_HOOKS
	public int DebugGetVisibleCardCount() =>
		_currentCategory == ItemsCategory ? _visibleItemCards.Count : _visibleCards.Count;

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
	public string DebugGetCurrentCategory() => _currentCategory;
	public int DebugGetGridColumns() => _grid.Columns;
	public bool DebugIsPreviewVisible() => _previewLayer.Visible;
	public bool DebugIsInputFocusVisible() => _inputFocusVisible;
	public bool DebugAreSuitTabsVisible() => _suitTabs.Visible;

	public int DebugGetTotalEnabledItemCardCount()
	{
		int count = 0;
		if (_itemCatalog == null)
		{
			return count;
		}

		foreach (string itemCardId in _itemCatalog.AllIds)
		{
			if (_itemCatalog.IsEnabled(itemCardId))
			{
				count++;
			}
		}
		return count;
	}

	public bool DebugSelectCategory(string category)
	{
		return SelectCategory(category, focusFirstCard: true);
	}

	public bool DebugSelectSuit(string suit)
	{
		return SelectSuit(suit, focusFirstCard: true);
	}

	public bool DebugActivateVisibleCard(int index)
	{
		if (_currentCategory == ItemsCategory)
		{
			return DebugActivateVisibleItemCard(index);
		}

		if (index < 0 || index >= _visibleCards.Count || _visibleCards[index].IsLocked)
		{
			return false;
		}

		OpenPreview(_visibleDefinitions[index], _visibleCards[index]);
		return true;
	}

	public bool DebugActivateVisibleItemCard(int index)
	{
		if (index < 0 || index >= _visibleItemCards.Count)
		{
			return false;
		}

		OpenItemPreview(_visibleItemDefinitions[index], _visibleItemCards[index]);
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

	public Godot.Collections.Array DebugGetVisibleItemCardStates()
	{
		var states = new Godot.Collections.Array();
		for (int index = 0; index < _visibleItemCards.Count; index++)
		{
			Godot.Collections.Dictionary state = _visibleItemCards[index].DebugGetCardState();
			state["sort_order"] = _visibleItemDefinitions[index].SortOrder;
			state["scene_file_path"] = _visibleItemCards[index].SceneFilePath;
			states.Add(state);
		}
		return states;
	}

	public Godot.Collections.Dictionary DebugGetPreviewCardState()
	{
		if (!string.IsNullOrEmpty(_previewItemCardId))
		{
			Godot.Collections.Dictionary itemState = _previewItemCard.DebugGetCardState();
			itemState["kind"] = ItemsCategory;
			return itemState;
		}

		Godot.Collections.Dictionary state = _previewCard.DebugGetCardState();
		state["id"] = _previewUpgradeId;
		state["kind"] = UpgradesCategory;
		return state;
	}
#endif

	private void LoadCatalogs()
	{
		_catalog = AdventureSuitUpgradeCatalog.Load(this);
		_manifest = _catalog.TryCreateManifest(out AdventureUpgradeManifest manifest)
			? manifest
			: null;
		if (!ItemCardCatalog.TryLoad(this, out ItemCardCatalog itemCatalog, out string errorMessage))
		{
			_itemCatalog = null;
			GD.PushError($"Unable to load item codex catalog: {errorMessage}");
			return;
		}

		_itemCatalog = itemCatalog;
	}

	private void LoadUnlockedUpgradeIds()
	{
		_unlockedUpgradeIds.Clear();
#if DECKLAND_TEST_HOOKS
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
#endif

		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		foreach (string upgradeId in SkillCodexUnlockRules.ParseUpgradeIds(
			profile.UnlockedSkillCodexUpgradeIdsCsv))
		{
			_unlockedUpgradeIds.Add(upgradeId);
		}
	}

	private bool SelectSuit(string suit, bool focusFirstCard)
	{
		if (!_acceptInput || _currentCategory != UpgradesCategory)
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
		_scrollContainer.ScrollVertical = 0;
		CallDeferred(nameof(RefreshGridColumns));
		if (focusFirstCard)
		{
			CallDeferred(nameof(FocusFirstAvailableControl));
		}
		return true;
	}

	private bool SelectCategory(string category, bool focusFirstCard)
	{
		if (!_acceptInput)
		{
			return false;
		}

		string normalizedCategory = category?.Trim().ToLowerInvariant() ?? string.Empty;
		if (normalizedCategory != UpgradesCategory && normalizedCategory != ItemsCategory)
		{
			return false;
		}

		_currentCategory = normalizedCategory;
		_suitTabs.Visible = _currentCategory == UpgradesCategory;
		ClosePreview();
		RebuildCards();
		RefreshCategoryTabs();
		RefreshSuitTabs();
		_scrollContainer.ScrollVertical = 0;
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
		_visibleItemCards.Clear();
		_visibleItemDefinitions.Clear();

		if (_currentCategory == ItemsCategory)
		{
			RebuildItemCards();
			return;
		}

		if (_catalog == null || _upgradeCardScene == null)
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

			if (_upgradeCardScene.Instantiate() is not AdventureUpgradeCatalogCard card)
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
					interactive: true,
					showRarityLabel: false);
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

	private void RebuildItemCards()
	{
		if (_itemCatalog == null || _itemCardScene == null)
		{
			_emptyLabel.Visible = true;
			return;
		}

		foreach (string itemCardId in _itemCatalog.AllIds)
		{
			if (!_itemCatalog.IsEnabled(itemCardId)
				|| !_itemCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
				|| _itemCardScene.Instantiate() is not MainMenuItemCodexCard card)
			{
				continue;
			}

			_itemCatalog.TryGetFirstFrameTexture(itemCardId, out Texture2D texture);
			card.Name = $"ItemCard_{ToPascalCase(itemCardId)}";
			card.CustomMinimumSize = new Vector2(CatalogCardWidth, CatalogCardHeight);
			_grid.AddChild(card);
			card.ConfigureCard(
				itemCardId,
				texture,
				LocalizeItemName(definition),
				LocalizeItemDescription(definition),
				interactive: true);
			ItemCardDefinition capturedDefinition = definition;
			card.Pressed += () => OpenItemPreview(capturedDefinition, card);
			card.FocusEntered += () => _scrollContainer.EnsureControlVisible(card);
			card.SetFocusHighlightEnabled(_inputFocusVisible);
			_visibleItemCards.Add(card);
			_visibleItemDefinitions.Add(definition);
		}

		_emptyLabel.Visible = _visibleItemCards.Count == 0;
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
			currentItemCount: GetCurrentItemCount());
	}

	private int GetCurrentItemCount()
	{
		AdventureRunSessionStore? store =
			GetNodeOrNull<AdventureRunSessionStore>("/root/AdventureRunSessionStore");
		return store?.HasActiveRun == true
			? store.GetReadModel().Effects.QuestionRewardCount
			: 0;
	}

	private string LocalizeDescription(AdventureSuitUpgradeDefinition definition)
	{
		string localized = Tr(definition.DescriptionKey).ToString();
		if (string.IsNullOrWhiteSpace(localized)
			|| string.Equals(localized, definition.DescriptionKey, StringComparison.Ordinal))
		{
			GD.PushError($"Missing skill codex localization key: {definition.DescriptionKey}");
		}

		return localized;
	}

	private string LocalizeItemName(ItemCardDefinition definition)
	{
		const string DescriptionPrefix = "ITEM_DESC_";
		string nameKey = definition.DescriptionKey.StartsWith(DescriptionPrefix, StringComparison.Ordinal)
			? $"ITEM_NAME_{definition.DescriptionKey[DescriptionPrefix.Length..]}"
			: string.Empty;
		string localized = string.IsNullOrEmpty(nameKey) ? string.Empty : Localize(nameKey);
		return string.IsNullOrWhiteSpace(localized) || string.Equals(localized, nameKey, StringComparison.Ordinal)
			? definition.DisplayName
			: localized;
	}

	private string LocalizeItemDescription(ItemCardDefinition definition)
	{
		string localized = Localize(definition.DescriptionKey);
		if (string.IsNullOrWhiteSpace(localized)
			|| string.Equals(localized, definition.DescriptionKey, StringComparison.Ordinal))
		{
			GD.PushError($"Missing item codex localization key: {definition.DescriptionKey}");
			return definition.Description;
		}

		return localized;
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
		_previewItemCardId = string.Empty;
		_previewCard.ConfigureCard(
			definition.Suit,
			definition.Rarity,
			FormatDescription(definition),
			interactive: false,
			showRarityLabel: false);
		_previewCard.Visible = true;
		_previewItemCard.Visible = false;
		_previewLayer.Visible = true;
		_previewFocusAnchor.GrabFocus();
	}

	private void OpenItemPreview(ItemCardDefinition definition, MainMenuItemCodexCard sourceCard)
	{
		if (!_acceptInput || _itemCatalog == null || !_itemCatalog.IsEnabled(definition.Id))
		{
			return;
		}

		_focusBeforePreview = sourceCard;
		_previewUpgradeId = string.Empty;
		_previewItemCardId = definition.Id;
		_itemCatalog.TryGetFirstFrameTexture(definition.Id, out Texture2D texture);
		_previewItemCard.ConfigureCard(
			definition.Id,
			texture,
			LocalizeItemName(definition),
			LocalizeItemDescription(definition),
			interactive: false);
		_previewCard.Visible = false;
		_previewItemCard.Visible = true;
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
		_previewItemCardId = string.Empty;
		_previewCard.Visible = true;
		_previewItemCard.Visible = false;
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
		_upgradesButton.Text = Localize("SKILL_CODEX_CATEGORY_UPGRADES");
		_itemsButton.Text = Localize("SKILL_CODEX_CATEGORY_ITEMS");
		foreach (string suit in SuitOrder)
		{
			_suitButtons[suit].Text = GetSuitSymbol(suit);
		}

		string previewUpgradeId = _previewUpgradeId;
		string previewItemCardId = _previewItemCardId;
		RebuildCards();
		RefreshCategoryTabs();
		RefreshSuitTabs();
		if (!string.IsNullOrEmpty(previewUpgradeId))
		{
			for (int index = 0; index < _visibleDefinitions.Count; index++)
			{
				if (string.Equals(_visibleDefinitions[index].Id, previewUpgradeId, StringComparison.Ordinal))
				{
					OpenPreview(_visibleDefinitions[index], _visibleCards[index]);
					break;
				}
			}
		}
		else if (!string.IsNullOrEmpty(previewItemCardId))
		{
			for (int index = 0; index < _visibleItemDefinitions.Count; index++)
			{
				if (string.Equals(_visibleItemDefinitions[index].Id, previewItemCardId, StringComparison.Ordinal))
				{
					OpenItemPreview(_visibleItemDefinitions[index], _visibleItemCards[index]);
					break;
				}
			}
		}
	}

	private void RefreshCategoryTabs()
	{
		bool upgradesSelected = _currentCategory == UpgradesCategory;
		MainMenuInteractiveStyle.ApplyThemedTab(
			_upgradesButton,
			MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette,
			upgradesSelected,
			19,
			hoverPalette: MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
		MainMenuInteractiveStyle.ApplyThemedTab(
			_itemsButton,
			MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette,
			!upgradesSelected,
			19,
			hoverPalette: MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
		_buttonFocusStyles[_upgradesButton] = _upgradesButton.GetThemeStylebox("focus");
		_buttonFocusStyles[_itemsButton] = _itemsButton.GetThemeStylebox("focus");
		if (!_inputFocusVisible)
		{
			_upgradesButton.AddThemeStyleboxOverride("focus", HiddenFocusStyle);
			_itemsButton.AddThemeStyleboxOverride("focus", HiddenFocusStyle);
		}
	}

	private void RefreshSuitTabs()
	{
		foreach (KeyValuePair<string, Button> pair in _suitButtons)
		{
			bool selected = string.Equals(pair.Key, _currentSuit, StringComparison.Ordinal);
			MainMenuInteractiveStyle.ApplyThemedTab(
				pair.Value,
				MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette,
				selected,
				18,
				hoverPalette: MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette);
			_buttonFocusStyles[pair.Value] = pair.Value.GetThemeStylebox("focus");
			if (!_inputFocusVisible)
			{
				pair.Value.AddThemeStyleboxOverride("focus", HiddenFocusStyle);
			}
		}
	}

	private void ConfigureUnifiedVisualStyle()
	{
		SetMeta(SecondaryMenuGlassStyle.VisualProfileMetadata, SecondaryMenuGlassStyle.UnifiedVisual.Id);
		_root.SetMeta(SecondaryMenuGlassStyle.VisualProfileMetadata, SecondaryMenuGlassStyle.UnifiedVisual.Id);
		SecondaryMenuGlassStyle.ApplyReadabilityVeil(
			GetNode<ColorRect>("Root/Backdrop"),
			SecondaryMenuGlassStyle.UnifiedVisual);

		PanelContainer topBar = GetNode<PanelContainer>("Root/SafeMargin/Layout/TopBar");
		topBar.AddThemeStyleboxOverride(
			"panel",
			SecondaryMenuGlassStyle.CreateFrameStyle(contentPane: true, SecondaryMenuGlassStyle.UnifiedVisual));
		MainMenuInteractiveStyle.ApplyThemedControl(_backButton, MainMenuInteractiveStyle.SecondaryMenuPalette, 20);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.91f, 0.94f, 0.97f, 1.0f));
		_emptyLabel.AddThemeColorOverride("font_color", new Color(0.66f, 0.72f, 0.77f, 0.92f));
		GetNode<ColorRect>("Root/SafeMargin/Layout/CatalogFrame/CatalogGlassSurface").Material =
			SecondaryMenuGlassStyle.CreateGlassMaterial(contentPane: true, SecondaryMenuGlassStyle.UnifiedVisual);
		GetNode<Panel>("Root/SafeMargin/Layout/CatalogFrame/CatalogGlassFrame").AddThemeStyleboxOverride(
			"panel",
			SecondaryMenuGlassStyle.CreateFrameStyle(contentPane: true, SecondaryMenuGlassStyle.UnifiedVisual));
		SecondaryMenuGlassStyle.StyleScrollBar(
			_scrollContainer.GetVScrollBar(),
			SecondaryMenuGlassStyle.UnifiedVisual);

		ColorRect previewScrim = GetNode<ColorRect>("Root/PreviewLayer/Scrim");
		SecondaryMenuGlassStyle.ApplyReadabilityVeil(previewScrim, SecondaryMenuGlassStyle.UnifiedVisual);
		GetNode<ColorRect>("Root/PreviewLayer/PreviewCenter/PreviewGlassSurface").Material =
			SecondaryMenuGlassStyle.CreateGlassMaterial(contentPane: false, SecondaryMenuGlassStyle.UnifiedVisual);
		GetNode<Panel>("Root/PreviewLayer/PreviewCenter/PreviewGlassFrame").AddThemeStyleboxOverride(
			"panel",
			SecondaryMenuGlassStyle.CreateFrameStyle(contentPane: false, SecondaryMenuGlassStyle.UnifiedVisual));

		RefreshCategoryTabs();
		RefreshSuitTabs();
	}

	private void RefreshGridColumns()
	{
		float availableWidth = Mathf.Max(0.0f, _scrollContainer.Size.X - 48.0f);
		float cardWidth = RefreshCatalogCardLayout();
		_grid.Columns = Mathf.Clamp(
			Mathf.FloorToInt((availableWidth + CatalogCardGap) / (cardWidth + CatalogCardGap)),
			2,
			MaximumVisibleCatalogColumns);
	}

	private float RefreshCatalogCardLayout()
	{
		float availableHeight = Mathf.Max(1.0f, _scrollContainer.Size.Y);
		int maximumVisibleRows = _currentCategory == ItemsCategory
			? MaximumVisibleItemRows
			: MaximumVisibleUpgradeRows;
		int visibleRows = Mathf.Clamp(
			Mathf.FloorToInt(
				(availableHeight + CatalogCardGap) / (MinimumCatalogCardHeight + CatalogCardGap)),
			1,
			maximumVisibleRows);
		float cardHeight = Mathf.Min(
			CatalogCardHeight,
			Mathf.Floor(
				(availableHeight - ((visibleRows - 1) * CatalogCardGap)) / visibleRows));
		cardHeight = Mathf.Max(MinimumCatalogCardHeight, cardHeight);
		_grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(CatalogCardGap));

		float cardWidth = Mathf.Round(cardHeight * CatalogCardWidth / CatalogCardHeight);
		foreach (AdventureUpgradeCatalogCard card in _visibleCards)
		{
			card.CustomMinimumSize = new Vector2(cardWidth, cardHeight);
		}
		foreach (MainMenuItemCodexCard card in _visibleItemCards)
		{
			card.CustomMinimumSize = new Vector2(cardWidth, cardHeight);
		}
		return cardWidth;
	}

	private void FocusFirstAvailableControl()
	{
		if (!Visible)
		{
			return;
		}

		if (_currentCategory == ItemsCategory)
		{
			foreach (MainMenuItemCodexCard card in _visibleItemCards)
			{
				card.GrabFocus();
				_scrollContainer.EnsureControlVisible(card);
				return;
			}
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

		foreach (MainMenuItemCodexCard card in _visibleItemCards)
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
