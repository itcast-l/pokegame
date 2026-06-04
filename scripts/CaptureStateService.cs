using System.Collections.Generic;

internal sealed class CaptureStateService
{
	internal CaptureStatePlan CreatePlan(
		GameBoardState state,
		int captureStartIndex,
		HashSet<int>? extraTableCardIndexes)
	{
		if (captureStartIndex < 0 || captureStartIndex >= state.TableCards.Count)
		{
			return CaptureStatePlan.Invalid;
		}

		var baseTableCardIndexes = new List<int>();
		for (int tableCardIndex = captureStartIndex; tableCardIndex < state.TableCards.Count; tableCardIndex++)
		{
			if (!state.TableCards[tableCardIndex].IsBuriedInSand)
			{
				baseTableCardIndexes.Add(tableCardIndex);
			}
		}

		List<int> extraCaptureIndexes = BuildValidExtraCaptureIndexes(state, captureStartIndex, extraTableCardIndexes);
		return new CaptureStatePlan(
			true,
			captureStartIndex,
			state.TableCards.Count - captureStartIndex,
			baseTableCardIndexes,
			extraCaptureIndexes);
	}

	internal CaptureStateResult ApplyCapture(
		GameBoardState state,
		CaptureStatePlan plan,
		int playerIndex,
		bool preserveCardEffectsWhenEnteringDeck)
	{
		if (!plan.IsValid || playerIndex < 0 || playerIndex >= state.PlayerDecks.Count)
		{
			return default;
		}

		int collectedCardCount = 0;
		int removedTableCardCount = 0;
		foreach (int tableCardIndex in plan.BaseTableCardIndexes)
		{
			TableCard tableCard = state.TableCards[tableCardIndex];
			removedTableCardCount++;
			if (tableCard.Card.HasValue && !tableCard.RemoveCardOnCapture)
			{
				AddCardToPlayerDeck(state, playerIndex, tableCard.Card.Value, preserveCardEffectsWhenEnteringDeck);
				collectedCardCount++;
			}
		}

		foreach (int tableCardIndex in plan.ExtraTableCardIndexes)
		{
			TableCard tableCard = state.TableCards[tableCardIndex];
			removedTableCardCount++;
			if (tableCard.Card.HasValue && !tableCard.RemoveCardOnCapture)
			{
				AddCardToPlayerDeck(state, playerIndex, tableCard.Card.Value, preserveCardEffectsWhenEnteringDeck);
				collectedCardCount++;
			}
		}

		for (int tableCardIndex = state.TableCards.Count - 1; tableCardIndex >= plan.CaptureStartIndex; tableCardIndex--)
		{
			if (!state.TableCards[tableCardIndex].IsBuriedInSand)
			{
				state.TableCards.RemoveAt(tableCardIndex);
			}
		}

		for (int index = plan.ExtraTableCardIndexes.Count - 1; index >= 0; index--)
		{
			state.TableCards.RemoveAt(plan.ExtraTableCardIndexes[index]);
		}

		return new CaptureStateResult(collectedCardCount, removedTableCardCount);
	}

	private static List<int> BuildValidExtraCaptureIndexes(
		GameBoardState state,
		int captureStartIndex,
		HashSet<int>? extraTableCardIndexes)
	{
		var validIndexes = new List<int>();
		if (extraTableCardIndexes == null)
		{
			return validIndexes;
		}

		foreach (int tableCardIndex in extraTableCardIndexes)
		{
			if (tableCardIndex < 0 || tableCardIndex >= captureStartIndex || tableCardIndex >= state.TableCards.Count)
			{
				continue;
			}

			TableCard tableCard = state.TableCards[tableCardIndex];
			if (!tableCard.Card.HasValue || tableCard.IsBuriedInSand)
			{
				continue;
			}

			validIndexes.Add(tableCardIndex);
		}

		validIndexes.Sort();
		return validIndexes;
	}

	private static void AddCardToPlayerDeck(
		GameBoardState state,
		int playerIndex,
		PlayingCard card,
		bool preserveCardEffectsWhenEnteringDeck)
	{
		state.PlayerDecks[playerIndex].Add(
			preserveCardEffectsWhenEnteringDeck ? card : card.WithoutItemEffects());
	}
}

internal readonly record struct CaptureStatePlan(
	bool IsValid,
	int CaptureStartIndex,
	int CaptureCount,
	IReadOnlyList<int> BaseTableCardIndexes,
	IReadOnlyList<int> ExtraTableCardIndexes)
{
	public static CaptureStatePlan Invalid => new(false, -1, 0, System.Array.Empty<int>(), System.Array.Empty<int>());
}

internal readonly record struct CaptureStateResult(int CollectedCardCount, int RemovedTableCardCount);
