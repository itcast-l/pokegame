using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private readonly record struct AdventureUpgradeOverlayOptionModel(
		string DisplayName,
		string Suit,
		string Rarity,
		string Description);

	private const string AdventureUpgradeSelectionTitle = "选择一项强化";
	private const string AdventureUpgradeRunStartSubtitle = "开局强化";
	private const string AdventureUpgradeBigCaptureSubtitle = "大收牌强化";
	private const string AdventureUpgradeVictorySubtitle = "胜利强化";
	private const int FirstMainAdventureAvatarStageNumber = 3;

	private AdventureSuitUpgradeCatalog _adventureUpgradeCatalog = default!;
	private readonly Queue<AdventureUpgradeTriggerSource> _pendingAdventureUpgradeTriggers = new();
	private Task _adventureUpgradeResolutionTask = Task.CompletedTask;
	private TaskCompletionSource<int>? _adventureUpgradeSelectionCompletionSource;
	private int _pendingAdventureWinnerPlayerIndex = -1;
	private bool _isResolvingAdventureUpgradeSelection;

	private void LoadAdventureUpgradeCatalog()
	{
		_adventureUpgradeCatalog = AdventureSuitUpgradeCatalog.Load(this);
	}

	private void EnsureAdventureUpgradeCatalogLoaded()
	{
		if (_adventureUpgradeCatalog != null && _adventureUpgradeCatalog.All.Count > 0)
		{
			return;
		}

		LoadAdventureUpgradeCatalog();
	}

	private void ResetAdventureUpgradeBoardState()
	{
		_pendingAdventureUpgradeTriggers.Clear();
		_pendingAdventureWinnerPlayerIndex = -1;
		_isResolvingAdventureUpgradeSelection = false;
		_adventureUpgradeResolutionTask = Task.CompletedTask;
		CancelAdventureUpgradeSelection();
		ClearSelectionOverlay();
	}

	private void InitializeAdventureUpgradeRunForCurrentBoard()
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return;
		}

		int avatarStageNumber = CharacterAvatarCatalog.GetAdventureStageNumber(adventure_enemy_avatar_id);
		if (!AdventureUpgradeRunState.IsActive)
		{
			AdventureUpgradeRunState.BeginNewRun(avatarStageNumber);
			return;
		}

		if (AdventureUpgradeRunState.CurrentAvatarStageNumber == avatarStageNumber)
		{
			return;
		}

		int runStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndex(avatarStageNumber);
		if (runStageIndex <= 0)
		{
			return;
		}

		if (runStageIndex == 1)
		{
			AdventureUpgradeRunState.BeginNewRun(avatarStageNumber);
			return;
		}

		AdventureUpgradeRunState.AdvanceToStage(avatarStageNumber);
	}

	private bool ShouldUseAdventureUpgradeSystem()
	{
		if (!adventure_mode || adventure_tutorial_mode)
		{
			return false;
		}

		int avatarStageNumber = CharacterAvatarCatalog.GetAdventureStageNumber(adventure_enemy_avatar_id);
		return AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(avatarStageNumber);
	}

	private void QueueAdventureRunStartUpgradeIfNeeded()
	{
		if (!ShouldUseAdventureUpgradeSystem() || !AdventureUpgradeRunState.ShouldGrantRunStartUpgrade)
		{
			return;
		}

		EnqueueAdventureUpgradeTrigger(AdventureUpgradeTriggerSource.RunStart);
	}

	private void EnqueueAdventureUpgradeTrigger(AdventureUpgradeTriggerSource triggerSource)
	{
		if (!ShouldUseAdventureUpgradeSystem() || triggerSource == AdventureUpgradeTriggerSource.None)
		{
			return;
		}

		_pendingAdventureUpgradeTriggers.Enqueue(triggerSource);
	}

	private Task ResolvePendingAdventureUpgradeTriggersAsync(int turnSequenceGeneration)
	{
		if (_isResolvingAdventureUpgradeSelection)
		{
			return _adventureUpgradeResolutionTask;
		}

		_adventureUpgradeResolutionTask = ResolvePendingAdventureUpgradeTriggersInternalAsync(turnSequenceGeneration);
		return _adventureUpgradeResolutionTask;
	}

	private async Task ResolvePendingAdventureUpgradeTriggersInternalAsync(int turnSequenceGeneration)
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return;
		}

		_isResolvingAdventureUpgradeSelection = true;
		try
		{
			while (_pendingAdventureUpgradeTriggers.Count > 0
				&& turnSequenceGeneration == _turnSequenceGeneration
				&& !_isLeavingGame
				&& !IsGameOver())
			{
				AdventureUpgradeTriggerSource triggerSource = _pendingAdventureUpgradeTriggers.Dequeue();
				MarkAdventureUpgradeTriggerAsGranted(triggerSource);
				IReadOnlyList<AdventureSuitUpgradeDefinition> offer = BuildAdventureUpgradeOffer();
				if (offer.Count == 0)
				{
					continue;
				}

				int selectedIndex = await ShowAdventureUpgradeSelectionAsync(triggerSource, offer);
				if (turnSequenceGeneration != _turnSequenceGeneration || _isLeavingGame)
				{
					return;
				}

				if (selectedIndex < 0 || selectedIndex >= offer.Count)
				{
					continue;
				}

				AdventureSuitUpgradeDefinition selectedDefinition = offer[selectedIndex];
				AdventureUpgradeRunState.AcquireUpgrade(selectedDefinition.Id);
				GD.Print($"Adventure upgrade acquired: {selectedDefinition.Id}");
			}
		}
		finally
		{
			_isResolvingAdventureUpgradeSelection = false;
		}

		if (_pendingAdventureWinnerPlayerIndex >= 0
			&& _pendingAdventureUpgradeTriggers.Count == 0
			&& turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver())
		{
			int winnerPlayerIndex = _pendingAdventureWinnerPlayerIndex;
			_pendingAdventureWinnerPlayerIndex = -1;
			DeclareWinner(winnerPlayerIndex);
		}
	}

	private void MarkAdventureUpgradeTriggerAsGranted(AdventureUpgradeTriggerSource triggerSource)
	{
		switch (triggerSource)
		{
			case AdventureUpgradeTriggerSource.RunStart:
				AdventureUpgradeRunState.MarkRunStartUpgradeGranted();
				break;
			case AdventureUpgradeTriggerSource.Victory:
				AdventureUpgradeRunState.MarkVictoryUpgradeGranted();
				break;
		}
	}

	private IReadOnlyList<AdventureSuitUpgradeDefinition> BuildAdventureUpgradeOffer()
	{
		EnsureAdventureUpgradeCatalogLoaded();
		if (_adventureUpgradeCatalog == null || _adventureUpgradeCatalog.All.Count == 0)
		{
			return Array.Empty<AdventureSuitUpgradeDefinition>();
		}

		var definitions = new List<AdventureUpgradeDefinitionModel>(_adventureUpgradeCatalog.All.Count);
		foreach (AdventureSuitUpgradeDefinition definition in _adventureUpgradeCatalog.All)
		{
			definitions.Add(new AdventureUpgradeDefinitionModel(
				definition.Id,
				definition.DisplayName,
				definition.Suit,
				definition.Rarity,
				definition.TriggerText,
				definition.EffectText,
				definition.Enabled));
		}

		IReadOnlyList<AdventureUpgradeDefinitionModel> offerModels = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			AdventureUpgradeRunState.GetAcquiredUpgradeIds(),
			Math.Max(1, AdventureUpgradeRunState.CurrentRunStageIndex),
			maxExclusive => _rng.RandiRange(0, maxExclusive - 1));

		if (offerModels.Count == 0)
		{
			return Array.Empty<AdventureSuitUpgradeDefinition>();
		}

		var offer = new List<AdventureSuitUpgradeDefinition>(offerModels.Count);
		foreach (AdventureUpgradeDefinitionModel model in offerModels)
		{
			if (_adventureUpgradeCatalog.TryGetDefinition(model.Id, out AdventureSuitUpgradeDefinition definition))
			{
				offer.Add(definition);
			}
		}

		return offer;
	}

	private async Task<int> ShowAdventureUpgradeSelectionAsync(
		AdventureUpgradeTriggerSource triggerSource,
		IReadOnlyList<AdventureSuitUpgradeDefinition> offer)
	{
		ClearSelectionOverlay();
		var completionSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
		_adventureUpgradeSelectionCompletionSource = completionSource;
		var overlayOptions = new List<AdventureUpgradeOverlayOptionModel>(offer.Count);
		_selectionOverlayActions.Clear();
		for (int index = 0; index < offer.Count; index++)
		{
			int capturedIndex = index;
			AdventureSuitUpgradeDefinition definition = offer[index];
			overlayOptions.Add(new AdventureUpgradeOverlayOptionModel(
				definition.DisplayName,
				definition.Suit,
				definition.Rarity,
				FormatAdventureUpgradeDescription(definition)));
			_selectionOverlayActions.Add(() =>
			{
				ClearSelectionOverlay();
				completionSource.TrySetResult(capturedIndex);
			});
		}
		ShowAdventureUpgradeSelectionOverlay(
			AdventureUpgradeSelectionTitle,
			GetAdventureUpgradeTriggerSubtitle(triggerSource),
			overlayOptions);
		int selectedIndex = await completionSource.Task;
		if (ReferenceEquals(_adventureUpgradeSelectionCompletionSource, completionSource))
		{
			_adventureUpgradeSelectionCompletionSource = null;
		}

		return selectedIndex;
	}

	private void CancelAdventureUpgradeSelection()
	{
		TaskCompletionSource<int>? completionSource = _adventureUpgradeSelectionCompletionSource;
		_adventureUpgradeSelectionCompletionSource = null;
		completionSource?.TrySetResult(-1);
	}

	private void ShowAdventureUpgradeSelectionOverlay(
		string title,
		string subtitle,
		IReadOnlyList<AdventureUpgradeOverlayOptionModel> options)
	{
		PackedScene? overlayScene = GD.Load<PackedScene>(AdventureUpgradeSelectionOverlayScenePath);
		if (overlayScene?.Instantiate() is not Control selectionOverlay)
		{
			GD.PushError($"Missing adventure upgrade selection overlay scene: {AdventureUpgradeSelectionOverlayScenePath}");
			return;
		}

		AddChild(selectionOverlay);
		_adventureUpgradeSelectionOverlay = selectionOverlay;
		_selectionOverlay = null;
		PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
		ConfigureAdventureUpgradeSelectionOverlay(selectionOverlay, title, subtitle, options);
	}

	private static string FormatAdventureUpgradeDescription(AdventureSuitUpgradeDefinition definition)
	{
		return definition.EffectText;
	}

	private void ConfigureAdventureUpgradeSelectionOverlay(
		Control selectionOverlay,
		string title,
		string subtitle,
		IReadOnlyList<AdventureUpgradeOverlayOptionModel> options)
	{
		selectionOverlay.GetNode<Label>("Margin/Layout/Header/SelectionTitle").Text = title;
		selectionOverlay.GetNode<Label>("Margin/Layout/Header/SelectionSubtitle").Text = subtitle;

		for (int index = 0; index < 3; index++)
		{
			int capturedIndex = index;
			Button button = selectionOverlay.GetNode<Button>($"Margin/Layout/CardsRow/Option{index + 1}Button");
			AdventureUpgradeChoiceArt choiceArt = button.GetNode<AdventureUpgradeChoiceArt>("CardFrame/ChoiceArt");
			Control cardFrame = button.GetNode<Control>("CardFrame");

			bool visible = index < options.Count;
			button.Visible = visible;
			button.Disabled = !visible;
			button.Text = string.Empty;
			button.FocusMode = visible ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
			cardFrame.PivotOffset = button.Size * 0.5f;
			cardFrame.Position = GetAdventureUpgradeCardFrameOffset(index);
			cardFrame.RotationDegrees = GetAdventureUpgradeCardFrameRotation(index);

			button.Pressed += () => SelectSelectionOverlayOption(capturedIndex);
			button.FocusEntered += () => RefreshAdventureUpgradeOverlayHighlight(button);
			button.FocusExited += () => RefreshAdventureUpgradeOverlayHighlight(button);
			button.MouseEntered += () => RefreshAdventureUpgradeOverlayHighlight(button);
			button.MouseExited += () => RefreshAdventureUpgradeOverlayHighlight(button);

			if (!visible)
			{
				choiceArt.ConfigureChoice(null, string.Empty, string.Empty, false);
				continue;
			}

			AdventureUpgradeOverlayOptionModel option = options[index];
			Texture2D? cardTexture = LoadAdventureUpgradeCardTexture(option.Suit, option.Rarity);
			choiceArt.ConfigureChoice(cardTexture, option.DisplayName, option.Description, false);
			ApplyAdventureUpgradeChoicePalette(choiceArt, option.Suit, option.Rarity);
			RefreshAdventureUpgradeOverlayHighlight(button);
		}

		CallDeferred(nameof(FocusAdventureUpgradeSelectionOverlayDefaultButton));
	}

	private void FocusAdventureUpgradeSelectionOverlayDefaultButton()
	{
		if (_adventureUpgradeSelectionOverlay == null
			|| !IsInstanceValid(_adventureUpgradeSelectionOverlay)
			|| !_adventureUpgradeSelectionOverlay.IsInsideTree())
		{
			return;
		}

		int[] preferredFocusOrder = { 1, 0, 2 };
		foreach (int index in preferredFocusOrder)
		{
			Button button = _adventureUpgradeSelectionOverlay.GetNode<Button>($"Margin/Layout/CardsRow/Option{index + 1}Button");
			if (!button.Visible || button.Disabled || !IsInstanceValid(button))
			{
				continue;
			}

			button.GrabFocus();
			RefreshAdventureUpgradeOverlayHighlight(button);
			return;
		}
	}

	private static void RefreshAdventureUpgradeOverlayHighlight(Button button)
	{
		int optionIndex = ExtractAdventureUpgradeOptionIndex(button);
		bool highlighted = button.Visible
			&& !button.Disabled
			&& (button.HasFocus() || button.IsHovered());
		AdventureUpgradeChoiceArt? choiceArt = button.GetNodeOrNull<AdventureUpgradeChoiceArt>("CardFrame/ChoiceArt");
		choiceArt?.SetHighlighted(highlighted);
		Control? cardFrame = button.GetNodeOrNull<Control>("CardFrame");
		if (cardFrame != null)
		{
			cardFrame.PivotOffset = button.Size * 0.5f;
			cardFrame.Position = GetAdventureUpgradeCardFrameOffset(optionIndex) + (highlighted ? new Vector2(0.0f, -2.0f) : Vector2.Zero);
			cardFrame.RotationDegrees = GetAdventureUpgradeCardFrameRotation(optionIndex);
			cardFrame.Scale = highlighted ? new Vector2(1.03f, 1.03f) : new Vector2(0.965f, 0.965f);
			cardFrame.ZIndex = highlighted ? 3 : 0;
		}
		button.ZIndex = highlighted ? 2 : 0;
	}

	private static Vector2 GetAdventureUpgradeCardFrameOffset(int optionIndex)
	{
		return optionIndex switch
		{
			0 => new Vector2(0.0f, 10.0f),
			1 => new Vector2(0.0f, 0.0f),
			2 => new Vector2(0.0f, 10.0f),
			_ => Vector2.Zero,
		};
	}

	private static float GetAdventureUpgradeCardFrameRotation(int optionIndex)
	{
		return optionIndex switch
		{
			0 => -0.75f,
			1 => 0.0f,
			2 => 0.75f,
			_ => 0.0f,
		};
	}

	private static int ExtractAdventureUpgradeOptionIndex(Button button)
	{
		string name = button.Name.ToString();
		if (!name.StartsWith("Option") || !name.EndsWith("Button"))
		{
			return 0;
		}

		string digits = name["Option".Length..^"Button".Length];
		return int.TryParse(digits, out int optionNumber)
			? Mathf.Max(0, optionNumber - 1)
			: 0;
	}

	private static Texture2D? LoadAdventureUpgradeCardTexture(string suit, string rarity)
	{
		string tier = MapAdventureUpgradeRarityToTextureTier(rarity);
		return AdventureUpgradeCardVisualResolver.LoadAtlasTexture(suit, tier);
	}

	private static void ApplyAdventureUpgradeChoicePalette(AdventureUpgradeChoiceArt choiceArt, string suit, string rarity)
	{
		string normalizedSuit = NormalizeAdventureUpgradeTextureKey(suit);
		string normalizedRarity = NormalizeAdventureUpgradeTextureKey(MapAdventureUpgradeRarityToTextureTier(rarity));

		Color titleColor = new(0.93f, 0.96f, 1.0f, 0.99f);
		Color glowColor = new(0.48f, 0.92f, 1.0f, 0.06f);
		Color ruleColor = new(0.98f, 0.9f, 0.7f, 0.08f);

		if (normalizedRarity == "rare")
		{
			titleColor = new Color(0.96f, 0.88f, 1.0f, 0.99f);
			glowColor = new Color(0.72f, 0.42f, 1.0f, 0.08f);
			ruleColor = new Color(0.94f, 0.8f, 1.0f, 0.1f);
		}
		else if (normalizedRarity == "uncommon")
		{
			titleColor = new Color(0.88f, 0.98f, 0.95f, 0.99f);
			glowColor = new Color(0.32f, 0.98f, 0.82f, 0.07f);
			ruleColor = new Color(0.95f, 0.92f, 0.7f, 0.09f);
		}

		if (normalizedSuit == "spades")
		{
			titleColor = titleColor.Lerp(new Color(0.9f, 0.94f, 1.0f, 1.0f), 0.25f);
		}
		else if (normalizedSuit == "hearts")
		{
			titleColor = titleColor.Lerp(new Color(0.96f, 0.9f, 0.86f, 1.0f), 0.18f);
		}
		else if (normalizedSuit == "diamonds")
		{
			titleColor = titleColor.Lerp(new Color(0.95f, 0.92f, 0.86f, 1.0f), 0.22f);
		}
		else if (normalizedSuit == "clubs")
		{
			titleColor = titleColor.Lerp(new Color(0.86f, 0.98f, 0.92f, 1.0f), 0.2f);
		}

		choiceArt.SetPalette(titleColor, glowColor, ruleColor);
	}

	private static string MapAdventureUpgradeRarityToTextureTier(string rarity)
	{
		return NormalizeAdventureUpgradeTextureKey(rarity) switch
		{
			"common" => "common",
			"rare" => "uncommon",
			"legendary" => "rare",
			_ => rarity,
		};
	}

	private static string NormalizeAdventureUpgradeTextureKey(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? string.Empty
			: value.Trim().ToLowerInvariant();
	}

	private static string GetAdventureUpgradeTriggerSubtitle(AdventureUpgradeTriggerSource triggerSource)
	{
		return triggerSource switch
		{
			AdventureUpgradeTriggerSource.RunStart => AdventureUpgradeRunStartSubtitle,
			AdventureUpgradeTriggerSource.BigCapture => AdventureUpgradeBigCaptureSubtitle,
			AdventureUpgradeTriggerSource.Victory => AdventureUpgradeVictorySubtitle,
			_ => AdventureUpgradeSelectionTitle,
		};
	}

	private bool TryQueueAdventureVictoryUpgradeBeforeWinner(int winnerPlayerIndex)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| winnerPlayerIndex != MainPlayerIndex
			|| !AdventureUpgradeRunState.ShouldGrantVictoryUpgrade)
		{
			return false;
		}

		if (_pendingAdventureWinnerPlayerIndex == winnerPlayerIndex)
		{
			return true;
		}

		_pendingAdventureWinnerPlayerIndex = winnerPlayerIndex;
		EnqueueAdventureUpgradeTrigger(AdventureUpgradeTriggerSource.Victory);
		_ = ResolvePendingAdventureUpgradeTriggersAsync(_turnSequenceGeneration);
		return true;
	}

	private async Task ResolveAdventureBigCaptureUpgradeAsync(int playerIndex, int totalCapturedCardCount, int turnSequenceGeneration)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| playerIndex != MainPlayerIndex
			|| !AdventureUpgradeChoiceRules.ShouldGrantBigCapture(totalCapturedCardCount))
		{
			return;
		}

		EnqueueAdventureUpgradeTrigger(AdventureUpgradeTriggerSource.BigCapture);
		await ResolvePendingAdventureUpgradeTriggersAsync(turnSequenceGeneration);
	}

	private static string GetFirstMainAdventureAvatarId()
	{
		return CharacterAvatarCatalog.GetAdventureAvatarIdForStage(FirstMainAdventureAvatarStageNumber);
	}
}
