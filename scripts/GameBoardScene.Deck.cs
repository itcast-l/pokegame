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

		_openingVisibleHandCardCounts.Clear();
		_openingVisiblePileCardCounts.Clear();
		ResetAdventureBattleScopedStateForNewBattle();
		_debugQuestionRewardGrantCount = 0;
		_debugLastQuestionRewardItemCardId = string.Empty;
		_hiddenHandDisplaySlots.Clear();
		_pendingQuestionRewardItemHandSlots.Clear();
		_authorityRuleSession?.ClearCardTriggerDispatch();
		InvalidateRenderCaches();
		_settledAdventureDefeatResolution = null;
		ResetPlayerAvatarActions();
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
			_openingVisibleHandCardCounts.Add(0);
			_openingVisiblePileCardCounts.Add(0);
		}

		if (TrySetupAdventureTutorialDeal())
		{
			ReleasePendingItemSelectionPresentation();
			RefreshPlayerScorePresentation();
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
			StartAdventureTutorial();
			return;
		}

		if (TrySetupDebugRecordingDeal())
		{
			ReleasePendingItemSelectionPresentation();
			RefreshPlayerScorePresentation();
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
			return;
		}

		if (!TryInitializeRuleMatch(deck, playOpeningDealAnimation))
		{
			return;
		}
		_isOpeningDealAnimationPlaying = playOpeningDealAnimation;

		ReleasePendingItemSelectionPresentation();
		RefreshPlayerScorePresentation();
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		if (playOpeningDealAnimation)
		{
			if (playOpeningIntroAnimation)
			{
				TrackBoardTask(PlayOpeningIntroThenDealSequenceAsync(_turnSequenceGeneration), nameof(PlayOpeningIntroThenDealSequenceAsync));
			}
			else
			{
				TrackBoardTask(PlayOpeningDealAnimationAsync(_turnSequenceGeneration), nameof(PlayOpeningDealAnimationAsync));
			}
		}
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

				deck.Add(CreatePlayingCardWithTexture(suit, rank, cardSprite.Texture));
			}
		}

		if (!item_mode)
		{
			AddSuitedJqkBackupCards(standardCards, deck);
		}

		pokerCards.Free();
		return deck;
	}

	private void AddSuitedJqkBackupCards(Node standardCards, List<PlayingCard> deck)
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

			deck.Add(CreatePlayingCardWithTexture(suit, rank, cardSprite.Texture));
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
			foreach (PlayingCard card in baseDeck)
			{
				doubledDeck.Add(card.CloneWithNewRuntimeInstanceId());
			}
			return doubledDeck;
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
		if (!TrySubmitDrawCardsRuleCommand(
			playerIndex,
			drawCount: 1,
			MaxTotalHandCards,
			out _))
		{
			return null;
		}
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
		if (!TrySubmitDrawCardsRuleCommand(
			playerIndex,
			drawCount: 1,
			MaxTotalHandCards,
			out _))
		{
			return null;
		}
		return new DrawnHandCard(drawnCard, handIndex);
	}
}
