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

		if (!TryRemoveCardFromHand(state, playerIndex, handIndex, out PlayingCard removedCard))
		{
			return CardPlayStartResult.NotPlayed;
		}

		return CompletePlayFromRemovedHandCard(state, playerIndex, removedCard, plan);
	}

	internal bool TryRemoveCardFromHand(
		GameBoardState state,
		int playerIndex,
		int handIndex,
		out PlayingCard playedCard)
	{
		playedCard = default;
		if (playerIndex < 0 || playerIndex >= state.PlayerHands.Count)
		{
			return false;
		}

		List<PlayingCard> hand = state.PlayerHands[playerIndex];
		if (handIndex < 0 || handIndex >= hand.Count)
		{
			return false;
		}

		playedCard = hand[handIndex];
		hand.RemoveAt(handIndex);
		return true;
	}

	internal CardPlayStartResult CompletePlayFromRemovedHandCard(
		GameBoardState state,
		int playerIndex,
		PlayingCard playedCard,
		CardPlayPlan plan)
	{
		int captureStartIndex = AddPlayedCardToTable(
			state,
			playerIndex,
			playedCard,
			plan,
			out bool matchedTableCardFlipped);
		int playedTableCardIndex = plan.TableInsertIndex;

		bool playedRankCollectsTable = plan.PlayedCardCollectsTable;
		if (playedRankCollectsTable)
		{
			captureStartIndex = 0;
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
		matchedTableCardFlipped = plan.MatchedTableCardFlipped;
		int insertIndex = plan.TableInsertIndex < 0
			? state.TableCards.Count
			: System.Math.Min(plan.TableInsertIndex, state.TableCards.Count);
		state.TableCards.Insert(insertIndex, new TableCard(playedCard, isFaceUp: plan.PlayedCardFacesUp, cardOwnerPlayerIndex: playerIndex));
		return plan.CaptureStartIndex;
	}
}
