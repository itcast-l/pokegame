using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task ApplyJellyfishBubbleAfterCaptureAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.BubbleCover)
			|| playerIndex != 1
			|| _jellyfishBubbleTriggeredThisTurn
			|| _jellyfishDisabledMainPlayerHandSlotIndex >= 0)
		{
			return;
		}

		int handSlotIndex = PickVisibleMainPlayerHandSlotIndex();
		if (handSlotIndex < 0)
		{
			return;
		}

		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.BubbleCover);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		_jellyfishBubbleTriggeredThisTurn = true;
		_jellyfishDisabledMainPlayerHandSlotIndex = handSlotIndex;
		RenderPlayerHands();
	}

	private bool ShouldApplyGardenEelSandBite(int playerIndex, int captureStartIndex, bool playedRankCollectsTable)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.SandAmbush)
			&& playerIndex == 1
			&& _gardenEelSandBiteReady
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

		_gardenEelSandBiteReady = false;
		_gardenEelCaptureCountUntilSandBite = 0;
		return GetHalfCapturedCardScoreValueForPlayer(_tableCards[captureStartIndex].Card!.Value, scorePlayerIndex, hasDoubleEffect);
	}

	private void RecordGardenEelCaptureForSandBite(int playerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.SandAmbush)
			|| playerIndex != 1
			|| _gardenEelSandBiteReady)
		{
			return;
		}

		_gardenEelCaptureCountUntilSandBite++;
		if (_gardenEelCaptureCountUntilSandBite >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeSandBite, 2))
		{
			_gardenEelCaptureCountUntilSandBite = 0;
			_gardenEelSandBiteReady = true;
		}
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

		_seaSlugMissCountUntilSlime++;
		if (_seaSlugMissCountUntilSlime < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.MissesBeforeSlimeCoat, 2))
		{
			return;
		}

		_seaSlugMissCountUntilSlime = 0;
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

		_tableCards[playedTableCardIndex].IsSlimeCoated = true;
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
			|| playerIndex != MainPlayerIndex)
		{
			return;
		}

		_nautilusMainPlayerCaptureCountUntilBomb++;
		if (_nautilusMainPlayerCaptureCountUntilBomb < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeBomb, 3))
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

		int insertionIndex = PickNautilusBombInsertionIndex();
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
		_nautilusMainPlayerCaptureCountUntilBomb = 0;
		RenderTableCards();
		await ResolveBombPairAsync(turnSequenceGeneration);
		RenderTableCards();
	}

	private void RecordNarwhalCaptureForDouble(int playerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.TuskBreak)
			|| playerIndex != 1
			|| _narwhalDoubleReady)
		{
			return;
		}

		_narwhalCaptureCountUntilDouble++;
		if (_narwhalCaptureCountUntilDouble >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeDouble, 2))
		{
			_narwhalCaptureCountUntilDouble = 0;
			_narwhalDoubleReady = true;
		}
	}

}
