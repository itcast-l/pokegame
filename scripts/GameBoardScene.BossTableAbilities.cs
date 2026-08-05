using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deckland.Rules;
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
			int nextStealProgress = CurrentMatchFlowState.StealThresholdProgress == int.MaxValue
				? int.MaxValue
				: CurrentMatchFlowState.StealThresholdProgress + 1;
			bool triggerSteal = nextStealProgress >= GetAdventureEnemyAbilityValue(
				AdventureEnemyAbilityValueKeys.CapturesBeforeSteal,
				3);
			TryUpdateMatchFlowState(state => state with
			{
				StealThresholdProgress = triggerSteal ? 0 : nextStealProgress,
			});
			if (!triggerSteal)
			{
				return;
			}
			await StealRandomMainPlayerHandCardForEnemyAsync(playerIndex, turnSequenceGeneration);
		}
		else if (IsBossKEnemy(playerIndex) && removedTableCardCount > 0)
		{
			if (CurrentMatchFlowState.LimitedStealActivationsThisMatch >= MaxBossKStealTriggersPerMatch)
			{
				return;
			}

			int nextLimitedStealProgress = CurrentMatchFlowState.LimitedStealThresholdProgress == int.MaxValue
				? int.MaxValue
				: CurrentMatchFlowState.LimitedStealThresholdProgress + 1;
			bool triggerLimitedSteal = nextLimitedStealProgress >= GetAdventureEnemyAbilityValue(
				AdventureEnemyAbilityValueKeys.CapturesBeforeSteal,
				3);
			TryUpdateMatchFlowState(state => state with
			{
				LimitedStealThresholdProgress = triggerLimitedSteal
					? 0
					: nextLimitedStealProgress,
				LimitedStealActivationsThisMatch = triggerLimitedSteal
					? state.LimitedStealActivationsThisMatch == int.MaxValue
						? int.MaxValue
						: state.LimitedStealActivationsThisMatch + 1
					: state.LimitedStealActivationsThisMatch,
			});
			if (!triggerLimitedSteal)
			{
				return;
			}
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

		bool chooseDeck = canStealDeck && !canStealHand;
		if (canStealDeck && canStealHand)
		{
			if (!TrySubmitChooseRandomIndexRuleCommand(
				playerIndex,
				minimumInclusive: 0,
				maximumInclusive: 1,
				turnSequenceGeneration,
				out int stealChoice))
			{
				return;
			}
			chooseDeck = stealChoice == 0;
		}

		if (chooseDeck)
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

		if (!TrySubmitChooseRandomHandDisplayIndexRuleCommand(
			enemyPlayerIndex,
			MainPlayerIndex,
			ImmutableArray<int>.Empty,
			turnSequenceGeneration,
			out int targetHandIndex))
		{
			return;
		}
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
		if (!TrySubmitTransferTopDeckCardRuleCommand(
			enemyPlayerIndex,
			MainPlayerIndex,
			stolenCard.InstanceId,
			ShouldPreserveCardEffectsWhenEnteringDeck(),
			turnSequenceGeneration,
			out _))
		{
			return;
		}
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

		bool presentationHideAdded =
			_presentationHiddenTableCardRuntimeInstanceIds.Add(targetTableCard.InstanceId);
		int damagePoints;
		try
		{
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

			damagePoints = MoveLeafySeadragonBladeBreakTableCardToDeck(
				playerIndex,
				targetTableCardIndex,
				turnSequenceGeneration);
			int cooldownTurns = GetAdventureEnemyAbilityValue(
				AdventureEnemyAbilityValueKeys.BladeBreakCooldownTurns,
				1);
			TryUpdateMatchFlowState(state => state with
			{
				DestructiveEffectCooldownTurns = cooldownTurns,
			});
			MarkBossKActiveControlUsedIfNeeded(playerIndex);
			RenderTableCards();
		}
		finally
		{
			if (presentationHideAdded)
			{
				_presentationHiddenTableCardRuntimeInstanceIds.Remove(targetTableCard.InstanceId);
				if (IsInsideTree())
				{
					RenderTableCards();
				}
			}
		}

		if (damagePoints <= 0)
		{
			return;
		}

		int damageTargetPlayerIndex = GetAdventureDamageTargetPlayerIndex(playerIndex);
		AdventureDirectDamageSceneCommit damageCommit =
			await CommitAdventureDirectDamageAsync(damagePoints);
		if (!damageCommit.Succeeded)
		{
			return;
		}
		await PlayAdventureAvatarAttackAsync(
			playerIndex,
			damageTargetPlayerIndex,
			turnSequenceGeneration,
			() => ApplyCommittedAdventureDirectDamage(playerIndex, damageCommit.CommitResult));
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

	private int MoveLeafySeadragonBladeBreakTableCardToDeck(
		int playerIndex,
		int tableCardIndex,
		int turnSequenceGeneration)
	{
		TableCard destroyedTableCard = _tableCards[tableCardIndex];
		PlayingCard destroyedCard = destroyedTableCard.Card!.Value;
		int damagePoints = Mathf.CeilToInt(GetCardScoreValue(destroyedCard) / 2.0f);
		if (!TrySubmitMoveTableCardToDeckRuleCommand(
			playerIndex,
			tableCardIndex,
			destroyedTableCard.InstanceId,
			playerIndex,
			ShouldPreserveCardEffectsWhenEnteringDeck(),
			turnSequenceGeneration,
			out _))
		{
			return 0;
		}
		ClearAdventureTraceHuntMarkerIfTableCardRemoved(destroyedTableCard);
		RenderPlayerCardPiles();
		return damagePoints;
	}

	private bool ShouldResolveLeafySeadragonBladeBreak(int playerIndex, PlayCardResult result)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.LeafyMirage)
			&& playerIndex == 1
			&& !result.Captured
			&& CurrentMatchFlowState.DestructiveEffectCooldownTurns <= 0
			&& _tableCards.Count >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.BladeBreakTableCardThreshold, 5)
			&& PickLeafySeadragonBladeBreakTargetIndex() >= 0
			&& (!IsBossKEnemy(playerIndex) || BossKCanUseActiveControl(playerIndex));
	}

	private void UpdateLeafySeadragonBladeBreakCooldown(int playerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.LeafyMirage)
			|| playerIndex != 1
			|| CurrentMatchFlowState.DestructiveEffectCooldownTurns <= 0)
		{
			return;
		}

		TryUpdateMatchFlowState(state => state with
		{
			DestructiveEffectCooldownTurns = state.DestructiveEffectCooldownTurns - 1,
		});
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
