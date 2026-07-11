using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task ApplyDragonQueenAndKingAfterEnemyCaptureAsync(
		int playerIndex,
		int removedTableCardCount,
		int turnSequenceGeneration)
	{
		if (!adventure_mode || playerIndex != 1)
		{
			return;
		}

		if (!IsBossKEnemy(playerIndex)
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.DragonQueen)
			&& removedTableCardCount >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.DragonQueenCaptureThreshold, 1))
		{
			_dragonQueenCaptureCountUntilSteal++;
			if (_dragonQueenCaptureCountUntilSteal < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeSteal, 3))
			{
				return;
			}

			_dragonQueenCaptureCountUntilSteal = 0;
			await StealRandomMainPlayerHandCardForEnemyAsync(playerIndex, turnSequenceGeneration);
		}
		else if (IsBossKEnemy(playerIndex) && removedTableCardCount > 0)
		{
			if (_bossKStealTriggersThisMatch >= MaxBossKStealTriggersPerMatch)
			{
				return;
			}

			_bossKCaptureCountUntilSteal++;
			if (_bossKCaptureCountUntilSteal < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeSteal, 3))
			{
				return;
			}

			_bossKCaptureCountUntilSteal = 0;
			_bossKStealTriggersThisMatch++;
			await ApplyBossKRandomStealAfterCaptureAsync(playerIndex, turnSequenceGeneration);
		}
	}

	private async Task ApplyBossKRandomStealAfterCaptureAsync(int playerIndex, int turnSequenceGeneration)
	{
		bool canStealDeck = CanStealMainPlayerTopDeckCard(playerIndex);
		bool canStealHand = CanStealRandomMainPlayerHandCard(playerIndex);
		if (!canStealDeck && !canStealHand)
		{
			return;
		}

		if ((canStealDeck && canStealHand && _rng.RandiRange(0, 1) == 0)
			|| (canStealDeck && !canStealHand))
		{
			await StealMainPlayerTopDeckCardForEnemyAsync(playerIndex, turnSequenceGeneration);
			return;
		}

		await StealRandomMainPlayerHandCardForEnemyAsync(playerIndex, turnSequenceGeneration);
	}

	private bool CanStealRandomMainPlayerHandCard(int enemyPlayerIndex)
	{
		return enemyPlayerIndex >= 0
			&& enemyPlayerIndex < _playerHands.Count
			&& enemyPlayerIndex < _playerItemHands.Count
			&& MainPlayerIndex < _playerHands.Count
			&& MainPlayerIndex < _playerItemHands.Count
			&& GetTotalHandCardCount(MainPlayerIndex) > 0;
	}

	private bool CanStealMainPlayerTopDeckCard(int enemyPlayerIndex)
	{
		return enemyPlayerIndex >= 0
			&& enemyPlayerIndex < _playerDecks.Count
			&& MainPlayerIndex < _playerDecks.Count
			&& _playerDecks[MainPlayerIndex].Count > 0;
	}

	private async Task StealRandomMainPlayerHandCardForEnemyAsync(int enemyPlayerIndex, int turnSequenceGeneration)
	{
		if (enemyPlayerIndex < 0
			|| enemyPlayerIndex >= _playerHands.Count
			|| enemyPlayerIndex >= _playerItemHands.Count
			|| MainPlayerIndex >= _playerHands.Count
			|| MainPlayerIndex >= _playerItemHands.Count)
		{
			return;
		}

		int targetHandCount = GetTotalHandCardCount(MainPlayerIndex);
		if (targetHandCount == 0)
		{
			return;
		}

		int targetHandIndex = _rng.RandiRange(0, targetHandCount - 1);
		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(enemyPlayerIndex, turnSequenceGeneration, AdventureEnemyAbilities.DragonQueen);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		PlayCardResult result = await ResolveQueenEffectAsync(enemyPlayerIndex, MainPlayerIndex, targetHandIndex, turnSequenceGeneration);
		if (result.Played)
		{
			RenderPlayerHands();
		}
	}

	private async Task StealMainPlayerTopDeckCardForEnemyAsync(int enemyPlayerIndex, int turnSequenceGeneration)
	{
		if (enemyPlayerIndex < 0
			|| enemyPlayerIndex >= _playerDecks.Count
			|| MainPlayerIndex >= _playerDecks.Count
			|| _playerDecks[MainPlayerIndex].Count == 0)
		{
			return;
		}

		Vector2 startGlobalPosition = GetDrawCardStartGlobalPosition(MainPlayerIndex);
		Vector2 targetGlobalPosition = GetDrawCardStartGlobalPosition(enemyPlayerIndex);
		await PlayBossKRoyalSeizureLanceAttackAsync(
			enemyPlayerIndex,
			GetPlayerCardPileGlobalCenter(MainPlayerIndex),
			turnSequenceGeneration,
			null);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		await PlayStolenDeckCardFlightAsync(
			startGlobalPosition,
			targetGlobalPosition,
			GetCardPileRotationDegreesForPlayer(MainPlayerIndex),
			GetCardPileRotationDegreesForPlayer(enemyPlayerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		PlayingCard stolenCard = _playerDecks[MainPlayerIndex][0];
		_playerDecks[MainPlayerIndex].RemoveAt(0);
		AddCardToPlayerDeck(enemyPlayerIndex, stolenCard);
		PlayPlayerAvatarAction(MainPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		RenderPlayerCardPiles();
	}

	private async Task ResolveLeafySeadragonBladeBreakAsync(
		int playerIndex,
		PlayCardResult result,
		int turnSequenceGeneration)
	{
		if (!ShouldResolveLeafySeadragonBladeBreak(playerIndex, result))
		{
			UpdateLeafySeadragonBladeBreakCooldown(playerIndex);
			return;
		}

		int targetTableCardIndex = PickLeafySeadragonBladeBreakTargetIndex();
		if (targetTableCardIndex < 0)
		{
			UpdateLeafySeadragonBladeBreakCooldown(playerIndex);
			return;
		}

		TableCard targetTableCard = _tableCards[targetTableCardIndex];
		if (IsBossKEnemy(playerIndex))
		{
			await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.LeafyMirage);
			if (!IsLeafySeadragonBladeBreakTargetCurrent(
				turnSequenceGeneration,
				targetTableCardIndex,
				targetTableCard))
			{
				return;
			}
		}

		await PlayLeafySeadragonBladeProjectilesAsync(playerIndex, targetTableCardIndex, turnSequenceGeneration);
		if (!IsLeafySeadragonBladeBreakTargetCurrent(
			turnSequenceGeneration,
			targetTableCardIndex,
			targetTableCard))
		{
			return;
		}

		targetTableCard.IsHiddenDuringCapture = true;
		RenderTableCards();
		if (!TryGetTableCardPosition(targetTableCardIndex, out Vector2 targetCardPosition))
		{
			return;
		}

		await PlayBombPairDestroyCardAsync(targetTableCardIndex, targetCardPosition);
		if (!IsLeafySeadragonBladeBreakTargetCurrent(
			turnSequenceGeneration,
			targetTableCardIndex,
			targetTableCard))
		{
			return;
		}

		PlayingCard destroyedCard = targetTableCard.Card!.Value;
		int damagePoints = Mathf.CeilToInt(GetCardScoreValue(destroyedCard) / 2.0f);
		ClearAdventureTraceHuntMarkerIfTableCardRemoved(targetTableCard);
		_tableCards.RemoveAt(targetTableCardIndex);
		RefreshGardenEelBuriedCardIndex();
		_leafySeadragonBladeBreakCooldownTurns = GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.BladeBreakCooldownTurns, 1);
		MarkBossKActiveControlUsedIfNeeded(playerIndex);
		RenderTableCards();

		if (damagePoints <= 0)
		{
			return;
		}

		int damageTargetPlayerIndex = GetAdventureDamageTargetPlayerIndex(playerIndex);
		await PlayAdventureAvatarAttackAsync(
			playerIndex,
			damageTargetPlayerIndex,
			turnSequenceGeneration,
			() => AddPlayerScore(playerIndex, damagePoints));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		CheckForWinner();
	}

	private bool IsLeafySeadragonBladeBreakTargetCurrent(
		int turnSequenceGeneration,
		int targetTableCardIndex,
		TableCard targetTableCard)
	{
		return turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver()
			&& targetTableCardIndex >= 0
			&& targetTableCardIndex < _tableCards.Count
			&& ReferenceEquals(_tableCards[targetTableCardIndex], targetTableCard);
	}

	private int RemoveLeafySeadragonBladeBreakTableCard(int tableCardIndex)
	{
		TableCard destroyedTableCard = _tableCards[tableCardIndex];
		PlayingCard destroyedCard = destroyedTableCard.Card!.Value;
		int damagePoints = Mathf.CeilToInt(GetCardScoreValue(destroyedCard) / 2.0f);
		ClearAdventureTraceHuntMarkerIfTableCardRemoved(destroyedTableCard);
		_tableCards.RemoveAt(tableCardIndex);
		RefreshGardenEelBuriedCardIndex();
		return damagePoints;
	}

	private bool ShouldResolveLeafySeadragonBladeBreak(int playerIndex, PlayCardResult result)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.LeafyMirage)
			&& playerIndex == 1
			&& !result.Captured
			&& _leafySeadragonBladeBreakCooldownTurns <= 0
			&& _tableCards.Count >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.BladeBreakTableCardThreshold, 5)
			&& PickLeafySeadragonBladeBreakTargetIndex() >= 0
			&& (!IsBossKEnemy(playerIndex) || BossKCanUseActiveControl(playerIndex));
	}

	private void UpdateLeafySeadragonBladeBreakCooldown(int playerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.LeafyMirage)
			|| playerIndex != 1
			|| _leafySeadragonBladeBreakCooldownTurns <= 0)
		{
			return;
		}

		_leafySeadragonBladeBreakCooldownTurns--;
	}

	private int PickLeafySeadragonBladeBreakTargetIndex()
	{
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.Card.HasValue && !IsFixedDuringTableShuffle(tableCard))
			{
				return tableCardIndex;
			}
		}

		return -1;
	}

}
