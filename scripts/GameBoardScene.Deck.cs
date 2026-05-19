using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private void DealCardsToPlayers()
	{
		if (_playerCardPiles.Count != MaxPlayers || _playerHandAreas.Count != MaxPlayers)
		{
			return;
		}

		List<PlayingCard> deck = BuildDeckForPlayerCount(player_count);
		Shuffle(deck);
		Cut(deck);

		_playerDecks.Clear();
		_playerHands.Clear();
		_playerItemHands.Clear();
		_tableCards.Clear();
		_turnSequenceGeneration++;
		_currentTurnPlayerIndex = MainPlayerIndex;
		_completedTurnsSinceQuestionCardRefresh = 0;
		_questionCardRefreshAnchorPlayerIndex = -1;
		_pendingMagnetAttachmentRewardPlayers.Clear();
		ClearSelectionOverlay();
		ClearPendingItemSelectionState();
		_isResolvingMagnetAttachmentSelection = false;
		_isResolvingTurnSequence = false;
		_isProcessingCardPlay = false;
		_winnerPlayerIndex = -1;
		ResetPlayerScores();
		if (_winnerLabel != null)
		{
			_winnerLabel.Visible = false;
		}
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
			_playerItemHands.Add(new List<ItemCard>());
		}

		for (int cardIndex = 0; cardIndex < deck.Count; cardIndex++)
		{
			int playerIndex = cardIndex % player_count;
			AddCardToPlayerDeck(playerIndex, deck[cardIndex]);
		}

		DrawStartingHands();
		ApplyDebugOpponentOneCardHand();
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
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

		return card.WithoutMagnetEffect();
	}

	private bool ShouldPreserveCardEffectsWhenEnteringDeck()
	{
		return false;
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

	private List<PlayingCard> LoadDeckWithoutFaceCards()
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

		foreach (Node suitNode in standardCards.GetChildren())
		{
			string suit = suitNode.Name.ToString();
			if (suit == "Back" || suit == SuitedJqkBackupCardsNodeName)
			{
				continue;
			}

			foreach (Node cardNode in suitNode.GetChildren())
			{
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

		pokerCards.Free();
		return deck;
	}

	private List<PlayingCard> BuildDeckForPlayerCount(int playerCount)
	{
		List<PlayingCard> baseDeck = LoadDeckWithoutFaceCards();
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
		List<PlayingCard> topCards = deck.GetRange(0, cutIndex);
		deck.RemoveRange(0, cutIndex);
		deck.AddRange(topCards);
	}

	private void DrawReplacementCard(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return;
		}

		if (_playerDecks[playerIndex].Count == 0)
		{
			return;
		}

		_playerHands[playerIndex].Add(_playerDecks[playerIndex][0]);
		_playerDecks[playerIndex].RemoveAt(0);
	}
}
