using System;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private AdventureSuitUpgradeCatalog _adventureUpgradeCatalog = default!;

	private TableCard? FindCurrentTableCard(long runtimeInstanceId)
	{
		if (runtimeInstanceId <= 0)
		{
			return null;
		}

		foreach (TableCard tableCard in _tableCards)
		{
			if (tableCard.InstanceId == runtimeInstanceId)
			{
				return tableCard;
			}
		}

		return null;
	}

	private void LoadAdventureUpgradeCatalog()
	{
		_adventureUpgradeCatalog = AdventureSuitUpgradeCatalog.Load(this);
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (_gameModePolicy.CanReadAdventureRun
			&& _authorityRuleSession != null
			&& store != null
			&& !store.BindAuthorityRuleSession(_authorityRuleSession))
		{
			GD.PushWarning("Failed to bind the adventure run to the board authority session.");
		}
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
		_isResolvingAdventureUpgradeSelection = false;
		ClearAdventureTraceHuntMarkerIntro();
		_adventureUpgradeResolutionTask = Task.CompletedTask;
		ResetAdventureUpgradeOverlayCardMotionStates();
		CancelAdventureUpgradeSelection();
		DisposeActiveSelectionOverlay();
	}

	private void WarnMissingAdventureRunSessionStore(string action)
	{
		GD.PushWarning($"AdventureRunSessionStore autoload is missing; cannot {action}.");
	}

	private AdventureRunReadModel? GetAdventureUpgradeRunState()
	{
		return GetAdventureRunSessionStore()?.GetReadModel();
	}

	private AdventureUpgradeGrantCommitResult GrantAdventureUpgrade(
		string upgradeId,
		AdventureUpgradeTriggerSource triggerSource,
		string? replacedUpgradeId = null)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("record an acquired adventure upgrade");
			return new AdventureUpgradeGrantCommitResult(
				AdventureRunWriteResult.SaveFailed,
				AdventureUpgradeAcquireResult.DeferredManifestUnavailable,
				default,
				null,
				null);
		}

		return store.GrantUpgrade(upgradeId, triggerSource, replacedUpgradeId);
	}

	private int DrawCardsFromDeckToHandForAdventureEffect(int playerIndex, int drawCount)
	{
		int normalizedDrawCount = Math.Max(0, drawCount);
		int drawnCardCount = 0;
		for (int drawIndex = 0; drawIndex < normalizedDrawCount; drawIndex++)
		{
			if (!DrawCardFromDeckToHand(playerIndex).HasValue)
			{
				break;
			}

			drawnCardCount++;
		}

		if (drawnCardCount > 0)
		{
			RenderPlayerCardPiles();
			RenderPlayerHands();
		}

		return drawnCardCount;
	}

	private void ResetAdventureBattleScopedStateForNewBattle()
	{
		ClearAdventureBattleScopedState(prepareNewBoard: true);
	}

	private void ClearAdventureBattleScopedStateForBattleEnd()
	{
		ClearAdventureBattleScopedState(prepareNewBoard: false);
	}

	private void ClearAdventureBattleScopedState(bool prepareNewBoard)
	{
		TryUpdateMatchFlowState(state => state with
		{
			SuccessfulCapturesThisTurn = 0,
			MainPlayerCapturesThisBattle = 0,
			QuestionRejudgeUsedThisBattle = false,
			HalfHealthRescueUsed = false,
			QuarterHealthRescueUsed = false,
			MainPlayerRealTurnActive = prepareNewBoard,
			MainPlayerRealTurnStartPending = false,
			PendingBaseCardFacesUp = false,
			PendingCardPlacement = PendingCardPlacementMode.None,
			PendingCaptureDamageDelta = 0,
			PendingPlacementAnchorRuntimeId = 0,
			CaptureMarkRuntimeId = 0,
			TurnCaptureMarkRuntimeId = 0,
			SequentialRevealAnchorRuntimeId = 0,
			PendingProgressionChoices = default,
			PendingWinnerSeat = -1,
		});
		ResetAdventureUpgradeBoardState();
		if (prepareNewBoard)
		{
			ResetAdventureInteractionStateForNewBoard();
		}
		else
		{
			ClearAdventureInteractionStateForBattleEnd();
		}

		MarkActivePlayerStatusTooltipForRefresh();
	}

	private async Task<bool> PrepareAdventureBattleForDealAsync()
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return true;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store?.HasActiveRun != true)
		{
			return false;
		}
		AdventureSaveGateResult<AdventureRunWriteResult> gate =
			await ExecuteAdventureSaveGateAsync(
				store.ResetBattleScopedEffects,
				result => result == AdventureRunWriteResult.Succeeded,
				result => result == AdventureRunWriteResult.SaveFailed);
		if (!gate.Succeeded)
		{
			GD.PushWarning($"Failed to reset adventure battle effects: {gate.Result}");
			return false;
		}
		return true;
	}

	private async Task<bool> DealCardsToPlayersWithAdventureGateAsync(
		bool playOpeningDealAnimation = false,
		bool playOpeningIntroAnimation = false)
	{
		if (!await PrepareAdventureBattleForDealAsync())
		{
			return false;
		}
		DealCardsToPlayers(playOpeningDealAnimation, playOpeningIntroAnimation);
		return true;
	}

	private bool HasAdventureUpgrade(string upgradeId)
	{
		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		return runState?.IsActive == true && runState.HasAcquiredUpgrade(upgradeId);
	}
}
