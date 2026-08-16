using System.Collections.Generic;
using System.Collections.Immutable;
using Deckland.Rules;

public partial class GameBoardScene
{
	private int GetCapturedTablePoints(int captureStartIndex, int scorePlayerIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		return MatchReadModel.GetCapturedScore(
			ToCaptureScoringTableCards(),
			captureStartIndex,
			scorePlayerIndex,
			BuildCaptureScoringContext(scorePlayerIndex),
			extraTableCardIndexes).Points;
	}

	private int GetCapturedTableCardScoreValueForPlayer(TableCard tableCard, int scorePlayerIndex, bool hasDoubleEffect)
	{
		return MatchReadModel.GetCapturedTableCardScoreValue(
			ToCaptureScoringTableCard(tableCard),
			BuildCaptureScoringContext(scorePlayerIndex),
			hasDoubleEffect);
	}

	private CaptureScoringContext BuildCaptureScoringContext(int scorePlayerIndex)
	{
		bool applyScoreReduction = ShouldApplyCrabScoreReduction(scorePlayerIndex);
		return new CaptureScoringContext(
			SuppressSlimeCoatedScore: ShouldSuppressSlimeCoatedCapturedScore(scorePlayerIndex),
			ApplyScoreReductionPerCard: applyScoreReduction,
			ScoreReductionPerCard: applyScoreReduction
				? GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.ScoreReductionPerCard, 2)
				: 0);
	}

	private bool ShouldSuppressSlimeCoatedCapturedScore(int scorePlayerIndex)
	{
		return adventure_mode
			&& scorePlayerIndex == MainPlayerIndex
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.ColorCurrent);
	}

	private bool IsSeaSlugSlimeScoreSuppressed(TableCard tableCard, int scorePlayerIndex)
	{
		return MatchReadModel.IsSlimeScoreSuppressed(
			tableCard.IsSlimeCoated,
			BuildCaptureScoringContext(scorePlayerIndex));
	}

	private bool HasCapturedDoubleScoreEffect(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		return MatchReadModel.HasCapturedDoubleScoreEffect(
			ToCaptureScoringTableCards(),
			captureStartIndex,
			extraTableCardIndexes);
	}

	private int GetCapturedTableScorePlayerIndex(int captureStartIndex, int fallbackPlayerIndex)
	{
		return MatchReadModel.GetCapturedScorePlayerIndex(
			ToCaptureScoringTableCards(),
			captureStartIndex,
			fallbackPlayerIndex,
			player_count);
	}

	private bool IsCapturedTableCardScoringIndex(int tableCardIndex, int captureStartIndex)
	{
		return MatchReadModel.IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex, _tableCards.Count);
	}

	private bool HasCapturedScoringTableCards(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		return MatchReadModel.HasCapturedScoringTableCards(
			ToCaptureScoringTableCards(),
			captureStartIndex,
			extraTableCardIndexes);
	}

	private int BuildAdventureCaptureDamageCardPointLists(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		int scorePlayerIndex,
		IReadOnlySet<int>? extraTableCardIndexes,
		List<int> rawPoints,
		List<int> resolvedPoints,
		List<int> tableIndexes,
		bool includeCaptureStartInScoring = false,
		bool includeCaptureEndInScoring = false,
		int excludedTableCardIndex = -1)
	{
		rawPoints.Clear();
		resolvedPoints.Clear();
		tableIndexes.Clear();
		if (captureStartIndex < 0 || captureStartIndex >= tableCards.Count)
		{
			return 0;
		}

		bool hasDoubleEffect = MatchReadModel.HasCapturedDoubleScoreEffect(
			tableCards,
			captureStartIndex,
			extraTableCardIndexes);
		CaptureScoringContext context = BuildCaptureScoringContext(scorePlayerIndex);
		var addedIndexes = new HashSet<int>();

		int TryAddCardPointValues(int tableCardIndex, bool allowAnyVisibleCapturedCard)
		{
			if (tableCardIndex < 0
				|| tableCardIndex >= tableCards.Count
				|| tableCardIndex == excludedTableCardIndex)
			{
				return 0;
			}

			CaptureScoringTableCard tableCard = tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand || !tableCard.Card.HasValue)
			{
				return 0;
			}

			if (!allowAnyVisibleCapturedCard
				&& !MatchReadModel.IsCapturedTableCardScoringIndex(
					tableCardIndex,
					captureStartIndex,
					tableCards.Count))
			{
				return 0;
			}

			int rawPoint = Math.Max(0, tableCard.Card.Value.ScoreValue);
			if (rawPoint <= 0 || !addedIndexes.Add(tableCardIndex))
			{
				return 0;
			}

			int resolvedPoint = MatchReadModel.GetCapturedTableCardScoreValue(
				tableCard,
				context,
				hasDoubleEffect);
			rawPoints.Add(rawPoint);
			resolvedPoints.Add(resolvedPoint);
			tableIndexes.Add(tableCardIndex);
			return resolvedPoint;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			TryAddCardPointValues(tableCardIndex, allowAnyVisibleCapturedCard: false);
		}

		if (extraTableCardIndexes != null)
		{
			var sortedExtraTableCardIndexes = new List<int>(
				extraTableCardIndexes);
			sortedExtraTableCardIndexes.Sort();
			foreach (int tableCardIndex in sortedExtraTableCardIndexes)
			{
				TryAddCardPointValues(tableCardIndex, allowAnyVisibleCapturedCard: true);
			}
		}

		if (!includeCaptureStartInScoring
			&& !includeCaptureEndInScoring)
		{
			return 0;
		}

		int captureTailTableCardIndex = -1;
		for (int tableCardIndex = tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			if (tableCardIndex != excludedTableCardIndex
				&& !tableCards[tableCardIndex].IsBuriedInSand)
			{
				captureTailTableCardIndex = tableCardIndex;
				break;
			}
		}

		int endpointResolvedPoints = includeCaptureStartInScoring
			? TryAddCardPointValues(
				captureStartIndex,
				allowAnyVisibleCapturedCard: true)
			: 0;
		if (includeCaptureEndInScoring)
		{
			endpointResolvedPoints += TryAddCardPointValues(
				captureTailTableCardIndex,
				allowAnyVisibleCapturedCard: true);
		}
		return endpointResolvedPoints;
	}

	private int ResolveBlackLedgerRedirectTableCardIndex(
		int playerIndex,
		int captureStartIndex)
	{
		var scoreValues = new int[_tableCards.Count];
		for (int tableCardIndex = 0;
			tableCardIndex < _tableCards.Count;
			tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			scoreValues[tableCardIndex] = !tableCard.IsBuriedInSand
				&& !tableCard.RemoveCardOnCapture
				&& tableCard.Card.HasValue
				? Math.Max(0, GetCardScoreValue(tableCard.Card.Value))
				: 0;
		}

		return GetAdventureUpgradeRunState()
			?.ResolveBlackLedgerRedirectTableCardIndex(
				playerIndex == MainPlayerIndex,
				scoreValues,
				captureStartIndex) ?? -1;
	}

	private TableCard? FindRightmostRemainingOldBaseTableCard(IReadOnlyCollection<TableCard> prePlayTableCards)
	{
		if (prePlayTableCards.Count == 0)
		{
			return null;
		}

		var oldTableCards = new HashSet<TableCard>(prePlayTableCards);
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (oldTableCards.Contains(tableCard) && !tableCard.IsBuriedInSand && tableCard.Card.HasValue)
			{
				return tableCard;
			}
		}

		return null;
	}

	private TableCard? FindLeftmostCurrentBaseTableCard()
	{
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue)
			{
				return tableCard;
			}
		}

		return null;
	}

	private bool CapturedOnlyOuterVisibleCards(
		IReadOnlyCollection<TableCard> prePlayTableCards,
		int captureStartIndex,
		HashSet<int>? extraTableCardIndexes = null)
	{
		if (prePlayTableCards.Count == 0)
		{
			return false;
		}

		var oldTableCards = new HashSet<TableCard>(prePlayTableCards);
		int visibleCapturedCount = 0;
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!oldTableCards.Contains(tableCard) || tableCard.IsBuriedInSand)
			{
				continue;
			}

			if (tableCard.IsFaceUp)
			{
				visibleCapturedCount++;
			}
		}

		if (extraTableCardIndexes != null)
		{
			foreach (int tableCardIndex in extraTableCardIndexes)
			{
				if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
				{
					continue;
				}

				TableCard tableCard = _tableCards[tableCardIndex];
				if (oldTableCards.Contains(tableCard) && !tableCard.IsBuriedInSand && tableCard.IsFaceUp)
				{
					visibleCapturedCount++;
				}
			}
		}

		return visibleCapturedCount == 2;
	}

	private CaptureScoringTableCard[] ToCaptureScoringTableCards()
	{
		return ToCaptureScoringTableCards(_tableCards);
	}

	private CaptureScoringTableCard[] ToCaptureScoringTableCards(IReadOnlyList<TableCard> sourceTableCards)
	{
		var tableCards = new CaptureScoringTableCard[sourceTableCards.Count];
		for (int index = 0; index < sourceTableCards.Count; index++)
		{
			tableCards[index] = ToCaptureScoringTableCard(sourceTableCards[index]);
		}

		return tableCards;
	}

	private CaptureScoringTableCard[] ToCaptureScoringTableCards(
		ImmutableArray<TableCardState> sourceTableCards)
	{
		var tableCards = new CaptureScoringTableCard[sourceTableCards.Length];
		for (int index = 0; index < sourceTableCards.Length; index++)
		{
			tableCards[index] = ToCaptureScoringTableCard(sourceTableCards[index]);
		}

		return tableCards;
	}

	private CaptureScoringTableCard ToCaptureScoringTableCard(TableCard tableCard)
	{
		CaptureScoringCard? card = tableCard.Card.HasValue
			? ToCaptureScoringCard(tableCard.Card.Value)
			: null;

		return new CaptureScoringTableCard(
			card,
			tableCard.IsBuriedInSand,
			tableCard.IsSlimeCoated,
			IsItemEffectType(tableCard, CardEffectTypes.DoubleCollect),
			IsItemEffectType(tableCard, CardEffectTypes.PlaceTablePairDestroy),
			tableCard.ItemOwnerPlayerIndex,
			tableCard.Card.HasValue && !tableCard.RemoveCardOnCapture);
	}

	private CaptureScoringTableCard ToCaptureScoringTableCard(TableCardState tableCard)
	{
		CaptureScoringCard? card = tableCard.Card.HasValue
			? ToCaptureScoringCard(tableCard.Card.Value)
			: null;

		return new CaptureScoringTableCard(
			card,
			tableCard.IsBuried,
			tableCard.IsScoreSuppressed,
			tableCard.ItemOverlay.HasValue
				&& IsItemEffectType(tableCard.ItemOverlay.Value.Id, CardEffectTypes.DoubleCollect),
			tableCard.ItemOverlay.HasValue
				&& IsItemEffectType(tableCard.ItemOverlay.Value.Id, CardEffectTypes.PlaceTablePairDestroy),
			tableCard.ItemOwnerSeat,
			tableCard.Card.HasValue && !tableCard.RemoveOnCapture);
	}

	private CaptureScoringCard ToCaptureScoringCard(PlayingCard card)
	{
		return new CaptureScoringCard(
			card.Rank,
			GetCardScoreValue(card),
			IsAttachedEffectType(card, CardEffectTypes.DoubleCollect),
			card.Suit);
	}

	private CaptureScoringCard ToCaptureScoringCard(LogicalCardState card)
	{
		PlayingCard restoredCard = FromRuleCardState(card);
		return ToCaptureScoringCard(restoredCard);
	}
}
