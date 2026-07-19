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

	private readonly Queue<AdventureUpgradeTriggerSource> _pendingAdventureUpgradeTriggers = new();
	private Task _adventureUpgradeResolutionTask = Task.CompletedTask;
	private int _pendingAdventureWinnerPlayerIndex = -1;
	private bool _isResolvingAdventureUpgradeSelection;

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
					AdventureSaveGateResult<AdventureUpgradeGrantCommitResult> grantGate =
						await ExecuteAdventureSaveGateAsync(
							() => GrantAdventureUpgrade(selectedDefinition.Id, triggerSource),
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
					_pendingAdventureUpgradeTriggers.Dequeue();
					continue;
				}

					await ApplyAdventureUpgradeAcquisitionEffectsAsync(
						grantResult,
						turnSequenceGeneration);
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
		if (!_adventureUpgradeCatalog.TryCreateManifest(out AdventureUpgradeManifest manifest))
		{
			return Array.Empty<AdventureSuitUpgradeDefinition>();
		}

		IReadOnlyList<AdventureUpgradeDefinitionModel> offerModels = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			runState.GetAcquiredUpgradeIds(),
			Math.Max(1, runState.CurrentRunStageIndex),
			maxExclusive => _rng.RandiRange(0, maxExclusive - 1),
			manifest);

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
}
