using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task AdvanceTurnAfterPlayedResultAsync(int playerIndex, PlayCardResult result)
	{
		if (!result.CompletesTurn)
		{
			return;
		}

		ClearJellyfishBubbleIfMainPlayerTurnCompleted(playerIndex, result);

		if (_questionCardRefreshAnchorPlayerIndex < 0)
		{
			_completedTurnsSinceQuestionCardRefresh = 0;
			_questionCardRefreshAnchorPlayerIndex = playerIndex;
		}

		_currentTurnPlayerIndex = result.Captured ? playerIndex : GetNextTurnPlayerIndex(playerIndex);
		await ResolveAdventureEnemyTurnEndAbilityAsync(playerIndex, result, _turnSequenceGeneration);
		if (IsGameOver())
		{
			return;
		}

		ResetMantaRayTideShiftTurnStateIfNeeded(playerIndex);
		ResetJellyfishBubbleTurnStateIfNeeded(playerIndex);
		if (!result.Captured)
		{
			int refreshedQuestionCardIndex = AdvanceQuestionCardRefreshTurnCounter(_currentTurnPlayerIndex);
			if (refreshedQuestionCardIndex >= 0 && refreshedQuestionCardIndex < _tableCards.Count)
			{
				_tableCards[refreshedQuestionCardIndex].IsHiddenDuringCapture = true;
			}

			RenderTableCards();
			if (refreshedQuestionCardIndex >= 0 && refreshedQuestionCardIndex < _tableCards.Count)
			{
				_tableCards[refreshedQuestionCardIndex].IsHiddenDuringCapture = false;
				await PlayQuestionCardRefreshAnimationAsync(refreshedQuestionCardIndex);
				RenderTableCards();
			}
		}

		await RefillCurrentTurnPlayerBaseHandAsync(_turnSequenceGeneration);
	}

	private async Task RefillCurrentTurnPlayerBaseHandAsync(int turnSequenceGeneration)
	{
		await RefillPlayerBaseHandAsync(_currentTurnPlayerIndex, turnSequenceGeneration);
	}

	private async Task RefillPlayerBaseHandAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0
			|| playerIndex >= player_count
			|| playerIndex >= _playerDecks.Count
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count)
		{
			return;
		}

		while (turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver()
			&& _playerHands[playerIndex].Count < StartingHandCards
			&& GetTotalHandCardCount(playerIndex) < MaxTotalHandCards
			&& _playerDecks[playerIndex].Count > 0)
		{
			DrawnHandCard? drawnCard = DrawReplacementCard(playerIndex);
			if (!drawnCard.HasValue)
			{
				break;
			}

			await PlayReplacementDrawAnimationAsync(playerIndex, drawnCard, turnSequenceGeneration);
		}

		RenderPlayerCardPiles();
		RenderPlayerHands();
	}

	private bool PlayerHasPlayableHandCards(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		return _playerHands[playerIndex].Count > 0 || HasPlayableItemCard(playerIndex);
	}

	private bool HasPlayableItemCard(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			if (CanPlayItemCard(playerIndex, itemHand[itemIndex].Id))
			{
				return true;
			}
		}

		return false;
	}

	private int CountPlayableItemIndexes(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return 0;
		}

		int playableCount = 0;
		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			if (CanPlayItemCard(playerIndex, itemHand[itemIndex].Id))
			{
				playableCount++;
			}
		}

		return playableCount;
	}

	private bool TryGetPlayableItemIndexAt(int playerIndex, int playableOrdinal, out int itemHandIndex)
	{
		itemHandIndex = -1;
		if (playableOrdinal < 0 || playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		int currentOrdinal = 0;
		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			if (!CanPlayItemCard(playerIndex, itemHand[itemIndex].Id))
			{
				continue;
			}

			if (currentOrdinal == playableOrdinal)
			{
				itemHandIndex = itemIndex;
				return true;
			}

			currentOrdinal++;
		}

		return false;
	}

	private bool CanPlayItemCard(int playerIndex, string itemCardId)
	{
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			|| !definition.EnabledInGame
			|| !definition.PlayableFromHand)
		{
			return false;
		}

		if (_itemPlayPresentationValidators.TryGetValue(definition.PlayPresentation, out CanPlayItemCardPresentation? presentationValidator)
			&& !presentationValidator(playerIndex, definition))
		{
			return false;
		}

		if (RequiresPlayerTarget(itemCardId) && !HasPlayableTargetForItemCard(itemCardId, playerIndex))
		{
			return false;
		}

		if (definition.EffectValues.GetString(CardEffectValueKeys.Target) == CardEffectTargets.PlayerDeck
			&& (playerIndex < 0 || playerIndex >= _playerDecks.Count || _playerDecks[playerIndex].Count == 0))
		{
			return false;
		}

		if (IsAttachmentItemCard(itemCardId) && !PlayerHasAttachableMagnetCard(playerIndex, itemCardId))
		{
			return false;
		}

		return true;
	}

}
