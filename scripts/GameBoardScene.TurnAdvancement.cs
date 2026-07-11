using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private void SetCurrentTurnPlayerIndex(int nextTurnPlayerIndex)
	{
		int previousTurnPlayerIndex = _currentTurnPlayerIndex;
		if (NetworkAiFallbackRules.ShouldClearBlockedItemForTurnChange(_currentTurnPlayerIndex, nextTurnPlayerIndex))
		{
			_networkAiBlockedItem = NetworkAiBlockedItemState.None;
		}

			_currentTurnPlayerIndex = nextTurnPlayerIndex;
		if (nextTurnPlayerIndex == MainPlayerIndex && previousTurnPlayerIndex != MainPlayerIndex)
		{
			_adventureMainPlayerSuccessfulCapturesThisTurn = 0;
			RefreshAdventureHuntOrderMarkForMainPlayerTurn();
		}
	}

	private async Task AdvanceTurnAfterPlayedResultAsync(int playerIndex, PlayCardResult result)
	{
		if (!result.CompletesTurn)
		{
			return;
		}

		ClearJellyfishBubbleIfMainPlayerTurnCompleted(playerIndex, result);

		SetCurrentTurnPlayerIndex(_turnAdvancementService.ApplyPlayedResult(
			_state,
			playerIndex,
			result.Captured,
			GetNextTurnPlayerIndex(playerIndex)));
		PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
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
		BeginNetworkAiProxyTurnIfNeeded();
	}

	private async Task RefillCurrentTurnPlayerBaseHandAsync(int turnSequenceGeneration)
	{
		await RefillPlayerBaseHandAsync(_currentTurnPlayerIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		await ApplyAdventureMainPlayerTurnStartEffectsAsync(_currentTurnPlayerIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		await TryResolveAdventurePendingExtraBaseCardActionForAiAsync(turnSequenceGeneration);
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

		return NetworkAiFallbackRules.HasPlayableAiTurnAction(
			_playerHands[playerIndex].Count,
			CountPlayableItemIndexes(playerIndex));
	}

	private bool HasPlayableItemCard(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		if (playerIndex == MainPlayerIndex && _adventureMainPlayerPendingExtraBaseCardAction)
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
			if (ShouldSkipNetworkAiBlockedItem(playerIndex, itemIndex, itemHand[itemIndex].Id))
			{
				continue;
			}

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
			if (ShouldSkipNetworkAiBlockedItem(playerIndex, itemIndex, itemHand[itemIndex].Id))
			{
				continue;
			}

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

	private bool ShouldSkipNetworkAiBlockedItem(int playerIndex, int itemHandIndex, string itemCardId)
	{
		return NetworkAiFallbackRules.ShouldSkipBlockedItem(
			_networkAiBlockedItem,
			playerIndex,
			itemCardId);
	}

	private bool CanPlayItemCard(int playerIndex, string itemCardId)
	{
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			|| !definition.EnabledInGame
			|| !definition.PlayableFromHand)
		{
			return false;
		}

		if (string.Equals(
				definition.EffectType,
				CardEffectTypes.DrawCard,
				StringComparison.Ordinal)
			&& !CanFulfillDrawItemCardEffect(playerIndex, definition))
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

	private bool CanFulfillDrawItemCardEffect(
		int playerIndex,
		ItemCardDefinition definition)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerDecks.Count
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		int drawCount = definition.EffectValues.GetInt(
			CardEffectValueKeys.Count,
			1);
		return ItemCardEffectSupport.CanFulfillDrawCardEffect(
			_playerDecks[playerIndex].Count,
			GetTotalHandCardCount(playerIndex),
			MaxTotalHandCards,
			drawCount);
	}

}
