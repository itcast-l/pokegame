using System.Collections.Generic;

internal readonly record struct CardPlayStartResult(
	bool Played,
	PlayingCard PlayedCard,
	CardPlayPlan Plan,
	int CaptureStartIndex,
	int PlayedTableCardIndex,
	bool MatchedTableCardFlipped,
	bool PlayedRankCollectsTable,
	bool PlayedTableCardIsFaceUp)
{
	public static CardPlayStartResult NotPlayed => new(false, default, default, -1, -1, false, false, false);
}

internal sealed class CardPlayStateService
{
	internal CardPlayStartResult TryStartPlayFromHand(
		GameBoardState state,
		int playerIndex,
		int handIndex,
		PlayingCard playedCard,
		CardPlayPlan plan)
	{
		if (playerIndex < 0 || playerIndex >= state.PlayerHands.Count)
		{
			return CardPlayStartResult.NotPlayed;
		}

		List<PlayingCard> hand = state.PlayerHands[playerIndex];
		if (handIndex < 0 || handIndex >= hand.Count)
		{
			return CardPlayStartResult.NotPlayed;
		}

		hand.RemoveAt(handIndex);
		int captureStartIndex = AddPlayedCardToTable(
			state,
			playerIndex,
			playedCard,
			plan,
			out bool matchedTableCardFlipped);
		int playedTableCardIndex = state.TableCards.Count - 1;

		bool playedRankCollectsTable = plan.PlayedCardCollectsTable;
		if (playedRankCollectsTable)
		{
			captureStartIndex = 0;
			state.TableCards[playedTableCardIndex].IsFaceUp = true;
		}

		bool playedTableCardIsFaceUp = state.TableCards[playedTableCardIndex].IsFaceUp;
		return new CardPlayStartResult(
			true,
			playedCard,
			plan,
			captureStartIndex,
			playedTableCardIndex,
			matchedTableCardFlipped,
			playedRankCollectsTable,
			playedTableCardIsFaceUp);
	}

	private static int AddPlayedCardToTable(
		GameBoardState state,
		int playerIndex,
		PlayingCard playedCard,
		CardPlayPlan plan,
		out bool matchedTableCardFlipped)
	{
		bool isFaceUp = plan.MatchingTableCardIndex >= 0;
		matchedTableCardFlipped = plan.MatchedTableCardFlipped;
		if (isFaceUp)
		{
			state.TableCards[plan.MatchingTableCardIndex].IsFaceUp = true;
		}

		state.TableCards.Add(new TableCard(playedCard, isFaceUp, cardOwnerPlayerIndex: playerIndex));
		return plan.CaptureStartIndex;
	}
}
