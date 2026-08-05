using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Deckland.Rules;
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

		ImmutableArray<PlayerStateSnapshot> players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				MainPlayerIndex,
				ImmutableArray.Create(ToRuleCardState(cards["Hearts:5"])),
				ImmutableArray.Create(
					ToRuleCardState(cards["Hearts:7"]),
					ToRuleCardState(cards["Hearts:2"])),
				ImmutableArray<LogicalItemState>.Empty,
				Score: 0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(ToRuleCardState(cards["Clubs:K"])),
				ImmutableArray<LogicalItemState>.Empty,
				Score: 0));
		ImmutableArray<TableCardState> tableCards = ImmutableArray.Create(
			ToRuleTableCardState(new TableCard(cards["Clubs:2"], true)),
			ToRuleTableCardState(new TableCard(cards["Diamonds:3"], true)),
			ToRuleTableCardState(new TableCard(cards["Spades:7"], true)),
			ToRuleTableCardState(new TableCard(cards["Clubs:A"], true)),
			ToRuleTableCardState(new TableCard(cards["Diamonds:4"], true)));
		if (!TryInitializeScriptedRuleMatch(players, tableCards))
		{
			return false;
		}

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

		ImmutableArray<PlayerStateSnapshot> players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				MainPlayerIndex,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(ToRuleCardState(cards["Hearts:4"])),
				ImmutableArray<LogicalItemState>.Empty,
				Score: 0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(ToRuleCardState(cards["Clubs:9"])),
				ImmutableArray<LogicalItemState>.Empty,
				Score: 0));
		ImmutableArray<TableCardState> tableCards = ImmutableArray.Create(
			ToRuleTableCardState(new TableCard(cards["Hearts:9"], true)),
			ToRuleTableCardState(new TableCard(cards["Diamonds:6"], true)),
			ToRuleTableCardState(new TableCard(cards["Clubs:4"], true)),
			ToRuleTableCardState(new TableCard(null, true, questionMark.Value)));
		if (!TryInitializeScriptedRuleMatch(players, tableCards))
		{
			return false;
		}

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

		private bool TryLoadAdventureTutorialCard(Node standardCards, string suit, string rank, out PlayingCard card)
	{
		card = default;
		string cardPath = $"{suit}/{CardNamePrefix}{rank}";
		if (standardCards.GetNodeOrNull<Sprite2D>(cardPath) is not Sprite2D cardSprite || cardSprite.Texture == null)
		{
			GD.PushError($"Missing tutorial card: {StandardCardsRootPath}/{cardPath}");
			return false;
		}

		card = CreatePlayingCardWithTexture(suit, rank, cardSprite.Texture);
		return true;
	}
}
