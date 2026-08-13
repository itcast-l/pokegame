using System.Collections.Generic;
using Deckland.Rules;

public partial class GameBoardScene
{
	private readonly record struct AdventureEnemyNextBaseCardModifiers(
		bool FacesUp,
		int TableInsertIndex,
		int CaptureDamageDelta);

	private TableCard? GetPendingAdventureEnemyInsertAnchor() =>
		FindCurrentTableCard(CurrentMatchFlowState.PendingPlacementAnchorRuntimeId);
	private AdventureEnemyNextCardInsertMode GetPendingAdventureEnemyCardInsertMode() =>
		(AdventureEnemyNextCardInsertMode)(int)CurrentMatchFlowState.PendingCardPlacement;

	private void ResetAdventureEnemyMissPendingState()
	{
		TryUpdateMatchFlowState(state => state with
		{
			PendingBaseCardFacesUp = false,
			PendingCardPlacement = PendingCardPlacementMode.None,
			PendingCaptureDamageDelta = 0,
			PendingPlacementAnchorRuntimeId = 0,
		});
	}

	private void ApplyAdventureEnemyMissEffects(
		int playerIndex,
		int playedTableCardIndex,
		IReadOnlyCollection<TableCard> prePlayTableCards)
	{
		if (!ShouldUseAdventureUpgradeSystem() || !IsAdventureEnemyPlayer(playerIndex))
		{
			return;
		}

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return;
		}

		bool hadOldTableCardsBeforeMiss = prePlayTableCards.Count > 0;
		AdventureEnemyMissEffectResult effectResult = runState.ResolveEnemyMissEffects(
			_tableCards.Count,
			hadOldTableCardsBeforeMiss);
		TableCard? insertionAnchor = ResolveAdventureEnemyNextInsertAnchorAfterMiss(
			effectResult.InsertMode,
			playedTableCardIndex);
		TryUpdateMatchFlowState(state => state with
		{
			PendingBaseCardFacesUp = effectResult.NextCardFacesUp,
			PendingCardPlacement = (PendingCardPlacementMode)(int)effectResult.InsertMode,
			PendingCaptureDamageDelta = effectResult.NextCaptureDamageDelta,
			PendingPlacementAnchorRuntimeId = insertionAnchor?.InstanceId ?? 0,
		});
		if (effectResult.DirectDamageToEnemy > 0)
		{
			AddPlayerScore(MainPlayerIndex, effectResult.DirectDamageToEnemy);
		}

		if (effectResult.GrantMainPlayerExtraBaseAction)
		{
			TryGrantAdventureMainPlayerExtraBaseCardAction(
				AdventureInterceptBeatUpgradeId,
				resumeNormalTurnAfterChain: true);
		}
	}

	private void ApplyAdventureEnemyCaptureEffects(int playerIndex)
	{
		if (!ShouldUseAdventureUpgradeSystem() || !IsAdventureEnemyPlayer(playerIndex))
		{
			return;
		}

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return;
		}

		AdventureEnemyCaptureEffectResult effectResult = runState.ResolveEnemyCaptureEffects();
		if (!effectResult.NextCardFacesUp && effectResult.InsertMode == AdventureEnemyNextCardInsertMode.None)
		{
			return;
		}

		TryUpdateMatchFlowState(state => state with
		{
			PendingBaseCardFacesUp = effectResult.NextCardFacesUp,
			PendingCardPlacement = (PendingCardPlacementMode)(int)effectResult.InsertMode,
			PendingPlacementAnchorRuntimeId = 0,
		});
	}

	private TableCard? ResolveAdventureEnemyNextInsertAnchorAfterMiss(
		AdventureEnemyNextCardInsertMode insertMode,
		int playedTableCardIndex)
	{
		if (insertMode != AdventureEnemyNextCardInsertMode.BeforeTrackedOldest
			|| _tableCards.Count <= 1)
		{
			return null;
		}

		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (tableCardIndex != playedTableCardIndex)
			{
				return _tableCards[tableCardIndex];
			}
		}

		return null;
	}

	private AdventureEnemyNextBaseCardModifiers ConsumeAdventureEnemyNextBaseCardModifiers(int playerIndex)
	{
		if (!IsAdventureEnemyPlayer(playerIndex))
		{
			return new AdventureEnemyNextBaseCardModifiers(false, -1, 0);
		}

		int tableInsertIndex = -1;
		switch (GetPendingAdventureEnemyCardInsertMode())
		{
			case AdventureEnemyNextCardInsertMode.Leftmost:
				tableInsertIndex = 0;
				break;
			case AdventureEnemyNextCardInsertMode.BeforeTrackedOldest:
				TableCard? insertionAnchor = GetPendingAdventureEnemyInsertAnchor();
				tableInsertIndex = insertionAnchor != null
					? _tableCards.IndexOf(insertionAnchor)
					: -1;
				if (tableInsertIndex < 0 && _tableCards.Count > 0)
				{
					tableInsertIndex = 0;
				}
				break;
		}

		AdventureEnemyNextBaseCardModifiers modifiers = new(
			CurrentMatchFlowState.PendingBaseCardFacesUp,
			tableInsertIndex,
			CurrentMatchFlowState.PendingCaptureDamageDelta);
		ResetAdventureEnemyMissPendingState();
		return modifiers;
	}
}
