using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const int AdventureUpgradeReplacementPageSize = 3;

	private enum AdventureUpgradeReplacementOutcome
	{
		Confirmed = 0,
		Returned = 1,
		DisplayFailed = 2,
	}

	private readonly record struct AdventureUpgradeReplacementResult(
		AdventureUpgradeReplacementOutcome Outcome,
		int SelectedIndex);

	private TaskCompletionSource<AdventureUpgradeReplacementResult>? _adventureUpgradeReplacementCompletionSource;
	private IReadOnlyList<AdventureSuitUpgradeDefinition> _adventureUpgradeReplacementOptions =
		Array.Empty<AdventureSuitUpgradeDefinition>();
	private int _adventureUpgradeReplacementPage;
	private readonly bool[] _adventureUpgradeReplacementPointerHovered = new bool[AdventureUpgradeReplacementPageSize];

	private async Task<AdventureUpgradeReplacementResult> ShowAdventureUpgradeReplacementAsync(
		AdventureSuitUpgradeDefinition selectedUpgrade,
		IReadOnlyList<AdventureSuitUpgradeDefinition> currentUpgrades)
	{
		DisposeActiveSelectionOverlay();
		_adventureUpgradeReplacementOptions = currentUpgrades;
		_adventureUpgradeReplacementPage = 0;
		Array.Fill(_adventureUpgradeReplacementPointerHovered, false);

		var completionSource = new TaskCompletionSource<AdventureUpgradeReplacementResult>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		_adventureUpgradeReplacementCompletionSource = completionSource;
		if (!ShowAdventureUpgradeReplacementOverlay(selectedUpgrade, currentUpgrades))
		{
			_adventureUpgradeReplacementCompletionSource = null;
			_adventureUpgradeReplacementOptions = Array.Empty<AdventureSuitUpgradeDefinition>();
			return new AdventureUpgradeReplacementResult(
				AdventureUpgradeReplacementOutcome.DisplayFailed,
				-1);
		}

		AdventureUpgradeReplacementResult result = await completionSource.Task;
		if (ReferenceEquals(_adventureUpgradeReplacementCompletionSource, completionSource))
		{
			_adventureUpgradeReplacementCompletionSource = null;
		}
		_adventureUpgradeReplacementOptions = Array.Empty<AdventureSuitUpgradeDefinition>();
		return result;
	}

	private bool ShowAdventureUpgradeReplacementOverlay(
		AdventureSuitUpgradeDefinition selectedUpgrade,
		IReadOnlyList<AdventureSuitUpgradeDefinition> currentUpgrades)
	{
#if DECKLAND_TEST_HOOKS
		if (ConsumeDebugForceNextAdventureUpgradeReplacementOverlayDisplayFailure())
		{
			return false;
		}
#endif

		PackedScene? overlayScene = GD.Load<PackedScene>(AdventureUpgradeReplacementOverlayScenePath);
		if (overlayScene?.Instantiate() is not Control replacementOverlay)
		{
			GD.PushError($"Missing adventure upgrade replacement overlay scene: {AdventureUpgradeReplacementOverlayScenePath}");
			return false;
		}

		var overlayLayer = new CanvasLayer
		{
			Name = "AdventureUpgradeReplacementLayer",
			Layer = 40,
		};
		AddChild(overlayLayer);
		overlayLayer.AddChild(replacementOverlay);

		var session = new SelectionOverlaySession(
			SelectionOverlayKind.AdventureUpgradeReplacement,
			replacementOverlay,
			currentUpgrades.Count,
			optionIndex =>
			{
				if (optionIndex < 0 || optionIndex >= currentUpgrades.Count)
				{
					return false;
				}

				ResolveAdventureUpgradeReplacement(
					new AdventureUpgradeReplacementResult(
						AdventureUpgradeReplacementOutcome.Confirmed,
						optionIndex),
					clearOverlay: true);
				return true;
			},
			cancel: ReturnFromAdventureUpgradeReplacement,
			dispose: () =>
			{
				ReturnFromAdventureUpgradeReplacement();
				if (IsInstanceValid(overlayLayer))
				{
					overlayLayer.QueueFree();
				}
			},
			abnormalRelease: () =>
			{
				ResolveAdventureUpgradeReplacement(
					new AdventureUpgradeReplacementResult(
						AdventureUpgradeReplacementOutcome.Confirmed,
						0),
					clearOverlay: false);
			});
		RegisterSelectionOverlaySession(session);
		try
		{
			ConfigureAdventureUpgradeReplacementOverlay(
				session,
				replacementOverlay,
				selectedUpgrade);
		}
		catch (Exception error)
		{
			GD.PushError($"Could not configure adventure upgrade replacement overlay: {error}");
			DisposeActiveSelectionOverlay();
			return false;
		}

		PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
		return true;
	}

	private void ConfigureAdventureUpgradeReplacementOverlay(
		SelectionOverlaySession session,
		Control overlay,
		AdventureSuitUpgradeDefinition selectedUpgrade)
	{
		overlay.GetNode<Label>("Margin/Layout/Header/Title").Text =
			Localize("GAME_UPGRADE_REPLACEMENT_TITLE");
		overlay.GetNode<Label>("Margin/Layout/Content/NewColumn/Label").Text =
			Localize("GAME_UPGRADE_REPLACEMENT_NEW");
		overlay.GetNode<Label>("Margin/Layout/Content/CurrentColumn/Label").Text =
			Localize("GAME_UPGRADE_REPLACEMENT_CURRENT");

		AdventureUpgradeChoiceArt selectedChoiceArt = overlay.GetNode<AdventureUpgradeChoiceArt>(
			"Margin/Layout/Content/NewColumn/Card/ChoiceArt");
		ConfigureAdventureUpgradeReplacementCard(selectedChoiceArt, selectedUpgrade, 0.36f);

		for (int slotIndex = 0; slotIndex < AdventureUpgradeReplacementPageSize; slotIndex++)
		{
			int capturedSlotIndex = slotIndex;
			Button button = GetAdventureUpgradeReplacementOptionButton(overlay, slotIndex);
			Action pressed = () => TrySelectAdventureUpgradeReplacementSlot(capturedSlotIndex);
			Action focusEntered = () => RefreshAdventureUpgradeReplacementHighlight(capturedSlotIndex);
			Action focusExited = () => RefreshAdventureUpgradeReplacementHighlight(capturedSlotIndex);
			Action mouseEntered = () => SetAdventureUpgradeReplacementPointerHover(capturedSlotIndex, true);
			Action mouseExited = () => SetAdventureUpgradeReplacementPointerHover(capturedSlotIndex, false);
			button.Pressed += pressed;
			button.FocusEntered += focusEntered;
			button.FocusExited += focusExited;
			button.MouseEntered += mouseEntered;
			button.MouseExited += mouseExited;
			session.RegisterUnsubscribe(() =>
			{
				if (!IsInstanceValid(button))
				{
					return;
				}
				button.Pressed -= pressed;
				button.FocusEntered -= focusEntered;
				button.FocusExited -= focusExited;
				button.MouseEntered -= mouseEntered;
				button.MouseExited -= mouseExited;
			});
		}

		Button previousButton = overlay.GetNode<Button>(
			"Margin/Layout/Content/CurrentColumn/PageRow/PreviousButton");
		Button nextButton = overlay.GetNode<Button>(
			"Margin/Layout/Content/CurrentColumn/PageRow/NextButton");
		Button backButton = overlay.GetNode<Button>("Margin/Layout/BackButton");
		backButton.Text = Localize("GAME_UPGRADE_REPLACEMENT_BACK");
		Action previousPressed = () => ChangeAdventureUpgradeReplacementPage(-1);
		Action nextPressed = () => ChangeAdventureUpgradeReplacementPage(1);
		Action backPressed = ReturnFromAdventureUpgradeReplacement;
		previousButton.Pressed += previousPressed;
		nextButton.Pressed += nextPressed;
		backButton.Pressed += backPressed;
		session.RegisterUnsubscribe(() =>
		{
			if (IsInstanceValid(previousButton))
			{
				previousButton.Pressed -= previousPressed;
			}
			if (IsInstanceValid(nextButton))
			{
				nextButton.Pressed -= nextPressed;
			}
			if (IsInstanceValid(backButton))
			{
				backButton.Pressed -= backPressed;
			}
		});

		RefreshAdventureUpgradeReplacementPage();
		CallDeferred(nameof(FocusAdventureUpgradeReplacementDefaultButton));
	}

	private void ConfigureAdventureUpgradeReplacementCard(
		AdventureUpgradeChoiceArt choiceArt,
		AdventureSuitUpgradeDefinition definition,
		float highlightAmount)
	{
		AdventureUpgradeCardVisualEntryResource? visualEntry =
			AdventureUpgradeCardVisualResolver.ResolveEntry(definition.Suit, definition.Rarity);
		Texture2D? cardTexture = AdventureUpgradeCardVisualResolver.LoadAtlasTextureForRarity(
			definition.Suit,
			definition.Rarity);
		choiceArt.Modulate = Colors.White;
		choiceArt.ConfigureChoice(
			cardTexture,
			string.Empty,
			FormatAdventureUpgradeDescription(definition),
			false);
		choiceArt.SetPalette(
			visualEntry?.TitleColor ?? DefaultAdventureUpgradeTitleColor,
			visualEntry?.GlowColor ?? DefaultAdventureUpgradeGlowColor,
			visualEntry?.RuleColor ?? DefaultAdventureUpgradeRuleColor);
		choiceArt.SetHighlightAmount(highlightAmount);
	}

	private void RefreshAdventureUpgradeReplacementPage()
	{
		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(
			SelectionOverlayKind.AdventureUpgradeReplacement);
		if (session == null || !IsSelectionOverlaySessionVisible(session))
		{
			return;
		}

		int pageCount = Math.Max(
			1,
			(_adventureUpgradeReplacementOptions.Count + AdventureUpgradeReplacementPageSize - 1)
				/ AdventureUpgradeReplacementPageSize);
		_adventureUpgradeReplacementPage = Math.Clamp(
			_adventureUpgradeReplacementPage,
			0,
			pageCount - 1);
		int firstOptionIndex = _adventureUpgradeReplacementPage * AdventureUpgradeReplacementPageSize;
		for (int slotIndex = 0; slotIndex < AdventureUpgradeReplacementPageSize; slotIndex++)
		{
			Button button = GetAdventureUpgradeReplacementOptionButton(session.Root, slotIndex);
			AdventureUpgradeChoiceArt choiceArt = button.GetNode<AdventureUpgradeChoiceArt>("ChoiceArt");
			int optionIndex = firstOptionIndex + slotIndex;
			bool available = optionIndex < _adventureUpgradeReplacementOptions.Count;
			button.Visible = true;
			button.Disabled = !available;
			button.FocusMode = available ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
			button.MouseFilter = available ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
			_adventureUpgradeReplacementPointerHovered[slotIndex] = false;
			if (available)
			{
				ConfigureAdventureUpgradeReplacementCard(
					choiceArt,
					_adventureUpgradeReplacementOptions[optionIndex],
					0.0f);
			}
			else
			{
				button.ReleaseFocus();
				choiceArt.ConfigureChoice(null, string.Empty, string.Empty, false);
				choiceArt.Modulate = Colors.Transparent;
			}
		}

		Button previousButton = session.Root.GetNode<Button>(
			"Margin/Layout/Content/CurrentColumn/PageRow/PreviousButton");
		Button nextButton = session.Root.GetNode<Button>(
			"Margin/Layout/Content/CurrentColumn/PageRow/NextButton");
		previousButton.Disabled = _adventureUpgradeReplacementPage == 0;
		nextButton.Disabled = _adventureUpgradeReplacementPage >= pageCount - 1;
		session.Root.GetNode<Label>("Margin/Layout/Content/CurrentColumn/PageLabel").Text =
			LocalizeFormat(
				"GAME_UPGRADE_REPLACEMENT_PAGE",
				_adventureUpgradeReplacementPage + 1,
				pageCount);
	}

	private void ChangeAdventureUpgradeReplacementPage(int delta)
	{
		int previousPage = _adventureUpgradeReplacementPage;
		_adventureUpgradeReplacementPage += Math.Sign(delta);
		RefreshAdventureUpgradeReplacementPage();
		if (_adventureUpgradeReplacementPage != previousPage)
		{
			FocusAdventureUpgradeReplacementDefaultButton();
		}
	}

	private void FocusAdventureUpgradeReplacementDefaultButton()
	{
		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(
			SelectionOverlayKind.AdventureUpgradeReplacement);
		if (session == null || !IsSelectionOverlaySessionVisible(session))
		{
			return;
		}

		for (int slotIndex = 0; slotIndex < AdventureUpgradeReplacementPageSize; slotIndex++)
		{
			Button button = GetAdventureUpgradeReplacementOptionButton(session.Root, slotIndex);
			if (!button.Disabled)
			{
				button.GrabFocus();
				RefreshAdventureUpgradeReplacementHighlight(slotIndex);
				return;
			}
		}
	}

	private bool TrySelectAdventureUpgradeReplacementSlot(int slotIndex)
	{
		int optionIndex = _adventureUpgradeReplacementPage * AdventureUpgradeReplacementPageSize + slotIndex;
		return TrySelectActiveSelectionOverlayOption(
			SelectionOverlayKind.AdventureUpgradeReplacement,
			optionIndex);
	}

	private void SetAdventureUpgradeReplacementPointerHover(int slotIndex, bool hovered)
	{
		if (slotIndex < 0 || slotIndex >= _adventureUpgradeReplacementPointerHovered.Length)
		{
			return;
		}
		_adventureUpgradeReplacementPointerHovered[slotIndex] = hovered;
		RefreshAdventureUpgradeReplacementHighlight(slotIndex);
	}

	private void RefreshAdventureUpgradeReplacementHighlight(int slotIndex)
	{
		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(
			SelectionOverlayKind.AdventureUpgradeReplacement);
		if (session == null || slotIndex < 0 || slotIndex >= AdventureUpgradeReplacementPageSize)
		{
			return;
		}

		Button button = GetAdventureUpgradeReplacementOptionButton(session.Root, slotIndex);
		AdventureUpgradeChoiceArt choiceArt = button.GetNode<AdventureUpgradeChoiceArt>("ChoiceArt");
		choiceArt.SetHighlightAmount(
			!button.Disabled && (button.HasFocus() || _adventureUpgradeReplacementPointerHovered[slotIndex])
				? 1.0f
				: 0.0f);
	}

	private static Button GetAdventureUpgradeReplacementOptionButton(Control root, int slotIndex)
	{
		return root.GetNode<Button>(
			$"Margin/Layout/Content/CurrentColumn/PageRow/CardsRow/Option{slotIndex + 1}Button");
	}

	private void ReturnFromAdventureUpgradeReplacement()
	{
		ResolveAdventureUpgradeReplacement(
			new AdventureUpgradeReplacementResult(
				AdventureUpgradeReplacementOutcome.Returned,
				-1),
			clearOverlay: true);
	}

	private void ResolveAdventureUpgradeReplacement(
		AdventureUpgradeReplacementResult result,
		bool clearOverlay)
	{
		TaskCompletionSource<AdventureUpgradeReplacementResult>? completionSource =
			_adventureUpgradeReplacementCompletionSource;
		_adventureUpgradeReplacementCompletionSource = null;
		completionSource?.TrySetResult(result);
		if (clearOverlay)
		{
			DisposeActiveSelectionOverlay();
		}
	}
}
