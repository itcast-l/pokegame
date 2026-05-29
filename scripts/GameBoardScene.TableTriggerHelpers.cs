using System;
using System.Collections.Generic;
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

			_narwhalDoubleReady = false;
			return card.WithAttachedItemEffect(doubleItemCardId);
		}

	private bool ShouldApplyNarwhalDoubleToPlayedCard(int playerIndex, PlayingCard card)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.TuskBreak)
			&& playerIndex == 1
			&& _narwhalDoubleReady
			&& !card.HasItemEffect
			&& !IsExcludedDeckRank(card.Rank)
			&& (!IsBossKEnemy(playerIndex) || FindMatchingTableCardIndex(card) >= 0);
	}

	private bool IsItemAttachedEffectType(string itemCardId, string effectType)
	{
		return _itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			&& string.Equals(definition.AttachedEffectType, effectType, StringComparison.Ordinal);
	}

	private int PickNautilusBombInsertionIndex()
	{
		if (_tableCards.Count < 2)
		{
			return -1;
		}

		if (_tableCards.Count == 2)
		{
			return 1;
		}

		return _rng.RandiRange(1, _tableCards.Count - 1);
	}

	private bool IsBossKEnemy(int playerIndex)
	{
		return adventure_mode
			&& playerIndex == 1
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.DragonKing);
	}

	private bool BossKCanUseActiveControl(int playerIndex)
	{
		return IsBossKEnemy(playerIndex) && !_bossKActiveControlUsedThisTurn;
	}

	private void MarkBossKActiveControlUsedIfNeeded(int playerIndex)
	{
		if (IsBossKEnemy(playerIndex))
		{
			_bossKActiveControlUsedThisTurn = true;
		}
	}

	private void RefreshGardenEelBuriedCardIndex()
	{
		_gardenEelBuriedTableCardIndex = -1;
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (_tableCards[tableCardIndex].IsBuriedInSand)
			{
				_gardenEelBuriedTableCardIndex = tableCardIndex;
				return;
			}
		}
	}

	private int PickVisibleMainPlayerHandSlotIndex()
	{
		int totalHandCardCount = GetTotalHandCardCount(MainPlayerIndex);
		int selectedDisplayIndex = -1;
		int candidateCount = 0;
		for (int displayIndex = 0; displayIndex < totalHandCardCount; displayIndex++)
		{
			if (!IsHandDisplaySlotHidden(MainPlayerIndex, displayIndex))
			{
				candidateCount++;
				if (_rng.RandiRange(1, candidateCount) == 1)
				{
					selectedDisplayIndex = displayIndex;
				}
			}
		}

		return candidateCount <= 1 ? -1 : selectedDisplayIndex;
	}

	private void ClearJellyfishBubbleIfMainPlayerTurnCompleted(int playerIndex, PlayCardResult result)
	{
		if (playerIndex != MainPlayerIndex
			|| result.Captured
			|| _jellyfishDisabledMainPlayerHandSlotIndex < 0)
		{
			return;
		}

		_jellyfishDisabledMainPlayerHandSlotIndex = -1;
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

		_jellyfishBubbleTriggeredThisTurn = false;
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
			|| _jellyfishDisabledMainPlayerHandSlotIndex < 0
			|| removedDisplayIndex > _jellyfishDisabledMainPlayerHandSlotIndex)
		{
			return;
		}

		if (removedDisplayIndex == _jellyfishDisabledMainPlayerHandSlotIndex)
		{
			_jellyfishDisabledMainPlayerHandSlotIndex = -1;
			return;
		}

		_jellyfishDisabledMainPlayerHandSlotIndex--;
	}

	private bool IsJellyfishDisabledHandDisplaySlot(int playerIndex, int displayIndex)
	{
		return playerIndex == MainPlayerIndex
			&& displayIndex == _jellyfishDisabledMainPlayerHandSlotIndex;
	}
}
