using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
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

	private Task _adventureUpgradeResolutionTask = Task.CompletedTask;
	private bool _isResolvingAdventureUpgradeSelection;

	private ImmutableArray<PendingProgressionChoiceKind> PendingProgressionChoices =>
		CurrentMatchFlowState.NormalizedPendingProgressionChoices;

	private void QueueAdventureRunStartUpgradeIfNeeded()
	{
		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (!ShouldUseAdventureUpgradeSystem() || runState?.ShouldGrantRunStartUpgrade != true)
		{
			return;
		}

		EnqueueAdventureUpgradeTriggers(
			runState.BuildUpgradeTriggerOrder(
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

		ImmutableArray<PendingProgressionChoiceKind> pending = PendingProgressionChoices;
		var next = pending.ToBuilder();
		foreach (AdventureUpgradeTriggerSource triggerSource in triggerSources)
		{
			PendingProgressionChoiceKind choice = ToPendingProgressionChoice(triggerSource);
			if (choice == PendingProgressionChoiceKind.None
				|| next.Contains(choice))
			{
				continue;
			}

			next.Add(choice);
		}

		if (next.Count != pending.Length)
		{
			ImmutableArray<PendingProgressionChoiceKind> updated = next.ToImmutable();
			TryUpdateMatchFlowState(state => state with
			{
				PendingProgressionChoices = updated,
			});
		}
	}

	private bool HasPendingAdventureUpgradeTrigger(AdventureUpgradeTriggerSource triggerSource)
	{
		PendingProgressionChoiceKind choice = ToPendingProgressionChoice(triggerSource);
		return choice != PendingProgressionChoiceKind.None
			&& PendingProgressionChoices.Contains(choice);
	}

	private int PendingAdventureUpgradeTriggerCount => PendingProgressionChoices.Length;

	private AdventureUpgradeTriggerSource PeekPendingAdventureUpgradeTrigger()
	{
		return PendingProgressionChoices is { IsEmpty: false } pending
			? FromPendingProgressionChoice(pending[0])
			: AdventureUpgradeTriggerSource.None;
	}

	private bool TryDequeuePendingAdventureUpgradeTrigger(
		AdventureUpgradeTriggerSource expectedTrigger)
	{
		PendingProgressionChoiceKind expected = ToPendingProgressionChoice(expectedTrigger);
		bool removed = false;
		bool updated = TryUpdateMatchFlowState(state =>
		{
			ImmutableArray<PendingProgressionChoiceKind> pending =
				state.NormalizedPendingProgressionChoices;
			if (expected == PendingProgressionChoiceKind.None
				|| pending.IsEmpty
				|| pending[0] != expected)
			{
				return state;
			}

			removed = true;
			return state with
			{
				PendingProgressionChoices = pending.Length == 1
					? default
					: pending.RemoveAt(0),
			};
		});
		return updated && removed;
	}

	private void ClearPendingAdventureUpgradeFlow()
	{
		TryUpdateMatchFlowState(state => state with
		{
			PendingProgressionChoices = default,
			PendingWinnerSeat = -1,
		});
	}

	private static PendingProgressionChoiceKind ToPendingProgressionChoice(
		AdventureUpgradeTriggerSource triggerSource)
	{
		return triggerSource switch
		{
			AdventureUpgradeTriggerSource.RunStart => PendingProgressionChoiceKind.SessionStart,
			AdventureUpgradeTriggerSource.BigCapture => PendingProgressionChoiceKind.Milestone,
			AdventureUpgradeTriggerSource.Victory => PendingProgressionChoiceKind.Victory,
			_ => PendingProgressionChoiceKind.None,
		};
	}

	private static AdventureUpgradeTriggerSource FromPendingProgressionChoice(
		PendingProgressionChoiceKind choice)
	{
		return choice switch
		{
			PendingProgressionChoiceKind.SessionStart => AdventureUpgradeTriggerSource.RunStart,
			PendingProgressionChoiceKind.Milestone => AdventureUpgradeTriggerSource.BigCapture,
			PendingProgressionChoiceKind.Victory => AdventureUpgradeTriggerSource.Victory,
			_ => AdventureUpgradeTriggerSource.None,
		};
	}

	private IReadOnlyList<AdventureUpgradeTriggerSource> BuildAdventureUpgradeTriggerOrder(
		bool runStartTriggered,
		bool bigCaptureTriggered,
		bool victoryTriggered)
	{
		return GetAdventureUpgradeRunState()?.BuildUpgradeTriggerOrder(
				runStartTriggered,
				bigCaptureTriggered,
				victoryTriggered)
			?? Array.Empty<AdventureUpgradeTriggerSource>();
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
		if (PendingAdventureUpgradeTriggerCount == 0)
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
			while (PendingAdventureUpgradeTriggerCount > 0
				&& turnSequenceGeneration == _turnSequenceGeneration
				&& !_isLeavingGame
				&& !IsGameOver())
			{
				AdventureUpgradeTriggerSource triggerSource = PeekPendingAdventureUpgradeTrigger();
				IReadOnlyList<AdventureSuitUpgradeDefinition> offer = BuildAdventureUpgradeOffer();
				if (offer.Count == 0)
				{
					if (!TryDequeuePendingAdventureUpgradeTrigger(triggerSource))
					{
						return;
					}
					continue;
				}

				bool triggerConsumed = false;
				while (!triggerConsumed
					&& turnSequenceGeneration == _turnSequenceGeneration
					&& !_isLeavingGame
					&& !IsGameOver())
				{
					AdventureUpgradeSelectionResult selectionResult =
						await ShowAdventureUpgradeSelectionAsync(triggerSource, offer);
					if (turnSequenceGeneration != _turnSequenceGeneration || _isLeavingGame)
					{
						return;
					}

					if (selectionResult.Outcome is AdventureUpgradeSelectionOutcome.Canceled
						&& triggerSource != AdventureUpgradeTriggerSource.None)
					{
						return;
					}

					int selectedIndex = selectionResult.Outcome == AdventureUpgradeSelectionOutcome.DisplayFailed
						? 0
						: selectionResult.SelectedIndex;
					if (selectedIndex < 0 || selectedIndex >= offer.Count)
					{
						if (selectionResult.Outcome == AdventureUpgradeSelectionOutcome.DisplayFailed)
						{
							return;
						}
						continue;
					}

					AdventureSuitUpgradeDefinition selectedDefinition = offer[selectedIndex];
					string? replacedUpgradeId = null;
					AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
					if (runState?.AcquiredUpgradeIds.Length >= AdventureRunReadModel.MaxActiveUpgradeCount)
					{
						IReadOnlyList<AdventureSuitUpgradeDefinition> currentUpgrades =
							BuildCurrentAdventureUpgradeDefinitions(runState);
						if (currentUpgrades.Count == 0)
						{
							return;
						}

						AdventureUpgradeReplacementResult replacementResult =
							await ShowAdventureUpgradeReplacementAsync(
								selectedDefinition,
								currentUpgrades);
						if (turnSequenceGeneration != _turnSequenceGeneration || _isLeavingGame)
						{
							return;
						}
						if (replacementResult.Outcome == AdventureUpgradeReplacementOutcome.Returned)
						{
							continue;
						}

						int replacementIndex =
							replacementResult.Outcome == AdventureUpgradeReplacementOutcome.DisplayFailed
								? 0
								: replacementResult.SelectedIndex;
						if (replacementIndex < 0 || replacementIndex >= currentUpgrades.Count)
						{
							if (replacementResult.Outcome == AdventureUpgradeReplacementOutcome.DisplayFailed)
							{
								return;
							}
							continue;
						}
						replacedUpgradeId = currentUpgrades[replacementIndex].Id;
					}

					AdventureSaveGateResult<AdventureUpgradeGrantCommitResult> grantGate =
						await ExecuteAdventureSaveGateAsync(
							() => GrantAdventureUpgrade(
								selectedDefinition.Id,
								triggerSource,
								replacedUpgradeId),
							result => result.WriteResult == AdventureRunWriteResult.Succeeded,
							result => result.WriteResult == AdventureRunWriteResult.SaveFailed);
					if (!grantGate.Succeeded)
					{
						GD.PushWarning(
							$"Adventure upgrade acquisition failed: id={selectedDefinition.Id} result={grantGate.Result.WriteResult}");
						return;
					}
					AdventureUpgradeGrantCommitResult grantResult = grantGate.Result;
					if (grantResult.AcquireResult == AdventureUpgradeAcquireResult.DeferredManifestUnavailable)
					{
					GD.PushWarning(
						$"Adventure upgrade acquisition deferred because the manifest is unavailable: {selectedDefinition.Id}");
					return;
				}
					if (grantResult.AcquireResult != AdventureUpgradeAcquireResult.Acquired)
					{
						GD.PushWarning(
							$"Adventure upgrade acquisition rejected: id={selectedDefinition.Id} result={grantResult.AcquireResult}");
						if (!TryDequeuePendingAdventureUpgradeTrigger(triggerSource))
						{
							return;
						}
						triggerConsumed = true;
						continue;
					}

					ClearReplacedAdventureUpgradeBoardState(grantResult.ReplacedUpgradeId);
					await ApplyAdventureUpgradeAcquisitionEffectsAsync(
						grantResult,
						turnSequenceGeneration);
					GD.Print(
						grantResult.ReplacedUpgradeId == null
							? $"Adventure upgrade acquired: {selectedDefinition.Id}"
							: $"Adventure upgrade replaced: {grantResult.ReplacedUpgradeId} -> {selectedDefinition.Id}");
					if (!TryDequeuePendingAdventureUpgradeTrigger(triggerSource))
					{
						return;
					}
					triggerConsumed = true;
				}
			}
		}
		finally
		{
			_isResolvingAdventureUpgradeSelection = false;
		}

		if (CurrentMatchFlowState.PendingWinnerSeat >= 0
			&& PendingAdventureUpgradeTriggerCount == 0
			&& turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver())
		{
			int winnerPlayerIndex = CurrentMatchFlowState.PendingWinnerSeat;
			if (!TryUpdateMatchFlowState(state => state with
				{
					PendingWinnerSeat = -1,
				}))
			{
				return;
			}
			DeclareWinner(winnerPlayerIndex);
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

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return Array.Empty<AdventureSuitUpgradeDefinition>();
		}
		if (!_adventureUpgradeCatalog.TryCreateManifest(out AdventureUpgradeManifest manifest))
		{
			return Array.Empty<AdventureSuitUpgradeDefinition>();
		}

		bool randomSelectionFailed = false;
		int NextOfferRandomIndex(int maxExclusive)
		{
			if (maxExclusive > 0
				&& TrySubmitChooseRandomIndexRuleCommand(
					_currentTurnPlayerIndex,
					minimumInclusive: 0,
					maximumInclusive: maxExclusive - 1,
					_turnSequenceGeneration,
					out int selectedIndex))
			{
				return selectedIndex;
			}

			randomSelectionFailed = true;
			return 0;
		}

		IReadOnlyList<AdventureUpgradeDefinitionModel> offerModels = runState.BuildUpgradeOffer(
			definitions,
			NextOfferRandomIndex,
			manifest);

		if (randomSelectionFailed || offerModels.Count == 0)
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

	private IReadOnlyList<AdventureSuitUpgradeDefinition> BuildCurrentAdventureUpgradeDefinitions(
		AdventureRunReadModel runState)
	{
		EnsureAdventureUpgradeCatalogLoaded();
		if (_adventureUpgradeCatalog == null || runState.AcquiredUpgradeIds.IsEmpty)
		{
			return Array.Empty<AdventureSuitUpgradeDefinition>();
		}

		var acquiredIds = new HashSet<string>(runState.AcquiredUpgradeIds, StringComparer.Ordinal);
		var currentUpgrades = new List<AdventureSuitUpgradeDefinition>(acquiredIds.Count);
		foreach (AdventureSuitUpgradeDefinition definition in _adventureUpgradeCatalog.All)
		{
			if (acquiredIds.Contains(definition.Id))
			{
				currentUpgrades.Add(definition);
			}
		}

		return currentUpgrades;
	}

	private void ClearReplacedAdventureUpgradeBoardState(string? replacedUpgradeId)
	{
		if (string.IsNullOrWhiteSpace(replacedUpgradeId))
		{
			return;
		}

		bool redrawTable = false;
		switch (replacedUpgradeId.Trim())
		{
			case "spades_trace_hunt":
				redrawTable = CurrentMatchFlowState.CaptureMarkRuntimeId != 0
					|| FindAdventureTraceHuntMarkerOverlay() != null;
				SetAdventureTraceHuntMarkedTableCard(null, playIntro: false);
				ClearAdventureTraceHuntMarkerIntro();
				break;
			case "spades_hunt_order":
				TryUpdateMatchFlowState(state => state with
				{
					TurnCaptureMarkRuntimeId = 0,
				});
				break;
			case "clubs_canopy_reveal":
				TryUpdateMatchFlowState(state => state with
				{
					SequentialRevealAnchorRuntimeId = 0,
				});
				break;
			case "spades_dark_harbor_reveal":
				TryUpdateMatchFlowState(state => state with
				{
					PendingBaseCardFacesUp = false,
				});
				break;
			case "spades_missed_beat_blunt_hurt":
				TryUpdateMatchFlowState(state => state with
				{
					PendingCaptureDamageDelta = 0,
				});
				break;
			case "spades_wrong_shore_lineup":
				TryUpdateMatchFlowState(state =>
					state.PendingCardPlacement == PendingCardPlacementMode.Leftmost
						? state with
						{
							PendingCardPlacement = PendingCardPlacementMode.None,
							PendingPlacementAnchorRuntimeId = 0,
						}
						: state);
				break;
			case "spades_return_tide_misorder":
				TryUpdateMatchFlowState(state =>
					state.PendingCardPlacement == PendingCardPlacementMode.BeforeTrackedAnchor
						? state with
						{
							PendingCardPlacement = PendingCardPlacementMode.None,
							PendingPlacementAnchorRuntimeId = 0,
						}
						: state);
				break;
		}

		if (redrawTable)
		{
			RenderTableCards();
		}
	}
}
