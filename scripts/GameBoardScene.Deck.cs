using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private void DealCardsToPlayers(bool playOpeningDealAnimation = false, bool playOpeningIntroAnimation = false)
	{
		if (_playerCardPiles.Count != MaxPlayers || _playerHandAreas.Count != MaxPlayers)
		{
			return;
		}

		List<PlayingCard> deck = BuildDeckForPlayerCount(player_count);
		ResetAdventureTutorialState();

		_playerDecks.Clear();
		_playerHands.Clear();
		_playerItemHands.Clear();
		_tableCards.Clear();
		_openingVisibleHandCardCounts.Clear();
		_openingVisiblePileCardCounts.Clear();
		AdvanceTurnSequenceGeneration();
		_currentTurnPlayerIndex = MainPlayerIndex;
		QuestionCardRefreshState questionCardRefreshState = TurnProgressRules.ResetQuestionCardRefreshTracking();
		_completedTurnsSinceQuestionCardRefresh = questionCardRefreshState.CompletedTurnsSinceQuestionCardRefresh;
		_questionCardRefreshAnchorPlayerIndex = questionCardRefreshState.QuestionCardRefreshAnchorPlayerIndex;
		_pendingItemAttachmentRewards.Clear();
		_hiddenHandDisplaySlots.Clear();
		_mantaRayTideShiftCheckedThisTurn = false;
		_jellyfishBubbleTriggeredThisTurn = false;
		_jellyfishDisabledMainPlayerHandSlotIndex = -1;
		_gardenEelCaptureCountUntilSandBite = 0;
		_gardenEelSandBiteReady = false;
		_gardenEelBuriedTableCardIndex = -1;
		_nautilusMainPlayerCaptureCountUntilBomb = 0;
		_nautilusBombInsertionsThisMatch = 0;
		_narwhalCaptureCountUntilDouble = 0;
		_narwhalDoubleReady = false;
		_bossKDoubleCollectTriggersThisMatch = 0;
		_leafySeadragonBladeBreakCooldownTurns = 0;
		_seaSlugMissCountUntilSlime = 0;
		_dragonPrinceJackUsed = false;
		_dragonQueenCaptureCountUntilSteal = 0;
		_bossKCaptureCountUntilSteal = 0;
		_bossKStealTriggersThisMatch = 0;
		_bossKActiveControlUsedThisTurn = false;
		_cardEventBus.Clear();
			DisposeActiveSelectionOverlay();
			ResetAdventureUpgradeBoardState();
		ClearPendingItemSelectionState();
		InvalidateRenderCaches();
		_isResolvingMagnetAttachmentSelection = false;
		_isResolvingTurnSequence = false;
		_isProcessingCardPlay = false;
		_isOpeningDealAnimationPlaying = playOpeningDealAnimation;
		_winnerPlayerIndex = -1;
		ResetPlayerScores();
		if (_defeatFinisherRoot != null && GodotObject.IsInstanceValid(_defeatFinisherRoot))
		{
			_defeatFinisherRoot.Visible = false;
		}
		if (_defeatFinisherTexture != null && GodotObject.IsInstanceValid(_defeatFinisherTexture))
		{
			_defeatFinisherTexture.Texture = null;
			_defeatFinisherTexture.Modulate = Colors.White;
			_defeatFinisherTexture.Scale = Vector2.One;
		}
		if (_winnerLabel != null)
		{
			HideWinnerBanner();
		}
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
			_playerItemHands.Add(new List<ItemCard>());
			_openingVisibleHandCardCounts.Add(0);
			_openingVisiblePileCardCounts.Add(0);
		}

		if (TrySetupAdventureTutorialDeal())
		{
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
			StartAdventureTutorial();
			return;
		}

		if (TrySetupDebugRecordingDeal())
		{
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
			return;
		}

		DealDeckToPlayerDecks(deck);

		DrawStartingHands();
		ApplyDebugMainPlayerOneCardHand();
		ApplyDebugOpponentOneCardHand();
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		if (playOpeningDealAnimation)
		{
			if (playOpeningIntroAnimation)
			{
				_ = PlayOpeningIntroThenDealSequenceAsync(_turnSequenceGeneration);
			}
			else
			{
				_ = PlayOpeningDealAnimationAsync(_turnSequenceGeneration);
			}
		}
	}

	private void DealDeckToPlayerDecks(List<PlayingCard> deck)
	{
		if (TryDealEqualRankDecks(deck, player_count))
		{
			ShufflePlayerDecks();
			return;
		}

		Shuffle(deck);
		Cut(deck);
		for (int cardIndex = 0; cardIndex < deck.Count; cardIndex++)
		{
			int playerIndex = cardIndex % player_count;
			AddCardToPlayerDeck(playerIndex, deck[cardIndex]);
		}
	}

	private bool TryDealEqualRankDecks(List<PlayingCard> deck, int playerCount)
	{
		if (playerCount <= 0)
		{
			return false;
		}

		var cardsByRank = new Dictionary<string, List<PlayingCard>>();
		foreach (PlayingCard card in deck)
		{
			if (!cardsByRank.TryGetValue(card.Rank, out List<PlayingCard>? rankCards))
			{
				rankCards = new List<PlayingCard>();
				cardsByRank[card.Rank] = rankCards;
			}

			rankCards.Add(card);
		}

		var remainingCards = new List<PlayingCard>();
		foreach (List<PlayingCard> rankCards in cardsByRank.Values)
		{
			Shuffle(rankCards);
			int cardsPerPlayer = rankCards.Count / playerCount;
			for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
			{
				for (int cardOffset = 0; cardOffset < cardsPerPlayer; cardOffset++)
				{
					int rankCardIndex = playerIndex * cardsPerPlayer + cardOffset;
					AddCardToPlayerDeck(playerIndex, rankCards[rankCardIndex]);
				}
			}

			for (int rankCardIndex = cardsPerPlayer * playerCount; rankCardIndex < rankCards.Count; rankCardIndex++)
			{
				remainingCards.Add(rankCards[rankCardIndex]);
			}
		}

		Shuffle(remainingCards);
		foreach (PlayingCard card in remainingCards)
		{
			AddCardToPlayerDeck(_rng.RandiRange(0, playerCount - 1), card);
		}

		return true;
	}

	private void ShufflePlayerDecks()
	{
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
		}
	}

	private void DrawStartingHands()
	{
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			int cardsToDraw = Math.Min(StartingHandCards, _playerDecks[playerIndex].Count);
			for (int cardIndex = 0; cardIndex < cardsToDraw; cardIndex++)
			{
				PlayingCard topCard = _playerDecks[playerIndex][0];
				_playerDecks[playerIndex].RemoveAt(0);
				_playerHands[playerIndex].Add(topCard);
			}
		}
	}

	private void AddCardToPlayerDeck(int playerIndex, PlayingCard card)
	{
		_playerDecks[playerIndex].Add(PrepareCardForPlayerDeck(card));
	}

	private PlayingCard PrepareCardForPlayerDeck(PlayingCard card)
	{
		if (ShouldPreserveCardEffectsWhenEnteringDeck())
		{
			return card;
		}

		return card.WithoutItemEffects();
	}

	private bool ShouldPreserveCardEffectsWhenEnteringDeck()
	{
		return true;
	}

	private void ApplyDebugOpponentOneCardHand()
	{
		const int opponentPlayerIndex = 1;
		if (!debug_opponent_has_one_card || player_count != 2 || _playerHands[opponentPlayerIndex].Count == 0)
		{
			return;
		}

		PlayingCard onlyCard = _playerHands[opponentPlayerIndex][0];
		_playerHands[opponentPlayerIndex].Clear();
		_playerHands[opponentPlayerIndex].Add(onlyCard);
		_playerDecks[opponentPlayerIndex].Clear();
	}

	private void ApplyDebugMainPlayerOneCardHand()
	{
		if (!debug_main_player_has_one_card || player_count != 2 || _playerHands[MainPlayerIndex].Count == 0)
		{
			return;
		}

		PlayingCard onlyCard = _playerHands[MainPlayerIndex][0];
		_playerHands[MainPlayerIndex].Clear();
		_playerHands[MainPlayerIndex].Add(onlyCard);
		_playerDecks[MainPlayerIndex].Clear();
	}

	private List<PlayingCard> LoadDeckCards()
	{
		var deck = new List<PlayingCard>();
		PackedScene? pokerCardsScene = GD.Load<PackedScene>(PokerCardsScenePath);
		if (pokerCardsScene == null)
		{
			GD.PushError($"Missing poker cards scene: {PokerCardsScenePath}");
			return deck;
		}

		Node pokerCards = pokerCardsScene.Instantiate();
		Node? standardCards = pokerCards.GetNodeOrNull<Node>(StandardCardsRootPath);
		if (standardCards == null)
		{
			GD.PushError($"Missing {StandardCardsRootPath} in {PokerCardsScenePath}.");
			pokerCards.Free();
			return deck;
		}

		for (int suitIndex = 0; suitIndex < standardCards.GetChildCount(); suitIndex++)
		{
			Node suitNode = standardCards.GetChild(suitIndex);
			string suit = suitNode.Name.ToString();
			if (suit == "Back" || suit == SuitedJqkBackupCardsNodeName)
			{
				continue;
			}

			for (int cardIndex = 0; cardIndex < suitNode.GetChildCount(); cardIndex++)
			{
				Node cardNode = suitNode.GetChild(cardIndex);
				if (cardNode is not Sprite2D cardSprite || cardSprite.Texture == null)
				{
					continue;
				}

				string cardName = cardSprite.Name.ToString();
				string rank = cardName.StartsWith(CardNamePrefix, StringComparison.Ordinal)
					? cardName[CardNamePrefix.Length..]
					: cardName;

				if (IsExcludedDeckRank(rank))
				{
					continue;
				}

				deck.Add(new PlayingCard(suit, rank, cardSprite.Texture));
			}
		}

		if (!item_mode)
		{
			AddSuitedJqkBackupCards(standardCards, deck);
		}

		pokerCards.Free();
		return deck;
	}

	private static void AddSuitedJqkBackupCards(Node standardCards, List<PlayingCard> deck)
	{
		Node? suitedJqkBackupCards = standardCards.GetNodeOrNull<Node>(SuitedJqkBackupCardsNodeName);
		if (suitedJqkBackupCards == null)
		{
			GD.PushWarning($"Missing {SuitedJqkBackupCardsNodeName} in {PokerCardsScenePath}.");
			return;
		}

		for (int cardIndex = 0; cardIndex < suitedJqkBackupCards.GetChildCount(); cardIndex++)
		{
			Node cardNode = suitedJqkBackupCards.GetChild(cardIndex);
			if (cardNode is not Sprite2D cardSprite || cardSprite.Texture == null)
			{
				continue;
			}

			if (!TryParseSuitedJqkBackupCardName(cardSprite.Name.ToString(), out string suit, out string rank))
			{
				GD.PushWarning($"Invalid suited JQK backup card name: {cardSprite.Name}.");
				continue;
			}

			deck.Add(new PlayingCard(suit, rank, cardSprite.Texture));
		}
	}

	private static bool TryParseSuitedJqkBackupCardName(string cardName, out string suit, out string rank)
	{
		string[] nameParts = cardName.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (nameParts.Length == 3
			&& string.Equals(nameParts[0], "Backup", StringComparison.Ordinal)
			&& CardRankCatalog.ShouldExcludeFromStandardDeck(nameParts[2]))
		{
			suit = nameParts[1];
			rank = nameParts[2];
			return true;
		}

		suit = string.Empty;
		rank = string.Empty;
		return false;
	}

	private List<PlayingCard> BuildDeckForPlayerCount(int playerCount)
	{
		List<PlayingCard> baseDeck = LoadDeckCards();
		if (playerCount <= 2)
		{
			return baseDeck;
		}

		var doubledDeck = new List<PlayingCard>(baseDeck.Count * 2);
		doubledDeck.AddRange(baseDeck);
		doubledDeck.AddRange(baseDeck);
		return doubledDeck;
	}

	private void Shuffle(List<PlayingCard> deck)
	{
		for (int index = deck.Count - 1; index > 0; index--)
		{
			int swapIndex = _rng.RandiRange(0, index);
			(deck[index], deck[swapIndex]) = (deck[swapIndex], deck[index]);
		}
	}

	private void Cut(List<PlayingCard> deck)
	{
		if (deck.Count < 2)
		{
			return;
		}

		int cutIndex = _rng.RandiRange(1, deck.Count - 1);
		deck.Reverse(0, cutIndex);
		deck.Reverse(cutIndex, deck.Count - cutIndex);
		deck.Reverse();
	}

	private DrawnHandCard? DrawReplacementCard(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return null;
		}

		if (_playerDecks[playerIndex].Count == 0
			|| _playerHands[playerIndex].Count >= StartingHandCards
			|| GetTotalHandCardCount(playerIndex) >= MaxTotalHandCards)
		{
			return null;
		}

		int handIndex = _playerHands[playerIndex].Count;
		PlayingCard drawnCard = _playerDecks[playerIndex][0];
		_playerHands[playerIndex].Add(drawnCard);
		_playerDecks[playerIndex].RemoveAt(0);
		return new DrawnHandCard(drawnCard, handIndex);
	}

	private DrawnHandCard? DrawCardFromDeckToHand(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count || playerIndex >= _playerHands.Count)
		{
			return null;
		}

		if (_playerDecks[playerIndex].Count == 0 || GetTotalHandCardCount(playerIndex) >= MaxTotalHandCards)
		{
			return null;
		}

		int handIndex = _playerHands[playerIndex].Count;
		PlayingCard drawnCard = _playerDecks[playerIndex][0];
		_playerHands[playerIndex].Add(drawnCard);
		_playerDecks[playerIndex].RemoveAt(0);
		return new DrawnHandCard(drawnCard, handIndex);
	}
}
