using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private async Task ApplyJellyfishBubbleAfterCaptureAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.BubbleCover)
			|| playerIndex != 1
			|| CurrentMatchFlowState.HandDisableTriggeredThisTurn
			|| CurrentMatchFlowState.DisabledMainPlayerHandDisplayIndex >= 0)
		{
			return;
		}

		int handSlotIndex = PickVisibleMainPlayerHandSlotIndex(
			playerIndex,
			turnSequenceGeneration);
		if (handSlotIndex < 0)
		{
			return;
		}

		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.BubbleCover);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		TryUpdateMatchFlowState(state => state with
		{
			HandDisableTriggeredThisTurn = true,
			DisabledMainPlayerHandDisplayIndex = handSlotIndex,
		});
		RenderPlayerHands();
	}

	private bool ShouldApplyGardenEelSandBite(int playerIndex, int captureStartIndex, bool playedRankCollectsTable)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.SandAmbush)
			&& playerIndex == 1
			&& CurrentMatchFlowState.CaptureThresholdReady
			&& !playedRankCollectsTable
			&& captureStartIndex >= 0
			&& captureStartIndex < _tableCards.Count
			&& !_tableCards[captureStartIndex].IsBuriedInSand
			&& _tableCards[captureStartIndex].Card.HasValue;
	}

	private int ConsumeGardenEelSandBiteBonusPoints(
		int playerIndex,
		int captureStartIndex,
		int scorePlayerIndex,
		bool hasDoubleEffect,
		bool playedRankCollectsTable)
	{
		if (!ShouldApplyGardenEelSandBite(playerIndex, captureStartIndex, playedRankCollectsTable))
		{
			return 0;
		}

		TryUpdateMatchFlowState(state => state with
		{
			CaptureThresholdReady = false,
			CaptureThresholdProgress = 0,
		});
		return GetHalfCapturedCardScoreValueForPlayer(_tableCards[captureStartIndex].Card!.Value, scorePlayerIndex, hasDoubleEffect);
	}

	private void RecordGardenEelCaptureForSandBite(int playerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.SandAmbush)
			|| playerIndex != 1
			|| CurrentMatchFlowState.CaptureThresholdReady)
		{
			return;
		}

		int threshold = GetAdventureEnemyAbilityValue(
			AdventureEnemyAbilityValueKeys.CapturesBeforeSandBite,
			2);
		TryUpdateMatchFlowState(state =>
		{
			int nextProgress = state.CaptureThresholdProgress == int.MaxValue
				? int.MaxValue
				: state.CaptureThresholdProgress + 1;
			bool ready = nextProgress >= threshold;
			return state with
			{
				CaptureThresholdProgress = ready ? 0 : nextProgress,
				CaptureThresholdReady = ready,
			};
		});
	}

	private async Task ApplySeaSlugSlimeCoatAfterMissAsync(int playerIndex, int playedTableCardIndex, int turnSequenceGeneration)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.ColorCurrent)
			|| playerIndex != 1
			|| playedTableCardIndex < 0
			|| playedTableCardIndex >= _tableCards.Count
			|| !_tableCards[playedTableCardIndex].Card.HasValue
			|| HasSeaSlugSlimeCoatedTableCard())
		{
			return;
		}

		int missThreshold = GetAdventureEnemyAbilityValue(
			AdventureEnemyAbilityValueKeys.MissesBeforeSlimeCoat,
			2);
		int nextMissProgress = CurrentMatchFlowState.MissThresholdProgress == int.MaxValue
			? int.MaxValue
			: CurrentMatchFlowState.MissThresholdProgress + 1;
		bool shouldTrigger = nextMissProgress >= missThreshold;
		TryUpdateMatchFlowState(state => state with
		{
			MissThresholdProgress = shouldTrigger ? 0 : nextMissProgress,
		});
		if (!shouldTrigger)
		{
			return;
		}
		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.ColorCurrent);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		if (playedTableCardIndex < 0
			|| playedTableCardIndex >= _tableCards.Count
			|| !_tableCards[playedTableCardIndex].Card.HasValue
			|| HasSeaSlugSlimeCoatedTableCard())
		{
			return;
		}

		TableCard playedTableCard = _tableCards[playedTableCardIndex];
		if (!TrySubmitSetTableCardsScoreSuppressedRuleCommand(
			playerIndex,
			ImmutableArray.Create(new RuleTableCardReference(
				playedTableCardIndex,
				playedTableCard.InstanceId)),
			expectedScoreSuppressed: false,
			scoreSuppressed: true,
			turnSequenceGeneration,
			out _))
		{
			return;
		}
		RenderTableCards();
	}

	private bool HasSeaSlugSlimeCoatedTableCard()
	{
		foreach (TableCard tableCard in _tableCards)
		{
			if (tableCard.IsSlimeCoated)
			{
				return true;
			}
		}

		return false;
	}

	private async Task ApplyNautilusBombAfterMainPlayerCaptureAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.SpiralShell)
			|| playerIndex != MainPlayerIndex
			|| CurrentMatchFlowState.PeriodicItemActivationsThisMatch >= GetNautilusBombInsertionLimit())
		{
			return;
		}

		int nextItemProgress = CurrentMatchFlowState.PeriodicItemProgress == int.MaxValue
			? int.MaxValue
			: CurrentMatchFlowState.PeriodicItemProgress + 1;
		TryUpdateMatchFlowState(state => state with
		{
			PeriodicItemProgress = nextItemProgress,
		});
		if (nextItemProgress < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeBomb, 3))
		{
			return;
		}

		if (_tableCards.Count < 2)
		{
			return;
		}

		string bombItemCardId = GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.BombItemCardId, BombItemCardId);
		if (!IsItemEffectType(bombItemCardId, CardEffectTypes.PlaceTablePairDestroy))
		{
			return;
		}

		ItemCard? bombCard = CreateItemCard(bombItemCardId);
		if (!bombCard.HasValue)
		{
			return;
		}

		int insertionIndex = PickNautilusBombInsertionIndex(
			playerIndex,
			turnSequenceGeneration);
		if (insertionIndex < 0)
		{
			return;
		}

		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(1, turnSequenceGeneration, AdventureEnemyAbilities.SpiralShell);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		InsertBombCardIntoTable(bombCard.Value, 1, insertionIndex);
		TryUpdateMatchFlowState(state => state with
		{
			PeriodicItemActivationsThisMatch =
				state.PeriodicItemActivationsThisMatch == int.MaxValue
					? int.MaxValue
					: state.PeriodicItemActivationsThisMatch + 1,
			PeriodicItemProgress = 0,
		});
		RenderTableCards();
		await ResolveBombPairAsync(turnSequenceGeneration);
		RenderTableCards();
	}

	private int GetNautilusBombInsertionLimit()
	{
		return IsBossKEnemy(AdventureEnemyPlayerIndex) ? MaxBossKBombInsertionsPerMatch : MaxNautilusBombInsertionsPerMatch;
	}

	private void RecordNarwhalCaptureForDouble(int playerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.TuskBreak)
			|| playerIndex != 1
			|| CurrentMatchFlowState.DoubleEffectReady
			|| (IsBossKEnemy(playerIndex)
				&& CurrentMatchFlowState.LimitedDoubleEffectActivationsThisMatch >= MaxBossKDoubleCollectTriggersPerMatch))
		{
			return;
		}

		int doubleThreshold = GetAdventureEnemyAbilityValue(
			AdventureEnemyAbilityValueKeys.CapturesBeforeDouble,
			2);
		TryUpdateMatchFlowState(state =>
		{
			int nextProgress = state.DoubleEffectProgress == int.MaxValue
				? int.MaxValue
				: state.DoubleEffectProgress + 1;
			bool ready = nextProgress >= doubleThreshold;
			return state with
			{
				DoubleEffectProgress = ready ? 0 : nextProgress,
				DoubleEffectReady = ready,
			};
		});
	}

}
