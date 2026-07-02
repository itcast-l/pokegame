using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private enum AdventureUpgradeSelectionOutcome
	{
		Confirmed = 0,
		Canceled = 1,
		DisplayFailed = 2,
	}

	private readonly record struct AdventureUpgradeSelectionResult(
		AdventureUpgradeSelectionOutcome Outcome,
		int SelectedIndex);

	private readonly record struct AdventureUpgradeOverlayOptionModel(
		string Suit,
		string Rarity,
		string Description);

	private static readonly Color DefaultAdventureUpgradeTitleColor = new(0.93f, 0.96f, 1.0f, 0.99f);
	private static readonly Color DefaultAdventureUpgradeGlowColor = new(0.48f, 0.92f, 1.0f, 0.06f);
	private static readonly Color DefaultAdventureUpgradeRuleColor = new(0.98f, 0.9f, 0.7f, 0.08f);
	private const int FirstMainAdventureAvatarStageNumber = 3;

	private AdventureSuitUpgradeCatalog _adventureUpgradeCatalog = default!;
	private readonly Queue<AdventureUpgradeTriggerSource> _pendingAdventureUpgradeTriggers = new();
	private Task _adventureUpgradeResolutionTask = Task.CompletedTask;
	private TaskCompletionSource<AdventureUpgradeSelectionResult>? _adventureUpgradeSelectionCompletionSource;
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
		DisposeActiveSelectionOverlay();
	}

	private void WarnMissingAdventureRunSessionStore(string action)
	{
		GD.PushWarning($"AdventureRunSessionStore autoload is missing; cannot {action}.");
	}

	private AdventureUpgradeRunState? GetAdventureUpgradeRunState()
	{
		return GetAdventureRunSessionStore()?.GetRunState();
	}

	private void BeginAdventureUpgradeRun(int avatarStageNumber)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("begin the adventure upgrade run");
			return;
		}

		store.BeginNewRun(avatarStageNumber);
	}

	private void AdvanceAdventureUpgradeRunToStage(int avatarStageNumber)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("advance the adventure upgrade run");
			return;
		}

		store.AdvanceToStage(avatarStageNumber);
	}

	private void MarkAdventureRunStartUpgradeGranted()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("mark the run-start upgrade as granted");
			return;
		}

		store.MarkRunStartUpgradeGranted();
	}

	private void MarkAdventureVictoryUpgradeGranted()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("mark the victory upgrade as granted");
			return;
		}

		store.MarkVictoryUpgradeGranted();
	}

	private void RecordAdventureAcquiredUpgrade(string upgradeId)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("record an acquired adventure upgrade");
			return;
		}

		store.AcquireUpgrade(upgradeId);
	}

	private void ClearAdventureUpgradeRun()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("clear the adventure upgrade run");
			return;
		}

		store.ClearRun();
	}

	private void InitializeAdventureUpgradeRunForCurrentBoard()
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return;
		}

		int avatarStageNumber = CharacterAvatarCatalog.GetAdventureStageNumber(adventure_enemy_avatar_id);
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			BeginAdventureUpgradeRun(avatarStageNumber);
			return;
		}

		if (runState.CurrentAvatarStageNumber == avatarStageNumber)
		{
			return;
		}

		AdvanceAdventureUpgradeRunToStage(avatarStageNumber);
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
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (!ShouldUseAdventureUpgradeSystem() || runState?.ShouldGrantRunStartUpgrade != true)
		{
			return;
		}

		EnqueueAdventureUpgradeTriggers(
			AdventureUpgradeTriggerRules.BuildTriggerOrder(
				runStartTriggered: true,
				bigCaptureTriggered: false,
				victoryTriggered: false));
	}

	private void EnqueueAdventureUpgradeTriggers(IReadOnlyList<AdventureUpgradeTriggerSource> triggerSources)
	{
		if (!ShouldUseAdventureUpgradeSystem() || triggerSources.Count == 0)
		{
			return;
		}

		foreach (AdventureUpgradeTriggerSource triggerSource in triggerSources)
		{
			if (triggerSource == AdventureUpgradeTriggerSource.None || HasPendingAdventureUpgradeTrigger(triggerSource))
			{
				continue;
			}

			_pendingAdventureUpgradeTriggers.Enqueue(triggerSource);
		}
	}

	private bool HasPendingAdventureUpgradeTrigger(AdventureUpgradeTriggerSource triggerSource)
	{
		foreach (AdventureUpgradeTriggerSource pendingTrigger in _pendingAdventureUpgradeTriggers)
		{
			if (pendingTrigger == triggerSource)
			{
				return true;
			}
		}

		return false;
	}

	private IReadOnlyList<AdventureUpgradeTriggerSource> BuildAdventureUpgradeTriggerOrder(
		bool runStartTriggered,
		bool bigCaptureTriggered,
		bool victoryTriggered)
	{
		return AdventureUpgradeTriggerRules.BuildTriggerOrder(
			runStartTriggered,
			bigCaptureTriggered,
			victoryTriggered);
	}

	private async Task QueueAndResolveAdventureUpgradeTriggersAsync(
		bool runStartTriggered,
		bool bigCaptureTriggered,
		bool victoryTriggered,
		int turnSequenceGeneration)
	{
		IReadOnlyList<AdventureUpgradeTriggerSource> triggerOrder = BuildAdventureUpgradeTriggerOrder(
			runStartTriggered,
			bigCaptureTriggered,
			victoryTriggered);
		EnqueueAdventureUpgradeTriggers(triggerOrder);
		if (_pendingAdventureUpgradeTriggers.Count == 0)
		{
			return;
		}

		await ResolvePendingAdventureUpgradeTriggersAsync(turnSequenceGeneration);
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
				AdventureUpgradeTriggerSource triggerSource = _pendingAdventureUpgradeTriggers.Peek();
				IReadOnlyList<AdventureSuitUpgradeDefinition> offer = BuildAdventureUpgradeOffer();
				if (offer.Count == 0)
				{
					_pendingAdventureUpgradeTriggers.Dequeue();
					continue;
				}

				AdventureUpgradeSelectionResult selectionResult = await ShowAdventureUpgradeSelectionAsync(triggerSource, offer);
				if (turnSequenceGeneration != _turnSequenceGeneration || _isLeavingGame)
				{
					return;
				}

					if (selectionResult.Outcome is AdventureUpgradeSelectionOutcome.Canceled
						&& triggerSource != AdventureUpgradeTriggerSource.None)
					{
						return;
					}

					int selectedIndex = selectionResult.SelectedIndex;
					if (selectionResult.Outcome == AdventureUpgradeSelectionOutcome.DisplayFailed)
					{
						selectedIndex = 0;
					}

					if (selectedIndex < 0 || selectedIndex >= offer.Count)
					{
						if (selectionResult.Outcome == AdventureUpgradeSelectionOutcome.DisplayFailed)
						{
							return;
						}

						continue;
					}

					AdventureSuitUpgradeDefinition selectedDefinition = offer[selectedIndex];
					MarkAdventureUpgradeTriggerAsGranted(triggerSource);
				RecordAdventureAcquiredUpgrade(selectedDefinition.Id);
				GD.Print($"Adventure upgrade acquired: {selectedDefinition.Id}");
				_pendingAdventureUpgradeTriggers.Dequeue();
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
				MarkAdventureRunStartUpgradeGranted();
				break;
			case AdventureUpgradeTriggerSource.Victory:
				MarkAdventureVictoryUpgradeGranted();
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

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return Array.Empty<AdventureSuitUpgradeDefinition>();
		}

		IReadOnlyList<AdventureUpgradeDefinitionModel> offerModels = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			runState.GetAcquiredUpgradeIds(),
			Math.Max(1, runState.CurrentRunStageIndex),
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

		private async Task<AdventureUpgradeSelectionResult> ShowAdventureUpgradeSelectionAsync(
			AdventureUpgradeTriggerSource triggerSource,
			IReadOnlyList<AdventureSuitUpgradeDefinition> offer)
		{
			DisposeActiveSelectionOverlay();
		var overlayOptions = new List<AdventureUpgradeOverlayOptionModel>(offer.Count);
			for (int index = 0; index < offer.Count; index++)
			{
				AdventureSuitUpgradeDefinition definition = offer[index];
				overlayOptions.Add(new AdventureUpgradeOverlayOptionModel(
					definition.Suit,
					definition.Rarity,
					FormatAdventureUpgradeDescription(definition)));
			}
			var completionSource = new TaskCompletionSource<AdventureUpgradeSelectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
			_adventureUpgradeSelectionCompletionSource = completionSource;
			if (!ShowAdventureUpgradeSelectionOverlay(overlayOptions))
			{
				_adventureUpgradeSelectionCompletionSource = null;
				return new AdventureUpgradeSelectionResult(
					AdventureUpgradeSelectionOutcome.DisplayFailed,
					-1);
			}

			AdventureUpgradeSelectionResult selectionResult = await completionSource.Task;
		if (ReferenceEquals(_adventureUpgradeSelectionCompletionSource, completionSource))
		{
			_adventureUpgradeSelectionCompletionSource = null;
		}

		return selectionResult;
	}

	private void CancelAdventureUpgradeSelection()
	{
		ResolveAdventureUpgradeSelection(
			new AdventureUpgradeSelectionResult(
				AdventureUpgradeSelectionOutcome.Canceled,
				-1),
			clearOverlay: false);
	}

	private void ResolveAdventureUpgradeSelection(
		AdventureUpgradeSelectionResult result,
		bool clearOverlay)
	{
		TaskCompletionSource<AdventureUpgradeSelectionResult>? completionSource = _adventureUpgradeSelectionCompletionSource;
		_adventureUpgradeSelectionCompletionSource = null;
		completionSource?.TrySetResult(result);
		if (clearOverlay)
		{
			DisposeActiveSelectionOverlay();
		}
	}

	private bool ShowAdventureUpgradeSelectionOverlay(IReadOnlyList<AdventureUpgradeOverlayOptionModel> options)
	{
		if (ConsumeDebugForceNextAdventureUpgradeOverlayDisplayFailure())
		{
			return false;
		}

		PackedScene? overlayScene = GD.Load<PackedScene>(AdventureUpgradeSelectionOverlayScenePath);
		if (overlayScene?.Instantiate() is not Control selectionOverlay)
		{
			GD.PushError($"Missing adventure upgrade selection overlay scene: {AdventureUpgradeSelectionOverlayScenePath}");
			return false;
		}

		AddChild(selectionOverlay);
		PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
		ConfigureAdventureUpgradeSelectionOverlay(selectionOverlay, options);
		RegisterSelectionOverlaySession(new SelectionOverlaySession(
			SelectionOverlayKind.AdventureUpgrade,
			selectionOverlay,
			options.Count,
			optionIndex =>
			{
				if (optionIndex < 0 || optionIndex >= options.Count)
				{
					return false;
				}

				ResolveAdventureUpgradeSelection(
					new AdventureUpgradeSelectionResult(
						AdventureUpgradeSelectionOutcome.Confirmed,
						optionIndex),
					clearOverlay: true);
				return true;
			},
			cancel: CancelAdventureUpgradeSelection,
			dispose: () =>
			{
				if (IsInstanceValid(selectionOverlay))
				{
					RemoveChild(selectionOverlay);
					selectionOverlay.Free();
				}
			}));
		return true;
	}

	private string FormatAdventureUpgradeDescription(AdventureSuitUpgradeDefinition definition)
	{
		return GetAdventureUpgradeDescription(definition.DescriptionKey, definition.EffectText);
	}

	private void ConfigureAdventureUpgradeSelectionOverlay(
		Control selectionOverlay,
		IReadOnlyList<AdventureUpgradeOverlayOptionModel> options)
	{
		Control? header = selectionOverlay.GetNodeOrNull<Control>("Margin/Layout/Header");
		if (header != null)
		{
			header.Visible = false;
		}

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

			button.Pressed += () => TrySelectActiveSelectionOverlayOption(SelectionOverlayKind.AdventureUpgrade, capturedIndex);
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
				AdventureUpgradeCardVisualEntryResource? visualEntry = AdventureUpgradeCardVisualResolver.ResolveEntry(option.Suit, option.Rarity);
				Texture2D? cardTexture = AdventureUpgradeCardVisualResolver.LoadAtlasTextureForRarity(option.Suit, option.Rarity);
				choiceArt.ConfigureChoice(cardTexture, string.Empty, option.Description, false);
				choiceArt.SetPalette(
					visualEntry?.TitleColor ?? DefaultAdventureUpgradeTitleColor,
					visualEntry?.GlowColor ?? DefaultAdventureUpgradeGlowColor,
					visualEntry?.RuleColor ?? DefaultAdventureUpgradeRuleColor);
				RefreshAdventureUpgradeOverlayHighlight(button);
			}

		CallDeferred(nameof(FocusAdventureUpgradeSelectionOverlayDefaultButton));
	}

	private void FocusAdventureUpgradeSelectionOverlayDefaultButton()
	{
		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade);
		if (session == null
			|| !IsSelectionOverlaySessionVisible(session)
			|| !session.Root.IsInsideTree())
		{
			return;
		}

		int[] preferredFocusOrder = { 1, 0, 2 };
		foreach (int index in preferredFocusOrder)
		{
			Button button = session.Root.GetNode<Button>($"Margin/Layout/CardsRow/Option{index + 1}Button");
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

	private bool ShouldDelayWinnerForAdventureVictoryUpgrade(int winnerPlayerIndex)
	{
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (!ShouldUseAdventureUpgradeSystem()
			|| winnerPlayerIndex != MainPlayerIndex
			|| runState?.ShouldGrantVictoryUpgrade != true)
		{
			return false;
		}

		if (_pendingAdventureWinnerPlayerIndex == winnerPlayerIndex)
		{
			return true;
		}

		_pendingAdventureWinnerPlayerIndex = winnerPlayerIndex;
		return true;
	}

	private bool ShouldQueueAdventureBigCaptureUpgrade(int playerIndex, int totalCapturedCardCount)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| playerIndex != MainPlayerIndex
			|| !AdventureUpgradeChoiceRules.ShouldGrantBigCapture(totalCapturedCardCount))
		{
			return false;
		}

		return true;
	}

	private static string GetFirstMainAdventureAvatarId()
	{
		return CharacterAvatarCatalog.GetAdventureAvatarIdForStage(FirstMainAdventureAvatarStageNumber);
	}
}
