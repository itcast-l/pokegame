using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private bool TrySetupAdventureTutorialDeal()
	{
		if (!IsAdventureTutorialEnabled())
		{
			return false;
		}

		if (player_count != 2)
		{
			GD.PushWarning("Adventure tutorial setup failed; falling back to normal deal.");
			return false;
		}

		bool setup = IsBombItemTutorialFlow()
			? TrySetupAdventureBombItemTutorialDeal()
			: TrySetupAdventureBasicCaptureTutorialDeal();
		if (!setup)
		{
			GD.PushWarning("Adventure tutorial setup failed; falling back to normal deal.");
		}

		return setup;
	}

	private bool TrySetupAdventureBasicCaptureTutorialDeal()
	{
		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "7"),
			("Hearts", "2"),
			("Hearts", "5"),
			("Clubs", "K"),
			("Clubs", "2"),
			("Diamonds", "3"),
			("Spades", "7"),
			("Clubs", "A"),
			("Diamonds", "4"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:7"]);
		_playerHands[MainPlayerIndex].Add(cards["Hearts:2"]);
		_playerDecks[MainPlayerIndex].Add(cards["Hearts:5"]);
		_playerHands[1].Add(cards["Clubs:K"]);

		_tableCards.Add(new TableCard(cards["Clubs:2"], true));
		_tableCards.Add(new TableCard(cards["Diamonds:3"], true));
		_tableCards.Add(new TableCard(cards["Spades:7"], true));
		_tableCards.Add(new TableCard(cards["Clubs:A"], true));
		_tableCards.Add(new TableCard(cards["Diamonds:4"], true));

		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		return true;
	}

	private bool TrySetupAdventureBombItemTutorialDeal()
	{
		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "4"),
			("Hearts", "9"),
			("Clubs", "4"),
			("Clubs", "9"),
			("Diamonds", "6"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		ItemCard? questionMark = CreateItemCard(QuestionMarkItemCardId);
		if (!questionMark.HasValue)
		{
			return false;
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:4"]);
		_playerHands[1].Add(cards["Clubs:9"]);

		_tableCards.Add(new TableCard(cards["Hearts:9"], true));
		_tableCards.Add(new TableCard(cards["Diamonds:6"], true));
		_tableCards.Add(new TableCard(cards["Clubs:4"], true));
		_tableCards.Add(new TableCard(null, true, questionMark.Value));

		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		return true;
	}

	private bool TryLoadAdventureTutorialCards(
		(string Suit, string Rank)[] requiredCards,
		out Dictionary<string, PlayingCard> cards)
	{
		cards = new Dictionary<string, PlayingCard>(StringComparer.Ordinal);
		PackedScene? pokerCardsScene = GD.Load<PackedScene>(PokerCardsScenePath);
		if (pokerCardsScene == null)
		{
			GD.PushError($"Missing poker cards scene: {PokerCardsScenePath}");
			return false;
		}

		Node pokerCards = pokerCardsScene.Instantiate();
		Node? standardCards = pokerCards.GetNodeOrNull<Node>(StandardCardsRootPath);
		if (standardCards == null)
		{
			GD.PushError($"Missing {StandardCardsRootPath} in {PokerCardsScenePath}.");
			pokerCards.Free();
			return false;
		}

		foreach ((string suit, string rank) in requiredCards)
		{
			if (!TryLoadAdventureTutorialCard(standardCards, suit, rank, out PlayingCard card))
			{
				pokerCards.Free();
				return false;
			}

			cards[$"{suit}:{rank}"] = card;
		}

		pokerCards.Free();
		return true;
	}

	private static bool TryLoadAdventureTutorialCard(Node standardCards, string suit, string rank, out PlayingCard card)
	{
		card = default;
		string cardPath = $"{suit}/{CardNamePrefix}{rank}";
		if (standardCards.GetNodeOrNull<Sprite2D>(cardPath) is not Sprite2D cardSprite || cardSprite.Texture == null)
		{
			GD.PushError($"Missing tutorial card: {StandardCardsRootPath}/{cardPath}");
			return false;
		}

		card = new PlayingCard(suit, rank, cardSprite.Texture);
		return true;
	}
}
