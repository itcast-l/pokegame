using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> PlayRandomHandEntryForPlayerAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		int playableItemCount = CountPlayableItemIndexes(playerIndex);
		if (TryPickAiCaptureHandIndex(playerIndex, out int captureHandIndex))
		{
			return await PlayCardFromHandAsync(playerIndex, captureHandIndex, turnSequenceGeneration);
		}

		int playableCount = _playerHands[playerIndex].Count + playableItemCount;
		if (playableCount == 0)
		{
			return new PlayCardResult(false, false);
		}

		int selectedIndex = _rng.RandiRange(0, playableCount - 1);
		PlayCardResult result;
		if (selectedIndex < _playerHands[playerIndex].Count)
		{
			result = await PlayCardFromHandAsync(playerIndex, selectedIndex, turnSequenceGeneration);
			return result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver()
				? result
				: await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
		}

		if (!TryGetPlayableItemIndexAt(playerIndex, selectedIndex - _playerHands[playerIndex].Count, out int itemHandIndex))
		{
			return new PlayCardResult(false, false);
		}

		result = await PlayItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		return result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver()
			? result
			: await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
	}

	private async Task<PlayCardResult> PlayRandomPlayableHandEntryWithoutCapturePriorityAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		int playableItemCount = CountPlayableItemIndexes(playerIndex);
		int playableCount = _playerHands[playerIndex].Count + playableItemCount;
		if (playableCount == 0)
		{
			return new PlayCardResult(false, false);
		}

		int selectedIndex = _rng.RandiRange(0, playableCount - 1);
		PlayCardResult result;
		if (selectedIndex < _playerHands[playerIndex].Count)
		{
			result = await PlayCardFromHandAsync(playerIndex, selectedIndex, turnSequenceGeneration);
			return result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver()
				? result
				: await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
		}

		if (!TryGetPlayableItemIndexAt(playerIndex, selectedIndex - _playerHands[playerIndex].Count, out int itemHandIndex))
		{
			return new PlayCardResult(false, false);
		}

		result = await PlayItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		return result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver()
			? result
			: await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
	}

	private async Task<PlayCardResult> PlayFirstAvailableHandEntryForPlayerAsync(int playerIndex, int turnSequenceGeneration)
	{
		for (int handIndex = 0; handIndex < _playerHands[playerIndex].Count; handIndex++)
		{
			PlayCardResult result = await PlayCardFromHandAsync(playerIndex, handIndex, turnSequenceGeneration);
			if (result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return result;
			}
		}

		int playableItemCount = CountPlayableItemIndexes(playerIndex);
		for (int playableItemOrdinal = 0; playableItemOrdinal < playableItemCount; playableItemOrdinal++)
		{
			if (!TryGetPlayableItemIndexAt(playerIndex, playableItemOrdinal, out int itemHandIndex))
			{
				continue;
			}

			PlayCardResult result = await PlayItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
			if (result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return result;
			}
		}

		return new PlayCardResult(false, false);
	}

	private bool TryPickAiCaptureHandIndex(int playerIndex, out int handIndex)
	{
		handIndex = -1;
		if (!adventure_mode || !IsAdventureEnemyPlayer(playerIndex))
		{
			return TryPickBestCaptureHandIndex(playerIndex, out handIndex);
		}

		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return false;
		}

		int stageNumber = CharacterAvatarCatalog.GetAdventureStageNumber(adventure_enemy_avatar_id);
		EnemyAiRules.AdventureProfile profile = EnemyAiRules.GetAdventureProfile(stageNumber);
		int royalCaptureThreshold = IsBossKEnemy(playerIndex)
			? GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5)
			: int.MaxValue;
		return EnemyAiRules.TryPickCaptureCardIndex(
			ToCaptureScoringTableCards(),
			ToPotentialCaptureCards(playerIndex, _playerHands[playerIndex]),
			BuildCaptureScoringContext(playerIndex),
			profile,
			royalCaptureThreshold,
			_rng.RandiRange(0, 99),
			_rng.RandiRange(0, 9999),
			out handIndex);
	}

	private bool TryPickBestCaptureHandIndex(int playerIndex, out int handIndex)
	{
		handIndex = -1;
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return false;
		}

		int royalCaptureThreshold = IsBossKEnemy(playerIndex)
			? GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5)
			: int.MaxValue;
		return PotentialCaptureRules.TryPickBestCaptureCardIndex(
			ToCaptureScoringTableCards(),
			ToPotentialCaptureCards(playerIndex, _playerHands[playerIndex]),
			BuildCaptureScoringContext(playerIndex),
			royalCaptureThreshold,
			out handIndex);
	}

	private int GetPotentialNormalCaptureCount(PlayingCard card)
	{
		return PotentialCaptureRules.GetPotentialNormalCaptureCount(
			ToCaptureScoringTableCards(),
			ToPotentialCaptureCard(card));
	}

	private int GetPotentialNormalCapturePoints(int playerIndex, PlayingCard card)
	{
		return PotentialCaptureRules.GetPotentialNormalCapturePoints(
			ToCaptureScoringTableCards(),
			ToPotentialCaptureCard(card, ShouldApplyNarwhalDoubleToPlayedCard(playerIndex, card)),
			BuildCaptureScoringContext(playerIndex));
	}

	private bool HasPotentialCapturedMiddleTableCards(int captureStartIndex)
	{
		return PotentialCaptureRules.HasPotentialCapturedMiddleTableCards(
			ToCaptureScoringTableCards(),
			captureStartIndex);
	}

	private async Task ResolveAdventureEnemyTurnStartAbilityAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (IsAdventureEnemyWithMantaRayTideShift(playerIndex))
		{
			if (_mantaRayTideShiftCheckedThisTurn)
			{
				return;
			}

			_mantaRayTideShiftCheckedThisTurn = true;
		}

		if (!ShouldResolveMantaRayTideShift(playerIndex))
		{
			return;
		}

		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.TideShift);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !ShouldResolveMantaRayTideShift(playerIndex))
		{
			return;
		}

		TableCard tideShiftedCard = _tableCards[0];
		Texture2D? triggerTexture = GetVisibleTableCardTexture(tideShiftedCard);
		if (triggerTexture != null)
		{
			if (TryGetTableCardPosition(0, out Vector2 cardPosition))
			{
				await PlaySpecialCardTriggerEffectAsync(triggerTexture, _centerArena.GlobalPosition + cardPosition, 0);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !ShouldResolveMantaRayTideShift(playerIndex))
				{
					return;
				}
			}
		}

		await PlayMantaRayTideShiftTableMoveAnimationAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !ShouldResolveMantaRayTideShift(playerIndex))
		{
			return;
		}

		_tableCards.RemoveAt(0);
		_tableCards.Add(tideShiftedCard);
		MarkBossKActiveControlUsedIfNeeded(playerIndex);
		RenderTableCards();
	}

	private bool ShouldResolveMantaRayTideShift(int playerIndex)
	{
		return adventure_mode
			&& playerIndex == 1
			&& _mantaRayTideShiftCheckedThisTurn
			&& IsMantaRayTideShiftEnemyTurn(playerIndex)
			&& _tableCards.Count >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.TideShiftMinTableCards, 4)
			&& (!IsBossKEnemy(playerIndex) || ShouldResolveBossKTideShift(playerIndex));
	}

	private bool ShouldResolveBossKTideShift(int playerIndex)
	{
		if (!BossKCanUseActiveControl(playerIndex) || ShouldResolveDragonPrinceJack(playerIndex))
		{
			return false;
		}

		int bossCaptureBefore = GetBestPotentialNormalCaptureCount(playerIndex, _tableCards);
		int bossCaptureAfter = GetBestPotentialNormalCaptureCountAfterTideShift(playerIndex);
		if (bossCaptureAfter > bossCaptureBefore
			&& bossCaptureAfter >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5))
		{
			return true;
		}

		int playerCaptureBefore = GetBestPotentialNormalCaptureCount(MainPlayerIndex, _tableCards);
		int playerCaptureAfter = GetBestPotentialNormalCaptureCountAfterTideShift(MainPlayerIndex);
		return playerCaptureBefore >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5)
			&& playerCaptureAfter < playerCaptureBefore;
	}

	private int GetBestPotentialNormalCaptureCountAfterTideShift(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || _tableCards.Count == 0)
		{
			return 0;
		}

		return PotentialCaptureRules.GetBestPotentialNormalCaptureCount(
			ToTideShiftedCaptureScoringTableCards(),
			ToPotentialCaptureCards(_playerHands[playerIndex]));
	}

	private int GetBestPotentialNormalCaptureCount(int playerIndex, IReadOnlyList<TableCard> tableCards)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || tableCards.Count == 0)
		{
			return 0;
		}

		return PotentialCaptureRules.GetBestPotentialNormalCaptureCount(
			ToCaptureScoringTableCards(tableCards),
			ToPotentialCaptureCards(_playerHands[playerIndex]));
	}

	private PotentialCaptureRules.Card[] ToPotentialCaptureCards(int playerIndex, IReadOnlyList<PlayingCard> cards)
	{
		var potentialCards = new PotentialCaptureRules.Card[cards.Count];
		for (int index = 0; index < cards.Count; index++)
		{
			PlayingCard card = cards[index];
			potentialCards[index] = ToPotentialCaptureCard(card, ShouldApplyNarwhalDoubleToPlayedCard(playerIndex, card));
		}

		return potentialCards;
	}

	private PotentialCaptureRules.Card[] ToPotentialCaptureCards(IReadOnlyList<PlayingCard> cards)
	{
		var potentialCards = new PotentialCaptureRules.Card[cards.Count];
		for (int index = 0; index < cards.Count; index++)
		{
			potentialCards[index] = ToPotentialCaptureCard(cards[index]);
		}

		return potentialCards;
	}

	private PotentialCaptureRules.Card ToPotentialCaptureCard(PlayingCard card, bool hasDoubleEffect = false)
	{
		return new PotentialCaptureRules.Card(
			card.Rank,
			ShouldPlayedCardCollectTable(card),
			hasDoubleEffect);
	}

	private CaptureScoringRules.TableCard[] ToTideShiftedCaptureScoringTableCards()
	{
		var tableCards = new CaptureScoringRules.TableCard[_tableCards.Count];
		for (int shiftedIndex = 0; shiftedIndex < _tableCards.Count; shiftedIndex++)
		{
			int tableCardIndex = shiftedIndex == _tableCards.Count - 1 ? 0 : shiftedIndex + 1;
			tableCards[shiftedIndex] = ToCaptureScoringTableCard(_tableCards[tableCardIndex]);
		}

		return tableCards;
	}

	private bool IsMantaRayTideShiftEnemyTurn(int playerIndex)
	{
		return playerIndex == 1
			&& IsAdventureEnemyWithMantaRayTideShift(playerIndex)
			&& _currentTurnPlayerIndex == playerIndex;
	}

	private bool IsAdventureEnemyWithMantaRayTideShift(int playerIndex)
	{
		return playerIndex == 1
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.TideShift);
	}

	private void ResetMantaRayTideShiftTurnStateIfNeeded(int previousPlayerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.TideShift)
			|| previousPlayerIndex == 1
			|| _currentTurnPlayerIndex != 1)
		{
			return;
		}

		_mantaRayTideShiftCheckedThisTurn = false;
		_bossKActiveControlUsedThisTurn = false;
	}

	private int AddBombCardToTable(ItemCard bombCard, int ownerPlayerIndex)
	{
		_tableCards.Add(new TableCard(null, false, bombCard, ownerPlayerIndex));
		return _tableCards.Count - 1;
	}

	private int InsertBombCardIntoTable(ItemCard bombCard, int ownerPlayerIndex, int tableCardIndex)
	{
		int clampedIndex = Mathf.Clamp(tableCardIndex, 0, _tableCards.Count);
		_tableCards.Insert(clampedIndex, new TableCard(null, false, bombCard, ownerPlayerIndex));
		return clampedIndex;
	}

}
