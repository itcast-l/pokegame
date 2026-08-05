using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private PlayingCard PrepareAdventureEnemyPlayedCard(int playerIndex, PlayingCard card)
	{
		if (!ShouldApplyNarwhalDoubleToPlayedCard(playerIndex, card))
		{
			return card;
		}

		string doubleItemCardId = GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.DoubleItemCardId, DoubleItemCardId);
		if (!IsItemAttachedEffectType(doubleItemCardId, CardEffectTypes.DoubleCollect))
		{
			return card;
		}

		bool countLimitedActivation = IsBossKEnemy(playerIndex);
		TryUpdateMatchFlowState(state => state with
		{
			DoubleEffectReady = false,
			LimitedDoubleEffectActivationsThisMatch = countLimitedActivation
				? state.LimitedDoubleEffectActivationsThisMatch == int.MaxValue
					? int.MaxValue
					: state.LimitedDoubleEffectActivationsThisMatch + 1
				: state.LimitedDoubleEffectActivationsThisMatch,
		});

		return card.WithAttachedItemEffect(doubleItemCardId);
	}

	private bool ShouldApplyNarwhalDoubleToPlayedCard(int playerIndex, PlayingCard card)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.TuskBreak)
			&& playerIndex == 1
			&& CurrentMatchFlowState.DoubleEffectReady
			&& !card.HasItemEffect
			&& !IsExcludedDeckRank(card.Rank)
			&& (!IsBossKEnemy(playerIndex)
				|| CurrentMatchFlowState.LimitedDoubleEffectActivationsThisMatch < MaxBossKDoubleCollectTriggersPerMatch)
			&& (!IsBossKEnemy(playerIndex) || FindMatchingTableCardIndex(card) >= 0);
	}

	private bool IsItemAttachedEffectType(string itemCardId, string effectType)
	{
		return _itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			&& string.Equals(definition.AttachedEffectType, effectType, StringComparison.Ordinal);
	}

	private int PickNautilusBombInsertionIndex(
		int actingPlayerIndex,
		int turnSequenceGeneration)
	{
		if (_tableCards.Count < 2)
		{
			return -1;
		}

		if (_tableCards.Count == 2)
		{
			return 1;
		}

		return TrySubmitChooseRandomIndexRuleCommand(
			actingPlayerIndex,
			minimumInclusive: 1,
			maximumInclusive: _tableCards.Count - 1,
			turnSequenceGeneration,
			out int insertionIndex)
			? insertionIndex
			: -1;
	}

	private bool IsBossKEnemy(int playerIndex)
	{
		return adventure_mode
			&& playerIndex == 1
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.DragonKing);
	}

	private bool BossKCanUseActiveControl(int playerIndex)
	{
		return IsBossKEnemy(playerIndex) && !CurrentMatchFlowState.ActiveControlUsedThisTurn;
	}

	private void MarkBossKActiveControlUsedIfNeeded(int playerIndex)
	{
		if (IsBossKEnemy(playerIndex))
		{
			TryUpdateMatchFlowState(state => state with
			{
				ActiveControlUsedThisTurn = true,
			});
		}
	}

	private int PickVisibleMainPlayerHandSlotIndex(
		int actingPlayerIndex,
		int turnSequenceGeneration)
	{
		int totalHandCardCount = GetTotalHandCardCount(MainPlayerIndex);
		var candidates = ImmutableArray.CreateBuilder<int>(totalHandCardCount);
		for (int displayIndex = 0; displayIndex < totalHandCardCount; displayIndex++)
		{
			if (!IsHandDisplaySlotHidden(MainPlayerIndex, displayIndex))
			{
				candidates.Add(displayIndex);
			}
		}

		if (candidates.Count <= 1)
		{
			if (candidates.Count == 1)
			{
				TrySubmitChooseReservoirHandDisplayIndexRuleCommand(
					actingPlayerIndex,
					MainPlayerIndex,
					candidates.ToImmutable(),
					turnSequenceGeneration,
					out _);
			}
			return -1;
		}

		return TrySubmitChooseReservoirHandDisplayIndexRuleCommand(
			actingPlayerIndex,
			MainPlayerIndex,
			candidates.ToImmutable(),
			turnSequenceGeneration,
			out int selectedDisplayIndex)
			? selectedDisplayIndex
			: -1;
	}

	private void ClearJellyfishBubbleIfMainPlayerTurnCompleted(int playerIndex, PlayCardResult result)
	{
		if (playerIndex != MainPlayerIndex
			|| result.Captured
			|| CurrentMatchFlowState.DisabledMainPlayerHandDisplayIndex < 0)
		{
			return;
		}

		TryUpdateMatchFlowState(state => state with
		{
			DisabledMainPlayerHandDisplayIndex = -1,
		});
		RenderPlayerHands();
	}

	private void ResetJellyfishBubbleTurnStateIfNeeded(int previousPlayerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.BubbleCover)
			|| previousPlayerIndex == 1
			|| _currentTurnPlayerIndex != 1)
		{
			return;
		}

		TryUpdateMatchFlowState(state => state with
		{
			HandDisableTriggeredThisTurn = false,
		});
	}

	private void ShiftHandDisplayStateAfterRemovedCard(int playerIndex, int removedDisplayIndex)
	{
		if (playerIndex < 0 || removedDisplayIndex < 0)
		{
			return;
		}

		ShiftHiddenHandDisplaySlotsAfterRemovedCard(playerIndex, removedDisplayIndex);
		ShiftJellyfishDisabledHandSlotAfterRemovedCard(playerIndex, removedDisplayIndex);
	}

	private void ShiftHiddenHandDisplaySlotsAfterRemovedCard(int playerIndex, int removedDisplayIndex)
	{
		if (_hiddenHandDisplaySlots.Count == 0)
		{
			return;
		}

		_shiftedHiddenHandDisplaySlots.Clear();
		bool changed = false;
		foreach (string key in _hiddenHandDisplaySlots)
		{
			if (!TryParseHandDisplaySlotKey(key, out int keyPlayerIndex, out int displayIndex)
				|| keyPlayerIndex != playerIndex)
			{
				_shiftedHiddenHandDisplaySlots.Add(key);
				continue;
			}

			changed = true;
			if (displayIndex < removedDisplayIndex)
			{
				_shiftedHiddenHandDisplaySlots.Add(key);
			}
			else if (displayIndex > removedDisplayIndex)
			{
				_shiftedHiddenHandDisplaySlots.Add(BuildHandDisplaySlotKey(playerIndex, displayIndex - 1));
			}
		}

		if (!changed)
		{
			_shiftedHiddenHandDisplaySlots.Clear();
			return;
		}

		_hiddenHandDisplaySlots.Clear();
		foreach (string key in _shiftedHiddenHandDisplaySlots)
		{
			_hiddenHandDisplaySlots.Add(key);
		}
		_shiftedHiddenHandDisplaySlots.Clear();
	}

	private static bool TryParseHandDisplaySlotKey(string key, out int playerIndex, out int displayIndex)
	{
		playerIndex = -1;
		displayIndex = -1;
		int separatorIndex = key.IndexOf(':');
		return separatorIndex > 0
			&& separatorIndex < key.Length - 1
			&& int.TryParse(key[..separatorIndex], out playerIndex)
			&& int.TryParse(key[(separatorIndex + 1)..], out displayIndex);
	}

	private void ShiftJellyfishDisabledHandSlotAfterRemovedCard(int playerIndex, int removedDisplayIndex)
	{
		if (playerIndex != MainPlayerIndex
			|| CurrentMatchFlowState.DisabledMainPlayerHandDisplayIndex < 0
			|| removedDisplayIndex > CurrentMatchFlowState.DisabledMainPlayerHandDisplayIndex)
		{
			return;
		}

		if (removedDisplayIndex == CurrentMatchFlowState.DisabledMainPlayerHandDisplayIndex)
		{
			TryUpdateMatchFlowState(state => state with
			{
				DisabledMainPlayerHandDisplayIndex = -1,
			});
			return;
		}

		TryUpdateMatchFlowState(state => state with
		{
			DisabledMainPlayerHandDisplayIndex =
				state.DisabledMainPlayerHandDisplayIndex - 1,
		});
	}

	private bool IsJellyfishDisabledHandDisplaySlot(int playerIndex, int displayIndex)
	{
		return playerIndex == MainPlayerIndex
			&& displayIndex == CurrentMatchFlowState.DisabledMainPlayerHandDisplayIndex;
	}
}
